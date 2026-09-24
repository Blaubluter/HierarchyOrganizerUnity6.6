#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// HierarchyOrganizerWindow – Moderner, erweiterter Hierarchy-Organizer mit Farb-Tabs & Power-Tools
/// Ablegen in:  Assets/JustDoIt_Tools/HierarchyOrganizer/HierarchyOrganizerWindow.cs
/// Öffnen via:  Window → Hierarchy Organizer  (Ctrl+Shift+H)
/// </summary>
public class HierarchyOrganizerWindow : EditorWindow
{
    // ══════════════════════════════════════════════
    //  Datentypen & Datenstrukturen
    // ══════════════════════════════════════════════

    [System.Serializable]
    public class TabData
    {
        public string  name           = "Tab";
        public bool    renaming;
        public string  renameBuffer   = "";
        public int     viewMode       = 1;   // 1=Tab-Inhalt (Pinned), 0=Ganze Szene (Hierarchy)
        public string  searchQuery    = "";
        public string  tagFilter      = "";
        public int     layerFilter    = -1;  // -1 = alle
        public int     colorIndex     = 0;   // Index in PresetColors (0..13)
        public string  customHexColor = "";  // Eigene Farbe (z.B. "#3B82F6")
        public string  iconEmoji      = "";  // Optionales Emoji (z.B. "📁", "🎮", "💡")
        public int     tabStyle       = 0;   // 0=Akzentlinie, 1=Volle Tönung, 2=Pille
        public int     tabKind        = 0;   // 0=Hierarchie, 1=Projekt (wichtig für leere Tabs)
        public string  projectSourceId = "";
        public string  groupedProjectSourceId = "";
        public bool    locked         = false;
        public string  notes          = "";
        public bool    showNotes      = false;
        public Vector2 scroll;
        public List<string> folderGuids = new List<string>();
        public List<string> folderNames = new List<string>();
        public bool showFolderNames = true;
        public string selectedProjectFolderGuid = "";
        public Vector2 projectSubTabScroll;
        [System.NonSerialized] public string newFolderName = "";
        public List<EntityId>     pinnedIDs   = new List<EntityId>();
        public string  quickFilter    = "";  // "", "lights", "ui", "colliders", "audio", "vfx", "missing", "inactive", "favorites"
    }

    [System.Serializable]
    public class ObjectTagColor
    {
        public EntityId entityId;
        public string hexColor;
    }

    public enum ThemeMode
    {
        ModernDark   = 0,
        ExtraBright  = 1,
        UnityNative  = 2,
        HighContrast = 3
    }

    // ══════════════════════════════════════════════
    //  Farb-Presets (14 leuchtende Farben)
    // ══════════════════════════════════════════════

    public static readonly (string name, Color color, string hex)[] PresetColors = new[]
    {
        ("Unity Blau",   new Color(0.23f, 0.51f, 0.96f), "#3B82F6"),
        ("Smaragdgrün",  new Color(0.06f, 0.72f, 0.51f), "#10B981"),
        ("Bernstein",    new Color(0.96f, 0.62f, 0.04f), "#F59E0B"),
        ("Karmesinrot",  new Color(0.94f, 0.27f, 0.27f), "#EF4444"),
        ("Violett",      new Color(0.55f, 0.36f, 0.96f), "#8B5CF6"),
        ("Cyan Türkis",  new Color(0.02f, 0.71f, 0.83f), "#06B6D4"),
        ("Pink Rose",    new Color(0.93f, 0.28f, 0.60f), "#EC4899"),
        ("Limonengrün",  new Color(0.52f, 0.80f, 0.09f), "#84CC16"),
        ("Goldgelb",     new Color(0.92f, 0.70f, 0.03f), "#EAB308"),
        ("Indigo",       new Color(0.39f, 0.40f, 0.95f), "#6366F1"),
        ("Magenta",      new Color(0.85f, 0.27f, 0.94f), "#D946EF"),
        ("Himmelblau",   new Color(0.22f, 0.74f, 0.97f), "#38BDF8"),
        ("Koralle",      new Color(0.98f, 0.45f, 0.09f), "#F97316"),
        ("Schiefergrau", new Color(0.58f, 0.64f, 0.72f), "#94A3B8"),
    };

    public static readonly (string name, Color color, string hex)[] ObjectHighlightColors = new[]
    {
        ("Rot",      new Color(0.95f, 0.30f, 0.30f), "#EF4444"),
        ("Grün",     new Color(0.15f, 0.80f, 0.45f), "#10B981"),
        ("Blau",     new Color(0.30f, 0.60f, 1.00f), "#3B82F6"),
        ("Gelb",     new Color(1.00f, 0.80f, 0.20f), "#F59E0B"),
        ("Violett",  new Color(0.70f, 0.40f, 1.00f), "#8B5CF6"),
        ("Orange",   new Color(1.00f, 0.50f, 0.15f), "#F97316"),
        ("Türkis",   new Color(0.10f, 0.85f, 0.85f), "#06B6D4"),
        ("Pink",     new Color(0.98f, 0.40f, 0.70f), "#EC4899"),
    };

    // ══════════════════════════════════════════════
    //  State
    // ══════════════════════════════════════════════

    List<TabData>                 _tabs               = new List<TabData>();
    int                           _selectedTab;
    HashSet<EntityId>             _expanded           = new HashSet<EntityId>();
    HashSet<EntityId>             _favorites          = new HashSet<EntityId>();
    Dictionary<EntityId, string>  _customObjectColors = new Dictionary<EntityId, string>();
    bool                          _globalSearch;
    string                        _globalQuery        = "";

    // Settings
    ThemeMode                     _currentTheme       = ThemeMode.ModernDark;
    float                         _rowHeight          = 26f;
    bool                          _showComponentIcons = true;
    bool                          _showHierarchyLines = true;
    bool                          _showQuickFilterBar = true;
    bool                          _showQuickFilterLabels;
    bool                          _showTagLayerBar;
    bool                          _showTooltips       = true;
    int                           _autosaveIntervalMinutes = 5;
    bool                          _autoNameTabs;
    bool                          _englishUI = true;
    const string                 LanguagePrefKey = "HierarchyOrganizer.LanguageEnglish";
    const string                 HierarchyTabName = "Hierarchie";
    const string                 LegacyMainTabName = "Main";

    // Drag & Reorder
    int                           _dragOverTab        = -1;
    int                           _dragSourceTab      = -1;
    EntityId                      _dragSourceID       = default;
    Vector2                       _dragStartPos;
    const float                   DragThreshold       = 8f;
    const string                  EntryDragKey        = "HierarchyOrganizer.EntryDrag";
    EntityId                      _entryDropTargetID  = default;
    int                           _entryDropZone      = -1; // 0=vorher, 1=Kind, 2=nachher
    TabData                       _entryDropTab;
    Vector2                       _globalScroll;
    Vector2                       _quickFilterScroll;
    bool                          _quickFiltersExpanded;

    readonly List<GameObject> _visibleSelectionRows = new List<GameObject>();
    readonly List<GameObject> _drawnSelectionRows = new List<GameObject>();
    readonly HashSet<EntityId> _drawnSelectionIDs = new HashSet<EntityId>();
    readonly HashSet<EntityId> _selectedIDs = new HashSet<EntityId>();
    readonly Dictionary<EntityId, bool> _filterMatchCache = new Dictionary<EntityId, bool>();
    readonly Dictionary<EntityId, Component[]> _componentCache = new Dictionary<EntityId, Component[]>();
    readonly Dictionary<EntityId, string> _tabMembershipCache = new Dictionary<EntityId, string>();
    readonly List<GameObject> _filteredSceneCache = new List<GameObject>();
    string _filteredSceneCacheKey;
    int _hierarchyVersion;
    int _stateVersion;
    GameObject _selectionAnchor;
    GameObject _pendingSingleSelection;
    EntityId _lastClickedObjectID;
    double _lastClickedObjectTime = -1;

    sealed class OrganizerEntryDrag
    {
        public TabData tab;
        public EntityId sourceID;
        public bool pinnedRoot;
    }

    // Die Farbzuordnung folgt den Pins; sie verändert keine Objektfarben.
    class TabColorGroup
    {
        public string key;
        public Color color;
        public readonly List<TabData> tabs = new List<TabData>();
        public readonly HashSet<EntityId> pinnedIDs = new HashSet<EntityId>();
    }

    sealed class QuickFilterOption
    {
        public readonly string key;
        public readonly GUIContent compactContent;
        public readonly GUIContent labeledContent;

        public QuickFilterOption(string key, string icon, string label, string tooltip)
        {
            this.key = key;
            compactContent = new GUIContent(icon, tooltip);
            labeledContent = new GUIContent(icon + " " + label, tooltip);
        }
    }

    static readonly QuickFilterOption[] QuickFilterOptions =
    {
        new QuickFilterOption("",          "●",  "Alle",       "Alle Objekte anzeigen und den Schnellfilter zurücksetzen."),
        new QuickFilterOption("triggers",  "⚡", "Trigger",    "Trigger-, Instruction- und EventTrigger-Objekte anzeigen."),
        new QuickFilterOption("actions",   "🎬", "Actions",    "Actions, Instructions, Sequenzen und Bedingungen anzeigen."),
        new QuickFilterOption("character", "👤", "Charaktere", "Charaktere, Player, NPCs, Gegner und Einheiten anzeigen."),
        new QuickFilterOption("variables", "📊", "Variablen",  "Variablen, Properties, Blackboards, Stats und Inventare anzeigen."),
        new QuickFilterOption("lights",    "💡", "Licht",      "Lichter, Reflection Probes und Light Probe Groups anzeigen."),
        new QuickFilterOption("cameras",   "🎥", "Kameras",    "Kameras, Cinemachine-Objekte und Shots anzeigen."),
        new QuickFilterOption("ui",        "🖼️", "UI",         "UI-Objekte mit RectTransform oder Canvas anzeigen."),
        new QuickFilterOption("colliders", "🔲", "Collider",   "Objekte mit 3D- oder 2D-Collidern anzeigen."),
        new QuickFilterOption("physics",   "⚖️", "Physik",     "Rigidbodies, Joints und Constant Forces anzeigen."),
        new QuickFilterOption("navmesh",   "🏃", "NavMesh",    "NavMesh Agents und Obstacles anzeigen."),
        new QuickFilterOption("audio",     "🔊", "Audio",      "Audio Sources, Listener und Reverb Zones anzeigen."),
        new QuickFilterOption("vfx",       "💥", "VFX",        "Partikel, Trails, Lines und Visual Effects anzeigen."),
        new QuickFilterOption("missing",   "⚠️", "Fehlend",    "Objekte mit fehlenden Komponenten anzeigen."),
        new QuickFilterOption("favorites", "⭐", "Favoriten",  "Nur als Favorit markierte Objekte anzeigen."),
        new QuickFilterOption("inactive",  "👁️", "Inaktiv",    "Nur inaktive Objekte anzeigen.")
    };

    readonly List<TabColorGroup> _tabColorGroups = new List<TabColorGroup>();
    TabData _sceneColorFilterTab;
    TabColorGroup _activeSceneColorGroup;
    int _tabColorGroupsVersion = -1;

    // Selten veränderte Editorlisten werden nicht bei jedem IMGUI-Event neu erzeugt.
    string[] _tagFilterOptions;
    string[] _layerFilterOptions;
    int[] _layerFilterValues;

    // Tooltip
    GameObject                    _hoveredGO;
    Vector2                       _hoverMousePos;
    double                        _hoverStartTime;
    const float                   TooltipDelay        = 0.15f;

    // Style-Cache
    GUIStyle _stTabActive, _stTabNormal, _stHeader;
    GUIStyle _stEntryName, _stEntryPath, _stBadge;
    GUIStyle _stNotes, _stFilterChip, _stFilterChipActive, _stPrefabCardName;

    // Autosave
    const int    AutosaveMaxFiles   = 5;
    double       _lastAutosaveTime  = -1;
    string       _lastAutosaveLabel = "";
    bool         _autosaveEnabled   = true;
    Scene _dataScene;
    HierarchyOrganizerSceneData _sceneStorage;
    bool _stateLoaded, _writingState, _suspendPersistence, _loadFailed;
    bool _persistenceSuppressedUntilOrganizerChange;
    bool _stateDirty;
    Scene _closingScene;
    string _lastSavedJson = "";
    double _nextStateSync;
    double _nextMaintenanceTime;

    public TabData CurrentTab => _tabs.Count > 0
        ? _tabs[Mathf.Clamp(_selectedTab, 0, _tabs.Count - 1)]
        : null;

    string GetAutosaveFolder()
    {
        string sceneKey = AssetDatabase.AssetPathToGUID(_dataScene.path ?? "");
        if (string.IsNullOrEmpty(sceneKey))
        {
            string session = SessionState.GetString("HierarchyOrganizer.Session", "");
            if (string.IsNullOrEmpty(session))
            {
                session = System.Guid.NewGuid().ToString("N");
                SessionState.SetString("HierarchyOrganizer.Session", session);
            }
            sceneKey = "Unsaved_" + session + "_" + _dataScene.handle;
        }
        // Backups are editor-local data, not assets. Keeping them in UserSettings
        // avoids imports, postprocessors and preview-cache invalidation on autosave.
        string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
        return System.IO.Path.Combine(projectRoot, "UserSettings", "HierarchyOrganizer", "Backups", sceneKey);
    }

    // ══════════════════════════════════════════════
    //  Dynamische Theme-Farben (Heller & Kontrastreich)
    // ══════════════════════════════════════════════

