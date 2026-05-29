using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuclearOption.MissionEditorScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EditorPlus.AtomicBuilder
{
    /// <summary>In-editor Atomic Builder panel: blueprint library, save/paste.</summary>
    internal sealed class AtomicBuilderUI : MonoBehaviour
    {
        static AtomicBuilderUI _instance;
        static string _pendingStatus;
        static string _lastBlueprintName;
        static string _libraryFilter = "";

        GameObject _panelRoot;
        TMP_InputField _nameInput;
        TMP_InputField _radiusInput;
        TMP_InputField _librarySearchInput;
        TMP_Text _statusText;
        TMP_Text _pathText;
        TMP_Text _libraryCountText;
        Transform _listContent;
        RectTransform _scrollContent;
        string _selectedLibraryName;

        GameObject _tabBlueprint;
        GameObject _tabLibrary;
        readonly List<Button> _tabButtons = new List<Button>();
        readonly List<TextMeshProUGUI> _tabLabels = new List<TextMeshProUGUI>();
        readonly List<GameObject> _libraryRowObjects = new List<GameObject>();
        int _activeTab;

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
            _instance._librarySearchInput = null;
            _instance._statusText = null;
            _instance._pathText = null;
            _instance._libraryCountText = null;
            _instance._listContent = null;
            _instance._scrollContent = null;
            _instance._tabBlueprint = null;
            _instance._tabLibrary = null;
            _instance._tabButtons.Clear();
            _instance._tabLabels.Clear();
            _instance._libraryRowObjects.Clear();
            _instance._activeTab = 0;
            _instance._selectedLibraryName = null;
            _libraryFilter = "";
        }

        void SetVisible(bool on)
        {
            if (_panelRoot == null && !BuildUi()) return;
            _panelRoot.SetActive(on);
            if (on)
            {
                _panelRoot.transform.SetAsLastSibling();
                RefreshBlueprintList();
            }
        }

        bool BuildUi()
        {
            if (_panelRoot != null) return true;

            AtomicBuilderPaths.EnsureBlueprintsFolder();

            Canvas host = FindEditorCanvas();
            if (host == null)
            {
                Plugin.Logger?.LogWarning("[AtomicBuilder] No editor canvas found ????? open the mission editor first.");
                return false;
            }

            _panelRoot = new GameObject("AtomicBuilderPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(host.transform, false);
            var panelRt = _panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0.5f);
            panelRt.anchorMax = new Vector2(1f, 0.5f);
            panelRt.pivot = new Vector2(1f, 0.5f);
            panelRt.sizeDelta = new Vector2(380f, 620f);
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
            _pathText = AddLayoutLabel(_panelRoot.transform, "Blueprints: ?????", 11, FontStyles.Normal, 28f);
            _pathText.color = MissionEditorStyling.LabelMuted;
            _pathText.enableWordWrapping = true;

            BuildTabBar(_panelRoot.transform);
            BuildTabPages(_panelRoot.transform);
            BuildFooter(_panelRoot.transform);

            SelectTab(0);
            UpdatePathLabel();
            _panelRoot.SetActive(false);
            if (!string.IsNullOrEmpty(_pendingStatus))
                SetStatus(_pendingStatus);
            return true;
        }

        void BuildTabBar(Transform parent)
        {
            var bar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(parent, false);
            var barLe = bar.AddComponent<LayoutElement>();
            barLe.minHeight = 34f;
            barLe.preferredHeight = 34f;
            var hlg = bar.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = false;

            _tabButtons.Clear();
            _tabLabels.Clear();
            AddTabButton(bar.transform, "Blueprint", 0);
            AddTabButton(bar.transform, "Library", 1);
        }

        void AddTabButton(Transform parent, string label, int index)
        {
            var go = new GameObject(label + "Tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tabLe = go.GetComponent<LayoutElement>();
            tabLe.minHeight = 30f;
            tabLe.preferredHeight = 30f;
            tabLe.flexibleWidth = 1f;
            var tabImg = go.GetComponent<Image>();
            tabImg.color = MissionEditorStyling.TabInactive;
            tabImg.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = tabImg;
            int captured = index;
            MissionEditorStyling.ApplyTabButton(btn, captured == _activeTab);
            btn.onClick.AddListener(() => SelectTab(captured));
            _tabButtons.Add(btn);

            var textGo = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            StretchRect(textGo.GetComponent<RectTransform>());
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = MissionEditorStyling.TabLabelColor(captured == _activeTab);
            tmp.raycastTarget = false;
            _tabLabels.Add(tmp);
        }

        void BuildTabPages(Transform parent)
        {
            var body = new GameObject("TabBody", typeof(RectTransform));
            body.transform.SetParent(parent, false);
            var bodyLe = body.AddComponent<LayoutElement>();
            bodyLe.flexibleHeight = 1f;
            bodyLe.minHeight = 200f;
            StretchRect(body.GetComponent<RectTransform>());

            _tabBlueprint = CreateTabPage(body.transform, "TabBlueprint");
            _tabLibrary = CreateTabPage(body.transform, "TabLibrary");

            BuildBlueprintTab(_tabBlueprint.transform);
            BuildLibraryTab(_tabLibrary.transform);
        }

        static GameObject CreateTabPage(Transform parent, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            StretchRect(page.GetComponent<RectTransform>());
            page.SetActive(false);
            return page;
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

        void BuildLibraryTab(Transform parent)
        {
            var pageVlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
            pageVlg.spacing = 6f;
            pageVlg.childControlWidth = true;
            pageVlg.childControlHeight = true;
            pageVlg.childForceExpandWidth = true;
            pageVlg.childForceExpandHeight = false;
            pageVlg.padding = new RectOffset(4, 4, 4, 4);

            var searchRow = new GameObject("SearchRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            searchRow.transform.SetParent(parent, false);
            var searchLe = searchRow.AddComponent<LayoutElement>();
            searchLe.minHeight = 34f;
            searchLe.preferredHeight = 34f;
            var searchHlg = searchRow.GetComponent<HorizontalLayoutGroup>();
            searchHlg.spacing = 6f;
            searchHlg.childForceExpandWidth = true;
            searchHlg.childControlHeight = true;

            var searchLabel = AddLayoutLabel(searchRow.transform, "Filter", 12, FontStyles.Normal, 0f);
            searchLabel.GetComponent<LayoutElement>().minWidth = 44f;
            _librarySearchInput = AddLayoutInput(searchRow.transform, "", "");
            _librarySearchInput.onValueChanged.AddListener(f =>
            {
                _libraryFilter = f ?? "";
                RefreshBlueprintList();
            });

            _libraryCountText = AddLayoutLabel(parent, "", 11, FontStyles.Normal, 22f);
            _libraryCountText.color = MissionEditorStyling.LabelMuted;

            var scrollArea = new GameObject("ScrollArea", typeof(RectTransform));
            scrollArea.transform.SetParent(parent, false);
            var scrollLe = scrollArea.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 100f;
            StretchRect(scrollArea.GetComponent<RectTransform>());

            var scrollBg = scrollArea.AddComponent<Image>();
            scrollBg.color = MissionEditorStyling.ScrollBackground;
            scrollBg.raycastTarget = false;

            scrollArea.AddComponent<RectMask2D>();
            var scroll = scrollArea.AddComponent<ScrollRect>();
            scroll.viewport = scrollArea.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scroll.viewport, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            _scrollContent = contentRt;
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 0);
            var contentVlg = contentGo.GetComponent<VerticalLayoutGroup>();
            contentVlg.childForceExpandHeight = false;
            contentVlg.childForceExpandWidth = true;
            contentVlg.spacing = 1f;
            contentVlg.padding = new RectOffset(4, 4, 4, 4);
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRt;
            _listContent = contentRt;

            RefreshBlueprintList();
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

        void SelectTab(int index)
        {
            _activeTab = Mathf.Clamp(index, 0, 1);
            SetTabPageActive(_tabBlueprint, _activeTab == 0);
            SetTabPageActive(_tabLibrary, _activeTab == 1);
            if (_activeTab == 1)
                RefreshBlueprintList();

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = i == _activeTab;
                MissionEditorStyling.ApplyTabButton(_tabButtons[i], active);
                if (i < _tabLabels.Count && _tabLabels[i] != null)
                    _tabLabels[i].color = MissionEditorStyling.TabLabelColor(active);
            }

            if (_panelRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRoot.GetComponent<RectTransform>());
        }

        static void SetTabPageActive(GameObject page, bool active)
        {
            if (page == null) return;
            page.SetActive(active);
            if (active)
                page.transform.SetAsLastSibling();
        }

        void UpdatePathLabel()
        {
            if (_pathText != null)
                _pathText.text = "Blueprints: " + AtomicBuilderPaths.BlueprintsRoot;
        }

        void OnSave()
        {
            string name = _nameInput?.text?.Trim();
            if (!float.TryParse(_radiusInput?.text, out float radius) || radius <= 0f)
                radius = 500f;

            Vector3 center = BlueprintCapture.GetCaptureCenter();
            if (BlueprintCapture.TrySaveBlueprint(name, radius, center, out string msg))
            {
                SetStatus(msg);
                RefreshBlueprintList();
            }
            else
                SetStatus(msg);
        }

        void OnPaste()
        {
            string name = _nameInput?.text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Enter a blueprint name on the Blueprint tab.");
                return;
            }
            RememberBlueprintName(name);
            Vector3 center = BlueprintPaste.GetPasteCenter();
            BlueprintPaste.TryPasteBlueprint(name, center, out string msg);
            SetStatus(msg);
        }

        void RefreshBlueprintList()
        {
            if (_listContent == null) return;

            AtomicBuilderPaths.EnsureBlueprintsFolder();

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);
            _libraryRowObjects.Clear();

            var names = AtomicBuilderPaths.ListBlueprintNames().ToList();
            string filter = (_librarySearchInput != null ? _librarySearchInput.text : _libraryFilter) ?? "";
            if (!string.IsNullOrWhiteSpace(filter))
            {
                string f = filter.Trim();
                names = names.Where(n => n.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            if (_libraryCountText != null)
                _libraryCountText.text = names.Count == 0 ? "No blueprint files in folder." : $"{names.Count} blueprint(s)";

            foreach (string name in names)
            {
                string captured = name;
                bool selected = captured == _selectedLibraryName;
                var row = CreateLibraryRow(_listContent, captured, selected);
                _libraryRowObjects.Add(row);
            }

            if (names.Count > 0 && string.IsNullOrEmpty(_selectedLibraryName))
            {
                _selectedLibraryName = names[0];
                if (_nameInput != null)
                    _nameInput.SetTextWithoutNotify(_selectedLibraryName);
            }

            UpdatePathLabel();
        }

        GameObject CreateLibraryRow(Transform parent, string blueprintName, bool selected)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            var rowImg = row.GetComponent<Image>();
            rowImg.color = selected
                ? MissionEditorStyling.RowSelected
                : MissionEditorStyling.RowBackground;

            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 28f;
            rowLe.preferredHeight = 28f;

            var btn = row.GetComponent<Button>();
            string cap = blueprintName;
            btn.onClick.AddListener(() => SelectLibraryBlueprint(cap));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(row.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.offsetMin = new Vector2(8, 0);
            labelRt.offsetMax = new Vector2(-8, 0);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = blueprintName;
            label.fontSize = 13;
            label.color = selected
                ? MissionEditorStyling.AccentGreen
                : MissionEditorStyling.LabelPrimary;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            return row;
        }

        void SelectLibraryBlueprint(string name)
        {
            _selectedLibraryName = name;
            RememberBlueprintName(name);
            if (_nameInput != null)
                _nameInput.SetTextWithoutNotify(name);
            SelectTab(0);
            SetStatus($"Selected \"{name}\" ????? Ctrl+Alt+V to paste at cursor.");
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
