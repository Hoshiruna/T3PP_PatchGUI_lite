using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PatchGUIlite
{
    internal static class LocalizationManager
    {
        private const string DefaultLang = "zh_CN";
        private static readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
        public static string CurrentLanguage { get; private set; } = DefaultLang;

        public static void LoadLanguage(string langCode)
        {
            try
            {
                string? path = FindLanguageFile(langCode);
                if (path == null)
                {
                    if (!langCode.Equals(DefaultLang, StringComparison.OrdinalIgnoreCase))
                    {
                        LoadLanguage(DefaultLang);
                    }
                    return;
                }

                string json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();

                _strings.Clear();
                foreach (var kv in parsed)
                {
                    _strings[kv.Key] = kv.Value;
                }

                CurrentLanguage = langCode;
            }
            catch
            {
                // ignore localization load failures
            }
        }

        private static string? FindLanguageFile(string langCode)
        {
            string fileName = Path.Combine("lang", $"{langCode}.json");

            foreach (string? baseDir in GetLanguageBaseDirectories())
            {
                if (string.IsNullOrWhiteSpace(baseDir))
                    continue;

                string path = Path.Combine(baseDir, fileName);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static IEnumerable<string?> GetLanguageBaseDirectories()
        {
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
                yield return Path.GetDirectoryName(processPath);

            yield return AppContext.BaseDirectory;
            yield return AppDomain.CurrentDomain.BaseDirectory;
            yield return Environment.CurrentDirectory;
        }

        public static string Get(string key, string fallback)
        {
            if (_strings.TryGetValue(key, out var value))
                return value;
            return fallback;
        }
    }
}

