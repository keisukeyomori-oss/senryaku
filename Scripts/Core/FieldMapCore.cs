using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayTactics.Core
{
    public enum FieldNodeKind
    {
        Camp,
        Road,
        Landmark,
        Enemy,
        Fortress
    }

    public enum FieldMoveResult
    {
        Blocked,
        Moved,
        Encounter
    }

    public sealed class FieldNode
    {
        public int Index { get; }
        public string Name { get; }
        public FieldNodeKind Kind { get; }
        public float X { get; }
        public float Y { get; }
        public int Threat { get; }
        public int StageIndex { get; }
        public IReadOnlyList<int> Connections { get; }

        internal FieldNode(
            int index,
            string name,
            FieldNodeKind kind,
            float x,
            float y,
            int threat,
            int stageIndex,
            params int[] connections)
        {
            Index = index;
            Name = name ?? string.Empty;
            Kind = kind;
            X = x;
            Y = y;
            Threat = Math.Max(0, threat);
            StageIndex = stageIndex;
            Connections = Array.AsReadOnly(connections ?? Array.Empty<int>());
        }
    }

    /// <summary>
    /// Deterministic world-map traversal. Encounters are visible destinations and never random.
    /// </summary>
    public sealed class FieldMapCore
    {
        private readonly FieldNode[] _nodes;

        public IReadOnlyList<FieldNode> Nodes => _nodes;
        public int PlayerNodeIndex { get; private set; }
        public int MoveCount { get; private set; }
        public int StageIndex { get; }
        public FieldNode CurrentNode => _nodes[PlayerNodeIndex];
        public FieldNode EncounterNode => _nodes.First(node => node.Kind == FieldNodeKind.Enemy);

        private FieldMapCore(int stageIndex, int startNodeIndex)
        {
            StageIndex = Math.Max(0, stageIndex);
            int threat = StageIndex + 1;
            _nodes = new[]
            {
                new FieldNode(0, "旅団野営地", FieldNodeKind.Camp, 0.09f, 0.72f, 0, -1, 1, 2),
                new FieldNode(1, "風見街道", FieldNodeKind.Road, 0.25f, 0.48f, 0, -1, 0, 3),
                new FieldNode(2, "古い渡し場", FieldNodeKind.Landmark, 0.27f, 0.82f, 0, -1, 0, 3, 4),
                new FieldNode(3, "白樺の森", FieldNodeKind.Landmark, 0.46f, 0.43f, 0, -1, 1, 2, 5),
                new FieldNode(4, "見張り丘", FieldNodeKind.Landmark, 0.50f, 0.78f, 0, -1, 2, 5),
                new FieldNode(5, "崩れた関所", FieldNodeKind.Road, 0.68f, 0.58f, 0, -1, 3, 4, 6),
                new FieldNode(6, "敵部隊", FieldNodeKind.Enemy, 0.84f, 0.42f, threat, StageIndex, 5, 7),
                new FieldNode(7, "城塞への道", FieldNodeKind.Fortress, 0.91f, 0.74f, threat + 1, -1, 6)
            };
            PlayerNodeIndex = startNodeIndex >= 0 && startNodeIndex < _nodes.Length
                ? startNodeIndex
                : 0;
        }

        public static FieldMapCore Create(int stageIndex, int startNodeIndex = 0)
        {
            return new FieldMapCore(stageIndex, startNodeIndex);
        }

        public bool CanMoveTo(int nodeIndex)
        {
            return nodeIndex >= 0 &&
                   nodeIndex < _nodes.Length &&
                   CurrentNode.Connections.Contains(nodeIndex);
        }

        public FieldMoveResult MoveTo(int nodeIndex)
        {
            if (!CanMoveTo(nodeIndex)) return FieldMoveResult.Blocked;
            PlayerNodeIndex = nodeIndex;
            MoveCount++;
            return CurrentNode.Kind == FieldNodeKind.Enemy
                ? FieldMoveResult.Encounter
                : FieldMoveResult.Moved;
        }

        public int NextNodeTowardEncounter()
        {
            int target = EncounterNode.Index;
            if (PlayerNodeIndex == target) return target;

            var queue = new Queue<int>();
            var previous = new Dictionary<int, int> { [PlayerNodeIndex] = -1 };
            queue.Enqueue(PlayerNodeIndex);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in _nodes[current].Connections.OrderBy(index => index))
                {
                    if (previous.ContainsKey(next)) continue;
                    previous[next] = current;
                    if (next == target)
                    {
                        int step = target;
                        while (previous[step] != PlayerNodeIndex) step = previous[step];
                        return step;
                    }
                    queue.Enqueue(next);
                }
            }
            return PlayerNodeIndex;
        }

        public FieldMoveResult AdvanceTowardEncounter()
        {
            if (CurrentNode.Kind == FieldNodeKind.Enemy) return FieldMoveResult.Encounter;
            int next = NextNodeTowardEncounter();
            return next == PlayerNodeIndex ? FieldMoveResult.Blocked : MoveTo(next);
        }
    }
}
