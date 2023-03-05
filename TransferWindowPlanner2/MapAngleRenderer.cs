using System;
using UnityEngine;

namespace TransferWindowPlanner2;

public class MapAngleRenderer : MonoBehaviour
{
    public bool IsDrawing => _currentDrawingState != DrawingState.Hidden;

    private DateTime _startDrawing;

    public CelestialBody? BodyOrigin { get; set; }

    public Vector3d AsymptoteDirection { get; set; }

    public Vector3d PeriapsisDirection { get; set; }

    // Nullability: initialized in Start(), de-initialized in OnDestroy()
    private GameObject _objLineStart = null!;
    private GameObject _objLineEnd = null!;
    private GameObject _objLineArc = null!;

    // Nullability: initialized in Start(), de-initialized in OnDestroy()
    private LineRenderer _lineStart = null!;
    private LineRenderer _lineEnd = null!;
    private LineRenderer _lineArc = null!;

    private const int ArcPoints = 72;
    private const float AppearTime = 0.5f;
    private const float HideTime = 0.25f;

    // Nullability: initialized in Start(), de-initialized in OnDestroy()
    private GUIStyle _styleLabelEnd = null!;
    private GUIStyle _styleLabelTarget = null!;

    private enum DrawingState
    {
        Hidden,
        DrawingLinesAppearing,
        DrawingArcAppearing,
        DrawingFullPicture,
        Hiding,
    };

    private DrawingState _currentDrawingState = DrawingState.Hidden;

