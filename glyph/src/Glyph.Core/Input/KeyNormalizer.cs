using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Glyph.Core.Input
{
    public class KeyNormalizer
    {
        public NormalizedKeyEvent Normalize(KeyEvent keyEvent)
        {
            // Normalize the key event to a consistent format
            return new NormalizedKeyEvent
            {
                PhysicalKey = keyEvent.PhysicalKey,
                LogicalKey = keyEvent.LogicalKey,
                Modifiers = keyEvent.Modifiers,
                IsKeyDown = keyEvent.IsKeyDown,
                IsInjected = keyEvent.IsInjected,
                IsRepeat = keyEvent.IsRepeat
            };
        }
    }

    public class NormalizedKeyEvent
    {
        public int PhysicalKey { get; set; }
        public Key LogicalKey { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public bool IsKeyDown { get; set; }
        public bool IsInjected { get; set; }
        public bool IsRepeat { get; set; }
    }
}