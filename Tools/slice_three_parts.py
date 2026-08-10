#!/usr/bin/env python3
"""立ち絵を「頭 / 胴（腕含む） / 武器」の3枚に切り分ける。

12分割との違い
--------------
前回の12分割は、パーツごとに切り詰めたキャンバスへ書き出し、実行時に
レイアウト表の座標で組み直していた。関節が1pxずれるだけで人型が破綻するため、
実機では崩れて見えた。

本ツールは**全パーツを元と同じキャンバスサイズで書き出す**。
つまり各パーツは「元絵の該当部分だけを残し、他を透明にした画像」になる。
同じ位置に重ねれば、元絵と1px単位で一致する。
座標合わせが原理的に不要なので、静止時に破綻することがない。

回転はピボット（首・肩・握り位置）を中心に掛ける。

頭の切り出し
------------
首の位置を自動検出する。可視領域の上から25%〜55%の帯で、
横幅がいちばん細くなる行を首とみなす（人型なら首が最も細い）。
検出結果は必ず preview で目視確認すること。

武器の切り出し
--------------
自動検出はしない。前回の破綻はまさに自動判定が原因だった。
武器は Tools/three_part_regions.json に矩形を手で書く。
未指定のユニットは頭/胴の2分割になる（それでも十分に動いて見える）。

使い方
------
    python Tools/slice_three_parts.py --preview hero
        -> tmp/three_part/hero_preview.png に検証用の画像を出す
           （元絵 / 3パーツ / 再合成 / 差分 / 動かした例）

    python Tools/slice_three_parts.py --write hero partner azuki
        -> Assets/Resources/Art/Battle/ThreeParts/<unit>/{head,body,weapon}.png
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

try:
    from PIL import Image, ImageChops, ImageDraw
except ImportError:  # pragma: no cover
    sys.exit("Pillow が必要です:  pip install pillow")

Image.MAX_IMAGE_PIXELS = None

REPO = pathlib.Path(__file__).resolve().parent.parent
UNIT_DIR = REPO / "Assets/Resources/Art/Battle/Units"
OUT_DIR = REPO / "Assets/Resources/Art/Battle/ThreeParts"
PREVIEW_DIR = REPO / "tmp/three_part"
REGIONS = REPO / "Tools/three_part_regions.json"

ALPHA_THRESHOLD = 8
# 首を探す帯（可視領域の上端からの割合）
NECK_SEARCH_TOP = 0.25
NECK_SEARCH_BOTTOM = 0.55


def load_regions() -> dict:
    if not REGIONS.exists():
        return {}
    return json.loads(REGIONS.read_text(encoding="utf-8"))


def visible_box(image: Image.Image):
    mask = image.getchannel("A").point(lambda v: 255 if v > ALPHA_THRESHOLD else 0)
    box = mask.getbbox()
    if box is None:
        raise ValueError("可視画素がありません")
    return box


def find_neck_row(image: Image.Image) -> int:
    """首（横幅が最も細い行）の絶対Y座標を返す。"""
    left, top, right, bottom = visible_box(image)
    alpha = image.getchannel("A").load()
    height = bottom - top

    search_from = top + int(height * NECK_SEARCH_TOP)
    search_to = top + int(height * NECK_SEARCH_BOTTOM)

    best_row, best_width = search_from, None
    for y in range(search_from, max(search_from + 1, search_to)):
        xs = [x for x in range(left, right) if alpha[x, y] > ALPHA_THRESHOLD]
        if not xs:
            continue
        width = xs[-1] - xs[0]
        if best_width is None or width < best_width:
            best_width, best_row = width, y
    return best_row


def build_parts(image: Image.Image, unit_id: str, regions: dict):
    """(head, body, weapon | None) を、すべて元と同じキャンバスで返す。"""
    width, height = image.size
    # 非人型（猫・竜など）は自動検出が効かない。
    # 例: azuki は前脚が頭の横にあるため、幅の最小値が首ではなく腰に出る。
    # そういうユニットは three_part_regions.json の "neck" で直接指定する。
    override = regions.get(unit_id, {}).get("neck")
    neck = int(override) if override is not None else find_neck_row(image)

    def blank():
        return Image.new("RGBA", (width, height), (0, 0, 0, 0))

    weapon = None
    weapon_rect = regions.get(unit_id, {}).get("weapon")
    remaining = image.copy()

    if weapon_rect:
        x0, y0, x1, y1 = weapon_rect
        weapon = blank()
        weapon.paste(image.crop((x0, y0, x1, y1)), (x0, y0))
        # 胴と頭から武器領域を抜く（二重に描かれるのを防ぐ）
        cleared = remaining.load()
        for y in range(y0, min(y1, height)):
            for x in range(x0, min(x1, width)):
                cleared[x, y] = (0, 0, 0, 0)

    head = blank()
    head.paste(remaining.crop((0, 0, width, neck)), (0, 0))
    body = blank()
    body.paste(remaining.crop((0, neck, width, height)), (0, neck))

    return head, body, weapon, neck


def compose(parts) -> Image.Image:
    result = Image.new("RGBA", parts[0].size, (0, 0, 0, 0))
    for part in parts:
        if part is not None:
            result = Image.alpha_composite(result, part)
    return result


def on_dark(image: Image.Image) -> Image.Image:
    background = Image.new("RGB", image.size, (24, 26, 32))
    background.paste(image, (0, 0), image)
    return background


def preview(unit_id: str, regions: dict):
    source = Image.open(UNIT_DIR / f"{unit_id}.png").convert("RGBA")
    head, body, weapon, neck = build_parts(source, unit_id, regions)
    rebuilt = compose([body, weapon, head])

    # 再合成が元絵と一致するか（ここがずれるとその時点で破綻確定）
    difference = ImageChops.difference(source, rebuilt)
    mismatch = difference.getbbox()

    # 動かした例: 頭を首中心に7度、武器を握り中心に18度回す
    moved_head = head.rotate(
        7, resample=Image.BICUBIC, center=(source.width // 2, neck))
    layers = [body]
    if weapon is not None:
        pivot = regions.get(unit_id, {}).get("weapon_pivot")
        center = tuple(pivot) if pivot else (source.width // 2, neck)
        layers.append(weapon.rotate(-18, resample=Image.BICUBIC, center=center))
    layers.append(moved_head)
    moved = compose(layers)

    tiles = [("元絵", source), ("頭", head), ("胴", body)]
    if weapon is not None:
        tiles.append(("武器", weapon))
    tiles.append(("再合成", rebuilt))
    tiles.append(("動かした例", moved))

    cell = 300
    sheet = Image.new("RGB", (cell * len(tiles), cell + 20), (18, 20, 24))
    draw = ImageDraw.Draw(sheet)
    for index, (label, tile) in enumerate(tiles):
        shown = on_dark(tile).copy()
        shown.thumbnail((cell - 8, cell - 8))
        sheet.paste(shown, (index * cell + (cell - shown.width) // 2, 20))
        draw.text((index * cell + 6, 4), f"{index+1}. {label}", fill=(255, 214, 110))

    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    out = PREVIEW_DIR / f"{unit_id}_preview.png"
    sheet.save(out)

    print(f"{unit_id}: 首の位置 y={neck} / キャンバス {source.size}")
    print(f"  再合成と元絵の差分: {'なし（完全一致）' if mismatch is None else f'あり {mismatch}'}")
    print(f"  武器パーツ: {'あり' if weapon is not None else 'なし（頭/胴の2分割）'}")
    print(f"  -> {out.relative_to(REPO)}")


def write_parts(unit_id: str, regions: dict):
    source = Image.open(UNIT_DIR / f"{unit_id}.png").convert("RGBA")
    head, body, weapon, neck = build_parts(source, unit_id, regions)
    if ImageChops.difference(source, compose([body, weapon, head])).getbbox() is not None:
        sys.exit(f"{unit_id}: 再合成が元絵と一致しません。書き出しを中止しました。")

    target = OUT_DIR / unit_id
    target.mkdir(parents=True, exist_ok=True)
    head.save(target / "head.png")
    body.save(target / "body.png")
    if weapon is not None:
        weapon.save(target / "weapon.png")
    print(f"{unit_id}: 書き出し完了 (首 y={neck}) -> {target.relative_to(REPO)}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", nargs="*", default=None)
    parser.add_argument("--write", nargs="*", default=None)
    args = parser.parse_args()

    regions = load_regions()
    if args.preview is not None:
        for unit_id in args.preview or ["hero"]:
            preview(unit_id, regions)
        return 0
    if args.write is not None:
        for unit_id in args.write or []:
            write_parts(unit_id, regions)
        return 0

    parser.print_help()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
