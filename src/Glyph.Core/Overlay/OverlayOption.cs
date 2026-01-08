namespace Glyph.Core.Overlay;

public sealed record OverlayOption(string Key, string Description, bool IsLayer, bool IsAction)
{
    public IReadOnlyList<string> KeyCaps { get; } = Key.Select(c => c.ToString()).ToArray();
}
