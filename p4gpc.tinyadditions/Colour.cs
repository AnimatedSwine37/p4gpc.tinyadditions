using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.tinyadditions
{
    public class Colour
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public Colour(byte r, byte g, byte b) { R = r; G = g; B = b; }

        public override string ToString()
        {
            return $"{R}, {G}, {B}";
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(Colour))
                return false;
            Colour colour = (Colour)obj;
            return colour.R == R && colour.G == G && colour.B == B;
        }

        public override int GetHashCode()
        {
            return R + G << 8+ B << 16;
        }
    }
}
