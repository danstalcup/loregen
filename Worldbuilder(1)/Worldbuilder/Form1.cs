using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using LoreGen.Simulation;
using LoreGen.Lang;
using LoreGen.Randomizer;
using LoreGen.WorldDisplay;
using LoreGen.WorldGen;
using LoreGen.Terrains;

namespace Worldbuilder
{
    /// <summary>
    /// Default form
    /// </summary>
    /// 
    public partial class Form1 : Form
    {
        /// <summary>
        /// Engine
        /// </summary>
        public SimEngine SimEngine;
        /// <summary>
        /// Test language
        /// </summary>
        public Language TestLanguage;

        public List<MapContext> Contexts;

        public MapContext CurrentContext = MapContext.General;
        private const bool TestBlocks = false;
        public bool DrawHiDef = true;
        public Tileset Tileset;
        public Continent CurrentContinent = null;
        public LoreGen.WorldGen.Region CurrentRegion = null;
        public MapZoomEnum CurrentZoom = MapZoomEnum.World;

        WorldBlock currentWorldBlock = null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="SimEngine">Simulation engine</param>
        public Form1(SimEngine SimEngine)
        {
            this.SimEngine = SimEngine;
            this.Tileset = new Tileset("tiles1.png");
            InitializeComponent();            
            mapTest.Paint += new PaintEventHandler(Redraw);
            mapTestContinent.Paint += new PaintEventHandler(ContinentRedraw);                    
            TestClimates();
            TestTerrains();
            TestBiomes();
            TestMapContexts();
            TestRegions();
            TestContinents();
        }

        private void TestMapContexts()
        {
            Contexts = new MapContext[]{MapContext.General, MapContext.Continents, MapContext.Regions, MapContext.Biomes, MapContext.Terrains, MapContext.TerrainTiles}.ToList();
            contextListBox.DataSource = Contexts; 
        }

        private void TestRegions()
        {
            StringBuilder txt = new StringBuilder();
            txt.Append("Regions:\n");
            //txt.Append("Name - Continent - Biome - X - Y - Height - Width\n");
            foreach(Continent c in SimEngine.World.Continents)
            {
                foreach(LoreGen.WorldGen.Region r in c.Regions)
                {
                    //txt.Append(r.Name+" - "+c.Name+" - "+r.Biome.Name+" - "+r.RegionBlock.X+" - "+r.RegionBlock.Y+" - "+r.RegionBlock.Height+" - "+r.RegionBlock.Width+"\n");                
                    txt.Append(r.Name + " - " + c.Name + " - " + r.RegionArea.BlocksList.Count+" - "+r.RegionArea.BlocksList.Where(b => b.Status.WaterStatus != LoreGen.WorldGen.WorldBlockWaterStatus.Water).Count()+"- "+r.RegionArea.ContiguousAreas().Count()+"\n");                
                }
                txt.Append("\n\n");
            }
            regionsTextBox.Text = txt.ToString();
        }

        private void TestClimates()
        {
            StringBuilder txt = new StringBuilder();
            txt.Append("Climate Types:\n");
            txt.Append("Name - ID\n");
            foreach(ClimateType ct in SimEngine.SimData.ClimateTypes)
            {
                txt.Append(ct.Name+" - "+ct.ID+"\n");                
            }
            txt.Append("\n\n");
            txt.Append("Climates:\n");
            txt.Append("Name - Primary - Secondary - ID\n");
            foreach(Climate c in SimEngine.SimData.Climates)
            {
                txt.Append(c.Name + " - " + c.PrimaryClimateType.Name + " - " + c.SecondaryClimateType.Name + " - " + c.ID+"\n");
            }
            climateTextBox.Text = txt.ToString();
        }

        private void TestTerrains()
        {
            StringBuilder txt = new StringBuilder();
            txt.Append("Terrain Types:\n");
            txt.Append("Name - ID\n");
            foreach (TerrainType tt in SimEngine.SimData.TerrainTypes)
            {
                txt.Append(tt.Name + " - " + tt.ID + "\n");
            }
            txt.Append("\n\n");
            txt.Append("Terrains:\n");
            txt.Append("Name - Terrain Type - Climate - ID\n");
            foreach (Terrain t in SimEngine.SimData.Terrains)
            {
                txt.Append(t.Name + " - " + t.TerrainType.Name + " - " + t.Climate.Name + " - " + t.ID + "\n");
            }
            terrainTextBox.Text = txt.ToString();
        }

