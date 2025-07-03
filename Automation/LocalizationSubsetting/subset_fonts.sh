#!/bin/bash

set -e
FONT_DIR="./OriginalFonts"
TEXT_DIR="./FontSubsetText"
OUT_DIR="./SubsetFonts"
mkdir -p "$OUT_DIR"

# Declare font mapping for each locale
declare -A fonts=(
  ["en"]="NotoSans-Regular.ttf"
  ["ja"]="NotoSansJP-Regular.otf"
  ["ko"]="NotoSansKR-Regular.otf"
  ["zh-Hant"]="NotoSansTC-Regular.otf"
)

# Extra: include Germania One for English if used for headers or titles
fonts_germania=("en")

for locale in "${!fonts[@]}"; do
  textFile="$TEXT_DIR/${locale}_strings.txt"
  baseFont="$FONT_DIR/${fonts[$locale]}"
  outFont="$OUT_DIR/${locale}_Subset.otf"

  if [[ -f "$textFile" && -f "$baseFont" ]]; then
    echo "📦 Subsetting ${baseFont} with ${textFile}..."

    pyftsubset "$baseFont" \
      --output-file="$outFont" \
      --text-file="$textFile" \
      --layout-features='*' \
      --glyph-names --symbol-cmap --legacy-cmap --notdef-outline --recommended-glyphs

    echo "✅ Created $outFont"
  else
    echo "⚠️ Missing font or text file for $locale"
  fi
done

# Optional: Subset Germania One for English too
if [[ " ${fonts_germania[*]} " =~ " en " ]]; then
  baseFont="$FONT_DIR/GermaniaOne-Regular.ttf"
  textFile="$TEXT_DIR/en_strings.txt"
  outFont="$OUT_DIR/en_GermaniaSubset.otf"

  if [[ -f "$baseFont" && -f "$textFile" ]]; then
    echo "📦 Subsetting Germania One for English headers..."

    pyftsubset "$baseFont" \
      --output-file="$outFont" \
      --text-file="$textFile" \
      --layout-features='*' \
      --glyph-names --symbol-cmap --legacy-cmap --notdef-outline --recommended-glyphs

    echo "✅ Created $outFont"
  fi
fi
