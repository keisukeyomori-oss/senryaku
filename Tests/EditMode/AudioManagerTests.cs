using System.Reflection;
using BirthdayTactics.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests.EditMode
{
    public sealed class AudioManagerTests
    {
        [TestCase("BD-01")]
        [TestCase("BD-02")]
        [TestCase("BD-03")]
        [TestCase("BD-04")]
        [TestCase("BD-05")]
        public void RequiredBgm_LoadsTheAuthoredStereoWave(string trackId)
        {
            var host = new GameObject("AudioManager test host");
            try
            {
                AudioManager manager = host.AddComponent<AudioManager>();
                MethodInfo loadTrack = typeof(AudioManager).GetMethod(
                    "LoadTrack",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(loadTrack, Is.Not.Null);
                var clip = loadTrack.Invoke(manager, new object[] { trackId }) as AudioClip;
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.samples, Is.GreaterThan(22050));
                Assert.That(clip.frequency, Is.GreaterThanOrEqualTo(32000));
                Assert.That(clip.channels, Is.EqualTo(2));

                float[] samples = new float[Mathf.Min(clip.samples, 22050)];
                Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(samples, Has.Some.Not.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase("select")]
        [TestCase("move")]
        [TestCase("attack")]
        [TestCase("victory")]
        [TestCase("defeat")]
        [TestCase("gift")]
        public void RequiredEffect_LoadsTheAuthoredWave(string effectId)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/SFX/{effectId}");
            Assert.That(clip, Is.Not.Null, effectId);
            Assert.That(clip.frequency, Is.GreaterThanOrEqualTo(32000));
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.samples, Is.GreaterThan(1000));
        }
    }
}
