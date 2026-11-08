using UnityEditor;

namespace VoiceAgent.Editor.Localization
{
    /// <summary>The RTL Text Mesh Pro inspector (Preserve Numbers, Farsi, Fix Tags, Force Fix) for <see cref="AutoRtlText"/>.</summary>
    [CustomEditor(typeof(AutoRtlText)), CanEditMultipleObjects]
    public sealed class AutoRtlTextEditor : RTLTMPro.RTLTextMeshProEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Force Fix and the left/right alignment follow the text: on while it contains Arabic letters (AutoRtlText).", MessageType.None);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_MirrorAlignment"));
            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }
    }
}
