using ClickThroughFix;
using KSP.UI.Screens;
using MechJebLibBindings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UniLinq;
using UnityEngine;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2.UI
{
    using Rendering;
    using Solver;
    using static GuiUtils;
    using static Solver.MoreMaths;

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
        // Width of the three mode buttons combined; leaves room for Show scales.
        private const int PlotModeSelectorWidth = 350;
        private const int InputPanelWidth = 250;
        private const int ElapsedAxisWidth = 94;
        private const int DepartureAxisHeight = 50;
        private const float MinZoomDragPixels = 6f;
        // The axes use their own layout space; do not reduce the plot's 500x400 resolution.
        private const int WindowWidth = InputPanelWidth + ElapsedAxisWidth + PlotWidth + 20;
        private const int WindowHeight = 650;
        private int DisplayWindowWidth => WindowWidth - (_showScales ? 0 : ElapsedAxisWidth);
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
        private GUIStyle _scaleValueStyle = null!;
        private GUIStyle _scaleTitleStyle = null!;
        private bool _showScales = true;

        // Plot drag-selection is in the local coordinates of the 500x400 porkchop plot.
        // A short click still selects a single transfer; a rectangle reruns the solver
        // over the selected departure-time and time-of-flight ranges.
        private bool _zoomDragging;
        private Vector2 _zoomDragStart;
        private Vector2 _zoomDragEnd;
        private readonly Stack<PlotRange> _zoomHistory = new Stack<PlotRange>();

        private struct PlotRange
        {
            internal double EarliestDeparture;
            internal double LatestDeparture;
            internal double MinTimeOfFlight;
            internal double MaxTimeOfFlight;

            internal PlotRange(double earliest, double latest, double minTof, double maxTof)
            {
                EarliestDeparture = earliest;
                LatestDeparture = latest;
                MinTimeOfFlight = minTof;
                MaxTimeOfFlight = maxTof;
            }
        }

        private MapAngleRenderer? _ejectAngleRenderer;
        private ParkingOrbitRendererHack? _parkingOrbitRenderer;
        private bool _showEjectAngle;
        private bool _showParkingOrbit;

        #endregion

        #region Input fields

        private enum PlotType
        {
            Departure = 0,
            Arrival = 1,
            Total = 2,
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
                _zoomHistory.Clear();
                _plotIsDrawn = false;
                OnInputChanged();
            }
        }

        internal Endpoint DepartureBody
        {
            get => _departureBody;
            set
            {
                _departureBody = value;
                _zoomHistory.Clear();
                _plotIsDrawn = false;
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
                _zoomHistory.Clear();
                _plotIsDrawn = false;
                if (_arrivalBody.IsCelestial)
                {
                    _arrivalPeriapsis.Max = _arrivalApoapsis.Max =
                        1e-3 * (_arrivalBody.Celestial!.sphereOfInfluence - _arrivalBody.Celestial!.Radius);
                }
                else { _arrivalPeriapsis.Max = _arrivalApoapsis.Max = double.PositiveInfinity; }
                OnInputChanged();
            }
        }

        private DoubleInput _departureAltitude = new DoubleInput(100.0, 0.0);
        private DoubleInput _departureInclination = new DoubleInput(0.0, 0.0, 90.0);
        private DoubleInput _arrivalPeriapsis = new DoubleInput(100.0, 0.0);
        private DoubleInput _arrivalApoapsis = new DoubleInput(100.0, 0.0);

        // These are initialized into a default invalid state. During Start(), ResetTimes() is called which will then
        // reset them to a valid state. This is done to avoid calling any KSP/Unity APIs during class construction.
        private DateInput _earliestDeparture = new DateInput();
        private DateInput _latestDeparture = new DateInput();
        private DoubleInput _minTimeOfFlight = new DoubleInput(100, min: 0.0, minInclusive: false);
        private DoubleInput _maxTimeOfFlight = new DoubleInput(400, min: 0.0, minInclusive: false);

        private DoubleInput _plotMarginDep = new DoubleInput(1e3, min: 0, minInclusive: false);
        private DoubleInput _plotMarginArr = new DoubleInput(1e3, min: 0, minInclusive: false);

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
            _scaleValueStyle = new GUIStyle(HighLogic.Skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                padding = new RectOffset(0, 0, 0, 0),
            };
            _scaleTitleStyle = new GUIStyle(_scaleValueStyle) { fontStyle = FontStyle.Bold };
            ResetInputs();
            _bodySelectionWindow = BodySelectionWindow.Setup(this);
        }

        private void ResetInputs()
        {
            CancelZoomDrag();
            _zoomHistory.Clear();
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
            _arrivalPeriapsis.Max = 1e-3 * (arrivalCb.sphereOfInfluence - arrivalCb.Radius);

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
            CancelZoomDrag();
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

            // Fit the window to the graph: when the scales are hidden, reclaim their
            // left margin rather than leaving an empty strip next to the plot.
            WinPos.width = DisplayWindowWidth;
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
            CancelZoomDrag();
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
                if (!_arrivalPeriapsis.Parsed || _arrivalPeriapsis.Value < 0)
                {
                    _errors.Add($"Arrival altitude should be a positive number ({_arrivalPeriapsis.Text})");
                }
                else if (_arrivalPeriapsis.Value > _arrivalPeriapsis.Max)
                {
                    _errors.Add($"{_arrivalPeriapsis.Value} km is outside the sphere of influence of {ArrivalBody.Name}");
                }

                if (!_arrivalApoapsis.Parsed || _arrivalApoapsis.Value < 0)
                {
                    _errors.Add($"Arrival apoapsis should be a positive number ({_arrivalApoapsis.Text})");
                }
                else if (_arrivalApoapsis.Value > _arrivalApoapsis.Max)
                {
                    _errors.Add($"{_arrivalApoapsis.Value} km is outside the sphere of influence of {ArrivalBody.Name}");
                }
                else if (_arrivalApoapsis.Value < _arrivalPeriapsis.Value)
                {
                    _errors.Add("Apoapsis must be greater than periapsis");
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
                GUILayout.ExpandWidth(false), GUILayout.Width(InputPanelWidth));

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
                        GUILayout.ExpandWidth(false), GUILayout.Width(InputPanelWidth / 2f)))
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
                            GUILayout.ExpandWidth(false), GUILayout.Width(InputPanelWidth / 2f)))
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
                            GUILayout.ExpandWidth(false), GUILayout.Width(InputPanelWidth / 2)))
                    {
                        nextSelectionKind = BodySelectionWindow.SelectionKind.Arrival;
                    }
                }
                using (new GuiEnabled(ArrivalBody.IsCelestial))
                {
                    LabeledDoubleInput("Periapsis", ref _arrivalPeriapsis, "km");
                    LabeledDoubleInput("Apoapsis", ref _arrivalApoapsis, "km");
                }
                LabeledDoubleInput("Min. transfer time", ref _minTimeOfFlight, "days");
                LabeledDoubleInput("Max. transfer time", ref _maxTimeOfFlight, "days");
                LabeledDoubleInput("Δv margin", ref _plotMarginArr, "m/s");
            }

            _bodySelectionWindow.Which = nextSelectionKind;


            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset times"))
            {
                _zoomHistory.Clear();
                ResetTimes();
            }
            using (new GUILayout.HorizontalScope())
            {
                using (new GuiEnabled(_errors.Count == 0 && !_solver.IsRunning))
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
            if (_plotIsUpdating && _solver.IsCompleted) { OnSolverDone(); }

            using (new GUILayout.VerticalScope(
                       GUILayout.ExpandWidth(false),
                       GUILayout.Width(PlotWidth + (_showScales ? ElapsedAxisWidth : 0))))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    _selectedPlot = (PlotType)GUILayout.SelectionGrid(
                        (int)_selectedPlot, new[] { "Departure", "Arrival", "Total" }, 3,
                        GUILayout.Width(PlotModeSelectorWidth));
                    GUILayout.Space(20);
                    _showScales = GUILayout.Toggle(_showScales, "", GUILayout.ExpandWidth(false));
                    GUILayout.Label("Show scales");
                }

                // The axis margin is NOT part of _plotPosition. Mouse selection, tooltips,
                // marker placement, and zoom coordinates continue to use image pixels only.
                // The previous repaint's plot rectangle is available to PlotHandleMouse().
                var tooltip = PlotHandleMouse();
                using (new GUILayout.HorizontalScope())
                {
                    // Keep the same GUILayout entries in both states so a toggle
                    // during MouseUp cannot change the number of layout controls.
                    GUILayout.Space(_showScales ? ElapsedAxisWidth : 0);
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

                // Reserve room for the date ticks and the "Departure date" caption.
                GUILayout.Space(_showScales ? DepartureAxisHeight : 0);

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(
                        _plotIsUpdating ? "Calculating selected area..." : "Drag to zoom; click to select",
                        GUILayout.ExpandWidth(true));
                    using (new GuiEnabled(_zoomHistory.Count > 0 && !_plotIsUpdating && _errors.Count == 0))
                    {
                        if (GUILayout.Button("Zoom out", GUILayout.ExpandWidth(false))) { ZoomOut(); }
                        if (GUILayout.Button("Reset zoom", GUILayout.ExpandWidth(false))) { ResetZoom(); }
                    }
                }
            }

            if (_plotIsDrawn && !_plotIsUpdating)
            {
                if (_showScales) { DrawPlotScales(); }
                DrawPlotMarker();
            }
            if (_zoomDragging) { DrawZoomRectangle(); }
        }

        private void DrawPlotScales()
        {
            if (Event.current.type != EventType.Repaint) { return; }

            // The solver uses inclusive pixel endpoints: (0, 0) is the earliest
            // departure / longest elapsed time, and (499, 399) the latest / shortest.
            // Reading tick values from TimesFor keeps the scales in sync after zoom.
            var plot = _plotPosition;
            var range = PlottedRange();
            var daySeconds = KSPUtil.dateTimeFormatter.Day;
            var includeTime = (range.LatestDeparture - range.EarliestDeparture) < 3.0 * daySeconds;
            var elapsedSpanDays = (range.MaxTimeOfFlight - range.MinTimeOfFlight) / daySeconds;

            // Use fewer date labels if long calendar strings would overlap near
            // the first and last ticks (whose labels are clamped to the chart).
            var earliestLabel = KSPUtil.PrintDateCompact(range.EarliestDeparture, includeTime).Trim();
            var middleLabel = KSPUtil.PrintDateCompact(
                (range.EarliestDeparture + range.LatestDeparture) * 0.5, includeTime).Trim();
            var latestLabel = KSPUtil.PrintDateCompact(range.LatestDeparture, includeTime).Trim();
            var widestDateLabel = Math.Max(_scaleValueStyle.CalcSize(new GUIContent(earliestLabel)).x,
                Math.Max(_scaleValueStyle.CalcSize(new GUIContent(middleLabel)).x,
                    _scaleValueStyle.CalcSize(new GUIContent(latestLabel)).x)) + 6;
            var dateTickCount = widestDateLabel > PlotWidth / 3f ? 2
                : includeTime || widestDateLabel > (PlotWidth - 1) / 6f ? 3 : 5;

            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(plot.xMin - 1, plot.yMin, 1, PlotHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(plot.xMin, plot.yMax, PlotWidth, 1), Texture2D.whiteTexture);

            // Horizontal axis: increasing departure date, left to right.
            for (var tick = 0; tick < dateTickCount; ++tick)
            {
                var xPixel = (int)Math.Round((PlotWidth - 1) * tick / (double)(dateTickCount - 1));
                var (departure, _) = _solver.TimesFor(xPixel, 0);
                var x = plot.xMin + xPixel;
                GUI.DrawTexture(new Rect(x, plot.yMax, 1, 6), Texture2D.whiteTexture);

                var label = KSPUtil.PrintDateCompact(departure, includeTime).Trim();
                var labelWidth = Mathf.Min(PlotWidth, _scaleValueStyle.CalcSize(new GUIContent(label)).x + 6);
                var left = Mathf.Clamp(x - labelWidth * 0.5f, plot.xMin, plot.xMax - labelWidth);
                GUI.color = previousColor;
                GUI.Label(new Rect(left, plot.yMax + 7, labelWidth, 18), label, _scaleValueStyle);
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
            }

            GUI.color = previousColor;
            GUI.Label(new Rect(plot.xMin, plot.yMax + 28, PlotWidth, 19),
                "Departure date", _scaleTitleStyle);

            // Vertical axis: elapsed flight time in KSP calendar days, decreasing
            // from the top of the graph to the bottom.
            for (var tick = 0; tick < 5; ++tick)
            {
                var yPixel = (int)Math.Round((PlotHeight - 1) * tick / 4.0);
                var (_, elapsedTime) = _solver.TimesFor(0, yPixel);
                var y = plot.yMin + yPixel;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                GUI.DrawTexture(new Rect(plot.xMin - 6, y, 6, 1), Texture2D.whiteTexture);
                GUI.color = previousColor;
                var label = FormatElapsedDays(elapsedTime / daySeconds, elapsedSpanDays);
                var labelTop = Mathf.Clamp(y - 9, plot.yMin, plot.yMax - 18);
                GUI.Label(new Rect(plot.xMin - 70, labelTop, 62, 18), label, _scaleValueStyle);
            }

            // Keep the axis title left of its numeric labels rather than over the
            // graph. Restore the matrix so subsequent IMGUI controls are unaffected.
            var pivot = new Vector2(plot.xMin - ElapsedAxisWidth + 10, plot.center.y);
            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(-90f, pivot);
            GUI.Label(new Rect(pivot.x - 88, pivot.y - 10, 176, 20),
                "Elapsed time (days)", _scaleTitleStyle);
            GUI.matrix = oldMatrix;
            GUI.color = previousColor;
        }

        private static string FormatElapsedDays(double days, double spanDays)
        {
            // Pick enough decimal places to distinguish adjacent major ticks even
            // for very narrow zoom ranges, without adding unnecessary precision.
            var majorStep = Math.Abs(spanDays) / 4.0;
            var decimals = majorStep > 0.0
                ? Math.Max(0, Math.Min(8, (int)Math.Ceiling(-Math.Log10(majorStep)) + 1))
                : 0;
            return days.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals),
                CultureInfo.CurrentCulture);
        }

        private void DrawPlotMarker()
        {
            var selected = new Vector2(_selectedTransfer.Item1, _selectedTransfer.Item2);
            var markerPosition = _plotPosition.position + selected - 0.5f * MarkerSize;
            GUI.Box(new Rect(markerPosition, MarkerSize), MarkerTex, GUIStyle.none);
        }

        private string PlotHandleMouse()
        {
            if (!_plotIsDrawn || _plotIsUpdating) { return ""; }

            var evt = Event.current;
            // Do not claim GUIUtility.hotControl here. Doing so can leave the enclosing
            // GUILayoutWindow unable to drag if the IMGUI control ID changes between
            // MouseDown and MouseUp (for example when the layout changes).
            // Consuming only the graph's mouse events keeps GUI.DragWindow() working
            // normally everywhere outside the plot.
            var insidePlot = _plotPosition.Contains(evt.mousePosition);
            var pos = ClampToPlot(evt.mousePosition - _plotPosition.position);

            // If a release was missed outside the game window, the next press
            // outside the plot must not inherit the old graph selection.
            if (_zoomDragging && evt.type == EventType.MouseDown && evt.button == 0 && !insidePlot)
            {
                CancelZoomDrag();
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && insidePlot)
            {
                _zoomDragging = true;
                _zoomDragStart = _zoomDragEnd = pos;
                evt.Use(); // Prevent GUI.DragWindow() from moving the window on this press.
                return "";
            }

            if (_zoomDragging)
            {
                if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    _zoomDragEnd = pos; // Clamped if dragged beyond the plot.
                    evt.Use();
                    return "";
                }

                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    CancelZoomDrag();
                    evt.Use();
                    return "";
                }

                if (evt.type == EventType.MouseDown && evt.button == 1)
                {
                    CancelZoomDrag();
                    evt.Use();
                    return "";
                }

                if (evt.type == EventType.MouseUp && evt.button == 0)
                {
                    _zoomDragEnd = pos;
                    var start = _zoomDragStart;
                    var end = _zoomDragEnd;
                    CancelZoomDrag();
                    evt.Use();

                    var width = Math.Abs(end.x - start.x);
                    var height = Math.Abs(end.y - start.y);
                    if (width >= MinZoomDragPixels && height >= MinZoomDragPixels)
                    {
                        ZoomToRectangle(start, end);
                    }
                    else if (width < MinZoomDragPixels && height < MinZoomDragPixels && insidePlot)
                    {
                        // Preserve the original single-click transfer selection behavior.
                        UpdateTransferDetails(((int)pos.x, (int)pos.y));
                    }
                    return "";
                }
                return "";
            }

            if (!insidePlot) { return ""; }

            var i = (int)pos.x;
            var j = (int)pos.y;
            var (dep, tof) = _solver.TimesFor(i, j);
            var tooltip = $"Departure: {KSPUtil.PrintDateCompact(dep, includeTime: true)}"
                          + $"\nArrival: {KSPUtil.PrintDateCompact(dep + tof, includeTime: true)}"
                          + $"\nEject: {_solver.DepΔv[i, j].ToSI()}m/s"
                          + $"\nInsert: {_solver.ArrΔv[i, j].ToSI()}m/s"
                          + $"\nTotal: {_solver.TotalΔv[i, j].ToSI()}m/s";
            return tooltip;
        }

        private static Vector2 ClampToPlot(Vector2 pos) => new Vector2(
            Mathf.Clamp(pos.x, 0, PlotWidth - 1),
            Mathf.Clamp(pos.y, 0, PlotHeight - 1));

        private void CancelZoomDrag()
        {
            if (!_zoomDragging) { return; }
            _zoomDragging = false;
        }

        private void DrawZoomRectangle()
        {
            // The drag coordinates are already clamped to the plotted image.
            var left = Math.Min(_zoomDragStart.x, _zoomDragEnd.x);
            var top = Math.Min(_zoomDragStart.y, _zoomDragEnd.y);
            var width = Math.Abs(_zoomDragEnd.x - _zoomDragStart.x);
            var height = Math.Abs(_zoomDragEnd.y - _zoomDragStart.y);
            var rect = new Rect(_plotPosition.x + left, _plotPosition.y + top, width, height);
            if (Event.current.type != EventType.Repaint || width < 2f || height < 2f) { return; }

            var previousColor = GUI.color;
            GUI.color = new Color(0.25f, 0.8f, 1f, 0.18f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.25f, 0.8f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private PlotRange PlottedRange()
        {
            var (firstDep, longestTof) = _solver.TimesFor(0, 0);
            var (lastDep, shortestTof) = _solver.TimesFor(PlotWidth - 1, PlotHeight - 1);
            return new PlotRange(firstDep, lastDep, shortestTof, longestTof);
        }

        private void ZoomToRectangle(Vector2 start, Vector2 end)
        {
            if (_plotIsUpdating || _errors.Count != 0) { return; }

            // The solver samples [0..width-1] and [0..height-1], not [0..width]
            // and [0..height]. Using these endpoints avoids an off-by-one shift on zoom.
            var old = PlottedRange();
            var left = Math.Min(start.x, end.x) / (PlotWidth - 1);
            var right = Math.Max(start.x, end.x) / (PlotWidth - 1);
            var top = Math.Min(start.y, end.y) / (PlotHeight - 1);
            var bottom = Math.Max(start.y, end.y) / (PlotHeight - 1);

            var next = new PlotRange(
                old.EarliestDeparture + (old.LatestDeparture - old.EarliestDeparture) * left,
                old.EarliestDeparture + (old.LatestDeparture - old.EarliestDeparture) * right,
                old.MaxTimeOfFlight - (old.MaxTimeOfFlight - old.MinTimeOfFlight) * bottom,
                old.MaxTimeOfFlight - (old.MaxTimeOfFlight - old.MinTimeOfFlight) * top);

            _zoomHistory.Push(old);
            ApplyPlotRange(next);
            if (_errors.Count == 0) { GeneratePlots(preserveZoomHistory: true); }
            else
            {
                _zoomHistory.Pop();
                ApplyPlotRange(old);
            }
        }

        private void ZoomOut()
        {
            if (_zoomHistory.Count == 0 || _plotIsUpdating) { return; }
            ApplyPlotRange(_zoomHistory.Pop());
            GeneratePlots(preserveZoomHistory: true);
        }

        private void ResetZoom()
        {
            if (_zoomHistory.Count == 0 || _plotIsUpdating) { return; }
            PlotRange original = default;
            while (_zoomHistory.Count > 0) { original = _zoomHistory.Pop(); }
            ApplyPlotRange(original);
            GeneratePlots(preserveZoomHistory: true);
        }

        private void ApplyPlotRange(PlotRange range)
        {
            // Keep fractional UT precision from the graph rather than rounding to days.
            _earliestDeparture.SetWithTime(range.EarliestDeparture);
            _latestDeparture.SetWithTime(range.LatestDeparture);
            _minTimeOfFlight.Value = range.MinTimeOfFlight / KSPUtil.dateTimeFormatter.Day;
            _maxTimeOfFlight.Value = range.MaxTimeOfFlight / KSPUtil.dateTimeFormatter.Day;
            OnInputChanged();
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
                       GUILayout.Width(DisplayWindowWidth / 3f)))
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
                       GUILayout.Width(DisplayWindowWidth / 3f)))
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
                       GUILayout.Width(DisplayWindowWidth / 3f)))
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

        private void GeneratePlots(bool preserveZoomHistory = false)
        {
            if (_solver.IsRunning)
            {
                Debug.LogError($"Solver is already working!");
                return;
            }

            if (!preserveZoomHistory) { _zoomHistory.Clear(); }
            _plotIsDrawn = false;
            _plotIsUpdating = true;

            _solver.GeneratePorkchop(
                DepartureBody, ArrivalBody,
                _earliestDeparture.Ut, _latestDeparture.Ut,
                _minTimeOfFlight.Value * KSPUtil.dateTimeFormatter.Day,
                _maxTimeOfFlight.Value * KSPUtil.dateTimeFormatter.Day,
                _departureAltitude.Value * 1e3, Deg2Rad(_departureInclination.Value),
                _arrivalPeriapsis.Value * 1e3, _arrivalApoapsis.Value * 1e3);
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
            if (!_solver.TryMarkReady())
            {
                throw new Exception("[TWP2] Failed to mark AsyncJob as ready");
            }
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

        #region UI Utils

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
