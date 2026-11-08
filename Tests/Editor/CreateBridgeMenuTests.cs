using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FEJsTBridge.Handler;
using Object = UnityEngine.Object;

namespace FEJsTBridge.Tests
{
    public class CreateBridgeMenuTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in _created)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            _created.Clear();
        }

        [Test]
        public void CreateObject_AddsComponentOnNewChild()
        {
            var parent = new GameObject("Avatar");
            _created.Add(parent);

            var created = CreateBridgeMenu.CreateObject(parent);

            Assert.That(created.name, Is.EqualTo(CreateBridgeMenu.ObjectName));
            Assert.That(created.transform.parent, Is.SameAs(parent.transform));
            Assert.That(created.GetComponent<FEJsTBridgeComponent>(), Is.Not.Null);
            Assert.That(parent.GetComponent<FEJsTBridgeComponent>(), Is.Null);
        }

        [Test]
        public void CreateObject_UsesDefaults()
        {
            var parent = new GameObject("Avatar");
            _created.Add(parent);

            var component = CreateBridgeMenu.CreateObject(parent).GetComponent<FEJsTBridgeComponent>();

            Assert.That(component.faceEmoteIndex, Is.EqualTo(FEJsTBridgeComponent.DefaultFaceEmoteIndex));
            Assert.That(component.bypassTrigger, Is.EqualTo(BypassTrigger.FacialExpressionsDisabled));
            Assert.That(component.enableTrackingReapply, Is.True);
        }

        [Test]
        public void CreateObject_CreatesAtSceneRoot_WhenParentIsNull()
        {
            var created = CreateBridgeMenu.CreateObject(null);
            _created.Add(created);

            Assert.That(created.transform.parent, Is.Null);
            Assert.That(created.GetComponent<FEJsTBridgeComponent>(), Is.Not.Null);
        }
    }
}
