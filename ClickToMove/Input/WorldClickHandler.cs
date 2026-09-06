using ClickToMove.Movement;
using ClickToMove.Util;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace ClickToMove.Input;

// World clicks are polled on the draw thread on purpose: ImGui mouse
// state is only valid there, and PlayerQuests already proves that
// ScreenToWorld works from Draw. No input hooks, no patch fragility.
internal sealed class WorldClickHandler
{
    private readonly Configuration config;
    private readonly MovementController controller;

    public WorldClickHandler(Configuration config, MovementController controller)
    {
        this.config = config;
        this.controller = controller;
    }

    public void OnDraw()
    {
        if (!this.config.PluginEnabled || !this.config.WorldClickEnabled)
            return;
        if (this.config.StopWhenWindowInactive && InputGuard.IsWindowInactive())
            return;
        if (InputGuard.IsPlayerBusyForStart())
            return;
        if (!MouseState.ModifierHeld(this.config.ClickModifier, this.config.RequireModifier))
            return;
        if (!MouseState.LeftReleased)
            return;
        if (MouseState.ReleaseWasDrag())
            return;
        if (!InputGuard.IsClickingInGameWorld())
            return;

        var player = Service.ObjectTable.LocalPlayer;
        if (player == null)
            return;

        if (!Service.GameGui.ScreenToWorld(ImGui.GetMousePos(), out var worldPos, 100000.0f))
            return;

        // Ignore accidental micro clicks next to the player.
        if (MathUtil.DistanceXZ(player.Position, worldPos) <= this.config.ArrivalTolerance)
            return;

        this.controller.RequestMove(worldPos);
    }
}
