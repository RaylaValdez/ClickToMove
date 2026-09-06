using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;
using CameraManager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager;

namespace ClickToMove.Util;

// Exact port of the PlayerQuests position picker drawing stack
// (Drawing/Brush.cs, Drawing/ConvexShape.cs, and the ring parts of
// Drawing/DrawFunctions.cs). Same brushes, same segments, same camera
// distance scaling, same rotation animation, so the reticle renders
// exactly like the one in PlayerQuests.
internal struct Brush
{
    public float Thickness;
    public Vector4 Color;
    public Vector4 Fill;

    public readonly bool HasFill()
    {
        return Fill.W != 0.0f;
    }
}

internal sealed class GroundShape
{
    private const float Tau = MathF.PI * 2.0f;
    private const int CircleSegments = 180;

    private readonly IGameGui gameGui;
    private readonly ImDrawListPtr drawList;
    private readonly Brush brush;
    private bool culled = true;

    public GroundShape(IGameGui gameGui, ImDrawListPtr drawList, Brush brush)
    {
        this.gameGui = gameGui;
        this.drawList = drawList;
        this.brush = brush;
    }

    public void Arc(Vector3 center, float radius, float startRads, float endRads)
    {
        var segments = ArcSegments(startRads, endRads);
        var deltaRads = (endRads - startRads) / segments;

        this.drawList.PathClear();
        for (var i = 0; i < segments + 1; i++)
            this.PointRadial(center, radius, startRads + (deltaRads * i));
    }

    public void Done()
    {
        if (this.culled)
        {
            this.drawList.PathClear();
            return;
        }

        if (this.brush.HasFill())
            this.drawList.PathFillConvex(ImGui.GetColorU32(this.brush.Fill));
        if (this.brush.Thickness != 0.0f)
            this.drawList.PathStroke(ImGui.GetColorU32(this.brush.Color), ImDrawFlags.None, this.brush.Thickness);
        this.drawList.PathClear();
    }

    private static int ArcSegments(float startRads, float endRads)
    {
        return (int)(MathF.Abs(endRads - startRads) * (CircleSegments / Tau)) + 1;
    }

    private void Point(Vector3 worldPos)
    {
        var visible = this.gameGui.WorldToScreen(worldPos, out var pos);
        this.drawList.PathLineTo(pos);
        if (visible)
            this.culled = false;
    }

    private void PointRadial(Vector3 center, float radius, float radians)
    {
        this.Point(new Vector3(
            center.X + (radius * MathF.Cos(radians)),
            center.Y,
            center.Z + (radius * MathF.Sin(radians))));
    }
}

internal static class GroundDraw
{
    private static float currentRotationAngle;
    private static float previousSmoothedSigmoidValue = 0.5f;

    public static void CircleXZ(IGameGui gameGui, Vector3 gamePos, float radius, Brush brush)
    {
        var scaleFactor = CalculateScaleFactor(GetCameraDistance());

        radius *= scaleFactor;
        brush.Thickness *= scaleFactor;

        var shape = new GroundShape(gameGui, ImGui.GetWindowDrawList(), brush);
        shape.Arc(gamePos, radius, 0.0f, MathF.Tau);
        shape.Done();
    }

    public static void RotatingCircle4SegmentsXZ(IGameGui gameGui, Vector3 gamePos, float radius, Brush brush, float rotationOffset = 0.0f, float gapRads = MathF.PI / 180.0f * 45.0f)
    {
        var scaleFactor = CalculateScaleFactor(GetCameraDistance());

        radius *= scaleFactor;
        gapRads *= scaleFactor;
        brush.Thickness *= scaleFactor;

        var segmentAngle = MathF.Tau / 4.0f;
        var radiansPerDegree = MathF.PI / 180.0f;
        var rotationRads = (currentRotationAngle + rotationOffset) * radiansPerDegree;

        var shape = new GroundShape(gameGui, ImGui.GetWindowDrawList(), brush);

        for (var i = 0; i < 4; i++)
        {
            var startRads = (i * segmentAngle) + rotationRads;
            var endRads = startRads + segmentAngle - gapRads;
            shape.Arc(gamePos, radius, startRads, endRads);
            shape.Done();
        }

        currentRotationAngle -= 1.0f;
        if (currentRotationAngle < 0.0f)
            currentRotationAngle += 360.0f;
        if (currentRotationAngle >= 360.0f)
            currentRotationAngle -= 360.0f;
    }

    private static unsafe float GetCameraDistance()
    {
        try
        {
            var manager = CameraManager.Instance();
            if (manager == null)
                return 0.0f;
            var camera = manager->GetActiveCamera();
            if (camera == null)
                return 0.0f;
            return camera->Distance;
        }
        catch
        {
            return 0.0f;
        }
    }

    private static float CalculateScaleFactor(float cameraDistance, float standardDistance = 1.0f, float smoothingFactor = 0.1f)
    {
        var normalizedDistance = Sigmoid(cameraDistance - standardDistance);
        return SmoothedSigmoid(normalizedDistance, smoothingFactor);
    }

    private static float Sigmoid(float value)
    {
        return 1.0f / (1.0f + MathF.Exp(-value));
    }

    private static float SmoothedSigmoid(float value, float smoothingFactor)
    {
        if (smoothingFactor <= 0.0f || smoothingFactor > 1.0f)
            return value;

        var sigmoidValue = Sigmoid(value);
        var smoothedValue = (smoothingFactor * sigmoidValue) + ((1.0f - smoothingFactor) * previousSmoothedSigmoidValue);
        previousSmoothedSigmoidValue = smoothedValue;
        return smoothedValue;
    }
}
