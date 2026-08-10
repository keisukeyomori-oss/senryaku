using System.IO;
using BirthdayTactics.Core;
using NUnit.Framework;
using UnityEngine;

namespace BirthdayTactics.Tests
{
    public sealed class RealtimePixelRigTests
    {
        [Test]
        public void EveryPixelCharacter_HasCompletePerPixelSkinData()
        {
            foreach (string unitId in PixelAnimationProfile.SupportedUnitIds)
            {
                TextAsset asset = Resources.Load<TextAsset>($"Art/Pixel/SkinData/{unitId}");
                Assert.That(asset, Is.Not.Null, unitId);
                using (var stream = new MemoryStream(asset.bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("PSK1"), unitId);
                    Assert.That(reader.ReadByte(), Is.EqualTo(128), unitId);
                    Assert.That(reader.ReadByte(), Is.EqualTo(128), unitId);
                    int pixelCount = reader.ReadInt32();
                    Assert.That(pixelCount, Is.GreaterThan(4000), unitId);
                    Assert.That(stream.Length, Is.EqualTo(10L + pixelCount * 9L), unitId);
                }
            }
        }

        [Test]
        public void EveryPixelCharacter_HasCompleteRigidPartResources()
        {
            string[] parts =
            {
                "cape", "thigh_right", "shin_right", "upper_arm_right", "forearm_right",
                "torso", "thigh_left", "shin_left", "upper_arm_left", "forearm_left",
                "weapon", "head"
            };
            foreach (string unitId in PixelAnimationProfile.SupportedUnitIds)
            {
                foreach (string part in parts)
                {
                    Texture2D texture = Resources.Load<Texture2D>(
                        $"Art/Pixel/BoneParts/{unitId}/{part}");
                    Assert.That(texture, Is.Not.Null, $"{unitId}/{part}");
                    Assert.That(texture.width, Is.EqualTo(128), $"{unitId}/{part}");
                    Assert.That(texture.height, Is.EqualTo(128), $"{unitId}/{part}");
                }
            }
        }
    }
}
