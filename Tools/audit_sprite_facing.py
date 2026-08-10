#!/usr/bin/env python3
"""立ち絵の「実表示での向き」を目視監査するためのコンタクトシート生成ツール。

なぜ必要か
----------
戦闘画面では敵チームのスプライトが flipX で反転される。素画のまま並べた
コンタクトシートでは、この反転後の見え方が分からないため、
「攻撃ポーズだけ武器が反対側を向く」「敵が味方と逆方向に魔法を撃つ」
といった破綻をレビューで検出できない。

本ツールは FormationPresentationProfile.GetFlipX と同じ規則を適用し、
実際に画面に出るのと同じ向きでシートを書き出す。

判定基準
--------
    味方 (hero / partner / azuki / memory* / c_*) : 右を向いて攻撃していれば正
    敵   (e_*)                                    : 左を向いて攻撃していれば正

さらに、同一ユニットの4ポーズ間で盾や武器が左右で入れ替わっていないかも
並べて確認する（idle と attack で盾が反対側にあると、ポーズ切替のたびに
盾が身体を横断して見える）。

使い方
------
    python Tools/audit_sprite_facing.py
    # -> tmp/facing_audit/ に players.png / enemies.png を出力
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import re
import sys

try:
    from PIL import Image, ImageDraw, ImageOps
except ImportError:  # pragma: no cover
    sys.exit("Pillow が必要です:  pip install pillow")

Image.MAX_IMAGE_PIXELS = None

REPO = pathlib.Path(__file__).resolve().parent.parent
UNIT_DIR = REPO / "Assets/Resources/Art/Battle/Units"
VARIANT_DIR = UNIT_DIR / "Variants"
PROFILE_CS = REPO / "Assets/Scripts/Core/FormationPresentationProfile.cs"
OUT_DIR = REPO / "tmp/facing_audit"

ALPHA_THRESHOLD = 8
POSES = ["", "_attack", "_hit", "_victory", "_defeat"]

PLAYERS = ["hero", "partner", "azuki", "memory1", "memory2", "memory3",
           "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage"]
ENEMIES = ["e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss"]


def load_left_facing() -> set[str]:
    """FormationPresentationProfile.LeftFacingSourceAssets を .cs から読み取る。"""
    text = PROFILE_CS.read_text(encoding="utf-8")
    match = re.search(
        r"LeftFacingSourceAssets\s*=\s*\{(.*?)\};", text, re.S
    )
    if not match:
        sys.exit("LeftFacingSourceAssets を FormationPresentationProfile.cs から読めませんでした")
    return set(re.findall(r'"([a-z0-9_]+)"', match.group(1)))


def find_asset(asset_id: str) -> pathlib.Path | None:
    for base in (VARIANT_DIR, UNIT_DIR):
        candidate = base / f"{asset_id}.png"
        if candidate.exists():
            return candidate
    return None


def build_sheet(units, is_enemy: bool, left_facing: set[str], out_path: pathlib.Path):
    cell = 240
    sheet = Image.new("RGB", (len(POSES) * cell, len(units) * (cell + 18)), (24, 26, 32))
    draw = ImageDraw.Draw(sheet)

    for row, unit in enumerate(units):
        for col, pose in enumerate(POSES):
            asset_id = unit + pose
            path = find_asset(asset_id)
            if path is None:
                continue

            # FormationPresentationProfile.GetFlipX と同じ規則
            flip = is_enemy ^ (asset_id in left_facing)

            image = Image.open(path).convert("RGBA")
            if flip:
                image = ImageOps.mirror(image)
            box = image.getchannel("A").point(lambda v: 255 if v > ALPHA_THRESHOLD else 0).getbbox()
            image = image.crop(box)
            image.thumbnail((cell - 8, cell - 8))

            tile = Image.new("RGBA", (cell, cell), (24, 26, 32, 255))
            tile.paste(image, ((cell - image.width) // 2, cell - image.height), image)
            sheet.paste(tile.convert("RGB"), (col * cell, row * (cell + 18) + 18))

            # 「攻撃すべき方向」を矢印で示す（PIL既定フォントはCJK非対応のためASCIIで描く）
            draw.text((col * cell + 4, row * (cell + 18) + 3),
                      f"{asset_id}{'  flipX' if flip else ''}", fill=(255, 215, 110))
            if pose == "_attack":
                arrow = "ATTACK -->" if not is_enemy else "<-- ATTACK"
                draw.text((col * cell + 150, row * (cell + 18) + 3), arrow, fill=(120, 230, 160))

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)
    print(f"{out_path.relative_to(REPO)}  ({sheet.width}x{sheet.height})")


def write_manifest(left_facing: set[str]) -> pathlib.Path:
    """全95ポーズの入力画像と実表示規約を機械可読JSONへ固定する。"""
    entries = []
    for units, is_enemy in ((PLAYERS, False), (ENEMIES, True)):
        for unit in units:
            for pose in POSES:
                asset_id = unit + pose
                path = find_asset(asset_id)
                if path is None:
                    raise FileNotFoundError(asset_id)
                image = Image.open(path).convert("RGBA")
                visible_box = image.getchannel("A").point(
                    lambda value: 255 if value > ALPHA_THRESHOLD else 0
                ).getbbox()
                source_faces_left = asset_id in left_facing
                runtime_flip_x = is_enemy ^ source_faces_left
                entries.append({
                    "assetId": asset_id,
                    "team": "enemy" if is_enemy else "player",
                    "pose": "idle" if not pose else pose[1:],
                    "path": path.relative_to(REPO).as_posix(),
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                    "canvas": {"width": image.width, "height": image.height},
                    "visibleBox": list(visible_box) if visible_box else None,
                    "sourceFacesLeft": source_faces_left,
                    "runtimeFlipX": runtime_flip_x,
                    "displayFaces": "left" if is_enemy else "right"
                })

    manifest = {
        "contract": "battle-sprite-facing-v1",
        "alphaThreshold": ALPHA_THRESHOLD,
        "poseCount": len(entries),
        "leftFacingSourceExceptions": sorted(left_facing),
        "entries": entries
    }
    output = OUT_DIR / "manifest.json"
    output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{output.relative_to(REPO)}  ({len(entries)} poses)")
    return output


def main() -> int:
    left_facing = load_left_facing()
    print(f"LeftFacingSourceAssets = {sorted(left_facing)}\n")
    build_sheet(PLAYERS, False, left_facing, OUT_DIR / "players.png")
    build_sheet(ENEMIES, True, left_facing, OUT_DIR / "enemies.png")
    write_manifest(left_facing)
    print("\n味方は攻撃ポーズで右を、敵は左を向いていれば正。")
    print("同一行の4ポーズで盾・武器が左右入れ替わっていないかも確認すること。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
