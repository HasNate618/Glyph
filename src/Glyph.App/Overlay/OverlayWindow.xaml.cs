using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using Glyph.Core.Overlay;
using Glyph.Win32.Windowing;
using Glyph.Win32.Interop;

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

        // If the theme requests no backdrop, ensure the underlying window background
        // is opaque and matches the panel color so square HWND corners do not show.
        try
        {
            if (string.Equals(backdrop, "None", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.Media.SolidColorBrush? opaqueBrush = null;
                try
                {
                    var val = System.Windows.Application.Current?.Resources["Glyph.Overlay.BackgroundBrush"];
                    if (val is SolidColorBrush sb)
                    {
                        var c = sb.Color;
                        opaqueBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, c.R, c.G, c.B));
                        if (opaqueBrush.CanFreeze) opaqueBrush.Freeze();
                    }
                }
                catch { }

                if (opaqueBrush is null)
                {
                    try
                    {
                        var conv = System.Windows.Media.ColorConverter.ConvertFromString(acrylicColor);
                        if (conv is System.Windows.Media.Color parsed)
                        {
                            opaqueBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, parsed.R, parsed.G, parsed.B));
                            if (opaqueBrush.CanFreeze) opaqueBrush.Freeze();
                        }
                    }
                    catch { }
                }

                if (opaqueBrush is not null)
                {
                    Background = opaqueBrush;
                }
                else
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0x1B, 0x1B, 0x1B));
                }
            }
            else
            {
                Background = System.Windows.Media.Brushes.Transparent;
            }
        }
        catch
        {
            // best-effort
        }

        if (!string.Equals(corners, "None", StringComparison.OrdinalIgnoreCase))
        {
            WindowEffects.TrySetRoundedCorners(_hwnd);
        }
    }

    // External callers can invoke this to force re-application of native backdrops / corners
    public void RefreshVisualEffects()
    {
        try
        {
            ApplyVisualEffects();
        }
        catch { }
    }

    private void PositionFromTheme()
    {
        // Determine the work area for the monitor that contains the foreground window.
        // Fall back to the primary monitor / SystemParameters if detection fails.
        var workArea = SystemParameters.WorkArea;
        try
        {
            // Determine the foreground window handle and its containing monitor
            var hwndForeground = NativeMethods.GetForegroundWindow();
            var hMonitor = Glyph.Win32.Windowing.MonitorHelper.GetMonitorForWindow(hwndForeground);
            if (hMonitor != IntPtr.Zero)
            {
                var monitorWorkArea = Glyph.Win32.Windowing.MonitorHelper.GetMonitorWorkArea(hMonitor);
                if (monitorWorkArea.HasValue)
                {
                    var rect = monitorWorkArea.Value;

                    // Convert physical pixels to WPF device-independent pixels (DIPs).
                    var transform = System.Windows.Media.Matrix.Identity;
                    try
                    {
                        var ps = PresentationSource.FromVisual(this);
                        if (ps?.CompositionTarget is not null)
                        {
                            transform = ps.CompositionTarget.TransformFromDevice;
                        }
                        else if (_hwnd != IntPtr.Zero)
                        {
                            var src = HwndSource.FromHwnd(_hwnd);
                            if (src?.CompositionTarget is not null)
                            {
                                transform = src.CompositionTarget.TransformFromDevice;
                            }
                        }
                    }
                    catch
                    {
                        transform = System.Windows.Media.Matrix.Identity;
                    }

                    var topLeft = transform.Transform(new System.Windows.Point(rect.Left, rect.Top));
                    var bottomRight = transform.Transform(new System.Windows.Point(rect.Right, rect.Bottom));
                    workArea = new System.Windows.Rect(topLeft.X, topLeft.Y, Math.Max(0, bottomRight.X - topLeft.X), Math.Max(0, bottomRight.Y - topLeft.Y));
                }
            }
        }
        catch
        {
            // ignore and keep primary workArea
        }

        var anchor = GetStringResource("Glyph.Overlay.ScreenAnchor") ?? "BottomRight";
        var padding = GetDoubleResource("Glyph.Overlay.ScreenPadding", 8.0);
        var offsetX = GetDoubleResource("Glyph.Overlay.OffsetX", 0.0);
        var offsetY = GetDoubleResource("Glyph.Overlay.OffsetY", 0.0);

        var width = ActualWidth;
        var height = ActualHeight;

        // If the window hasn't been measured/rendered yet (first show), ActualWidth/Height
        // may be 0. Fall back to measuring the root panel to compute desired size so
        // we can correctly position the overlay even before it's visible.
        if (width <= 0 || height <= 0)
        {
            try
            {
                RootPanel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                var ds = RootPanel.DesiredSize;
                if (ds.Width > 0) width = ds.Width;
                if (ds.Height > 0) height = ds.Height;
            }
            catch
            {
                // ignore and fall through to the existing guard
            }
        }

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
        // Respect theme-driven breadcrumbs-only mode. If enabled, hide discovery options and shrink background.
        var breadcrumbsOnly = false;
        try
        {
            var val = System.Windows.Application.Current?.Resources["Glyph.Overlay.BreadcrumbsOnly"];
            if (val is bool b) breadcrumbsOnly = b;
            else if (val is string s && bool.TryParse(s, out var parsed)) breadcrumbsOnly = parsed;
        }
        catch
        {
        }

        if (breadcrumbsOnly)
        {
            OptionsList.ItemsSource = Array.Empty<OverlayOption>();
            OptionsList.Visibility = Visibility.Collapsed;

            // Shrink background to breadcrumb width: remove min width and left-align content.
            try
            {
                RootPanel.MinWidth = 0;
                RootPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            }
            catch { }
        }
        else
        {
            OptionsList.ItemsSource = model.Options;
            OptionsList.Visibility = Visibility.Visible;

            // Restore panel sizing behavior
            try
            {
                var val = System.Windows.Application.Current?.Resources["Glyph.Overlay.PanelMinWidth"];
                if (val is double d) RootPanel.MinWidth = d;
                else if (val is int i) RootPanel.MinWidth = i;
                else RootPanel.MinWidth = 300.0;

                RootPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            }
            catch { }
        }   
    }

    public void PrepareForShow() {
        this.UpdateLayout();
        PositionFromTheme();
    }     
}
