using Rhino.PlugIns;
using RhinoMinion.Plugin.Bridge;

namespace RhinoMinion.Plugin;

[System.Runtime.InteropServices.Guid("BCDD862D-C88E-4FEC-B21D-D8730CC07406")]
public sealed class RhinoMinionPlugin : PlugIn
{
    private BridgeClient? _bridgeClient;

    public static RhinoMinionPlugin? Instance { get; private set; }

    public RhinoMinionPlugin()
    {
        Instance = this;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        _bridgeClient = new BridgeClient(new RhinoRequestDispatcher());
        _bridgeClient.Start();
        return LoadReturnCode.Success;
    }

    protected override void OnShutdown()
    {
        _bridgeClient?.Dispose();
        _bridgeClient = null;
        base.OnShutdown();
    }
}
