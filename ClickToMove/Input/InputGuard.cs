using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace ClickToMove.Input;

// Fail fast guards shared by both click sources.
internal static class InputGuard
{
    // Port of Utils.IsClickingInGameWorld from ffxiv-bundleoftweaks.
    // Must run on the draw thread because it touches ImGui state.
    public static unsafe bool IsClickingInGameWorld()
    {
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow))
            return false;
        if (ImGui.GetIO().WantCaptureMouse)
            return false;

        var stage = AtkStage.Instance();
        if (stage == null || stage->RaptureAtkUnitManager == null)
            return false;
        if (stage->RaptureAtkUnitManager->AtkUnitManager.FocusedUnitsList.Count != 0)
            return false;

        var cursor = FFXIVClientStructs.FFXIV.Client.System.Input.Cursor.Instance();
        if (cursor == null)
            return false;
        return cursor->ActiveCursorType == 0;
    }

    public static unsafe bool IsWindowInactive()
    {
        try
        {
            var framework = GameFramework.Instance();
            return framework == null || framework->WindowInactive;
        }
        catch
        {
            return true;
        }
    }

    // True when the player state should block starting a new move.
    public static bool IsPlayerBusyForStart()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null || player.IsDead)
            return true;
        if (!Service.ClientState.IsLoggedIn)
            return true;

        var condition = Service.Condition;
        return condition[ConditionFlag.Unconscious]
            || condition[ConditionFlag.Occupied]
            || condition[ConditionFlag.OccupiedInEvent]
            || condition[ConditionFlag.OccupiedInQuestEvent]
            || condition[ConditionFlag.OccupiedInCutSceneEvent]
            || condition[ConditionFlag.WatchingCutscene]
            || condition[ConditionFlag.WatchingCutscene78]
            || condition[ConditionFlag.BetweenAreas]
            || condition[ConditionFlag.BetweenAreas51]
            || condition[ConditionFlag.Crafting]
            || condition[ConditionFlag.Gathering]
            || condition[ConditionFlag.Fishing]
            || condition[ConditionFlag.Mounting]
            || condition[ConditionFlag.MountOrOrnamentTransition]
            || condition[ConditionFlag.LoggingOut]
            || condition[ConditionFlag.MeldingMateria]
            || Service.ClientState.IsGPosing;
    }
}
