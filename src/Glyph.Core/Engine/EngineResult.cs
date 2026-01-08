using Glyph.Core.Actions;
using Glyph.Core.Overlay;

namespace Glyph.Core.Engine;

public readonly record struct EngineResult(bool Consumed, OverlayModel? Overlay, ActionRequest? Action, TimeSpan? ExecuteAfter)
{
    public static EngineResult None => new(false, null, null, null);
    public static EngineResult ConsumedNoOverlay => new(true, null, null, null);
}
