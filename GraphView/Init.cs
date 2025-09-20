using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus
{
    public sealed partial class GraphView : MonoBehaviour
    {
        private Camera worldCamera;                          
        private Camera _uiCam;                               
        private Camera UiCam => _uiCam ??= GetComponentInParent<Canvas>()?.worldCamera;
        private RectTransform viewport, content, edgeLayer;
        public Func<string, bool> CanOutcomeHaveOutputs;
        public Action<string> OnEditObjective;
        public Action<string> OnEditOutcome;
        public Action<string, bool, string, bool> OnLink;
        public Action<string, bool, string, bool> OnUnlink;
        private readonly Dictionary<string, NodeView> _obj = new();
        private readonly Dictionary<string, NodeView> _out = new();
        [SerializeField] private NodeView nodePrefab;
        [SerializeField] public Sprite edgeFadeSprite;

        private void Awake()
        {
            AutoWire();
            EnsureGridLayer();
            var ci = content.GetComponent<Image>();
            if (ci) ci.raycastTarget = false;
            EnsureEdgesContainer();
            UpdateGridUV();
        }
        public Action<string, bool> OnRequestAddSelectionToNode; 
        internal void RequestAddSelectionToNode(NodeView n)
        {
            OnRequestAddSelectionToNode?.Invoke(n.Id, n.Kind == NodeKind.Objective);
        }

        private void AutoWire()
        {
            if (!viewport)
            {
                var t = transform.Find("Viewport");
                if (t) viewport = t as RectTransform;
            }
            if (!content && viewport)
            {
                var t = viewport.Find("Content");
                if (t) content = t as RectTransform;
                if (!content) { Plugin.Logger.LogError("[GraphView] Missing 'Content' under Viewport"); return; }
            }
            if (!edgeLayer && content)
            {
                var t = content.Find("Edges");
                if (t) edgeLayer = t as RectTransform;
            }

        }
        public void SetWorldCamera(Camera cam) { worldCamera = cam; }
        public Camera GetWorldCamera() => worldCamera;
        private void EnsureEdgesContainer()
        {
            if (!content) return;
            if (!edgeLayer)
            {
                var go = new GameObject("Edges", typeof(RectTransform));
                edgeLayer = go.GetComponent<RectTransform>();
                edgeLayer.SetParent(content, false);
                edgeLayer.anchorMin = Vector2.zero;
                edgeLayer.anchorMax = Vector2.one;
                edgeLayer.offsetMin = Vector2.zero;
                edgeLayer.offsetMax = Vector2.zero;
            }
            edgeLayer.SetSiblingIndex(0);
        }
        private void EnsureGridLayer()
        {
            if (!viewport) return;

            if (_bgRT == null)
            {
                var t = viewport.Find("Background") as RectTransform;
                if (t)
                {
                    _bgRT = t;
                    _bgImg = t.GetComponent<Image>();
                    if (_bgImg) _bgImg.raycastTarget = false;
                    _bgRT.gameObject.SetActive(_bgGridVisible);
                }
            }

            if (_minorRT == null)
            {
                var t = (viewport.Find("Grid") as RectTransform);
                if (t)
                {
                    _minorRT = t;
                    _minorImg = t.GetComponent<RawImage>();
                    if (_minorImg) _minorImg.raycastTarget = false;
                    _minorRT.gameObject.SetActive(_bgGridVisible);
                }
            }
            if (_minorImg)
            {
                _minorImg.raycastTarget = false;
                _minorImg.uvRect = new Rect(0, 0, 1, 1);
            }

            if (_bgRT) _bgRT.SetAsFirstSibling();
            if (_minorRT) _minorRT.SetSiblingIndex(1);
            if (content) content.SetSiblingIndex(2);
        }
        public void ToggleBackgroundAndGrid()
        {
            SetBackgroundAndGridVisible(!_bgGridVisible);
        }
        public void SetBackgroundAndGridVisible(bool on)
        {
            _bgGridVisible = on;
            EnsureGridLayer();
            if (_bgRT) _bgRT.gameObject.SetActive(on);
            if (_minorRT) _minorRT.gameObject.SetActive(on);
            if (_minorImg) _minorImg.enabled = on;
        }

    }
}
