namespace Glyph.Core.Overlay;

public sealed record OverlayPolicy(
    bool SubstituteProgramLayerLabelWithActiveProcess,
    bool ShowLookaheadForBlankIntermediates,
    bool ShowRepeatedKeyDisambiguation)
{
    public static OverlayPolicy Default { get; } = new(
        SubstituteProgramLayerLabelWithActiveProcess: true,
        ShowLookaheadForBlankIntermediates: true,
        ShowRepeatedKeyDisambiguation: true);
}
