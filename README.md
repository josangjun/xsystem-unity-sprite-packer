# Unity Sprite Packer

여러 Unity `Sprite` 에셋을 하나의 텍스처 아틀라스로 묶고, `ScriptableObject`를 통해 런타임에서 이름으로 조회하거나 UI에 표시할 수 있는 패키지입니다.

## 요구 사항

- Unity `6000.3` 이상
- `com.unity.ugui` `2.0.0`
- `SpriteAtlasImage`를 사용하려면 Addressables 패키지가 필요합니다.

## 주요 기능

- `SpriteAtlasManifest` 에셋으로 아틀라스 설정 관리
- 인스펙터에서 선택한 스프라이트를 항목에 추가
- `Texture2D.PackTextures`를 사용해 아틀라스 PNG 생성
- 원본 스프라이트의 피벗, 보더, 사각형 메타데이터 보존
- 항목별 `Source Scale (%)` 설정 지원
- 항목을 비우고 하위 스프라이트 메타데이터를 다시 생성해 아틀라스 재빌드
- 스프라이트 이름으로 항목 검색, 정렬, 개별 삭제
- 아틀라스에서 개별 스프라이트 PNG 추출
- 런타임에 `SpriteAtlasManifest.GetSprite(name)`으로 스프라이트 조회
- `TextureAtlasImage`를 사용한 아틀라스 기반 UI 표시
- `SpriteAtlasImage`와 `AssetReferenceAtlasedSprite`를 사용한 Addressables 기반 UI 지원
- `SpriteMapper`로 `SpriteAtlasManager.atlasRequested` 경고 억제

## 설치

### 로컬 패키지

Unity 프로젝트의 `Packages/` 폴더에 이 패키지 폴더를 넣으세요.

```text
Packages/xsystem.unityspritepacker
```

### Git UPM

`Packages/manifest.json`에 의존성을 추가하세요.

```json
{
  "dependencies": {
    "xsystem.unityspritepacker": "https://github.com/josangjun/xsystem-unity-sprite-packer.git"
  }
}
```

특정 태그나 커밋을 지정하려면 URL 뒤에 `#ref`를 붙이세요.

```text
https://github.com/josangjun/xsystem-unity-sprite-packer.git#v1.0.0
```

## 빠른 시작

1. Project 창에서 마우스 오른쪽 버튼을 누르고 `Create > XSystem > Sprite Atlas Manifest`를 선택합니다.
2. 생성된 `SpriteAtlasManifest` 에셋을 선택합니다.
3. Project 창에서 묶을 스프라이트를 선택한 뒤 인스펙터에서 `Add Selected Sprites`를 클릭합니다.
4. 필요에 따라 `Padding`, `Max Size`, `Allow Rotation`, `Source Scale (%)`을 조정합니다.
5. `Refresh Pivot/Border from Source Sprites`를 눌러 원본 메타데이터를 다시 동기화합니다.
6. `Sort Entries By Name`을 눌러 이름순으로 항목을 정렬합니다.
7. `Rebuild Atlas`를 눌러 매니페스트 에셋과 같은 폴더에 `<ManifestName>.png` 아틀라스를 생성합니다.

`Rebuild Atlas`는 생성된 PNG의 텍스처 타입을 `Sprite (Multiple)`로 설정하고 현재 항목을 기준으로 하위 스프라이트 메타데이터를 다시 만듭니다.

## 런타임에서 사용하기

`SpriteAtlasManifest`에서 이름으로 스프라이트를 가져옵니다.

```csharp
using UnityEngine;

public class CharacterIcon : MonoBehaviour
{
    [SerializeField]
    private SpriteAtlasManifest atlas;

    private void Start()
    {
        Sprite icon = atlas.GetSprite("HeroIdle");
    }
}
```

존재하지 않는 이름을 요청하면 경고를 기록하고 `null`을 반환합니다.

## UI 구성요소

### TextureAtlasImage

`TextureAtlasImage`는 `UnityEngine.UI.Image`를 상속한 구성요소로, `SpriteAtlasManifest`와 `Sprite Name`을 사용해 표시할 스프라이트를 자동으로 갱신합니다.

사용 방법:

1. UI GameObject에 `TextureAtlasImage` 구성요소를 추가합니다.
2. `Atlas`에 `SpriteAtlasManifest` 에셋을 할당합니다.
3. `Sprite Search`에서 스프라이트 이름을 검색합니다.
4. `Sprite Name` 드롭다운에서 표시할 스프라이트를 선택합니다.

코드에서 스프라이트를 변경할 수도 있습니다.

```csharp
using UnityEngine;

public class IconSwitcher : MonoBehaviour
{
    [SerializeField]
    private TextureAtlasImage image;

    public void SetIcon(string spriteName)
    {
        image.SpriteName = spriteName;
    }
}
```

### SpriteAtlasImage

`SpriteAtlasImage`는 Addressables의 `AssetReferenceAtlasedSprite`를 직접 참조하는 `Image` 파생 구성요소입니다.

### SpriteAtlasBinder

