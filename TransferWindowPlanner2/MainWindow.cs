using System;
using KSP.UI.Screens;
using UnityEngine;
using static TransferWindowPlanner2.MathUtils;

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

    private const double Rad2Deg = 180.0 / Math.PI;

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

    private DoubleInput _plotMargin = new(5.0);

    private (int, int) _selectedTransfer;
    private Solver.TransferDetails _transferDetails;

    private Solver? _solver;

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
    }

    protected void Start()
    {
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

    private static bool ValidCbCombination(CelestialBody cb1, CelestialBody cb2)
    {
        return cb1 != cb2 &&
               !cb1.isStar && !cb2.isStar &&
               cb1.referenceBody == cb2.referenceBody;
    }

    private bool ValidInputs()
    {
        return ValidCbCombination(_departureCb, _arrivalCb) &&
               _departureAltitude.Valid &&
               _departureInclination.Valid &&
               _earliestDeparture.Valid &&
               _latestDeparture.Valid &&
               _arrivalAltitude.Valid &&
               _earliestArrival.Valid &&
               _latestArrival.Valid &&
               _plotMargin.Valid;
    }

    private void ShowInputs()
    {
        using var scope = new GUILayout.VerticalScope(
            GUILayout.ExpandWidth(false), GUILayout.Width(WindowWidth - PlotWidth));

        GUILayout.FlexibleSpace();

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

        GUILayout.FlexibleSpace();

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

        if (GUILayout.Button("Reset times")) { ResetTimes(); }

        GUILayout.FlexibleSpace();

        using (new GuiEnabled(ValidInputs()))
        {
            if (GUILayout.Button("Plot it!")) { GeneratePlots(); }
        }

        GUILayout.FlexibleSpace();
    }

    private void ShowPlot()
    {
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

            if (_solver == null) { return; }

            var mousePos = Event.current.mousePosition;
            if (_plotPosition.Contains(mousePos))
            {
                var pos = mousePos - _plotPosition.position;
                var i = (int)pos.x;
                var j = (int)pos.y;
                var (dep, arr) = _solver.TimesFor(i, j);
                var tooltip = $"Departure: {KSPUtil.PrintDateCompact(dep, false)}\n"
                              + $"Arrival: {KSPUtil.PrintDateCompact(arr, false)}\n"
                              + $"Eject: {ToStringSIPrefixed(_solver.DepΔv[i, j], "m/s")}\n"
                              + $"Insert: {ToStringSIPrefixed(_solver.ArrΔv[i, j], "m/s")}\n"
                              + $"Total: {ToStringSIPrefixed(_solver.TotalΔv[i, j], "m/s")}";
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
        using (new GUILayout.VerticalScope(
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Departure", _boxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_transferDetails.DepartureTime, true));
            LabeledInfo(
                "Periapsis altitude",
                ToStringSIPrefixed(_transferDetails.DeparturePeriapsis, "m"));
            LabeledInfo("Inclination", $"{_transferDetails.DepartureInclination * Rad2Deg:F2} °");
            LabeledInfo("LAN", $"{_transferDetails.DepartureLAN * Rad2Deg:F2} °");
            LabeledInfo("Asymptote right ascension", $"{_transferDetails.DepartureAsyRA * Rad2Deg:F2} °");
            LabeledInfo("Asymptote declination", $"{_transferDetails.DepartureAsyDecl * Rad2Deg:F2} °");
            LabeledInfo("C3", ToStringSIPrefixed(_transferDetails.DepartureC3, "m²/s²", 2));
            LabeledInfo("Δv", ToStringSIPrefixed(_transferDetails.DepartureΔv, "m/s"));
        }
    }

    private void ShowArrivalInfo()
    {
        using (new GUILayout.VerticalScope(
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Arrival", _boxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_transferDetails.ArrivalTime, true));
            LabeledInfo(
                "Periapsis altitude",
                ToStringSIPrefixed(_transferDetails.ArrivalPeriapsis, "m"));
            LabeledInfo("Distance between bodies", ToStringSIPrefixed(_transferDetails.ArrivalDistance, "m"));
            GUILayout.Label(""); // Empty row
            LabeledInfo("Asymptote right ascension", $"{_transferDetails.ArrivalAsyRA * Rad2Deg:F2} °");
            LabeledInfo("Asymptote declination", $"{_transferDetails.ArrivalAsyDecl * Rad2Deg:F2} °");
            LabeledInfo("C3", ToStringSIPrefixed(_transferDetails.ArrivalC3, "m²/s²", 2));
            LabeledInfo("Δv", ToStringSIPrefixed(_transferDetails.ArrivalΔv, "m/s"));
        }
    }

    private void ShowTransferInfo()
    {
        using var scope = new GUILayout.VerticalScope();

        using (new GUILayout.VerticalScope(
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Transfer", _boxTitleStyle);
            LabeledInfo(
                "Flight time",
                KSPUtil.PrintDateDeltaCompact(
                    _transferDetails.TimeOfFlight, true, false, 2));
            LabeledInfo("Total Δv", ToStringSIPrefixed(_transferDetails.TotalΔv, "m/s"));
        }

        using (new GuiEnabled(_solver != null))
        {
            // Nullability: the methods can only be called if the buttons are enabled, i.e. when _solver is not null.
            if (GUILayout.Button("Minimize departure Δv")) { UpdateTransferDetails(_solver!.MinDepPoint); }
            if (GUILayout.Button("Minimize arrival Δv")) { UpdateTransferDetails(_solver!.MinArrPoint); }
            if (GUILayout.Button("Minimize total Δv")) { UpdateTransferDetails(_solver!.MinTotalPoint); }
        }
        GUILayout.FlexibleSpace();
        using (new GuiEnabled(_transferDetails.IsValid))
        {
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
        _solver ??= new Solver(PlotWidth, PlotHeight);

        _solver.GeneratePorkchop(
            _departureCb, _arrivalCb,
            _earliestDeparture.Ut, _latestDeparture.Ut,
            _earliestArrival.Ut, _latestArrival.Ut,
            _departureAltitude.Value * 1e3, _arrivalAltitude.Value * 1e3, _circularize);

        DrawTexture(_plotDeparture, _solver.DepΔv, _solver.MinDepΔv, _solver.MinDepΔv * _plotMargin.Value);
        DrawTexture(_plotArrival, _solver.ArrΔv, _solver.MinArrΔv, _solver.MinDepΔv * _plotMargin.Value);
        DrawTexture(_plotTotal, _solver.TotalΔv, _solver.MinTotalΔv, _solver.MinDepΔv * _plotMargin.Value);

        UpdateTransferDetails(_solver.MinTotalPoint);
    }

    private void UpdateTransferDetails((int, int) point)
    {
        if (_solver == null) { return; }
        var (tDep, tArr) = _solver.TimesFor(point);
        _selectedTransfer = point;
        _transferDetails = Solver.CalculateDetails(
            _departureCb, _arrivalCb, _departureAltitude.Value * 1e3, _arrivalAltitude.Value * 1e3,
            _departureInclination.Value / Rad2Deg, _circularize, tDep, tArr);

        if (_showEjectAngle) { EnableEjectionRenderer(); }
        if (_showParkingOrbit) { EnableParkingOrbitRenderer(); }
    }

    private void EnableParkingOrbitRenderer()
    {
        if (_parkingOrbitRenderer != null) { _parkingOrbitRenderer.Cleanup(); }
        _parkingOrbitRenderer = ParkingOrbitRenderer.Setup(
            _departureCb,
            _transferDetails.DeparturePeriapsis,
            _transferDetails.DepartureInclination * Rad2Deg,
            _transferDetails.DepartureLAN * Rad2Deg);
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
        _ejectAngleRenderer.DrawAngle(
            _departureCb, _transferDetails.DepartureVInf, _transferDetails.DeparturePeDirection);
    }

    private void DisableEjectionRenderer()
    {
        if (_ejectAngleRenderer == null) { return; }
        _ejectAngleRenderer.HideAngle();
    }


    private static void DrawTexture(Texture2D tex, double[,] c3, double minC3, double maxC3)
    {
        for (var i = 0; i < PlotWidth; ++i)
        {
            for (var j = 0; j < PlotHeight; ++j)
            {
                var color = ColorMap.MapColorLogarithmicReverse((float)c3[i, j], (float)minC3, (float)maxC3);
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
