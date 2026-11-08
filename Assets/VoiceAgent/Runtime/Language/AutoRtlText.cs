using RTLTMPro;
using TMPro;
using UnityEngine;

namespace VoiceAgent
{
    /// <summary>
    /// The RTL Text Mesh Pro component every text in both scenes uses. It is RTLTextMeshPro with Force Fix decided per
    /// text instead of per element:
    ///
    /// RTLTMPro shapes and reverses a text only when its first letter is RTL, so Nova's "TV أُطفئ" or a reply starting
    /// with an English device name or a number would render unshaped, left to right. Force Fix always shapes, but it
    /// also runs pure English through the RTL fixer, which reverses the line order of wrapped multi-line English.
    /// Here Force Fix is on exactly when the text contains an Arabic letter, so mixed Arabic + English + numbers render
    /// in the right order and English / Korean stay on TMP's own path, unchanged.
    ///
    /// <see cref="m_MirrorAlignment"/> right-aligns Arabic text where the authored alignment is left (and the other way
    /// around), as an RTL reader expects; centred text stays centred.
    ///
    /// Defaults for Arabic: Farsi off (it would turn ي/ك into Persian ی/ک and digits into Persian digits), Preserve
    /// Numbers on (Western digits in a value such as "22°C" or "50%" keep their order), Fix Tags on (rich text).
    /// </summary>
    [ExecuteInEditMode]
    public class AutoRtlText : RTLTextMeshPro
    {
        [Tooltip("Swap left/right alignment while the text is Arabic.")]
        [SerializeField] bool m_MirrorAlignment = true;

        [SerializeField, HideInInspector] TextAlignmentOptions m_AuthoredAlignment = TextAlignmentOptions.TopLeft;
        [SerializeField, HideInInspector] bool m_HasAuthoredAlignment;

        public bool MirrorAlignment
        {
            get => m_MirrorAlignment;
            set => m_MirrorAlignment = value;
        }

        protected override void Awake()
        {
            base.Awake();
            farsi = false;
        }

        /// <summary>The alignment authored for left-to-right text; set this instead of <c>alignment</c> from code.</summary>
        public TextAlignmentOptions AuthoredAlignment
        {
            get => m_HasAuthoredAlignment ? m_AuthoredAlignment : alignment;
            set
            {
                m_AuthoredAlignment = value;
                m_HasAuthoredAlignment = true;
                ApplyDirection(originalText);
            }
        }

#if TMP_VERSION_2_1_0_OR_NEWER || UNITY_6000_0_OR_NEWER
        public override string text
#else
        public new string text
#endif
        {
            get => base.text;
            set
            {
                ApplyDirection(value);
                base.text = value;
            }
        }

        void ApplyDirection(string value)
        {
            var arabic = ArabicText.ContainsArabic(value);
            ForceFix = arabic;
            if (!m_HasAuthoredAlignment)
            {
                m_AuthoredAlignment = alignment;
                m_HasAuthoredAlignment = true;
            }
            var target = arabic && m_MirrorAlignment ? Mirror(m_AuthoredAlignment) : m_AuthoredAlignment;
            if (alignment != target) alignment = target;
            if (originalText == value && havePropertiesChanged) UpdateText();
        }

        static TextAlignmentOptions Mirror(TextAlignmentOptions a) => a switch
        {
            TextAlignmentOptions.TopLeft => TextAlignmentOptions.TopRight,
            TextAlignmentOptions.TopRight => TextAlignmentOptions.TopLeft,
            TextAlignmentOptions.Left => TextAlignmentOptions.Right,
            TextAlignmentOptions.Right => TextAlignmentOptions.Left,
            TextAlignmentOptions.BottomLeft => TextAlignmentOptions.BottomRight,
            TextAlignmentOptions.BottomRight => TextAlignmentOptions.BottomLeft,
            TextAlignmentOptions.BaselineLeft => TextAlignmentOptions.BaselineRight,
            TextAlignmentOptions.BaselineRight => TextAlignmentOptions.BaselineLeft,
            TextAlignmentOptions.MidlineLeft => TextAlignmentOptions.MidlineRight,
            TextAlignmentOptions.MidlineRight => TextAlignmentOptions.MidlineLeft,
            TextAlignmentOptions.CaplineLeft => TextAlignmentOptions.CaplineRight,
            TextAlignmentOptions.CaplineRight => TextAlignmentOptions.CaplineLeft,
            _ => a,
        };
    }
}
