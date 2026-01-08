namespace Glyph.Core.Actions;

public sealed class ActionRequest
{
    public string? ActionId { get; init; }
    public string? TypeText { get; init; }
    public string? SendSpec { get; init; }

    public List<ActionRequest>? Steps { get; init; }

    public string? ExecPath { get; init; }
    public string? ExecArgs { get; init; }
    public string? ExecCwd { get; init; }

    public ActionRequest() { }
    public ActionRequest(string actionId) => ActionId = actionId;
}
