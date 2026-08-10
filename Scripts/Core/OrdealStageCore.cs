using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    /// <summary>
    /// 試練用の StageData を、通常のステージ定義から機械的に組み立てる。
    ///
    /// 手で試練用のステージをJSONに書くと、通常ステージの調整とズレていく。
    /// 「最終戦の何倍か」で定義しておけば、本編のバランスを触っても試練が自動で追従する。
    ///
    /// 生成は決定的で、同じ入力からは必ず同じステージが出る。
    /// </summary>
    public static class OrdealStagePolicy
    {
        /// <summary>隊列に配置できる上限。これを超えるとスロットが確保できない。</summary>
        public const int MaxUnitsPerTeam = FormationPresentationProfile.MaxFormationSlots;

        public const string PlayerTeam = "player";
        public const string EnemyTeam = "enemy";

        /// <summary>
        /// 試練のステージを組み立てる。
        /// </summary>
        /// <param name="baseStage">基準にする通常ステージ。通常は最終戦を渡す。</param>
        /// <param name="ordeal">試練の定義。</param>
        public static StageData BuildStage(StageData baseStage, OrdealEncounter ordeal)
        {
            if (baseStage == null) throw new ArgumentNullException(nameof(baseStage));
            if (ordeal == null) throw new ArgumentNullException(nameof(ordeal));
            if (baseStage.units == null || baseStage.units.Length == 0)
                throw new ArgumentException("Base stage must contain units.", nameof(baseStage));

            StageUnitData[] players = baseStage.units
                .Where(unit => IsTeam(unit, PlayerTeam))
                .OrderBy(unit => unit.y)
                .ThenBy(unit => unit.id, StringComparer.Ordinal)
                .Take(MaxUnitsPerTeam)
                .Select(ClonePlayer)
                .ToArray();

            if (players.Length == 0)
                throw new ArgumentException("Base stage must contain player units.", nameof(baseStage));

            StageUnitData[] foes = ordeal.FoeKind == OrdealFoeKind.MirrorOfParty
                ? BuildMirrorFoes(players, ordeal)
                : BuildSingleFoe(baseStage, ordeal);

            return new StageData
            {
                id = "ordeal-" + ordeal.Id,
                displayName = ordeal.Name,
                sourceStageId = baseStage.id,
                chapter = ordeal.Subtitle,
                backgroundId = string.IsNullOrWhiteSpace(ordeal.BackgroundId)
                    ? baseStage.backgroundId
                    : ordeal.BackgroundId,
                learningObjective = "常識の通じない相手に、編成だけで挑む",
                recommendedLevel = baseStage.recommendedLevel,
                // 通常ステージより必ず後ろに並ぶようにして、難度順の想定を壊さない。
                difficultyIndex = baseStage.difficultyIndex + 100,
                width = baseStage.width,
                height = baseStage.height,
                units = players.Concat(foes).ToArray()
            };
        }

        /// <summary>
        /// たった1体だが、最終戦の敵部隊すべてを合わせた体力に倍率を掛けた値を持つ。
        /// 攻撃力は「最終戦でいちばん痛い一撃」を基準に倍率を掛ける。
        /// </summary>
        private static StageUnitData[] BuildSingleFoe(StageData baseStage, OrdealEncounter ordeal)
        {
            StageUnitData[] baseFoes = baseStage.units
                .Where(unit => IsTeam(unit, EnemyTeam))
                .ToArray();

            if (baseFoes.Length == 0)
                throw new ArgumentException("Base stage must contain enemy units.", nameof(baseStage));

            int totalHp = baseFoes.Sum(unit => Math.Max(1, unit.maxHp));
            int peakDamage = baseFoes.Max(unit => Math.Max(1, unit.damage));
            StageUnitData strongestTemplate = baseFoes
                .OrderByDescending(unit => unit.maxHp)
                .ThenBy(unit => unit.id, StringComparer.Ordinal)
                .First();
            StageUnitData template = baseFoes.FirstOrDefault(unit =>
                    string.Equals(
                        unit.sourceUnitId,
                        ordeal.FoeSourceUnitId,
                        StringComparison.Ordinal))
                ?? strongestTemplate;

            return new[]
            {
                new StageUnitData
                {
                    id = "ordeal-foe",
                    sourceUnitId = string.IsNullOrWhiteSpace(ordeal.FoeSourceUnitId)
                        ? strongestTemplate.sourceUnitId
                        : ordeal.FoeSourceUnitId,
                    displayName = ordeal.Name,
                    className = template.className,
                    team = EnemyTeam,
                    level = template.level,
                    x = template.x,
                    y = template.y,
                    maxHp = Scale(totalHp, ordeal.PowerMultiplier),
                    moveRange = template.moveRange,
                    attackRange = template.attackRange,
                    damage = Scale(peakDamage, ordeal.PowerMultiplier),
                    weaponId = template.weaponId,
                    armorId = template.armorId,
                    tactic = template.tactic
                }
            };
        }

        /// <summary>
        /// こちらの編成をそのまま鏡写しにする。クラスも武器も同じで、体力と攻撃だけが倍率ぶん上。
        /// 「帰らなかった方の自分たち」という筋書きに合わせ、立ち絵も味方のものを流用する。
        /// </summary>
        private static StageUnitData[] BuildMirrorFoes(StageUnitData[] players, OrdealEncounter ordeal)
        {
            return players
                .Select((player, index) => new StageUnitData
                {
                    id = "ordeal-mirror-" + player.id,
                    sourceUnitId = player.sourceUnitId,
                    displayName = player.displayName + "の影",
                    className = player.className,
                    team = EnemyTeam,
                    level = player.level,
                    x = player.x,
                    // 味方と同じ y を使うと slot 順が揃い、左右対称に並ぶ。
                    y = player.y,
                    maxHp = Scale(Math.Max(1, player.maxHp), ordeal.PowerMultiplier),
                    moveRange = player.moveRange,
                    attackRange = player.attackRange,
                    damage = Scale(Math.Max(1, player.damage), ordeal.PowerMultiplier),
                    weaponId = player.weaponId,
                    armorId = player.armorId,
                    tactic = player.tactic
                })
                .ToArray();
        }

        private static StageUnitData ClonePlayer(StageUnitData source)
        {
            return new StageUnitData
            {
                id = source.id,
                sourceUnitId = source.sourceUnitId,
                displayName = source.displayName,
                className = source.className,
                team = PlayerTeam,
                level = source.level,
                x = source.x,
                y = source.y,
                maxHp = source.maxHp,
                moveRange = source.moveRange,
                attackRange = source.attackRange,
                damage = source.damage,
                weaponId = source.weaponId,
                armorId = source.armorId,
                tactic = source.tactic
            };
        }

        private static int Scale(int value, float multiplier)
        {
            // 切り上げ。倍率を掛けたのに値が据え置きになる事故を防ぐ。
            return Math.Max(1, (int)Math.Ceiling(value * (double)multiplier));
        }

        private static bool IsTeam(StageUnitData unit, string team)
        {
            return string.Equals(unit.team, team, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 銘器の効果を戦闘コアから参照するための問い合わせ。
    ///
    /// 効果そのものの実装は FormationBattleCore 側に置き、
    /// 「その効果が有効かどうか」の判定だけをここに集約する。
    /// 所持判定を戦闘コードに散らすと、銘器を増やしたときに漏れる。
    /// </summary>
    public static class RelicEffectPolicy
    {
        public const string HushEdgeId = "hush-edge";
        public const string ReturningCoatId = "returning-coat";
        public const string DuetUnisonId = "duet-unison";

        /// <summary>
        /// 静寂の刃: 相手の防御姿勢（Defensive の構え、および物理に強い knight）による
        /// ダメージ軽減を無視する。音が消えるので身構えが間に合わない、という理屈。
        /// </summary>
        public static bool NegatesGuard(IEnumerable<string> resolvedIds)
        {
            return StoryChoicePolicy.HasRelic(resolvedIds, HushEdgeId);
        }

        /// <summary>
        /// 帰路の外套: 味方が倒れるはずの一撃を、1戦につき一度だけHP1で耐える。
        /// </summary>
        public static bool RevivesOnceWhenFelled(IEnumerable<string> resolvedIds)
        {
            return StoryChoicePolicy.HasRelic(resolvedIds, ReturningCoatId);
        }

        /// <summary>二重奏: 連携技の「隣接していること」という条件を無視する。</summary>
        public static bool IgnoresBondAdjacency(IEnumerable<string> resolvedIds)
        {
            return StoryChoicePolicy.HasRelic(resolvedIds, DuetUnisonId);
        }

        /// <summary>
        /// 銘器の効果は本編の戦闘にのみ適用し、試練の中では無効にする。
        /// 試練で得た力で次の試練を楽にすると、3つ目がただの消化になるため。
        /// </summary>
        public static bool AppliesTo(StageData stage)
        {
            if (stage == null) return false;
            return !(stage.id ?? string.Empty).StartsWith("ordeal-", StringComparison.Ordinal);
        }
    }
}
