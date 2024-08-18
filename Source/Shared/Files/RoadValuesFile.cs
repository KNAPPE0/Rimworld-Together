using System;

namespace Shared
{
    [Serializable]
    public class RoadValuesFile
    {
        public bool AllowDirtPath { get; private set; } = true;
        public bool AllowDirtRoad { get; private set; } = true;
        public bool AllowStoneRoad { get; private set; } = true;
        public bool AllowAsphaltPath { get; private set; } = true;
        public bool AllowAsphaltHighway { get; private set; } = true;

        public int DirtPathCost { get; private set; } = 10;
        public int DirtRoadCost { get; private set; } = 20;
        public int StoneRoadCost { get; private set; } = 25;
        public int AsphaltPathCost { get; private set; } = 30;
        public int AsphaltHighwayCost { get; private set; } = 50;

        // Parameterless constructor for default initialization
        public RoadValuesFile() { }

        // Constructor to allow initialization with specific values
        public RoadValuesFile(bool allowDirtPath, bool allowDirtRoad, bool allowStoneRoad, bool allowAsphaltPath, bool allowAsphaltHighway,
                              int dirtPathCost, int dirtRoadCost, int stoneRoadCost, int asphaltPathCost, int asphaltHighwayCost)
        {
            AllowDirtPath = allowDirtPath;
            AllowDirtRoad = allowDirtRoad;
            AllowStoneRoad = allowStoneRoad;
            AllowAsphaltPath = allowAsphaltPath;
            AllowAsphaltHighway = allowAsphaltHighway;

            DirtPathCost = dirtPathCost;
            DirtRoadCost = dirtRoadCost;
            StoneRoadCost = stoneRoadCost;
            AsphaltPathCost = asphaltPathCost;
            AsphaltHighwayCost = asphaltHighwayCost;
        }
    }
}