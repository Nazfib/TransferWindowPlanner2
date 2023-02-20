using System;
using System.Globalization;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using KSP.Localization;
using KSP.UI.Screens;
using UnityEngine;

namespace TransferWindowPlanner2;

[KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
public class MainWindow : MonoBehaviour
{
    private const string ModName = "TransferWindowPlanner2";
    private const string Icon = "TransferWindowPlanner2/icon";
    private const int PlotWidth = 500;
    private const int PlotHeight = 400;
    private const int WindowWidth = 750;
    private const int WindowHeight = 600;

    private Texture2D _plotArrival = new(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private Texture2D _plotDeparture = new(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);
    private Texture2D _plotTotal = new(PlotWidth, PlotHeight, TextureFormat.ARGB32, false);

    private GUIStyle _boxStyle,
        _boxTitleStyle,
        _plotBoxStyle,
        _inputStyle,
        _invalidInputStyle,
        _buttonStyle,
        _invalidButtonStyle;

    private ApplicationLauncherButton _button;

    private enum PlotType
    {
        Departure = 0,
        Arrival = 1,
        Total = 2
    }

    private PlotType _selectedPlot = PlotType.Total;

    private bool _showMainWindow;
    private Rect _winPos = new(450, 100, WindowWidth, WindowHeight);
    private bool _showDepartureCbWindow;
    private Rect _departureCbWinPos = new(200, 200, 200, 200);
    private bool _showArrivalCbWindow;
    private Rect _arrivalCbWinPos = new(300, 200, 200, 200);

    // Input fields
    private CelestialBody _departureCb;
    private CelestialBody _arrivalCb;
    private DoubleInput _departureAltitude = new(185.0);
    private DoubleInput _departureInclination = new(28.5);
    private DoubleInput _arrivalAltitude = new(350.0);
    private bool _minimalCapture = true;

    private DateInput _earliestDeparture = new(0.0);
    private DateInput _latestDeparture = new(0.0);
    private DateInput _earliestArrival = new(0.0);
    private DateInput _latestArrival = new(0.0);

    protected void Awake()
    {
        GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
    }

    protected void Start()
    {
        _boxStyle = new GUIStyle(HighLogic.Skin.box) { alignment = TextAnchor.UpperLeft };
        _inputStyle = new GUIStyle(HighLogic.Skin.textField) { alignment = TextAnchor.MiddleRight };
        _invalidInputStyle = new GUIStyle(_inputStyle)
        {
            normal = { textColor = Color.red }
        };
        _boxTitleStyle = new GUIStyle(HighLogic.Skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        _plotBoxStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter };
        _buttonStyle = new GUIStyle(HighLogic.Skin.button);
        _invalidButtonStyle = new GUIStyle(_buttonStyle) { normal = { textColor = Color.red } };

        _departureCb = FlightGlobals.GetHomeBody();
        _arrivalCb = FlightGlobals.Bodies.Find(cb => ValidCbCombination(_departureCb, cb));
        if (_arrivalCb == null) _arrivalCb = _departureCb; // Let the user worry about it.
    }

    public void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
        if (_button != null) ApplicationLauncher.Instance.RemoveModApplication(_button);
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (_showMainWindow) _winPos = GUILayout.Window(GetHashCode(), _winPos, WindowGUI, ModName);
        if (_showDepartureCbWindow)
            _departureCbWinPos =
                GUILayout.Window(GetHashCode() + 1, _departureCbWinPos, DepartureCbWindow, "Origin body");
        if (_showArrivalCbWindow)
            _arrivalCbWinPos =
                GUILayout.Window(GetHashCode() + 2, _arrivalCbWinPos, ArrivalCbWindow, "Destination body");
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
        GUILayout.BeginVertical();
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

        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void ArrivalCbWindow(int id)
    {
        var cb = ShowCbSelection(_departureCb);
        if (cb == null) return;
        _arrivalCb = cb;
        _showArrivalCbWindow = false;
        GUI.DragWindow();
    }

    private void DepartureCbWindow(int id)
    {
        var cb = ShowCbSelection(_arrivalCb);
        if (cb == null) return;
        _departureCb = cb;
        _showDepartureCbWindow = false;
        GUI.DragWindow();
    }

    private CelestialBody ShowCbSelection(CelestialBody other)
    {
        GUILayout.BeginVertical();
        foreach (var cb in FlightGlobals.Bodies)
        {
            if (cb.isStar)
                continue;
            if (GUILayout.Button(
                    cb.displayName.LocalizeRemoveGender(),
                    ValidCbCombination(other, cb) ? _buttonStyle : _invalidButtonStyle)
               )
                return cb;
        }

        GUILayout.EndVertical();
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
               _latestArrival.Valid;
    }

    private void ShowInputs()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(false), GUILayout.Width(WindowWidth - PlotWidth));

        GUILayout.FlexibleSpace();

        using (new GUILayout.VerticalScope(_boxStyle))
        {
            GUILayout.Label("Origin", _boxTitleStyle);
            _showDepartureCbWindow = GUILayout.Toggle(_showDepartureCbWindow,
                _departureCb.displayName.LocalizeRemoveGender(),
                ValidCbCombination(_departureCb, _arrivalCb) ? _buttonStyle : _invalidButtonStyle);
            LabeledDoubleInput("Altitude", ref _departureAltitude, "km");
            LabeledDoubleInput("Min. Inclination", ref _departureInclination, "°");
            LabeledDateInput("Earliest", ref _earliestDeparture);
            LabeledDateInput("Latest", ref _latestDeparture);
        }

        GUILayout.FlexibleSpace();

        using (new GUILayout.VerticalScope(_boxStyle))
        {
            GUILayout.Label("Destination", _boxTitleStyle);
            _showArrivalCbWindow = GUILayout.Toggle(_showArrivalCbWindow,
                _arrivalCb.displayName.LocalizeRemoveGender(),
                ValidCbCombination(_departureCb, _arrivalCb) ? _buttonStyle : _invalidButtonStyle);
            LabeledDoubleInput("Altitude", ref _arrivalAltitude, "km");
            _minimalCapture = GUILayout.Toggle(_minimalCapture, "Minimal capture");
            LabeledDateInput("Earliest", ref _earliestArrival);
            LabeledDateInput("Latest", ref _latestArrival);
        }

        GUILayout.FlexibleSpace();

        GUI.enabled = ValidInputs();
        if (GUILayout.Button("Plot it!"))
        {
            // TODO: start the porkchop calculation
        }

        GUI.enabled = true;

        GUILayout.FlexibleSpace();

        GUILayout.EndVertical();
    }

    private void ShowPlot()
    {
        using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(false), GUILayout.Width(PlotWidth)))
        {
            _selectedPlot = (PlotType)GUILayout.SelectionGrid((int)_selectedPlot, new[]
            {
                "Departure", "Arrival", "Total"
            }, 3);

            GUILayout.Box(_selectedPlot switch
                {
                    PlotType.Departure => _plotDeparture,
                    PlotType.Arrival => _plotArrival,
                    PlotType.Total => _plotTotal,
                    _ => _plotTotal
                },
                _plotBoxStyle,
                GUILayout.Width(PlotWidth), GUILayout.Height(PlotHeight),
                GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        }
    }

    private void ShowDepartureInfo()
    {
        using (new GUILayout.VerticalScope(
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            // TODO: calculate actual values for most of these fields; fiddle with the formatting
            GUILayout.Label("Departure", _boxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_earliestDeparture.Ut, true));
            LabeledInfo("Periapsis altitude", $"{_departureAltitude.Value:N0} km");
            LabeledInfo("Inclination", $"{_departureInclination.Value:N2} °");
            LabeledInfo("LAN", $"{243.2:N2} °");
            LabeledInfo("Asymptote right ascension", $"{80.10:N2} °");
            LabeledInfo("Asymptote declination", $"{15.0:N2} °");
            LabeledInfo("C3", $"{12.30:N2} km²/s²");
            LabeledInfo("Δv", $"{3851.6:N0} m/s");
        }
    }

    private void ShowArrivalInfo()
    {
        using (new GUILayout.VerticalScope(_boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            // TODO: calculate actual values for most of these fields; fiddle with the formatting
            GUILayout.Label("Arrival", _boxTitleStyle);
            LabeledInfo("Date", KSPUtil.PrintDateNew(_latestArrival.Ut, true));
            LabeledInfo("Periapsis altitude", $"{_arrivalAltitude.Value:N0} km");
            LabeledInfo("Inclination", $"{32.9:N2} °");
            LabeledInfo("LAN", $"{10.2:N2} °");
            LabeledInfo("Asymptote right ascension", $"{80.1:N2} °");
            LabeledInfo("Asymptote declination", $"{15.0:N2} °");
            LabeledInfo("C3", $"{12.3:N2} km²/s²");
            LabeledInfo("Δv", $"{1183.2:N0} m/s");
        }
    }

    private void ShowTransferInfo()
    {
        using (new GUILayout.VerticalScope(
                   _boxStyle,
                   GUILayout.ExpandWidth(false),
                   GUILayout.Width(WindowWidth / 3f)))
        {
            GUILayout.Label("Transfer", _boxTitleStyle);
            LabeledInfo("Flight time", KSPUtil.PrintDateDelta(_latestArrival.Ut - _earliestDeparture.Ut, false));
            LabeledInfo("Total Δv", $"{3851.6 + 1183.2:N0} m/s");
        }
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

    private void LabeledDoubleInput(string label, ref DoubleInput input, [CanBeNull] string unit)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label);
        GUILayout.FlexibleSpace();
        input.Text = GUILayout.TextField(
            input.Text,
            input.Valid ? _inputStyle : _invalidInputStyle,
            GUILayout.Width(100));
        if (!string.IsNullOrEmpty(unit)) GUILayout.Label(unit, GUILayout.ExpandWidth(false));

        GUILayout.EndHorizontal();
    }

    private void LabeledDateInput(string label, ref DateInput input)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(label);
        GUILayout.FlexibleSpace();

        input.Text = GUILayout.TextField(
            input.Text,
            input.Valid ? _inputStyle : _invalidInputStyle,
            GUILayout.Width(100));

        GUILayout.EndHorizontal();
    }
}

