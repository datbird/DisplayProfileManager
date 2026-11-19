using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using NLog;

using DisplayProfileManager.Common;
using DisplayProfileManager.Pointer.Models;

using static DisplayProfileManager.Pointer.Interop.MouseNative;

namespace DisplayProfileManager.Pointer
{
    // Pointer and wheel settings from the Windows Mouse control panel; changes persist for the current user
    public class MouseSettingsService : IMouseSettingsService
    {
        private static readonly Logger _logger = LoggerHelper.GetLogger();

        public MouseSetting CaptureCurrent()
        {
            var current = new MouseSetting();

            uint speed = 0;
            if (SystemParametersInfo(SpiGetMouseSpeed, 0, ref speed, 0))
                current.PointerSpeed = (int)speed;

            var acceleration = new int[3];
            if (SystemParametersInfo(SpiGetMouse, 0, acceleration, 0))
                current.EnhancePointerPrecision = acceleration[2] != 0;

            uint lines = 0;
            if (SystemParametersInfo(SpiGetWheelScrollLines, 0, ref lines, 0))
                current.VerticalScrollLines = (int)Math.Clamp(lines, (uint)MouseSetting.MinScrollAmount, (uint)MouseSetting.MaxScrollAmount);

            uint chars = 0;
            if (SystemParametersInfo(SpiGetWheelScrollChars, 0, ref chars, 0))
                current.HorizontalScrollChars = (int)Math.Clamp(chars, (uint)MouseSetting.MinScrollAmount, (uint)MouseSetting.MaxScrollAmount);

            current.SwapButtons = GetSystemMetrics(SmSwapButton) != 0;

            uint doubleClick = GetDoubleClickTime();
            if (doubleClick > 0)
                current.DoubleClickTime = MouseSetting.ClampDoubleClickTime((int)Math.Min(doubleClick, (uint)MouseSetting.MaxDoubleClickTime));

            int trails = 0;
            if (SystemParametersInfo(SpiGetMouseTrails, 0, ref trails, 0))
                current.MouseTrails = MouseSetting.NormalizeMouseTrails(trails);

            return current;
        }

        public bool ApplyMouseSettings(MouseSetting settings)
        {
            if (settings == null || !settings.AppliesAny()) return true;

            var changes = MouseChangePlan.Build(settings, CaptureCurrent());
            if (changes.Count == 0)
            {
                _logger.Info($"Mouse settings already match -> {settings}");
                return true;
            }

            var failures = new List<string>();
            uint lastAction = 0;

            // Write each value without SPIF_SENDCHANGE, which waits on every top-level window per call
            foreach (var change in changes)
            {
                var (action, ok, label) = Write(change, settings);
                if (ok)
                    lastAction = action;
                else
                    failures.Add($"{label} ({Marshal.GetLastWin32Error()})");
            }

            if (lastAction != 0)
                BroadcastSettingChange(lastAction);

            if (failures.Count > 0)
            {
                _logger.Warn($"Mouse settings partly failed: {string.Join(", ", failures)}");
                return false;
            }

            _logger.Info($"Mouse settings applied -> {settings} (changed {changes.Count})");
            return true;
        }

        private static (uint Action, bool Ok, string Label) Write(MouseChange change, MouseSetting settings)
        {
            switch (change)
            {
                case MouseChange.PointerSpeed:
                    return (SpiSetMouseSpeed, SystemParametersInfo(SpiSetMouseSpeed, 0, (IntPtr)settings.ClampedPointerSpeed(), SpifUpdateIniFile), "pointer speed");
                case MouseChange.PointerPrecision:
                    int[] acceleration = settings.EnhancePointerPrecision ? [.. PrecisionOn] : [.. PrecisionOff];
                    return (SpiSetMouse, SystemParametersInfo(SpiSetMouse, 0, acceleration, SpifUpdateIniFile), "pointer precision");
                case MouseChange.VerticalScroll:
                    return (SpiSetWheelScrollLines, SystemParametersInfo(SpiSetWheelScrollLines, (uint)settings.ClampedVerticalScroll(), IntPtr.Zero, SpifUpdateIniFile), "scroll lines");
                case MouseChange.HorizontalScroll:
                    return (SpiSetWheelScrollChars, SystemParametersInfo(SpiSetWheelScrollChars, (uint)settings.ClampedHorizontalScroll(), IntPtr.Zero, SpifUpdateIniFile), "horizontal scroll");
                case MouseChange.SwapButtons:
                    return (SpiSetMouseButtonSwap, SystemParametersInfo(SpiSetMouseButtonSwap, settings.SwapButtons ? 1u : 0u, IntPtr.Zero, SpifUpdateIniFile), "button swap");
                case MouseChange.DoubleClickTime:
                    return (SpiSetDoubleClickTime, SystemParametersInfo(SpiSetDoubleClickTime, (uint)MouseSetting.ClampDoubleClickTime(settings.DoubleClickTime), IntPtr.Zero, SpifUpdateIniFile), "double-click speed");
                case MouseChange.MouseTrails:
                    return (SpiSetMouseTrails, SystemParametersInfo(SpiSetMouseTrails, (uint)MouseSetting.NormalizeMouseTrails(settings.MouseTrails), IntPtr.Zero, SpifUpdateIniFile), "mouse trails");
                default:
                    return (0, true, change.ToString());
            }
        }

        // One WM_SETTINGCHANGE for the whole batch, off the apply path so a slow window can't stall the switch
        private static void BroadcastSettingChange(uint action)
        {
            Task.Run(() =>
            {
                try
                {
                    var started = Environment.TickCount64;
                    SendMessageTimeout(HwndBroadcast, WmSettingChange, (UIntPtr)action, IntPtr.Zero, SmtoAbortIfHung, BroadcastTimeoutMs, out _);
                    _logger.Debug($"Mouse setting change broadcast took {Environment.TickCount64 - started} ms");
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Mouse setting change broadcast failed");
                }
            });
        }
    }
}
