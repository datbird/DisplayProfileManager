using System.Collections.Generic;

using DisplayProfileManager.Audio.Models;

namespace DisplayProfileManager.Audio
{
    public interface IAudioDeviceService
    {
        List<AudioDeviceInfo> GetPlaybackDevices();
        List<AudioDeviceInfo> GetCaptureDevices();
        AudioDeviceInfo GetDefaultPlaybackDevice();
        AudioDeviceInfo GetDefaultCaptureDevice();
        bool SetDefaultPlaybackDevice(string deviceId);
        bool SetDefaultCaptureDevice(string deviceId);
        AudioApplyOutcome ApplyAudioSettings(AudioSetting settings);
    }
}
