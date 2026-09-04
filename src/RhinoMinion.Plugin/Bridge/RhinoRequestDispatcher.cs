using System.Text.Json;
using Rhino;
using RhinoMinion.Protocol;

namespace RhinoMinion.Plugin.Bridge;

internal sealed class RhinoRequestDispatcher
{
    public async Task<BridgeResponse> DispatchAsync(
        BridgeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProtocolVersion != ProtocolConstants.CurrentVersion)
        {
            return Failure(request.RequestId, ErrorCode.ProtocolError, "Unsupported protocol version.");
        }

        try
        {
            var result = request.Type switch
            {
                "get_capabilities" => RhinoToolExecutor.GetCapabilities(),
                "get_scene" => await UiThread.InvokeAsync(
                    () => RhinoToolExecutor.GetScene(RhinoDoc.ActiveDoc, request.Payload),
                    cancellationToken).ConfigureAwait(false),
                "execute_batch" => await UiThread.InvokeAsync(
                    () => ExecuteBatch(RhinoDoc.ActiveDoc, request.Payload),
                    cancellationToken).ConfigureAwait(false),
                _ => throw new ToolException(ErrorCode.ProtocolError, $"Unknown request type: {request.Type}")
            };
            return new BridgeResponse(request.RequestId, true, result);
        }
        catch (ToolException exception)
        {
            return Failure(request.RequestId, exception.Code, exception.Message, exception.OperationId);
        }
        catch (OperationCanceledException)
        {
            return Failure(request.RequestId, ErrorCode.Timeout, "Request was cancelled or timed out.");
        }
        catch (Exception exception)
        {
            return Failure(request.RequestId, ErrorCode.InternalError, exception.Message);
        }
    }

    private static JsonElement ExecuteBatch(RhinoDoc? document, JsonElement payload)
    {
        if (document is null)
        {
            throw new ToolException(ErrorCode.DocumentUnavailable, "No active Rhino document.");
        }

        var batch = payload.Deserialize<ExecuteBatchPayload>(JsonDefaults.Options)
            ?? throw new ToolException(ErrorCode.InvalidArgument, "Invalid batch payload.");
        if (batch.Operations.Count is < 1 or > ProtocolConstants.DefaultMaxOperationsPerBatch)
        {
            throw new ToolException(
                ErrorCode.InvalidArgument,
                $"Batch must contain 1-{ProtocolConstants.DefaultMaxOperationsPerBatch} operations.");
        }

        ValidateOperations(batch.Operations);
        var undoSerial = document.BeginUndoRecord(string.IsNullOrWhiteSpace(batch.Label) ? "RHINO Minion" : batch.Label);
        if (undoSerial == 0)
        {
            throw new ToolException(ErrorCode.InternalError, "Could not start Rhino Undo record.");
        }

        var operationResults = new List<object>();
        try
        {
            foreach (var operation in batch.Operations)
            {
                try
                {
                    var objectIds = RhinoToolExecutor.Execute(document, operation);
                    operationResults.Add(new
                    {
                        operation_id = operation.OperationId,
                        object_ids = objectIds.Select(id => id.ToString()).ToArray()
                    });
                }
                catch (ToolException exception)
                {
                    throw new ToolException(exception.Code, exception.Message, operation.OperationId);
                }
            }
            document.EndUndoRecord(undoSerial);
        }
        catch
        {
            document.EndUndoRecord(undoSerial);
            RhinoApp.RunScript("_Undo", false);
            document.Views.Redraw();
            throw;
        }

        document.Views.Redraw();
        return ToJsonElement(new { operations = operationResults });
    }

    private static void ValidateOperations(IReadOnlyList<ToolOperation> operations)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if (string.IsNullOrWhiteSpace(operation.OperationId) || !identifiers.Add(operation.OperationId))
            {
                throw new ToolException(ErrorCode.InvalidArgument, "Operation IDs must be non-empty and unique.");
            }
            RhinoToolExecutor.Validate(operation);
        }
    }

    internal static JsonElement ToJsonElement<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonDefaults.Options));
        return document.RootElement.Clone();
    }

    private static BridgeResponse Failure(
        Guid requestId,
        ErrorCode code,
        string message,
        string? operationId = null) =>
        new(requestId, false, Error: new BridgeError(code, message, operationId));
}
