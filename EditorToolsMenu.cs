using System;
using EditorPlus.AtomicBuilder;
using NuclearOption.MissionEditorScripts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EditorPlus
{
    internal sealed class EditorToolsMenu : MonoBehaviour
    {
        static EditorToolsMenu _instance;

        GameObject _popoverRoot;
        RectTransform _anchorRt;
        Canvas _hostCanvas;

        internal static Action ToggleGraphGrid;

        internal static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[EditorPlus_ToolsMenu]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<EditorToolsMenu>();
        }

        internal static void BindAnchor(RectTransform anchor)
        {
            EnsureExists();
            _instance._anchorRt = anchor;
        }

        internal static void Toggle()
        {
            if (SceneSingleton<MissionEditor>.i == null) return;
            EnsureExists();
            if (_instance._popoverRoot == null && !_instance.BuildPopover())
                return;

            bool show = !_instance._popoverRoot.activeSelf;
            _instance._popoverRoot.SetActive(show);
            if (show)
            {
                Canvas.ForceUpdateCanvases();
                _instance.RepositionPopover();
                _instance._popoverRoot.transform.SetAsLastSibling();
            }
        }

        internal static void Hide()
        {
            if (_instance?._popoverRoot != null)
                _instance._popoverRoot.SetActive(false);
        }

        internal static void OnSceneUnloaded()
        {
            if (_instance == null) return;
            if (_instance._popoverRoot != null)
            {
                Destroy(_instance._popoverRoot);
                _instance._popoverRoot = null;
            }
            _instance._anchorRt = null;
            _instance._hostCanvas = null;
        }

        void Update()
        {
            if (_popoverRoot == null || !_popoverRoot.activeSelf) return;
            if (!Input.GetMouseButtonDown(0)) return;

            if (IsPointerOverMenuOrAnchor())
                return;

            Hide();
        }

        bool IsPointerOverMenuOrAnchor()
        {
            if (EventSystem.current == null) return false;

            var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);

            foreach (RaycastResult hit in results)
            {
                if (hit.gameObject == null) continue;
                Transform t = hit.gameObject.transform;
                if (_popoverRoot != null && t.IsChildOf(_popoverRoot.transform))
                    return true;
                if (_anchorRt != null && t.IsChildOf(_anchorRt))
                    return true;
            }

            return false;
        }

        bool BuildPopover()
        {
            if (_popoverRoot != null) return true;
            if (_anchorRt == null) return false;

            _hostCanvas = _anchorRt.GetComponentInParent<Canvas>();
            if (_hostCanvas == null) return false;

            _popoverRoot = new GameObject("EditorPlusToolsPopover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _popoverRoot.transform.SetParent(_hostCanvas.transform, false);

            var popRt = _popoverRoot.GetComponent<RectTransform>();
            popRt.anchorMin = new Vector2(0.5f, 0.5f);
            popRt.anchorMax = new Vector2(0.5f, 0.5f);
            popRt.pivot = new Vector2(0.5f, 1f);
            popRt.sizeDelta = new Vector2(200f, 0f);

            _popoverRoot.GetComponent<Image>().color = MissionEditorStyling.PanelBackground;

            var layout = _popoverRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _popoverRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            AddMenuButton("Duplicate  (Ctrl+D)", () =>
            {
                GroupCopyPaste.DuplicateInPlace();
                Hide();
            });

            AddMenuButton("Batch rename…", () =>
            {
                BatchRenameUI.TogglePanel();
                Hide();
            });

            AddMenuButton("Graph grid", () =>
            {
                ToggleGraphGrid?.Invoke();
                Hide();
            });

            _popoverRoot.SetActive(false);
            return true;
        }

        void RepositionPopover()
        {
            if (_popoverRoot == null || _anchorRt == null || _hostCanvas == null) return;

            var popRt = _popoverRoot.GetComponent<RectTransform>();
            var canvasRt = _hostCanvas.transform as RectTransform;
            Camera cam = _hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _hostCanvas.worldCamera;

            Vector3[] corners = new Vector3[4];
            _anchorRt.GetWorldCorners(corners);
            Vector3 worldBottomCenter = (corners[0] + corners[3]) * 0.5f;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt,
                    RectTransformUtility.WorldToScreenPoint(cam, worldBottomCenter),
                    cam,
                    out Vector2 localPoint))
            {
                popRt.anchoredPosition = localPoint + new Vector2(0f, -8f);
            }
        }

        void AddMenuButton(string label, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_popoverRoot.transform, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 30f;

            var img = go.GetComponent<Image>();
            img.color = MissionEditorStyling.ButtonNormal;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = MissionEditorStyling.LabelPrimary;
            tmp.margin = new Vector4(8f, 0f, 4f, 0f);
            tmp.raycastTarget = false;
        }

        static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
