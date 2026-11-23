using System;
using IronHorizon.Core.Sim;
using NUnit.Framework;

namespace IronHorizon.Tests.Core
{
    public class SimRandomTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new SimRandom(20261117);
            var b = new SimRandom(20261117);
            for (int i = 0; i < 100000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), "Diverged at draw " + i);
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentSequences()
        {
            var a = new SimRandom(1);
            var b = new SimRandom(2);
            int same = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    same++;
                }
            }

            Assert.Less(same, 5);
        }

        // Reference values from an independent C implementation of SplitMix64 + xoshiro128**.
        // If these change, every saved replay breaks: never edit them to make the test pass.
        [TestCase(0UL, new uint[] { 3737715805u, 2584255861u, 2876756834u, 3286328325u, 1553311962u, 1625202774u, 3260698944u, 2754151956u })]
        [TestCase(20261117UL, new uint[] { 3592463673u, 4003312634u, 3815044563u, 699939305u, 932432285u, 4142755711u, 3149199187u, 2048340503u })]
        public void MatchesReferenceImplementation(ulong seed, uint[] expected)
        {
            var random = new SimRandom(seed);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], random.NextUInt(), "Draw " + i);
            }
        }

        [Test]
        public void NextInt_StaysInRangeAndCoversIt()
        {
            var random = new SimRandom(7);
            var seen = new bool[10];
            for (int i = 0; i < 10000; i++)
            {
                int v = random.NextInt(10);
                Assert.That(v, Is.InRange(0, 9));
                seen[v] = true;
            }

            Assert.That(seen, Is.All.True);
        }

        [Test]
        public void Range_HandlesNegativeAndExtremeBounds()
        {
            var random = new SimRandom(9);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(random.Range(-5, 5), Is.InRange(-5, 4));
                Assert.That(random.Range(int.MinValue, int.MaxValue), Is.InRange(int.MinValue, int.MaxValue - 1));
            }
        }

        [Test]
        public void Chance_RespectsZeroAndCertainty()
        {
            var random = new SimRandom(11);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(random.Chance(0, 100));
                Assert.IsTrue(random.Chance(100, 100));
            }
        }

        [Test]
        public void Chance_IsCloseToRequestedRate()
        {
            var random = new SimRandom(13);
            int hits = 0;
            const int draws = 100000;
            for (int i = 0; i < draws; i++)
            {
                if (random.Chance(25, 100))
                {
                    hits++;
                }
            }

            Assert.That(hits, Is.InRange(24000, 26000));
        }

        [Test]
        public void State_RoundTripRestoresSequence()
        {
            var random = new SimRandom(42);
            random.NextUInt();
            SimRandomState saved = random.State;
            uint expected = random.NextUInt();

            random.NextUInt();
            random.State = saved;
            Assert.AreEqual(expected, random.NextUInt());
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var random = new SimRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Range(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Chance(101, 100));
            Assert.Throws<ArgumentException>(() => random.State = new SimRandomState(0, 0, 0, 0));
        }
    }
}
