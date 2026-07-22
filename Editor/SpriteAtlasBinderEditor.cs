using UnityEditor;
using UnityEngine;

namespace XSystem.Editor
{
    [CustomEditor(typeof(SpriteAtlasBinder), true)]
    public class SpriteAtlasBinderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                foreach (Object selectedTarget in targets)
                {
                    SpriteAtlasBinder binder = selectedTarget as SpriteAtlasBinder;
                    if (binder == null)
                    {
                        continue;
                    }

                    binder.ReconnectSprite();
                    EditorUtility.SetDirty(binder);
                }
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
