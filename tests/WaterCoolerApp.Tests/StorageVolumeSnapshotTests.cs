using WaterCoolerApp.Models;

namespace WaterCoolerApp.Tests;

public sealed class StorageVolumeSnapshotTests
{
    [Theory]
    [InlineData(40, 100, 60, 40)]
    [InlineData(0, 100, 100, 0)]
    [InlineData(100, 100, 0, 100)]
    public void VolumeSnapshot_CalculatesExpectedValues(
        double used, double total, double free, double percent)
    {
        var snapshot = new StorageVolumeSnapshot(
            "C:", used, total, free, percent);

        Assert.Equal(used, snapshot.UsedGiB);
        Assert.Equal(total, snapshot.TotalGiB);
        Assert.Equal(free, snapshot.FreeGiB);
        Assert.Equal(percent, snapshot.UsedPercent);
    }

    [Fact]
    public void VolumeSnapshot_AllowsMultipleDrivesWithIndependentValues()
    {
        var systemDrive = new StorageVolumeSnapshot("C:", 256, 512, 256, 50);
        var dataDrive = new StorageVolumeSnapshot("D:", 900, 1000, 100, 90);

        Assert.NotEqual(systemDrive.Name, dataDrive.Name);
        Assert.Equal(50, systemDrive.UsedPercent);
        Assert.Equal(90, dataDrive.UsedPercent);
        Assert.Equal(256, systemDrive.FreeGiB);
        Assert.Equal(100, dataDrive.FreeGiB);
    }

    [Fact]
    public void VolumeSnapshot_UsesGiBAndPercentAsIndependentValues()
    {
        var snapshot = new StorageVolumeSnapshot("E:", 1.5, 2, 0.5, 75);

        Assert.Equal(1.5, snapshot.UsedGiB);
        Assert.Equal(2, snapshot.TotalGiB);
        Assert.Equal(0.5, snapshot.FreeGiB);
        Assert.Equal(75, snapshot.UsedPercent);
    }
}
