using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class OnlineActivityData
    {
        // Step mode and type of the online activity
        public OnlineActivityStepMode ActivityStepMode { get; set; } = OnlineActivityStepMode.Request; // Default to 'Request'
        public OnlineActivityType ActivityType { get; set; } = OnlineActivityType.None; // Default to 'None'

        // Map-related data
        public MapData MapData { get; set; } = new MapData(); // Initialize with a new MapData object
        public List<HumanData> MapHumans { get; set; } = new List<HumanData>(); // Initialize with an empty list of HumanData
        public List<AnimalData> MapAnimals { get; set; } = new List<AnimalData>(); // Initialize with an empty list of AnimalData
        public List<HumanData> CaravanHumans { get; set; } = new List<HumanData>(); // Initialize with an empty list of caravan HumanData
        public List<AnimalData> CaravanAnimals { get; set; } = new List<AnimalData>(); // Initialize with an empty list of caravan AnimalData

        // Miscellaneous data
        public string OtherPlayerName { get; set; } = string.Empty; // Default to an empty string
        public int FromTile { get; set; } = 0; // Default to 0, represents the tile from which the activity originates
        public int TargetTile { get; set; } = 0; // Default to 0, represents the target tile

        // Orders related to the online activity
        public PawnOrder PawnOrder { get; set; } = new PawnOrder(); // Initialize with a new PawnOrder object
        public CreationOrder CreationOrder { get; set; } = new CreationOrder(); // Initialize with a new CreationOrder object
        public DestructionOrder DestructionOrder { get; set; } = new DestructionOrder(); // Initialize with a new DestructionOrder object
        public DamageOrder DamageOrder { get; set; } = new DamageOrder(); // Initialize with a new DamageOrder object
        public HediffOrder HediffOrder { get; set; } = new HediffOrder(); // Initialize with a new HediffOrder object
        public TimeSpeedOrder TimeSpeedOrder { get; set; } = new TimeSpeedOrder(); // Initialize with a new TimeSpeedOrder object
        public GameConditionOrder GameConditionOrder { get; set; } = new GameConditionOrder(); // Initialize with a new GameConditionOrder object
        public WeatherOrder WeatherOrder { get; set; } = new WeatherOrder(); // Initialize with a new WeatherOrder object
        public KillOrder KillOrder { get; set; } = new KillOrder(); // Initialize with a new KillOrder object
    }
}
