Last updated: 4/9/16

WorldRules.csv

This file contains the probabilities used when generating the layout of a world.
Rows may appear in any order. Every key must be present, and unrecognized keys are an error.
Format for each row:
Key (*) [,]
Value [,]
Description (optional, ignored by the program; must not contain commas)

(*) The key is the exact name of the matching field in the WorldRules struct, e.g. ContinentsMin.
Blank lines and lines starting with # are ignored. Decimal numbers always use '.', whatever the machine's language.

LetterSounds.txt

This file contains the letter sounds used when assembling languages. It is a unicode TXT, not a CSV, because the latter does not play nice with unicode.
Format for each line:
ID number [space]
1/0 is a vowel [space]
1/0 is an S/Z consonant [space]
1/0 is an R/L consonant [space]
number of written forms of this sound [space]
written forms of this sound, from least to most exotic [separated by spaces]


PhoneticInventories.csv 

This file contains the "root" languages that simulated languages phonetic vocabularies are based off of.
Format for each row:
Name of root language [,]
IDs of letter sounds in this phonetic vocalbulary [separated by ,s]


LanguageRules.csv

This file contains the probabilities used when generating new words in new languages.
Same key-based format as WorldRules.csv; keys match the fields of the LanguageRules struct, e.g. LongRimeProbability.

ClimateTypes.csv

This file contains the Climate Types.
Format for each row:
Name [,]
ID Number [,]
Color Name (*)

(*) Name must match (with proper caps and no spaces), a color from this chart: http://www.flounder.com/csharp_color_table.htm

Climates.csv

This file contains the Climates. (Each Climate has a primary Climate Type and, optionally, a secondary Climate Type.)

Format for each row:
ID Number [,]
Primary Climate Type ID Number [,]
Secondary Climate Type ID Number [,]
Name

Biomes.txt

[This is saved as a TXT, not a CSV, to preserve leading 0s in the Biome Parameters Codes]

This file contains the terrains and their basic parameters. (It does not include the mapping of biomes and terrains.)
Format for each row:
ID Number [,]
Biome Parameters Code (*) [,]
Biome Name

(*) Biome Parameters Code
Each biome parameters code is 6 digits long: ABCDEF
Below is a list of what each digit represents.
A - Temperature (0-4). (0 = frigid, 1 = cold, 2 = temperate, 3 = warm, 4 = sweltering)
B - Precipitation (0-4). (0 = arid, 1 = dry, 2 = moderate, 3 = wet, 4 = tropical)
C - Contour (0-4). (0 = lowlands, 1 = flat, 2 = normal/hilly, 3 = jagged/cliffs, 4 = mountains)
D - Temperature Seasons (0-1). (0 = No Seasons, 1 = Cold Winter)
E - Precipitation Season (0-1). (0 = No Seasons, 1 = Wet Summers)
F - Habitability (0-5). (0 = Inhabitable, 1 = Harsh, 2 = Unfavorable, 3 = Livable, 4 = Comfortable, 5 = Lush)

TerrainTypes.csv

This file contains the Terrain Types.

Format for each row:
Name [,]
ID Number [,]
Description [,]
Relief (optional, 0-4; default 1). How high and steep this terrain is: 0 = flat lowland, 2 = hills, 3 = cliffs, 4 = peaks.
Generators that model elevation (such as "noise") put higher relief on higher ground. The "legacy" generator ignores it.

Terrains.csv

This file contains the Terrains. (Each Terrain has Climate and a Terrain Type.)

Format for each row:

ID Number [,]
Climate ID Number [,]
Terrain Type ID Number [,]
Name

BiomePatterns.csv

This file contains a mapping of Biomes to Climate Types and Terrain Types.

This is a pattern of three rows per Biome:

Row 1:
Biome ID Number

Row 2:
A repeated sequence of...
Climate Type ID Number [,]
Weight of that Climate Type (*) [,]

Row 3:
A repeated sequence of...
Terrain Type ID Number [,]
Weight of that Climate (*) [,]

(*) Weights are logarithmic, from 1 to 6. E.G. A weight of 5 means this terrain/climate type is twice as common as one with a weight of 4, which is twice as common as one with a weight of 3.

In other words, a weight of 6 is 32 times as common as something with a weight of 1.

Weights put to words:
6 = Very Common
5 = Common
4 = Fairly Common
3 = Fairly Uncommon
2 = Uncommon
1 = Very Uncommon

TerrainRules.csv

