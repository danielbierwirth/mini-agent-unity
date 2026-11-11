using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VoiceAgent
{
    /// <summary>
    /// Spells numbers out as Arabic words for speech. Supertonic reads digits (Western or Arabic-Indic) poorly or not at
    /// all in Arabic, so a reply such as "درجة الحرارة 22.5°" or "الساعة الآن 3:05 مساءً" has to reach it as words.
    /// Handles integers, decimals, negatives, thousands separators, clock times, and the %, °/°C and كم/س units the
    /// replies use. Uses the masculine (default) forms; gender agreement with the counted noun is not attempted.
    /// </summary>
    public static class ArabicNumbers
    {
        static readonly string[] Ones =
        {
            "صفر", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
            "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر",
        };
        static readonly string[] Tens = { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
        static readonly string[] Hundreds = { "", "مئة", "مئتان", "ثلاثمئة", "أربعمئة", "خمسمئة", "ستمئة", "سبعمئة", "ثمانمئة", "تسعمئة" };

        // (one, two, plural 3–10, singular 11+) for each power of a thousand.
        static readonly (long Scale, string One, string Two, string Plural)[] Scales =
        {
            (1_000_000_000_000, "تريليون", "تريليونان", "تريليونات"),
            (1_000_000_000, "مليار", "ملياران", "مليارات"),
            (1_000_000, "مليون", "مليونان", "ملايين"),
            (1_000, "ألف", "ألفان", "آلاف"),
        };

        /// <summary>A whole number in words, e.g. 2026 → "ألفان وستة وعشرون".</summary>
        public static string ToWords(long n)
        {
            if (n < 0) return "سالب " + ToWords(-n);
            if (n == 0) return Ones[0];
            var parts = new List<string>();
            foreach (var (scale, one, two, plural) in Scales)
            {
                var count = n / scale;
                if (count == 0) continue;
                n %= scale;
                parts.Add(count switch
                {
                    1 => one,
                    2 => two,
                    <= 10 => Below1000(count) + " " + plural,
                    _ => Below1000(count) + " " + one,
                });
            }
            if (n > 0) parts.Add(Below1000(n));
            return string.Join(" و", parts);
        }

        static string Below1000(long n)
        {
            var parts = new List<string>();
            if (n >= 100) parts.Add(Hundreds[n / 100]);
            n %= 100;
            if (n >= 20)
            {
                // Units come before tens: 21 → واحد وعشرون.
                if (n % 10 > 0) parts.Add(Ones[n % 10]);
                parts.Add(Tens[n / 10]);
            }
            else if (n > 0) parts.Add(Ones[n]);
            return string.Join(" و", parts);
        }

        // A clock time such as 3:05 or 14:30 (not part of a longer number like a ratio 1:2:3).
        static readonly Regex Time = new(@"(?<![\d:])(\d{1,2}):(\d{2})(?![\d:])", RegexOptions.Compiled);
        // 1,234,567 → 1234567 (Western or Arabic thousands separator).
        static readonly Regex Grouped = new(@"(?<!\d)\d{1,3}(?:[,٬]\d{3})+(?![\d])", RegexOptions.Compiled);
        // A number with an optional sign, decimal part and unit.
        static readonly Regex Number = new(
            @"(?<sign>(?<![\w.])[-−])?(?<int>\d+)(?:[.٫](?<frac>\d+))?\s*(?<unit>°\s*C|°|%|٪|كم/س)?",
            RegexOptions.Compiled);

        /// <summary>The text with every number (and its unit) replaced by Arabic words, ready for Arabic speech.</summary>
        public static string Speakable(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var s = ToWesternDigits(text);
            s = Grouped.Replace(s, m => m.Value.Replace(",", "").Replace("٬", ""));
            s = Time.Replace(s, m => " " + TimeWords(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) + " ");
            s = Number.Replace(s, m => " " + NumberWords(m) + " ");
            s = Regex.Replace(s, @" {2,}", " ");
            return Regex.Replace(Regex.Replace(s, @" (?=[.,،؟?!:;)\]])", ""), @"(?<=[(\[]) ", "").Trim();
        }

        static string NumberWords(Match m)
        {
            var digits = m.Groups["int"].Value;
            // Too long to be a quantity (a phone or order number): read digit by digit.
            var words = digits.Length > 15
                ? string.Join(" ", Digits(digits))
                : ToWords(long.Parse(digits, CultureInfo.InvariantCulture));
            if (m.Groups["frac"].Success && m.Groups["frac"].Value.Trim('0').Length > 0) words += " فاصلة " + Fraction(m.Groups["frac"].Value);
            if (m.Groups["sign"].Success) words = "سالب " + words;
            return m.Groups["unit"].Value.Replace(" ", "") switch
            {
                "°C" => words + " درجة مئوية",
                "°" => words + " درجة",
                "%" or "٪" => words + " بالمئة",
                "كم/س" => words + " كيلومتر في الساعة",
                _ => words,
            };
        }

        // 5 → خمسة, 05 → صفر خمسة, 25 → خمسة وعشرون: leading zeros are read, the rest as one number.
        static string Fraction(string frac)
        {
            var leadingZeros = frac.Length - frac.TrimStart('0').Length;
            var rest = frac.Substring(leadingZeros);
            var parts = new List<string>();
            for (var i = 0; i < leadingZeros; i++) parts.Add(Ones[0]);
            if (rest.Length > 0) parts.Add(rest.Length > 15 ? string.Join(" ", Digits(rest)) : ToWords(long.Parse(rest, CultureInfo.InvariantCulture)));
            return string.Join(" ", parts);
        }

        static IEnumerable<string> Digits(string digits)
        {
            foreach (var c in digits) yield return Ones[c - '0'];
        }

        // Clock hours are read as feminine ordinals (الساعة الثالثة); 13–23 (a 24-hour time) as plain numbers.
        static readonly string[] HourOrdinals =
        {
            "الثانية عشرة", "الواحدة", "الثانية", "الثالثة", "الرابعة", "الخامسة", "السادسة", "السابعة", "الثامنة",
            "التاسعة", "العاشرة", "الحادية عشرة", "الثانية عشرة",
        };
        // Feminine counts for 3–10 minutes (ثلاث دقائق … عشر دقائق).
        static readonly string[] FeminineCounts = { "", "", "", "ثلاث", "أربع", "خمس", "ست", "سبع", "ثماني", "تسع", "عشر" };

        static string TimeWords(int hour, int minute)
        {
            var h = hour <= 12 ? HourOrdinals[hour] : ToWords(hour);
            return minute switch
            {
                0 => h,
                1 => h + " ودقيقة",
                2 => h + " ودقيقتان",
                <= 10 => h + " و" + FeminineCounts[minute] + " دقائق",
                15 => h + " والربع",
                30 => h + " والنصف",
                _ => h + " و" + ToWords(minute) + " دقيقة",
            };
        }

        /// <summary>Arabic-Indic (٠–٩) and Persian (۰–۹) digits → 0–9, so both kinds of number are spelled out.</summary>
        static string ToWesternDigits(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
                sb.Append(c switch
                {
                    >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                    >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                    _ => c,
                });
            return sb.ToString();
        }
    }
}
