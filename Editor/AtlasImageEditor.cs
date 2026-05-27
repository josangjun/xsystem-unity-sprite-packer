using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace XSystem.Editor
{
    [CustomEditor(typeof(AtlasImage), true)]
    [CanEditMultipleObjects]
    public class AtlasImageEditor : ImageEditor
    {
        SerializedProperty m_AtlasPack;
        SerializedProperty m_SpriteName;
        SerializedProperty m_Sprite;
        
        SerializedProperty m_Type;
        SerializedProperty m_PreserveAspect;
        SerializedProperty m_UseSpriteMesh;
        SerializedProperty m_FillMethod;
        SerializedProperty m_FillAmount;
        SerializedProperty m_FillOrigin;
        SerializedProperty m_FillClockwise;

        private string m_SearchText = "";
        private List<string> m_SpriteNames = new List<string>();

        protected override void OnEnable()
        {
            base.OnEnable();
            m_AtlasPack = serializedObject.FindProperty("atlas");
            m_SpriteName = serializedObject.FindProperty("spriteName");
            m_Sprite = serializedObject.FindProperty("m_Sprite");
            
            m_Type = serializedObject.FindProperty("m_Type");
            m_PreserveAspect = serializedObject.FindProperty("m_PreserveAspect");
            m_UseSpriteMesh = serializedObject.FindProperty("m_UseSpriteMesh");
            m_FillMethod = serializedObject.FindProperty("m_FillMethod");
            m_FillAmount = serializedObject.FindProperty("m_FillAmount");
            m_FillOrigin = serializedObject.FindProperty("m_FillOrigin");
            m_FillClockwise = serializedObject.FindProperty("m_FillClockwise");
            
            UpdateSpriteNames();
        }

        private void UpdateSpriteNames()
        {
            m_SpriteNames.Clear();
            var atlas = m_AtlasPack.objectReferenceValue as SpriteAtlasManifest;
            if (atlas == null) return;

            // SpriteAtlasPack의 entries 리스트에서 스프라이트 이름을 가져옵니다.
            var so = new SerializedObject(atlas);
            var entries = so.FindProperty("entries");
            if (entries != null && entries.isArray)
            {
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var spriteName = string.Empty;
                    var stringProp = entry.FindPropertyRelative("spriteName");
                    if (stringProp != null) spriteName = stringProp.stringValue;
                    
                    m_SpriteNames.Add(spriteName);
                }
            }
            m_SpriteNames.Sort();
        }

        private bool TryFindSpriteNameByAssignedSprite(SpriteAtlasManifest atlas, Sprite assignedSprite, out string mappedSpriteName)
        {
            mappedSpriteName = null;
            if (atlas == null || assignedSprite == null || atlas.entries == null)
                return false;

            if (atlas.atlasTexture == null)
                return false;

            string atlasPath = AssetDatabase.GetAssetPath(atlas.atlasTexture);
            if (string.IsNullOrEmpty(atlasPath))
                return false;

            var atlasSprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath).OfType<Sprite>().ToArray();
            if (atlasSprites.Length == 0)
                return false;

            string assignedGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(assignedSprite).ToString();
            bool isSpriteInsideAtlas = atlasSprites.Any(s => GlobalObjectId.GetGlobalObjectIdSlow(s).ToString() == assignedGlobalId);
            if (!isSpriteInsideAtlas)
                return false;

            string assignedName = assignedSprite.name;

            foreach (var entry in atlas.entries)
            {
                if (entry == null) continue;

                if (entry.sprite == assignedSprite || entry.sourceSprite == assignedSprite)
                {
                    mappedSpriteName = entry.spriteName;
                    return !string.IsNullOrEmpty(mappedSpriteName);
                }

                bool atlasNameMatch = !string.IsNullOrEmpty(entry.spriteName) && entry.spriteName == assignedName;
                if (atlasNameMatch)
                {
                    mappedSpriteName = entry.spriteName;
                    return !string.IsNullOrEmpty(mappedSpriteName);
                }
            }

            return false;
        }

        private void SyncSpriteNameFromAssignedSpriteIfNeeded()
        {
            if (serializedObject.isEditingMultipleObjects) return;

            var atlasImage = target as AtlasImage;
            if (atlasImage == null || atlasImage.atlas == null || atlasImage.sprite == null) return;

            if (TryFindSpriteNameByAssignedSprite(atlasImage.atlas, (Sprite)m_Sprite.objectReferenceValue, out var mappedSpriteName) &&
                !string.Equals(atlasImage.spriteName, mappedSpriteName))
            {
                m_SpriteName.stringValue = mappedSpriteName;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Atlas Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_AtlasPack);
            if (EditorGUI.EndChangeCheck()) UpdateSpriteNames();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_Sprite);
            if (EditorGUI.EndChangeCheck())
            {
                SyncSpriteNameFromAssignedSpriteIfNeeded();
            }

            if (m_AtlasPack.objectReferenceValue != null)
            {
                if (m_SpriteNames.Count == 0) UpdateSpriteNames();

                // 검색어가 비어있다면 현재 선택된 스프라이트 이름을 라벨에 표시
                // string searchLabel = string.IsNullOrEmpty(m_SearchText) && !string.IsNullOrEmpty(m_SpriteName.stringValue) 
                //     ? $"Sprite Search ({m_SpriteName.stringValue})" 
                //     : "Sprite Search";
                var searchLabel = "Sprite Search";

                m_SearchText = EditorGUILayout.TextField(searchLabel, m_SearchText);

                // 검색어에 따른 필터링된 스프라이트 목록 (검색어가 없으면 전체 목록 반환)
                string[] filtered = m_SpriteNames
                    .Where(n => string.IsNullOrEmpty(m_SearchText.Trim()) || n.ToLower().Contains(m_SearchText.ToLower()))
                    .ToArray();

                // 드롭다운 리스트 표시: 현재 spriteName의 인덱스를 찾아 기본 선택값으로 지정
                if (filtered.Length > 0)
                {
                    int currentIndex = System.Array.IndexOf(filtered, m_SpriteName.stringValue);
                    int newIndex = EditorGUILayout.Popup("Sprite Name", currentIndex, filtered);
                    
                    if (newIndex >= 0)
                        m_SpriteName.stringValue = filtered[newIndex];
                }
                else {
                    EditorGUILayout.HelpBox("No sprites found matching search.", MessageType.None);
                }
            }
            else EditorGUILayout.PropertyField(m_SpriteName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Image Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_Color);
            EditorGUILayout.PropertyField(m_Material);
            EditorGUILayout.PropertyField(m_RaycastTarget);
            EditorGUILayout.PropertyField(m_RaycastPadding);
            EditorGUILayout.PropertyField(m_Maskable);
            EditorGUILayout.PropertyField(m_Type);

            Image.Type type = (Image.Type)m_Type.enumValueIndex;
            if (type == Image.Type.Filled)
            {
                EditorGUILayout.PropertyField(m_FillMethod);
                EditorGUILayout.PropertyField(m_FillOrigin);
                EditorGUILayout.PropertyField(m_FillAmount);
                EditorGUILayout.PropertyField(m_FillClockwise);
            }
            else
            {
                EditorGUILayout.PropertyField(m_PreserveAspect);
                EditorGUILayout.PropertyField(m_UseSpriteMesh);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Set Native Size"))
            {
                foreach (var t in targets) { ((AtlasImage)t).SetNativeSize(); EditorUtility.SetDirty(t); }
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var target in targets) ((AtlasImage)target).UpdateSprite();
            }
        }
    }
}