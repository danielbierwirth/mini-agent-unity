using System.Text;

namespace VoiceAgent
{
    /// <summary>Arabic script helpers: detection, a matching form for keyword rules, and digit conversion.</summary>
    public static class ArabicText
    {
        /// <summary>Arabic letters (base block, extended blocks and presentation forms), not Arabic digits or punctuation.</summary>
        public static bool IsArabicLetter(char c) =>
            (c >= 'ؠ' && c <= 'ي') || (c >= 'ٮ' && c <= 'ۓ') || (c >= 'ۺ' && c <= 'ۿ') ||
            (c >= 'ݐ' && c <= 'ݿ') || (c >= 'ࢠ' && c <= 'ࣿ') ||
            (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= 'ﻼ');

        public static bool ContainsArabic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text)
                if (IsArabicLetter(c)) return true;
            return false;
        }

        /// <summary>Share of letters that are Arabic, 0–1.</summary>
        public static float ArabicShare(string text)
        {
            int arabic = 0, letters = 0;
            foreach (var c in text ?? string.Empty)
            {
                if (!char.IsLetter(c)) continue;
                letters++;
                if (IsArabicLetter(c)) arabic++;
            }
            return letters == 0 ? 0f : arabic / (float)letters;
        }

        /// <summary>
        /// The form the keyword rules match: no diacritics (tashkeel) or tatweel, one alef (أ إ آ ٱ → ا), hamza carriers
        /// to their letter (ؤ → و, ئ → ي) and the lone hamza dropped, ى → ي, ة → ه, Arabic-Indic digits → 0–9,
        /// Arabic punctuation → ASCII, Latin lowercased. "أطفئ أضواءَ المطبخ" → "اطفي اضوا المطبخ".
        /// </summary>
        public static string Normalize(string text)
        {
            var sb = new StringBuilder((text ?? string.Empty).Length);
            foreach (var c in text ?? string.Empty)
            {
                switch (c)
                {
                    case >= 'ً' and <= 'ٟ':
                    case 'ٰ':
                    case 'ـ':
                    case 'ء':
                        continue;
                    case 'أ' or 'إ' or 'آ' or 'ٱ': sb.Append('ا'); continue;
                    case 'ؤ': sb.Append('و'); continue;
                    case 'ئ' or 'ى': sb.Append('ي'); continue;
                    case 'ة': sb.Append('ه'); continue;
                    case >= '٠' and <= '٩': sb.Append((char)('0' + (c - '٠'))); continue;
                    case >= '۰' and <= '۹': sb.Append((char)('0' + (c - '۰'))); continue;
                    case '٪': sb.Append('%'); continue;
                    case '،': sb.Append(','); continue;
                    case '؟': sb.Append('?'); continue;
                    case '؛': sb.Append(';'); continue;
                    default: sb.Append(char.ToLowerInvariant(c)); continue;
                }
            }
            return sb.ToString();
        }

        /// <summary>0–9 → ٠–٩ and % → ٪, for text Supertonic reads in Arabic, so numbers are read as Arabic numbers rather than English ones.</summary>
        public static string ToArabicDigits(string text)
        {
            var sb = new StringBuilder((text ?? string.Empty).Length);
            foreach (var c in text ?? string.Empty)
                sb.Append(c switch
                {
                    >= '0' and <= '9' => (char)('٠' + (c - '0')),
                    '%' => '٪',
                    _ => c,
                });
            return sb.ToString();
        }
    }
}
