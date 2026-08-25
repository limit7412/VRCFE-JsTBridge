using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    public class FxLayerRemoverTests
    {
        private AnimatorController _controller;
        private readonly List<Object> _created = new List<Object>();

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
            foreach (var target in _created)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            _created.Clear();

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

        [Test]
        public void FindFxController_ReturnsController_WhenFxIsCustom()
        {
            var avatarRoot = CreateAvatarWithFx(_controller, isDefault: false);

            Assert.That(FxLayerRemover.FindFxController(avatarRoot), Is.SameAs(_controller));
        }

        [Test]
        public void FindFxController_ReturnsNull_WhenFxIsDefault()
        {
            // Defaultを選んだあとも参照が残ることがあるが、ビルドでは無視される
            var avatarRoot = CreateAvatarWithFx(_controller, isDefault: true);

            Assert.That(FxLayerRemover.FindFxController(avatarRoot), Is.Null);
        }

        [Test]
        public void FindFxController_ResolvesOverrideController()
        {
            var overrideController = new AnimatorOverrideController(_controller);
            _created.Add(overrideController);
            var avatarRoot = CreateAvatarWithFx(overrideController, isDefault: false);

            Assert.That(FxLayerRemover.FindFxController(avatarRoot), Is.SameAs(_controller));
        }

        [Test]
        public void CollectAvatarControllers_SkipsDefaultLayers()
        {
            var avatarRoot = CreateAvatarWithFx(_controller, isDefault: true);

            Assert.That(FxLayerRemover.CollectAvatarControllers(avatarRoot), Is.Empty);
        }

        private GameObject CreateAvatarWithFx(RuntimeAnimatorController fx, bool isDefault)
        {
            var avatarRoot = new GameObject("Avatar");
            _created.Add(avatarRoot);

            var descriptor = avatarRoot.AddComponent<VRCAvatarDescriptor>();
            descriptor.baseAnimationLayers = new[]
            {
                new VRCAvatarDescriptor.CustomAnimLayer
                {
                    type = VRCAvatarDescriptor.AnimLayerType.FX,
                    animatorController = fx,
                    isDefault = isDefault,
                    isEnabled = true,
                },
            };
            descriptor.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];

            return avatarRoot;
        }

        [Test]
        public void Remove_ReturnsIndexMap_ForRemainingLayers()
        {
            var result = FxLayerRemover.Remove(_controller, new[] { 1 });

            // 除去したレイヤーは-1、後ろのレイヤーは1つ前へ詰まる
            Assert.That(result.NewLayerIndices, Is.EqualTo(new[] { 0, -1, 1, 2 }));
        }

        [Test]
        public void RemapFxLayerControls_ShiftsIndex_WhenTargetMovedForward()
        {
            var control = AddLayerControl(_controller, VRCAnimatorLayerControl.BlendableLayer.FX, 3);

            var result = FxLayerRemover.RemapFxLayerControls(
                new[] { _controller }, new[] { 0, -1, 1, 2 }, _ => true);

            Assert.That(control.layer, Is.EqualTo(2));
            Assert.That(result.RemappedCount, Is.EqualTo(1));
            Assert.That(result.DetachedOwners, Is.Empty);
        }

        [Test]
        public void RemapFxLayerControls_ClearsTarget_WhenTargetWasRemoved()
        {
            var control = AddLayerControl(_controller, VRCAnimatorLayerControl.BlendableLayer.FX, 1);

            var result = FxLayerRemover.RemapFxLayerControls(
                new[] { _controller }, new[] { 0, -1, 1, 2 }, _ => true);

            // 範囲外の索引はVRChatに無視されるため、誤爆させずに済む
            Assert.That(control.layer, Is.EqualTo(-1));
            Assert.That(result.DetachedOwners, Is.EqualTo(new[] { _controller.name }));
        }

        [Test]
        public void RemapFxLayerControls_LeavesOtherPlayableLayersAlone()
        {
            var control = AddLayerControl(_controller, VRCAnimatorLayerControl.BlendableLayer.Gesture, 3);

            var result = FxLayerRemover.RemapFxLayerControls(
                new[] { _controller }, new[] { 0, -1, 1, 2 }, _ => true);

            Assert.That(control.layer, Is.EqualTo(3));
            Assert.That(result.RemappedCount, Is.EqualTo(0));
        }

        [Test]
        public void RemapFxLayerControls_LeavesIndexOutsideTheMapAlone()
        {
            var control = AddLayerControl(_controller, VRCAnimatorLayerControl.BlendableLayer.FX, 9);

            FxLayerRemover.RemapFxLayerControls(new[] { _controller }, new[] { 0, -1, 1, 2 }, _ => true);

            Assert.That(control.layer, Is.EqualTo(9));
        }

        [Test]
        public void RemapFxLayerControls_SkipsController_WhenItIsNotEditable()
        {
            var control = AddLayerControl(_controller, VRCAnimatorLayerControl.BlendableLayer.FX, 3);

            var result = FxLayerRemover.RemapFxLayerControls(
                new[] { _controller }, new[] { 0, -1, 1, 2 }, _ => false);

            Assert.That(control.layer, Is.EqualTo(3));
            Assert.That(result.SkippedControllers, Is.EqualTo(new[] { _controller.name }));
        }

        private static VRCAnimatorLayerControl AddLayerControl(
            AnimatorController controller, VRCAnimatorLayerControl.BlendableLayer playable, int layer)
        {
            var state = controller.layers[0].stateMachine.AddState("Control");
            var control = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            control.playable = playable;
            control.layer = layer;
            return control;
        }

        private void SetSyncedLayerIndex(int layerIndex, int syncedLayerIndex)
        {
            var layers = _controller.layers;
            layers[layerIndex].syncedLayerIndex = syncedLayerIndex;
            _controller.layers = layers;
        }
    }
}
