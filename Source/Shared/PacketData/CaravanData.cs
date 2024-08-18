using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class CaravanData
    {
        // The current step mode of the caravan process
        public CaravanStepMode StepMode { get; set; } = CaravanStepMode.Add; // Default to 'Add', adjust as necessary

        // Details about the caravan
        public CaravanDetails Details { get; set; } = new CaravanDetails(); // Initialize with a new CaravanDetails object
    }
}