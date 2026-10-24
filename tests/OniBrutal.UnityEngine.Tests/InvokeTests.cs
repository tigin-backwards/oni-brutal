using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // MonoBehaviour.Invoke as measured in the original player (PerfProbe measure.txt), and named
    // colours mods use (BlueprintsV2).
    public class InvokeTests : EngineTest
    {
        sealed class Target : MonoBehaviour
        {
            public string Phase = "start";
            public readonly List<string> Calls = new();
            void Update() => Phase = "update";
            void LateUpdate() => Phase = "late";
            void Hit() => Calls.Add("hit after " + Phase);
            void Repeat() => Calls.Add("repeat");
        }

        sealed class Starter : MonoBehaviour
        {
            public Target Target;
            IEnumerator Start()
            {
                yield return null;
                Target.Invoke("Hit", 0f);
            }
        }

        [Fact]
        public void ADueCallRunsAfterUpdateEvenOnADisabledOrInactiveTarget()
        {
            var enabled = new GameObject("Enabled").AddComponent<Target>();
            var disabled = new GameObject("Disabled").AddComponent<Target>();
            disabled.enabled = false;
            var inactive = new GameObject("Inactive").AddComponent<Target>();
            enabled.Invoke("Hit", 0.1f);
            disabled.Invoke("Hit", 0.1f);
            inactive.Invoke("Hit", 0.1f);
            inactive.gameObject.SetActive(false);
            Assert.True(enabled.IsInvoking("Hit"));
            Assert.True(inactive.IsInvoking());
            for (int i = 0; i < 12; i++) Frame();
            Assert.Equal(["hit after update"], enabled.Calls);
            Assert.Equal(["hit after start"], disabled.Calls);
            Assert.Equal(["hit after start"], inactive.Calls);
            Assert.False(enabled.IsInvoking());
        }

        [Fact]
        public void ACallMadeAfterUpdateRunsAfterLateUpdateTheSameFrame()
        {
            var target = new GameObject("Target").AddComponent<Target>();
            new GameObject("Starter").AddComponent<Starter>().Target = target;
            Frame();
            Frame();
            Assert.Equal(["hit after late"], target.Calls);
        }

        [Fact]
        public void RepeatingCallsRunAtTheirRateUntilCancelled()
        {
            var target = new GameObject("Target").AddComponent<Target>();
            target.InvokeRepeating("Repeat", 0f, 0.1f);
            for (int i = 0; i < 60; i++) Frame();
            // At 0, 0.1, … 0.9 within the first second's frames (60 at 1/60 s).
            Assert.InRange(target.Calls.Count, 10, 11);
            target.CancelInvoke("Repeat");
            Assert.False(target.IsInvoking());
            int count = target.Calls.Count;
            for (int i = 0; i < 30; i++) Frame();
            Assert.Equal(count, target.Calls.Count);
        }

        [Fact]
        public void AMissingMethodIsSkippedQuietly()
        {
            var target = new GameObject("Target").AddComponent<Target>();
            target.Invoke("Missing", 0f);
            Frame();
            Assert.False(target.IsInvoking());
        }
    }
}
