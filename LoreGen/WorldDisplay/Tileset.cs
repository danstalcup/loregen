using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace LoreGen.WorldDisplay
{
    public class Tileset
    {
        public Bitmap MyOriginalTilesheet;
        public Bitmap[,] Tiles;
        public int TileWidth;
        public int TileHeight;
        public int NumTilesX;
        public int NumTilesY;
        public int TilesetType;

        public Tileset(string filename)
        {
            MyOriginalTilesheet = new Bitmap(filename);
            TileWidth = 15;
            TileHeight = 15;
            NumTilesX = 32;
            NumTilesY = 29;
            TilesetType = 1;
            //InitializeTiles();
        }

        public Tileset(string filename, int tileX, int tileY, int numTilesX, int numTilesY, int tilesetType)
        {
            MyOriginalTilesheet = new Bitmap(filename);
            TileWidth = tileX;
            TileHeight = tileY;
            NumTilesX = numTilesX;
            NumTilesY = numTilesY;
            TilesetType = tilesetType;
            //InitializeTiles();
        }

        private void InitializeTiles()
        {
            Tiles = new Bitmap[NumTilesX, NumTilesY];
            for (int x = 0; x < NumTilesX; x++)
                for (int y = 0; y < NumTilesY; y++)
                    Tiles[x, y] = MyOriginalTilesheet.Clone(new Rectangle(x * TileWidth, y * TileHeight, TileWidth, TileHeight), System.Drawing.Imaging.PixelFormat.Format8bppIndexed);//System.Drawing.Imaging.PixelFormat.DontCare);
        }

        public Image Tile(int tile)
        {
            return Tile(tile % NumTilesX, tile / NumTilesX);
        }

        public Image Tile(int x, int y)
        {
            return Tiles[x, y];
        }

        public void DrawTile(int tile, Rectangle destination, Graphics g)
        {
            DrawTile(tile / NumTilesX, tile % NumTilesX, destination, g);
        }

        public void DrawTile(int x, int y, Rectangle destination, Graphics g)
        {
            g.DrawImage(MyOriginalTilesheet, destination, x * TileWidth, y * TileHeight, TileWidth, TileHeight, GraphicsUnit.Pixel);
        }

        public Bitmap GetClone(int x, int y)
        {
            return MyOriginalTilesheet.Clone(new Rectangle(x * TileWidth, y * TileHeight, 15, 15), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }
        public Bitmap CopyTile(int x, int y)
        {
            Rectangle rect = new Rectangle(0, 0, TileWidth, TileHeight);
            Bitmap bmp = new Bitmap(rect.Width, rect.Height);
            Graphics g = Graphics.FromImage(bmp);
            DrawTile(x, y, rect, g);
            g.Dispose();
            return bmp;
        }

    }
}