This file contains values used when generating biome and terrain patterns.
Same key-based format as WorldRules.csv; keys match the fields of the TerrainRules struct, e.g. CombinationWeightReducer.
Generator-specific rules files

Each world generator keeps its own settings in its own keyed rules file (same Key,Value,Description format as WorldRules.csv),
loaded through SimData.LoadRules. The shared files above (WorldRules.csv and so on) only hold settings every generator uses
or that belong to the original "legacy" generator, so adding a generator never changes their format.

NoiseGeneratorRules.csv

Settings for the "noise" world generator (Key,Value,Description rows, any order; all keys required).
LandFractionMean/StdDev/Min/Max - the share of the map that is land is drawn from this normal distribution and clamped
Octaves, Frequency, Lacunarity, Persistence - the fractal noise: layers, size of the broadest features, how fast detail grows and fades
WarpStrength - how much coastlines wind (0 is none)
MaskStrength - how strongly land gathers around the continent centres (0 is pure noise, 1 is smooth blobs)
MaskRadiusMin/Max - size of the area around each continent centre (1 is the map height)
EdgeBorder - width of the border where land fades into ocean at the map edge (share of the map)
MinLandmassBlocks - smallest island kept; MinSeaBlocks - smallest enclosed body of water kept (smaller ones are filled in)
The number of continent centres comes from ContinentsMin/Max in WorldRules.csv.
CoastDetail - 1 gives blocks along coasts a 10x10 grid of sub-blocks with fine coastlines, 0 leaves the world at block resolution
DetailStrength - how much fine wiggle is added to coastlines (0 is a smooth curve); DetailRange - how far from the coast it reaches
DetailFrequency, DetailOctaves - the size and layering of the wiggle

Climate and biome settings (also in NoiseGeneratorRules.csv)
ClimateFrequency, ClimateOctaves - size and detail of the broad random climate patterns
TemperatureLapse - how much colder the highest ground is; TemperatureNoise - random variation from the plain equator-to-pole pattern
MoistureNoiseWeight, MoistureCoastWeight, MoistureLatitudeWeight - how much moisture comes from random patterns, from nearness to water, and from latitude
MoistureReach - how far from water (in blocks) the coastal effect reaches; MoistureElevationGain - extra moisture on high ground
MoistureColdPenalty - how much drier cold places are (0 is not at all)
ContourFlatFrom, ContourHillyFrom, ContourJaggedFrom, ContourMountainousFrom - terrain roughness (mean elevation plus its spread) at which a region counts as each contour
BiomeTemperatureWeight, BiomePrecipitationWeight, BiomeContourWeight - how much each matters when choosing the biome nearest a region's climate
TerrainReliefStrength - how strongly high ground favours steep terrain (0 is not at all); ElevationPerRelief - how much elevation is one step of terrain relief

Region and border settings (also in NoiseGeneratorRules.csv)
RegionSeedCandidates - how hard region centres try to be far apart (candidates tried for each; 1 is purely random)
RegionSlopeWeight, RegionHeightWeight, RegionClimateWeight - how costly it is for a region to spread across a slope, onto high ground, or across a change of climate (higher keeps borders on ridges and climate transitions; 0 for all three is plain outward spreading)
RegionJitter - random unevenness in how far regions spread (higher gives more ragged borders)
RegionMinBlocks - smallest region kept; smaller ones are merged into the neighbour they share the most border with (1 keeps all)
RegionBorderDetail - 1 gives blocks on region borders a 10x10 grid of sub-blocks with winding borders, 0 leaves borders along block edges
RegionBorderWarp, RegionBorderFrequency - how far (in blocks) and how often region borders wander
DetailMinIsletCells - smallest fine-grained islet kept (in sub-blocks); smaller specks of land are dropped (1 keeps all)
CoastDetail and RegionBorderDetail are independent: either, both or neither can be on.

Settings that choose for you when left at 0 (also in NoiseGeneratorRules.csv)
LandFraction - exact share of the map that is land; 0 chooses at random from LandFractionMean, StdDev, Min and Max
ContinentCentres - exact number of places land gathers around; 0 chooses at random from the continent counts in WorldRules.csv
RegionTarget - exact total number of regions to aim for; 0 chooses at random from the region counts in WorldRules.csv
TemperatureOffset - shifts the whole world warmer (positive) or colder (negative); 0 is as usual
Any key in this file can also be set per world with a generator setting of the same name (see the main README).
