using Glyph.Actions;
using Glyph.Core.Actions;

namespace Glyph.App.Services;

public sealed class ActionRuntimeExecutor : IActionExecutor
{
    private readonly ActionRuntime _runtime;

    public ActionRuntimeExecutor(ActionRuntime runtime)
    {
        _runtime = runtime;
    }

    public Task ExecuteAsync(ActionRequest action, CancellationToken cancellationToken)
    {
        return _runtime.ExecuteAsync(action, cancellationToken);
    }
}
