using ClickToMove.Input;
using ClickToMove.Movement;
using ClickToMove.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace ClickToMove.Windows;

// Vanilla style ground targeting preview, matching the PlayerQuests
// position picker look. Follows the cursor while armed: always for
// modifier None, only while held for any other modifier. Once a move
// starts it pins to the destination until arrival or cancel clears it.
internal sealed class PreviewWindow : Window
{
    private readonly Configuration config;
    private readonly MovementController controller;

    public PreviewWindow(Configuration config, MovementController controller)
        : base(
            "ClickToMove Placement Preview",
            ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoFocusOnAppearing,
            true)
    {
        this.config = config;
        this.controller = controller;
        this.IsOpen = true;
        this.RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        base.PreDraw();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
    }

    public override void Draw()
    {
        if (!this.config.PluginEnabled || !this.config.WorldClickEnabled || !this.config.ShowPlacementPreview)
            return;

        Vector3 at;
        var pinned = this.controller.PinnedDestination;
        if (pinned != null)
        {
            at = pinned.Value;
        }
        else
        {
            if (this.config.ClickModifier != ModifierKey.None
                && !MouseState.ModifierHeld(this.config.ClickModifier, this.config.RequireModifier))
                return;
            if (InputGuard.IsPlayerBusyForStart())
                return;
            if (this.config.StopWhenWindowInactive && InputGuard.IsWindowInactive())
                return;
            if (!InputGuard.IsClickingInGameWorld())
                return;
            if (!Service.GameGui.ScreenToWorld(ImGui.GetMousePos(), out at, 100000.0f))
                return;
        }

        var gameGui = Service.GameGui;
        GroundDraw.CircleXZ(gameGui, at, 0.76f, new Brush { Color = new Vector4(171, 133, 130, 100) / 255f, Fill = new Vector4(171, 133, 130, 100) / 255f, Thickness = 2.5f });
        GroundDraw.CircleXZ(gameGui, at, 0.7f, new Brush { Color = new Vector4(255, 255, 180, 200) / 255f, Fill = new Vector4(255, 255, 180, 0) / 255f, Thickness = 2.5f });
        GroundDraw.CircleXZ(gameGui, at, 0.74f, new Brush { Color = new Vector4(236, 170, 108, 200) / 255f, Fill = new Vector4(236, 170, 108, 0) / 255f, Thickness = 2.5f });
        GroundDraw.CircleXZ(gameGui, at, 0.72f, new Brush { Color = new Vector4(255, 204, 113, 200) / 255f, Fill = new Vector4(255, 204, 113, 0) / 255f, Thickness = 2.5f });
        GroundDraw.CircleXZ(gameGui, at, 0.35f, new Brush { Color = new Vector4(255, 174, 78, 200) / 255f, Fill = new Vector4(255, 174, 78, 0) / 255f, Thickness = 2.5f });
        GroundDraw.CircleXZ(gameGui, at, 0.349f, new Brush { Color = new Vector4(255, 255, 166, 50) / 255f, Fill = new Vector4(255, 255, 166, 50) / 255f, Thickness = 2.5f });
        GroundDraw.RotatingCircle4SegmentsXZ(gameGui, at, 0.7f, new Brush { Color = new Vector4(236, 170, 108, 200) / 255f, Fill = new Vector4(236, 170, 108, 0) / 255f, Thickness = 25f });
        GroundDraw.RotatingCircle4SegmentsXZ(gameGui, at, 0.15f, new Brush { Color = new Vector4(236, 170, 108, 200) / 255f, Fill = new Vector4(236, 170, 108, 0) / 255f, Thickness = 25f }, 45f, MathF.PI / 180f * 33);
    }

    public override void PostDraw()
    {
        base.PostDraw();
        ImGui.PopStyleVar();
    }
}
