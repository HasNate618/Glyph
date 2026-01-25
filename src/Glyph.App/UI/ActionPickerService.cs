using System;
using System.Collections.Generic;
using System.Linq;
using Glyph.Actions;

namespace Glyph.App.UI
{
    /// <summary>
    /// Service to enumerate and manage available actions for the keymap editor
    /// </summary>
    public class ActionPickerService
    {
        public ActionPickerService()
        {
        }

        /// <summary>
        /// Get all known action IDs suitable for the action dropdown
        /// </summary>
        public IEnumerable<string> GetAvailableActions()
        {
            var knownActions = new[]
            {
                "openBrowser",
                "mediaPlayPause",
                "mediaNext",
                "mediaPrev",
                "volumeMute",
                "windowMinimize",
                "openSpotify",
                "logForeground",
                "openLogs",
                "openConfig",
                "openGlyphGui",
                "openKeymapEditor",
                "quitGlyph",
                "reloadKeymaps",
                "toggleBreadcrumbsMode"
            };

            return knownActions.OrderBy(a => a);
        }

        /// <summary>
        /// Validate that an action exists
        /// </summary>
        public bool IsValidAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return true; // Empty is valid (layer)

            return GetAvailableActions().Contains(actionId);
        }

        /// <summary>
        /// Get theme options (populated from theme files)
        /// </summary>
        public IEnumerable<string> GetAvailableThemes()
        {
            return new[]
            {
                "fluent",
                "catppuccinMocha",
                "light",
                "nord",
                "darcula",
                "rosePine",
                "tokyoNight"
            };
        }
    }
}
