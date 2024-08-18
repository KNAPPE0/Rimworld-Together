using System;

namespace Shared
{
    public static class TimeConverter
    {
        // Convert the current time to Unix Epoch time in milliseconds
        public static double CurrentTimeToEpoch()
        {
            return Math.Round((DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds);
        }

        // Check if the current time exceeds a specified epoch time plus an additional value
        public static bool CheckForEpochTimer(double toCompare, double extraValue)
        {
            return CurrentTimeToEpoch() > toCompare + extraValue;
        }
    }
}