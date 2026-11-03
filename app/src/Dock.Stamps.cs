using System;

namespace GamesDock
{
    public partial class Dock
    {
        struct ShapeStamp
        {
            public string Position;
            public bool Magnify;
            public double Width, Height, Hover, Pointer, Magnification, First, Last;
            public bool Matches(ShapeStamp other)
            {
                return Position == other.Position && Magnify == other.Magnify && Width == other.Width && Height == other.Height
                    && Hover == other.Hover && Pointer == other.Pointer && Magnification == other.Magnification
                    && First == other.First && Last == other.Last;
            }
        }
        struct BarStamp
        {
            public string Position;
            public double Inset, Overrun;
            public bool Fits;
            public bool Matches(BarStamp other)
            {
                return Position == other.Position && Inset == other.Inset && Overrun == other.Overrun && Fits == other.Fits;
            }
        }
    }
}
