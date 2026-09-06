using ClickToMove.Input;
using ClickToMove.Util;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ClickToMove.Movement;

// Picks an engine per click, owns the current destination, and turns
// engine stops into preview pin and chat feedback. Runs on Framework.Update.
internal sealed class MovementController : IDisposable
{
    private readonly Configuration config;
    private readonly PathfindEngine pathfind;
    private readonly DirectEngine direct;
    private readonly Chat chat;

    private IMovementEngine? activeEngine;
    private bool activeIsPathfind;
    private bool pathfindWasRunning;
    private Vector3? destination;
    private bool disposed;

    public MovementController(Configuration config, PathfindEngine pathfind, DirectEngine direct, Chat chat)
    {
        this.config = config;
        this.pathfind = pathfind;
        this.direct = direct;
        this.chat = chat;
        this.direct.Stopped += this.OnDirectStopped;
    }

    public bool IsMoving => this.activeEngine?.IsRunning() ?? false;

    public string ActiveEngineName => this.activeIsPathfind ? this.pathfind.Name : this.direct.Name;

    // Raw destination while a move is active. Backs the placement
    // preview pin. Cleared on arrival, cancel, and engine finish.
    public Vector3? PinnedDestination => this.destination;

    public void Dispose()
    {
        if (this.disposed)
            return;
        this.disposed = true;
        this.direct.Stopped -= this.OnDirectStopped;
        this.StopEngines();
    }

    public void RequestMove(Vector3 destination)
    {
        this.StopEngines();

        var preferred = this.config.WorldClickMovement;
        var fly = this.config.AllowFly && Service.Condition[ConditionFlag.InFlight];

        if (preferred == MovementType.Pathfind && this.pathfind.IsAvailable())
        {
            if (this.pathfind.Start(destination, fly))
            {
                this.SetActive(this.pathfind, true, destination);
                return;
            }

            this.chat.Error("Pathfind start failed, trying direct move.");
        }

        if (preferred == MovementType.Direct || this.config.FallbackToDirect)
        {
            if (preferred == MovementType.Pathfind && this.config.ChatFeedbackOnFallback)
                this.chat.Info("vnavmesh unavailable (" + this.pathfind.UnavailableReason() + "), using direct move.");

            if (!this.direct.IsAvailable())
            {
                this.chat.Error("Direct move unavailable: " + this.direct.UnavailableReason() + ".");
                return;
            }

            if (this.direct.Start(destination, fly))
            {
                this.SetActive(this.direct, false, destination);
                return;
            }

            this.chat.Error("Direct move failed to start.");
            return;
        }

        var detail = this.pathfind.UnavailableReason();
        if (!this.direct.IsAvailable())
            detail += " Direct also unavailable: " + this.direct.UnavailableReason() + ".";
        this.chat.Error("Move ignored: " + detail);
    }

    public void StopAll()
    {
        this.StopEngines();
        this.destination = null;
    }

    // Forces a Direct move regardless of config. Used by the test button.
    public void RequestDirect(Vector3 destination)
    {
        this.StopEngines();
        if (!this.direct.IsAvailable())
        {
            this.chat.Error("Direct move unavailable: " + this.direct.UnavailableReason() + ".");
            return;
        }

        if (this.direct.Start(destination, false))
            this.SetActive(this.direct, false, destination);
    }

    // Test helper: walk 5 yalms in the direction the player faces.
    public void TestDirectForward()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null)
            return;
        var yaw = player.Rotation;
        var offset = new Vector3(MathF.Sin(yaw) * 5.0f, 0.0f, MathF.Cos(yaw) * 5.0f);
        this.RequestDirect(player.Position + offset);
    }

    public void Update(IFramework framework)
    {
        if (this.activeEngine == this.direct && this.direct.IsRunning())
            this.direct.Update();

        if (this.activeIsPathfind)
        {
            var running = this.pathfind.IsRunning();
            if (this.pathfindWasRunning && !running)
            {
                // Path finished or was cancelled externally. Either way the
                // preview pin has served its purpose.
                this.destination = null;
                this.activeEngine = null;
                this.activeIsPathfind = false;
            }

            this.pathfindWasRunning = running;
        }
    }

    public List<string> StatusLines()
    {
        var lines = new List<string>
        {
            "Moving: " + (this.IsMoving ? "yes (" + this.ActiveEngineName + ")" : "no"),
            "Destination: " + (this.destination?.ToString() ?? "none"),
            "Direct: " + (this.direct.IsAvailable() ? "ready" : "unavailable (" + this.direct.UnavailableReason() + ")"),
        };
        return lines;
    }

    private void SetActive(IMovementEngine engine, bool isPathfind, Vector3 destination)
    {
        this.activeEngine = engine;
        this.activeIsPathfind = isPathfind;
        this.pathfindWasRunning = isPathfind && engine.IsRunning();
        this.destination = destination;
    }

    private void StopEngines()
    {
        try
        {
            this.direct.Stop();
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: Direct stop failed: " + ex.Message);
        }

        try
        {
            this.pathfind.Stop();
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: Pathfind stop failed: " + ex.Message);
        }

        this.activeEngine = null;
        this.activeIsPathfind = false;
        this.pathfindWasRunning = false;
    }

    private void OnDirectStopped(DirectEngine engine, DirectStopReason reason)
    {
        this.destination = null;
        this.activeEngine = null;

        switch (reason)
        {
            case DirectStopReason.Arrived:
            case DirectStopReason.UserInput:
            case DirectStopReason.Jump:
            case DirectStopReason.Cast:
            case DirectStopReason.Combat:
            case DirectStopReason.Interrupted:
                break;
            case DirectStopReason.Stuck:
                if (this.config.ChatFeedbackOnError)
                    this.chat.Error("Direct move stopped: stuck. Try vnavmesh for paths around obstacles.");
                break;
            case DirectStopReason.Timeout:
                if (this.config.ChatFeedbackOnError)
                    this.chat.Error("Direct move stopped: timed out before arrival.");
                break;
            case DirectStopReason.PlayerGone:
                break;
        }
    }
}
