using System.Collections.Generic;

namespace DisplayProfileManager.Audio.Models
{
    public class AudioApplyOutcome
    {
        public bool Success { get; set; } = true;
        // Saved devices with no match among connected devices; skipped, not failed
        public List<string> NotConnected { get; } = [];
    }
}
