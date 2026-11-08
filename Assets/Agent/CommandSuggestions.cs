using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoiceAgent;   // AppLanguage

/// <summary>
/// The row of example commands above the input field. Tapping one puts its text in the input field and submits it;
/// the shuffle button deals a new random set. The buttons are disabled while the agent loads or answers.
/// The examples are in the selected language (EN / KR / AR) and are dealt again when it changes.
/// </summary>
public sealed class CommandSuggestions : MonoBehaviour
{
    [SerializeField] HomeAgentConnector _connector;
    [Tooltip("Example buttons, left to right; each shows its command in its child text.")]
    [SerializeField] Button[] _examples;
    [SerializeField] Button _shuffle;

    static readonly string[] s_English =
    {
        "Turn on the living room light",
        "Turn off the bedroom light",
        "Set the kitchen light to blue",
        "Turn on the bedroom light at 30%",
        "Turn on the living room TV",
        "Play some jazz",
        "Stop the music",
        "Set the volume to 60",
        "Start the vacuum",
        "What's the weather in Seoul?",
        "What time is it in New York?",
        "Where am I?",
    };

    static readonly string[] s_Korean =
    {
        "거실 불 켜줘",
        "침실 불 꺼줘",
        "부엌 조명 초록색으로 바꿔줘",
        "거실 티비 켜줘",
        "컴퓨터 켜줘",
        "신나는 음악 틀어줘",
        "볼륨 30으로 줄여줘",
        "청소기 돌려줘",
        "거실 불 켜고 거실 티비 켜줘",
        "서울 날씨 어때?",
        "오늘 기분 어때?",
    };

    static readonly string[] s_Arabic =
    {
        "أطفئ أضواء المطبخ",
        "شغّل ضوء غرفة المعيشة",
        "اجعل ضوء المطبخ أزرق",
        "شغّل ضوء غرفة النوم بنسبة 30%",
        "شغّل التلفاز",
        "شغّل موسيقى جاز",
        "أوقف الموسيقى",
        "اضبط الصوت على 60",
        "شغّل المكنسة",
        "شغّل التلفاز وأطفئ ضوء المطبخ",
        "كيف الطقس في دبي؟",
        "كم الساعة في طوكيو؟",
        "أين أنا؟",
        "كيف حالك اليوم؟",
    };

    static string[] Examples => AppLanguage.Current switch
    {
        Lang.Ko => s_Korean,
        Lang.Ar => s_Arabic,
        _ => s_English,
    };

    TMP_Text[] m_Labels;
    // The command on each button. Read back from here, not from the label: an RTL label's text is the shaped,
    // reversed display string.
    string[] m_Commands;
    readonly HashSet<int> m_Shown = new();

    bool CanSubmit => _connector != null && _connector.IsReady && !_connector.IsBusy;

    void Awake()
    {
        // Same rounded look as the input field and send button (HomeAgentConnector.StyleUI).
        var rounded = UiRuntimeAssets.RoundedSprite(16f);
        m_Labels = new TMP_Text[_examples.Length];
        m_Commands = new string[_examples.Length];
        for (var i = 0; i < _examples.Length; i++)
        {
            m_Labels[i] = _examples[i].GetComponentInChildren<TMP_Text>();
            Round(_examples[i], rounded);
            var slot = i;
            _examples[i].onClick.AddListener(() => { if (CanSubmit) _connector.Submit(m_Commands[slot]); });
        }
        Round(_shuffle, rounded);
        _shuffle.onClick.AddListener(Deal);
        Deal();
        AppLanguage.Changed += OnLanguageChanged;
    }

    void OnLanguageChanged(Lang lang)
    {
        m_Shown.Clear();
        Deal();
    }

    void OnDestroy() => AppLanguage.Changed -= OnLanguageChanged;

    static void Round(Button button, Sprite sprite)
    {
        var image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    /// <summary>Shows a new random set of examples, avoiding the ones on screen when the pool allows it.</summary>
    public void Deal()
    {
        var examples = Examples;
        var pool = new List<int>();
        for (var i = 0; i < examples.Length; i++)
            if (!m_Shown.Contains(i)) pool.Add(i);
        if (pool.Count < m_Labels.Length)
            for (var i = 0; i < examples.Length; i++)
                if (!pool.Contains(i)) pool.Add(i);
        m_Shown.Clear();
        for (var slot = 0; slot < m_Labels.Length; slot++)
        {
            var pick = Random.Range(slot, pool.Count);
            (pool[slot], pool[pick]) = (pool[pick], pool[slot]);
            m_Shown.Add(pool[slot]);
            m_Commands[slot] = examples[pool[slot]];
            m_Labels[slot].text = m_Commands[slot];
        }
    }

    void Update()
    {
        var enabled = CanSubmit;
        foreach (var button in _examples)
            if (button.interactable != enabled) button.interactable = enabled;
    }
}
