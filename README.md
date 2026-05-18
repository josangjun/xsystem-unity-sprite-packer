# Unity Sprite Packer

A simple and efficient tool to create and manage Sprite Atlases in Unity using ScriptableObjects. This package allows you to pack multiple textures into a single atlas, preserving sprite metadata like pivots and borders.

## Features

- **ScriptableObject Based**: Manage atlas configurations as persistent assets in your project.
- **Automatic Packing**: Uses Unity's `PackTextures` API to combine multiple textures into one.
- **Metadata Preservation**: Automatically syncs Sprite Pivot and Border settings from source textures.
- **Runtime Access**: Retrieve sprites by name at runtime using a simple API.
- **Sprite Extraction**: Extract individual sprites from an existing atlas back into standalone PNG files.
- **Texture Importer Integration**: Automatically configures the generated PNG as a Multiple Sprite texture.

## Installation

Copy the package folder into your Unity project's `Packages` directory.

## How to Use

1. **Create an Atlas Asset**: Right-click in the Project window and select `Create > XSystem > Sprite Atlas Pack`.
2. **Add Textures**: 
   - Select textures in the Project window and click **Add Selected Textures** in the inspector.
   - Or, manually assign textures to the entries list.
3. **Configure Settings**: Adjust `Padding`, `Max Size`, and `Allow Rotation` as needed.
4. **Build the Atlas**: Click the **Rebuild Atlas** button. This will:
   - Generate a PNG file next to your asset.
   - Configure the PNG as a Sprite (Multiple).
   - Map all source sprites to their new locations in the atlas.
5. **Accessing Sprites in Code**:
   ```csharp
   public SpriteAtlasPack atlas;

   void Start() {
       Sprite mySprite = atlas.GetSprite("SpriteName");
   }
   ```

## License

This project is licensed under the MIT License - see the LICENSE.md file for details.
Copyright (c) 2026 josangjun