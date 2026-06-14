# LutLightTool

A 2D artist-oriented tool for creating color palettes with shadow gradients and baking them into LUT (Look-Up Table) textures for use with [LutLight2D](https://github.com/NullTale/LutLight2D) in Unity's Universal Render Pipeline (URP) 2D.

Built on top of the [LutLight2D](https://github.com/NullTale/LutLight2D) system by [NullTale](https://twitter.com/NullTale/).

## Features

- **Sprite Atlas Import** -- Load any PNG sprite atlas and automatically extract unique colors, sorted by HSV
- **Color Palette Generation** -- Generate a base palette from extracted colors with configurable shadow degrees
- **Shadow Gradient Control** -- Set the number of shadow levels (2+) to create rows of progressively darker shades
- **Color Editing** -- Edit individual colors via RGBA inputs, full HSV color picker (saturation/value grid, hue slider, alpha slider), or column interpolation
- **LUT Baking** -- Bake the palette into a LUT texture and material using the `Shader Graphs/LutLight` shader
- **Live 2D Preview** -- Preview the result on your sprite with a draggable 2D point light and adjustable intensity
- **Pan & Zoom** -- Navigate the preview with Shift+WASD (pan) and Q/E (zoom, 0.25x--4x)
- **Download & Restore** -- Save your color palette as a PNG and reload it for further editing later

## Requirements

- **Unity 6** (6000.2.14f1)
- **Universal Render Pipeline (URP) 2D** (com.unity.render-pipelines.universal 17.2.0+)
- **Input System** (com.unity.inputsystem 1.16.0+)

## Usage

1. **Upload** a sprite atlas (PNG) to extract colors
2. **Configure** shadow degree to set how many shade levels to generate
3. **Edit** colors using the RGBA inputs, color picker, or interpolation
4. **Bake** the palette into a LUT texture
5. **Preview** the result with the draggable 2D point light
6. **Download** the final palette as a PNG for reuse or sharing

## Project Structure

```
LutLightTool/
  Assets/
    Runtime/          -- Core scripts (LutGenerator, UI, shaders)
    Editor/           -- Editor extensions (custom inspectors, auto-import)
    Scenes/           -- Sample scene
    UI/               -- UXML/USS layouts and styles
    Shaders/          -- LutLight shader graph and HLSL include
  Packages/           -- Unity package dependencies
  ProjectSettings/    -- Unity project settings
```

## License

[MIT](LICENSE)

## Author

[Ilya Nasanovich](https://github.com/IlyaNasanovich)

## Credits

- [LutLight2D](https://github.com/NullTale/LutLight2D) by [NullTale](https://twitter.com/NullTale/) -- the original LUT-based 2D lighting system for URP