        private void TestBiomes()
        {
            StringBuilder txt = new StringBuilder();
            txt.Append("Biomes:\n");
            txt.Append("Name - ID - Temperature - Precipitation - Contour - Seasons (temp) - Seasons (precip) - Habitability\n");
            foreach (Biome b in SimEngine.SimData.Biomes)
            {
                txt.Append(b.Name + " - " + b.ID + " - " + Biome.TraitName(b.Temperature) + " - " + Biome.TraitName(b.Precipitation)+" - "+Biome.TraitName(b.Contour)+" - "+Biome.TraitName(b.SeasonsTemp)+" - "+Biome.TraitName(b.SeasonsPrecip)+" - "+Biome.TraitName(b.Habitability)+"\n\n");
                txt.Append("Terrain Type Weights:\n");
                foreach(BiomeTerrainTypeWeight bttw in b.TerrainTypeWeights)
                {
                    txt.Append(bttw.TerrainType.Name + " - " + bttw.Weight + "\n");
                }
                txt.Append("\nClimate Type Weights:\n");
                foreach (BiomeClimateTypeWeight bctw in b.ClimateTypeWeights)
                {
                    txt.Append(bctw.ClimateType.Name + " - " + bctw.Weight + "\n");
                }
                txt.Append("\nClimate Weights:\n");
                foreach (BiomeClimateWeight bcw in b.ClimateWeights)
                {
                    txt.Append(bcw.Climate.Name + " - " + bcw.Weight.ToString("F") + "\n");
                }
                List<Terrain> terrains = b.TerrainPattern.GetAllElements();
                List<double> weights = b.TerrainPattern.GetAllWeights();
                txt.Append("\nTerrain Weights:\n");
                for(int i=0;i<terrains.Count;i++)
                {
                    txt.Append(terrains[i].Name + " - " + weights[i].ToString("F") + "\n");
                }
                txt.Append("\n\n");
            }            
            biomeTextBox.Text = txt.ToString();
        }

