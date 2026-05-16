using System.IO;
using GreenDistrict.Simulation.Localization;
using Xunit;

namespace GreenDistrict.Tests;

public class LocalizationTests
{
    [Fact]
    public void Translate_Returns_Current_Language_Value()
    {
        var localization = new LocalizationSystem();
        localization.AddTranslation(GameLanguage.English, "ui.budget", "Budget");
        localization.AddTranslation(GameLanguage.Russian, "ui.budget", "Бюджет");

        Assert.Equal("Budget", localization.Translate("ui.budget"));

        localization.SetLanguage(GameLanguage.Russian);

        Assert.Equal("Бюджет", localization.Translate("ui.budget"));
    }

    [Fact]
    public void Translate_Falls_Back_To_Default_Language_And_Then_Key()
    {
        var localization = new LocalizationSystem(defaultLanguage: GameLanguage.Russian, fallbackLanguage: GameLanguage.English);
        localization.AddTranslation(GameLanguage.English, "ui.population", "Population");

        Assert.Equal("Population", localization.Translate("ui.population"));
        Assert.Equal("missing.key", localization.Translate("missing.key"));
    }

    [Fact]
    public void LoadJson_Loads_Language_Table()
    {
        var localization = new LocalizationSystem();

        localization.LoadJson("""
            {
              "language": "ru",
              "strings": {
                "ui.support": "Поддержка"
              }
            }
            """);
        localization.SetLanguage(GameLanguage.Russian);

        Assert.True(localization.HasTranslation(GameLanguage.Russian, "ui.support"));
        Assert.Equal("Поддержка", localization.Translate("ui.support"));
    }

    [Fact]
    public void Data_Localization_Files_Are_Loadable()
    {
        var localization = new LocalizationSystem();
        var root = GetRepositoryRoot();

        localization.LoadJsonFile(Path.Combine(root, "data", "localization", "en.json"));
        localization.LoadJsonFile(Path.Combine(root, "data", "localization", "ru.json"));

        Assert.Equal("Budget", localization.Translate("ui.budget"));

        localization.SetLanguage(GameLanguage.Russian);
        Assert.Equal("Бюджет", localization.Translate("ui.budget"));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Codex_plan.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
