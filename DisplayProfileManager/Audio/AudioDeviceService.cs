using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using NLog;

using DisplayProfileManager.Audio.Models;
using DisplayProfileManager.Common;
using static DisplayProfileManager.Audio.Interop.CoreAudioNative;

namespace DisplayProfileManager.Audio
{
    public sealed class AudioDeviceService : IAudioDeviceService
    {
        private static readonly Logger _logger = LoggerHelper.GetLogger();

        private static IMMDeviceEnumerator CreateEnumerator() => (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

        internal static string GetDeviceFriendlyName(IMMDevice device) => GetStringProperty(device, PKEY_Device_FriendlyName, "friendly name");

        private static string GetStringProperty(IMMDevice device, PROPERTYKEY propertyKey, string label)
        {
            IPropertyStore store = null;
            PROPVARIANT pv = default;
            bool ownsValue = false;

            try
            {
                int hr = device.OpenPropertyStore(StgmRead, out store);
                if (hr < 0 || store == null)
                {
                    _logger.Warn($"Skipping device {label} read -> OpenPropertyStore failed (0x{hr:X8})");
                    return null;
                }

                var key = propertyKey;
                hr = store.GetValue(ref key, out pv);
                if (hr < 0)
                {
                    // A failed call does not transfer ownership of the output value
                    pv = default;
                    _logger.Debug($"Skipping device {label} read -> GetValue failed (0x{hr:X8})");
                    return null;
                }

                ownsValue = true;
                if (pv.vt != VtLpwstr || pv.pwszVal == IntPtr.Zero)
                {
                    return null;
                }

                return Marshal.PtrToStringUni(pv.pwszVal);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Failed to read device {label} from property store");
                return null;
            }
            finally
            {
                if (ownsValue)
                    PropVariantClear(ref pv);

                if (store != null)
                    Marshal.ReleaseComObject(store);
            }
        }

        private static AudioDeviceInfo DeviceToInfo(IMMDevice device, DeviceType type)
        {
            Marshal.ThrowExceptionForHR(device.GetId(out var id));
            Marshal.ThrowExceptionForHR(device.GetState(out var state));

            var friendlyName = GetDeviceFriendlyName(device);
            // Bluetooth devices sometimes surface as "Unknown" — try WMI correlation
            if (string.IsNullOrEmpty(friendlyName) || friendlyName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                friendlyName = AudioDeviceNameResolver.TryGetBluetoothName(id) ?? friendlyName;
            if (!string.IsNullOrEmpty(friendlyName) && !friendlyName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                AudioDeviceNameResolver.CacheDeviceName(id, friendlyName);

            return new AudioDeviceInfo
            {
                Id = id,
                Name = friendlyName,
                SystemName = friendlyName,
                HardwareId = AudioDeviceMatcher.ToHardwareId(GetStringProperty(device, PKEY_AudioEndpoint_ParentInstancePath, "parent instance path")),
                IsActive = state == DeviceStateActive,
                Type = type
            };
        }

        public List<AudioDeviceInfo> GetPlaybackDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;

            try
            {
                enumerator = CreateEnumerator();

                if (enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out collection) >= 0 && collection != null)
                {
                    Marshal.ThrowExceptionForHR(collection.GetCount(out var count));

                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice device = null;

                        try
                        {
                            if (collection.Item(i, out device) >= 0 && device != null)
                                devices.Add(DeviceToInfo(device, DeviceType.Playback));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error processing playback device");
                        }
                        finally
                        {
                            if (device != null)
                                Marshal.ReleaseComObject(device);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GetPlaybackDevices failed");
            }
            finally
            {
                if (collection != null)
                    Marshal.ReleaseComObject(collection);

                if (enumerator != null)
                    Marshal.ReleaseComObject(enumerator);
            }

            return devices;
        }

        public List<AudioDeviceInfo> GetCaptureDevices()
        {
            var devices = new List<AudioDeviceInfo>();
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;

            try
            {
                enumerator = CreateEnumerator();
                if (enumerator.EnumAudioEndpoints(EDataFlow.Capture, DeviceStateActive, out collection) >= 0 && collection != null)
                {
                    Marshal.ThrowExceptionForHR(collection.GetCount(out var count));

                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice device = null;

                        try
                        {
                            if (collection.Item(i, out device) >= 0 && device != null)
                                devices.Add(DeviceToInfo(device, DeviceType.Capture));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error processing capture device");
                        }
                        finally
                        {
                            if (device != null)
                                Marshal.ReleaseComObject(device);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GetCaptureDevices failed");
            }
            finally
            {
                if (collection != null)
                    Marshal.ReleaseComObject(collection);

                if (enumerator != null)
                    Marshal.ReleaseComObject(enumerator);
            }

            return devices;
        }

        public AudioDeviceInfo GetDefaultPlaybackDevice()
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;

            try
            {
                enumerator = CreateEnumerator();

                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out device) < 0 || device == null)
                {
                    return null;
                }

                return DeviceToInfo(device, DeviceType.Playback);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GetDefaultPlaybackDevice failed");
                return null;
            }
            finally
            {
                if (device != null)
                    Marshal.ReleaseComObject(device);

                if (enumerator != null)
                    Marshal.ReleaseComObject(enumerator);
            }
        }

        public AudioDeviceInfo GetDefaultCaptureDevice()
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;

            try
            {
                enumerator = CreateEnumerator();

                if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Capture, ERole.Console, out device) < 0 || device == null)
                {
                    return null;
                }

                return DeviceToInfo(device, DeviceType.Capture);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GetDefaultCaptureDevice failed");
                return null;
            }
            finally
            {
                if (device != null)
                    Marshal.ReleaseComObject(device);

                if (enumerator != null)
                    Marshal.ReleaseComObject(enumerator);
            }
        }