        private void RedrawOld(object sender, PaintEventArgs e)
        {
            var p = sender as Panel;            
            var g = e.Graphics;
            

            WorldBlock wb = SimEngine.World.WorldBlock;

            double width = mapTest.Width;
            double height = mapTest.Height;

            double widthScalar = width / SimEngine.World.WorldBlock.TrueWidth;
            double heightScalar = height / SimEngine.World.WorldBlock.TrueHeight;

            for (int i = 0; i < wb.Width; i++)
            {
                for (int j = 0; j < wb.Height; j++)
                {
                    bool draw = true;
                    WorldBlock block = SimEngine.World.WorldBlock.ChildBlocks[i,j];
                    Rectangle rec = block.AsRectangle(widthScalar,heightScalar);
                    //if (block.Status.WaterStatus == WorldBlockWaterStatus.Coastline)
                    {
                        if (block.ChildBlocks != null && DrawHiDef && CurrentContext != MapContext.TerrainTiles)
                        {
                            draw = false;
                            for (int x = 0; x < block.Width; x++)
                            {
                                for (int y = 0; y < block.Height; y++)
                                {
                                    Rectangle rec2 = block.ChildBlocks[x,y].AsRectangle(widthScalar,heightScalar);
                                    WorldBlock block2 = block.ChildBlocks[x, y];
                                    if (CurrentContext == MapContext.TerrainTiles && block2.Status.Terrain != null)
                                    {
                                        Tileset.DrawTile(block2.Status.Terrain.TerrainType.ID, block2.Status.Terrain.Climate.ID, ExpandBlockRectangle(rec2), g);
                                    }
                                    else
                                    {
                                        g.FillRectangle(new SolidBrush(Map.GetColor(block2, CurrentContext)), ExpandBlockRectangle(rec2));
                                        //g.DrawRectangle(new Pen(Map.GetColor(block2, CurrentContext)), ExpandBlockRectangle(rec2));
                                    }

                                    g.FillRectangle(new SolidBrush(Map.GetColor(block.ChildBlocks[x,y],MapContext.Regions)),ExpandBlockRectangle(block.ChildBlocks[x,y].AsRectangle(widthScalar,heightScalar)));
                                }
                            }
                        }
                    }

                    if (draw)                    
                    {
                        g.FillRectangle(new SolidBrush(Map.GetColor(block, MapContext.Regions)), ExpandBlockRectangle(rec));
                        if (CurrentContext == MapContext.TerrainTiles && block.Status.Terrain != null)
                        {
                            Tileset.DrawTile(block.Status.Terrain.TerrainType.ID, block.Status.Terrain.Climate.ID, ExpandBlockRectangle(rec), g);
                        }
                        else
                        {
                            //this can eventually be deleted!
                            if(TestBlocks)
                            {
                                if (block.Status.WaterStatus == WorldBlockWaterStatus.Water)
                                {
                                    //g.DrawRectangle(new Pen(Color.Blue), ExpandBlockRectangle(rec));
                                    g.FillRectangle(new SolidBrush(Color.Blue), ExpandBlockRectangle(rec));
                                }
                                else
                                {
                                    //g.DrawRectangle(new Pen(Color.Green), ExpandBlockRectangle(rec));
                                    g.FillRectangle(new SolidBrush(Color.Green), ExpandBlockRectangle(rec));
                                    //g.FillRectangle(new SolidBrush(Color.FromArgb(SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255))), ExpandBlockRectangle(rec));
                                }
                            }
                            else
                            {

                                g.FillRectangle(new SolidBrush(Map.GetColor(block, CurrentContext)), ExpandBlockRectangle(rec));                                
                                //g.DrawRectangle(new Pen(Map.GetColor(block, CurrentContext)), ExpandBlockRectangle(rec));
                            }
                        }
                    }

                }
            }


            /*foreach (Continent c in SimEngine.World.Continents)
            {
                WorldBlock b = c.ContinentBlock;
                double widthScalar = 400.0 / SimEngine.World.WorldBlock.TrueWidth;
                double heightScalar = 300.0 / SimEngine.World.WorldBlock.TrueHeight;
                Color contColor = Color.FromArgb(SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255));
                foreach (WorldGen.Region r in c.Regions)
                {
                    if (r.RegionBlock.ChildBlocks.Count > 0)
                    {
                        foreach (WorldBlock wb in r.RegionBlock.ChildBlocks)
                        {
                            Rectangle rec = wb.AsRectangle(widthScalar, heightScalar);
                            Color fillColor = Color.FromArgb(SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255));
                            if (wb.Status.WaterStatus == WorldBlockWaterStatus.Coastline)
                            {
                                fillColor = Color.Pink;
                            }
                            else if (wb.Status.WaterStatus == WorldBlockWaterStatus.Land)
                            {
                                fillColor = Color.Cyan;
                            }
                            else if (wb.Status.WaterStatus == WorldBlockWaterStatus.Water)
                            {
                                fillColor = Color.White;
                            }
                            g.FillRectangle(new SolidBrush(fillColor), ExpandBlockRectangle(rec));
                        }
                    }
                    else
                    {
                        Color fillColor = Color.Black;
                        if (r.RegionBlock.Status.WaterStatus == WorldBlockWaterStatus.Coastline)
                        {
                            fillColor = Color.Red;
                        }
                        else if (r.RegionBlock.Status.WaterStatus == WorldBlockWaterStatus.Land)
                        {
                            fillColor = Color.Green;
                        }


                        WorldBlock wb = r.RegionBlock;
                        Rectangle rec = wb.AsRectangle(widthScalar, heightScalar);

                        g.FillRectangle(new SolidBrush(fillColor), ExpandBlockRectangle(rec));
                    }
                    //contColor = Color.FromArgb(SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255));
                }
                foreach (Sea s in c.Seas)
                {                    
                    WorldBlock wb = s.SeaBlock;
                    g.FillRectangle(new SolidBrush(Color.Blue), ExpandBlockRectangle(wb.AsRectangle(widthScalar, heightScalar)));                    

                }
                //infoBox.Text += b.ToString() + "\n";
            }
            foreach (Ocean o in SimEngine.World.Oceans)
            {
                WorldBlock b = o.OceanBlock;
                double widthScalar = 400.0 / SimEngine.World.WorldBlock.TrueWidth;
                double heightScalar = 300.0 / SimEngine.World.WorldBlock.TrueHeight;
                Color contColor = Color.Blue;
                g.FillRectangle(new SolidBrush(contColor), ExpandBlockRectangle(b.AsRectangle(widthScalar, heightScalar)));
                contColor = Color.FromArgb(SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255));                
            }*/
            /*p.Visible = true;
            currentPanel.Visible = false;
            currentBuffer = currentPanel;
            currentPanel = p;*/            
        }

