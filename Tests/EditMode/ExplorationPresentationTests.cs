using System.Reflection;
using BirthdayTactics.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class ExplorationPresentationTests
    {
        [Test]
        public void RunBlend_RisesWhilePositionChangesAndSettlesAfterStopping()
        {
            MethodInfo advance = typeof(VerticalSliceController).GetMethod(
                "AdvanceRunBlend",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(advance, Is.Not.Null);

            object[] movingArguments = { 0f, Vector2.zero, new Vector2(0.1f, 0f), 0.1f };
            float moving = (float)advance.Invoke(null, movingArguments);
            Assert.That(moving, Is.GreaterThan(0.8f));

            var previous = (Vector2)movingArguments[1];
            object[] stoppedArguments = { moving, previous, previous, 0.1f };
            float stopped = (float)advance.Invoke(null, stoppedArguments);
            Assert.That(stopped, Is.LessThan(moving));
            Assert.That(stopped, Is.GreaterThan(0f));
        }

        [Test]
        public void Headquarters_UsesDedicatedHighResolutionBackground()
        {
            Texture2D background = Resources.Load<Texture2D>("Art/Story/story_base");

            Assert.That(background, Is.Not.Null);
            Assert.That(background.width, Is.GreaterThanOrEqualTo(1600));
            Assert.That(background.height, Is.GreaterThanOrEqualTo(900));
        }
    }
}
