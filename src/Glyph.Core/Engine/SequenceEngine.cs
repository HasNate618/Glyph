using Glyph.Core.Actions;
using Glyph.Core.Input;
using Glyph.Core.Logging;
using Glyph.Core.Overlay;

namespace Glyph.Core.Engine;

public sealed class SequenceEngine
{
    private readonly Trie<ActionRequest> _global;
    private readonly Dictionary<string, Trie<ActionRequest>> _perApp;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _disambiguationTimeout;
    private readonly Func<KeyStroke, bool> _isGlyphKey;

    private bool _active;
    private string _buffer = string.Empty;
    private DateTimeOffset _lastInput;

    private readonly OverlayPolicy _overlayPolicy;

    private SequenceEngine(
        Trie<ActionRequest> global,
        Dictionary<string, Trie<ActionRequest>> perApp,
        TimeSpan timeout,
        Func<KeyStroke, bool> isGlyphKey,
        bool glyphDoublePressTogglesCaps)
    {
        _global = global;
        _perApp = perApp;
        _timeout = timeout;
        _disambiguationTimeout = TimeSpan.FromMilliseconds(250);
        _isGlyphKey = isGlyphKey;
        _overlayPolicy = OverlayPolicy.Default;
    }

    public static SequenceEngine CreatePrototype()
    {
        var global = new Trie<ActionRequest>();

        // App-specific bindings (populated via YAML at runtime)
        var perApp = new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase);

        // Glyph key: CapsLock (VK_CAPITAL = 0x14)
        Func<KeyStroke, bool> isGlyph = stroke =>
            stroke.VkCode == 0x14 && !stroke.Ctrl && !stroke.Shift && !stroke.Alt && !stroke.Win;

