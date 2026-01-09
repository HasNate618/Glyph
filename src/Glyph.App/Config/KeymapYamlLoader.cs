using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Glyph.Actions;
using Glyph.Core.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Logging;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Glyph.App.Config;

public interface IKeymapProvider
{
    string KeymapsPath { get; }
    void ApplyToEngine(SequenceEngine engine);
}

public sealed class YamlKeymapProvider : IKeymapProvider
{
    public string KeymapsPath { get; }

    private readonly string _repoDefaultPath;

    public YamlKeymapProvider(string? keymapsPath = null, string? repoDefaultPath = null)
    {
        KeymapsPath = string.IsNullOrWhiteSpace(keymapsPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glyph", "keymaps.yaml")
            : keymapsPath;

        _repoDefaultPath = string.IsNullOrWhiteSpace(repoDefaultPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "src", "Glyph.App", "Config", "default_keymaps.yaml")
            : repoDefaultPath;
    }

    public void ApplyToEngine(SequenceEngine engine)
    {
        try
        {
            EnsureDefaultFileExists();

            var yaml = File.ReadAllText(KeymapsPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            KeymapYamlRoot? root;
            try
            {
                root = deserializer.Deserialize<KeymapYamlRoot>(yaml);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse keymaps YAML: {KeymapsPath}", ex);
                return;
            }

            if ((root?.Bindings is null || root.Bindings.Count == 0) && (root?.Apps is null || root.Apps.Count == 0))
            {
                Logger.Info($"Keymaps: no bindings found in {KeymapsPath}");
                return;
            }

            // Only clear existing bindings after we've successfully parsed and validated the YAML
            engine.ClearAllBindings();

            var applied = 0;
            var skippedUnknown = 0;
            var skippedInvalid = 0;

            if (root?.Bindings is { Count: > 0 })
            {
                foreach (var node in root.Bindings)
                {
                    ApplyNode(engine, prefix: string.Empty, node, ref applied, ref skippedUnknown, ref skippedInvalid);
                }
            }

            if (root?.Apps is { Count: > 0 })
            {
                foreach (var app in root.Apps)
                {
                    var process = (app?.Process ?? string.Empty).Trim();
                    if (process.Length == 0)
                    {
                        skippedInvalid++;
                        continue;
                    }

                    engine.SetPerAppPrefixDescription(process, "p", process);

                    if (app?.Bindings is not { Count: > 0 })
                    {
                        continue;
                    }

                    foreach (var node in app.Bindings)
                    {
                        ApplyAppNode(engine, process, prefix: "p", node, ref applied, ref skippedUnknown, ref skippedInvalid);
                    }
                }
            }

            if (root?.Groups is { Count: > 0 })
            {
                foreach (var group in root.Groups)
                {
                    if (group is null || group.Processes is null || group.Processes.Count == 0 || group.Bindings is null || group.Bindings.Count == 0)
                    {
                        skippedInvalid++;
                        continue;
                    }

                    foreach (var process in group.Processes)
                    {
                        var proc = (process ?? string.Empty).Trim();
                        if (proc.Length == 0) continue;

                        var label = string.IsNullOrWhiteSpace(group.Name) ? proc : group.Name;
                        engine.SetPerAppPrefixDescription(proc, "p", label);

                        foreach (var node in group.Bindings)
                        {
                            ApplyAppNode(engine, proc, prefix: "p", node, ref applied, ref skippedUnknown, ref skippedInvalid);
                        }
                    }
                }
            }

            Logger.Info($"Keymaps loaded: applied={applied} unknownAction={skippedUnknown} invalid={skippedInvalid} ({KeymapsPath})");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load keymaps from {KeymapsPath}", ex);
        }
    }

    private void EnsureDefaultFileExists()
    {
        var dir = Path.GetDirectoryName(KeymapsPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(KeymapsPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_repoDefaultPath))
            {
                File.Copy(_repoDefaultPath, KeymapsPath);
                Logger.Info($"Copied repository default keymaps to: {KeymapsPath}");
                return;
            }

            Logger.Info($"Repository default keymaps not found at {_repoDefaultPath}; not creating {KeymapsPath}");
        }
        catch (Exception ex)
        {
            Logger.Info($"Could not copy repo default keymaps: {ex.Message}");
        }
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

        if (key.Any(char.IsWhiteSpace))
        {
            skippedInvalid++;
            return;
        }

        var seq = prefix + key;

        var existing = engine.GetPrefixDescription(seq);
        if (string.IsNullOrWhiteSpace(existing))
        {
            engine.SetPrefixDescription(seq, label);
        }
        else
        {
            if (!string.Equals(existing, label, StringComparison.Ordinal))
            {
                Logger.Info($"Keymaps: retained built-in label '{existing}' for '{seq}' (yaml: '{label}')");
            }
        }

        var actionId = (node.Action ?? string.Empty).Trim();
        var typeText = (node.Type ?? string.Empty).Trim();
        var sendSpec = (node.Send ?? string.Empty).Trim();
        var thenSpec = (node.Then ?? string.Empty).Trim();
        var execPath = (node.Exec ?? string.Empty).Trim();
        var execArgs = (node.ExecArgs ?? string.Empty).Trim();
        var execCwd = (node.ExecCwd ?? string.Empty).Trim();

        if (node.Steps is { Count: > 0 })
        {
            var steps = node.Steps.Select(s => new ActionRequest
            {
                ActionId = string.IsNullOrWhiteSpace(s.Action) ? null : s.Action,
                TypeText = string.IsNullOrWhiteSpace(s.Type) ? null : s.Type,
                SendSpec = string.IsNullOrWhiteSpace(s.Send) ? null : s.Send,
                ExecPath = string.IsNullOrWhiteSpace(s.Exec) ? null : s.Exec,
                ExecArgs = string.IsNullOrWhiteSpace(s.ExecArgs) ? null : s.ExecArgs,
                ExecCwd = string.IsNullOrWhiteSpace(s.ExecCwd) ? null : s.ExecCwd,
            }).ToList();

            engine.AddGlobalBinding(seq, new ActionRequest { Steps = steps }, label);
            applied++;
        }
        else if (actionId.Length > 0)
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
        else if (typeText.Length > 0 && thenSpec.Length > 0)
        {
            engine.AddGlobalBinding(seq, new ActionRequest { Steps = new List<ActionRequest> { new() { TypeText = typeText }, new() { SendSpec = thenSpec } } }, label);
            applied++;
        }
        else if (typeText.Length > 0)
        {
            engine.AddGlobalBinding(seq, new ActionRequest { TypeText = typeText }, label);
            applied++;
        }
        else if (sendSpec.Length > 0)
        {
            engine.AddGlobalBinding(seq, new ActionRequest { SendSpec = sendSpec }, label);
            applied++;
        }
        else if (execPath.Length > 0)
        {
            engine.AddGlobalBinding(seq, new ActionRequest { ExecPath = execPath, ExecArgs = string.IsNullOrWhiteSpace(execArgs) ? null : execArgs, ExecCwd = string.IsNullOrWhiteSpace(execCwd) ? null : execCwd }, label);
            applied++;
        }

        if (node.Children is { Count: > 0 })
        {
            foreach (var child in node.Children)
            {
                ApplyNode(engine, seq, child, ref applied, ref skippedUnknown, ref skippedInvalid);
            }
        }
    }

    private static void ApplyAppNode(
        SequenceEngine engine,
        string processName,
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

        if (key.Any(char.IsWhiteSpace))
        {
            skippedInvalid++;
            return;
        }

        var seq = prefix + key;

        var existing = engine.GetPerAppPrefixDescription(processName, seq);
        if (string.IsNullOrWhiteSpace(existing))
        {
            engine.SetPerAppPrefixDescription(processName, seq, label);
        }

        var actionId = (node.Action ?? string.Empty).Trim();
        var typeText = (node.Type ?? string.Empty).Trim();
        var sendSpec = (node.Send ?? string.Empty).Trim();
        var thenSpec = (node.Then ?? string.Empty).Trim();
        var execPath = (node.Exec ?? string.Empty).Trim();
        var execArgs = (node.ExecArgs ?? string.Empty).Trim();
        var execCwd = (node.ExecCwd ?? string.Empty).Trim();

        if (node.Steps is { Count: > 0 })
        {
            var steps = node.Steps.Select(s => new ActionRequest
            {
                ActionId = string.IsNullOrWhiteSpace(s.Action) ? null : s.Action,
                TypeText = string.IsNullOrWhiteSpace(s.Type) ? null : s.Type,
                SendSpec = string.IsNullOrWhiteSpace(s.Send) ? null : s.Send,
                ExecPath = string.IsNullOrWhiteSpace(s.Exec) ? null : s.Exec,
                ExecArgs = string.IsNullOrWhiteSpace(s.ExecArgs) ? null : s.ExecArgs,
                ExecCwd = string.IsNullOrWhiteSpace(s.ExecCwd) ? null : s.ExecCwd,
            }).ToList();

            engine.AddPerAppBinding(processName, seq, new ActionRequest { Steps = steps }, label);
            applied++;
        }
        else if (actionId.Length > 0)
        {
            if (!ActionRuntime.KnownActionIds.Contains(actionId))
            {
                skippedUnknown++;
                Logger.Info($"Keymaps: unknown action '{actionId}' for app '{processName}' '{seq}' ({label})");
            }
            else
            {
                engine.AddPerAppBinding(processName, seq, new ActionRequest(actionId), label);
                applied++;
            }
        }
        else if (typeText.Length > 0 && thenSpec.Length > 0)
        {
            engine.AddPerAppBinding(processName, seq, new ActionRequest { Steps = new List<ActionRequest> { new() { TypeText = typeText }, new() { SendSpec = thenSpec } } }, label);
            applied++;
        }
        else if (typeText.Length > 0)
        {
            engine.AddPerAppBinding(processName, seq, new ActionRequest { TypeText = typeText }, label);
            applied++;
        }
        else if (sendSpec.Length > 0)
        {
            engine.AddPerAppBinding(processName, seq, new ActionRequest { SendSpec = sendSpec }, label);
            applied++;
        }
        else if (execPath.Length > 0)
        {
            engine.AddPerAppBinding(processName, seq, new ActionRequest { ExecPath = execPath, ExecArgs = string.IsNullOrWhiteSpace(execArgs) ? null : execArgs, ExecCwd = string.IsNullOrWhiteSpace(execCwd) ? null : execCwd }, label);
            applied++;
        }

        if (node.Children is { Count: > 0 })
        {
            foreach (var child in node.Children)
            {
                ApplyAppNode(engine, processName, seq, child, ref applied, ref skippedUnknown, ref skippedInvalid);
            }
        }
    }
}

public static class KeymapYamlLoader
{
    public static string KeymapsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Glyph",
        "keymaps.yaml");

