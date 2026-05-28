using UnityEngine;
using UnityEngine.UI;

namespace XSystem
{
    [AddComponentMenu("AtlasImage")]
    public class AtlasImage : Image
    {
        [SerializeField]
        private SpriteAtlasManifest m_Atlas;
        
        public SpriteAtlasManifest Atlas => m_Atlas;
        
        [SerializeField]
        private string m_SpriteName;
        
        public string SpriteName
        {
            get => m_SpriteName;
            set
            {
                m_SpriteName = value;
                UpdateSprite();
            }
        }
        
        public void UpdateSprite()
        {
            Sprite newSprite = (m_Atlas != null && !string.IsNullOrEmpty(m_SpriteName)) 
                ? m_Atlas.GetSprite(m_SpriteName) 
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