        // Preserve legacy behavior: no-arg factory treats CapsLock glyph as toggling CapsLock on double-press.
        return new SequenceEngine(global, perApp, TimeSpan.FromMilliseconds(2000), isGlyph, true);
    }

    public bool IsSessionActive => _active;

    public void EndSession()
    {
        Reset();
    }

    public EngineResult BeginSession(DateTimeOffset now, string? activeProcessName)
    {
        _lastInput = now;
        _active = true;
        _buffer = string.Empty;

        return new EngineResult(
            Consumed: true,
            Overlay: BuildOverlay(activeProcessName),
            Action: null,
            ExecuteAfter: null,
            ForceHide: false,
            HideAfterSustain: false);
    }

    public static SequenceEngine CreatePrototype(Func<KeyStroke, bool> isGlyphKey)
    {
        var global = new Trie<ActionRequest>();

        // App-specific bindings (populated via YAML at runtime)
        var perApp = new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase);

        // By default do not toggle CapsLock on double-press for custom glyphs.
        return new SequenceEngine(global, perApp, TimeSpan.FromMilliseconds(2000), isGlyphKey, false);
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

    public static SequenceEngine CreateWithGlyphKey(Func<KeyStroke, bool> isGlyphKey)
    {
        var global = new Trie<ActionRequest>();
        return new SequenceEngine(global, new Dictionary<string, Trie<ActionRequest>>(StringComparer.OrdinalIgnoreCase), TimeSpan.FromMilliseconds(2000), isGlyphKey, false);
    }

    public EngineResult Handle(KeyStroke stroke, DateTimeOffset now, string? activeProcessName)
    {
        // Removed session timeout logic; users have unlimited time to read keys.
        _lastInput = now;

        // Glyph gesture
        if (!_active)
        {
            if (_isGlyphKey(stroke))
            {
                Logger.Info($"Session started (glyph key: {stroke.Key ?? '?'} )");
                _active = true;
                _buffer = string.Empty;
                return new EngineResult(
                    Consumed: true,
                    Overlay: BuildOverlay(activeProcessName),
                    Action: null,
                    ExecuteAfter: null,
                    ForceHide: false,
                    HideAfterSustain: false);
            }

            return EngineResult.None;
        }

        // Double-press glyph key (CapsLock twice) may trigger CapsLock if configured to do so.
        if (_isGlyphKey(stroke) && string.IsNullOrEmpty(_buffer))
        {
            Logger.Info("Glyph double-press detected");
            Reset();

            return new EngineResult(Consumed: true, Overlay: null, Action: null, ExecuteAfter: null, ForceHide: false, HideAfterSustain: false);
        }

        // While active, Esc cancels.
        if (stroke.Key == '\u001B')
        {
            Reset();
            return new EngineResult(Consumed: true, Overlay: null, Action: null, ExecuteAfter: null, ForceHide: true, HideAfterSustain: false);
        }

        // Ignore unmapped keys during session.
        // Note: Space is a valid bindable step (use `Space` token or literal space).
        if (stroke.Key is null)
        {
            return new EngineResult(
                Consumed: true,
                Overlay: BuildOverlay(activeProcessName),
                Action: null,
                ExecuteAfter: null,
                ForceHide: false,
                HideAfterSustain: false);
        }

        _buffer += stroke.Key.Value;

        var lookup = LookupMerged(_buffer, activeProcessName);
        if (!lookup.IsValidPrefix)
        {
            Logger.Info($"Invalid sequence: {_buffer}");
            var invalidTitle = OverlayBuilder.BuildTitle(
                buffer: _buffer,
                activeProcessName: activeProcessName,
                lookup: p => LookupMerged(p, activeProcessName),
                getGlobalPrefixDescription: prefix => GetPrefixDescription(prefix),
                getPerAppPrefixDescription: prefix =>
                {
                    if (string.IsNullOrWhiteSpace(activeProcessName)) return null;
                    return GetPerAppPrefixDescription(activeProcessName, prefix);
                },
                policy: _overlayPolicy);

            Reset();
            return new EngineResult(
                Consumed: true,
                Overlay: new OverlayModel($"{invalidTitle} (not bound)", Array.Empty<OverlayOption>()),
                Action: null,
                ExecuteAfter: null,
                ForceHide: false,
                HideAfterSustain: true);
        }

        if (lookup.IsComplete && lookup.Value is not null)
        {
            var action = lookup.Value;

            // If this sequence is both complete *and* a valid prefix for longer sequences,
            // delay execution briefly to allow fast disambiguation (e.g. `d` vs `dd`).
            if (lookup.NextKeys.Count > 0)
            {
                Logger.Info($"Ambiguous complete sequence: {_buffer} (delaying { _disambiguationTimeout.TotalMilliseconds }ms)");
                return new EngineResult(
                    Consumed: true,
                    Overlay: BuildOverlay(activeProcessName, lookup.NextKeys),
                    Action: action,
                    ExecuteAfter: _disambiguationTimeout,
                    ForceHide: false,
                    HideAfterSustain: false);
            }

            Logger.Info($"Complete sequence: {_buffer} -> {action.ActionId}");
            var completeTitle = OverlayBuilder.BuildTitle(
                buffer: _buffer,
                activeProcessName: activeProcessName,
                lookup: p => LookupMerged(p, activeProcessName),
                getGlobalPrefixDescription: prefix => GetPrefixDescription(prefix),
                getPerAppPrefixDescription: prefix =>
                {
                    if (string.IsNullOrWhiteSpace(activeProcessName)) return null;
                    return GetPerAppPrefixDescription(activeProcessName, prefix);
                },
                policy: _overlayPolicy);
            Reset();
            return new EngineResult(
                Consumed: true,
                Overlay: new OverlayModel(completeTitle, Array.Empty<OverlayOption>()),
                Action: action,
                ExecuteAfter: null,
                ForceHide: false,
                HideAfterSustain: true);
        }

        return new EngineResult(
            Consumed: true,
            Overlay: BuildOverlay(activeProcessName, lookup.NextKeys),
            Action: null,
            ExecuteAfter: null,
            ForceHide: false,
            HideAfterSustain: false);
    }

    private void Reset()
    {
        _active = false;
        _buffer = string.Empty;
    }

    private OverlayModel BuildOverlay(string? activeProcessName, IReadOnlyList<TrieNextKey>? next = null)
    {
        var nextKeys = next ?? LookupMerged(_buffer, activeProcessName).NextKeys;

        return OverlayBuilder.Build(
            buffer: _buffer,
            activeProcessName: activeProcessName,
            nextKeys: nextKeys,
            lookup: p => LookupMerged(p, activeProcessName),
            getGlobalPrefixDescription: prefix => GetPrefixDescription(prefix),
            getPerAppPrefixDescription: prefix =>
            {
                if (string.IsNullOrWhiteSpace(activeProcessName)) return null;
                return GetPerAppPrefixDescription(activeProcessName, prefix);
            },
            policy: _overlayPolicy);
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

        // If global is invalid but app is valid, take app as-is.
        if (!global.IsValidPrefix)
        {
            return app.Value;
        }

        // Completion precedence: app overrides global.
        if (app.Value.IsComplete && app.Value.Value is not null)
        {
            return app.Value;
        }

        // Merge next keys (union), but prefer app descriptions when overlapping.
        var merged = new Dictionary<char, TrieNextKey>(Math.Max(8, global.NextKeys.Count + app.Value.NextKeys.Count));
        foreach (var nk in global.NextKeys)
        {
            merged[nk.Key] = nk;
        }

        foreach (var nk in app.Value.NextKeys)
        {
            if (merged.TryGetValue(nk.Key, out var existing))
            {
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

        // If app doesn't complete, fall back to global completion (if any), but still allow
        // app to contribute next keys/descriptions.
        var resolvedValue = global.Value;
        var resolvedIsComplete = global.IsComplete;

        return new TrieLookupResult<ActionRequest>(
            IsValidPrefix: true,
            IsComplete: resolvedIsComplete,
            Value: resolvedValue,
            NextKeys: nextKeys);
    }
}
