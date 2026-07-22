using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace UnityEngine
{
    /// <summary>
    /// Reassigns an Addressables atlased sprite to a required <see cref="SpriteRenderer"/>.
    /// This does not retain a direct <see cref="SpriteAtlas"/> dependency or use
    /// <see cref="SpriteAtlasManager.atlasRequested"/> later binding.
    /// </summary>
    [AddComponentMenu("SpriteAtlasBinder")]
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class SpriteAtlasBinder : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;

#if UNITY_EDITOR
        private Sprite _lastObservedSprite;
        private string _lastObservedReferenceKey;
#endif

        [SerializeField]
        private AssetReferenceSprite spriteReference;

        /// <summary>
        /// The Addressables reference used to load the atlased sprite.
        /// </summary>
        public AssetReferenceSprite SpriteReference => spriteReference;

        private void Awake()
        {
            ReconnectSprite();
        }

        /// <summary>
        /// Loads the configured sprite reference and assigns it to the required renderer.
        /// </summary>
        public void ReconnectSprite()
        {
            if (spriteReference == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!string.IsNullOrEmpty(spriteReference.SubObjectGUID))
                {
                    Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetByGUID<Sprite>(
                        new UnityEditor.GUID(spriteReference.SubObjectGUID));
                    SetSprite(editorSprite);
                }

                return;
            }
#endif

            Sprite loadedSprite = spriteReference.Asset as Sprite;
            if (loadedSprite != null && loadedSprite.texture != null)
            {
                SetSprite(loadedSprite);
                return;
            }

            if (spriteReference.OperationHandle.IsValid())
            {
                spriteReference.ReleaseAsset();
            }

            AsyncOperationHandle<Sprite> handle = spriteReference.LoadAssetAsync();
            handle.Completed += completedHandle =>
            {
                if (completedHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    SetSprite(completedHandle.Result);
                }
            };

#if !UNITY_WEBGL
            handle.WaitForCompletion();
#endif
        }

        private void OnDisable()
        {
            if (spriteReference != null && spriteReference.OperationHandle.IsValid())
            {
                spriteReference.ReleaseAsset();
            }
        }

        private void SetSprite(Sprite newSprite)
        {
            SpriteRenderer targetRenderer = GetSpriteRenderer();
            if (targetRenderer != null && newSprite != null && targetRenderer.sprite != newSprite)
            {
                targetRenderer.sprite = newSprite;
            }
        }

        private SpriteRenderer GetSpriteRenderer()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            return _spriteRenderer;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying)
            {
                SynchronizeSpriteReference();
            }
        }

        private void OnValidate()
        {
            SynchronizeSpriteReference();
        }

        private void SynchronizeSpriteReference()
        {
            SpriteRenderer targetRenderer = GetSpriteRenderer();
            Sprite currentSprite = targetRenderer != null ? targetRenderer.sprite : null;
            string currentReferenceKey = GetReferenceKey();

            if (_lastObservedReferenceKey != currentReferenceKey)
            {
                _lastObservedReferenceKey = currentReferenceKey;
                ReconnectSprite();
                return;
            }

            if (_lastObservedSprite == currentSprite)
            {
                return;
            }

            _lastObservedSprite = currentSprite;
            UpdateSpriteReferenceFromSprite(currentSprite);
            _lastObservedReferenceKey = GetReferenceKey();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private string GetReferenceKey()
        {
            if (spriteReference == null)
            {
                return null;
            }

            return spriteReference.AssetGUID + ":" + spriteReference.SubObjectGUID;
        }

        /// <summary>
        /// Finds the atlas containing <paramref name="newSprite"/> and creates an
        /// <see cref="AssetReferenceSprite"/> with that atlas and sub-object.
        /// </summary>
        public void UpdateSpriteReferenceFromSprite(Sprite newSprite)
        {
            if (newSprite == null)
            {
                spriteReference = null;
                return;
            }

            SpriteAtlas targetAtlas = null;
            string atlasGuid = null;

            if (spriteReference != null && !string.IsNullOrEmpty(spriteReference.AssetGUID))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(spriteReference.AssetGUID);
                SpriteAtlas currentAtlas = UnityEditor.AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (currentAtlas != null && currentAtlas.CanBindTo(newSprite))
                {
                    targetAtlas = currentAtlas;
                    atlasGuid = spriteReference.AssetGUID;
                }
            }

            if (targetAtlas == null)
            {
                string[] atlasGuids = UnityEditor.AssetDatabase.FindAssets("t:SpriteAtlas");
                foreach (string guid in atlasGuids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    SpriteAtlas atlas = UnityEditor.AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
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

            if (spriteReference == null || spriteReference.AssetGUID != atlasGuid)
            {
                spriteReference = new AssetReferenceSprite(atlasGuid);
                spriteReference.SetEditorAsset(targetAtlas);
            }

            spriteReference.SetEditorSubObject(newSprite);
        }
#endif
    }
}
