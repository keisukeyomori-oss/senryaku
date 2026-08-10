#!/usr/bin/env python3
"""既存の専用敗北絵を128px高密度ドットへ変換する。"""

from __future__ import annotations

import pathlib

from PIL import Image


ROOT = pathlib.Path(__file__).resolve().parent.parent
UNITS = ROOT / "Assets/Resources/Art/Battle/Units"
VARIANTS = UNITS / "Variants"
DESTINATION = ROOT / "Assets/Resources/Art/Pixel/Characters/Defeat"
IDS = (
    "hero", "partner", "azuki", "memory1", "memory2", "memory3",
    "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
    "e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss",
)
CELL = 128
PADDING = 6


def convert(unit_id: str) -> pathlib.Path:
    source = (UNITS if unit_id == "memory3" else VARIANTS) / f"{unit_id}_defeat.png"
    image = Image.open(source).convert("RGBA")
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError(f"empty defeat image: {unit_id}")
    image = image.crop(bounds)
    scale = min((CELL - PADDING * 2) / image.width, (CELL - PADDING * 2) / image.height)
    width = max(1, round(image.width * scale))
    height = max(1, round(image.height * scale))
    image = image.resize((width, height), Image.Resampling.LANCZOS)
    alpha = image.getchannel("A")
    colors = image.convert("RGB").quantize(colors=96, method=Image.Quantize.MEDIANCUT).convert("RGBA")
    colors.putalpha(alpha.point(lambda value: 0 if value < 24 else 255))
    output = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    output.alpha_composite(colors, ((CELL - width) // 2, CELL - PADDING - height))
    destination = DESTINATION / f"{unit_id}_defeat.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    output.save(destination, optimize=True)
    return destination


def main():
    for unit_id in IDS:
        print(convert(unit_id).relative_to(ROOT).as_posix())


if __name__ == "__main__":
    main()
