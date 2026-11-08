// Tiger's user-facing replies in the selected language (English / Korean / Arabic). The tool calls, their arguments and
// the device IDs stay English: only what the user reads and hears is localized here.
using VoiceAgent;

namespace NeedleTest
{
    public static class AgentReplies
    {
        static Lang L => AppLanguage.Current;
        static string Pick(string en, string ko, string ar) => AppLanguage.Pick(L, en, ko, ar);

        public static string Room(HomeDeviceController.Room room) => room switch
        {
            HomeDeviceController.Room.Kitchen => Pick("Kitchen", "주방", "المطبخ"),
            HomeDeviceController.Room.LivingRoom => Pick("Living Room", "거실", "غرفة المعيشة"),
            HomeDeviceController.Room.Bedroom => Pick("Bedroom", "침실", "غرفة النوم"),
            _ => "?",
        };

        /// <summary>A light color name from the scene's palette (HomeDeviceController); unknown names pass through.</summary>
        public static string Color(string color) => (color ?? string.Empty).ToLowerInvariant() switch
        {
            "red" => Pick("red", "빨간색", "الأحمر"),
            "orange" => Pick("orange", "주황색", "البرتقالي"),
            "yellow" => Pick("yellow", "노란색", "الأصفر"),
            "green" => Pick("green", "초록색", "الأخضر"),
            "blue" => Pick("blue", "파란색", "الأزرق"),
            "purple" => Pick("purple", "보라색", "البنفسجي"),
            "pink" => Pick("pink", "분홍색", "الوردي"),
            "white" => Pick("white", "흰색", "الأبيض"),
            "warm" => Pick("warm white", "따뜻한 색", "الأبيض الدافئ"),
            "cool" => Pick("cool white", "차가운 색", "الأبيض البارد"),
            _ => color,
        };

        public static string Genre(string genre) => (genre ?? string.Empty).ToLowerInvariant() switch
        {
            "jazz" => Pick("jazz", "재즈", "الجاز"),
            "rock" => Pick("rock", "록", "الروك"),
            "lofi" => Pick("lofi", "로파이", "اللوفاي"),
            _ => genre,
        };

        public static string Greeting => Pick("Hi, there!", "안녕하세요!", "مرحبًا!");
        public static string CantHandle => Pick("Sorry, I can't handle that request.", "죄송해요, 그건 처리할 수 없어요.", "عذرًا، لا أستطيع تنفيذ هذا الطلب.");

        public static string NoRoom(string room) => Pick(
            $"There's no '{room}' room in this house. (Kitchen / Living Room / Bedroom)",
            $"이 집에는 '{room}' 방이 없어요. (주방 / 거실 / 침실)",
            $"لا توجد غرفة '{room}' في هذا المنزل. (المطبخ / غرفة المعيشة / غرفة النوم)");

        public static string LightOn(string room, int? brightness) => brightness.HasValue
            ? Pick($"Turned on the {room} light at {brightness}% brightness.", $"{room} 불을 밝기 {brightness}%로 켰어요.", $"شغّلتُ ضوء {room} بسطوع {brightness}%.")
            : Pick($"Turned on the {room} light.", $"{room} 불을 켰어요.", $"شغّلتُ ضوء {room}.");

        public static string LightOff(string room) => Pick($"Turned off the {room} light.", $"{room} 불을 껐어요.", $"أطفأتُ ضوء {room}.");

        public static string LightColor(string room, string color) =>
            Pick($"Changed the {room} light to {Color(color)}.", $"{room} 조명을 {Color(color)}으로 바꿨어요.", $"غيّرتُ ضوء {room} إلى اللون {Color(color)}.");

        public static string Tv(bool on) => on
            ? Pick("Turned on the TV.", "TV를 켰어요.", "شغّلتُ التلفاز.")
            : Pick("Turned off the TV.", "TV를 껐어요.", "أطفأتُ التلفاز.");

        public static string Computer(bool on) => on
            ? Pick("Turned on the computer.", "컴퓨터를 켰어요.", "شغّلتُ الكمبيوتر.")
            : Pick("Turned off the computer.", "컴퓨터를 껐어요.", "أطفأتُ الكمبيوتر.");

