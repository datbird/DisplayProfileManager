namespace DisplayProfileManager.Audio.Models
{
    public enum DeviceType
    {
        Playback,
        Capture
    }

    public class AudioDeviceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SystemName { get; set; }
        // Port-independent hardware ID, like USB\VID_0909&PID_001C&MI_00
        public string HardwareId { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailable { get; set; } = true;
        public DeviceType Type { get; set; }

        public override string ToString()
        {
            var name = SystemName ?? Name ?? "Unknown Device";
            return IsAvailable ? name : $"{name} (Unavailable)";
        }
    }
}
