using System;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using UnityEngine;
using ClickThroughFix;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2.UI
{
using Solver;
using Rendering;
using static Solver.MoreMaths;
using static GuiUtils;

[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
public class MainWindow : MonoBehaviour
{
    private const string ModName = "TransferWindowPlanner2";
    private const string Icon = "TransferWindowPlanner2/icon";
    private const string Marker = "TransferWindowPlanner2/marker";
    private static readonly Vector2 MarkerSize = new Vector2(16, 16);
    private Texture2D _markerTex = null!;

    private const int PlotWidth = 500;
    private const int PlotHeight = 400;
    private const int WindowWidth = 750;
    private const int WindowHeight = 600;

    private readonly Texture2D _plotArrival = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private readonly Texture2D _plotDeparture = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private readonly Texture2D _plotTotal = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private bool _plotIsDrawn = false;

    private ApplicationLauncherButton? _button;

    private enum PlotType
    {
        Departure = 0, Arrival = 1, Total = 2,
    }

    private PlotType _selectedPlot = PlotType.Total;

    private bool _showMainWindow;
    internal Rect WinPos = new Rect(450, 100, WindowWidth, WindowHeight);
    private Rect _plotPosition;
    private string _tooltip = "";

    // Input fields
    private BodySelectionWindow _bodySelectionWindow = null!;
    private CelestialBody _centralBody = null!;
    private Endpoint _departureBody;
    private Endpoint _arrivalBody;

    internal CelestialBody CentralBody
    {
        get => _centralBody;
        set
        {
            _centralBody = value;
            OnInputChanged();
        }
    }

    internal Endpoint DepartureBody
    {
        get => _departureBody;
        set
        {
            _departureBody = value;
            if (_departureBody.IsCelestial)
            {
                _departureAltitude.Max =
                    1e-3 * (_departureBody.Celestial!.sphereOfInfluence - _departureBody.Celestial!.Radius);
            }
            else { _departureAltitude.Max = double.PositiveInfinity; }
            OnInputChanged();
        }
    }

    internal Endpoint ArrivalBody
    {
        get => _arrivalBody;
        set
        {
            _arrivalBody = value;
            if (_arrivalBody.IsCelestial)
            {
                _arrivalAltitude.Max =
                    1e-3 * (_arrivalBody.Celestial!.sphereOfInfluence - _arrivalBody.Celestial!.Radius);
            }
            else { _arrivalAltitude.Max = double.PositiveInfinity; }
            OnInputChanged();
        }
    }

    private DoubleInput _departureAltitude = new DoubleInput(100.0, 0.0);
    private DoubleInput _departureInclination = new DoubleInput(0.0, 0.0, 90.0);
    private DoubleInput _arrivalAltitude = new DoubleInput(100.0, 0.0);
    private bool _circularize = true;

    private DateInput _earliestDeparture = new DateInput(0.0);
    private DateInput _latestDeparture = new DateInput(0.0);
    private DateInput _earliestArrival = new DateInput(0.0);
    private DateInput _latestArrival = new DateInput(0.0);

    private DoubleInput _plotMargin = new DoubleInput(2.0, 1.0);

    private (int, int) _selectedTransfer;
    private TransferDetails _transferDetails;

    private readonly List<string> _errors = new List<string>();

    private bool _hasPrincipia;
    private Solver _solver = null!; // Initialized in Awake()
    private bool _plotIsUpdating;

    private MapAngleRenderer? _ejectAngleRenderer;
    private ParkingOrbitRendererHack? _parkingOrbitRenderer;
    private bool _showEjectAngle;
    private bool _showParkingOrbit;

    protected void Awake()
    {
        GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
        GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
        if (RenderUtils.CurrentSceneHasMapView())
        {
            _ejectAngleRenderer = MapView.MapCamera.gameObject.AddComponent<MapAngleRenderer>();
        }
        _hasPrincipia =
            AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "principia.ksp_plugin_adapter");
        Debug.Log("[TWP2] Detected Principia");
        _solver = new Solver(PlotWidth, PlotHeight, _hasPrincipia);

        ClearTexture(_plotDeparture);
        ClearTexture(_plotArrival);
        ClearTexture(_plotTotal);

        _markerTex = GameDatabase.Instance.GetTexture(Marker, false);
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

        // Set the field directly, bypassing the property; OnInputChanged() gets called later (inside ResetTimes()),
        // so we skip having to validate the inputs multiple times. However, we do need to set the max altitudes.
        _centralBody = centralCb;
        _departureBody = new Endpoint(departureCb);
        _departureAltitude.Max = 1e-3 * (departureCb.sphereOfInfluence - departureCb.Radius);
        _arrivalBody = new Endpoint(arrivalCb);
        _arrivalAltitude.Max = 1e-3 * (arrivalCb.sphereOfInfluence - arrivalCb.Radius);

        _bodySelectionWindow = BodySelectionWindow.Setup(this);
        ResetTimes();
    }

    public void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
        GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
        Destroy(_bodySelectionWindow);
        if (_button != null) { ApplicationLauncher.Instance.RemoveModApplication(_button); }
        if (_ejectAngleRenderer != null) { Destroy(_ejectAngleRenderer); }
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (!_showMainWindow) { return; }

        WinPos = ClickThruBlocker.GUILayoutWindow(GetHashCode(), WinPos, WindowGUI, ModName);

        if (!string.IsNullOrEmpty(_tooltip))
        {
            var pos = Event.current.mousePosition + new Vector2(25, -5);
            ClickThruBlocker.GUILayoutWindow(
                GetHashCode() + 1, new Rect(pos, Vector2.zero), TooltipWindowGUI, "", GUIStyle.none);
        }
    }


    private void ShowWindow()
    {
        _showMainWindow = true;
    }

    private void HideWindow()
    {
        _showMainWindow = false;
        _bodySelectionWindow.Which = BodySelectionWindow.SelectionKind.None;
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

        if (Event.current.type is EventType.Repaint) { _tooltip = GUI.tooltip; }
    }

    private void TooltipWindowGUI(int id)
    {
        GUILayout.Label(_tooltip, TooltipStyle);
        GUI.BringWindowToFront(id);
    }

    private bool ValidOriginOrDestination(Endpoint end) =>
        end.Orbit.referenceBody == CentralBody && end.Celestial != CentralBody;

    private void OnInputChanged()
    {
        ValidateInputs();
    }

    private void ValidateInputs()
    {
        _errors.Clear();
        if (!ValidOriginOrDestination(DepartureBody))
        {
            _errors.Add($"{DepartureBody.Name} does not orbit {CentralBody.displayName.LocalizeRemoveGender()}");
        }
        if (!ValidOriginOrDestination(ArrivalBody))
        {
            _errors.Add($"{ArrivalBody.Name} does not orbit {CentralBody.displayName.LocalizeRemoveGender()}");
        }
        if (DepartureBody.Equals(ArrivalBody))
        {
            _errors.Add($"Can't plot a transfer from {DepartureBody.Name} to itself");
        }
        if (DepartureBody.IsCelestial)
        {
            if (!_departureAltitude.Parsed || _departureAltitude.Value < 0)
            {
                _errors.Add($"Departure altitude should be a number greater than zero ({_departureAltitude.Text})");
            }
            else if (_departureAltitude.Value > _departureAltitude.Max)
            {
                _errors.Add(
                    $"{_departureAltitude.Value} km is outside the sphere of influence of {DepartureBody.Name}");
            }

            if (!_departureInclination.Valid)
            {
                _errors.Add(
                    $"Departure inclination should be a number between 0 and 90 ({_departureInclination.Text})");
            }
        }

        if (!_earliestDeparture.Valid) { _errors.Add($"Could not parse departure date ({_earliestDeparture.Text})"); }
        if (!_latestDeparture.Valid) { _errors.Add($"Could not parse departure date ({_latestDeparture.Text})"); }
        if (_earliestDeparture.Ut >= _latestDeparture.Ut)
        {
            _errors.Add("Earliest departure must be before latest departure");
        }

        if (ArrivalBody.IsCelestial)
        {
            if (!_arrivalAltitude.Parsed || _arrivalAltitude.Value < 0)
            {
                _errors.Add($"Arrival altitude should be a positive number ({_arrivalAltitude.Text})");
            }
            else if (_arrivalAltitude.Value > _arrivalAltitude.Max)
            {
                _errors.Add($"{_arrivalAltitude.Value} km is outside the sphere of influence of {ArrivalBody.Name}");
            }
        }

        if (!_earliestArrival.Valid) { _errors.Add($"Could not parse arrival date ({_earliestArrival.Text})"); }
        if (!_latestArrival.Valid) { _errors.Add($"Could not parse arrival date ({_latestArrival.Text})"); }
        if (_earliestArrival.Ut >= _latestArrival.Ut) { _errors.Add("Earliest arrival must be before latest arrival"); }

        if (_earliestDeparture.Ut >= _latestArrival.Ut)
        {
            _errors.Add("Earliest departure must be before latest arrival");
        }

        if (!_plotMargin.Valid) { _errors.Add($"Plot margin should be a number greater than 1 ({_plotMargin.Text})"); }
    }

    private void ShowInputs()
    {
        using var scope = new GUILayout.VerticalScope(
            GUILayout.ExpandWidth(false), GUILayout.Width(WindowWidth - PlotWidth));

        var nextSelectionKind = BodySelectionWindow.SelectionKind.None;

        using (new GUILayout.HorizontalScope(BoxStyle))
        {
            GUILayout.Label("Central body", BoxTitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(
                    _bodySelectionWindow.Which is BodySelectionWindow.SelectionKind.Central,
                    // && nextSelectionKind is BodySelectionWindow.SelectionKind.None, // Always true
                    CentralBody.displayName.LocalizeRemoveGender(),
                    ButtonStyle,
                    GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2f)))
            {
                nextSelectionKind = BodySelectionWindow.SelectionKind.Central;
            }
        }

        using (new GUILayout.VerticalScope(BoxStyle))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Origin", BoxTitleStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Toggle(
                        _bodySelectionWindow.Which is BodySelectionWindow.SelectionKind.Departure
                        && nextSelectionKind is BodySelectionWindow.SelectionKind.None,
                        DepartureBody.Name,
                        ValidOriginOrDestination(DepartureBody) && !DepartureBody.Equals(ArrivalBody)
                            ? ButtonStyle
                            : InvalidButtonStyle,
                        GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2f)))
                {
                    nextSelectionKind = BodySelectionWindow.SelectionKind.Departure;
                }
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
                if (GUILayout.Toggle(
                        _bodySelectionWindow.Which is BodySelectionWindow.SelectionKind.Arrival
                        && nextSelectionKind is BodySelectionWindow.SelectionKind.None,
                        ArrivalBody.Name,
                        ValidOriginOrDestination(ArrivalBody) && !DepartureBody.Equals(ArrivalBody)
                            ? ButtonStyle
                            : InvalidButtonStyle,
                        GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2)))
                {
                    nextSelectionKind = BodySelectionWindow.SelectionKind.Arrival;
                }
            }
            using (new GuiEnabled(ArrivalBody.IsCelestial))
            {
                LabeledDoubleInput("Altitude", ref _arrivalAltitude, "km");
                _circularize = GUILayout.Toggle(_circularize, "Circularize");
            }
            LabeledDateInput("Earliest", ref _earliestArrival);
            LabeledDateInput("Latest", ref _latestArrival);
        }

        _bodySelectionWindow.Which = nextSelectionKind;

        LabeledDoubleInput("Plot margin", ref _plotMargin);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Reset times")) { ResetTimes(); }
        using (new GuiEnabled(_errors.Count == 0 && !_solver.IsRunning()))
        {
            if (GUILayout.Button(new GUIContent("Plot it!", string.Join("\n", _errors)))) { GeneratePlots(); }
        }

        GUILayout.FlexibleSpace();
    }

    private void ShowPlot()
    {
        if (_plotIsUpdating && _solver.ResultReady) { OnSolverDone(); }

        using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false), GUILayout.Width(PlotWidth)))
        {
            _selectedPlot = (PlotType)GUILayout.SelectionGrid(
                (int)_selectedPlot, new[] { "Departure", "Arrival", "Total" }, 3);


            // Ideally, we'd like to handle this _after_ drawing the plot (and therefore updating _plotPosition);
            // however, we need the tooltip string earlier. The position might be a frame outdated; don't worry about
            // it.
            var tooltip = PlotHandleMouse();
            GUILayout.Box(
                new GUIContent(
                    _selectedPlot switch
                    {
                        PlotType.Departure => _plotDeparture,
                        PlotType.Arrival => _plotArrival,
                        PlotType.Total => _plotTotal,
                        _ => _plotTotal,
                    }, tooltip),
                PlotBoxStyle,
                GUILayout.Width(PlotWidth), GUILayout.Height(PlotHeight),
                GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            if (Event.current.type is EventType.Repaint) { _plotPosition = GUILayoutUtility.GetLastRect(); }
        }

        if (_plotIsDrawn) { DrawPlotMarker(); }
    }

    private void DrawPlotMarker()
    {
        var selected = new Vector2(_selectedTransfer.Item1, _selectedTransfer.Item2);
        var markerPosition = _plotPosition.position + selected - 0.5f * MarkerSize;
        GUI.Box(new Rect(markerPosition, MarkerSize), _markerTex, GUIStyle.none);
    }

    private string PlotHandleMouse()
    {
        if (!_plotIsDrawn) { return ""; }
        var mousePos = Event.current.mousePosition;
        if (!_plotPosition.Contains(mousePos)) { return ""; }

        var pos = mousePos - _plotPosition.position;
        var i = (int)pos.x;
        var j = (int)pos.y;
        var (dep, arr) = _solver.TimesFor(i, j);
        var tooltip = $"Departure: {KSPUtil.PrintDateCompact(dep, false)}"
                      + $"\nArrival: {KSPUtil.PrintDateCompact(arr, false)}";
        if (dep < arr)
        {
            tooltip += $"\nEject: {_solver.DepΔv[i, j].ToSI()}m/s"
                       + $"\nInsert: {_solver.ArrΔv[i, j].ToSI()}m/s"
                       + $"\nTotal: {_solver.TotalΔv[i, j].ToSI()}m/s";
        }

        if (Event.current.type == EventType.MouseUp && Event.current.button == 0) { UpdateTransferDetails((i, j)); }
        return tooltip;
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
                $"{Rad2Deg(_transferDetails.ArrivalAsyRA):F2}° RA\n{Rad2Deg(_transferDetails.ArrivalAsyDecl):F2}° Dec");
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

            if (RenderUtils.CurrentSceneHasMapView())
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
        if (!RenderUtils.CurrentSceneHasMapView()) { return; }
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
        if (_solver.IsRunning())
        {
            Debug.LogError($"Solver is already working!");
            return;
        }
        _plotIsUpdating = true;

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
        _plotIsDrawn = true;

        // Reset it for the next time
        _plotIsUpdating = false;
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

        _parkingOrbitRenderer = ParkingOrbitRendererHack.Setup(
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

    private static void ClearTexture(Texture2D tex)
    {
        for (var i = 0; i < tex.width; ++i)
        {
            for (var j = 0; j < tex.height; ++j) { tex.SetPixel(i, j, Color.clear); }
        }
        tex.Apply();
    }

    private void ResetTimes()
    {
        var departureRange = Clamp(
            2 * SynodicPeriod(DepartureBody.Orbit.period, ArrivalBody.Orbit.period),
            Math.Max(DepartureBody.Orbit.period, KSPUtil.dateTimeFormatter.Day),
            2 * DepartureBody.Orbit.period);

        _earliestDeparture.Ut = Planetarium.GetUniversalTime();
        _latestDeparture.Ut = _earliestDeparture.Ut + departureRange;

        var hohmannTime = HohmannTime(
            DepartureBody.Orbit.referenceBody.gravParameter,
            DepartureBody.Orbit.semiMajorAxis,
            ArrivalBody.Orbit.semiMajorAxis);
        var transferRange = Math.Min(1.5 * hohmannTime, 2 * ArrivalBody.Orbit.period);
        var transferMin = Math.Max(0.5 * hohmannTime, hohmannTime - ArrivalBody.Orbit.period);
        var travelMax = transferMin + transferRange;

        _earliestArrival.Ut = _earliestDeparture.Ut + transferMin;
        _latestArrival.Ut = _latestDeparture.Ut + travelMax;
        // We set the input value/text directly; therefore, we need to call OnInputChanged manually as well.
        OnInputChanged();
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

    private void LabeledDoubleInput(string label, ref DoubleInput input, string unit = "")
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label, ResultLabelStyle);
        GUILayout.FlexibleSpace();
        var newText = GUILayout.TextField(
            input.Text,
            input.Valid ? InputStyle : InvalidInputStyle,
            GUILayout.Width(100));
        if (newText != input.Text)
        {
            input.Text = newText;
            OnInputChanged();
        }
        GUILayout.Label(unit, ResultLabelStyle, GUILayout.Width(25));
    }

    private void LabeledDateInput(string label, ref DateInput input)
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label, ResultLabelStyle);
        GUILayout.FlexibleSpace();

        var newText = GUILayout.TextField(
            input.Text,
            input.Valid ? InputStyle : InvalidInputStyle,
            GUILayout.Width(100));
        if (newText != input.Text)
        {
            input.Text = newText;
            OnInputChanged();
        }
        // Not using a GUILayout.Space(25) here, so I don't have to figure out what padding to use to make it the same
        // size as a LabeledDoubleInput
        GUILayout.Label("", ResultLabelStyle, GUILayout.Width(25));
    }
}
}
