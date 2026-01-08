using Glyph.Core.Overlay;

namespace Glyph.App.Services;

public sealed class OverlayWindowPresenter : IOverlayPresenter
{
    private readonly OverlayWindow _window;

    public OverlayWindowPresenter(OverlayWindow window)
    {
        _window = window;
    }

    public void Render(OverlayModel? overlay)
    {
        // Strict ordering: update model first, then show/hide.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (overlay is not null)
            {
                _window.Update(overlay);
                if (!_window.IsVisible)
                {
                    _window.Show();
                }
            }
            else
            {
                if (_window.IsVisible)
                {
                    _window.Hide();
                }
            }
        });
    }

    public void Dispose()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_window.IsVisible)
            {
                _window.Hide();
            }
        });
    }
}
