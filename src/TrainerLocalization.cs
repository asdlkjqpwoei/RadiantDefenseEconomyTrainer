using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public sealed class LocalizedMessage
{
    public string Key { get; private set; }
    public object[] Arguments { get; private set; }

    public LocalizedMessage(string key, params object[] arguments)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("A message key is required.", "key");
        Key = key;
        Arguments = arguments ?? new object[0];
    }

    public string Signature
    {
        get
        {
            StringBuilder signature = new StringBuilder(Key);
            foreach (object argument in Arguments)
            {
                signature.Append('\u001f');
                signature.Append(Convert.ToString(argument, CultureInfo.InvariantCulture));
            }
            return signature.ToString();
        }
    }
}

public sealed class TrainerLocalization
{
    public const string CONFIG_FILENAME = "trainer.yaml";

    static readonly string[] LANGUAGE_CODES = { "zh-CN", "en" };
    static readonly string[] REQUIRED_KEYS = {
        "language.name.zh-CN", "language.name.en", "app.title",
        "ui.language_label", "ui.game_status", "ui.game_not_found", "ui.detect",
        "ui.current_money", "ui.money_unavailable", "ui.set", "ui.lock",
        "ui.relocate", "ui.add_label", "ui.add", "ui.locate_group",
        "ui.locate_help", "ui.waiting_for_game", "ui.locating", "ui.relocating",
        "ui.waiting_for_locator", "ui.location_failed", "ui.read_failed",
        "ui.location_retrying", "ui.location_succeeded", "ui.admin_required",
        "ui.connected", "ui.process_detection_failed", "ui.hint",
        "ui.instructions", "ui.operation_steps", "status.ready",
        "status.process_detection_failed", "status.open_process_failed",
        "status.connected", "status.locator_task_failed", "status.locator_failed",
        "status.locator_succeeded", "status.balance_read_failed",
        "status.invalid_set_value", "status.set_value_range", "status.locating",
        "status.set_success", "status.set_success_with_warning", "status.set_failure", "status.add_value_range",
        "status.add_value_invalid", "status.read_current_failed",
        "status.add_overflow", "status.add_success", "status.add_success_with_warning", "status.add_failure",
        "status.lock_failure", "status.game_required", "status.relocating",
        "locator.module_missing", "locator.version_unsupported",
        "locator.instance_missing", "locator.money_uninitialized",
        "locator.multiple_instances", "locator.module_info_failed",
        "locator.process_info_failed", "locator.module_list_empty",
        "locator.unexpected", "error.write_mismatch",
        "error.page_protection_restore", "error.access_denied",
        "error.write_failed", "error.write_verify_failed",
        "error.unhandled_exception"
    };

    readonly Dictionary<string, Dictionary<string, string>> languages;
    readonly string configPath;
    string languageCode;

    public string DefaultLanguage { get; private set; }
    public int DefaultSetAmount { get; private set; }
    public int DefaultAddAmount { get; private set; }
    public string LanguageCode { get { return languageCode; } }
    public string ConfigPath { get { return configPath; } }

    TrainerLocalization(string path, Dictionary<string, string> root,
        Dictionary<string, Dictionary<string, string>> languageMap)
    {
        configPath = path;
        languages = languageMap;
        DefaultLanguage = root["default_language"];
        DefaultSetAmount = ParseAmount(root, "default_set_amount", true);
        DefaultAddAmount = ParseAmount(root, "default_add_amount", false);
        SetLanguage(DefaultLanguage);
    }

    public static TrainerLocalization LoadDefault()
    {
        string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
        if (File.Exists(basePath)) return Load(basePath);

        string current = Environment.CurrentDirectory;
        string[] candidates = {
            Path.Combine(current, CONFIG_FILENAME),
            Path.Combine(current, "config", CONFIG_FILENAME)
        };
        foreach (string candidate in candidates)
            if (File.Exists(candidate)) return Load(candidate);

        throw new FileNotFoundException(
            "The trainer configuration file was not found next to the application or in the working directory.",
            basePath);
    }