    private void Start()
    {
        if (!GuiUtils.CurrentSceneHasMapView())
        {
            enabled = false;
            return;
        }

        Debug.Log("Initializing EjectAngle Render");
        _objLineStart = new GameObject("LineStart");
        _objLineEnd = new GameObject("LineEnd");
        _objLineArc = new GameObject("LineArc");

        //Get the orbit lines material so things look similar
        var orbitLines = ((MapView)FindObjectOfType(typeof(MapView))).orbitLinesMaterial;

        //init all the lines
        _lineStart = InitLine(_objLineStart, Color.blue, 2, 10, orbitLines);
        _lineEnd = InitLine(_objLineEnd, Color.red, 2, 10, orbitLines);
        _lineArc = InitLine(_objLineArc, Color.green, ArcPoints, 10, orbitLines);

        _styleLabelEnd = new GUIStyle
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };
        _styleLabelTarget = new GUIStyle
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };
    }

    private static LineRenderer InitLine(
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


    private void OnDestroy()
    {
        _currentDrawingState = DrawingState.Hidden;

        //Bin the objects
        _lineStart = null!;
        _lineEnd = null!;
        _lineArc = null!;

        _objLineStart.DestroyGameObject();
        _objLineEnd.DestroyGameObject();
        _objLineArc.DestroyGameObject();
    }

    public void DrawAngle(CelestialBody bodyOrigin, Vector3d asymptote, Vector3d periapsis)
    {
        BodyOrigin = bodyOrigin;
        AsymptoteDirection = asymptote.normalized;
        PeriapsisDirection = periapsis.normalized;

        _startDrawing = DateTime.Now;
        _currentDrawingState = DrawingState.DrawingLinesAppearing;
    }

    public void HideAngle()
    {
        _startDrawing = DateTime.Now;
        _currentDrawingState = DrawingState.Hiding;
    }

    private static Vector3d VectorToUnityFrame(Vector3d v)
    {
        v.Swizzle(); // Swap y and z axes
        v = Planetarium.Rotation * v;
        return v;
    }

    internal void OnPreCull()
    {
        if (!GuiUtils.CurrentSceneHasMapView()) { return; }

        if (BodyOrigin == null) { return; }

        if (!MapView.MapIsEnabled || !IsDrawing)
        {
            _lineStart.enabled = false;
            _lineEnd.enabled = false;
            _lineArc.enabled = false;
            return;
        }

        var lineLength = BodyOrigin.Radius * 5;
        var arcRadius = BodyOrigin.Radius * 3;
        var asymptote = VectorToUnityFrame(AsymptoteDirection.normalized);
        var periapsis = VectorToUnityFrame(PeriapsisDirection.normalized);

        //Are we Showing, Hiding or Static State
        float pctDone;

        switch (_currentDrawingState)
        {
            case DrawingState.Hidden:
                break;

            case DrawingState.DrawingLinesAppearing:
                pctDone = (float)(DateTime.Now - _startDrawing).TotalSeconds / AppearTime;
                if (pctDone >= 1)
                {
                    _currentDrawingState = DrawingState.DrawingArcAppearing;
                    _startDrawing = DateTime.Now;
                }
                pctDone = Mathf.Clamp01(pctDone);

                var partialAsymptote = asymptote * Mathf.Lerp(0, (float)lineLength, pctDone);
                DrawLine(_lineStart, Vector3d.zero, partialAsymptote);
                break;

            case DrawingState.DrawingArcAppearing:
                pctDone = (float)(DateTime.Now - _startDrawing).TotalSeconds / AppearTime;
                if (pctDone >= 1) { _currentDrawingState = DrawingState.DrawingFullPicture; }
                pctDone = Mathf.Clamp01(pctDone);

                Vector3d partialPeriapsis = Vector3.Slerp(asymptote, periapsis, pctDone);

                DrawLine(_lineStart, Vector3d.zero, asymptote * lineLength);
                DrawLine(_lineEnd, Vector3d.zero, partialPeriapsis * lineLength);
                DrawArc(_lineArc, asymptote, partialPeriapsis, arcRadius);
                break;

            case DrawingState.DrawingFullPicture:
                DrawLine(_lineStart, Vector3d.zero, asymptote * lineLength);
                DrawLine(_lineEnd, Vector3d.zero, periapsis * lineLength);
                DrawArc(_lineArc, asymptote, periapsis, arcRadius);
                break;

            case DrawingState.Hiding:
                pctDone = (float)(DateTime.Now - _startDrawing).TotalSeconds / HideTime;
                if (pctDone >= 1) { _currentDrawingState = DrawingState.Hidden; }
                pctDone = Mathf.Clamp01(pctDone);

                var partialLineLength = Mathf.Lerp((float)lineLength, 0, pctDone);
                var partialArcRadius = Mathf.Lerp((float)arcRadius, 0, pctDone);

                DrawLine(_lineStart, Vector3d.zero, asymptote * partialLineLength);
                DrawLine(_lineEnd, Vector3d.zero, periapsis * partialLineLength);
                DrawArc(_lineArc, asymptote, periapsis, partialArcRadius);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal void OnGUI()
    {
        if (BodyOrigin == null) { return; }
        if (!MapView.MapIsEnabled || !IsDrawing) { return; }

        var center = BodyOrigin.transform.position;
        var length = 5 * BodyOrigin.Radius;
        var asymptote = PlanetariumCamera.Camera.WorldToScreenPoint(
            ScaledSpace.LocalToScaledSpace(
                center + length * VectorToUnityFrame(AsymptoteDirection.normalized)));
        var periapsis = PlanetariumCamera.Camera.WorldToScreenPoint(
            ScaledSpace.LocalToScaledSpace(
                center + length * VectorToUnityFrame(PeriapsisDirection.normalized)));

        GUI.Label(
            new Rect(
                periapsis.x - 50,
                Screen.height - periapsis.y - 15,
                100, 30),
            $"Burn position", _styleLabelEnd);

        GUI.Label(
            new Rect(
                asymptote.x - 50,
                Screen.height - asymptote.y - 15,
                100, 30),
            "Escape direction", _styleLabelTarget);
    }

    private void DrawArc(LineRenderer line, Vector3d start, Vector3d end, double radius)
    {
        if (BodyOrigin == null) { return; }

        Vector3d center = BodyOrigin.transform.position;
        for (var i = 0; i < ArcPoints; i++)
        {
            var t = (float)i / (ArcPoints - 1);
            // I'd like to use Vector3d.Slerp here, but it throws a MissingMethodException.
            Vector3d arcSegment = Vector3.Slerp(start, end, t);
            line.SetPosition(i, ScaledSpace.LocalToScaledSpace(center + arcSegment * radius));
        }

        line.startWidth = line.endWidth = 10f / 1000f * PlanetariumCamera.fetch.Distance;
        line.enabled = true;
    }

    private void DrawLine(LineRenderer line, Vector3d pointStart, Vector3d pointEnd)
    {
        if (BodyOrigin == null) { return; }

        Vector3d center = BodyOrigin.transform.position;
        line.SetPosition(0, ScaledSpace.LocalToScaledSpace(center + pointStart));
        line.SetPosition(1, ScaledSpace.LocalToScaledSpace(center + pointEnd));
        line.startWidth = line.endWidth = 10f / 1000f * PlanetariumCamera.fetch.Distance;
        line.enabled = true;
    }
}