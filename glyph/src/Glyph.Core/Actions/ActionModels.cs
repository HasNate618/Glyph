using System;

namespace Glyph.Core.Actions
{
    public class ActionModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public object Parameters { get; set; }
        public int? TimeoutMs { get; set; }
        public bool RequiresElevation { get; set; }
        public string When { get; set; }
    }
}