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
            if (atlasPack != null && !string.IsNullOrEmpty(spriteName))
            {
                sprite = atlasPack.GetSprite(spriteName);
            }
            else
            {
                sprite = null;
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
#endif
    }
}