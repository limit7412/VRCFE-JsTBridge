using System.Linq;
using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 生成計画が仕様どおりかを検証する
    /// 条件の閾値やDriverの値といった仕様の実体はすべて計画にあるため、
    /// アニメーターのAPIに触れずに確認できる
    /// </summary>
    public class BridgePlanBuilderTests
    {
        private static BridgeSettings Settings(
            BypassTrigger trigger = BypassTrigger.FacialExpressionsDisabled,
            bool enableTrackingReapply = true,
            float reapplyDelaySeconds = 0.2f)
        {
            return new BridgeSettings(trigger, enableTrackingReapply, reapplyDelaySeconds);
        }

        [Test]
        public void Build_GeneratesTwoLayers_ByDefault()
        {
            var plan = BridgePlanBuilder.Build(Settings());

            Assert.That(plan.Layers.Select(layer => layer.Name), Is.EqualTo(new[]
            {
                BridgePlanBuilder.BypassLayerName,
                BridgePlanBuilder.TrackingReapplyLayerName,
            }));
        }

        [Test]
        public void Build_GeneratesBypassLayerOnly_WhenReapplyDisabled()
        {
            var plan = BridgePlanBuilder.Build(Settings(enableTrackingReapply: false));

            Assert.That(plan.Layers.Select(layer => layer.Name),
                Is.EqualTo(new[] { BridgePlanBuilder.BypassLayerName }));
        }

        [Test]
        public void Build_DeclaresParameters_WithSpecifiedNamesAndTypes()
        {
            var plan = BridgePlanBuilder.Build(Settings());

            Assert.That(
                plan.Parameters.Select(parameter => (parameter.Name, parameter.Type)),
                Is.EqualTo(new[]
                {
                    (BridgeParameterNames.FacialExpressionsDisabled, BridgeParameterType.Bool),
                    (BridgeParameterNames.LipTrackingActive, BridgeParameterType.Float),
                    (BridgeParameterNames.EyeTrackingActive, BridgeParameterType.Float),
                    (BridgeParameterNames.VisemesEnable, BridgeParameterType.Bool),
                    (BridgeParameterNames.ForceBypassEnable, BridgeParameterType.Bool),
                }));
        }

        [Test]
        public void Build_KeepsParameterDeclarations_WhenReapplyDisabled()
        {
            var withReapply = BridgePlanBuilder.Build(Settings());
            var withoutReapply = BridgePlanBuilder.Build(Settings(enableTrackingReapply: false));

            Assert.That(
                withoutReapply.Parameters.Select(parameter => parameter.Name),
                Is.EqualTo(withReapply.Parameters.Select(parameter => parameter.Name)));
        }

        [Test]
        public void BypassLayer_DrivesForceBypassEnable_WithZeroAndOne()
        {
            var layer = BridgePlanBuilder.Build(Settings()).FindLayer(BridgePlanBuilder.BypassLayerName);

            var idle = layer.FindState(BridgePlanBuilder.IdleStateName);
            var bypass = layer.FindState(BridgePlanBuilder.BypassStateName);

            Assert.That(layer.DefaultStateName, Is.EqualTo(BridgePlanBuilder.IdleStateName));
            AssertBypassDriver(idle, 0f);
            AssertBypassDriver(bypass, 1f);
        }

        private static void AssertBypassDriver(BridgeStatePlan state, float expectedValue)
        {
            Assert.That(state.Driver, Is.Not.Null, $"{state.Name}にDriverがない");

            // 同期済みの入力から導出するため、リモートでもDriverを走らせる必要がある
            Assert.That(state.Driver.LocalOnly, Is.False);
            Assert.That(state.Driver.Entries.Count, Is.EqualTo(1));
            Assert.That(state.Driver.Entries[0].Parameter, Is.EqualTo(BridgeParameterNames.ForceBypassEnable));
            Assert.That(state.Driver.Entries[0].Value, Is.EqualTo(expectedValue));
        }

        [Test]
        public void BypassLayer_UsesBoolComparison_InFacialExpressionsDisabledMode()
        {
            var layer = BridgePlanBuilder.Build(Settings()).FindLayer(BridgePlanBuilder.BypassLayerName);

            var on = layer.TransitionsFrom(BridgePlanBuilder.IdleStateName)
                .Single(t => t.To == BridgePlanBuilder.BypassStateName);
            var off = layer.TransitionsFrom(BridgePlanBuilder.BypassStateName)
                .Single(t => t.To == BridgePlanBuilder.IdleStateName);

            AssertCondition(on.Conditions.Single(),
                BridgeParameterNames.FacialExpressionsDisabled, BridgeConditionMode.If, 0f);
            AssertCondition(off.Conditions.Single(),
                BridgeParameterNames.FacialExpressionsDisabled, BridgeConditionMode.IfNot, 0f);
            Assert.That(on.HasExitTime, Is.False);
            Assert.That(off.HasExitTime, Is.False);
        }

        [Test]
        public void BypassLayer_UsesThresholdComparison_InLipTrackingOnlyMode()
        {
            var layer = BridgePlanBuilder.Build(Settings(BypassTrigger.LipTrackingOnly))
                .FindLayer(BridgePlanBuilder.BypassLayerName);

            var on = layer.TransitionsFrom(BridgePlanBuilder.IdleStateName)
                .Single(t => t.To == BridgePlanBuilder.BypassStateName);
            var off = layer.TransitionsFrom(BridgePlanBuilder.BypassStateName)
                .Single(t => t.To == BridgePlanBuilder.IdleStateName);

            AssertCondition(on.Conditions.Single(),
                BridgeParameterNames.LipTrackingActive, BridgeConditionMode.Greater, 0.5f);
            AssertCondition(off.Conditions.Single(),
                BridgeParameterNames.LipTrackingActive, BridgeConditionMode.Less, 0.5f);
        }

        [TestCase(BridgePlanBuilder.IdleStateName)]
        [TestCase(BridgePlanBuilder.BypassStateName)]
        public void BypassLayer_States_ReenterThemselves_ToRefireDriver(string stateName)
        {
            var layer = BridgePlanBuilder.Build(Settings()).FindLayer(BridgePlanBuilder.BypassLayerName);

            var transitions = layer.TransitionsFrom(stateName);

            Assert.That(transitions.Count, Is.EqualTo(2));

            var loop = transitions.Single(t => t.To == stateName);
            Assert.That(loop.HasExitTime, Is.True);
            Assert.That(loop.ExitTime, Is.EqualTo(1.0f));
            Assert.That(loop.Conditions, Is.Empty);

            // トリガーの切替が優先されるよう、ループは記載順の最後に置く
            Assert.That(transitions[transitions.Count - 1], Is.SameAs(loop));
        }

        [Test]
        public void ArmedState_ClipLength_MatchesReapplyDelay()
        {
            var layer = BridgePlanBuilder.Build(Settings(reapplyDelaySeconds: 0.35f))
                .FindLayer(BridgePlanBuilder.TrackingReapplyLayerName);

            Assert.That(layer.FindState(BridgePlanBuilder.ArmedStateName).MotionLengthSeconds,
                Is.EqualTo(0.35f).Within(1e-5f));

            // Armed以外は共有の空クリップを使う
            Assert.That(layer.FindState(BridgePlanBuilder.WaitStateName).MotionLengthSeconds,
                Is.EqualTo(BridgePlanBuilder.DefaultClipLengthSeconds));
        }

        [Test]
        public void ArmedState_ClipLength_IsClampedToSettableRange()
        {
            var layer = BridgePlanBuilder.Build(Settings(reapplyDelaySeconds: 10f))
                .FindLayer(BridgePlanBuilder.TrackingReapplyLayerName);

            Assert.That(layer.FindState(BridgePlanBuilder.ArmedStateName).MotionLengthSeconds,
                Is.EqualTo(FEJsTBridgeComponent.MaxReapplyDelaySeconds));
        }

        // 期待値を名前で渡す。BridgeTrackingStateはinternalであり、
        // publicなテストメソッドの引数には置けない (CS0051)
        [TestCase(false, false, "Tracking", "Animation")]
        [TestCase(false, true, "Tracking", "Tracking")]
        [TestCase(true, false, "Animation", "Animation")]
        [TestCase(true, true, "Animation", "Tracking")]
        public void ApplyStates_TrackingControl_MatchesJerryState(
            bool eye, bool visemes, string expectedEyes, string expectedMouth)
        {
            var layer = BridgePlanBuilder.Build(Settings())
                .FindLayer(BridgePlanBuilder.TrackingReapplyLayerName);

            var state = layer.FindState(BridgePlanBuilder.ApplyStateName(eye, visemes));

            Assert.That(state, Is.Not.Null);
            Assert.That(state.TrackingControl, Is.Not.Null);
            Assert.That(state.TrackingControl.Eyes.ToString(), Is.EqualTo(expectedEyes));
            Assert.That(state.TrackingControl.Mouth.ToString(), Is.EqualTo(expectedMouth));
        }

        [Test]
        public void TrackingReapplyLayer_HasSixStates()
        {
            var layer = BridgePlanBuilder.Build(Settings())
                .FindLayer(BridgePlanBuilder.TrackingReapplyLayerName);

            Assert.That(layer.States.Count, Is.EqualTo(6));
            Assert.That(layer.DefaultStateName, Is.EqualTo(BridgePlanBuilder.WaitStateName));
            Assert.That(layer.FindState(BridgePlanBuilder.WaitStateName).TrackingControl, Is.Null);
            Assert.That(layer.FindState(BridgePlanBuilder.ArmedStateName).TrackingControl, Is.Null);
        }

        [Test]
        public void ArmedState_ExitTimeTransitions_CoverAllCombinations()
        {
            var layer = BridgePlanBuilder.Build(Settings())
                .FindLayer(BridgePlanBuilder.TrackingReapplyLayerName);

            var fromArmed = layer.TransitionsFrom(BridgePlanBuilder.ArmedStateName);
            var toApply = fromArmed.Where(transition => transition.HasExitTime).ToArray();

            Assert.That(toApply.Length, Is.EqualTo(4));
            Assert.That(toApply.All(transition => transition.ExitTime == 1.0f));
            Assert.That(
                toApply.Select(transition => transition.To).OrderBy(name => name),
                Is.EqualTo(new[]
                {
                    BridgePlanBuilder.ApplyStateName(false, false),
                    BridgePlanBuilder.ApplyStateName(false, true),
                    BridgePlanBuilder.ApplyStateName(true, false),
                    BridgePlanBuilder.ApplyStateName(true, true),
                }));

            // 待機中に解除された場合へ戻る1本
            var toWait = fromArmed.Where(transition => transition.To == BridgePlanBuilder.WaitStateName).ToArray();
            Assert.That(toWait.Length, Is.EqualTo(1));
            Assert.That(toWait[0].HasExitTime, Is.False);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ApplyStates_HaveFourOutgoingTransitions_ForEyeFlipVisemesFlipReleaseAndRearm(bool eye, bool visemes)
        {
            var settings = Settings();
            var layer = BridgePlanBuilder.Build(settings)
                .FindLayer(BridgePlanBuilder.TrackingReapplyLayerName);

            var transitions = layer.TransitionsFrom(BridgePlanBuilder.ApplyStateName(eye, visemes));

            Assert.That(transitions.Count, Is.EqualTo(4));

            var eyeFlip = transitions.Single(t => t.To == BridgePlanBuilder.ApplyStateName(!eye, visemes));
            Assert.That(eyeFlip.HasExitTime, Is.False);
            AssertCondition(
                eyeFlip.Conditions.Single(),
                BridgeParameterNames.EyeTrackingActive,
                eye ? BridgeConditionMode.Less : BridgeConditionMode.Greater,
                0.5f);

            var visemesFlip = transitions.Single(t => t.To == BridgePlanBuilder.ApplyStateName(eye, !visemes));
            Assert.That(visemesFlip.HasExitTime, Is.False);
            AssertCondition(
                visemesFlip.Conditions.Single(),
                BridgeParameterNames.VisemesEnable,
                visemes ? BridgeConditionMode.IfNot : BridgeConditionMode.If,
                0f);

            var release = transitions.Single(t => t.To == BridgePlanBuilder.WaitStateName);
            Assert.That(release.HasExitTime, Is.False);
            var expectedOff = BridgePlanBuilder.TriggerOff(settings);
            AssertCondition(
                release.Conditions.Single(), expectedOff.Parameter, expectedOff.Mode, expectedOff.Threshold);

            var rearm = transitions.Single(t => t.To == BridgePlanBuilder.ArmedStateName);
            Assert.That(rearm.HasExitTime, Is.True);
            Assert.That(rearm.ExitTime, Is.EqualTo(1.0f));
            Assert.That(rearm.Conditions, Is.Empty);

            // 条件付きの3本が優先されるよう、ループは記載順の最後に置く
            Assert.That(transitions[transitions.Count - 1], Is.SameAs(rearm));
        }

        private static void AssertCondition(
            BridgeConditionPlan condition, string parameter, BridgeConditionMode mode, float threshold)
        {
            Assert.That(condition.Parameter, Is.EqualTo(parameter));
            Assert.That(condition.Mode, Is.EqualTo(mode));
            Assert.That(condition.Threshold, Is.EqualTo(threshold).Within(1e-5f));
        }
    }
}
