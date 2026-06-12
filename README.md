# Unity Sprite Packer

A package that packs multiple Unity `Sprite` assets into a single texture atlas using a `ScriptableObject`, and provides runtime access by name as well as UI display support.

## Requirements

- Unity `6000.3` or later
- `com.unity.ugui` `2.0.0`
- Addressables package is required for `SpriteAtlasImage`

## Features

- Manage atlas configuration with the `SpriteAtlasManifest` asset
- Add selected sprites to entries from the inspector
- Generate atlas PNG using `Texture2D.PackTextures`
- Preserve source sprite pivot, border, and rect metadata
- Support `Source Scale (%)` per entry
- Rebuild atlas by clearing and regenerating sub-sprite metadata
- Search, sort, and remove individual entries by sprite name
- Extract individual sprite PNGs from the atlas
- Lookup sprites at runtime via `SpriteAtlasManifest.GetSprite(name)`
- Display atlas-based UI with `TextureAtlasImage`
- Support Addressables-based UI with `SpriteAtlasImage` and `AssetReferenceAtlasedSprite`
- Suppress `SpriteAtlasManager.atlasRequested` warnings with `SpriteMapper`

## Installation

### Local Package

Place this package folder under `Packages/` in your Unity project.

```text
Packages/jiffy.unityspritepacker
```

### Git UPM

Add a dependency to `Packages/manifest.json`.

```json
{
  "dependencies": {
    "jiffy.unityspritepacker": "https://github.com/josangjun/unity-sprite-atlas-packer.git"
  }
}
```

To pin a specific tag or commit, append `#ref` to the URL.

```text
https://github.com/josangjun/unity-sprite-atlas-packer.git#v1.0.0
```

## Quick Start

1. In the Project window, right-click and choose `Create > XSystem > Sprite Atlas Manifest`.
2. Select the created `SpriteAtlasManifest` asset.
3. Select the sprites to pack in the Project window, then click `Add Selected Sprites` in the inspector.
4. Adjust `Padding`, `Max Size`, `Allow Rotation`, and `Source Scale (%)` as needed.
5. Use `Refresh Pivot/Border from Source Sprites` to re-sync source metadata.
6. Use `Sort Entries By Name` to sort entries by sprite name.
7. Click `Rebuild Atlas` to generate the `<ManifestName>.png` atlas next to the manifest asset.

`Rebuild Atlas` sets the generated PNG to `Sprite (Multiple)` and recreates sub-sprite metadata based on the current entries.

## Runtime Usage

Retrieve sprites by name from `SpriteAtlasManifest`.

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

Requesting a non-existing name logs a warning and returns `null`.

## UI Components

### TextureAtlasImage

`TextureAtlasImage` is a component derived from `UnityEngine.UI.Image` that automatically updates its displayed sprite using a `SpriteAtlasManifest` and a `Sprite Name`.

Usage:

1. Add a `TextureAtlasImage` component to a UI GameObject.
2. Assign the `SpriteAtlasManifest` asset to `Atlas`.
3. Search sprite names using `Sprite Search`.
4. Select the sprite to display from the `Sprite Name` dropdown.

You can also change the sprite from code.

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

`SpriteAtlasImage` is an `Image`-derived component that references Addressables `AssetReferenceAtlasedSprite` directly.

- In the editor, it loads the asset by GUID and updates the preview.
- In Play Mode, it uses Addressables asynchronous loading.
- When the component is disabled, it releases any valid Addressables handle.

## Inspector Buttons

- `Add Selected Sprites`: Adds the selected sprites from the Project window to the manifest entries. If no sprites are selected, it opens the sprite picker.
- `Refresh Pivot/Border from Source Sprites`: Updates entry metadata using the source importer pivot, border, and rect values.
- `Sort Entries By Name`: Sorts entries by `spriteName`.
- `Rebuild Atlas`: Regenerates the atlas PNG and sub-sprite metadata.
- `Extract`: Saves the selected entry’s atlas region as a PNG.
- `Remove`: Removes the selected entry from the manifest.

## Notes

- If a source texture is not readable, the editor script enables `isReadable` and reimports it.
- Duplicate source sprites are removed during rebuild.
- `Allow Rotation` uses reflection to call Unity’s internal `PackTextures` overload. If unsupported in the current Unity version, it packs without rotation.
- Entry `border` values are scaled according to `Source Scale (%)` and reflected in the atlas sprite metadata.
- The generated atlas PNG is saved to the same folder as the manifest asset.

## Package Layout

- `Runtime/SpriteAtlasManifest.cs`: Atlas manifest, entry data, runtime sprite lookup
- `Runtime/TextureAtlasImage.cs`: Manifest-based UI image component
- `Runtime/SpriteAtlasImage.cs`: Addressables atlased sprite UI image component
- `Runtime/SpriteMapper.cs`: `SpriteAtlasManager.atlasRequested` listener
- `Editor/SpriteAtlasPacker.cs`: Manifest custom inspector and atlas build/extract functionality
- `Editor/TextureAtlasImageEditor.cs`: `TextureAtlasImage` custom inspector
- `Editor/SpriteAtlasImageEditor.cs`: `SpriteAtlasImage` custom inspector

## Editor Context Menu & Tools

- Context menu (single): Right-click the `Image` component header in the Inspector and choose `Convert to SpriteAtlasImage` to convert that single `Image` component to a `SpriteAtlasImage`. This menu item is a component-context action and only operates on the single `Image` instance you invoked it on.
- Tools menu (multi-select): To convert multiple `Image` components at once, select the GameObjects in the Hierarchy and use the top menu `Tools → Convert Selected Images to SpriteAtlasImage`. This command iterates the selection, converts each non-`SpriteAtlasImage` `Image` component, and skips items that are already converted.

When converting, the original `Image.sprite` will be preserved on the converted component. If the sprite is included in a `SpriteAtlas`, the conversion attempts to set the corresponding `AssetReferenceSprite` GUID and sub-object reference; otherwise a warning is logged indicating the sprite is not packed in any atlas.

## License

MIT License. See `LICENSE` for details.
