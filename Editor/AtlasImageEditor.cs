using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using System.Linq;

namespace XSystem
{
    [CustomEditor(typeof(AtlasImage), true)]
    [CanEditMultipleObjects]
    public class AtlasImageEditor : ImageEditor
    {
        SerializedProperty atlasPackProp;
        SerializedProperty spriteNameProp;
        SerializedProperty colorProp;
        SerializedProperty materialProp;
        SerializedProperty raycastTargetProp;
        SerializedProperty raycastPaddingProp;
        SerializedProperty maskableProp;

        private string spriteSearch = string.Empty;

        protected override void OnEnable()
        {
            base.OnEnable();
            atlasPackProp = serializedObject.FindProperty("atlasPack");
            spriteNameProp = serializedObject.FindProperty("spriteName");
            colorProp = serializedObject.FindProperty("m_Color");
            materialProp = serializedObject.FindProperty("m_Material");
            raycastTargetProp = serializedObject.FindProperty("m_RaycastTarget");
            raycastPaddingProp = serializedObject.FindProperty("m_RaycastPadding");
            maskableProp = serializedObject.FindProperty("m_Maskable");

            // 에디터가 활성화될 때 (예: 어셈블리 리로드 후) Sprite를 명시적으로 업데이트하여
            // 에디터 화면에 올바르게 표시되도록 합니다.
            ((AtlasImage)target).UpdateSprite();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(atlasPackProp, new GUIContent("Atlas Pack"));

            // SerializedProperty의 objectReferenceValue를 직접 사용하여 PropertyField가 변경한 최신 값을 즉시 반영합니다.
            // 이렇게 하면 ApplyModifiedProperties() 호출 이전에 Atlas Pack이 할당되었는지 확인할 수 있습니다.
            SpriteAtlasPack currentAtlasPack = atlasPackProp.objectReferenceValue as SpriteAtlasPack;
            if (currentAtlasPack != null && !atlasPackProp.hasMultipleDifferentValues)
            {
                spriteSearch = EditorGUILayout.TextField("Search Sprite", spriteSearch);

                // 아틀라스에 포함된 스프라이트 이름들을 드롭다운으로 표시
                var allNames = currentAtlasPack.entries.Select(e => e.spriteName).ToArray();
                var filteredNames = allNames
                    .Where(n => string.IsNullOrEmpty(spriteSearch) || n.IndexOf(spriteSearch, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

                if (allNames.Length > 0)
                {
                    int currentIndex = System.Array.IndexOf(filteredNames, spriteNameProp.stringValue);
                    int newIndex = EditorGUILayout.Popup("Sprite Name", currentIndex, filteredNames);
                    if (newIndex >= 0 && newIndex < filteredNames.Length)
                    {
                        spriteNameProp.stringValue = filteredNames[newIndex];
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Atlas Pack에 등록된 스프라이트가 없습니다. Rebuild를 먼저 진행하세요.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.PropertyField(spriteNameProp);
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Image Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(colorProp);
            EditorGUILayout.PropertyField(materialProp);
            EditorGUILayout.PropertyField(raycastTargetProp);
            EditorGUILayout.PropertyField(raycastPaddingProp);
            EditorGUILayout.PropertyField(maskableProp);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target); // AtlasImage 컴포넌트가 변경되었음을 에디터에 알립니다.
        }
    }
}