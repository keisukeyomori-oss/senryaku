#!/usr/bin/env python3
"""ImageGenのあずき四足アニメを6x4固定セルへ正規化する。"""

from __future__ import annotations

import pathlib
import sys

from PIL import Image

from process_pixel_sheets import extract_component, find_components


ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets/ArtSource/Generated/azuki_quadruped_source.png"
DESTINATION = ROOT / "Assets/Resources/Art/Pixel/Characters/azuki_quadruped.png"
COLUMNS = 6
ROWS = 4
CELL = 128
PADDING = 5
EXPECTED = (5, 6, 5, 6)


def main() -> int:
    image = Image.open(SOURCE).convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            distance = (red - 255) ** 2 + green ** 2 + (blue - 255) ** 2
            if alpha > 0 and distance < 135 ** 2:
                pixels[x, y] = (red, green, blue, 0)

    labels, components = find_components(image)
    rows = [[] for _ in range(ROWS)]
    for component in components:
        if component["count"] < 2500:
            continue
        row = min(ROWS - 1, int(component["center"][1] * ROWS / image.height))
        rows[row].append(component)
    for row in rows:
        row.sort(key=lambda component: component["center"][0])
    counts = tuple(len(row) for row in rows)
    if counts != EXPECTED:
        raise ValueError(f"unexpected Azuki frame counts: {counts}, expected {EXPECTED}")

    output = Image.new("RGBA", (COLUMNS * CELL, ROWS * CELL), (0, 0, 0, 0))
    for row_index, row in enumerate(rows):
        for column, component in enumerate(row):
            frame = extract_component(image, labels, component)
            scale = min(
                (CELL - PADDING * 2) / frame.width,
                (CELL - PADDING * 2) / frame.height,
            )
            width = max(1, round(frame.width * scale))
            height = max(1, round(frame.height * scale))
            frame = frame.resize((width, height), Image.Resampling.NEAREST)
            x = column * CELL + (CELL - width) // 2
            y = row_index * CELL + CELL - PADDING - height
            output.alpha_composite(frame, (x, y))

    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    output.save(DESTINATION, optimize=True)
    print(f"AZUKI_QUADRUPED frames={counts} path={DESTINATION.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
