using System;
using System.Collections.Generic;
using System.Linq;

using DisplayProfileManager.Audio.Models;

namespace DisplayProfileManager.Audio
{
    public enum AudioMatchKind { None, Id, Hardware, Name }

    // Finds a saved device again after Windows gives it a new endpoint ID (new USB port, driver reinstall)
    public static class AudioDeviceMatcher
    {
        public static (AudioDeviceInfo Device, AudioMatchKind Kind) Resolve(string savedId, string savedName, string savedHardwareId, IReadOnlyList<AudioDeviceInfo> active)
        {
            if (active == null || active.Count == 0) return (null, AudioMatchKind.None);

            var byId = active.FirstOrDefault(d => string.Equals(d.Id, savedId, StringComparison.OrdinalIgnoreCase));
            if (byId != null) return (byId, AudioMatchKind.Id);

            if (!string.IsNullOrEmpty(savedHardwareId))
            {
                var sameHardware = active.Where(d => string.Equals(d.HardwareId, savedHardwareId, StringComparison.OrdinalIgnoreCase)).ToList();
                // One USB device can expose several endpoints (mic, line in), so the name breaks ties
                if (sameHardware.Count > 1)
                    sameHardware = sameHardware.Where(d => SameName(d, savedName)).ToList();
                if (sameHardware.Count == 1) return (sameHardware[0], AudioMatchKind.Hardware);
            }

            if (!string.IsNullOrEmpty(savedName))
            {
                var sameName = active.Where(d => SameName(d, savedName)).ToList();
                // Two identical devices are ambiguous, so don't guess
                if (sameName.Count == 1) return (sameName[0], AudioMatchKind.Name);
            }

            return (null, AudioMatchKind.None);
        }

        // "{1}.USB\VID_0909&PID_001C&MI_00\9&2F9204D7&0&0000" -> "USB\VID_0909&PID_001C&MI_00"
        // The last segment is the per-port instance; the rest stays the same on every port
        public static string ToHardwareId(string parentInstancePath)
        {
            if (string.IsNullOrWhiteSpace(parentInstancePath)) return string.Empty;

            string path = parentInstancePath.Trim();
            if (path.StartsWith("{"))
            {
                int dot = path.IndexOf("}.", StringComparison.Ordinal);
                if (dot >= 0) path = path[(dot + 2)..];
            }

            int last = path.LastIndexOf('\\');
            return last > 0 ? path[..last].ToUpperInvariant() : string.Empty;
        }

        private static bool SameName(AudioDeviceInfo device, string name) =>
            !string.IsNullOrEmpty(name) && string.Equals(device.SystemName ?? device.Name, name, StringComparison.OrdinalIgnoreCase);
    }
}
