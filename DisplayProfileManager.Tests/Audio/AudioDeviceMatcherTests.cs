using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using DisplayProfileManager.Audio;
using DisplayProfileManager.Audio.Models;
using DisplayProfileManager.Profiles.Models;
using DisplayProfileManager.UI.Presentation;

namespace DisplayProfileManager.Tests.Audio
{
    [TestClass]
    public class AudioDeviceMatcherTests
    {
        private const string Mic = "Microphone (AT2020USB+)";
        private const string MicHardware = @"USB\VID_0909&PID_001C&MI_00";

        private static AudioDeviceInfo Device(string id, string name, string hardware = "") => new() { Id = id, Name = name, SystemName = name, HardwareId = hardware };

        [TestMethod]
        [TestCategory("Unit")]
        public void Resolve_SavedIdPresent_MatchesById()
        {
            var active = new List<AudioDeviceInfo> { Device("{new}", Mic, MicHardware), Device("{old}", Mic, MicHardware) };

            var (device, kind) = AudioDeviceMatcher.Resolve("{OLD}", Mic, MicHardware, active);

            Assert.AreEqual("{old}", device.Id);
            Assert.AreEqual(AudioMatchKind.Id, kind);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Resolve_IdChanged_MatchesByHardware()
        {
            var active = new List<AudioDeviceInfo> { Device("{new}", "Microphone (renamed)", MicHardware), Device("{other}", "Line In", @"USB\VID_1111&PID_2222") };

            var (device, kind) = AudioDeviceMatcher.Resolve("{old}", Mic, MicHardware, active);

            Assert.AreEqual("{new}", device.Id);
            Assert.AreEqual(AudioMatchKind.Hardware, kind);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Resolve_SharedHardware_NameBreaksTie()
        {
            const string hub = @"USB\VID_0BDA&PID_4014&MI_00";
            var active = new List<AudioDeviceInfo> { Device("{line}", "Line In (Realtek USB Audio)", hub), Device("{mic}", "Microphone (Realtek USB Audio)", hub) };

            var (device, kind) = AudioDeviceMatcher.Resolve("{old}", "Microphone (Realtek USB Audio)", hub, active);

            Assert.AreEqual("{mic}", device.Id);
            Assert.AreEqual(AudioMatchKind.Hardware, kind);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Resolve_NoHardwareIdSaved_MatchesByName()
        {
            var active = new List<AudioDeviceInfo> { Device("{new}", Mic, MicHardware), Device("{other}", "Headset Microphone") };

            var (device, kind) = AudioDeviceMatcher.Resolve("{old}", Mic, string.Empty, active);

            Assert.AreEqual("{new}", device.Id);
            Assert.AreEqual(AudioMatchKind.Name, kind);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Resolve_TwoDevicesWithSameName_DoesNotGuess()
        {
            var active = new List<AudioDeviceInfo> { Device("{a}", Mic), Device("{b}", Mic) };

            var (device, kind) = AudioDeviceMatcher.Resolve("{old}", Mic, string.Empty, active);

            Assert.IsNull(device);
            Assert.AreEqual(AudioMatchKind.None, kind);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Resolve_DeviceUnplugged_ReturnsNone()
        {
            var active = new List<AudioDeviceInfo> { Device("{other}", "Headset Microphone", @"USB\VID_1111&PID_2222") };

            Assert.AreEqual(AudioMatchKind.None, AudioDeviceMatcher.Resolve("{old}", Mic, MicHardware, active).Kind);
        }

        [TestMethod]
        [TestCategory("Unit")]
        [DataRow(@"{1}.USB\VID_0909&PID_001C&MI_00\9&2F9204D7&0&0000", @"USB\VID_0909&PID_001C&MI_00")]
        [DataRow(@"HDAUDIO\FUNC_01&VEN_10DE&DEV_00A3\5&1C2A3F&0&0001", @"HDAUDIO\FUNC_01&VEN_10DE&DEV_00A3")]
        [DataRow("", "")]
        [DataRow("NoSeparator", "")]
        public void ToHardwareId_DropsPortSpecificInstance(string path, string expected)
        {
            Assert.AreEqual(expected, AudioDeviceMatcher.ToHardwareId(path));
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void NoticeSummary_NotConnectedDevices_ReadsAsSkipped()
        {
            var result = new ProfileApplyResult { AudioSuccess = true, DpiChanged = true, AudioNotConnected = [Mic] };

            Assert.AreEqual("", ProfileApplyFormatter.GetApplyWarningSummary(result));
            Assert.AreEqual($"{Mic} not connected, skipped", ProfileApplyFormatter.GetApplyNoticeSummary(result));
            Assert.AreEqual($"Applied in 1 second — {Mic} not connected, skipped", ProfileApplyFormatter.AppendApplyNotices("Applied in 1 second", ProfileApplyFormatter.GetApplyNoticeSummary(result)));
        }
    }
}
