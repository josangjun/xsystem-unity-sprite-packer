

using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using UnityEngine.AddressableAssets;

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
            
            EditorGUI.BeginChangeCheck();

            var subAssetGuidProp = m_SpriteReference.FindPropertyRelative("m_SubObjectGUID");
            
            EditorGUILayout.PropertyField(m_SpriteReference);
            
            base.OnInspectorGUI();
                        
            if (EditorGUI.EndChangeCheck())
            {
                SerializedProperty spriteProp = serializedObject.FindProperty("m_Sprite");
                Sprite newSprite = spriteProp.objectReferenceValue as Sprite;

                UpdateSpriteReferenceInSerializedObject(serializedObject, newSprite);    
            }
            else
            {
                serializedObject.Update();
            }
            serializedObject.ApplyModifiedProperties();
            targetImage.UpdateSprite(true);
        }

        [MenuItem("CONTEXT/Image/Convert to SpriteAtlasImage", true)]
        private static bool ValidateConvertToSpriteAtlasImage(MenuCommand command)
        {
            Image image = command.context as Image;
            return image != null && !(image is SpriteAtlasImage);
        }

        [MenuItem("CONTEXT/Image/Convert to SpriteAtlasImage", false, 100)]
        private static void ConvertToSpriteAtlasImage(MenuCommand command)
        {
            Image image = command.context as Image;
            if (image == null) return;

            GameObject go = image.gameObject;
            Sprite sprite = image.sprite;

            // Find the SpriteAtlasImage MonoScript using MonoImporter
            MonoScript spriteAtlasImageScript = null;
            foreach (var script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script.GetClass() == typeof(SpriteAtlasImage))
                {
                    spriteAtlasImageScript = script;
                    break;
                }
            }

            if (spriteAtlasImageScript == null)
            {
                Debug.LogError("SpriteAtlasImage script not found in compiled assemblies!");
                return;
            }

            Undo.RegisterCompleteObjectUndo(go, "Convert Image to SpriteAtlasImage");

            SerializedObject so = new SerializedObject(image);
            SerializedProperty scriptProp = so.FindProperty("m_Script");
            if (scriptProp != null)
            {
                scriptProp.objectReferenceValue = spriteAtlasImageScript;
                so.ApplyModifiedProperties();
            }

            SpriteAtlasImage spriteAtlasImage = go.GetComponent<SpriteAtlasImage>();
            if (spriteAtlasImage == null)
            {
                Debug.LogError("Failed to retrieve converted SpriteAtlasImage component!");
                return;
            }

            if (sprite != null)
            {
                var spriteAtlasImageSO = new SerializedObject(spriteAtlasImage);
                UpdateSpriteReferenceInSerializedObject(spriteAtlasImageSO, sprite);
                spriteAtlasImageSO.ApplyModifiedProperties();

                EditorUtility.SetDirty(spriteAtlasImage);
                spriteAtlasImage.UpdateSprite(true);

                var field = typeof(SpriteAtlasImage).GetField("spriteReference", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var val = field.GetValue(spriteAtlasImage) as AssetReferenceSprite;
                    if (val == null || string.IsNullOrEmpty(val.AssetGUID))
                    {
                        Debug.LogWarning($"Sprite '{sprite.name}' is not packed in any SpriteAtlas. Converted component, but could not set SpriteAtlas reference.");
                    }
                }
            }
        }

        private static void UpdateSpriteReferenceInSerializedObject(SerializedObject so, Sprite newSprite)
        {
            SerializedProperty spriteRefProp = so.FindProperty("spriteReference");
            if (spriteRefProp == null) return;

            if (newSprite == null)
            {
                NullifySpriteReference(spriteRefProp);
                return;
            }

            SpriteAtlas targetAtlas = null;
            string atlasGuid = null;

            string existingAssetGuid = spriteRefProp.FindPropertyRelative("m_AssetGUID").stringValue;
            if (!string.IsNullOrEmpty(existingAssetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(existingAssetGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    if (atlas != null && atlas.CanBindTo(newSprite))
                    {
                        targetAtlas = atlas;
                        atlasGuid = existingAssetGuid;
                    }
                }
            }

            if (targetAtlas == null)
            {
                string[] atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");
                foreach (string guid in atlasGuids)
                {
                    if (guid == existingAssetGuid)
                        continue;

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    if (atlas != null && atlas.CanBindTo(newSprite))
                    {
                        targetAtlas = atlas;
                        atlasGuid = guid;
                        break;
                    }
                }
            }

            if (targetAtlas == null)
            {
                NullifySpriteReference(spriteRefProp);
                return;
            }

            UpdateSpriteReferenceSubObject(spriteRefProp, targetAtlas, atlasGuid, newSprite);
        }
        
        private static void NullifySpriteReference(SerializedProperty spriteRefProp)
        {
            spriteRefProp.FindPropertyRelative("m_AssetGUID").stringValue = string.Empty;
            spriteRefProp.FindPropertyRelative("m_SubObjectName").stringValue = string.Empty;
            spriteRefProp.FindPropertyRelative("m_SubObjectType").stringValue = string.Empty;
            spriteRefProp.FindPropertyRelative("m_SubObjectGUID").stringValue = string.Empty;
            spriteRefProp.FindPropertyRelative("m_EditorAssetChanged").boolValue = true;
        }

        private static void UpdateSpriteReferenceSubObject(SerializedProperty spriteRefProp, SpriteAtlas targetAtlas, string atlasGuid, Sprite newSprite)
        {
            var assetRef = new AssetReferenceSprite(atlasGuid);
            if (assetRef.SetEditorAsset(targetAtlas) && assetRef.SetEditorSubObject(newSprite))
            {
                spriteRefProp.FindPropertyRelative("m_AssetGUID").stringValue = atlasGuid;
                spriteRefProp.FindPropertyRelative("m_SubObjectName").stringValue = assetRef.SubObjectName ?? newSprite.name;
                spriteRefProp.FindPropertyRelative("m_SubObjectType").stringValue = typeof(Sprite).AssemblyQualifiedName;
                spriteRefProp.FindPropertyRelative("m_SubObjectGUID").stringValue = assetRef.SubObjectGUID;
                spriteRefProp.FindPropertyRelative("m_EditorAssetChanged").boolValue = true;
                return;
            }

            string spritePath = AssetDatabase.GetAssetPath(newSprite);
            string spriteGuid = AssetDatabase.AssetPathToGUID(spritePath);

            spriteRefProp.FindPropertyRelative("m_AssetGUID").stringValue = atlasGuid;
            spriteRefProp.FindPropertyRelative("m_SubObjectName").stringValue = newSprite.name;
            spriteRefProp.FindPropertyRelative("m_SubObjectType").stringValue = typeof(Sprite).AssemblyQualifiedName;
            spriteRefProp.FindPropertyRelative("m_SubObjectGUID").stringValue = spriteGuid;
            spriteRefProp.FindPropertyRelative("m_EditorAssetChanged").boolValue = true;
        }
    }
}

