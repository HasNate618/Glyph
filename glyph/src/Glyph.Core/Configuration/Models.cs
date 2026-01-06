using System.Collections.Generic;

namespace Glyph.Core.Configuration
{
    public class KeyBinding
    {
        public string Sequence { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
    }

    public class ActionDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public class ConfigurationModel
    {
        public List<KeyBinding> KeyBindings { get; set; }
        public List<ActionDefinition> Actions { get; set; }
        public string LeaderKey { get; set; }
        public int SessionTimeoutMs { get; set; }
    }
}