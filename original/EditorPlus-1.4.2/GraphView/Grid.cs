using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus
{
    public sealed partial class GraphView
    {
        private RectTransform _bgRT;
        private Image _bgImg;
        private RawImage _minorImg;
        private RectTransform _minorRT;
        private bool _bgGridVisible = true;

        private Vector2 _lastContentPos;
        private Vector2 _lastContentScale;
        private Vector2 _lastViewportSize;
        private float _gridPhaseX = 0f, _gridPhaseY = 0f;
        private float _gridCellPx = 64f * 4;
        private void UpdateGridUV()
        {
            if (!viewport || !content || _minorImg == null) return;

            var vpSize = viewport.rect.size;
            if (vpSize.x <= 0f || vpSize.y <= 0f) return;

            var s = content.localScale;
            float sx = Mathf.Abs(s.x) > 1e-6f ? s.x : 1f;
            float sy = Mathf.Abs(s.y) > 1e-6f ? s.y : 1f;
            float cell = Mathf.Max(1e-3f, _gridCellPx);

            float uPerPx = 1f / (cell * sx);
            float vPerPx = 1f / (cell * sy);

            float tilesX = vpSize.x * uPerPx;
            float tilesY = vpSize.y * vPerPx;

            Vector2 originPx = ViewportPixelOfContent(Vector2.zero);

            float uOff = Mathf.Round(-originPx.x + _gridPhaseX / uPerPx) * uPerPx;
            float vOff = Mathf.Round(-originPx.y + _gridPhaseY / vPerPx) * vPerPx;

            _minorImg.enabled = true;
            _minorImg.uvRect = new Rect(uOff, vOff, tilesX, tilesY);
        }
        private void LateUpdate()
        {
            if (!viewport || !content) return;

            var pos = content.anchoredPosition;
            var scale = content.localScale;
            var size = viewport.rect.size;

            if (_lastContentPos != pos || _lastContentScale != (Vector2)scale || _lastViewportSize != size)
            {
                _lastContentPos = pos;
                _lastContentScale = scale;
                _lastViewportSize = size;
                UpdateGridUV();
            }
            UpdateUnitAnchorsToScreen();
        }
        private Vector2 ViewportPixelOfContent(Vector2 contentPoint)
        {
            var world = content.TransformPoint(new Vector3(contentPoint.x, contentPoint.y, 0f));
            var vLocal = viewport.InverseTransformPoint(world);
            var r = viewport.rect;
            float px = vLocal.x + r.width * viewport.pivot.x;
            float py = vLocal.y + r.height * viewport.pivot.y;
            return new Vector2(px, py);
        }

    }
}
