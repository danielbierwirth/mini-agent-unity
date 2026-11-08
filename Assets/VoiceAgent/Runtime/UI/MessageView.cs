using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VoiceAgent.UI
{
    /// <summary>A chat bubble that wraps at a max width while letting short messages shrink to fit.</summary>
    public sealed class MessageView : MonoBehaviour
    {
        [SerializeField] TMP_Text m_Text;
        [SerializeField] LayoutElement m_TextLayout;
        [SerializeField] float m_MaxWidth = 540f;

        public void SetText(string text)
        {
            m_Text.text = text;
            Refresh();
        }

        void Refresh()
        {
            // The width of the text on one line (the RTL-shaped text for Arabic), capped at the bubble's max width.
            var width = m_Text.GetPreferredValues(m_Text.text, float.PositiveInfinity, float.PositiveInfinity).x;
            m_TextLayout.preferredWidth = Mathf.Min(m_MaxWidth, width + 6);
        }
    }
}
