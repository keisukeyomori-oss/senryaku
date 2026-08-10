"""Build deterministic pixel skin data from the authored battle-idle cells.

The output keeps every source pixel exactly once. Unity blends neighboring dot
weights around named joints at runtime, so animation is no longer a flipbook and
bone boundaries stay visually closed.
"""

from pathlib import Path
import struct
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Resources/Art/Pixel/Characters"
OUTPUT = ROOT / "Assets/Resources/Art/Pixel/BoneParts"
SKIN_OUTPUT = ROOT / "Assets/Resources/Art/Pixel/SkinData"

UNIT_IDS = (
    "hero", "partner", "azuki", "memory1", "memory2", "memory3",
    "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
    "e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss",
)

PARTS = (
    "cape", "thigh_right", "shin_right", "upper_arm_right", "forearm_right",
    "torso", "thigh_left", "shin_left", "upper_arm_left", "forearm_left",
    "weapon", "head",
)

BONE_INDEX = {
    "torso": 0,
    "head": 1,
    "upper_arm_left": 2,
    "forearm_left": 3,
    "upper_arm_right": 4,
    "forearm_right": 5,
    "thigh_left": 6,
    "shin_left": 7,
    "thigh_right": 8,
    "shin_right": 9,
    "cape": 10,
    "weapon": 11,
}

PARENT_BONE = {
    "torso": 0,
    "head": 0,
    "upper_arm_left": 0,
    "forearm_left": 2,
    "upper_arm_right": 0,
    "forearm_right": 4,
    "thigh_left": 0,
    "shin_left": 6,
    "thigh_right": 0,
    "shin_right": 8,
    "cape": 0,
    "weapon": 3,
}


def battle_idle(unit_id: str) -> Image.Image:
    if unit_id == "azuki":
        atlas = Image.open(SOURCE / "azuki_quadruped.png").convert("RGBA")
        return atlas.crop((0, 0, 128, 128))
    atlas = Image.open(SOURCE / f"{unit_id}_atlas.png").convert("RGBA")
    # Row four in the visible atlas is the side-facing battle row.
    return atlas.crop((0, 384, 128, 512))


def opaque_bounds(frame: Image.Image):
    alpha = frame.getchannel("A")
    bounds = alpha.getbbox()
    if not bounds:
        raise RuntimeError("empty battle cell")
    return bounds


def assign_humanoid(unit_id: str, nx: float, ny: float, pixel) -> str:
    # Side-facing battle art carries shields on the right and blades on the lower
    # left.  Keeping those two silhouettes separate avoids assigning hair to the
    # weapon bone or slicing the outside edge off a shield.
    if nx > 0.88:
        if ny < 0.32:
            return "head"
        return "upper_arm_right" if ny < 0.53 else "forearm_right"
    if ny < 0.29:
        return "head"
    if ny < 0.48:
        if nx < 0.34:
            return "upper_arm_left"
        if nx > 0.66:
            return "upper_arm_right"
        return "torso"
    if ny < 0.66:
        if nx < 0.32:
            return "forearm_left"
        if nx > 0.68:
            return "forearm_right"
        return "torso" if nx > 0.36 and nx < 0.64 else "cape"
    if nx < 0.49:
        return "thigh_left" if ny < 0.82 else "shin_left"
    return "thigh_right" if ny < 0.82 else "shin_right"


def assign_quadruped(nx: float, ny: float) -> str:
    if nx > 0.70 and ny < 0.60:
        return "head"
    if nx < 0.18 and ny < 0.68:
        return "cape"  # tail
    if ny < 0.60:
        return "torso"
    if nx > 0.62:
        return "upper_arm_left" if ny < 0.78 else "forearm_left"
    if nx > 0.48:
        return "upper_arm_right" if ny < 0.78 else "forearm_right"
    if nx > 0.30:
        return "thigh_left" if ny < 0.78 else "shin_left"
    return "thigh_right" if ny < 0.78 else "shin_right"


def split(unit_id: str):
    frame = battle_idle(unit_id)
    left, top, right, bottom = opaque_bounds(frame)
    width = max(1, right - left)
    height = max(1, bottom - top)
    outputs = {name: Image.new("RGBA", frame.size) for name in PARTS}
    source = frame.load()
    targets = {name: image.load() for name, image in outputs.items()}
    counts = {name: 0 for name in PARTS}
    skin_records = []

    for y in range(frame.height):
        for x in range(frame.width):
            pixel = source[x, y]
            if pixel[3] == 0:
                continue
            nx = (x - left) / width
            ny = (y - top) / height
            part = (assign_quadruped(nx, ny) if unit_id == "azuki"
                    else assign_humanoid(unit_id, nx, ny, pixel))
            targets[part][x, y] = pixel
            counts[part] += 1
            child = BONE_INDEX[part]
            parent = PARENT_BONE[part]
            child_weight = skin_weight(part, nx, ny, unit_id == "azuki")
            skin_records.append((
                x, y, pixel[0], pixel[1], pixel[2], pixel[3],
                parent, child, child_weight))

    # Empty weapon slots are valid for unarmed units, but Unity expects every resource.
    destination = OUTPUT / unit_id
    destination.mkdir(parents=True, exist_ok=True)
    for name, image in outputs.items():
        image.save(destination / f"{name}.png", optimize=True)

    covered = sum(counts.values())
    expected = sum(1 for pixel in frame.getdata() if pixel[3] > 0)
    if covered != expected:
        raise RuntimeError(f"{unit_id}: pixel coverage {covered}/{expected}")
    write_skin_data(unit_id, skin_records)
    return counts


def skin_weight(part: str, nx: float, ny: float, quadruped: bool) -> int:
    """Child-bone weight. Pixels at a joint blend with the parent to keep seams closed."""
    if part == "torso":
        return 255
    if part in ("weapon", "cape"):
        return 248
    if quadruped:
        if part == "head":
            t = max(0.0, min(1.0, (nx - 0.70) / 0.30))
        elif "forearm" in part or "shin" in part:
            t = max(0.0, min(1.0, (ny - 0.72) / 0.28))
        else:
            t = max(0.0, min(1.0, (ny - 0.55) / 0.25))
    else:
        if part == "head":
            t = max(0.0, min(1.0, (0.34 - ny) / 0.24))
        elif "upper_arm" in part:
            t = max(0.0, min(1.0, (ny - 0.27) / 0.25))
        elif "forearm" in part:
            t = max(0.0, min(1.0, (ny - 0.45) / 0.25))
        elif "thigh" in part:
            t = max(0.0, min(1.0, (ny - 0.60) / 0.24))
        else:
            t = max(0.0, min(1.0, (ny - 0.76) / 0.24))
    return round((0.46 + t * 0.54) * 255)


def write_skin_data(unit_id: str, records):
    SKIN_OUTPUT.mkdir(parents=True, exist_ok=True)
    path = SKIN_OUTPUT / f"{unit_id}.bytes"
    with path.open("wb") as stream:
        stream.write(b"PSK1")
        stream.write(struct.pack("<BBI", 128, 128, len(records)))
        for record in records:
            stream.write(struct.pack("<BBBBBBBBB", *record))


def main():
    for unit_id in UNIT_IDS:
        counts = split(unit_id)
        print(unit_id, sum(counts.values()), "pixels", counts)


if __name__ == "__main__":
    main()