    public static void ApplyToEngine(SequenceEngine engine)
    {
        new YamlKeymapProvider(keymapsPath: KeymapsPath).ApplyToEngine(engine);
    }
}

public sealed class KeymapYamlRoot
{
    public List<KeymapYamlNode>? Bindings { get; set; }
    public List<KeymapYamlApp>? Apps { get; set; }
    public List<KeymapYamlGroup>? Groups { get; set; }
}

public sealed class KeymapYamlApp
{
    public string? Process { get; set; }
    public List<KeymapYamlNode>? Bindings { get; set; }
}

public sealed class KeymapYamlGroup
{
    public string? Name { get; set; }
    public List<string>? Processes { get; set; }
    public List<KeymapYamlNode>? Bindings { get; set; }
}

public sealed class KeymapYamlNode
{
    public string? Key { get; set; }
    public string? Label { get; set; }
    public string? Action { get; set; }
    public string? Type { get; set; }
    public string? Send { get; set; }
    public string? Then { get; set; }
    public List<KeymapYamlStep>? Steps { get; set; }
    public string? Exec { get; set; }
    public string? ExecArgs { get; set; }
    public string? ExecCwd { get; set; }
    public List<KeymapYamlNode>? Children { get; set; }
}

public sealed class KeymapYamlStep
{
    public string? Action { get; set; }
    public string? Type { get; set; }
    public string? Send { get; set; }
    public string? Exec { get; set; }
    public string? ExecArgs { get; set; }
    public string? ExecCwd { get; set; }
}
