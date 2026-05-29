using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EditorPlus
{
    public sealed partial class GraphView
    {
        private readonly HashSet<NodeView> _hlNodes = new();
        private readonly HashSet<EdgeView> _hlEdges = new();
        private readonly HashSet<string> _focusObjIds = new();
        private readonly HashSet<string> _focusOutIds = new();

        private bool _focusOn;
        private string _focusId;
        private bool _focusIsObj;

        private bool _isFocusBuild;      
        private string _focusAnchorId;   
        private bool _focusAnchorIsObj;

        public void ToggleFocusRebuild(NodeView center)
        {
            if (!center) return;
            bool isObj = center.Kind == NodeKind.Objective;

            if (!_focusOn)
            {
                FocusRebuildAround(center);
                return;
            }

            if (_focusIsObj == isObj && _focusId == center.Id)
            {
                RestoreFullGraph(center);
                return;
            }

            ExpandFocus(center);
        }
        public void FocusRebuildAround(NodeView center)
        {
            if (!center) return;
            _focusObjIds.Clear(); _focusOutIds.Clear();
            bool isObj = (center.Kind == NodeKind.Objective);
            AddNeighborhoodFromFull(center.Id, isObj, _focusObjIds, _focusOutIds);

            PinnedRebuild(center, () => BuildDataFromFocusSets());

            _focusOn = true;
            _focusAnchorId = center.Id;
            _focusAnchorIsObj = isObj;
            _focusId = _focusAnchorId;
            _focusIsObj = _focusAnchorIsObj;
        }
        private void ExpandFocus(NodeView center)
        {
            if (!center || !_focusOn) return;

            bool isObj = (center.Kind == NodeKind.Objective);
            AddNeighborhoodFromFull(center.Id, isObj, _focusObjIds, _focusOutIds);
            NodeView anchor = null;
            if (!string.IsNullOrEmpty(_focusAnchorId))
            {
                anchor = _focusAnchorIsObj
                    ? (_obj.TryGetValue(_focusAnchorId, out var n) ? n : null)
                    : (_out.TryGetValue(_focusAnchorId, out var m) ? m : null);
            }
            if (!anchor) anchor = center;

            PinnedRebuild(anchor, () => BuildDataFromFocusSets());

            _focusOn = true;
        }

        public void RestoreFullGraph(NodeView center)
        {
            if (_fullObjectives == null || _fullOutcomes == null || _fullLinks == null) return;

            PinnedRebuild(center, () => new GraphData
            {
                Objectives = _fullObjectives,
                Outcomes = _fullOutcomes,
                Links = _fullLinks
            });

            _focusOn = false; _focusId = null; _focusObjIds.Clear(); _focusOutIds.Clear();

            _gridPhaseX = 0f; _gridPhaseY = 0f; UpdateGridUV();
        }
        private GraphData BuildDataFromFocusSets()
        {
            var obj = (_fullObjectives ?? Array.Empty<ObjectiveDTO>()).Where(o => _focusObjIds.Contains(o.Id)).ToArray();
            var outc = (_fullOutcomes ?? Array.Empty<OutcomeDTO>()).Where(o => _focusOutIds.Contains(o.Id)).ToArray();
            var links = (_fullLinks ?? Array.Empty<LinkDTO>()).Where(l =>
                (l.FromIsObjective ? _focusObjIds.Contains(l.FromId) : _focusOutIds.Contains(l.FromId)) &&
                (l.ToIsObjective ? _focusObjIds.Contains(l.ToId) : _focusOutIds.Contains(l.ToId))
            ).ToArray();
            return new GraphData { Objectives = obj, Outcomes = outc, Links = links };
        }
        private void AddNeighborhoodFromFull(string id, bool isObj, HashSet<string> keepObj, HashSet<string> keepOut)
        {
            if (isObj) keepObj.Add(id); else keepOut.Add(id);
            if (_fullLinks == null) return;

            foreach (var l in _fullLinks)
            {
                if (!((l.FromIsObjective == isObj && l.FromId == id) ||
                      (l.ToIsObjective == isObj && l.ToId == id)))
                    continue;

                if (l.FromIsObjective) keepObj.Add(l.FromId); else keepOut.Add(l.FromId);
                if (l.ToIsObjective) keepObj.Add(l.ToId); else keepOut.Add(l.ToId);
            }
        }
        private void PinnedRebuild(NodeView center, Func<GraphData> make)
        {
            if (!center || !content || !viewport) return;

            float s = content.localScale.x;
            var vpSize = viewport.rect.size;

            float cell = Mathf.Max(1e-3f, _gridCellPx);
            float sxBefore = Mathf.Approximately(s, 0f) ? 1f : s;
            float tilesX_before = vpSize.x / (cell * sxBefore);
            float tilesY_before = vpSize.y / (cell * sxBefore);

            Vector2 originPx_before = ViewportPixelOfContent(Vector2.zero);
            float baseX_before = -(originPx_before.x / vpSize.x) * tilesX_before;
            float baseY_before = -(originPx_before.y / vpSize.y) * tilesY_before;

            Vector2 anchorLocal_before = ToContentLocalCenter(center.RT);
            Vector2 anchorPx_before = ViewportPixelOfContent(anchorLocal_before);

            float u_anchor_before = (anchorPx_before.x / vpSize.x) * tilesX_before + (baseX_before + _gridPhaseX);
            float v_anchor_before = (anchorPx_before.y / vpSize.y) * tilesY_before + (baseY_before + _gridPhaseY);

            Vector2 lockVp = content.anchoredPosition + ToContentLocalCenter(center.RT) * s;

            ClearUnitGhosts();
            var data = make();
            _isFocusBuild = true;
            try
            {
                BuildGraph(data, computeLayout: true, snapshotAsFull: false);
            }
            finally { _isFocusBuild = false; }

            NodeView rebuilt = null;
            if (center.Kind == NodeKind.Objective) _obj.TryGetValue(center.Id, out rebuilt);
            else _out.TryGetValue(center.Id, out rebuilt);

            if (rebuilt && content)
            {
                float s2 = content.localScale.x;
                Vector2 post = ToContentLocalCenter(rebuilt.RT);
                content.anchoredPosition = lockVp - post * s2;

                float tilesX_after = vpSize.x / (cell * (Mathf.Approximately(s2, 0f) ? 1f : s2));
                float tilesY_after = vpSize.y / (cell * (Mathf.Approximately(s2, 0f) ? 1f : s2));

                Vector2 originPx_after = ViewportPixelOfContent(Vector2.zero);
                float baseX_after = -(originPx_after.x / vpSize.x) * tilesX_after;
                float baseY_after = -(originPx_after.y / vpSize.y) * tilesY_after;

                Vector2 anchorLocal_after = ToContentLocalCenter(rebuilt.RT);
                Vector2 anchorPx_after = ViewportPixelOfContent(anchorLocal_after);

                _gridPhaseX = u_anchor_before - ((anchorPx_after.x / vpSize.x) * tilesX_after + baseX_after);
                _gridPhaseY = v_anchor_before - ((anchorPx_after.y / vpSize.y) * tilesY_after + baseY_after);

                UpdateGridUV();
            }
        }

    }
}
