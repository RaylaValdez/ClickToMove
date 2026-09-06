using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ClickToMove.Movement;

// Minimal port of the movement override from Jaksuhn clib
// (Utils/OverrideMovement.cs), which itself follows vnavmesh.
// It hooks the game's own movement input readers and injects a
// direction toward DesiredPosition, so walking works with default
// and custom keybinds, in standard movement mode, without touching
// key state (IKeyState can only block keys, never press them).
//
// Differences from clib, all deliberate:
// - No Camera struct dependency. Standard mode input is expressed
//   relative to player rotation exactly like clib. Legacy mode gets
//   a facing assist from DirectEngine, then the same relative math.
// - Signature and hook setup failures are caught and reported through
//   Available/UnavailableReason instead of throwing, so a game patch
//   that moves the signatures degrades Direct mode with a clear
//   message instead of breaking plugin load.
internal sealed class OverrideMovement : IDisposable
{
    private const string WalkSignature = "E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D";
    private const string FlySignature = "E8 ?? ?? ?? ?? 0F B6 0D ?? ?? ?? ?? B8";
    private const string InputEnabledSignature1 = "E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C";
    private const string InputEnabledSignature2 = "E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F";

    private delegate bool InputEnabledDelegate(nint self);

    private delegate void WalkDelegate(nint self, nint sumLeft, nint sumForward, nint sumTurnLeft, nint haveBackwardOrStrafe, nint a6, byte additiveUnk);

    private delegate void FlyDelegate(nint self, nint result);

    private readonly Hook<WalkDelegate> walkHook;
    private readonly Hook<FlyDelegate> flyHook;
    private readonly InputEnabledDelegate isInputEnabled1;
    private readonly InputEnabledDelegate isInputEnabled2;
    private bool disposed;

    public OverrideMovement(IGameInteropProvider interop, ISigScanner scanner)
    {
        Hook<WalkDelegate>? walk = null;
        Hook<FlyDelegate>? fly = null;
        try
        {
            var enabled1 = ResolveCall(scanner, InputEnabledSignature1);
            var enabled2 = ResolveCall(scanner, InputEnabledSignature2);
            walk = interop.HookFromSignature<WalkDelegate>(WalkSignature, this.WalkDetour);
            fly = interop.HookFromSignature<FlyDelegate>(FlySignature, this.FlyDetour);
            this.isInputEnabled1 = enabled1;
            this.isInputEnabled2 = enabled2;
            this.walkHook = walk;
            this.flyHook = fly;
            Service.Log.Information("ClickToMove: movement hooks created (walk and fly).");
        }
        catch (Exception ex)
        {
            try
            {
                walk?.Dispose();
            }
            catch
            {
                // Best effort cleanup on the way out.
            }

            try
            {
                fly?.Dispose();
            }
            catch
            {
                // Best effort cleanup on the way out.
            }

            throw new InvalidOperationException("Movement hooks not found for this game version: " + ex.Message, ex);
        }

        this.DesiredPosition = Vector3.Zero;
        this.Precision = 0.05f;
    }

    public Vector3 DesiredPosition { get; set; }

    public float Precision { get; set; }

    public bool Enabled
    {
        get => this.walkHook.IsEnabled;
        set
        {
            if (value)
            {
                this.walkHook.Enable();
                this.flyHook.Enable();
            }
            else
            {
                this.walkHook.Disable();
                this.flyHook.Disable();
            }
        }
    }

    public void Dispose()
    {
        if (this.disposed)
            return;
        this.disposed = true;
        this.walkHook.Dispose();
        this.flyHook.Dispose();
    }

    // Dalamud ScanText already resolves E8/E9 call and jump targets
    // and validates they land in .text, so the result is used directly.
    private static InputEnabledDelegate ResolveCall(ISigScanner scanner, string signature)
    {
        var target = scanner.ScanText(signature);
        if (target == nint.Zero)
            throw new InvalidOperationException("Signature not found: " + signature);

        Service.Log.Information("ClickToMove: sig '" + signature + "' resolved to 0x" + target.ToString("X"));
        return Marshal.GetDelegateForFunctionPointer<InputEnabledDelegate>(target);
    }

    private unsafe void WalkDetour(nint self, nint sumLeftPtr, nint sumForwardPtr, nint sumTurnLeftPtr, nint haveBackwardOrStrafePtr, nint a6Ptr, byte additiveUnk)
    {
        this.walkHook.Original(self, sumLeftPtr, sumForwardPtr, sumTurnLeftPtr, haveBackwardOrStrafePtr, a6Ptr, additiveUnk);
        try
        {
            var player = Service.ObjectTable.LocalPlayer;
            if (player == null)
                return;

            var toDestination = this.DesiredPosition - player.Position;
            toDestination.Y = 0.0f;
            if (toDestination.LengthSquared() <= this.Precision * this.Precision)
                return;

            if (additiveUnk != 0)
                return;
            if (!this.isInputEnabled1(self) || !this.isInputEnabled2(self))
                return;

            var sumLeft = (float*)sumLeftPtr;
            var sumForward = (float*)sumForwardPtr;
            if (*sumLeft != 0.0f || *sumForward != 0.0f)
                return;

            var relative = MathF.Atan2(toDestination.X, toDestination.Z) - player.Rotation;
            *sumLeft = MathF.Sin(relative);
            *sumForward = MathF.Cos(relative);
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: walk detour failed: " + ex.Message);
        }
    }

    private unsafe void FlyDetour(nint self, nint resultPtr)
    {
        this.flyHook.Original(self, resultPtr);
        try
        {
            var player = Service.ObjectTable.LocalPlayer;
            if (player == null)
                return;

            var toDestination = this.DesiredPosition - player.Position;
            if (toDestination.LengthSquared() <= this.Precision * this.Precision)
                return;

            var result = (FlyInput*)resultPtr;
            if (result->Forward != 0.0f || result->Left != 0.0f || result->Up != 0.0f)
                return;

            var horizontal = MathF.Sqrt((toDestination.X * toDestination.X) + (toDestination.Z * toDestination.Z));
            var relative = MathF.Atan2(toDestination.X, toDestination.Z) - player.Rotation;
            result->Forward = MathF.Cos(relative);
            result->Left = MathF.Sin(relative);
            result->Up = MathF.Atan2(toDestination.Y, horizontal);
        }
        catch (Exception ex)
        {
            Service.Log.Warning("ClickToMove: fly detour failed: " + ex.Message);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    private struct FlyInput
    {
        [FieldOffset(0x0)] public float Forward;
        [FieldOffset(0x4)] public float Left;
        [FieldOffset(0x8)] public float Up;
    }
}
