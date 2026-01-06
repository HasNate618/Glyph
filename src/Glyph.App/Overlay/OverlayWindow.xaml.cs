using System.Windows;

using Glyph.Core.Engine;

namespace Glyph.App;

public partial class OverlayWindow : Window
{
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

        // Position bottom-right with 8px padding from taskbar and edge
        WindowStartupLocation = WindowStartupLocation.Manual;
        Loaded += (_, _) => PositionBottomRight();
        SizeChanged += (_, _) => PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        // Ensure the overlay sits 8px from the taskbar and screen edge.
        var padding = 8.0;
        Left = Math.Max(workArea.Left + padding, workArea.Right - ActualWidth - padding);
        Top = Math.Max(workArea.Top + padding, workArea.Bottom - ActualHeight - padding);
    }

    public void Update(OverlayModel model)
    {
        SequenceText.Text = model.Sequence;
        OptionsList.ItemsSource = model.Options;
    }
}
