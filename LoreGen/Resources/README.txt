Last updated: 4/9/16

WorldRules.csv

This file contains the probabilities used when generating the layout of a world. Values must remain in order. All values read from the second column.
Format for each row:
Description of value [,]
Value

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

This file contains the probabilities used when generating new words in new languages. Values must remain in order. All values read from the second column.
Format for each row:
Description of value [,]
Value

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
ID Number

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

This file contains values used when generating biome and terrain patterns. Values must remain in order. All values read from the second column.
Format for each row:
Description of value [,]
Value