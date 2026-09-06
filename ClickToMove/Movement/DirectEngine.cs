using ClickToMove.Input;
using ClickToMove.Util;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Numerics;

namespace ClickToMove.Movement;

internal enum DirectStopReason
{
    Arrived,
    UserInput,
    Jump,
    Cast,
    Combat,
    Interrupted,
    Stuck,
    Timeout,
    PlayerGone,
}

// Straight line fallback mover. Drives through OverrideMovement, which
// hooks the game's own movement input readers, so walking works with
// default and custom keybinds. The engine itself only supervises:
// arrival, stuck, timeout, and user cancel conditions.
// Runs on Framework.Update.
internal sealed class DirectEngine : IMovementEngine
{
    private const float StuckDistance = 0.15f;
    private const double StuckSeconds = 1.2;
    private const double TimeoutSeconds = 30.0;

    private readonly Configuration config;
    private readonly OverrideMovement? movement;
    private readonly string? initError;

    private bool enabled;
    private Vector3 destination;
    private DateTimeOffset startTime;
    private Vector3 lastPosition;
    private DateTimeOffset lastMoveTime;

    public DirectEngine(Configuration config, OverrideMovement? movement, string? initError)
    {
        this.config = config;
        this.movement = movement;
        this.initError = initError;
    }

    public event Action<DirectEngine, DirectStopReason>? Stopped;

    public string Name => "Direct";

    public bool IsRunning()
    {
        return this.enabled;
    }

    public bool IsAvailable()
    {
        return this.movement != null;
    }

    public string UnavailableReason()
    {
        return this.initError ?? "movement hooks unavailable for this game version";
    }

    public bool Start(Vector3 destination, bool fly)
    {
        if (this.movement == null)
            return false;

        var player = Service.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead)
            return false;

        this.enabled = true;
        this.destination = destination;
        this.movement.DesiredPosition = destination;
        this.movement.Enabled = true;
        this.startTime = DateTimeOffset.UtcNow;
        this.lastPosition = player.Position;
        this.lastMoveTime = this.startTime;
        return true;
    }

    public void Stop()
    {
        this.enabled = false;
        if (this.movement == null)
            return;
        try
        {
            this.movement.Enabled = false;
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: movement disable failed: " + ex.Message);
        }
    }

    public void Update()
    {
        if (!this.enabled)
            return;

        try
        {
            this.Tick();
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: DirectEngine tick failed: " + ex.Message);
            this.Finish(DirectStopReason.PlayerGone);
        }
    }

    private void Tick()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead)
        {
            this.Finish(DirectStopReason.PlayerGone);
            return;
        }

        var position = player.Position;
        var condition = Service.Condition;

        if (this.config.StopOnWASD && MouseState.WasdDown)
        {
            this.Finish(DirectStopReason.UserInput);
            return;
        }

        if (this.config.StopOnJump && (condition[ConditionFlag.Jumping] || condition[ConditionFlag.Jumping61]))
        {
            this.Finish(DirectStopReason.Jump);
            return;
        }

        if (this.config.StopOnCast && condition[ConditionFlag.Casting])
        {
            this.Finish(DirectStopReason.Cast);
            return;
        }

        if (this.config.CancelInCombat && condition[ConditionFlag.InCombat])
        {
            this.Finish(DirectStopReason.Combat);
            return;
        }

        if (condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.WatchingCutscene78]
            || condition[ConditionFlag.OccupiedInCutSceneEvent]
            || condition[ConditionFlag.BetweenAreas]
            || condition[ConditionFlag.BetweenAreas51])
        {
            this.Finish(DirectStopReason.Interrupted);
            return;
        }

        if (Vector3.Distance(position, this.destination) <= this.config.DirectStopDistance)
        {
            this.Finish(DirectStopReason.Arrived);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if ((now - this.startTime).TotalSeconds >= TimeoutSeconds)
        {
            this.Finish(DirectStopReason.Timeout);
            return;
        }

        // Stuck detection. Paused while casting, since casts root the player.
        if (Vector3.Distance(position, this.lastPosition) >= StuckDistance)
        {
            this.lastPosition = position;
            this.lastMoveTime = now;
        }
        else if (!condition[ConditionFlag.Casting] && (now - this.lastMoveTime).TotalSeconds >= StuckSeconds)
        {
            this.Finish(DirectStopReason.Stuck);
            return;
        }

        // Legacy movement mode expresses input relative to facing, so keep
        // facing the target. Standard mode needs no facing assist.
        if (IsLegacyMode() && this.config.DirectFaceTarget)
            FacePlayer(this.destination);
    }

    private void Finish(DirectStopReason reason)
    {
        this.Stop();
        try
        {
            this.Stopped?.Invoke(this, reason);
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: DirectEngine stop handler failed: " + ex.Message);
        }
    }

    private static unsafe void FacePlayer(Vector3 destination)
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null)
            return;
        var position = player.Position;
        if (MathUtil.DistanceXZ(position, destination) < 0.05f)
            return;
        var gameObject = (GameObject*)player.Address;
        if (gameObject == null)
            return;
        gameObject->SetRotation(MathUtil.YawTo(position, destination));
    }

    private static bool IsLegacyMode()
    {
        try
        {
            return Service.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
        }
        catch
        {
            return false;
        }
    }
}
