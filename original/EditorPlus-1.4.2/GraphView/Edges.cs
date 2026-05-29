using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus
{
    public sealed partial class GraphView
    {
        [Header("Edge")]
        [SerializeField] public Color edgeTint = new(1f, 1f, 1f, 0.70f);
        [SerializeField] public float edgeThickness = 2f;

        [Header("Highlight Edge")]
        [SerializeField] public Color highlightEdgeTint = new(1f, 1f, 1f, 1f);
        [SerializeField] public float highlightEdgeThickness = 3f;

        private readonly List<Connection> _connections = new();
        private EdgeView _hoverEdge;

        public void ClearHighlights()
        {
            foreach (var n in _hlNodes) if (n) n.SetHighlighted(false);
            foreach (var e in _hlEdges) if (e) e.ApplyAppearance(false);
            _hlNodes.Clear();
            _hlEdges.Clear();
        }
        public void HighlightNeighborhood(NodeView center, bool on)
        {
            ClearHighlights();
            if (!on || center == null) { _hoverNode = null; return; }

            center.SetHighlighted(true);
            _hlNodes.Add(center);
            foreach (var c in _connections)
                if (c.From == center || c.To == center)
                {
                    if (c.Edge) { c.Edge.ApplyAppearance(true); _hlEdges.Add(c.Edge); }
                    var other = (c.From == center) ? c.To : c.From;
                    if (other) { other.SetHighlighted(true); _hlNodes.Add(other); }
                }
            _hoverNode = center;
        }
        private EdgeView CreateEdge(string name, RectTransform begin, RectTransform end, in EdgeView.Appearance app)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(EdgeView));
            var rt = (RectTransform)go.transform;
            rt.SetParent(edgeLayer, false);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;

            var e = go.GetComponent<EdgeView>();
            e.Init(content, begin, end);
            e.ApplyAppearance(app, highlighted: false);
            edgeLayer.SetAsFirstSibling();
            return e;
        }
        private EdgeView.Appearance AppForNormal(NodeView from, NodeView to)
        {
            Color normal = edgeTint;
            if ((from.Kind == NodeKind.Objective && from.TryGetFactionColor(out Color fc)) ||
                (to.Kind == NodeKind.Objective && to.TryGetFactionColor(out fc)))
            {
                normal = new Color(fc.r, fc.g, fc.b, edgeTint.a);
            }

            return new EdgeView.Appearance
            {
                normalTint = normal * 2f,
                highlightTint = highlightEdgeTint,
                normalThickness = edgeThickness,
                highlightThickness = highlightEdgeThickness,
                minHitThickness = EdgeView.Appearance.Default.minHitThickness,
                sprite = edgeFadeSprite
            };
        }
        private EdgeView.Appearance AppForGhost() => new EdgeView.Appearance
        {
            normalTint = ghostEdgeTint,
            highlightTint = ghostEdgeTint,
            normalThickness = ghostEdgeThickness,
            highlightThickness = ghostEdgeThickness,
            minHitThickness = EdgeView.Appearance.Default.minHitThickness,
            sprite = edgeFadeSprite
        };
        private EdgeView.Appearance AppForUnitGhost() => new EdgeView.Appearance
        {
            normalTint = unitGhostTint,
            highlightTint = unitGhostTint,
            normalThickness = unitGhostThickness,
            highlightThickness = unitGhostThickness,
            minHitThickness = EdgeView.Appearance.Default.minHitThickness,
            sprite = edgeFadeSprite
        };
        public Connection MakeConnection(NodeView from, NodeView to)
        {
            if (from == null || to == null || ReferenceEquals(from, to)) return null;
            if (from.Kind == to.Kind) return null;
            foreach (var c in _connections) if (c.From == from && c.To == to) return null;

            var app = AppForNormal(from, to);
            var edge = CreateEdge($"Edge_{from.name}_to_{to.name}", from.RightPort, to.LeftPort, app);

            var conn = new Connection(from, to, edge);
            from.AddOutgoing(conn);
            RegisterConnection(conn);
            return conn;
        }
        internal void RegisterConnection(Connection c)
        {
            if (c == null || _connections.Contains(c)) return;
            _connections.Add(c);

            if (c.Edge != null)
            {
                c.Edge.OnRequestDelete = OnEdgeRequestDelete;
                c.Edge.OnHoverChanged = OnEdgeHoverChanged;
            }
        }
        private void RemoveConnection(Connection c, bool notifyModel)
        {
            if (c.Edge) { c.Edge.OnRequestDelete = null; c.Edge.OnHoverChanged = null; Destroy(c.Edge.gameObject); }
            _connections.Remove(c);
            c.From?.RemoveConnectionTo(c.To);
            if (notifyModel && OnUnlink != null && c.From != null && c.To != null)
                OnUnlink.Invoke(c.From.Id, c.From.Kind == NodeKind.Objective, c.To.Id, c.To.Kind == NodeKind.Objective);
            ClearHighlights();
        }
        internal void Unlink(NodeView from, NodeView to)
        {
            var c = _connections.Find(cc => cc.From == from && cc.To == to);
            if (c != null) RemoveConnection(c, notifyModel: true);
        }
        private void RemoveAllConnections(Predicate<Connection> match, bool notifyModel)
        {
            var toRemove = _connections.Where(c => match(c)).ToList();
            foreach (var c in toRemove)
                RemoveConnection(c, notifyModel);
        }
        internal void UnlinkOutgoing(NodeView n) => RemoveAllConnections(c => c.From == n, true);
        internal void UnlinkIncoming(NodeView n) => RemoveAllConnections(c => c.To == n, true);
        void OnEdgeHoverChanged(EdgeView edge, bool on)
        {
            var c = _connections.Find(cc => cc.Edge == edge);
            if (c == null) return;

            if (on)
                HighlightConnection(c);
            else
                ClearHighlights();
            _hoverEdge = on ? edge : null;
        }
        public void OnPortHoverChanged(NodeView node, PortKind kind, bool on)
        {
            if (on)
            {
                _hoverPortNode = node;
                _hoverPortKind = kind;
            }
            else
            {
                if (_hoverPortNode == node) _hoverPortNode = null;
            }
        }
        void HighlightConnection(Connection c)
        {
            ClearHighlights();

            if (c.Edge) { c.Edge.ApplyAppearance(true); _hlEdges.Add(c.Edge); }
            if (c.From) { c.From.SetHighlighted(true); _hlNodes.Add(c.From); }
            if (c.To) { c.To.SetHighlighted(true); _hlNodes.Add(c.To); }
        }
        private void OnEdgeRequestDelete(EdgeView edge)
        {
            var c = _connections.Find(cc => cc.Edge == edge);
            if (c == null) return;
            Plugin.Logger.LogDebug($"[Graph] UI delete edge {c.From?.Id} > {c.To?.Id}");
            RemoveConnection(c, notifyModel: true);
        }
    }
}
