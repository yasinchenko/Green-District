using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GreenDistrict.Simulation.Localization;

public enum GameLanguage
{
    English,
    Russian
}

/// <summary>
/// Stores UI/game text translations and resolves strings for the active language.
/// </summary>
public class LocalizationSystem
{
    private readonly Dictionary<GameLanguage, Dictionary<string, string>> _translations = new();

    public GameLanguage CurrentLanguage { get; private set; }
    public GameLanguage FallbackLanguage { get; }

    public LocalizationSystem(GameLanguage defaultLanguage = GameLanguage.English, GameLanguage fallbackLanguage = GameLanguage.English)
    {
        CurrentLanguage = defaultLanguage;
        FallbackLanguage = fallbackLanguage;
    }

    public void SetLanguage(GameLanguage language)
    {
        CurrentLanguage = language;
    }

    public void AddTranslation(GameLanguage language, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Translation key cannot be empty.", nameof(key));
        if (value == null) throw new ArgumentNullException(nameof(value));

        if (!_translations.TryGetValue(language, out var languageTable))
        {
            languageTable = new Dictionary<string, string>(StringComparer.Ordinal);
            _translations[language] = languageTable;
        }

        languageTable[key] = value;
    }

    public void LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Localization JSON cannot be empty.", nameof(json));

        var document = JsonSerializer.Deserialize<LocalizationDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (document == null) throw new InvalidOperationException("Localization JSON is invalid.");
        if (!TryParseLanguage(document.Language, out var language))
        {
            throw new InvalidOperationException($"Unsupported localization language '{document.Language}'.");
        }

        foreach (var pair in document.Strings)
        {
            AddTranslation(language, pair.Key, pair.Value);
        }
    }

    public void LoadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Localization path cannot be empty.", nameof(path));
        LoadJson(File.ReadAllText(path));
    }

    public string Translate(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        if (TryGet(CurrentLanguage, key, out var value)) return value;
        if (CurrentLanguage != FallbackLanguage && TryGet(FallbackLanguage, key, out value)) return value;

        return key;
    }

    public bool HasTranslation(GameLanguage language, string key)
    {
        return TryGet(language, key, out _);
    }

    private bool TryGet(GameLanguage language, string key, out string value)
    {
        value = string.Empty;
        if (!_translations.TryGetValue(language, out var languageTable)) return false;
        return languageTable.TryGetValue(key, out value!);
    }

    private static bool TryParseLanguage(string? language, out GameLanguage gameLanguage)
    {
        gameLanguage = GameLanguage.English;
        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "english", StringComparison.OrdinalIgnoreCase))
        {
            gameLanguage = GameLanguage.English;
            return true;
        }

        if (string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "russian", StringComparison.OrdinalIgnoreCase))
        {
            gameLanguage = GameLanguage.Russian;
            return true;
        }

        return false;
    }

    private sealed class LocalizationDocument
    {
        public string? Language { get; set; }
        public Dictionary<string, string> Strings { get; set; } = new();
    }
}
