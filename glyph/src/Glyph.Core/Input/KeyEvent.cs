using System;

namespace Glyph.Core.Input
{
    public class KeyEvent
    {
        public int ScanCode { get; set; }
        public bool IsExtended { get; set; }
        public int VirtualKey { get; set; }
        public bool IsKeyDown { get; set; }
        public bool IsInjected { get; set; }
        public bool IsRepeat { get; set; }

        public KeyEvent(int scanCode, bool isExtended, int virtualKey, bool isKeyDown, bool isInjected, bool isRepeat)
        {
            ScanCode = scanCode;
            IsExtended = isExtended;
            VirtualKey = virtualKey;
            IsKeyDown = isKeyDown;
            IsInjected = isInjected;
            IsRepeat = isRepeat;
        }
    }
}