        private void ContinentRedraw(object sender, PaintEventArgs e)
        { }

        private void Redraw(object sender, PaintEventArgs e)
        {           
            var p = sender as Panel;
            var g = e.Graphics;

            Rectangle currentDisplayRectangle = GetCurrentDisplayRectangle();

            double width = currentDisplayRectangle.Width;
            double height = currentDisplayRectangle.Height;

            double ratio = width / height;

            /*double widthScalar = width / SimEngine.World.WorldBlock.TrueWidth;
            double heightScalar = height / SimEngine.World.WorldBlock.TrueHeight;*/

            WorldBlock[,] Grid = GetCurrentGrid();

                

            List<WorldBlock> GridList = Grid.Cast<WorldBlock>().ToList();

            double gridWidth = GridList.Max(wb => wb.TrueXMax)- GridList.Min(wb => wb.TrueX);
            double gridHeight = GridList.Max(wb => wb.TrueYMax) - GridList.Min(wb => wb.TrueY);
            double gridRatio = gridWidth / gridHeight;                        

            double widthScalar = width / gridWidth;
            double heightScalar = height / gridHeight;

           /* if(gridRatio > ratio)
            {
                heightScalar = widthScalar / ratio;
            }
            else if(gridRatio < ratio)
            {
                widthScalar = heightScalar * ratio;
            }*/

            Rectangle allBlocksRectangle = GetCurrentRectangle();

            int iMax = Grid.GetUpperBound(0);
            int jMax = Grid.GetUpperBound(1);

            for (int i = 0; i <= iMax; i++)
            {
                for (int j = 0; j <= jMax; j++)
                {
                    bool draw = true;
                    WorldBlock block = Grid[i, j];
                    Rectangle rec = block.AsRectangle(widthScalar, heightScalar, allBlocksRectangle);
                    //if (block.Status.WaterStatus == WorldBlockWaterStatus.Coastline)
                    {
                        if (block.ChildBlocks != null && DrawHiDef && CurrentContext != MapContext.TerrainTiles)
                        {
                            draw = false;
                            for (int x = 0; x < block.Width; x++)
                            {
                                for (int y = 0; y < block.Height; y++)
                                {
                                    Rectangle rec2x = block.ChildBlocks[x, y].AsRectangle(widthScalar, heightScalar);
                                    Rectangle rec2 = block.ChildBlocks[x, y].AsRectangle(widthScalar, heightScalar,allBlocksRectangle);
                                    WorldBlock block2 = block.ChildBlocks[x, y];
                                    if (CurrentContext == MapContext.TerrainTiles && block2.Status.Terrain != null)
                                    {
                                        Tileset.DrawTile(block2.Status.Terrain.TerrainType.ID, block2.Status.Terrain.Climate.ID, ExpandBlockRectangle(rec2), g);
                                    }
                                    else
                                    {
                                        g.FillRectangle(new SolidBrush(Map.GetColor(block2, CurrentContext)), ExpandBlockRectangle(rec2));
                                        //g.DrawRectangle(new Pen(Map.GetColor(block2, CurrentContext)), ExpandBlockRectangle(rec2));
                                    }

                                    //g.FillRectangle(new SolidBrush(Map.GetColor(block.ChildBlocks[x,y],MapContext.Regions)),ExpandBlockRectangle(block.ChildBlocks[x,y].AsRectangle(widthScalar,heightScalar)));
                                }
                            }
                        }
                    }

                    if (draw)                    
                    {
                        g.FillRectangle(new SolidBrush(Map.GetColor(block, MapContext.Regions)), ExpandBlockRectangle(rec));
                        if (CurrentContext == MapContext.TerrainTiles && block.Status.Terrain != null)
                        {
                            Tileset.DrawTile(block.Status.Terrain.TerrainType.ID, block.Status.Terrain.Climate.ID, ExpandBlockRectangle(rec), g);
                        }
                        else
                        {
                            //this can eventually be deleted!
                            if (TestBlocks)
                            {
                                if (block.Status.WaterStatus == WorldBlockWaterStatus.Water)
                                {
                                    //g.DrawRectangle(new Pen(Color.Blue), ExpandBlockRectangle(rec));
                                    g.FillRectangle(new SolidBrush(Color.Blue), ExpandBlockRectangle(rec));
                                }
                                else
                                {
                                    //g.DrawRectangle(new Pen(Color.Green), ExpandBlockRectangle(rec));
                                    g.FillRectangle(new SolidBrush(Color.Green), ExpandBlockRectangle(rec));
                                    //g.FillRectangle(new SolidBrush(Color.FromArgb(SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255), SimEngine.Rnd.Unweighted(0, 255))), ExpandBlockRectangle(rec));
                                }
                            }
                            else
                            {

                                g.FillRectangle(new SolidBrush(Map.GetColor(block, CurrentContext)), ExpandBlockRectangle(rec));
                                //g.DrawRectangle(new Pen(Map.GetColor(block, CurrentContext)), ExpandBlockRectangle(rec));
                            }
                        }
                    }

                }
            }
        }

