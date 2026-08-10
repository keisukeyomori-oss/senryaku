"""Generate 60 fps pixel-motion atlases from the four pilot 4x4 atlases.

The interpolator moves opaque source pixels toward palette-compatible target
pixels and splats them at sub-pixel positions.  It therefore changes the
silhouette between key poses instead of cross-fading two complete drawings.
Only Pillow and NumPy are required so the output can be reproduced offline.
"""

from __future__ import annotations

from collections import deque
from pathlib import Path
import sys

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "Assets/Resources/Art/Pixel/Characters"
OUTPUT_DIR = SOURCE_DIR / "Motion60"
UNIT_IDS = (
    "hero", "partner", "azuki", "memory1", "memory2", "memory3",
    "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
    "e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss",
)

SOURCE_COLUMNS = 4
SOURCE_ROWS = 4
SOURCE_CELL = 128
MOTION_COLUMNS = 15
MOTION_FPS = 60
RUN_FRAMES = 20
POSE_FRAMES = 60
LEFT_FACING_ACTION_SOURCE_IDS = {
    "e_cavalry", "e_archer", "e_flier", "e_cleric"
}
def remove_edge_intrusions(frame: np.ndarray) -> tuple[np.ndarray, int]:
    """Remove disconnected pieces leaked in from an adjacent atlas cell."""
    mask = frame[..., 3] >= 12
    visited = np.zeros(mask.shape, dtype=bool)
    components: list[list[tuple[int, int]]] = []
    neighbours = ((-1, 0), (1, 0), (0, -1), (0, 1),
                  (-1, -1), (-1, 1), (1, -1), (1, 1))
    for start_y, start_x in zip(*np.nonzero(mask)):
        if visited[start_y, start_x]:
            continue
        component: list[tuple[int, int]] = []
        queue: deque[tuple[int, int]] = deque([(int(start_y), int(start_x))])
        visited[start_y, start_x] = True
        while queue:
            y, x = queue.popleft()
            component.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if (0 <= ny < SOURCE_CELL and 0 <= nx < SOURCE_CELL and
                        mask[ny, nx] and not visited[ny, nx]):
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        components.append(component)

    if not components:
        return frame.copy(), 0
    main = max(components, key=len)
    main_xs = [x for _, x in main]
    main_ys = [y for y, _ in main]
    main_min_x = min(main_xs)
    main_max_x = max(main_xs)
    main_min_y = min(main_ys)
    main_max_y = max(main_ys)
    edge_band = SOURCE_CELL // 5
    cleaned = frame.copy()
    removed = 0
    for component in components:
        if component is main or len(component) > 512:
            continue
        xs = [x for _, x in component]
        ys = [y for y, _ in component]
        left_intrusion = max(xs) < edge_band and max(xs) < main_min_x - 6
        right_intrusion = min(xs) >= SOURCE_CELL - edge_band and min(xs) > main_max_x + 6
        top_intrusion = max(ys) < edge_band and max(ys) < main_min_y - 6
        bottom_intrusion = min(ys) >= SOURCE_CELL - edge_band and min(ys) > main_max_y + 6
        if not (left_intrusion or right_intrusion or top_intrusion or bottom_intrusion):
            continue
        for y, x in component:
            cleaned[y, x] = 0
        removed += len(component)
    return cleaned, removed


def keep_largest_silhouette(frame: np.ndarray) -> tuple[np.ndarray, int]:
    """Keep one connected actor silhouette for stable battle-idle frames."""
    mask = frame[..., 3] >= 12
    visited = np.zeros(mask.shape, dtype=bool)
    components: list[list[tuple[int, int]]] = []
    neighbours = ((-1, 0), (1, 0), (0, -1), (0, 1),
                  (-1, -1), (-1, 1), (1, -1), (1, 1))
    for start_y, start_x in zip(*np.nonzero(mask)):
        if visited[start_y, start_x]:
            continue
        component: list[tuple[int, int]] = []
        queue: deque[tuple[int, int]] = deque([(int(start_y), int(start_x))])
        visited[start_y, start_x] = True
        while queue:
            y, x = queue.popleft()
            component.append((y, x))
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if (0 <= ny < SOURCE_CELL and 0 <= nx < SOURCE_CELL and
                        mask[ny, nx] and not visited[ny, nx]):
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        components.append(component)
    if not components:
        return frame.copy(), 0
    main = max(components, key=len)
    keep = np.zeros(mask.shape, dtype=bool)
    for y, x in main:
        keep[y, x] = True
    cleaned = frame.copy()
    removed_mask = mask & ~keep
    cleaned[removed_mask] = 0
    return cleaned, int(removed_mask.sum())


