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

This file contains the probabilities used when generating new words in new languages (PhoneticLanguage).
Same key-based format as WorldRules.csv; keys match the fields of the LanguageRules struct, e.g. LongRimeProbability.


CommonNameFlavors.csv

This file contains the naming flavours that CommonLanguage blends: families of naming traditions such as Latinate.
Format for each row:
Flavor name [,]
Slot: Root, Middle, Ending, LandEnding, RegionEnding, WaterEnding or LangEnding [,]
Pieces for that slot [separated by ,s]
Every flavour needs one row for every slot. Blank lines and lines starting with # are ignored.


CommonNameTemplates.csv

This file contains the templates CommonLanguage builds names from, such as "The Sea of {SeaNoun}".
Format for each row:
Kind: Language, World, Continent, Region, Sea or Ocean [,]
Style: Proper or Descriptive [,]
Weight (how often it is chosen relative to others of the same kind and style) [,]
Template
The comments at the top of the file list the tokens. Every kind needs a Proper template that uses no {Dir}, {DirAdj}
or {Parent}, so that a name can always be made.


CommonNameVocabulary.csv

This file contains the English words for CommonLanguage's descriptive names, and which biomes count as which kind of land.
Format for each row:
List (Adj, AdjC, LandAdj, SeaAdj, SeaNoun, Feature, FeatureC or BiomeKeyword) [,]
Key: Any, or a biome category such as Wetland [,]
Words [separated by ,s]
A BiomeKeyword row lists words that put a biome in that category when found in its name; the first matching row wins.


CommonLanguageRules.csv

This file contains the settings for CommonLanguage: how often names are descriptive, how flavours are blended, and
how long words may be. Same key-based format as WorldRules.csv; each row's description says what it does.

ClimateTypes.csv

This file contains the Climate Types.
Format for each row:
Name [,]
ID Number [,]
Color Name (*) [,]
Description [,]
Ideal Temperature [,] (optional, 0 coldest to 1 hottest)
Ideal Moisture (optional, 0 driest to 1 wettest)

The ideal temperature and moisture are the climate this type is most typical of (0.5 each if left out). Generators that
model climate (such as "noise" and "tectonic") give each place the climates nearest its own temperature and moisture: a
climate with two types sits a third of the way from its primary type's ideal toward its secondary type's.

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
Relief [,] (optional, 0-4; default 1). How high and steep this terrain is: 0 = flat lowland, 2 = hills, 3 = cliffs, 4 = peaks.
Wetness [,] (optional, 0-1; default 0). How much standing water it has: 1 = marsh.
Trees (optional, 0-1; default 0). How wooded it is: 1 = dense forest.
Generators that model elevation and climate (such as "noise" and "tectonic") put higher relief on higher ground, wetter
terrain where water gathers on flat ground, and more trees where it is wet and warm. The "legacy" generator ignores them.

TerrainInfluences.csv

How features and plate boundaries change the terrain on them, for generators that model elevation and climate.
Format for each row:
Influence [,] (a feature type Id from FeatureTypes.csv, or Plate:Convergent, Plate:Divergent or Plate:Transform)
ReliefAdd [,] ReliefMin [,] ReliefMax [,] (relief steps, 0-4)
WetMin [,] WetMax [,] TreesMin [,] TreesMax [,] (0-1)
Moisture [,] (added to the moisture the place's climate is judged by)
Description (may contain commas)
Leave a column blank for no change. Each place's terrain has a target relief (from its height), wetness (from how much
water it holds) and tree cover (from how wet and warm it is). An influence adds ReliefAdd to the relief, then keeps the
three targets within its minimums and maximums. The plate boundary applies first, then features from the largest scope
to the smallest, so a part (a peak, an oasis) has the last word over the feature it is part of. Feature types without a
row leave the terrain alone.

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
BiomeTerrainWeight - how strongly a region's biome is chosen to fit the terrain in it rather than only its average climate

