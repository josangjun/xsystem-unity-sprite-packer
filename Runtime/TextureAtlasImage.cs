using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.UI
{
    [AddComponentMenu("TextureAtlasImage")]
    public class TextureAtlasImage : Image
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

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            base.OnPopulateMesh(vertexHelper);
            AtlasSpriteEntry entry = m_Atlas != null ? m_Atlas.GetEntry(m_SpriteName) : null;
            Sprite displayedSprite = overrideSprite;
            if (entry == null || !entry.packedRotated || displayedSprite == null || displayedSprite != sprite)
            {
                return;
            }
            if (type == Type.Simple && useSpriteMesh)
            {
                // The imported Sprite mesh already carries the corrected UVs.
                return;
            }

            Vector2 textureSize = new Vector2(displayedSprite.texture.width, displayedSprite.texture.height);
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                Vector2 uv = SpriteAtlasRotationUtility.MapUV(
                    vertex.uv0, displayedSprite.rect, entry.atlasRect, textureSize);
                vertex.uv0 = new Vector4(uv.x, uv.y, vertex.uv0.z, vertex.uv0.w);
                vertexHelper.SetUIVertex(vertex, i);
            }
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
