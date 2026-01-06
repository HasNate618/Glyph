using System.Windows;

namespace Glyph.App.Overlay
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
            // Additional initialization logic can be added here
        }

        // Method to update the overlay with the current sequence and valid next keys
        public void UpdateOverlay(string currentSequence, string[] validNextKeys)
        {
            // Logic to update the overlay UI with the current sequence and valid next keys
        }

        // Method to show the overlay
        public void ShowOverlay()
        {
            this.Visibility = Visibility.Visible;
        }

        // Method to hide the overlay
        public void HideOverlay()
        {
            this.Visibility = Visibility.Hidden;
        }
    }
}