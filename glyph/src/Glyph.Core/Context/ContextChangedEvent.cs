using System;

namespace Glyph.Core.Context
{
    public class ContextChangedEventArgs : EventArgs
    {
        public string PreviousContext { get; }
        public string CurrentContext { get; }

        public ContextChangedEventArgs(string previousContext, string currentContext)
        {
            PreviousContext = previousContext;
            CurrentContext = currentContext;
        }
    }

    public delegate void ContextChangedEventHandler(object sender, ContextChangedEventArgs e);
}