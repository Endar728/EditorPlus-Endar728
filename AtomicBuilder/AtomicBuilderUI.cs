using System.IO;
using NuclearOption.MissionEditorScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus.AtomicBuilder
{
    /// <summary>In-editor Atomic Builder panel: blueprint save/paste (no on-disk library browser).</summary>
    internal sealed class AtomicBuilderUI : MonoBehaviour
    {
        static AtomicBuilderUI _instance;
        static string _pendingStatus;
        static string _lastBlueprintName;

        GameObject _panelRoot;
        TMP_InputField _nameInput;
        TMP_InputField _radiusInput;
        TMP_Text _statusText;

        internal static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[EditorPlus_AtomicBuilderUI]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AtomicBuilderUI>();
        }

        internal static void TogglePanel()
        {
            if (SceneSingleton<MissionEditor>.i == null || ReflectionUtils.GetMissionObjectives() == null) return;
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

        internal static bool TryGetBlueprintNameForPaste(out string name)
        {
            name = null;
            string fromInput = _instance?._nameInput?.text?.Trim();
            if (!string.IsNullOrEmpty(fromInput) && File.Exists(AtomicBuilderPaths.BlueprintFile(fromInput)))
            {
                name = fromInput;
                _lastBlueprintName = fromInput;
                return true;
            }

            if (!string.IsNullOrEmpty(_lastBlueprintName)
                && File.Exists(AtomicBuilderPaths.BlueprintFile(_lastBlueprintName)))
            {
                name = _lastBlueprintName;
                return true;
            }

            return false;
        }

        static void RememberBlueprintName(string blueprintName)
        {
            if (string.IsNullOrWhiteSpace(blueprintName)) return;
            _lastBlueprintName = blueprintName.Trim();
        }

        internal static void OnSceneUnloaded()
        {
            if (_instance == null) return;
            if (_instance._panelRoot != null)
            {
                Destroy(_instance._panelRoot);
                _instance._panelRoot = null;
            }
            _instance._nameInput = null;
            _instance._radiusInput = null;
            _instance._statusText = null;
        }

        void SetVisible(bool on)
        {
            if (_panelRoot == null && !BuildUi()) return;
            _panelRoot.SetActive(on);
            if (on)
                _panelRoot.transform.SetAsLastSibling();
        }

        bool BuildUi()
        {
            if (_panelRoot != null) return true;

            Canvas host = FindEditorCanvas();
            if (host == null)
            {
                Plugin.Logger?.LogWarning("[AtomicBuilder] No editor canvas found — open the mission editor first.");
                return false;
            }

            _panelRoot = new GameObject("AtomicBuilderPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(host.transform, false);
            var panelRt = _panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0.5f);
            panelRt.anchorMax = new Vector2(1f, 0.5f);
            panelRt.pivot = new Vector2(1f, 0.5f);
            panelRt.sizeDelta = new Vector2(380f, 420f);
            panelRt.anchoredPosition = new Vector2(-10f, 0f);
            _panelRoot.GetComponent<Image>().color = MissionEditorStyling.PanelBackground;
            _panelRoot.transform.SetAsLastSibling();

            var rootLayout = _panelRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(14, 14, 14, 14);
            rootLayout.spacing = 6f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var titleLabel = AddLayoutLabel(_panelRoot.transform, "Atomic Builder", 18, FontStyles.Bold, 28f);
            titleLabel.color = MissionEditorStyling.AccentGreen;

            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(_panelRoot.transform, false);
            var bodyLe = body.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 200f;
            StretchRect(body.GetComponent<RectTransform>());
            BuildBlueprintTab(body.transform);
            BuildFooter(_panelRoot.transform);

            _panelRoot.SetActive(false);
            if (!string.IsNullOrEmpty(_pendingStatus))
                SetStatus(_pendingStatus);
            return true;
        }

        void BuildBlueprintTab(Transform parent)
        {
            var vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            _nameInput = AddLayoutInput(parent, "Blueprint name", "my_base");
            _radiusInput = AddLayoutInput(parent, "Radius (m)", "500");
            AddLayoutButton(parent, "Save blueprint", OnSave, primary: true);
            AddLayoutButton(parent, "Paste at cursor (Ctrl+Alt+V)", OnPaste, primary: true);
            var hint = AddLayoutLabel(parent, "Ctrl+Alt+V pastes blueprint at cursor. Ctrl+V is for copied units only.", 11, FontStyles.Italic, 44f);
            hint.color = MissionEditorStyling.LabelSecondary;
        }

        void BuildFooter(Transform parent)
        {
            var footer = new GameObject("Footer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            footer.transform.SetParent(parent, false);
            var footerLe = footer.AddComponent<LayoutElement>();
            footerLe.minHeight = 64f;
            footerLe.preferredHeight = 64f;
            var footerVlg = footer.GetComponent<VerticalLayoutGroup>();
            footerVlg.spacing = 4f;
            footerVlg.childControlWidth = true;
            footerVlg.childControlHeight = true;
            footerVlg.childForceExpandWidth = true;
            footerVlg.childForceExpandHeight = false;

            _statusText = AddLayoutLabel(footer.transform, "", 11, FontStyles.Italic, 36f);
            _statusText.color = MissionEditorStyling.LabelMuted;
            _statusText.alignment = TextAlignmentOptions.TopLeft;
            _statusText.enableWordWrapping = true;

            AddLayoutButton(footer.transform, "Close", () => SetVisible(false), primary: false);
        }

        void OnSave()
        {
            string name = _nameInput?.text?.Trim();
            if (!float.TryParse(_radiusInput?.text, out float radius) || radius <= 0f)
                radius = 500f;

            Vector3 center = BlueprintCapture.GetCaptureCenter();
            if (BlueprintCapture.TrySaveBlueprint(name, radius, center, out string msg))
                SetStatus(msg);
            else
                SetStatus(msg);
        }

        void OnPaste()
        {
            string name = _nameInput?.text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Enter a blueprint name.");
                return;
            }
            RememberBlueprintName(name);
            Vector3 center = BlueprintPaste.GetPasteCenter();
            BlueprintPaste.TryPasteBlueprint(name, center, out string msg);
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

        static Toggle AddLayoutToggle(Transform parent, string label, bool initial, UnityEngine.Events.UnityAction<bool> onChanged)
        {
            var row = new GameObject("ToggleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var rowLe = row.GetComponent<LayoutElement>();
            rowLe.minHeight = 30f;
            rowLe.preferredHeight = 30f;
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
            toggleGo.transform.SetParent(row.transform, false);
            var toggleLe = toggleGo.GetComponent<LayoutElement>();
            toggleLe.minWidth = 22f;
            toggleLe.preferredWidth = 22f;
            toggleLe.minHeight = 22f;
            toggleLe.preferredHeight = 22f;
            var toggleImg = toggleGo.GetComponent<Image>();
            toggleImg.color = MissionEditorStyling.TabInactive;
            toggleImg.raycastTarget = true;
            var toggle = toggleGo.GetComponent<Toggle>();
            toggle.targetGraphic = toggleImg;
            toggle.isOn = initial;
            toggle.onValueChanged.AddListener(onChanged);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(row.transform, false);
            labelGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return toggle;
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