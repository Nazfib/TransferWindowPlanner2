using System;
using System.Globalization;
using System.Text.RegularExpressions;
using KSP.Localization;
using UnityEngine;

namespace TransferWindowPlanner2.UI
{
public static class GuiUtils
{
    public struct DoubleInput
    {
        public DoubleInput(double value, double min = double.NegativeInfinity, double max = double.PositiveInfinity, bool minInclusive = true, bool maxInclusive = true)
        {
            _text = value.ToString(CultureInfo.CurrentCulture);
            _value = value;
            Parsed = true;
            Min = min;
            Max = max;
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
        }

        private string _text;
        private double _value;
        public bool Parsed { get; private set; }

        public double Min, Max;
        public bool MinInclusive, MaxInclusive;

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

        public bool Valid => Parsed &&
                             (MinInclusive ? Min <= _value : Min < _value) &&
                             (MaxInclusive ? _value <= Max : _value < Max);
    }

    public struct DateInput
    {
        // Note: printing the time using KSPUtil.PrintDateCompact may construct a new DefaultDateTimeFormatter. I'm
        // not fully sure whether this is safe to do from a class constructor, so just to be safe we initialize the
        // DateInput in an invalid state.
        // For this we rely on the default value of a boolean field, which is false
        public bool Valid { get; private set; }

        private double _ut;
        private string _text;

        // Lazily initialized in TryParseDate
        private static Regex? _stockDateRegex;

        public double Ut
        {
            get => _ut;
            set
            {
                _ut = value;
                _text = KSPUtil.PrintDateCompact(value, includeTime: false)
                               .Trim(); // RSSTimeFormatter includes a trailing space
                Valid = true;
            }
        }

        // Drag-zoom can select fractions of a day. Keep the full UT value for the
        // solver, while displaying an editable hour/minute/second suffix.
        public void SetWithTime(double ut)
        {
            _ut = ut;
            var daySeconds = KSPUtil.dateTimeFormatter.Day;
            var hourSeconds = KSPUtil.dateTimeFormatter.Hour;
            var minuteSeconds = KSPUtil.dateTimeFormatter.Minute;
            var elapsed = ut - Math.Floor(ut / daySeconds) * daySeconds;
            var hours = (int)Math.Floor(elapsed / hourSeconds);
            elapsed -= hours * hourSeconds;
            var minutes = (int)Math.Floor(elapsed / minuteSeconds);
            elapsed -= minutes * minuteSeconds;
            var seconds = (int)Math.Floor(elapsed);

            _text = KSPUtil.PrintDateCompact(ut, includeTime: false).Trim()
                    + $" {hours:00}:{minutes:00}:{seconds:00}";
            Valid = true;
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

        private static bool TryParseDate(string text, out double ut)
        {
            if (_stockDateRegex is null)
            {
                // Stock's default datetime formatter always formats as "Ynn, Dnn" (where the letters Y and D may be
                // localized, and nn are integers). This accepts a slightly more free format, namely that the comma and
                // space between the year and day are optional. Leading and trialing whitespace is not accepted, though.
                var year = Localizer.Format("#autoLOC_6002344");
                var day = Localizer.Format("#autoLOC_6002345");
                _stockDateRegex = new Regex(
                    $@"^{Regex.Escape(year)}(\d+),?\s*{Regex.Escape(day)}(\d+)(?:\s+(\d{{1,2}}):(\d{{2}})(?::(\d{{2}}))?)?$",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            var match = _stockDateRegex.Match(text);
            if (match.Success)
            {
                var year = int.Parse(match.Groups[1].Value);
                var day = int.Parse(match.Groups[2].Value);
                if (year == 0 || day == 0)
                {
                    // Matches the regex, but it is not a valid date. We can early-exit, since this can never be a valid
                    // date for the RSS time format either.
                    ut = 0.0;
                    return false;
                }
                ut = (year - 1) * KSPUtil.dateTimeFormatter.Year + (day - 1) * KSPUtil.dateTimeFormatter.Day;

                if (match.Groups[3].Success)
                {
                    var hour = int.Parse(match.Groups[3].Value);
                    var minute = int.Parse(match.Groups[4].Value);
                    var second = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
                    if (hour >= KSPUtil.dateTimeFormatter.Day / KSPUtil.dateTimeFormatter.Hour ||
                        minute >= 60 || second >= 60)
                    {
                        ut = 0.0;
                        return false;
                    }
                    ut += hour * KSPUtil.dateTimeFormatter.Hour +
                          minute * KSPUtil.dateTimeFormatter.Minute + second;
                }
                return true;
            }

            // RSSTimeFormatter: printed as yyyy-MM-dd
            // When parsing, allow omitting the leading zero in the day and month values.
            if (DateTime.TryParseExact(
                    text, new[] { "yyyy-M-d", "yyyy-M-d H:mm", "yyyy-M-d H:mm:ss" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var result))
            {
                var epoch = new DateTime(1951, 1, 1);
                ut = (result - epoch).TotalSeconds;
                return true;
            }

            ut = 0.0;
            return false;
        }
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

    public static void InitStyles()
    {
        BoxStyle = new GUIStyle(HighLogic.Skin.box) { alignment = TextAnchor.UpperLeft };
        BoxTitleStyle = new GUIStyle(HighLogic.Skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        PlotBoxStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter };
        TooltipStyle = new GUIStyle(HighLogic.Skin.window)
        {
            alignment = TextAnchor.UpperLeft,
            fontStyle = FontStyle.Normal,
            wordWrap = false,
            padding = { bottom = -10, top = 28, left = 8, right = 6 },
        };
        InputStyle = new GUIStyle(HighLogic.Skin.textField)
            { alignment = TextAnchor.MiddleRight };
        InvalidInputStyle = new GUIStyle(InputStyle)
            { normal = { textColor = XKCDColors.KSPNotSoGoodOrange } };
        ButtonStyle = new GUIStyle(HighLogic.Skin.button);
        InvalidButtonStyle = new GUIStyle(ButtonStyle)
            { normal = { textColor = XKCDColors.KSPNotSoGoodOrange } };
        ResultLabelStyle = new GUIStyle(HighLogic.Skin.label)
            { alignment = TextAnchor.MiddleLeft };
        ResultValueStyle = new GUIStyle(HighLogic.Skin.label)
            { alignment = TextAnchor.MiddleRight };
    }

    public static GUIStyle? BoxStyle;
    public static GUIStyle? BoxTitleStyle;
    public static GUIStyle? PlotBoxStyle;
    public static GUIStyle? TooltipStyle;
    public static GUIStyle? InputStyle;
    public static GUIStyle? InvalidInputStyle;
    public static GUIStyle? ButtonStyle;
    public static GUIStyle? InvalidButtonStyle;
    public static GUIStyle? ResultLabelStyle;
    public static GUIStyle? ResultValueStyle;
}
}
