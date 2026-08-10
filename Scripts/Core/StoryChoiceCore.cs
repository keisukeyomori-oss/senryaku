using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    /// <summary>選択肢を選んだ結果、物語がどこへ向かうか。</summary>
    public enum StoryChoiceOutcome
    {
        /// <summary>通常の会話へ合流する。場違いな選択肢のほとんどはこれ。</summary>
        Conversation,

        /// <summary>試練。あり得ない強さの敵との戦闘に入る。勝てば銘器が手に入る。</summary>
        Ordeal,

        /// <summary>破滅。ゲームオーバー演出に入るが、進行は失われず同じ選択肢からやり直せる。</summary>
        Downfall
    }

    /// <summary>銘器の装備区分。</summary>
    public enum RelicSlot
    {
        Weapon,
        Armor,
        Technique
    }

    /// <summary>
    /// 試練に勝つと手に入る唯一無二の品。同じものは二度と手に入らない。
    /// </summary>
    public sealed class UniqueRelic
    {
        public string Id { get; }
        public string Name { get; }
        public RelicSlot Slot { get; }
        public string Description { get; }
        /// <summary>入手時に表示する一文。</summary>
        public string AcquisitionLine { get; }

        internal UniqueRelic(string id, string name, RelicSlot slot, string description, string acquisitionLine)
        {
            Id = id;
            Name = name;
            Slot = slot;
            Description = description;
            AcquisitionLine = acquisitionLine;
        }
    }

    /// <summary>試練の敵の組み立て方。</summary>
    public enum OrdealFoeKind
    {
        /// <summary>たった1体。最終戦の敵部隊すべてを合わせた以上の体力を持つ。</summary>
        SingleFoe,

        /// <summary>こちらの編成をそのまま鏡写しにした部隊。人数も構成もこちらと同じになる。</summary>
        MirrorOfParty
    }

    /// <summary>試練で戦う相手。通常戦闘の敵とは別枠で、意図的に理不尽な強さにする。</summary>
    public sealed class OrdealEncounter
    {
        public string Id { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public string BackgroundId { get; }
        /// <summary>通常の最終ボスを1.0としたときの強さ倍率。</summary>
        public float PowerMultiplier { get; }
        /// <summary>撃破時に得られる銘器のID。</summary>
        public string RelicId { get; }
        public OrdealFoeKind FoeKind { get; }
        /// <summary>SingleFoe のときに使う立ち絵のユニットID。MirrorOfParty では未使用。</summary>
        public string FoeSourceUnitId { get; }
        public IReadOnlyList<string> IntroLines { get; }

        internal OrdealEncounter(
            string id,
            string name,
            string subtitle,
            string backgroundId,
            float powerMultiplier,
            string relicId,
            OrdealFoeKind foeKind,
            string foeSourceUnitId,
            params string[] introLines)
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
            BackgroundId = backgroundId;
            PowerMultiplier = powerMultiplier;
            RelicId = relicId;
            FoeKind = foeKind;
            FoeSourceUnitId = foeSourceUnitId;
            IntroLines = introLines ?? Array.Empty<string>();
        }
    }

    /// <summary>選択肢1つ。</summary>
    public sealed class StoryChoiceOption
    {
        public string Text { get; }

        /// <summary>
        /// その場にそぐわない選択肢かどうか。UI側で色や配置を変えず、
        /// 「普通の選択肢と同じ顔をして紛れている」状態を保つこと。
        /// この値はテストと作劇の管理用であり、プレイヤーには見せない。
        /// </summary>
        public bool IsAbsurd { get; }

        public StoryChoiceOutcome Outcome { get; }

        /// <summary>選んだ直後に流れる台詞。</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>Outcome が Ordeal のときだけ設定される。</summary>
        public string OrdealId { get; }

        internal StoryChoiceOption(
            string text,
            bool isAbsurd,
            StoryChoiceOutcome outcome,
            string ordealId,
            params string[] lines)
        {
            Text = text;
            IsAbsurd = isAbsurd;
            Outcome = outcome;
            OrdealId = ordealId;
            Lines = lines ?? Array.Empty<string>();
        }
    }

    /// <summary>選択を迫る場面。</summary>
    public sealed class StoryChoicePrompt
    {
        public string Id { get; }
        /// <summary>この選択肢が出る章（stageIndex）。</summary>
        public int StageIndex { get; }
        public string Situation { get; }
        public IReadOnlyList<StoryChoiceOption> Options { get; }

        internal StoryChoicePrompt(string id, int stageIndex, string situation, params StoryChoiceOption[] options)
        {
            Id = id;
            StageIndex = stageIndex;
            Situation = situation;
            Options = options ?? Array.Empty<StoryChoiceOption>();
        }
    }

    /// <summary>
    /// 幻想水滸伝IIを意識した選択肢システムの中核。
    ///
    /// 設計方針
    /// --------
    /// 1. **結果は乱数ではなく作り込み。**
    ///    「まれに」を乱数で出すと、テストできず、体験も再現できず、
    ///    せっかくの銘器に一生たどり着けない可能性が残る。
    ///    そのため (promptId, optionIndex) から結果は一意に決まる。
    ///    「まれ」であることは、場違いな選択肢18個のうち Ordeal 3個・Downfall 2個という
    ///    **配合**で表現する。プレイヤーから見た体感は乱数と変わらない。
    ///
    /// 2. **場違いな選択肢は普通の顔で紛れる。**
    ///    IsAbsurd は作劇とテストの管理用で、UIに出してはいけない。
    ///
    /// 3. **破滅しても進行は失われない。**
    ///    Downfall はゲームオーバー演出を出すが、セーブは書き換えない。
    ///    同じ選択肢へ戻り、一度死んだ選択肢には既読の印が付く。
    ///
    /// 保存について
    /// ------------
    /// CampaignSaveData は変更しない。既存の string[] である
    /// resolvedStoryEntityIds に、以下の書式のIDを追記して状態を持つ。
    ///     "choice:{promptId}:{optionIndex}"  … その選択肢を選んだことがある
    ///     "relic:{relicId}"                  … その銘器を入手済み
    /// これによりセーブのスキーマ変更が不要になり、既存セーブとの互換性も保たれる。
    /// </summary>
    public static class StoryChoicePolicy
    {
        public const string ChoiceIdPrefix = "choice:";
        public const string RelicIdPrefix = "relic:";

        /// <summary>その場面を決着させたことを表す接頭辞。破滅では付かないので、何度でもやり直せる。</summary>
        public const string SettledIdPrefix = "prompt:";

        private static readonly UniqueRelic[] Relics =
        {
            new UniqueRelic(
                "hush-edge",
                "静寂の刃",
                RelicSlot.Weapon,
                "斬るたび周囲の音が一拍だけ消える。相手の防御姿勢を無視して斬る。",
                "戦いのあと、地面に残されていたのは音のしない一振りだった。"),
            new UniqueRelic(
                "returning-coat",
                "帰路の外套",
                RelicSlot.Armor,
                "着る者は必ず帰り道を見つける。倒れるはずの一撃を、1戦につき一度だけ耐える。",
                "外套は誰のものでもなかった。ただ、帰る方角だけを知っていた。"),
            new UniqueRelic(
                "duet-unison",
                "二重奏",
                RelicSlot.Technique,
                "連携技の発動条件を無視し、隣接していなくても二人で撃てるようになる。",
                "重なった呼吸が、そのまま技の名前になった。")
        };

        private static readonly OrdealEncounter[] Ordeals =
        {
            new OrdealEncounter(
                "ordeal-well",
                "井戸の底のもの",
                "覗き込んではいけないと言われていた",
                "night",
                3.5f,
                "hush-edge",
                OrdealFoeKind.SingleFoe,
                "e_boss",
                "井戸に向かって呼びかけた声が、少し遅れて、違う声で返ってきた。",
                "みんも「ケイハンさん。今の……返事、しましたよね」",
                "水面が盛り上がり、音という音が吸い込まれて消えた。"),
            new OrdealEncounter(
                "ordeal-mirror",
                "水鏡の隊列",
                "そこには、こちらと同じ数だけ立っていた",
                "castle",
                4.0f,
                "returning-coat",
                OrdealFoeKind.MirrorOfParty,
                null,
                "磨かれた床に映った自分たちが、こちらより一歩早く武器を構えた。",
                "記憶の射手「あれ、私……あんな顔してた？」",
                "ケイハン「違う。あれは、帰らなかった方の俺たちだ」"),
            new OrdealEncounter(
                "ordeal-encore",
                "鳴り止まぬ拍手",
                "誰もいない客席から、それは始まった",
                "throne",
                5.0f,
                "duet-unison",
                OrdealFoeKind.SingleFoe,
                "e_cleric",
                "楽師が最後の弦を弾き終えたとき、無人の広間から拍手が起きた。",
                "記憶の吟遊詩人「……やめて。この拍手、僕の演奏に合ってない」",
                "拍手は形をとり、アンコールを求めて立ち上がった。")
        };

        private static readonly StoryChoicePrompt[] Prompts =
        {
            // 第一章 — 場違いな選択肢は全て会話に合流する。まず安心させる。
            new StoryChoicePrompt(
                "town-well", 0,
                "町の広場。使われていない古い井戸の前を通りかかった。",
                new StoryChoiceOption(
                    "先を急ごう", false, StoryChoiceOutcome.Conversation, null,
                    "ケイハン「寄り道は帰りにしよう」",
                    "みんも「はい。日が高いうちに門を抜けましょう」"),
                new StoryChoiceOption(
                    "井戸を覗き込む", true, StoryChoiceOutcome.Conversation, null,
                    "ケイハン「……底が見えないな」",
                    "みんも「落ちないでくださいね。引き上げるの、大変ですから」",
                    "覗き込んでも、暗いだけだった。"),
                new StoryChoiceOption(
                    "井戸に向かって叫ぶ", true, StoryChoiceOutcome.Conversation, null,
                    "ケイハン「おーい」",
                    "みんも「子供みたいなことしないでください」",
                    "自分の声が、少しだけ遅れて返ってきた。")),

            // 第二章 — 同じ井戸に、夜、もう一度。ここで初めて試練になる。
            new StoryChoicePrompt(
                "town-well-night", 1,
                "夜。同じ井戸の前。昼に叫んだときより、水の音が近い気がする。",
                new StoryChoiceOption(
                    "何もせず通り過ぎる", false, StoryChoiceOutcome.Conversation, null,
                    "みんも「……行きましょう」",
                    "ケイハン「ああ。今日は、そういう気分じゃない」"),
                new StoryChoiceOption(
                    "もう一度叫んでみる", true, StoryChoiceOutcome.Ordeal, "ordeal-well",
                    "ケイハン「おーい」"),
                new StoryChoiceOption(
                    "石を投げ込む", true, StoryChoiceOutcome.Conversation, null,
                    "投げ込んだ石は、いつまでも音を立てなかった。",
                    "みんも「……戻りましょう。すぐに」")),

            // 第三章 — 破滅。ただし進行は失われない。
            new StoryChoicePrompt(
                "chapel-altar", 2,
                "礼拝堂の祭壇。触れてはいけないと衛兵に言われていた燭台がある。",
                new StoryChoiceOption(
                    "祈りだけ捧げる", false, StoryChoiceOutcome.Conversation, null,
                    "記憶の癒し手「……ここで待っていた人がいたんですね」",
                    "ケイハン「連れて帰ろう。全員で」"),
                new StoryChoiceOption(
                    "燭台の火を吹き消す", true, StoryChoiceOutcome.Downfall, null,
                    "ケイハン「これくらい、いいだろう」",
                    "火が消えた瞬間、堂内の空気が下から抜けた。",
                    "記憶の癒し手「だめ——！　その火、『帰り道』です！」"),
                new StoryChoiceOption(
                    "燭台を持ち帰ろうとする", true, StoryChoiceOutcome.Conversation, null,
                    "みんも「置いていってください」",
                    "ケイハン「……はい」")),

            // 第四章 — 試練その2。
            new StoryChoicePrompt(
                "castle-hall", 3,
                "城門をくぐった先の大広間。床がよく磨かれていて、天井まで映り込んでいる。",
                new StoryChoiceOption(
                    "隊列を組み直して進む", false, StoryChoiceOutcome.Conversation, null,
                    "ケイハン「前衛から順に。足元に気をつけて」",
                    "記憶の射手「床、滑りますね。……よく磨いてある」"),
                new StoryChoiceOption(
                    "床の自分に手を振る", true, StoryChoiceOutcome.Ordeal, "ordeal-mirror",
                    "ケイハン「……なあ、今、振り返すのが早くなかったか」"),
                new StoryChoiceOption(
                    "床を踏み鳴らしてみる", true, StoryChoiceOutcome.Conversation, null,
                    "みんも「音が、返ってきませんね」",
                    "ケイハン「石が厚いんだろう。……行こう」")),

            // 第五章 — 破滅その2。
            new StoryChoicePrompt(
                "camp-night", 4,
                "敗走した夜の野営。見張りの当番を決めるところで、誰かが妙なことを言い出した。",
                new StoryChoiceOption(
                    "交代で見張る", false, StoryChoiceOutcome.Conversation, null,
                    "ケイハン「二人ずつだ。一人にはしない」",
                    "みんも「……はい。それがいいです」"),
                new StoryChoiceOption(
                    "全員で寝る", true, StoryChoiceOutcome.Downfall, null,
                    "ケイハン「今夜くらい、全員休もう」",
                    "みんも「……そうですね。少しだけ」",
                    "目が覚めたとき、隊列は組めなかった。"),
                new StoryChoiceOption(
                    "一晩中しゃべっている", true, StoryChoiceOutcome.Conversation, null,
                    "記憶の吟遊詩人「じゃあ朝まで演奏していようか」",
                    "全員寝不足のまま朝を迎えたが、誰も欠けていなかった。")),

            // 最終章 — 試練その3。最後の銘器。
            new StoryChoicePrompt(
                "throne-encore", 5,
                "玉座の間。すべてが終わったあと、楽師が一曲だけ弾き終えた。",
                new StoryChoiceOption(
                    "帰り支度をする", false, StoryChoiceOutcome.Conversation, null,
                    "ケイハン「帰ろう。全員で」",
                    "みんも「はい。帰りましょう」"),
                new StoryChoiceOption(
                    "拍手を返す", true, StoryChoiceOutcome.Ordeal, "ordeal-encore",
                    "ケイハン「いい演奏だった」",
                    "拍手が、二人ぶんでは済まなかった。"),
                new StoryChoiceOption(
                    "アンコールを頼む", true, StoryChoiceOutcome.Conversation, null,
                    "記憶の吟遊詩人「一曲だけだよ。帰り道のぶんは、家で弾くから」"))
        };

        public static IReadOnlyList<StoryChoicePrompt> AllPrompts => Prompts;
        public static IReadOnlyList<OrdealEncounter> AllOrdeals => Ordeals;
        public static IReadOnlyList<UniqueRelic> AllRelics => Relics;

        /// <summary>その章で提示すべき選択肢。無ければ null。</summary>
        public static StoryChoicePrompt GetPrompt(int stageIndex)
        {
            return Prompts.FirstOrDefault(prompt => prompt.StageIndex == stageIndex);
        }

        public static StoryChoicePrompt FindPrompt(string promptId)
        {
            return Prompts.FirstOrDefault(prompt =>
                string.Equals(prompt.Id, promptId, StringComparison.Ordinal));
        }

        public static OrdealEncounter FindOrdeal(string ordealId)
        {
            return Ordeals.FirstOrDefault(ordeal =>
                string.Equals(ordeal.Id, ordealId, StringComparison.Ordinal));
        }

        public static UniqueRelic FindRelic(string relicId)
        {
            return Relics.FirstOrDefault(relic =>
                string.Equals(relic.Id, relicId, StringComparison.Ordinal));
        }

        /// <summary>
        /// 選択肢を1つ選んだときの結果を返す。乱数を使わないため、
        /// 同じ入力なら必ず同じ結果になる。
        /// </summary>
        public static StoryChoiceOption Resolve(string promptId, int optionIndex)
        {
            StoryChoicePrompt prompt = FindPrompt(promptId);
            if (prompt == null)
                throw new ArgumentException($"Unknown story choice prompt: {promptId}", nameof(promptId));
            if (optionIndex < 0 || optionIndex >= prompt.Options.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            return prompt.Options[optionIndex];
        }

        /// <summary>選択済みを表す保存用ID。</summary>
        public static string BuildChoiceRecordId(string promptId, int optionIndex)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("promptId is required.", nameof(promptId));
            if (optionIndex < 0) throw new ArgumentOutOfRangeException(nameof(optionIndex));
            return $"{ChoiceIdPrefix}{promptId}:{optionIndex}";
        }

        /// <summary>銘器の入手を表す保存用ID。</summary>
        public static string BuildRelicRecordId(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId))
                throw new ArgumentException("relicId is required.", nameof(relicId));
            return RelicIdPrefix + relicId;
        }

        /// <summary>その選択肢を一度でも選んだことがあるか。破滅した選択肢に印を出すのに使う。</summary>
        public static bool HasChosen(IEnumerable<string> resolvedIds, string promptId, int optionIndex)
        {
            string record = BuildChoiceRecordId(promptId, optionIndex);
            return Contains(resolvedIds, record);
        }

        public static bool HasRelic(IEnumerable<string> resolvedIds, string relicId)
        {
            return Contains(resolvedIds, BuildRelicRecordId(relicId));
        }

        /// <summary>入手済みの銘器を一覧で返す。所持品画面用。</summary>
        public static IReadOnlyList<UniqueRelic> GetOwnedRelics(IEnumerable<string> resolvedIds)
        {
            var owned = new HashSet<string>(resolvedIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            return Relics.Where(relic => owned.Contains(BuildRelicRecordId(relic.Id))).ToArray();
        }

        /// <summary>
        /// 試練に勝ったとき、追記すべきIDを返す。既に持っていれば空を返す。
        /// 銘器は唯一無二なので、二度目の入手は起こらない。
        /// </summary>
        public static IReadOnlyList<string> BuildOrdealVictoryRecords(
            IEnumerable<string> resolvedIds,
            string ordealId)
        {
            OrdealEncounter ordeal = FindOrdeal(ordealId);
            if (ordeal == null)
                throw new ArgumentException($"Unknown ordeal: {ordealId}", nameof(ordealId));

            string relicRecord = BuildRelicRecordId(ordeal.RelicId);
            return Contains(resolvedIds, relicRecord)
                ? Array.Empty<string>()
                : new[] { relicRecord };
        }

        /// <summary>
        /// 破滅から復帰するときに保存へ書き足すID。
        /// **進行そのものは一切変更しない。** 記録するのは「この選択肢は選んだことがある」ことだけで、
        /// 再開時に同じ選択肢へ戻り、既読の印を出すために使う。
        /// </summary>
        public static IReadOnlyList<string> BuildDownfallRecords(string promptId, int optionIndex)
        {
            return new[] { BuildChoiceRecordId(promptId, optionIndex) };
        }

        /// <summary>その場面を決着させたことを表す保存用ID。</summary>
        public static string BuildSettledRecordId(string promptId)
        {
            if (string.IsNullOrWhiteSpace(promptId))
                throw new ArgumentException("promptId is required.", nameof(promptId));
            return SettledIdPrefix + promptId;
        }

        /// <summary>
        /// その場面が決着済みか。決着していれば二度と選択肢は出ない。
        /// 破滅では決着扱いにならないため、何度でも同じ場面に戻ってこられる。
        /// </summary>
        public static bool HasSettled(IEnumerable<string> resolvedIds, string promptId)
        {
            return Contains(resolvedIds, BuildSettledRecordId(promptId));
        }

        /// <summary>
        /// 選択の結果としてセーブへ追記すべきIDを返す。
        ///
        /// 破滅と試練開始時は決着マーカーを付けない。これが「直前からやり直せる」の実体で、
        /// 試練は勝利後にだけ決着済みとして保存する。
        /// 進行に関わる値（stageIndex / maxUnlocked）には一切触れない。
        /// </summary>
        public static IReadOnlyList<string> BuildResolutionRecords(string promptId, int optionIndex)
        {
            StoryChoiceOption option = Resolve(promptId, optionIndex);
            string chosen = BuildChoiceRecordId(promptId, optionIndex);

            return option.Outcome == StoryChoiceOutcome.Downfall ||
                   option.Outcome == StoryChoiceOutcome.Ordeal
                ? new[] { chosen }
                : new[] { chosen, BuildSettledRecordId(promptId) };
        }

        /// <summary>その章でまだ提示すべき選択肢。決着済みなら null。</summary>
        public static StoryChoicePrompt GetPendingPrompt(int stageIndex, IEnumerable<string> resolvedIds)
        {
            StoryChoicePrompt prompt = GetPrompt(stageIndex);
            if (prompt == null) return null;
            return HasSettled(resolvedIds, prompt.Id) ? null : prompt;
        }

        private static bool Contains(IEnumerable<string> resolvedIds, string value)
        {
            if (resolvedIds == null) return false;
            foreach (string id in resolvedIds)
                if (string.Equals(id, value, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
