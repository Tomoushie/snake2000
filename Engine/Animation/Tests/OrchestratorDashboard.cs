// /Engine/Animation/Test/OrchestratorDashboard.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Engine.Animation.Test
{
    #region Enums & Structs (réutilisés ou nouveaux pour le Dashboard)
    // Réutilisation de certains enums/types de l'orchestrator
    using Engine.Animation;

    // --- THEMES & DISPLAY ---
    public enum DashboardTheme
    {
        Light,
        Dark,
        HighContrast
    }

    public enum DashboardDisplayMode
    {
        Standard,
        Compact,
        Minimal,
        Fullscreen
    }

    public enum DashboardExportFormat
    {
        JSON,
        CSV,
        PNG // Pour une capture d'écran du dashboard
    }

    // --- WIDGETS ---
    public enum WidgetType
    {
        HealthIndicator,
        MetricsChart,
        EventTimeline,
        LogViewer,
        AlertList,
        PerformanceCounter,
        TextBlock,
        ProgressBar,
        Gauge,
        Image
    }

    public class DashboardWidget
    {
        public string Id { get; set; }
        public WidgetType Type { get; set; }
        public string Title { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVisible { get; set; } = true;
        public object Data { get; set; }
        public DateTime LastUpdated { get; set; }
        public IDataSource DataSource { get; set; } // [NOUVEAU]
    }

    public class WidgetConfiguration
    {
        public string WidgetId { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }

    public class Annotation
    {
        public string Id { get; set; }
        public string WidgetId { get; set; }
        public string Text { get; set; }
        public string Author { get; set; }
        public DateTime Timestamp { get; set; }
        public Point Position { get; set; }
    }

    public class Bookmark
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime Timestamp { get; set; }
        public DashboardState State { get; set; } // [CHANGEMENT] Utilise la nouvelle classe DashboardState
    }

    public class DashboardAlert
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public AlertSeverity Severity { get; set; }
        public AlertPriority Priority { get; set; }
        public DateTime Timestamp { get; set; }
        public string Source { get; set; }
    }

    // AlertSeverity et AlertPriority sont declares dans Tests/CommonTypes.cs.

    #endregion

    #region [NOUVEAU] Gestion des Widgets
    /// <summary>
    /// Gestionnaire dédié à la manipulation des widgets du dashboard.
    /// </summary>
    public sealed class WidgetManager
    {
        private readonly List<DashboardWidget> _widgets = new List<DashboardWidget>();
        private readonly Dictionary<string, WidgetConfiguration> _configs = new Dictionary<string, WidgetConfiguration>();

        public IReadOnlyList<DashboardWidget> Widgets => _widgets.AsReadOnly();

        public void Add(DashboardWidget widget)
        {
            if (widget == null || string.IsNullOrEmpty(widget.Id)) return;
            _widgets.Add(widget);
        }

        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _widgets.RemoveAll(w => w.Id == id);
            _configs.Remove(id);
        }

        public void Update(string id, object data)
        {
            if (string.IsNullOrEmpty(id)) return;
            var widget = _widgets.FirstOrDefault(w => w.Id == id);
            if (widget != null)
            {
                widget.Data = data;
                widget.LastUpdated = DateTime.UtcNow;
            }
        }

        public void Configure(string id, WidgetConfiguration config)
        {
            if (string.IsNullOrEmpty(id) || config == null) return;
            _configs[id] = config;
        }

        public void SetVisibility(string id, bool visible)
        {
            if (string.IsNullOrEmpty(id)) return;
            var widget = _widgets.FirstOrDefault(w => w.Id == id);
            if (widget != null)
            {
                widget.IsVisible = visible;
            }
        }

        public void RenderAll()
        {
            foreach (var widget in _widgets.Where(w => w.IsVisible))
            {
                // Simulation d'un rendu. Dans une implémentation réelle, cela pourrait envoyer à une UI.
                Console.WriteLine($"[WidgetManager] Rendering widget: {widget.Title} ({widget.Type})");
            }
        }

        public void Clear()
        {
            _widgets.Clear();
            _configs.Clear();
        }
    }
    #endregion

    #region [NOUVEAU] Source de Données Abstraite
    /// <summary>
    /// Interface pour une source de données connectable à un widget.
    /// </summary>
    public interface IDataSource
    {
        object FetchData();
        bool IsValid();
    }

    /// <summary>
    /// Source de données pour les métriques de l'orchestrateur.
    /// </summary>
    public class OrchestratorMetricsSource : IDataSource
    {
        private readonly AnimationEngineStubOrchestrator _orchestrator;
        private readonly Func<Dictionary<OrchestratorMetricType, float>, object> _transform;

        public OrchestratorMetricsSource(AnimationEngineStubOrchestrator orchestrator, Func<Dictionary<OrchestratorMetricType, float>, object> transform = null)
        {
            _orchestrator = orchestrator;
            _transform = transform ?? (metrics => metrics);
        }

        public object FetchData()
        {
            var metrics = _orchestrator.GetMetrics();
            return _transform(metrics);
        }

        public bool IsValid() => _orchestrator != null;
    }

    /// <summary>
    /// Source de données pour un fichier externe (JSON, CSV...).
    /// </summary>
    public class FileDataSource : IDataSource
    {
        private readonly string _filePath;

        public FileDataSource(string filePath)
        {
            _filePath = filePath;
        }

        public object FetchData()
        {
            if (!File.Exists(_filePath)) return null;
            var content = File.ReadAllText(_filePath);
            try
            {
                return JsonSerializer.Deserialize<object>(content);
            }
            catch
            {
                return content; // Retourne le texte brut en cas d'échec de parsing
            }
        }

        public bool IsValid() => !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath);
    }

    /// <summary>
    /// Source de données factice pour la simulation.
    /// </summary>
    public class SimulatedDataSource : IDataSource
    {
        private readonly Func<object> _simulate;

        public SimulatedDataSource(Func<object> simulate)
        {
            _simulate = simulate;
        }

        public object FetchData() => _simulate();

        public bool IsValid() => _simulate != null;
    }
    #endregion

    #region [NOUVEAU] État du Dashboard Sérialisable
    /// <summary>
    /// Représente l'état complet du dashboard, sérialisable pour les bookmarks ou la persistance.
    /// </summary>
    public class DashboardState
    {
        public List<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();
        public DashboardTheme Theme { get; set; } = DashboardTheme.Dark;
        public DashboardDisplayMode DisplayMode { get; set; } = DashboardDisplayMode.Standard;
        public Dictionary<string, object> Filters { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> UserPreferences { get; set; } = new Dictionary<string, object>();
    }
    #endregion

    #region [NOUVEAU] Moteur de Layout
    /// <summary>
    /// Gestionnaire du placement automatique et du layout des widgets.
    /// </summary>
    public class DashboardLayoutEngine
    {
        private readonly List<DashboardWidget> _widgets;
        private int _gridSize = 10;
        private int _maxWidth = 1000;
        private int _maxHeight = 700;

        public DashboardLayoutEngine(List<DashboardWidget> widgets)
        {
            _widgets = widgets;
        }

        public void SetGridSize(int size) => _gridSize = size;
        public void SetMaxBounds(int width, int height) { _maxWidth = width; _maxHeight = height; }

        /// <summary>
        /// Dispose les widgets de manière compacte en grille.
        /// </summary>
        public void ArrangeInGrid()
        {
            int x = 0, y = 0;
            int rowHeight = 0;
            const int margin = 10;

            foreach (var widget in _widgets.OrderBy(w => w.Title))
            {
                if (x + widget.Width > _maxWidth)
                {
                    x = 0;
                    y += rowHeight + margin;
                    rowHeight = 0;
                }

                widget.X = x;
                widget.Y = y;

                x += widget.Width + margin;
                if (widget.Height > rowHeight) rowHeight = widget.Height;
            }
        }

        /// <summary>
        /// Dispose les widgets de manière empilée verticalement.
        /// </summary>
        public void StackVertically()
        {
            int y = 0;
            const int margin = 10;

            foreach (var widget in _widgets.OrderBy(w => w.Title))
            {
                widget.X = 0;
                widget.Y = y;
                y += widget.Height + margin;
            }
        }

        /// <summary>
        /// Vérifie et corrige les collisions simples entre widgets.
        /// </summary>
        public void ResolveCollisions()
        {
            for (int i = 0; i < _widgets.Count; i++)
            {
                for (int j = i + 1; j < _widgets.Count; j++)
                {
                    var w1 = _widgets[i];
                    var w2 = _widgets[j];

                    if (DoWidgetsOverlap(w1, w2))
                    {
                        // Simple correction : déplacer w2 en dessous de w1
                        w2.Y = w1.Y + w1.Height + 10; // Marge de 10
                    }
                }
            }
        }

        private bool DoWidgetsOverlap(DashboardWidget w1, DashboardWidget w2)
        {
            return w1.X < w2.X + w2.Width &&
                   w1.X + w1.Width > w2.X &&
                   w1.Y < w2.Y + w2.Height &&
                   w1.Y + w1.Height > w2.Y;
        }
    }
    #endregion

    /// <summary>
    /// Dashboard centralisé pour l'AnimationEngineStubOrchestrator.
    /// Permet de visualiser l'état, les métriques, les logs, les alertes, etc.
    /// de l'orchestrateur et de ses composants via une interface intuitive.
    /// </summary>
    public class OrchestratorDashboard
    {
        #region Fields
        private readonly AnimationEngineStubOrchestrator _orchestrator;
        private readonly object _sync = new object();

        // [AJOUT] Configuration du dashboard
        private DashboardTheme _currentTheme = DashboardTheme.Dark;
        private DashboardDisplayMode _displayMode = DashboardDisplayMode.Standard;
        private bool _autoRefreshEnabled = true;
        private TimeSpan _refreshInterval = TimeSpan.FromSeconds(5);
        private readonly Dictionary<string, object> _filters = new Dictionary<string, object>();
        private readonly Dictionary<string, object> _userPreferences = new Dictionary<string, object>();

        // [CHANGEMENT] Utilisation du WidgetManager
        private readonly WidgetManager _widgetManager = new WidgetManager();

        // Anciens champs déplacés ou rendus obsolètes par le WidgetManager
        // private readonly List<DashboardWidget> _widgets = new List<DashboardWidget>();
        // private readonly Dictionary<string, WidgetConfiguration> _widgetConfigs = new Dictionary<string, WidgetConfiguration>();

        // [AJOUT] Alertes & Règles
        private readonly Dictionary<string, string> _alertRules = new Dictionary<string, string>(); // nom -> expression
        private readonly List<DashboardAlert> _dashboardAlerts = new List<DashboardAlert>();

        // [AJOUT] Annotations & Bookmarks
        private readonly List<Annotation> _annotations = new List<Annotation>();
        private readonly List<Bookmark> _bookmarks = new List<Bookmark>();

        // [AJOUT] Plugins
        private readonly List<IDashboardPlugin> _plugins = new List<IDashboardPlugin>();
        #endregion

        #region Properties
        // [CHANGEMENT] Propriétés basées sur le WidgetManager
        public IReadOnlyList<DashboardWidget> Widgets => _widgetManager.Widgets;
        public List<Annotation> Annotations => new List<Annotation>(_annotations);
        public List<Bookmark> Bookmarks => new List<Bookmark>(_bookmarks);
        public List<DashboardAlert> Alerts => new List<DashboardAlert>(_dashboardAlerts);
        public DashboardTheme CurrentTheme => _currentTheme;
        public DashboardDisplayMode DisplayMode => _displayMode;
        public bool IsAutoRefreshEnabled => _autoRefreshEnabled;
        public TimeSpan RefreshInterval => _refreshInterval;
        #endregion

        #region Constructor
        public OrchestratorDashboard(AnimationEngineStubOrchestrator orchestrator)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            InitializeDefaultWidgets();
        }
        #endregion

        #region [AJOUT] Initialisation & Setup
        private void InitializeDefaultWidgets()
        {
            // Crée quelques widgets par défaut pour le dashboard
            _widgetManager.Add(new DashboardWidget
            {
                Id = "health_gauge",
                Type = WidgetType.HealthIndicator,
                Title = "Overall Health",
                X = 0, Y = 0, Width = 200, Height = 100,
                DataSource = new OrchestratorMetricsSource(_orchestrator, m => m.GetValueOrDefault(OrchestratorMetricType.HealthPercentage, 100f))
            });

            _widgetManager.Add(new DashboardWidget
            {
                Id = "metrics_chart",
                Type = WidgetType.MetricsChart,
                Title = "Performance Metrics",
                X = 210, Y = 0, Width = 200, Height = 150,
                DataSource = new OrchestratorMetricsSource(_orchestrator)
            });

            _widgetManager.Add(new DashboardWidget
            {
                Id = "events_timeline",
                Type = WidgetType.EventTimeline,
                Title = "Event History",
                X = 0, Y = 270, Width = 610, Height = 150
                // Pas de source de données par défaut pour celui-ci
            });
        }
        #endregion

        #region [AJOUT] A. Visualisation & UI/UX Moderne
        public OrchestratorDashboard SetDisplayMode(DashboardDisplayMode mode)
        {
            lock (_sync)
            {
                _displayMode = mode;
                // Appliquer les changements d'UI ici (masquer/changer éléments)
                Console.WriteLine($"[Dashboard] Display mode set to {mode}");
            }
            return this;
        }

        public OrchestratorDashboard SetTheme(DashboardTheme theme)
        {
            lock (_sync)
            {
                _currentTheme = theme;
                // Appliquer le thème ici (changer couleurs, polices, etc.)
                Console.WriteLine($"[Dashboard] Theme set to {theme}");
            }
            return this;
        }

        // [CHANGEMENT] Méthodes de gestion des widgets via le WidgetManager
        public OrchestratorDashboard AddWidget(DashboardWidget widget)
        {
            lock (_sync)
            {
                _widgetManager.Add(widget);
                Console.WriteLine($"[Dashboard] Added widget: {widget.Title} ({widget.Type})");
            }
            return this;
        }

        public OrchestratorDashboard RemoveWidget(string widgetId)
        {
            lock (_sync)
            {
                _widgetManager.Remove(widgetId);
                Console.WriteLine($"[Dashboard] Removed widget: {widgetId}");
            }
            return this;
        }

        public OrchestratorDashboard ConfigureWidget(string widgetId, WidgetConfiguration config)
        {
            lock (_sync)
            {
                _widgetManager.Configure(widgetId, config);
                Console.WriteLine($"[Dashboard] Configured widget: {widgetId}");
            }
            return this;
        }

        public OrchestratorDashboard ToggleWidgetVisibility(string widgetId, bool visible)
        {
            lock (_sync)
            {
                _widgetManager.SetVisibility(widgetId, visible);
            }
            return this;
        }

        // [NOUVEAU] Méthodes pour le layout
        public OrchestratorDashboard ArrangeWidgetsInGrid()
        {
            lock (_sync)
            {
                var layoutEngine = new DashboardLayoutEngine(_widgetManager.Widgets.ToList());
                layoutEngine.ArrangeInGrid();
                Console.WriteLine("[Dashboard] Widgets arranged in grid.");
            }
            return this;
        }

        public OrchestratorDashboard StackWidgetsVertically()
        {
            lock (_sync)
            {
                var layoutEngine = new DashboardLayoutEngine(_widgetManager.Widgets.ToList());
                layoutEngine.StackVertically();
                Console.WriteLine("[Dashboard] Widgets stacked vertically.");
            }
            return this;
        }

        public OrchestratorDashboard ResolveWidgetCollisions()
        {
            lock (_sync)
            {
                var layoutEngine = new DashboardLayoutEngine(_widgetManager.Widgets.ToList());
                layoutEngine.ResolveCollisions();
                Console.WriteLine("[Dashboard] Widget collisions resolved.");
            }
            return this;
        }
        #endregion

        #region [AJOUT] B. Métriques & Monitoring en Temps Réel
        public OrchestratorDashboard RefreshMetrics()
        {
            // Récupère les métriques de l'orchestrateur et les propage aux widgets concernés
            var metrics = _orchestrator.GetMetrics();
            var healthStatus = _orchestrator.GetOrchestratorHealthStatus();
            var recentLogs = _orchestrator.GetRecentLogs(10);
            var recentEvents = _orchestrator.GetRecentEvents(10);
            var incidentLog = _orchestrator.GetIncidentLog();
            var trendReports = _orchestrator.GetStoredTrendReports();

            // Mettre à jour les widgets avec les nouvelles données
            UpdateWidgetData("health_gauge", healthStatus);
            UpdateWidgetData("metrics_chart", metrics);

            // [NOUVEAU] Mise à jour des widgets connectés à une source
            foreach (var widget in _widgetManager.Widgets)
            {
                if (widget.DataSource != null && widget.DataSource.IsValid())
                {
                    var data = widget.DataSource.FetchData();
                    UpdateWidgetData(widget.Id, data);
                }
            }

            // Vérifier les règles d'alerte
            CheckAlertRules(metrics);

            return this;
        }

        private void UpdateWidgetData(string widgetId, object data)
        {
            _widgetManager.Update(widgetId, data);
        }

        public OrchestratorDashboard StartAutoRefresh(TimeSpan interval)
        {
            lock (_sync)
            {
                _refreshInterval = interval;
                _autoRefreshEnabled = true;
                // Démarrer une tâche de fond pour appeler RefreshMetrics à intervalle régulier
                Task.Run(async () =>
                {
                    while (_autoRefreshEnabled)
                    {
                        await Task.Delay(_refreshInterval);
                        if (_autoRefreshEnabled) // Vérifier à nouveau avant d'actualiser
                        {
                            RefreshMetrics();
                        }
                    }
                });
                Console.WriteLine($"[Dashboard] Auto-refresh started with interval {_refreshInterval}.");
            }
            return this;
        }

        public OrchestratorDashboard StopAutoRefresh()
        {
            lock (_sync)
            {
                _autoRefreshEnabled = false;
                Console.WriteLine("[Dashboard] Auto-refresh stopped.");
            }
            return this;
        }
        #endregion

        #region [AJOUT] D. Alertes & Notifications Intelligentes
        public OrchestratorDashboard AddAlertRule(string name, string expression)
        {
            lock (_sync)
            {
                _alertRules[name] = expression;
                Console.WriteLine($"[Dashboard] Added alert rule: {name}");
            }
            return this;
        }

        private void CheckAlertRules(Dictionary<OrchestratorMetricType, float> metrics)
        {
            foreach (var rule in _alertRules)
            {
                if (EvaluateExpression(rule.Value, metrics))
                {
                    var alert = new DashboardAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = $"Rule Triggered: {rule.Key}",
                        Description = $"Expression '{rule.Value}' evaluated to true.",
                        Severity = AlertSeverity.Warning, // Peut-être dynamique
                        Priority = AlertPriority.Normal, // Peut-être dynamique
                        Timestamp = DateTime.UtcNow,
                        Source = "RuleEngine"
                    };
                    _dashboardAlerts.Add(alert);
                    Console.WriteLine($"[Dashboard] Alert triggered: {alert.Title}");
                }
            }
        }

        private bool EvaluateExpression(string expression, Dictionary<OrchestratorMetricType, float> metrics)
        {
            // Exemple simple : "ActiveStubs > 100"
            var parts = expression.Split(' ');
            if (parts.Length < 3) return false;

            var metricName = parts[0];
            var op = parts[1];
            var thresholdStr = parts[2];

            if (Enum.TryParse<OrchestratorMetricType>(metricName, out var metricType))
            {
                var currentValue = metrics.GetValueOrDefault(metricType, 0);
                if (float.TryParse(thresholdStr, out var threshold))
                {
                    switch (op)
                    {
                        case ">": return currentValue > threshold;
                        case "<": return currentValue < threshold;
                        case ">=": return currentValue >= threshold;
                        case "<=": return currentValue <= threshold;
                        case "==": return Math.Abs(currentValue - threshold) < float.Epsilon;
                        case "!=": return Math.Abs(currentValue - threshold) >= float.Epsilon;
                    }
                }
            }
            // Ajouter d'autres règles ici...
            return false;
        }
        #endregion

        #region [AJOUT] E. Interaction & Contrôle
        public OrchestratorDashboard AddAnnotation(string widgetId, string text, string author)
        {
            lock (_sync)
            {
                var annotation = new Annotation
                {
                    Id = Guid.NewGuid().ToString(),
                    WidgetId = widgetId,
                    Text = text,
                    Author = author,
                    Timestamp = DateTime.UtcNow,
                    Position = new Point(0, 0) // Doit être fourni ou déduit de l'interaction UI
                };
                _annotations.Add(annotation);
                Console.WriteLine($"[Dashboard] Added annotation on widget {widgetId}: {text}");
            }
            return this;
        }

        public OrchestratorDashboard AddBookmark(string name)
        {
            lock (_sync)
            {
                var bookmark = new Bookmark
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Timestamp = DateTime.UtcNow,
                    State = CaptureDashboardState() // [CHANGEMENT] Sauvegarde via DashboardState
                };
                _bookmarks.Add(bookmark);
                Console.WriteLine($"[Dashboard] Added bookmark: {name}");
            }
            return this;
        }

        // [CHANGEMENT] Capture d'état via la nouvelle classe DashboardState
        private DashboardState CaptureDashboardState()
        {
            return new DashboardState
            {
                Widgets = _widgetManager.Widgets.ToList(),
                Theme = _currentTheme,
                DisplayMode = _displayMode,
                Filters = new Dictionary<string, object>(_filters),
                UserPreferences = new Dictionary<string, object>(_userPreferences)
            };
        }

        public OrchestratorDashboard RestoreFromBookmark(string bookmarkId)
        {
            var bookmark = _bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark != null)
            {
                ApplyDashboardState(bookmark.State); // [CHANGEMENT] Restauration via DashboardState
                Console.WriteLine($"[Dashboard] Restored from bookmark: {bookmark.Name}");
            }
            else
            {
                Console.WriteLine($"[Dashboard] Bookmark {bookmarkId} not found.");
            }
            return this;
        }

        // [CHANGEMENT] Application d'état via la nouvelle classe DashboardState
        private void ApplyDashboardState(DashboardState state)
        {
            if (state == null) return;

            _widgetManager.Clear();
            foreach (var widget in state.Widgets)
            {
                _widgetManager.Add(widget);
            }

            if (Enum.TryParse<DashboardTheme>(state.Theme.ToString(), out var theme))
            {
                _currentTheme = theme;
            }
            if (Enum.TryParse<DashboardDisplayMode>(state.DisplayMode.ToString(), out var mode))
            {
                _displayMode = mode;
            }
            _filters.Clear();
            foreach (var kvp in state.Filters)
            {
                _filters[kvp.Key] = kvp.Value;
            }
            _userPreferences.Clear();
            foreach (var kvp in state.UserPreferences)
            {
                _userPreferences[kvp.Key] = kvp.Value;
            }
            // ... autres états
        }
        #endregion

        #region [AJOUT] F. Export & Partage
        public OrchestratorDashboard Export(DashboardExportFormat format, string path)
        {
            lock (_sync)
            {
                // Récupérer les données à exporter
                var exportData = new
                {
                    Widgets = _widgetManager.Widgets.ToList(),
                    Theme = _currentTheme,
                    DisplayMode = _displayMode,
                    Filters = _filters,
                    UserPreferences = _userPreferences,
                    RecentLogs = _orchestrator.GetRecentLogs(50),
                    RecentEvents = _orchestrator.GetRecentEvents(50),
                    Alerts = _dashboardAlerts,
                    Incidents = _orchestrator.GetIncidentLog()
                };
                string content;
                switch (format)
                {
                    case DashboardExportFormat.JSON:
                        content = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                        break;
                    case DashboardExportFormat.CSV:
                        // Convertir les données en CSV (simplifié)
                        content = "CSV Export Not Implemented";
                        break;
                    case DashboardExportFormat.PNG:
                        // Capturer une image du dashboard (conceptuel)
                        content = "PNG Export Not Implemented";
                        break;
                    default:
                        content = "";
                        break;
                }

                File.WriteAllText(path, content);
                Console.WriteLine($"[Dashboard] Data exported to {path} in {format} format.");
            }
            return this;
        }
        #endregion

        #region [AJOUT] G. Intelligence Artificielle & Insights (Conceptuel)
        public OrchestratorDashboard GenerateInsights()
        {
            var metrics = _orchestrator.GetMetrics();
            var incidentLog = _orchestrator.GetIncidentLog();
            string insight = "System status appears normal.";

            if (metrics.GetValueOrDefault(OrchestratorMetricType.AverageLatencyMs, 0) > 100)
            {
                insight = "Average latency is high. Consider scaling down or optimizing.";
            }
            if (incidentLog.Any(i => i.timestamp > DateTime.UtcNow.AddMinutes(-5)))
            {
                insight = "Recent incidents were logged. Please review the incident log.";
            }
            Console.WriteLine($"[Dashboard] AI Insight: {insight}");
            return this;
        }
        #endregion

        #region [AJOUT] K. Intégrations & Extensions
        public OrchestratorDashboard RegisterPlugin(IDashboardPlugin plugin)
        {
            lock (_sync)
            {
                _plugins.Add(plugin);
                plugin.Initialize(this);

                // Ajouter les widgets du plugin
                foreach (var widget in plugin.GetWidgets())
                {
                    AddWidget(widget);
                }
                Console.WriteLine($"[Dashboard] Registered plugin: {plugin.Name} (v{plugin.Version})");
            }
            return this;
        }

        public OrchestratorDashboard UpdatePlugins(float deltaTime)
        {
            lock (_sync)
            {
                foreach (var plugin in _plugins)
                {
                    plugin.Update(deltaTime);
                }
            }
            return this;
        }
        #endregion

        #region [AJOUT] L. Méthodes utilitaires pour la sérialisation et le layout
        public DashboardState GetCurrentState() => CaptureDashboardState();
        public void LoadState(DashboardState state) => ApplyDashboardState(state);
        #endregion
    }

    // [AJOUT] Structure pour représenter un plugin de dashboard
    public interface IDashboardPlugin
    {
        string Name { get; }
        string Version { get; }
        void Initialize(OrchestratorDashboard dashboard);
        void Update(float deltaTime);
        void Render(); // Méthode conceptuelle pour le rendu
        void Shutdown();
        List<DashboardWidget> GetWidgets(); // Widgets fournis par le plugin
    }
}