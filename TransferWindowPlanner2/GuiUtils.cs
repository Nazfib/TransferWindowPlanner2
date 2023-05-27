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
                else { Valid = false; }
            }
        }

        public bool Valid;
        public double Value;
    }

    public struct DateInput
    {
        public bool Valid;

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

    internal static bool CurrentSceneHasMapView() =>
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
}
}