def split_source(atlas: Image.Image) -> list[np.ndarray]:
    rgba = np.asarray(atlas.convert("RGBA"), dtype=np.uint8)
    frames: list[np.ndarray] = []
    for row in range(SOURCE_ROWS):
        for column in range(SOURCE_COLUMNS):
            y0 = row * SOURCE_CELL
            x0 = column * SOURCE_CELL
            frames.append(rgba[y0:y0 + SOURCE_CELL, x0:x0 + SOURCE_CELL].copy())
    return frames


def palette_bucket(rgba: np.ndarray) -> np.ndarray:
    rgb = rgba[..., :3].astype(np.int16)
    high = rgb.max(axis=2)
    low = rgb.min(axis=2)
    value = high
    bucket = np.argmax(rgb, axis=2).astype(np.int16) + 2
    bucket[value < 58] = 0
    bucket[(value > 178) & ((high - low) < 44)] = 1
    bucket[rgba[..., 3] < 12] = -1
    return bucket


def centroid(mask: np.ndarray) -> np.ndarray:
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return np.array([SOURCE_CELL / 2, SOURCE_CELL / 2], dtype=np.float32)
    return np.array([xs.mean(), ys.mean()], dtype=np.float32)


def nearest_coordinates(mask: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Return the closest true pixel for every cell using an eight-way BFS."""
    height, width = mask.shape
    nearest_y = np.full((height, width), -1, dtype=np.int16)
    nearest_x = np.full((height, width), -1, dtype=np.int16)
    queue: deque[tuple[int, int]] = deque()
    for y, x in zip(*np.nonzero(mask)):
        nearest_y[y, x] = y
        nearest_x[y, x] = x
        queue.append((y, x))
    if not queue:
        return nearest_y, nearest_x
    neighbours = ((-1, 0), (1, 0), (0, -1), (0, 1),
                  (-1, -1), (-1, 1), (1, -1), (1, 1))
    while queue:
        y, x = queue.popleft()
        for dy, dx in neighbours:
            ny, nx = y + dy, x + dx
            if 0 <= ny < height and 0 <= nx < width and nearest_y[ny, nx] < 0:
                nearest_y[ny, nx] = nearest_y[y, x]
                nearest_x[ny, nx] = nearest_x[y, x]
                queue.append((ny, nx))
    return nearest_y, nearest_x


def correspondence(source: np.ndarray, target: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    source_mask = source[..., 3] >= 12
    target_mask = target[..., 3] >= 12
    source_bucket = palette_bucket(source)
    target_bucket = palette_bucket(target)
    shift = centroid(target_mask) - centroid(source_mask)
    target_maps: dict[int, tuple[np.ndarray, np.ndarray]] = {}
    for key in range(5):
        bucket_mask = target_mask & (target_bucket == key)
        if not bucket_mask.any():
            bucket_mask = target_mask
        target_maps[key] = nearest_coordinates(bucket_mask)
    fallback = nearest_coordinates(target_mask)
    destination_y = np.zeros(source_mask.shape, dtype=np.float32)
    destination_x = np.zeros(source_mask.shape, dtype=np.float32)
    ys, xs = np.nonzero(source_mask)
    for y, x in zip(ys, xs):
        predicted_x = int(np.clip(round(x + shift[0]), 0, SOURCE_CELL - 1))
        predicted_y = int(np.clip(round(y + shift[1]), 0, SOURCE_CELL - 1))
        key = int(source_bucket[y, x])
        nearest_y, nearest_x = target_maps.get(key, fallback)
        dy = int(nearest_y[predicted_y, predicted_x])
        dx = int(nearest_x[predicted_y, predicted_x])
        if dy < 0:
            dy = int(fallback[0][predicted_y, predicted_x])
            dx = int(fallback[1][predicted_y, predicted_x])
        destination_y[y, x] = dy
        destination_x[y, x] = dx
    return destination_y, destination_x


def splat(
    source: np.ndarray,
    destination_y: np.ndarray,
    destination_x: np.ndarray,
    t: float,
) -> tuple[np.ndarray, np.ndarray]:
    height, width = source.shape[:2]
    accum = np.zeros((height, width, 4), dtype=np.float32)
    weight = np.zeros((height, width), dtype=np.float32)
    ys, xs = np.nonzero(source[..., 3] >= 12)
    fy = ys + (destination_y[ys, xs] - ys) * t
    fx = xs + (destination_x[ys, xs] - xs) * t
    y0 = np.floor(fy).astype(np.int32)
    x0 = np.floor(fx).astype(np.int32)
    colors = source[ys, xs].astype(np.float32)
    fractions_y = fy - y0
    fractions_x = fx - x0
    for oy, wy in ((0, 1.0 - fractions_y), (1, fractions_y)):
        for ox, wx in ((0, 1.0 - fractions_x), (1, fractions_x)):
            py = y0 + oy
            px = x0 + ox
            weights = wy * wx
            valid = (
                (py >= 0) & (py < height) &
                (px >= 0) & (px < width) &
                (weights > 0.0001)
            )
            if not valid.any():
                continue
            vy = py[valid]
            vx = px[valid]
            vw = weights[valid]
            np.add.at(weight, (vy, vx), vw)
            for channel in range(4):
                np.add.at(accum[..., channel], (vy, vx), colors[valid, channel] * vw)
    return accum, weight


def morph_with_maps(
    source: np.ndarray,
    target: np.ndarray,
    t: float,
    forward: tuple[np.ndarray, np.ndarray],
    reverse: tuple[np.ndarray, np.ndarray],
) -> np.ndarray:
    if t <= 0.0:
        return source.copy()
    if t >= 1.0:
        return target.copy()
    forward_y, forward_x = forward
    reverse_y, reverse_x = reverse
    source_accum, source_weight = splat(source, forward_y, forward_x, t)
    target_accum, target_weight = splat(target, reverse_y, reverse_x, 1.0 - t)
    source_factor = 1.0 - t
    target_factor = t
    total_weight = source_weight * source_factor + target_weight * target_factor
    total = source_accum * source_factor + target_accum * target_factor
    output = np.zeros_like(source)
    occupied = total_weight > 0.015
    output[occupied] = np.clip(
        total[occupied] / total_weight[occupied, None], 0, 255
    ).astype(np.uint8)
    output[..., 3] = np.where(
        occupied,
        np.clip(total_weight * 255.0, 0, 255),
        0,
    ).astype(np.uint8)
    return output


def morph(source: np.ndarray, target: np.ndarray, t: float) -> np.ndarray:
    return morph_with_maps(
        source,
        target,
        t,
        correspondence(source, target),
        correspondence(target, source),
    )


def breathe(source: np.ndarray, frame: int, frame_count: int, strength: float = 1.0) -> np.ndarray:
    phase = frame / frame_count * np.pi * 2.0
    mask = source[..., 3] >= 12
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return source.copy()
    top = float(ys.min())
    bottom = float(ys.max())
    span = max(1.0, bottom - top)
    destination_y = np.indices(mask.shape, dtype=np.float32)[0]
    destination_x = np.indices(mask.shape, dtype=np.float32)[1]
    for y, x in zip(ys, xs):
        height_factor = (bottom - y) / span
        cloth_phase = phase + height_factor * 0.9 + (x % 7) * 0.025
        destination_y[y, x] = y - np.sin(phase) * height_factor * 1.25 * strength
        destination_x[y, x] = x + np.sin(cloth_phase) * height_factor * 0.75 * strength
    accum, weight = splat(source, destination_y, destination_x, 1.0)
    output = np.zeros_like(source)
    occupied = weight > 0.015
    output[occupied] = np.clip(
        accum[occupied] / weight[occupied, None], 0, 255
    ).astype(np.uint8)
    output[..., 3] = np.where(
        occupied,
        np.clip(weight * 255.0, 0, 255),
        0,
    ).astype(np.uint8)
    return output


def warp_pose(
    source: np.ndarray,
    dx: float,
    dy: float,
    scale_x: float,
    scale_y: float,
    shear: float,
) -> np.ndarray:
    mask = source[..., 3] >= 12
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        return source.copy()
    center_x = float(xs.mean())
    bottom = float(ys.max())
    top = float(ys.min())
    span = max(1.0, bottom - top)
    destination_y = np.indices(mask.shape, dtype=np.float32)[0]
    destination_x = np.indices(mask.shape, dtype=np.float32)[1]
    height_factor = (bottom - ys) / span
    destination_x[ys, xs] = (
        center_x + (xs - center_x) * scale_x + dx + shear * height_factor
    )
    destination_y[ys, xs] = bottom + (ys - bottom) * scale_y + dy
    accum, weight = splat(source, destination_y, destination_x, 1.0)
    output = np.zeros_like(source)
    occupied = weight > 0.015
    output[occupied] = np.clip(
        accum[occupied] / weight[occupied, None], 0, 255
    ).astype(np.uint8)
    output[..., 3] = np.where(
        occupied,
        np.clip(weight * 255.0, 0, 255),
        0,
    ).astype(np.uint8)
    return output


def composite(source: np.ndarray, target: np.ndarray, t: float) -> np.ndarray:
    source_alpha = source[..., 3:4].astype(np.float32) / 255.0 * (1.0 - t)
    target_alpha = target[..., 3:4].astype(np.float32) / 255.0 * t
    alpha = source_alpha + target_alpha
    rgb = (
        source[..., :3].astype(np.float32) * source_alpha +
        target[..., :3].astype(np.float32) * target_alpha
    )
    output = np.zeros_like(source)
    occupied = alpha[..., 0] > 0.001
    output[..., :3][occupied] = np.clip(
        rgb[occupied] / alpha[occupied], 0, 255
    ).astype(np.uint8)
    output[..., 3] = np.clip(alpha[..., 0] * 255.0, 0, 255).astype(np.uint8)
    return output


def smoothstep(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def transition(
    source: np.ndarray,
    target: np.ndarray,
    count: int,
    pose: str,
) -> list[np.ndarray]:
    """Make a clean 60 fps pose change without dissolving the silhouette."""
    settings = {
        "attack": (-3.0, 0.0, 5.0, 0.0, 3.0),
        "hit": (1.5, 0.0, -3.5, 0.5, -2.5),
        "victory": (-1.0, 0.8, 0.0, -1.5, 1.8),
        "defeat": (1.0, 0.0, -1.5, 1.2, -1.8),
    }
    pre_dx, pre_dy, post_dx, post_dy, post_shear = settings[pose]
    frames: list[np.ndarray] = []
    for frame in range(count):
        t = frame / (count - 1)
        anticipation = smoothstep(min(1.0, t / 0.44))
        settle = 1.0 - smoothstep(max(0.0, (t - 0.42) / 0.58))
        source_warp = warp_pose(
            source,
            pre_dx * anticipation,
            pre_dy * anticipation,
            1.0 + 0.055 * anticipation,
            1.0 - 0.045 * anticipation,
            -post_shear * 0.45 * anticipation,
        )
        target_warp = warp_pose(
            target,
            post_dx * settle,
            post_dy * settle,
            1.0 - 0.045 * settle,
            1.0 + 0.065 * settle,
            post_shear * settle,
        )
        blend = smoothstep((t - 0.40) / 0.08)
        frames.append(composite(source_warp, target_warp, blend))
    frames[0] = source.copy()
    frames[-1] = target.copy()
    return frames


def run_cycle(keys: list[np.ndarray]) -> list[np.ndarray]:
    frames: list[np.ndarray] = []
    per_pair = RUN_FRAMES // len(keys)
    for index, source in enumerate(keys):
        target = keys[(index + 1) % len(keys)]
        forward = correspondence(source, target)
        reverse = correspondence(target, source)
        for step in range(per_pair):
            frames.append(morph_with_maps(source, target, step / per_pair, forward, reverse))
    return frames


def pack(frames: list[np.ndarray], columns: int = MOTION_COLUMNS) -> Image.Image:
    rows = (len(frames) + columns - 1) // columns
    atlas = Image.new("RGBA", (columns * SOURCE_CELL, rows * SOURCE_CELL), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        x = index % columns * SOURCE_CELL
        y = index // columns * SOURCE_CELL
        atlas.paste(Image.fromarray(frame, "RGBA"), (x, y))
    return atlas


def generate_unit(unit_id: str) -> None:
    source_path = SOURCE_DIR / f"{unit_id}_atlas.png"
    frames = split_source(Image.open(source_path))
    removed_source_pixels = 0
    for clean_index in range(13):
        frames[clean_index], removed = remove_edge_intrusions(frames[clean_index])
        removed_source_pixels += removed

    field_frames: list[np.ndarray] = []
    for row in range(3):
        field_frames.extend(run_cycle(frames[row * 4:row * 4 + 4]))
    for row in range(3):
        idle = frames[row * 4]
        field_frames.extend(breathe(idle, frame, POSE_FRAMES, 0.78) for frame in range(POSE_FRAMES))
    removed_generated_pixels = 0
    for frame_index, generated_frame in enumerate(field_frames):
        field_frames[frame_index], removed = remove_edge_intrusions(generated_frame)
        removed_generated_pixels += removed

    battle_idle: list[np.ndarray] = []
    removed_idle_pixels = 0
    for idle_frame in range(POSE_FRAMES):
        clean_idle, removed = keep_largest_silhouette(
            breathe(frames[12], idle_frame, POSE_FRAMES, 1.0)
        )
        battle_idle.append(clean_idle)
        removed_idle_pixels += removed
    battle_attack = transition(frames[12], frames[13], POSE_FRAMES, "attack")
    if unit_id in LEFT_FACING_ACTION_SOURCE_IDS:
        battle_attack = [np.ascontiguousarray(frame[:, ::-1]) for frame in battle_attack]
    battle_hit = transition(frames[12], frames[14], POSE_FRAMES, "hit")
    battle_victory = transition(frames[12], frames[15], POSE_FRAMES, "victory")
    battle_defeat = transition(frames[12], frames[14], POSE_FRAMES, "defeat")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    pack(field_frames).save(OUTPUT_DIR / f"{unit_id}_field60.png", optimize=True)
    pack(battle_idle + battle_attack + battle_hit).save(
        OUTPUT_DIR / f"{unit_id}_battle60a.png", optimize=True
    )
    pack(battle_victory + battle_defeat).save(
        OUTPUT_DIR / f"{unit_id}_battle60b.png", optimize=True
    )
    print(
        f"generated {unit_id}: field={len(field_frames)}, battle=300 "
        f"sourceEdgeIntrusionsRemoved={removed_source_pixels} "
        f"generatedEdgeIntrusionsRemoved={removed_generated_pixels} "
        f"idleDetachedPixelsRemoved={removed_idle_pixels}"
    )


def main() -> None:
    requested = tuple(sys.argv[1:]) or UNIT_IDS
    unknown = sorted(set(requested) - set(UNIT_IDS))
    if unknown:
        raise SystemExit(f"unknown unit ids: {', '.join(unknown)}")
    for unit_id in requested:
        generate_unit(unit_id)


if __name__ == "__main__":
    main()
