using System.Windows.Controls;

namespace RMMonitor.Controls;

/// <summary>
/// Owns the dashboard's top-level CPU, GPU, memory, and storage metric cards.
/// Sensor acquisition remains in HardwareMonitoringService; MainWindow supplies rendered values.
/// </summary>
public partial class DashboardMetricsControl : UserControl
{
    public DashboardMetricsControl() => InitializeComponent();
    public TextBlock CpuModelText => CpuModelTextElement;
    public TextBlock CpuTempText => CpuTempTextElement;
    public TextBlock CpuLoadText => CpuLoadTextElement;
    public TextBlock GpuModelText => GpuModelTextElement;
    public TextBlock GpuTempText => GpuTempTextElement;
    public TextBlock GpuLoadText => GpuLoadTextElement;
    public TextBlock MemoryText => MemoryTextElement;
    public TextBlock MemoryPercentText => MemoryPercentTextElement;
    public ProgressBar MemoryUsageBar => MemoryUsageBarElement;
    public TextBlock StorageVolumesText => StorageVolumesTextElement;
    public TextBlock StorageModelText => StorageModelTextElement;
    public TextBlock StorageUsedPercentText => StorageUsedPercentTextElement;
    public TextBlock StorageCapacityText => StorageCapacityTextElement;
    public ProgressBar StorageUsageBar => StorageUsageBarElement;
}
