using Xunit;
using Glyph.Core.Engine;
using Glyph.Core.Actions;
using Glyph.Core.Input;

namespace Glyph.Core.Tests;

public class SequenceEngineTests
{
    [Fact]
    public void Overlay_ShowsMergedNextKeys_ForAppAndGlobal()
    {
        var engine = SequenceEngine.CreatePrototype();

        // Add global binding: 'a' -> globalA, child 'b'
        engine.AddGlobalBinding("a", new ActionRequest("globalA"), "globalA");
        engine.AddGlobalBinding("ab", new ActionRequest("globalAB"), "globalAB");

        // Add per-app binding for process 'Code': 'a' has a different label and child 'c'
        engine.AddPerAppBinding("Code", "a", new ActionRequest("appA"), "appA");
        engine.AddPerAppBinding("Code", "ac", new ActionRequest("appAC"), "appAC");

        // Begin session and type 'a' as the user would.
        var began = engine.BeginSession(System.DateTimeOffset.UtcNow, "Code");
        var result = engine.Handle(Glyph.Core.Input.KeyStroke.FromVkCode(0x41, false, false, false, false), System.DateTimeOffset.UtcNow, "Code");

        Assert.True(result.Overlay is not null);
        var keys = result.Overlay.Options.Select(o => o.Key).ToList();
        // App prefix children should be shown (child key 'c' for 'ac'); global-only child 'b' should not override
        Assert.Contains("c", keys);
        Assert.DoesNotContain("b", keys);
    }

    [Fact]
    public void Overlay_DoesNotRenderGrandchildCompletions()
    {
        var engine = SequenceEngine.CreatePrototype();

        engine.SetPrefixDescription(",", "Glyph");
        engine.SetPrefixDescription(",t", "Theme");
        engine.AddGlobalBinding(",tf", new ActionRequest("fluent"), "Fluent");

        engine.BeginSession(System.DateTimeOffset.UtcNow, null);
        var afterComma = engine.Handle(KeyStroke.FromVkCode(0xBC, false, false, false, false), System.DateTimeOffset.UtcNow, null);

        Assert.NotNull(afterComma.Overlay);
        var keys = afterComma.Overlay!.Options.Select(o => o.Key).ToList();

        Assert.Contains("t", keys);
        Assert.DoesNotContain("tf", keys);
    }

    [Fact]
    public void Overlay_IncludesRepeatedKeyDisambiguation_Dd()
    {
        var engine = SequenceEngine.CreatePrototype();

        engine.AddGlobalBinding("d", new ActionRequest("deleteChar"), "Delete char");
        engine.AddGlobalBinding("dd", new ActionRequest("deleteLine"), "Delete line");

        var began = engine.BeginSession(System.DateTimeOffset.UtcNow, null);
        Assert.NotNull(began.Overlay);

        var keys = began.Overlay!.Options.Select(o => o.Key).ToList();
        Assert.Contains("d", keys);
        Assert.Contains("dd", keys);
    }

    [Fact]
    public void Overlay_ShowsLookaheadForBlankIntermediates_Mx()
    {
        var engine = SequenceEngine.CreatePrototype();

        // 'm' has no label and doesn't complete; 'mx' completes with a label.
        engine.AddGlobalBinding("mx", new ActionRequest("mx"), "Do mx");

        var began = engine.BeginSession(System.DateTimeOffset.UtcNow, null);
        Assert.NotNull(began.Overlay);

        var keys = began.Overlay!.Options.Select(o => o.Key).ToList();
        Assert.Contains("mx", keys);
        Assert.DoesNotContain("m", keys);
    }
}
