using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;

namespace Glyph.App.Tray;

public partial class TrayMenuWindow : Window
{
    private bool _allowClose;

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
    }

    public void SetStartupChecked(bool isChecked)
    {
        StartOnStartupCheckBox.IsChecked = isChecked;
    }

    public void ShowAt(System.Windows.Point screenPoint)
    {
        if (!IsVisible)
        {
            Show();
        }

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

    private void OpenGuiButton_Click(object sender, RoutedEventArgs e)
    {
        OpenGuiAction?.Invoke();
        Hide();
    }

    private void OpenConfigButton_Click(object sender, RoutedEventArgs e)
    {
        OpenConfigAction?.Invoke();
        Hide();
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenLogsAction?.Invoke();
        Hide();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        ExitAction?.Invoke();
        ForceClose();
    }

    private void StartOnStartupCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        ToggleStartupAction?.Invoke(true);
    }

    private void StartOnStartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        ToggleStartupAction?.Invoke(false);
    }
}
