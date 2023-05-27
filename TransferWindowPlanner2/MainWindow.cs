using System;
using System.Linq;
using KSP.UI.Screens;
using static MechJebLib.Utils.Statics;
using UnityEngine;
using static TransferWindowPlanner2.MoreMaths;

namespace TransferWindowPlanner2;

using static GuiUtils;

[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
public class MainWindow : MonoBehaviour
{
    private const string ModName = "TransferWindowPlanner2";
    private const string Icon = "TransferWindowPlanner2/icon";
    private const string Marker = "TransferWindowPlanner2/marker";
    private static readonly Vector2 MarkerSize = new(16, 16);
    private const int PlotWidth = 500;
    private const int PlotHeight = 400;
    private const int WindowWidth = 750;
    private const int WindowHeight = 600;

    private readonly Texture2D _plotArrival = new(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private readonly Texture2D _plotDeparture = new(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private readonly Texture2D _plotTotal = new(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);

    // Nullability: these are all initialized in `Start()`
    private GUIStyle _boxStyle = null!,
                     _boxTitleStyle = null!,
                     _plotBoxStyle = null!,
                     _inputStyle = null!,
                     _invalidInputStyle = null!,
                     _buttonStyle = null!,
                     _invalidButtonStyle = null!,
                     _plotTooltipStyle = null!;

    private ApplicationLauncherButton? _button;

    private enum PlotType
    {
        Departure = 0, Arrival = 1, Total = 2,
    }

    private PlotType _selectedPlot = PlotType.Total;

    private bool _showMainWindow;
    private Rect _winPos = new(450, 100, WindowWidth, WindowHeight);
    private bool _showDepartureCbWindow;
    private Rect _departureCbWinPos = new(200, 200, 200, 200);
    private bool _showArrivalCbWindow;
    private Rect _arrivalCbWinPos = new(300, 200, 200, 200);
    private Rect _plotPosition;

    // Input fields
    private CelestialBody _departureCb = null!; // Nullability: Initialized in `Start()`
    private CelestialBody _arrivalCb = null!; // Nullability: Initialized in `Start()`
    private DoubleInput _departureAltitude = new(100.0);
    private DoubleInput _departureInclination = new(0.0);
    private DoubleInput _arrivalAltitude = new(100.0);
    private bool _circularize = true;

    private DateInput _earliestDeparture = new(0.0);
    private DateInput _latestDeparture = new(0.0);
    private DateInput _earliestArrival = new(0.0);
    private DateInput _latestArrival = new(0.0);

    private DoubleInput _plotMargin = new(2.0);

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

        _boxStyle = new GUIStyle(HighLogic.Skin.box) { alignment = TextAnchor.UpperLeft };
        _inputStyle = new GUIStyle(HighLogic.Skin.textField) { alignment = TextAnchor.MiddleRight };
        _invalidInputStyle = new GUIStyle(_inputStyle)
        {
            normal = { textColor = Color.red },
        };
        _boxTitleStyle = new GUIStyle(HighLogic.Skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        _plotBoxStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter };
        _buttonStyle = new GUIStyle(HighLogic.Skin.button);
        _invalidButtonStyle = new GUIStyle(_buttonStyle) { normal = { textColor = Color.red } };
        _plotTooltipStyle = new GUIStyle(_boxStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
        };


        _departureCb = FlightGlobals.GetHomeBody();
        _arrivalCb = FlightGlobals.Bodies.Find(cb => ValidCbCombination(_departureCb, cb));
        if (_arrivalCb == null)
        {
            _arrivalCb = _departureCb; // No valid destinations from the home body: let the user worry about it.
        }
        ResetTimes();
    }

    public void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
        if (_button != null) { ApplicationLauncher.Instance.RemoveModApplication(_button); }
        if (_ejectAngleRenderer != null) { Destroy(_ejectAngleRenderer); }
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (_showMainWindow) { _winPos = GUILayout.Window(GetHashCode(), _winPos, WindowGUI, ModName); }

        if (_showDepartureCbWindow)
        {
            _departureCbWinPos =
                GUILayout.Window(GetHashCode() + 1, _departureCbWinPos, DepartureCbWindow, "Origin body");
        }

        if (_showArrivalCbWindow)
        {
            _arrivalCbWinPos =
                GUILayout.Window(GetHashCode() + 2, _arrivalCbWinPos, ArrivalCbWindow, "Destination body");
        }
    }


    private void ShowWindow()
    {
        _showMainWindow = true;
    }

    private void HideWindow()
    {
        _showMainWindow = false;
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

    private void ArrivalCbWindow(int id)
    {
        var cb = ShowCbSelection(_departureCb);
        GUI.DragWindow();

        if (cb == null) { return; }
        _arrivalCb = cb;
        _showArrivalCbWindow = false;
        ResetTimes();
    }

    private void DepartureCbWindow(int id)
    {
        var cb = ShowCbSelection(_arrivalCb);
        GUI.DragWindow();

        if (cb == null) { return; }
        _departureCb = cb;
        _showDepartureCbWindow = false;
        ResetTimes();
    }

    private CelestialBody? ShowCbSelection(CelestialBody other)
    {
        using var scope = new GUILayout.VerticalScope();

        foreach (var cb in FlightGlobals.Bodies)
        {
            if (cb.isStar) { continue; }

            if (GUILayout.Button(
                    cb.displayName.LocalizeRemoveGender(),
                    ValidCbCombination(other, cb) ? _buttonStyle : _invalidButtonStyle)) { return cb; }
        }

        return null;
    }

    private static bool ValidCbCombination(CelestialBody cb1, CelestialBody cb2) =>
        cb1 != cb2 &&
        !cb1.isStar && !cb2.isStar &&
        cb1.referenceBody == cb2.referenceBody;

    private bool ValidInputs() =>
        ValidCbCombination(_departureCb, _arrivalCb) &&
        _departureAltitude.Valid &&
        _departureInclination.Valid &&
        _earliestDeparture.Valid &&
        _latestDeparture.Valid &&
        _arrivalAltitude.Valid &&
        _earliestArrival.Valid &&
        _latestArrival.Valid &&
        _plotMargin.Valid;

    private void ShowInputs()
    {
        using var scope = new GUILayout.VerticalScope(
            GUILayout.ExpandWidth(false), GUILayout.Width(WindowWidth - PlotWidth));

        using (new GUILayout.VerticalScope(_boxStyle))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Origin", _boxTitleStyle);
                GUILayout.FlexibleSpace();
                _showDepartureCbWindow = GUILayout.Toggle(
                    _showDepartureCbWindow,
                    _departureCb.displayName.LocalizeRemoveGender(),
                    ValidCbCombination(_departureCb, _arrivalCb) ? _buttonStyle : _invalidButtonStyle,
                    GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2));
            }
            LabeledDoubleInput("Altitude", ref _departureAltitude, "km");
            LabeledDoubleInput("Min. Inclination", ref _departureInclination, "°");
            LabeledDateInput("Earliest", ref _earliestDeparture);
            LabeledDateInput("Latest", ref _latestDeparture);
        }

        using (new GUILayout.VerticalScope(_boxStyle))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Destination", _boxTitleStyle);
                GUILayout.FlexibleSpace();
                _showArrivalCbWindow = GUILayout.Toggle(
                    _showArrivalCbWindow,
                    _arrivalCb.displayName.LocalizeRemoveGender(),
                    ValidCbCombination(_departureCb, _arrivalCb) ? _buttonStyle : _invalidButtonStyle,
                    GUILayout.ExpandWidth(false), GUILayout.Width((WindowWidth - PlotWidth) / 2));
            }
            LabeledDoubleInput("Altitude", ref _arrivalAltitude, "km");
            _circularize = GUILayout.Toggle(_circularize, "Circularize");
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
                _plotBoxStyle,
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
                var size = _plotTooltipStyle.CalcSize(new GUIContent(tooltip));
                GUI.Label(
                    new Rect(mousePos.x + 25, mousePos.y - 5, size.x, size.y),
                    tooltip, _plotTooltipStyle);
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
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Departure", _boxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_transferDetails.DepartureTime, _transferDetails.IsShort));
            LabeledInfo("Periapsis altitude", $"{_transferDetails.DeparturePeriapsis.ToSI()}m");
            LabeledInfo("Inclination", $"{Rad2Deg(_transferDetails.DepartureInclination):F2} °");
            LabeledInfo("LAN", $"{Rad2Deg(_transferDetails.DepartureLAN):F2} °");
            LabeledInfo("Asymptote right ascension", $"{Rad2Deg(_transferDetails.DepartureAsyRA):F2} °");
            LabeledInfo("Asymptote declination", $"{Rad2Deg(_transferDetails.DepartureAsyDecl):F2} °");
            LabeledInfo("C3", $"{_transferDetails.DepartureC3 / 1e6:F2}km²/s²");
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
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Arrival", _boxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_transferDetails.ArrivalTime, _transferDetails.IsShort));
            LabeledInfo("Periapsis altitude", $"{_transferDetails.ArrivalPeriapsis.ToSI()}m");
            LabeledInfo("Distance between bodies", $"{_transferDetails.ArrivalDistance.ToSI()}m");
            GUILayout.Label(""); // Empty row
            LabeledInfo("Asymptote right ascension", $"{Rad2Deg(_transferDetails.ArrivalAsyRA):F2} °");
            LabeledInfo("Asymptote declination", $"{Rad2Deg(_transferDetails.ArrivalAsyDecl):F2} °");
            LabeledInfo("C3", $"{_transferDetails.ArrivalC3 / 1e6:F2}km²/s²");
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
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Transfer", _boxTitleStyle);
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
                _showParkingOrbit = GUILayout.Toggle(_showParkingOrbit, "Show parking orbit in map view", _buttonStyle);
                _showEjectAngle = GUILayout.Toggle(_showEjectAngle, "Show ejection angles in map view", _buttonStyle);
            }
        }
    }

    private void Update()
    {
        if (!CurrentSceneHasMapView()) { return; }
        if (_showParkingOrbit && _parkingOrbitRenderer == null) { EnableParkingOrbitRenderer(); }
        else if (!_showParkingOrbit && _parkingOrbitRenderer != null) { DisableParkingOrbitRenderer(); }

        if (_showEjectAngle && !_ejectAngleRenderer!.IsDrawing) { EnableEjectionRenderer(); }
        else if (!_showEjectAngle && !_ejectAngleRenderer!.IsHiding) { DisableEjectionRenderer(); }
    }

    private void GeneratePlots()
    {
        if (_solver.WorkerState is not Solver.BackgroundWorkerState.Idle)
        {
            Debug.LogError($"Solver is already working!");
            return;
        }

        _solver.GeneratePorkchop(
            _departureCb, _arrivalCb,
            _earliestDeparture.Ut, _latestDeparture.Ut,
            _earliestArrival.Ut, _latestArrival.Ut,
            _departureAltitude.Value * 1e3, _arrivalAltitude.Value * 1e3, _circularize);
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
        _transferDetails = Solver.CalculateDetails(
            _departureCb, _arrivalCb, _departureAltitude.Value * 1e3, _arrivalAltitude.Value * 1e3,
            Deg2Rad(_departureInclination.Value), _circularize, tDep, tArr);

        if (_showEjectAngle) { EnableEjectionRenderer(); }
        if (_showParkingOrbit) { EnableParkingOrbitRenderer(); }
    }

    private void EnableParkingOrbitRenderer()
    {
        if (_parkingOrbitRenderer != null) { _parkingOrbitRenderer.Cleanup(); }
        _parkingOrbitRenderer = ParkingOrbitRenderer.Setup(
            _departureCb,
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
        if (_ejectAngleRenderer == null) { return; }
        var vInf = new Vector3d(
            _transferDetails.DepartureVInf.x,
            _transferDetails.DepartureVInf.y,
            _transferDetails.DepartureVInf.z);
        var peDir = new Vector3d(
            _transferDetails.DeparturePeDirection.x,
            _transferDetails.DeparturePeDirection.y,
            _transferDetails.DeparturePeDirection.z);

        _ejectAngleRenderer.Draw(_departureCb, vInf, peDir);
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
                _transferDetails.Origin.bodyDisplayName.LocalizeRemoveGender(),
                _transferDetails.Destination.bodyDisplayName.LocalizeRemoveGender(),
                KSPUtil.PrintDateDelta(_transferDetails.TimeOfFlight, _transferDetails.IsShort)),
            _transferDetails.DepartureTime - 24 * 60 * 60);

        var alarm = KACWrapper.KAC.Alarms.First(a => a.ID == tmpID);
        alarm.Notes = _transferDetails.Description();
        alarm.AlarmMargin = 24 * 60 * 60;
        alarm.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
        alarm.XferOriginBodyName = _transferDetails.Origin.bodyDisplayName.LocalizeRemoveGender();
        alarm.XferTargetBodyName = _transferDetails.Destination.bodyDisplayName.LocalizeRemoveGender();
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
            2 * SynodicPeriod(_departureCb.orbit.period, _arrivalCb.orbit.period),
            2 * _departureCb.orbit.period);

        _earliestDeparture.Ut = Planetarium.GetUniversalTime();
        _latestDeparture.Ut = _earliestDeparture.Ut + departureRange;

        var hohmannTime = HohmannTime(
            _departureCb.referenceBody.gravParameter, _departureCb.orbit.semiMajorAxis,
            _arrivalCb.orbit.semiMajorAxis);
        var transferMin = Math.Max(hohmannTime - _arrivalCb.orbit.period, hohmannTime / 2);
        var travelMax = transferMin + Math.Min(2 * _arrivalCb.orbit.period, hohmannTime);

        _earliestArrival.Ut = _earliestDeparture.Ut + transferMin;
        _latestArrival.Ut = _latestDeparture.Ut + travelMax;
    }


    private static void LabeledInfo(string label, string value)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value);
        }
    }

    private void LabeledDoubleInput(string label, ref DoubleInput input, string? unit = null)
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label);
        GUILayout.FlexibleSpace();
        input.Text = GUILayout.TextField(
            input.Text,
            input.Valid ? _inputStyle : _invalidInputStyle,
            GUILayout.Width(100));
        if (!string.IsNullOrEmpty(unit)) { GUILayout.Label(unit, GUILayout.ExpandWidth(false)); }
    }

    private void LabeledDateInput(string label, ref DateInput input)
    {
        using var scope = new GUILayout.HorizontalScope();

        GUILayout.Label(label);
        GUILayout.FlexibleSpace();

        input.Text = GUILayout.TextField(
            input.Text,
            input.Valid ? _inputStyle : _invalidInputStyle,
            GUILayout.Width(100));
    }
}
