# Localization Subsetting
### AKA: Shrinking the chinese/japanese fonts so we don't ship 100+ megabytes of unused glyphs.

The Localization Subsetting automation is intended to build subsets of the fonts that are used
within the game.

First you must run a custom tool in the Unity Editor under "/Localization/Export Text for Font Subsetting".
This will produce a folder at "/Automation/LocalizationSubsetting/FontSubsetText" which contains numerous text
files of all the localized texts within the defined Localization Tables.

**NOTE** If new Localization Tables are created, they must be added in the tool at '/Assets/_Project/Tools/LocalizationFontExtractor.cs'.

### Once the 'FontSubsetText' folder is populated with localized txt files...
The subsetting can be initiated by running the 'run_localization_chain.sh' script, which will trigger the
'fetch_fonts.sh' script followed by the 'subset_fonts.sh' script in a sequence.