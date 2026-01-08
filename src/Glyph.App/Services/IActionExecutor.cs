using Glyph.Core.Actions;

namespace Glyph.App.Services;

public interface IActionExecutor
{
    Task ExecuteAsync(ActionRequest action, CancellationToken cancellationToken);
}
