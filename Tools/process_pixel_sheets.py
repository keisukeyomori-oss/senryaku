#!/usr/bin/env python3
"""ImageGenの4x4素材をUnity用の固定128pxセルへ整列する。"""

from __future__ import annotations

import pathlib
import sys
from array import array
from collections import deque

from PIL import Image


ROOT = pathlib.Path(__file__).resolve().parent.parent
CHARACTER_DIR = ROOT / "Assets/Resources/Art/Pixel/Characters"
SOURCE_DIR = ROOT / "Assets/ArtSource/Pixel"
NAMES = (
    "memory1", "memory2", "memory3",
    "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
    "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss",
)
GRID = 4
CELL = 128
PADDING = 6
ALPHA_THRESHOLD = 12


def find_components(image: Image.Image):
    """シート全体を連結成分に分け、セル境界をまたぐ剣や杖も同じ絵として扱う。"""
    width, height = image.size
    alpha = image.getchannel("A").tobytes()
    labels = array("H", [0]) * (width * height)
    components = []
    next_label = 0

    for index, value in enumerate(alpha):
        if value <= ALPHA_THRESHOLD or labels[index] != 0:
            continue
        next_label += 1
        if next_label >= 65535:
            raise ValueError("too many connected components")
        queue = deque([index])
        labels[index] = next_label
        count = 0
        sum_x = 0
        sum_y = 0
        min_x = width
        min_y = height
        max_x = -1
        max_y = -1
        while queue:
            current = queue.popleft()
            y, x = divmod(current, width)
            count += 1
            sum_x += x
            sum_y += y
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
            for offset_y in (-1, 0, 1):
                neighbor_y = y + offset_y
                if neighbor_y < 0 or neighbor_y >= height:
                    continue
                for offset_x in (-1, 0, 1):
                    if offset_x == 0 and offset_y == 0:
                        continue
                    neighbor_x = x + offset_x
                    if neighbor_x < 0 or neighbor_x >= width:
                        continue
                    neighbor = neighbor_y * width + neighbor_x
                    if alpha[neighbor] <= ALPHA_THRESHOLD or labels[neighbor] != 0:
                        continue
                    labels[neighbor] = next_label
                    queue.append(neighbor)
        if count >= 64:
            components.append({
                "label": next_label,
                "count": count,
                "center": (sum_x / count, sum_y / count),
                "bounds": (min_x, min_y, max_x + 1, max_y + 1),
            })
    return labels, components


def extract_component(image: Image.Image, labels, component) -> Image.Image:
    left, top, right, bottom = component["bounds"]
    frame = image.crop((left, top, right, bottom))
    width, height = frame.size
    mask = bytearray(width * height)
    image_width = image.width
    label = component["label"]
    source_alpha = image.getchannel("A").tobytes()
    for y in range(height):
        source_row = (top + y) * image_width + left
        target_row = y * width
        for x in range(width):
            source_index = source_row + x
            if labels[source_index] == label:
                mask[target_row + x] = source_alpha[source_index]
    frame.putalpha(Image.frombytes("L", (width, height), bytes(mask)))
    return frame


def normalize_sheet(name: str) -> pathlib.Path:
    source = SOURCE_DIR / f"{name}_sheet.png"
    image = Image.open(source).convert("RGBA")
    # ImageGenのアンチエイリアスに残る高彩度マゼンタだけを除去する。
    # キャラ固有の紫はR/Bがこの閾値まで同時に上がらないため保持される。
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            key_distance = (red - 255) ** 2 + green ** 2 + (blue - 255) ** 2
            if alpha > 0 and key_distance < 125 ** 2:
                pixels[x, y] = (red, green, blue, 0)
    output = Image.new("RGBA", (GRID * CELL, GRID * CELL), (0, 0, 0, 0))

    for row in range(GRID):
        for column in range(GRID):
            left = round(image.width * column / GRID)
            top = round(image.height * row / GRID)
            right = round(image.width * (column + 1) / GRID)
            bottom = round(image.height * (row + 1) / GRID)
            frame = image.crop((left, top, right, bottom))
            bounds = frame.getchannel("A").getbbox()
            if bounds is None:
                raise ValueError(f"{name} row={row} column={column} has no character pixels")
            frame = frame.crop(bounds)
            scale = min(
                (CELL - PADDING * 2) / frame.width,
                (CELL - PADDING * 2) / frame.height,
                1.0,
            )
            width = max(1, round(frame.width * scale))
            height = max(1, round(frame.height * scale))
            frame = frame.resize((width, height), Image.Resampling.NEAREST)
            x = column * CELL + (CELL - width) // 2
            y = row * CELL + CELL - PADDING - height
            output.alpha_composite(frame, (x, y))

    destination = CHARACTER_DIR / f"{name}_atlas.png"
    output.save(destination, optimize=True)
    return destination


def main() -> int:
    for name in NAMES:
        destination = normalize_sheet(name)
        print(destination.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
