using UnityEngine;

namespace TransferWindowPlanner2.UI.Rendering
{
public static class RenderUtils
{
    public static LineRenderer InitLine(
        GameObject objToAttach, Color lineColor, int vertexCount, int initialWidth, Material linesMaterial)
    {
        objToAttach.layer = 9;
        var lineReturn = objToAttach.AddComponent<LineRenderer>();

        lineReturn.material = linesMaterial;
        lineReturn.startColor = lineColor;
        lineReturn.endColor = lineColor;
        lineReturn.transform.parent = null;
        lineReturn.useWorldSpace = true;
        lineReturn.startWidth = initialWidth;
        lineReturn.endWidth = initialWidth;
        lineReturn.positionCount = vertexCount;
        lineReturn.enabled = false;

        return lineReturn;
    }

    public static Vector3d VectorToUnityFrame(Vector3d v) => Planetarium.fetch.rotation * v.xzy;

    public static void DrawArc(
        LineRenderer line, Vector3d center, Vector3d start, Vector3d end, double scale, int arcPoints)
    {
        for (var i = 0; i < arcPoints; i++)
        {
            var t = (float)i / (arcPoints - 1);
            // I'd like to use Vector3d.Slerp here, but it throws a MissingMethodException.
            Vector3d arcSegment = Vector3.Slerp(start, end, t);
            line.SetPosition(i, ScaledSpace.LocalToScaledSpace(center + arcSegment * scale));
        }

        line.startWidth = line.endWidth = 10f / 1000f * PlanetariumCamera.fetch.Distance;
        line.enabled = true;
    }

    public static void DrawLine(LineRenderer line, Vector3d center, Vector3d start, Vector3d end)
    {
        var startPos = ScaledSpace.LocalToScaledSpace(center + start);
        var endPos = ScaledSpace.LocalToScaledSpace(center + end);
        var camPos = PlanetariumCamera.Camera.transform.position;
        line.SetPosition(0, startPos);
        line.SetPosition(1, endPos);
        line.startWidth = 5f / 1000f * Vector3.Distance(camPos, startPos);
        line.endWidth = 5f / 1000f * Vector3.Distance(camPos, startPos);
        line.enabled = true;
    }

    public static bool CurrentSceneHasMapView() =>
        HighLogic.LoadedScene is GameScenes.FLIGHT
        || HighLogic.LoadedScene is GameScenes.TRACKSTATION;
}
}