Terrain settings (also in NoiseGeneratorRules.csv)
Each place's terrain is scored against a target relief (from its height), wetness and tree cover (from how much water it
holds and how warm it is) and its climate (temperature and moisture), as moved by the features on it
(TerrainInfluences.csv). It takes the landscape (broad climate, ruggedness and tree cover) whose terrains fit best, then
one of that landscape's terrains at random in proportion to how well each fits.
TerrainReliefStrength - how strongly high ground favours steep terrain (0 is not at all); ElevationPerRelief - how much elevation is one step of terrain relief
TerrainClimateSpread - how far a place's temperature and moisture can be from a climate's ideal and still often get it
TerrainWetStrength, TerrainTreeStrength - how strongly wet ground favours wet terrain, and wet, warm ground wooded terrain (0 is not at all)
WetlandFrom - how much water flat ground must hold (moisture plus river and lake water) before it starts to turn to marsh
TreeMoistureFrom, TreeMoistureFull - how much water ground must hold for trees to start growing, and to be fully wooded
TreeTemperatureFrom - temperature below which no trees grow; TreeLineRelief - terrain relief above which no trees grow
RiverHydration, LakeHydration - how much wetter ground is where a large river's worth of water flows, and in a lake's hollow and beside its shore
TerrainPatchSize - about how wide patches of one terrain are (in top-level blocks); 0 draws each place on its own
TerrainSmoothing - how readily a top-level block takes the landscape most of the blocks around it have (0 never, 1 whenever most share one)

Region and border settings (also in NoiseGeneratorRules.csv)
Regions are defined by terrain and features: each continent is split into zones (connected stretches of one landscape
under one set of features), every zone of at least RegionZoneMinBlocks gets region centres by its size (the first at its
heart), and regions spread from there, keeping to their zone. With RegionBorderDetail, borders through sub-blocks are
drawn again once the fine terrain is known, so they follow it too.
RegionTerrainWeight - how costly it is for a region to spread into a different landscape (times how different: 1 for another kind of climate, 0.5 for each step of ruggedness and for wooded against open)
RegionFeatureWeight - how costly it is for a region to spread into or out of a feature such as a mountain range
RegionZoneMinBlocks - smallest zone that gets a region of its own; smaller ones join a neighbouring region
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

Feature settings (also in NoiseGeneratorRules.csv)
MountainRanges, Deserts - 1 finds named mountain ranges / deserts, 0 does not; the other Mountain*, Desert* and Feature* keys tune them
FeatureDetail - 1 gives feature edges, and every block of a mountain range (and of a desert when DesertParts is on), a 10x10 grid of sub-blocks; the parts of features below need it
SubRanges, SubRangeShare, SubRangeMinBlocks, SubRangesPerRange, SubrangeElongationFrom - the crests of each range, as subranges (long) or massifs (compact)
PeaksPerRange, PassesPerRange - how many peaks and passes each range gets (0 for none); PeakProminenceFrom - how far a summit must stand above the saddle joining it to higher ground to count
DesertParts - 1 gives desert ground gentle rises and hollows (DesertRelief, DesertReliefFrequency) and finds its ergs, salt flats and oases
BasinDepthFrom - how deep a closed hollow must be to count as a basin; SaltFlatMinBlocks - basin floors this big are salt flats, smaller ones can hold oases
SaltFlatsPerDesert, OasesPerDesert, ErgsPerDesert - most of each named per desert; ErgShare, ErgMinBlocks - how much of a desert is sand sea, and the smallest erg kept

Water settings (also in NoiseGeneratorRules.csv)
RainShadow, RainShadowReach - how much drier land downwind of high ground is, and how far upwind (in blocks) the high ground counts
Hydrology - 1 runs the rain off the land into lakes and rivers, 0 does not; RunoffEvaporation - how much warmth cuts the water that runs off
LakeDepthFrom - how deep a hollow must be to hold a lake; LakeEvaporation - how much water a lake loses for each block of its hollow (fed less, it is a salt lake)
LakeMinFill - least share of that loss the inflow must make up for there to be a lake at all; LakeAreaShare - share of its hollow a full lake covers; LakeMinBlocks - smallest lake kept (sub-blocks)
RiverFlowFrom - how much water a river must carry to be named; RiverMinBlocks - shortest river kept (blocks)
RiverMeander - how much rivers wind; RiverWidth - a river's width in km where it starts (it widens downstream)
WaterFeatures - 1 makes oceans and seas features with bays and straits; BayEnclosure, BayReach, BayMinBlocks, BaysPerBody - how bays are found and how many are named
StraitMaxWidth, StraitMinBlocks, StraitsPerBody - how narrow a strait is, the smallest kept, and how many are named


FeatureTypes.csv

This file contains the kinds of geographic feature (mountain ranges, deserts, lakes...) that world generators can find.
Format for each row (the comment lines at the top of the file explain each column):
Id,Name,Category,Shape,Scope,Parent,NameKind,Description
Parent may list several types separated by '|' (a bay can be part of an ocean or a sea).


TectonicGeneratorRules.csv

This file contains the plate settings for the tectonic world generator (number of plates, continental crust, mountain
building at plate boundaries). Same key-based format as WorldRules.csv. Everything after the elevation uses
NoiseGeneratorRules.csv. Any key in this file can also be set per world with a generator setting of the same name.
