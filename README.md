# Unity Sprite Packer

ScriptableObject 기반으로 여러 Unity `Sprite`를 하나의 texture atlas로 pack하고, runtime에서 이름으로 sprite를 가져오거나 UI `Image`처럼 표시할 수 있게 해주는 패키지입니다.

## Requirements

- Unity `6000.3` 이상
- `com.unity.ugui` `2.0.0`
- `SpriteAtlasImage`를 사용할 경우 Addressables 패키지 필요

## Features

- `SpriteAtlasManifest` asset으로 atlas 구성 관리
- 선택한 sprite를 inspector에서 entries에 추가
- `Texture2D.PackTextures` 기반 atlas PNG 생성
- source sprite의 pivot, border, rect metadata 반영
- entry별 `Source Scale (%)` 지원
- atlas rebuild 시 기존 sub-sprite metadata 초기화 후 재생성
- sprite 이름 검색, 정렬, 개별 제거
- atlas에서 개별 sprite PNG 추출
- runtime에서 `SpriteAtlasManifest.GetSprite(name)`으로 sprite 조회
- `TextureAtlasImage`로 manifest 기반 UI 표시
- `SpriteAtlasImage`로 Addressables `AssetReferenceAtlasedSprite` 기반 UI 표시
- `SpriteMapper`로 `SpriteAtlasManager.atlasRequested` 경고를 억제

## Installation

### Local Package

이 패키지 폴더를 Unity 프로젝트의 `Packages/` 아래에 둡니다.

```text
Packages/jiffy.unityspritepacker
```

### Git UPM

`Packages/manifest.json`에 dependency를 추가합니다.

```json
{
  "dependencies": {
    "jiffy.unityspritepacker": "https://github.com/josangjun/unity-sprite-atlas-packer.git"
  }
}
```

특정 tag나 commit으로 고정하려면 URL 뒤에 ref를 붙입니다.

```text
https://github.com/josangjun/unity-sprite-atlas-packer.git#v1.0.0
```

## Quick Start

1. Project 창에서 우클릭 후 `Create > XSystem > Sprite Atlas Manifest`를 선택합니다.
2. 생성된 `SpriteAtlasManifest` asset을 선택합니다.
3. pack할 sprite asset을 Project 창에서 선택한 뒤 inspector의 `Add Selected Sprites`를 누릅니다.
4. 필요하면 `Padding`, `Max Size`, `Allow Rotation`, `Source Scale (%)`를 조정합니다.
5. `Refresh Pivot/Border from Source Sprites`로 source metadata를 다시 동기화할 수 있습니다.
6. `Sort Entries By Name`으로 entries를 이름순 정렬할 수 있습니다.
7. `Rebuild Atlas`를 누르면 manifest asset 옆에 `<ManifestName>.png` atlas가 생성됩니다.

`Rebuild Atlas`는 생성된 PNG를 `Sprite (Multiple)` texture로 설정하고, current entries 기준으로 sub-sprite metadata를 다시 만듭니다.

## Runtime Usage

`SpriteAtlasManifest`에서 이름으로 sprite를 조회합니다.

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

없는 이름을 요청하면 warning을 남기고 `null`을 반환합니다.

## UI Components

### TextureAtlasImage

`TextureAtlasImage`는 `SpriteAtlasManifest`와 `Sprite Name`을 받아 UI sprite를 자동으로 갱신하는 `UnityEngine.UI.Image` 파생 component입니다.

사용 방법:

1. UI GameObject에 `TextureAtlasImage` component를 추가합니다.
2. `Atlas`에 `SpriteAtlasManifest` asset을 지정합니다.
3. `Sprite Search`로 sprite 이름을 검색합니다.
4. `Sprite Name` dropdown에서 표시할 sprite를 선택합니다.

코드에서 sprite를 바꿀 수도 있습니다.

```csharp
using UnityEngine;
using UnityEngine.UI;

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

`SpriteAtlasImage`는 Addressables의 `AssetReferenceAtlasedSprite`를 직접 참조하는 `Image` 파생 component입니다.

- editor에서는 asset GUID로 sprite를 로드해 미리보기를 갱신합니다.
- play mode에서는 Addressables 비동기 로드를 사용합니다.
- component가 disable되면 유효한 Addressables handle을 release합니다.

## Inspector Buttons

- `Add Selected Sprites`: Project 창에서 선택한 sprite를 entries에 추가합니다. 선택된 sprite가 없으면 sprite picker를 엽니다.
- `Refresh Pivot/Border from Source Sprites`: source importer의 pivot, border, rect 정보를 entries에 다시 반영합니다.
- `Sort Entries By Name`: entries를 `spriteName` 기준으로 정렬합니다.
- `Rebuild Atlas`: atlas PNG와 sub-sprite metadata를 다시 생성합니다.
- `Extract`: 해당 entry의 atlas 영역을 PNG로 저장합니다.
- `Remove`: 해당 entry를 manifest에서 제거합니다.

## Notes

- source texture가 readable이 아니면 editor script가 `isReadable`을 켜고 reimport합니다.
- 동일한 source sprite가 중복 등록되면 rebuild 중 중복 entry가 제거됩니다.
- `Allow Rotation`은 Unity 내부 `PackTextures` overload를 reflection으로 호출합니다. 현재 Unity 버전에서 지원되지 않으면 rotation 없이 pack합니다.
- entry의 `border` 값은 `Source Scale (%)`에 맞춰 scaling되어 atlas sprite metadata에 반영됩니다.
- 생성된 atlas PNG는 manifest asset과 같은 폴더에 저장됩니다.

## Package Layout

- `Runtime/SpriteAtlasManifest.cs`: atlas manifest와 entry data, runtime sprite lookup
- `Runtime/TextureAtlasImage.cs`: manifest 기반 UI image component
- `Runtime/SpriteAtlasImage.cs`: Addressables atlased sprite 기반 UI image component
- `Runtime/SpriteMapper.cs`: `SpriteAtlasManager.atlasRequested` listener
- `Editor/SpriteAtlasPacker.cs`: manifest custom inspector와 atlas build/extract 기능
- `Editor/TextureAtlasImageEditor.cs`: `TextureAtlasImage` custom inspector
- `Editor/SpriteAtlasImageEditor.cs`: `SpriteAtlasImage` custom inspector

## License

MIT License. 자세한 내용은 `LICENSE`를 참고하세요.
