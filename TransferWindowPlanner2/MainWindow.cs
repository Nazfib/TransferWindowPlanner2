using System;
using System.Linq;
using KSP.UI.Screens;
using static MechJebLib.Utils.Statics;
using UnityEngine;

namespace TransferWindowPlanner2
{
using static MoreMaths;
using static GuiUtils;

[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
public class MainWindow : MonoBehaviour
{
    private const string ModName = "TransferWindowPlanner2";
    private const string Icon = "TransferWindowPlanner2/icon";
    private const string Marker = "TransferWindowPlanner2/marker";
    private static readonly Vector2 MarkerSize = new Vector2(16, 16);
    private const int PlotWidth = 500;
    private const int PlotHeight = 400;
    private const int WindowWidth = 750;
    private const int WindowHeight = 600;

    private readonly Texture2D _plotArrival = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private readonly Texture2D _plotDeparture = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private readonly Texture2D _plotTotal = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);

    private ApplicationLauncherButton? _button;

    private enum PlotType
    {
        Departure = 0, Arrival = 1, Total = 2,
    }

    private PlotType _selectedPlot = PlotType.Total;

    private bool _showMainWindow;
    private Rect _winPos = new Rect(450, 100, WindowWidth, WindowHeight);
    private Rect _plotPosition;

    // Input fields
    private BodySelectionWindow _departureBodyWindow = null!;
    private BodySelectionWindow _arrivalBodyWindow = null!;
    private BodySelectionWindow _centralBodyWindow = null!;
    private Endpoint DepartureBody => _departureBodyWindow.SelectedBody;
    private Endpoint ArrivalBody => _arrivalBodyWindow.SelectedBody;

    private CelestialBody CentralBody =>
        _centralBodyWindow.SelectedBody.Celestial == null
            ? throw new InvalidOperationException()
            : _centralBodyWindow.SelectedBody.Celestial;

    private DoubleInput _departureAltitude = new DoubleInput(100.0);
    private DoubleInput _departureInclination = new DoubleInput(0.0);
    private DoubleInput _arrivalAltitude = new DoubleInput(100.0);
    private bool _circularize = true;

    private DateInput _earliestDeparture = new DateInput(0.0);
    private DateInput _latestDeparture = new DateInput(0.0);
    private DateInput _earliestArrival = new DateInput(0.0);
    private DateInput _latestArrival = new DateInput(0.0);

    private DoubleInput _plotMargin = new DoubleInput(2.0);

    private (int, int) _selectedTransfer;
    private Solver.TransferDetails _transferDetails;

    private Solver _solver = null!; // Initialized in Awake()

    private MapAngleRenderer? _ejectAngleRenderer;
    private ParkingOrbitRenderer? _parkingOrbitRenderer;
    private bool _showEjectAngle;
    private bool _showParkingOrbit;

    protected void Awake()
    {
        GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
        if (CurrentSceneHasMapView())
        {
            _ejectAngleRenderer = MapView.MapCamera.gameObject.AddComponent<MapAngleRenderer>();
        }
        _solver = new Solver(PlotWidth, PlotHeight);
    }

    protected void Start()
    {
        KACWrapper.InitKACWrapper();

        var departureCb = FlightGlobals.GetHomeBody();
        var centralCb = FlightGlobals.GetHomeBody().referenceBody;
        var arrivalCb = FlightGlobals.Bodies.Find(
            cb => cb != centralCb && cb.referenceBody == centralCb && cb != departureCb);
        if (arrivalCb == null)
        {
            // No valid destinations from the home body: let the user worry about it.
            arrivalCb = departureCb;
        }

        _departureBodyWindow = BodySelectionWindow.Setup(this, "Origin body", new Endpoint(departureCb));
        _arrivalBodyWindow = BodySelectionWindow.Setup(this, "Destination body", new Endpoint(arrivalCb));
        _centralBodyWindow = BodySelectionWindow.Setup(this, "Central body", new Endpoint(centralCb));
        ResetTimes();
    }

    public void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
        Destroy(_departureBodyWindow);
        Destroy(_arrivalBodyWindow);
        Destroy(_centralBodyWindow);
        if (_button != null) { ApplicationLauncher.Instance.RemoveModApplication(_button); }
        if (_ejectAngleRenderer != null) { Destroy(_ejectAngleRenderer); }
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (_showMainWindow) { _winPos = GUILayout.Window(GetHashCode(), _winPos, WindowGUI, ModName); }
    }


