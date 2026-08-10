using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum WarOrder
    {
        Assault,
        Hold,
        Support
    }

    public enum WarWinner
    {
        None,
        Player,
        Enemy
    }

    public sealed class WarLaneState
    {
        public string Id { get; }
        public string Name { get; }
        public int PlayerStrength { get; internal set; }
        public int EnemyStrength { get; internal set; }
        public int Control { get; internal set; }
        public WarOrder PlayerOrder { get; internal set; }
        internal WarOrder EnemyIntent { get; set; }
        public bool IsIntentRevealed { get; internal set; }
        public WarOrder? RevealedEnemyIntent => IsIntentRevealed
            ? EnemyIntent
            : (WarOrder?)null;

        internal WarLaneState(
            string id,
            string name,
            int playerStrength,
            int enemyStrength,
            WarOrder playerOrder)
        {
            Id = id;
            Name = name;
            PlayerStrength = playerStrength;
            EnemyStrength = enemyStrength;
            PlayerOrder = playerOrder;
        }
    }

    public sealed class WarRoundReport
    {
        public int Round { get; }
        public int PlayerLosses { get; }
        public int EnemyLosses { get; }
        public int ControlShift { get; }
        public WarWinner Winner { get; }

        internal WarRoundReport(
            int round,
            int playerLosses,
            int enemyLosses,
            int controlShift,
            WarWinner winner)
        {
            Round = round;
            PlayerLosses = playerLosses;
            EnemyLosses = enemyLosses;
            ControlShift = controlShift;
            Winner = winner;
        }
    }

    /// <summary>
    /// Three-front deterministic war battle with telegraphed enemy intentions.
    /// Assault beats Support, Support beats Hold, and Hold beats Assault.
    /// </summary>
    public sealed class WarCampaignCore
    {
        private readonly WarLaneState[] _lanes;
        private readonly int _difficulty;
        private readonly int _scoutsPerRound;

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<WarLaneState> Lanes => _lanes;
        public int Round { get; private set; }
        public WarWinner Winner { get; private set; }
        public int MaxRounds => 8;
        public int ScoutsRemaining { get; private set; }

        public WarCampaignCore(WarmapCatalogData data, int scoutsPerRound = 1)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (scoutsPerRound < 1 || scoutsPerRound > 3)
                throw new ArgumentOutOfRangeException(nameof(scoutsPerRound));
            Id = string.IsNullOrWhiteSpace(data.id) ? "war" : data.id;
            DisplayName = string.IsNullOrWhiteSpace(data.displayName) ? "戦役" : data.displayName;
            _difficulty = DifficultyValue(data.difficulty);
            _scoutsPerRound = scoutsPerRound;
            int enemyBase = 54 + _difficulty * 8;
            _lanes = new[]
            {
                new WarLaneState("north", "北方戦線", 78, enemyBase + 4, WarOrder.Assault),
                new WarLaneState("center", "中央戦線", 84, enemyBase + 8, WarOrder.Hold),
                new WarLaneState("south", "南方戦線", 74, enemyBase, WarOrder.Support)
            };
            RefreshEnemyIntent();
        }

        public void SetOrder(int laneIndex, WarOrder order)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Length)
                throw new ArgumentOutOfRangeException(nameof(laneIndex));
            if (!Enum.IsDefined(typeof(WarOrder), order))
                throw new ArgumentOutOfRangeException(nameof(order));
            _lanes[laneIndex].PlayerOrder = order;
        }

        public WarOrder CycleOrder(int laneIndex)
        {
            WarOrder next = (WarOrder)(((int)_lanes[laneIndex].PlayerOrder + 1) % 3);
            SetOrder(laneIndex, next);
            return next;
        }

        public WarOrder CounterOrder(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Length)
                throw new ArgumentOutOfRangeException(nameof(laneIndex));
            WarLaneState lane = _lanes[laneIndex];
            if (!lane.IsIntentRevealed)
                throw new InvalidOperationException("Enemy intent must be revealed before selecting its counter.");
            WarOrder intent = lane.EnemyIntent;
            if (intent == WarOrder.Assault) return WarOrder.Hold;
            if (intent == WarOrder.Hold) return WarOrder.Support;
            return WarOrder.Assault;
        }

        public bool RevealIntent(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Length)
                throw new ArgumentOutOfRangeException(nameof(laneIndex));
            if (Winner != WarWinner.None || ScoutsRemaining <= 0 || _lanes[laneIndex].IsIntentRevealed)
                return false;
            _lanes[laneIndex].IsIntentRevealed = true;
            ScoutsRemaining--;
            return true;
        }

        public WarRoundReport AdvanceRound()
        {
            if (Winner != WarWinner.None) return null;
            Round++;
            int playerLosses = 0;
            int enemyLosses = 0;
            int controlBefore = _lanes.Sum(lane => lane.Control);
            WarOrder[] enemyOrders = _lanes.Select(lane => lane.EnemyIntent).ToArray();
            int[] playerSupport = SupportBonuses(_lanes.Select(lane => lane.PlayerOrder).ToArray());
            int[] enemySupport = SupportBonuses(enemyOrders);

            for (int i = 0; i < _lanes.Length; i++)
            {
                WarLaneState lane = _lanes[i];
                int playerPower = 8 + lane.PlayerStrength / 18 + playerSupport[i] +
                                  Advantage(lane.PlayerOrder, enemyOrders[i]);
                int enemyPower = 7 + _difficulty + lane.EnemyStrength / 20 + enemySupport[i] +
                                 Advantage(enemyOrders[i], lane.PlayerOrder);
                int playerLoss = Math.Min(lane.PlayerStrength, Math.Max(2, enemyPower));
                int enemyLoss = Math.Min(lane.EnemyStrength, Math.Max(2, playerPower));
                lane.PlayerStrength -= playerLoss;
                lane.EnemyStrength -= enemyLoss;
                playerLosses += playerLoss;
                enemyLosses += enemyLoss;

                int powerDifference = playerPower - enemyPower;
                if (lane.EnemyStrength == 0 || powerDifference >= 3) lane.Control++;
                else if (lane.PlayerStrength == 0 || powerDifference <= -3) lane.Control--;
                lane.Control = Clamp(lane.Control, -2, 2);
            }

            UpdateWinner();
            if (Winner == WarWinner.None) RefreshEnemyIntent();
            return new WarRoundReport(
                Round,
                playerLosses,
                enemyLosses,
                _lanes.Sum(lane => lane.Control) - controlBefore,
                Winner);
        }

        private void RefreshEnemyIntent()
        {
            for (int i = 0; i < _lanes.Length; i++)
            {
                _lanes[i].EnemyIntent = (WarOrder)((Round + i + _difficulty) % 3);
                _lanes[i].IsIntentRevealed = false;
            }
            ScoutsRemaining = _scoutsPerRound;
        }

        private void UpdateWinner()
        {
            int control = _lanes.Sum(lane => lane.Control);
            int playerStrength = _lanes.Sum(lane => lane.PlayerStrength);
            int enemyStrength = _lanes.Sum(lane => lane.EnemyStrength);
            if (enemyStrength == 0 || (Round >= 3 && control >= 5))
                Winner = WarWinner.Player;
            else if (playerStrength == 0 || (Round >= 3 && control <= -5))
                Winner = WarWinner.Enemy;
            else if (Round >= MaxRounds)
                Winner = playerStrength + control * 12 >= enemyStrength
                    ? WarWinner.Player
                    : WarWinner.Enemy;
        }

        private static int[] SupportBonuses(IReadOnlyList<WarOrder> orders)
        {
            var bonuses = new int[orders.Count];
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != WarOrder.Support) continue;
                if (i > 0) bonuses[i - 1] += 3;
                if (i < orders.Count - 1) bonuses[i + 1] += 3;
            }
            return bonuses;
        }

        private static int Advantage(WarOrder attacker, WarOrder defender)
        {
            bool wins = attacker == WarOrder.Assault && defender == WarOrder.Support ||
                        attacker == WarOrder.Support && defender == WarOrder.Hold ||
                        attacker == WarOrder.Hold && defender == WarOrder.Assault;
            return wins ? 7 : 0;
        }

        private static int DifficultyValue(string difficulty)
        {
            if (string.Equals(difficulty, "hard", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(difficulty, "normal", StringComparison.OrdinalIgnoreCase)) return 2;
            return 1;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
