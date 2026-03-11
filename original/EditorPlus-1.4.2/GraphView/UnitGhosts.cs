using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus
{
    public sealed partial class GraphView
    {
        private readonly List<(RectTransform anchor, EdgeView edge, Func<Vector3> worldGetter, RectTransform begin)> _unitGhosts = new();
        private NodeView _unitPinnedCenter;
        private bool _unitPinnedOn;

        public Func<string, bool, IEnumerable<Func<Vector3>>> QueryUnitWorldPositions;

        [Header("Unit Ghost Edges")]
        [SerializeField] public Color unitGhostTint = new(1f, 1f, 1f, 0.35f);
        [SerializeField] public float unitGhostThickness = 2f;

        public void ToggleUnitConnections(NodeView center)
        {
            if (center == null) return;
            if (_unitPinnedOn && _unitPinnedCenter == center)
            {
                _unitPinnedOn = false;
                _unitPinnedCenter = null;
                ClearUnitGhosts();
                return;
            }
            _unitPinnedOn = true;
            _unitPinnedCenter = center;
            BuildUnitConnections(center);
        }
        public void ClearUnitGhosts()
        {
            for (int i = 0; i < _unitGhosts.Count; i++)
            {
                if (_unitGhosts[i].edge) Destroy(_unitGhosts[i].edge.gameObject);
                if (_unitGhosts[i].anchor) Destroy(_unitGhosts[i].anchor.gameObject);
            }
            _unitGhosts.Clear();
        }
        private void OnDisable()
        {
            ClearUnitGhosts();
        }
        private void OnDestroy()
        {
            ClearUnitGhosts();
        }
        private void BuildUnitConnections(NodeView center)
        {
            ClearUnitGhosts();
            if (center == null || !_unitPinnedOn || _unitPinnedCenter != center) return;
            if (QueryUnitWorldPositions == null || content == null || edgeLayer == null) return;

            var isObj = (center.Kind == NodeKind.Objective);
            var worldGetters = QueryUnitWorldPositions(center.Id, isObj);
            if (worldGetters == null) return;
            foreach (var getWorld in worldGetters)
            {
                var anchor = new GameObject("UnitAnchor", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                anchor.SetParent(content, false);
                anchor.sizeDelta = Vector2.one;
                anchor.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                anchor.GetComponent<Image>().raycastTarget = false;

                var edge = CreateEdge("Edge_UnitGhost", center.RT, anchor, AppForUnitGhost());
                _unitGhosts.Add((anchor, edge, getWorld, center.RT));

            }

            UpdateUnitAnchorsToScreen();
        }
        private void UpdateUnitAnchorsToScreen()
        {
            if (_unitGhosts.Count == 0 || !content) return;

            var worldCam = worldCamera ?? Camera.main;

            if (worldCam == null) return;
            var vpRectInContent = GetViewportRectInContent();
            foreach (var item in _unitGhosts)
            {
                if (!item.anchor) continue;
                Vector3 world;
                try { world = item.worldGetter != null ? item.worldGetter() : default; }
                catch { item.anchor.gameObject.SetActive(false); continue; }
                var screen = worldCam.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    var vp = worldCam.WorldToViewportPoint(world);
                    vp.x = 1f - vp.x; vp.y = 1f - vp.y;
                    screen = new Vector3(vp.x * Screen.width, vp.y * Screen.height, 0.0001f);
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, UiCam, out var local);

                var fromLocal = ToContentLocalCenter(item.begin);
                var clamped = ClampToRectAlongSegment(fromLocal, local, vpRectInContent);
                item.anchor.anchoredPosition = clamped;

            }
        }
        private Rect GetViewportRectInContent()
        {
            if (!viewport || !content) return new Rect(0, 0, 0, 0);
            var wc = new Vector3[4];
            viewport.GetWorldCorners(wc);
            var a = content.InverseTransformPoint(wc[0]);
            var b = content.InverseTransformPoint(wc[2]);
            var min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            var max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Vector2 ToContentLocalCenter(RectTransform rt)
        {
            var t = rt.TransformPoint(rt.rect.center);
            var p = ((RectTransform)rt.parent).InverseTransformPoint(t);
            return new Vector2(p.x, p.y);
        }
        private static Vector2 ClampToRectAlongSegment(Vector2 from, Vector2 to, Rect rect)
        {
            if (rect.Contains(to)) return to;
            var d = to - from;
            const float EPS = 1e-5f;
            float bestT = float.PositiveInfinity;
            Vector2 best = to;

            void TryHit(float t, float x, float y)
            {
                if (t < 0f || t > 1f) return;
                var p = from + d * t;
                if (p.x < rect.xMin - EPS || p.x > rect.xMax + EPS ||
                    p.y < rect.yMin - EPS || p.y > rect.yMax + EPS) return;
                if (t < bestT) { bestT = t; best = p; }
            }

            if (Mathf.Abs(d.x) > EPS)
            {
                TryHit((rect.xMin - from.x) / d.x, rect.xMin, 0f);
                TryHit((rect.xMax - from.x) / d.x, rect.xMax, 0f);
            }
            if (Mathf.Abs(d.y) > EPS)
            {
                TryHit((rect.yMin - from.y) / d.y, 0f, rect.yMin);
                TryHit((rect.yMax - from.y) / d.y, 0f, rect.yMax);
            }
            best.x = Mathf.Clamp(best.x, rect.xMin + 1f, rect.xMax - 1f);
            best.y = Mathf.Clamp(best.y, rect.yMin + 1f, rect.yMax - 1f);
            return best;
        }
    }
}
