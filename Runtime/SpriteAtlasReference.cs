using UnityEngine.U2D;

namespace UnityEngine.AddressableAssets
{
    [System.Serializable]
    public class SpriteAtlasReference : AssetReferenceT<SpriteAtlas>
    {
        public SpriteAtlasReference(string guid) : base(guid) { }
        
        public async Awaitable<Sprite> GetSpriteAsync(string name)
        {
            if (!RuntimeKeyIsValid())
            {
                Debug.LogWarning($"Orbit item '{name}' has no valid atlas reference assigned.");
                return null;
            }
            
            SpriteAtlas atlas;
            if (OperationHandle.IsValid())
            {
                if (Asset)
                {
                    atlas = Asset as SpriteAtlas;
                }
                else if (OperationHandle.IsDone)
                {
                    atlas = OperationHandle.Result as SpriteAtlas;
                }
                else
                {
                    await OperationHandle.Task;
                    if (OperationHandle.Status == ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        atlas = OperationHandle.Result as SpriteAtlas;
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to load sprite atlas for orbit item '{base.AssetGUID}'.");
                        return null;
                    }
                }
            }
            else
            {
                var handle = LoadAssetAsync<SpriteAtlas>();
                await handle.Task;
                if (handle.Status == ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    atlas = handle.Result;
                }
                else
                {
                    Debug.LogWarning($"Failed to load sprite atlas for orbit item '{base.AssetGUID}'.");
                    return null;
                }
            }
            
            if (atlas == null)
            {
                Debug.LogWarning($"Failed to load sprite atlas for orbit item '{base.AssetGUID}'.");
                return null;
            }
            
            var sprite = atlas.GetSprite(name);
            
            if (sprite == null)
            {
                Debug.LogWarning($"Sprite '{name}' not found in atlas for orbit item '{base.AssetGUID}'.");
            }
            return sprite;
        }
    }
}