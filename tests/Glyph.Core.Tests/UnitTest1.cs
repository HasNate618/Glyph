using Glyph.Core.Engine;
using Glyph.Core.Input;

namespace Glyph.Core.Tests;

public class SequenceEngineTests
{
    [Fact]
    public void LeaderGesture_ActivatesOverlay()
    {
        var engine = SequenceEngine.CreatePrototype();

        // Leader key: F12 (VK_F12 = 0x7B)
        var leader = KeyStroke.FromVkCode(0x7B, ctrl: false, shift: false, alt: false, win: false);

        var result = engine.Handle(leader, DateTimeOffset.UtcNow, activeProcessName: null);

        Assert.True(result.Consumed);
        Assert.NotNull(result.Overlay);
        Assert.Null(result.Action);
    }

    [Fact]
    public void LeaderThenRc_TriggersLaunchChrome()
    {
        var engine = SequenceEngine.CreatePrototype();
        var now = DateTimeOffset.UtcNow;

        var leader = KeyStroke.FromVkCode(0x7B, ctrl: false, shift: false, alt: false, win: false);

        _ = engine.Handle(leader, now, activeProcessName: null);
        _ = engine.Handle(new KeyStroke('r', Ctrl: false, Shift: false, Alt: false, Win: false), now.AddMilliseconds(10), activeProcessName: null);
        var result = engine.Handle(new KeyStroke('c', Ctrl: false, Shift: false, Alt: false, Win: false), now.AddMilliseconds(20), activeProcessName: null);

        Assert.True(result.Consumed);
        Assert.NotNull(result.Action);
        Assert.Equal("launchChrome", result.Action!.ActionId);
        Assert.Null(result.Overlay);
    }

    [Fact]
    public void LeaderThenRt_TriggersOpenTerminal()
    {
        var engine = SequenceEngine.CreatePrototype();
        var now = DateTimeOffset.UtcNow;

        var leader = KeyStroke.FromVkCode(0x7B, ctrl: false, shift: false, alt: false, win: false);

        _ = engine.Handle(leader, now, activeProcessName: null);
        _ = engine.Handle(new KeyStroke('r', Ctrl: false, Shift: false, Alt: false, Win: false), now.AddMilliseconds(10), activeProcessName: null);
        var result = engine.Handle(new KeyStroke('t', Ctrl: false, Shift: false, Alt: false, Win: false), now.AddMilliseconds(20), activeProcessName: null);

        Assert.True(result.Consumed);
        Assert.NotNull(result.Action);
        Assert.Equal("openTerminal", result.Action!.ActionId);
        Assert.Null(result.Overlay);
    }

    [Fact]
    public void LeaderThenRf_TriggersOpenExplorer()
    {
        var engine = SequenceEngine.CreatePrototype();
        var now = DateTimeOffset.UtcNow;

        var leader = KeyStroke.FromVkCode(0x7B, ctrl: false, shift: false, alt: false, win: false);

        _ = engine.Handle(leader, now, activeProcessName: null);
        _ = engine.Handle(new KeyStroke('r', Ctrl: false, Shift: false, Alt: false, Win: false), now.AddMilliseconds(10), activeProcessName: null);
        var result = engine.Handle(new KeyStroke('f', Ctrl: false, Shift: false, Alt: false, Win: false), now.AddMilliseconds(20), activeProcessName: null);

        Assert.True(result.Consumed);
        Assert.NotNull(result.Action);
        Assert.Equal("openExplorer", result.Action!.ActionId);
        Assert.Null(result.Overlay);
    }
}