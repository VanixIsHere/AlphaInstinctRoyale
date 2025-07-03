#!/bin/bash

set -e

FONT_DIR="./OriginalFonts"
mkdir -p "$FONT_DIR"

# Array of fonts and their URLs
declare -A fonts=(
  ["NotoSans-Regular.ttf"]="https://github.com/googlefonts/noto-fonts/blob/main/hinted/ttf/NotoSans/NotoSans-Regular.ttf?raw=true"
  ["NotoSansJP-Regular.otf"]="https://github.com/googlefonts/noto-cjk/blob/main/Sans/OTF/Japanese/NotoSansJP-Regular.otf?raw=true"
  ["NotoSansKR-Regular.otf"]="https://github.com/googlefonts/noto-cjk/blob/main/Sans/OTF/Korean/NotoSansKR-Regular.otf?raw=true"
  ["NotoSansTC-Regular.otf"]="https://github.com/googlefonts/noto-cjk/blob/main/Sans/OTF/TraditionalChinese/NotoSansTC-Regular.otf?raw=true"
  ["GermaniaOne-Regular.ttf"]="https://github.com/google/fonts/blob/main/ofl/germaniaone/GermaniaOne-Regular.ttf?raw=true"
)

echo "📁 Checking fonts in $FONT_DIR..."

for filename in "${!fonts[@]}"; do
  filepath="$FONT_DIR/$filename"
  url="${fonts[$filename]}"

  if [[ -f "$filepath" ]]; then
    echo "✅ $filename already exists."
  else
    echo "⬇️ Downloading $filename..."
    curl -L -o "$filepath" "$url"
    echo "✅ Downloaded $filename"
  fi
done

echo "🎉 All required fonts are available."
