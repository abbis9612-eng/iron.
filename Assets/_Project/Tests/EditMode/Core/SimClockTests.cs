using System;
using IronHorizon.Core.Sim;
using NUnit.Framework;

namespace IronHorizon.Tests.Core
{
    public class SimClockTests
    {
        [TestCase(SimSpeed.Half, 15)]
        [TestCase(SimSpeed.Normal, 30)]
        [TestCase(SimSpeed.Double, 60)]
        [TestCase(SimSpeed.Quadruple, 120)]
        public void OneRealSecond_RunsThirtyTicksTimesSpeed(SimSpeed speed, int expectedTicks)
        {
            var clock = new SimClock();
            clock.SetSpeed(speed);
            int ticks = 0;
            for (int frame = 0; frame < 100; frame++)
            {
                ticks += clock.Advance(10000); // 100 frames of 10 ms
            }

            Assert.AreEqual(expectedTicks, ticks);
        }

        [Test]
        public void OddFrameTimes_LoseNoTime()
        {
            // 30 frames of 33,333 us = 999,990 us: one microsecond short of 30 ticks would still be 29.
            var clock = new SimClock();
            int ticks = 0;
            for (int i = 0; i < 30; i++)
            {
                ticks += clock.Advance(33333);
            }

            Assert.AreEqual(29, ticks);
            Assert.AreEqual(1, clock.Advance(10));
        }

        [Test]
        public void Paused_RunsNoTicks()
        {
            var clock = new SimClock();
            clock.Pause();
            Assert.AreEqual(0, clock.Advance(500000));
            clock.Resume();
            Assert.AreEqual(15, clock.Advance(500000));
        }

        [Test]
        public void LongHitch_IsCappedAndCounted()
        {
            var clock = new SimClock(maxTicksPerAdvance: 4);
            Assert.AreEqual(4, clock.Advance(1000000));
            Assert.AreEqual(26, clock.DroppedTicks);
        }

        [Test]
        public void InvalidInput_Throws()
        {
            var clock = new SimClock();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetSpeed((SimSpeed)3));
        }
    }
}
