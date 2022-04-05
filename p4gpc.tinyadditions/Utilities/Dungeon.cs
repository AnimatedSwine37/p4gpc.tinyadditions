using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.tinyadditions.Utilities
{
    public class Dungeon
    {
        public string Name { get; }
        public int StartFloor { get; }
        public int EndFloor { get; }

        public Dungeon(string name, int startFloor, int endFloor)
        {
            Name = name;
            StartFloor = startFloor;
            EndFloor = endFloor;
        }
    }
}
