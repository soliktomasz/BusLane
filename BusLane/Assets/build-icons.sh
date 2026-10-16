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

# Website copy: the same artwork cropped to the tile, without the dock shadow,
# so the favicon and 30-34px brand marks render full size on the page.
web=../../docs/BusLane/Assets
sed -e 's|viewBox="0 0 1024 1024"|viewBox="100 100 824 824"|' -e '/translate(0 10)/d' icon.svg > "$web/icon.svg"
render "$web/icon.svg" 512 "$web/icon.png"
echo "Icons regenerated."
