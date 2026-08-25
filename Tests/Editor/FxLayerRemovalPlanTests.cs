using System.Linq;
using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    public class FxLayerRemovalPlanTests
    {
        private static readonly string[] Layers =
        {
            "Base Layer",
            "Left Hand Face",
            "Right Hand Face",
            "MA Responsive: BodyAll",
            "MA Responsive: BodyAll",
        };

        [Test]
        public void Resolve_ReturnsIndices_ForMatchingNames()
        {
            var plan = FxLayerRemovalPlan.Resolve(Layers, new[] { "Left Hand Face", "Right Hand Face" });

            Assert.That(plan.LayerIndices, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(plan.MissingNames, Is.Empty);
            Assert.That(plan.IsEmpty, Is.False);
        }

        [Test]
        public void Resolve_ReturnsEveryIndex_WhenNamesAreDuplicated()
        {
            var plan = FxLayerRemovalPlan.Resolve(Layers, new[] { "MA Responsive: BodyAll" });

            Assert.That(plan.LayerIndices, Is.EqualTo(new[] { 3, 4 }));
        }

        [Test]
        public void Resolve_ReportsMissingNames()
        {
            var plan = FxLayerRemovalPlan.Resolve(Layers, new[] { "Left Hand Face", "Mabataki" });

            Assert.That(plan.LayerIndices, Is.EqualTo(new[] { 1 }));
            Assert.That(plan.MissingNames, Is.EqualTo(new[] { "Mabataki" }));
        }

        [Test]
        public void Resolve_IgnoresEmptyEntriesAndSurroundingSpaces()
        {
            var plan = FxLayerRemovalPlan.Resolve(Layers, new[] { "", "  ", null, "  Left Hand Face  " });

            Assert.That(plan.LayerIndices, Is.EqualTo(new[] { 1 }));
            Assert.That(plan.MissingNames, Is.Empty);
        }

        [Test]
        public void Resolve_CountsRepeatedRequestOnce()
        {
            var plan = FxLayerRemovalPlan.Resolve(Layers, new[] { "Left Hand Face", "Left Hand Face" });

            Assert.That(plan.LayerIndices, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Resolve_ReturnsIndicesInAscendingOrder()
        {
            var plan = FxLayerRemovalPlan.Resolve(Layers, new[] { "Right Hand Face", "Base Layer" });

            Assert.That(plan.LayerIndices, Is.EqualTo(plan.LayerIndices.OrderBy(index => index)));
        }

        [Test]
        public void Resolve_IsEmpty_WhenNothingRequested()
        {
            Assert.That(FxLayerRemovalPlan.Resolve(Layers, new string[0]).IsEmpty, Is.True);
        }

        [Test]
        public void HasRequestedName_IsFalse_ForEmptyEntriesOnly()
        {
            Assert.That(FxLayerRemovalPlan.HasRequestedName(new[] { "", "  ", null }), Is.False);
            Assert.That(FxLayerRemovalPlan.HasRequestedName(new string[0]), Is.False);
            Assert.That(FxLayerRemovalPlan.HasRequestedName(null), Is.False);
        }

        [Test]
        public void HasRequestedName_IsTrue_WhenAnyNameIsGiven()
        {
            Assert.That(FxLayerRemovalPlan.HasRequestedName(new[] { "", "  Mabataki  " }), Is.True);
        }
    }
}
