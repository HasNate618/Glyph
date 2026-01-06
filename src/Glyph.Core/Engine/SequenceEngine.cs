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

        // Top-level (discoverable) prefixes
        global.SetDescription("r", "Run");

        // Run layer
        global.Add("rc", new ActionRequest("launchChrome"), "Chrome");
        global.Add("rt", new ActionRequest("openTerminal"), "Windows Terminal");
        global.Add("rf", new ActionRequest("openExplorer"), "File Explorer");
        global.Add("rm", new ActionRequest("openTaskManager"), "Task Manager");

        // App-specific bindings
        var perApp = new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase);

        // When Windows Terminal is focused, leader+n types `nvim .` + Enter
        var terminal = new Trie<ActionRequest>();
        terminal.SetDescription("n", "nvim .");
        terminal.Add("n", new ActionRequest("typeNvimDot"), "nvim . (enter)");
        perApp["WindowsTerminal"] = terminal;

        // Leader key: Ctrl+Shift+NumPad * (VK_MULTIPLY = 0x6A)
        Func<KeyStroke, bool> isLeader = stroke =>
            stroke.VkCode == 0x6A && stroke.Ctrl && stroke.Shift && !stroke.Alt;

        return new SequenceEngine(global, perApp, TimeSpan.FromMilliseconds(2000), isLeader);
    }

    public static SequenceEngine CreateWithLeaderKey(Func<KeyStroke, bool> isLeaderKey)
    {
        var global = new Trie<ActionRequest>();
        global.Add("rn", new ActionRequest("launchNotepad"), "Notepad");
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
        Logger.Info($"Buffer: {_buffer}");

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
        var options = (next ?? LookupMerged(_buffer, activeProcessName).NextKeys)
            .Select(k => new OverlayOption(k.Key.ToString(), k.Description))
            .ToList();

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
        var merged = new Dictionary<char, string>();
        foreach (var nk in global.NextKeys)
        {
            merged[nk.Key] = nk.Description;
        }

        foreach (var nk in app.Value.NextKeys)
        {
            merged[nk.Key] = nk.Description;
        }

        var nextKeys = merged
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new TrieNextKey(kvp.Key, kvp.Value))
            .ToList();

        return new TrieLookupResult<ActionRequest>(
            IsValidPrefix: true,
            IsComplete: global.IsComplete,
            Value: global.Value,
            NextKeys: nextKeys);
    }
}

public sealed record ActionRequest(string ActionId);

public sealed record OverlayModel(string Sequence, IReadOnlyList<OverlayOption> Options);

public sealed record OverlayOption(string Key, string Description);

public readonly record struct EngineResult(bool Consumed, OverlayModel? Overlay, ActionRequest? Action)
{
    public static EngineResult None => new(false, null, null);
    public static EngineResult ConsumedNoOverlay => new(true, null, null);
}
