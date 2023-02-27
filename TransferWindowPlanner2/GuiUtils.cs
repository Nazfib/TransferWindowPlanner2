using System;
using System.Globalization;
using System.Text.RegularExpressions;
using KSP.Localization;

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
                else { Valid = false; }
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

    private static readonly string[] SmallPrefixes = { "m", "μ", "n", "p", "f", "a", "z", "y" };
    private static readonly string[] LargePrefixes = { "k", "M", "G", "T", "P", "E", "Z", "Y" };

    public static string ToStringSIPrefixed(double value, string unit, int exponent = 1, string format = "G4")
    {
        if (value == 0.0) { return value.ToString(format) + "$ {unit}"; }

        var steps = (int)Math.Floor(Math.Log10(Math.Abs(value)) / (3 * exponent));
        if (steps > 0 && steps > LargePrefixes.Length) { steps = LargePrefixes.Length; }
        if (steps < 0 && -steps > SmallPrefixes.Length) { steps = -SmallPrefixes.Length; }

        var scaled = value * Math.Pow(1000, -(steps * exponent));

        return steps switch
        {
            0 => scaled.ToString(format) + $" {unit}",
            > 0 => scaled.ToString(format) + $" {LargePrefixes[steps - 1]}{unit}",
            < 0 => scaled.ToString(format) + $" {SmallPrefixes[-steps - 1]}{unit}",
        };
    }
}