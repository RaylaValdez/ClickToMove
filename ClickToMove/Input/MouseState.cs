using Dalamud.Bindings.ImGui;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ClickToMove.Input;

// Hardware input state polled with user32, same pattern as PlayerQuests.
// Emulates nothing, so it only sees real key presses. That matters for
// StopOnWASD: keys the DirectEngine holds via IKeyState must not count
// as the user pressing them.
internal static class MouseState
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    private const int VK_LBUTTON = 1;
    private const int VK_ESCAPE = 27;
    private const int VK_SHIFT = 16;
    private const int VK_CONTROL = 17;
    private const int VK_MENU = 18;
    private const int VK_W = 87;
    private const int VK_A = 65;
    private const int VK_S = 83;
    private const int VK_D = 68;

    private static bool prevLeft;
    private static bool curLeft;
    private static bool prevEscape;
    private static bool curEscape;

    // Drag guard, same idea as PlayerQuests click detection: a press that
    // moves more than a few pixels is a camera drag, not a click.
    private const float MaxClickDragPixels = 8.0f;
    private static Vector2 pressAnchor;
    private static bool pressArmed;

    // Call once per frame on the draw thread before reading edges.
    public static void Update()
    {
        prevLeft = curLeft;
        curLeft = GetAsyncKeyState(VK_LBUTTON) != 0;
        prevEscape = curEscape;
        curEscape = GetAsyncKeyState(VK_ESCAPE) != 0;

        if (curLeft && !prevLeft)
        {
            // Only arm when the press starts off UI. Pressing on a panel,
            // dragging onto the world, and releasing must never move.
            pressArmed = !ImGui.GetIO().WantCaptureMouse;
            if (pressArmed)
                pressAnchor = ImGui.GetMousePos();
        }
        else if (!curLeft && !prevLeft)
        {
            pressArmed = false;
        }
    }

    public static bool LeftDown => curLeft;
    public static bool LeftPressed => curLeft && !prevLeft;
    public static bool LeftReleased => !curLeft && prevLeft;
    public static bool EscapeReleased => !curEscape && prevEscape;

    // True when this release must not count as a click: the press started
    // on UI, or the cursor dragged too far since the press.
    public static bool ReleaseWasDrag()
    {
        if (!pressArmed)
            return true;
        return Vector2.Distance(pressAnchor, ImGui.GetMousePos()) > MaxClickDragPixels;
    }

    public static bool ShiftDown => KeyDown(VK_SHIFT);
    public static bool CtrlDown => KeyDown(VK_CONTROL);
    public static bool AltDown => KeyDown(VK_MENU);

    public static bool WasdDown =>
        KeyDown(VK_W) ||
        KeyDown(VK_A) ||
        KeyDown(VK_S) ||
        KeyDown(VK_D);

    // High bit only: is the key physically down right now. A bare
    // GetAsyncKeyState != 0 also matches the sticky "pressed since last
    // call" low bit, which goes stale whenever nothing polls the key.
    // WasdDown is only polled while Direct runs, so the stale bit caused
    // a ghost UserInput cancel on the first tick after keyboard walking.
    private static bool KeyDown(int virtualKeyCode)
    {
        return (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
    }

    public static bool ModifierHeld(ModifierKey modifier, bool requireModifier)
    {
        if (!requireModifier && modifier == ModifierKey.None)
            return true;

        return modifier switch
        {
            ModifierKey.None => true,
            ModifierKey.Shift => ShiftDown,
            ModifierKey.Ctrl => CtrlDown,
            ModifierKey.Alt => AltDown,
            _ => false,
        };
    }
}
