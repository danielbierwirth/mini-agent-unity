using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoiceAgent
{
    /// <summary>
    /// The EN / KR / AR toggle: a three-segment control on its own overlay canvas (like SceneToggle), so it looks the
    /// same in both scenes. It edits the one shared <see cref="AppLanguage"/>; speech input, replies and speech output
    /// follow it from the next turn on, without a scene reload. The labels are RTL text components (the Arabic one is
    /// shaped by RTLTMPro).
    /// </summary>
    public sealed class LanguageToggle : MonoBehaviour
    {
        static readonly (Lang Lang, string Label)[] s_Options = { (Lang.En, "EN"), (Lang.Ko, "한국어"), (Lang.Ar, "العربية") };

        [Header("Placement (reference resolution 1920 x 1080)")]
        [Tooltip("Anchor and pivot: (0,1) top-left, (0.5,1) top-center, (1,1) top-right.")]
        [SerializeField] Vector2 m_Corner = new(1f, 1f);
        [SerializeField] Vector2 m_Offset = new(-20f, -20f);
        [SerializeField] Vector2 m_SegmentSize = new(96f, 48f);
        [SerializeField] int m_FontSize = 22;
        [SerializeField] int m_SortingOrder = 1001;

        [SerializeField] Color m_Background = new(0.16f, 0.17f, 0.22f, 0.9f);
        [SerializeField] Color m_Selected = new(0.24f, 0.62f, 0.96f, 1f);

        readonly Image[] m_Segments = new Image[s_Options.Length];

        void Start()
        {
            var canvasGo = new GameObject("LanguageToggleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = m_SortingOrder;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var bar = new GameObject("LanguageToggle", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = m_Corner;
            rt.anchoredPosition = m_Offset;
            rt.sizeDelta = new Vector2(m_SegmentSize.x * s_Options.Length + 8, m_SegmentSize.y);
            var background = bar.GetComponent<Image>();
            background.sprite = UiRuntimeAssets.RoundedSprite(16f);
            background.type = Image.Type.Sliced;
            background.color = m_Background;
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 0;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;

            for (var i = 0; i < s_Options.Length; i++)
            {
                var (lang, label) = s_Options[i];
                var segment = new GameObject($"Segment_{AppLanguage.Code(lang)}", typeof(RectTransform), typeof(Image), typeof(Button));
                segment.transform.SetParent(bar.transform, false);
                var image = segment.GetComponent<Image>();
                image.sprite = UiRuntimeAssets.RoundedSprite(12f);
                image.type = Image.Type.Sliced;
                m_Segments[i] = image;
                segment.GetComponent<Button>().onClick.AddListener(() => AppLanguage.Set(lang));

                var textGo = new GameObject("Label", typeof(RectTransform), typeof(AutoRtlText));
                textGo.transform.SetParent(segment.transform, false);
                var trt = (RectTransform)textGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                var text = textGo.GetComponent<AutoRtlText>();
                text.fontSize = m_FontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.PreserveNumbers = true;
                text.text = label;
            }

            Refresh(AppLanguage.Current);
            AppLanguage.Changed += Refresh;
        }

        void Refresh(Lang current)
        {
            for (var i = 0; i < s_Options.Length; i++)
                if (m_Segments[i] != null) m_Segments[i].color = s_Options[i].Lang == current ? m_Selected : new Color(1f, 1f, 1f, 0f);
        }

        void OnDestroy() => AppLanguage.Changed -= Refresh;
    }
}
