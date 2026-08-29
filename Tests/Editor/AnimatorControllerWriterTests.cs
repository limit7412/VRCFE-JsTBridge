using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using FEJsTBridge.Domain;
using FEJsTBridge.Infra;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 計画がそのままAnimatorControllerへ写ることを検証する
    /// </summary>
    public class AnimatorControllerWriterTests
    {
        private readonly List<Object> _generated = new List<Object>();

        private AnimatorController Write(BridgeSettings settings)
        {
            var plan = BridgePlanBuilder.Build(settings);
            return AnimatorControllerWriter.Write(plan, asset => _generated.Add(asset));
        }

        private static BridgeSettings Settings(
            BypassTrigger trigger = BypassTrigger.FacialExpressionsDisabled,
            bool enableTrackingReapply = true,
            float reapplyDelaySeconds = 0.2f)
        {
            return new BridgeSettings(trigger, enableTrackingReapply, reapplyDelaySeconds);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _generated.Where(asset => asset != null))
            {
                Object.DestroyImmediate(asset);
            }

            _generated.Clear();
        }

        [Test]
        public void Write_CreatesLayers_WithFullWeight()
        {
            var controller = Write(Settings());

            Assert.That(controller.layers.Select(layer => layer.name), Is.EqualTo(new[]
            {
                BridgePlanBuilder.BypassLayerName,
                BridgePlanBuilder.TrackingReapplyLayerName,
            }));

            // AddLayerで作られるレイヤーの重みは0のため、揃え忘れると再適用レイヤーが動かない
            Assert.That(controller.layers.All(layer => layer.defaultWeight == 1f));
        }

        [Test]
        public void Write_DeclaresParameters_AsPlanned()
        {
            var controller = Write(Settings());

            Assert.That(
                controller.parameters.Select(parameter => (parameter.name, parameter.type)),
                Is.EqualTo(new[]
                {
                    (BridgeParameterNames.FacialExpressionsDisabled, AnimatorControllerParameterType.Bool),
                    (BridgeParameterNames.LipTrackingActive, AnimatorControllerParameterType.Float),
                    (BridgeParameterNames.EyeTrackingActive, AnimatorControllerParameterType.Float),
                    (BridgeParameterNames.VisemesEnable, AnimatorControllerParameterType.Bool),
                    (BridgeParameterNames.ForceBypassEnable, AnimatorControllerParameterType.Bool),
                }));
        }

        [Test]
        public void Write_MatchesPlan_InStateAndTransitionCounts()
        {
            var settings = Settings();
            var plan = BridgePlanBuilder.Build(settings);
            var controller = AnimatorControllerWriter.Write(plan, asset => _generated.Add(asset));

            for (var i = 0; i < plan.Layers.Count; i++)
            {
                var layerPlan = plan.Layers[i];
                var stateMachine = controller.layers[i].stateMachine;

                Assert.That(stateMachine.states.Length, Is.EqualTo(layerPlan.States.Count), layerPlan.Name);
                Assert.That(
                    stateMachine.states.Sum(state => state.state.transitions.Length),
                    Is.EqualTo(layerPlan.Transitions.Count),
                    layerPlan.Name);
                Assert.That(stateMachine.defaultState.name, Is.EqualTo(layerPlan.DefaultStateName));
            }
        }

        [Test]
        public void Write_WritesDriver_ForBypassStates()
        {
            var controller = Write(Settings());
            var bypassLayer = controller.layers[0].stateMachine;

            AssertBypassDriver(FindState(bypassLayer, BridgePlanBuilder.IdleStateName), 0f);
            AssertBypassDriver(FindState(bypassLayer, BridgePlanBuilder.BypassStateName), 1f);
        }

        private static void AssertBypassDriver(AnimatorState state, float expectedValue)
        {
            var driver = state.behaviours.OfType<VRCAvatarParameterDriver>().Single();

            Assert.That(driver.localOnly, Is.False);
            Assert.That(driver.parameters.Count, Is.EqualTo(1));
            Assert.That(driver.parameters[0].type, Is.EqualTo(VRC_AvatarParameterDriver.ChangeType.Set));
            Assert.That(driver.parameters[0].name, Is.EqualTo(BridgeParameterNames.ForceBypassEnable));
            Assert.That(driver.parameters[0].value, Is.EqualTo(expectedValue));
        }

        [Test]
        public void Write_WritesTrackingControl_ForApplyStates()
        {
            var controller = Write(Settings());
            var reapplyLayer = controller.layers[1].stateMachine;

            var state = FindState(reapplyLayer, BridgePlanBuilder.ApplyStateName(true, false));
            var trackingControl = state.behaviours.OfType<VRCAnimatorTrackingControl>().Single();

            Assert.That(trackingControl.trackingEyes,
                Is.EqualTo(VRC_AnimatorTrackingControl.TrackingType.Animation));
            Assert.That(trackingControl.trackingMouth,
                Is.EqualTo(VRC_AnimatorTrackingControl.TrackingType.Animation));

            // 指定していない部位はNoChangeのままにする
            Assert.That(trackingControl.trackingHead,
                Is.EqualTo(VRC_AnimatorTrackingControl.TrackingType.NoChange));
        }

        [Test]
        public void Write_GivesArmedState_ItsOwnClipLength()
        {
            var controller = Write(Settings(reapplyDelaySeconds: 0.35f));
            var reapplyLayer = controller.layers[1].stateMachine;

            var armed = FindState(reapplyLayer, BridgePlanBuilder.ArmedStateName);
            var wait = FindState(reapplyLayer, BridgePlanBuilder.WaitStateName);

            Assert.That(((AnimationClip)armed.motion).length, Is.EqualTo(0.35f).Within(0.01f));
            Assert.That(((AnimationClip)wait.motion).length,
                Is.EqualTo(BridgePlanBuilder.DefaultClipLengthSeconds).Within(0.01f));

            // 同じ長さのクリップは共有する
            var idle = FindState(controller.layers[0].stateMachine, BridgePlanBuilder.IdleStateName);
            Assert.That(wait.motion, Is.SameAs(idle.motion));
            Assert.That(armed.motion, Is.Not.SameAs(wait.motion));
        }

        [Test]
        public void Write_CopiesTransitionConditions_FromPlan()
        {
            var controller = Write(Settings());
            var bypassLayer = controller.layers[0].stateMachine;

            var transition = FindState(bypassLayer, BridgePlanBuilder.IdleStateName).transitions
                .Single(t => t.destinationState.name == BridgePlanBuilder.BypassStateName);

            Assert.That(transition.destinationState.name, Is.EqualTo(BridgePlanBuilder.BypassStateName));
            Assert.That(transition.hasExitTime, Is.False);
            Assert.That(transition.duration, Is.EqualTo(0f));
            Assert.That(transition.conditions.Length, Is.EqualTo(1));
            Assert.That(transition.conditions[0].parameter,
                Is.EqualTo(BridgeParameterNames.FacialExpressionsDisabled));
            Assert.That(transition.conditions[0].mode, Is.EqualTo(AnimatorConditionMode.If));
        }

        [Test]
        public void Write_WritesSelfTransition_ForBypassStates()
        {
            var controller = Write(Settings());
            var bypassLayer = controller.layers[0].stateMachine;

            var bypass = FindState(bypassLayer, BridgePlanBuilder.BypassStateName);
            var loop = bypass.transitions.Single(t => t.destinationState == bypass);

            Assert.That(loop.hasExitTime, Is.True);
            Assert.That(loop.exitTime, Is.EqualTo(1.0f));
            Assert.That(loop.conditions, Is.Empty);
        }

        [Test]
        public void Write_UsesExitTime_ForArmedTransitions()
        {
            var controller = Write(Settings());
            var reapplyLayer = controller.layers[1].stateMachine;

            var transitions = FindState(reapplyLayer, BridgePlanBuilder.ArmedStateName).transitions;
            var withExitTime = transitions.Where(transition => transition.hasExitTime).ToArray();

            Assert.That(withExitTime.Length, Is.EqualTo(4));
            Assert.That(withExitTime.All(transition => transition.exitTime == 1.0f));
            Assert.That(withExitTime.All(transition => transition.conditions.Length == 2));
        }

        [Test]
        public void Write_GeneratesSingleLayer_WhenReapplyDisabled()
        {
            var controller = Write(Settings(enableTrackingReapply: false));

            Assert.That(controller.layers.Length, Is.EqualTo(1));
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            return stateMachine.states.Single(state => state.state.name == name).state;
        }
    }
}
