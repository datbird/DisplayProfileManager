using System.Collections.Generic;

using DisplayProfileManager.Pointer.Models;

namespace DisplayProfileManager.Pointer
{
    public enum MouseChange
    {
        PointerSpeed,
        PointerPrecision,
        VerticalScroll,
        HorizontalScroll,
        SwapButtons,
        DoubleClickTime,
        MouseTrails
    }

    // Picks only the checked settings that differ from what Windows has now
    public static class MouseChangePlan
    {
        public static IReadOnlyList<MouseChange> Build(MouseSetting desired, MouseSetting current)
        {
            var changes = new List<MouseChange>();
            if (desired == null) return changes;
            current ??= new MouseSetting();

            if (desired.ApplyPointerSpeed && desired.ClampedPointerSpeed() != current.PointerSpeed)
                changes.Add(MouseChange.PointerSpeed);
            if (desired.ApplyPointerPrecision && desired.EnhancePointerPrecision != current.EnhancePointerPrecision)
                changes.Add(MouseChange.PointerPrecision);
            if (desired.ApplyVerticalScroll && desired.ClampedVerticalScroll() != current.VerticalScrollLines)
                changes.Add(MouseChange.VerticalScroll);
            if (desired.ApplyHorizontalScroll && desired.ClampedHorizontalScroll() != current.HorizontalScrollChars)
                changes.Add(MouseChange.HorizontalScroll);
            if (desired.ApplySwapButtons && desired.SwapButtons != current.SwapButtons)
                changes.Add(MouseChange.SwapButtons);
            if (desired.ApplyDoubleClickTime && MouseSetting.ClampDoubleClickTime(desired.DoubleClickTime) != current.DoubleClickTime)
                changes.Add(MouseChange.DoubleClickTime);
            if (desired.ApplyMouseTrails && MouseSetting.NormalizeMouseTrails(desired.MouseTrails) != current.MouseTrails)
                changes.Add(MouseChange.MouseTrails);

            return changes;
        }
    }
}
