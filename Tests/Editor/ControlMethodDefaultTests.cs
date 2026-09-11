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
        /// コンポーネントを追加するとResetが呼ばれ、既定が入る
        /// </summary>
        /// <remarks>
        /// 保存データにこの値を持たないアバターを読み込んだ場合はResetが呼ばれず、
        /// フィールドの初期化子の値 (バイパス) が残る。そちらはエディタ上で
        /// コンポーネントを作る操作がどれもResetを通るため、ここでは再現できない。
        /// 初期化子を変えてはいけない理由は FEJsTBridgeComponent.Reset に書いてある。
        /// </remarks>
        [Test]
        public void AddedComponent_UsesExpressionControl()
        {
            var component = ObjectFactory.AddComponent<FEJsTBridgeComponent>(_holder);

            Assert.That(component.controlMethod, Is.EqualTo(ControlMethod.ExpressionControl));
            Assert.That(component.faceEmoteIndex, Is.EqualTo(FEJsTBridgeComponent.DefaultFaceEmoteIndex));
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
