using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhinoMinion.Protocol;

public sealed record BridgeRequest(
    [property: JsonPropertyName("protocol_version")] string ProtocolVersion,
    [property: JsonPropertyName("request_id")] Guid RequestId,
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] JsonElement Payload);

public sealed record BridgeResponse(
    [property: JsonPropertyName("request_id")] Guid RequestId,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("result")] JsonElement? Result = null,
    [property: JsonPropertyName("error")] BridgeError? Error = null);

public sealed record BridgeError(
    [property: JsonPropertyName("code")] ErrorCode Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("operation_id")] string? OperationId = null,
    [property: JsonPropertyName("details")] IReadOnlyDictionary<string, JsonElement>? Details = null);

public enum ErrorCode
{
    InvalidArgument,
    ObjectNotFound,
    GeometryFailed,
    DocumentUnavailable,
    Timeout,
    ProtocolError,
    InternalError
}

public sealed record ToolOperation(
    [property: JsonPropertyName("operation_id")] string OperationId,
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("arguments")] JsonElement Arguments);

public sealed record ExecuteBatchPayload(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("operations")] IReadOnlyList<ToolOperation> Operations);
