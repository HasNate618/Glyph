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
            // Reset engine to built-in state before applying YAML so removed bindings are cleared.
            engine.ResetToBuiltins();

            EnsureDefaultFileExists();

            var yaml = File.ReadAllText(KeymapsPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var root = deserializer.Deserialize<KeymapYamlRoot>(yaml);
            if ((root?.Bindings is null || root.Bindings.Count == 0) && (root?.Apps is null || root.Apps.Count == 0))
            {
                Logger.Info($"Keymaps: no bindings found in {KeymapsPath}");
                return;
            }

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

                    // Label the program layer for this process with the process name
                    // so the overlay shows the program name instead of a generic label.
                    engine.SetPerAppPrefixDescription(process, "p", process);

                    if (app?.Bindings is not { Count: > 0 })
                    {
                        // No bindings for this app; still set the label so the overlay
                        // displays the process name rather than a generic "Program".
                        continue;
                    }

                    foreach (var node in app.Bindings)
                    {
                        // Place program-specific bindings under the program layer 'p'.
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

                        // Use group.Name as the label if present, otherwise the process name.
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

    private static void EnsureDefaultFileExists()
    {
        var dir = Path.GetDirectoryName(KeymapsPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(KeymapsPath))
        {
            return;
        }
        // Require a repository default file when running from source so maintainers
        // can edit and verify defaults easily. Do NOT fall back to an embedded template.
        try
        {
            var repoDefault = Path.Combine(Directory.GetCurrentDirectory(), "src", "Glyph.App", "Config", "default_keymaps.yaml");
            if (File.Exists(repoDefault))
            {
                File.Copy(repoDefault, KeymapsPath);
                Logger.Info($"Copied repository default keymaps to: {KeymapsPath}");
                return;
            }

            Logger.Info($"Repository default keymaps not found at {repoDefault}; not creating {KeymapsPath}");
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
        var typeText = (node.Type ?? string.Empty).Trim();
        var sendSpec = (node.Send ?? string.Empty).Trim();
        var thenSpec = (node.Then ?? string.Empty).Trim();
        var execPath = (node.Exec ?? string.Empty).Trim();
        var execArgs = (node.ExecArgs ?? string.Empty).Trim();
        var execCwd = (node.ExecCwd ?? string.Empty).Trim();

        // Prefer explicit ordered `steps:` for chaining. Falls back to legacy single-field bindings
        // and the deprecated `then:` helper for simple two-step chains.
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
            // Legacy: Chain TypeText + SendSpec: type text, then send key
            engine.AddGlobalBinding(seq, new ActionRequest { Steps = new List<ActionRequest> { new ActionRequest { TypeText = typeText }, new ActionRequest { SendSpec = thenSpec } } }, label);
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

        // Allow multi-character keys like "mx".
        if (key.Any(char.IsWhiteSpace))
        {
            skippedInvalid++;
            return;
        }

        var seq = prefix + key;

        // App-specific prefixes are discoverable only when that process is active.
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

        // Prefer explicit ordered `steps:` for chaining. Falls back to legacy single-field bindings
        // and the deprecated `then:` helper for simple two-step chains.
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
            // Legacy: Chain TypeText + SendSpec: type text, then send key
            engine.AddPerAppBinding(processName, seq, new ActionRequest { Steps = new List<ActionRequest> { new ActionRequest { TypeText = typeText }, new ActionRequest { SendSpec = thenSpec } } }, label);
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

    // Embedded defaults removed; KeymapYamlLoader now requires a repository
    // default_keymaps.yaml to be present when initializing defaults.
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
    // Deprecated: use `steps:` for arbitrary chaining. Kept for backwards-compatibility.
    public string? Then { get; set; }
    // New: an ordered list of steps that will be executed sequentially. Each step
    // may contain an `action`, `type`, `send`, or `exec` entry.
    public List<KeymapYamlStep>? Steps { get; set; }
    // exec: program path to run
    public string? Exec { get; set; }
    // execArgs: arguments to pass to the program
    public string? ExecArgs { get; set; }
    // execCwd: working directory for the launched program
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