`SpriteAtlasBinder`는 필수 `SpriteRenderer`를 요구하는 `MonoBehaviour`입니다. `SpriteAtlasImage`와 마찬가지로 직접 `SpriteAtlas`를 참조하는 대신 Addressables의 `AssetReferenceSprite`를 저장해 아틀라스가 암시적인 AssetBundle 의존성이 되는 것을 방지합니다. 에디터에서는 `AssetDatabase`로 확인한 원본 스프라이트를 할당합니다. 스프라이트 참조를 바꾸면 필수 렌더러가 갱신되고, 필수 렌더러의 스프라이트를 바꾸면 참조가 갱신됩니다. Play Mode에서는 `Awake`에서 참조된 스프라이트를 로드해 필수 렌더러에 연결하며, 이후 `SpriteAtlasManager.atlasRequested` 이벤트 구독을 통한 바인딩은 사용하지 않습니다.

- 에디터에서는 GUID로 에셋을 불러와 미리보기를 갱신합니다.
- Play Mode에서는 Addressables 비동기 로딩을 사용합니다.
- 구성요소를 비활성화하면 유효한 Addressables 핸들을 해제합니다.

## 인스펙터 버튼

- `Add Selected Sprites`: Project 창에서 선택한 스프라이트를 매니페스트 항목에 추가합니다. 스프라이트를 선택하지 않았다면 스프라이트 선택 창을 엽니다.
- `Refresh Pivot/Border from Source Sprites`: 원본 임포터의 피벗, 보더, 사각형 값을 사용해 항목 메타데이터를 갱신합니다.
- `Sort Entries By Name`: `spriteName`을 기준으로 항목을 정렬합니다.
- `Rebuild Atlas`: 아틀라스 PNG와 하위 스프라이트 메타데이터를 다시 생성합니다.
- `Extract`: 선택한 항목의 아틀라스 영역을 PNG 파일로 저장합니다.
- `Remove`: 매니페스트에서 선택한 항목을 제거합니다.

## 참고 사항

- 원본 텍스처를 읽을 수 없는 경우 에디터 스크립트가 `isReadable`을 활성화하고 다시 임포트합니다.
- 재빌드 중 중복된 원본 스프라이트는 제거됩니다.
- `Allow Rotation`은 리플렉션으로 Unity 내부 `PackTextures` 오버로드를 호출합니다. 현재 Unity 버전에서 지원되지 않으면 회전 없이 패킹합니다.
- 항목의 `border` 값은 `Source Scale (%)`에 따라 조정되어 아틀라스 스프라이트 메타데이터에 반영됩니다.
- 생성된 아틀라스 PNG는 매니페스트 에셋과 같은 폴더에 저장됩니다.

## 패키지 구성

- `Runtime/SpriteAtlasManifest.cs`: 아틀라스 매니페스트, 항목 데이터, 런타임 스프라이트 조회
- `Runtime/TextureAtlasImage.cs`: 매니페스트 기반 UI 이미지 구성요소
- `Runtime/SpriteAtlasImage.cs`: Addressables 아틀라스 스프라이트 UI 이미지 구성요소
- `Runtime/SpriteAtlasBinder.cs`: `SpriteRenderer` 구성요소를 위한 Addressables 스프라이트 참조 바인더
- `Runtime/SpriteMapper.cs`: `SpriteAtlasManager.atlasRequested` 리스너
- `Editor/SpriteAtlasPacker.cs`: 매니페스트 커스텀 인스펙터 및 아틀라스 생성/추출 기능
- `Editor/TextureAtlasImageEditor.cs`: `TextureAtlasImage` 커스텀 인스펙터
- `Editor/SpriteAtlasImageEditor.cs`: `SpriteAtlasImage` 커스텀 인스펙터

## 에디터 컨텍스트 메뉴 및 도구

- 단일 변환: 인스펙터에서 `Image` 구성요소 헤더를 마우스 오른쪽 버튼으로 누르고 `Convert to SpriteAtlasImage`를 선택하면 해당 `Image` 구성요소 하나만 변환합니다. 이 메뉴 항목은 구성요소 컨텍스트 메뉴에서 실행한 단일 인스턴스에만 적용됩니다.
- 다중 선택 변환: 여러 `Image` 구성요소를 한 번에 변환하려면 Hierarchy에서 GameObject를 선택한 뒤 상단 메뉴의 `Tools > Convert Selected Images to SpriteAtlasImage`를 사용합니다. 이 명령은 선택 항목을 순회하며 아직 `SpriteAtlasImage`로 변환되지 않은 `Image` 구성요소를 변환하고, 이미 변환된 항목은 건너뜁니다.

변환 후에도 기존 `Image.sprite`는 유지됩니다. 해당 스프라이트가 `SpriteAtlas`에 포함되어 있으면 변환 과정에서 대응하는 `AssetReferenceSprite` GUID와 하위 오브젝트 참조를 설정하려고 시도합니다. 어떤 아틀라스에도 포함되지 않은 스프라이트라면 경고가 기록됩니다.

## 라이선스

MIT 라이선스입니다. 자세한 내용은 `LICENSE`를 참조하세요.
