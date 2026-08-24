using System.Collections.Generic;
using System.Linq;

namespace FEJsTBridge.Domain
{
    /// <summary>
    /// アバターにJerry's TemplatesとFaceEmoが載っているかの検出結果
    ///
    /// 判定はパッケージのインストール有無ではなく、アバターに載っているコントローラの
    /// パラメータ名で行う。生成済みプレハブがMerge Animator経由で載る形を対象にするため、
    /// アセンブリ検出では「このアバターに実際に載っているか」を判定できない。
    /// </summary>
    internal readonly struct EnvironmentReport
    {
        public EnvironmentReport(bool jerryDetected, bool faceEmoDetected)
        {
            JerryDetected = jerryDetected;
            FaceEmoDetected = faceEmoDetected;
        }

        public bool JerryDetected { get; }

        public bool FaceEmoDetected { get; }

        /// <summary>
        /// アバター内の各コントローラのパラメータ名から検出結果を組み立てる
        /// </summary>
        public static EnvironmentReport Detect(IEnumerable<IReadOnlyCollection<string>> controllerParameterNames)
        {
            var jerryDetected = false;
            var faceEmoDetected = false;

            if (controllerParameterNames == null)
            {
                return new EnvironmentReport(false, false);
            }

            foreach (var names in controllerParameterNames)
            {
                if (names == null)
                {
                    continue;
                }

                if (names.Contains(BridgeParameterNames.FacialExpressionsDisabled)
                    && names.Contains(BridgeParameterNames.EyeTrackingActive))
                {
                    jerryDetected = true;
                }

                if (names.Contains(BridgeParameterNames.ForceBypassEnable))
                {
                    faceEmoDetected = true;
                }
            }

            return new EnvironmentReport(jerryDetected, faceEmoDetected);
        }
    }
}
