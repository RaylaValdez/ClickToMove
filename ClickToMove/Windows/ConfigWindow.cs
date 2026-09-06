using ClickToMove.Ipc;
using ClickToMove.Input;
using ClickToMove.Movement;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace ClickToMove.Windows;

internal sealed class ConfigWindow : Window
{
    private static readonly string[] ModifierNames = ["None", "Shift", "Ctrl", "Alt"];
    private static readonly string[] MovementNames = ["Direct", "Pathfind"];

    private readonly Configuration config;
    private readonly VnavmeshIpc ipc;
    private readonly MovementController controller;

    public ConfigWindow(Configuration config, VnavmeshIpc ipc, MovementController controller)
        : base("ClickToMove Settings###ClickToMoveConfig")
    {
        this.config = config;
        this.ipc = ipc;
        this.controller = controller;
        this.Size = new Vector2(460, 620);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        this.Checkbox("Plugin enabled", "Master switch. Everything stops when off.", this.config.PluginEnabled, v => this.config.PluginEnabled = v);

        ImGui.Separator();
        ImGui.TextUnformatted("World Click");
        this.Checkbox("World click enabled", "Hold the modifier and left-click 3D ground to move there.", this.config.WorldClickEnabled, v => this.config.WorldClickEnabled = v);
        this.MovementCombo("World movement", "Pathfind uses vnavmesh when ready, else falls back to Direct if fallback is on.", this.config.WorldClickMovement, v => this.config.WorldClickMovement = v);

        ImGui.Separator();
        ImGui.TextUnformatted("Modifier");
        var modifierIndex = (int)this.config.ClickModifier;
        if (ImGui.Combo("Modifier key", ref modifierIndex, ModifierNames, ModifierNames.Length))
        {
            this.config.ClickModifier = (ModifierKey)modifierIndex;
            this.config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Shift is the default so plain clicks keep selecting targets. None means every left-click release moves you.");

        this.Checkbox("Require modifier", "When off and modifier is None, clicks always move. Keep on unless you know why.", this.config.RequireModifier, v => this.config.RequireModifier = v);

        ImGui.Separator();
        ImGui.TextUnformatted("Movement and Safety");
        this.Checkbox("Fallback to Direct", "When Pathfind is picked but vnavmesh is missing or not ready, walk straight instead of doing nothing.", this.config.FallbackToDirect, v => this.config.FallbackToDirect = v);
        this.Checkbox("Stop on WASD", "Pressing W, A, S or D cancels a Direct move.", this.config.StopOnWASD, v => this.config.StopOnWASD = v);
        this.Checkbox("Stop on jump", "Jumping cancels movement.", this.config.StopOnJump, v => this.config.StopOnJump = v);
        this.Checkbox("Stop on cast", "Starting a cast cancels movement.", this.config.StopOnCast, v => this.config.StopOnCast = v);
        this.Checkbox("Cancel in combat", "Also cancel when combat starts. Off by default so you can keep walking out of AoEs.", this.config.CancelInCombat, v => this.config.CancelInCombat = v);
        this.Checkbox("Stop when window inactive", "Ignore clicks while the game window is not focused.", this.config.StopWhenWindowInactive, v => this.config.StopWhenWindowInactive = v);
        this.Checkbox("Face target (Direct)", "Turn the character toward the destination during Direct moves. Needed for legacy movement mode.", this.config.DirectFaceTarget, v => this.config.DirectFaceTarget = v);
        this.Checkbox("Allow fly", "Let the Pathfind engine fly when the zone and your state allow it.", this.config.AllowFly, v => this.config.AllowFly = v);

        var arrival = this.config.ArrivalTolerance;
        if (ImGui.SliderFloat("Arrival tolerance", ref arrival, 0.1f, 3.0f, "%.2f yalms"))
        {
            this.config.ArrivalTolerance = arrival;
            this.config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Clicks closer than this are ignored as accidents.");

        var stopDist = this.config.DirectStopDistance;
        if (ImGui.SliderFloat("Direct stop distance", ref stopDist, 0.05f, 1.0f, "%.2f yalms"))
        {
            this.config.DirectStopDistance = stopDist;
            this.config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("How close a Direct move must get before it stops.");

        ImGui.Separator();
        ImGui.TextUnformatted("Feedback");
        this.Checkbox("Show placement preview", "Vanilla style ground reticle. Follows the cursor while armed, pins to the destination while moving.", this.config.ShowPlacementPreview, v => this.config.ShowPlacementPreview = v);

        this.Checkbox("Chat on fallback", "Say in chat when a click falls back to Direct.", this.config.ChatFeedbackOnFallback, v => this.config.ChatFeedbackOnFallback = v);
        this.Checkbox("Chat on error", "Say in chat when a move fails, gets stuck, or times out.", this.config.ChatFeedbackOnError, v => this.config.ChatFeedbackOnError = v);

        ImGui.Separator();
        ImGui.TextUnformatted("Status");
        ImGui.TextUnformatted("vnavmesh: " + (this.ipc.HasVnavmesh ? "detected" : "not detected"));
        ImGui.TextUnformatted("Navmesh ready: " + (this.ipc.IsReady() ? "yes" : "no"));
        var progress = this.ipc.BuildProgress();
        ImGui.TextUnformatted("Build progress: " + (progress < 0.0f ? "idle" : ((int)(progress * 100.0f)) + "%"));
        foreach (var line in this.controller.StatusLines())
            ImGui.TextUnformatted(line);

        ImGui.Spacing();
        if (ImGui.Button("Test Direct (5 yalms)"))
            this.controller.TestDirectForward();
        ImGui.SameLine();
        if (ImGui.Button("Stop"))
            this.controller.StopAll();
    }

    private void Checkbox(string label, string tooltip, bool value, Action<bool> set)
    {
        var edited = value;
        if (ImGui.Checkbox(label, ref edited))
        {
            set(edited);
            this.config.Save();
            return;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void MovementCombo(string label, string tooltip, MovementType value, Action<MovementType> set)
    {
        var index = (int)value;
        if (ImGui.Combo(label, ref index, MovementNames, MovementNames.Length))
        {
            set((MovementType)index);
            this.config.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }
}