    private void ShowWindow()
    {
        _showMainWindow = true;
    }

    private void HideWindow()
    {
        _showMainWindow = false;
        _departureBodyWindow.IsVisible = false;
        _arrivalBodyWindow.IsVisible = false;
        _centralBodyWindow.IsVisible = false;
    }

    private void OnSceneChange(GameScenes s)
    {
        _showMainWindow = false;
    }

    private void OnGuiAppLauncherReady()
    {
        try
        {
            _button = ApplicationLauncher.Instance.AddModApplication(
                ShowWindow,
                HideWindow,
                null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS & ~ApplicationLauncher.AppScenes.MAINMENU,
                GameDatabase.Instance.GetTexture($"{Icon}", false));
            GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
        }
        catch (Exception ex)
        {
            Debug.LogError($"{ModName} failed to register button");
            Debug.LogException(ex);
        }
    }

    private void WindowGUI(int id)
    {
        using var scope = new GUILayout.VerticalScope();

        using (new GUILayout.HorizontalScope())
        {
            ShowInputs();
            ShowPlot();
        }

        using (new GUILayout.HorizontalScope())
        {
            ShowDepartureInfo();
            ShowArrivalInfo();
            ShowTransferInfo();
        }

        GUI.DragWindow();
    }

    private bool ValidOriginOrDestination(Endpoint end) =>
        end.Orbit.referenceBody == CentralBody && end.Celestial != CentralBody;

    private bool ValidInputs() =>
        ValidOriginOrDestination(DepartureBody) &&
        ValidOriginOrDestination(ArrivalBody) &&
        !DepartureBody.Equals(ArrivalBody) &&
        _departureAltitude.Valid &&
        (!DepartureBody.IsCelestial ||
         _departureAltitude.Value + DepartureBody.Celestial!.Radius < DepartureBody.Celestial!.sphereOfInfluence) &&
        _departureInclination.Valid &&
        _earliestDeparture.Valid &&
        _latestDeparture.Valid &&
        _arrivalAltitude.Valid &&
        (!ArrivalBody.IsCelestial ||
         _arrivalAltitude.Value + ArrivalBody.Celestial!.Radius < ArrivalBody.Celestial!.sphereOfInfluence) &&
        _earliestArrival.Valid &&
        _latestArrival.Valid &&
        _plotMargin.Valid;

    private void ShowInputs()
    {
        using var scope = new GUILayout.VerticalScope(
            GUILayout.ExpandWidth(false), GUILayout.Width(WindowWidth - PlotWidth));

        using (new GUILayout.HorizontalScope(BoxStyle))
        {
            GUILayout.Label("Central body", BoxTitleStyle);
            GUILayout.FlexibleSpace();
            _centralBodyWindow.IsVisible = GUILayout.Toggle(
                _centralBodyWindow.IsVisible,
                CentralBody.displayName.LocalizeRemoveGender(),
                ButtonStyle,
                GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2f));
        }

