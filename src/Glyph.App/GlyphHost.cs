using System.Windows;

using Glyph.Actions;
using Glyph.App.Config;
using Glyph.App.Services;
using Glyph.App.Overlay.Theming;
using Glyph.Core.Actions;
using Glyph.Core.Engine;
using Glyph.Core.Input;
using Glyph.Core.Logging;
using Glyph.Win32.Hooks;
using Glyph.Win32.Interop;
using Glyph.Win32.Windowing;

namespace Glyph.App;

public sealed class GlyphHost : IDisposable
{
    private readonly KeyboardHook _keyboardHook;
    private SequenceEngine _engine;
    private readonly IActionExecutor _actionExecutor;
    private readonly IOverlayPresenter _overlayPresenter;
    private readonly IKeymapProvider _keymapProvider;

    private readonly object _engineSync = new();
    private CancellationTokenSource? _pendingActionCts;

    private static void FireAndForget(Task task)
    {
        task.ContinueWith(
            t =>
            {
                if (t.Exception is not null)
                {
                    Logger.Error("Fire-and-forget task failed", t.Exception);
                }
                else
                {
                    Logger.Error("Fire-and-forget task failed", new Exception("Task faulted without Exception"));
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private List<Glyph.App.Config.GlyphKeyConfig> _glyphSequence;
    private int _glyphProgress;
    private readonly HashSet<int> _currentlyDown = new();
    private readonly Dictionary<int, bool> _modifierCandidates = new();

    public GlyphHost()
    {
        // Load persisted config (glyph key + theme) and create engine accordingly.
        var cfg = Glyph.App.Config.AppConfig.Load();
        _glyphSequence = NormalizeGlyph(cfg);
        _glyphProgress = 0;

        if (_glyphSequence.Count == 1)
        {
            var single = _glyphSequence[0];
            Func<KeyStroke, bool> glyphFunc = stroke => StrokeMatchesGlyph(stroke, single);
            _engine = SequenceEngine.CreatePrototype(glyphFunc);
        }
        else
        {
            // Multi-stroke glyph: glyph detection is handled in GlyphHost so we can
            // suppress every keystroke that participates in the glyph sequence.
            _engine = SequenceEngine.CreatePrototype(_ => false);
        }

        _keymapProvider = new YamlKeymapProvider();
        _keymapProvider.ApplyToEngine(_engine);

        _actionExecutor = new ActionRuntimeExecutor(new ActionRuntime());
        _overlayPresenter = new OverlayWindowPresenter(new OverlayWindow());

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.KeyUp += OnGlobalKeyUp;
    }

    private static void ToggleBreadcrumbsMode()
    {
        try
        {
            var cfg = AppConfig.Load();
            cfg.BreadcrumbsMode = !cfg.BreadcrumbsMode;
            AppConfig.Save(cfg);
            ThemeManager.Reload();
            Logger.Info($"Breadcrumbs mode {(cfg.BreadcrumbsMode ? "enabled" : "disabled")}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to toggle breadcrumbs mode", ex);
        }
    }

    public void Start()
    {
        Logger.Info("GlyphHost starting...");
        _keyboardHook.Start();
        Logger.Info("Keyboard hook started (background). Default glyph is F12 (configurable in Settings).");
    }

    public void Dispose()
    {
        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.KeyUp -= OnGlobalKeyUp;
        _keyboardHook.Dispose();
        _overlayPresenter.Dispose();
    }

    private void OnGlobalKeyDown(object? sender, KeyboardHookEventArgs e)
    {
        CancelPendingAction();

        ReconcileModifierCandidates();
        _currentlyDown.Add(e.VkCode);

        // Derive modifier state from the currently-down set to avoid timing issues
        var ctrlDown = _currentlyDown.Contains(VirtualKey.VK_CONTROL) || _currentlyDown.Contains(0xA2) || _currentlyDown.Contains(0xA3);
        var shiftDown = _currentlyDown.Contains(VirtualKey.VK_SHIFT) || _currentlyDown.Contains(0xA0) || _currentlyDown.Contains(0xA1);
        var altDown = _currentlyDown.Contains(VirtualKey.VK_MENU) || _currentlyDown.Contains(0xA4) || _currentlyDown.Contains(0xA5);
        var winDown = _currentlyDown.Contains(VirtualKey.VK_LWIN) || _currentlyDown.Contains(VirtualKey.VK_RWIN);

        var stroke = KeyStroke.FromVkCode(
            e.VkCode,
            ctrl: ctrlDown,
            shift: shiftDown,
            alt: altDown,
            win: winDown);

        var activeProcess = ForegroundApp.TryGetProcessName();

        // Single-stroke modifier glyph: begin session immediately on key-down.
        // We suppress the modifier key DOWN so Windows never sees it held.
        if (_glyphSequence.Count == 1 && IsModifierVk(_glyphSequence[0].VkCode) && ModifierMatchesVk(e.VkCode, _glyphSequence[0].VkCode))
        {
            if (!_engine.IsSessionActive)
            {
                EngineResult began;
                lock (_engineSync)
                {
                    began = _engine.BeginSession(DateTimeOffset.UtcNow, activeProcess);
                }
                if (began.Overlay is not null)
                {
                    _overlayPresenter.Render(began.Overlay, false, false);
                }
            }

            e.Suppress = true;
            return;
        }

        // If there are any active modifier candidates and this keydown is NOT the same modifier,
        // abort those candidates (user pressed another key while modifier held).
        if (_modifierCandidates.Count > 0)
        {
            foreach (var k in _modifierCandidates.Keys.ToList())
            {
                if (k != e.VkCode)
                {
                    _modifierCandidates[k] = true; // aborted
                    Logger.Info($"Modifier candidate aborted: 0x{k:X}");
                }
            }
        }

        // Multi-stroke glyph detection (when engine session is inactive).
        // Consume modifier steps on key-down as well to avoid an artificial "wait for key-up" delay.
        if (_glyphSequence.Count > 1 && !_engine.IsSessionActive)
        {
            var expected = _glyphSequence[_glyphProgress];
            if (IsModifierVk(expected.VkCode))
            {
                if (TryConsumeGlyphStroke(stroke, activeProcess, out var beganSession))
                {
                    e.Suppress = true;
                    if (beganSession is not null)
                    {
                        _overlayPresenter.Render(beganSession.Value.Overlay!, false, false);
                    }
                    return;
                }
            }
            else
            {
                if (TryConsumeGlyphStroke(stroke, activeProcess, out var beganSession))
                {
                    e.Suppress = true;
                    if (beganSession is not null)
                    {
                        _overlayPresenter.Render(beganSession.Value.Overlay!, false, false);
                    }
                    return;
                }
            }
        }

        EngineResult result;
        lock (_engineSync)
        {
            result = _engine.Handle(stroke, DateTimeOffset.UtcNow, activeProcess);
        }

        if (result.Consumed)
        {
            e.Suppress = true;
        }

        _overlayPresenter.Render(result.Overlay, result.ForceHide, result.HideAfterSustain);

        if (result.Action is not null)
        {
            // If the engine reports an ambiguous completion, delay execution briefly
            // so the user can type the next key (e.g. `d` vs `dd`).
            if (result.ExecuteAfter is not null)
            {
                ScheduleDelayedAction(result.Action, result.ExecuteAfter.Value, activeProcess);
                return;
            }

            Logger.Info($"Action triggered: {result.Action.ActionId} (app={activeProcess ?? "?"})");
            if (string.Equals(result.Action.ActionId, "openGlyphKeymapEditor", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var window = System.Windows.Application.Current.Windows.OfType<UI.SettingsWindow>().FirstOrDefault() ?? new UI.SettingsWindow();

                    if (!window.IsVisible)
                    {
                        window.Show();
                    }

                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }

                    window.Activate();
                    window.Topmost = true;
                    window.Topmost = false;
                    window.Focus();
                    window.OpenKeymapEditorFromAction();
                });
                return;
            }
            if (string.Equals(result.Action.ActionId, "openGlyphGui", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var window = System.Windows.Application.Current.Windows.OfType<UI.SettingsWindow>().FirstOrDefault() ?? new UI.SettingsWindow();

                    if (!window.IsVisible)
                    {
                        window.Show();
                    }

                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }

                    window.Activate();
                    window.Topmost = true;
                    window.Topmost = false;
                    window.Focus();
                });
                return;
            }
            if (string.Equals(result.Action.ActionId, "quitGlyph", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
                return;
            }

            if (string.Equals(result.Action.ActionId, "reloadKeymaps", StringComparison.OrdinalIgnoreCase))
            {
                // Re-apply keymaps from YAML into the current engine instance so user edits take effect immediately.
                _keymapProvider.ApplyToEngine(_engine);
                Logger.Info("Keymaps reloaded from YAML");
                return;
            }

            if (string.Equals(result.Action.ActionId, "toggleBreadcrumbsMode", StringComparison.OrdinalIgnoreCase))
            {
                ToggleBreadcrumbsMode();
                return;
            }

                FireAndForget(_actionExecutor.ExecuteAsync(result.Action, CancellationToken.None));
        }
    }

    private void CancelPendingAction()
    {
        var cts = _pendingActionCts;
        if (cts is null) return;

        _pendingActionCts = null;
        try
        {
            cts.Cancel();
        }
        catch
        {
            // best-effort
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void ScheduleDelayedAction(ActionRequest action, TimeSpan delay, string? activeProcess)
    {
        CancelPendingAction();

        var cts = new CancellationTokenSource();
        _pendingActionCts = cts;

        FireAndForget(Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            // Only execute if we are still the pending action.
            if (!ReferenceEquals(_pendingActionCts, cts)) return;
            _pendingActionCts = null;
            cts.Dispose();

            // End the engine session before executing so subsequent input starts cleanly.
            lock (_engineSync)
            {
                _engine.EndSession();
            }

            _overlayPresenter.Render(null, false, false);

            Logger.Info($"Delayed action triggered: {action.ActionId} (app={activeProcess ?? "?"})");

            if (string.Equals(action.ActionId, "reloadKeymaps", StringComparison.OrdinalIgnoreCase))
            {
                lock (_engineSync)
                {
                    _keymapProvider.ApplyToEngine(_engine);
                }
                Logger.Info("Keymaps reloaded from YAML");
                return;
            }

            FireAndForget(_actionExecutor.ExecuteAsync(action, CancellationToken.None));
        }));
    }

    private void OnGlobalKeyUp(object? sender, KeyboardHookEventArgs e)
    {
        // Remove from currently down
        _currentlyDown.Remove(e.VkCode);

        var ctrlDown = _currentlyDown.Contains(VirtualKey.VK_CONTROL) || _currentlyDown.Contains(0xA2) || _currentlyDown.Contains(0xA3);
        var shiftDown = _currentlyDown.Contains(VirtualKey.VK_SHIFT) || _currentlyDown.Contains(0xA0) || _currentlyDown.Contains(0xA1);
        var altDown = _currentlyDown.Contains(VirtualKey.VK_MENU) || _currentlyDown.Contains(0xA4) || _currentlyDown.Contains(0xA5);
        var winDown = _currentlyDown.Contains(VirtualKey.VK_LWIN) || _currentlyDown.Contains(VirtualKey.VK_RWIN);

        var stroke = KeyStroke.FromVkCode(
            e.VkCode,
            ctrl: ctrlDown,
            shift: shiftDown,
            alt: altDown,
            win: winDown);

        var activeProcess = ForegroundApp.TryGetProcessName();

        // Single-stroke modifier glyph: begin session on key-up, suppressing the key-up.
        // Because we also suppress the modifier key-down, Windows never sees the modifier held.
        if (_glyphSequence.Count == 1 && IsModifierVk(_glyphSequence[0].VkCode) && ModifierMatchesVk(e.VkCode, _glyphSequence[0].VkCode))
        {
            if (_modifierCandidates.TryGetValue(e.VkCode, out var abortedSingle))
            {
                _modifierCandidates.Remove(e.VkCode);
                if (!abortedSingle && !_engine.IsSessionActive)
                {
                    var began = _engine.BeginSession(DateTimeOffset.UtcNow, activeProcess);
                    e.Suppress = true;

                    if (began.Overlay is not null)
                    {
                        _overlayPresenter.Render(began.Overlay, false, false);
                    }
                }
            }

            return;
        }

        // If a modifier candidate exists for this vk (single-stroke modifier glyph), match it on key-up.
        if (_modifierCandidates.TryGetValue(e.VkCode, out var aborted))
        {
            _modifierCandidates.Remove(e.VkCode);
            if (!aborted)
            {
                // Multi-stroke glyph: consume modifier steps on key-up.
                if (_glyphSequence.Count > 1 && !_engine.IsSessionActive)
                {
                    var expected = _glyphSequence[_glyphProgress];
                    if (IsModifierVk(expected.VkCode) && e.VkCode == expected.VkCode)
                    {
                        Logger.Info($"Modifier candidate key-up (multi-stroke): 0x{e.VkCode:X}, attempting glyph consume");
                        if (TryConsumeGlyphStroke(stroke, activeProcess, out var beganSession))
                        {
                            // Important: do NOT suppress modifier events; suppressing can lead to a stuck modifier state.
                            if (beganSession is not null)
                            {
                                _overlayPresenter.Render(beganSession.Value.Overlay!, false, false);
                            }
                        }
                        return;
                    }
                }

                // (Single-stroke modifier glyphs are handled above.)
            }
            else
            {
                Logger.Info($"Modifier candidate 0x{e.VkCode:X} aborted on key-down");
            }
        }
    }

    private static bool IsModifierVk(int vk)
    {
        // Generic: VK_SHIFT=0x10, VK_CONTROL=0x11, VK_MENU(Alt)=0x12
        // Sided: VK_LSHIFT=0xA0, VK_RSHIFT=0xA1, VK_LCONTROL=0xA2, VK_RCONTROL=0xA3, VK_LMENU=0xA4, VK_RMENU=0xA5
        // Windows: VK_LWIN=0x5B, VK_RWIN=0x5C
        return vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x5B || vk == 0x5C || (vk >= 0xA0 && vk <= 0xA5);
    }

    private static bool ModifierMatchesVk(int actualVk, int expectedVk)
    {
        if (actualVk == expectedVk) return true;

        // Allow generic ↔ sided matches.
        return expectedVk switch
        {
            // Shift
            0x10 => actualVk == 0xA0 || actualVk == 0xA1,
            0xA0 or 0xA1 => actualVk == 0x10,

            // Ctrl
            0x11 => actualVk == 0xA2 || actualVk == 0xA3,
            0xA2 or 0xA3 => actualVk == 0x11,

            // Alt
            0x12 => actualVk == 0xA4 || actualVk == 0xA5,
            0xA4 or 0xA5 => actualVk == 0x12,

            // Win
            0x5B => actualVk == 0x5C,
            0x5C => actualVk == 0x5B,

            _ => false,
        };
    }

    private void ReconcileModifierCandidates()
    {
        if (_modifierCandidates.Count == 0) return;

        // If we missed a key-up event, don't leave a stale candidate hanging around.
        // This is especially important for modifier glyphs.
        foreach (var vk in _modifierCandidates.Keys.ToList())
        {
            if (!NativeMethods.IsKeyDown(vk))
            {
                _modifierCandidates.Remove(vk);
            }
        }
    }

    public void UpdateGlyphSequence(IReadOnlyList<Glyph.App.Config.GlyphKeyConfig>? sequence)
    {
        _glyphSequence = NormalizeGlyph(sequence);
        _glyphProgress = 0;

        CancelPendingAction();

        if (_glyphSequence.Count == 1)
        {
            var single = _glyphSequence[0];
            lock (_engineSync)
            {
                _engine = SequenceEngine.CreatePrototype(stroke => StrokeMatchesGlyph(stroke, single));
            }
        }
        else
        {
            lock (_engineSync)
            {
                _engine = SequenceEngine.CreatePrototype(_ => false);
            }
        }

        lock (_engineSync)
        {
            _keymapProvider.ApplyToEngine(_engine);
        }

        Logger.Info($"Glyph updated from settings (len={_glyphSequence.Count})");
    }

    public void ReloadKeymaps()
    {
        lock (_engineSync)
        {
            _keymapProvider.ApplyToEngine(_engine);

            if (_engine.IsSessionActive)
            {
                var activeProcess = ForegroundApp.TryGetProcessName();
                var overlay = _engine.BuildOverlaySnapshot(activeProcess);
                _overlayPresenter.Render(overlay, false, false);
            }
        }
        Logger.Info("Keymaps reloaded from YAML");
    }

    private static List<Glyph.App.Config.GlyphKeyConfig> NormalizeGlyph(Glyph.App.Config.AppConfig cfg)
    {
        if (cfg.GlyphSequence is { Count: > 0 })
        {
            return cfg.GlyphSequence.Where(IsValidGlyphStep).ToList();
        }

        if (cfg.Glyph is not null && IsValidGlyphStep(cfg.Glyph))
        {
            return new List<Glyph.App.Config.GlyphKeyConfig> { cfg.Glyph };
        }

        // Default glyph: Ctrl+Shift+NumPad *
        return new List<Glyph.App.Config.GlyphKeyConfig>
        {
            new Glyph.App.Config.GlyphKeyConfig { Ctrl = false, Shift = false, Alt = false, Win = false, VkCode = 0x7B }
        };
    }

    private static List<Glyph.App.Config.GlyphKeyConfig> NormalizeGlyph(IReadOnlyList<Glyph.App.Config.GlyphKeyConfig>? sequence)
    {
        if (sequence is { Count: > 0 })
        {
            var cleaned = sequence.Where(IsValidGlyphStep).ToList();
            if (cleaned.Count > 0) return cleaned;
        }

        return new List<Glyph.App.Config.GlyphKeyConfig>
        {
            new Glyph.App.Config.GlyphKeyConfig { Ctrl = false, Shift = false, Alt = false, Win = false, VkCode = 0x7B }
        };
    }

    private static bool IsValidGlyphStep(Glyph.App.Config.GlyphKeyConfig step)
    {
        // Must specify a concrete key. (Prevents the “matches everything” bug.)
        // Also reject Space (0x20) as a valid glyph step to avoid accidental triggers.
        return step.VkCode != 0 && step.VkCode != 0x20;
    }

    private static bool StrokeMatchesGlyph(KeyStroke stroke, Glyph.App.Config.GlyphKeyConfig l)
    {
        if (l.VkCode == 0) return false;
        // For modifier glyph steps, ignore modifier flags and match by VK (generic ↔ sided).
        if (IsModifierVk(l.VkCode))
        {
            return ModifierMatchesVk(stroke.VkCode, l.VkCode);
        }

        if (stroke.VkCode != l.VkCode) return false;

        // If the recorded glyph step is a direction-specific modifier (Left/Right Ctrl/Shift/Alt),
        // match only by virtual-key code. Modifier flags (generic Ctrl/Shift/Alt) may not reliably
        // reflect the sided key in the low-level hook, so requiring them breaks detection.
        // VK codes: VK_LSHIFT=0xA0, VK_RSHIFT=0xA1, VK_LCONTROL=0xA2, VK_RCONTROL=0xA3,
        // VK_LMENU=0xA4, VK_RMENU=0xA5
        int vk = l.VkCode;
        if (vk == 0xA0 || vk == 0xA1 || vk == 0xA2 || vk == 0xA3 || vk == 0xA4 || vk == 0xA5)
        {
            return true;
        }

        if (stroke.Ctrl != l.Ctrl) return false;
        if (stroke.Shift != l.Shift) return false;
        if (stroke.Alt != l.Alt) return false;
        if (stroke.Win != l.Win) return false;
        return true;
    }

    private bool TryConsumeGlyphStroke(KeyStroke stroke, string? activeProcessName, out EngineResult? beganSession)
    {
        beganSession = null;

        // Only match concrete steps.
        if (_glyphProgress < 0 || _glyphProgress >= _glyphSequence.Count)
        {
            _glyphProgress = 0;
        }

        var expected = _glyphSequence[_glyphProgress];

        if (StrokeMatchesGlyph(stroke, expected))
        {
            _glyphProgress++;
            Logger.Info($"Glyph matched step {_glyphProgress}/{_glyphSequence.Count}");
            if (_glyphProgress >= _glyphSequence.Count)
            {
                _glyphProgress = 0;
                lock (_engineSync)
                {
                    beganSession = _engine.BeginSession(DateTimeOffset.UtcNow, activeProcessName);
                }
                Logger.Info("Glyph sequence complete — session begun");
            }

            return true; // consume every glyph stroke
        }

        // Mismatch: reset progress.
        _glyphProgress = 0;
        return false;
    }
}
