using System;
using System.Collections.Generic;
using System.Linq;

using BirthdayTactics.Core;
using NUnit.Framework;

namespace BirthdayTactics.Tests
{
    /// <summary>
    /// 選択肢システムの契約を固定する。
    ///
    /// 最重要なのは「銘器に必ずたどり着けること」と「破滅しても進行が消えないこと」の2点。
    /// どちらも贈り物としての体験に直結するため、作劇を変えたときに壊れたら気付けるようにする。
    /// </summary>
    public sealed class StoryChoiceCoreTests
    {
        [Test]
        public void EveryPrompt_HasUniqueIdAndAtLeastOneSafeOption()
        {
            IReadOnlyList<StoryChoicePrompt> prompts = StoryChoicePolicy.AllPrompts;

            Assert.That(
                prompts.Select(prompt => prompt.Id).Distinct().Count(),
                Is.EqualTo(prompts.Count),
                "選択肢IDが重複しています。");

            foreach (StoryChoicePrompt prompt in prompts)
            {
                Assert.That(prompt.Options.Count, Is.InRange(2, 4), prompt.Id);

                // 「普通に進むだけの選択肢」が必ず1つ以上あること。
                // これが無いと、場違いな選択肢しか無い理不尽な場面になる。
                Assert.That(
                    prompt.Options.Any(option =>
                        !option.IsAbsurd && option.Outcome == StoryChoiceOutcome.Conversation),
                    Is.True,
                    $"{prompt.Id} に安全な選択肢がありません。");

                // 安全な選択肢が試練や破滅につながってはいけない。
                foreach (StoryChoiceOption option in prompt.Options.Where(o => !o.IsAbsurd))
                {
                    Assert.That(
                        option.Outcome,
                        Is.EqualTo(StoryChoiceOutcome.Conversation),
                        $"{prompt.Id} の通常選択肢が {option.Outcome} になっています。");
                }

                foreach (StoryChoiceOption option in prompt.Options)
                {
                    Assert.That(option.Text, Is.Not.Null.And.Not.Empty, prompt.Id);
                    Assert.That(option.Lines.Count, Is.GreaterThan(0), $"{prompt.Id} / {option.Text}");
                }
            }
        }

        /// <summary>
        /// 「まれ」であることを配合で表現しているので、その配合を固定する。
        /// 場違いな選択肢の大半が普通の会話に合流しないと、選ぶこと自体が怖くなる。
        /// </summary>
        [Test]
        public void AbsurdOptions_AreMostlyHarmless()
        {
            StoryChoiceOption[] absurd = StoryChoicePolicy.AllPrompts
                .SelectMany(prompt => prompt.Options)
                .Where(option => option.IsAbsurd)
                .ToArray();

            int conversation = absurd.Count(o => o.Outcome == StoryChoiceOutcome.Conversation);
            int ordeal = absurd.Count(o => o.Outcome == StoryChoiceOutcome.Ordeal);
            int downfall = absurd.Count(o => o.Outcome == StoryChoiceOutcome.Downfall);

            Assert.That(absurd.Length, Is.GreaterThanOrEqualTo(8), "場違いな選択肢が少なすぎます。");
            Assert.That(
                conversation / (float)absurd.Length,
                Is.GreaterThanOrEqualTo(0.5f),
                "場違いな選択肢の半分以上は普通の会話に合流すること。");
            Assert.That(ordeal, Is.EqualTo(StoryChoicePolicy.AllOrdeals.Count), "試練へ至る導線の数が合いません。");
            Assert.That(downfall, Is.GreaterThan(0), "破滅がひとつも無いと緊張感が出ません。");
        }

