using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FEJsTBridge.Domain;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// 制御方式の既定と、保存済みデータの扱いを検証する
    /// </summary>
    public class ControlMethodDefaultTests
    {
        private GameObject _holder;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject(nameof(ControlMethodDefaultTests));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        /// <summary>
        /// エディタがコンポーネントを追加する経路ではResetが呼ばれ、既定が入る
        /// </summary>
        [Test]
        public void AddedComponent_UsesExpressionControl()
        {
            var component = ObjectFactory.AddComponent<FEJsTBridgeComponent>(_holder);

            Assert.That(component.controlMethod, Is.EqualTo(ControlMethod.ExpressionControl));
            Assert.That(component.faceEmoteIndex, Is.EqualTo(FEJsTBridgeComponent.DefaultFaceEmoteIndex));
        }

        /// <summary>
        /// 制御方式を持たない版で保存したアバターは、シリアライズデータにこの値を持たない
        /// </summary>
        /// <remarks>
        /// Unityはデータに無いフィールドを初期化子の値のまま残すため、
        /// 初期化子がバイパスでなければ、更新しただけで方式が変わってしまう。
        /// Resetを通らない追加は、そのときの読み込みと同じ状態になる。
        /// </remarks>
        [Test]
        public void ComponentWithoutSavedMethod_StaysOnBypass()
        {
            var component = _holder.AddComponent<FEJsTBridgeComponent>();

            Assert.That(component.controlMethod, Is.EqualTo(ControlMethod.Bypass));
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
