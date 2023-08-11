using System;
using System.Globalization;
using System.Text.RegularExpressions;
using KSP.Localization;
using UnityEngine;

namespace TransferWindowPlanner2
{
public static class GuiUtils
{
    public struct DoubleInput
    {
        public DoubleInput(double value, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            _text = value.ToString(CultureInfo.CurrentCulture);
            _value = value;
            Parsed = true;
            Min = min;
            Max = max;
        }

        private string _text;
        private double _value;
        public bool Parsed { get; private set; }

        public double Min, Max;

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var result))
                {
                    Parsed = true;
                    _value = result;
                }
                else { Parsed = false; }
            }
        }


        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                _text = value.ToString(CultureInfo.CurrentCulture);
                Parsed = true;
            }
        }

        public bool Valid => Parsed && Min <= _value && _value <= Max;
    }

    public struct DateInput
    {
        public bool Valid { get; private set; }

        private double _ut;
        private string _text;

        private readonly string _stockDateRegex;

        public DateInput(double ut)
        {
            _ut = ut;
            _text = KSPUtil.PrintDateCompact(ut, false);
            Valid = true;

            // Ynn, Dnn
            var year = Localizer.Format("#autoLOC_6002344");
            var day = Localizer.Format("#autoLOC_6002345");
            _stockDateRegex = $@"^{year}(\d+),?\s*{day}(\d+)$";
        }

        public double Ut
        {
            get => _ut;
            set
            {
                _ut = value;
                _text = KSPUtil.PrintDateCompact(value, false);
                Valid = true;
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                if (TryParseDate(value, out var result))
                {
                    _ut = result;
                    Valid = true;
                }
                else { Valid = false; }
            }
        }

        private bool TryParseDate(string text, out double ut)
        {
            // Stock format: Ynn, Dnn
            var match = Regex.Match(text, _stockDateRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var year = int.Parse(match.Groups[1].Value);
                var day = int.Parse(match.Groups[2].Value);
                ut = (year - 1) * KSPUtil.dateTimeFormatter.Year + (day - 1) * KSPUtil.dateTimeFormatter.Day;
                return true;
            }

            // RSSTimeFormatter: yyyy-MM-dd
            // This is a valid date format string for all cultures.
            if (DateTime.TryParse(text, out var result))
            {
                var epoch = new DateTime(1951, 1, 1);
                ut = (result - epoch).TotalSeconds;
                return true;
            }

            ut = 0.0;
            return false;
        }
    }

    public static bool CurrentSceneHasMapView() =>
        HighLogic.LoadedScene is GameScenes.FLIGHT
        || HighLogic.LoadedScene is GameScenes.TRACKSTATION;

    public readonly struct GuiEnabled : IDisposable
    {
        private readonly bool _prev;

        public GuiEnabled(bool enabled)
        {
            _prev = GUI.enabled;
            GUI.enabled = enabled;
        }

        public void Dispose()
        {
            GUI.enabled = _prev;
        }
    }

    public static readonly Color Orange = new Color(1.0f, 0.65f, 0.0f);

    public static readonly GUIStyle BoxStyle = new GUIStyle(HighLogic.Skin.box) { alignment = TextAnchor.UpperLeft };

    public static readonly GUIStyle BoxTitleStyle = new GUIStyle(HighLogic.Skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = FontStyle.Bold,
    };

    public static readonly GUIStyle PlotBoxStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter };

    public static readonly GUIStyle PlotTooltipStyle = new GUIStyle(HighLogic.Skin.box)
    {
        alignment = TextAnchor.MiddleLeft,
        fontStyle = FontStyle.Normal,
    };

    public static readonly GUIStyle InputStyle = new GUIStyle(HighLogic.Skin.textField)
        { alignment = TextAnchor.MiddleRight };

    public static readonly GUIStyle InvalidInputStyle = new GUIStyle(InputStyle)
        { normal = { textColor = Orange } };

    public static readonly GUIStyle ButtonStyle = new GUIStyle(HighLogic.Skin.button);

    public static readonly GUIStyle InvalidButtonStyle = new GUIStyle(ButtonStyle)
        { normal = { textColor = Orange } };

    public static readonly GUIStyle ResultLabelStyle = new GUIStyle(HighLogic.Skin.label)
        { alignment = TextAnchor.MiddleLeft };

    public static readonly GUIStyle ResultValueStyle = new GUIStyle(HighLogic.Skin.label)
        { alignment = TextAnchor.MiddleRight };
}
}
