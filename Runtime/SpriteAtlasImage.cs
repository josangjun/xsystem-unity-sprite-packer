using UnityEngine.U2D;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityEngine.UI
{
    [AddComponentMenu("SpriteAtlasImage")]
    public class SpriteAtlasImage : Image
    {
        [SerializeField]
        private AssetReferenceAtlasedSprite spriteReference;

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
                sprite = newSprite;
        }
        
        public override void SetMaterialDirty()
        {
            base.SetMaterialDirty();

            UpdateSprite();
        }
        
        protected override void OnValidate()
        {
            base.OnValidate();

            UpdateSprite();
        }
    }
}
