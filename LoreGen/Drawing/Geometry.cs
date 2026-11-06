using System;
using System.Collections.Generic;

namespace LoreGen.Drawing
{
    /// <summary>
    /// Platform-neutral replacement for System.Drawing.Point
    /// </summary>
    public struct Point
    {
        public int X;
        public int Y;
        public Point(int x, int y) { X = x; Y = y; }
    }

    /// <summary>
    /// Platform-neutral replacement for System.Drawing.Size
    /// </summary>
    public struct Size
    {
        public int Width;
        public int Height;
        public Size(int width, int height) { Width = width; Height = height; }
    }

    /// <summary>
    /// Platform-neutral replacement for System.Drawing.Rectangle
    /// </summary>
    public struct Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rectangle(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
        public Rectangle(Point location, Size size) : this(location.X, location.Y, size.Width, size.Height) { }

        public bool Contains(Point p)
        {
            return p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;
        }
    }

    /// <summary>
    /// Platform-neutral replacement for System.Drawing.Color (RGB only)
    /// </summary>
    public struct Color
    {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }

        private Color(int r, int g, int b)
        {
            R = (byte)Math.Max(0, Math.Min(255, r));
            G = (byte)Math.Max(0, Math.Min(255, g));
            B = (byte)Math.Max(0, Math.Min(255, b));
        }

        public static Color FromArgb(int r, int g, int b) { return new Color(r, g, b); }

        /// <summary>Packed 0xRRGGBB value</summary>
        public int ToRgb() { return (R << 16) | (G << 8) | B; }

        public string ToHex() { return "#" + ToRgb().ToString("x6"); }

        private static readonly Dictionary<string, Color> Named = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            {"Black", new Color(0,0,0)}, {"White", new Color(255,255,255)},
            {"Red", new Color(255,0,0)}, {"DarkRed", new Color(139,0,0)},
            {"Green", new Color(0,128,0)}, {"DarkGreen", new Color(0,100,0)},
            {"Blue", new Color(0,0,255)}, {"DarkBlue", new Color(0,0,139)}, {"LightBlue", new Color(173,216,230)},
            {"Yellow", new Color(255,255,0)}, {"Orange", new Color(255,165,0)}, {"Purple", new Color(128,0,128)},
            {"GreenYellow", new Color(173,255,47)}, {"SpringGreen", new Color(0,255,127)},
            {"OliveDrab", new Color(107,142,35)}, {"Tan", new Color(210,180,140)},
            {"SaddleBrown", new Color(139,69,19)}, {"Gold", new Color(255,215,0)},
            {"Gray", new Color(128,128,128)}, {"Brown", new Color(165,42,42)},
        };

        /// <summary>
        /// Looks up a color by its (case-insensitive) name. Unknown names yield black.
        /// </summary>
        public static Color FromName(string name)
        {
            Color c;
            return name != null && Named.TryGetValue(name.Trim(), out c) ? c : Black;
        }

        public static Color Black { get { return Named["Black"]; } }
        public static Color White { get { return Named["White"]; } }
        public static Color Red { get { return Named["Red"]; } }
        public static Color DarkRed { get { return Named["DarkRed"]; } }
        public static Color Green { get { return Named["Green"]; } }
        public static Color DarkGreen { get { return Named["DarkGreen"]; } }
        public static Color Blue { get { return Named["Blue"]; } }
        public static Color DarkBlue { get { return Named["DarkBlue"]; } }
        public static Color LightBlue { get { return Named["LightBlue"]; } }
        public static Color Yellow { get { return Named["Yellow"]; } }
        public static Color Orange { get { return Named["Orange"]; } }
        public static Color Purple { get { return Named["Purple"]; } }
        public static Color SpringGreen { get { return Named["SpringGreen"]; } }
        public static Color OliveDrab { get { return Named["OliveDrab"]; } }
        public static Color Tan { get { return Named["Tan"]; } }
    }
}
