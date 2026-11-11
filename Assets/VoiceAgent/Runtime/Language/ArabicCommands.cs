using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace VoiceAgent
{
    /// <summary>
    /// Arabic home commands → the canonical English command the on-device models were trained on
    /// ("أطفئ أضواء المطبخ" → "turn off the kitchen light"). Neither FunctionGemma (MiniAgentScene) nor the Decision AI
    /// head (VoiceAgentDemo) saw Arabic in training, so an Arabic command is rewritten before it reaches them; the tool
    /// names, arguments and device IDs therefore stay English, and Arabic, Korean and English device names all end up on
    /// the same IDs. Only home control and the info intents are rewritten: anything else returns null and goes to the
    /// chat model as typed, which replies in Arabic.
    ///
    /// Pure C#, keyword rules on <see cref="ArabicText.Normalize"/>d text. A word matches a cue when the cue starts the
    /// word, with or without the attached prefixes و ف ب ل ال (Arabic writes "and", "the", "in/with" onto the word), so
    /// "والتلفاز" and "بالمطبخ" match too. Compound commands split on و / ثم before a verb.
    /// </summary>
    public static class ArabicCommands
    {
        /// <summary>How a web search rewrite starts ("search the web for لبنان").</summary>
        public const string SearchPrefix = "search the web for";

        // Dialect verbs alongside the MSA ones: ولّع/شعّل/ضوّي (Levantine, Gulf, Maghrebi "light, switch on"), حطّ (Levantine,
        // Gulf "put on": music, the TV), اسمع ("I want to listen to": بدي/عايز/ابغى اسمع موسيقى), بطّل (Levantine, Egyptian
        // "stop"), سكّت ("silence"), هدّي/وطّي/واطي ("lower"). علّي ("raise") is handled next to a volume word only (see Clause).
        static readonly string[] OnVerbs = { "شغل", "تشغيل", "اشعل", "شعل", "ولع", "افتح", "فتح", "اضي", "انر", "نور", "ابدا", "شغلي", "دور", "ادر", "اعزف", "ضوي", "حط", "اسمع" };
        static readonly string[] OffVerbs = { "اطفي", "اطف", "طفي", "اطفا", "اغلق", "اغلاق", "سكر", "قفل", "اقفل", "اوقف", "وقف", "ايقاف", "اسكت", "اوقفي", "طفيه", "بطل", "سكت" };
        static readonly string[] UpVerbs = { "ارفع", "رفع", "زد", "زيد", "زود", "كبر" };
        static readonly string[] DownVerbs = { "اخفض", "خفض", "قلل", "نزل", "وطي", "واطي", "خفف", "صغر", "اهدا", "هدي" };
        static readonly string[] SetVerbs = { "اضبط", "ضبط", "اجعل", "خلي", "غير", "حول", "بدل", "لون" };

        static readonly string[] LightWords = { "ضو", "اضوا", "نور", "انوار", "مصباح", "مصابيح", "اناره", "اضاه", "لمبه", "لمبات" };
        static readonly string[] TvWords = { "تلفاز", "تلفزيون", "تلفزه", "تليفزيون", "تلفيزيون", "tv" };
        static readonly string[] ComputerWords = { "كمبيوتر", "كومبيوتر", "حاسوب", "حاسب", "لابتوب", "pc" };
        // "ربوت": Whisper writes روبوت without its و ("أوقف الربوت").
        static readonly string[] VacuumWords = { "مكنس", "روبوت", "ربوت", "تنظيف", "نظف" };
        static readonly string[] MusicWords = { "موسيق", "اغني", "اغان", "اغاني" };
        static readonly string[] VolumeWords = { "صوت", "الصوت" };

        static readonly (string Cue, string Room)[] Rooms =
        {
            ("مطبخ", "kitchen"),
            ("معيشه", "living room"), ("صاله", "living room"), ("صالون", "living room"), ("جلوس", "living room"),
            ("نوم", "bedroom"),
        };

        static readonly (string Cue, string Color)[] Colors =
        {
            ("احمر", "red"), ("حمرا", "red"), ("برتقالي", "orange"), ("اصفر", "yellow"), ("صفرا", "yellow"),
            ("اخضر", "green"), ("خضرا", "green"), ("ازرق", "blue"), ("زرقا", "blue"), ("نيلي", "indigo"),
            ("بنفسجي", "purple"), ("ارجواني", "purple"), ("وردي", "pink"), ("زهري", "pink"),
            ("ابيض", "white"), ("بيضا", "white"), ("دافي", "warm"), ("بارد", "cool"),
        };

        static readonly (string Cue, string Genre)[] Genres = { ("جاز", "jazz"), ("روك", "rock"), ("لوفاي", "lofi"), ("هادي", "lofi") };

        /// <summary>Common city names in Arabic → the English name the geocoders know.</summary>
        static readonly Dictionary<string, string> Cities = new()
        {
            ["دبي"] = "Dubai", ["ابوظبي"] = "Abu Dhabi", ["ابو ظبي"] = "Abu Dhabi", ["الرياض"] = "Riyadh", ["جده"] = "Jeddah",
            ["مكه"] = "Mecca", ["المدينه"] = "Medina", ["الدوحه"] = "Doha", ["الكويت"] = "Kuwait City", ["المنامه"] = "Manama",
            ["مسقط"] = "Muscat", ["عمان"] = "Amman", ["بيروت"] = "Beirut", ["دمشق"] = "Damascus", ["بغداد"] = "Baghdad",
            ["القاهره"] = "Cairo", ["الاسكندريه"] = "Alexandria", ["تونس"] = "Tunis", ["الجزاير"] = "Algiers",
            ["الرباط"] = "Rabat", ["الدار البيضا"] = "Casablanca", ["لندن"] = "London", ["باريس"] = "Paris",
            ["برلين"] = "Berlin", ["طوكيو"] = "Tokyo", ["سيول"] = "Seoul", ["نيويورك"] = "New York", ["اسطنبول"] = "Istanbul",
        };

        static readonly string[] Prefixes = { "وال", "بال", "فال", "كال", "لل", "ال", "و", "ف", "ب", "ل" };
        static readonly string[] PlaceStops = { "اليوم", "غدا", "بكره", "بكرا", "الان", "حاليا", "من", "فضلك", "لو", "سمحت" };
        static readonly Regex Number = new(@"\d+", RegexOptions.Compiled);

        /// <summary>The English command for an Arabic utterance, or null when it isn't a home or info command (chat).</summary>
        public static string ToEnglish(string text)
        {
            if (!ArabicText.ContainsArabic(text)) return null;
            var normalized = ArabicText.Normalize(text);
            // Speech-to-text (Whisper's smaller sizes especially) confuses the emphatic consonants with their plain neighbours:
            // "أطفئ الأضواء" comes back as "أتفي أل أدواء". When the exact cues find nothing, they are tried again with
            // both sides folded (ط→ت ض→د ص→س ظ/ذ→ز ق→ك) and a detached "ال" re-attached.
            // Info intents read the whole sentence (they are never compound).
            var info = Info(normalized, Tokens(normalized)) ?? Folded(() => Info(Fold(normalized), Tokens(Fold(normalized))));
            if (info != null) return info;

            // Each clause: exact cues first, then folded ones, so one misheard clause doesn't sink the other.
            var phrases = Clauses(Tokens(normalized))
                .Select(words => Clause(words) ?? Folded(() => Clause(Tokens(Fold(string.Join(" ", words))))))
                .Where(p => p != null).ToList();
            return phrases.Count == 0 ? null : string.Join(" and ", phrases);
        }

        /// <summary>A Normalize'd text with the letters STT confuses folded together and a detached "ال" re-attached.</summary>
        static string Fold(string normalized) =>
            Regex.Replace(normalized, @"(^|\s)ال\s+", "$1ال").Replace('ط', 'ت').Replace('ض', 'د').Replace('ص', 'س').Replace('ظ', 'ز').Replace('ذ', 'ز').Replace('ق', 'ك');

        // While true, every cue is folded too (see Fold). Only set inside Folded, on the calling thread.
        [ThreadStatic] static bool s_Folded;

        static string Folded(Func<string> rewrite)
        {
            s_Folded = true;
            try { return rewrite(); }
            finally { s_Folded = false; }
        }

        /// <summary>The English text the models should see: the rewrite for an Arabic command, otherwise the text itself.</summary>
        public static string ForModel(string text) => ToEnglish(text) ?? text;

        static List<string> Tokens(string normalized) =>
            Regex.Split(normalized, @"[\s\?\!\.,;:]+").Where(t => t.Length > 0).ToList();

        /// <summary>The word and its forms without attached prefixes ("والتلفاز" → والتلفاز, التلفاز, تلفاز).</summary>
        static IEnumerable<string> Forms(string word)
        {
            yield return word;
            foreach (var prefix in Prefixes)
                if (word.Length - prefix.Length >= 2 && word.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var rest = word[prefix.Length..];
                    yield return rest;
                    if (rest.StartsWith("ال", StringComparison.Ordinal) && rest.Length > 4) yield return rest[2..];
                }
        }

        static string Cue(string cue) => s_Folded ? Fold(cue) : cue;
        static bool Matches(string word, string[] cues) => Forms(word).Any(f => cues.Any(c => f.StartsWith(Cue(c), StringComparison.Ordinal)));
        static bool Any(List<string> words, string[] cues) => words.Any(w => Matches(w, cues));
        static string Find(List<string> words, (string Cue, string Value)[] table) =>
            words.SelectMany(Forms).Select(f => table.FirstOrDefault(t => f.StartsWith(Cue(t.Cue), StringComparison.Ordinal)).Value).FirstOrDefault(v => v != null);

        static readonly string[][] Verbs = { OnVerbs, OffVerbs, UpVerbs, DownVerbs, SetVerbs };

        /// <summary>The word itself starts with a verb cue (no prefix stripped): وقف is "stop", not "and" + قف.</summary>
        static bool IsVerb(string word) =>
            Verbs.Any(list => list.Any(c => word.StartsWith(c, StringComparison.Ordinal) || Fold(word).StartsWith(Fold(c), StringComparison.Ordinal)));

        /// <summary>Splits "أطفئ الضوء وشغل التلفاز" into one clause per verb.</summary>
        static List<List<string>> Clauses(List<string> words)
        {
            var clauses = new List<List<string>> { new() };
            foreach (var word in words)
            {
                var current = clauses[^1];
                var joins = word is "و" or "ثم" || (word.Length > 2 && word[0] == 'و' && IsVerb(word[1..]) && !IsVerb(word));
                if (joins && current.Count > 0)
                {
                    clauses.Add(new List<string>());
                    if (word is "و" or "ثم") continue;
                    clauses[^1].Add(word[1..]);
                    continue;
                }
                current.Add(word);
            }
            return clauses.Where(c => c.Count > 0).ToList();
        }

        static string Clause(List<string> words)
        {
            bool on = Any(words, OnVerbs), off = Any(words, OffVerbs), up = Any(words, UpVerbs), down = Any(words, DownVerbs), set = Any(words, SetVerbs);
            var room = Find(words, Rooms);
            var color = Find(words, Colors);
            var number = Number.Match(string.Join(" ", words));
            var value = number.Success ? number.Value : null;
            var roomPart = room != null ? room + " " : string.Empty;

            if (Any(words, VolumeWords) && !Any(words, MusicWords))
            {
                // Whisper hears "قلل الصوت" (lower the volume) as "قل الصوت" ("say the volume"), which means nothing else.
                // Only the whole word counts, next to a volume word: as a cue, قل would also start قلب, قليل, …
                if (!up && words.Contains("قل")) down = true;
                // علّي الصوت (dialect "raise the volume") normalizes to "علي", the preposition "on": it only means "raise"
                // before a volume word with no number ("خلي الصوت على 30" sets the volume to 30).
                if (!down && value == null && words.Any(w => w is "علي" or "عليه" || w.StartsWith("عليل", StringComparison.Ordinal))) up = true;
                if (value != null && !up && !down) return $"set the volume to {value}";
                if (up && !down) return "turn the volume up";
                if (down && !up) return "turn the volume down";
                if (words.Any(w => w is "كم" or "ما")) return "what is the volume";
                return value != null ? $"set the volume to {value}" : null;
            }
            if (Any(words, TvWords)) return off ? $"turn off the {roomPart}tv" : on ? $"turn on the {roomPart}tv" : null;
            if (Any(words, ComputerWords)) return off ? $"turn off the {roomPart}computer" : on ? $"turn on the {roomPart}computer" : null;
            if (Any(words, MusicWords))
            {
                if (off) return "stop the music";
                var genre = Find(words, Genres);
                return on || genre != null ? (genre != null ? $"play {genre} music" : "play music") : null;
            }
            if (Any(words, VacuumWords)) return off ? "stop the robot vacuum" : "start the robot vacuum";
            // A colour alone is no light command ("ليه السما زرقا؟" is "why is the sky blue?"): it needs a light word, a
            // room, or a verb that sets or switches something.
            if (Any(words, LightWords) || room != null || (color != null && (set || on)))
            {
                if (color != null && !off) return $"change the {roomPart}light to {color}";
                if (off) return $"turn off the {roomPart}light";
                if (on || set) return value != null ? $"turn on the {roomPart}light at {value}% brightness" : $"turn on the {roomPart}light";
            }
            return null;
        }

        static string Info(string normalized, List<string> words)
        {
            bool Has(string phrase) => normalized.Contains(Cue(phrase), StringComparison.Ordinal);

            if (Has("من انت") || Has("مين انت") || Has("من تكون") || Has("عرف بنفسك") || Has("عرفني بنفسك") || Has("عرفنا بنفسك") ||
                (words.Contains("اسمك") && words.Any(w => TimeQuestion.Contains(w))))
                return "who are you";
            if (Has("اين انا") || Has("وين انا") || Has("فين انا") || Has("موقعي") || Has("مكاني"))
                return "where am i";
            if (Any(words, new[] { "طقس", "حراره", "مطر", "امطار" }) || words.Any(w => Forms(w).Contains("جو")))
            {
                var day = Has("بعد غد") || Has("بعد بكره") ? " the day after tomorrow" : Has("غدا") || Has("بكره") || Has("بكرا") ? " tomorrow" : string.Empty;
                var city = Place(words);
                return $"what's the weather{(city != null ? " in " + city : string.Empty)}{day}";
            }
            if ((Any(words, new[] { "ساعه", "وقت" }) && words.Any(w => TimeQuestion.Contains(w))) || Has("كم الساعه"))
            {
                var city = Place(words);
                return city != null ? $"what time is it in {city}" : "what time is it";
            }
            // Questions about Nova ("كيف حالك", "كم عمرك", "ما رأيك", "كيف أخبارك") are conversation, never a search.
            if (words.Any(w => Forms(w).Any(f => Personal.Any(p => Cue(p) == f)))) return null;
            // News: the noun itself ("أخبار", "خبر", "الأخبار"), not the verb "خبرني" (tell me), which is a search.
            if (words.Any(w => Forms(w).Any(f => f == Cue("خبر") || f.StartsWith(Cue("اخبار"), StringComparison.Ordinal))))
                return $"latest news about {Topic(words)}".TrimEnd();
            // "ما هو مستوى الصوت" is a volume question, not a search: device words leave it to the command rules.
            var device = Any(words, LightWords) || Any(words, TvWords) || Any(words, ComputerWords) || Any(words, VacuumWords) || Any(words, MusicWords) || Any(words, VolumeWords);
            if (device) return null;
            if (IsSearch(words) || Has("من هو") || Has("من هي") || Has("ما هو") || Has("ما هي"))
                return $"{SearchPrefix} {Topic(words)}".TrimEnd();
            return null;
        }

        /// <summary>"What"/"how much" in MSA and dialect, for "what time is it" (كم الساعة، شو الساعة، الساعة كام، قديش الساعة)
        /// and "what's your name" (ما اسمك، شو اسمك، اسمك ايه).</summary>
        static readonly string[] TimeQuestion = { "كم", "ما", "ماهو", "ماهي", "شو", "ايش", "كام", "قديش", "اديش", "ايه", "شنو", "واش" };

        /// <summary>Second-person and small-talk words: a question holding one is about Nova, not the world.</summary>
        static readonly string[] Personal =
        {
            "حالك", "حالكم", "كيفك", "كيفكم", "شلونك", "شلونكم", "ازيك", "ازيكم", "عامل", "عامله", "اخبارك", "اخباركم",
            "عمرك", "رايك", "رايكم", "تحبني", "تحب", "تحبين", "انت", "انتي", "اسمك", "صحتك", "يومك", "نفسك",
        };

        /// <summary>
        /// Question words that ask about the world, in Modern Standard Arabic and the common dialects (Gulf, Levantine,
        /// Egyptian), as Whisper writes them after <see cref="ArabicText.Normalize"/>: أين/وين/فين, ماذا/شو/إيش/إيه,
        /// لماذا/ليش/ليه, مين, كم/قديش/كام, متى/إمتى, كيف/ازاي, أي, هل.
        /// </summary>
        static readonly string[] QuestionWords =
        {
            "اين", "وين", "فين", "ماذا", "شو", "ايش", "ايه", "لماذا", "ليش", "ليه", "مين", "كم", "قديش", "كام",
            "متي", "امتي", "كيف", "ازاي", "اي", "هل",
        };

        /// <summary>Requests for information: "ابحث عن", "أخبرني عن", "حدثني عن", "اشرح", "عرّف", "معلومات عن", "جوجل".</summary>
        static readonly string[] SearchWords =
        {
            "ابحث", "بحث", "فتش", "اخبرني", "خبرني", "حدثني", "احكيلي", "اشرح", "اشرحلي", "عرف", "تعريف", "معلومات", "جوجل", "غوغل", "قوقل",
        };

        static bool IsSearch(List<string> words)
        {
            if (words.Count == 0) return false;
            if (words.Any(w => QuestionWords.Contains(w) || SearchWords.Contains(w))) return true;
            // "دوّر على" (look for): دوّر alone is also "turn on", which the command rules read.
            for (var i = 0; i + 1 < words.Count; i++)
                if (words[i] == "دور" && words[i + 1] == "علي") return true;
            // "ما" and "من" open a question at the start of the sentence ("ما عاصمة فرنسا؟", "من مخترع الهاتف؟");
            // inside it they are "not" and "from". "من فضلك" (please) is not "who".
            if (words[0] is "ما" or "ماهو" or "ماهي") return true;
            return words[0] == "من" && (words.Count < 2 || words[1] != "فضلك");
        }

        /// <summary>The place after في ("in"), as an English name when it is a known city, otherwise as written.</summary>
        static string Place(List<string> words)
        {
            var at = words.FindIndex(w => w == "في");
            if (at < 0 || at == words.Count - 1) return null;
            var place = words.Skip(at + 1).TakeWhile(w => !PlaceStops.Contains(w)).Take(3).ToList();
            for (var n = place.Count; n > 0; n--)
            {
                var name = string.Join(" ", place.Take(n));
                if (Cities.TryGetValue(name, out var english)) return english;
                if (name.StartsWith("ال", StringComparison.Ordinal) && Cities.TryGetValue(name[2..], out english)) return english;
            }
            return place.Count > 0 ? string.Join(" ", place) : null;
        }

        static readonly string[] TopicStops = { "ابحث", "بحث", "عن", "في", "من", "هو", "هي", "ما", "ماذا", "متي", "اخبار", "خبر", "اخر", "اخبرني", "قل", "لي", "الانترنت", "الويب", "اليوم", "عن", "اين", "لماذا", "كم", "يقع", "تقع", "يوجد", "توجد",
            "وين", "فين", "شو", "ايش", "ايه", "ليش", "ليه", "مين", "قديش", "كام", "امتي", "كيف", "ازاي", "اي", "هل", "الذي", "التي",
            "فتش", "خبرني", "حدثني", "احكيلي", "اشرح", "اشرحلي", "عرف", "تعريف", "معلومات", "جوجل", "غوغل", "قوقل" };

        /// <summary>A search topic: the words left after the question and search words ("ابحث عن آلان تورنغ" → "الان تورنغ").</summary>
        public static string Topic(List<string> words) => string.Join(" ", words.Where((w, i) => !IsTopicStop(words, i)));

        /// <summary>Whether normalized word <paramref name="i"/> is a question or search word; "دوّر على" (look for) as a pair, since دور alone
        /// is also "role" and على is also the name Ali.</summary>
        static bool IsTopicStop(List<string> normalized, int i) =>
            IsTopicStop(normalized[i]) ||
            (normalized[i] == "دور" && i + 1 < normalized.Count && normalized[i + 1] == "علي") ||
            (normalized[i] == "علي" && i > 0 && normalized[i - 1] == "دور");

        static bool IsTopicStop(string normalizedWord) => Forms(normalizedWord).Any(f => TopicStops.Contains(f));

        /// <summary>The search topic of an Arabic question, in its original spelling minus question words.</summary>
        public static string SearchTopic(string text)
        {
            var original = Regex.Split(text ?? string.Empty, @"[\s\?\!\.,;:؟،]+").Where(t => t.Length > 0).ToList();
            var normalized = original.Select(ArabicText.Normalize).ToList();
            var kept = original.Where((w, i) => !IsTopicStop(normalized, i)).ToList();
            return kept.Count > 0 ? string.Join(" ", kept) : text?.Trim();
        }
    }
}
