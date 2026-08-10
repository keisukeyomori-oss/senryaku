"""Render a deterministic contact sheet directly from generated pixel atlases."""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from generate_pixel_motion import keep_largest_silhouette, remove_edge_intrusions


ROOT = Path(__file__).resolve().parents[1]
MOTION = ROOT / "Assets/Resources/Art/Pixel/Characters/Motion60"
PIXEL = ROOT / "Assets/Resources/Art/Pixel/Characters"
OUTPUT = ROOT / "TestResults/Motion60SequencePreview-PNG.png"
CAST_OUTPUT = ROOT / "TestResults/MageCastSinglePassPreview.gif"
QA_OUTPUT = ROOT / "TestResults/BattleTimelineQA.png"
CELL = 128
FRAME_COLUMNS = 15


def frame(atlas: Image.Image, index: int, columns: int) -> Image.Image:
    x = index % columns * CELL
    y = index // columns * CELL
    return atlas.crop((x, y, x + CELL, y + CELL))


def main() -> None:
    caster_ids = (
        "hero", "partner", "memory1", "memory2", "memory3",
        "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
        "e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss",
    )
    checked_idle_frames = 0
    for unit_id in caster_ids:
        atlas = Image.open(MOTION / f"{unit_id}_battle60a.png").convert("RGBA")
        for index in range(60):
            _, removed = remove_edge_intrusions(
                np.asarray(frame(atlas, index, FRAME_COLUMNS)).copy()
            )
            if removed:
                raise RuntimeError(
                    f"idle edge intrusion remains: {unit_id} frame={index} pixels={removed}"
                )
            _, detached = keep_largest_silhouette(
                np.asarray(frame(atlas, index, FRAME_COLUMNS)).copy()
            )
            if detached:
                raise RuntimeError(
                    f"detached idle pixels remain: {unit_id} frame={index} pixels={detached}"
                )
            checked_idle_frames += 1

    entrance_units = caster_ids
    checked_entrance_frames = 0
    for unit_id in entrance_units:
        atlas = Image.open(MOTION / f"{unit_id}_field60.png").convert("RGBA")
        for index in range(40, 60):
            _, removed = remove_edge_intrusions(
                np.asarray(frame(atlas, index, FRAME_COLUMNS)).copy()
            )
            if removed:
                raise RuntimeError(
                    f"entrance edge intrusion remains: {unit_id} frame={index} pixels={removed}"
                )
            checked_entrance_frames += 1

    rows = (
        ("HERO  field walk", MOTION / "hero_field60.png", (40, 44, 48, 52, 56), FRAME_COLUMNS),
        ("HERO  windup > release > impact > post-impact idle > return idle", MOTION / "hero_battle60a.png", (60, 89, 119, 0, 15), FRAME_COLUMNS),
        ("MAGE  windup > release > impact > post-impact idle > return idle", MOTION / "c_mage_battle60a.png", (60, 89, 119, 0, 15), FRAME_COLUMNS),
        ("KNIGHT  windup > release > impact > post-impact idle > return idle", MOTION / "e_knight_battle60a.png", (60, 89, 119, 0, 15), FRAME_COLUMNS),
        ("AZUKI  quadruped run+bite", PIXEL / "azuki_quadruped.png", (12, 13, 14, 18, 21), 6),
    )
    canvas = Image.new("RGBA", (1100, 945), (39, 43, 52, 255))
    draw = ImageDraw.Draw(canvas)
    for row, (label, path, indices, columns) in enumerate(rows):
        atlas = Image.open(path).convert("RGBA")
        draw.text((20, row * 185 + 12), label, fill=(228, 235, 246, 255))
        for column, index in enumerate(indices):
            image = frame(atlas, index, columns).resize((154, 154), Image.Resampling.NEAREST)
            canvas.alpha_composite(image, (155 + column * 185, row * 185 + 22))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(OUTPUT, optimize=True)
    mage_atlas = Image.open(MOTION / "c_mage_battle60a.png").convert("RGBA")
    gather = [round((step / 37) * 0.62 * 59) for step in range(38)]
    release = [round((0.62 + (step / 22) * 0.38) * 59) for step in range(1, 23)]
    cast_indices = gather + release
    restarts = sum(
        1 for previous, current in zip(cast_indices, cast_indices[1:])
        if current < previous
    )
    if cast_indices[0] != 0 or cast_indices[-1] != 59 or restarts:
        raise RuntimeError(
            f"mage cast is not a single pass: first={cast_indices[0]} "
            f"last={cast_indices[-1]} restarts={restarts}"
        )
    cast_preview = [
        frame(mage_atlas, 60 + index, FRAME_COLUMNS).resize(
            (384, 384), Image.Resampling.NEAREST
        )
        for index in cast_indices
    ]
    cast_preview.extend(
        frame(mage_atlas, index, FRAME_COLUMNS).resize(
            (384, 384), Image.Resampling.NEAREST
        )
        for index in range(15)
    )
    cast_preview[0].save(
        CAST_OUTPUT,
        save_all=True,
        append_images=cast_preview[1:],
        duration=17,
        loop=0,
        disposal=2,
    )

    qa_rows = (
        ("MAGE single cast  0 > 15 > 36 > 48 > 59 > idle", "c_mage", "battle60a", (60, 75, 96, 108, 119, 0), False),
        ("ENTRANCE stable idle  no frame switching", "c_lancer", "battle60a", (0, 0, 0, 0, 0, 0), False),
        ("ENEMY cavalry  corrected -> RIGHT", "e_cavalry", "battle60a", (60, 74, 89, 104, 119, 119), True),
        ("ENEMY knight  source -> RIGHT", "e_knight", "battle60a", (60, 74, 89, 104, 119, 119), False),
        ("ENEMY boss  corrected -> RIGHT", "e_boss", "battle60a", (60, 74, 89, 104, 119, 119), True),
        ("TARGET hit  0 > 15 > 42 > 50 > 59 > idle", "e_knight", "battle60a", (120, 135, 162, 170, 179, 0), False),
    )
    qa_canvas = Image.new("RGBA", (1160, 1114), (39, 43, 52, 255))
    qa_draw = ImageDraw.Draw(qa_canvas)
    for row, (label, unit_id, suffix, indices, flip) in enumerate(qa_rows):
        atlas = Image.open(MOTION / f"{unit_id}_{suffix}.png").convert("RGBA")
        qa_draw.text((18, row * 184 + 10), label, fill=(232, 238, 247, 255))
        for column, index in enumerate(indices):
            image = frame(atlas, index, FRAME_COLUMNS)
            if flip:
                image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            image = image.resize((144, 144), Image.Resampling.NEAREST)
            qa_canvas.alpha_composite(image, (150 + column * 164, row * 184 + 28))
    qa_canvas.save(QA_OUTPUT, optimize=True)
    print(
        f"MOTION60_PNG_PREVIEW_OK {OUTPUT} "
        f"casterIdleFrames={checked_idle_frames} edgeIntrusions=0 "
        f"entranceFrames={checked_entrance_frames} entranceIntrusions=0 "
        f"mageCastFrames={len(cast_indices)} mageCastRestarts={restarts} "
        f"castPreview={CAST_OUTPUT} qaPreview={QA_OUTPUT}"
    )


if __name__ == "__main__":
    main()
