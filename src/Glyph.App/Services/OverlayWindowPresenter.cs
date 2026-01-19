using Glyph.Core.Overlay;

namespace Glyph.App.Services;

public sealed class OverlayWindowPresenter : IOverlayPresenter
{
    private readonly OverlayWindow _window;
    private System.Threading.CancellationTokenSource? _hideCts;

    public OverlayWindowPresenter(OverlayWindow window)
    {
        _window = window;
    }

    public void Render(OverlayModel? overlay, bool forceHide, bool hideAfterSustain)
    {
        // Strict ordering: update model first, then show/hide.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (forceHide)
            {
                try { _hideCts?.Cancel(); } catch { }
                if (_window.IsVisible)
                {
                    _window.Hide();
                }
                return;
            }

            if (overlay is not null)
            {
                // Cancel any pending hide when new overlay arrives
                try { _hideCts?.Cancel(); } catch { }

                _window.Update(overlay);
                if (!_window.IsVisible)
                {
                    _window.Show();
                }

                if (hideAfterSustain)
                {
                    ScheduleHideAfterSustain();
                }
                return;
            }

            // overlay is null -> hide, but respect sustain duration from theme
            ScheduleHideAfterSustain();
        });
    }

    private void ScheduleHideAfterSustain()
    {
        var sustainMs = 0.0;
        try
        {
            var val = System.Windows.Application.Current?.Resources["Glyph.Overlay.SustainMs"];
            if (val is double d) sustainMs = d;
            else if (val is int i) sustainMs = i;
            else if (val is string s && double.TryParse(s, out var parsed)) sustainMs = parsed;
        }
        catch
        {
        }

        if (sustainMs <= 0 || !_window.IsVisible)
        {
            if (_window.IsVisible)
            {
                _window.Hide();
            }
            return;
        }

        // Delay hide by sustainMs unless cancelled by a new overlay
        try
        {
            _hideCts?.Cancel();
        }
        catch { }

        _hideCts = new System.Threading.CancellationTokenSource();
        var token = _hideCts.Token;
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(sustainMs), token);
                if (token.IsCancellationRequested) return;
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (_window.IsVisible) _window.Hide();
                });
            }
            catch (OperationCanceledException) { }
            catch { }
        }, token);
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
