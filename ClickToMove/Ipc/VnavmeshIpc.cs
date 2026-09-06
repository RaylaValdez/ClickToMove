using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;
using System.Numerics;

namespace ClickToMove.Ipc;

// Thin wrapper around the vnavmesh CallGate API.
// Every call is guarded by HasFunction/HasAction and try/catch,
// so a missing or half loaded vnavmesh can never throw into our tick.
// Endpoint list matches vnavmesh IPCProvider plus the reference
// implementation in Jaksuhn ffxiv-bundleoftweaks / clib.
internal sealed class VnavmeshIpc
{
    private readonly ICallGateSubscriber<bool> navIsReady;
    private readonly ICallGateSubscriber<float> navBuildProgress;
    private readonly ICallGateSubscriber<object> pathStop;
    private readonly ICallGateSubscriber<bool> pathIsRunning;
    private readonly ICallGateSubscriber<Vector3, bool, bool> pathfindAndMoveTo;
    private readonly ICallGateSubscriber<bool> pathfindInProgress;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> pointOnFloor;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> nearestPointReachable;
    private readonly ICallGateSubscriber<Vector3?> flagToPoint;

    public VnavmeshIpc(IDalamudPluginInterface pluginInterface)
    {
        this.navIsReady = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        this.navBuildProgress = pluginInterface.GetIpcSubscriber<float>("vnavmesh.Nav.BuildProgress");
        this.pathStop = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        this.pathIsRunning = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
        this.pathfindAndMoveTo = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        this.pathfindInProgress = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        this.pointOnFloor = pluginInterface.GetIpcSubscriber<Vector3, bool, float, Vector3?>("vnavmesh.Query.Search.PointOnFloor");
        this.nearestPointReachable = pluginInterface.GetIpcSubscriber<Vector3, float, float, Vector3?>("vnavmesh.Query.Search.NearestPointReachable");
        this.flagToPoint = pluginInterface.GetIpcSubscriber<Vector3?>("vnavmesh.Query.Search.FlagToPoint");
    }

    public bool HasVnavmesh =>
        this.navIsReady.HasFunction ||
        this.pathfindAndMoveTo.HasFunction ||
        this.pathIsRunning.HasFunction;

    public bool IsReady()
    {
        try
        {
            return this.navIsReady.HasFunction && this.navIsReady.InvokeFunc();
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: vnavmesh IsReady check failed: " + ex.Message);
            return false;
        }
    }

    public float BuildProgress()
    {
        try
        {
            return this.navBuildProgress.HasFunction ? this.navBuildProgress.InvokeFunc() : -1.0f;
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: vnavmesh BuildProgress check failed: " + ex.Message);
            return -1.0f;
        }
    }

    public bool IsRunning()
    {
        try
        {
            return this.pathIsRunning.HasFunction && this.pathIsRunning.InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    public bool IsPathfinding()
    {
        try
        {
            return this.pathfindInProgress.HasFunction && this.pathfindInProgress.InvokeFunc();
        }
        catch
        {
            return false;
        }
    }

    public bool PathfindAndMoveTo(Vector3 destination, bool fly)
    {
        try
        {
            return this.pathfindAndMoveTo.HasFunction && this.pathfindAndMoveTo.InvokeFunc(destination, fly);
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: vnavmesh PathfindAndMoveTo failed: " + ex.Message);
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            if (this.pathStop.HasAction)
                this.pathStop.InvokeAction();
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: vnavmesh Stop failed: " + ex.Message);
        }
    }

    public Vector3? PointOnFloor(Vector3 position, bool allowUnlandable = false, float halfExtentXZ = 5.0f)
    {
        try
        {
            return this.pointOnFloor.HasFunction ? this.pointOnFloor.InvokeFunc(position, allowUnlandable, halfExtentXZ) : null;
        }
        catch
        {
            return null;
        }
    }

    public Vector3? NearestPointReachable(Vector3 position, float halfExtentXZ = 5.0f, float halfExtentY = 5.0f)
    {
        try
        {
            return this.nearestPointReachable.HasFunction ? this.nearestPointReachable.InvokeFunc(position, halfExtentXZ, halfExtentY) : null;
        }
        catch
        {
            return null;
        }
    }
}
