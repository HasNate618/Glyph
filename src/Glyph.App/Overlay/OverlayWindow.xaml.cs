using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using Glyph.Core.Overlay;
using Glyph.Win32.Windowing;

namespace Glyph.App;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;

    public OverlayWindow()
    {
        InitializeComponent();

        // Native Windows shadow (subtle, soft)
        this.Resources["DropShadowEffect"] = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = System.Windows.Media.Color.FromArgb(120, 30, 30, 30),
            BlurRadius = 24,
            ShadowDepth = 0,
            Opacity = 0.5
        };

        // Position is theme-configurable via resources.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Loaded += (_, _) => PositionFromTheme();
        SizeChanged += (_, _) => PositionFromTheme();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                ApplyVisualEffects();
                PositionFromTheme();
            }
        };

        SourceInitialized += (_, _) =>
        {
            try
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                if (_hwnd != IntPtr.Zero)
                {
                    // Ensure the underlying HWND surface is transparent so DWM backdrops can show.
                    try
                    {
                        var source = HwndSource.FromHwnd(_hwnd);
                        if (source?.CompositionTarget is not null)
                        {
                            source.CompositionTarget.BackgroundColor = Colors.Transparent;
                        }
                    }
                    catch
                    {
                    }

                    ApplyVisualEffects();
                }
            }
            catch
            {
                // Best-effort visuals only.
            }
        };
    }

    private void ApplyVisualEffects()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var backdrop = GetStringResource("Glyph.Overlay.WindowBackdrop") ?? "Auto";
        var acrylicColor = GetStringResource("Glyph.Overlay.WindowAcrylicColor") ?? "#991B1B1B";
        var corners = GetStringResource("Glyph.Overlay.WindowCorners") ?? "Round";

        WindowEffects.ApplyBackdrop(_hwnd, backdrop, acrylicColor);

        if (!string.Equals(corners, "None", StringComparison.OrdinalIgnoreCase))
        {
            WindowEffects.TrySetRoundedCorners(_hwnd);
        }
    }

    private void PositionFromTheme()
    {
        var workArea = SystemParameters.WorkArea;

        var anchor = GetStringResource("Glyph.Overlay.ScreenAnchor") ?? "BottomRight";
        var padding = GetDoubleResource("Glyph.Overlay.ScreenPadding", 8.0);
        var offsetX = GetDoubleResource("Glyph.Overlay.OffsetX", 0.0);
        var offsetY = GetDoubleResource("Glyph.Overlay.OffsetY", 0.0);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double left;
        double top;

        var a = anchor.Replace("_", string.Empty).Replace(" ", string.Empty);
        if (string.Equals(a, "TopLeft", StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Left + padding;
            top = workArea.Top + padding;
        }
        else if (string.Equals(a, "TopRight", StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Right - width - padding;
            top = workArea.Top + padding;
        }
        else if (string.Equals(a, "BottomLeft", StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Left + padding;
            top = workArea.Bottom - height - padding;
        }
        else if (string.Equals(a, "BottomCenter", StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Left + (workArea.Width - width) / 2.0;
            top = workArea.Bottom - height - padding;
        }
        else if (string.Equals(a, "TopCenter", StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Left + (workArea.Width - width) / 2.0;
            top = workArea.Top + padding;
        }
        else if (string.Equals(a, "Center", StringComparison.OrdinalIgnoreCase))
        {
            left = workArea.Left + (workArea.Width - width) / 2.0;
            top = workArea.Top + (workArea.Height - height) / 2.0;
        }
        else
        {
            // BottomRight (default)
            left = workArea.Right - width - padding;
            top = workArea.Bottom - height - padding;
        }

        left += offsetX;
        top += offsetY;

        Left = Math.Max(workArea.Left + padding, Math.Min(left, workArea.Right - width - padding));
        Top = Math.Max(workArea.Top + padding, Math.Min(top, workArea.Bottom - height - padding));
    }

    private string? GetStringResource(string key)
    {
        try
        {
            return (System.Windows.Application.Current?.Resources[key] as string)?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private double GetDoubleResource(string key, double fallback)
    {
        try
        {
            var val = System.Windows.Application.Current?.Resources[key];
            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is string s && double.TryParse(s, out var parsed)) return parsed;
            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public void Update(OverlayModel model)
    {
        SequenceText.Text = model.Sequence;
        OptionsList.ItemsSource = model.Options;

        // Ensure theme-driven placement stays correct as the overlay size changes.
        PositionFromTheme();
    }
}
