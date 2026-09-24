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
            float reapplyDelaySeconds = 0.2f,
            ControlMethod controlMethod = ControlMethod.Bypass,
            int faceEmoteIndex = 0)
        {
            return new BridgeSettings(
                controlMethod, trigger, enableTrackingReapply, reapplyDelaySeconds, faceEmoteIndex);
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
        public void Build_GeneratesExpressionControlLayer_WhenMethodIsExpressionControl()
        {
            var plan = BridgePlanBuilder.Build(Settings(controlMethod: ControlMethod.ExpressionControl));

            Assert.That(plan.Layers.Select(layer => layer.Name), Is.EqualTo(new[]
            {
                BridgePlanBuilder.ExpressionControlLayerName,
                BridgePlanBuilder.TrackingReapplyLayerName,
            }));
        }

        [Test]
        public void Build_DeclaresResolvedFaceEmoParameters_WhenMethodIsExpressionControl()
        {
            var plan = BridgePlanBuilder.Build(
                Settings(controlMethod: ControlMethod.ExpressionControl), ResolvedNames);

            Assert.That(
                plan.Parameters.Select(parameter => (parameter.Name, parameter.Type)),
                Is.EqualTo(new[]
                {
                    (BridgeParameterNames.FacialExpressionsDisabled, BridgeParameterType.Bool),
                    (BridgeParameterNames.LipTrackingActive, BridgeParameterType.Float),
                    (BridgeParameterNames.EyeTrackingActive, BridgeParameterType.Float),
                    (BridgeParameterNames.VisemesEnable, BridgeParameterType.Bool),
                    ("FaceEmo_LOCK", BridgeParameterType.Bool),
                    ("FaceEmo_BLINK", BridgeParameterType.Bool),
                    ("FaceEmo_EMOTE", BridgeParameterType.Int),
                }));

            // 出力先は方式ごとに違う。使わないパラメータは宣言しない
            Assert.That(
                plan.Parameters.Select(parameter => parameter.Name),
                Has.No.Member(BridgeParameterNames.ForceBypassEnable));
        }

        [Test]
        public void Build_UsesRawFaceEmoNames_WhenNamesAreNotResolved()
        {
            var plan = BridgePlanBuilder.Build(Settings(controlMethod: ControlMethod.ExpressionControl));

            Assert.That(plan.Parameters.Select(parameter => parameter.Name), Is.SupersetOf(new[]
            {
                BridgeParameterNames.EmoteLockEnable,
                BridgeParameterNames.ForceBlinkDisable,
                BridgeParameterNames.Emote,
            }));
        }

        [Test]
        public void ExpressionControlLayer_DrivesLockBlinkAndEmote_WhenEngaged()
        {
            var engaged = ExpressionControlLayer(faceEmoteIndex: 3)
                .FindState(BridgePlanBuilder.EngagedStateName);

            Assert.That(engaged.Driver, Is.Not.Null, "EngagedにDriverがない");

            // 書き込む先はすべて同期パラメータなので、駆動するのは装着者のクライアントだけでよい
            Assert.That(engaged.Driver.LocalOnly, Is.True);
            Assert.That(
                engaged.Driver.Entries.Select(entry => (entry.Parameter, entry.Value)),
                Is.EqualTo(new[]
                {
                    ("FaceEmo_LOCK", 1f),
                    ("FaceEmo_BLINK", 1f),
                    ("FaceEmo_EMOTE", 3f),
                }));
        }

        [Test]
        public void ExpressionControlLayer_ReleasesLockAndBlink_WithoutRestoringEmote()
        {
            var idle = ExpressionControlLayer().FindState(BridgePlanBuilder.IdleStateName);

            Assert.That(idle.Driver, Is.Not.Null, "IdleにDriverがない");
            Assert.That(idle.Driver.LocalOnly, Is.True);

            // 表情番号は戻す先を覚えられないため、ロックを外してFaceEmoに決め直させる
            Assert.That(
                idle.Driver.Entries.Select(entry => (entry.Parameter, entry.Value)),
                Is.EqualTo(new[]
                {
                    ("FaceEmo_LOCK", 0f),
                    ("FaceEmo_BLINK", 0f),
                }));
        }

        [Test]
        public void ExpressionControlLayer_SwitchesByTrigger()
        {
            var settings = Settings(controlMethod: ControlMethod.ExpressionControl);
            var layer = BridgePlanBuilder.Build(settings, ResolvedNames)
                .FindLayer(BridgePlanBuilder.ExpressionControlLayerName);

            Assert.That(layer.DefaultStateName, Is.EqualTo(BridgePlanBuilder.IdleStateName));

            var engage = layer.TransitionsFrom(BridgePlanBuilder.IdleStateName).Single();
            Assert.That(engage.To, Is.EqualTo(BridgePlanBuilder.EngagedStateName));
            var on = BridgePlanBuilder.TriggerOn(settings);
            AssertCondition(engage.Conditions.Single(), on.Parameter, on.Mode, on.Threshold);

            var release = layer.TransitionsFrom(BridgePlanBuilder.EngagedStateName).Single();
            Assert.That(release.To, Is.EqualTo(BridgePlanBuilder.IdleStateName));
            var off = BridgePlanBuilder.TriggerOff(settings);
            AssertCondition(release.Conditions.Single(), off.Parameter, off.Mode, off.Threshold);
        }

        [Test]
        public void ExpressionControlLayer_DoesNotRewritePeriodically()
        {
            var layer = ExpressionControlLayer();

            // 周期的に書き直すと、装着者がメニューで選び直した表情を毎周期奪ってしまう
            Assert.That(layer.States.Select(state => state.Name), Is.EqualTo(new[]
            {
                BridgePlanBuilder.IdleStateName,
                BridgePlanBuilder.EngagedStateName,
            }));
            Assert.That(layer.Transitions.All(transition => !transition.HasExitTime), Is.True);
        }

        [Test]
        public void ExpressionControlLayer_ClampsNegativeEmoteIndex()
        {
            var engaged = ExpressionControlLayer(faceEmoteIndex: -1)
                .FindState(BridgePlanBuilder.EngagedStateName);

            Assert.That(
                engaged.Driver.Entries.Single(entry => entry.Parameter == "FaceEmo_EMOTE").Value,
                Is.EqualTo(0f));
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
        public void BypassLayer_States_ExitToRedrive_ToRefireDriver(string stateName)
        {
            var layer = BridgePlanBuilder.Build(Settings()).FindLayer(BridgePlanBuilder.BypassLayerName);

            var transitions = layer.TransitionsFrom(stateName);

            Assert.That(transitions.Count, Is.EqualTo(2));

            var loop = transitions.Single(t => t.To == BridgePlanBuilder.RedriveStateName);
            Assert.That(loop.HasExitTime, Is.True);
            Assert.That(loop.ExitTime, Is.EqualTo(1.0f));
            Assert.That(loop.Conditions, Is.Empty);

            // トリガーの切替が優先されるよう、ループは記載順の最後に置く
            Assert.That(transitions[transitions.Count - 1], Is.SameAs(loop));
        }

        [Test]
        public void RedriveState_ReturnsImmediately_ToStateMatchingTrigger()
        {
            var settings = Settings();
            var layer = BridgePlanBuilder.Build(settings).FindLayer(BridgePlanBuilder.BypassLayerName);

            Assert.That(layer.States.Count, Is.EqualTo(3));
            Assert.That(layer.FindState(BridgePlanBuilder.RedriveStateName).Driver, Is.Null);

            var transitions = layer.TransitionsFrom(BridgePlanBuilder.RedriveStateName);
            Assert.That(transitions.Count, Is.EqualTo(2));

            var toBypass = transitions.Single(t => t.To == BridgePlanBuilder.BypassStateName);
            var expectedOn = BridgePlanBuilder.TriggerOn(settings);
            Assert.That(toBypass.HasExitTime, Is.False);
            AssertCondition(
                toBypass.Conditions.Single(), expectedOn.Parameter, expectedOn.Mode, expectedOn.Threshold);

            var toIdle = transitions.Single(t => t.To == BridgePlanBuilder.IdleStateName);
            var expectedOff = BridgePlanBuilder.TriggerOff(settings);
            Assert.That(toIdle.HasExitTime, Is.False);
            AssertCondition(
                toIdle.Conditions.Single(), expectedOff.Parameter, expectedOff.Mode, expectedOff.Threshold);
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

        [Test]
        public void Build_OmitsExtraParametersLayer_WhenNoExtraParameters()
        {
            var plan = BridgePlanBuilder.Build(Settings());

            Assert.That(plan.FindLayer(BridgePlanBuilder.ExtraParametersLayerName), Is.Null);
        }

        [Test]
        public void Build_AppendsExtraParametersLayer_AfterExistingLayers()
        {
            var plan = BridgePlanBuilder.Build(SettingsWithExtras(
                new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, 1f)));

            Assert.That(plan.Layers.Select(layer => layer.Name), Is.EqualTo(new[]
            {
                BridgePlanBuilder.BypassLayerName,
                BridgePlanBuilder.TrackingReapplyLayerName,
                BridgePlanBuilder.ExtraParametersLayerName,
            }));
        }

        [Test]
        public void Build_DeclaresExtraParameters_WithoutDuplicates()
        {
            var plan = BridgePlanBuilder.Build(SettingsWithExtras(
                new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, 1f),
                new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 1f, 0f),
                new ExtraParameterTarget("Mask", BridgeParameterType.Int, 2f, 0f),
                new ExtraParameterTarget(BridgeParameterNames.VisemesEnable, BridgeParameterType.Bool, 1f, null)));

            var names = plan.Parameters.Select(parameter => parameter.Name).ToArray();

            // 同名を重ねて宣言すると、Unityが別名のパラメータを作ってしまう
            Assert.That(names, Is.Unique);
            Assert.That(
                plan.Parameters.Where(parameter => parameter.Name == "Mask").Select(parameter => parameter.Type),
                Is.EqualTo(new[] { BridgeParameterType.Int }));
            Assert.That(names, Has.Member("Blush"));
        }

        [Test]
        public void ExtraParametersLayer_StartsFromInitialWithoutDriver()
        {
            var layer = ExtraParametersLayer(new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, 1f));

            // 読み込みのたびに解除時の値を書くと、保存されたトグルの状態を上書きしてしまう
            Assert.That(layer.DefaultStateName, Is.EqualTo(BridgePlanBuilder.InitialStateName));
            Assert.That(layer.FindState(BridgePlanBuilder.InitialStateName).Driver, Is.Null);

            var fromInitial = layer.TransitionsFrom(BridgePlanBuilder.InitialStateName);
            Assert.That(fromInitial.Count, Is.EqualTo(1));
            Assert.That(fromInitial[0].To, Is.EqualTo(BridgePlanBuilder.EngagedStateName));
            AssertCondition(
                fromInitial[0].Conditions.Single(),
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeConditionMode.If,
                0f);
        }

        [Test]
        public void ExtraParametersLayer_WritesEngagedAndReleasedValues()
        {
            var layer = ExtraParametersLayer(
                new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, 1f),
                new ExtraParameterTarget("Mask", BridgeParameterType.Int, 2f, 0f));

            var engaged = layer.FindState(BridgePlanBuilder.EngagedStateName).Driver;
            Assert.That(engaged.LocalOnly, Is.True);
            Assert.That(
                engaged.Entries.Select(entry => (entry.Parameter, entry.Value)),
                Is.EqualTo(new[] { ("Blush", 0f), ("Mask", 2f) }));

            var released = layer.FindState(BridgePlanBuilder.ReleasedStateName).Driver;
            Assert.That(released.LocalOnly, Is.True);
            Assert.That(
                released.Entries.Select(entry => (entry.Parameter, entry.Value)),
                Is.EqualTo(new[] { ("Blush", 1f), ("Mask", 0f) }));
        }

        [Test]
        public void ExtraParametersLayer_SkipsKeptParameters_OnRelease()
        {
            var layer = ExtraParametersLayer(
                new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, null),
                new ExtraParameterTarget("Mask", BridgeParameterType.Int, 2f, 0f));

            var released = layer.FindState(BridgePlanBuilder.ReleasedStateName).Driver;
            Assert.That(released.Entries.Select(entry => entry.Parameter), Is.EqualTo(new[] { "Mask" }));
        }

        [Test]
        public void ExtraParametersLayer_HasNoReleaseDriver_WhenEveryParameterIsKept()
        {
            var layer = ExtraParametersLayer(new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, null));

            Assert.That(layer.FindState(BridgePlanBuilder.ReleasedStateName).Driver, Is.Null);
        }

        [Test]
        public void ExtraParametersLayer_TogglesBetweenEngagedAndReleased()
        {
            var layer = ExtraParametersLayer(new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, 1f));

            var fromEngaged = layer.TransitionsFrom(BridgePlanBuilder.EngagedStateName).Single();
            Assert.That(fromEngaged.To, Is.EqualTo(BridgePlanBuilder.ReleasedStateName));
            AssertCondition(
                fromEngaged.Conditions.Single(),
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeConditionMode.IfNot,
                0f);

            var fromReleased = layer.TransitionsFrom(BridgePlanBuilder.ReleasedStateName).Single();
            Assert.That(fromReleased.To, Is.EqualTo(BridgePlanBuilder.EngagedStateName));
            AssertCondition(
                fromReleased.Conditions.Single(),
                BridgeParameterNames.FacialExpressionsDisabled,
                BridgeConditionMode.If,
                0f);
        }

        [Test]
        public void ExtraParametersLayer_FollowsLipTrackingTrigger()
        {
            var layer = BridgePlanBuilder
                .Build(Settings(trigger: BypassTrigger.LipTrackingOnly).WithExtraParameters(new[]
                {
                    new ExtraParameterTarget("Blush", BridgeParameterType.Bool, 0f, 1f),
                }))
                .FindLayer(BridgePlanBuilder.ExtraParametersLayerName);

            AssertCondition(
                layer.TransitionsFrom(BridgePlanBuilder.InitialStateName).Single().Conditions.Single(),
                BridgeParameterNames.LipTrackingActive,
                BridgeConditionMode.Greater,
                0.5f);
        }

        private static BridgeSettings SettingsWithExtras(params ExtraParameterTarget[] targets)
        {
            return Settings().WithExtraParameters(targets);
        }

        private static BridgeLayerPlan ExtraParametersLayer(params ExtraParameterTarget[] targets)
        {
            return BridgePlanBuilder
                .Build(SettingsWithExtras(targets))
                .FindLayer(BridgePlanBuilder.ExtraParametersLayerName);
        }

        /// <summary>リネーム後の名前が計画へ入ることを見るための、解決済みの名前</summary>
        private static FaceEmoParameterNames ResolvedNames =>
            new FaceEmoParameterNames("FaceEmo_LOCK", "FaceEmo_BLINK", "FaceEmo_EMOTE", resolved: true);

        private static BridgeLayerPlan ExpressionControlLayer(int faceEmoteIndex = 0)
        {
            return BridgePlanBuilder
                .Build(
                    Settings(controlMethod: ControlMethod.ExpressionControl, faceEmoteIndex: faceEmoteIndex),
                    ResolvedNames)
                .FindLayer(BridgePlanBuilder.ExpressionControlLayerName);
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
