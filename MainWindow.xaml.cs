using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.IO;
using System.Text.Json;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using Button = System.Windows.Controls.Button;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Control = System.Windows.Controls.Control;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using WaterCoolerApp.Models;
using WaterCoolerApp.Services;

namespace WaterCoolerApp;

public partial class MainWindow : Window
{
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan HistoryPersistenceInterval = TimeSpan.FromSeconds(30);
    private const int WmActivateExistingInstance = 0x8001;
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _storageCycleTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Queue<SensorSnapshot> _history = new();
    private readonly Queue<SensorSnapshot> _utilizationHistory = new();
    private readonly Queue<SensorSnapshot> _storedHistory = new();
    private readonly TelemetryHistoryStore _historyStore = new();
    private readonly TelemetryHistoryWriter _historyWriter;
    private DateTime _lastHistorySaveAt = DateTime.MinValue;
    private DateTime _lastPersistedSnapshotAt = DateTime.MinValue;
    private TimeSpan _historyRetention = TelemetryHistoryStore.DefaultRetention;
    private HardwareMonitoringService? _monitor;
    private readonly DisplayTransportService _displayTransport = new();
    private bool _reading;
    private bool _closing;
    private string _language = "pt";
    private SensorSnapshot? _lastSnapshot;
    private int _storageDriveIndex = -1;
    private string? _selectedStorageDriveName;
    private readonly string _preferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HWMetrics", "preferences.json");
    private readonly string _legacyPreferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AquaCore", "preferences.json");
    private bool _loadingPreferences;
    private NotifyIcon? _trayIcon;
    private System.Drawing.Icon? _trayApplicationIcon;
    private bool _allowExit;
    private bool _closePromptAnswered;
    private bool _startHidden;

    private sealed class AppPreferences
    {
        public int ThemeIndex { get; set; }
        public int PollIntervalIndex { get; set; } = 1;
        public int LanguageIndex { get; set; } = 1;
        public int CoolerModelIndex { get; set; }
        public int HistoryRetentionIndex { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ClosePromptAnswered { get; set; }
    }

