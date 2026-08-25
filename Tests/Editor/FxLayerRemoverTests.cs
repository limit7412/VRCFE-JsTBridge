using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    public class FxLayerRemoverTests
    {
        private AnimatorController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new AnimatorController { name = "FX" };
            foreach (var name in new[] { "Base Layer", "Left Hand Face", "Right Hand Face", "Mabataki" })
            {
                _controller.AddLayer(name);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null)
            {
                Object.DestroyImmediate(_controller);
            }
        }

        [Test]
        public void Remove_RemovesRequestedLayers_AndKeepsTheRest()
        {
            var result = FxLayerRemover.Remove(_controller, new[] { 1, 2 });

            Assert.That(_controller.layers.Select(layer => layer.name),
                Is.EqualTo(new[] { "Base Layer", "Mabataki" }));
            Assert.That(result.RemovedLayerNames,
                Is.EqualTo(new[] { "Left Hand Face", "Right Hand Face" }));
            Assert.That(result.DetachedSyncedLayerNames, Is.Empty);
        }

        [Test]
        public void Remove_DoesNothing_WhenNoIndexGiven()
        {
            var result = FxLayerRemover.Remove(_controller, new int[0]);

            Assert.That(_controller.layers.Length, Is.EqualTo(4));
            Assert.That(result.RemovedLayerNames, Is.Empty);
        }

        [Test]
        public void Remove_ShiftsSyncedLayerIndex_WhenAnEarlierLayerIsRemoved()
        {
            SetSyncedLayerIndex(3, 2);

            FxLayerRemover.Remove(_controller, new[] { 1 });

            var mabataki = _controller.layers.Single(layer => layer.name == "Mabataki");
            Assert.That(mabataki.syncedLayerIndex, Is.EqualTo(1));
        }

        [Test]
        public void Remove_DetachesSyncedLayer_WhenItsSourceIsRemoved()
        {
            SetSyncedLayerIndex(3, 2);

            var result = FxLayerRemover.Remove(_controller, new[] { 2 });

            var mabataki = _controller.layers.Single(layer => layer.name == "Mabataki");
            Assert.That(mabataki.syncedLayerIndex, Is.EqualTo(-1));
            Assert.That(result.DetachedSyncedLayerNames, Is.EqualTo(new[] { "Mabataki" }));
        }

        [Test]
        public void GetLayerNames_ReturnsNamesInOrder()
        {
            Assert.That(FxLayerRemover.GetLayerNames(_controller),
                Is.EqualTo(new[] { "Base Layer", "Left Hand Face", "Right Hand Face", "Mabataki" }));
        }

        [Test]
        public void GetLayerNames_ReturnsEmpty_ForNull()
        {
            Assert.That(FxLayerRemover.GetLayerNames(null), Is.Empty);
        }

        private void SetSyncedLayerIndex(int layerIndex, int syncedLayerIndex)
        {
            var layers = _controller.layers;
            layers[layerIndex].syncedLayerIndex = syncedLayerIndex;
            _controller.layers = layers;
        }
    }
}
