using Glyph.Core.Overlay;

namespace Glyph.App.Services;

public interface IOverlayPresenter : IDisposable
{
    void Render(OverlayModel? overlay, bool forceHide, bool hideAfterSustain);
}
