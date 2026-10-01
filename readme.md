# UABEANext - PS4 and PS5 texture support

This fork adds experimental PS4 and PS5 texture preview, export and import.

Preview, export and import work through the usual texture menus. PS4 and PS5
textures use the console layout when `m_IsPreProcessed` is set.

- **Show original swizzled texture** shows the stored pixel order in the main preview.
- **Mipmaps** shows the saved lower levels when expanded.
- Replacing an image keeps its size, format, mip count and padding. Lower mips
  are rebuilt from the replacement image. BC compression can change image data.
- **Is preprocessed** converts the stored data without recompressing it. Turn it
  off and padding is discarded. Turn it back on and new padding is filled with
  zeros. PS5 uses 4 KB tiles for this conversion.

## Supported layouts

PS4 uses 8x8 Morton tiles. Some mip chains also pad the base storage dimensions
to powers of two. The full data size selects the matching layout, while preview
and import keep the original image dimensions.
PS5 supports standard 256-byte, 4 KB and 64 KB tiles,
including the shared tile for small mipmaps. Prefer 4 KB when the data size
matches, as it did in the tested Unity samples. Otherwise, only a single
matching tile size is accepted. Size alone cannot identify every PS5 layout.

Support is limited to complete 2D textures with one image. Arrays, cubemaps,
3D textures, streaming mipmaps and PS5 display/XOR layouts are unsupported.
RGB24 uses four-byte RGBA storage in the PS4/PS5 samples. Turning preprocessing
off therefore changes that format to RGBA32.

Alpha8/R8, RGB24, 32-bit color and BC formats support image replacement.
Other fixed-size integer/float formats and ETC/EAC, ATC and ASTC LDR blocks
support preview, export and raw conversion, but have only synthetic tests.
RGB48/RGBFloat, YUY2, PVRTC, Crunch and ASTC HDR storage are unsupported.
PNG output uses 8-bit channels, so it loses HDR and higher channel precision.

## Notes from testing

Padding can contain old image data. In one PS4 sample it matched bytes from
another texture, which suggests a reused buffer. The source of the PS5 padding
has not been confirmed. Keeping the original buffer preserves these bytes.

An extra repeated block was discovered in one PS5 resource file. Its texture
offsets did not account for that block, so later textures looked broken.
The PS5 reader skips it in memory only when all texture ranges fit and exactly
one repeated boundary is found. The file and saved offsets stay unchanged.
This workaround runs only for preprocessed PS5 textures in loose resource files.
It is a best-effort check, not a general PS5 storage rule.

PS5 mip placement is based on the public
[AMD PAL layout code](https://github.com/GPUOpen-Drivers/pal/blob/c5e800072a32f68b6ccc4422936d96167c6e0728/src/core/imported/addrlib/src/gfx10/gfx10addrlib.cpp#L3802-L4025)
and [swizzle tables](https://github.com/GPUOpen-Drivers/pal/blob/c5e800072a32f68b6ccc4422936d96167c6e0728/src/core/imported/addrlib/src/gfx10/gfx10SwizzlePattern.h).
Local checks cover layout addresses, mipmaps, padding, image import and saved
asset reloads. Game assets and local test tools are not included. In-game
loading has not been tested.

---

## UABEANext

UABEA with dock support. When this repo becomes stable and reaches feature parity, the code will be merged into the original UABEA repo.

Upstream nightly builds (do not include this fork's console texture changes):

- https://nightly.link/nesrak1/UABEANext/workflows/build-windows/master/uabea-windows.zip
- https://nightly.link/nesrak1/UABEANext/workflows/build-ubuntu/master/uabea-ubuntu.zip

Report any issues on the original repo: https://github.com/nesrak1/UABEA
