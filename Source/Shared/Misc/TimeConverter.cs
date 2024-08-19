using System;

namespace Shared
{
    public static class TimeConverter
    {
        // Define Unix Epoch (January 1, 1970 00:00:00 UTC)
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Convert the current time to Unix Epoch time in milliseconds
        public static double CurrentTimeToEpoch()
        {
            return Math.Round((DateTime.UtcNow - UnixEpoch).TotalMilliseconds);
        }

        // Check if the current time exceeds a specified epoch time plus an additional value
        public static bool CheckForEpochTimer(double toCompare, double extraValue)
        {
            return CurrentTimeToEpoch() > toCompare + extraValue;
        }
    }
}