    Color ColBg => _currentTheme switch
    {
        ThemeMode.ExtraBright  => new Color(0.30f, 0.30f, 0.30f),
        ThemeMode.UnityNative  => EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.76f, 0.76f, 0.76f),
        ThemeMode.HighContrast => new Color(0.12f, 0.12f, 0.12f),
        _                      => new Color(0.24f, 0.24f, 0.24f)
    };

    Color ColTabBar => _currentTheme switch
    {
        ThemeMode.ExtraBright  => new Color(0.24f, 0.24f, 0.24f),
        ThemeMode.UnityNative  => EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.68f, 0.68f, 0.68f),
        ThemeMode.HighContrast => new Color(0.08f, 0.08f, 0.08f),
        _                      => new Color(0.18f, 0.18f, 0.18f)
    };

    Color ColTabBg => _currentTheme switch
    {
        ThemeMode.ExtraBright  => new Color(0.34f, 0.34f, 0.34f),
        ThemeMode.HighContrast => new Color(0.16f, 0.16f, 0.16f),
        _                      => new Color(0.27f, 0.27f, 0.27f)
    };

    Color ColTabActive => _currentTheme switch
    {
        ThemeMode.ExtraBright  => new Color(0.38f, 0.38f, 0.38f),
        ThemeMode.HighContrast => new Color(0.22f, 0.22f, 0.22f),
        _                      => new Color(0.31f, 0.31f, 0.31f)
    };

    Color ColTabHover => _currentTheme switch
    {
        ThemeMode.ExtraBright  => new Color(0.40f, 0.40f, 0.40f),
        _                      => new Color(0.35f, 0.35f, 0.35f)
    };

    Color ColRowAlt => _currentTheme switch
    {
        ThemeMode.ExtraBright  => new Color(0.27f, 0.27f, 0.27f),
        ThemeMode.HighContrast => new Color(0.10f, 0.10f, 0.10f),
        _                      => new Color(0.215f, 0.215f, 0.215f)
    };

    Color ColHover    => new Color(0.28f, 0.40f, 0.58f, 0.85f);
    Color ColSelected => new Color(0.24f, 0.48f, 0.82f, 0.95f);
    Color ColSep      => new Color(0.16f, 0.16f, 0.16f);
    Color ColDragOver => new Color(1.00f, 0.65f, 0.10f, 0.90f);
    Color ColFav      => new Color(1.00f, 0.85f, 0.20f);
    Color ColLocked   => new Color(1.00f, 0.45f, 0.35f);

    public Color GetTabColor(TabData tab)
    {
        if (tab == null) return new Color(0.90f, 0.28f, 0.28f);
        return IsProjectTab(tab)
            ? new Color(0.24f, 0.72f, 0.36f)
            : new Color(0.90f, 0.28f, 0.28f);
    }

    // ══════════════════════════════════════════════
    //  Fenster öffnen & Unity Menüs
    // ══════════════════════════════════════════════

    [MenuItem("Window/Hierarchy Organizer %#h")]
    public static void Open()
    {
        var w = GetWindow<HierarchyOrganizerWindow>();
        w.minSize      = new Vector2(320, 280);
        w.titleContent = new GUIContent("Hierarchy Organizer", EditorGUIUtility.IconContent("UnityEditor.HierarchyWindow").image);
        w.Show();
    }

    [MenuItem("GameObject/Pin to Hierarchy Organizer", false, -10)]
    public static void PinSelectedToHierarchyOrganizer()
    {
        var w = GetWindow<HierarchyOrganizerWindow>();
        if (w != null && w.CurrentTab != null)
        {
            int added = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (!w.IsObjectInContext(go)) continue;
                EntityId id = go.GetEntityId();
                if (!w.CurrentTab.pinnedIDs.Contains(id))
                {
                    w.CurrentTab.pinnedIDs.Add(id);
                    w._expanded.Add(id);
                    added++;
                }
            }
            w.CurrentTab.viewMode = 1;
            w.StateChanged();
            if (added > 0)
                w.ShowNotification(new GUIContent(added + " Objekt(e) zu '" + w.CurrentTab.name + "' hinzugefügt"));
        }
    }

    [MenuItem("GameObject/Pin to Hierarchy Organizer", true)]
    public static bool ValidatePinSelected() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

    // ══════════════════════════════════════════════
    //  Lifecycle
    // ══════════════════════════════════════════════

    void OnEnable()
    {
        wantsMouseMove = true;
        _englishUI = EditorPrefs.GetBool(LanguagePrefKey, true);
        _prefabPreviewSize = Mathf.Clamp(EditorPrefs.GetFloat(PreviewSizePrefKey, 104f), 64f, 240f);
        EditorApplication.projectChanged += RefreshFolderAssets;
        DragAndDrop.AddDropHandlerV2((DragAndDrop.SceneDropHandler)ObserveProjectSceneDrop);
        DragAndDrop.AddDropHandlerV2((DragAndDrop.HierarchyDropHandlerV2)ObserveProjectHierarchyDrop);
        _suspendPersistence = EditorApplication.isPlayingOrWillChangePlaymode;
        LoadState();

        Selection.selectionChanged         += OnSelectionChanged;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.update           += AutosaveCheck;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.newSceneCreated += OnNewSceneCreated;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.projectChanged -= RefreshFolderAssets;
        DragAndDrop.RemoveDropHandlerV2((DragAndDrop.SceneDropHandler)ObserveProjectSceneDrop);
        DragAndDrop.RemoveDropHandlerV2((DragAndDrop.HierarchyDropHandlerV2)ObserveProjectHierarchyDrop);
        Selection.selectionChanged         -= OnSelectionChanged;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.update           -= AutosaveCheck;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SaveState();
        if (_autosaveEnabled) DoAutosave();
    }

    void OnSelectionChanged()
    {
        // Auswahländerungen beeinflussen nur die Hervorhebung. Sie verändern keine
        // Organizer-Daten mehr und lösen deshalb auch keinen Speichervorgang aus.
        Repaint();
    }

    void OnHierarchyChanged()
    {
        _hierarchyVersion++;
        _componentCache.Clear();
        InvalidateViewCaches();
        // Änderungen durch SaveState (z. B. das versteckte Speicherobjekt) dürfen
        // keinen weiteren Speichervorgang auslösen.
        if (!_writingState)
        {
            _stateDirty = true;
            _stateVersion++;
            _nextStateSync = EditorApplication.timeSinceStartup + 0.75;
        }
        Repaint();
    }

    void InvalidateViewCaches()
    {
        _filteredSceneCacheKey = null;
        _filterMatchCache.Clear();
        _tabMembershipCache.Clear();
    }

    internal void StateChanged()
    {
        _persistenceSuppressedUntilOrganizerChange = false;
        _stateDirty = true;
        _stateVersion++;
        _nextStateSync = EditorApplication.timeSinceStartup + 0.75;
        InvalidateViewCaches();
        Repaint();
    }

    // ══════════════════════════════════════════════
    //  GUI – Hauptstruktur
    // ══════════════════════════════════════════════

    void OnGUI()
    {
        wantsMouseMove = true;
        EnsureSceneContext();
        EnsureStyles();
        _prefabCardControlId = GUIUtility.GetControlID("HierarchyOrganizer.PrefabCardDrag".GetHashCode(), FocusType.Passive);
        HandlePrefabCardGesture();
        if (!_stateLoaded)
        {
            DrawHint(_loadFailed ? "Die Organizer-Daten dieser Szene konnten nicht geladen werden. Details stehen in der Console."
                : "Öffne eine Szene, um ihren Organizer zu verwenden.");
            return;
        }
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), ColBg);

        // Hover wird nur beim Repaint-Event zurückgesetzt & neu gesetzt
        // Bei anderen Events (MouseMove etc.) bleibt der letzte Wert erhalten
        GameObject prevHovered = _hoveredGO;
        if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseMove)
        {
            _hoveredGO = null;
        }

        EditorGUI.BeginChangeCheck();
        DrawToolbar();

        // Alle Such- und Filterelemente bleiben fest ganz oben. Sie gehören zum
        // aktuell gewählten Tab; ein Tab-Wechsel aktualisiert sie im nächsten Repaint.
        var tab = CurrentTab;
        if (tab != null)
        {
            bool isMainTab = IsHierarchyTab(tab);
            tab.viewMode = isMainTab ? 0 : 1;

            if (!string.IsNullOrEmpty(tab.searchQuery)) tab.searchQuery = "";
            _globalSearch = false;
            _globalQuery = "";
            if (_showTagLayerBar)
                DrawFilterBar(tab);

            if (_showQuickFilterBar)
                DrawQuickFilterChips(tab);
        }

        DrawTabBar();

        tab = CurrentTab;
        if (tab == null)
        {
            if (EditorGUI.EndChangeCheck()) StateChanged();
            return;
        }

        _selectedIDs.Clear();
        var selectedObjects = Selection.gameObjects;
        for (int i = 0; i < selectedObjects.Length; i++)
            if (selectedObjects[i] != null) _selectedIDs.Add(selectedObjects[i].GetEntityId());

        _sceneColorFilterTab = null;
        _activeSceneColorGroup = null;

        if (Event.current.type == EventType.Repaint)
        {
            _drawnSelectionRows.Clear();
            _drawnSelectionIDs.Clear();
        }
        DrawContent(tab);
        if (Event.current.type == EventType.Repaint)
        {
            _visibleSelectionRows.Clear();
            _visibleSelectionRows.AddRange(_drawnSelectionRows);
        }
        if (Event.current.rawType == EventType.MouseUp || Event.current.type == EventType.DragExited)
        {
            _pendingSingleSelection = null;
            _dragSourceID = default;
        }

        // Tooltip immer zuletzt zeichnen (über allem anderen)
        DrawObjectTooltip(prevHovered);

        if (EditorGUI.EndChangeCheck()) StateChanged();

        if (Event.current.type == EventType.MouseMove)
        {
            Repaint();
        }
    }

    // ── Toolbar ─────────────────────────────────

    bool CompactLayout => position.width < 390f;
    string T(string german, string english) => _englishUI ? english : german;

    void DrawToolbar()
    {
        bool compact = CompactLayout;
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (!compact)
            GUILayout.Label("Hierarchy Organizer", _stHeader ?? EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

        if (GUILayout.Button(new GUIContent(_englishUI ? "EN" : "DE",
                T("Oberfläche auf Englisch umstellen.", "Switch the interface to German.")),
            EditorStyles.toolbarButton, GUILayout.Width(27f)))
        {
            _englishUI = !_englishUI;
            EditorPrefs.SetBool(LanguagePrefKey, _englishUI);
            Repaint();
        }

        if (GUILayout.Button(new GUIContent(compact ? "↻" : "↻ Hierarchie",
            T("Hierarchie neu einlesen: Öffnet den Hierarchie-Tab mit der gesamten aktuellen Szene und setzt dort alle Filter zurück.",
              "Refresh Hierarchy: Opens the Hierarchy tab with the complete current scene and resets all filters.")),
            EditorStyles.toolbarButton, GUILayout.Width(compact ? 25f : 56f)))
            RefreshMainView();

        if (!string.IsNullOrEmpty(_lastAutosaveLabel))
        {
            var sc = GUI.color; GUI.color = new Color(0.6f, 0.95f, 0.65f);
            if (GUILayout.Button(new GUIContent(compact ? "💾" : "💾 " + _lastAutosaveLabel,
                    T("Ordner der automatischen Sicherungen öffnen.", "Open the automatic-backup folder.")),
                EditorStyles.toolbarButton, GUILayout.Width(compact ? 25f : 72f)))
            {
                string folder = GetAutosaveFolder();
                if (System.IO.Directory.Exists(folder)) EditorUtility.RevealInFinder(folder);
            }
            GUI.color = sc;
        }

        var currentTab = CurrentTab;
        bool hasTagLayerFilter = currentTab != null &&
            (!string.IsNullOrEmpty(currentTab.tagFilter) || currentTab.layerFilter >= 0);
        var gCol = _showTagLayerBar ? new Color(0.40f, 0.90f, 1f)
            : hasTagLayerFilter ? new Color(1f, 0.72f, 0.25f) : new Color(0.75f, 0.75f, 0.75f);
        var old = GUI.color; GUI.color = gCol;
        if (GUILayout.Button(new GUIContent("T/L", _showTagLayerBar
                ? T("Tag- und Layer-Filter ausblenden.", "Hide tag and layer filters.")
                : hasTagLayerFilter ? T("Tag- und Layer-Filter einblenden – ein Filter ist aktiv.", "Show tag and layer filters — a filter is active.")
                : T("Tag- und Layer-Filter einblenden.", "Show tag and layer filters.")),
            EditorStyles.toolbarButton, GUILayout.Width(30f)))
            _showTagLayerBar = !_showTagLayerBar;
        GUI.color = old;

        if (GUILayout.Button(new GUIContent(compact ? "+" : "+ Obj",
                T("Neues Szenenobjekt erstellen", "Create a new scene object")),
            EditorStyles.toolbarButton, GUILayout.Width(compact ? 25f : 43f)))
            ShowCreateObjectMenu();

        float autoPulse = 0.5f + 0.5f * Mathf.Sin((float)EditorApplication.timeSinceStartup * Mathf.PI);
        var autoNameCol = _autoNameTabs
            ? Color.Lerp(new Color(0.62f, 0.12f, 0.10f), new Color(1f, 0.48f, 0.38f), autoPulse)
            : new Color(0.75f, 0.75f, 0.75f);
        GUI.color = autoNameCol;
        if (GUILayout.Button(new GUIContent(compact ? (_autoNameTabs ? "●" : "○") : (_autoNameTabs ? "● Auto" : "○ Auto"),
            T("Auto: " + (_autoNameTabs ? "AN" : "AUS") + "\nWenn aktiv, erzeugen platzierte Projekt-Prefabs automatisch eine Gruppe und einen Hierarchie-Tab. Leere Tabs werden nach dem ersten Objekt benannt.",
              "Auto: " + (_autoNameTabs ? "ON" : "OFF") + "\nWhen enabled, placed project prefabs automatically create a group and hierarchy tab. Empty tabs are named after their first object.")),
            EditorStyles.toolbarButton, GUILayout.Width(compact ? 25f : 55f)))
        {
            _autoNameTabs = !_autoNameTabs;
            if (!_autoNameTabs) DragAndDrop.SetGenericData(ProjectDragKey, null);
            StateChanged();
            Repaint();
        }
        GUI.color = old;

        GUI.color = new Color(0.7f, 0.95f, 0.75f);
        if (GUILayout.Button(new GUIContent(compact ? "↑" : "↑ Save", T("Backup speichern.", "Save backup.")),
            EditorStyles.toolbarButton, GUILayout.Width(compact ? 25f : 48f)))
            SaveBackup();
        GUI.color = new Color(0.7f, 0.85f, 1f);
        if (GUILayout.Button(new GUIContent(compact ? "↓" : "↓ Load", T("Backup laden.", "Load backup.")),
            EditorStyles.toolbarButton, GUILayout.Width(compact ? 25f : 48f)))
            LoadBackup();
        GUI.color = old;

        if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(26)))
            ShowSettingsMenu();

        EditorGUILayout.EndHorizontal();
    }

    void ShowCreateObjectMenu()
    {
        var tab = CurrentTab;
        if (tab == null || IsProjectTab(tab))
        {
            ShowNotification(new GUIContent(T("Objekte können nur in Hierarchie-Tabs erstellt werden.",
                "Objects can only be created from hierarchy tabs.")));
            return;
        }
        if (tab.locked)
        {
            ShowNotification(new GUIContent(T("Dieser Tab ist gesperrt.", "This tab is locked.")));
            return;
        }

        var menu = new GenericMenu();
        try
        {
            AddUnityCreateItems(menu, tab);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            ShowNotification(new GUIContent(T("Unitys Erstellen-Menü konnte nicht geladen werden.",
                "Could not load Unity's Create menu.")));
            return;
        }
        menu.ShowAsContext();
    }

    // Use Unity's menu builder so package entries, validation, ordering and
    // temporary MenuCommand contexts match the native Hierarchy.
    void AddUnityCreateItems(GenericMenu menu, TabData tab)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var editorAssembly = typeof(EditorApplication).Assembly;
        var menuUtils = editorAssembly.GetType("UnityEditor.MenuUtils", true);
        var builder = menuUtils.GetMethods(flags).Single(method =>
            method.Name == "AddCreateGameObjectItemsToMenu" && method.GetParameters().Length == 9);
        var parameters = builder.GetParameters();
        var originType = parameters[6].ParameterType;
        var sceneType = parameters[5].ParameterType;
        var parent = Selection.activeGameObject;
        if (parent != null && (!IsObjectInContext(parent) || EditorUtility.IsPersistent(parent)))
            parent = null;
        UnityEngine.Object[] context = parent != null ? new UnityEngine.Object[] { parent } : null;
        var scene = parent != null ? parent.scene : _dataScene;
        if (!scene.IsValid() || !scene.isLoaded)
            throw new System.InvalidOperationException("No loaded scene for object creation.");

        var callback = new NativeCreateContext(this, tab, scene);
        var callbackFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        var before = typeof(NativeCreateContext).GetMethod("Before", callbackFlags)
            .MakeGenericMethod(originType, sceneType);
        var after = typeof(NativeCreateContext).GetMethod("After", callbackFlags)
            .MakeGenericMethod(originType, sceneType);
        builder.Invoke(null, new object[] {
            menu, context, true, true, false, scene.handle,
            System.Enum.Parse(originType, parent != null ? "GameObject" : "Toolbar"),
            System.Delegate.CreateDelegate(parameters[7].ParameterType, callback, before),
            System.Delegate.CreateDelegate(parameters[8].ParameterType, callback, after)
        });

        // Packages can also contribute through the native Hierarchy hook.
        var hooks = editorAssembly.GetType("UnityEditor.SceneManagement.SceneHierarchyHooks");
        hooks?.GetMethod("AddCustomItemsToCreateMenu", flags)?.Invoke(null, new object[] { menu });
    }

    sealed class NativeCreateContext
    {
        readonly HierarchyOrganizerWindow owner;
        readonly TabData tab;
        readonly Scene scene;
        HashSet<EntityId> existing;
        System.Reflection.MethodInfo setTarget;
        double focusReturnDeadline;
        double nextFocusCheck;

        public NativeCreateContext(HierarchyOrganizerWindow owner, TabData tab, Scene scene)
        {
            this.owner = owner;
            this.tab = tab;
            this.scene = scene;
        }

        public void Before<TOrigin, TScene>(string path, UnityEngine.Object[] context, TOrigin origin, TScene target)
        {
            existing = new HashSet<EntityId>(Resources.FindObjectsOfTypeAll<GameObject>()
                .Select(go => go.GetEntityId()));
            setTarget = typeof(EditorSceneManager).GetMethod("SetTargetSceneForNewGameObjects",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(TScene) }, null);
            if (setTarget == null)
                throw new System.MissingMethodException("SetTargetSceneForNewGameObjects");
            setTarget.Invoke(null, new object[] { target });
        }

        public void After<TOrigin, TScene>(string path, UnityEngine.Object[] context, TOrigin origin, TScene target)
        {
setTarget.Invoke(null, new object[] { default(TScene) });
            // Focus restoration must not depend on pinning or tab membership.
            focusReturnDeadline = EditorApplication.timeSinceStartup + 1.25;
            nextFocusCheck = EditorApplication.timeSinceStartup + 0.15;
            EditorApplication.update -= RestoreOrganizerFocus;
            EditorApplication.update += RestoreOrganizerFocus;
            // Let the native command own parenting, component setup and Undo.
            // Only expose newly selected objects in the tab that opened the menu.
            if (owner == null || tab.locked || !owner._tabs.Contains(tab)) return;
            bool isMain = IsHierarchyTab(tab);
            foreach (var go in Selection.gameObjects)
            {
                if (go == null || go.scene != scene || existing == null || existing.Contains(go.GetEntityId())) continue;
                var id = go.GetEntityId();
                if (!isMain)
                {
                    owner.AutoNameEmptyTab(tab, go);
                    if (!tab.pinnedIDs.Contains(id)) tab.pinnedIDs.Add(id);
                }
                for (var parent = go.transform.parent; parent != null; parent = parent.parent)
                    owner._expanded.Add(parent.gameObject.GetEntityId());
            }
            owner.StateChanged();
            owner.Repaint();
        }

        void RestoreOrganizerFocus()
        {
            double now = EditorApplication.timeSinceStartup;
            if (owner == null || now >= focusReturnDeadline || EditorApplication.isCompiling)
            {
                EditorApplication.update -= RestoreOrganizerFocus;
                return;
            }
            if (now < nextFocusCheck) return;
            nextFocusCheck = now + 0.1;

            var focused = EditorWindow.focusedWindow;
            if (focused == owner) return;
            // Creation may focus the Inspector or Scene View before the Hierarchy.
            // Preserve a package's dedicated wizard instead of stealing its focus.
            string focusedType = focused != null ? focused.GetType().FullName : null;
            if (focused != null && focusedType != "UnityEditor.SceneHierarchyWindow" &&
                focusedType != "UnityEditor.InspectorWindow" && !(focused is SceneView))
            {
                EditorApplication.update -= RestoreOrganizerFocus;
                return;
            }
            // Show selects the dock tab; Focus alone can leave it behind another tab.
            owner.Show();
            owner.Focus();
            owner.Repaint();
            // Keep observing briefly: native framing/rename callbacks can focus
            // the original Hierarchy again on a later editor update.
        }
    }

    void RefreshMainView()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            ShowNotification(new GUIContent("Keine aktive Szene geladen."));
            return;
        }

        var main = _tabs.Find(IsHierarchyTab);
        if (main == null)
        {
            main = new TabData { name = HierarchyTabName, iconEmoji = "📁" };
            _tabs.Insert(0, main);
        }

        _selectedTab = _tabs.IndexOf(main);
        main.viewMode = 0;
        main.searchQuery = "";
        main.tagFilter = "";
        main.layerFilter = -1;
        main.quickFilter = "";
        main.scroll = Vector2.zero;
        _globalSearch = false;
        _globalQuery = "";
        _globalScroll = Vector2.zero;
        _sceneColorFilterTab = null;
        _hoveredGO = null;
        RebuildTabColorGroups();

        // Die Szenenansicht liest bei jedem Zeichnen die aktuellen Root-Objekte
        // und deren Kinder direkt aus der geöffneten Szene.
        StateChanged();
        ShowNotification(new GUIContent("Hierarchie neu eingelesen: " + scene.name));
    }

    // ── Tab Bar ──────────────────────────────────

    static bool IsProjectTab(TabData tab)
    {
        return tab != null && tab.tabKind == 1;
    }

    static bool IsHierarchyTab(TabData tab)
    {
        if (tab == null || IsProjectTab(tab)) return false;
        string name = tab.name?.Trim();
        return string.Equals(name, HierarchyTabName, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, LegacyMainTabName, System.StringComparison.OrdinalIgnoreCase);
    }

    bool CanAcceptTabDrop(TabData tab)
    {
        if (IsProjectTab(tab))
            return DragAndDrop.objectReferences.Any(IsProjectFolder);
        return DragAndDrop.objectReferences.Any(o =>
            o is GameObject go && !EditorUtility.IsPersistent(go) && IsObjectInContext(go));
    }

    string GetTabDisplayName(TabData tab)
    {
        string prefix = string.IsNullOrEmpty(tab.iconEmoji) ? "" : tab.iconEmoji + " ";
        return prefix + tab.name;
    }

    Rect[] CalculateTabLayout(float width, List<int> indices, bool projectTabs, out float height)
    {
        float padding = CompactLayout ? 3f : 4f;
        float gap = CompactLayout ? 2f : 3f;
        float rowHeight = projectTabs ? (CompactLayout ? 52f : 60f) : (CompactLayout ? 29f : 36f);
        float tabHeight = projectTabs ? (CompactLayout ? 49f : 56f) : (CompactLayout ? 26f : 32f);
        float availableWidth = Mathf.Max(1f, width - padding * 2);
        var rects = new Rect[indices.Count + 1]; // Der letzte Platz gehört dem Plus-Button.
        float x = padding;
        float y = 2f;
        for (int slot = 0; slot <= indices.Count; slot++)
        {
            float itemWidth;
            if (slot == indices.Count)
            {
                itemWidth = Mathf.Min(26f, availableWidth);
            }
            else
            {
                var tab = _tabs[indices[slot]];
                string measuredText = tab.renaming ? tab.renameBuffer : GetTabDisplayName(tab);
                float controlsWidth = 48f;
                float measuredWidth = EditorStyles.label.CalcSize(new GUIContent(measuredText)).x + 12f + controlsWidth;
                if (projectTabs)
                {
                    int subFolderCount = GetDirectProjectSubFolders(tab).Count;
                    if (subFolderCount > 0)
                        measuredWidth = Mathf.Max(measuredWidth, (subFolderCount + 1) * 21f + 6f);
                }
                itemWidth = Mathf.Clamp(measuredWidth, CompactLayout ? 56f : 64f, Mathf.Min(210f, availableWidth));
            }
            if (x > padding && x + itemWidth > padding + availableWidth)
            {
                x = padding;
                y += rowHeight;
            }
            rects[slot] = new Rect(x, y, itemWidth, tabHeight);
            x += itemWidth + gap;
        }
        height = y + tabHeight + 2f;
        return rects;
    }

    void DrawTabBar()
    {
        DrawTabSection(true, T("PROJEKT-TABS", "PROJECT TABS"));
        DrawTabSection(false, T("HIERARCHIE-TABS", "HIERARCHY TABS"));
    }

    sealed class ProjectSubFolder
    {
        public readonly string guid;
        public readonly string path;
        public readonly GUIContent content;

        public ProjectSubFolder(string guid, string path, string label, string tooltip = null)
        {
            this.guid = guid ?? "";
            this.path = path ?? "";
            content = new GUIContent(label, tooltip ?? path);
        }
    }

    List<ProjectSubFolder> GetDirectProjectSubFolders(TabData tab)
    {
        var result = new List<ProjectSubFolder>();
        var seen = new HashSet<string>();
        foreach (string masterGuid in tab.folderGuids ?? new List<string>())
        {
            string masterPath = AssetDatabase.GUIDToAssetPath(masterGuid);
            if (!AssetDatabase.IsValidFolder(masterPath)) continue;
            // Only categories located directly below a Prefabs folder become
            // sub-tabs. If Prefabs itself was dropped, use it as the root.
            IEnumerable<string> prefabRoots = string.Equals(System.IO.Path.GetFileName(masterPath), "Prefabs",
                    System.StringComparison.OrdinalIgnoreCase)
                ? new[] { masterPath }
                : AssetDatabase.GetSubFolders(masterPath).Where(path =>
                    string.Equals(System.IO.Path.GetFileName(path), "Prefabs",
                        System.StringComparison.OrdinalIgnoreCase));
            foreach (string prefabRoot in prefabRoots)
            {
                foreach (string childPath in AssetDatabase.GetSubFolders(prefabRoot))
                {
                    string guid = AssetDatabase.AssetPathToGUID(childPath);
                    if (string.IsNullOrEmpty(guid) || !seen.Add(guid)) continue;
                    string folderName = System.IO.Path.GetFileName(childPath);
                    string shortLabel = string.IsNullOrEmpty(folderName)
                        ? "?" : folderName.Substring(0, 1).ToUpperInvariant();
                    result.Add(new ProjectSubFolder(guid, childPath, shortLabel,
                        folderName + "\n" + childPath));
                }
            }
        }
        return result.OrderBy(folder => folder.content.text, System.StringComparer.OrdinalIgnoreCase).ToList();
    }

    void DrawProjectSubFolderTabs(TabData tab, int tabIndex, Rect groupRect)
    {
        var folders = GetDirectProjectSubFolders(tab);
        if (folders.Count == 0)
        {
            tab.selectedProjectFolderGuid = "";
            return;
        }
        if (!string.IsNullOrEmpty(tab.selectedProjectFolderGuid)
            && !folders.Any(folder => folder.guid == tab.selectedProjectFolderGuid))
            tab.selectedProjectFolderGuid = "";

        const float gap = 1f;
        float mainHeight = CompactLayout ? 26f : 32f;
        Rect bar = new Rect(groupRect.x, groupRect.y + mainHeight + 2f, groupRect.width,
            Mathf.Max(1f, groupRect.height - mainHeight - 2f));
        EditorGUI.DrawRect(bar, new Color(ColTabBar.r, ColTabBar.g, ColTabBar.b, 0.98f));
        Rect viewport = new Rect(bar.x + 3f, bar.y,
            Mathf.Max(1f, bar.width - 6f), Mathf.Max(1f, bar.height));
        var entries = new List<ProjectSubFolder>
        {
            new ProjectSubFolder("", "", "M",
                T("Master – alle Prefab-Unterordner anzeigen", "Master – show all prefab subfolders"))
        };
        entries.AddRange(folders);
        float[] widths = Enumerable.Repeat(20f, entries.Count).ToArray();
        float contentWidth = widths.Sum() + gap * (widths.Length - 1);
        float maxScroll = Mathf.Max(0f, contentWidth - viewport.width);
        tab.projectSubTabScroll.x = Mathf.Clamp(tab.projectSubTabScroll.x, 0f, maxScroll);
        if (Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition) && maxScroll > 0f)
        {
            tab.projectSubTabScroll.x = Mathf.Clamp(tab.projectSubTabScroll.x + Event.current.delta.y * 24f, 0f, maxScroll);
            Event.current.Use();
            Repaint();
        }

        GUI.BeginGroup(viewport);
        float x = -tab.projectSubTabScroll.x;
        for (int i = 0; i < entries.Count; i++)
        {
            ProjectSubFolder entry = entries[i];
            bool active = tab.selectedProjectFolderGuid == entry.guid;
            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = active && tabIndex == _selectedTab ? new Color(0.78f, 0.78f, 0.78f)
                : new Color(0.56f, 0.56f, 0.56f);
            if (active)
                GUI.Label(new Rect(x + 4f, -1f, 12f, 8f), new GUIContent("▼",
                    T("Aktiver Unterordner", "Active subfolder")), EditorStyles.centeredGreyMiniLabel);
            if (GUI.Button(new Rect(x, 6f, widths[i], 15f), entry.content, EditorStyles.miniButton))
            {
                _selectedTab = tabIndex;
                tab.selectedProjectFolderGuid = entry.guid;
                tab.scroll = Vector2.zero;
                StateChanged();
            }
            GUI.backgroundColor = oldBackground;
            x += widths[i] + gap;
        }
        GUI.EndGroup();
        EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), ColSep);
    }

    void DrawTabSection(bool projectTabs, string heading)
    {
        var indices = Enumerable.Range(0, _tabs.Count)
            .Where(i => IsProjectTab(_tabs[i]) == projectTabs).ToList();
        int n = indices.Count;
        var tabRects = CalculateTabLayout(position.width, indices, projectTabs, out float tabsHeight);
        float headingHeight = CompactLayout ? 13f : 17f;
        float barHeight = tabsHeight + headingHeight;
        var bar = GUILayoutUtility.GetRect(0, barHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bar, ColTabBar);
        GUI.Label(new Rect(bar.x + 5, bar.y, bar.width - 10, headingHeight), heading, EditorStyles.miniBoldLabel);

        for (int slot = 0; slot < n; slot++)
        {
            int i = indices[slot];
            var  tab      = _tabs[i];
            bool active   = i == _selectedTab;
            bool dragOver = _dragOverTab == i;
            var groupRect = tabRects[slot];
            groupRect.position += bar.position;
            groupRect.y += headingHeight;
            var tRect = groupRect;
            if (projectTabs) tRect.height = CompactLayout ? 26f : 32f;
            var  tabCol   = GetTabColor(tab);

            Color baseBg = active ? ColTabActive : ColTabBg;
            if (dragOver) baseBg = ColDragOver;
            EditorGUI.DrawRect(tRect, baseBg);

            float topH = active ? 2.5f : 1.5f;
            Color mutedTabCol = new Color(tabCol.r, tabCol.g, tabCol.b, active ? 0.82f : 0.38f);
            EditorGUI.DrawRect(new Rect(tRect.x, tRect.y, tRect.width, topH), mutedTabCol);
            if (projectTabs)
            {
                float markerPulse = 0.5f + 0.5f *
                    Mathf.Sin((float)EditorApplication.timeSinceStartup * Mathf.PI);
                var markerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = active ? 15 : 13
                };
                markerStyle.normal.textColor = active
                    ? Color.Lerp(new Color(0.92f, 0.03f, 0.02f),
                        new Color(1f, 0.90f, 0.78f), markerPulse)
                    : new Color(0.78f, 0.78f, 0.78f, 0.82f);
                GUI.Label(new Rect(tRect.x + 1f, tRect.y + 8f, 15f, 16f),
                    new GUIContent(active ? "●" : "○", active
                        ? T("Aktiver Projekt-Tab", "Active project tab")
                        : T("Inaktiver Projekt-Tab", "Inactive project tab")),
                    markerStyle);
            }
            var cR = new Rect(tRect.xMax - 23, tRect.y + 7, 18, 18);
            var lockR = new Rect(tRect.xMax - 44, tRect.y + 7, 18, 18);

            if (tab.renaming)
            {
                string ctrl = "TabRename_" + i;
                bool confirm = false, cancel = false;
                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    { confirm = true; Event.current.Use(); }
                    else if (Event.current.keyCode == KeyCode.Escape)
                    { cancel  = true; Event.current.Use(); }
                }
                if (Event.current.type == EventType.MouseDown && !tRect.Contains(Event.current.mousePosition))
                    confirm = true;

                if (confirm || cancel)
                {
                    if (confirm) tab.name = GetUniqueTabName(tab.renameBuffer, tab);
                    tab.renaming = false;
                    GUI.FocusControl(null);
                    StateChanged();
                }
                else
                {
                    GUI.SetNextControlName(ctrl);
                    tab.renameBuffer = GUI.TextField(
                        new Rect(tRect.x + 6, tRect.y + 6, tRect.width - 34, tRect.height - 10),
                        tab.renameBuffer, EditorStyles.miniTextField);
                    if (GUI.GetNameOfFocusedControl() != ctrl) GUI.FocusControl(ctrl);
                }
            }
            else
            {
                string displayName = GetTabDisplayName(tab);

                float textX = tRect.x + (projectTabs ? 16f : 8f);
                string tooltip = tab.name + "\n" + (tab.folderGuids?.Count ?? 0) + T(" Projektordner, ", " project folders, ")
                    + tab.pinnedIDs.Count + T(" Hierarchieobjekte", " hierarchy objects");
                float textWidth = Mathf.Max(1f, tRect.xMax - textX - 48f);
                GUI.Label(new Rect(textX, tRect.y + 3, textWidth, tRect.height - 4),
                    new GUIContent(displayName, tooltip), active ? (_stTabActive ?? EditorStyles.boldLabel) : (_stTabNormal ?? EditorStyles.label));

                if (Event.current.type == EventType.MouseDown && tRect.Contains(Event.current.mousePosition)
                    && !lockR.Contains(Event.current.mousePosition) && !cR.Contains(Event.current.mousePosition))
                {
                    if (Event.current.button == 1)
                    {
                        ShowTabContextMenu(i);
                    }
                    else if (Event.current.clickCount == 2)
                    {
                        tab.renaming = true;
                        tab.renameBuffer = tab.name;
                    }
                    else
                    {
                        _lastClickedObjectID = default;
                        _lastClickedObjectTime = -1;
                        _selectedTab = i;
                        if (IsProjectTab(tab)) tab.selectedProjectFolderGuid = "";
                        _dragSourceTab = i;
                    }
                    Event.current.Use();
                    StateChanged();
                }
            }

            var lockColor = GUI.color;
            GUI.color = tab.locked ? ColLocked : new Color(1f, 1f, 1f, 0.55f);
            if (GUI.Button(lockR, new GUIContent(tab.locked ? "🔒" : "🔓",
                    tab.locked ? T("Tab entsperren", "Unlock tab") : T("Tab sperren", "Lock tab")), EditorStyles.miniButton))
            {
                tab.locked = !tab.locked;
                StateChanged();
            }
            GUI.color = lockColor;

            bool closeHovered = cR.Contains(Event.current.mousePosition);
            var oc = GUI.color;
            var ob = GUI.backgroundColor;
            GUI.color = closeHovered ? Color.white : new Color(1f, 1f, 1f, 0.62f);
            GUI.backgroundColor = closeHovered ? new Color(0.92f, 0.30f, 0.30f) : new Color(0.34f, 0.34f, 0.34f);
            if (GUI.Button(cR, new GUIContent("×", T("Tab löschen", "Delete tab")), EditorStyles.miniButton))
            {
                RemoveTab(i);
                return;
            }
            GUI.color = oc;
            GUI.backgroundColor = ob;

            HandleTabDragDrop(i, tRect, tab);
            if (projectTabs) DrawProjectSubFolderTabs(tab, i, groupRect);

        }

        var addR = tabRects[n];
        addR.position += bar.position;
        addR.y += headingHeight;
        var plusR = new Rect(addR.x, addR.y + 2, 26, Mathf.Min(26, addR.height - 2));
        bool dropHover = plusR.Contains(Event.current.mousePosition) && CanAcceptNewAutoTabDrop(projectTabs);
        HandleNewAutoTabDrop(plusR, projectTabs);
        var oldColor = GUI.color;
        var oldBackground = GUI.backgroundColor;
        GUI.color = new Color(1f, 1f, 1f, 0.82f);
        if (dropHover) GUI.backgroundColor = ColDragOver;
        if (GUI.Button(plusR, new GUIContent("+", T(
                projectTabs ? "Neuen Projekt-Tab hinzufügen oder Ordner hier ablegen" : "Neuen Hierarchie-Tab hinzufügen oder Objekt hier ablegen",
                projectTabs ? "Add a project tab or drop a folder here" : "Add a hierarchy tab or drop an object here")), EditorStyles.miniButton))
            AddTab(projectTabs);
        GUI.color = oldColor;
        GUI.backgroundColor = oldBackground;

        EditorGUI.DrawRect(new Rect(bar.x, bar.yMax, bar.width, 1), ColSep);

        if (Event.current.type == EventType.DragExited)
        {
            _dragOverTab   = -1;
            _dragSourceTab = -1;
            _dragSourceID  = default;
            Repaint();
        }
    }

    void AutoNameEmptyTab(TabData tab, Object firstObject)
    {
        if (!_autoNameTabs || tab == null || firstObject == null || tab.locked || tab.pinnedIDs.Count > 0 || (tab.folderGuids?.Count ?? 0) > 0)
            return;
        if (IsHierarchyTab(tab))
            return;

        tab.name = GetUniqueTabName(firstObject.name, tab);
        tab.renameBuffer = tab.name;
        tab.renaming = false;
    }

    void HandleTabDragDrop(int tabIndex, Rect tRect, TabData tab)
    {
        var ev = Event.current.type;
        if (tRect.Contains(Event.current.mousePosition))
        {
            if (ev == EventType.DragUpdated)
            {
                bool hasGo = CanAcceptTabDrop(tab);
                DragAndDrop.visualMode = hasGo ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                _dragOverTab = hasGo ? tabIndex : -1;
                Event.current.Use();
                Repaint();
            }
            else if (ev == EventType.DragPerform)
            {
                if (!CanAcceptTabDrop(tab)) return;
                DragAndDrop.AcceptDrag();
                int added = IsProjectTab(tab) ? AddDroppedFolders(tab) : 0;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (!IsProjectTab(tab) && obj is GameObject go &&
                        !EditorUtility.IsPersistent(go) && IsObjectInContext(go))
                    {
                        EntityId id = go.GetEntityId();
                        if (!tab.pinnedIDs.Contains(id))
                        {
                            AutoNameEmptyTab(tab, go);
                            tab.pinnedIDs.Add(id);
                            _expanded.Add(id);
                            added++;
                        }
                    }
                }
                if (added > 0)
                {
                    _selectedTab = tabIndex;
                    tab.viewMode = 1;
                }
                _dragOverTab = -1;
                Event.current.Use();
                StateChanged();
            }
            else if (ev == EventType.DragExited)
            {
                _dragOverTab = -1;
                Repaint();
            }
        }
        else if (_dragOverTab == tabIndex && (ev == EventType.DragUpdated || ev == EventType.DragExited))
        {
            _dragOverTab = -1;
            Repaint();
        }
    }

    // ── Tab Kontextmenü ──────────────────────────

    void ShowTabContextMenu(int idx)
    {
        var tab  = _tabs[idx];
        var menu = new GenericMenu();

        string[] sampleEmojis = { "📁 Ordner", "🏠 Main", "🎮 Gameplay", "🖼️ GUI/UI", "💡 Beleuchtung", "🔊 Audio", "🤖 KI/Enemies", "🎬 Kamera/Cutscene", "📦 Props/Assets", "⚙️ Setup", "🚩 Spawns", "💎 Collectibles", "✨ Effekte" };
        foreach (var se in sampleEmojis)
        {
            string emoji = se.Split(' ')[0];
            menu.AddItem(new GUIContent("Icon / Emoji/" + se), tab.iconEmoji == emoji, () => { tab.iconEmoji = emoji; StateChanged(); });
        }
        menu.AddItem(new GUIContent("Icon / Emoji/✕ Kein Icon"), string.IsNullOrEmpty(tab.iconEmoji), () => { tab.iconEmoji = ""; StateChanged(); });

        menu.AddSeparator("");

        if (idx > 0)
            menu.AddItem(new GUIContent(T("Nach links verschieben", "Move left")), false, () => MoveTab(idx, -1));
        else
            menu.AddDisabledItem(new GUIContent(T("Nach links verschieben", "Move left")));

        if (idx < _tabs.Count - 1)
            menu.AddItem(new GUIContent(T("Nach rechts verschieben", "Move right")), false, () => MoveTab(idx, 1));
        else
            menu.AddDisabledItem(new GUIContent(T("Nach rechts verschieben", "Move right")));

        menu.AddItem(new GUIContent(T("Tab duplizieren", "Duplicate tab")), false, () => DuplicateTab(idx));

        menu.AddSeparator("");
        menu.AddItem(new GUIContent(T("Umbenennen", "Rename")), false, () => { tab.renaming = true; tab.renameBuffer = tab.name; StateChanged(); });
        menu.AddItem(new GUIContent(tab.locked ? T("Entsperren", "Unlock") : T("Sperren", "Lock")), false, () => { tab.locked = !tab.locked; StateChanged(); });

        menu.AddSeparator("");
        menu.AddItem(new GUIContent(T("Tab leeren (alle Pins entfernen)", "Clear tab (remove all pins)")), false, () =>
        {
            if (tab.pinnedIDs.Count == 0) return;
            if (EditorUtility.DisplayDialog("Tab leeren?", $"Alle {tab.pinnedIDs.Count} angehefteten Objekte aus '{tab.name}' entfernen?", "Ja, leeren", "Abbrechen"))
            {
                tab.pinnedIDs.Clear();
                StateChanged();
            }
        });
        menu.AddItem(new GUIContent(T("Tab löschen", "Delete tab")), false, () => RemoveTab(idx));

        menu.ShowAsContext();
    }

    void MoveTab(int fromIndex, int dir)
    {
        int toIndex = fromIndex + dir;
        if (toIndex < 0 || toIndex >= _tabs.Count) return;
        var t = _tabs[fromIndex];
        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(toIndex, t);
        _selectedTab = toIndex;
        StateChanged();
    }

    void DuplicateTab(int index)
    {
        var src = _tabs[index];
        string uniqueName = GetUniqueTabName(src.name + " (Kopie)");
        var clone = new TabData
        {
            name           = uniqueName,
            viewMode       = src.viewMode,
            searchQuery    = src.searchQuery,
            tagFilter      = src.tagFilter,
            layerFilter    = src.layerFilter,
            colorIndex     = src.colorIndex,
            customHexColor = src.customHexColor,
            iconEmoji      = src.iconEmoji,
            tabStyle       = src.tabStyle,
            tabKind        = src.tabKind,
            notes          = src.notes,
            showNotes      = src.showNotes,
            pinnedIDs      = new List<EntityId>(src.pinnedIDs),
            folderNames    = new List<string>(src.folderNames ?? new List<string>()),
            showFolderNames = src.showFolderNames,
            folderGuids = new List<string>(src.folderGuids ?? new List<string>()),
            selectedProjectFolderGuid = src.selectedProjectFolderGuid,
            projectSubTabScroll = src.projectSubTabScroll
        };
        _tabs.Insert(index + 1, clone);
        _selectedTab = index + 1;
        StateChanged();
    }

    // ── Suchleisten ─────────────────────────────

    void DrawSearchBar(TabData tab)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
        GUILayout.Space(6);
        tab.searchQuery = EditorGUILayout.TextField(tab.searchQuery, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
        if (!string.IsNullOrEmpty(tab.searchQuery))
        {
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
                tab.searchQuery = "";
        }
        GUILayout.Space(4);
        EditorGUILayout.EndHorizontal();

        DrawFilterBar(tab);
    }

    void DrawGlobalSearchBar()
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
        GUILayout.Space(6);
        GUI.color = new Color(1f, 0.95f, 0.70f);
        _globalQuery = EditorGUILayout.TextField(_globalQuery, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
        GUI.color = Color.white;
        if (!string.IsNullOrEmpty(_globalQuery))
        {
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
                _globalQuery = "";
        }
        GUILayout.Space(4);
        EditorGUILayout.EndHorizontal();
    }

    void EnsureFilterOptions()
    {
        if (_tagFilterOptions != null && _layerFilterOptions != null && _layerFilterValues != null)
            return;

        string[] editorTags = UnityEditorInternal.InternalEditorUtility.tags ?? new string[0];
        _tagFilterOptions = new string[editorTags.Length + 1];
        _tagFilterOptions[0] = "(alle)";
        System.Array.Copy(editorTags, 0, _tagFilterOptions, 1, editorTags.Length);

        var layerNames = new List<string> { "(alle)" };
        var layerValues = new List<int> { -1 };
        for (int layer = 0; layer < 32; layer++)
        {
            string layerName = LayerMask.LayerToName(layer);
            if (string.IsNullOrEmpty(layerName)) continue;
            layerNames.Add(layerName);
            layerValues.Add(layer);
        }
        _layerFilterOptions = layerNames.ToArray();
        _layerFilterValues = layerValues.ToArray();
    }

    void DrawFilterBar(TabData tab)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
        GUILayout.Space(6);

        try
        {
            EnsureFilterOptions();

            GUILayout.Label(CompactLayout ? "T" : "Tag:", EditorStyles.miniLabel, GUILayout.Width(CompactLayout ? 12f : 28f));
            int tagIdx = Mathf.Max(0, System.Array.IndexOf(_tagFilterOptions, tab.tagFilter));
            int newTag = EditorGUILayout.Popup(tagIdx, _tagFilterOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactLayout ? 70f : 80f));
            tab.tagFilter = newTag == 0 ? "" : _tagFilterOptions[newTag];

            GUILayout.Space(CompactLayout ? 3f : 8f);

            GUILayout.Label(CompactLayout ? "L" : "Layer:", EditorStyles.miniLabel, GUILayout.Width(CompactLayout ? 12f : 38f));
            int layerIdx = System.Array.IndexOf(_layerFilterValues, tab.layerFilter);
            if (layerIdx < 0) layerIdx = 0;
            int newLayer = EditorGUILayout.Popup(layerIdx, _layerFilterOptions, EditorStyles.toolbarPopup, GUILayout.Width(CompactLayout ? 70f : 80f));
            tab.layerFilter = _layerFilterValues[Mathf.Clamp(newLayer, 0, _layerFilterValues.Length - 1)];

        }
        catch
        {
            GUILayout.Label("Filter n/a", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Space(4);
        EditorGUILayout.EndHorizontal();
    }

    void ActivateMainTab()
    {
        int mainIndex = _tabs.FindIndex(IsHierarchyTab);
        if (mainIndex < 0)
        {
            ShowNotification(new GUIContent(T("Kein Tab namens Hierarchie gefunden.", "No Hierarchy tab was found.")));
            return;
        }

        _selectedTab = mainIndex;
        _sceneColorFilterTab = null;
        _activeSceneColorGroup = null;
        StateChanged();
        Repaint();
    }

    // ── Schnellfilter-Chips (1-Klick Filter) ───────

    void DrawQuickFilterChips(TabData tab)
    {
        float mainButtonWidth = CompactLayout ? 40f : 55f;
        // Extra Reserve berücksichtigt GUILayout-Ränder und unterschiedliche Emoji-Breiten
        // der Unity-Skins. Der Pfeil erscheint dadurch, bevor das erste Symbol verschwindet.
        float availableForFilters = Mathf.Max(30f, position.width - 6f - 28f - 8f - mainButtonWidth - 7f - 70f);
        bool hasOverflow = GetQuickFilterRequiredWidth() > availableForFilters;
        if (!hasOverflow) _quickFiltersExpanded = false;

        EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
        GUILayout.Space(6);

        var oldColor = GUI.color;
        GUI.color = _showQuickFilterLabels ? new Color(0.40f, 0.90f, 1f) : new Color(0.72f, 0.72f, 0.72f);
        if (GUILayout.Button(new GUIContent("Aa", _showQuickFilterLabels
                ? "Filter-Bezeichnungen ausblenden – die Tooltips bleiben erhalten."
                : "Filter-Bezeichnungen einblenden."),
            EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(20)))
            _showQuickFilterLabels = !_showQuickFilterLabels;
        GUI.color = oldColor;
        GUILayout.Space(8f);

        if (GUILayout.Button(new GUIContent(CompactLayout ? "Hier." : T("Zu Hierarchie", "Go to Hierarchy"),
                T("Zum Tab Hierarchie wechseln.", "Switch to the Hierarchy tab.")), EditorStyles.miniButton,
            GUILayout.Width(mainButtonWidth), GUILayout.Height(20f)))
            ActivateMainTab();

        GUILayout.Space(7f);

        if (hasOverflow)
        {
            var arrowColor = GUI.color;
            GUI.color = new Color(0.45f, 0.90f, 1f);
            if (GUILayout.Button(new GUIContent(CompactLayout
                        ? (_quickFiltersExpanded ? "▲" : "▼")
                        : (_quickFiltersExpanded ? "▲ Filter" : "▼ Filter"),
                    T(_quickFiltersExpanded ? "Filter einklappen" : "Alle Filter nach unten aufklappen",
                      _quickFiltersExpanded ? "Collapse filters" : "Expand all filters below")),
                EditorStyles.miniButton, GUILayout.Width(CompactLayout ? 26f : 58f), GUILayout.Height(20f)))
            {
                _quickFiltersExpanded = !_quickFiltersExpanded;
                _quickFilterScroll = Vector2.zero;
            }
            GUI.color = arrowColor;
            GUILayout.Space(3f);
        }

        if (_quickFiltersExpanded)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            DrawExpandedQuickFilters(tab);
            return;
        }

        _quickFilterScroll = EditorGUILayout.BeginScrollView(_quickFilterScroll, GUIStyle.none, GUIStyle.none, GUILayout.Height(26));
        EditorGUILayout.BeginHorizontal();

        DrawChip(tab, QuickFilterOptions[0]);
        DrawQuickFilterGroupLabel("GC2", "Filter für Game Creator 2.");
        for (int i = 1; i <= 4; i++)
            DrawChip(tab, QuickFilterOptions[i]);
        DrawQuickFilterGroupLabel("UNITY", "Filter für Unity-Komponenten und allgemeine Objektzustände.");
        for (int i = 5; i < QuickFilterOptions.Length; i++)
            DrawChip(tab, QuickFilterOptions[i]);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);
        EditorGUILayout.EndHorizontal();
    }

    float GetQuickFilterRequiredWidth()
    {
        float width = 3f + 1f + 27f + 3f + 1f + 36f;
        for (int i = 0; i < QuickFilterOptions.Length; i++)
            width += GetQuickFilterWidth(QuickFilterOptions[i]) + 2f;
        return width;
    }

    float GetQuickFilterWidth(QuickFilterOption option)
    {
        if (!_showQuickFilterLabels) return 28f;
        var style = _stFilterChip ?? EditorStyles.miniButtonMid;
        return Mathf.Max(28f, Mathf.Ceil(style.CalcSize(GetQuickFilterContent(option)).x) + 10f);
    }

    void DrawExpandedQuickFilters(TabData tab)
    {
        float available = Mathf.Max(80f, position.width - 12f);
        float x = 0f, y = 0f;
        const float rowHeight = 22f, gap = 3f;
        var rects = new List<Rect>();
        var options = new List<QuickFilterOption>();
        var labels = new List<string>();

        System.Action<QuickFilterOption> addOption = option =>
        {
            float width = GetQuickFilterWidth(option);
            if (x > 0f && x + width > available) { x = 0f; y += rowHeight + gap; }
            rects.Add(new Rect(x, y, width, rowHeight)); options.Add(option); labels.Add(null);
            x += width + gap;
        };
        System.Action<string> addLabel = label =>
        {
            float width = label == "GC2" ? 36f : 48f;
            if (x > 0f && x + width > available) { x = 0f; y += rowHeight + gap; }
            rects.Add(new Rect(x, y, width, rowHeight)); options.Add(null); labels.Add(label);
            x += width + gap;
        };

        addOption(QuickFilterOptions[0]);
        addLabel("GC2");
        for (int i = 1; i <= 4; i++) addOption(QuickFilterOptions[i]);
        addLabel("UNITY");
        for (int i = 5; i < QuickFilterOptions.Length; i++) addOption(QuickFilterOptions[i]);

        Rect area = GUILayoutUtility.GetRect(0f, y + rowHeight + 3f, GUILayout.ExpandWidth(true));
        for (int i = 0; i < rects.Count; i++)
        {
            Rect rect = rects[i]; rect.position += new Vector2(area.x + 6f, area.y);
            if (options[i] != null) DrawChipAt(rect, tab, options[i]);
            else
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 1f, rect.height - 6f), new Color(1f, 1f, 1f, 0.18f));
                GUI.Label(new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height), labels[i], EditorStyles.centeredGreyMiniLabel);
            }
        }
    }

    void DrawQuickFilterGroupLabel(string label, string tooltip)
    {
        GUILayout.Space(3f);
        Rect separator = GUILayoutUtility.GetRect(1f, 16f, GUILayout.Width(1f), GUILayout.Height(16f));
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(separator, new Color(1f, 1f, 1f, 0.18f));
        GUILayout.Label(new GUIContent(label, tooltip), EditorStyles.centeredGreyMiniLabel,
            GUILayout.Width(label == "GC2" ? 27f : 36f), GUILayout.Height(20f));
    }

    void DrawChip(TabData tab, QuickFilterOption option)
    {
        bool active = tab.quickFilter == option.key;
        var style   = (active ? _stFilterChipActive : _stFilterChip) ?? EditorStyles.miniButtonMid;
        var tabCol  = GetTabColor(tab);
        GUIContent content = GetQuickFilterContent(option);

        var oldBg = GUI.backgroundColor;
        if (active) GUI.backgroundColor = tabCol;

        bool clicked = _showQuickFilterLabels
            ? GUILayout.Button(content, style)
            : GUILayout.Button(content, style, GUILayout.Width(28));
        if (clicked)
        {
            tab.quickFilter = (active && option.key != "") ? "" : option.key;
        }
        GUI.backgroundColor = oldBg;
    }

    void DrawChipAt(Rect rect, TabData tab, QuickFilterOption option)
    {
        bool active = tab.quickFilter == option.key;
        var style = (active ? _stFilterChipActive : _stFilterChip) ?? EditorStyles.miniButtonMid;
        var oldBg = GUI.backgroundColor;
        if (active) GUI.backgroundColor = GetTabColor(tab);
        if (GUI.Button(rect, GetQuickFilterContent(option), style))
            tab.quickFilter = (active && option.key != "") ? "" : option.key;
        GUI.backgroundColor = oldBg;
    }

    GUIContent GetQuickFilterContent(QuickFilterOption option)
    {
        if (!_englishUI) return _showQuickFilterLabels ? option.labeledContent : option.compactContent;
        string label = EnglishQuickFilterLabel(option.key);
        return new GUIContent(option.compactContent.text + (_showQuickFilterLabels ? " " + label : ""),
            "Show " + label.ToLowerInvariant() + " objects.");
    }

    static string EnglishQuickFilterLabel(string key)
    {
        switch (key)
        {
            case "": return "All";
            case "lights": return "Lights";
            case "triggers": return "Triggers";
            case "actions": return "Actions";
            case "character": return "Characters";
            case "variables": return "Variables";
            case "cameras": return "Cameras";
            case "ui": return "UI";
            case "colliders": return "Colliders";
            case "physics": return "Physics";
            case "navmesh": return "NavMesh";
            case "audio": return "Audio";
            case "vfx": return "VFX";
            case "missing": return "Missing";
            case "favorites": return "Favorites";
            case "inactive": return "Inactive";
            default: return key;
        }
    }

    // ── Tab-Filter: jedes Hierarchie-Tab ist ein eigener Filter ──

    void RebuildTabColorGroups()
    {
        _tabColorGroups.Clear();
        if (_sceneColorFilterTab != null && !_tabs.Contains(_sceneColorFilterTab))
            _sceneColorFilterTab = null;

        for (int i = 0; i < _tabs.Count; i++)
        {
            var sourceTab = _tabs[i];
            if (IsProjectTab(sourceTab)) continue;
            var group = new TabColorGroup { key = i.ToString(), color = GetTabColor(sourceTab) };
            _tabColorGroups.Add(group);
            group.tabs.Add(sourceTab);
            if (sourceTab.pinnedIDs != null)
                group.pinnedIDs.UnionWith(sourceTab.pinnedIDs);
        }

        _activeSceneColorGroup = _sceneColorFilterTab == null ? null
            : _tabColorGroups.Find(g => g.tabs.Contains(_sceneColorFilterTab));
        _tabColorGroupsVersion = _stateVersion;
    }

    void DrawTabColorFilterBar(TabData viewTab)
    {
        if (_tabColorGroupsVersion != _stateVersion)
            RebuildTabColorGroups();
        else
            _activeSceneColorGroup = _sceneColorFilterTab == null ? null
                : _tabColorGroups.Find(g => g.tabs.Contains(_sceneColorFilterTab));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUILayout.Label(new GUIContent("Tab-Filter", "Objekte eines bestimmten Hierarchie-Tabs anzeigen. Unterobjekte gehören dazu."),
            EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Alle Tabs", "Tab-Filter aufheben; andere Suchfilter bleiben erhalten."),
            _activeSceneColorGroup == null ? EditorStyles.miniButtonMid : EditorStyles.miniButton,
            GUILayout.Width(82)))
        {
            _sceneColorFilterTab = null;
            _activeSceneColorGroup = null;
            viewTab.scroll = Vector2.zero;
            _globalScroll = Vector2.zero;
        }
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        // Größe und Zeilenhöhe aus dem vollständigen Text berechnen. Eigene
        // Rechtecke vermeiden zusätzliche GUILayout-Abstände an den Zeilenrändern.
        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 0f,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };
        float availableWidth = Mathf.Max(80f, position.width - 12f);
        var filterRects = new Rect[_tabColorGroups.Count];
        var filterLabels = new GUIContent[_tabColorGroups.Count];
        float x = 0f, y = 0f, rowHeight = 0f;
        for (int i = 0; i < _tabColorGroups.Count; i++)
        {
            var content = new GUIContent(_tabColorGroups[i].tabs[0].name);
            filterLabels[i] = content;
            float width = Mathf.Min(availableWidth, Mathf.Max(75f,
                Mathf.Ceil(labelStyle.CalcSize(content).x) + 18f));
            float height = Mathf.Max(24f,
                Mathf.Ceil(labelStyle.CalcHeight(content, width - 12f)) + 8f);
            if (x > 0f && x + width > availableWidth)
            {
                x = 0f;
                y += rowHeight + 4f;
                rowHeight = 0f;
            }
            filterRects[i] = new Rect(x, y, width, height);
            rowHeight = Mathf.Max(rowHeight, height);
            x += width + 4f;
        }

        var filterArea = GUILayoutUtility.GetRect(0, y + rowHeight + 3f, GUILayout.ExpandWidth(true));
        for (int i = 0; i < _tabColorGroups.Count; i++)
        {
            var group = _tabColorGroups[i];
            string names = filterLabels[i].text;
            bool active = group == _activeSceneColorGroup;
            Rect rect = filterRects[i];
            rect.position += new Vector2(filterArea.x + 6f, filterArea.y);
            string tooltip = "Tab: " + names
                + "\nZeigt dessen angeheftete Objekte einschließlich ihrer Unterobjekte."
                + (active ? "\nErneut klicken, um den Tab-Filter aufzuheben." : "");
            if (GUI.Button(rect, new GUIContent(names, tooltip),
                active ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
            {
                _sceneColorFilterTab = active ? null : group.tabs[0];
                _activeSceneColorGroup = active ? null : group;
                viewTab.scroll = Vector2.zero;
                _globalScroll = Vector2.zero;
                StateChanged();
            }
            if (active)
                EditorGUI.DrawRect(new Rect(rect.x + 2, rect.yMax - 3, rect.width - 4, 2), group.color);
        }
    }

    bool MatchesTabColorFilter(GameObject go)
    {
        if (_activeSceneColorGroup == null) return true;
        // Kinder bleiben auffindbar, auch wenn nur ihr übergeordnetes Objekt angeheftet ist.
        for (Transform current = go.transform; current != null; current = current.parent)
            if (_activeSceneColorGroup.pinnedIDs.Contains(current.gameObject.GetEntityId()))
                return true;
        return false;
    }

    // ── View Mode Bar ────────────────────────────

    void DrawViewModeBar(TabData tab)
    {
        bool compact = CompactLayout;
        EditorGUILayout.BeginHorizontal(GUILayout.Height(26));
        GUILayout.Space(6);

        // 1 = Pinned (Tab-Inhalt), 0 = Hierarchy (Ganze Szene)
        DrawModeBtn(tab, 1, (compact ? "📌 " : "📌 Tab-Inhalt ") + (tab.pinnedIDs.Count > 0 ? "(" + tab.pinnedIDs.Count + ")" : ""));
        DrawModeBtn(tab, 0, compact ? "🌐 Szene" : "🌐 Ganze Szene");

        GUILayout.FlexibleSpace();

        if (tab.viewMode == 1 && tab.pinnedIDs.Count > 0)
        {
            if (GUILayout.Button(new GUIContent(compact ? "⌫" : "Leeren", "Alle angehefteten Objekte aus diesem Tab entfernen."),
                EditorStyles.miniButton, GUILayout.Width(compact ? 25f : 46f)))
                tab.pinnedIDs.Clear();
        }
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), ColSep);
    }

    void DrawModeBtn(TabData tab, int mode, string label)
    {
        bool active = tab.viewMode == mode;
        var  tabCol = GetTabColor(tab);
        var  bgCol  = active ? new Color(tabCol.r * 0.45f, tabCol.g * 0.45f, tabCol.b * 0.45f, 1f)
                             : new Color(0.28f, 0.28f, 0.28f, 1f);

        var old = GUI.backgroundColor;
        GUI.backgroundColor = bgCol;
        var fgOld = GUI.color;
        GUI.color = active ? Color.white : new Color(0.85f, 0.85f, 0.85f);

        if (GUILayout.Button(label, EditorStyles.miniButtonMid, GUILayout.Width(CompactLayout ? 72f : 100f)))
            tab.viewMode = mode;

        GUI.backgroundColor = old;
        GUI.color = fgOld;
    }

    // ── Notiz-Bereich ────────────────────────────

    void DrawNotesArea(TabData tab)
    {
        if (tab.notes == null) tab.notes = "";
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), ColSep);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        var noteStyle = _stNotes ?? EditorStyles.textArea;
        tab.notes = EditorGUILayout.TextArea(tab.notes, noteStyle,
            GUILayout.ExpandWidth(true), GUILayout.MinHeight(50), GUILayout.MaxHeight(85));
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), ColSep);
    }

    // ══════════════════════════════════════════════
    //  Content Dispatch
    // ══════════════════════════════════════════════

    void DrawContent(TabData tab)
    {
        if (_globalSearch && !string.IsNullOrEmpty(_globalQuery))
        {
            DrawGlobalSearchResults();
            return;
        }

        DrawFolderNames(tab);
        tab.scroll = EditorGUILayout.BeginScrollView(tab.scroll);
        switch (tab.viewMode)
        {
            case 1: DrawPinnedView(tab);    break;
            case 0: DrawHierarchyView(tab); break;
        }
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════
    //  Global Search
    // ══════════════════════════════════════════════

    void DrawFolderNames(TabData tab)
    {
        if (tab.folderNames == null) tab.folderNames = new List<string>();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = EditorGUILayout.Foldout(tab.showFolderNames,
            T("Ordner-Namensliste", "Folder names"), true);
        if (expanded != tab.showFolderNames)
        {
            tab.showFolderNames = expanded;
            StateChanged();
        }
        if (expanded)
        {
            using (new EditorGUI.DisabledScope(tab.locked || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                Rect dropBox = GUILayoutUtility.GetRect(0, 58f, GUILayout.ExpandWidth(true));
                GUI.Box(dropBox, T("＋ Hier hineinziehen\nOrdner aus den Namen erstellen",
                    "＋ Drop here\nCreate folders from names"), EditorStyles.helpBox);
                HandleFolderNameDrop(dropBox, tab, null);
                EditorGUILayout.BeginHorizontal();
                tab.newFolderName = EditorGUILayout.TextField(tab.newFolderName ?? "");
                string name = (tab.newFolderName ?? "").Trim();
                using (new EditorGUI.DisabledScope(name.Length == 0))
                {
                    if (GUILayout.Button(T("+ Name", "+ Name"), GUILayout.Width(70)))
                    {
                        if (!tab.folderNames.Any(n => string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase)))
                            tab.folderNames.Add(name);
                        tab.newFolderName = "";
                        GUI.FocusControl(null);
                        StateChanged();
                    }
                }
                EditorGUILayout.EndHorizontal();
                int remove = -1;
                for (int i = 0; i < tab.folderNames.Count; i++)
                {
                    string folderName = tab.folderNames[i];
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(new GUIContent("📁 " + folderName,
                        T("Objekte hier hineinziehen. Klick öffnet den Ordner.",
                          "Drop objects here. Click to open the folder.")), GUILayout.Height(28)))
                    {
                        EditorApplication.delayCall += () =>
                        {
                            if (this != null && _tabs.Contains(tab)) InsertNamedFolder(tab, folderName, new Object[0]);
                        };
                    }
                    HandleFolderNameDrop(GUILayoutUtility.GetLastRect(), tab, folderName);
                    if (GUILayout.Button(new GUIContent("×", T("Nur diesen Namen aus der Liste entfernen.",
                        "Remove only this name from the list.")), GUILayout.Width(22))) remove = i;
                    EditorGUILayout.EndHorizontal();
                }
                if (remove >= 0)
                {
                    tab.folderNames.RemoveAt(remove);
                    StateChanged();
                }
                GUILayout.Label(T("Oben: Namen übernehmen. Unten: Objekte auf einen Ordnernamen ziehen.",
                    "Above: add folder names. Below: drop objects onto a folder name."), EditorStyles.wordWrappedMiniLabel);
            }
        }
        EditorGUILayout.EndVertical();
    }

    static bool IsOrganizerFolder(GameObject go)
    {
        return go != null && !PrefabUtility.IsPartOfAnyPrefab(go)
            && go.GetComponents<Component>().Length == 1;
    }

    bool CanFileDroppedObject(Object obj)
    {
        var go = obj as GameObject;
        if (go == null) return false;
        if (EditorUtility.IsPersistent(go)) return PrefabUtility.IsPartOfPrefabAsset(go);
        return IsObjectInContext(go) && (!PrefabUtility.IsPartOfAnyPrefab(go)
            || PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go);
    }

    void HandleFolderNameDrop(Rect rect, TabData tab, string targetName)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition) || tab.locked || !GUI.enabled
            || EditorApplication.isPlayingOrWillChangePlaymode
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        Object[] objects = DragAndDrop.objectReferences.Where(o => o != null).ToArray();
        string[] names = objects.Select(o => o.name.Trim()).Where(n => n.Length > 0)
            .Distinct(System.StringComparer.OrdinalIgnoreCase).ToArray();
        bool acceptable = targetName == null ? names.Length > 0
            : objects.Length > 0 && objects.All(CanFileDroppedObject);
        DragAndDrop.visualMode = !acceptable ? DragAndDropVisualMode.Rejected
            : targetName == null || objects.Any(EditorUtility.IsPersistent)
                ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Move;
        if (e.type == EventType.DragPerform && acceptable)
        {
            DragAndDrop.AcceptDrag();
            Scene scene = _dataScene;
            EditorApplication.delayCall += () =>
            {
                if (this == null || !_tabs.Contains(tab) || _dataScene != scene || tab.locked) return;
                if (targetName == null)
                {
                    foreach (string name in names)
                    {
                        string existing = tab.folderNames.FirstOrDefault(n =>
                            string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase));
                        if (existing == null) tab.folderNames.Add(name);
                        InsertNamedFolder(tab, existing ?? name, new Object[0]);
                    }
                }
                else InsertNamedFolder(tab, targetName, objects);
                StateChanged();
            };
        }
        e.Use();
    }

    void InsertNamedFolder(TabData tab, string folderName, Object[] selection)
    {
        if (tab.locked || EditorApplication.isPlayingOrWillChangePlaymode
            || !_dataScene.IsValid() || !_dataScene.isLoaded
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        if (IsProjectTab(tab))
        {
            var projectTab = tab;
            if (string.IsNullOrEmpty(projectTab.projectSourceId))
                projectTab.projectSourceId = System.Guid.NewGuid().ToString("N");
            tab = _tabs.FirstOrDefault(t => !IsProjectTab(t)
                && t.groupedProjectSourceId == projectTab.projectSourceId);
            if (tab == null)
            {
                tab = new TabData
                {
                    name = projectTab.name, tabKind = 0, viewMode = 1, iconEmoji = "📁",
                    colorIndex = projectTab.colorIndex,
                    groupedProjectSourceId = projectTab.projectSourceId
                };
                _tabs.Add(tab);
            }
            if (tab.locked) return;
            if (tab.folderNames == null) tab.folderNames = new List<string>();
            if (!tab.folderNames.Contains(folderName)) tab.folderNames.Add(folderName);
        }
        // Automatically grouped tabs keep their named folders under the source group.
        GameObject sourceGroup = string.IsNullOrEmpty(tab.groupedProjectSourceId) ? null
            : tab.pinnedIDs.Select(id => EditorUtility.EntityIdToObject(id) as GameObject)
                .FirstOrDefault(go => IsObjectInContext(go) && IsOrganizerFolder(go));
        IEnumerable<GameObject> candidates = sourceGroup != null
            ? sourceGroup.transform.Cast<Transform>().Select(t => t.gameObject)
            : tab.viewMode == 0 ? _dataScene.GetRootGameObjects()
            : tab.pinnedIDs.Select(id => EditorUtility.EntityIdToObject(id) as GameObject);
        GameObject folder = candidates.FirstOrDefault(go => IsObjectInContext(go)
            && IsOrganizerFolder(go) && go.name == folderName);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        string action = "Sort into " + folderName;
        Undo.SetCurrentGroupName(action);
        if (sourceGroup == null && !string.IsNullOrEmpty(tab.groupedProjectSourceId))
        {
            sourceGroup = new GameObject(tab.name);
            SceneManager.MoveGameObjectToScene(sourceGroup, _dataScene);
            Undo.RegisterCreatedObjectUndo(sourceGroup, action);
            tab.pinnedIDs.Add(sourceGroup.GetEntityId());
        }
        if (folder == null)
        {
            folder = new GameObject(folderName);
            SceneManager.MoveGameObjectToScene(folder, _dataScene);
            Undo.RegisterCreatedObjectUndo(folder, action);
            if (sourceGroup != null) Undo.SetTransformParent(folder.transform, sourceGroup.transform, action);
        }
        var droppedObjects = new List<GameObject>();
        foreach (var obj in selection ?? new Object[0])
        {
            if (!CanFileDroppedObject(obj)) continue;
            var go = (GameObject)obj;
            if (EditorUtility.IsPersistent(go))
            {
                go = PrefabUtility.InstantiatePrefab(go, _dataScene) as GameObject;
                if (go == null) continue;
                Undo.RegisterCreatedObjectUndo(go, action);
            }
            droppedObjects.Add(go);
        }
        var selected = new HashSet<GameObject>(droppedObjects
            .Where(go => IsObjectInContext(go) && go != folder && go != sourceGroup
                && !folder.transform.IsChildOf(go.transform)
                && (!PrefabUtility.IsPartOfAnyPrefab(go) || PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go)));
        foreach (var go in selected)
        {
            bool ancestorSelected = false;
            for (var parent = go.transform.parent; parent != null; parent = parent.parent)
                if (selected.Contains(parent.gameObject)) { ancestorSelected = true; break; }
            if (!ancestorSelected && go.transform.parent != folder.transform)
                Undo.SetTransformParent(go.transform, folder.transform, action);
        }
        Undo.CollapseUndoOperations(undoGroup);
        EntityId folderId = folder.GetEntityId();
        if (sourceGroup == null && !tab.pinnedIDs.Contains(folderId)) tab.pinnedIDs.Add(folderId);
        if (sourceGroup != null) _expanded.Add(sourceGroup.GetEntityId());
        _expanded.Add(folderId);
        Selection.activeGameObject = folder;
        EditorGUIUtility.PingObject(folder);
        EditorSceneManager.MarkSceneDirty(_dataScene);
        _hierarchyVersion++;
        StateChanged();
    }

    void DrawGlobalSearchResults()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            GUILayout.Label("Keine Szene geladen.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        var hits = GetFilteredSceneObjects(CurrentTab, _globalQuery, true);

        GUILayout.Label("  " + hits.Count + (_activeSceneColorGroup == null
            ? " Treffer in der gesamten Szene" : " Treffer im gewählten Tab"), EditorStyles.boldLabel);
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), ColSep);

        _globalScroll = EditorGUILayout.BeginScrollView(_globalScroll);
        int row = 0;
        foreach (var go in hits)
            DrawFlatGoEntry(go, row++, CurrentTab);
        EditorGUILayout.EndScrollView();
    }

    static void SearchGO(GameObject[] roots, List<GameObject> result, string query)
    {
        foreach (var r in roots) SearchRecursive(r, result, query);
    }

    static void SearchRecursive(GameObject go, List<GameObject> result, string query)
    {
        if (go == null || go.GetComponent<HierarchyOrganizerSceneData>() != null) return;
        if (go.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)
            result.Add(go);
        for (int c = 0; c < go.transform.childCount; c++)
            SearchRecursive(go.transform.GetChild(c).gameObject, result, query);
    }

    // ══════════════════════════════════════════════
    //  Hierarchy View (Ganze Szene)
    // ══════════════════════════════════════════════

    void DrawHierarchyView(TabData tab)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) { DrawHint("Keine Szene geladen."); return; }

        var roots = scene.GetRootGameObjects();
        bool hasQuery = !string.IsNullOrEmpty(tab.searchQuery) || !string.IsNullOrEmpty(tab.tagFilter)
            || tab.layerFilter >= 0 || !string.IsNullOrEmpty(tab.quickFilter) || _activeSceneColorGroup != null;
        int row = 0;

        if (hasQuery)
        {
            var filtered = GetFilteredSceneObjects(tab, tab.searchQuery, false);
            for (int i = 0; i < filtered.Count; i++) DrawFlatGoEntry(filtered[i], row++, tab);
        }
        else
        {
            foreach (var root in roots) DrawTreeNode(root, 0, tab, ref row, isPinnedList: false);
        }
        if (_activeSceneColorGroup != null && row == 0)
            DrawHint("Keine passenden Objekte in der aktiven Szene. Prüfe die Tab-Pins und weitere Suchfilter.");
    }

    List<GameObject> GetFilteredSceneObjects(TabData tab, string query, bool global)
    {
        string colorKey = _activeSceneColorGroup == null ? "" : _activeSceneColorGroup.key;
        string key = _hierarchyVersion + "|" + _stateVersion + "|" + _dataScene.handle + "|"
            + global + "|" + query + "|" + tab.tagFilter + "|" + tab.layerFilter + "|"
            + tab.quickFilter + "|" + colorKey;
        if (_filteredSceneCacheKey == key) return _filteredSceneCache;

        _filteredSceneCacheKey = key;
        _filteredSceneCache.Clear();
        _filterMatchCache.Clear();
        var roots = _dataScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) CollectFiltered(roots[i], tab, query, global);
        return _filteredSceneCache;
    }

    void CollectFiltered(GameObject go, TabData tab, string query, bool global)
    {
        if (!IsObjectInContext(go)) return;
        bool matches = global
            ? go.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0
            : MatchesFilter(go, tab);
        if (matches && MatchesTabColorFilter(go)) _filteredSceneCache.Add(go);
        var transform = go.transform;
        for (int i = 0; i < transform.childCount; i++)
            CollectFiltered(transform.GetChild(i).gameObject, tab, query, global);
    }

    bool MatchesFilter(GameObject go, TabData tab)
    {
        if (!IsObjectInContext(go)) return false;

        if (!string.IsNullOrEmpty(tab.searchQuery))
        {
            if (go.name.IndexOf(tab.searchQuery, System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (!string.IsNullOrEmpty(tab.tagFilter) && go.tag != tab.tagFilter)
            return false;

        if (tab.layerFilter >= 0 && go.layer != tab.layerFilter)
            return false;

        if (!string.IsNullOrEmpty(tab.quickFilter))
        {
            switch (tab.quickFilter)
            {
                case "lights":
                    if (go.GetComponent<Light>() == null && go.GetComponent<ReflectionProbe>() == null && go.GetComponent<LightProbeGroup>() == null) return false;
                    break;
                case "triggers":
                    var trgComps = GetCachedComponents(go);
                    bool isTrig = false;
                    foreach (var c in trgComps)
                    {
                        if (c == null) continue;
                        string tName = c.GetType().Name;
                        if (tName.Contains("Trigger") || tName.Contains("Instruction") || tName.Contains("EventTrigger"))
                        {
                            isTrig = true;
                            break;
                        }
                    }
                    if (!isTrig)
                    {
                        var col = go.GetComponent<Collider>();
                        if (col != null && col.isTrigger) isTrig = true;
                    }
                    if (!isTrig)
                    {
                        var col2D = go.GetComponent<Collider2D>();
                        if (col2D != null && col2D.isTrigger) isTrig = true;
                    }
                    if (!isTrig) return false;
                    break;
                case "actions":
                    var actComps = GetCachedComponents(go);
                    bool isAct = false;
                    foreach (var c in actComps)
                    {
                        if (c == null) continue;
                        string tName = c.GetType().Name;
                        if (tName.Contains("Action") || tName.Contains("Instruction") || tName.Contains("Sequence") || tName.Contains("Condition") || tName == "PlayableDirector")
                        {
                            isAct = true;
                            break;
                        }
                    }
                    if (!isAct) return false;
                    break;
                case "character":
                    bool isCh = go.GetComponent<CharacterController>() != null || go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
                    if (!isCh)
                    {
                        var chComps = GetCachedComponents(go);
                        foreach (var c in chComps)
                        {
                            if (c == null) continue;
                            string tName = c.GetType().Name;
                            if (tName.Contains("Character") || tName.Contains("Player") || tName.Contains("NPC") || tName.Contains("Enemy") || tName.Contains("Unit"))
                            {
                                isCh = true;
                                break;
                            }
                        }
                    }
                    if (!isCh) return false;
                    break;
                case "variables":
                    var varComps = GetCachedComponents(go);
                    bool isV = false;
                    foreach (var c in varComps)
                    {
                        if (c == null) continue;
                        string tName = c.GetType().Name;
                        if (tName.Contains("Variable") || tName.Contains("Property") || tName.Contains("Blackboard") || tName.Contains("Stats") || tName.Contains("Inventory"))
                        {
                            isV = true;
                            break;
                        }
                    }
                    if (!isV) return false;
                    break;
                case "cameras":
                    bool isCm = go.GetComponent<Camera>() != null;
                    if (!isCm)
                    {
                        var cmComps = GetCachedComponents(go);
                        foreach (var c in cmComps)
                        {
                            if (c == null) continue;
                            string tName = c.GetType().Name;
                            if (tName.Contains("Camera") || tName.Contains("Cinemachine") || tName.Contains("Shot"))
                            {
                                isCm = true;
                                break;
                            }
                        }
                    }
                    if (!isCm) return false;
                    break;
                case "ui":
                    if (go.GetComponent<RectTransform>() == null && go.GetComponent<Canvas>() == null) return false;
                    break;
                case "colliders":
                    if (go.GetComponent<Collider>() == null && go.GetComponent<Collider2D>() == null) return false;
                    break;
                case "physics":
                    if (go.GetComponent<Rigidbody>() == null && go.GetComponent<Rigidbody2D>() == null && go.GetComponent<Joint>() == null && go.GetComponent<ConstantForce>() == null) return false;
                    break;
                case "navmesh":
                    if (go.GetComponent<UnityEngine.AI.NavMeshAgent>() == null && go.GetComponent<UnityEngine.AI.NavMeshObstacle>() == null) return false;
                    break;
                case "audio":
                    if (go.GetComponent<AudioSource>() == null && go.GetComponent<AudioListener>() == null && go.GetComponent<AudioReverbZone>() == null) return false;
                    break;
                case "vfx":
                    if (go.GetComponent<ParticleSystem>() == null && go.GetComponent<TrailRenderer>() == null && go.GetComponent<LineRenderer>() == null)
                    {
                        var vfxComps = GetCachedComponents(go);
                        bool hasVFX = vfxComps.Any(c => c != null && (c.GetType().Name.Contains("VisualEffect") || c.GetType().Name.Contains("VFX")));
                        if (!hasVFX) return false;
                    }
                    break;
                case "missing":
                    var comps = GetCachedComponents(go);
                    if (comps == null || !comps.Any(c => c == null)) return false;
                    break;
                case "favorites":
                    if (!_favorites.Contains(go.GetEntityId())) return false;
                    break;
                case "inactive":
                    if (go.activeInHierarchy) return false;
                    break;
            }
        }

        return true;
    }

    bool HasMatchingDescendant(GameObject go, TabData tab)
    {
        EntityId id = go.GetEntityId();
        if (_filterMatchCache.TryGetValue(id, out bool cached)) return cached;
        bool result = MatchesFilter(go, tab);
        var transform = go.transform;
        for (int i = 0; !result && i < transform.childCount; i++)
            result = HasMatchingDescendant(transform.GetChild(i).gameObject, tab);
        _filterMatchCache[id] = result;
        return result;
    }

    // ══════════════════════════════════════════════
    //  Tree Node
    // ══════════════════════════════════════════════

    void DrawTreeNode(GameObject go, int depth, TabData tab, ref int row, bool isPinnedList = false, int pinnedIndex = -1)
    {
        if (!IsObjectInContext(go)) return;

        if (!string.IsNullOrEmpty(tab.tagFilter) || tab.layerFilter >= 0 || !string.IsNullOrEmpty(tab.quickFilter))
            if (!HasMatchingDescendant(go, tab)) return;

        RegisterSelectionRow(go);
        float rowH = _rowHeight;
        bool hasChildren = go.transform.childCount > 0;
        EntityId id = go.GetEntityId();
        bool expanded    = _expanded.Contains(id);
        bool selected    = _selectedIDs.Contains(go.GetEntityId());
        bool inactive    = !go.activeInHierarchy;
        bool isFav       = _favorites.Contains(id);
        bool hasCustomCol = _customObjectColors.TryGetValue(id, out string hexCol);

        var  rect  = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
        bool hover = rect.Contains(Event.current.mousePosition);

        Color rowBg = selected ? ColSelected : hover ? ColHover : (row % 2 == 0 ? ColBg : ColRowAlt);
        EditorGUI.DrawRect(rect, rowBg);

        // Track hovered object for tooltip
        if (hover && _showTooltips)
        {
            _hoveredGO = go;
            _hoverMousePos = Event.current.mousePosition;
        }

        if (hasCustomCol && ColorUtility.TryParseHtmlString(hexCol, out Color cTag))
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), cTag);
            EditorGUI.DrawRect(new Rect(rect.x + 4, rect.y, rect.width - 4, rect.height), new Color(cTag.r, cTag.g, cTag.b, 0.12f));
        }

        row++;

        float indent = depth * 16f + 6f;
        float midY   = rect.y + rowH * 0.5f;
        float controlSize = Mathf.Min(18f, rowH);
        float iconSize = Mathf.Min(16f, Mathf.Max(10f, rowH - 1f));

        if (_showHierarchyLines && depth > 0)
        {
            Color lineCol = new Color(1f, 1f, 1f, 0.15f);
            EditorGUI.DrawRect(new Rect(rect.x + indent - 10, rect.y, 1, rect.height), lineCol);
            EditorGUI.DrawRect(new Rect(rect.x + indent - 10, midY, 8, 1), lineCol);
        }

        if (hasChildren)
        {
            var arrowRect = new Rect(rect.x + indent - 2, midY - controlSize * 0.5f, controlSize, controlSize);
            if (GUI.Button(arrowRect, expanded ? "▾" : "▸", EditorStyles.miniLabel))
            {
                if (expanded) _expanded.Remove(id);
                else          _expanded.Add(id);
            }
        }

        float cx = rect.x + indent + 16f;

        var ic = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image;
        if (ic != null)
        {
            var oldI = GUI.color;
            if (inactive) GUI.color = new Color(1, 1, 1, 0.45f);
            GUI.DrawTexture(new Rect(cx, midY - iconSize * 0.5f, iconSize, iconSize), ic, ScaleMode.ScaleToFit);
            GUI.color = oldI;
        }
        cx += 20f;

        var old = GUI.color;
        GUI.color = inactive ? new Color(0.65f, 0.65f, 0.65f)
                  : selected  ? Color.white
                  :             new Color(0.96f, 0.96f, 0.96f);

        float rightButtonsWidth = isPinnedList ? 110f : 90f;
        string membership = tab.viewMode == 0 ? GetTabMembership(go) : "";
        float availableTextWidth = Mathf.Max(1f, rect.xMax - rightButtonsWidth - cx);
        var entryStyle = _stEntryName ?? EditorStyles.label;
        if (string.IsNullOrEmpty(membership))
        {
            GUI.Label(new Rect(cx, rect.y, availableTextWidth, rowH), go.name, entryStyle);
        }
        else
        {
            float nameWidth = Mathf.Min(availableTextWidth,
                Mathf.Ceil(entryStyle.CalcSize(new GUIContent(go.name)).x) + 5f);
            GUI.Label(new Rect(cx, rect.y, nameWidth, rowH), go.name, entryStyle);
            if (availableTextWidth > nameWidth)
            {
                var membershipColor = GUI.color;
                GUI.color = Color.black;
                GUI.Label(new Rect(cx + nameWidth, rect.y, availableTextWidth - nameWidth, rowH),
                    new GUIContent("‹" + membership + "›", T("Organizer-Tabs: ", "Organizer tabs: ") + membership), entryStyle);
                GUI.color = membershipColor;
            }
        }
        GUI.color = old;

        if (_showComponentIcons)
            DrawComponentIcons(go, rect, midY);

        float btnX = rect.xMax - 22;

        // Unpin button (✕) in Pinned view
        if (isPinnedList && depth == 0)
        {
            var oc = GUI.color; GUI.color = new Color(1f, 0.45f, 0.45f, hover ? 0.95f : 0.4f);
            if (GUI.Button(new Rect(btnX, midY - controlSize * 0.5f, controlSize, controlSize),
                new GUIContent("✕", T("Aus dem Tab entfernen", "Remove from tab")), EditorStyles.miniLabel))
            {
                tab.pinnedIDs.Remove(id);
                StateChanged();
                GUI.color = oc;
                return;
            }
            GUI.color = oc;
            btnX -= 20;
        }

        var starC = isFav ? ColFav : new Color(0.5f, 0.5f, 0.5f, hover ? 0.9f : 0.2f);
        GUI.color = starC;
        if (GUI.Button(new Rect(btnX, midY - controlSize * 0.5f, controlSize, controlSize), "★", EditorStyles.miniLabel))
        {
            if (isFav) _favorites.Remove(id);
            else       _favorites.Add(id);
        }
        GUI.color = old;
        btnX -= 20;

        if (hover || !go.activeSelf)
        {
            GUI.color = go.activeSelf ? new Color(0.7f, 0.95f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            if (GUI.Button(new Rect(btnX, midY - controlSize * 0.5f, controlSize, controlSize), go.activeSelf ? "👁" : "○", EditorStyles.miniLabel))
            {
                Undo.RecordObject(go, "Toggle Active");
                go.SetActive(!go.activeSelf);
            }
            GUI.color = old;
            btnX -= 20;
        }

        if (hover)
        {
            GUI.color = new Color(0.9f, 0.9f, 0.9f, 0.85f);
            if (GUI.Button(new Rect(btnX, midY - controlSize * 0.5f, controlSize, controlSize), "🎯", EditorStyles.miniLabel))
            {
                Selection.activeGameObject = go;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
            }
            GUI.color = old;
        }

        HandleEntryEvents(rect, go, tab, isPinnedList && depth == 0, pinnedIndex);

        if (hasChildren && expanded)
        {
            for (int i = 0; i < go.transform.childCount; i++)
                DrawTreeNode(go.transform.GetChild(i).gameObject, depth + 1, tab, ref row, isPinnedList: false);
        }
    }

    Component[] GetCachedComponents(GameObject go)
    {
        EntityId id = go.GetEntityId();
        if (!_componentCache.TryGetValue(id, out var components))
            _componentCache[id] = components = go.GetComponents<Component>();
        return components;
    }

    string GetTabMembership(GameObject go)
    {
        EntityId objectId = go.GetEntityId();
        if (_tabMembershipCache.TryGetValue(objectId, out string cached)) return cached;

        var names = new List<string>();
        for (int i = 0; i < _tabs.Count; i++)
        {
            var organizerTab = _tabs[i];
            if (IsProjectTab(organizerTab) || IsHierarchyTab(organizerTab))
                continue;

            bool belongs = false;
            for (Transform current = go.transform; current != null && !belongs; current = current.parent)
                belongs = organizerTab.pinnedIDs.Contains(current.gameObject.GetEntityId());
            if (belongs && !string.IsNullOrWhiteSpace(organizerTab.name)) names.Add(organizerTab.name.Trim());
        }

        string result = string.Join(", ", names);
        _tabMembershipCache[objectId] = result;
        return result;
    }

    void DrawComponentIcons(GameObject go, Rect rect, float midY)
    {
        if (Event.current.type != EventType.Repaint) return;
        try
        {
            var components = GetCachedComponents(go);
            if (components == null) return;

            int drawn = 0;
            float iconSize = Mathf.Min(14f, Mathf.Max(9f, rect.height - 1f));
            float ix  = rect.xMax - 70;
            var   old = GUI.color;

            foreach (var comp in components)
            {
                if (comp == null || comp is Transform) continue;
                if (drawn >= 3) break;
                var content = EditorGUIUtility.ObjectContent(comp, comp.GetType());
                if (content?.image != null)
                {
                    GUI.color = new Color(1, 1, 1, 0.75f);
                    GUI.DrawTexture(new Rect(ix, midY - iconSize * 0.5f, iconSize, iconSize), content.image, ScaleMode.ScaleToFit);
                    ix -= iconSize + 2f;
                    drawn++;
                }
            }
            GUI.color = old;
        }
        catch { }
    }

    // ══════════════════════════════════════════════
    //  Pinned View (Tab-Inhalt)
    // ══════════════════════════════════════════════

    void DrawPinnedView(TabData tab)
    {
        if (IsProjectTab(tab)) DrawProjectFolders(tab);

        if (tab.pinnedIDs.Count == 0 && (tab.folderGuids == null || tab.folderGuids.Count == 0))
        {
            GUILayout.Space(14);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(10);
            GUILayout.Label(T("📁 Dieser Tab ist leer", "📁 This tab is empty"), EditorStyles.boldLabel);
            GUILayout.Space(4);
            GUILayout.Label(T("Hefte Szenenobjekte oder Projektordner hier an:", "Pin scene objects or project folders here:"), EditorStyles.label);
            GUILayout.Space(10);

            int selCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
            var sc = GUI.color; GUI.color = new Color(0.35f, 0.85f, 1f);
            if (GUILayout.Button(selCount > 0
                    ? T("＋ Ausgewählte Objekte anheften (" + selCount + " markiert)", "＋ Pin selected objects (" + selCount + " selected)")
                    : T("＋ Ausgewählte Objekte anheften", "＋ Pin selected objects"), GUILayout.Height(30)))
            {
                if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
                {
                    foreach (var go in Selection.gameObjects)
                    {
                        if (!IsObjectInContext(go)) continue;
                        EntityId id = go.GetEntityId();
                        if (!tab.pinnedIDs.Contains(id))
                        {
                            tab.pinnedIDs.Add(id);
                            _expanded.Add(id);
                        }
                    }
                    StateChanged();
                }
                else
                {
                    ShowNotification(new GUIContent(T("Wähle zuerst Objekte in Unitys Hierarchy aus", "Select objects in Unity's Hierarchy first")));
                }
            }
            GUI.color = sc;

            GUILayout.Space(10);
            GUILayout.Label(T("Möglichkeiten zum Hinzufügen:", "Ways to add content:"), EditorStyles.miniBoldLabel);
            GUILayout.Label(T("1. Objekte aus der Unity Hierarchy per Drag & Drop direkt hier hineinziehen", "1. Drag objects here from the Unity Hierarchy"), EditorStyles.miniLabel);
            GUILayout.Label(T("2. Unity Hierarchy → Rechtsklick auf Objekt → 'Pin to Hierarchy Organizer'", "2. Unity Hierarchy → Right-click object → 'Pin to Hierarchy Organizer'"), EditorStyles.miniLabel);
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
            HandleWindowDragDrop(tab);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
        {
            var sc = GUI.color; GUI.color = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button(T("＋ " + Selection.gameObjects.Length + " ausgewählte anheften",
                    "＋ Pin " + Selection.gameObjects.Length + " selected"), EditorStyles.miniButton, GUILayout.Width(175)))
            {
                foreach (var go in Selection.gameObjects)
                {
                    if (!IsObjectInContext(go)) continue;
                    EntityId id = go.GetEntityId();
                    if (!tab.pinnedIDs.Contains(id))
                    {
                        tab.pinnedIDs.Add(id);
                        _expanded.Add(id);
                    }
                }
                StateChanged();
            }
            GUI.color = sc;
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(tab.pinnedIDs.Count + T(" Objekt(e) angeheftet", " object(s) pinned"), EditorStyles.miniLabel);
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        int row     = 0;
        var missing = new List<EntityId>();
        for (int p = 0; p < tab.pinnedIDs.Count; p++)
        {
            EntityId id = tab.pinnedIDs[p];
            var go = EditorUtility.EntityIdToObject(id) as GameObject;
            if (go == null) { missing.Add(id); continue; }
            DrawTreeNode(go, 0, tab, ref row, isPinnedList: true, pinnedIndex: p);
        }
        foreach (var id in missing) tab.pinnedIDs.Remove(id);
        HandleWindowDragDrop(tab);
    }

    sealed class FolderPrefabCache
    {
        public string path;
        public bool exists;
        public GUIContent title;
        public string[] prefabs;
        public string[] labels;
        public GUIContent[] cardContents;
        public GameObject[] previewAssets;
        public double[] previewRetryUntil;
        public string query;
        public int[] matches;

        public void EnsurePrefabs(string search)
        {
            if (prefabs == null)
            {
                prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                labels = prefabs.Select(p => p.Substring(path.Length + 1)).ToArray();
                cardContents = new GUIContent[prefabs.Length];
                previewAssets = new GameObject[prefabs.Length];
                previewRetryUntil = new double[prefabs.Length];
            }
            search = search ?? "";
            if (matches != null && query == search) return;
            query = search;
            matches = Enumerable.Range(0, prefabs.Length)
                .Where(i => search.Length == 0 || labels[i].IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        }
    }

    readonly Dictionary<string, FolderPrefabCache> _folderPrefabCache = new Dictionary<string, FolderPrefabCache>();
    readonly Dictionary<string, int> _prefabInstanceCounts = new Dictionary<string, int>();
    int _prefabInstanceCountVersion = -1;
    readonly HashSet<string> _collapsedFolders = new HashSet<string>();
    Object _pendingAssetDrag;
    TabData _pendingAssetDragTab;
    int _prefabCardControlId;

    void HandlePrefabCardGesture()
    {
        if (_pendingAssetDrag == null) return;
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            if (GUIUtility.hotControl == _prefabCardControlId) GUIUtility.hotControl = 0;
            _pendingAssetDrag = null;
            _pendingAssetDragTab = null;
            e.Use();
            return;
        }
        if (e.type == EventType.MouseDrag && e.button == 0
            && GUIUtility.hotControl == _prefabCardControlId)
        {
            if (Vector2.Distance(GUIUtility.GUIToScreenPoint(e.mousePosition), _assetDragStart) > DragThreshold)
            {
                var asset = _pendingAssetDrag;
                var source = _pendingAssetDragTab;
                GUIUtility.hotControl = 0;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { asset };
                DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(asset) };
                if (source != null && _tabs.Contains(source)) TrackProjectDrag(source, asset);
                DragAndDrop.StartDrag(asset.name);
                _pendingAssetDrag = null;
                _pendingAssetDragTab = null;
            }
            e.Use();
        }
        else if (e.rawType == EventType.MouseUp && e.button == 0)
        {
            var asset = _pendingAssetDrag;
            bool ownsGesture = GUIUtility.hotControl == _prefabCardControlId;
            if (ownsGesture) GUIUtility.hotControl = 0;
            _pendingAssetDrag = null;
            _pendingAssetDragTab = null;
            if (ownsGesture)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                e.Use();
            }
        }
    }
    const string ProjectDragKey = "HierarchyOrganizer.ProjectSourceDrag";

    sealed class ProjectSourceDrag
    {
        public HierarchyOrganizerWindow owner;
        public TabData source;
        public Scene scene;
        public string prefabPath;
        public HashSet<EntityId> existing = new HashSet<EntityId>();
        public bool scheduled;
    }

    void TrackProjectDrag(TabData source, Object asset)
    {
        if (!_autoNameTabs)
        {
            DragAndDrop.SetGenericData(ProjectDragKey, null);
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || !_dataScene.IsValid()
            || !_dataScene.isLoaded || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        if (string.IsNullOrEmpty(source.projectSourceId))
        {
            source.projectSourceId = System.Guid.NewGuid().ToString("N");
            StateChanged();
        }
        var drag = new ProjectSourceDrag
        {
            owner = this, source = source, scene = _dataScene,
            prefabPath = AssetDatabase.GetAssetPath(asset)
        };
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.scene == drag.scene) drag.existing.Add(go.GetEntityId());
        DragAndDrop.SetGenericData(ProjectDragKey, drag);
    }

    DragAndDropVisualMode ObserveProjectSceneDrop(Object target, Vector3 worldPosition,
        Vector2 viewportPosition, Transform parent, bool perform)
    {
        if (perform) QueueProjectGrouping();
        return DragAndDropVisualMode.None;
    }

    DragAndDropVisualMode ObserveProjectHierarchyDrop(EntityId target, HierarchyDropFlags flags,
        Transform parent, bool perform)
    {
        if (perform) QueueProjectGrouping();
        return DragAndDropVisualMode.None;
    }

    void QueueProjectGrouping()
    {
        var drag = DragAndDrop.GetGenericData(ProjectDragKey) as ProjectSourceDrag;
        if (drag == null || drag.owner != this || drag.scheduled
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        if (!DragAndDrop.objectReferences.Any(o => o != null &&
            AssetDatabase.GetAssetPath(o) == drag.prefabPath)) return;
        drag.scheduled = true;
        // Let Unity finish its normal placement before touching the instance.
        EditorApplication.delayCall += () =>
        {
            if (this != null) GroupProjectDrop(drag);
        };
    }

    void GroupProjectDrop(ProjectSourceDrag drag)
    {
        if (!_autoNameTabs || EditorApplication.isPlayingOrWillChangePlaymode || !drag.scene.IsValid()
            || !drag.scene.isLoaded || drag.scene != _dataScene || !_tabs.Contains(drag.source)
            || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        var instances = Resources.FindObjectsOfTypeAll<GameObject>().Where(go =>
            go.scene == drag.scene && !EditorUtility.IsPersistent(go)
            && go.hideFlags == HideFlags.None && !drag.existing.Contains(go.GetEntityId())
            && PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go
            && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == drag.prefabPath).ToArray();
        // A cancelled/rejected drop must not create a tab or an empty scene group.
        if (instances.Length == 0) return;

        var hierarchyTab = _tabs.FirstOrDefault(t => !IsProjectTab(t)
            && t.groupedProjectSourceId == drag.source.projectSourceId);
        GameObject group = hierarchyTab == null ? null : hierarchyTab.pinnedIDs
            .Select(id => EditorUtility.EntityIdToObject(id) as GameObject)
            .FirstOrDefault(go => IsObjectInContext(go) && !PrefabUtility.IsPartOfAnyPrefab(go));

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Group objects from " + drag.source.name);
        if (group == null)
        {
            group = new GameObject(drag.source.name);
            SceneManager.MoveGameObjectToScene(group, drag.scene);
            Undo.RegisterCreatedObjectUndo(group, "Create project group");
        }
        foreach (var instance in instances)
            Undo.SetTransformParent(instance.transform, group.transform, "Group project object");
        Undo.CollapseUndoOperations(undoGroup);

        if (hierarchyTab == null)
        {
            hierarchyTab = new TabData
            {
                name = drag.source.name, tabKind = 0, viewMode = 1, iconEmoji = "📁",
                colorIndex = drag.source.colorIndex,
                groupedProjectSourceId = drag.source.projectSourceId
            };
            _tabs.Add(hierarchyTab);
        }
        EntityId groupId = group.GetEntityId();
        if (!hierarchyTab.pinnedIDs.Contains(groupId)) hierarchyTab.pinnedIDs.Add(groupId);
        _expanded.Add(groupId);
        EditorSceneManager.MarkSceneDirty(drag.scene);
        _hierarchyVersion++;
        StateChanged();
    }

    Vector2 _assetDragStart;
    bool _visiblePreviewsPending;
    const string PreviewSizePrefKey = "HierarchyOrganizer.PrefabPreviewSize";
    float _prefabPreviewSize = 104f;

    void OnInspectorUpdate()
    {
        // Unity generates asset previews asynchronously; repaint at the editor's
        // inspector cadence only while visible cards are waiting for an image.
        if (_visiblePreviewsPending)
        {
            _visiblePreviewsPending = false;
            Repaint();
        }
        if (_autoNameTabs || _tabs.Any(IsProjectTab)) Repaint();
    }

    Texture GetPrefabCardPreview(FolderPrefabCache cache, int index)
    {
        var asset = cache.previewAssets[index];
        if (asset == null)
        {
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(cache.prefabs[index]);
            cache.previewAssets[index] = asset;
        }
        if (asset == null) return AssetDatabase.GetCachedIcon(cache.prefabs[index]);

        Texture preview = AssetPreview.GetAssetPreview(asset);
        if (preview != null)
        {
            cache.previewRetryUntil[index] = 0;
            return preview;
        }

        double now = EditorApplication.timeSinceStartup;
        if (cache.previewRetryUntil[index] == 0)
            cache.previewRetryUntil[index] = now + 15.0;
        // Some prefabs have no renderable content. Do not keep repainting forever.
        if (now < cache.previewRetryUntil[index]) _visiblePreviewsPending = true;
        return AssetPreview.GetMiniThumbnail(asset) ?? AssetDatabase.GetCachedIcon(cache.prefabs[index]);
    }

    void RefreshFolderAssets()
    {
        _visiblePreviewsPending = false;
        _folderPrefabCache.Clear();
        _tagFilterOptions = null;
        _layerFilterOptions = null;
        _layerFilterValues = null;
        Repaint();
    }

    static bool IsProjectFolder(Object obj)
    {
        return obj != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj));
    }

    void EnsurePrefabInstanceCounts()
    {
        if (_prefabInstanceCountVersion == _hierarchyVersion) return;
        _prefabInstanceCountVersion = _hierarchyVersion;
        _prefabInstanceCounts.Clear();

        var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject go = sceneObjects[i];
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid() || !go.scene.isLoaded) continue;
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            if (instanceRoot != go) continue;
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(prefabPath)) continue;
            _prefabInstanceCounts.TryGetValue(prefabPath, out int current);
            _prefabInstanceCounts[prefabPath] = current + 1;
        }
    }

    int AddDroppedFolders(TabData tab)
    {
        if (tab.folderGuids == null) tab.folderGuids = new List<string>();
        int added = 0;
        foreach (var obj in DragAndDrop.objectReferences)
        {
            if (!IsProjectFolder(obj)) continue;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
            if (tab.folderGuids.Contains(guid)) continue;
            AutoNameEmptyTab(tab, obj);
            tab.folderGuids.Add(guid);
            added++;
        }
        return added;
    }

    bool CanAcceptNewAutoTabDrop(bool projectTab)
    {
        return projectTab
            ? DragAndDrop.objectReferences.Any(IsProjectFolder)
            : DragAndDrop.objectReferences.Any(obj => obj is GameObject go &&
                !EditorUtility.IsPersistent(go) && IsObjectInContext(go));
    }

    void HandleNewAutoTabDrop(Rect rect, bool projectTab)
    {
        var e = Event.current;
        bool acceptable = CanAcceptNewAutoTabDrop(projectTab);
        if (!rect.Contains(e.mousePosition) || !acceptable) return;
        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            _dragOverTab = -2;
            Repaint();
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            TabData tab;
            if (projectTab)
            {
                var first = DragAndDrop.objectReferences.First(IsProjectFolder);
                tab = new TabData
                {
                    name = GetUniqueTabName(first.name), viewMode = 1, tabKind = 1,
                    iconEmoji = "📁", colorIndex = _tabs.Count % PresetColors.Length
                };
                AddDroppedFolders(tab);
            }
            else
            {
                var objects = DragAndDrop.objectReferences.OfType<GameObject>()
                    .Where(go => !EditorUtility.IsPersistent(go) && IsObjectInContext(go)).ToArray();
                tab = new TabData
                {
                    name = GetUniqueTabName(objects[0].name), viewMode = 1, tabKind = 0,
                    iconEmoji = "📁", colorIndex = _tabs.Count % PresetColors.Length
                };
                foreach (var go in objects)
                {
                    EntityId id = go.GetEntityId();
                    if (!tab.pinnedIDs.Contains(id)) tab.pinnedIDs.Add(id);
                    _expanded.Add(id);
                }
            }
            _tabs.Add(tab);
            _selectedTab = _tabs.Count - 1;
            _dragOverTab = -1;
            e.Use();
            StateChanged();
        }
    }

    void DrawProjectFolders(TabData tab)
    {
        if (Event.current.type == EventType.Repaint) _visiblePreviewsPending = false;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(T("Vorschaugröße", "Preview size"), EditorStyles.miniLabel, GUILayout.Width(90f));
        EditorGUI.BeginChangeCheck();
        float previewSize = GUILayout.HorizontalSlider(_prefabPreviewSize, 64f, 240f, GUILayout.MinWidth(60f), GUILayout.MaxWidth(220f));
        if (EditorGUI.EndChangeCheck())
        {
            _prefabPreviewSize = Mathf.Round(previewSize);
            EditorPrefs.SetFloat(PreviewSizePrefKey, _prefabPreviewSize);
            Repaint();
        }
        GUILayout.Label(Mathf.RoundToInt(_prefabPreviewSize).ToString(), EditorStyles.miniLabel, GUILayout.Width(28f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        if (tab.folderGuids == null) tab.folderGuids = new List<string>();
        EnsurePrefabInstanceCounts();
        string remove = null;
        string selectedAssetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        IEnumerable<string> visibleFolderGuids = string.IsNullOrEmpty(tab.selectedProjectFolderGuid)
            ? tab.folderGuids
            : new[] { tab.selectedProjectFolderGuid };
        foreach (string guid in visibleFolderGuids)
        {
            if (!_folderPrefabCache.TryGetValue(guid, out var cache))
            {
                string folderPath = AssetDatabase.GUIDToAssetPath(guid);
                bool valid = AssetDatabase.IsValidFolder(folderPath);
                cache = new FolderPrefabCache
                {
                    path = folderPath,
                    exists = valid,
                    title = new GUIContent(valid ? System.IO.Path.GetFileName(folderPath) : "Ordner nicht gefunden", folderPath)
                };
                _folderPrefabCache[guid] = cache;
            }
            string path = cache.path;
            bool exists = cache.exists;
            EditorGUILayout.BeginHorizontal();
            bool expanded = !_collapsedFolders.Contains(guid);
            bool next = EditorGUILayout.Foldout(expanded, cache.title, true);
            if (next) _collapsedFolders.Remove(guid); else _collapsedFolders.Add(guid);
            if (exists && GUILayout.Button(T("Im Project", "In Project"), EditorStyles.miniButton, GUILayout.Width(75)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(path);
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
            if (tab.folderGuids.Contains(guid)
                && GUILayout.Button(new GUIContent("×", T("Ordner aus diesem Tab entfernen", "Remove folder from this tab")), EditorStyles.miniButton, GUILayout.Width(22)))
                remove = guid;
            EditorGUILayout.EndHorizontal();
            if (!next || !exists) continue;
            cache.EnsurePrefabs(tab.searchQuery);
            int count = cache.matches.Length;
            if (count == 0)
            {
                GUILayout.Label(T("Keine passenden Prefabs in diesem Ordner oder seinen Unterordnern.", "No matching prefabs in this folder or its subfolders."), EditorStyles.miniLabel);
                continue;
            }
            // Spaltenanzahl und Kartenhöhe folgen der gewählten Vorschaugröße.
            // Es werden weiterhin nur die sichtbaren Rasterzeilen gezeichnet.
            float cardMinWidth = _prefabPreviewSize + 38f;
            float cardHeight = _prefabPreviewSize + 50f;
            const float cardGap = 4f;
            Rect widthProbe = GUILayoutUtility.GetRect(0, 1f, GUILayout.ExpandWidth(true));
            widthProbe.xMin += 18f;
            float availableWidth = Mathf.Max(1f, widthProbe.width);
            int columns = Mathf.Clamp(
                Mathf.FloorToInt((availableWidth + cardGap) / (cardMinWidth + cardGap)),
                1,
                count);
            int gridRows = Mathf.CeilToInt(count / (float)columns);
            float gridHeight = gridRows * (cardHeight + cardGap) - cardGap;
            Rect gridRect = GUILayoutUtility.GetRect(0, gridHeight, GUILayout.ExpandWidth(true));
            gridRect.xMin += 18f;
            if (Event.current.type == EventType.Layout) continue;
            float cardWidth = (gridRect.width - (columns - 1) * cardGap) / columns;
            float rowStride = cardHeight + cardGap;
            int firstRow = Mathf.Clamp(Mathf.FloorToInt((tab.scroll.y - gridRect.y) / rowStride) - 1, 0, gridRows);
            int endRow = Mathf.Clamp(Mathf.CeilToInt((tab.scroll.y + position.height - gridRect.y) / rowStride) + 1, firstRow, gridRows);
            for (int gridRow = firstRow; gridRow < endRow; gridRow++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int visible = gridRow * columns + column;
                    if (visible >= count) break;
                    int index = cache.matches[visible];
                    string prefabPath = cache.prefabs[index];
                    _prefabInstanceCounts.TryGetValue(prefabPath, out int instanceCount);
                    Rect card = new Rect(
                        gridRect.x + column * (cardWidth + cardGap),
                        gridRect.y + gridRow * rowStride,
                        cardWidth,
                        cardHeight);
                    var e = Event.current;

                    string usageText = T("In geöffneten Szenen verwendet: ", "Used in open scenes: ") + instanceCount;
                    GUI.Box(card, new GUIContent("", prefabPath + "\n" + usageText), EditorStyles.helpBox);
                    if (e.type == EventType.Repaint)
                    {
                        bool selected = selectedAssetPath == prefabPath;
                        bool hovered = card.Contains(e.mousePosition);
                        if (selected || hovered)
                        {
                            Color tint = selected
                                ? new Color(0.20f, 0.55f, 0.85f, 0.32f)
                                : new Color(1f, 1f, 1f, 0.055f);
                            EditorGUI.DrawRect(new Rect(card.x + 1f, card.y + 1f, card.width - 2f, card.height - 2f), tint);
                        }

                        if (cache.cardContents[index] == null)
                        {
                            string shortName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
                            cache.cardContents[index] = new GUIContent(shortName, prefabPath);
                        }
                        Rect iconRect = new Rect(card.x + 6f, card.y + 6f, card.width - 12f, _prefabPreviewSize);
                        EditorGUI.DrawRect(iconRect, new Color(0f, 0f, 0f, 0.14f));
                        Texture icon = GetPrefabCardPreview(cache, index);
                        if (icon != null)
                            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                        Rect nameRect = new Rect(card.x + 7f, iconRect.yMax + 5f, card.width - 14f, 32f);
                        GUI.Label(nameRect, cache.cardContents[index], _stPrefabCardName);
                        Rect badgeRect = new Rect(iconRect.xMax - 36f, iconRect.yMax - 18f, 33f, 14f);
                        EditorGUI.DrawRect(badgeRect, new Color(0f, 0f, 0f, 0.28f));
                        GUI.Label(badgeRect, new GUIContent("× " + instanceCount, usageText), _stBadge);
                    }
                    if (e.type == EventType.MouseDown && e.button == 0 && card.Contains(e.mousePosition))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        if (asset == null) continue;
                        if (e.clickCount == 2)
                        {
                            GUIUtility.hotControl = 0;
                            _pendingAssetDrag = null;
                            _pendingAssetDragTab = null;
                            AssetDatabase.OpenAsset(asset);
                        }
                        else
                        {
                            GUIUtility.keyboardControl = 0;
                            GUIUtility.hotControl = _prefabCardControlId;
                            _pendingAssetDrag = asset;
                            _pendingAssetDragTab = tab;
                            _assetDragStart = GUIUtility.GUIToScreenPoint(e.mousePosition);
                        }
                        e.Use();
                    }
                }
            }
        }
        if (remove != null)
        {
            tab.folderGuids.Remove(remove);
            if (tab.selectedProjectFolderGuid == remove) tab.selectedProjectFolderGuid = "";
            StateChanged();
        }
    }

    void HandleWindowDragDrop(TabData tab)
    {
        var ev = Event.current.type;
        if (ev == EventType.DragUpdated)
        {
            ClearEntryDropTarget();
            bool hasGo = CanAcceptTabDrop(tab);
            DragAndDrop.visualMode = hasGo ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            Event.current.Use();
        }
        else if (ev == EventType.DragPerform)
        {
            if (!CanAcceptTabDrop(tab)) return;
            DragAndDrop.AcceptDrag();
            int added = IsProjectTab(tab) ? AddDroppedFolders(tab) : 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (!IsProjectTab(tab) && obj is GameObject go &&
                    !EditorUtility.IsPersistent(go) && IsObjectInContext(go))
                {
                    EntityId id = go.GetEntityId();
                    if (!tab.pinnedIDs.Contains(id))
                    {
                        AutoNameEmptyTab(tab, go);
                        tab.pinnedIDs.Add(id);
                        _expanded.Add(id);
                        added++;
                    }
                }
            }
            if (added > 0)
            {
                tab.viewMode = 1;
                StateChanged();
            }
            Event.current.Use();
        }
    }

    // ══════════════════════════════════════════════
    //  Flacher GO-Eintrag (Suche)
    // ══════════════════════════════════════════════

    void DrawFlatGoEntry(GameObject go, int index, TabData tab)
    {
        if (go == null) return;
        RegisterSelectionRow(go);
        bool selected = _selectedIDs.Contains(go.GetEntityId());
        bool inactive = !go.activeInHierarchy;
        var  rect     = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
        bool hover    = rect.Contains(Event.current.mousePosition);

        EditorGUI.DrawRect(rect, selected ? ColSelected : hover ? ColHover
            : index % 2 == 0 ? ColBg : ColRowAlt);

        // Track hovered object for tooltip
        if (hover && _showTooltips)
        {
            _hoveredGO = go;
            _hoverMousePos = Event.current.mousePosition;
        }

        var ic = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image;
        if (ic != null)
            GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 4, 16, 16), ic, ScaleMode.ScaleToFit);

        var old = GUI.color;
        GUI.color = inactive ? new Color(0.6f, 0.6f, 0.6f)
                  : selected  ? Color.white
                  :             new Color(0.95f, 0.95f, 0.95f);
        GUI.Label(new Rect(rect.x + 26, rect.y + 4, rect.width - 34, 18), go.name, _stEntryName ?? EditorStyles.label);
        GUI.color = old;

        HandleEntryEvents(rect, go, tab);
    }

    // ══════════════════════════════════════════════
    //  Events (Klick, Drag, Kontextmenü)
    // ══════════════════════════════════════════════

    void RegisterSelectionRow(GameObject go)
    {
        if (Event.current.type == EventType.Repaint && _drawnSelectionIDs.Add(go.GetEntityId()))
            _drawnSelectionRows.Add(go);
    }

    void SelectEntry(GameObject go, bool toggle, bool range)
    {
        _pendingSingleSelection = null;
        var current = new List<GameObject>(Selection.gameObjects ?? new GameObject[0]);
        if (range)
        {
            int start = _visibleSelectionRows.IndexOf(_selectionAnchor);
            if (start < 0)
            {
                _selectionAnchor = Selection.activeGameObject;
                start = _visibleSelectionRows.IndexOf(_selectionAnchor);
            }
            int end = _visibleSelectionRows.IndexOf(go);
            var selected = toggle ? current : new List<GameObject>();
            if (start >= 0 && end >= 0)
            {
                for (int i = Mathf.Min(start, end); i <= Mathf.Max(start, end); i++)
                {
                    var item = _visibleSelectionRows[i];
                    if (item != null && !selected.Contains(item)) selected.Add(item);
                }
            }
            else
            {
                if (!selected.Contains(go)) selected.Add(go);
                _selectionAnchor = go;
            }
            Selection.objects = selected.ToArray();
        }
        else if (toggle)
        {
            if (current.Contains(go)) current.Remove(go);
            else current.Add(go);
            _selectionAnchor = go;
            Selection.objects = current.ToArray();
        }
        else
        {
            _selectionAnchor = go;
            // Die Gruppe bis zum Loslassen erhalten, damit sie gemeinsam gezogen werden kann.
            if (current.Count > 1 && current.Contains(go)) _pendingSingleSelection = go;
            else Selection.objects = new Object[] { go };
        }
    }

    List<GameObject> GetDraggedSceneRoots()
    {
        var dragged = DragAndDrop.objectReferences.OfType<GameObject>()
            .Where(item => item != null && !EditorUtility.IsPersistent(item) && IsObjectInContext(item))
            .Distinct().ToList();
        if (dragged.Count == 0) return dragged;
        var draggedSet = new HashSet<GameObject>(dragged);
        return dragged.Where(item =>
        {
            for (Transform parent = item.transform.parent; parent != null; parent = parent.parent)
                if (draggedSet.Contains(parent.gameObject)) return false;
            return true;
        }).ToList();
    }

    bool CanDropEntriesOn(GameObject target, List<GameObject> dragged)
    {
        if (target == null || dragged == null || dragged.Count == 0) return false;
        foreach (var item in dragged)
        {
            if (item == target || target.transform.IsChildOf(item.transform)) return false;
            if (PrefabUtility.IsPartOfAnyPrefab(item)
                && PrefabUtility.GetOutermostPrefabInstanceRoot(item) != item) return false;
        }
        return true;
    }

    void ReorderPinnedEntries(TabData tab, List<GameObject> dragged, EntityId targetID, bool after)
    {
        var moving = tab.pinnedIDs.Where(id => dragged.Any(go => go.GetEntityId() == id)).ToList();
        if (moving.Count == 0) return;
        foreach (var id in moving) tab.pinnedIDs.Remove(id);
        int targetIndex = tab.pinnedIDs.IndexOf(targetID);
        if (targetIndex < 0) targetIndex = tab.pinnedIDs.Count;
        else if (after) targetIndex++;
        tab.pinnedIDs.InsertRange(targetIndex, moving);
        StateChanged();
    }

    void MoveHierarchyEntries(List<GameObject> dragged, GameObject target, int zone)
    {
        string action = zone == 1 ? "Move hierarchy objects into object" : "Reorder hierarchy objects";
        Transform newParent = zone == 1 ? target.transform : target.transform.parent;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(action);
        Undo.RecordObjects(dragged.Select(item => item.transform).ToArray(), action);

        foreach (var item in dragged)
            if (item.transform.parent != newParent)
                Undo.SetTransformParent(item.transform, newParent, action);

        if (zone == 1)
        {
            foreach (var item in dragged) item.transform.SetAsLastSibling();
            _expanded.Add(target.GetEntityId());
        }
        else
        {
            var ordered = newParent == null
                ? _dataScene.GetRootGameObjects().ToList()
                : newParent.Cast<Transform>().Select(child => child.gameObject).ToList();
            ordered.RemoveAll(dragged.Contains);
            int targetIndex = ordered.IndexOf(target);
            if (targetIndex < 0) targetIndex = ordered.Count;
            else if (zone == 2) targetIndex++;
            ordered.InsertRange(targetIndex, dragged);
            for (int i = 0; i < ordered.Count; i++) ordered[i].transform.SetSiblingIndex(i);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(_dataScene);
        _hierarchyVersion++;
        Repaint();
    }

    void ClearEntryDropTarget()
    {
        _entryDropTargetID = default;
        _entryDropZone = -1;
        _entryDropTab = null;
    }

    void HandleEntryEvents(Rect rect, GameObject go, TabData tab, bool pinnedRoot = false, int pinnedIndex = -1)
    {
        var e = Event.current;

        if (e.type == EventType.Repaint && _entryDropTab == tab
            && _entryDropTargetID == go.GetEntityId() && _entryDropZone >= 0)
        {
            Color marker = new Color(1f, 0.22f, 0.12f, 0.95f);
            if (_entryDropZone == 1)
            {
                EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, 3f, rect.height - 2f), marker);
                EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1.5f), marker);
                EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.yMax - 2.5f, rect.width - 2f, 1.5f), marker);
            }
            else
            {
                float y = _entryDropZone == 0 ? rect.y : rect.yMax - 2f;
                EditorGUI.DrawRect(new Rect(rect.x + 3f, y, rect.width - 6f, 2f), marker);
            }
        }

        if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && rect.Contains(e.mousePosition))
        {
            var dragged = GetDraggedSceneRoots();
            float relativeY = (e.mousePosition.y - rect.y) / Mathf.Max(1f, rect.height);
            int zone = relativeY < 0.25f ? 0 : relativeY > 0.75f ? 2 : 1;
            bool valid = !tab.locked && CanDropEntriesOn(go, dragged);
            var internalDrag = DragAndDrop.GetGenericData(EntryDragKey) as OrganizerEntryDrag;
            bool reorderPins = valid && zone != 1 && pinnedRoot && internalDrag != null
                && internalDrag.tab == tab && internalDrag.pinnedRoot
                && dragged.All(item => tab.pinnedIDs.Contains(item.GetEntityId()));

            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
            if (valid)
            {
                _entryDropTargetID = go.GetEntityId();
                _entryDropZone = zone;
                _entryDropTab = tab;
                Repaint();
            }
            if (e.type == EventType.DragPerform && valid)
            {
                DragAndDrop.AcceptDrag();
                if (reorderPins) ReorderPinnedEntries(tab, dragged, go.GetEntityId(), zone == 2);
                else MoveHierarchyEntries(dragged, go, zone);
                DragAndDrop.SetGenericData(EntryDragKey, null);
                ClearEntryDropTarget();
            }
            e.Use();
            return;
        }

        if (e.type == EventType.DragExited)
        {
            ClearEntryDropTarget();
            Repaint();
        }

        if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
        {
            _pendingSingleSelection = null;
            if (Selection.gameObjects == null || !Selection.gameObjects.Contains(go))
            {
                Selection.activeGameObject = go;
            }
            ShowObjectContextMenu(go, tab);
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            EntityId clickedID = go.GetEntityId();
            double clickTime = EditorApplication.timeSinceStartup;
            bool isRealDoubleClick = _lastClickedObjectID == clickedID
                && _lastClickedObjectTime >= 0
                && clickTime - _lastClickedObjectTime <= 0.35;
            _lastClickedObjectID = clickedID;
            _lastClickedObjectTime = clickTime;

            _dragSourceID = clickedID;
            _dragStartPos = e.mousePosition;

            SelectEntry(go, e.control || e.command, e.shift);

            if (isRealDoubleClick)
            {
                EditorGUIUtility.PingObject(go);
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
            }
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0 && rect.Contains(e.mousePosition)
            && _pendingSingleSelection == go)
        {
            Selection.objects = new Object[] { go };
            _pendingSingleSelection = null;
            _dragSourceID = default;
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0 &&
            _dragSourceID == go.GetEntityId() &&
            Vector2.Distance(e.mousePosition, _dragStartPos) > DragThreshold)
        {
            _pendingSingleSelection = null;
            _dragSourceID = default;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(EntryDragKey, new OrganizerEntryDrag
            {
                tab = tab,
                sourceID = go.GetEntityId(),
                pinnedRoot = pinnedRoot
            });

            if (_selectedIDs.Contains(go.GetEntityId()) && Selection.gameObjects.Length > 1)
            {
                DragAndDrop.objectReferences = Selection.objects;
                DragAndDrop.StartDrag($"{Selection.gameObjects.Length} GameObjects");
            }
            else
            {
                DragAndDrop.objectReferences = new Object[] { go };
                DragAndDrop.StartDrag(go.name);
            }
            e.Use();
        }
    }

    void ShowObjectContextMenu(GameObject go, TabData tab)
    {
        var menu = new GenericMenu();

        GameObject[] targets;
        if (_selectedIDs.Contains(go.GetEntityId()) && Selection.gameObjects.Length > 1)
        {
            targets = Selection.gameObjects;
        }
        else
        {
            targets = new GameObject[] { go };
        }

        bool isMulti = targets.Length > 1;
        string headerTitle = isMulti ? $"📦 {targets.Length} Objekte ausgewählt" : $"📦 {go.name}";
        menu.AddDisabledItem(new GUIContent(headerTitle));
        menu.AddSeparator("");

        if (!isMulti)
        {
            menu.AddItem(new GUIContent(T("Auswählen", "Select")), false, () => Selection.activeGameObject = go);
            menu.AddItem(new GUIContent(T("In Hierarchy finden", "Find in Hierarchy")), false, () => EditorGUIUtility.PingObject(go));
            menu.AddItem(new GUIContent(T("In Scene fokussieren", "Focus in Scene")), false, () =>
            {
                Selection.activeGameObject = go;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
            });
            menu.AddItem(new GUIContent(T("Objekt umbenennen", "Rename object")), false, () =>
            {
                Selection.activeGameObject = go;
                HierarchyOrganizerRenameWindow.ShowWindow(go, _englishUI);
            });
            menu.AddSeparator("");
        }

        if (!tab.locked)
        {
            menu.AddItem(new GUIContent(T("Erstellen …", "Create …")), false, () =>
            {
                Selection.activeGameObject = go;
                ShowCreateObjectMenu();
            });
            menu.AddSeparator("");
        }

        // Farb-Markierung
        for (int c = 0; c < ObjectHighlightColors.Length; c++)
        {
            var hc = ObjectHighlightColors[c];
            string label = isMulti ? $"Farb-Markierung ({targets.Length}x)/{hc.name}" : $"Farb-Markierung/{hc.name}";
            menu.AddItem(new GUIContent(label), false, () =>
            {
                foreach (var t in targets)
                {
                    if (t != null) _customObjectColors[t.GetEntityId()] = hc.hex;
                }
                StateChanged();
            });
        }
        menu.AddItem(new GUIContent(isMulti ? $"Farb-Markierung ({targets.Length}x)/✕ Markierung entfernen" : "Farb-Markierung/✕ Markierung entfernen"), false, () =>
        {
            foreach (var t in targets)
            {
                if (t != null) _customObjectColors.Remove(t.GetEntityId());
            }
            StateChanged();
        });

        menu.AddSeparator("");

        // Favoriten
        menu.AddItem(new GUIContent(isMulti ? $"★ Zu Favoriten hinzufügen ({targets.Length}x)" : (_favorites.Contains(go.GetEntityId()) ? "★ Favorit entfernen" : "☆ Als Favorit markieren")), false, () =>
        {
            foreach (var t in targets)
            {
                if (t == null) continue;
                EntityId tid = t.GetEntityId();
                if (!_favorites.Contains(tid)) _favorites.Add(tid);
            }
            StateChanged();
        });

        // Aktivieren / Deaktivieren
        menu.AddItem(new GUIContent(isMulti ? $"Sichtbarkeit umschalten ({targets.Length}x)" : (go.activeSelf ? "Deaktivieren" : "Aktivieren")), false, () =>
        {
            foreach (var t in targets)
            {
                if (t == null) continue;
                Undo.RecordObject(t, "Toggle Active Multi");
                t.SetActive(!t.activeSelf);
            }
        });

        menu.AddSeparator("");

        // Duplizieren
        menu.AddItem(new GUIContent(isMulti ? $"Duplizieren ({targets.Length} Objekte)" : "Duplizieren (Ctrl+D)"), false, () =>
        {
            List<GameObject> newDups = new List<GameObject>();
            foreach (var t in targets)
            {
                if (t == null) continue;
                var dup = Instantiate(t, t.transform.parent);
                dup.name = t.name;
                Undo.RegisterCreatedObjectUndo(dup, "Duplicate Object");
                newDups.Add(dup);
            }
            Selection.objects = newDups.ToArray();
        });

        menu.AddItem(new GUIContent(isMulti
            ? T($"Objekte aus Szene löschen ({targets.Length})", $"Delete objects from scene ({targets.Length})")
            : T("Objekt aus Szene löschen", "Delete object from scene")), false, () =>
        {
            foreach (var target in targets)
            {
                if (target == null) continue;
                EntityId id = target.GetEntityId();
                foreach (var organizerTab in _tabs) organizerTab.pinnedIDs.Remove(id);
                _favorites.Remove(id);
                _customObjectColors.Remove(id);
                _expanded.Remove(id);
                Undo.DestroyObjectImmediate(target);
            }
            StateChanged();
        });

        menu.AddSeparator("");

        // Pin zu diesem Tab
        menu.AddItem(new GUIContent(isMulti ? $"Pin zu diesem Tab ({targets.Length} Objekte)" : $"Pin zu diesem Tab ({tab.name})"), false, () =>
        {
            int added = 0;
            foreach (var t in targets)
            {
                if (!IsObjectInContext(t)) continue;
                EntityId tid = t.GetEntityId();
                if (!tab.pinnedIDs.Contains(tid))
                {
                    tab.pinnedIDs.Add(tid);
                    _expanded.Add(tid);
                    added++;
                }
            }
            if (added > 0)
            {
                tab.viewMode = 1;
                StateChanged();
                ShowNotification(new GUIContent($"{added} Objekte angeheftet!"));
            }
        });

        // Unpin aus diesem Tab
        menu.AddItem(new GUIContent(isMulti ? T($"Aus dem Tab entfernen ({targets.Length} Objekte)", $"Remove from tab ({targets.Length} objects)") : T("Aus dem Tab entfernen", "Remove from tab")), false, () =>
        {
            foreach (var t in targets)
            {
                if (t == null) continue;
                tab.pinnedIDs.Remove(t.GetEntityId());
            }
            StateChanged();
        });

        // Pin zu anderen Tabs
        for (int i = 0; i < _tabs.Count; i++)
        {
            var t = _tabs[i];
            if (t == tab) continue;
            var targetTab = t;
            menu.AddItem(new GUIContent($"Pin zu anderem Tab/{targetTab.name}" + (isMulti ? $" ({targets.Length}x)" : "")), false, () =>
            {
                int added = 0;
                foreach (var obj in targets)
                {
                    if (!IsObjectInContext(obj)) continue;
                    EntityId tid = obj.GetEntityId();
                    if (!targetTab.pinnedIDs.Contains(tid))
                    {
                        targetTab.pinnedIDs.Add(tid);
                        _expanded.Add(tid);
                        added++;
                    }
                }
                if (added > 0)
                {
                    StateChanged();
                    ShowNotification(new GUIContent($"{added} Objekt(e) zu '{targetTab.name}' hinzugefügt"));
                }
            });
        }

        menu.ShowAsContext();
    }

    // ── Settings Menü (⚙️) ─────────────────────────

    void ShowSettingsMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent(T("Nach Updates suchen …", "Check for updates…")), false, HierarchyOrganizerUpdater.CheckForUpdates);
        menu.AddDisabledItem(new GUIContent("Version " + HierarchyOrganizerUpdater.VersionName));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent(T("Design & Helligkeit/Modern Dark (Empfohlen)", "Theme & Brightness/Modern Dark (Recommended)")), _currentTheme == ThemeMode.ModernDark, () => { _currentTheme = ThemeMode.ModernDark; StateChanged(); });
        menu.AddItem(new GUIContent(T("Design & Helligkeit/Extra Hell", "Theme & Brightness/Extra Bright")), _currentTheme == ThemeMode.ExtraBright, () => { _currentTheme = ThemeMode.ExtraBright; StateChanged(); });
        menu.AddItem(new GUIContent(T("Design & Helligkeit/Unity Native Skin", "Theme & Brightness/Unity Native Skin")), _currentTheme == ThemeMode.UnityNative, () => { _currentTheme = ThemeMode.UnityNative; StateChanged(); });
        menu.AddItem(new GUIContent(T("Design & Helligkeit/High Contrast", "Theme & Brightness/High Contrast")), _currentTheme == ThemeMode.HighContrast, () => { _currentTheme = ThemeMode.HighContrast; StateChanged(); });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent(T("Zeilenhöhe/Extrem kompakt (13px)", "Row Height/Ultra Compact (13px)")), Mathf.Approximately(_rowHeight, 13f), () => { _rowHeight = 13f; StateChanged(); });
        menu.AddItem(new GUIContent(T("Zeilenhöhe/Sehr kompakt (16px)", "Row Height/Very Compact (16px)")), Mathf.Approximately(_rowHeight, 16f), () => { _rowHeight = 16f; StateChanged(); });
        menu.AddItem(new GUIContent(T("Zeilenhöhe/Kompakt (22px)", "Row Height/Compact (22px)")), Mathf.Approximately(_rowHeight, 22f), () => { _rowHeight = 22f; StateChanged(); });
        menu.AddItem(new GUIContent(T("Zeilenhöhe/Standard (26px)", "Row Height/Standard (26px)")), Mathf.Approximately(_rowHeight, 26f), () => { _rowHeight = 26f; StateChanged(); });
        menu.AddItem(new GUIContent(T("Zeilenhöhe/Groß (30px)", "Row Height/Large (30px)")), Mathf.Approximately(_rowHeight, 30f), () => { _rowHeight = 30f; StateChanged(); });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent(T("Schnellfilter-Leiste anzeigen", "Show quick-filter bar")), _showQuickFilterBar, () => { _showQuickFilterBar = !_showQuickFilterBar; StateChanged(); });
        menu.AddItem(new GUIContent(T("Schnellfilter-Bezeichnungen anzeigen", "Show quick-filter labels")), _showQuickFilterLabels, () => { _showQuickFilterLabels = !_showQuickFilterLabels; StateChanged(); });
        menu.AddItem(new GUIContent(T("Tag- und Layer-Filter anzeigen", "Show tag and layer filters")), _showTagLayerBar, () => { _showTagLayerBar = !_showTagLayerBar; StateChanged(); });
        menu.AddItem(new GUIContent(T("Komponenten-Icons anzeigen", "Show component icons")), _showComponentIcons, () => { _showComponentIcons = !_showComponentIcons; StateChanged(); });
        menu.AddItem(new GUIContent(T("Hierarchy-Baumlinien anzeigen", "Show hierarchy tree lines")), _showHierarchyLines, () => { _showHierarchyLines = !_showHierarchyLines; StateChanged(); });
        menu.AddItem(new GUIContent(T("Objekt-Info Tooltips anzeigen", "Show object-info tooltips")), _showTooltips, () => { _showTooltips = !_showTooltips; StateChanged(); });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent(T("Autosave/Aus", "Autosave/Off")), !_autosaveEnabled, () => { _autosaveEnabled = false; StateChanged(); });
        menu.AddItem(new GUIContent(T("Autosave/Alle 1 Minute", "Autosave/Every minute")), _autosaveEnabled && _autosaveIntervalMinutes == 1, () => { _autosaveEnabled = true; _autosaveIntervalMinutes = 1; StateChanged(); });
        menu.AddItem(new GUIContent(T("Autosave/Alle 5 Minuten", "Autosave/Every 5 minutes")), _autosaveEnabled && _autosaveIntervalMinutes == 5, () => { _autosaveEnabled = true; _autosaveIntervalMinutes = 5; StateChanged(); });
        menu.AddItem(new GUIContent(T("Autosave/Alle 15 Minuten", "Autosave/Every 15 minutes")), _autosaveEnabled && _autosaveIntervalMinutes == 15, () => { _autosaveEnabled = true; _autosaveIntervalMinutes = 15; StateChanged(); });
        menu.AddItem(new GUIContent(T("Autosave/Jetzt speichern", "Autosave/Save now")), false, () => { DoAutosave(); });
        menu.AddItem(new GUIContent(T("Autosave/Backup-Ordner öffnen", "Autosave/Open backup folder")), false, () =>
        {
            string folder = GetAutosaveFolder();
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        });

        menu.AddSeparator("");
        if (EditorPrefs.HasKey(PrefKey) || EditorPrefs.HasKey("HierarchyOrganizer_v3"))
            menu.AddItem(new GUIContent(T("Szenendaten/Alte globale Tabs übernehmen...", "Scene Data/Import old global tabs…")), false, ImportLegacyState);
        string sceneName = _dataScene.IsValid() ? _dataScene.name : "–";
        menu.AddDisabledItem(new GUIContent(T("Szenendaten/Aktive Szene: ", "Scene Data/Active scene: ") + sceneName));
        menu.AddDisabledItem(new GUIContent(T("Szenendaten/Werden mit der aktiven Szene gespeichert", "Scene Data/Saved with the active scene")));
        menu.AddSeparator("Szenendaten/");
        menu.AddItem(new GUIContent(T("Szenendaten/Auf Standard zurücksetzen...", "Scene Data/Reset to defaults…")), false, ResetActiveSceneOrganizerData);
        if (_sceneStorage != null)
            menu.AddItem(new GUIContent(T("Szenendaten/Aus der Szene entfernen...", "Scene Data/Remove from scene…")), false,
                () => DeleteActiveSceneOrganizerData(false));
        else
            menu.AddDisabledItem(new GUIContent(T("Szenendaten/Aus der Szene entfernen...", "Scene Data/Remove from scene…")));
        menu.AddItem(new GUIContent(T("Szenendaten/Alles löschen (inkl. Backups)...", "Scene Data/Delete everything (including backups)…")), false,
            () => DeleteActiveSceneOrganizerData(true));

        menu.ShowAsContext();
    }

    // ══════════════════════════════════════════════
    //  Tab-Verwaltung
    // ══════════════════════════════════════════════

    public string GetUniqueTabName(string desiredName, TabData excludeTab = null)
    {
        desiredName = string.IsNullOrWhiteSpace(desiredName) ? "Tab" : desiredName.Trim();

        bool NameExists(string n) => _tabs.Any(t => t != excludeTab && string.Equals(t.name?.Trim(), n, System.StringComparison.OrdinalIgnoreCase));

        if (!NameExists(desiredName))
            return desiredName;

        string baseName = desiredName;
        int counter = 2;

        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(desiredName, @"^(.*?)(?: \((\d+)\)| (\d+))?$");
        if (match.Success && (match.Groups[2].Success || match.Groups[3].Success))
        {
            baseName = match.Groups[1].Value.Trim();
            string numStr = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            if (int.TryParse(numStr, out int parsedNum))
            {
                counter = parsedNum + 1;
            }
        }

        while (true)
        {
            string candidate = $"{baseName} {counter}";
            if (!NameExists(candidate))
                return candidate;
            counter++;
        }
    }

    void AddTab(bool projectTab = false)
    {
        int newIdx = _tabs.Count % PresetColors.Length;
        string uniqueName = GetUniqueTabName("Tab " + (_tabs.Count + 1));
        _tabs.Add(new TabData
        {
            name       = uniqueName,
            viewMode   = 1, // Neuer Tab startet immer als leerer "Tab-Inhalt" (Pinned)
            iconEmoji  = "📁",
            tabKind    = projectTab ? 1 : 0,
            colorIndex = newIdx
        });
        _selectedTab = _tabs.Count - 1;
        StateChanged();
    }

    void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        var tab = _tabs[index];
        string tabName = string.IsNullOrEmpty(tab.name) ? $"Tab {index + 1}" : tab.name;
        int pinnedCount = tab.pinnedIDs != null ? tab.pinnedIDs.Count : 0;

        string msg = $"Möchtest du den Tab '{tabName}' wirklich löschen?";
        if (pinnedCount > 0)
        {
            msg += $"\n\nIn diesem Tab sind {pinnedCount} Objekt(e) angeheftet.";
        }

        bool confirm = EditorUtility.DisplayDialog(
            "Tab löschen",
            msg,
            "Löschen",
            "Abbrechen"
        );

        if (!confirm) return;

        if (_tabs.Count <= 1)
        {
            _tabs[0].name = HierarchyTabName;
            _tabs[0].pinnedIDs.Clear();
            _tabs[0].notes = "";
            StateChanged();
            return;
        }
        _tabs.RemoveAt(index);
        _selectedTab = Mathf.Clamp(_selectedTab, 0, _tabs.Count - 1);
        StateChanged();
    }

    // ── Autosave ─────────────────────────────────

    void AutosaveCheck()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextMaintenanceTime) return;
        _nextMaintenanceTime = now + 0.25d;

        EnsureSceneContext();
        if (!_stateLoaded || _suspendPersistence || EditorApplication.isPlaying) return;
        // Szenenspeicherung ist unabhängig von den optionalen JSON-Sicherungen.
        if (_stateDirty && now >= _nextStateSync)
            SaveState();
        if (!_autosaveEnabled) return;
        if (_lastAutosaveTime < 0) { _lastAutosaveTime = now; return; }
        if (now - _lastAutosaveTime >= _autosaveIntervalMinutes * 60.0)
        {
            DoAutosave();
            _lastAutosaveTime = now;
        }
    }

    void DoAutosave()
    {
        if (!_stateLoaded || _suspendPersistence || _persistenceSuppressedUntilOrganizerChange
            || EditorApplication.isPlaying || !_dataScene.IsValid() || !_dataScene.isLoaded) return;
        try
        {
            SaveState();
            string folder = GetAutosaveFolder();
            System.IO.Directory.CreateDirectory(folder);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string filePath = System.IO.Path.Combine(folder, "HO_Autosave_" + timestamp + ".json");
            System.IO.File.WriteAllText(filePath, JsonUtility.ToJson(CaptureSaveData(out _), true));
            var files = System.IO.Directory.GetFiles(folder, "HO_Autosave_*.json");
            System.Array.Sort(files);
            for (int i = 0; i < files.Length - AutosaveMaxFiles; i++)
            {
                System.IO.File.Delete(files[i]);
                if (System.IO.File.Exists(files[i] + ".meta")) System.IO.File.Delete(files[i] + ".meta");
            }
            _lastAutosaveLabel = System.DateTime.Now.ToString("HH:mm");
            Repaint();
        }
        catch (System.Exception ex) { Debug.LogWarning("[HierarchyOrganizer] Autosave fehlgeschlagen: " + ex.Message); }
    }

    void SaveBackup()
    {
        if (!_stateLoaded) return;
        string path = EditorUtility.SaveFilePanel("Hierarchy Organizer – Backup speichern",
            GetAutosaveFolder(), "HierarchyOrganizer_Backup", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(CaptureSaveData(out _), true));
            EditorUtility.DisplayDialog("Backup gespeichert", "Backup gespeichert:\n" + path, "OK");
        }
        catch (System.Exception ex) { EditorUtility.DisplayDialog("Fehler", ex.Message, "OK"); }
    }

    void LoadBackup()
    {
        if (!_stateLoaded) return;
        string path = EditorUtility.OpenFilePanel("Hierarchy Organizer – Backup laden", GetAutosaveFolder(), "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var data = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(path));
            if (data == null || data.tabs == null) throw new System.FormatException("Ungültige Backup-Datei.");
            string currentGuid = AssetDatabase.AssetPathToGUID(_dataScene.path ?? "");
            if (data.schemaVersion >= 5 && !string.IsNullOrEmpty(data.sceneGuid) && data.sceneGuid != currentGuid)
                throw new System.InvalidOperationException("Dieses Backup gehört zu einer anderen Szene. Öffne zuerst die zugehörige Szene: " + data.scenePath);
            ApplySaveData(data, null, true);
            SaveState(true);
            Repaint();
            EditorUtility.DisplayDialog("Backup geladen", data.tabs.Count + " Tab(s) wiederhergestellt. Bitte die Szene speichern.", "OK");
        }
        catch (System.Exception ex) { EditorUtility.DisplayDialog("Fehler", ex.Message, "OK"); }
    }

    void ResetActiveSceneOrganizerData()
    {
        if (!_stateLoaded || !_dataScene.IsValid() || !_dataScene.isLoaded) return;
        string sceneName = _dataScene.name;
        if (!EditorUtility.DisplayDialog(
            T("Organizer zurücksetzen?", "Reset Organizer?"),
            T("Alle Tabs, Pins, Favoriten, Farben und Organizer-Einstellungen der Szene '" + sceneName
                + "' werden auf Standard zurückgesetzt. Vorhandene Backup-Dateien bleiben erhalten.",
              "All tabs, pins, favorites, colors, and Organizer settings for scene '" + sceneName
                + "' will be reset to defaults. Existing backup files will be kept."),
            T("Zurücksetzen", "Reset"), T("Abbrechen", "Cancel"))) return;

        ResetSceneState();
        _persistenceSuppressedUntilOrganizerChange = false;
        _lastSavedJson = "";
        _stateDirty = true;
        _stateVersion++;
        InvalidateViewCaches();
        SaveState(true);
        Repaint();
        ShowNotification(new GUIContent(T("Organizer zurückgesetzt – Szene speichern.",
            "Organizer reset — save the scene.")));
    }

    void DeleteActiveSceneOrganizerData(bool includeBackups)
    {
        if (!_stateLoaded || !_dataScene.IsValid() || !_dataScene.isLoaded) return;
        string sceneName = _dataScene.name;
        string message = includeBackups
            ? T("Die Organizer-Daten der Szene '" + sceneName
                    + "' und alle automatischen Backups dieser Szene werden endgültig gelöscht.",
                "The Organizer data for scene '" + sceneName
                    + "' and all automatic backups for this scene will be permanently deleted.")
            : T("Die Organizer-Daten werden aus der Szene '" + sceneName
                    + "' entfernt. Automatische Backups bleiben erhalten.",
                "The Organizer data will be removed from scene '" + sceneName
                    + "'. Automatic backups will be kept.");
        if (!EditorUtility.DisplayDialog(
            T("Szenendaten löschen?", "Delete scene data?"), message,
            T("Löschen", "Delete"), T("Abbrechen", "Cancel"))) return;

        bool removedSceneObject = _sceneStorage != null;
        _writingState = true;
        try
        {
            if (_sceneStorage != null)
                Undo.DestroyObjectImmediate(_sceneStorage.gameObject);
        }
        finally
        {
            _sceneStorage = null;
            _writingState = false;
        }

        string backupError = null;
        if (includeBackups)
        {
            try
            {
                string folder = GetAutosaveFolder();
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
            catch (System.Exception ex) { backupError = ex.Message; }
        }

        ResetSceneState();
        _persistenceSuppressedUntilOrganizerChange = true;
        _lastSavedJson = JsonUtility.ToJson(CaptureSaveData(out _));
        _stateDirty = false;
        _stateVersion++;
        InvalidateViewCaches();
        if (removedSceneObject) EditorSceneManager.MarkSceneDirty(_dataScene);
        Repaint();

        if (backupError != null)
            EditorUtility.DisplayDialog(T("Backups konnten nicht gelöscht werden", "Backups could not be deleted"), backupError, "OK");
        else
            ShowNotification(new GUIContent(includeBackups
                ? T("Szenendaten und Backups gelöscht – Szene speichern.", "Scene data and backups deleted — save the scene.")
                : T("Szenendaten entfernt – Szene speichern.", "Scene data removed — save the scene.")));
    }

    void DrawObjectTooltip(GameObject prevHovered)
    {
        if (!_showTooltips) return;

        // Wenn neues Objekt gehovered → Timer resetten
        if (_hoveredGO != prevHovered)
        {
            _hoverStartTime = EditorApplication.timeSinceStartup;
        }

        if (_hoveredGO == null) return;

        double elapsed = EditorApplication.timeSinceStartup - _hoverStartTime;
        if (elapsed < TooltipDelay)
        {
            // Nur das Tooltip benötigt während seiner kurzen Verzögerung einen Repaint.
            Repaint();
            return;
        }

        var go = _hoveredGO;
        if (go == null) return;

        // ── Tooltip-Inhalt sammeln ──
        var lines = new List<string>();

        // Status-Zeile
        string status = go.activeInHierarchy ? "✅ Aktiv" : "⛔ Inaktiv";
        if (go.isStatic) status += "  |  📌 Static";
        lines.Add(status);

        // Tag & Layer
        string tagLayer = "";
        if (go.tag != "Untagged") tagLayer += "🏷️ " + go.tag;
        string layerName = LayerMask.LayerToName(go.layer);
        if (!string.IsNullOrEmpty(layerName) && go.layer != 0)
            tagLayer += (tagLayer.Length > 0 ? "  |  " : "") + "📐 Layer: " + layerName;
        if (tagLayer.Length > 0) lines.Add(tagLayer);

        // Komponenten
        var comps = GetCachedComponents(go);
        var compNames = new List<string>();
        int missingCount = 0;
        foreach (var c in comps)
        {
            if (c == null) { missingCount++; continue; }
            if (c is Transform) continue;
            compNames.Add(c.GetType().Name);
        }
        if (compNames.Count > 0)
        {
            lines.Add("");
            lines.Add("🔧 Komponenten (" + compNames.Count + "):");
            int maxComps = Mathf.Min(compNames.Count, 8);
            for (int i = 0; i < maxComps; i++)
                lines.Add("   • " + compNames[i]);
            if (compNames.Count > maxComps)
                lines.Add("   ... +" + (compNames.Count - maxComps) + " weitere");
        }
        if (missingCount > 0)
            lines.Add("⚠️ " + missingCount + " fehlende Komponente(n)!");

        // Kinder
        int childCount = go.transform.childCount;
        if (childCount > 0)
        {
            lines.Add("");
            lines.Add("📂 Kinder (" + childCount + "):");
            int maxChildren = Mathf.Min(childCount, 10);
            for (int i = 0; i < maxChildren; i++)
            {
                var child = go.transform.GetChild(i);
                string childIcon = child.gameObject.activeInHierarchy ? "  ▸ " : "  ▸ (inaktiv) ";
                int grandChildren = child.childCount;
                string suffix = grandChildren > 0 ? "  [" + grandChildren + " ▾]" : "";
                lines.Add(childIcon + child.name + suffix);
            }
            if (childCount > maxChildren)
                lines.Add("   ... +" + (childCount - maxChildren) + " weitere");

            int totalDescendants = CountDescendants(go.transform);
            if (totalDescendants > childCount)
                lines.Add("   📊 Gesamt: " + totalDescendants + " Objekte in der Hierarchie");
        }
        else
        {
            lines.Add("");
            lines.Add("📭 Keine Kinder");
        }

        // ── Tooltip Layout & Rendering ──
        float lineHeight = 16f;
        float padding    = 10f;
        float tooltipW   = 300f;
        float tooltipH   = padding * 2 + lines.Count * lineHeight + 24f;

        // Position: Maus-Koordinaten aus dem Haupt-Fenster (NICHT aus dem ScrollView!)
        // Event.current.mousePosition ist hier korrekt weil wir außerhalb von BeginScrollView/EndScrollView sind
        Vector2 mouse = Event.current.mousePosition;
        float tipX = mouse.x + 18f;
        float tipY = mouse.y + 12f;

        if (tipX + tooltipW > position.width - 10f)
            tipX = Mathf.Max(10f, mouse.x - tooltipW - 14f);

        if (tipY + tooltipH > position.height - 10f)
            tipY = Mathf.Max(10f, mouse.y - tooltipH - 10f);

        var tooltipRect = new Rect(tipX, tipY, tooltipW, tooltipH);

        // Schatten
        EditorGUI.DrawRect(new Rect(tooltipRect.x + 3, tooltipRect.y + 3, tooltipRect.width, tooltipRect.height),
            new Color(0, 0, 0, 0.45f));

        // Hintergrund
        EditorGUI.DrawRect(tooltipRect, new Color(0.12f, 0.12f, 0.14f, 0.98f));

        // Rahmen
        float bw = 1f;
        Color borderCol = new Color(0.35f, 0.55f, 0.95f, 0.85f);
        EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.y, tooltipRect.width, bw), borderCol);
        EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.yMax - bw, tooltipRect.width, bw), borderCol);
        EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.y, bw, tooltipRect.height), borderCol);
        EditorGUI.DrawRect(new Rect(tooltipRect.xMax - bw, tooltipRect.y, bw, tooltipRect.height), borderCol);

        // Header
        var headerRect = new Rect(tooltipRect.x + padding, tooltipRect.y + 6, tooltipRect.width - padding * 2, 18);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.90f, 0.95f, 1f) }
        };

        var goIcon = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image;
        if (goIcon != null)
            GUI.DrawTexture(new Rect(headerRect.x, headerRect.y, 16, 16), goIcon, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(headerRect.x + 20, headerRect.y - 1, headerRect.width - 20, 18), go.name, headerStyle);

        // Trennlinie
        EditorGUI.DrawRect(new Rect(tooltipRect.x + 6, tooltipRect.y + 26, tooltipRect.width - 12, 1),
            new Color(0.4f, 0.45f, 0.6f, 0.6f));

        // Text
        var contentStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            wordWrap = false,
            richText = false,
            normal = { textColor = new Color(0.88f, 0.90f, 0.94f) }
        };

        float y = tooltipRect.y + 30;
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                y += lineHeight * 0.4f;
                continue;
            }
            GUI.Label(new Rect(tooltipRect.x + padding, y, tooltipRect.width - padding * 2, lineHeight), line, contentStyle);
            y += lineHeight;
        }
    }

    static int CountDescendants(Transform t)
    {
        int count = t.childCount;
        for (int i = 0; i < t.childCount; i++)
            count += CountDescendants(t.GetChild(i));
        return count;
    }

    // ══════════════════════════════════════════════
    //  Hilfsmethoden
    // ══════════════════════════════════════════════

    void DrawHint(string text)
    {
        GUILayout.Space(24);
        GUILayout.Label(text, EditorStyles.centeredGreyMiniLabel);
    }

    static string BuildPath(GameObject go)
    {
        var parts = new List<string>();
        var t     = go.transform;
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join(" / ", parts);
    }

    // ══════════════════════════════════════════════
    //  Persistenz
    // ══════════════════════════════════════════════

    // Die alten Schlüssel werden ausschließlich über den ausdrücklichen Import gelesen.
    const string PrefKey = "HierarchyOrganizer_v4";

    [System.Serializable]
    class SavedObjectReference
    {
        public EntityId id;
        public string globalId;
    }

    [System.Serializable]
    class SaveData
    {
        public int schemaVersion;
        public string sceneGuid;
        public string scenePath;
        public List<TabData> tabs;
        public int selectedTab;
        public List<EntityId> favorites;
        public List<ObjectTagColor> customColors;
        public List<EntityId> expanded;
        public List<SavedObjectReference> objectReferences;
        public int currentTheme;
        public float rowHeight = 26f;
        public bool showComponentIcons = true;
        public bool showHierarchyLines = true;
        public bool showQuickFilterBar = true;
        public bool showQuickFilterLabels;
        public bool showTagLayerBar;
        public bool showTooltips = true;
        public int autosaveIntervalMinutes = 5;
        public bool autosaveEnabled = true;
        public bool autoNameTabs;
        public bool globalSearch;
        public string globalQuery;
        public int colorFilterTab = -1;
    }

    bool IsObjectInContext(GameObject go)
    {
        return go != null && _dataScene.IsValid() && go.scene == _dataScene
            && (_sceneStorage == null || go != _sceneStorage.gameObject);
    }

    static HierarchyOrganizerSceneData FindSceneStorage(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var storage = root.GetComponent<HierarchyOrganizerSceneData>();
            if (storage != null) return storage;
        }
        return null;
    }

    void ResetSceneState()
    {
        _tabs = new List<TabData> { new TabData { name = HierarchyTabName, viewMode = 0, iconEmoji = "📁" } };
        _selectedTab = 0;
        _favorites.Clear();
        _customObjectColors.Clear();
        _expanded.Clear();
        _currentTheme = ThemeMode.ModernDark;
        _rowHeight = 26f;
        _showComponentIcons = _showHierarchyLines = _showQuickFilterBar = _showTooltips = true;
        _showQuickFilterLabels = false;
        _showTagLayerBar = false;
        _autosaveIntervalMinutes = 5;
        _autosaveEnabled = true;
        _autoNameTabs = false;
        _globalSearch = false;
        _globalQuery = "";
        _globalScroll = _quickFilterScroll = Vector2.zero;
        _sceneColorFilterTab = null;
        _activeSceneColorGroup = null;
        _tabColorGroups.Clear();
        _visibleSelectionRows.Clear();
        _drawnSelectionRows.Clear();
        _drawnSelectionIDs.Clear();
        _selectionAnchor = _pendingSingleSelection = _hoveredGO = null;
        _dragSourceID = default;
        _dragSourceTab = _dragOverTab = -1;
        _lastAutosaveTime = EditorApplication.timeSinceStartup;
        _lastAutosaveLabel = "";
        _nextMaintenanceTime = 0d;
    }

    static HashSet<EntityId> ReferencedIds(SaveData data)
    {
        var ids = new HashSet<EntityId>();
        foreach (var tab in data.tabs ?? new List<TabData>())
        {
            if (tab == null) continue;
            if (tab.pinnedIDs != null) ids.UnionWith(tab.pinnedIDs);
        }
        if (data.favorites != null) ids.UnionWith(data.favorites);
        if (data.expanded != null) ids.UnionWith(data.expanded);
        if (data.customColors != null)
            foreach (var color in data.customColors)
                if (color != null) ids.Add(color.entityId);
        return ids;
    }

    static List<EntityId> RemapIds(List<EntityId> ids, Dictionary<EntityId, EntityId> map)
    {
        return (ids ?? new List<EntityId>()).Where(map.ContainsKey).Select(id => map[id]).Distinct().ToList();
    }

    static void RemapObjectIds(SaveData data, Dictionary<EntityId, EntityId> map)
    {
        data.tabs = (data.tabs ?? new List<TabData>()).Where(t => t != null).ToList();
        foreach (var tab in data.tabs)
        {
            tab.folderGuids = tab.folderGuids ?? new List<string>();
            // Ältere gespeicherte Projekt-Tabs hatten noch keinen festen Typ.
            if (tab.folderGuids.Count > 0 && (tab.pinnedIDs?.Count ?? 0) == 0)
                tab.tabKind = 1;
            tab.pinnedIDs = RemapIds(tab.pinnedIDs, map);
            if (tab.viewMode != 0 && tab.viewMode != 1)
                tab.viewMode = 1;
            tab.renaming = false;
            tab.renameBuffer = tab.name;
            tab.notes = tab.notes ?? "";
            tab.searchQuery = tab.searchQuery ?? "";
            tab.selectedProjectFolderGuid = tab.selectedProjectFolderGuid ?? "";
        }
        data.favorites = RemapIds(data.favorites, map);
        data.expanded = RemapIds(data.expanded, map);
        data.customColors = (data.customColors ?? new List<ObjectTagColor>())
            .Where(c => c != null && map.ContainsKey(c.entityId)).ToList();
        foreach (var color in data.customColors) color.entityId = map[color.entityId];
    }

    SaveData CaptureSaveData(out List<HierarchyOrganizerSceneData.ObjectLink> links)
    {
        var data = new SaveData
        {
            schemaVersion = 5,
            sceneGuid = AssetDatabase.AssetPathToGUID(_dataScene.path ?? ""),
            scenePath = _dataScene.path,
            tabs = _tabs,
            selectedTab = _selectedTab,
            favorites = _favorites.ToList(),
            customColors = _customObjectColors.Select(kv => new ObjectTagColor { entityId = kv.Key, hexColor = kv.Value }).ToList(),
            expanded = _expanded.ToList(),
            currentTheme = (int)_currentTheme,
            rowHeight = _rowHeight,
            showComponentIcons = _showComponentIcons,
            showHierarchyLines = _showHierarchyLines,
            showQuickFilterBar = _showQuickFilterBar,
            showQuickFilterLabels = _showQuickFilterLabels,
            showTagLayerBar = _showTagLayerBar,
            showTooltips = _showTooltips,
            autosaveIntervalMinutes = _autosaveIntervalMinutes,
            autosaveEnabled = _autosaveEnabled,
            autoNameTabs = _autoNameTabs,
            globalSearch = _globalSearch,
            globalQuery = _globalQuery,
            colorFilterTab = _sceneColorFilterTab == null ? -1 : _tabs.IndexOf(_sceneColorFilterTab)
        };
        // Eine Kopie bereinigen, damit das Speichern die laufende Ansicht nicht verändert.
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        links = new List<HierarchyOrganizerSceneData.ObjectLink>();
        data.objectReferences = new List<SavedObjectReference>();
        var valid = new Dictionary<EntityId, EntityId>();
        foreach (var id in ReferencedIds(data))
        {
            var go = EditorUtility.EntityIdToObject(id) as GameObject;
            if (!IsObjectInContext(go)) continue;
            valid[id] = id;
            links.Add(new HierarchyOrganizerSceneData.ObjectLink { savedId = id, target = go });
            data.objectReferences.Add(new SavedObjectReference
            {
                id = id,
                globalId = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()
            });
        }
        RemapObjectIds(data, valid);
        return data;
    }

    void ApplySaveData(SaveData data, List<HierarchyOrganizerSceneData.ObjectLink> links, bool allowLegacyIds = false)
    {
        if (data == null || data.tabs == null) throw new System.FormatException("Ungültige Organizer-Daten.");
        if (data.schemaVersion > 5) throw new System.FormatException("Diese Daten stammen aus einer neueren Organizer-Version.");
        var map = new Dictionary<EntityId, EntityId>();
        // Native Szenenreferenzen haben Vorrang, auch bei 'Speichern unter' und kopierten Szenen.
        if (links != null)
            foreach (var link in links)
                if (link != null && IsObjectInContext(link.target)) map[link.savedId] = link.target.GetEntityId();
        foreach (var reference in data.objectReferences ?? new List<SavedObjectReference>())
        {
            if (reference == null || map.ContainsKey(reference.id)) continue;
            if (!GlobalObjectId.TryParse(reference.globalId, out var globalId)) continue;
            var go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
            if (IsObjectInContext(go)) map[reference.id] = go.GetEntityId();
        }
        // Nur beim ausdrücklich gewählten Import alter Daten sind Sitzungs-IDs erlaubt.
        if (allowLegacyIds && data.schemaVersion < 5)
            foreach (var id in ReferencedIds(data))
            {
                var go = EditorUtility.EntityIdToObject(id) as GameObject;
                if (IsObjectInContext(go)) map[id] = id;
            }
        RemapObjectIds(data, map);
        ResetSceneState();
        if (data.tabs.Count > 0) _tabs = data.tabs;
        foreach (var tab in _tabs)
            if (!IsProjectTab(tab) && string.Equals(tab.name?.Trim(), LegacyMainTabName,
                    System.StringComparison.OrdinalIgnoreCase))
                tab.name = HierarchyTabName;
        _selectedTab = Mathf.Clamp(data.selectedTab, 0, _tabs.Count - 1);
        _favorites = new HashSet<EntityId>(data.favorites);
        _expanded = new HashSet<EntityId>(data.expanded);
        foreach (var color in data.customColors) _customObjectColors[color.entityId] = color.hexColor;
        _currentTheme = (ThemeMode)Mathf.Clamp(data.currentTheme, 0, 3);
        _rowHeight = data.rowHeight > 10 ? data.rowHeight : 26f;
        _showComponentIcons = data.showComponentIcons;
        _showHierarchyLines = data.showHierarchyLines;
        _showQuickFilterBar = data.showQuickFilterBar;
        _showQuickFilterLabels = data.showQuickFilterLabels;
        _showTagLayerBar = data.showTagLayerBar;
        _showTooltips = data.showTooltips;
        _autosaveIntervalMinutes = Mathf.Max(1, data.autosaveIntervalMinutes);
        _autosaveEnabled = data.schemaVersion < 5 || data.autosaveEnabled;
        _autoNameTabs = data.autoNameTabs;
        _globalSearch = false;
        _globalQuery = "";
        _sceneColorFilterTab = null;
        _persistenceSuppressedUntilOrganizerChange = false;
        RebuildTabColorGroups();
    }

    void SaveState(bool forceCreate = false)
    {
        if (!_stateLoaded || _writingState || _suspendPersistence || EditorApplication.isPlaying
            || (_sceneStorage == null && _persistenceSuppressedUntilOrganizerChange)
            || (!_stateDirty && !forceCreate) || !_dataScene.IsValid() || !_dataScene.isLoaded
            || EditorSceneManager.IsPreviewScene(_dataScene)) return;
        _writingState = true;
        try
        {
            var data = CaptureSaveData(out var links);
            string json = JsonUtility.ToJson(data);
            if (json == _lastSavedJson && (_sceneStorage != null || !forceCreate))
            {
                _stateDirty = false;
                return;
            }
            if (_sceneStorage == null)
            {
                var holder = new GameObject("__HierarchyOrganizerSceneData");
                SceneManager.MoveGameObjectToScene(holder, _dataScene);
                holder.tag = "EditorOnly";
                holder.hideFlags = HideFlags.HideInHierarchy;
                holder.SetActive(false);
                _sceneStorage = holder.AddComponent<HierarchyOrganizerSceneData>();
            }
            _sceneStorage.stateJson = json;
            _sceneStorage.objectLinks = links;
            EditorUtility.SetDirty(_sceneStorage);
            EditorSceneManager.MarkSceneDirty(_dataScene);
            _lastSavedJson = json;
            _stateDirty = false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[HierarchyOrganizer] Szenendaten konnten nicht gespeichert werden: " + ex.Message);
        }
        finally { _writingState = false; }
    }

    void LoadState()
    {
        _stateLoaded = false;
        _loadFailed = false;
        _dataScene = SceneManager.GetActiveScene();
        _sceneStorage = null;
        ResetSceneState();
        if (!_dataScene.IsValid() || !_dataScene.isLoaded || EditorSceneManager.IsPreviewScene(_dataScene)) return;
        _sceneStorage = FindSceneStorage(_dataScene);
        _persistenceSuppressedUntilOrganizerChange = _sceneStorage == null;
        try
        {
            if (_sceneStorage != null && !string.IsNullOrEmpty(_sceneStorage.stateJson))
                ApplySaveData(JsonUtility.FromJson<SaveData>(_sceneStorage.stateJson), _sceneStorage.objectLinks);
            // Kein Rückgriff auf globale EditorPrefs: eine neue Szene beginnt leer.
            _lastSavedJson = JsonUtility.ToJson(CaptureSaveData(out _));
            _stateLoaded = true;
        }
        catch (System.Exception ex)
        {
            _loadFailed = true;
            Debug.LogError("[HierarchyOrganizer] Szenendaten konnten nicht geladen werden; gespeicherte Daten bleiben unverändert. " + ex.Message);
        }
        _stateDirty = false;
        Repaint();
    }

    void EnsureSceneContext()
    {
        if (_suspendPersistence || EditorApplication.isPlaying || _writingState) return;
        var current = SceneManager.GetActiveScene();
        if (current == _closingScene) return;
        if (_loadFailed && current == _dataScene) return;
        if (!_stateLoaded || current != _dataScene)
        {
            SaveState();
            _closingScene = default;
            LoadState();
        }
    }

    void OnActiveSceneChanged(Scene previous, Scene next) => EnsureSceneContext();
    void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        _closingScene = default;
        EnsureSceneContext();
    }
    void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
    {
        _closingScene = default;
        EnsureSceneContext();
    }

    void OnSceneClosing(Scene scene, bool removingScene)
    {
        if (_suspendPersistence || scene != _dataScene) return;
        SaveState();
        _closingScene = scene;
        _stateLoaded = false;
    }

    void OnSceneSaving(Scene scene, string path)
    {
        if (scene == _dataScene) SaveState(true);
    }

    void OnSceneSaved(Scene scene)
    {
        // 'Speichern unter' kann GUIDs ändern. Die nativen Verweise wurden mitgespeichert.
        if (!_suspendPersistence && _stateLoaded && scene == _dataScene && _sceneStorage != null)
        {
            _lastSavedJson = _sceneStorage.stateJson ?? "";
            _stateDirty = false;
        }
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SaveState();
            _suspendPersistence = true;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            _suspendPersistence = false;
            _closingScene = default;
            LoadState();
        }
    }

    void ImportLegacyState()
    {
        string json = EditorPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(json)) json = EditorPrefs.GetString("HierarchyOrganizer_v3", "");
        if (string.IsNullOrEmpty(json) || !_stateLoaded) return;
        if (!EditorUtility.DisplayDialog("Alte Tabs übernehmen?",
            "Der alte globale Stand wird dieser aktiven Szene zugeordnet und ersetzt ihre Organizer-Tabs. "
            + "Objekte aus anderen oder nicht mehr geladenen Szenen können nicht zugeordnet werden.", "Übernehmen", "Abbrechen")) return;
        try
        {
            ApplySaveData(JsonUtility.FromJson<SaveData>(json), null, true);
            SaveState(true);
            Repaint();
            ShowNotification(new GUIContent("Alte Tabs übernommen. Bitte die Szene speichern."));
        }
        catch (System.Exception ex) { EditorUtility.DisplayDialog("Import fehlgeschlagen", ex.Message, "OK"); }
    }

    void EnsureStyles()
    {
        if (_stTabActive != null && _stTabNormal != null && _stHeader != null &&
            _stEntryName != null && _stEntryPath != null && _stBadge != null &&
            _stNotes != null && _stFilterChip != null && _stFilterChipActive != null &&
            _stPrefabCardName != null)
        {
            return;
        }

        _stTabActive = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        _stTabNormal = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };

        _stHeader = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
        };

        _stEntryName = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        _stEntryPath = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.72f, 0.72f, 0.72f) }
        };

        _stBadge = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.65f, 0.88f, 1f) }
        };

        _stNotes = new GUIStyle(EditorStyles.textArea)
        {
            fontSize = 11,
            wordWrap = true,
            normal = { textColor = new Color(0.95f, 0.95f, 0.85f) }
        };

        _stFilterChip = new GUIStyle(EditorStyles.miniButtonMid)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 18,
            normal = { textColor = new Color(0.82f, 0.82f, 0.82f) }
        };

        _stFilterChipActive = new GUIStyle(EditorStyles.miniButtonMid)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fixedHeight = 18,
            normal = { textColor = Color.white }
        };

        _stPrefabCardName = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            clipping = TextClipping.Clip,
            normal = { textColor = new Color(0.84f, 0.84f, 0.84f) }
        };
    }
}

