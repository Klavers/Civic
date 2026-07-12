using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Civic.Simulation
{
    public sealed class CivicLocalizationException : Exception
    {
        public CivicLocalizationException(params string[] errors)
            : base(string.Join("\n", errors ?? Array.Empty<string>()))
        {
            Errors = errors ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Errors { get; }
    }

    public sealed class CivicLocalizationEntry
    {
        public CivicLocalizationEntry(string key, int version, string value, string source, int line)
        {
            Key = key;
            Version = version;
            Value = value;
            Source = source;
            Line = line;
        }

        public string Key { get; }
        public int Version { get; }
        public string Value { get; }
        public string Source { get; }
        public int Line { get; }
    }

    public sealed class CivicLocalizationCatalog
    {
        private static readonly Regex TokenPattern = new Regex(@"\$(?<name>[A-Z][A-Z0-9_]*)(?:\|(?<format>[^$]+))?\$", RegexOptions.Compiled);
        private static readonly Regex ConceptPattern = new Regex(@"\[(?<key>[A-Za-z0-9_.-]+)\]", RegexOptions.Compiled);
        private static readonly Regex ColorPattern = new Regex(@"#(?<name>positive|negative|emphasis)\s+(?<text>.*?)#!", RegexOptions.Compiled);

        private readonly IReadOnlyDictionary<string, CivicLocalizationEntry> entries;

        internal CivicLocalizationCatalog(string language, IReadOnlyDictionary<string, CivicLocalizationEntry> entries)
        {
            Language = language;
            this.entries = entries;
        }

        public string Language { get; }
        public IReadOnlyDictionary<string, CivicLocalizationEntry> Entries => entries;
        public bool Contains(string key) => !string.IsNullOrEmpty(key) && entries.ContainsKey(key);

        public string Resolve(string key, IReadOnlyDictionary<string, object> arguments = null, bool richText = true)
        {
            if (string.IsNullOrEmpty(key) || !entries.TryGetValue(key, out var entry)) return key ?? string.Empty;
            return Render(entry.Value, arguments ?? EmptyArguments.Instance, richText, 0);
        }

        public string ConceptDescription(string conceptKey, bool richText = true)
        {
            var descriptionKey = conceptKey + ".desc";
            return Contains(descriptionKey) ? Resolve(descriptionKey, null, richText) : conceptKey;
        }

        private string Render(string template, IReadOnlyDictionary<string, object> arguments, bool richText, int depth)
        {
            if (depth > 8) return template;
            var rendered = TokenPattern.Replace(template, match =>
            {
                var name = match.Groups["name"].Value;
                if (!arguments.TryGetValue(name, out var value)) return match.Value;
                return CivicLocalizationFormatter.Format(value, match.Groups["format"].Value, richText);
            });
            rendered = ConceptPattern.Replace(rendered, match =>
            {
                var conceptKey = match.Groups["key"].Value;
                var label = entries.TryGetValue(conceptKey, out var concept)
                    ? Render(concept.Value, EmptyArguments.Instance, false, depth + 1)
                    : conceptKey;
                return richText ? $"<link=\"{conceptKey}\"><u>{label}</u></link>" : label;
            });
            rendered = ColorPattern.Replace(rendered, match =>
            {
                if (!richText) return match.Groups["text"].Value;
                var color = match.Groups["name"].Value == "positive"
                    ? "#62C982"
                    : match.Groups["name"].Value == "negative" ? "#E36B6B" : "#E5C76B";
                return $"<color={color}>{match.Groups["text"].Value}</color>";
            });
            return rendered
                .Replace(CivicLocalizationParser.LiteralOpenBracket.ToString(), "[")
                .Replace(CivicLocalizationParser.LiteralCloseBracket.ToString(), "]");
        }

        private sealed class EmptyArguments : IReadOnlyDictionary<string, object>
        {
            public static readonly EmptyArguments Instance = new EmptyArguments();
            public int Count => 0;
            public IEnumerable<string> Keys => Array.Empty<string>();
            public IEnumerable<object> Values => Array.Empty<object>();
            public object this[string key] => throw new KeyNotFoundException();
            public bool ContainsKey(string key) => false;
            public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Enumerable.Empty<KeyValuePair<string, object>>().GetEnumerator();
            public bool TryGetValue(string key, out object value) { value = null; return false; }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    public static class CivicLocalizationParser
    {
        internal const char LiteralOpenBracket = '\uE000';
        internal const char LiteralCloseBracket = '\uE001';
        private static readonly Regex HeaderPattern = new Regex(@"^(?<language>[A-Za-z][A-Za-z0-9_]*):\s*$", RegexOptions.Compiled);
        private static readonly Regex EntryPattern = new Regex("^\\s+(?<key>[A-Za-z0-9_.-]+)(?::(?<version>[0-9]+))?\\s+\"(?<value>(?:[^\"\\\\]|\\\\.)*)\"\\s*$", RegexOptions.Compiled);

        public static CivicLocalizationCatalog Parse(params CivicLocalizationSource[] sources)
        {
            var errors = new List<string>();
            var entries = new Dictionary<string, CivicLocalizationEntry>(StringComparer.Ordinal);
            string language = null;
            foreach (var source in sources ?? Array.Empty<CivicLocalizationSource>())
            {
                var text = (source.Text ?? string.Empty).TrimStart('\uFEFF');
                var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                var sourceLanguage = string.Empty;
                for (var index = 0; index < lines.Length; index++)
                {
                    var line = lines[index];
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                    var header = HeaderPattern.Match(line);
                    if (header.Success && line.Length == line.TrimStart().Length)
                    {
                        sourceLanguage = header.Groups["language"].Value;
                        if (language == null) language = sourceLanguage;
                        else if (!string.Equals(language, sourceLanguage, StringComparison.Ordinal)) errors.Add($"{source.Name}:{index + 1}: language header must be {language}.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(sourceLanguage))
                    {
                        errors.Add($"{source.Name}:{index + 1}: localization entry appears before a language header.");
                        continue;
                    }

                    var match = EntryPattern.Match(line);
                    if (!match.Success)
                    {
                        errors.Add($"{source.Name}:{index + 1}: unsupported localization syntax.");
                        continue;
                    }

                    var key = match.Groups["key"].Value;
                    var version = match.Groups["version"].Success ? int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture) : 0;
                    var value = Unescape(match.Groups["value"].Value, source.Name, index + 1, errors);
                    if (entries.ContainsKey(key)) errors.Add($"{source.Name}:{index + 1}: duplicate localization key '{key}'.");
                    else entries.Add(key, new CivicLocalizationEntry(key, version, value, source.Name, index + 1));
                }
            }

            if (string.IsNullOrEmpty(language)) errors.Add("Localization catalog has no language header.");
            if (errors.Count > 0) throw new CivicLocalizationException(errors.ToArray());
            return new CivicLocalizationCatalog(language, entries);
        }

        private static string Unescape(string value, string source, int line, ICollection<string> errors)
        {
            var builder = new StringBuilder();
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character != '\\') { builder.Append(character); continue; }
                if (++index >= value.Length) { errors.Add($"{source}:{line}: trailing escape character."); break; }
                switch (value[index])
                {
                    case 'n': builder.Append('\n'); break;
                    case '\\': builder.Append('\\'); break;
                    case '"': builder.Append('"'); break;
                    case '[': builder.Append(LiteralOpenBracket); break;
                    case ']': builder.Append(LiteralCloseBracket); break;
                    default: errors.Add($"{source}:{line}: unsupported escape '\\{value[index]}'."); break;
                }
            }
            return builder.ToString();
        }
    }

    public readonly struct CivicLocalizationSource
    {
        public CivicLocalizationSource(string name, string text) { Name = string.IsNullOrEmpty(name) ? "<memory>" : name; Text = text ?? string.Empty; }
        public string Name { get; }
        public string Text { get; }
    }

    public static class CivicLocalizationFormatter
    {
        public static readonly IReadOnlyCollection<string> SupportedFormats = new[] { "text", "number", "integer", "percent", "multiplier", "duration" };

        public static string Format(object value, string specification, bool richText)
        {
            var parts = (specification ?? "text").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var format = parts.Length == 0 ? "text" : parts[0].Trim();
            var options = parts.Skip(1).Select(ParseOption).Where(item => item.Key.Length > 0).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
            if (format == "text" || value is string) return value?.ToString() ?? string.Empty;
            if (!TryNumber(value, out var number)) return value?.ToString() ?? string.Empty;
            var decimals = options.TryGetValue("decimals", out var decimalsText) && int.TryParse(decimalsText, out var parsed) ? Math.Max(0, Math.Min(4, parsed)) : 2;
            var numeric = format == "percent" ? number * 100d : format == "multiplier" ? 1d + number : number;
            var suffix = format == "percent" ? "%" : format == "multiplier" ? "×" : format == "duration" ? "초" : string.Empty;
            var prefix = format == "multiplier" ? suffix : string.Empty;
            if (format == "multiplier") suffix = string.Empty;
            var sign = options.TryGetValue("sign", out var signOption) ? signOption : "auto";
            var pattern = format == "integer" ? "0" : "0." + new string('#', decimals);
            var signText = sign == "always" && numeric > 0d ? "+" : sign == "never" ? string.Empty : numeric < 0d ? "-" : string.Empty;
            var absolute = sign == "never" || numeric < 0d ? Math.Abs(numeric) : numeric;
            var result = prefix + signText + absolute.ToString(pattern, CultureInfo.InvariantCulture) + suffix;
            if (!richText || !options.TryGetValue("good", out var good) || good == "neutral" || Math.Abs(number) < 1e-12d) return result;
            var positive = good == "up" ? number > 0d : good == "down" && number < 0d;
            return $"<color={(positive ? "#62C982" : "#E36B6B")}>{result}</color>";
        }

        public static void ValidateSpecification(string specification, string location, ICollection<string> errors)
        {
            var parts = (specification ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var format = parts.Length == 0 ? "text" : parts[0].Trim();
            if (!SupportedFormats.Contains(format)) errors.Add($"{location}: unknown FORMAT '{format}'.");
            foreach (var part in parts.Skip(1))
            {
                var option = ParseOption(part);
                if (option.Key == "sign" && option.Value != "auto" && option.Value != "always" && option.Value != "never") errors.Add($"{location}: invalid sign option '{option.Value}'.");
                else if (option.Key == "good" && option.Value != "up" && option.Value != "down" && option.Value != "neutral") errors.Add($"{location}: invalid good option '{option.Value}'.");
                else if (option.Key == "decimals" && (!int.TryParse(option.Value, out var decimals) || decimals < 0 || decimals > 4)) errors.Add($"{location}: decimals must be 0..4.");
                else if (option.Key != "sign" && option.Key != "good" && option.Key != "decimals") errors.Add($"{location}: unknown FORMAT option '{option.Key}'.");
            }
        }

        private static KeyValuePair<string, string> ParseOption(string part)
        {
            var separator = (part ?? string.Empty).IndexOf('=');
            return separator <= 0
                ? new KeyValuePair<string, string>(string.Empty, string.Empty)
                : new KeyValuePair<string, string>(part.Substring(0, separator).Trim(), part.Substring(separator + 1).Trim());
        }

        private static bool TryNumber(object value, out double number)
        {
            if (value is CivicNumber civic) { number = civic.ToDouble(); return true; }
            try { number = Convert.ToDouble(value, CultureInfo.InvariantCulture); return true; }
            catch { number = 0d; return false; }
        }
    }

    public static class CivicLocalizationService
    {
        private static CivicLocalizationCatalog catalog;

        public static CivicLocalizationCatalog Catalog => catalog ?? (catalog = LoadDefault());

        public static CivicLocalizationCatalog LoadDefault()
        {
            var assets = Resources.LoadAll<TextAsset>("Localization/Korean");
            if (assets == null || assets.Length == 0) throw new CivicLocalizationException("No Korean localization TextAsset was found at Resources/Localization/Korean.");
            return CivicLocalizationParser.Parse(assets.OrderBy(item => item.name, StringComparer.Ordinal).Select(item => new CivicLocalizationSource(item.name, item.text)).ToArray());
        }

        public static void ResetForTests(CivicLocalizationCatalog replacement = null) => catalog = replacement;
    }

    public static class CivicLocalizationValidator
    {
        private static readonly Regex TokenPattern = new Regex(@"\$(?<name>[A-Z][A-Z0-9_]*)(?:\|(?<format>[^$]+))?\$", RegexOptions.Compiled);
        private static readonly Regex UnclosedTokenPattern = new Regex(@"\$[A-Z][A-Z0-9_]*(?=[^A-Z0-9_|$]|$)", RegexOptions.Compiled);
        private static readonly Regex ConceptPattern = new Regex(@"\[(?<key>[A-Za-z0-9_.-]+)\]", RegexOptions.Compiled);
        private static readonly string[] RequiredUiKeys =
        {
            "event.choice.no_effect",
            "condition.status_met",
            "condition.status_unmet",
            "tooltip.continue",
            "tooltip.depth_limit",
        };

        public static void Validate(CivicLocalizationCatalog catalog, IEnumerable<string> requiredEffectTypes)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var errors = new List<string>();
            foreach (var effectType in (requiredEffectTypes ?? Array.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal))
            {
                var key = "effect." + effectType;
                if (!catalog.Contains(key)) errors.Add("Missing localization key: " + key);
            }
            foreach (var key in RequiredUiKeys)
            {
                if (!catalog.Contains(key)) errors.Add("Missing localization key: " + key);
            }

            foreach (var entry in catalog.Entries.Values)
            {
                foreach (Match token in UnclosedTokenPattern.Matches(entry.Value))
                {
                    errors.Add($"{entry.Source}:{entry.Line}:{entry.Key}: unclosed localization token '{token.Value}'. Use ${token.Value.Substring(1)}$.");
                }
                foreach (Match token in TokenPattern.Matches(entry.Value))
                {
                    CivicLocalizationFormatter.ValidateSpecification(token.Groups["format"].Value, $"{entry.Source}:{entry.Line}:{entry.Key}", errors);
                }
                foreach (Match concept in ConceptPattern.Matches(entry.Value))
                {
                    var key = concept.Groups["key"].Value;
                    if (!catalog.Contains(key)) errors.Add($"{entry.Source}:{entry.Line}:{entry.Key}: missing concept label '{key}'.");
                    if (!catalog.Contains(key + ".desc")) errors.Add($"{entry.Source}:{entry.Line}:{entry.Key}: missing concept description '{key}.desc'.");
                }
            }

            if (errors.Count > 0) throw new CivicLocalizationException(errors.ToArray());
        }
    }
}
