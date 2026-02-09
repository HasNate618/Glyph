using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms;

namespace Glyph.App.Tray;

public partial class TrayMenuWindow : Window
{
    private bool _allowClose;
    private bool _startupEnabled;

    public Action? OpenGuiAction { get; set; }
    public Action? OpenConfigAction { get; set; }
    public Action? OpenLogsAction { get; set; }
    public Action? ExitAction { get; set; }
    public Action<bool>? ToggleStartupAction { get; set; }

    public TrayMenuWindow()
    {
        InitializeComponent();
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
        Deactivated += (_, _) => Hide();
        Loaded += TrayMenuWindow_Loaded;
        SourceInitialized += TrayMenuWindow_SourceInitialized;
    }

    private void TrayMenuWindow_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateSystemColors();
    }

    private void TrayMenuWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateSystemColors();
    }

    private void UpdateSystemColors()
    {
        // Use ControlColor for background - it's more reliable across themes
        // MenuColor can be black in dark mode, which isn't ideal for menus
        var bgColor = System.Windows.SystemColors.ControlColor;
        var textColor = System.Windows.SystemColors.ControlTextColor;
        var borderColor = System.Windows.SystemColors.ControlDarkColor;
        
        MenuBorder.Background = new SolidColorBrush(bgColor);
        MenuBorder.BorderBrush = new SolidColorBrush(borderColor);
        
        var textBrush = new SolidColorBrush(textColor);
        OpenGuiText.Foreground = textBrush;
        StartOnStartupText.Foreground = textBrush;
        OpenConfigText.Foreground = textBrush;
        OpenLogsText.Foreground = textBrush;
        ExitText.Foreground = textBrush;
    }

    private void MenuBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, border.ActualWidth, border.ActualHeight),
                RadiusX = 6,
                RadiusY = 6
            };
        }
    }

    public void SetStartupChecked(bool isChecked)
    {
        _startupEnabled = isChecked;
        UpdateStartupText();
    }

    private void UpdateStartupText()
    {
        StartOnStartupText.Text = _startupEnabled ? "✓ Start on startup" : "Start on startup";
    }

    public void ShowAt(System.Windows.Point screenPoint)
    {
        if (!IsVisible)
        {
            Show();
        }

        UpdateSystemColors();
        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(this);
        var x = screenPoint.X / dpi.DpiScaleX;
        var y = screenPoint.Y / dpi.DpiScaleY;

        var screen = Screen.FromPoint(new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));
        var work = screen.WorkingArea;
        var workLeft = work.Left / dpi.DpiScaleX;
        var workTop = work.Top / dpi.DpiScaleY;
        var workRight = work.Right / dpi.DpiScaleX;
        var workBottom = work.Bottom / dpi.DpiScaleY;

        // Anchor bottom-right of menu to cursor position
        var left = x - ActualWidth;
        var top = y - ActualHeight;

        // Clamp to working area
        left = Math.Min(Math.Max(left, workLeft), workRight - ActualWidth);
        top = Math.Min(Math.Max(top, workTop), workBottom - ActualHeight);

        Left = left;
        Top = top;

        Activate();
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    private void OpenGuiText_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenGuiAction?.Invoke();
        Hide();
    }

    private void OpenConfigText_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenConfigAction?.Invoke();
        Hide();
    }

    private void OpenLogsText_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenLogsAction?.Invoke();
        Hide();
    }

    private void ExitText_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ExitAction?.Invoke();
        ForceClose();
    }

    private void StartOnStartupText_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _startupEnabled = !_startupEnabled;
        UpdateStartupText();
        ToggleStartupAction?.Invoke(_startupEnabled);
    }

    private void MenuItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            textBlock.Background = new SolidColorBrush(System.Windows.SystemColors.HighlightColor);
            textBlock.Foreground = new SolidColorBrush(System.Windows.SystemColors.HighlightTextColor);
        }
    }

    private void MenuItem_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            textBlock.Background = System.Windows.Media.Brushes.Transparent;
            textBlock.Foreground = new SolidColorBrush(System.Windows.SystemColors.ControlTextColor);
        }
    }
}
