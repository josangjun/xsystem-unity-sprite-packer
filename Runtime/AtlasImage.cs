using UnityEngine;
using UnityEngine.UI;

namespace XSystem
{
    public class AtlasImage : Image
    {
        public SpriteAtlasPack atlasPack;

        public string spriteName;
        
        public void UpdateSprite()
        {
            Sprite newSprite = (atlasPack != null && !string.IsNullOrEmpty(spriteName)) 
                ? atlasPack.GetSprite(spriteName) 
                : null;

            if (sprite != newSprite)
                sprite = newSprite;
        }
        
        public override void SetMaterialDirty()
        {
            base.SetMaterialDirty();
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