internal struct DoubleInput
{
    public DoubleInput(double value)
    {
        _text = value.ToString(CultureInfo.CurrentCulture);
        Valid = true;
        Value = value;
    }

    private string _text;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var result))
            {
                Valid = true;
                Value = result;
            }
            else
            {
                Valid = false;
            }
        }
    }

    public bool Valid;
    public double Value;
}

internal struct DateInput
{
    public bool Valid;
    public double Ut;

    private string _text;

    private readonly string _stockDateRegex;

    public DateInput(double ut)
    {
        _text = KSPUtil.PrintDateCompact(ut, false);
        Valid = true;
        Ut = ut;

        _stockDateRegex = // Ynn, Dnn
            Localizer.Format("#autoLOC_6002344") + @"(\d+), " +
            Localizer.Format("#autoLOC_6002345") + @"(\d+)";
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            if (TryParseDate(value, out var result))
            {
                Valid = true;
                Ut = result;
            }
            else
            {
                Valid = false;
            }
        }
    }

    private bool TryParseDate(string text, out double ut)
    {
        // Stock format: Ynn, Dnn

        var match = Regex.Match(text, _stockDateRegex, RegexOptions.Compiled);
        if (match.Success)
        {
            var year = int.Parse(match.Groups[1].Value);
            var day = int.Parse(match.Groups[2].Value);
            ut = (year - 1) * KSPUtil.dateTimeFormatter.Year + (day - 1) * KSPUtil.dateTimeFormatter.Day;
            return true;
        }

        // TODO: test this with RSS and RSSTimeFormatter
        // RSSTimeFormatter: yyyy-MM-dd
        if (DateTime.TryParseExact(
                text, "yyyy-MM-dd",
                CultureInfo.CurrentCulture, // Is this correct? Probably...
                DateTimeStyles.None,
                out var result))
        {
            var epoch = new DateTime(1951, 1, 1);
            ut = (result - epoch).TotalSeconds;
            return true;
        }

        ut = 0.0;
        return false;
    }
}
