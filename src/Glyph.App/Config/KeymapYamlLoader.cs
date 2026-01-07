using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Glyph.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Logging;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Glyph.App.Config;

public static class KeymapYamlLoader
{
    public static string KeymapsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "keymaps.yaml");

    public static void ApplyToEngine(SequenceEngine engine)
    {
        try
        {
            EnsureDefaultFileExists();

            var yaml = File.ReadAllText(KeymapsPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var root = deserializer.Deserialize<KeymapYamlRoot>(yaml);
            if (root?.Bindings is null || root.Bindings.Count == 0)
            {
                Logger.Info($"Keymaps: no bindings found in {KeymapsPath}");
                return;
            }

            var applied = 0;
            var skippedUnknown = 0;
            var skippedInvalid = 0;

            foreach (var node in root.Bindings)
            {
                ApplyNode(engine, prefix: string.Empty, node, ref applied, ref skippedUnknown, ref skippedInvalid);
            }

            Logger.Info($"Keymaps loaded: applied={applied} unknownAction={skippedUnknown} invalid={skippedInvalid} ({KeymapsPath})");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load keymaps from {KeymapsPath}", ex);
        }
    }

    private static void EnsureDefaultFileExists()
    {
        var dir = Path.GetDirectoryName(KeymapsPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(KeymapsPath))
        {
            return;
        }

        File.WriteAllText(KeymapsPath, DefaultYaml);
        Logger.Info($"Created default keymaps file: {KeymapsPath}");
    }

    private static void ApplyNode(
        SequenceEngine engine,
        string prefix,
        KeymapYamlNode node,
        ref int applied,
        ref int skippedUnknown,
        ref int skippedInvalid)
    {
        if (node is null)
        {
            skippedInvalid++;
            return;
        }

        var key = (node.Key ?? string.Empty).Trim();
        var label = (node.Label ?? string.Empty).Trim();

        if (key.Length == 0 || label.Length == 0)
        {
            skippedInvalid++;
            return;
        }

        // Allow multi-character keys like "mx" or "wmx".
        if (key.Any(char.IsWhiteSpace))
        {
            skippedInvalid++;
            return;
        }

        var seq = prefix + key;

        // Ensure the prefix exists and is discoverable. Do not overwrite an
        // existing description created by built-ins unless the config
        // explicitly changes it (preserve user-friendly defaults).
        var existing = engine.GetPrefixDescription(seq);
        if (string.IsNullOrWhiteSpace(existing))
        {
            engine.SetPrefixDescription(seq, label);
        }
        else
        {
            // If the YAML label differs from the existing one, log but do not override.
            if (!string.Equals(existing, label, StringComparison.Ordinal))
            {
                Logger.Info($"Keymaps: retained built-in label '{existing}' for '{seq}' (yaml: '{label}')");
            }
        }

        var actionId = (node.Action ?? string.Empty).Trim();
        if (actionId.Length > 0)
        {
            if (!ActionRuntime.KnownActionIds.Contains(actionId))
            {
                skippedUnknown++;
                Logger.Info($"Keymaps: unknown action '{actionId}' for '{seq}' ({label})");
            }
            else
            {
                engine.AddGlobalBinding(seq, new ActionRequest(actionId), label);
                applied++;
            }
        }

        if (node.Children is { Count: > 0 })
        {
            foreach (var child in node.Children)
            {
                ApplyNode(engine, seq, child, ref applied, ref skippedUnknown, ref skippedInvalid);
            }
        }
    }

    private const string DefaultYaml =
        "# Glyph keymaps\n" +
        "#\n" +
        "# This file is loaded on startup from:\n" +
        "#   %APPDATA%\\Glyph\\keymaps.yaml\n" +
        "#\n" +
        "# Schema:\n" +
        "# bindings: [ { key, label, action?, children? } ]\n" +
        "#\n" +
        "# Notes:\n" +
        "# - key can be multi-character (ex: \"mx\"), which is useful for sequences like wmx.\n" +
        "# - action must be a known action id; unknown actions are skipped (but the prefix label stays).\n" +
        "#\n" +
        "bindings:\n" +
        "  - key: r\n" +
        "    label: Run\n" +
        "    children:\n" +
        "      - key: b\n" +
        "        label: Open Browser\n" +
        "        action: openBrowser\n" +
        "      - key: t\n" +
        "        label: Windows Terminal\n" +
        "        action: openTerminal\n" +
        "  - key: m\n" +
        "    label: Media\n" +
        "    children:\n" +
        "      - key: p\n" +
        "        label: Play / Pause\n" +
        "        action: mediaPlayPause\n" +
        "      - key: n\n" +
        "        label: Next Track\n" +
        "        action: mediaNext\n" +
        "      - key: b\n" +
        "        label: Previous Track\n" +
        "        action: mediaPrev\n" +
        "      - key: v\n" +
        "        label: Toggle Volume Mute\n" +
        "        action: volumeMute\n" +
        "      - key: m\n" +
        "        label: Toggle Microphone Mute\n" +
        "        action: muteMic\n" +
        "  - key: w\n" +
        "    label: Window\n" +
        "    children:\n" +
        "      - key: n\n" +
        "        label: Minimize\n" +
        "        action: windowMinimize\n" +
        "      - key: mx\n" +
        "        label: Maximize\n" +
        "        action: windowMaximize\n" +
        "      - key: r\n" +
        "        label: Restore\n" +
        "        action: windowRestore\n" +
        "      - key: c\n" +
        "        label: Close\n" +
        "        action: windowClose\n" +
        "      - key: t\n" +
        "        label: Toggle Topmost\n" +
        "        action: windowTopmost\n";
}

public sealed class KeymapYamlRoot
{
    public List<KeymapYamlNode>? Bindings { get; set; }
}

public sealed class KeymapYamlNode
{
    public string? Key { get; set; }
    public string? Label { get; set; }
    public string? Action { get; set; }
    public List<KeymapYamlNode>? Children { get; set; }
}
