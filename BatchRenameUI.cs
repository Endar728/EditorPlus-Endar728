using EditorPlus.AtomicBuilder;
using NuclearOption.MissionEditorScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus
{
    internal sealed class BatchRenameUI : MonoBehaviour
    {
        static BatchRenameUI _instance;
        static string _pendingStatus;

        GameObject _panelRoot;
        TMP_InputField _prefixInput;
        TMP_InputField _startIndexInput;
        TMP_Text _statusText;
        TMP_Text _selectionText;

        internal static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[EditorPlus_BatchRenameUI]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BatchRenameUI>();
        }

        internal static void TogglePanel()
        {
            if (SceneSingleton<MissionEditor>.i == null) return;
            EnsureExists();
            if (_instance._panelRoot == null && !_instance.BuildUi())
            {
                SetStatus("Mission editor UI not ready yet.");
                return;
            }
            _instance.SetVisible(!_instance._panelRoot.activeSelf);
        }

        internal static void SetStatus(string msg)
        {
            _pendingStatus = msg;
            if (_instance != null && _instance._statusText != null)
                _instance._statusText.text = msg ?? "";
        }

        internal static void OnSceneUnloaded()
        {
            if (_instance == null) return;
            if (_instance._panelRoot != null)
            {
                Destroy(_instance._panelRoot);
                _instance._panelRoot = null;
            }
            _instance._prefixInput = null;
            _instance._startIndexInput = null;
            _instance._statusText = null;
            _instance._selectionText = null;
        }

        void SetVisible(bool on)
        {
            if (_panelRoot == null && !BuildUi()) return;
            _panelRoot.SetActive(on);
            if (on)
            {
                RefreshSelectionCount();
                _panelRoot.transform.SetAsLastSibling();
            }
        }

        void RefreshSelectionCount()
        {
            if (_selectionText == null) return;
            int count = BatchRename.CountSelectedUnits();
            _selectionText.text = count == 1
                ? "1 unit selected"
                : $"{count} units selected";
        }

        bool BuildUi()
        {
            if (_panelRoot != null) return true;

            Canvas host = FindEditorCanvas();
            if (host == null)
            {
                Plugin.Logger?.LogWarning("[BatchRename] No editor canvas found — open the mission editor first.");
                return false;
            }

            _panelRoot = new GameObject("BatchRenamePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(host.transform, false);
            var panelRt = _panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.sizeDelta = new Vector2(300f, 0f);
            panelRt.anchoredPosition = new Vector2(-12f, 12f);
            _panelRoot.GetComponent<Image>().color = MissionEditorStyling.PanelBackground;
            _panelRoot.transform.SetAsLastSibling();

            var rootLayout = _panelRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 14, 14);
            rootLayout.spacing = 6f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var fitter = _panelRoot.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var titleLabel = AddLayoutLabel(_panelRoot.transform, "Batch Rename", 18, FontStyles.Bold, 28f);
            titleLabel.color = MissionEditorStyling.AccentGreen;

            _selectionText = AddLayoutLabel(_panelRoot.transform, "0 units selected", 12, FontStyles.Italic, 22f);
            _selectionText.color = MissionEditorStyling.LabelSecondary;

            _prefixInput = AddLayoutInput(_panelRoot.transform, "Name prefix", "Squad_A");
            _startIndexInput = AddLayoutInput(_panelRoot.transform, "Start number", "1");

            AddLayoutButton(_panelRoot.transform, "Rename selected", OnApply, primary: true);
            var hint = AddLayoutLabel(
                _panelRoot.transform,
                "Select multiple units (Shift+drag), then apply. Names become prefix_1, prefix_2, …",
                11,
                FontStyles.Italic,
                44f);
            hint.color = MissionEditorStyling.LabelSecondary;

            _statusText = AddLayoutLabel(_panelRoot.transform, "", 11, FontStyles.Italic, 28f);
            _statusText.color = MissionEditorStyling.LabelMuted;
            _statusText.alignment = TextAlignmentOptions.TopLeft;
            _statusText.enableWordWrapping = true;

            AddLayoutButton(_panelRoot.transform, "Close", () => SetVisible(false), primary: false);

            _panelRoot.SetActive(false);
            if (!string.IsNullOrEmpty(_pendingStatus))
                SetStatus(_pendingStatus);
            return true;
        }

        void OnApply()
        {
            RefreshSelectionCount();
            string prefix = _prefixInput?.text;
            if (!int.TryParse(_startIndexInput?.text, out int startIndex))
                startIndex = 1;

            if (BatchRename.TryRenameSelected(prefix, startIndex, out string msg))
                SetStatus(msg);
            else
                SetStatus(msg);
        }

        static Canvas FindEditorCanvas()
        {
            GameObject container = GameObject.Find("SceneEssentials/Canvas");
            if (container)
            {
                Canvas[] canvases = container.GetComponentsInChildren<Canvas>(true);
                Canvas pick = null;
                foreach (Canvas c in canvases)
                {
                    if (!c.isActiveAndEnabled) continue;
                    if (c.name.Contains("Menu", System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (pick == null || c.sortingOrder >= pick.sortingOrder) pick = c;
                    }
                }
                if (pick != null) return pick;
            }
            return FindObjectOfType<Canvas>();
        }

        static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static TMP_Text AddLayoutLabel(Transform parent, string text, int size, FontStyles style, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            if (height > 0f)
            {
                le.minHeight = height;
                le.preferredHeight = height;
            }
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = MissionEditorStyling.LabelPrimary;
            tmp.raycastTarget = false;
            return tmp;
        }

        static TMP_InputField AddLayoutInput(Transform parent, string label, string defaultVal)
        {
            if (!string.IsNullOrEmpty(label))
                AddLayoutLabel(parent, label, 12, FontStyles.Normal, 22f);
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 32f;
            le.preferredHeight = 32f;
            go.GetComponent<Image>().color = MissionEditorStyling.InputFieldBackground;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            StretchRect(textRt);
            textRt.offsetMin = new Vector2(8, 4);
            textRt.offsetMax = new Vector2(-8, -4);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.color = MissionEditorStyling.InputFieldText;

            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = textRt;
            field.textComponent = text;
            field.SetTextWithoutNotify(defaultVal);
            return field;
        }

        static void AddLayoutButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, bool primary = false)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 34f;
            le.preferredHeight = 34f;
            var btnImg = go.GetComponent<Image>();
            btnImg.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            if (primary)
            {
                btnImg.color = MissionEditorStyling.AccentGreen;
                tmp.color = MissionEditorStyling.TextOnGreen;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
                colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
                btn.colors = colors;
            }
            else
            {
                btnImg.color = MissionEditorStyling.ButtonNormal;
                tmp.color = MissionEditorStyling.LabelPrimary;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = MissionEditorStyling.ButtonHighlighted;
                colors.pressedColor = MissionEditorStyling.ButtonPressed;
                btn.colors = colors;
            }
        }
    }
}
