using System.Text.RegularExpressions;
using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class LocalizerTests
{
    [Fact]
    public void EveryEntryHasATranslationForEveryNonEnglishLanguage()
    {
        foreach (var (key, row) in Localizer.Table)
        {
            Assert.Equal(Localizer.Languages.Count - 1, row.Length);
            Assert.All(row, text => Assert.False(string.IsNullOrWhiteSpace(text), $"Empty translation for '{key}'"));
        }
    }

    [Fact]
    public void FormatPlaceholdersMatchTheEnglishText()
    {
        var placeholder = new Regex(@"\{\d+\}");
        foreach (var (key, row) in Localizer.Table)
        {
            var expected = placeholder.Matches(key).Select(m => m.Value).OrderBy(v => v).ToArray();
            foreach (var text in row)
                Assert.Equal(expected, placeholder.Matches(text).Select(m => m.Value).OrderBy(v => v).ToArray());
        }
    }

    [Fact]
    public void UnknownTextFallsBackToEnglish()
    {
        foreach (var language in Localizer.Languages)
            Assert.Equal("Not a real key", Localizer.Get("Not a real key", language.Code));
    }

    [Fact]
    public void AllVariantsContainsEveryLanguageOfTheWindowTitle()
    {
        var variants = Localizer.AllVariants("HW Metrics — Hardware Studio").ToArray();
        Assert.Equal(Localizer.Languages.Count, variants.Length);
        Assert.Equal(variants.Length, variants.Distinct().Count());
    }

    [Theory]
    [InlineData("Sair", "Exit")]
    [InlineData("Salir", "Exit")]
    [InlineData("Esci", "Exit")]
    [InlineData("Quitter", "Exit")]
    [InlineData("退出", "Exit")]
    public void LocalizedTextCanBeResolvedToItsEnglishKey(string localized, string expected) =>
        Assert.Equal(expected, Localizer.GetEnglishKey(localized));

    [Theory]
    [InlineData("pt", "Sair")]
    [InlineData("es", "Salir")]
    [InlineData("it", "Esci")]
    [InlineData("fr", "Quitter")]
    [InlineData("zh", "退出")]
    [InlineData("en", "Exit")]
    public void ReturnsTheRequestedLanguage(string code, string expected) =>
        Assert.Equal(expected, Localizer.Get("Exit", code));
}
