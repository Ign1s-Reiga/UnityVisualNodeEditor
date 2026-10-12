using NUnit.Framework;
using Reiga.VisualNodeEditor.YarnSpinner.Editor;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;

namespace Reiga.VisualNodeEditor.YarnSpinner.Tests
{
    /// <summary>
    /// Yarn Graph Bridge を足したときの Variable Storage の設定（<see cref="YarnBridgeSetup.ShareVariables"/>）:
    /// Yarn の既定の In Memory Variable Storage（Dialogue System のプレハブに最初から入っている）は置き換え、ユーザーが選んだものは残す。
    /// </summary>
    public sealed class YarnBridgeSetupTests
    {
        private GameObject _gameObject;
        private DialogueRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Dialogue System");
            _runner = _gameObject.AddComponent<DialogueRunner>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        [Test]
        public void ShareVariables_ReplacesYarnsDefaultInMemoryStorage_AndRemovesIt()
        {
            SetStorage(_gameObject.AddComponent<InMemoryVariableStorage>());

            Assert.That(YarnBridgeSetup.ShareVariables(_runner), Is.True);

            Assert.That(GetStorage(), Is.InstanceOf<GraphVariableStorage>());
            Assert.That(_gameObject.GetComponent<InMemoryVariableStorage>(), Is.Null, "an unused storage left behind could be picked by GetComponent");
        }

        [Test]
        public void ShareVariables_LeavesAStorageOnAnotherGameObject()
        {
            var other = new GameObject("Variables");
            try
            {
                SetStorage(other.AddComponent<InMemoryVariableStorage>());

                Assert.That(YarnBridgeSetup.ShareVariables(_runner, false), Is.True);

                Assert.That(GetStorage(), Is.InstanceOf<GraphVariableStorage>());
                Assert.That(other.GetComponent<InMemoryVariableStorage>(), Is.Not.Null, "only the one on the Dialogue Runner's GameObject is removed");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void ShareVariables_FillsAnEmptyStorage()
        {
            Assert.That(YarnBridgeSetup.ShareVariables(_runner), Is.True);

            Assert.That(GetStorage(), Is.InstanceOf<GraphVariableStorage>());
        }

        [Test]
        public void IsReplaceable_OnlyYarnsDefault_NotAStorageTheUserChose()
        {
            Assert.That(YarnBridgeSetup.IsReplaceable(null), Is.True, "nothing set yet");
            Assert.That(YarnBridgeSetup.IsReplaceable(typeof(InMemoryVariableStorage)), Is.True, "Yarn's default");
            Assert.That(YarnBridgeSetup.IsReplaceable(typeof(GraphVariableStorage)), Is.True);
            Assert.That(YarnBridgeSetup.IsReplaceable(typeof(CustomVariableStorage)), Is.False, "a class of the user's own, even one built on the default");
        }

        private void SetStorage(Object storage)
        {
            var serializedRunner = new SerializedObject(_runner);
            serializedRunner.FindProperty(YarnBridgeSetup.VariableStoragePropertyName).objectReferenceValue = storage;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }

        private Object GetStorage() =>
            new SerializedObject(_runner).FindProperty(YarnBridgeSetup.VariableStoragePropertyName).objectReferenceValue;
    }
}
