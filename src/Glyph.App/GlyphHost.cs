using System.Windows;

using Glyph.Actions;
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
    private readonly ActionRuntime _actionRuntime;
    private readonly OverlayWindow _overlay;

    private List<Glyph.App.Config.LeaderKeyConfig> _leaderSequence;
    private int _leaderProgress;
    private readonly HashSet<int> _currentlyDown = new();
    private readonly Dictionary<int, bool> _modifierCandidates = new();

    public GlyphHost()
    {
        // Load persisted config (leader key + theme) and create engine accordingly.
        var cfg = Glyph.App.Config.AppConfig.Load();
        _leaderSequence = NormalizeLeader(cfg);
        _leaderProgress = 0;

        if (_leaderSequence.Count == 1)
        {
            var single = _leaderSequence[0];
            Func<KeyStroke, bool> leaderFunc = stroke => StrokeMatchesLeader(stroke, single);
            _engine = SequenceEngine.CreatePrototype(leaderFunc);
        }
        else
        {
            // Multi-stroke leader: leader detection is handled in GlyphHost so we can
            // suppress every keystroke that participates in the leader sequence.
            _engine = SequenceEngine.CreatePrototype(_ => false);
        }

        Glyph.App.Config.KeymapYamlLoader.ApplyToEngine(_engine);
        _actionRuntime = new ActionRuntime();
        _overlay = new OverlayWindow();

        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyDown += OnGlobalKeyDown;
        _keyboardHook.KeyUp += OnGlobalKeyUp;
    }

    public void Start()
    {
        Logger.Info("GlyphHost starting...");
        _keyboardHook.Start();
        Logger.Info("Keyboard hook started (background). Default leader is F12 (configurable in Settings).");
    }

    public void Dispose()
    {
        _keyboardHook.KeyDown -= OnGlobalKeyDown;
        _keyboardHook.KeyUp -= OnGlobalKeyUp;
        _keyboardHook.Dispose();
        _overlay.Dispatcher.Invoke(() =>
        {
            if (_overlay.IsVisible)
            {
                _overlay.Hide();
            }
        });
    }

    private void OnGlobalKeyDown(object? sender, KeyboardHookEventArgs e)
    {
        ReconcileModifierCandidates();
        _currentlyDown.Add(e.VkCode);

        // Special-case: if the leader is a single modifier key (Alt/Ctrl/Shift/Win),
        // treat it as a "tap to open leader" gesture.
        // We suppress the modifier key DOWN event so Windows never sees the modifier held,
        // preventing stuck-modifier behavior (e.g., Alt+<key> everywhere) if we later
        // consume the corresponding key-up.
        if (_leaderSequence.Count == 1 && IsModifierVk(_leaderSequence[0].VkCode) && ModifierMatchesVk(e.VkCode, _leaderSequence[0].VkCode))
        {
            _modifierCandidates[e.VkCode] = false;
            e.Suppress = true;
            return;
        }
        var stroke = KeyStroke.FromVkCode(
            e.VkCode,
            ctrl: NativeMethods.IsKeyDown(VirtualKey.VK_CONTROL),
            shift: NativeMethods.IsKeyDown(VirtualKey.VK_SHIFT),
            alt: NativeMethods.IsKeyDown(VirtualKey.VK_MENU),
            win: NativeMethods.IsKeyDown(VirtualKey.VK_LWIN) || NativeMethods.IsKeyDown(VirtualKey.VK_RWIN));

        var activeProcess = ForegroundApp.TryGetProcessName();

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

        // Multi-stroke leader detection (when engine session is inactive).
        // Allow modifiers, but consume modifier steps on key-up to avoid breaking normal modifier+key combos.
        if (_leaderSequence.Count > 1 && !_engine.IsSessionActive)
        {
            var expected = _leaderSequence[_leaderProgress];
            if (IsModifierVk(expected.VkCode))
            {
                // Candidate modifier down (do not consume now; wait for key-up)
                if (ModifierMatchesVk(e.VkCode, expected.VkCode))
                {
                    _modifierCandidates[e.VkCode] = false;
                    Logger.Info($"Modifier candidate down (multi-stroke): 0x{e.VkCode:X}");
                }
            }
            else
            {
                if (TryConsumeLeaderStroke(stroke, activeProcess, out var beganSession))
                {
                    e.Suppress = true;
                    if (beganSession is not null)
                    {
                        // Show overlay immediately.
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _overlay.Update(beganSession.Value.Overlay!);
                            if (!_overlay.IsVisible)
                            {
                                _overlay.Show();
                            }
                        });
                    }
                    return;
                }
            }
        }

        var result = _engine.Handle(stroke, DateTimeOffset.UtcNow, activeProcess);

        if (result.Consumed)
        {
            e.Suppress = true;
        }

        if (result.Overlay is not null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _overlay.Update(result.Overlay);
                if (!_overlay.IsVisible)
                {
                    _overlay.Show();
                }
            });
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_overlay.IsVisible)
                {
                    _overlay.Hide();
                }
            });
        }

        if (result.Action is not null)
        {
            Logger.Info($"Action triggered: {result.Action.ActionId} (app={activeProcess ?? "?"})");
            if (string.Equals(result.Action.ActionId, "quitGlyph", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(System.Windows.Application.Current.Shutdown);
                return;
            }

            _ = _actionRuntime.ExecuteAsync(result.Action, CancellationToken.None);
        }
    }

    private void OnGlobalKeyUp(object? sender, KeyboardHookEventArgs e)
    {
        // Remove from currently down
        _currentlyDown.Remove(e.VkCode);

        var stroke = KeyStroke.FromVkCode(
            e.VkCode,
            ctrl: NativeMethods.IsKeyDown(VirtualKey.VK_CONTROL),
            shift: NativeMethods.IsKeyDown(VirtualKey.VK_SHIFT),
            alt: NativeMethods.IsKeyDown(VirtualKey.VK_MENU),
            win: NativeMethods.IsKeyDown(VirtualKey.VK_LWIN) || NativeMethods.IsKeyDown(VirtualKey.VK_RWIN));

        var activeProcess = ForegroundApp.TryGetProcessName();

        // Single-stroke modifier leader: begin session on key-up, suppressing the key-up.
        // Because we also suppress the modifier key-down, Windows never sees the modifier held.
        if (_leaderSequence.Count == 1 && IsModifierVk(_leaderSequence[0].VkCode) && ModifierMatchesVk(e.VkCode, _leaderSequence[0].VkCode))
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
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            _overlay.Update(began.Overlay);
                            if (!_overlay.IsVisible)
                            {
                                _overlay.Show();
                            }
                        });
                    }
                }
            }

            return;
        }

        // If a modifier candidate exists for this vk (single-stroke modifier leader), match it on key-up.
        if (_modifierCandidates.TryGetValue(e.VkCode, out var aborted))
        {
            _modifierCandidates.Remove(e.VkCode);
            if (!aborted)
            {
                // Multi-stroke leader: consume modifier steps on key-up.
                if (_leaderSequence.Count > 1 && !_engine.IsSessionActive)
                {
                    var expected = _leaderSequence[_leaderProgress];
                    if (IsModifierVk(expected.VkCode) && e.VkCode == expected.VkCode)
                    {
                        Logger.Info($"Modifier candidate key-up (multi-stroke): 0x{e.VkCode:X}, attempting leader consume");
                        if (TryConsumeLeaderStroke(stroke, activeProcess, out var beganSession))
                        {
                            // Important: do NOT suppress modifier events; suppressing can lead to a stuck modifier state.
                            if (beganSession is not null)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _overlay.Update(beganSession.Value.Overlay!);
                                    if (!_overlay.IsVisible)
                                    {
                                        _overlay.Show();
                                    }
                                });
                            }
                        }
                        return;
                    }
                }

                // (Single-stroke modifier leaders are handled above.)
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
        // This is especially important for modifier leaders.
        foreach (var vk in _modifierCandidates.Keys.ToList())
        {
            if (!NativeMethods.IsKeyDown(vk))
            {
                _modifierCandidates.Remove(vk);
            }
        }
    }

    public void UpdateLeaderSequence(IReadOnlyList<Glyph.App.Config.LeaderKeyConfig>? sequence)
    {
        _leaderSequence = NormalizeLeader(sequence);
        _leaderProgress = 0;

        if (_leaderSequence.Count == 1)
        {
            var single = _leaderSequence[0];
            _engine = SequenceEngine.CreatePrototype(stroke => StrokeMatchesLeader(stroke, single));
        }
        else
        {
            _engine = SequenceEngine.CreatePrototype(_ => false);
        }

        Glyph.App.Config.KeymapYamlLoader.ApplyToEngine(_engine);

        Logger.Info($"Leader updated from settings (len={_leaderSequence.Count})");
    }

    private static List<Glyph.App.Config.LeaderKeyConfig> NormalizeLeader(Glyph.App.Config.AppConfig cfg)
    {
        if (cfg.LeaderSequence is { Count: > 0 })
        {
            return cfg.LeaderSequence.Where(IsValidLeaderStep).ToList();
        }

        if (cfg.Leader is not null && IsValidLeaderStep(cfg.Leader))
        {
            return new List<Glyph.App.Config.LeaderKeyConfig> { cfg.Leader };
        }

        // Default leader: Ctrl+Shift+NumPad *
        return new List<Glyph.App.Config.LeaderKeyConfig>
        {
            new Glyph.App.Config.LeaderKeyConfig { Ctrl = false, Shift = false, Alt = false, Win = false, VkCode = 0x7B }
        };
    }

    private static List<Glyph.App.Config.LeaderKeyConfig> NormalizeLeader(IReadOnlyList<Glyph.App.Config.LeaderKeyConfig>? sequence)
    {
        if (sequence is { Count: > 0 })
        {
            var cleaned = sequence.Where(IsValidLeaderStep).ToList();
            if (cleaned.Count > 0) return cleaned;
        }

        return new List<Glyph.App.Config.LeaderKeyConfig>
        {
            new Glyph.App.Config.LeaderKeyConfig { Ctrl = false, Shift = false, Alt = false, Win = false, VkCode = 0x7B }
        };
    }

    private static bool IsValidLeaderStep(Glyph.App.Config.LeaderKeyConfig step)
    {
        // Must specify a concrete key. (Prevents the “matches everything” bug.)
        // Also reject Space (0x20) as a valid leader step to avoid accidental triggers.
        return step.VkCode != 0 && step.VkCode != 0x20;
    }

    private static bool StrokeMatchesLeader(KeyStroke stroke, Glyph.App.Config.LeaderKeyConfig l)
    {
        if (l.VkCode == 0) return false;
        if (stroke.VkCode != l.VkCode) return false;

        // If the recorded leader step is a direction-specific modifier (Left/Right Ctrl/Shift/Alt),
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

    private bool TryConsumeLeaderStroke(KeyStroke stroke, string? activeProcessName, out EngineResult? beganSession)
    {
        beganSession = null;

        // Only match concrete steps.
        if (_leaderProgress < 0 || _leaderProgress >= _leaderSequence.Count)
        {
            _leaderProgress = 0;
        }

        var expected = _leaderSequence[_leaderProgress];
        // Log for diagnostics
        Logger.Info($"Leader check: progress={_leaderProgress}/{_leaderSequence.Count} got vk=0x{stroke.VkCode:X} ctrl={stroke.Ctrl} shift={stroke.Shift} alt={stroke.Alt} win={stroke.Win} expected=0x{expected.VkCode:X} ctrl={expected.Ctrl} shift={expected.Shift} alt={expected.Alt} win={expected.Win}");

        if (StrokeMatchesLeader(stroke, expected))
        {
            _leaderProgress++;
            Logger.Info($"Leader matched step {_leaderProgress}/{_leaderSequence.Count}");
            if (_leaderProgress >= _leaderSequence.Count)
            {
                _leaderProgress = 0;
                beganSession = _engine.BeginSession(DateTimeOffset.UtcNow, activeProcessName);
                Logger.Info("Leader sequence complete — session begun");
            }

            return true; // consume every leader stroke
        }

        // Mismatch: reset progress.
        _leaderProgress = 0;
        return false;
    }
}
