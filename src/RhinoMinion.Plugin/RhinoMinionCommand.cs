using Rhino;
using Rhino.Commands;
using RhinoMinion.Protocol;

namespace RhinoMinion.Plugin;

public sealed class RhinoMinionCommand : Command
{
    public override string EnglishName => "RhinoMinion";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        RhinoApp.WriteLine(
            "RHINO Minion bridge loaded. Protocol version: {0}. Backend: ws://127.0.0.1:8766/ws/rhino",
            ProtocolConstants.CurrentVersion);
        return Result.Success;
    }
}
