using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A button that switches to another scene (MiniAgentScene ⇄ VoiceAgentDemo). It builds its own small overlay canvas so
/// it looks the same in both scenes and doesn't depend on either scene's UI layout; the scene's EventSystem drives it.
/// Loading the other scene unloads this one, so each scene's models are disposed before the next scene loads its own.
/// Both scenes must be in the Build Profile's scene list.
/// </summary>
public class SceneToggle : MonoBehaviour
{
    [Tooltip("Scene to load (name as listed in the Build Profile).")]
    [SerializeField] private string _targetScene = "VoiceAgentDemo";
    [Tooltip("Button label.")]
    [SerializeField] private string _label = "Voice Agent";

    [Header("Placement (reference resolution 1920 x 1080)")]
    [Tooltip("Anchor and pivot: (0,1) top-left, (0.5,1) top-center, (1,1) top-right, (0,0) bottom-left, (1,0) bottom-right.")]
    [SerializeField] private Vector2 _corner = new(0f, 1f);
    [SerializeField] private Vector2 _offset = new(20f, -20f);
    [SerializeField] private Vector2 _size = new(170f, 56f);
    [SerializeField] private int _fontSize = 26;

    [SerializeField] private Color _color = new(0.16f, 0.17f, 0.22f, 0.9f);

    Button m_Button;
    bool m_Loading;

    void Start()
    {
        var canvasGo = new GameObject("SceneToggleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // above each scene's own UI
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var buttonGo = new GameObject("SceneToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)buttonGo.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = _corner;
        rt.anchoredPosition = _offset;
        rt.sizeDelta = _size;
        var image = buttonGo.GetComponent<Image>();
        image.sprite = UiRuntimeAssets.RoundedSprite(16f);
        image.type = Image.Type.Sliced;
        image.color = _color;
        m_Button = buttonGo.GetComponent<Button>();
        m_Button.onClick.AddListener(Switch);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = _fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = _label;
    }

    /// <summary>Loads the target scene (once; repeated clicks while it loads are ignored).</summary>
    public void Switch()
    {
        if (m_Loading || string.IsNullOrEmpty(_targetScene)) return;
        if (!Application.CanStreamedLevelBeLoaded(_targetScene))
        {
            Debug.LogError($"[SceneToggle] '{_targetScene}' isn't in the Build Profile's scene list.");
            return;
        }
        m_Loading = true;
        if (m_Button != null) m_Button.interactable = false;
        SceneManager.LoadScene(_targetScene);
    }
}
