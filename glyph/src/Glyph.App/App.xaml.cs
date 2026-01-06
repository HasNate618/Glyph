using System.Windows;

namespace Glyph.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Initialize application resources and services here
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up resources and services here
            base.OnExit(e);
        }
    }
}