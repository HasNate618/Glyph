using Microsoft.Win32;

namespace Glyph.App;

internal static class WindowsThemeHelper
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            if (key?.GetValue(AppsUseLightThemeValue) is int value)
            {
                return value > 0;
            }
        }
        catch
        {
            // Ignore and fall back to light theme.
        }

        return true;
    }

    public static string GetIconFileName()
    {
        return IsLightTheme() ? "LogoBlack.ico" : "LogoWhite.ico";
    }
}
