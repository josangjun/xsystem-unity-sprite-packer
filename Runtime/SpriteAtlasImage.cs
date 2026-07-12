using UnityEngine.U2D;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityEngine.UI
{
    [AddComponentMenu("SpriteAtlasImage")]
    public class SpriteAtlasImage : Image
    {
        [SerializeField]
        private AssetReferenceSprite spriteReference;

        public void UpdateSprite(bool forceUpdate)
        {
            if (forceUpdate)
            {
                sprite = null;
                if (spriteReference.OperationHandle.IsValid())
                {
                    spriteReference.ReleaseAsset();
                }
            }
            UpdateSprite();
        }

        public void UpdateSprite()
        {
            if (spriteReference == null)
                return;

            var sprite = spriteReference.Asset as Sprite;
            if (sprite == null || sprite.texture == null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    sprite = UnityEditor.AssetDatabase.LoadAssetByGUID<Sprite>(new UnityEditor.GUID(spriteReference.SubObjectGUID));
                    SetSprite(sprite);
                    return;
                }
#endif

                if (spriteReference.OperationHandle.IsValid())
                {
                    spriteReference.ReleaseAsset();
                }
                var h = spriteReference.LoadAssetAsync();
                h.Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        var newSprite = handle.Result;
                        SetSprite(newSprite);
                    }
                };
#if !UNITY_WEBGL
                h.WaitForCompletion();
#endif
            }
            else
            {
                SetSprite(sprite);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (spriteReference.OperationHandle.IsValid())
            {
                spriteReference.ReleaseAsset();
            }
        }

        private void SetSprite(Sprite newSprite)
        {
            if (sprite != newSprite)
            {
                sprite = newSprite;
            }
        }

        public override void SetMaterialDirty()
        {
            base.SetMaterialDirty();

            UpdateSprite();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            UpdateSprite();
        }

        public void UpdateSpriteReferenceFromSprite(Sprite newSprite)
        {
            if (newSprite == null)
            {
                spriteReference = null;
                return;
            }

            SpriteAtlas targetAtlas = null;
            string atlasGuid = null;

            // Try checking the currently referenced SpriteAtlas first for O(1) optimization
            if (spriteReference != null && !string.IsNullOrEmpty(spriteReference.AssetGUID))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(spriteReference.AssetGUID);
                if (!string.IsNullOrEmpty(path))
                {
                    var atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path);
                    if (atlas != null && atlas.CanBindTo(newSprite))
                    {
                        targetAtlas = atlas;
                        atlasGuid = spriteReference.AssetGUID;
                    }
                }
            }

            // Fallback: search all other SpriteAtlases in the project
            if (targetAtlas == null)
            {
                string[] atlasGuids = UnityEditor.AssetDatabase.FindAssets("t:SpriteAtlas");
                foreach (string guid in atlasGuids)
                {
                    if (spriteReference != null && guid == spriteReference.AssetGUID)
                        continue;

                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path);
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
                spriteReference = null;
                return;
            }

            if (spriteReference != null && spriteReference.AssetGUID == atlasGuid)
            {
                spriteReference.SetEditorSubObject(newSprite);
            }
            else
            {
                spriteReference = new AssetReferenceSprite(atlasGuid);
                spriteReference.SetEditorAsset(targetAtlas);
                spriteReference.SetEditorSubObject(newSprite);
            }
        }
#endif
    }
}
