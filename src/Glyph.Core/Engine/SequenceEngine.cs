using Glyph.Core.Input;
using Glyph.Core.Logging;

namespace Glyph.Core.Engine;

public sealed class SequenceEngine
{
    private readonly Trie<ActionRequest> _global;
    private readonly Dictionary<string, Trie<ActionRequest>> _perApp;
    private readonly TimeSpan _timeout;
    private readonly Func<KeyStroke, bool> _isLeaderKey;

    private bool _active;
    private string _buffer = string.Empty;
    private DateTimeOffset _lastInput;

    private SequenceEngine(
        Trie<ActionRequest> global,
        Dictionary<string, Trie<ActionRequest>> perApp,
        TimeSpan timeout,
        Func<KeyStroke, bool> isLeaderKey)
    {
        _global = global;
        _perApp = perApp;
        _timeout = timeout;
        _isLeaderKey = isLeaderKey;
    }

    public static SequenceEngine CreatePrototype()
    {
        var global = new Trie<ActionRequest>();

        // App-specific bindings (populated via YAML at runtime)
        var perApp = new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase);

        // Leader key: CapsLock (VK_CAPITAL = 0x14)
        Func<KeyStroke, bool> isLeader = stroke =>
            stroke.VkCode == 0x14 && !stroke.Ctrl && !stroke.Shift && !stroke.Alt && !stroke.Win;

