using RhinoMinion.Protocol;

namespace RhinoMinion.Plugin.Bridge;

internal sealed class ToolException : Exception
{
    public ToolException(ErrorCode code, string message, string? operationId = null)
        : base(message)
    {
        Code = code;
        OperationId = operationId;
    }

    public ErrorCode Code { get; }

    public string? OperationId { get; }
}
