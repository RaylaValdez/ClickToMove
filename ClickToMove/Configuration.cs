using ClickToMove.Input;
using Dalamud.Configuration;
using System;

namespace ClickToMove;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool PluginEnabled { get; set; } = true;

    public bool WorldClickEnabled { get; set; } = true;
    public MovementType WorldClickMovement { get; set; } = MovementType.Pathfind;

    public ModifierKey ClickModifier { get; set; } = ModifierKey.Shift;
    public bool RequireModifier { get; set; } = true;

    public bool FallbackToDirect { get; set; } = true;

    public bool StopOnWASD { get; set; } = true;
    public bool StopOnJump { get; set; } = true;
    public bool StopOnCast { get; set; } = true;
    public bool CancelInCombat { get; set; } = false;
    public bool StopWhenWindowInactive { get; set; } = true;

    public float ArrivalTolerance { get; set; } = 0.5f;
    public float DirectStopDistance { get; set; } = 0.2f;
    public bool DirectFaceTarget { get; set; } = true;
    public bool AllowFly { get; set; } = true;

    public bool ShowPlacementPreview { get; set; } = true;

    public bool ChatFeedbackOnFallback { get; set; } = true;
    public bool ChatFeedbackOnError { get; set; } = true;

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}