        private Rectangle ExpandBlockRectangle(Rectangle rec)
        {
            rec.X = rec.X - 1;
            rec.Y = rec.Y - 1;
            rec.Width = rec.Width + 2;
            rec.Height = rec.Height + 2;
            return rec;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            TestLanguage = SimEngine.Language.GetRandomLanguage();


            StringBuilder text = new StringBuilder();
            int offset = (int)(DateTime.Now.Ticks % 10000);
            double total=0;            
            for(int i=0;i<1000;i++)
            {
                int numVals = (i + offset) % 3 + 3;
                int[] value = new int[numVals];
                for(int j=0;j<numVals;j++)
                {
                    value[j]=i+offset;
                }
                double next = SimEngine.Rnd.Next(value);
                total += next;
                text.Append( i.ToString() + ": " + next + "\n");
            }
            richTextBox1.Text = "Total: " + total + "\nAverage: " + (total / 100)+"\n";
            richTextBox1.Text += text.ToString();
        }        

        private void button2_Click(object sender, EventArgs e)
        {
            if (null == TestLanguage)
            {
                TestLanguage = SimEngine.Language.GetRandomLanguage();
            }
            
            richTextBox1.Text = TestLanguage.ToString();
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (null == TestLanguage)
            {
                TestLanguage = SimEngine.Language.GetRandomLanguage();
            }

            string s = "";
            for (int i = 0; i < 100; i++) 
            {
                s += TestLanguage.GetName()+"\n";
            }
            richTextBox1.Text = s;
        }

        private void TestContinents()
        {
            string s = "";
            s += "Seed: " + SimEngine.Rnd.Seed + "\n";
            s += "World size: " + SimEngine.World.WorldBlock.Width + "x" + SimEngine.World.WorldBlock.Height + "\n\n";
            foreach (Continent c in SimEngine.World.Continents)
            {
               /* s += c.Name + "\nPosition - (" + c.ContinentBlock.TrueX / 10000 + "," + c.ContinentBlock.TrueY / 10000 + ")\n" +
                    "Size - (" + c.ContinentBlock.TrueWidth / 10000 + "," + c.ContinentBlock.TrueHeight / 10000 + ")\n" +
                    "Area - " + (((long)(c.ContinentBlock.TrueHeight)) * ((long)c.ContinentBlock.TrueWidth) / ((long)100000000)) + "\n\n";*/
                s += c.Name + ": " + c.ContinentArea.BlocksList.Where(b => b.Status.WaterStatus == WorldBlockWaterStatus.Land || b.Status.WaterStatus == WorldBlockWaterStatus.Coastline).Count()+"\n";
            }
            s += "\n\nOceans:\n";
            foreach(Ocean o in SimEngine.World.Oceans)
            {
                s += o.Name + ": " + o.OceanArea.BlocksList.Count + "\n";
            }
            /*s += "\nCoastline Patterns:";
            foreach (EdgePattern cRaw in SimEngine.SimData.EdgePatterns)
            {
                EdgePattern c = cRaw.FitToLength(20);
                s += "Length: " + c.Length;
                s += "\nNorth: " + c.NorthEdge + "\nSouth: " + c.SouthEdge + "\nEast: " + c.EastEdge + "\nWest: " + c.WestEdge + "\n";
                for (int i = 0; i < c.Length; i++)
                {
                    for (int j = 0; j < c.Length; j++)
                    {
                        if (c.Pattern[j, i] == WorldBlockWaterStatus.Water)
                        {
                            s += "_";
                        }
                        else if (c.Pattern[j, i] == WorldBlockWaterStatus.Coastline)
                        {
                            s += "o";
                        }
                        else if (c.Pattern[j, i] == WorldBlockWaterStatus.Land)
                        {
                            s += "O";
                        }
                    }
                    s += "\n";
                }
            }*/
            testContInfoBox.Text = s;
        }

