using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoreGen.WorldDisplay
{
    public class Tileset
    {
        public Image<Rgba32> MyOriginalTilesheet;
        public Image<Rgba32>[,] Tiles;
        public int TileWidth;
        public int TileHeight;
        public int NumTilesX;
        public int NumTilesY;
        public int TilesetType;

        public Tileset(string filename)
        {
            MyOriginalTilesheet = Image.Load<Rgba32>(filename);
            TileWidth = 15;
            TileHeight = 15;
            NumTilesX = 32;
            NumTilesY = 29;
            TilesetType = 1;
            //InitializeTiles();
        }

        public Tileset(string filename, int tileX, int tileY, int numTilesX, int numTilesY, int tilesetType)
        {
            MyOriginalTilesheet = Image.Load<Rgba32>(filename);
            TileWidth = tileX;
            TileHeight = tileY;
            NumTilesX = numTilesX;
            NumTilesY = numTilesY;
            TilesetType = tilesetType;
            //InitializeTiles();
        }

        private void InitializeTiles()
        {
            Tiles = new Image<Rgba32>[NumTilesX, NumTilesY];
            for (int x = 0; x < NumTilesX; x++)
                for (int y = 0; y < NumTilesY; y++)
                    Tiles[x, y] = MyOriginalTilesheet.Clone(ctx => ctx.Crop(new Rectangle(x * TileWidth, y * TileHeight, TileWidth, TileHeight)));
        }

        public Image<Rgba32> Tile(int tile)
        {
            return Tile(tile % NumTilesX, tile / NumTilesX);
        }

        public Image<Rgba32> Tile(int x, int y)
        {
            return Tiles[x, y];
        }

        public void DrawTile(int tile, Rectangle destination, Image<Rgba32> target)
        {
            DrawTile(tile / NumTilesX, tile % NumTilesX, destination, target);
        }

        public void DrawTile(int x, int y, Rectangle destination, Image<Rgba32> target)
        {
            using (Image<Rgba32> sourceTile = MyOriginalTilesheet.Clone(ctx => ctx.Crop(new Rectangle(x * TileWidth, y * TileHeight, TileWidth, TileHeight))))
            {
                target.Mutate(ctx =>
                {
                    if (destination.Width != TileWidth || destination.Height != TileHeight)
                    {
                        sourceTile.Mutate(sctx => sctx.Resize(destination.Width, destination.Height));
                    }
                    ctx.DrawImage(sourceTile, new Point(destination.X, destination.Y), 1f);
                });
            }
        }

        public Image<Rgba32> GetClone(int x, int y)
        {
            return MyOriginalTilesheet.Clone(ctx => ctx.Crop(new Rectangle(x * TileWidth, y * TileHeight, 15, 15)));
        }

        public Image<Rgba32> CopyTile(int x, int y)
        {
            Rectangle rect = new Rectangle(0, 0, TileWidth, TileHeight);
            Image<Rgba32> bmp = new Image<Rgba32>(rect.Width, rect.Height);
            DrawTile(x, y, rect, bmp);
            return bmp;
        }

    }
}
