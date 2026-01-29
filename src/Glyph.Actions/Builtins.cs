using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Glyph.Core.Logging;

namespace Glyph.Actions;

public static class Builtins
{
    // Matches {{now:format}} or {{utcnow:format}} or {{date:format}}
    private static readonly Regex NowToken = new Regex(@"\{\{(utcnow|now|date)(:(.+?))?\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ResolveBuiltins(string input, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        culture ??= CultureInfo.CurrentCulture;

        try
        {
            // Temporary escape sequence: allow literal '{{' by writing '{{{{' in YAML.
            // Replace '{{{{' with a placeholder token that won't match the regex, then restore after replacements.
            const string ESC_PLACEHOLDER = "__GLYPH_ESC_LBRACE__";
            var working = input.Replace("{{{{", ESC_PLACEHOLDER);

            var result = NowToken.Replace(working, m =>
            {
                try
                {
                    var kind = m.Groups[1].Value.ToLowerInvariant();
                    var fmt = m.Groups[3].Success ? m.Groups[3].Value : null;

                    DateTime dt = kind == "utcnow" ? DateTime.UtcNow : DateTime.Now;

                    if (string.IsNullOrEmpty(fmt))
                    {
                        // Default: general format
                        return dt.ToString("G", culture);
                    }

                    // Use user-specified .NET format string
                    return dt.ToString(fmt, culture);
                }
                catch (FormatException fx)
                {
                    Logger.Error("Date format error in Builtins.ResolveBuiltins", fx);
                    // Fallback to ISO round-trip format
                    return DateTime.Now.ToString("o", culture);
                }
                catch (Exception ex)
                {
                    Logger.Error("Unexpected error in Builtins.ResolveBuiltins", ex);
                    return string.Empty;
                }
            });

            // Restore escaped literal
            result = result.Replace(ESC_PLACEHOLDER, "{{");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to resolve builtins", ex);
            return input;
        }
    }
}
