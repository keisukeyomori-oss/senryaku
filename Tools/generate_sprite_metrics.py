#!/usr/bin/env python3
"""FormationPresentationProfile.SpriteMetrics テーブルの生成・検証ツール。

背景
----
SpriteMetrics は「立ち絵PNGの可視領域」を表す契約データで、以下を決めている。

    PivotX        : 可視バウンディングボックスの水平中心（キャンバス幅に対する比）
    PivotY        : 可視バウンディングボックスの下端（= 足元）の高さ（キャンバス高に対する比）
    VisibleHeight : 可視バウンディングボックスの高さ（キャンバス高に対する比）

このテーブルが実画像とずれると、キャラクターが地面から浮いたり、
ポーズ切替で見かけの身長が変わったりする。手書き管理をやめ、
本ツールで PNG から機械的に生成することで再発を防ぐ。

アルファ閾値について（重要）
--------------------------
可視判定は ALPHA_THRESHOLD (=8, 約3%不透明) を超える画素のみを「見える」とみなす。

閾値0（alpha>0）で測ると、目視できないアルファ残渣まで境界に含めてしまう。
実例: c_guard.png はキャンバス下端に alpha 1〜8 の帯が 74px あり、
閾値0で測ると PivotY=0.0 / VisibleHeight=0.9453 になる。これは
「見えないハロー」を足元として扱うため、実際のキャラクターは約0.27ワールド
ユニット浮き、身長も約7.9%小さく描画される。閾値8で測れば正しい値になる。

使い方
------
    # テーブルを標準出力に生成（そのまま .cs に貼れる形式）
    python Tools/generate_sprite_metrics.py

    # 現在の .cs のテーブルと突き合わせて差分だけ表示（CI/検証向け・差分があれば exit 1）
    python Tools/generate_sprite_metrics.py --check

    # .cs を直接書き換える
    python Tools/generate_sprite_metrics.py --apply
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    sys.exit("Pillow が必要です:  pip install pillow")

Image.MAX_IMAGE_PIXELS = None

REPO = pathlib.Path(__file__).resolve().parent.parent
UNIT_DIR = REPO / "Assets/Resources/Art/Battle/Units"
VARIANT_DIR = UNIT_DIR / "Variants"
PROFILE_CS = REPO / "Assets/Scripts/Core/FormationPresentationProfile.cs"

# 見えるとみなすアルファの下限（これ以下は不可視の残渣として無視する）
ALPHA_THRESHOLD = 8
# 閾値0との境界差がこれ以上あればアルファ残渣として警告する
HALO_WARN_PX = 6

POSES = ["", "_attack", "_hit", "_victory", "_defeat"]

# FormationPresentationProfile.UnitIds と StoryExplorationCore.MemoryMinstrelId に対応。
# 増やしたときはここも合わせること（--check が不足を検出する）。
ROSTER = [
    "hero", "partner", "azuki", "memory1", "memory2", "memory3",
    "c_lancer", "c_skywarden", "c_cleric", "c_guard", "c_archer", "c_mage",
    "e_knight", "e_cavalry", "e_archer", "e_flier", "e_mage", "e_cleric", "e_boss",
]

TABLE_RE = re.compile(
    r'\{\s*"([a-z0-9_]+)",\s*new BattleSpriteMetrics\('
    r'([-\d.]+)f,\s*([-\d.]+)f,\s*([-\d.]+)f\)\s*\},?'
)


def find_asset(asset_id: str) -> pathlib.Path | None:
    for base in (VARIANT_DIR, UNIT_DIR):
        candidate = base / f"{asset_id}.png"
        if candidate.exists():
            return candidate
    return None


def bounds(image: Image.Image, threshold: int):
    mask = image.getchannel("A").point(lambda v: 255 if v > threshold else 0)
    return mask.getbbox()


def measure(path: pathlib.Path):
    """(pivot_x, pivot_y, visible_height, halo_px) を返す。"""
    image = Image.open(path).convert("RGBA")
    width, height = image.size
    box = bounds(image, ALPHA_THRESHOLD)
    if box is None:
        raise ValueError(f"{path.name}: 可視画素がありません")
    left, top, right, bottom = box

    raw = bounds(image, 0)
    halo = max(bottom - raw[3], raw[3] - bottom, raw[1] - top, top - raw[1], 0) if raw else 0
    halo = max(abs(raw[3] - bottom), abs(raw[1] - top)) if raw else 0

    return (
        (left + right) / 2 / width,
        (height - bottom) / height,
        (bottom - top) / height,
        halo,
    )


def collect():
    rows, warnings = [], []
    for unit in ROSTER:
        for pose in POSES:
            asset_id = unit + pose
            path = find_asset(asset_id)
            if path is None:
                warnings.append(f"[欠落] {asset_id}.png が見つかりません")
                continue
            pivot_x, pivot_y, visible, halo = measure(path)
            rows.append((asset_id, pivot_x, pivot_y, visible))
            if halo >= HALO_WARN_PX:
                warnings.append(
                    f"[アルファ残渣] {asset_id}.png: 不可視画素(alpha<={ALPHA_THRESHOLD})が"
                    f"境界を {halo}px 押し広げています。書き出し設定の見直しを推奨"
                )

    known = {asset_id for asset_id, *_ in rows}
    for path in sorted(list(UNIT_DIR.glob("*.png")) + list(VARIANT_DIR.glob("*.png"))):
        if path.stem not in known:
            warnings.append(f"[未参照] {path.stem}.png はロスターのどのポーズにも対応していません")
    return rows, warnings


def render(rows) -> str:
    lines = []
    for asset_id, pivot_x, pivot_y, visible in rows:
        lines.append(
            f'                {{ "{asset_id}", new BattleSpriteMetrics('
            f"{pivot_x:.6f}f, {pivot_y:.6f}f, {visible:.6f}f) }},"
        )
    lines[-1] = lines[-1].rstrip(",")
    return "\n".join(lines)


def parse_existing() -> dict:
    text = PROFILE_CS.read_text(encoding="utf-8")
    return {
        m.group(1): (float(m.group(2)), float(m.group(3)), float(m.group(4)))
        for m in TABLE_RE.finditer(text)
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="現在の .cs と突き合わせ、差分があれば exit 1")
    parser.add_argument("--apply", action="store_true", help=".cs のテーブルを実測値で置き換える")
    parser.add_argument("--tolerance", type=float, default=0.0005)
    args = parser.parse_args()

    rows, warnings = collect()
    for warning in warnings:
        print(warning, file=sys.stderr)

    if args.check:
        existing = parse_existing()
        problems = []
        for asset_id, pivot_x, pivot_y, visible in rows:
            if asset_id not in existing:
                problems.append(f"{asset_id}: テーブル未登録")
                continue
            have = existing[asset_id]
            delta = max(abs(have[0] - pivot_x), abs(have[1] - pivot_y), abs(have[2] - visible))
            if delta > args.tolerance:
                problems.append(
                    f"{asset_id}: 実測 ({pivot_x:.6f}, {pivot_y:.6f}, {visible:.6f}) / "
                    f"テーブル ({have[0]:.6f}, {have[1]:.6f}, {have[2]:.6f})  最大差 {delta:.6f}"
                )
        for asset_id in sorted(set(existing) - {r[0] for r in rows}):
            problems.append(f"{asset_id}: 画像が無いのにテーブルに登録されています")

        if problems:
            print(f"\n不一致 {len(problems)} 件:")
            for problem in problems:
                print("  " + problem)
            return 1
        print(f"OK: {len(rows)} 件すべて実測値と一致 (許容差 {args.tolerance})")
        return 0

    block = render(rows)
    if args.apply:
        text = PROFILE_CS.read_text(encoding="utf-8")
        start = text.index('{ "hero", new BattleSpriteMetrics')
        start = text.rindex("\n", 0, start) + 1
        end = text.index("\n            };", start)
        PROFILE_CS.write_text(text[:start] + block + text[end:], encoding="utf-8")
        print(f"{PROFILE_CS.relative_to(REPO)} のテーブルを {len(rows)} 件で更新しました")
        return 0

    print(block)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
