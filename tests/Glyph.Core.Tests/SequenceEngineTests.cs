using Xunit;
using Glyph.Core.Engine;

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
}
