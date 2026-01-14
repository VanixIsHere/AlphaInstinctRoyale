#!/bin/bash

set -e

# Get the full path to the script's directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Absolute paths to folders
FONT_DIR="$SCRIPT_DIR/OriginalFonts"
TEXT_DIR="$SCRIPT_DIR/FontSubsetText"
OUT_DIR="$SCRIPT_DIR/SubsetFonts"
LOG_DIR="$SCRIPT_DIR/Logs"
mkdir -p "$OUT_DIR"
mkdir -p "$LOG_DIR"

# Create log file
LOG_FILE="$LOG_DIR/subset_log_$(date +"%Y-%m-%d_%H-%M-%S").log"
exec > >(tee -a "$LOG_FILE") 2>&1

echo "📁 Subsetting script started at $(date)"
echo "📝 Logging to: $LOG_FILE"

# Declare font mapping for each locale (static .ttf fonts only)
declare -A fonts=(
  ["en-US"]="NotoSans-Regular.ttf"
  ["ja"]="NotoSansJP-Regular.ttf"
  ["ko"]="NotoSansKR-Regular.ttf"
  ["zh-Hant"]="NotoSansTC-Regular.ttf"
)

for locale in "${!fonts[@]}"; do
  textFile="$TEXT_DIR/${locale}_strings.txt"
  baseFont="$FONT_DIR/${fonts[$locale]}"
  outFont="$OUT_DIR/${locale}_NotoSans_Subset.ttf"

  if [[ -f "$textFile" && -f "$baseFont" ]]; then
    echo "📦 Subsetting $baseFont → $outFont with $textFile"

    python -m fontTools.subset "$baseFont" \
      --output-file="$outFont" \
      --text-file="$textFile" \
      --layout-features='*' \
      --glyph-names --symbol-cmap --legacy-cmap --notdef-outline --recommended-glyphs

    echo "✅ Created: $outFont"
  else
    echo "⚠️ Missing font or text file for $locale"
  fi
done

# Subset GermaniaOne-Regular as a decorative font for English
GERMANIA_FONT="$FONT_DIR/GermaniaOne-Regular.ttf"
GERMANIA_OUT="$OUT_DIR/en-US_Germania_Subset.ttf"
GERMANIA_TEXT="$TEXT_DIR/en-US_strings.txt"

if [[ -f "$GERMANIA_FONT" && -f "$GERMANIA_TEXT" ]]; then
  echo "📦 Subsetting GermaniaOne-Regular → $GERMANIA_OUT"

  python -m fontTools.subset "$GERMANIA_FONT" \
    --output-file="$GERMANIA_OUT" \
    --text-file="$GERMANIA_TEXT" \
    --layout-features='*' \
    --glyph-names --symbol-cmap --legacy-cmap --notdef-outline --recommended-glyphs

  echo "✅ Created: $GERMANIA_OUT"
fi
