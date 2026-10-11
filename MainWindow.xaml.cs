using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using WaterCoolerApp.Models;
using WaterCoolerApp.Services;

namespace WaterCoolerApp;

public partial class MainWindow : Window
{
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(30);
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _storageCycleTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Queue<SensorSnapshot> _history = new();
    private readonly Queue<SensorSnapshot> _utilizationHistory = new();
    private readonly TelemetryHistoryStore _historyStore = new();
    private DateTime _lastHistorySaveAt = DateTime.MinValue;
    private HardwareMonitoringService? _monitor;
    private bool _reading;
    private bool _closing;
    private string _language = "pt";
    private SensorSnapshot? _lastSnapshot;
    private int _storageDriveIndex = -1;
    private string? _selectedStorageDriveName;

    private static readonly Dictionary<string, string> Portuguese = new(StringComparer.Ordinal)
    {
        ["Aqua Core — Hardware Studio"] = "Aqua Core — Central de Hardware",
        ["WORKSPACE"] = "ESPAÇO DE TRABALHO", ["Overview"] = "Visão geral", ["Cooling"] = "Refrigeração",
        ["Hardware"] = "Hardware", ["Settings"] = "Configurações", ["DISPLAY DEVICE"] = "DISPOSITIVO DE DISPLAY",
        ["Waiting for device"] = "Aguardando dispositivo", ["USB / HID connection not enabled"] = "Conexão USB / HID desativada",
        ["APPEARANCE"] = "APARÊNCIA", ["Interface theme"] = "Tema da interface", ["Dark"] = "Escuro", ["Light"] = "Claro",
        ["Refresh rate"] = "Intervalo de atualização", ["Language"] = "Idioma", ["SYSTEM SETTINGS"] = "CONFIGURAÇÕES DO SISTEMA", ["1 sec"] = "1 s", ["2 sec"] = "2 s", ["5 sec"] = "5 s",
        ["Preview build · .NET 10"] = "Versão de prévia · .NET 10", ["System overview"] = "Visão geral do sistema",
        ["Your hardware, at a glance."] = "Seu hardware em um só lugar.",
        ["PROCESSOR"] = "PROCESSADOR", ["CPU"] = "CPU", ["GPU"] = "GPU",
        ["GRAPHICS"] = "GRÁFICOS", ["MEMORY"] = "MEMÓRIA", ["Capacity unavailable"] = "Capacidade indisponível",
        ["STORAGE"] = "ARMAZENAMENTO", ["Waiting for disk data"] = "Aguardando dados do disco",
        ["Thermal performance"] = "Desempenho térmico", ["Utilization performance"] = "Desempenho de utilização", ["Live CPU, GPU and memory history · rolling 30 minutes"] = "Histórico ao vivo de CPU, GPU e memória · últimos 30 minutos", ["Live temperature history · rolling 30 minutes"] = "Histórico de temperatura ao vivo · últimos 30 minutos",
        ["Rolling 30 minutes · data since launch"] = "Últimos 30 minutos · dados desde a inicialização", ["100%"] = "100%", ["75%"] = "75%", ["50%"] = "50%", ["25%"] = "25%",
        ["Waiting for sensor data"] = "Aguardando dados dos sensores", ["System utilization"] = "Utilização do sistema",
        ["Current workload by component"] = "Carga atual por componente", ["CPU utilization"] = "Utilização da CPU",
        ["GPU utilization"] = "Utilização da GPU", ["Memory in use"] = "Memória em uso",
        ["Water cooler"] = "Water Cooler", ["Temperature"] = "Temperatura", ["Live temperature history"] = "Histórico da temperatura em tempo real", ["Utilization"] = "Utilização", ["Live utilization history"] = "Histórico da utilização em tempo real", ["WATER COOLER MODEL"] = "MODELO DO WATER COOLER", ["Generic LCD Pump"] = "Bomba LCD genérica", ["NZXT Kraken LCD"] = "NZXT Kraken LCD", ["Corsair iCUE LCD"] = "Corsair iCUE LCD", ["ASUS ROG Ryujin LCD"] = "ASUS ROG Ryujin LCD", ["Preferences"] = "Preferências", ["Choose light or dark appearance"] = "Escolha o tema claro ou escuro", ["Sensor polling interval"] = "Intervalo de leitura dos sensores", ["Application display language"] = "Idioma da interface", ["Close"] = "Fechar", ["Pump display preview"] = "Prévia do display da bomba", ["CPU ONLY"] = "SOMENTE CPU",
        ["CPU TEMP"] = "TEMP. CPU", ["CELSIUS"] = "CELSIUS", ["Hardware link not connected"] = "Hardware não conectado",
        ["Preview only · HID transmission disabled"] = "Somente prévia · transmissão HID desativada",
        ["Thermal components"] = "Componentes térmicos", ["Sensor availability on this machine"] = "Sensores disponíveis neste computador",
        ["CPU package"] = "Pacote da CPU", ["Processor temperature"] = "Temperatura do processador",
        ["GPU core"] = "Núcleo da GPU", ["Graphics temperature"] = "Temperatura gráfica", ["Storage"] = "Armazenamento",
        ["Drive temperature, if exposed"] = "Temperatura do disco, se disponível",
        ["Storage volumes will appear here."] = "Os volumes de armazenamento aparecerão aqui.",
        ["Fan and pump control will appear after protocol validation."] = "O controle de ventoinhas e bomba será disponibilizado após validar o protocolo.",
        ["AQUA CORE  /  SYSTEM MONITOR"] = "AQUA CORE  /  MONITOR DO SISTEMA",
        ["All temperatures in °C"] = "Todas as temperaturas em °C",
        ["No local volumes detected"] = "Nenhum volume local detectado",
        ["Monitoring active · unsupported sensors remain N/D"] = "Monitoramento ativo · sensores não suportados aparecem como N/D",
        ["No readable sensors detected · try running as administrator"] = "Nenhum sensor legível · tente executar como administrador",
        ["Sensor service ready · polling every 2 seconds"] = "Serviço de sensores pronto · leitura a cada 2 segundos",
        ["Display transmission disabled"] = "Transmissão para o display desativada", ["Sensors unavailable"] = "Sensores indisponíveis",
        ["Storage capacity unavailable"] = "Capacidade do armazenamento indisponível", ["Drive"] = "Unidade",
        ["used"] = "em uso", ["used of"] = "em uso de", ["total"] = "Total", ["free"] = "livres", ["Disk"] = "Disco",
        ["Language changed"] = "Idioma alterado",
        ["Temperature sensors available: "] = "Sensores de temperatura disponíveis: ",
        ["No temperature sensors exposed by the current hardware/driver."] = "O hardware/driver atual não disponibilizou sensores de temperatura."
    };

