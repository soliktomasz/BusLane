#!/bin/sh
# Regenerates every raster app icon from the two SVG sources.
#   icon.svg       large artwork on the macOS icon grid (64px and up)
#   icon-small.svg pixel-snapped artwork for 16px and 32px
# Requires rsvg-convert (brew install librsvg) and macOS iconutil.
set -eu
cd "$(dirname "$0")"

render() { rsvg-convert -w "$2" -h "$2" "$1" -o "$3"; }

render icon.svg 512 icon.png

set_dir=icon.iconset
render icon-small.svg 16 "$set_dir/icon_16x16.png"
render icon-small.svg 32 "$set_dir/icon_16x16@2x.png"
render icon-small.svg 32 "$set_dir/icon_32x32.png"
render icon.svg 64 "$set_dir/icon_32x32@2x.png"
render icon.svg 64 "$set_dir/icon_64x64.png"
render icon.svg 128 "$set_dir/icon_64x64@2x.png"
render icon.svg 128 "$set_dir/icon_128x128.png"
render icon.svg 256 "$set_dir/icon_128x128@2x.png"
render icon.svg 256 "$set_dir/icon_256x256.png"
# The 512px dock assets must be byte-identical to icon.png (see AppIconAssetTests).
cp icon.png "$set_dir/icon_256x256@2x.png"
cp icon.png "$set_dir/icon_512x512.png"
render icon.svg 1024 "$set_dir/icon_512x512@2x.png"

iconutil -c icns "$set_dir" -o icon.icns
echo "Icons regenerated."
