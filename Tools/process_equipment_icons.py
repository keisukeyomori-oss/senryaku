#!/usr/bin/env python3
"""生成した4x4装備アイコンを透過・固定セルへ正規化する。"""

from __future__ import annotations

import pathlib
import sys

from PIL import Image


ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "Assets/ArtSource/Generated/equipment_icon_atlas_source.png"
DESTINATION = ROOT / "Assets/Resources/Art/UI/equipment_icon_atlas.png"
GRID = 4
CELL = 128
PADDING = 8


def main() -> int:
    source = Image.open(SOURCE).convert("RGBA")
    output = Image.new("RGBA", (GRID * CELL, GRID * CELL), (0, 0, 0, 0))
    for row in range(GRID):
        for column in range(GRID):
            left = round(source.width * column / GRID)
            top = round(source.height * row / GRID)
            right = round(source.width * (column + 1) / GRID)
            bottom = round(source.height * (row + 1) / GRID)
            frame = source.crop((left, top, right, bottom))
            pixels = frame.load()
            for y in range(frame.height):
                for x in range(frame.width):
                    red, green, blue, alpha = pixels[x, y]
                    # 背景とその補間色のみを除去。装備に使われる深緑は残す。
                    key_distance = (red - 3) ** 2 + (green - 248) ** 2 + (blue - 5) ** 2
                    if alpha > 0 and key_distance < 110 ** 2:
                        pixels[x, y] = (red, green, blue, 0)
            bounds = frame.getchannel("A").getbbox()
            if bounds is None:
                raise ValueError(f"empty equipment cell: {row},{column}")
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
            y = row * CELL + (CELL - height) // 2
            output.alpha_composite(frame, (x, y))
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    output.save(DESTINATION, optimize=True)
    print(DESTINATION.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
