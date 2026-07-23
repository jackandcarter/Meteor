using System;

namespace AetherXIV.Core.Map.actors.area
{
    /// <summary>
    /// Dynamic content areas exist only for the lifetime of one map-server
    /// process. Character persistence must never retain a dynamic area name or
    /// instance number. Content with a confirmed retry entrance also returns to
    /// that entrance so its quest can reconstruct the duty from the beginning.
    /// </summary>
    static class TransientContentRecoveryPolicy
    {
        public static bool IsTransient(string privateAreaName)
        {
            return !String.IsNullOrEmpty(privateAreaName) &&
                privateAreaName.StartsWith("SimpleContent", StringComparison.Ordinal);
        }

        public static bool TryGetRecoveryPoint(
            string privateAreaName,
            out uint zoneId,
            out float x,
            out float y,
            out float z,
            out float rotation)
        {
            zoneId = 0;
            x = 0;
            y = 0;
            z = 0;
            rotation = 0;

            if (!String.Equals(privateAreaName, "SimpleContentMan0g101", StringComparison.OrdinalIgnoreCase))
                return false;

            // White Wolf Gate, immediately before Man0g1's escort-duty push.
            zoneId = 155;
            x = -194.73f;
            y = 3.54f;
            z = -1021.33f;
            rotation = -1.642f;
            return true;
        }
    }
}