        using (new GUILayout.VerticalScope(BoxStyle))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Origin", BoxTitleStyle);
                GUILayout.FlexibleSpace();
                _departureBodyWindow.CentralBody = CentralBody;
                _departureBodyWindow.IsVisible = GUILayout.Toggle(
                    _departureBodyWindow.IsVisible,
                    DepartureBody.Name,
                    ValidOriginOrDestination(DepartureBody) && !DepartureBody.Equals(ArrivalBody)
                        ? ButtonStyle
                        : InvalidButtonStyle,
                    GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2f));
            }
            using (new GuiEnabled(DepartureBody.IsCelestial))
            {
                LabeledDoubleInput("Altitude", ref _departureAltitude, "km");
                LabeledDoubleInput("Min. Inclination", ref _departureInclination, "°");
            }
            LabeledDateInput("Earliest", ref _earliestDeparture);
            LabeledDateInput("Latest", ref _latestDeparture);
        }

        using (new GUILayout.VerticalScope(BoxStyle))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Destination", BoxTitleStyle);
                GUILayout.FlexibleSpace();
                _arrivalBodyWindow.CentralBody = CentralBody;
                _arrivalBodyWindow.IsVisible = GUILayout.Toggle(
                    _arrivalBodyWindow.IsVisible,
                    ArrivalBody.Name,
                    ValidOriginOrDestination(ArrivalBody) && !DepartureBody.Equals(ArrivalBody)
                        ? ButtonStyle
                        : InvalidButtonStyle,
                    GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2));
            }
            using (new GuiEnabled(ArrivalBody.IsCelestial))
            {
                LabeledDoubleInput("Altitude", ref _arrivalAltitude, "km");
                _circularize = GUILayout.Toggle(_circularize, "Circularize");
            }
            LabeledDateInput("Earliest", ref _earliestArrival);
            LabeledDateInput("Latest", ref _latestArrival);
        }

        LabeledDoubleInput("Plot margin", ref _plotMargin);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Reset times")) { ResetTimes(); }
        using (new GuiEnabled(ValidInputs() && _solver.WorkerState is Solver.BackgroundWorkerState.Idle))
        {
            if (GUILayout.Button("Plot it!")) { GeneratePlots(); }
        }

        GUILayout.FlexibleSpace();
    }

    private void ShowPlot()
    {
        if (_solver.WorkerState is Solver.BackgroundWorkerState.Done) { OnSolverDone(); }

        using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false), GUILayout.Width(PlotWidth)))
        {
            _selectedPlot = (PlotType)GUILayout.SelectionGrid(
                (int)_selectedPlot, new[] { "Departure", "Arrival", "Total" }, 3);

            GUILayout.Box(
                _selectedPlot switch
                {
                    PlotType.Departure => _plotDeparture,
                    PlotType.Arrival => _plotArrival,
                    PlotType.Total => _plotTotal,
                    _ => _plotTotal,
                },
                PlotBoxStyle,
                GUILayout.Width(PlotWidth), GUILayout.Height(PlotHeight),
                GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            _plotPosition = GUILayoutUtility.GetLastRect();

            var mousePos = Event.current.mousePosition;
            if (_plotPosition.Contains(mousePos))
            {
                var pos = mousePos - _plotPosition.position;
                var i = (int)pos.x;
                var j = (int)pos.y;
                var (dep, arr) = _solver.TimesFor(i, j);
                var tooltip = $"Departure: {KSPUtil.PrintDateCompact(dep, false)}\n"
                              + $"Arrival: {KSPUtil.PrintDateCompact(arr, false)}\n"
                              + $"Eject: {_solver.DepΔv[i, j].ToSI()}m/s\n"
                              + $"Insert: {_solver.ArrΔv[i, j].ToSI()}m/s\n"
                              + $"Total: {_solver.TotalΔv[i, j].ToSI()}m/s";
                var size = PlotTooltipStyle.CalcSize(new GUIContent(tooltip));
                GUI.Label(
                    new Rect(mousePos.x + 25, mousePos.y - 5, size.x, size.y),
                    tooltip, PlotTooltipStyle);
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    UpdateTransferDetails((i, j));
                }
            }
            var selected = new Vector2(_selectedTransfer.Item1, _selectedTransfer.Item2);
            var tex = GameDatabase.Instance.GetTexture(Marker, false);
            var markerPosition = _plotPosition.position + selected - 0.5f * MarkerSize;
            GUI.Box(
                new Rect(markerPosition, MarkerSize), tex,
                GUIStyle.none);
        }
    }

    private void ShowDepartureInfo()
    {
        using var scope = new GUILayout.VerticalScope();

        using (new GuiEnabled(_transferDetails.IsValid))
        {
            if (GUILayout.Button("Minimize departure Δv")) { UpdateTransferDetails(_solver.MinDepPoint); }
        }

        using (new GUILayout.VerticalScope(
                   BoxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Departure", BoxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_transferDetails.DepartureTime, _transferDetails.IsShort));
            LabeledInfo("Periapsis altitude", $"{_transferDetails.DeparturePeriapsis.ToSI()}m");
            LabeledInfo("Inclination", $"{Rad2Deg(_transferDetails.DepartureInclination):F2} °");
            LabeledInfo("LAN", $"{Rad2Deg(_transferDetails.DepartureLAN):F2} °");
            LabeledInfo(
                "Asymptote direction",
                $"{Rad2Deg(_transferDetails.DepartureAsyRA):F2}° RA\n{Rad2Deg(_transferDetails.DepartureAsyDecl):F2}° Dec");
            LabeledInfo("C3", $"{_transferDetails.DepartureC3 / 1e6:F2} km²/s²");
            LabeledInfo("Δv", $"{_transferDetails.DepartureΔv.ToSI()}m/s");
        }
    }

    private void ShowArrivalInfo()
    {
        using var scope = new GUILayout.VerticalScope();

        using (new GuiEnabled(_transferDetails.IsValid))
        {
            if (GUILayout.Button("Minimize arrival Δv")) { UpdateTransferDetails(_solver.MinArrPoint); }
        }

        using (new GUILayout.VerticalScope(
                   BoxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Arrival", BoxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_transferDetails.ArrivalTime, _transferDetails.IsShort));
            LabeledInfo("Periapsis altitude", $"{_transferDetails.ArrivalPeriapsis.ToSI()}m");
            LabeledInfo("Distance between bodies", $"{_transferDetails.ArrivalDistance.ToSI()}m");
            GUILayout.Label(""); // Empty row
            LabeledInfo(
                "Asymptote direction",
                $"{Rad2Deg(_transferDetails.DepartureAsyRA):F2}° RA\n{Rad2Deg(_transferDetails.DepartureAsyDecl):F2}° Dec");
            LabeledInfo("C3", $"{_transferDetails.ArrivalC3 / 1e6:F2} km²/s²");
            LabeledInfo("Δv", $"{_transferDetails.ArrivalΔv.ToSI()}m/s");
        }
    }

    private void ShowTransferInfo()
    {
        using var scope = new GUILayout.VerticalScope();
        using (new GuiEnabled(_transferDetails.IsValid))
        {
            if (GUILayout.Button("Minimize total Δv")) { UpdateTransferDetails(_solver.MinTotalPoint); }
        }

        using (new GUILayout.VerticalScope(
                   BoxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Transfer", BoxTitleStyle);
            LabeledInfo(
                "Flight time",
                KSPUtil.PrintDateDeltaCompact(
                    _transferDetails.TimeOfFlight, _transferDetails.IsShort, false, 2));
            LabeledInfo("Total Δv", $"{_transferDetails.TotalΔv.ToSI()}m/s");
        }

        using (new GuiEnabled(_transferDetails.IsValid))
        {
            if (GUILayout.Button("Create alarm")) { CreateAlarm(); }
            if (KACWrapper.APIReady && GUILayout.Button("Create KAC alarm")) { CreateKACAlarm(); }

            if (CurrentSceneHasMapView())
            {
                using (new GuiEnabled(_transferDetails.IsValid && _transferDetails.Origin.IsCelestial))
                {
                    _showParkingOrbit = GUILayout.Toggle(
                        _showParkingOrbit, "Show parking orbit in map view", ButtonStyle);
                    _showEjectAngle = GUILayout.Toggle(
                        _showEjectAngle, "Show ejection angles in map view", ButtonStyle);
                }
            }
        }
    }

    private void Update()
    {
        if (!CurrentSceneHasMapView()) { return; }
        if (!_transferDetails.IsValid || !_transferDetails.Origin.IsCelestial)
        {
            _showParkingOrbit = _showEjectAngle = false;
        }

        if (_showParkingOrbit && _parkingOrbitRenderer == null) { EnableParkingOrbitRenderer(); }
        else if (!_showParkingOrbit && _parkingOrbitRenderer != null) { DisableParkingOrbitRenderer(); }

        if (_showEjectAngle && !_ejectAngleRenderer!.IsDrawing) { EnableEjectionRenderer(); }
        else if (!_showEjectAngle && !_ejectAngleRenderer!.IsHiding) { DisableEjectionRenderer(); }
    }

    private void GeneratePlots()
    {
        if (!(_solver.WorkerState is Solver.BackgroundWorkerState.Idle))
        {
            Debug.LogError($"Solver is already working!");
            return;
        }

        _solver.GeneratePorkchop(
            DepartureBody, ArrivalBody,
            _earliestDeparture.Ut, _latestDeparture.Ut,
            _earliestArrival.Ut, _latestArrival.Ut,
            _departureAltitude.Value * 1e3, Deg2Rad(_departureInclination.Value),
            _arrivalAltitude.Value * 1e3, _circularize);
    }

    private void OnSolverDone()
    {
        DrawTexture(_plotDeparture, _solver.DepΔv, _solver.MinDepΔv, _solver.MinDepΔv * _plotMargin.Value);
        DrawTexture(_plotArrival, _solver.ArrΔv, _solver.MinArrΔv, _solver.MinArrΔv * _plotMargin.Value);
        DrawTexture(_plotTotal, _solver.TotalΔv, _solver.MinTotalΔv, _solver.MinTotalΔv * _plotMargin.Value);
        UpdateTransferDetails(_solver.MinTotalPoint);

        // Reset it for the next time
        _solver.WorkerState = Solver.BackgroundWorkerState.Idle;
    }

    private void UpdateTransferDetails((int, int) point)
    {
        var (tDep, tArr) = _solver.TimesFor(point);
        _selectedTransfer = point;
        _transferDetails = _solver.CalculateDetails(tDep, tArr);

        if (_showEjectAngle) { EnableEjectionRenderer(); }
        if (_showParkingOrbit) { EnableParkingOrbitRenderer(); }
    }

    private void EnableParkingOrbitRenderer()
    {
        if (_parkingOrbitRenderer != null) { _parkingOrbitRenderer.Cleanup(); }
        if (!_transferDetails.IsValid) { return; }
        if (!_transferDetails.Origin.IsCelestial) { return; }

        _parkingOrbitRenderer = ParkingOrbitRenderer.Setup(
            _transferDetails.Origin.Celestial!,
            _transferDetails.DeparturePeriapsis,
            Rad2Deg(_transferDetails.DepartureInclination),
            Rad2Deg(_transferDetails.DepartureLAN));
    }

    private void DisableParkingOrbitRenderer()
    {
        if (_parkingOrbitRenderer == null) { return; }
        _parkingOrbitRenderer.Cleanup();
        _parkingOrbitRenderer = null;
    }

    private void EnableEjectionRenderer()
    {
        if (!_transferDetails.IsValid) { return; }
        if (!_transferDetails.Origin.IsCelestial) { return; }
        if (_ejectAngleRenderer == null) { return; }
        var vInf = new Vector3d(
            _transferDetails.DepartureVInf.x,
            _transferDetails.DepartureVInf.y,
            _transferDetails.DepartureVInf.z);
        var peDir = new Vector3d(
            _transferDetails.DeparturePeDirection.x,
            _transferDetails.DeparturePeDirection.y,
            _transferDetails.DeparturePeDirection.z);

        _ejectAngleRenderer.Draw(_transferDetails.Origin.Celestial!, vInf, peDir);
    }

    private void DisableEjectionRenderer()
    {
        if (_ejectAngleRenderer == null) { return; }
        _ejectAngleRenderer.Hide();
    }

    private void CreateAlarm()
    {
        var alarm = new TWPAlarm(_transferDetails);
        AlarmClockScenario.AddAlarm(alarm);
    }

    private void CreateKACAlarm()
    {
        var tmpID = KACWrapper.KAC.CreateAlarm(
            KACWrapper.KACAPI.AlarmTypeEnum.TransferModelled,
            string.Format(
                "{0} -> {1} ({2})",
                _transferDetails.Origin.Name,
                _transferDetails.Destination.Name,
                KSPUtil.PrintDateDelta(_transferDetails.TimeOfFlight, _transferDetails.IsShort)),
            _transferDetails.DepartureTime - 24 * 60 * 60);

        var alarm = KACWrapper.KAC.Alarms.First(a => a.ID == tmpID);
        alarm.Notes = _transferDetails.Description();
        alarm.AlarmMargin = 24 * 60 * 60;
        alarm.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
        alarm.XferOriginBodyName = _transferDetails.Origin.Name;
        alarm.XferTargetBodyName = _transferDetails.Destination.Name;
    }

    private static void DrawTexture(Texture2D tex, double[,] c3, double minC3, double maxC3)
    {
        for (var i = 0; i < PlotWidth; ++i)
        {
            for (var j = 0; j < PlotHeight; ++j)
            {
                var color = ColorMap.MapColorReverse((float)c3[i, j], (float)minC3, (float)maxC3);
                tex.SetPixel(i, PlotHeight - j - 1, color);
            }
        }
        tex.Apply();
    }

    private void ResetTimes()
    {
        var departureRange = Math.Min(
            2 * SynodicPeriod(DepartureBody.Orbit.period, ArrivalBody.Orbit.period),
            2 * DepartureBody.Orbit.period);

        _earliestDeparture.Ut = Planetarium.GetUniversalTime();
        _latestDeparture.Ut = _earliestDeparture.Ut + departureRange;

        var hohmannTime = HohmannTime(
            DepartureBody.Orbit.referenceBody.gravParameter,
            DepartureBody.Orbit.semiMajorAxis,
            ArrivalBody.Orbit.semiMajorAxis);
        var transferMin = Math.Max(hohmannTime - ArrivalBody.Orbit.period, hohmannTime / 2);
        var travelMax = transferMin + Math.Min(2 * ArrivalBody.Orbit.period, hohmannTime);

        _earliestArrival.Ut = _earliestDeparture.Ut + transferMin;
        _latestArrival.Ut = _latestDeparture.Ut + travelMax;
    }


    private static void LabeledInfo(string label, string value)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(label, ResultLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, ResultValueStyle);
        }
    }

    private void LabeledDoubleInput(string label, ref DoubleInput input, string? unit = null)
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label, ResultLabelStyle);
        GUILayout.FlexibleSpace();
        input.Text = GUILayout.TextField(
            input.Text,
            input.Valid ? InputStyle : InvalidInputStyle,
            GUILayout.Width(100));
        if (!string.IsNullOrEmpty(unit)) { GUILayout.Label(unit, ResultLabelStyle, GUILayout.ExpandWidth(false)); }
    }

    private void LabeledDateInput(string label, ref DateInput input)
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label, ResultLabelStyle);
        GUILayout.FlexibleSpace();

        input.Text = GUILayout.TextField(
            input.Text,
            input.Valid ? InputStyle : InvalidInputStyle,
            GUILayout.Width(100));
    }
}
}
