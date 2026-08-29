using System.Collections.Generic;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// 設定値から生成計画を組み立てる
    /// 生成物仕様の実体はすべてここにあり、他の層は判断を持たない
    /// </summary>
    internal static class BridgePlanBuilder
    {
        public const string BypassLayerName = "BypassBridge";
        public const string TrackingReapplyLayerName = "TrackingReapply";

        public const string IdleStateName = "Idle";
        public const string BypassStateName = "Bypass";
        public const string WaitStateName = "Wait";
        public const string ArmedStateName = "Armed";

        /// <summary>Armed以外の全ステートで共有する空クリップの長さ</summary>
        public const float DefaultClipLengthSeconds = 1.0f;

        /// <summary>
        /// Float型トリガーの判定閾値
        /// EyeTrackingActiveとLipTrackingActiveは0か1しか取らないため、
        /// 0.5で切ればJerryの0.992 / 0.008による判定と等価になる
        /// </summary>
        private const float FloatTriggerThreshold = 0.5f;

        public static BridgeControllerPlan Build(BridgeSettings settings)
        {
            var parameters = new[]
            {
                new BridgeParameterPlan(BridgeParameterNames.FacialExpressionsDisabled, BridgeParameterType.Bool),
                new BridgeParameterPlan(BridgeParameterNames.LipTrackingActive, BridgeParameterType.Float),
                new BridgeParameterPlan(BridgeParameterNames.EyeTrackingActive, BridgeParameterType.Float),
                new BridgeParameterPlan(BridgeParameterNames.VisemesEnable, BridgeParameterType.Bool),
                new BridgeParameterPlan(BridgeParameterNames.ForceBypassEnable, BridgeParameterType.Bool),
            };

            var layers = new List<BridgeLayerPlan> { BuildBypassLayer(settings) };
            if (settings.EnableTrackingReapply)
            {
                layers.Add(BuildTrackingReapplyLayer(settings));
            }

            return new BridgeControllerPlan(parameters, layers);
        }

        /// <summary>
        /// トリガーをCN_FORCE_BYPASS_ENABLEへ写すレイヤー
        ///
        /// 写しは一度きりにせず、空クリップを1周するたびに同じステートへ入り直して
        /// Driverを発火し直す。アバターのロード中はDriverの書き込みが失われることがあり、
        /// ステートが目的地に着いたまま値だけが既定へ戻ると、装着者がトラッキングを
        /// 切り替え直すまでバイパスが成立しない。FaceEmo側の遷移はこの値そのものを
        /// 条件に持つため、書き直しさえ届けば以降の連鎖はFaceEmoの中で復旧する。
        /// 同じ値の書き直しは無害で、非同期パラメータのため同期帯域も使わない。
        /// </summary>
        private static BridgeLayerPlan BuildBypassLayer(BridgeSettings settings)
        {
            var states = new[]
            {
                new BridgeStatePlan(
                    IdleStateName,
                    DefaultClipLengthSeconds,
                    driver: BypassDriver(0f)),
                new BridgeStatePlan(
                    BypassStateName,
                    DefaultClipLengthSeconds,
                    driver: BypassDriver(1f)),
            };

            var transitions = new[]
            {
                new BridgeTransitionPlan(IdleStateName, BypassStateName, new[] { TriggerOn(settings) }),
                new BridgeTransitionPlan(BypassStateName, IdleStateName, new[] { TriggerOff(settings) }),

                // 空クリップを1周したら同じステートへ入り直し、Driverを発火し直す。
                // 条件付きの遷移を記載順の先に置いてあるため、トリガーの切替はループより優先される
                new BridgeTransitionPlan(
                    IdleStateName,
                    IdleStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f),
                new BridgeTransitionPlan(
                    BypassStateName,
                    BypassStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f),
            };

            return new BridgeLayerPlan(BypassLayerName, IdleStateName, states, transitions);
        }

        /// <summary>
        /// バイパス確定後にTracking ControlをJerryの状態へ合わせ直すレイヤー
        ///
        /// JerryとFaceEmoはどちらもステート突入時にTracking Controlを一度だけ適用する。
        /// バイパスはDriverの連鎖で成立するぶんFaceEmo側の適用が必ず後になり、
        /// Jerryの適用を上書きしてしまうため、さらに後から適用し直す。
        ///
        /// 適用し直しは一度きりにせず、バイパス継続中はApplyからArmedへ周期的に戻して繰り返す。
        /// 後からjoinした人のクライアントではアバターのロード中にフレームが大きく落ち、
        /// FaceEmo側の適用がReapplyDelaySecondsを超えて遅れることがある。
        /// 一度きりだと逆転した適用順のまま残るが、同じ値のTracking Controlの再適用は
        /// 見た目を変えないため、繰り返しても既にいる人には影響しない。
        /// </summary>
        private static BridgeLayerPlan BuildTrackingReapplyLayer(BridgeSettings settings)
        {
            var states = new List<BridgeStatePlan>
            {
                new BridgeStatePlan(WaitStateName, DefaultClipLengthSeconds),
                new BridgeStatePlan(ArmedStateName, settings.ReapplyDelaySeconds),
            };

            foreach (var (eye, visemes) in TrackingCombinations())
            {
                states.Add(new BridgeStatePlan(
                    ApplyStateName(eye, visemes),
                    DefaultClipLengthSeconds,
                    trackingControl: new BridgeTrackingControlPlan(
                        eyes: eye ? BridgeTrackingState.Animation : BridgeTrackingState.Tracking,
                        mouth: visemes ? BridgeTrackingState.Tracking : BridgeTrackingState.Animation)));
            }

            var transitions = new List<BridgeTransitionPlan>
            {
                new BridgeTransitionPlan(WaitStateName, ArmedStateName, new[] { TriggerOn(settings) }),
            };

            // Armedの空クリップを1周するまで発火しないため、Exit Timeが待ち時間そのものになる
            foreach (var (eye, visemes) in TrackingCombinations())
            {
                transitions.Add(new BridgeTransitionPlan(
                    ArmedStateName,
                    ApplyStateName(eye, visemes),
                    new[] { EyeCondition(eye), VisemesCondition(visemes) },
                    hasExitTime: true,
                    exitTime: 1.0f));
            }

            transitions.Add(new BridgeTransitionPlan(ArmedStateName, WaitStateName, new[] { TriggerOff(settings) }));

            // バイパス継続中に目の有効化やVisemesのトグルがあっても追従して再適用する
            foreach (var (eye, visemes) in TrackingCombinations())
            {
                var from = ApplyStateName(eye, visemes);

                transitions.Add(new BridgeTransitionPlan(
                    from,
                    ApplyStateName(!eye, visemes),
                    new[] { EyeCondition(!eye) }));

                transitions.Add(new BridgeTransitionPlan(
                    from,
                    ApplyStateName(eye, !visemes),
                    new[] { VisemesCondition(!visemes) }));

                transitions.Add(new BridgeTransitionPlan(
                    from,
                    WaitStateName,
                    new[] { TriggerOff(settings) }));

                // 空クリップを1周したらArmedへ戻り、Tracking Controlを適用し直す。
                // 条件付きの3本を先に並べてあるため、切替追従と解除はこのループより優先される
                transitions.Add(new BridgeTransitionPlan(
                    from,
                    ArmedStateName,
                    new BridgeConditionPlan[0],
                    hasExitTime: true,
                    exitTime: 1.0f));
            }

            return new BridgeLayerPlan(TrackingReapplyLayerName, WaitStateName, states, transitions);
        }

        public static string ApplyStateName(bool eyeTracking, bool visemesEnabled)
        {
            return $"Apply_E{(eyeTracking ? 1 : 0)}_V{(visemesEnabled ? 1 : 0)}";
        }

        /// <summary>目のトラッキングとVisemesの全組み合わせ</summary>
        private static IEnumerable<(bool eye, bool visemes)> TrackingCombinations()
        {
            yield return (false, false);
            yield return (false, true);
            yield return (true, false);
            yield return (true, true);
        }

        private static BridgeDriverPlan BypassDriver(float value)
        {
            return new BridgeDriverPlan(
                localOnly: false,
                entries: new[] { new BridgeDriverEntry(BridgeParameterNames.ForceBypassEnable, value) });
        }

        public static BridgeConditionPlan TriggerOn(BridgeSettings settings)
        {
            return settings.BypassTrigger == BypassTrigger.LipTrackingOnly
                ? new BridgeConditionPlan(
                    BridgeParameterNames.LipTrackingActive, BridgeConditionMode.Greater, FloatTriggerThreshold)
                : new BridgeConditionPlan(
                    BridgeParameterNames.FacialExpressionsDisabled, BridgeConditionMode.If);
        }

        public static BridgeConditionPlan TriggerOff(BridgeSettings settings)
        {
            return settings.BypassTrigger == BypassTrigger.LipTrackingOnly
                ? new BridgeConditionPlan(
                    BridgeParameterNames.LipTrackingActive, BridgeConditionMode.Less, FloatTriggerThreshold)
                : new BridgeConditionPlan(
                    BridgeParameterNames.FacialExpressionsDisabled, BridgeConditionMode.IfNot);
        }

        private static BridgeConditionPlan EyeCondition(bool active)
        {
            return new BridgeConditionPlan(
                BridgeParameterNames.EyeTrackingActive,
                active ? BridgeConditionMode.Greater : BridgeConditionMode.Less,
                FloatTriggerThreshold);
        }

        private static BridgeConditionPlan VisemesCondition(bool enabled)
        {
            // Jerry自身のMouth切替はメニューの同期パラメータをそのまま条件に持つため、
            // 同じ値を読めばApplyステートの選択がJerryの適用と一致する
            return new BridgeConditionPlan(
                BridgeParameterNames.VisemesEnable,
                enabled ? BridgeConditionMode.If : BridgeConditionMode.IfNot);
        }
    }
}
