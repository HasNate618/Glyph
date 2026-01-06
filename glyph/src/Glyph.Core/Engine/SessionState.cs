using System;

namespace Glyph.Core.Engine
{
    public class SessionState
    {
        public bool IsActive { get; private set; }
        public string Buffer { get; private set; }
        public DateTime LastActivity { get; private set; }
        public TimeSpan SessionTimeout { get; set; }

        public SessionState(TimeSpan sessionTimeout)
        {
            IsActive = false;
            Buffer = string.Empty;
            LastActivity = DateTime.Now;
            SessionTimeout = sessionTimeout;
        }

        public void StartSession()
        {
            IsActive = true;
            Buffer = string.Empty;
            LastActivity = DateTime.Now;
        }

        public void EndSession()
        {
            IsActive = false;
            Buffer = string.Empty;
        }

        public void AddToBuffer(string input)
        {
            Buffer += input;
            LastActivity = DateTime.Now;
        }

        public bool IsSessionTimedOut()
        {
            return IsActive && (DateTime.Now - LastActivity) > SessionTimeout;
        }

        public void ResetBuffer()
        {
            Buffer = string.Empty;
        }
    }
}