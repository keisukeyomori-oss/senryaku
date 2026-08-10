using System;

namespace BirthdayTactics.Core
{
    /// <summary>
    /// ボスを「ただの強い敵」ではなく、格の違う相手として見せるための契約。
    ///
    /// これまでボスは通常の敵と同じ大きさで、技も武器由来の共通技だった。
    /// 見た目でも挙動でも区別が付かず、章の山場として成立していなかった。
    ///
    /// ここでは3つを一括で決める。
    ///   1. 体格 … 画面に映る大きさ
    ///   2. 技名 … 専用の名前
    ///   3. 技の頻度と威力 … 通常より早く、重く撃つ
    /// </summary>
    public static class BossPresencePolicy
    {
        public const string ChapterBossUnitId = "e_boss";

        /// <summary>通常ユニットの体格を1.0としたときのボスの体格。</summary>
        public const float BossPresenceScale = 1.34f;

        /// <summary>試練の主。章ボスよりさらに大きい。</summary>
        public const float OrdealPresenceScale = 1.52f;

        /// <summary>章ボスかどうか。</summary>
        public static bool IsChapterBoss(string sourceUnitId)
        {
            return string.Equals(sourceUnitId, ChapterBossUnitId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 試練の主かどうか。OrdealStagePolicy が付けるユニットIDで判定する。
        /// 鏡像（ordeal-mirror-*）は等身大のままにする。こちらの写しなので大きくすると筋が通らない。
        /// </summary>
        public static bool IsOrdealFoe(string unitId)
        {
            return string.Equals(unitId, "ordeal-foe", StringComparison.Ordinal);
        }

        /// <summary>
        /// 画面に映る大きさの倍率。アンカーの Height に掛けて使う。
        /// </summary>
        public static float GetPresenceScale(string unitId, string sourceUnitId)
        {
            if (IsOrdealFoe(unitId)) return OrdealPresenceScale;
            return IsChapterBoss(sourceUnitId) ? BossPresenceScale : 1f;
        }

        /// <summary>格上の相手かどうか（体格・技ともに強化される対象）。</summary>
        public static bool HasBossBehaviour(string unitId, string sourceUnitId)
        {
            return IsOrdealFoe(unitId) || IsChapterBoss(sourceUnitId);
        }

        /// <summary>
        /// 必殺技の間隔。通常は3手に1回だが、ボスは2手に1回撃ってくる。
        /// 「またあの技が来る」という圧を出すための数値。
        /// </summary>
        public static int GetSpecialCooldown(string unitId, string sourceUnitId)
        {
            // 値は「必殺技の間に挟む通常行動数」。
            // 1なら2手に1回、2なら3手に1回になる。
            return HasBossBehaviour(unitId, sourceUnitId) ? 1 : 2;
        }

        /// <summary>
        /// 必殺技の威力（百分率）。通常135%に対し、ボスは190%。
        /// </summary>
        public static int GetSpecialPowerPercent(string unitId, string sourceUnitId)
        {
            return HasBossBehaviour(unitId, sourceUnitId) ? 190 : 135;
        }

        /// <summary>
        /// 専用技の名前。武器由来の共通技名を上書きする。
        /// 空文字を返した場合は、呼び出し側が従来どおり武器の技名を使う。
        /// </summary>
        public static string GetSpecialName(string unitId, string sourceUnitId, FormationActionKind kind)
        {
            if (IsOrdealFoe(unitId)) return "ABYSSAL VERDICT — 深淵の裁定";
            if (!IsChapterBoss(sourceUnitId)) return string.Empty;

            switch (kind)
            {
                case FormationActionKind.Magic:
                    return "GLOOM SOVEREIGN — 黒鎧の呪詛";
                case FormationActionKind.Ranged:
                    return "DREAD VOLLEY — 黒鎧の断罪";
                default:
                    return "SOVEREIGN EDGE — 黒鎧の一閃";
            }
        }
    }
}
