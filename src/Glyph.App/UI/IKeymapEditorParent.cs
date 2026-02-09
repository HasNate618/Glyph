using System.Collections.Generic;

namespace Glyph.App.UI;

public interface IKeymapEditorParent
{
    List<string> CachedActionIds { get; }
    List<(string Id, string Name)> CachedThemes { get; }
    void MarkUnsaved();
}
