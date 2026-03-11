using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EditorPlus
{
    public sealed partial class GraphView
    {
        private ObjectiveDTO[] _fullObjectives;
        private OutcomeDTO[] _fullOutcomes;
        private LinkDTO[] _fullLinks;
        private bool _building;

        public void BuildGraph(GraphData data, bool computeLayout = true, bool snapshotAsFull = true)
        {
            if (computeLayout)
                data = ComputeTreeLayout(data);

            if (snapshotAsFull)
            {
                _fullObjectives = data.Objectives ?? Array.Empty<ObjectiveDTO>();
                _fullOutcomes = data.Outcomes ?? Array.Empty<OutcomeDTO>();
                _fullLinks = data.Links ?? Array.Empty<LinkDTO>();
            }
            Build(data.Objectives, data.Outcomes, data.Links);
        }
        private struct NodeKey : IEquatable<NodeKey>
        {
            public bool IsObjective;
            public string Id;
            public bool Equals(NodeKey other) => IsObjective == other.IsObjective && Id == other.Id;
            public override bool Equals(object o) => o is NodeKey k && Equals(k);
            public override int GetHashCode() => (IsObjective ? 1 : 0) * 397 ^ (Id?.GetHashCode() ?? 0);
        }
        private static NodeKey NK(bool isObj, string id) => new NodeKey { IsObjective = isObj, Id = id };
        private GraphData ComputeTreeLayout(GraphData src)
        {
            var objById = (src.Objectives ?? Array.Empty<ObjectiveDTO>())
                .ToDictionary(o => o.Id, StringComparer.Ordinal);
            var outById = (src.Outcomes ?? Array.Empty<OutcomeDTO>())
                .ToDictionary(u => u.Id, StringComparer.Ordinal);

            var adj = new Dictionary<NodeKey, List<NodeKey>>();
            var indeg = new Dictionary<NodeKey, int>();
            void Ensure(NodeKey k) { if (!adj.ContainsKey(k)) adj[k] = new List<NodeKey>(); if (!indeg.ContainsKey(k)) indeg[k] = 0; }

            var usedBy = new Dictionary<string, int>(StringComparer.Ordinal);
            var outcomeCount = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var l in src.Links ?? Array.Empty<LinkDTO>())
            {
                var u = NK(l.FromIsObjective, l.FromId);
                var v = NK(l.ToIsObjective, l.ToId);
                Ensure(u); Ensure(v);
                adj[u].Add(v); indeg[v]++;

                if (l.FromIsObjective && !l.ToIsObjective)
                    outcomeCount[l.FromId] = outcomeCount.TryGetValue(l.FromId, out var cc) ? cc + 1 : 1;
                if (!l.FromIsObjective && l.ToIsObjective)
                    usedBy[l.FromId] = usedBy.TryGetValue(l.FromId, out var uu) ? uu + 1 : 1;
            }

            var layer = new Dictionary<NodeKey, int>();
            var q = new Queue<NodeKey>(indeg.Where(p => p.Value == 0).Select(p => p.Key));
            foreach (var k in q) layer[k] = 0;
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                if (!adj.TryGetValue(u, out var outs)) continue;
                foreach (var v in outs)
                {
                    int next = layer[u] + 1;
                    if (!layer.TryGetValue(v, out var lv) || next > lv) layer[v] = next;
                    if (--indeg[v] == 0) q.Enqueue(v);
                }
            }

            var pred = new Dictionary<NodeKey, List<NodeKey>>();
            foreach (var (u, outs) in adj)
            {
                foreach (var v in outs)
                {
                    if (!pred.TryGetValue(v, out var list)) pred[v] = list = new List<NodeKey>();
                    list.Add(u);
                }
                if (!pred.ContainsKey(u)) pred[u] = new List<NodeKey>();
            }
            foreach (var n in adj.Keys)
                if (!layer.ContainsKey(n))
                    layer[n] = pred.TryGetValue(n, out var ps) && ps.Count > 0 ? ps.Select(p => layer.TryGetValue(p, out var L) ? L + 1 : 0).Max() : 0;

            var primaryParent = new Dictionary<NodeKey, NodeKey>();
            var treeChildren = new Dictionary<NodeKey, List<NodeKey>>();
            void AddChild(NodeKey p, NodeKey c) { if (!treeChildren.TryGetValue(p, out var kids)) treeChildren[p] = kids = new List<NodeKey>(); kids.Add(c); }

            foreach (var u in adj.Keys.OrderBy(k => layer[k]).ThenBy(k => k.Id, StringComparer.Ordinal))
            {
                if (!adj.TryGetValue(u, out var outs)) continue;
                foreach (var v in outs.Where(v => layer[v] > layer[u]).OrderBy(v => layer[v]).ThenBy(v => v.Id, StringComparer.Ordinal))
                    if (!primaryParent.ContainsKey(v)) { primaryParent[v] = u; AddChild(u, v); }
            }

            int Degree(NodeKey n) => adj.TryGetValue(n, out var l) ? l.Count : 0;
            foreach (var kv in treeChildren.ToList())
                kv.Value.Sort((a, b) => { int cmp = layer[a].CompareTo(layer[b]); if (cmp != 0) return cmp; cmp = Degree(b).CompareTo(Degree(a)); if (cmp != 0) return cmp; return string.Compare(a.Id, b.Id, StringComparison.Ordinal); });

            var roots = adj.Keys.Where(k => !primaryParent.ContainsKey(k)).OrderBy(k => layer[k]).ThenBy(k => k.Id, StringComparer.Ordinal).ToList();

            var subtreeSpan = new Dictionary<NodeKey, int>();
            var centerY = new Dictionary<NodeKey, float>();
            int Span(NodeKey n)
            {
                if (!treeChildren.TryGetValue(n, out var kids) || kids.Count == 0) return subtreeSpan[n] = 1;
                int s = 0; foreach (var c in kids) s += Span(c); return subtreeSpan[n] = Math.Max(s, 1);
            }
            float Place(NodeKey n, float startY)
            {
                if (!treeChildren.TryGetValue(n, out var kids) || kids.Count == 0) { centerY[n] = startY + (subtreeSpan[n] - 1) * 0.5f; return centerY[n]; }
                float cursor = startY; var childC = new List<float>(kids.Count);
                foreach (var c in kids) { float cy = Place(c, cursor); childC.Add(cy); cursor += subtreeSpan[c]; }
                float myC = childC.Average(); centerY[n] = myC; return myC;
            }
            float forest = 0f;
            foreach (var r in roots) { Span(r); Place(r, forest); forest += subtreeSpan[r]; }
            foreach (var n in adj.Keys) if (!centerY.ContainsKey(n)) { subtreeSpan[n] = 1; centerY[n] = forest; forest += 1; }

            var row = centerY.ToDictionary(kv => kv.Key, kv => Mathf.RoundToInt(kv.Value));

            var allKeys = new List<NodeKey>();
            foreach (var id in objById.Keys) allKeys.Add(NK(true, id));
            foreach (var id in outById.Keys) allKeys.Add(NK(false, id));

            var connected = new HashSet<NodeKey>(adj.Keys);
            foreach (var kv in adj) foreach (var k in kv.Value) connected.Add(k);

            var unconnected = allKeys.Where(k => !connected.Contains(k))
                                     .OrderBy(k => k.Id, StringComparer.Ordinal)
                                     .ToList();

            var fixedRow = new Dictionary<NodeKey, int>();
            for (int i = 0; i < unconnected.Count; i++) fixedRow[unconnected[i]] = i;

            int rowOffset = unconnected.Count;

            int GetLayer(NodeKey k)
            {
                if (fixedRow.ContainsKey(k)) return 0;
                return layer.TryGetValue(k, out var L) ? L : 0;
            }
            int GetRow(NodeKey k)
            {
                if (fixedRow.TryGetValue(k, out var rFixed)) return rFixed;
                var r = row.TryGetValue(k, out var R) ? R : 0;
                return r + rowOffset;
            }

            ObjectiveDTO MapObj(ObjectiveDTO o) => new ObjectiveDTO
            {
                Id = o.Id,
                UniqueName = o.UniqueName,
                DisplayName = o.DisplayName,
                TypeName = o.TypeName,
                Hidden = o.Hidden,
                OutcomeCount = outcomeCount.TryGetValue(o.Id, out var oc) ? oc : o.OutcomeCount,
                Layer = GetLayer(NK(true, o.Id)),
                Row = GetRow(NK(true, o.Id)),
                FactionName = o.FactionName
            };
            OutcomeDTO MapOut(OutcomeDTO u) => new OutcomeDTO
            {
                Id = u.Id,
                UniqueName = u.UniqueName,
                TypeName = u.TypeName,
                UsedByCount = usedBy.TryGetValue(u.Id, out var ub) ? ub : u.UsedByCount,
                Layer = GetLayer(NK(false, u.Id)),
                Row = GetRow(NK(false, u.Id))
            };

            return new GraphData
            {
                Objectives = objById.Values.Select(MapObj).ToArray(),
                Outcomes = outById.Values.Select(MapOut).ToArray(),
                Links = src.Links ?? Array.Empty<LinkDTO>()
            };
        }
        public void Build(ObjectiveDTO[] objectives, OutcomeDTO[] outcomes, LinkDTO[] links)
        {
            if (_building) return;
            _building = true;
            try
            {
                AutoWire();
                EnsureEdgesContainer();
                if (!content || !viewport || !nodePrefab)
                {
                    Plugin.Logger.LogError($"[GraphView] Missing refs: content={(content != null)}, viewport={(viewport != null)}, nodePrefab={(nodePrefab != null)}");
                    return;
                }
                Clear();

                var nodes = new List<(NodeView v, int layer, int row)>();
                int maxLayer = 0, maxRow = 0;

                foreach (var o in objectives)
                {
                    var n = Instantiate(nodePrefab, content);
                    n.name = $"OBJ_{o.UniqueName}";
                    n.InitGraph(edgeLayer, content, this);
                    n.BindObjective(o.Id, o.UniqueName, o.DisplayName, o.TypeName, o.Hidden, o.OutcomeCount);
                    n.ApplyFaction(o.FactionName);
                    nodes.Add((n, o.Layer, o.Row));
                    maxLayer = Mathf.Max(maxLayer, o.Layer);
                    maxRow = Mathf.Max(maxRow, o.Row);
                    _obj[o.Id] = n;
                }

                foreach (var oc in outcomes)
                {
                    var n = Instantiate(nodePrefab, content);
                    n.name = $"OUT_{oc.UniqueName}";
                    n.InitGraph(edgeLayer, content, this);
                    n.BindOutcome(oc.Id, oc.UniqueName, oc.TypeName, oc.UsedByCount);
                    bool supports = CanOutcomeHaveOutputs?.Invoke(n.Id) ?? false;
                    n.SetOutputPortVisible(supports);
                    nodes.Add((n, oc.Layer, oc.Row));
                    maxLayer = Mathf.Max(maxLayer, oc.Layer);
                    maxRow = Mathf.Max(maxRow, oc.Row);
                    _out[oc.Id] = n;
                }

                var colWidths = new float[maxLayer + 1];
                var rowHeights = new float[maxRow + 1];
                foreach (var e in nodes)
                {
                    var sz = e.v.GetLayoutSize();
                    if (sz.x > colWidths[e.layer]) colWidths[e.layer] = sz.x;
                    if (sz.y > rowHeights[e.row]) rowHeights[e.row] = sz.y;
                }

                var colX = new float[colWidths.Length];
                var rowY = new float[rowHeights.Length];
                float accX = 0f;
                float accY = 0f;
                for (int c = 0; c < colWidths.Length; c++) { colX[c] = accX; accX += colWidths[c]; }
                for (int r = 0; r < rowHeights.Length; r++) { rowY[r] = accY; accY += rowHeights[r]; }

                foreach (var (v, layer, row) in nodes)
                {
                    v.RT.anchorMin = v.RT.anchorMax = new Vector2(0, 1);
                    v.RT.pivot = new Vector2(0, 1);
                    v.RT.anchoredPosition = new Vector2(colX[layer], rowY[row]);
                }

                foreach (var l in links)
                {
                    NodeView from = l.FromIsObjective
                        ? (_obj.TryGetValue(l.FromId, out var oFrom) ? oFrom : null)
                        : (_out.TryGetValue(l.FromId, out var uFrom) ? uFrom : null);
                    NodeView to = l.ToIsObjective
                        ? (_obj.TryGetValue(l.ToId, out var oTo) ? oTo : null)
                        : (_out.TryGetValue(l.ToId, out var uTo) ? uTo : null);
                    if (from != null && to != null && !ReferenceEquals(from, to))
                        MakeConnection(from, to);
                }

                const float slack = 16f;
                float neededW = Mathf.Max(viewport.rect.width, accX + slack);
                float neededH = Mathf.Max(viewport.rect.height, accY + slack);
                content.sizeDelta = new Vector2(neededW, neededH);

                EnsureGridLayer();
                UpdateGridUV();
                if (!_isFocusBuild && nodes.Count > 0)
                {
                    CenterViewportOn(nodes[0].v);
                }
            }
            finally
            {
                _building = false;
            }
        }
        private void CenterViewportOn(NodeView n)
        {
            if (!viewport || !content || !n) return;
            float s = Mathf.Approximately(content.localScale.x, 0f) ? 1f : content.localScale.x;
            Vector2 local = ToContentLocalCenter(n.RT);
            content.anchoredPosition = -local * s;
            UpdateGridUV();
        }
        public void Clear()
        {
            if (edgeLayer)
            {
                for (int i = edgeLayer.childCount - 1; i >= 0; i--)
                    Destroy(edgeLayer.GetChild(i).gameObject);
            }

            if (content)
            {
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var ch = content.GetChild(i) as RectTransform;
                    if (!ch) continue;
                    if ((edgeLayer && ch == edgeLayer)) continue;
                    Destroy(ch.gameObject);
                }
            }

            foreach (var n in _obj.Values) if (n) n.DisconnectAll();
            foreach (var n in _out.Values) if (n) n.DisconnectAll();
            _obj.Clear();
            _out.Clear();
            _connections.Clear();
            ClearHighlights();
        }
    }
}
