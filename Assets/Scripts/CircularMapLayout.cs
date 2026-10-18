using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace OdysseyOracle
{
    public enum CircularMapNodeType { EmptyBiome, PathSpoke, TowerNode, CenterCore, Portal }

    /// <summary>Immutable data format for shared maps; parsing never changes the live dungeon.</summary>
    public sealed class CircularMapLayout
    {
        public const int MaximumNodes = 41;
        public const int MaximumInputLength = 4096;
        public const int MaximumRing = 5;
        public const int SpokeCount = 8;
        public const int MaximumTowersPerLane = 3;

        public readonly struct Node
        {
            public int Ring { get; }
            public int Spoke { get; }
            public CircularMapNodeType Type { get; }
            public Node(int ring, int spoke, CircularMapNodeType type) { Ring = ring; Spoke = spoke; Type = type; }
        }

        private readonly Node[] nodes;
        private readonly ReadOnlyCollection<Node> view;
        private readonly int[] coordinateIndex;
        public IReadOnlyList<Node> Nodes => view;
        public int Count => nodes.Length;

        private CircularMapLayout(Node[] validatedNodes)
        {
            nodes = (Node[])validatedNodes.Clone();
            Array.Sort(nodes, (a, b) => a.Ring != b.Ring ? a.Ring.CompareTo(b.Ring) : a.Spoke.CompareTo(b.Spoke));
            view = Array.AsReadOnly(nodes);
            coordinateIndex = new int[(MaximumRing + 1) * SpokeCount];
            for (var i = 0; i < coordinateIndex.Length; i++) coordinateIndex[i] = -1;
            for (var i = 0; i < nodes.Length; i++) coordinateIndex[nodes[i].Ring * SpokeCount + nodes[i].Spoke] = i;
        }

        public bool TryGetNode(int ring, int spoke, out Node node)
        {
            node = default;
            if (ring < 0 || ring > MaximumRing || spoke < 0 || spoke >= SpokeCount) return false;
            var index = coordinateIndex[ring * SpokeCount + spoke];
            if (index < 0) return false;
            node = nodes[index];
            return true;
        }

        public int TowerCountInLane(int spoke)
        {
            if (spoke < 0 || spoke >= SpokeCount) return 0;
            var count = 0;
            for (var ring = 1; ring < MaximumRing; ring++)
                if (TryGetNode(ring, spoke, out var node) && node.Type == CircularMapNodeType.TowerNode) count++;
            return count;
        }

        /// <summary>Returns a new validated layout; protected cells and failed placements leave this instance unchanged.</summary>
        public bool TryWithTowerNode(int ring, int spoke, out CircularMapLayout result, out string error)
        {
            result = null;
            error = "";
            if (ring < 1 || ring >= MaximumRing || spoke < 0 || spoke >= SpokeCount)
            { error = "Towers need a cell on rings 1–4 and spokes 0–7."; return false; }
            var exists = TryGetNode(ring, spoke, out var old);
            if (exists && (old.Type == CircularMapNodeType.CenterCore || old.Type == CircularMapNodeType.Portal))
            { error = "A tower cannot replace the center core or a portal."; return false; }
            if (exists && old.Type == CircularMapNodeType.TowerNode) { result = this; return true; }
            if (TowerCountInLane(spoke) >= MaximumTowersPerLane)
            { error = "A lane can contain at most three tower nodes."; return false; }
            if (!exists && nodes.Length >= MaximumNodes)
            { error = "This map already contains 41 nodes."; return false; }
            var replacement = new Node[nodes.Length + (exists ? 0 : 1)];
            Array.Copy(nodes, replacement, nodes.Length);
            var index = exists ? coordinateIndex[ring * SpokeCount + spoke] : nodes.Length;
            replacement[index] = new Node(ring, spoke, CircularMapNodeType.TowerNode);
            result = new CircularMapLayout(replacement);
            return true;
        }

        public string ExportCanonical()
        {
            var text = new StringBuilder(nodes.Length * 21);
            for (var i = 0; i < nodes.Length; i++)
            {
                if (i > 0) text.Append('|');
                text.Append(nodes[i].Ring).Append(',').Append(nodes[i].Spoke).Append(',').Append(nodes[i].Type);
            }
            return text.ToString();
        }

        public static bool TryParse(string text, out CircularMapLayout result, out string error)
        {
            result = null;
            error = "";
            if (string.IsNullOrEmpty(text)) { error = "Enter a map layout."; return false; }
            if (text.Length > MaximumInputLength) { error = "Map text exceeds the 4096-character limit."; return false; }
            if (string.IsNullOrWhiteSpace(text)) { error = "Enter a map layout."; return false; }
            for (var i = 0; i < text.Length; i++)
                if (text[i] > 127 || (char.IsControl(text[i]) && text[i] != '\r' && text[i] != '\n' && text[i] != '\t'))
                { error = "Map text contains an unsupported character."; return false; }
            var entries = text.Split('|');
            if (entries.Length > MaximumNodes) { error = "A map can contain at most 41 nodes."; return false; }
            var parsed = new Node[entries.Length];
            var used = new bool[(MaximumRing + 1) * SpokeCount];
            var towers = new int[SpokeCount];
            var coreCount = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                var fields = entries[i].Split(',');
                if (fields.Length != 3) { error = "Node " + (i + 1) + " must be ring,spoke,NodeType."; return false; }
                if (!TryIndex(fields[0], MaximumRing, out var ring) || !TryIndex(fields[1], SpokeCount - 1, out var spoke))
                { error = "Node " + (i + 1) + " needs a ring 0–5 and a spoke 0–7."; return false; }
                if (!TryType(fields[2].Trim(), out var type))
                { error = "Node " + (i + 1) + " has an unknown node type."; return false; }
                if (ring == 0 && (spoke != 0 || type != CircularMapNodeType.CenterCore))
                { error = "Ring 0 is reserved for 0,0,CenterCore."; return false; }
                if (type == CircularMapNodeType.CenterCore && (ring != 0 || spoke != 0))
                { error = "CenterCore must be at ring 0, spoke 0."; return false; }
                if (type == CircularMapNodeType.Portal && ring != MaximumRing)
                { error = "Portals must be on ring 5."; return false; }
                if ((type == CircularMapNodeType.PathSpoke || type == CircularMapNodeType.TowerNode) && (ring < 1 || ring >= MaximumRing))
                { error = "Paths and towers must be on rings 1–4."; return false; }
                var coordinate = ring * SpokeCount + spoke;
                if (used[coordinate]) { error = "Two nodes use ring " + ring + ", spoke " + spoke + "."; return false; }
                used[coordinate] = true;
                if (type == CircularMapNodeType.CenterCore) coreCount++;
                if (type == CircularMapNodeType.TowerNode && ++towers[spoke] > MaximumTowersPerLane)
                { error = "Spoke " + spoke + " contains more than three tower nodes."; return false; }
                parsed[i] = new Node(ring, spoke, type);
            }
            if (coreCount != 1) { error = "A map needs exactly one 0,0,CenterCore."; return false; }
            result = new CircularMapLayout(parsed);
            return true;
        }

        /// <summary>Eight-spoke data template. Rendering and active lane selection remain separate.</summary>
        public static CircularMapLayout CreateDefault()
        {
            var template = new Node[MaximumNodes];
            template[0] = new Node(0, 0, CircularMapNodeType.CenterCore);
            var index = 1;
            for (var ring = 1; ring <= MaximumRing; ring++)
                for (var spoke = 0; spoke < SpokeCount; spoke++)
                    template[index++] = new Node(ring, spoke, ring == MaximumRing ? CircularMapNodeType.Portal : CircularMapNodeType.PathSpoke);
            return new CircularMapLayout(template);
        }

        private static bool TryIndex(string value, int maximum, out int result)
        {
            value = value.Trim();
            result = value.Length == 1 && value[0] >= '0' && value[0] <= '9' ? value[0] - '0' : -1;
            return result >= 0 && result <= maximum;
        }

        private static bool TryType(string value, out CircularMapNodeType type)
        {
            switch (value)
            {
                case "EmptyBiome": type = CircularMapNodeType.EmptyBiome; return true;
                case "PathSpoke": type = CircularMapNodeType.PathSpoke; return true;
                case "TowerNode": type = CircularMapNodeType.TowerNode; return true;
                case "CenterCore": type = CircularMapNodeType.CenterCore; return true;
                case "Portal": type = CircularMapNodeType.Portal; return true;
                default: type = default; return false;
            }
        }
    }
}
