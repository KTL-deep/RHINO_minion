using System.Text.Json;
using RhinoMinion.Protocol;

namespace RhinoMinion.Protocol.Tests;

public sealed class SerializationTests
{
    [Fact]
    public void RequestUsesSnakeCaseWireNames()
    {
        using var payload = JsonDocument.Parse("{\"client_name\":\"tests\"}");
        var request = new BridgeRequest(
            ProtocolConstants.CurrentVersion,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "handshake",
            payload.RootElement.Clone());

        var json = JsonSerializer.Serialize(request, JsonDefaults.Options);

        Assert.Contains("\"protocol_version\"", json);
        Assert.Contains("\"request_id\"", json);
        Assert.DoesNotContain("ProtocolVersion", json);
    }
}
