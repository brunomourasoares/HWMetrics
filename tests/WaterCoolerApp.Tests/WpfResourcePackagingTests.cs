using System.Collections;
using System.Resources;
using WaterCoolerApp;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class WpfResourcePackagingTests
{
    [Fact]
    public void Dseg7Font_IsPackagedAtExpectedLogicalPath()
    {
        var assembly = typeof(MainWindow).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(".g.resources", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(resourceName);

        using var stream = assembly.GetManifestResourceStream(resourceName!);
        Assert.NotNull(stream);

        using var reader = new ResourceReader(stream!);
        var resourceKeys = reader.Cast<DictionaryEntry>()
            .Select(entry => entry.Key?.ToString())
            .Where(key => key is not null)
            .ToArray();

        Assert.Contains(
            resourceKeys,
            key => string.Equals(
                key,
                "resources/fonts/dseg7classic-regular.ttf",
                StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            resourceKeys,
            key => string.Equals(
                key,
                "resources/images/rise-mode.png",
                StringComparison.OrdinalIgnoreCase));
    }
}
