using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FEJsTBridge.Presentation;

namespace FEJsTBridge.Tests
{
    /// <summary>
    /// インスペクタから追加した追加パラメータが、既定値で始まることを検証する
    /// </summary>
    public class ExtraParameterEntryDrawerTests
    {
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("ExtraParameterEntryDrawerTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ApplyDefaults_ResetsDuplicatedElement()
        {
            var component = _gameObject.AddComponent<FEJsTBridgeComponent>();
            component.extraParameters.Add(new ExtraParameterEntry
            {
                source = ExtraParameterSource.ParameterName,
                parameterName = "Blush",
                releaseMode = ExtraReleaseMode.Keep,
                syncMode = ExtraSyncMode.Unsynced,
            });

            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(nameof(FEJsTBridgeComponent.extraParameters));

            // 配列を伸ばすと、Unityは末尾の要素を複製する。標準の「+」と同じ状態を作る
            property.arraySize++;
            ExtraParameterEntryDrawer.ApplyDefaults(property.GetArrayElementAtIndex(1));
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var added = component.extraParameters[1];
            var defaults = new ExtraParameterEntry();
            Assert.That(added.source, Is.EqualTo(defaults.source));
            Assert.That(added.parameterName, Is.EqualTo(defaults.parameterName));
            Assert.That(added.releaseMode, Is.EqualTo(ExtraReleaseMode.Restore));
            Assert.That(added.syncMode, Is.EqualTo(defaults.syncMode));

            // 複製元は変わらない
            Assert.That(component.extraParameters[0].parameterName, Is.EqualTo("Blush"));
            Assert.That(component.extraParameters[0].releaseMode, Is.EqualTo(ExtraReleaseMode.Keep));
        }
    }
}
