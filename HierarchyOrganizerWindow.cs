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
    public class HistoryEntry
    {
        public EntityId entityId;
        public string name;
        public string scenePath;
        public HistoryEntry(GameObject go)
        {
            entityId = go.GetEntityId();
            name       = go.name;
            scenePath  = BuildPath(go);
        }
        public GameObject Resolve() => EditorUtility.EntityIdToObject(entityId) as GameObject;
    }

    [System.Serializable]
    public class TabData
    {
        public string  name           = "Tab";
        public bool    renaming;
        public string  renameBuffer   = "";
        public int     viewMode       = 1;   // 1=Tab-Inhalt (Pinned), 0=Ganze Szene (Hierarchy), 2=Verlauf (History)
        public string  searchQuery    = "";
        public string  tagFilter      = "";
        public int     layerFilter    = -1;  // -1 = alle
        public int     colorIndex     = 0;   // Index in PresetColors (0..13)
        public string  customHexColor = "";  // Eigene Farbe (z.B. "#3B82F6")
        public string  iconEmoji      = "";  // Optionales Emoji (z.B. "📁", "🎮", "💡")
        public int     tabStyle       = 0;   // 0=Akzentlinie, 1=Volle Tönung, 2=Pille
        public bool    locked         = false;
        public string  notes          = "";
        public bool    showNotes      = false;
        public Vector2 scroll;
        public List<string> folderGuids = new List<string>();
        public List<EntityId>     pinnedIDs   = new List<EntityId>();
        public List<HistoryEntry> history     = new List<HistoryEntry>();
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
    bool                          _recordEnabled      = true;
    int                           _maxHistory         = 50;
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
    bool                          _showTooltips       = true;
    int                           _autosaveIntervalMinutes = 5;
    bool                          _autoNameTabs;

    // Drag & Reorder
    int                           _dragOverTab        = -1;
    int                           _dragSourceTab      = -1;
    EntityId                      _dragSourceID       = default;
    Vector2                       _dragStartPos;
    const float                   DragThreshold       = 8f;
    Vector2                       _globalScroll;
    Vector2                       _quickFilterScroll;

    readonly List<GameObject> _visibleSelectionRows = new List<GameObject>();
    readonly List<GameObject> _drawnSelectionRows = new List<GameObject>();
    readonly HashSet<EntityId> _drawnSelectionIDs = new HashSet<EntityId>();
    readonly HashSet<EntityId> _selectedIDs = new HashSet<EntityId>();
    readonly Dictionary<EntityId, bool> _filterMatchCache = new Dictionary<EntityId, bool>();
    readonly Dictionary<EntityId, Component[]> _componentCache = new Dictionary<EntityId, Component[]>();
    readonly List<GameObject> _filteredSceneCache = new List<GameObject>();
    string _filteredSceneCacheKey;
    int _hierarchyVersion;
    int _stateVersion;
    GameObject _selectionAnchor;
    GameObject _pendingSingleSelection;

    // Die Farbzuordnung folgt den Pins; sie verändert keine Objektfarben.
    class TabColorGroup
    {
        public string key;
        public Color color;
        public readonly List<TabData> tabs = new List<TabData>();
        public readonly HashSet<EntityId> pinnedIDs = new HashSet<EntityId>();
    }

    readonly List<TabColorGroup> _tabColorGroups = new List<TabColorGroup>();
    TabData _sceneColorFilterTab;
    TabColorGroup _activeSceneColorGroup;

    // Tooltip
    GameObject                    _hoveredGO;
    Vector2                       _hoverMousePos;
    double                        _hoverStartTime;
    const float                   TooltipDelay        = 0.15f;

    // Style-Cache
    GUIStyle _stTabActive, _stTabNormal, _stHeader;
    GUIStyle _stEntryName, _stEntryPath, _stBadge;
    GUIStyle _stNotes, _stFilterChip, _stFilterChipActive;

    // Autosave
    const int    AutosaveMaxFiles   = 5;
    double       _lastAutosaveTime  = -1;
    string       _lastAutosaveLabel = "";
    bool         _autosaveEnabled   = true;
    Scene _dataScene;
    HierarchyOrganizerSceneData _sceneStorage;
    bool _stateLoaded, _writingState, _suspendPersistence, _loadFailed;
    bool _stateDirty;
    Scene _closingScene;
    string _lastSavedJson = "";
    double _nextStateSync;

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
        return System.IO.Path.Combine(Application.dataPath, "JustDoIt_Tools", "HierarchyOrganizer", "HO_Backup", sceneKey);
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
        if (tab == null) return PresetColors[0].color;
        if (!string.IsNullOrEmpty(tab.customHexColor) && ColorUtility.TryParseHtmlString(tab.customHexColor, out Color c))
            return c;
        int idx = Mathf.Clamp(tab.colorIndex, 0, PresetColors.Length - 1);
        return PresetColors[idx].color;
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
        EditorApplication.projectChanged += RefreshFolderAssets;
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
        var go = Selection.activeGameObject;
        if (!IsObjectInContext(go) || CurrentTab == null) return;

        bool changed = false;
        foreach (var tab in _tabs)
        {
            if (tab.locked && tab != CurrentTab) continue;
            if (!_recordEnabled) continue;
            tab.history.RemoveAll(e => e.entityId == go.GetEntityId());
            tab.history.Insert(0, new HistoryEntry(go));
            if (tab.history.Count > _maxHistory)
                tab.history.RemoveRange(_maxHistory, tab.history.Count - _maxHistory);
            changed = true;
        }
        if (changed) StateChanged();
        else Repaint();
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
    }

    internal void StateChanged()
    {
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
        DrawTabBar();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        if (GUILayout.Button(new GUIContent("Main neu einlesen",
            "Öffnet Main mit der gesamten aktuellen Szene und setzt dort Such- und Farbfilter zurück."),
            EditorStyles.miniButton, GUILayout.Width(140), GUILayout.Height(24)))
            RefreshMainView();
        GUILayout.Space(4);
        _autoNameTabs = GUILayout.Toggle(_autoNameTabs,
            new GUIContent(_autoNameTabs ? "Auto-Name: AN" : "Auto-Name: AUS",
                "Leere Tabs beim Ablegen nach dem ersten Objekt benennen. Main und gesperrte Tabs behalten ihren Namen."),
            EditorStyles.miniButton, GUILayout.Width(125), GUILayout.Height(24));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        var tab = CurrentTab;
        if (tab == null)
        {
            if (EditorGUI.EndChangeCheck()) StateChanged();
            return;
        }

        _selectedIDs.Clear();
        var selectedObjects = Selection.gameObjects;
        for (int i = 0; i < selectedObjects.Length; i++)
            if (selectedObjects[i] != null) _selectedIDs.Add(selectedObjects[i].GetEntityId());

        if (_globalSearch)
            DrawGlobalSearchBar();
        else
            DrawSearchBar(tab);

        if (_showQuickFilterBar)
            DrawQuickFilterChips(tab);

        DrawViewModeBar(tab);

        _activeSceneColorGroup = null;
        if (tab.viewMode == 0 || (_globalSearch && !string.IsNullOrEmpty(_globalQuery)))
            DrawTabColorFilterBar(tab);

        if (tab.showNotes) DrawNotesArea(tab);

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

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Hierarchy Organizer", _stHeader ?? EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

        if (!string.IsNullOrEmpty(_lastAutosaveLabel))
        {
            var sc = GUI.color; GUI.color = new Color(0.6f, 0.95f, 0.65f);
            if (GUILayout.Button("💾 " + _lastAutosaveLabel, EditorStyles.toolbarButton, GUILayout.Width(72)))
            {
                string folder = GetAutosaveFolder();
                if (System.IO.Directory.Exists(folder)) EditorUtility.RevealInFinder(folder);
            }
            GUI.color = sc;
        }

        var gCol = _globalSearch ? new Color(1f, 0.88f, 0.25f) : new Color(0.85f, 0.85f, 0.85f);
        var old = GUI.color; GUI.color = gCol;
        if (GUILayout.Button("⌕ Global", EditorStyles.toolbarButton, GUILayout.Width(62)))
        {
            _globalSearch = !_globalSearch;
            _globalQuery  = "";
        }
        GUI.color = old;

        var recCol = _recordEnabled ? new Color(0.35f, 0.95f, 0.45f) : new Color(0.75f, 0.75f, 0.75f);
        GUI.color = recCol;
        if (GUILayout.Button(_recordEnabled ? "● REC" : "○ REC", EditorStyles.toolbarButton, GUILayout.Width(54)))
            _recordEnabled = !_recordEnabled;
        GUI.color = old;

        GUI.color = new Color(0.7f, 0.95f, 0.75f);
        if (GUILayout.Button("↑ Save", EditorStyles.toolbarButton, GUILayout.Width(48)))
            SaveBackup();
        GUI.color = new Color(0.7f, 0.85f, 1f);
        if (GUILayout.Button("↓ Load", EditorStyles.toolbarButton, GUILayout.Width(48)))
            LoadBackup();
        GUI.color = old;

        if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(26)))
            ShowSettingsMenu();

        EditorGUILayout.EndHorizontal();
    }

    void RefreshMainView()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            ShowNotification(new GUIContent("Keine aktive Szene geladen."));
            return;
        }

        var main = _tabs.Find(t => string.Equals(t.name?.Trim(), "Main",
            System.StringComparison.OrdinalIgnoreCase));
        if (main == null)
        {
            main = new TabData { name = "Main", iconEmoji = "📁" };
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
        ShowNotification(new GUIContent("Main neu eingelesen: " + scene.name));
    }

    // ── Tab Bar ──────────────────────────────────

    static string GetTabKindLabel(TabData tab)
    {
        bool hasFolders = (tab.folderGuids?.Count ?? 0) > 0;
        bool hasHierarchy = (tab.pinnedIDs?.Count ?? 0) > 0;
        if (hasFolders) return hasHierarchy ? "Gemischt" : "Projekt";
        return hasHierarchy || tab.viewMode != 1 ? "Hierarchie" : "Leer";
    }

    static Rect[] CalculateTabLayout(float width, int tabCount, out float height)
    {
        const float padding = 4f, gap = 3f, rowHeight = 48f, tabHeight = 44f;
        float availableWidth = Mathf.Max(1f, width - padding * 2);
        float tabWidth = Mathf.Min(140f, availableWidth);
        var rects = new Rect[tabCount + 1]; // Der letzte Platz gehört dem Plus-Button.
        float x = padding;
        float y = 2f;
        for (int i = 0; i <= tabCount; i++)
        {
            float itemWidth = i == tabCount ? Mathf.Min(30f, availableWidth) : tabWidth;
            if (x > padding && x + itemWidth > padding + availableWidth)
            {
                x = padding;
                y += rowHeight;
            }
            rects[i] = new Rect(x, y, itemWidth, tabHeight);
            x += itemWidth + gap;
        }
        height = y + tabHeight + 2f;
        return rects;
    }

    void DrawTabBar()
    {
        int n = _tabs.Count;
        var tabRects = CalculateTabLayout(position.width, n, out float barHeight);
        var bar = GUILayoutUtility.GetRect(0, barHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(bar, ColTabBar);

        for (int i = 0; i < n; i++)
        {
            var  tab      = _tabs[i];
            bool active   = i == _selectedTab;
            bool dragOver = _dragOverTab == i;
            var  tRect    = tabRects[i];
            tRect.position += bar.position;
            var  tabCol   = GetTabColor(tab);

            Color baseBg = active ? ColTabActive : ColTabBg;
            if (dragOver) baseBg = ColDragOver;

            if (tab.tabStyle == 1)
            {
                Color tint = new Color(tabCol.r, tabCol.g, tabCol.b, active ? 0.32f : 0.16f);
                EditorGUI.DrawRect(tRect, Color.Lerp(baseBg, tint, 0.6f));
            }
            else
            {
                EditorGUI.DrawRect(tRect, baseBg);
            }

            if (tab.tabStyle == 2)
            {
                EditorGUI.DrawRect(new Rect(tRect.x + 6, tRect.y + (tRect.height - 8) / 2, 8, 8), tabCol);
            }
            else
            {
                float topH = active ? 3.5f : 2f;
                EditorGUI.DrawRect(new Rect(tRect.x, tRect.y, tRect.width, topH), tabCol);

                float barW = active ? 3.5f : 2f;
                EditorGUI.DrawRect(new Rect(tRect.x, tRect.y + topH, barW, tRect.height - topH), tabCol);
            }

            if (active)
            {
                EditorGUI.DrawRect(new Rect(tRect.x, tRect.yMax - 1, tRect.width, 1), tabCol);
            }

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
                        new Rect(tRect.x + 6, tRect.y + 6, tRect.width - 24, tRect.height - 10),
                        tab.renameBuffer, EditorStyles.miniTextField);
                    if (GUI.GetNameOfFocusedControl() != ctrl) GUI.FocusControl(ctrl);
                }
            }
            else
            {
                if (tab.locked)
                {
                    var lc = GUI.color; GUI.color = ColLocked;
                    GUI.Label(new Rect(tRect.xMax - 32, tRect.y + 4, 14, tRect.height), "🔒", EditorStyles.miniLabel);
                    GUI.color = lc;
                }

                string prefix = string.IsNullOrEmpty(tab.iconEmoji) ? "" : tab.iconEmoji + " ";
                string displayName = prefix + tab.name;
                if (tab.pinnedIDs.Count + (tab.folderGuids?.Count ?? 0) > 0)
                    displayName += "  +" + (tab.pinnedIDs.Count + (tab.folderGuids?.Count ?? 0));

                float textX = (tab.tabStyle == 2) ? tRect.x + 18 : tRect.x + 8;
                string kind = GetTabKindLabel(tab);
                string tooltip = kind + "-Tab: " + tab.name + "\n"
                    + (tab.folderGuids?.Count ?? 0) + " Projektordner, "
                    + tab.pinnedIDs.Count + " Hierarchieobjekte";
                float textWidth = Mathf.Max(1f, tRect.xMax - textX - (tab.locked ? 34f : 18f));
                GUI.Label(new Rect(textX, tRect.y + 4, textWidth, 15),
                    new GUIContent(kind, tooltip), EditorStyles.miniLabel);
                GUI.Label(new Rect(textX, tRect.y + 19, textWidth, 21),
                    new GUIContent(displayName, tooltip), active ? (_stTabActive ?? EditorStyles.boldLabel) : (_stTabNormal ?? EditorStyles.label));

                if (Event.current.type == EventType.MouseDown && tRect.Contains(Event.current.mousePosition))
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
                        _selectedTab = i;
                        _dragSourceTab = i;
                    }
                    Event.current.Use();
                    StateChanged();
                }
            }

            var cR = new Rect(tRect.xMax - 16, tRect.y + 7, 12, 12);
            var oc = GUI.color; GUI.color = new Color(1, 1, 1, 0.65f);
            if (GUI.Button(cR, "×", EditorStyles.miniLabel))
            {
                RemoveTab(i);
                return;
            }
            GUI.color = oc;

            HandleTabDragDrop(i, tRect, tab);

        }

        var addR = tabRects[n];
        addR.position += bar.position;
        EditorGUI.DrawRect(addR, ColTabHover);
        var oldColor = GUI.color; GUI.color = Color.white;
        if (GUI.Button(addR, "+", EditorStyles.boldLabel))
            AddTab();
        GUI.color = oldColor;
        HandleNewFolderTabDrop(addR);

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
        if (string.Equals(tab.name?.Trim(), "Main", System.StringComparison.OrdinalIgnoreCase))
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
                bool hasGo = DragAndDrop.objectReferences.Any(o => (o is GameObject go && IsObjectInContext(go)) || IsProjectFolder(o));
                DragAndDrop.visualMode = hasGo ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                _dragOverTab = hasGo ? tabIndex : -1;
                Event.current.Use();
                Repaint();
            }
            else if (ev == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                int added = AddDroppedFolders(tab);
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && IsObjectInContext(go))
                    {
                        EntityId id = go.GetEntityId();
                        if (!tab.pinnedIDs.Contains(id))
                        {
                            AutoNameEmptyTab(tab, go);
                            tab.pinnedIDs.Add(id);
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

        menu.AddItem(new GUIContent("🎨 Farbe & Stil anpassen..."), false, () =>
        {
            TabColorPickerPopup.ShowWindow(this, tab);
        });
        menu.AddSeparator("");

        for (int c = 0; c < PresetColors.Length; c++)
        {
            int ci = c;
            menu.AddItem(new GUIContent("Farbe/" + PresetColors[c].name),
                string.IsNullOrEmpty(tab.customHexColor) && tab.colorIndex == ci, () =>
                {
                    tab.colorIndex = ci;
                    tab.customHexColor = "";
                    StateChanged();
                });
        }
        menu.AddSeparator("Farbe/");
        menu.AddItem(new GUIContent("Farbe/🎨 Eigene Farbe wählen..."), false, () => TabColorPickerPopup.ShowWindow(this, tab));

        menu.AddItem(new GUIContent("Tab-Stil/Akzentlinie oben"), tab.tabStyle == 0, () => { tab.tabStyle = 0; StateChanged(); });
        menu.AddItem(new GUIContent("Tab-Stil/Volle Tönung"),    tab.tabStyle == 1, () => { tab.tabStyle = 1; StateChanged(); });
        menu.AddItem(new GUIContent("Tab-Stil/Pille mit Punkt"), tab.tabStyle == 2, () => { tab.tabStyle = 2; StateChanged(); });

        string[] sampleEmojis = { "📁 Ordner", "🏠 Main", "🎮 Gameplay", "🖼️ GUI/UI", "💡 Beleuchtung", "🔊 Audio", "🤖 KI/Enemies", "🎬 Kamera/Cutscene", "📦 Props/Assets", "⚙️ Setup", "🚩 Spawns", "💎 Collectibles", "✨ Effekte" };
        foreach (var se in sampleEmojis)
        {
            string emoji = se.Split(' ')[0];
            menu.AddItem(new GUIContent("Icon / Emoji/" + se), tab.iconEmoji == emoji, () => { tab.iconEmoji = emoji; StateChanged(); });
        }
        menu.AddItem(new GUIContent("Icon / Emoji/✕ Kein Icon"), string.IsNullOrEmpty(tab.iconEmoji), () => { tab.iconEmoji = ""; StateChanged(); });

        menu.AddSeparator("");

        if (idx > 0)
            menu.AddItem(new GUIContent("Nach links verschieben"), false, () => MoveTab(idx, -1));
        else
            menu.AddDisabledItem(new GUIContent("Nach links verschieben"));

        if (idx < _tabs.Count - 1)
            menu.AddItem(new GUIContent("Nach rechts verschieben"), false, () => MoveTab(idx, 1));
        else
            menu.AddDisabledItem(new GUIContent("Nach rechts verschieben"));

        menu.AddItem(new GUIContent("Tab duplizieren"), false, () => DuplicateTab(idx));

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Umbenennen"), false, () => { tab.renaming = true; tab.renameBuffer = tab.name; StateChanged(); });
        menu.AddItem(new GUIContent(tab.locked ? "Entsperren" : "Sperren"), false, () => { tab.locked = !tab.locked; StateChanged(); });
        menu.AddItem(new GUIContent(tab.showNotes ? "Notiz verbergen" : "Notiz anzeigen"), false, () => { tab.showNotes = !tab.showNotes; StateChanged(); });

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Tab leeren (alle Pins entfernen)"), false, () =>
        {
            if (tab.pinnedIDs.Count == 0) return;
            if (EditorUtility.DisplayDialog("Tab leeren?", $"Alle {tab.pinnedIDs.Count} angehefteten Objekte aus '{tab.name}' entfernen?", "Ja, leeren", "Abbrechen"))
            {
                tab.pinnedIDs.Clear();
                StateChanged();
            }
        });
        menu.AddItem(new GUIContent("Tab löschen"), false, () => RemoveTab(idx));

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
            notes          = src.notes,
            showNotes      = src.showNotes,
            pinnedIDs      = new List<EntityId>(src.pinnedIDs),
            folderGuids = new List<string>(src.folderGuids ?? new List<string>()),
            history        = new List<HistoryEntry>(src.history)
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

    void DrawFilterBar(TabData tab)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
        GUILayout.Space(6);

        try
        {
            GUILayout.Label("Tag:", EditorStyles.miniLabel, GUILayout.Width(28));
            string[] allTags = UnityEditorInternal.InternalEditorUtility.tags ?? new string[0];
            string[] tags    = new[] { "(alle)" }.Concat(allTags).ToArray();
            int      tagIdx  = Mathf.Max(0, System.Array.IndexOf(tags, tab.tagFilter));
            int      newTag  = EditorGUILayout.Popup(tagIdx, tags, EditorStyles.toolbarPopup, GUILayout.Width(80));
            tab.tagFilter    = newTag == 0 ? "" : tags[newTag];

            GUILayout.Space(8);

            GUILayout.Label("Layer:", EditorStyles.miniLabel, GUILayout.Width(38));
            var layerNames = new List<string> { "(alle)" };
            for (int l = 0; l < 32; l++)
            {
                string ln = LayerMask.LayerToName(l);
                if (!string.IsNullOrEmpty(ln)) layerNames.Add(ln);
            }
            string[] layers   = layerNames.ToArray();
            int      layerIdx = tab.layerFilter < 0 ? 0 : Mathf.Clamp(tab.layerFilter + 1, 0, layers.Length - 1);
            int      newLayer = EditorGUILayout.Popup(layerIdx, layers, EditorStyles.toolbarPopup, GUILayout.Width(80));
            tab.layerFilter   = newLayer == 0 ? -1 : newLayer - 1;
        }
        catch
        {
            GUILayout.Label("Filter n/a", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Space(4);
        EditorGUILayout.EndHorizontal();
    }

    // ── Schnellfilter-Chips (1-Klick Filter) ───────

    void DrawQuickFilterChips(TabData tab)
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
        GUILayout.Space(6);

        _quickFilterScroll = EditorGUILayout.BeginScrollView(_quickFilterScroll, GUIStyle.none, GUIStyle.none, GUILayout.Height(26));
        EditorGUILayout.BeginHorizontal();

        DrawChip(tab, "", "Alle");
        DrawChip(tab, "lights", "💡 Lights");
        DrawChip(tab, "triggers", "⚡ Triggers (GC2)");
        DrawChip(tab, "actions", "🎬 Actions (GC2)");
        DrawChip(tab, "character", "👤 Character / Player");
        DrawChip(tab, "variables", "📊 Variables (GC2)");
        DrawChip(tab, "cameras", "🎥 Kameras / Shots");
        DrawChip(tab, "ui", "🖼️ UI");
        DrawChip(tab, "colliders", "🔲 Collider");
        DrawChip(tab, "physics", "⚖️ Physics / RB");
        DrawChip(tab, "navmesh", "🏃 NavMesh");
        DrawChip(tab, "audio", "🔊 Audio");
        DrawChip(tab, "vfx", "💥 VFX");
        DrawChip(tab, "missing", "⚠️ Missing");
        DrawChip(tab, "favorites", "⭐ Favs");
        DrawChip(tab, "inactive", "👁️ Inaktiv");

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);
        EditorGUILayout.EndHorizontal();
    }

    void DrawChip(TabData tab, string key, string label)
    {
        bool active = tab.quickFilter == key;
        var style   = (active ? _stFilterChipActive : _stFilterChip) ?? EditorStyles.miniButtonMid;
        var tabCol  = GetTabColor(tab);

        var oldBg = GUI.backgroundColor;
        if (active) GUI.backgroundColor = tabCol;

        if (GUILayout.Button(label, style))
        {
            tab.quickFilter = (active && key != "") ? "" : key;
        }
        GUI.backgroundColor = oldBg;
    }

    // ── Tab-Farben: unsichtbare Mitgliedschaft, sichtbarer Suchfilter ──

    void RebuildTabColorGroups()
    {
        _tabColorGroups.Clear();
        if (_sceneColorFilterTab != null && !_tabs.Contains(_sceneColorFilterTab))
            _sceneColorFilterTab = null;

        foreach (var sourceTab in _tabs)
        {
            Color color = GetTabColor(sourceTab);
            string key = ColorUtility.ToHtmlStringRGB(color);
            var group = _tabColorGroups.Find(g => g.key == key);
            if (group == null)
            {
                group = new TabColorGroup { key = key, color = color };
                _tabColorGroups.Add(group);
            }
            group.tabs.Add(sourceTab);
            if (sourceTab.pinnedIDs != null)
                group.pinnedIDs.UnionWith(sourceTab.pinnedIDs);
        }

        _activeSceneColorGroup = _sceneColorFilterTab == null ? null
            : _tabColorGroups.Find(g => g.tabs.Contains(_sceneColorFilterTab));
    }

    void DrawTabColorFilterBar(TabData viewTab)
    {
        RebuildTabColorGroups();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUILayout.Label(new GUIContent("Tab-Farben", "Objekte anhand ihrer Tab-Zuordnung finden. Unterobjekte gehören dazu."),
            EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Alle Farben", "Farbfilter aufheben; andere Suchfilter bleiben erhalten."),
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
            var content = new GUIContent(string.Join(", ", _tabColorGroups[i].tabs.Select(t => t.name)));
            filterLabels[i] = content;
            float width = Mathf.Min(availableWidth, Mathf.Max(75f,
                Mathf.Ceil(labelStyle.CalcSize(content).x) + 40f));
            float height = Mathf.Max(24f,
                Mathf.Ceil(labelStyle.CalcHeight(content, width - 38f)) + 8f);
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
            string tooltip = "Tabs: " + names + "\nFarbe: #" + group.key
                + "\nZeigt angeheftete Objekte einschließlich ihrer Unterobjekte."
                + (active ? "\nErneut klicken, um den Farbfilter aufzuheben." : "");
            if (GUI.Button(rect, new GUIContent("", tooltip), EditorStyles.miniButton))
            {
                _sceneColorFilterTab = active ? null : group.tabs[0];
                _activeSceneColorGroup = active ? null : group;
                viewTab.scroll = Vector2.zero;
                _globalScroll = Vector2.zero;
                StateChanged();
            }
            EditorGUI.DrawRect(new Rect(rect.x + 6, rect.center.y - 5, 20, 10),
                new Color(group.color.r, group.color.g, group.color.b, 1f));
            GUI.Label(new Rect(rect.x + 31, rect.y + 4, rect.width - 38, rect.height - 8), filterLabels[i], labelStyle);
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
        EditorGUILayout.BeginHorizontal(GUILayout.Height(26));
        GUILayout.Space(6);

        // 1 = Pinned (Tab-Inhalt), 0 = Hierarchy (Ganze Szene), 2 = History (Verlauf)
        DrawModeBtn(tab, 1, "📌 Tab-Inhalt " + (tab.pinnedIDs.Count > 0 ? "(" + tab.pinnedIDs.Count + ")" : ""));
        DrawModeBtn(tab, 0, "🌐 Ganze Szene");
        DrawModeBtn(tab, 2, "⏱ Verlauf " + (tab.history.Count > 0 ? "(" + tab.history.Count + ")" : ""));

        GUILayout.FlexibleSpace();

        var nc = tab.showNotes ? new Color(1f, 0.9f, 0.35f) : new Color(0.7f, 0.7f, 0.7f);
        var oc = GUI.color; GUI.color = nc;
        if (GUILayout.Button("✏ Notiz", EditorStyles.miniButton, GUILayout.Width(52)))
            tab.showNotes = !tab.showNotes;
        GUI.color = oc;

        var lc2 = tab.locked ? ColLocked : new Color(0.7f, 0.7f, 0.7f);
        GUI.color = lc2;
        if (GUILayout.Button(tab.locked ? "🔒 Gesperrt" : "🔓 Offen", EditorStyles.miniButton, GUILayout.Width(70)))
            tab.locked = !tab.locked;
        GUI.color = oc;

        if (tab.viewMode == 1 && tab.pinnedIDs.Count > 0)
        {
            if (GUILayout.Button("Leeren", EditorStyles.miniButton, GUILayout.Width(46)))
                tab.pinnedIDs.Clear();
        }
        if (tab.viewMode == 2 && tab.history.Count > 0)
        {
            if (GUILayout.Button("Leeren", EditorStyles.miniButton, GUILayout.Width(46)))
                tab.history.Clear();
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

        if (GUILayout.Button(label, EditorStyles.miniButtonMid, GUILayout.Width(100)))
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

        tab.scroll = EditorGUILayout.BeginScrollView(tab.scroll);
        switch (tab.viewMode)
        {
            case 1: DrawPinnedView(tab);    break;
            case 0: DrawHierarchyView(tab); break;
            case 2: DrawHistoryView(tab);   break;
        }
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════
    //  Global Search
    // ══════════════════════════════════════════════

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
            ? " Treffer in der gesamten Szene" : " Treffer mit gewählter Tab-Farbe"), EditorStyles.boldLabel);
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

        if (_showHierarchyLines && depth > 0)
        {
            Color lineCol = new Color(1f, 1f, 1f, 0.15f);
            EditorGUI.DrawRect(new Rect(rect.x + indent - 10, rect.y, 1, rect.height), lineCol);
            EditorGUI.DrawRect(new Rect(rect.x + indent - 10, midY, 8, 1), lineCol);
        }

        if (hasChildren)
        {
            var arrowRect = new Rect(rect.x + indent - 2, midY - 9, 18, 18);
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
            GUI.DrawTexture(new Rect(cx, midY - 8, 16, 16), ic, ScaleMode.ScaleToFit);
            GUI.color = oldI;
        }
        cx += 20f;

        var old = GUI.color;
        GUI.color = inactive ? new Color(0.65f, 0.65f, 0.65f)
                  : selected  ? Color.white
                  :             new Color(0.96f, 0.96f, 0.96f);

        float rightButtonsWidth = isPinnedList ? 110f : 90f;
        GUI.Label(new Rect(cx, midY - 9, rect.width - cx - rightButtonsWidth, 18), go.name, _stEntryName ?? EditorStyles.label);
        GUI.color = old;

        if (_showComponentIcons)
            DrawComponentIcons(go, rect, midY);

        float btnX = rect.xMax - 22;

        // Unpin button (✕) in Pinned view
        if (isPinnedList && depth == 0)
        {
            var oc = GUI.color; GUI.color = new Color(1f, 0.45f, 0.45f, hover ? 0.95f : 0.4f);
            if (GUI.Button(new Rect(btnX, midY - 9, 18, 18), "✕", EditorStyles.miniLabel))
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
        if (GUI.Button(new Rect(btnX, midY - 9, 18, 18), "★", EditorStyles.miniLabel))
        {
            if (isFav) _favorites.Remove(id);
            else       _favorites.Add(id);
        }
        GUI.color = old;
        btnX -= 20;

        if (hover || !go.activeSelf)
        {
            GUI.color = go.activeSelf ? new Color(0.7f, 0.95f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
            if (GUI.Button(new Rect(btnX, midY - 9, 18, 18), go.activeSelf ? "👁" : "○", EditorStyles.miniLabel))
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
            if (GUI.Button(new Rect(btnX, midY - 9, 18, 18), "🎯", EditorStyles.miniLabel))
            {
                Selection.activeGameObject = go;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
            }
            GUI.color = old;
        }

        HandleEntryEvents(rect, go, tab);

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

    void DrawComponentIcons(GameObject go, Rect rect, float midY)
    {
        if (Event.current.type != EventType.Repaint) return;
        try
        {
            var components = GetCachedComponents(go);
            if (components == null) return;

            int drawn = 0;
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
                    GUI.DrawTexture(new Rect(ix, midY - 7, 14, 14), content.image, ScaleMode.ScaleToFit);
                    ix -= 16f;
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
        HandleWindowDragDrop(tab);
        DrawProjectFolders(tab);

        if (tab.pinnedIDs.Count == 0 && (tab.folderGuids == null || tab.folderGuids.Count == 0))
        {
            GUILayout.Space(14);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(10);
            GUILayout.Label("📁 Dieser Tab ist leer", EditorStyles.boldLabel);
            GUILayout.Space(4);
            GUILayout.Label("Hefte Szenenobjekte oder Projektordner hier an:", EditorStyles.label);
            GUILayout.Space(10);

            int selCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
            var sc = GUI.color; GUI.color = new Color(0.35f, 0.85f, 1f);
            if (GUILayout.Button(selCount > 0 ? "＋ Ausgewählte Objekte anheften (" + selCount + " markiert)" : "＋ Ausgewählte Objekte anheften", GUILayout.Height(30)))
            {
                if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
                {
                    foreach (var go in Selection.gameObjects)
                    {
                        if (!IsObjectInContext(go)) continue;
                        EntityId id = go.GetEntityId();
                        if (!tab.pinnedIDs.Contains(id)) tab.pinnedIDs.Add(id);
                    }
                    StateChanged();
                }
                else
                {
                    ShowNotification(new GUIContent("Wähle zuerst Objekte in Unitys Hierarchy aus"));
                }
            }
            GUI.color = sc;

            GUILayout.Space(10);
            GUILayout.Label("Möglichkeiten zum Hinzufügen:", EditorStyles.miniBoldLabel);
            GUILayout.Label("1. Objekte aus der Unity Hierarchy per Drag & Drop direkt hier hineinziehen", EditorStyles.miniLabel);
            GUILayout.Label("2. Unity Hierarchy → Rechtsklick auf Objekt → 'Pin to Hierarchy Organizer'", EditorStyles.miniLabel);
            GUILayout.Label("3. Modus '🌐 Ganze Szene' wählen → Rechtsklick → 'Pin zu diesem Tab'", EditorStyles.miniLabel);
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
        {
            var sc = GUI.color; GUI.color = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("＋ " + Selection.gameObjects.Length + " ausgewählte anheften", EditorStyles.miniButton, GUILayout.Width(175)))
            {
                foreach (var go in Selection.gameObjects)
                {
                    if (!IsObjectInContext(go)) continue;
                    EntityId id = go.GetEntityId();
                    if (!tab.pinnedIDs.Contains(id)) tab.pinnedIDs.Add(id);
                }
                StateChanged();
            }
            GUI.color = sc;
        }
        GUILayout.FlexibleSpace();
        GUILayout.Label(tab.pinnedIDs.Count + " Objekt(e) angeheftet", EditorStyles.miniLabel);
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
    }

    sealed class FolderPrefabCache
    {
        public string path;
        public bool exists;
        public GUIContent title;
        public string[] prefabs;
        public string[] labels;
        public GUIContent[] rowContents;
        public string query;
        public int[] matches;

        public void EnsurePrefabs(string search)
        {
            if (prefabs == null)
            {
                prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                labels = prefabs.Select(p => p.Substring(path.Length + 1)).ToArray();
                rowContents = new GUIContent[prefabs.Length];
            }
            search = search ?? "";
            if (matches != null && query == search) return;
            query = search;
            matches = Enumerable.Range(0, prefabs.Length)
                .Where(i => search.Length == 0 || labels[i].IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        }
    }

    readonly Dictionary<string, FolderPrefabCache> _folderPrefabCache = new Dictionary<string, FolderPrefabCache>();
    readonly HashSet<string> _collapsedFolders = new HashSet<string>();
    Object _pendingAssetDrag;
    Vector2 _assetDragStart;

    void RefreshFolderAssets()
    {
        _folderPrefabCache.Clear();
        Repaint();
    }

    static bool IsProjectFolder(Object obj)
    {
        return obj != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj));
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

    void HandleNewFolderTabDrop(Rect rect)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition) || !DragAndDrop.objectReferences.Any(IsProjectFolder)) return;
        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            var first = DragAndDrop.objectReferences.First(IsProjectFolder);
            var tab = new TabData { name = GetUniqueTabName(first.name), viewMode = 1 };
            AddDroppedFolders(tab);
            _tabs.Add(tab);
            _selectedTab = _tabs.Count - 1;
            DragAndDrop.AcceptDrag();
            e.Use();
            StateChanged();
        }
    }

    void DrawProjectFolders(TabData tab)
    {
        if (tab.folderGuids == null) tab.folderGuids = new List<string>();
        string remove = null;
        foreach (string guid in tab.folderGuids)
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
            if (exists && GUILayout.Button("Im Project", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(path);
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
            if (GUILayout.Button(new GUIContent("×", "Ordner aus diesem Tab entfernen"), EditorStyles.miniButton, GUILayout.Width(22))) remove = guid;
            EditorGUILayout.EndHorizontal();
            if (!next || !exists) continue;
            cache.EnsurePrefabs(tab.searchQuery);
            int count = cache.matches.Length;
            if (count == 0)
            {
                GUILayout.Label("Keine passenden Prefabs in diesem Ordner oder seinen Unterordnern.", EditorStyles.miniLabel);
                continue;
            }
            // Ein Layout-Rechteck für die ganze Liste; nur sichtbare Zeilen zeichnen.
            // Die Fensterhöhe ist eine konservative Obergrenze für den Scroll-Viewport.
            float rowHeight = Mathf.Max(1f, _rowHeight);
            Rect listRect = GUILayoutUtility.GetRect(0, count * rowHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Layout) continue;
            int first = Mathf.Clamp(Mathf.FloorToInt((tab.scroll.y - listRect.y) / rowHeight), 0, count);
            int end = Mathf.Clamp(Mathf.CeilToInt((tab.scroll.y + position.height - listRect.y) / rowHeight), first, count);
            for (int visible = first; visible < end; visible++)
            {
                int index = cache.matches[visible];
                string prefabPath = cache.prefabs[index];
                Rect row = new Rect(listRect.x, listRect.y + visible * rowHeight, listRect.width, rowHeight);
                row.xMin += 18;
                var e = Event.current;
                if (e.type == EventType.Repaint)
                {
                    if (cache.rowContents[index] == null)
                        cache.rowContents[index] = new GUIContent(cache.labels[index], AssetDatabase.GetCachedIcon(prefabPath), prefabPath);
                    GUI.Label(row, cache.rowContents[index], EditorStyles.label);
                }
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (asset == null) continue;
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    _pendingAssetDrag = asset;
                    _assetDragStart = e.mousePosition;
                    if (e.clickCount == 2) AssetDatabase.OpenAsset(asset);
                    e.Use();
                }
            }
        }
        if (remove != null)
        {
            tab.folderGuids.Remove(remove);
            StateChanged();
        }
        var current = Event.current;
        if (current.type == EventType.MouseDrag && _pendingAssetDrag != null && Vector2.Distance(current.mousePosition, _assetDragStart) > DragThreshold)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { _pendingAssetDrag };
            DragAndDrop.StartDrag(_pendingAssetDrag.name);
            _pendingAssetDrag = null;
            current.Use();
        }
        if (current.type == EventType.MouseUp || current.type == EventType.DragExited) _pendingAssetDrag = null;
    }

    void HandleWindowDragDrop(TabData tab)
    {
        var ev = Event.current.type;
        if (ev == EventType.DragUpdated)
        {
            bool hasGo = DragAndDrop.objectReferences.Any(o => (o is GameObject go && IsObjectInContext(go)) || IsProjectFolder(o));
            DragAndDrop.visualMode = hasGo ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            Event.current.Use();
        }
        else if (ev == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            int added = AddDroppedFolders(tab);
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is GameObject go && IsObjectInContext(go))
                {
                    EntityId id = go.GetEntityId();
                    if (!tab.pinnedIDs.Contains(id))
                    {
                        AutoNameEmptyTab(tab, go);
                        tab.pinnedIDs.Add(id);
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
    //  History View
    // ══════════════════════════════════════════════

    void DrawHistoryView(TabData tab)
    {
        if (tab.history.Count == 0)
        {
            DrawHint("Wähle GameObjects in der Szene aus, um den Verlauf zu befüllen.");
            return;
        }
        for (int i = 0; i < tab.history.Count; i++)
            DrawHistoryEntry(tab, i);
    }

    void DrawHistoryEntry(TabData tab, int index)
    {
        var  entry    = tab.history[index];
        var  go       = entry.Resolve();
        bool exists   = go != null;
        if (exists) RegisterSelectionRow(go);
        bool selected = exists && _selectedIDs.Contains(go.GetEntityId());
        bool isFav    = exists && _favorites.Contains(go.GetEntityId());

        var  rect  = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        bool hover = rect.Contains(Event.current.mousePosition);

        EditorGUI.DrawRect(rect, selected ? ColSelected : hover ? ColHover
            : index % 2 == 0 ? ColBg : ColRowAlt);

        // Track hovered object for tooltip
        if (hover && exists && _showTooltips)
        {
            _hoveredGO = go;
            _hoverMousePos = Event.current.mousePosition;
        }

        var tabCol = GetTabColor(tab);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 4, 3, rect.height - 8), exists ? tabCol : Color.gray);

        var icon = exists
            ? EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image
            : EditorGUIUtility.IconContent("GameObject Icon").image;
        if (icon != null)
            GUI.DrawTexture(new Rect(rect.x + 8, rect.y + 10, 20, 20), icon, ScaleMode.ScaleToFit);

        var old = GUI.color;
        GUI.color = exists ? (selected ? Color.white : new Color(0.95f, 0.95f, 0.95f))
                           : new Color(0.60f, 0.60f, 0.60f);
        GUI.Label(new Rect(rect.x + 32, rect.y + 4,  rect.width - 65, 18), entry.name,      _stEntryName ?? EditorStyles.label);
        GUI.color = new Color(0.70f, 0.70f, 0.70f);
        GUI.Label(new Rect(rect.x + 32, rect.y + 22, rect.width - 65, 14), entry.scenePath, _stEntryPath ?? EditorStyles.miniLabel);
        GUI.color = old;

        if (!exists)
        {
            GUI.color = new Color(0.95f, 0.45f, 0.35f);
            GUI.Label(new Rect(rect.xMax - 60, rect.y + 14, 52, 14), "gelöscht", EditorStyles.miniLabel);
            GUI.color = old;
        }

        if (exists)
        {
            GUI.color = isFav ? ColFav : new Color(0.5f, 0.5f, 0.5f, hover ? 0.9f : 0f);
            if (GUI.Button(new Rect(rect.xMax - 18, rect.y + 12, 16, 16), "★", EditorStyles.miniLabel))
            {
                if (isFav) _favorites.Remove(go.GetEntityId());
                else       _favorites.Add(go.GetEntityId());
            }
            GUI.color = old;
        }

        if (hover)
        {
            GUI.color = new Color(0.9f, 0.4f, 0.4f);
            if (GUI.Button(new Rect(rect.xMax - 36, rect.y + 12, 16, 16), "×", EditorStyles.miniLabel))
            {
                tab.history.RemoveAt(index);
                StateChanged();
                GUI.color = old;
                return;
            }
            GUI.color = old;
        }

        if (exists) HandleEntryEvents(rect, go, tab);
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

    void HandleEntryEvents(Rect rect, GameObject go, TabData tab)
    {
        var e = Event.current;

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
            _dragSourceID = go.GetEntityId();
            _dragStartPos = e.mousePosition;

            SelectEntry(go, e.control || e.command, e.shift);

            if (e.clickCount == 2)
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
            menu.AddItem(new GUIContent("Auswählen"), false, () => Selection.activeGameObject = go);
            menu.AddItem(new GUIContent("In Hierarchy finden"), false, () => EditorGUIUtility.PingObject(go));
            menu.AddItem(new GUIContent("In Scene fokussieren"), false, () =>
            {
                Selection.activeGameObject = go;
                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();
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

        // Löschen
        menu.AddItem(new GUIContent(isMulti ? $"Löschen ({targets.Length} Objekte)" : "Löschen"), false, () =>
        {
            foreach (var t in targets)
            {
                if (t != null) Undo.DestroyObjectImmediate(t);
            }
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
        menu.AddItem(new GUIContent(isMulti ? $"Aus diesem Tab entfernen ({targets.Length} Objekte)" : "Aus diesem Tab entfernen (Unpin)"), false, () =>
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
        menu.AddItem(new GUIContent("Nach Updates suchen …"), false, HierarchyOrganizerUpdater.CheckForUpdates);
        menu.AddDisabledItem(new GUIContent("Version " + HierarchyOrganizerUpdater.Version));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Design & Helligkeit/Modern Dark (Empfohlen)"), _currentTheme == ThemeMode.ModernDark, () => { _currentTheme = ThemeMode.ModernDark; StateChanged(); });
        menu.AddItem(new GUIContent("Design & Helligkeit/Extra Hell"),              _currentTheme == ThemeMode.ExtraBright, () => { _currentTheme = ThemeMode.ExtraBright; StateChanged(); });
        menu.AddItem(new GUIContent("Design & Helligkeit/Unity Native Skin"),       _currentTheme == ThemeMode.UnityNative, () => { _currentTheme = ThemeMode.UnityNative; StateChanged(); });
        menu.AddItem(new GUIContent("Design & Helligkeit/High Contrast"),           _currentTheme == ThemeMode.HighContrast, () => { _currentTheme = ThemeMode.HighContrast; StateChanged(); });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Zeilenhöhe/Kompakt (22px)"),  Mathf.Approximately(_rowHeight, 22f), () => { _rowHeight = 22f; StateChanged(); });
        menu.AddItem(new GUIContent("Zeilenhöhe/Standard (26px)"), Mathf.Approximately(_rowHeight, 26f), () => { _rowHeight = 26f; StateChanged(); });
        menu.AddItem(new GUIContent("Zeilenhöhe/Groß (30px)"),     Mathf.Approximately(_rowHeight, 30f), () => { _rowHeight = 30f; StateChanged(); });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Schnellfilter-Leiste anzeigen"), _showQuickFilterBar, () => { _showQuickFilterBar = !_showQuickFilterBar; StateChanged(); });
        menu.AddItem(new GUIContent("Komponenten-Icons anzeigen"),   _showComponentIcons, () => { _showComponentIcons = !_showComponentIcons; StateChanged(); });
        menu.AddItem(new GUIContent("Hierarchy-Baumlinien anzeigen"), _showHierarchyLines, () => { _showHierarchyLines = !_showHierarchyLines; StateChanged(); });
        menu.AddItem(new GUIContent("Objekt-Info Tooltips anzeigen"), _showTooltips, () => { _showTooltips = !_showTooltips; StateChanged(); });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Autosave/Aus"),                   !_autosaveEnabled, () => { _autosaveEnabled = false; StateChanged(); });
        menu.AddItem(new GUIContent("Autosave/Alle 1 Minute"),         _autosaveEnabled && _autosaveIntervalMinutes == 1, () => { _autosaveEnabled = true; _autosaveIntervalMinutes = 1; StateChanged(); });
        menu.AddItem(new GUIContent("Autosave/Alle 5 Minuten"),        _autosaveEnabled && _autosaveIntervalMinutes == 5, () => { _autosaveEnabled = true; _autosaveIntervalMinutes = 5; StateChanged(); });
        menu.AddItem(new GUIContent("Autosave/Alle 15 Minuten"),       _autosaveEnabled && _autosaveIntervalMinutes == 15, () => { _autosaveEnabled = true; _autosaveIntervalMinutes = 15; StateChanged(); });
        menu.AddItem(new GUIContent("Autosave/Jetzt speichern"),       false, () => { DoAutosave(); });
        menu.AddItem(new GUIContent("Autosave/Backup-Ordner öffnen"),  false, () =>
        {
            string folder = GetAutosaveFolder();
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        });

        menu.AddSeparator("");
        if (EditorPrefs.HasKey(PrefKey) || EditorPrefs.HasKey("HierarchyOrganizer_v3"))
            menu.AddItem(new GUIContent("Szenendaten/Alte globale Tabs übernehmen..."), false, ImportLegacyState);
        menu.AddDisabledItem(new GUIContent("Szenendaten/Werden mit der aktiven Szene gespeichert"));

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

    void AddTab()
    {
        int newIdx = _tabs.Count % PresetColors.Length;
        string uniqueName = GetUniqueTabName("Tab " + (_tabs.Count + 1));
        _tabs.Add(new TabData
        {
            name       = uniqueName,
            viewMode   = 1, // Neuer Tab startet immer als leerer "Tab-Inhalt" (Pinned)
            iconEmoji  = "📁",
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
            _tabs[0].name = "Main";
            _tabs[0].pinnedIDs.Clear();
            _tabs[0].history.Clear();
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
        EnsureSceneContext();
        if (!_stateLoaded || _suspendPersistence || EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
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
        if (!_stateLoaded || _suspendPersistence || EditorApplication.isPlaying || !_dataScene.IsValid() || !_dataScene.isLoaded) return;
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
        public bool showTooltips = true;
        public int autosaveIntervalMinutes = 5;
        public bool autosaveEnabled = true;
        public bool autoNameTabs;
        public bool recordEnabled = true;
        public int maxHistory = 50;
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
        _tabs = new List<TabData> { new TabData { name = "Main", viewMode = 0, iconEmoji = "📁" } };
        _selectedTab = 0;
        _favorites.Clear();
        _customObjectColors.Clear();
        _expanded.Clear();
        _currentTheme = ThemeMode.ModernDark;
        _rowHeight = 26f;
        _showComponentIcons = _showHierarchyLines = _showQuickFilterBar = _showTooltips = true;
        _autosaveIntervalMinutes = 5;
        _autosaveEnabled = true;
        _autoNameTabs = false;
        _recordEnabled = true;
        _maxHistory = 50;
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
    }

    static HashSet<EntityId> ReferencedIds(SaveData data)
    {
        var ids = new HashSet<EntityId>();
        foreach (var tab in data.tabs ?? new List<TabData>())
        {
            if (tab == null) continue;
            if (tab.pinnedIDs != null) ids.UnionWith(tab.pinnedIDs);
            if (tab.history != null)
                foreach (var entry in tab.history)
                    if (entry != null) ids.Add(entry.entityId);
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
            tab.pinnedIDs = RemapIds(tab.pinnedIDs, map);
            tab.history = (tab.history ?? new List<HistoryEntry>())
                .Where(e => e != null && map.ContainsKey(e.entityId)).ToList();
            foreach (var entry in tab.history) entry.entityId = map[entry.entityId];
            tab.renaming = false;
            tab.renameBuffer = tab.name;
            tab.notes = tab.notes ?? "";
            tab.searchQuery = tab.searchQuery ?? "";
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
            showTooltips = _showTooltips,
            autosaveIntervalMinutes = _autosaveIntervalMinutes,
            autosaveEnabled = _autosaveEnabled,
            autoNameTabs = _autoNameTabs,
            recordEnabled = _recordEnabled,
            maxHistory = _maxHistory,
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
        _selectedTab = Mathf.Clamp(data.selectedTab, 0, _tabs.Count - 1);
        _favorites = new HashSet<EntityId>(data.favorites);
        _expanded = new HashSet<EntityId>(data.expanded);
        foreach (var color in data.customColors) _customObjectColors[color.entityId] = color.hexColor;
        _currentTheme = (ThemeMode)Mathf.Clamp(data.currentTheme, 0, 3);
        _rowHeight = data.rowHeight > 10 ? data.rowHeight : 26f;
        _showComponentIcons = data.showComponentIcons;
        _showHierarchyLines = data.showHierarchyLines;
        _showQuickFilterBar = data.showQuickFilterBar;
        _showTooltips = data.showTooltips;
        _autosaveIntervalMinutes = Mathf.Max(1, data.autosaveIntervalMinutes);
        _autosaveEnabled = data.schemaVersion < 5 || data.autosaveEnabled;
        _autoNameTabs = data.autoNameTabs;
        _recordEnabled = data.schemaVersion < 5 || data.recordEnabled;
        _maxHistory = data.schemaVersion < 5 ? 50 : Mathf.Max(1, data.maxHistory);
        _globalSearch = data.globalSearch;
        _globalQuery = data.globalQuery ?? "";
        if (data.schemaVersion >= 5 && data.colorFilterTab >= 0 && data.colorFilterTab < _tabs.Count)
            _sceneColorFilterTab = _tabs[data.colorFilterTab];
        RebuildTabColorGroups();
    }

    void SaveState(bool forceCreate = false)
    {
        if (!_stateLoaded || _writingState || _suspendPersistence || EditorApplication.isPlaying
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
            _stNotes != null && _stFilterChip != null && _stFilterChipActive != null)
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
#endif