    private static readonly Dictionary<string, string> Portuguese = new(StringComparer.Ordinal)
    {
        ["HW Metrics — Hardware Studio"] = "HW Metrics — Central de Hardware", ["HARDWARE STUDIO"] = "CENTRAL DE HARDWARE",
        ["Minimize"] = "Minimizar", ["Maximize / Restore"] = "Maximizar / Restaurar",
        ["Close settings"] = "Fechar configurações",
        ["Changes the preview appearance only; it does not control the hardware."] = "Altera apenas a aparência da prévia; não controla o hardware.",
        ["Detecting processor..."] = "Detectando processador...", ["Detecting graphics..."] = "Detectando placa de vídeo...",
        ["Detecting drive..."] = "Detectando unidade...", ["Waiting for memory data"] = "Aguardando dados da memória",
        ["Utilization --%"] = "Utilização --%",
        ["Open HW Metrics"] = "Abrir HW Metrics", ["Exit"] = "Sair",
        ["WORKSPACE"] = "ESPAÇO DE TRABALHO", ["Overview"] = "Visão geral", ["Cooling"] = "Refrigeração",
        ["Hardware"] = "Hardware", ["Settings"] = "Configurações", ["DISPLAY DEVICE"] = "DISPOSITIVO DE DISPLAY",
        ["Waiting for device"] = "Aguardando dispositivo", ["USB / HID connection not enabled"] = "Conexão USB / HID desativada",
        ["HID display connected"] = "Display HID conectado", ["Telemetry sent to physical display"] = "Telemetria enviada ao display físico",
        ["HID display not found"] = "Display HID não encontrado", ["CPU temperature unavailable · not sent"] = "Temperatura da CPU indisponível · não enviado",
        ["HID report incompatible"] = "Relatório HID incompatível", ["Could not open HID display"] = "Não foi possível abrir o display HID",
        ["HID write failed"] = "Falha ao transmitir por HID", ["Preview only · HID not connected"] = "Somente prévia · HID não conectado",
        ["HID telemetry active · fan/pump control disabled"] = "Telemetria HID ativa · controle de bomba/ventoinha desativado",
        ["APPEARANCE"] = "APARÊNCIA", ["Interface theme"] = "Tema da interface", ["Dark"] = "Escuro", ["Light"] = "Claro",
        ["Refresh rate"] = "Intervalo de atualização", ["Language"] = "Idioma", ["SYSTEM SETTINGS"] = "CONFIGURAÇÕES DO SISTEMA",
        ["Minimize to tray on close"] = "Minimizar para a bandeja ao fechar", ["Keep HW Metrics running in the notification area"] = "Manter o HW Metrics em execução na área de notificação",
        ["Start with Windows"] = "Iniciar com o Windows", ["Launch automatically in the tray when you sign in"] = "Abrir automaticamente na bandeja ao entrar no Windows", ["1 sec"] = "1 s", ["2 sec"] = "2 s", ["5 sec"] = "5 s",
        ["Preview build · .NET 10"] = "Versão de prévia · .NET 10", ["System overview"] = "Visão geral do sistema",
        ["Your hardware, at a glance."] = "Seu hardware em um só lugar.",
        ["PROCESSOR"] = "PROCESSADOR", ["CPU"] = "CPU", ["GPU"] = "GPU",
        ["GRAPHICS"] = "GRÁFICOS", ["MEMORY"] = "MEMÓRIA", ["Memory model unavailable"] = "Modelo de memória indisponível", ["Capacity unavailable"] = "Capacidade indisponível",
        ["STORAGE"] = "ARMAZENAMENTO", ["Waiting for disk data"] = "Aguardando dados do disco",
        ["Thermal performance"] = "Desempenho térmico", ["Utilization performance"] = "Desempenho de utilização", ["Live CPU, GPU and memory history · rolling 30 minutes"] = "Histórico ao vivo de CPU, GPU e memória · últimos 30 minutos", ["Live temperature history · rolling 30 minutes"] = "Histórico de temperatura ao vivo · últimos 30 minutos",
        ["Rolling 30 minutes · data since launch"] = "Últimos 30 minutos · dados desde a inicialização", ["100%"] = "100%", ["75%"] = "75%", ["50%"] = "50%", ["25%"] = "25%",
        ["Waiting for sensor data"] = "Aguardando dados dos sensores", ["System utilization"] = "Utilização do sistema",
        ["Current workload by component"] = "Carga atual por componente", ["CPU utilization"] = "Utilização da CPU",
        ["GPU utilization"] = "Utilização da GPU", ["Memory in use"] = "Memória em uso",
        ["Water cooler"] = "Water Cooler", ["Temperature"] = "Temperatura", ["Live temperature history"] = "Histórico da temperatura em tempo real", ["Utilization"] = "Utilização", ["Live utilization history"] = "Histórico da utilização em tempo real", ["WATER COOLER MODEL"] = "MODELO DO WATER COOLER", ["Preferences"] = "Preferências", ["Choose light or dark appearance"] = "Escolha o tema claro ou escuro", ["Sensor polling interval"] = "Intervalo de leitura dos sensores", ["Application display language"] = "Idioma da interface", ["Close"] = "Fechar", ["Pump display preview"] = "Prévia do display da bomba", ["CPU ONLY"] = "SOMENTE CPU",
        ["CPU TEMP"] = "TEMP. CPU", ["CELSIUS"] = "CELSIUS", ["Hardware link not connected"] = "Hardware não conectado",
        ["Preview only · HID transmission disabled"] = "Somente prévia · transmissão HID desativada",
        ["Thermal components"] = "Componentes térmicos", ["Sensor availability on this machine"] = "Sensores disponíveis neste computador",
        ["CPU package"] = "Pacote da CPU", ["Processor temperature"] = "Temperatura do processador",
        ["GPU core"] = "Núcleo da GPU", ["Graphics temperature"] = "Temperatura gráfica", ["Storage"] = "Armazenamento",
        ["Drive temperature, if exposed"] = "Temperatura do disco, se disponível",
        ["Storage volumes will appear here."] = "Os volumes de armazenamento aparecerão aqui.",
        ["Fan and pump control will appear after protocol validation."] = "O controle de ventoinhas e bomba será disponibilizado após validar o protocolo.",
        ["HW METRICS  /  SYSTEM MONITOR"] = "HW METRICS  /  MONITOR DO SISTEMA",
        ["All temperatures in °C"] = "Todas as temperaturas em °C",
        ["No local volumes detected"] = "Nenhum volume local detectado",
        ["Monitoring active · unsupported sensors remain N/D"] = "Monitoramento ativo · sensores não suportados aparecem como N/D",
        ["No readable sensors detected · try running as administrator"] = "Nenhum sensor legível · tente executar como administrador",
        ["Sensor service ready · polling every 2 seconds"] = "Serviço de sensores pronto · leitura a cada 2 segundos",
        ["Display transmission disabled"] = "Transmissão para o display desativada", ["Sensors unavailable"] = "Sensores indisponíveis",
        ["Storage capacity unavailable"] = "Capacidade do armazenamento indisponível", ["Drive"] = "Unidade",
        ["used"] = "em uso", ["used of"] = "em uso de", ["total"] = "Total", ["free"] = "livres", ["Disk"] = "Disco",
        ["Language changed"] = "Idioma alterado",
        ["Save history"] = "Salvar histórico",
        ["Save telemetry history"] = "Salvar histórico de telemetria",
        ["Telemetry history saved successfully."] = "Histórico de telemetria salvo com sucesso.",
        ["History saved"] = "Histórico salvo",
        ["Could not save telemetry history."] = "Não foi possível salvar o histórico de telemetria.",
        ["Save failed"] = "Falha ao salvar",
        ["History retention"] = "Retenção do histórico",
        ["How long to keep telemetry locally"] = "Por quanto tempo manter a telemetria localmente",
        ["30 minutes"] = "30 minutos",
        ["1 hour"] = "1 hora",
        ["6 hours"] = "6 horas",
        ["24 hours"] = "24 horas",
        ["Temperature sensors available: "] = "Sensores de temperatura disponíveis: ",
        ["No temperature sensors exposed by the current hardware/driver."] = "O hardware/driver atual não disponibilizou sensores de temperatura."
    };

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        _historyWriter = new TelemetryHistoryWriter(_historyStore);
        ApplyApplicationIcon();
        LoadPreferences();
        ApplyLanguage(LanguageSelector.SelectedIndex == 0 ? "en" : "pt");
        _pollTimer.Tick += async (_, _) => await RefreshSensorsAsync();
        _storageCycleTimer.Tick += (_, _) => RotateStorageDrive();
        InitializeTrayIcon();
    }

    private void ApplyApplicationIcon()
    {
        try
        {
            using var bitmap = LoadApplicationBitmap();
            var handle = bitmap.GetHicon();
            try
            {
                Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HWMetrics] Could not apply window icon: {ex}");
        }
    }

