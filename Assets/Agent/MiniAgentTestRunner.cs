using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a scripted list of commands through <see cref="HomeAgentConnector"/>, one at
/// a time, in the selected language (English, Korean or Arabic list). Put this on a "Test" GameObject in the scene,
/// fill the lists in the Inspector (executed top to bottom), and set the timings. When the scene
/// starts it waits <see cref="_startDelay"/> seconds, then submits each command, waiting
/// <see cref="_interval"/> seconds between them (and for the agent to finish each turn).
/// The agent logs every turn (incl. the harvest log), so progress is visible there.
///
/// With <see cref="_toggleButton"/> assigned, the button starts the list and stops it mid-run (the turn already
/// running finishes; no further commands are submitted).
/// </summary>
public class MiniAgentTestRunner : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds to wait after the scene starts before the first command runs.")]
    [Min(0f)]
    [SerializeField] private float _startDelay = 5f;

    [Tooltip("Seconds to wait between commands (each command also waits for the agent to finish).")]
    [Min(0f)]
    [SerializeField] private float _interval = 5f;

    [Tooltip("Run the command list automatically when the scene starts.")]
    [SerializeField] private bool _autoRun = true;

    [Tooltip("Optional button that starts / stops the command list.")]
    [SerializeField] private Button _toggleButton;

    // One list per language, run in the selected language (the EN / 한국어 / العربية toggle). The lists hold the same
    // commands so a run looks the same in every language; _commands keeps its name so the scene's English list loads.
    [Header("Commands (run in order, top to bottom, in the selected language)")]
    [Tooltip("English commands.")]
    [SerializeField]
    private List<string> _commands = new List<string>
    {
        "Turn on the living room light",
        "Turn on the kitchen light blue",
        "Turn on the TV",
        "Turn on the living room speaker",
        "Set volume to 30%",
        "Play rock music",
        "Run the vacuum cleaner",
        "Turn off the living room light, and turn off the living room TV",
        "How's the weather in Seoul?",
        "What time is it right now?",
        "Turn on the bedroom computer",
        "How are you feeling today?"
    };

    [Tooltip("Korean commands.")]
    [SerializeField]
    private List<string> _commandsKo = new List<string>
    {
        "거실 불 켜줘",
        "부엌 불 파란색으로 켜줘",
        "티비 켜줘",
        "거실 스피커 켜줘",
        "볼륨 30으로 해줘",
        "락 틀어줘",
        "청소기 돌려줘",
        "거실 불 끄고, 거실 티비 꺼줘",
        "서울 날씨 어때?",
        "지금 몇시야?",
        "안방 컴퓨터 켜줘",
        "오늘 기분 어때?"
    };

    [Tooltip("Arabic commands.")]
    [SerializeField]
    private List<string> _commandsAr = new List<string>
    {
        "شغّل ضوء غرفة المعيشة",
        "شغّل ضوء المطبخ باللون الأزرق",
        "شغّل التلفاز",
        "شغّل الموسيقى في غرفة المعيشة",
        "اضبط الصوت على 30",
        "شغّل موسيقى الروك",
        "شغّل المكنسة",
        "أطفئ ضوء غرفة المعيشة وأطفئ التلفاز في غرفة المعيشة",
        "كيف الطقس في سيول؟",
        "كم الساعة الآن؟",
        "شغّل الكمبيوتر في غرفة النوم",
        "كيف حالك اليوم؟"
    };

    /// <summary>The command list for the selected language (English when that list is empty).</summary>
    List<string> Commands
    {
        get
        {
            var list = VoiceAgent.AppLanguage.Current switch { VoiceAgent.Lang.Ko => _commandsKo, VoiceAgent.Lang.Ar => _commandsAr, _ => _commands };
            return list != null && list.Count > 0 ? list : _commands;
        }
    }

    Coroutine m_Run;
    TMP_Text m_Label;
    string m_IdleLabel;           // the button's own label from the scene, shown while stopped

    public bool IsRunning => m_Run != null;

    private void Start()
    {
        if (_toggleButton != null)
        {
            _toggleButton.onClick.AddListener(Toggle);
            m_Label = _toggleButton.GetComponentInChildren<TMP_Text>();
            if (m_Label != null) m_IdleLabel = m_Label.text;
            var image = _toggleButton.GetComponent<Image>();
            if (image != null) { image.sprite = UiRuntimeAssets.RoundedSprite(16f); image.type = Image.Type.Sliced; }
        }
        if (_autoRun) StartRun(_startDelay);
        else UpdateLabel();
    }

    /// <summary>Start the command list if it isn't running, otherwise stop it.</summary>
    public void Toggle()
    {
        if (IsRunning) StopRun();
        else StartRun(0f);
    }

    public void StartRun(float delay)
    {
        if (IsRunning) return;
        m_Run = StartCoroutine(RunWhenReady(delay));
        UpdateLabel();
    }

    public void StopRun()
    {
        if (!IsRunning) return;
        StopCoroutine(m_Run);
        m_Run = null;
        Debug.Log("[TESTRUNNER] STOPPED");
        UpdateLabel();
    }

    IEnumerator RunWhenReady(float delay)
    {
        // Wait the delay, then make sure the agent finished warmup
        // (submitting before it is ready would be ignored).
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var conn = FindAnyObjectByType<HomeAgentConnector>();
        while (conn == null || !conn.IsReady)
        {
            if (conn == null) conn = FindAnyObjectByType<HomeAgentConnector>();
            yield return null;
        }

        yield return Run(conn);
        m_Run = null;
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (m_Label != null) m_Label.text = IsRunning ? "Stop" : m_IdleLabel;
    }

    /// <summary>Submit every command in order.</summary>
    public IEnumerator Run(HomeAgentConnector conn)
    {
        if (conn == null) conn = FindAnyObjectByType<HomeAgentConnector>();
        if (conn == null) { Debug.LogWarning("[TESTRUNNER] no HomeAgentConnector in scene"); yield break; }

        // The list is read again each step, so switching the language mid-run continues in the new language.
        for (int i = 0; i < Commands.Count; i++)
        {
            while (conn.IsBusy) yield return null;
            var commands = Commands;
            if (i >= commands.Count) break;
            var cmd = commands[i];
            if (string.IsNullOrWhiteSpace(cmd)) continue;

            Debug.Log($"[TESTRUNNER] {i + 1}/{commands.Count}  {cmd}");
            conn.Submit(cmd);

            // let the turn start, then hold for the interval
            yield return null;
            yield return new WaitForSeconds(_interval);
        }

        while (conn.IsBusy) yield return null;
        Debug.Log("[TESTRUNNER] DONE");
    }
}
