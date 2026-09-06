using ClickToMove.Ipc;
using System.Numerics;

namespace ClickToMove.Movement;

// Pathfind engine. Delegates all movement to vnavmesh through IPC.
// Never touches game memory itself.
internal sealed class PathfindEngine : IMovementEngine
{
    private readonly VnavmeshIpc ipc;

    public PathfindEngine(VnavmeshIpc ipc)
    {
        this.ipc = ipc;
    }

    public string Name => "Pathfind";

    public bool IsAvailable()
    {
        return this.ipc.HasVnavmesh && this.ipc.IsReady();
    }

    public string UnavailableReason()
    {
        if (!this.ipc.HasVnavmesh)
            return "vnavmesh not installed or not loaded";
        var progress = this.ipc.BuildProgress();
        if (progress >= 0.0f && progress < 1.0f)
            return "navmesh still building (" + (int)(progress * 100.0f) + "%)";
        return "navmesh not ready for this zone";
    }

    public bool Start(Vector3 destination, bool fly)
    {
        // Snap the click onto the mesh so off mesh clicks still work.
        var snapped = this.ipc.PointOnFloor(destination)
            ?? this.ipc.NearestPointReachable(destination)
            ?? destination;
        return this.ipc.PathfindAndMoveTo(snapped, fly);
    }

    public void Stop()
    {
        this.ipc.Stop();
    }

    public bool IsRunning()
    {
        return this.ipc.IsRunning() || this.ipc.IsPathfinding();
    }

    public void Update()
    {
        // vnavmesh drives itself. The controller polls IsRunning.
    }
}