    private void LoadPreferences()
    {
        _loadingPreferences = true;
        try
        {
            var preferencesFile = File.Exists(_preferencesPath)
                ? _preferencesPath
                : _legacyPreferencesPath;
            var preferences = File.Exists(preferencesFile)
                ? JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(preferencesFile)) ?? new AppPreferences()
                : new AppPreferences();
            ThemeSelector.SelectedIndex = Math.Clamp(preferences.ThemeIndex, 0, 1);
            PollIntervalSelector.SelectedIndex = Math.Clamp(preferences.PollIntervalIndex, 0, 2);
            _pollTimer.Interval = GetSelectedPollInterval();
            LanguageSelector.SelectedIndex = Math.Clamp(preferences.LanguageIndex, 0, 1);
            CoolerModelSelector.SelectedIndex = Math.Clamp(preferences.CoolerModelIndex, 0, Math.Max(0, CoolerModelSelector.Items.Count - 1));
            HistoryRetentionSelector.SelectedIndex = Math.Clamp(preferences.HistoryRetentionIndex, 0, HistoryRetentionSelector.Items.Count - 1);
            CloseToTrayCheckBox.IsChecked = preferences.MinimizeToTrayOnClose;
            StartWithWindowsCheckBox.IsChecked = preferences.StartWithWindows;
            _closePromptAnswered = preferences.ClosePromptAnswered;
        }
        catch (Exception)
        {
            ThemeSelector.SelectedIndex = 0;
            PollIntervalSelector.SelectedIndex = 1;
            LanguageSelector.SelectedIndex = 1;
            CoolerModelSelector.SelectedIndex = 0;
            HistoryRetentionSelector.SelectedIndex = 0;
            CloseToTrayCheckBox.IsChecked = false;
            StartWithWindowsCheckBox.IsChecked = false;
            _closePromptAnswered = false;
        }
        finally
        {
            _loadingPreferences = false;
        }
    }

    private void SavePreferences()
    {
        if (_loadingPreferences || !IsLoaded) return;
        try
        {
            var preferences = new AppPreferences
            {
                ThemeIndex = Math.Max(0, ThemeSelector.SelectedIndex),
                PollIntervalIndex = Math.Max(0, PollIntervalSelector.SelectedIndex),
                LanguageIndex = Math.Max(0, LanguageSelector.SelectedIndex),
                CoolerModelIndex = Math.Max(0, CoolerModelSelector.SelectedIndex),
                HistoryRetentionIndex = Math.Max(0, HistoryRetentionSelector.SelectedIndex),
                MinimizeToTrayOnClose = CloseToTrayCheckBox.IsChecked == true,
                StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
                ClosePromptAnswered = _closePromptAnswered
            };
            var directory = Path.GetDirectoryName(_preferencesPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _preferencesPath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, _preferencesPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Cleanup is best-effort; preference persistence must not affect UI availability.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cleanup is best-effort; preference persistence must not affect UI availability.
                }
            }
        }
        catch (Exception)
        {
            // Preference persistence is best-effort; UI interaction should remain available.
        }
    }

    public bool StartHidden { get => _startHidden; set => _startHidden = value; }

    private static void FillRoundedRectangle(System.Drawing.Graphics graphics, System.Drawing.Brush brush, System.Drawing.Rectangle bounds, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    private static System.Drawing.Bitmap LoadApplicationBitmap()
    {
        var resource = Application.GetResourceStream(new Uri("Resources/Images/icon.png", UriKind.Relative))
            ?? throw new InvalidOperationException("The application icon resource is unavailable.");
        using var stream = resource.Stream;
        using var bitmap = new System.Drawing.Bitmap(stream);
        return new System.Drawing.Bitmap(bitmap);
    }

    private System.Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = LoadApplicationBitmap();
        using var scaled = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(32, 32));
        var handle = scaled.GetHicon();
        try
        {
            return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);

    private void InitializeTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(T("Open HW Metrics", "Abrir HW Metrics"), null, (_, _) => Dispatcher.Invoke(RestoreFromTray));
        menu.Items.Add(T("Exit", "Sair"), null, (_, _) => Dispatcher.Invoke(() => { _allowExit = true; Close(); }));
        _trayApplicationIcon = CreateTrayIcon();
        _trayIcon = new NotifyIcon
        {
            Icon = _trayApplicationIcon,
            Text = "HW Metrics — Hardware Studio",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WindowMessageHook);
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WmActivateExistingInstance) return 0;

        RestoreFromTray();
        handled = true;
        return 0;
    }

    private void RestoreFromTray()
    {
        Opacity = 1;
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        if (_trayIcon is not null) _trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply the default model after InitializeComponent has created every named control.
        CoolerModelSelector_SelectionChanged(CoolerModelSelector, null!);
        if (FindResource("IntroStoryboard") is Storyboard intro) intro.Begin(this, true);
        if (_startHidden) HideToTray();
        try
        {
            _historyRetention = GetSelectedHistoryRetention();
            var now = DateTime.Now;
            var persistedHistory = _historyStore.Load(now, _historyRetention);
            foreach (var point in persistedHistory)
            {
                _storedHistory.Enqueue(point);
                _lastPersistedSnapshotAt = point.UpdatedAt;
                if (point.UpdatedAt >= now - HistoryWindow)
                {
                    _history.Enqueue(point);
                    _utilizationHistory.Enqueue(point);
                }
            }

            var monitor = await Task.Run(() => new HardwareMonitoringService());
            // The window can close while the hardware service is opening on a worker thread.
            // Dispose the newly created service instead of attaching it to a closed window.
            if (_closing)
            {
                monitor.Dispose();
                return;
            }

            _monitor = monitor;
            UpdateDisplayStatus(DisplayTransportStatus.DeviceNotFound);
            _pollTimer.Start();
            _storageCycleTimer.Start();
            await RefreshSensorsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Sensor initialization failed: {ex}");
        }
    }

    private async Task RefreshSensorsAsync()
    {
        var monitor = _monitor;
        if (monitor is null || _reading || _closing) return;
        _reading = true;
        try
        {
            var snapshot = await Task.Run(monitor.Read);
            if (_closing) return;
            Render(snapshot);
            var displayStatus = await Task.Run(() => _displayTransport.Send(snapshot));
            if (!_closing) UpdateDisplayStatus(displayStatus);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Sensor refresh failed: {ex}");
        }
        finally
        {
            _reading = false;
        }
    }

    private void UpdateDisplayStatus(DisplayTransportStatus status)
    {
        var (title, detail, connected) = status switch
        {
            DisplayTransportStatus.Sent => (
                T("HID display connected", "Display HID conectado"),
                T("Telemetry sent to physical display", "Telemetria enviada ao display físico"),
                true),
            DisplayTransportStatus.InvalidTelemetry => (
                T("Waiting for device", "Aguardando dispositivo"),
                T("CPU temperature unavailable · not sent", "Temperatura da CPU indisponível · não enviado"),
                false),
            DisplayTransportStatus.IncompatibleReport => (
                T("HID display not found", "Display HID não encontrado"),
                T("HID report incompatible", "Relatório HID incompatível"),
                false),
            DisplayTransportStatus.OpenFailed => (
                T("HID display not found", "Display HID não encontrado"),
                T("Could not open HID display", "Não foi possível abrir o display HID"),
                false),
            DisplayTransportStatus.Error => (
                T("HID display not found", "Display HID não encontrado"),
                T("HID write failed", "Falha ao transmitir por HID"),
                false),
            _ => (
                T("Waiting for device", "Aguardando dispositivo"),
                T("HID display not found", "Display HID não encontrado"),
                false)
        };

        DisplayConnectionTitleText.Text = title;
        DisplayConnectionStatusText.Text = detail;
        DisplayConnectionIndicator.Fill = connected
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("WarningBrush");
    }

    private void RotateStorageDrive()
    {
        var volumes = _lastSnapshot?.StorageVolumes;
        if (volumes is null || volumes.Count < 2) return;

        _storageDriveIndex = (_storageDriveIndex + 1) % volumes.Count;
        RenderStorageVolume(volumes[_storageDriveIndex]);
    }

    private void RenderStorageVolume(StorageVolumeSnapshot volume)
    {
        _selectedStorageDriveName = volume.Name;
        // ProductModel identifies the physical disk, not the filesystem volume label.
        StorageModelText.Text = string.IsNullOrWhiteSpace(volume.ProductModel)
            ? T("Drive model unavailable", "Modelo do disco indisponível")
            : volume.ProductModel;
        StorageUsedPercentText.Text = $"{volume.UsedPercent:0}%";
        StorageCapacityText.Text =
            $"{FormatCapacity(volume.UsedGiB)} / {FormatCapacity(volume.TotalGiB)} {T("total", "Total")} - " +
            $"{FormatCapacity(volume.FreeGiB)} {T("free", "livres")}";
        AnimateProgress(StorageUsageBar, volume.UsedPercent);
    }

    private void Render(SensorSnapshot s)
    {
        _lastSnapshot = s;
        CpuModelText.Text = string.IsNullOrWhiteSpace(s.CpuModel) ? T("Processor model unavailable", "Modelo do processador indisponível") : s.CpuModel;
        GpuModelText.Text = string.IsNullOrWhiteSpace(s.GpuModel) ? T("Graphics model unavailable", "Modelo gráfico indisponível") : s.GpuModel;
        StorageModelText.Text = string.IsNullOrWhiteSpace(s.StorageModel) ? T("Drive model unavailable", "Modelo do disco indisponível") : s.StorageModel;
        CpuTempText.Text = ShortTemp(s.CpuTemperatureC);
        CpuDetailTemp.Text = FullTemp(s.CpuTemperatureC);
        CoolerDisplayTemp.Text = s.CpuTemperatureC.HasValue ? $"{s.CpuTemperatureC.Value:0}" : "--";
        GpuTempText.Text = ShortTemp(s.GpuTemperatureC);
        GpuDetailTemp.Text = FullTemp(s.GpuTemperatureC);
        StorageDetailTemp.Text = FullTemp(s.StorageTemperatureC);
        if (s.StorageVolumes.Count > 0)
        {
            // Select the Windows system volume on first render; subsequent reads keep the
            // selected volume stable instead of snapping back to C: every polling cycle.
            var selectedIndex = -1;
            if (_selectedStorageDriveName is not null)
            {
                for (var i = 0; i < s.StorageVolumes.Count; i++)
                {
                    if (!string.Equals(s.StorageVolumes[i].Name, _selectedStorageDriveName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    selectedIndex = i;
                    break;
                }
            }
            if (selectedIndex >= 0)
            {
                _storageDriveIndex = selectedIndex;
            }
            else if (_storageDriveIndex < 0 || _storageDriveIndex >= s.StorageVolumes.Count ||
                     _selectedStorageDriveName is not null)
            {
                var systemIndex = -1;
                for (var i = 0; i < s.StorageVolumes.Count; i++)
                {
                    if (!string.Equals(s.StorageVolumes[i].Name, s.SystemDriveName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    systemIndex = i;
                    break;
                }
                _storageDriveIndex = systemIndex >= 0 ? systemIndex : 0;
            }
            RenderStorageVolume(s.StorageVolumes[_storageDriveIndex]);
            StorageVolumesText.Text = s.StorageVolumesSummary;
        }
        else if (s.StorageUsedPercent.HasValue && s.StorageUsedGiB.HasValue && s.StorageTotalGiB.HasValue)
        {
            // Keep a safe fallback for snapshots with legacy aggregate storage data.
            StorageUsedPercentText.Text = $"{s.StorageUsedPercent.Value:0}%";
            StorageCapacityText.Text = $"{FormatCapacity(s.StorageUsedGiB.Value)} / {FormatCapacity(s.StorageTotalGiB.Value)} {T("total", "Total")}" +
                (s.StorageFreeGiB.HasValue ? $" - {FormatCapacity(s.StorageFreeGiB.Value)} {T("free", "livres")}" : string.Empty);
            AnimateProgress(StorageUsageBar, s.StorageUsedPercent.Value);
            StorageVolumesText.Text = s.StorageVolumesSummary;
        }
        else
        {
            StorageUsedPercentText.Text = "N/D";
            StorageCapacityText.Text = T("Storage capacity unavailable", "Capacidade do armazenamento indisponível");
            StorageUsageBar.Value = 0;
            StorageVolumesText.Text = T("No local volumes detected", "Nenhum volume local detectado");
        }

        CpuLoadText.Text = T("Utilization ", "Utilização ") + Percent(s.CpuLoadPercent);
        GpuLoadText.Text = T("Utilization ", "Utilização ") + Percent(s.GpuLoadPercent);

        if (s.MemoryUsedGiB.HasValue && s.MemoryTotalGiB.HasValue && s.MemoryTotalGiB.Value > 0)
        {
            var total = s.MemoryTotalGiB.Value;
            var used = Math.Clamp(s.MemoryUsedGiB.Value, 0, total);
            var free = Math.Max(0, total - used);
            var pct = Math.Clamp(used / total * 100, 0, 100);
            MemoryText.Text = $"{pct:0}%";
            MemoryPercentText.Text = T($"{FormatCapacity(used)} used · {FormatCapacity(free)} free of {FormatCapacity(total)}", $"{FormatCapacity(used)} em uso · {FormatCapacity(free)} livres de {FormatCapacity(total)}");
            AnimateProgress(MemoryUsageBar, pct);
        }
        else
        {
            MemoryText.Text = "N/D";
            MemoryPercentText.Text = T("Capacity unavailable", "Capacidade indisponível");
            AnimateProgress(MemoryUsageBar, 0);
        }

        _history.Enqueue(s);
        var cutoff = s.UpdatedAt - HistoryWindow;
        while (_history.Count > 0 && _history.Peek().UpdatedAt < cutoff) _history.Dequeue();
        UpdateTemperatureHistoryChart(s);

        _utilizationHistory.Enqueue(s);
        while (_utilizationHistory.Count > 0 && _utilizationHistory.Peek().UpdatedAt < cutoff) _utilizationHistory.Dequeue();
        UpdateUtilizationHistoryChart(s);

        if (_storedHistory.Count == 0 || s.UpdatedAt - _lastPersistedSnapshotAt >= TimeSpan.FromSeconds(10))
        {
            _storedHistory.Enqueue(s);
            _lastPersistedSnapshotAt = s.UpdatedAt;
        }

        var retentionCutoff = s.UpdatedAt - _historyRetention;
        while (_storedHistory.Count > 0 && _storedHistory.Peek().UpdatedAt < retentionCutoff)
            _storedHistory.Dequeue();

        if (s.UpdatedAt - _lastHistorySaveAt >= HistoryPersistenceInterval)
        {
            _historyWriter.Enqueue(_storedHistory, s.UpdatedAt, _historyRetention);
            _lastHistorySaveAt = s.UpdatedAt;
        }
    }

    private async void SaveHistory_Click(object sender, RoutedEventArgs e)
    {
        bool exportTemperature = sender is Button button &&
            button.Name == "SaveTemperatureHistoryButton";
        bool showCpu;
        bool showGpu;
        bool showRam = false;

        if (exportTemperature)
        {
            showCpu = TemperatureCpuFilter.IsChecked == true;
            showGpu = TemperatureGpuFilter.IsChecked == true;
        }
        else
        {
            showCpu = UtilizationCpuFilter.IsChecked == true;
            showGpu = UtilizationGpuFilter.IsChecked == true;
            showRam = UtilizationRamFilter.IsChecked == true;
        }

        if (!showCpu && !showGpu && !showRam)
        {
            MessageBox.Show(
                T("Select at least one series to export.", "Selecione ao menos uma série para exportar."),
                T("Nothing to export", "Nada para exportar"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = T("Export history to CSV", "Exportar histórico para CSV"),
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"hw-metrics-history-{(exportTemperature ? "temperature" : "utilization")}-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (dialog.ShowDialog(this) != true) return;

        // Export only the metric family and series selected in the corresponding chart.
        var snapshots = _storedHistory.OrderBy(snapshot => snapshot.UpdatedAt).ToArray();
        try
        {
            var csv = await Task.Run(() => BuildHistoryCsv(
                snapshots, exportTemperature, showCpu, showGpu, showRam));
            await Task.Run(() => File.WriteAllText(
                dialog.FileName,
                csv,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true)));
            MessageBox.Show(
                T("History exported successfully.", "Histórico exportado com sucesso."),
                T("Export complete", "Exportação concluída"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] History export failed: {ex}");
            MessageBox.Show(
                T("Could not export history.", "Não foi possível exportar o histórico."),
                T("Export failed", "Falha na exportação"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string BuildHistoryCsv(
        IReadOnlyList<SensorSnapshot> snapshots,
        bool exportTemperature,
        bool showCpu,
        bool showGpu,
        bool showRam)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Tipo;Horário;Componente;Valor;Unidade");

        foreach (var snapshot in snapshots)
        {
            var timestamp = snapshot.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            if (exportTemperature)
            {
                if (showCpu && snapshot.CpuTemperatureC is double cpuTemperature)
                    AppendHistoryCsvRow(csv, "Temperatura", timestamp, "CPU", cpuTemperature, "°C");
                if (showGpu && snapshot.GpuTemperatureC is double gpuTemperature)
                    AppendHistoryCsvRow(csv, "Temperatura", timestamp, "GPU", gpuTemperature, "°C");
            }
            else
            {
                if (showCpu && snapshot.CpuLoadPercent is double cpuLoad)
                    AppendHistoryCsvRow(csv, "Utilização", timestamp, "CPU", cpuLoad, "%");
                if (showGpu && snapshot.GpuLoadPercent is double gpuLoad)
                    AppendHistoryCsvRow(csv, "Utilização", timestamp, "GPU", gpuLoad, "%");
                if (showRam && snapshot.MemoryUsedGiB is double memoryUsed &&
                    snapshot.MemoryTotalGiB is double memoryTotal && memoryTotal > 0)
                    AppendHistoryCsvRow(csv, "Utilização", timestamp, "Memória", memoryUsed / memoryTotal * 100d, "%");
            }
        }

        return csv.ToString();
    }

    private static void AppendHistoryCsvRow(
        System.Text.StringBuilder csv,
        string type,
        string timestamp,
        string component,
        double value,
        string unit)
    {
        // All text fields are controlled labels/timestamps and contain no CSV delimiters.
        csv.Append(type).Append(';')
            .Append(timestamp).Append(';')
            .Append(component).Append(';')
            .Append(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
            .AppendLine(unit);
    }

    private TimeSpan GetSelectedHistoryRetention() => HistoryRetentionSelector.SelectedIndex switch
    {
        1 => TimeSpan.FromHours(1),
        2 => TimeSpan.FromHours(6),
        3 => TimeSpan.FromHours(24),
        _ => TimeSpan.FromMinutes(30)
    };

    private void HistoryRetentionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || HistoryRetentionSelector.SelectedIndex < 0) return;
        _historyRetention = GetSelectedHistoryRetention();
        SavePreferences();

        var now = DateTime.Now;
        var cutoff = now - _historyRetention;
        var retained = _storedHistory.Where(snapshot => snapshot.UpdatedAt >= cutoff && snapshot.UpdatedAt <= now).ToArray();
        _storedHistory.Clear();
        foreach (var snapshot in retained) _storedHistory.Enqueue(snapshot);
        _historyWriter.Enqueue(_storedHistory, now, _historyRetention);
        _lastHistorySaveAt = now;
    }

    private void HistoryFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized || _lastSnapshot is null) return;
        UpdateTemperatureHistoryChart(_lastSnapshot);
        UpdateUtilizationHistoryChart(_lastSnapshot);
    }

    private void UpdateTemperatureHistoryChart(SensorSnapshot current)
    {
        bool showCpu = TemperatureCpuFilter.IsChecked == true;
        bool showGpu = TemperatureGpuFilter.IsChecked == true;
        CpuHistoryPath.Visibility = showCpu ? Visibility.Visible : Visibility.Collapsed;
        GpuHistoryPath.Visibility = showGpu ? Visibility.Visible : Visibility.Collapsed;
        CpuHistoryPath.Data = showCpu
            ? BuildTemperatureGeometry(_history.Select(point => point.CpuTemperatureC), _history.Count)
            : Geometry.Empty;
        GpuHistoryPath.Data = showGpu
            ? BuildTemperatureGeometry(_history.Select(point => point.GpuTemperatureC), _history.Count)
            : Geometry.Empty;
    }

    private void UpdateUtilizationHistoryChart(SensorSnapshot current)
    {
        bool showCpu = UtilizationCpuFilter.IsChecked == true;
        bool showGpu = UtilizationGpuFilter.IsChecked == true;
        bool showRam = UtilizationRamFilter.IsChecked == true;
        CpuUtilizationHistoryPath.Visibility = showCpu ? Visibility.Visible : Visibility.Collapsed;
        GpuUtilizationHistoryPath.Visibility = showGpu ? Visibility.Visible : Visibility.Collapsed;
        MemoryUtilizationHistoryPath.Visibility = showRam ? Visibility.Visible : Visibility.Collapsed;
        CpuUtilizationHistoryPath.Data = showCpu
            ? BuildUtilizationGeometry(_utilizationHistory.Select(point => point.CpuLoadPercent), _utilizationHistory.Count)
            : Geometry.Empty;
        GpuUtilizationHistoryPath.Data = showGpu
            ? BuildUtilizationGeometry(_utilizationHistory.Select(point => point.GpuLoadPercent), _utilizationHistory.Count)
            : Geometry.Empty;
        MemoryUtilizationHistoryPath.Data = showRam
            ? BuildUtilizationGeometry(_utilizationHistory.Select(point =>
                point.MemoryUsedGiB.HasValue && point.MemoryTotalGiB is > 0
                    ? (double?)(point.MemoryUsedGiB.Value / point.MemoryTotalGiB.Value * 100d) : null),
                _utilizationHistory.Count)
            : Geometry.Empty;
        // The utilization card uses a fixed color legend in its footer, so no
        // separate caption control is required here.
    }

    private static Geometry BuildUtilizationGeometry(IEnumerable<double?> source, int sampleCount) =>
        BuildHistoryGeometry(source, sampleCount, static percent =>
            128d - Math.Clamp(percent, 0d, 100d) * 1.2d);

    private static Geometry BuildTemperatureGeometry(IEnumerable<double?> source, int sampleCount) =>
        BuildHistoryGeometry(source, sampleCount, static temperature =>
        {
            var normalized = Math.Clamp((temperature - 30d) / 60d, 0d, 1d);
            return 128d - normalized * 120d;
        });

    private static Geometry BuildHistoryGeometry(
        IEnumerable<double?> source,
        int sampleCount,
        Func<double, double> getY)
    {
        if (sampleCount == 0) return Geometry.Empty;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var index = 0;
            var pointCount = 0;
            Point firstPoint = default;
            foreach (var value in source)
            {
                if (value is double number && double.IsFinite(number))
                {
                    var x = sampleCount <= 1 ? 0d : index * 900d / (sampleCount - 1);
                    var point = new Point(x, getY(number));
                    if (pointCount == 0)
                    {
                        firstPoint = point;
                        context.BeginFigure(point, isFilled: false, isClosed: false);
                    }
                    else
                    {
                        context.LineTo(point, isStroked: true, isSmoothJoin: true);
                    }

                    pointCount++;
                }

                index++;
            }

            if (pointCount == 0) return Geometry.Empty;
            if (pointCount == 1)
                context.LineTo(new Point(Math.Min(900d, firstPoint.X + 2d), firstPoint.Y),
                    isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static void AnimateProgress(ProgressBar bar, double target)
    {
        var animation = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(420),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        bar.BeginAnimation(ProgressBar.ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || ThemeSelector.SelectedIndex < 0 || Application.Current is null) return;
        var source = ThemeSelector.SelectedIndex == 1 ? "Themes/Light.xaml" : "Themes/Dark.xaml";
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
        SavePreferences();
    }

    private static TimeSpan GetSelectedPollInterval(int selectedIndex) => selectedIndex switch
    {
        0 => TimeSpan.FromSeconds(1),
        2 => TimeSpan.FromSeconds(5),
        _ => TimeSpan.FromSeconds(2)
    };

    private TimeSpan GetSelectedPollInterval() => GetSelectedPollInterval(PollIntervalSelector.SelectedIndex);

    private void PollIntervalSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || PollIntervalSelector.SelectedIndex < 0) return;
        _pollTimer.Interval = GetSelectedPollInterval();
        if (_monitor is not null && !_pollTimer.IsEnabled) _pollTimer.Start();
        SavePreferences();
    }

    private void HideSettingsPanel()
    {
        SettingsDialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void NavOverview_Click(object sender, RoutedEventArgs e)
    {
        HideSettingsPanel();
        SetActiveNavigation(NavOverviewButton);
        MainScrollViewer.ScrollToTop();
    }

    private void NavCooling_Click(object sender, RoutedEventArgs e)
    {
        HideSettingsPanel();
        SetActiveNavigation(NavCoolingButton);
        CoolerCard.BringIntoView();
    }

    private void NavHardware_Click(object sender, RoutedEventArgs e)
    {
        HideSettingsPanel();
        SetActiveNavigation(NavHardwareButton);
        ThermalComponentsCard.BringIntoView();
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        SetActiveNavigation(NavSettingsButton);
        SettingsDialogOverlay.Visibility = Visibility.Visible;
        ThemeSelector.Focus();
    }

    private void CloseSettingsDialog_Click(object sender, RoutedEventArgs e)
    {
        HideSettingsPanel();
    }

    private void CoolerModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || CoolerModelSelector.SelectedIndex < 0) return;

        // Tag schema: product code | finish | display style. This selector changes
        // only the block preview; it never sends commands to the physical cooler.
        var selectedItem = CoolerModelSelector.SelectedItem as ComboBoxItem;
        var tag = selectedItem?.Tag?.ToString() ?? string.Empty;
        var parts = tag.Split('|');
        var modelCode = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])
            ? parts[0]
            : selectedItem?.Content?.ToString() ?? "Rise Mode";
        var blackModel = parts.Length > 1
            ? string.Equals(parts[1], "black", StringComparison.OrdinalIgnoreCase)
            : modelCode.Contains("WAIB", StringComparison.OrdinalIgnoreCase)
                || modelCode.Contains("WCB", StringComparison.OrdinalIgnoreCase)
                || modelCode.Contains("WC-AB", StringComparison.OrdinalIgnoreCase);
        var hasNumericDisplay = parts.Length > 2
            ? string.Equals(parts[2], "display", StringComparison.OrdinalIgnoreCase)
            : modelCode.Contains("WAI-", StringComparison.OrdinalIgnoreCase)
                || modelCode.Contains("WAIB-", StringComparison.OrdinalIgnoreCase);
        var family = parts.Length > 3 ? parts[3] : (hasNumericDisplay ? "auraice" : "classic");

        CoolerModelBadge.Text = modelCode;
        CoolerAuraIceVisual.Visibility = family == "auraice" ? Visibility.Visible : Visibility.Collapsed;
        CoolerAuraFrostProVisual.Visibility = family == "aurafrostpro" ? Visibility.Visible : Visibility.Collapsed;
        CoolerClassicVisual.Visibility = family == "classic" ? Visibility.Visible : Visibility.Collapsed;
        CoolerAllBlackVisual.Visibility = family == "allblack" ? Visibility.Visible : Visibility.Collapsed;

        var bodyColor = blackModel ? Color.FromRgb(20, 22, 27) : Color.FromRgb(239, 242, 245);
        var bodyStroke = blackModel ? Color.FromRgb(66, 76, 88) : Color.FromRgb(177, 188, 199);
        CoolerBody.Fill = new SolidColorBrush(bodyColor);
        CoolerBody.Stroke = new SolidColorBrush(bodyStroke);
        CoolerProBody.Background = new SolidColorBrush(bodyColor);
        CoolerProBody.BorderBrush = new SolidColorBrush(bodyStroke);
        CoolerClassicBody.Fill = new SolidColorBrush(bodyColor);
        CoolerClassicBody.Stroke = new SolidColorBrush(bodyStroke);

        CoolerDisplayTemp.Visibility = hasNumericDisplay ? Visibility.Visible : Visibility.Collapsed;
        CoolerDisplayLabel.Visibility = hasNumericDisplay ? Visibility.Visible : Visibility.Collapsed;

        // Shared ARGB ring: only the block shell and its display vary by model.
        var ringBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        ringBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 0), 0.0));
        ringBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 255, 0), 0.5));
        ringBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 255), 1.0));
        CoolerArgbRing.Stroke = ringBrush;
        CoolerProLight.BorderBrush = new SolidColorBrush(Color.FromRgb(98, 230, 197));
        CoolerClassicLight.Stroke = new SolidColorBrush(Color.FromRgb(98, 230, 197));
        CoolerDisplayTemp.Foreground = new SolidColorBrush(Color.FromRgb(244, 251, 255));
        CoolerModelBadge.Foreground = (Brush)FindResource("AccentBrush");
        SavePreferences();
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageSelector.SelectedIndex < 0) return;
        ApplyLanguage(LanguageSelector.SelectedIndex == 1 ? "pt" : "en");
        SavePreferences();
    }

    private void ApplyLanguage(string language)
    {
        _language = language;
        var reverse = Portuguese.GroupBy(pair => pair.Value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
        TranslateTree(this, language == "pt" ? Portuguese : reverse);
        Title = language == "pt" ? Portuguese["HW Metrics — Hardware Studio"] : "HW Metrics — Hardware Studio";
        if (_trayIcon is not null)
        {
            _trayIcon.Text = T("HW Metrics — Hardware Studio", "HW Metrics — Central de Hardware");
            if (_trayIcon.ContextMenuStrip is ContextMenuStrip trayMenu && trayMenu.Items.Count >= 2)
            {
                trayMenu.Items[0].Text = T("Open HW Metrics", "Abrir HW Metrics");
                trayMenu.Items[1].Text = T("Exit", "Sair");
            }
        }
    }

    private static void TranslateTree(DependencyObject parent, IReadOnlyDictionary<string, string> dictionary)
    {
        if (parent is TextBlock textBlock && dictionary.TryGetValue(textBlock.Text, out var translatedText))
            textBlock.Text = translatedText;
        if (parent is ContentControl contentControl && contentControl.Content is string content && dictionary.TryGetValue(content, out var translatedContent))
            contentControl.Content = translatedContent;
        if (parent is FrameworkElement element && element.ToolTip is string toolTip && dictionary.TryGetValue(toolTip, out var translatedToolTip))
            element.ToolTip = translatedToolTip;

        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>().ToArray())
            TranslateTree(child, dictionary);
    }

    private string T(string english, string portuguese) => _language == "pt" ? portuguese : english;

    private void SetActiveNavigation(Button active)
    {
        foreach (var button in new[] { NavOverviewButton, NavCoolingButton, NavHardwareButton, NavSettingsButton })
        {
            button.Background = Brushes.Transparent;
            button.SetResourceReference(Control.ForegroundProperty, "TextSecondaryBrush");
        }
        active.SetResourceReference(Control.BackgroundProperty, "PanelRaisedBrush");
        active.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
    }

    private static string ShortTemp(double? value) => value.HasValue ? $"{value.Value:0}°" : "--°";
    private static string FullTemp(double? value) => value.HasValue ? $"{value.Value:0}°C" : "N/D";
    private static string FormatCapacity(double gib)
    {
        if (!double.IsFinite(gib) || gib < 0) return "N/D";
        return gib >= 1024d
            ? $"{gib / 1024d:0.0} TB"
            : $"{gib:0.0} GB";
    }

    private static string Percent(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? $"{Math.Clamp(value.Value, 0d, 100d):0}%" : "N/D";
    private static double Clamp(double? value) => value.HasValue ? Math.Clamp(value.Value, 0, 100) : 0;

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateWindowControlGlyphs();
    }

    private void UpdateWindowControlGlyphs()
    {
        MaximizeGlyph.Visibility = WindowState == WindowState.Maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreGlyph.Visibility = WindowState == WindowState.Maximized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowExit) return;
        e.Cancel = true;

        if (!_closePromptAnswered)
        {
            var choice = MessageBox.Show(
                T("Would you like to exit HW Metrics or keep it running in the system tray?",
                  "Deseja fechar o HW Metrics ou mantê-lo em execução na bandeja do sistema?"),
                T("Close HW Metrics", "Fechar HW Metrics"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Cancel);
            if (choice == MessageBoxResult.Cancel) return;

            _closePromptAnswered = true;
            CloseToTrayCheckBox.IsChecked = choice == MessageBoxResult.No;
            SavePreferences();
            if (choice == MessageBoxResult.No)
            {
                HideToTray();
                return;
            }

            _allowExit = true;
            e.Cancel = false;
            Close();
            return;
        }

        if (CloseToTrayCheckBox.IsChecked == true)
        {
            HideToTray();
            return;
        }

        _allowExit = true;
        e.Cancel = false;
        Close();
    }

    private void CloseToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        SavePreferences();
    }

    internal static string BuildWindowsStartupCommand(string executable, string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new ArgumentException("An executable path is required.", nameof(executable));

        // When launched through the .NET host, Windows must run dotnet.exe with
        // the app assembly before passing the application-specific --tray argument.
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentException("An assembly path is required when launching through dotnet.", nameof(assemblyPath));

            return $"\"{executable}\" \"{assemblyPath}\" --tray";
        }

        return $"\"{executable}\" --tray";
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingPreferences) return;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
                ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");
            if (StartWithWindowsCheckBox.IsChecked == true)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable))
                    throw new InvalidOperationException("Could not determine the current executable path.");

                var startupCommand = BuildWindowsStartupCommand(executable, typeof(App).Assembly.Location);
                key.SetValue("HWMetrics", startupCommand);
                key.DeleteValue("AquaCore", throwOnMissingValue: false);
            }
            else
            {
                key.DeleteValue("HWMetrics", throwOnMissingValue: false);
                key.DeleteValue("AquaCore", throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                T("Could not update Windows startup settings.", "Não foi possível alterar a inicialização do Windows.") + Environment.NewLine + ex.Message,
                T("Startup setting failed", "Falha na configuração de inicialização"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        SavePreferences();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsDialogOverlay.Visibility == Visibility.Visible)
        {
            HideSettingsPanel();
            e.Handled = true;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        _closing = true;
        _pollTimer.Stop();
        _storageCycleTimer.Stop();

        try
        {
            _historyWriter.Enqueue(_storedHistory, DateTime.Now, _historyRetention);
            await _historyWriter.DisposeAsync();
        }
        finally
        {
            // Isolate cleanup failures so one provider cannot prevent releasing later resources.
            try { _monitor?.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Hardware cleanup failed: {ex}"); }

            try { _displayTransport.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] HID cleanup failed: {ex}"); }

            if (_trayIcon is not null)
            {
                try
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Tray icon cleanup failed: {ex}");
                }
                finally
                {
                    _trayIcon = null;
                }
            }

            try { _trayApplicationIcon?.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Application icon cleanup failed: {ex}"); }
            finally { _trayApplicationIcon = null; }
        }
    }
}