        private void testContinentButton_Click(object sender, EventArgs e)
        {
           
        }

        private void contextListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentContext = (MapContext)contextListBox.SelectedItem;
        }

        private void newWorldButton_Click(object sender, EventArgs e)
        {
            this.SimEngine.GenerateWorld();
            TestContinents();
            TestRegions();
            mapTest.Refresh();
        }

        private void contextListBox_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            CurrentContext = (MapContext)contextListBox.SelectedItem;
            mapTest.Refresh();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DrawHiDef = !DrawHiDef;
            mapTest.Refresh();
        }

        private void mapTest_Click(object sender, System.EventArgs e)
        {
            //Point point = new Point(mapTest.PointToClient)
            Point point = mapTest.PointToClient(Cursor.Position);
            if(!GetCurrentDisplayRectangle().Contains(point))
            {
                return;
            }
            //currentWorldBlock = SimEngine.World.WorldBlock.ChildBlockAtPoint(point, new Rectangle(0,0,mapTest.Width,mapTest.Height));
            currentWorldBlock = LoreGen.WorldGen.WorldTasks.GetBlockAtPoint(GetCurrentGrid(), point, GetCurrentDisplayRectangle());
            WorldBlockStatus status = currentWorldBlock.Status;
            //mapTest.Refresh();
            StringBuilder text = new StringBuilder("X: "+currentWorldBlock.X+" Y: "+currentWorldBlock.Y+"\n");
            text.Append("\n");
            if (status.Continent != null) text.Append("Continent: " + status.Continent.Name+"\n");
            if (status.Ocean != null) text.Append("Ocean: " + status.Ocean.Name + "\n");     
            text.Append("Water: " + (status.WaterStatus == WorldBlockWaterStatus.Water ? "Water" : status.WaterStatus == WorldBlockWaterStatus.Land ? "Land" : status.WaterStatus == WorldBlockWaterStatus.Coastline ? "Coastline" : "Unknown") + "\n");
            text.Append("\n");
            if (status.Region != null)
            {
                text.Append("Region: " + status.Region.Name + "\n");
                List<LoreGen.WorldGen.Region> NeighboringRegions = status.Region.NeighboringRegions();
                text.Append("Neighboring Regions: ");
                foreach(var region in NeighboringRegions)
                {
                    if (region != NeighboringRegions[0]) text.Append(", ");
                    text.Append(region.Name);
                }
                if(NeighboringRegions.Count == 0)
                {
                    text.Append("NONE");
                }
                text.Append("\n");
            }
            if (status.Region.Biome != null) text.Append("Regional Biome: " + status.Region.Biome.Name + "\n");
            if (status.Region.Language != null) text.Append("Regional Language: " + status.Region.Language.LanguageName + "\n");
            if (status.Sea != null) text.Append("Sea: " + status.Sea.Name + "\n");
            text.Append("\n");
            if (status.Terrain != null) text.Append("Terrain: " + status.Terrain.Name + "\n");
            if (status.Terrain != null) text.Append("Terrain Type: " + status.Terrain.TerrainType.Name + "\n");
            if (status.Terrain != null) text.Append("Climate: " + status.Terrain.Climate.Name + "\n");
            if (status.Terrain != null) text.Append("Primary Climate Type: " + status.Terrain.Climate.PrimaryClimateType.Name + "\n");
            if (status.Terrain != null && status.Terrain.Climate.PrimaryClimateType != status.Terrain.Climate.SecondaryClimateType)
                text.Append("Secondary Climate Type: " + status.Terrain.Climate.SecondaryClimateType.Name + "\n");            
            currentBlockInfo.Text = text.ToString();
        }

        private void mapTest_DoubleClick(object sender, System.EventArgs e)
        {
            //Point point = new Point(mapTest.PointToClient)
            Point point = mapTest.PointToClient(Cursor.Position);
            //currentWorldBlock = SimEngine.World.WorldBlock.ChildBlockAtPoint(point, new Rectangle(0, 0, mapTest.Width, mapTest.Height));
            currentWorldBlock = LoreGen.WorldGen.WorldTasks.GetBlockAtPoint(GetCurrentGrid(), point, GetCurrentDisplayRectangle());
            
            WorldBlockStatus status = currentWorldBlock.Status;
            CurrentContinent = status.Continent;
            CurrentRegion = status.Region;

            switch(CurrentZoom)
            { 
                case MapZoomEnum.World:
                    if (CurrentContinent != null)
                    {
                        CurrentZoom = MapZoomEnum.Continent;
                        mapBackButton.Enabled = true;
                    }
                    break;
                case MapZoomEnum.Continent:
                    if(CurrentRegion != null)
                    {
                        CurrentZoom = MapZoomEnum.Region;
                        mapBackButton.Enabled = true;
                    }
                    break;
            }

            //tabs.SelectedTab = currentContinentTab;            
            mapTest.Refresh();
        }

        private void mapBackButton_Click(object sender, EventArgs e)
        {
            if (CurrentZoom == MapZoomEnum.Continent)
            {
                CurrentZoom = MapZoomEnum.World;
                mapBackButton.Enabled = false;
            }
            if(CurrentZoom == MapZoomEnum.Region)
            {
                CurrentZoom = MapZoomEnum.Continent;                
            }
            mapTest.Refresh();
        }

        private Rectangle GetCurrentRectangle()
        {
            List<WorldBlock> GridList = GetCurrentGrid().Cast<WorldBlock>().ToList();
            double gridWidth = GridList.Max(wb => wb.TrueXMax) - GridList.Min(wb => wb.TrueX);
            double gridHeight = GridList.Max(wb => wb.TrueYMax) - GridList.Min(wb => wb.TrueY);            
            return new Rectangle(GridList.Min(wb => wb.TrueX), GridList.Min(wb => wb.TrueY), (int)gridWidth, (int)gridHeight);
        }

        private WorldBlock[,] GetCurrentGrid()
        {
            if (CurrentZoom == MapZoomEnum.Continent && null != CurrentContinent)
            {
                return CurrentContinent.ContinentArea.BlocksAsGrid();
            }
            else if(CurrentZoom == MapZoomEnum.Region && null != CurrentRegion)
            {
                return CurrentRegion.RegionArea.BlocksAsGrid();
            }
            else
            {
                return SimEngine.World.WorldBlock.ChildBlocks;
            }
        }
        
        private Rectangle GetCurrentDisplayRectangle()
        {
            double displayWidth = mapTest.Width;
            double displayHeight = mapTest.Height;            

            //double displayRatio = displayWidth / displayHeight;

            Rectangle regionRectangle = GetCurrentRectangle();

            double regionWidth = regionRectangle.Width;
            double regionHeight = regionRectangle.Height;

            //double regionRatio = regionWidth / regionHeight;

            //double widthsRatio = regionWidth / displayWidth;
            //double heightsRatio = regionHeight / displayHeight;

            double widthsRatio = regionWidth / displayWidth;
            double heightsRatio =  regionHeight / displayHeight;

            double minRatio = Math.Min(widthsRatio, heightsRatio);
            double maxRatio = Math.Max(widthsRatio, heightsRatio);

            widthsRatio /= maxRatio;
            heightsRatio /= maxRatio;


            return new Rectangle(0, 0, (int)(mapTest.Width * widthsRatio), (int)(mapTest.Height * heightsRatio));
        }
    }

    public enum MapZoomEnum
    {
        World, Continent, Region
    }
}