        return new SequenceEngine(global, perApp, TimeSpan.FromMilliseconds(2000), isLeader);
    }

    public bool IsSessionActive => _active;

    public EngineResult BeginSession(DateTimeOffset now, string? activeProcessName)
    {
        _lastInput = now;
        _active = true;
        _buffer = string.Empty;

        return new EngineResult(
            Consumed: true,
            Overlay: BuildOverlay(activeProcessName),
            Action: null);
    }

    public static SequenceEngine CreatePrototype(Func<KeyStroke, bool> isLeaderKey)
    {
        var global = new Trie<ActionRequest>();

        // App-specific bindings (populated via YAML at runtime)
        var perApp = new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase);

        return new SequenceEngine(global, perApp, TimeSpan.FromMilliseconds(2000), isLeaderKey);
    }

    public void SetPrefixDescription(string prefix, string description)
    {
        _global.SetDescription(prefix, description);
    }

    public string? GetPrefixDescription(string prefix)
    {
        return _global.GetDescription(prefix);
    }

    public void AddGlobalBinding(string sequence, ActionRequest action, string description)
    {
        _global.Add(sequence, action, description);
    }

    public void SetPerAppPrefixDescription(string processName, string prefix, string description)
    {
        if (string.IsNullOrWhiteSpace(processName)) throw new ArgumentException("Process name must be non-empty", nameof(processName));
        if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("Prefix must be non-empty", nameof(prefix));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description must be non-empty", nameof(description));

        if (!_perApp.TryGetValue(processName, out var trie))
        {
            trie = new Trie<ActionRequest>();
            _perApp[processName] = trie;
        }

        trie.SetDescription(prefix, description);
    }

    public void ClearAllBindings()
    {
        // Clear existing global bindings and per-app bindings.
        // All keymaps should come from YAML so behavior is transparent and fully customizable.
        _global.Clear();
        _perApp.Clear();
    }

    public string? GetPerAppPrefixDescription(string processName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(processName)) return null;
        if (string.IsNullOrWhiteSpace(prefix)) return null;
        if (!_perApp.TryGetValue(processName, out var trie)) return null;
        return trie.GetDescription(prefix);
    }

    public void AddPerAppBinding(string processName, string sequence, ActionRequest action, string description)
    {
        if (string.IsNullOrWhiteSpace(processName)) throw new ArgumentException("Process name must be non-empty", nameof(processName));
        if (string.IsNullOrWhiteSpace(sequence)) throw new ArgumentException("Sequence must be non-empty", nameof(sequence));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description must be non-empty", nameof(description));

        if (!_perApp.TryGetValue(processName, out var trie))
        {
            trie = new Trie<ActionRequest>();
            _perApp[processName] = trie;
        }

        trie.Add(sequence, action, description);
    }

    public static SequenceEngine CreateWithLeaderKey(Func<KeyStroke, bool> isLeaderKey)
    {
        var global = new Trie<ActionRequest>();
        return new SequenceEngine(global, new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase), TimeSpan.FromMilliseconds(2000), isLeaderKey);
    }

    public EngineResult Handle(KeyStroke stroke, DateTimeOffset now, string? activeProcessName)
    {
        // Removed session timeout logic; users have unlimited time to read keys.
        _lastInput = now;

        // Leader gesture
        if (!_active)
        {
            if (_isLeaderKey(stroke))
            {
                Logger.Info($"Session started (leader key: {stroke.Key ?? '?'})");
                _active = true;
                _buffer = string.Empty;
                return new EngineResult(
                    Consumed: true,
                    Overlay: BuildOverlay(activeProcessName),
                    Action: null);
            }

            return EngineResult.None;
        }

        // Double-press leader key (CapsLock twice) triggers actual CapsLock
        if (_isLeaderKey(stroke) && string.IsNullOrEmpty(_buffer))
        {
            Logger.Info("Leader double-press detected, toggling CapsLock");
            Reset();
            return new EngineResult(
                Consumed: true,
                Overlay: null,
                Action: new ActionRequest { SendSpec = "CapsLock" });
        }

        // While active, Esc cancels.
        if (stroke.Key == '\u001B')
        {
            Reset();
            return EngineResult.ConsumedNoOverlay;
        }

        // Ignore non-text keys during session (prototype).
        if (stroke.Key is null || stroke.Key == ' ')
        {
            return new EngineResult(Consumed: true, Overlay: BuildOverlay(activeProcessName), Action: null);
        }

        _buffer += stroke.Key.Value;

        var lookup = LookupMerged(_buffer, activeProcessName);
        if (!lookup.IsValidPrefix)
        {
            Logger.Info($"Invalid sequence: {_buffer}");
            Reset();
            return EngineResult.ConsumedNoOverlay;
        }

        if (lookup.IsComplete && lookup.Value is not null)
        {
            var action = lookup.Value;
            Logger.Info($"Complete sequence: {_buffer} -> {action.ActionId}");
            Reset();
            return new EngineResult(Consumed: true, Overlay: null, Action: action);
        }

        return new EngineResult(Consumed: true, Overlay: BuildOverlay(activeProcessName, lookup.NextKeys), Action: null);
    }

    private void Reset()
    {
        _active = false;
        _buffer = string.Empty;
    }

    private OverlayModel BuildOverlay(string? activeProcessName, IReadOnlyList<TrieNextKey>? next = null)
    {
        var nextKeys = (next ?? LookupMerged(_buffer, activeProcessName).NextKeys);

        var options = nextKeys
            .Select(k =>
            {
                var desc = k.Description;

                // If this is the top-level program prefix ('p') and there's no per-app
                // description, prefer the active process name instead of the generic
                // "Program" label. Only substitute when the current buffer is empty
                // (i.e. the option represents the program layer itself) to avoid
                // overwriting unrelated 'p' keys in deeper sublayers.
                if (k.Key == 'p' && string.IsNullOrEmpty(_buffer) && !string.IsNullOrWhiteSpace(activeProcessName))
                {
                    var per = GetPerAppPrefixDescription(activeProcessName, "p");
                    if (!string.IsNullOrWhiteSpace(per))
                    {
                        desc = per;
                    }
                    else
                    {
                        desc = activeProcessName!;
                    }
                }

                return new OverlayOption(
                    Key: k.Key.ToString(),
                    Description: desc,
                    IsLayer: k.Continues,
                    IsAction: k.Completes);
            })
            .ToList();

        // If the user has entered a layer that contains no bindings, show a helpful message.
        if (options.Count == 0)
        {
            var seq = $"Leader {_buffer}".TrimEnd();
            string msg;
            // If this is the program layer (p) and we have an active process, mention it.
            if (!string.IsNullOrWhiteSpace(activeProcessName) && _buffer.StartsWith("p", StringComparison.Ordinal))
            {
                msg = $"No keys defined for {activeProcessName}";
            }
            else
            {
                msg = "No keys defined for this layer";
            }

            return new OverlayModel(seq, new List<OverlayOption> { new OverlayOption("—", msg, false, false) });
        }

        return new OverlayModel($"Leader {_buffer}".TrimEnd(), options);
    }

    private TrieLookupResult<ActionRequest> LookupMerged(string prefix, string? activeProcessName)
    {
        TrieLookupResult<ActionRequest>? app = null;
        if (!string.IsNullOrWhiteSpace(activeProcessName) && _perApp.TryGetValue(activeProcessName, out var appTrie))
        {
            app = appTrie.Lookup(prefix);
        }

        var global = _global.Lookup(prefix);

        if (app is null)
        {
            return global;
        }

        // If app layer says prefix invalid, fall back to global validity.
        if (!app.Value.IsValidPrefix)
        {
            return global;
        }

        // Completion precedence: app overrides global.
        if (app.Value.IsComplete && app.Value.Value is not null)
        {
            return app.Value;
        }

        // Merge next keys (union), but prefer app descriptions when overlapping.
        var merged = new Dictionary<char, TrieNextKey>();
        foreach (var nk in global.NextKeys)
        {
            merged[nk.Key] = nk;
        }

        foreach (var nk in app.Value.NextKeys)
        {
            if (merged.TryGetValue(nk.Key, out var existing))
            {
                // Merge flags, but prefer app description when present.
                merged[nk.Key] = new TrieNextKey(
                    Key: nk.Key,
                    Description: string.IsNullOrWhiteSpace(nk.Description) ? existing.Description : nk.Description,
                    Continues: nk.Continues || existing.Continues,
                    Completes: nk.Completes || existing.Completes);
            }
            else
            {
                merged[nk.Key] = nk;
            }
        }

        var nextKeys = merged
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => kvp.Value)
            .ToList();

        return new TrieLookupResult<ActionRequest>(
            IsValidPrefix: true,
            IsComplete: global.IsComplete,
            Value: global.Value,
            NextKeys: nextKeys);
    }
}
public sealed class ActionRequest
{
    public string? ActionId { get; init; }
    public string? TypeText { get; init; }
    public string? SendSpec { get; init; }

    // Support chaining of multiple ActionRequests (executed in order)
    public List<ActionRequest>? Steps { get; init; }

    // Exec support
    public string? ExecPath { get; init; }
    public string? ExecArgs { get; init; }
    public string? ExecCwd { get; init; }

    public ActionRequest() { }
    public ActionRequest(string actionId) => ActionId = actionId;
}

public sealed record OverlayModel(string Sequence, IReadOnlyList<OverlayOption> Options);

public sealed record OverlayOption(string Key, string Description, bool IsLayer, bool IsAction);

public readonly record struct EngineResult(bool Consumed, OverlayModel? Overlay, ActionRequest? Action)
{
    public static EngineResult None => new(false, null, null);
    public static EngineResult ConsumedNoOverlay => new(true, null, null);
}