        public static string Playing(string genre) => Pick($"Playing {Genre(genre)} music.", $"{Genre(genre)} 음악을 틀게요.", $"أشغّل موسيقى {Genre(genre)}.");
        public static string MusicStopped => Pick("Stopped the music.", "음악을 껐어요.", "أوقفتُ الموسيقى.");
        public static string NoSpeaker => Pick("There's no speaker.", "스피커가 없어요.", "لا يوجد مكبّر صوت.");
        public static string VolumeSet(int volume) => Pick($"Set the volume to {volume}%.", $"볼륨을 {volume}%로 맞췄어요.", $"ضبطتُ الصوت على {volume}%.");
        public static string VolumeIs(int volume) => Pick($"The volume is {volume}%.", $"지금 볼륨은 {volume}%예요.", $"مستوى الصوت {volume}%.");

        public static string VolumeStep(bool up, int volume) => up
            ? Pick($"Volume up to {volume}%.", $"볼륨을 {volume}%로 올렸어요.", $"رفعتُ الصوت إلى {volume}%.")
            : Pick($"Volume down to {volume}%.", $"볼륨을 {volume}%로 내렸어요.", $"خفضتُ الصوت إلى {volume}%.");

        public static string VacuumStarted => Pick("The robot vacuum started cleaning.", "로봇청소기가 청소를 시작했어요.", "بدأت المكنسة الآلية التنظيف.");
        public static string VacuumStopped => Pick("The robot vacuum stopped and is heading back to its dock.", "로봇청소기를 멈췄어요. 충전대로 돌아가요.", "توقفت المكنسة الآلية وهي تعود إلى قاعدتها.");
        public static string NoVacuum => Pick("There's no robot vacuum.", "로봇청소기가 없어요.", "لا توجد مكنسة آلية.");

        public static string Introduction => Pick(
            "I'm Tiger, the smart-home assistant of this house. I can switch the lights in the kitchen, living room and bedroom, " +
            "the TV, the computer and the robot vacuum, play music and change the volume, and tell you the weather, the time or look things up.",
            "저는 이 집의 스마트홈 비서 타이거예요. 주방, 거실, 침실 불과 TV, 컴퓨터, 로봇청소기를 켜고 끌 수 있고, 음악을 틀고 볼륨을 바꾸고, " +
            "날씨와 시간을 알려 드리거나 검색도 해 드려요.",
            "أنا تايغر، مساعد المنزل الذكي في هذا البيت. أستطيع تشغيل الأضواء في المطبخ وغرفة المعيشة وغرفة النوم وإطفاءها، " +
            "والتلفاز والكمبيوتر والمكنسة الآلية، وتشغيل الموسيقى وتغيير مستوى الصوت، وإخبارك بالطقس والوقت أو البحث عن المعلومات.");

        public static string NotFound(string query) => Pick($"I couldn't find an answer for \"{query}\".", $"\"{query}\"에 대한 답을 찾지 못했어요.", $"لم أجد إجابة عن \"{query}\".");
        public static string CantHandleCall(string name) => Pick($"Can't handle '{name}'.", $"'{name}'은(는) 처리할 수 없어요.", $"لا أستطيع تنفيذ '{name}'.");

        /// <summary>The instruction that pins the cloud model's reply language to the selected one.</summary>
        public static string ReplyLanguageRule => Pick(
            "Reply ONLY in English, whatever language the user writes in.",
            "Reply ONLY in Korean (polite 해요체), whatever language the user writes in.",
            "Reply ONLY in Modern Standard Arabic, in Arabic script, whatever language the user writes in.");

        public static string InputPlaceholder => Pick(
            "Type a command (e.g., turn on the living room light)",
            "명령을 입력하세요 (예: 거실 불 켜줘)",
            "اكتب أمرًا (مثال: شغّل ضوء غرفة المعيشة)");

        public static string Send => Pick("Send", "보내기", "إرسال");
    }
}
