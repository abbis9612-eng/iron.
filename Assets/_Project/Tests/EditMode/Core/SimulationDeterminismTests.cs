using IronHorizon.Core.Sim;
using NUnit.Framework;

namespace IronHorizon.Tests.Core
{
    public class SimulationDeterminismTests
    {
        private const int Units = 200;
        private const int Ticks = 900; // 30 seconds of match time

        private static void ScheduleScript(Simulation sim)
        {
            sim.Schedule(new SimCommand(15, DeterminismTestWorld.TeleportCommand, 0, 3, 500, -500));
            sim.Schedule(new SimCommand(15, DeterminismTestWorld.FreezeCommand, 1, 3, 60));
            sim.Schedule(new SimCommand(300, DeterminismTestWorld.TeleportCommand, 1, 150, 0, 0));
            sim.Schedule(new SimCommand(301, DeterminismTestWorld.FreezeCommand, 0, 42, 200));
            sim.Schedule(new SimCommand(777, DeterminismTestWorld.TeleportCommand, 0, 199, -9, 9));
        }

        private static DeterminismTestWorld Build(ulong seed, out Simulation sim)
        {
            sim = new Simulation(seed);
            var world = new DeterminismTestWorld(sim, Units, Ticks);
            ScheduleScript(sim);
            return world;
        }

        [Test]
        public void SameSeedAndCommands_GiveSameHashEveryTick()
        {
            DeterminismTestWorld a = Build(12345, out Simulation simA);
            DeterminismTestWorld b = Build(12345, out Simulation simB);
            simA.RunUntil(Ticks);
            simB.RunUntil(Ticks);

            CollectionAssert.AreEqual(a.HashAtTick, b.HashAtTick);
        }

        [Test]
        public void DifferentSeed_GivesDifferentMatch()
        {
            DeterminismTestWorld a = Build(1, out Simulation simA);
            DeterminismTestWorld b = Build(2, out Simulation simB);
            simA.RunUntil(Ticks);
            simB.RunUntil(Ticks);

            Assert.AreNotEqual(a.HashAtTick[Ticks], b.HashAtTick[Ticks]);
        }

        [Test]
        public void GameSpeedAndFrameRate_DoNotChangeTheResult()
        {
            // Reference: stepped directly, no clock.
            DeterminismTestWorld reference = Build(777, out Simulation refSim);
            refSim.RunUntil(Ticks);

            // 1x at a steady 60 fps.
            DeterminismTestWorld steady = Build(777, out Simulation steadySim);
            while (steadySim.Tick < Ticks)
            {
                steadySim.Update(16667);
            }

            // 4x with uneven frame times.
            DeterminismTestWorld fast = Build(777, out Simulation fastSim);
            fastSim.Clock.SetSpeed(SimSpeed.Quadruple);
            long[] frames = { 7000, 16667, 33333, 41000, 9000, 120000 };
            for (int f = 0; fastSim.Tick < Ticks; f++)
            {
                fastSim.Update(frames[f % frames.Length]);
            }

            // Mixed: speed changes and pauses mid-match.
            DeterminismTestWorld mixed = Build(777, out Simulation mixedSim);
            SimSpeed[] speeds = { SimSpeed.Half, SimSpeed.Normal, SimSpeed.Double, SimSpeed.Quadruple };
            for (int f = 0; mixedSim.Tick < Ticks; f++)
            {
                if (f % 50 == 0)
                {
                    mixedSim.Clock.SetSpeed(speeds[(f / 50) % speeds.Length]);
                }

                if (f % 70 == 0)
                {
                    mixedSim.Clock.Pause();
                }
                else if (f % 70 == 5)
                {
                    mixedSim.Clock.Resume();
                }

                mixedSim.Update(20000);
            }

            for (int tick = 1; tick <= Ticks; tick++)
            {
                Assert.AreEqual(reference.HashAtTick[tick], steady.HashAtTick[tick], "1x diverged at tick " + tick);
                Assert.AreEqual(reference.HashAtTick[tick], fast.HashAtTick[tick], "4x diverged at tick " + tick);
                Assert.AreEqual(reference.HashAtTick[tick], mixed.HashAtTick[tick], "mixed diverged at tick " + tick);
            }
        }

        [Test]
        public void SteadyStateTick_AllocatesNothing()
        {
            DeterminismTestWorld world = Build(5, out Simulation sim);
            sim.RunUntil(20);
            AllocationAssert.NoAllocation(() => sim.Step(), "Simulation.Step");
        }
    }
}