        private static bool SetDefaultEndpoint(string deviceId)
        {
            IPolicyConfig policyConfig = null;

            try
            {
                policyConfig = (IPolicyConfig)new PolicyConfigClientComObject();

                var hr1 = policyConfig.SetDefaultEndpoint(deviceId, ERole.Console);
                var hr2 = policyConfig.SetDefaultEndpoint(deviceId, ERole.Multimedia);
                var hr3 = policyConfig.SetDefaultEndpoint(deviceId, ERole.Communications);

                if (hr1 != 0 || hr2 != 0 || hr3 != 0)
                {
                    _logger.Warn($"SetDefaultEndpoint partial failure — HRESULT console={hr1:X} multimedia={hr2:X} comms={hr3:X}");

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"SetDefaultEndpoint failed for {deviceId}");
                return false;
            }
            finally
            {
                if (policyConfig != null)
                    Marshal.ReleaseComObject(policyConfig);
            }
        }

        public bool SetDefaultPlaybackDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.Warn("SetDefaultPlaybackDevice called with null/empty ID");
                return false;
            }

            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;

            try
            {
                enumerator = CreateEnumerator();

                if (enumerator.GetDevice(deviceId, out device) < 0 || device == null)
                {
                    _logger.Warn($"Playback device not found: {deviceId}");
                    return false;
                }

                var result = SetDefaultEndpoint(deviceId);

                if (result)
                {
                    string id = device.GetId(out var resolvedId) < 0 || resolvedId == null ? deviceId : resolvedId;
                    _logger.Info($"Set default playback device: {AudioDeviceNameResolver.GetCachedName(id) ?? deviceId}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"SetDefaultPlaybackDevice failed: {deviceId}");
                return false;
            }
            finally
            {
                if (device != null)
                    Marshal.ReleaseComObject(device);

                if (enumerator != null)
                    Marshal.ReleaseComObject(enumerator);
            }
        }

        public bool SetDefaultCaptureDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.Warn("SetDefaultCaptureDevice called with null/empty ID");
                return false;
            }

            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;

            try
            {
                enumerator = CreateEnumerator();

                if (enumerator.GetDevice(deviceId, out device) < 0 || device == null)
                {
                    _logger.Warn($"Capture device not found: {deviceId}");
                    return false;
                }

                var result = SetDefaultEndpoint(deviceId);

                if (result)
                {
                    string id = device.GetId(out var resolvedId) < 0 || resolvedId == null ? deviceId : resolvedId;
                    _logger.Info($"Set default capture device: {AudioDeviceNameResolver.GetCachedName(id) ?? deviceId}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"SetDefaultCaptureDevice failed: {deviceId}");
                return false;
            }
            finally
            {
                if (device != null)
                    Marshal.ReleaseComObject(device);

                if (enumerator != null)
                    Marshal.ReleaseComObject(enumerator);
            }
        }

        public AudioApplyOutcome ApplyAudioSettings(AudioSetting audioSettings)
        {
            var outcome = new AudioApplyOutcome();
            if (audioSettings == null)
            {
                _logger.Debug("No audio settings to apply");
                return outcome;
            }

            try
            {
                if (audioSettings.ApplyPlaybackDevice && audioSettings.HasPlaybackDevice())
                    ApplyDevice(DeviceType.Playback, audioSettings.DefaultPlaybackDeviceId, audioSettings.PlaybackDeviceName, audioSettings.PlaybackHardwareId, outcome);

                if (audioSettings.ApplyCaptureDevice && audioSettings.HasCaptureDevice())
                    ApplyDevice(DeviceType.Capture, audioSettings.DefaultCaptureDeviceId, audioSettings.CaptureDeviceName, audioSettings.CaptureHardwareId, outcome);

                if (!outcome.Success)
                    _logger.Warn("Some audio settings failed to apply");

                return outcome;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error applying audio settings");
                outcome.Success = false;
                return outcome;
            }
        }

        private void ApplyDevice(DeviceType type, string savedId, string savedName, string savedHardwareId, AudioApplyOutcome outcome)
        {
            string label = type == DeviceType.Playback ? "playback" : "capture";
            var active = type == DeviceType.Playback ? GetPlaybackDevices() : GetCaptureDevices();
            var (device, kind) = AudioDeviceMatcher.Resolve(savedId, savedName, savedHardwareId, active);

            if (device == null)
            {
                // Unplugged is not a failure; say so in the notification instead
                _logger.Info($"Skipping {label} device -> not connected: {savedName} ({savedId})");
                outcome.NotConnected.Add(string.IsNullOrEmpty(savedName) ? $"{label} device" : savedName);
                return;
            }

            if (kind != AudioMatchKind.Id)
                _logger.Info($"Matched {label} device '{device.SystemName}' by {kind.ToString().ToLowerInvariant()} -> saved ID {savedId} is now {device.Id}");

            var current = type == DeviceType.Playback ? GetDefaultPlaybackDevice() : GetDefaultCaptureDevice();
            if (current != null && string.Equals(current.Id, device.Id, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug($"Skipping {label} device -> already default: {device.SystemName}");
                return;
            }

            bool ok = type == DeviceType.Playback ? SetDefaultPlaybackDevice(device.Id) : SetDefaultCaptureDevice(device.Id);
            if (!ok)
            {
                _logger.Warn($"Failed to set {label} device: {savedName}");
                outcome.Success = false;
            }
        }
    }
}
