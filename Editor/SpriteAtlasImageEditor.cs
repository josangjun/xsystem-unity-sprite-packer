

using UnityEditor;
using UnityEditor.UI;
using UnityEngine.UI;

namespace XSystem.Editor
{
    [CustomEditor(typeof(SpriteAtlasImage), true)]
    public class SpriteAtlasImageEditor : ImageEditor
    {
        SerializedProperty m_SpriteReference;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            m_SpriteReference = serializedObject.FindProperty("spriteReference");
        }

        public override void OnInspectorGUI()
        {
            var targetImage = target as SpriteAtlasImage;
            
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_SpriteReference);
            
            serializedObject.ApplyModifiedProperties();
            
            targetImage.UpdateSprite();
            
            base.OnInspectorGUI();
        }
    }
}
