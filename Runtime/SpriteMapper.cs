

using UnityEngine;
using UnityEngine.U2D;

namespace XSystem
{
    [DisallowMultipleComponent]
    public class SpriteMapper : MonoBehaviour
    {
        private static SpriteMapper _instance;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad), UnityEngine.Scripting.Preserve]
        private static void ListenAtlasRequested()
        {
            Allocate();
        }
        
        public static void Allocate()
        {
            if (_instance == null)
            {
                var go = new GameObject("SpriteMapper");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SpriteMapper>();
            }
        }
        
        [UnityEngine.Scripting.Preserve]
        public static void Destroy()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }
                
        private void OnAtlasRequested(string tag, System.Action<SpriteAtlas> callback)
        {
            // AtlasImage를 통해 직접할당되도록 하고 여기서는 아무것도 하지 않음. 경고메시지를 생략하기 위한 용도.
        }
        
        private void OnEnable()
        {
            SpriteAtlasManager.atlasRequested += OnAtlasRequested;
        }
        
        private void OnDisable()
        {
            SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
        }
    }
}