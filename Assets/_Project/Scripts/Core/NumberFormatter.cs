using System;
using System.Globalization;

namespace VoidProspector.Core
{
    /// <summary>
    /// Short display form for game numbers: 7.5, 999, 1.23K, 45.6K, 789K, 4.56M, 7.89B … 1.23Dc, then 1.23e36.
    /// Values are truncated, never rounded up, so the display never shows more than the player has.
    /// </summary>
    public static class NumberFormatter
    {
        static readonly string[] Suffixes = { "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const double Epsilon = 1e-9;

        public static string Format(GameNumber number) => Format(number.Value);

        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
            if (v < 0) return "-" + Format(-v);

            if (v < 10)
            {
                double t = Truncate(v, 1);
                return t.ToString("0.#", Inv);
            }
            if (v < 1000) return Math.Floor(v + Epsilon).ToString("0", Inv);

            int group = (int)Math.Floor(Math.Log10(v) / 3 + Epsilon);   // 1 = K, 2 = M, ...
            if (group > Suffixes.Length) return Scientific(v);

            double mantissa = v / Math.Pow(1000, group);
            // Guard against floating error putting the mantissa at 1000 or just under 1.
            if (mantissa >= 1000 - Epsilon) { mantissa /= 1000; group++; }
            if (group > Suffixes.Length) return Scientific(v);

            return Mantissa(mantissa) + Suffixes[group - 1];
        }

        static string Mantissa(double m)
        {
            if (m < 10) return Truncate(m, 2).ToString("0.00", Inv);
            if (m < 100) return Truncate(m, 1).ToString("0.0", Inv);
            return Math.Floor(m + Epsilon).ToString("0", Inv);
        }

        static string Scientific(double v)
        {
            int exp = (int)Math.Floor(Math.Log10(v) + Epsilon);
            double m = v / Math.Pow(10, exp);
            if (m >= 10 - Epsilon) { m /= 10; exp++; }
            return Truncate(m, 2).ToString("0.00", Inv) + "e" + exp.ToString(Inv);
        }

        static double Truncate(double v, int decimals)
        {
            double scale = Math.Pow(10, decimals);
            return Math.Floor(v * scale + Epsilon) / scale;
        }
    }
}
