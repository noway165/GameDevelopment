using System;
using System.Collections.Generic;
using Metropolis.Core;
using NUnit.Framework;

namespace Metropolis.Game.Tests
{
    public class HudTextTests
    {
        [TestCase(50000L, "50,000")]
        [TestCase(-65870L, "-65,870")]
        [TestCase(0L, "0")]
        public void Money(long value, string expected) => Assert.That(HudText.Money(value), Is.EqualTo(expected));

        [TestCase(215L, "+215")]
        [TestCase(-90L, "-90")]
        [TestCase(0L, "0")]
        public void Signed(long value, string expected) => Assert.That(HudText.Signed(value), Is.EqualTo(expected));

        [Test]
        public void Failure_EveryReasonHasItsOwnSentence()
        {
            var seen = new HashSet<string>();
            foreach (CommandFailure f in Enum.GetValues(typeof(CommandFailure)))
            {
                if (f == CommandFailure.None)
                    continue;
                Assert.That(HudText.Failure(f), Is.Not.Empty, f.ToString());
                Assert.That(seen.Add(HudText.Failure(f)), Is.True, f.ToString());
            }

            Assert.That(HudText.Failure(CommandFailure.None), Is.Empty);
            Assert.That(HudText.Failure(CommandFailure.NoRoadAccess), Is.EqualTo("Too far from a connected road."));
        }

        [TestCase(BuildingStatus.None, "Running")]
        [TestCase(BuildingStatus.NoWater, "Not running: no water")]
        [TestCase(BuildingStatus.NoRoad | BuildingStatus.NoPower | BuildingStatus.NoWater, "Not running: no road access, no power, no water")]
        public void Status(BuildingStatus status, string expected) => Assert.That(HudText.Status(status), Is.EqualTo(expected));
    }
}
