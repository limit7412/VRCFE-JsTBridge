using System.Collections.Generic;
using NUnit.Framework;
using FEJsTBridge.Domain;

namespace FEJsTBridge.Tests
{
    public class EnvironmentReportTests
    {
        private static readonly string[] JerryParameters =
        {
            BridgeParameterNames.FacialExpressionsDisabled,
            BridgeParameterNames.EyeTrackingActive,
            BridgeParameterNames.LipTrackingActive,
            BridgeParameterNames.VisemesEnable,
        };

        private static readonly string[] FaceEmoParameters =
        {
            BridgeParameterNames.ForceBypassEnable,
            "CN_BLINK_ENABLE",
        };

        [Test]
        public void Detect_FindsBoth_WhenControllersOfBothPackagesExist()
        {
            var report = EnvironmentReport.Detect(new List<IReadOnlyCollection<string>>
            {
                new[] { "Unrelated" },
                JerryParameters,
                FaceEmoParameters,
            });

            Assert.That(report.JerryDetected, Is.True);
            Assert.That(report.FaceEmoDetected, Is.True);
        }

        [Test]
        public void Detect_DoesNotFindJerry_WhenOnlyOneOfItsParametersExists()
        {
            var report = EnvironmentReport.Detect(new List<IReadOnlyCollection<string>>
            {
                new[] { BridgeParameterNames.FacialExpressionsDisabled },
                FaceEmoParameters,
            });

            Assert.That(report.JerryDetected, Is.False);
            Assert.That(report.FaceEmoDetected, Is.True);
        }

        [Test]
        public void Detect_FindsNothing_WhenNoControllerExists()
        {
            var report = EnvironmentReport.Detect(new List<IReadOnlyCollection<string>>());

            Assert.That(report.JerryDetected, Is.False);
            Assert.That(report.FaceEmoDetected, Is.False);
        }

        [Test]
        public void Detect_IgnoresNull()
        {
            var report = EnvironmentReport.Detect(new List<IReadOnlyCollection<string>> { null, JerryParameters });

            Assert.That(report.JerryDetected, Is.True);
            Assert.That(report.FaceEmoDetected, Is.False);
        }
    }
}
