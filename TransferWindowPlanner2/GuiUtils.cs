using System;
using System.Globalization;
using System.Text.RegularExpressions;
using KSP.Localization;
using UnityEngine;

namespace TransferWindowPlanner2;

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

    private static readonly string[] SmallPrefixes = { "m", "μ", "n", "p", "f", "a", "z", "y" };
    private static readonly string[] LargePrefixes = { "k", "M", "G", "T", "P", "E", "Z", "Y" };

    public static string ToStringSIPrefixed(double value, string unit, int exponent = 1, int sigFigs = 3)
    {
        if (value == 0.0 || !double.IsFinite(value)) { return value.ToString("N3") + $" {unit}"; }

        var order = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var steps = order < 0 ? (order - 2) / (3 * exponent) : order / (3 * exponent);

        if (steps > 0 && steps > LargePrefixes.Length) { steps = LargePrefixes.Length; }
        if (steps < 0 && -steps > SmallPrefixes.Length) { steps = -SmallPrefixes.Length; }

        var scaled = value * Math.Pow(1000, -(steps * exponent));

        var integerDigits = order - steps * 3 * exponent;
        var fracDigits = Math.Max(0, sigFigs - integerDigits - 1);

        return steps switch
        {
            0 => scaled.ToString($"N{fracDigits}") + $" {unit}",
            > 0 => scaled.ToString($"N{fracDigits}") + $" {LargePrefixes[steps - 1]}{unit}",
            < 0 => scaled.ToString($"N{fracDigits}") + $" {SmallPrefixes[-steps - 1]}{unit}",
        };
    }

    internal static bool CurrentSceneHasMapView()
    {
        return HighLogic.LoadedScene is GameScenes.FLIGHT or GameScenes.TRACKSTATION;
    }

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
