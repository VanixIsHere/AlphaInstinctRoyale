# Localization Subsetting
### AKA: Shrinking the Chinese, Japanese, and Korean fonts so we don't ship 100+ MB of unused glyphs.

This automation workflow is designed to generate subsetted font files based on the characters actually used in localized text. The goal is to significantly reduce file sizes for large fonts like Noto Sans JP, KR, and TC.

---

## 🔧 Step 1 — Export Font Usage From Unity

In the Unity Editor, navigate to:

```
/Localization/Export Text for Font Subsetting
```

This tool scans all defined `LocalizedStringTable` assets and outputs `.txt` files containing the combined set of characters used per locale. These files are written to:

```
/Automation/LocalizationSubsetting/FontSubsetText/
```

Each output file is named according to the locale, such as `en_strings.txt`, `ja_strings.txt`, etc.

> 💡 When adding new Localization Tables, be sure to register them in:  
> `/Assets/_Project/Tools/LocalizationFontExtractor.cs`

---

## 🔡 Step 2 — Prepare Fonts

Original, **static** font files should be placed into:

```
/Automation/LocalizationSubsetting/OriginalFonts/
```

These files should be downloaded from the official [Noto Fonts](https://www.google.com/get/noto/) site. The expected font files include:

- `NotoSans-Regular.ttf`
- `NotoSansJP-Regular.ttf`
- `NotoSansKR-Regular.ttf`
- `NotoSansTC-Regular.ttf`
- `GermaniaOne-Regular.ttf` (if used for stylized headings or titles)

> 📝 These original font files are used exclusively for subsetting and are not included in the final Unity build.

---

## ✂️ Step 3 — Run the Subsetting Script

To generate the minimized font files, execute:

```bash
bash Automation/LocalizationSubsetting/subset_fonts.sh
```

This script:

- Uses `FontSubsetText/*.txt` as input character sets
- Uses `OriginalFonts/*.ttf` as the source fonts
- Outputs subset `.ttf` files into:

```
/Automation/LocalizationSubsetting/SubsetFonts/
```

Subset fonts can then be manually copied into the Unity Assets folder as needed.

> 📄 Logs from the operation are saved in:
> `/Automation/LocalizationSubsetting/Logs/`

---

## 📌 Notes

- Subsetting is powered by [fontTools](https://github.com/fonttools/fonttools).  
  Ensure Python is installed, then run `pip install fonttools` once.
- Subsets are generated per locale based solely on the glyphs found in current localization text.
- Germania One is optionally subsetted for English use cases like headers or titles.

---

## ✅ Summary

| Folder                                 | Purpose                            |
|----------------------------------------|------------------------------------|
| `OriginalFonts/`                       | Source font files for subsetting   |
| `FontSubsetText/`                      | Text exports from Unity tool       |
| `SubsetFonts/`                         | Output of subset fonts             |
| `Logs/`                                | Script execution logs              |

---

With this process in place, the project avoids shipping unnecessary font data and maintains a lean build size 🚀
