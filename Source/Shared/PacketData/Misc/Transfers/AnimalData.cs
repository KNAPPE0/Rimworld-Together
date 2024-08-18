using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public class AnimalData
    {
        // Basic Information
        public string DefName { get; set; } = "";
        public string Name { get; set; } = "";
        public string BiologicalAge { get; set; } = "";
        public string ChronologicalAge { get; set; } = "";
        public string Gender { get; set; } = "";
        public string FactionDef { get; set; } = "";
        public string KindDef { get; set; } = "";

        // Health Conditions (Hediffs)
        public List<string> HediffDefNames { get; set; } = new List<string>();
        public List<string> HediffPartDefNames { get; set; } = new List<string>();
        public List<string> HediffSeverities { get; set; } = new List<string>();
        public List<bool> HediffPermanents { get; set; } = new List<bool>();

        // Trainables
        public List<string> TrainableDefNames { get; set; } = new List<string>();
        public List<bool> CanTrain { get; set; } = new List<bool>();
        public List<bool> HasLearned { get; set; } = new List<bool>();
        public List<bool> IsDisabled { get; set; } = new List<bool>();

        // Transform (Position and Rotation)
        public string[] Position { get; set; } = Array.Empty<string>();
        public int Rotation { get; set; } = 0;
    }
}