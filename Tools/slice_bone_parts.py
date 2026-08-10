"""Slice a standardized 1254x1254 bone-parts sheet into Unity-ready PNGs."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


REFERENCE_SIZE = (1254, 1254)
PART_RECTS = {
    "head": (35, 22, 447, 420),
    "torso": (470, 80, 891, 680),
    "upper_arm_left": (451, 150, 594, 400),
    "upper_arm_right": (895, 115, 1071, 398),
    "forearm_left": (402, 400, 517, 626),
    "forearm_right": (960, 398, 1084, 625),
    "weapon": (38, 457, 221, 1182),
    "thigh_left": (376, 674, 579, 928),
    "thigh_right": (672, 675, 868, 927),
    "shin_left": (372, 895, 525, 1208),
    "shin_right": (701, 895, 837, 1205),
    "cape": (900, 704, 1223, 1189),
}


def scale_rect(rect: tuple[int, int, int, int], size: tuple[int, int]):
    sx = size[0] / REFERENCE_SIZE[0]
    sy = size[1] / REFERENCE_SIZE[1]
    return tuple(
        int(round(value * (sx if index % 2 == 0 else sy)))
        for index, value in enumerate(rect)
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    source = Image.open(args.input).convert("RGBA")
    args.output.mkdir(parents=True, exist_ok=True)

    for name, reference_rect in PART_RECTS.items():
        output_path = args.output / f"{name}.png"
        if output_path.exists() and not args.force:
            raise FileExistsError(f"{output_path} already exists; use --force")

        part = source.crop(scale_rect(reference_rect, source.size))
        alpha = part.getchannel("A")
        if alpha.getbbox() is None:
            raise ValueError(f"{name} has no visible pixels")
        part.save(output_path)
        print(f"{name}: {part.size[0]}x{part.size[1]} -> {output_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
