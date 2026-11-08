using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using VoiceAgent;                 // VoiceInput, ModelRoots (shared with the VoiceAgentDemo scene)

/// <summary>
/// Voice as a second input to the one home agent. Tap the mic button: Silero VAD + SenseVoice (on-device, see
/// <see cref="VoiceInput"/>) listen until you stop talking, partial transcripts show live in the command field,
/// and the final transcript goes to <see cref="HomeAgentConnector.SubmitTranscript"/>, the same turn a typed
/// command runs. Nothing here knows about tools or devices.
///
/// The speech models load after the agent has warmed up, so the two loads don't stack into one long hitch.
/// </summary>
public class VoiceCommandInput : MonoBehaviour
{
    [SerializeField] private HomeAgentConnector _connector;
    [SerializeField] private Button _micButton;
    [Tooltip("Shows partial transcripts while you talk (the agent's command field).")]
    [SerializeField] private InputField _input;

    [Header("Mic button colors")]
    [SerializeField] private Color _idleColor = new(0.20f, 0.55f, 0.95f);
    [SerializeField] private Color _listeningColor = new(0.92f, 0.26f, 0.30f);
    [SerializeField] private Color _disabledColor = new(0.55f, 0.55f, 0.58f);

    VoiceInput m_Voice;
    Text m_Label;
    Image m_Image;
    string m_Pending;             // a final transcript waiting for the agent to finish its current turn

    public bool IsReady => m_Voice != null && m_Voice.IsLoaded;
    public bool IsListening => m_Voice != null && m_Voice.IsListening;

    void Start()
    {
        if (_connector == null) _connector = FindAnyObjectByType<HomeAgentConnector>();
        if (_micButton != null)
        {
            _micButton.onClick.AddListener(ToggleListen);
            m_Label = _micButton.GetComponentInChildren<Text>();
            m_Image = _micButton.GetComponent<Image>();
            if (m_Image != null) { m_Image.sprite = UiRuntimeAssets.RoundedSprite(16f); m_Image.type = Image.Type.Sliced; }
        }
        SetButton("Mic", _disabledColor, false);
        StartCoroutine(LoadWhenAgentReady());
    }

    IEnumerator LoadWhenAgentReady()
    {
        while (_connector != null && !_connector.IsReady) yield return null;
        var prepare = ModelRoots.PrepareAsync();   // Android: copies the models out of the APK on first launch
        while (!prepare.IsCompleted) yield return null;
        try
        {
            prepare.GetAwaiter().GetResult();   // rethrows a failed copy
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Voice] speech models aren't available, voice input is off: {e.Message}");
            SetButton("No mic", _disabledColor, false);
            yield break;
        }
        try
        {
            m_Voice = new VoiceInput();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Voice] speech models didn't load, voice input is off: {e.Message}");
            SetButton("No mic", _disabledColor, false);
            yield break;
        }
        m_Voice.Partial += OnPartial;
        m_Voice.Final += OnFinal;
        m_Voice.ListeningStopped += () => SetButton("Mic", _idleColor, true);
        SetButton("Mic", _idleColor, true);
        Debug.Log("[Voice] speech-to-text ready (Silero VAD + SenseVoice)");
    }

    void ToggleListen()
    {
        if (m_Voice == null) return;
        if (m_Voice.IsListening) { m_Voice.Stop(); return; }
        if (_connector != null && _connector.IsBusy) return;   // one turn at a time
        if (!m_Voice.Listen())
        {
            Debug.LogWarning("[Voice] can't listen: no microphone, or mic permission not granted yet.");
            return;
        }
        if (_input != null) _input.text = "";
        SetButton("Stop", _listeningColor, true);
    }

    void OnPartial(string text)
    {
        if (_input != null && !string.IsNullOrWhiteSpace(text)) _input.text = text;
    }

    void OnFinal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Debug.Log($"[Voice] transcript: {text}");
        m_Pending = text;
    }

    /// <summary>
    /// Test hook: feeds a transcript through the same path a spoken final transcript takes, as if SenseVoice had
    /// just produced it. Works without a microphone.
    /// </summary>
    public void InjectTranscript(string transcript) => OnFinal(transcript);

    void Update()
    {
        m_Voice?.Tick();
        if (m_Voice != null && m_Voice.IsListening && m_Image != null)
            m_Image.color = Color.Lerp(_listeningColor, Color.white, Mathf.Clamp01(m_Voice.Level * 4f) * 0.5f);

        // Hand the transcript over once the agent is free (a turn may still be running).
        if (m_Pending != null && _connector != null && _connector.IsReady && !_connector.IsBusy)
        {
            var text = m_Pending;
            m_Pending = null;
            _connector.SubmitTranscript(text);
        }
    }

    void SetButton(string label, Color color, bool interactable)
    {
        if (m_Label != null) m_Label.text = label;
        if (m_Image != null) m_Image.color = color;
        if (_micButton != null) _micButton.interactable = interactable;
    }

    void OnDestroy() => m_Voice?.Dispose();
}
