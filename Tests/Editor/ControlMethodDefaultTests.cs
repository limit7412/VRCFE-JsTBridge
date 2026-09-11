using NUnit.Framework;
using UnityEngine;
using FEJsTBridge.Domain;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 制御方式の既定と、シリアライズされた値の意味を検証する
    /// </summary>
    public class ControlMethodDefaultTests
    {
        [Test]
        public void NewComponent_UsesExpressionControl()
        {
            var holder = new GameObject(nameof(NewComponent_UsesExpressionControl));

            try
            {
                var component = holder.AddComponent<FEJsTBridgeComponent>();

                Assert.That(component.controlMethod, Is.EqualTo(ControlMethod.ExpressionControl));
                Assert.That(component.faceEmoteIndex, Is.EqualTo(FEJsTBridgeComponent.DefaultFaceEmoteIndex));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void DefaultSettings_UseExpressionControl()
        {
            Assert.That(BridgeSettings.Default.ControlMethod, Is.EqualTo(ControlMethod.ExpressionControl));
        }

        /// <summary>
        /// 宣言順はシリアライズされた値の意味そのものである。
        /// 入れ替えると、追加済みのコンポーネントが別の方式で動くようになる
        /// </summary>
        [Test]
        public void ControlMethod_KeepsDeclarationOrder()
        {
            Assert.That((int)ControlMethod.Bypass, Is.EqualTo(0));
            Assert.That((int)ControlMethod.ExpressionControl, Is.EqualTo(1));
        }
    }
}