        [Test]
        public void Resolve_IsDeterministicAndRejectsUnknownInput()
        {
            foreach (StoryChoicePrompt prompt in StoryChoicePolicy.AllPrompts)
            {
                for (int index = 0; index < prompt.Options.Count; index++)
                {
                    StoryChoiceOption first = StoryChoicePolicy.Resolve(prompt.Id, index);
                    StoryChoiceOption second = StoryChoicePolicy.Resolve(prompt.Id, index);
                    Assert.That(first, Is.SameAs(second), $"{prompt.Id}:{index} が決定的ではありません。");
                }

                Assert.That(
                    () => StoryChoicePolicy.Resolve(prompt.Id, prompt.Options.Count),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            }

            Assert.That(
                () => StoryChoicePolicy.Resolve("does-not-exist", 0),
                Throws.TypeOf<ArgumentException>());
        }

        /// <summary>
        /// すべての銘器が、実際にたどり着ける試練に紐づいていること。
        /// 到達不能な報酬が混ざると、作った意味が無くなる。
        /// </summary>
        [Test]
        public void EveryRelic_IsReachableThroughExactlyOneOrdeal()
        {
            foreach (UniqueRelic relic in StoryChoicePolicy.AllRelics)
            {
                OrdealEncounter[] sources = StoryChoicePolicy.AllOrdeals
                    .Where(ordeal => string.Equals(ordeal.RelicId, relic.Id, StringComparison.Ordinal))
                    .ToArray();

                Assert.That(sources.Length, Is.EqualTo(1), $"{relic.Id} の入手経路が1つではありません。");

                OrdealEncounter ordeal = sources[0];
                bool reachable = StoryChoicePolicy.AllPrompts
                    .SelectMany(prompt => prompt.Options)
                    .Any(option =>
                        option.Outcome == StoryChoiceOutcome.Ordeal &&
                        string.Equals(option.OrdealId, ordeal.Id, StringComparison.Ordinal));

                Assert.That(reachable, Is.True, $"{ordeal.Id} へ至る選択肢がありません。");
            }

            // 銘器の区分が偏っていないこと（武器・防具・技が揃っている）。
            Assert.That(
                StoryChoicePolicy.AllRelics.Select(relic => relic.Slot).Distinct().Count(),
                Is.EqualTo(3));
        }

        [Test]
        public void EveryOrdeal_IsHarderThanTheFinalStageAndHasIntro()
        {
            foreach (OrdealEncounter ordeal in StoryChoicePolicy.AllOrdeals)
            {
                Assert.That(
                    ordeal.PowerMultiplier,
                    Is.GreaterThanOrEqualTo(3f),
                    $"{ordeal.Id} が『あり得ない強さ』になっていません。");
                Assert.That(ordeal.IntroLines.Count, Is.GreaterThan(0), ordeal.Id);
                Assert.That(StoryChoicePolicy.FindRelic(ordeal.RelicId), Is.Not.Null, ordeal.Id);
            }

            Assert.That(
                () => StoryChoicePolicy.BuildOrdealVictoryRecords(Array.Empty<string>(), "no-such-ordeal"),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void OrdealVictory_GrantsTheRelicOnceAndNeverAgain()
        {
            OrdealEncounter ordeal = StoryChoicePolicy.AllOrdeals[0];
            var saved = new List<string>();

            IReadOnlyList<string> first =
                StoryChoicePolicy.BuildOrdealVictoryRecords(saved, ordeal.Id);
            Assert.That(first.Count, Is.EqualTo(1));
            saved.AddRange(first);

            Assert.That(StoryChoicePolicy.HasRelic(saved, ordeal.RelicId), Is.True);

            // 二度目の撃破では何も増えない（唯一無二であること）。
            IReadOnlyList<string> second =
                StoryChoicePolicy.BuildOrdealVictoryRecords(saved, ordeal.Id);
            Assert.That(second, Is.Empty);

            IReadOnlyList<UniqueRelic> owned = StoryChoicePolicy.GetOwnedRelics(saved);
            Assert.That(owned.Count, Is.EqualTo(1));
            Assert.That(owned[0].Id, Is.EqualTo(ordeal.RelicId));
        }

        /// <summary>
        /// 破滅しても、記録されるのは「選んだことがある」という事実だけで、
        /// 進行に関わるIDは一切増えない。これが「直前からやり直せる」の根拠になる。
        /// </summary>
        [Test]
        public void Downfall_RecordsOnlyTheChoiceAndNeverTouchesProgress()
        {
            StoryChoicePrompt prompt = StoryChoicePolicy.AllPrompts
                .First(candidate => candidate.Options.Any(o => o.Outcome == StoryChoiceOutcome.Downfall));
            int index = prompt.Options
                .Select((option, i) => (option, i))
                .First(pair => pair.option.Outcome == StoryChoiceOutcome.Downfall).i;

            IReadOnlyList<string> records = StoryChoicePolicy.BuildDownfallRecords(prompt.Id, index);

            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0], Does.StartWith(StoryChoicePolicy.ChoiceIdPrefix));
            Assert.That(
                records[0],
                Does.Not.StartWith(StoryChoicePolicy.RelicIdPrefix),
                "破滅で銘器が入ってはいけません。");

            // 記録後は既読の印が立ち、同じ選択肢へ戻ったときに警告を出せる。
            Assert.That(StoryChoicePolicy.HasChosen(records, prompt.Id, index), Is.True);
            Assert.That(StoryChoicePolicy.HasChosen(records, prompt.Id, index + 1), Is.False);
        }

        [Test]
        public void Ordeal_DoesNotSettleUntilVictorySoItCanBeRetried()
        {
            StoryChoicePrompt prompt = StoryChoicePolicy.AllPrompts
                .First(candidate => candidate.Options.Any(o => o.Outcome == StoryChoiceOutcome.Ordeal));
            int index = prompt.Options
                .Select((option, i) => (option, i))
                .First(pair => pair.option.Outcome == StoryChoiceOutcome.Ordeal).i;

            IReadOnlyList<string> records =
                StoryChoicePolicy.BuildResolutionRecords(prompt.Id, index);

            Assert.That(StoryChoicePolicy.HasChosen(records, prompt.Id, index), Is.True);
            Assert.That(StoryChoicePolicy.HasSettled(records, prompt.Id), Is.False);
            Assert.That(
                StoryChoicePolicy.GetPendingPrompt(prompt.StageIndex, records),
                Is.SameAs(prompt),
                "試練の中断・敗北後も同じ選択場面へ戻れる必要があります。");

            string[] victoryRecords = records
                .Append(StoryChoicePolicy.BuildSettledRecordId(prompt.Id))
                .ToArray();
            Assert.That(
                StoryChoicePolicy.GetPendingPrompt(prompt.StageIndex, victoryRecords),
                Is.Null,
                "試練に勝った後だけ選択場面を決着済みにします。");
        }

        [Test]
        public void Conversation_StillSettlesImmediately()
        {
            StoryChoicePrompt prompt = StoryChoicePolicy.AllPrompts
                .First(candidate => candidate.Options.Any(o => o.Outcome == StoryChoiceOutcome.Conversation));
            int index = prompt.Options
                .Select((option, i) => (option, i))
                .First(pair => pair.option.Outcome == StoryChoiceOutcome.Conversation).i;

            IReadOnlyList<string> records =
                StoryChoicePolicy.BuildResolutionRecords(prompt.Id, index);

            Assert.That(StoryChoicePolicy.HasChosen(records, prompt.Id, index), Is.True);
            Assert.That(StoryChoicePolicy.HasSettled(records, prompt.Id), Is.True);
            Assert.That(
                StoryChoicePolicy.GetPendingPrompt(prompt.StageIndex, records),
                Is.Null,
                "通常会話は従来どおり一度で決着し、同じ選択場面を繰り返しません。");
        }

        /// <summary>
        /// 保存IDは既存の resolvedStoryEntityIds に相乗りするため、
        /// 既存のストーリーIDと衝突しないことを保証する。
        /// </summary>
        [Test]
        public void RecordIds_DoNotCollideWithExistingStoryEntityIds()
        {
            string choiceRecord = StoryChoicePolicy.BuildChoiceRecordId("town-well", 1);
            string relicRecord = StoryChoicePolicy.BuildRelicRecordId("hush-edge");

            Assert.That(choiceRecord, Is.EqualTo("choice:town-well:1"));
            Assert.That(relicRecord, Is.EqualTo("relic:hush-edge"));

            // 既存のIDは接頭辞を持たない素の文字列なので、衝突しない。
            foreach (string existing in new[]
                     {
                         "town-smith", "dungeon-memory-archer", "inn-minstrel", "chapter-story-s2"
                     })
            {
                Assert.That(existing, Does.Not.StartWith(StoryChoicePolicy.ChoiceIdPrefix));
                Assert.That(existing, Does.Not.StartWith(StoryChoicePolicy.RelicIdPrefix));
            }

            Assert.That(
                () => StoryChoicePolicy.BuildChoiceRecordId(" ", 0),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => StoryChoicePolicy.BuildRelicRecordId(null),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void GetPrompt_CoversEveryStageOfTheCampaign()
        {
            for (int stageIndex = 0; stageIndex <= 5; stageIndex++)
            {
                StoryChoicePrompt prompt = StoryChoicePolicy.GetPrompt(stageIndex);
                Assert.That(prompt, Is.Not.Null, $"第{stageIndex + 1}戦に選択肢がありません。");
                Assert.That(prompt.StageIndex, Is.EqualTo(stageIndex));
                Assert.That(prompt.Situation, Is.Not.Null.And.Not.Empty);
            }

            Assert.That(StoryChoicePolicy.GetPrompt(99), Is.Null);
        }
    }
}