    public MainWindow()
    {
        InitializeComponent();
        ApplyLanguage("pt");
        _pollTimer.Tick += async (_, _) => await RefreshSensorsAsync();
        _storageCycleTimer.Tick += (_, _) => RotateStorageDrive();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (FindResource("IntroStoryboard") is Storyboard intro) intro.Begin(this, true);
        try
        {
            var persistedHistory = _historyStore.Load(DateTime.Now);
            foreach (var point in persistedHistory)
            {
                _history.Enqueue(point);
                _utilizationHistory.Enqueue(point);
            }

            _monitor = await Task.Run(() => new HardwareMonitoringService());
            DeviceStatusText.Text = T("Display transmission disabled", "Transmissão para o display desativada");
            _pollTimer.Start();
            _storageCycleTimer.Start();
            await RefreshSensorsAsync();
        }
        catch (Exception ex)
        {
            DeviceStatusText.Text = T("Sensors unavailable", "Sensores indisponíveis");
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
            if (!_closing) Render(snapshot);
        }
        catch (Exception ex)
        {
        }
        finally
        {
            _reading = false;
        }
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
        StorageDriveNameText.Text = volume.Name;
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
            var selectedIndex = _selectedStorageDriveName is null
                ? -1
                : s.StorageVolumes.ToList().FindIndex(volume =>
                    string.Equals(volume.Name, _selectedStorageDriveName, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                _storageDriveIndex = selectedIndex;
            }
            else if (_storageDriveIndex < 0 || _storageDriveIndex >= s.StorageVolumes.Count ||
                     _selectedStorageDriveName is not null)
            {
                var systemIndex = s.StorageVolumes.ToList().FindIndex(volume =>
                    string.Equals(volume.Name, s.SystemDriveName, StringComparison.OrdinalIgnoreCase));
                _storageDriveIndex = systemIndex >= 0 ? systemIndex : 0;
            }
            RenderStorageVolume(s.StorageVolumes[_storageDriveIndex]);
            StorageVolumesText.Text = s.StorageVolumesSummary;
        }
        else if (s.StorageUsedPercent.HasValue && s.StorageUsedGiB.HasValue && s.StorageTotalGiB.HasValue)
        {
            // Keep a safe fallback for snapshots with legacy aggregate storage data.
            StorageUsedPercentText.Text = $"{s.StorageUsedPercent.Value:0}%";
            StorageDriveNameText.Text = s.SystemDriveName ?? string.Empty;
            StorageCapacityText.Text = $"{FormatCapacity(s.StorageUsedGiB.Value)} / {FormatCapacity(s.StorageTotalGiB.Value)} {T("total", "Total")}" +
                (s.StorageFreeGiB.HasValue ? $" - {FormatCapacity(s.StorageFreeGiB.Value)} {T("free", "livres")}" : string.Empty);
            AnimateProgress(StorageUsageBar, s.StorageUsedPercent.Value);
            StorageVolumesText.Text = s.StorageVolumesSummary;
        }
        else
        {
            StorageUsedPercentText.Text = "N/D";
            StorageDriveNameText.Text = string.Empty;
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
            MemoryPercentText.Text = $"{FormatCapacity(used)} / {FormatCapacity(total)} {T("total", "Total")} - {FormatCapacity(free)} {T("free", "livres")}";
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
        CpuHistoryPath.Data = BuildTemperatureGeometry(_history.Select(point => point.CpuTemperatureC));
        GpuHistoryPath.Data = BuildTemperatureGeometry(_history.Select(point => point.GpuTemperatureC));
        GraphCaption.Text = $"CPU {FullTemp(s.CpuTemperatureC)} · GPU {FullTemp(s.GpuTemperatureC)}";

        _utilizationHistory.Enqueue(s);
        while (_utilizationHistory.Count > 0 && _utilizationHistory.Peek().UpdatedAt < cutoff) _utilizationHistory.Dequeue();
        CpuUtilizationHistoryPath.Data = BuildUtilizationGeometry(_utilizationHistory.Select(point => point.CpuLoadPercent));
        GpuUtilizationHistoryPath.Data = BuildUtilizationGeometry(_utilizationHistory.Select(point => point.GpuLoadPercent));
        MemoryUtilizationHistoryPath.Data = BuildUtilizationGeometry(_utilizationHistory.Select(point =>
            point.MemoryUsedGiB.HasValue && point.MemoryTotalGiB is > 0
                ? (double?)(point.MemoryUsedGiB.Value / point.MemoryTotalGiB.Value * 100d) : null));
        UtilizationGraphCaption.Text = $"CPU {Percent(s.CpuLoadPercent)} · GPU {Percent(s.GpuLoadPercent)} · RAM {MemoryUsagePercent(s)}";

        // Persist at a bounded cadence instead of writing to disk on every sensor poll.
        if (s.UpdatedAt - _lastHistorySaveAt >= TimeSpan.FromSeconds(10))
        {
            _historyStore.Save(_history, s.UpdatedAt);
            _lastHistorySaveAt = s.UpdatedAt;
        }


    }

    private static string MemoryUsagePercent(SensorSnapshot snapshot) =>
        snapshot.MemoryUsedGiB.HasValue && snapshot.MemoryTotalGiB is > 0
            ? $"{Math.Clamp(snapshot.MemoryUsedGiB.Value / snapshot.MemoryTotalGiB.Value * 100d, 0d, 100d):0}%"
            : "N/D";

    private static Geometry BuildUtilizationGeometry(IEnumerable<double?> source)
    {
        var values = source.ToList();
        var points = new List<Point>();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not double percent || !double.IsFinite(percent)) continue;
            var x = values.Count <= 1 ? 0 : i * 900d / (values.Count - 1);
            var y = 128d - Math.Clamp(percent, 0d, 100d) * 1.2d;
            points.Add(new Point(x, y));
        }
        if (points.Count == 0) return Geometry.Empty;
        if (points.Count == 1) points.Add(new Point(Math.Min(900, points[0].X + 2), points[0].Y));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], isFilled: false, isClosed: false);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i], isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildTemperatureGeometry(IEnumerable<double?> source)
    {
        var values = source.ToList();
        var points = new List<Point>();
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not double temperature || !double.IsFinite(temperature)) continue;
            var x = values.Count <= 1 ? 0 : i * 900d / (values.Count - 1);
            var normalized = Math.Clamp((temperature - 30d) / 60d, 0d, 1d);
            var y = 128d - normalized * 120d;
            points.Add(new Point(x, y));
        }

        if (points.Count == 0) return Geometry.Empty;
        if (points.Count == 1)
        {
            var point = points[0];
            points.Add(new Point(Math.Min(900, point.X + 2), point.Y));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], isFilled: false, isClosed: false);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i], isStroked: true, isSmoothJoin: true);
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
    }

    private void PollIntervalSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || PollIntervalSelector.SelectedIndex < 0) return;
        var seconds = PollIntervalSelector.SelectedIndex switch { 0 => 1, 2 => 5, _ => 2 };
        _pollTimer.Interval = TimeSpan.FromSeconds(seconds);
        if (_monitor is not null && !_pollTimer.IsEnabled) _pollTimer.Start();
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
        var model = (CoolerModelSelector.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Generic LCD Pump";
        CoolerModelBadge.Text = model;
        var accent = CoolerModelSelector.SelectedIndex switch
        {
            1 => Color.FromRgb(105, 191, 255),
            2 => Color.FromRgb(244, 189, 104),
            3 => Color.FromRgb(189, 163, 255),
            _ => Color.FromRgb(112, 232, 208)
        };
        CoolerDisplayTemp.Foreground = new SolidColorBrush(accent);
        CoolerModelBadge.Foreground = new SolidColorBrush(accent);
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || LanguageSelector.SelectedIndex < 0) return;
        ApplyLanguage(LanguageSelector.SelectedIndex == 1 ? "pt" : "en");
    }

    private void ApplyLanguage(string language)
    {
        _language = language;
        var reverse = Portuguese.GroupBy(pair => pair.Value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
        TranslateTree(this, language == "pt" ? Portuguese : reverse);
        Title = language == "pt" ? Portuguese["Aqua Core — Hardware Studio"] : "Aqua Core — Hardware Studio";
    }

    private static void TranslateTree(DependencyObject parent, IReadOnlyDictionary<string, string> dictionary)
    {
        if (parent is TextBlock textBlock && dictionary.TryGetValue(textBlock.Text, out var translatedText))
            textBlock.Text = translatedText;
        if (parent is ContentControl contentControl && contentControl.Content is string content && dictionary.TryGetValue(content, out var translatedContent))
            contentControl.Content = translatedContent;

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

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _closing = true;
        _pollTimer.Stop();
        _storageCycleTimer.Stop();
        _historyStore.Save(_history, DateTime.Now);
        _monitor?.Dispose();
    }
}