// ══════════════════════════════════════════════
//  Tab Color & Style Popup Dialog
// ══════════════════════════════════════════════

public class TabColorPickerPopup : EditorWindow
{
    HierarchyOrganizerWindow.TabData _tab;
    HierarchyOrganizerWindow         _parent;
    Color                            _customColor = Color.cyan;

    public static void ShowWindow(HierarchyOrganizerWindow parent, HierarchyOrganizerWindow.TabData tab)
    {
        var win = CreateInstance<TabColorPickerPopup>();
        win.titleContent = new GUIContent("Tab anpassen");
        win._tab    = tab;
        win._parent = parent;
        win.minSize = new Vector2(280, 360);
        win.maxSize = new Vector2(320, 420);

        if (!string.IsNullOrEmpty(tab.customHexColor) && ColorUtility.TryParseHtmlString(tab.customHexColor, out Color c))
            win._customColor = c;
        else
            win._customColor = parent.GetTabColor(tab);

        win.ShowUtility();
    }

    void OnGUI()
    {
        if (_tab == null || _parent == null)
        {
            Close();
            return;
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.Space(8);
        GUILayout.Label("Tab anpassen: " + _tab.name, EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Name & Emoji:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        _tab.iconEmoji = EditorGUILayout.TextField(_tab.iconEmoji, GUILayout.Width(36));
        _tab.name      = EditorGUILayout.TextField(_tab.name);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        string[] quickIcons = { "📁", "🏠", "🎮", "🖼️", "💡", "🔊", "🤖", "🎬", "📦", "⚙️", "🚩", "💎", "✨" };
        foreach (var qi in quickIcons)
        {
            if (GUILayout.Button(qi, EditorStyles.miniButton, GUILayout.Width(22)))
            {
                _tab.iconEmoji = qi;
                _parent.Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Farbe wählen:", EditorStyles.miniBoldLabel);

        int cols = 7;
        for (int i = 0; i < HierarchyOrganizerWindow.PresetColors.Length; i += cols)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = 0; j < cols && (i + j) < HierarchyOrganizerWindow.PresetColors.Length; j++)
            {
                int idx = i + j;
                var pc  = HierarchyOrganizerWindow.PresetColors[idx];
                var oldB = GUI.backgroundColor;
                GUI.backgroundColor = pc.color;
                if (GUILayout.Button("", GUILayout.Width(34), GUILayout.Height(22)))
                {
                    _tab.colorIndex     = idx;
                    _tab.customHexColor = "";
                    _customColor        = pc.color;
                    _parent.Repaint();
                }
                GUI.backgroundColor = oldB;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("Freie RGB / HEX Farbwahl:", EditorStyles.miniBoldLabel);
        EditorGUI.BeginChangeCheck();
        _customColor = EditorGUILayout.ColorField(new GUIContent("Eigene Farbe"), _customColor, true, false, false);
        if (EditorGUI.EndChangeCheck())
        {
            _tab.customHexColor = "#" + ColorUtility.ToHtmlStringRGB(_customColor);
            _parent.Repaint();
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Darstellungs-Stil:", EditorStyles.miniBoldLabel);
        _tab.tabStyle = EditorGUILayout.Popup("Stil", _tab.tabStyle, new[] { "Akzentlinie oben", "Volle Tönung (Glow)", "Pille mit Farbpunkt" });

        if (EditorGUI.EndChangeCheck())
            _parent.StateChanged();

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Fertig", GUILayout.Height(28)))
        {
            if (_tab != null && _parent != null)
                _tab.name = _parent.GetUniqueTabName(_tab.name, _tab);
            _parent.StateChanged();
            Close();
        }
    }

    void OnDisable()
    {

        if (_tab != null && _parent != null)
        {
            _tab.name = _parent.GetUniqueTabName(_tab.name, _tab);
            _parent.StateChanged();
        }
    }
}

public sealed class HierarchyOrganizerRenameWindow : EditorWindow
{
    GameObject _target;
    string _nameBuffer;
    bool _english;
    bool _focusField = true;

    public static void ShowWindow(GameObject target, bool english)
    {
        if (target == null) return;
        var window = CreateInstance<HierarchyOrganizerRenameWindow>();
        window._target = target;
        window._nameBuffer = target.name;
        window._english = english;
        window.titleContent = new GUIContent(english ? "Rename Object" : "Objekt umbenennen");
        window.minSize = window.maxSize = new Vector2(320f, 82f);
        window.ShowUtility();
    }

    void OnGUI()
    {
        if (_target == null)
        {
            Close();
            return;
        }

        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            e.Use();
            Close();
            return;
        }

        GUILayout.Space(8f);
        GUI.SetNextControlName("HierarchyOrganizerRenameField");
        _nameBuffer = EditorGUILayout.TextField(_english ? "Name" : "Name", _nameBuffer);
        if (_focusField)
        {
            EditorGUI.FocusTextInControl("HierarchyOrganizerRenameField");
            _focusField = false;
        }

        bool confirmWithEnter = e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_english ? "Cancel" : "Abbrechen", GUILayout.Width(85f))) Close();
        if (GUILayout.Button(_english ? "Rename" : "Umbenennen", GUILayout.Width(90f)) || confirmWithEnter)
        {
            if (confirmWithEnter) e.Use();
            string nextName = (_nameBuffer ?? "").Trim();
            if (!string.IsNullOrEmpty(nextName) && nextName != _target.name)
            {
                Undo.RecordObject(_target, _english ? "Rename Object" : "Objekt umbenennen");
                _target.name = nextName;
                EditorUtility.SetDirty(_target);
                EditorGUIUtility.PingObject(_target);
            }
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif
