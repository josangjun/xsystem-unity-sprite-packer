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

        private AsyncOperationHandle<Sprite> _spriteLoadHandle;
        private AssetReferenceSprite _loadedSpriteReference;
        private bool _isUpdatingSprite;
        private bool _isEnabled;
        private bool _isQuitting;

        public void UpdateSprite(bool forceUpdate)
        {
            if (forceUpdate)
            {
                _isUpdatingSprite = true;
                try
                {
                    ReleaseSprite();
                    sprite = null;
                }
                finally
                {
                    _isUpdatingSprite = false;
                }
            }
            UpdateSprite();
        }

        public void UpdateSprite()
        {
            if (this == null || _isUpdatingSprite || _isQuitting || spriteReference == null
                || (Application.isPlaying && !_isEnabled))
            {
                return;
            }

            _isUpdatingSprite = true;
            try
            {
                UpdateSpriteCore();
            }
            finally
            {
                _isUpdatingSprite = false;
            }
        }

        private void UpdateSpriteCore()
        {
            if (_loadedSpriteReference != null && _loadedSpriteReference != spriteReference)
            {
                ReleaseSprite();
            }

            var sprite = spriteReference.Asset as Sprite;
            if (sprite == null || sprite.texture == null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    #if UNITY_6000_6_OR_NEWER
                    sprite = UnityEditor.AssetDatabase.LoadAssetByGUID<Sprite>(new UnityEngine.GUID(spriteReference.SubObjectGUID));
                    #else
                    sprite = UnityEditor.AssetDatabase.LoadAssetByGUID<Sprite>(new UnityEditor.GUID(spriteReference.SubObjectGUID));
                    #endif
                    SetSprite(sprite);
                    return;
                }
#endif

                // Material dirties can recur while a load is pending. Keep one request
                // and a removable callback for this component's enabled lifetime.
                if (_spriteLoadHandle.IsValid())
                {
                    return;
                }

                _loadedSpriteReference = spriteReference;
                _spriteLoadHandle = spriteReference.OperationHandle.IsValid()
                    ? spriteReference.OperationHandle.Convert<Sprite>()
                    : spriteReference.LoadAssetAsync();
                _spriteLoadHandle.Completed += OnSpriteLoaded;
#if !UNITY_WEBGL
                _spriteLoadHandle.WaitForCompletion();
#endif
            }
            else
            {
                SetSprite(sprite);
            }
        }

        protected override void OnEnable()
        {
            _isEnabled = true;
            _isQuitting = false;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _isEnabled = false;
            base.OnDisable();
            ReleaseSprite();
        }

        protected override void OnDestroy()
        {
            _isEnabled = false;
            ReleaseSprite();
            base.OnDestroy();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            ReleaseSprite();
        }

        private void OnSpriteLoaded(AsyncOperationHandle<Sprite> handle)
        {
            if (this == null || !_isEnabled || _isQuitting || !handle.IsValid()
                || !_spriteLoadHandle.Equals(handle))
            {
                return;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                SetSprite(handle.Result);
            }
        }

        private void ReleaseSprite()
        {
            // Detach before releasing: Addressables may defer completion until after
            // OnDisable/OnDestroy, including when exiting Play Mode without reloads.
            if (_spriteLoadHandle.IsValid())
            {
                _spriteLoadHandle.Completed -= OnSpriteLoaded;
            }

            _spriteLoadHandle = default;
            var loadedReference = _loadedSpriteReference;
            _loadedSpriteReference = null;
            if (loadedReference != null && loadedReference.OperationHandle.IsValid())
            {
                loadedReference.ReleaseAsset();
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
