using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using DisplayProfileManager.Pointer;
using DisplayProfileManager.Pointer.Models;

namespace DisplayProfileManager.Tests.Pointer
{
    [TestClass]
    public class MouseChangePlanTests
    {
        private static MouseSetting Current() => new()
        {
            PointerSpeed = 10,
            EnhancePointerPrecision = true,
            VerticalScrollLines = 3,
            HorizontalScrollChars = 3,
            SwapButtons = false,
            DoubleClickTime = 500,
            MouseTrails = 0
        };

        [TestMethod]
        [TestCategory("Unit")]
        public void Build_ValuesAlreadyMatch_ReturnsNoChanges()
        {
            var desired = Current();
            desired.ApplyPointerSpeed = desired.ApplyPointerPrecision = desired.ApplyVerticalScroll = desired.ApplyHorizontalScroll = true;
            desired.ApplySwapButtons = desired.ApplyDoubleClickTime = desired.ApplyMouseTrails = true;

            Assert.AreEqual(0, MouseChangePlan.Build(desired, Current()).Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Build_OnlyDifferentCheckedValues_AreIncluded()
        {
            var desired = Current();
            desired.ApplyPointerSpeed = true;
            desired.PointerSpeed = 4;
            desired.ApplyVerticalScroll = true;
            desired.ApplyDoubleClickTime = false;
            desired.DoubleClickTime = 700;

            var changes = MouseChangePlan.Build(desired, Current());

            CollectionAssert.AreEqual(new[] { MouseChange.PointerSpeed }, changes.ToArray());
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Build_ComparesClampedAndNormalizedValues()
        {
            var current = Current();
            current.PointerSpeed = MouseSetting.MaxPointerSpeed;
            current.DoubleClickTime = MouseSetting.MinDoubleClickTime;

            var desired = Current();
            desired.ApplyPointerSpeed = true;
            desired.PointerSpeed = 99;
            desired.ApplyDoubleClickTime = true;
            desired.DoubleClickTime = 50;
            desired.ApplyMouseTrails = true;
            desired.MouseTrails = 1;

            Assert.AreEqual(0, MouseChangePlan.Build(desired, current).Count);
        }

        [TestMethod]
        [TestCategory("Unit")]
        public void Build_EveryDifferentValue_ReturnsEveryChange()
        {
            var desired = new MouseSetting
            {
                ApplyPointerSpeed = true, PointerSpeed = 4,
                ApplyPointerPrecision = true, EnhancePointerPrecision = false,
                ApplyVerticalScroll = true, VerticalScrollLines = 6,
                ApplyHorizontalScroll = true, HorizontalScrollChars = 6,
                ApplySwapButtons = true, SwapButtons = true,
                ApplyDoubleClickTime = true, DoubleClickTime = 300,
                ApplyMouseTrails = true, MouseTrails = 5
            };

            Assert.AreEqual(7, MouseChangePlan.Build(desired, Current()).Count);
        }
    }
}
