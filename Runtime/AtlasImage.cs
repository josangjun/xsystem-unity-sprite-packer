using UnityEngine;
using UnityEngine.UI;

namespace XSystem
{
    [AddComponentMenu("AtlasImage")]
    public class AtlasImage : Image
    {
        public SpriteAtlasManifest atlas;

        public string spriteName;
        
        public void UpdateSprite()
        {
            Sprite newSprite = (atlas != null && !string.IsNullOrEmpty(spriteName)) 
                ? atlas.GetSprite(spriteName) 
                : null;

            if (sprite != newSprite)
                sprite = newSprite;
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
#endif
    }
}