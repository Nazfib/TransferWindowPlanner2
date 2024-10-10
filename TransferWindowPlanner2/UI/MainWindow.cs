using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.UI.Screens;
using UnityEngine;
using ClickThroughFix;
using MechJebLibBindings;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2.UI
{
using Solver;
using Rendering;
using static Solver.MoreMaths;
using static GuiUtils;

[KSPAddon(KSPAddon.Startup.AllGameScenes, once: true)]
public class MainWindow : MonoBehaviour
{
    #region UI Fields

    private const string ModName = "TransferWindowPlanner2";

    private const string Icon = "TransferWindowPlanner2/icon";
    private const string Marker = "TransferWindowPlanner2/marker";
    private static readonly Vector2 MarkerSize = new Vector2(16, 16);
    private Texture2D? _markerTex = null;

    private Texture2D MarkerTex
    {
        get
        {
            if (_markerTex == null) { _markerTex = GameDatabase.Instance.GetTexture(Marker, false); }
            return _markerTex;
        }
    }

    private const int PlotWidth = 500;
    private const int PlotHeight = 400;
    private const int WindowWidth = 750;
    private const int WindowHeight = 600;
    private const int UnitLabelWidth = 35;
    private const int InputLabelWidth = 120;

    // Initialized in Awake()
    private Texture2D _plotArrival = null!;
    private Texture2D _plotDeparture = null!;
    private Texture2D _plotTotal = null!;

    private bool _plotIsDrawn = false;

    private ApplicationLauncherButton? _button;

    private bool _showMainWindow;
    private const string WindowTitle = "Transfer Window Planner 2";
    internal Rect WinPos = new Rect(450, 100, WindowWidth, WindowHeight);
    private Rect _plotPosition;
    private string _tooltip = "";

    private MapAngleRenderer? _ejectAngleRenderer;
    private ParkingOrbitRendererHack? _parkingOrbitRenderer;
    private bool _showEjectAngle;
    private bool _showParkingOrbit;

    #endregion

    #region Input fields

    private enum PlotType
    {
        Departure = 0, Arrival = 1, Total = 2,
    }

    private PlotType _selectedPlot = PlotType.Total;

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

    // These are initialized into a default invalid state. During Start(), ResetTimes() is called which will then
    // reset them to a valid state. This is done to avoid calling any KSP/Unity APIs during class construction.
    private DateInput _earliestDeparture = new DateInput();
    private DateInput _latestDeparture = new DateInput();
    private DoubleInput _minTimeOfFlight = new DoubleInput(100, min: 0.0);
    private DoubleInput _maxTimeOfFlight = new DoubleInput(400, min: 0.0);

    private DoubleInput _plotMarginDep = new DoubleInput(1e3, min: 0);
    private DoubleInput _plotMarginArr = new DoubleInput(1e3, min: 0);

    private DoubleInput _alarmMargin = new DoubleInput(value: 24, min: 0); // Default 24h; should be 6h for stock?
    private readonly List<string> _errors = new List<string>();

    #endregion

    #region Solver fields

    private (int, int) _selectedTransfer;
    private TransferDetails _transferDetails;

    private bool _hasPrincipia;
    private Solver _solver = null!; // Initialized in Awake()
    private bool _plotIsUpdating;

    #endregion

    #region Unity methods/event handlers

    protected void Awake()
    {
        DontDestroyOnLoad(this);
        GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
        GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGuiAppLauncherDestroyed);
        GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);

        _hasPrincipia =
            AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "principia.ksp_plugin_adapter");
        Debug.Log(_hasPrincipia ? "[TWP2] Detected Principia" : "[TWP2] No Principia detected");
        _solver = new Solver(PlotWidth, PlotHeight, _hasPrincipia);

        _plotArrival = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
        _plotDeparture = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
        _plotTotal = new Texture2D(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    }

    protected void Start()
    {
        KACWrapper.InitKACWrapper();
        InitStyles();
        ResetInputs();
        _bodySelectionWindow = BodySelectionWindow.Setup(this);
    }

    private void ResetInputs()
    {
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

        ResetTimes();

        // From the Unity docs: Do not assume that the texture will be created and available in Awake. All texture
        // uploads are synchronized on the Main thread at Start. Perform texture operations in Start.
        ClearTexture(_plotDeparture);
        ClearTexture(_plotArrival);
        ClearTexture(_plotTotal);

        _transferDetails = new TransferDetails();
        _plotIsDrawn = false;
    }

    public void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
        GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnGuiAppLauncherDestroyed);
        GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);

        Destroy(_bodySelectionWindow);
        if (_ejectAngleRenderer != null) { Destroy(_ejectAngleRenderer); }

        Destroy(_plotArrival);
        Destroy(_plotDeparture);
        Destroy(_plotTotal);
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (!_showMainWindow) { return; }

        WinPos = ClickThruBlocker.GUILayoutWindow(GetHashCode(), WinPos, WindowGUI, WindowTitle);

        if (!string.IsNullOrEmpty(_tooltip))
        {
            var pos = Event.current.mousePosition + new Vector2(25, -5);
            ClickThruBlocker.GUILayoutWindow(
                GetHashCode() + 1, new Rect(pos, Vector2.zero), TooltipWindowGUI, "", GUIStyle.none);
        }
    }

    private void OnSceneChange(GameScenes s)
    {
        HideWindow();
        _showEjectAngle = _showParkingOrbit = false;
        if (_ejectAngleRenderer != null) { Destroy(_ejectAngleRenderer); }
        if (_parkingOrbitRenderer != null)
        {
            _parkingOrbitRenderer.Cleanup();
            _parkingOrbitRenderer = null;
        }
    }

    private void OnGuiAppLauncherReady()
    {
        if (_button != null) { return; }

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

    private void OnGuiAppLauncherDestroyed()
    {
        if (_button != null) { ApplicationLauncher.Instance.RemoveModApplication(_button); }
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

        if (_ejectAngleRenderer == null)
        {
            _ejectAngleRenderer = MapView.MapCamera.gameObject.AddComponent<MapAngleRenderer>();
        }
        if (_showEjectAngle && !_ejectAngleRenderer!.IsDrawing) { EnableEjectionRenderer(); }
        else if (!_showEjectAngle && !_ejectAngleRenderer!.IsHiding) { DisableEjectionRenderer(); }
    }

    #endregion

    #region GUI

    private void ShowWindow()
    {
        // Issue #1: When the origin or target was set to a vessel, and then a scene change occurs, both  the celestial
        // and the vessel are null (Unity lifetime check); this is an invalid state, and causes an
        // InvalidOperationException to be thrown in every OnGUI call (i.e. multiple times per frame).
        // When the main window is opened, and the departure or arrival bodies are null, reset the inputs. This will
        // reset the window to a valid state.
        if (DepartureBody.IsNull || ArrivalBody.IsNull) { ResetInputs(); }

        _showMainWindow = true;
    }

    private void HideWindow()
    {
        _showMainWindow = false;
        _bodySelectionWindow.Which = BodySelectionWindow.SelectionKind.None;
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
        if (!_plotMarginDep.Valid)
        {
            _errors.Add($"Departure Δv margin should be a positive number ({_plotMarginDep.Text})");
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

        if (!_minTimeOfFlight.Valid)
        {
            _errors.Add($"Transfer time should be a positive number ({_minTimeOfFlight.Text})");
        }
        if (!_maxTimeOfFlight.Valid)
        {
            _errors.Add($"Transfer time should be a positive number ({_maxTimeOfFlight.Text})");
        }
        if (_minTimeOfFlight.Value >= _maxTimeOfFlight.Value)
        {
            _errors.Add("Min. transfer time must be less than max. transfer time");
        }
        if (!_plotMarginArr.Valid)
        {
            _errors.Add($"Arrival Δv margin should be a positive number ({_plotMarginArr.Text})");
        }
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
                LabeledDoubleInput("Min. inclination", ref _departureInclination, "°");
            }
            LabeledDateInput("Earliest", ref _earliestDeparture);
            LabeledDateInput("Latest", ref _latestDeparture);
            LabeledDoubleInput("Δv margin", ref _plotMarginDep, "m/s");
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
            LabeledDoubleInput("Min. transfer time", ref _minTimeOfFlight, "days");
            LabeledDoubleInput("Max. transfer time", ref _maxTimeOfFlight, "days");
            LabeledDoubleInput("Δv margin", ref _plotMarginArr, "m/s");
        }

        _bodySelectionWindow.Which = nextSelectionKind;


        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Reset times")) { ResetTimes(); }
        using (new GUILayout.HorizontalScope())
        {
            using (new GuiEnabled(_errors.Count == 0 && !_solver.IsRunning()))
            {
                if (GUILayout.Button(new GUIContent("Plot it!", string.Join("\n", _errors)))) { GeneratePlots(); }
            }
            // using (new GuiEnabled(_plotIsDrawn))
            // {
            //     if (GUILayout.Button(new GUIContent("Save PNG"))) { SaveToPNG(); }
            // }
        }

        GUILayout.FlexibleSpace();
    }

    private void ShowPlot()
    {
        if (_plotIsUpdating && _solver.ResultReady) { OnSolverDone(); }

        using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false), GUILayout.Width(PlotWidth)))
        {
            _selectedPlot = (PlotType)GUILayout.SelectionGrid(
                (int)_selectedPlot, new[] { "Departure", "Arrival", "Total" }, xCount: 3);


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
        GUI.Box(new Rect(markerPosition, MarkerSize), MarkerTex, GUIStyle.none);
    }

    private string PlotHandleMouse()
    {
        if (!_plotIsDrawn) { return ""; }
        var mousePos = Event.current.mousePosition;
        if (!_plotPosition.Contains(mousePos)) { return ""; }

        var pos = mousePos - _plotPosition.position;
        var i = (int)pos.x;
        var j = (int)pos.y;
        var (dep, tof) = _solver.TimesFor(i, j);
        var tooltip = $"Departure: {KSPUtil.PrintDateCompact(dep, includeTime: false)}"
                      + $"\nArrival: {KSPUtil.PrintDateCompact(dep + tof, includeTime: false)}"
                      + $"\nEject: {_solver.DepΔv[i, j].ToSI()}m/s"
                      + $"\nInsert: {_solver.ArrΔv[i, j].ToSI()}m/s"
                      + $"\nTotal: {_solver.TotalΔv[i, j].ToSI()}m/s";

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
                    _transferDetails.TimeOfFlight, _transferDetails.IsShort, includeSeconds: false,
                    interestedPlaces: 2));
            LabeledInfo("Total Δv", $"{_transferDetails.TotalΔv.ToSI()}m/s");
        }

        using (new GuiEnabled(_transferDetails.IsValid))
        {
            if (GUILayout.Button("Create alarm")) { CreateAlarm(); }
            if (KACWrapper.APIReady && GUILayout.Button("Create KAC alarm")) { CreateKACAlarm(); }
            LabeledDoubleInput("Alarm margin", ref _alarmMargin, "h");

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

        _minTimeOfFlight.Value = Math.Ceiling(transferMin / KSPUtil.dateTimeFormatter.Day);
        _maxTimeOfFlight.Value = Math.Ceiling(travelMax / KSPUtil.dateTimeFormatter.Day);
        // We set the input value/text directly; therefore, we need to call OnInputChanged manually as well.
        OnInputChanged();
    }

    // ReSharper disable once UnusedMember.Local
    private void SaveToPNG()
    {
        var tex = _selectedPlot switch
        {
            PlotType.Departure => _plotDeparture,
            PlotType.Arrival => _plotArrival,
            PlotType.Total => _plotTotal,
            _ => _plotTotal,
        };
        var bytes = tex.EncodeToPNG();
        var fileName = Path.Combine(KSPUtil.ApplicationRootPath, "Screenshots/TWP_Window.png");
        File.WriteAllBytes(KSPUtil.GenerateFilePathWithDate(fileName), bytes);
    }

    #endregion

    #region Generating the plots

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
            _minTimeOfFlight.Value * KSPUtil.dateTimeFormatter.Day,
            _maxTimeOfFlight.Value * KSPUtil.dateTimeFormatter.Day,
            _departureAltitude.Value * 1e3, Deg2Rad(_departureInclination.Value),
            _arrivalAltitude.Value * 1e3, _circularize);
    }

    private void OnSolverDone()
    {
        DrawTexture(_plotDeparture, _solver.DepΔv, _solver.MinDepΔv, _solver.MinDepΔv + (float)_plotMarginDep.Value);
        DrawTexture(_plotArrival, _solver.ArrΔv, _solver.MinArrΔv, _solver.MinArrΔv + (float)_plotMarginArr.Value);
        DrawTexture(
            _plotTotal, _solver.TotalΔv, _solver.MinTotalΔv,
            _solver.MinTotalΔv + (float)_plotMarginDep.Value + (float)_plotMarginArr.Value);
        UpdateTransferDetails(_solver.MinTotalPoint);
        _plotIsDrawn = true;

        // Reset it for the next time
        _plotIsUpdating = false;
    }


    // ReSharper disable once InconsistentNaming
    private static void DrawTexture(Texture2D tex, float[,] Δv, float minΔv, float maxΔv)
    {
        for (var i = 0; i < PlotWidth; ++i)
        {
            for (var j = 0; j < PlotHeight; ++j)
            {
                var color = ColorMap.MapColorReverse(Δv[i, j], minΔv, maxΔv);
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

    private void UpdateTransferDetails((int, int) point)
    {
        var (tDep, tof) = _solver.TimesFor(point);
        _selectedTransfer = point;
        _transferDetails = _solver.CalculateDetails(tDep, tDep + tof);

        if (_showEjectAngle) { EnableEjectionRenderer(); }
        if (_showParkingOrbit) { EnableParkingOrbitRenderer(); }
    }

    #endregion

    #region Map view renderers

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

        var vInf = _transferDetails.DepartureVInf.ToVector3d();
        var peDir = _transferDetails.DeparturePeDirection.ToVector3d();

        _ejectAngleRenderer.Draw(_transferDetails.Origin.Celestial!, vInf, peDir);
    }

    private void DisableEjectionRenderer()
    {
        if (_ejectAngleRenderer == null) { return; }
        _ejectAngleRenderer.Hide();
    }

    #endregion

    #region Alarms

    private void CreateAlarm()
    {
        var alarm = new TWPAlarm(_transferDetails, _alarmMargin.Value * KSPUtil.dateTimeFormatter.Hour);
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
            _transferDetails.DepartureTime - _alarmMargin.Value * KSPUtil.dateTimeFormatter.Hour);

        var alarm = KACWrapper.KAC.Alarms.First(a => a.ID == tmpID);
        alarm.Notes = _transferDetails.Description();
        alarm.AlarmMargin = _alarmMargin.Value * KSPUtil.dateTimeFormatter.Hour;
        alarm.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
        alarm.XferOriginBodyName = _transferDetails.Origin.Name;
        alarm.XferTargetBodyName = _transferDetails.Destination.Name;
    }

    #endregion

    # region UI Utils

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

        GUILayout.Label(label, ResultLabelStyle, GUILayout.Width(InputLabelWidth));
        var newText = GUILayout.TextField(
            input.Text,
            input.Valid ? InputStyle : InvalidInputStyle,
            GUILayout.ExpandWidth(true));
        if (newText != input.Text)
        {
            input.Text = newText;
            OnInputChanged();
        }

        GUILayout.Label(unit, ResultLabelStyle, GUILayout.Width(UnitLabelWidth));
    }

    private void LabeledDateInput(string label, ref DateInput input)
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label, ResultLabelStyle, GUILayout.Width(InputLabelWidth));

        var newText = GUILayout.TextField(
            input.Text,
            input.Valid ? InputStyle : InvalidInputStyle,
            GUILayout.ExpandWidth(true));
        if (newText != input.Text)
        {
            input.Text = newText;
            OnInputChanged();
        }
    }

    #endregion
}
}