    public static TrainerLocalization Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A configuration path is required.", "path");
        if (!File.Exists(path)) throw new FileNotFoundException("The trainer configuration file was not found.", path);

        Dictionary<string, string> root = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, string>> languageMap =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        ParseYaml(path, root, languageMap);

        string defaultLanguage;
        if (!root.TryGetValue("default_language", out defaultLanguage))
            throw new InvalidDataException("The configuration is missing 'default_language'.");
        if (!languageMap.ContainsKey(defaultLanguage))
            throw new InvalidDataException("The configured default language is not present under 'languages'.");

        foreach (string code in LANGUAGE_CODES)
        {
            Dictionary<string, string> strings;
            if (!languageMap.TryGetValue(code, out strings))
                throw new InvalidDataException("The configuration must define both zh-CN and en languages.");
            foreach (string key in REQUIRED_KEYS)
                if (!strings.ContainsKey(key))
                    throw new InvalidDataException("Language '" + code + "' is missing the text key '" + key + "'.");
        }

        return new TrainerLocalization(Path.GetFullPath(path), root, languageMap);
    }

    public void SetLanguage(string code)
    {
        if (string.IsNullOrEmpty(code) || !languages.ContainsKey(code))
            throw new ArgumentException("The language is not defined in the trainer configuration.", "code");
        languageCode = code;
    }

    public string Get(string key)
    {
        return Get(languageCode, key);
    }

    public string Get(string code, string key)
    {
        Dictionary<string, string> strings;
        if (!languages.TryGetValue(code, out strings))
            throw new KeyNotFoundException("The language '" + code + "' is not defined.");
        string value;
        if (!strings.TryGetValue(key, out value))
            throw new KeyNotFoundException("The text key '" + key + "' is not defined for language '" + code + "'.");
        return value;
    }

    public string Format(string key, params object[] arguments)
    {
        try
        {
            object[] values = arguments ?? new object[0];
            object[] formattedValues = new object[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                LocalizedMessage nestedMessage = values[i] as LocalizedMessage;
                formattedValues[i] = nestedMessage == null ? values[i] : Format(nestedMessage);
            }
            return string.Format(CultureInfo.InvariantCulture, Get(key), formattedValues);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The configured text template '" + key + "' is invalid.", ex);
        }
    }

    public string Format(LocalizedMessage message)
    {
        if (message == null) throw new ArgumentNullException("message");
        return Format(message.Key, message.Arguments);
    }

    static int ParseAmount(Dictionary<string, string> root, string key, bool allowZero)
    {
        string value;
        int amount;
        if (!root.TryGetValue(key, out value) ||
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) ||
            (allowZero ? amount < 0 : amount <= 0))
            throw new InvalidDataException("The configuration value '" + key + "' must be a valid positive int32 amount.");
        return amount;
    }

    static void ParseYaml(string path, Dictionary<string, string> root,
        Dictionary<string, Dictionary<string, string>> languageMap)
    {
        // The external file uses a strict mapping/scalar YAML subset so no parser package is required.
        bool inLanguages = false;
        string currentLanguage = null;
        int lineNumber = 0;
        using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                int indent = 0;
                while (indent < line.Length && line[indent] == ' ') indent++;
                if (indent < line.Length && line[indent] == '\t')
                    throw YamlError(lineNumber, "Tabs are not allowed for indentation.");

                string content = line.Substring(indent).TrimEnd();
                if (content.Length == 0 || content.StartsWith("#", StringComparison.Ordinal)) continue;

                int colon = content.IndexOf(':');
                if (colon <= 0) throw YamlError(lineNumber, "Expected a mapping entry in 'key: value' form.");
                string key = content.Substring(0, colon).Trim();
                string rawValue = content.Substring(colon + 1).Trim();
                if (key.Length == 0) throw YamlError(lineNumber, "Mapping keys cannot be empty.");

                if (indent == 0)
                {
                    currentLanguage = null;
                    if (key == "languages")
                    {
                        if (rawValue.Length != 0) throw YamlError(lineNumber, "'languages' must be a mapping.");
                        inLanguages = true;
                        continue;
                    }
                    inLanguages = false;
                    AddEntry(root, key, ParseScalar(rawValue, lineNumber), lineNumber);
                    continue;
                }

                if (indent == 2 && inLanguages)
                {
                    if (rawValue.Length != 0) throw YamlError(lineNumber, "Language entries must be mappings.");
                    currentLanguage = key;
                    if (languageMap.ContainsKey(currentLanguage))
                        throw YamlError(lineNumber, "Duplicate language '" + currentLanguage + "'.");
                    languageMap.Add(currentLanguage, new Dictionary<string, string>(StringComparer.Ordinal));
                    continue;
                }

                if (indent == 4 && inLanguages && currentLanguage != null)
                {
                    AddEntry(languageMap[currentLanguage], key, ParseScalar(rawValue, lineNumber), lineNumber);
                    continue;
                }

                throw YamlError(lineNumber, "Unexpected indentation or mapping section.");
            }
        }
    }

    static void AddEntry(Dictionary<string, string> entries, string key, string value, int lineNumber)
    {
        if (entries.ContainsKey(key)) throw YamlError(lineNumber, "Duplicate key '" + key + "'.");
        entries.Add(key, value);
    }

    static string ParseScalar(string value, int lineNumber)
    {
        if (value.Length == 0) return string.Empty;
        if (value[0] == '"') return ParseDoubleQuotedScalar(value, lineNumber);
        if (value[0] == '\'') return ParseSingleQuotedScalar(value, lineNumber);

        int comment = value.IndexOf(" #", StringComparison.Ordinal);
        if (comment >= 0) value = value.Substring(0, comment).TrimEnd();
        return value;
    }

    static string ParseDoubleQuotedScalar(string value, int lineNumber)
    {
        StringBuilder result = new StringBuilder();
        for (int i = 1; i < value.Length; i++)
        {
            char current = value[i];
            if (current == '"')
            {
                ValidateScalarSuffix(value.Substring(i + 1), lineNumber);
                return result.ToString();
            }
            if (current != '\\')
            {
                result.Append(current);
                continue;
            }

            if (++i >= value.Length) throw YamlError(lineNumber, "Incomplete escape sequence.");
            switch (value[i])
            {
                case '"': result.Append('"'); break;
                case '\\': result.Append('\\'); break;
                case 'n': result.Append('\n'); break;
                case 'r': result.Append('\r'); break;
                case 't': result.Append('\t'); break;
                case 'u':
                    if (i + 4 >= value.Length) throw YamlError(lineNumber, "Incomplete Unicode escape.");
                    int codePoint;
                    if (!int.TryParse(value.Substring(i + 1, 4), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out codePoint))
                        throw YamlError(lineNumber, "Invalid Unicode escape.");
                    result.Append((char)codePoint);
                    i += 4;
                    break;
                default:
                    throw YamlError(lineNumber, "Unsupported double-quoted escape sequence.");
            }
        }
        throw YamlError(lineNumber, "Unterminated double-quoted scalar.");
    }

    static string ParseSingleQuotedScalar(string value, int lineNumber)
    {
        StringBuilder result = new StringBuilder();
        for (int i = 1; i < value.Length; i++)
        {
            if (value[i] != '\'')
            {
                result.Append(value[i]);
                continue;
            }
            if (i + 1 < value.Length && value[i + 1] == '\'')
            {
                result.Append('\'');
                i++;
                continue;
            }
            ValidateScalarSuffix(value.Substring(i + 1), lineNumber);
            return result.ToString();
        }
        throw YamlError(lineNumber, "Unterminated single-quoted scalar.");
    }

    static void ValidateScalarSuffix(string suffix, int lineNumber)
    {
        suffix = suffix.Trim();
        if (suffix.Length != 0 && !suffix.StartsWith("#", StringComparison.Ordinal))
            throw YamlError(lineNumber, "Unexpected text after quoted scalar.");
    }

    static InvalidDataException YamlError(int lineNumber, string message)
    {
        return new InvalidDataException("Invalid trainer.yaml at line " + lineNumber + ": " + message);
    }
}
