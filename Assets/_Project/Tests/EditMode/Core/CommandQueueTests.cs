using System;
using System.Collections.Generic;
using IronHorizon.Core.Sim;
using NUnit.Framework;

namespace IronHorizon.Tests.Core
{
    public class CommandQueueTests
    {
        private sealed class RecordingHandler : ICommandHandler
        {
            private readonly Simulation _sim;
            public readonly List<(int executedOnTick, SimCommand command)> Log = new List<(int, SimCommand)>();

            public RecordingHandler(Simulation sim)
            {
                _sim = sim;
            }

            public void Execute(in SimCommand command)
            {
                // Tick is still the previous tick while commands run; the running tick is Tick + 1.
                Log.Add((_sim.Tick + 1, command));
            }
        }

        [Test]
        public void Command_ExecutesOnItsExactTick()
        {
            var sim = new Simulation(seed: 1);
            var handler = new RecordingHandler(sim);
            sim.RegisterHandler(1, handler);

            sim.Schedule(new SimCommand(tick: 250, type: 1, player: 0, arg0: 7));
            sim.Schedule(new SimCommand(tick: 3, type: 1, player: 0, arg0: 8));
            sim.RunUntil(400);

            Assert.AreEqual(2, handler.Log.Count);
            Assert.AreEqual(3, handler.Log[0].executedOnTick);
            Assert.AreEqual(8, handler.Log[0].command.Arg0);
            Assert.AreEqual(250, handler.Log[1].executedOnTick);
            Assert.AreEqual(7, handler.Log[1].command.Arg0);
        }

        [Test]
        public void SameTickCommands_KeepEnqueueOrder()
        {
            var queue = new CommandQueue(initialCapacity: 2); // small, to exercise growth
            queue.Enqueue(new SimCommand(5, 1, 0, arg0: 1));
            queue.Enqueue(new SimCommand(2, 1, 0, arg0: 2));
            queue.Enqueue(new SimCommand(5, 1, 0, arg0: 3));
            queue.Enqueue(new SimCommand(2, 1, 0, arg0: 4));
            queue.Enqueue(new SimCommand(5, 1, 0, arg0: 5));

            var order = new List<int>();
            for (int tick = 1; tick <= 5; tick++)
            {
                while (queue.TryDequeue(tick, out SimCommand c))
                {
                    order.Add(c.Arg0);
                }

                queue.CloseTick(tick);
            }

            CollectionAssert.AreEqual(new[] { 2, 4, 1, 3, 5 }, order);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void CommandForPastTick_IsRejected()
        {
            var sim = new Simulation(seed: 1);
            sim.RunUntil(10);
            Assert.Throws<InvalidOperationException>(() => sim.Schedule(new SimCommand(10, 1, 0)));
            Assert.Throws<InvalidOperationException>(() => sim.Schedule(new SimCommand(3, 1, 0)));
            Assert.DoesNotThrow(() => sim.Schedule(new SimCommand(11, 1, 0)));
        }

        [Test]
        public void CommandWithoutHandler_FailsLoudly()
        {
            var sim = new Simulation(seed: 1);
            sim.Schedule(new SimCommand(1, 99, 0));
            Assert.Throws<InvalidOperationException>(() => sim.Step());
        }

        [Test]
        public void ManyCommands_AfterCompaction_StayOrdered()
        {
            var queue = new CommandQueue(initialCapacity: 4);
            int next = 1;
            for (int tick = 1; tick <= 200; tick++)
            {
                queue.Enqueue(new SimCommand(tick + 1, 1, 0, arg0: next++));
                queue.Enqueue(new SimCommand(tick + 3, 1, 0, arg0: next++));
                int previousTickSeen = int.MinValue;
                while (queue.TryDequeue(tick, out SimCommand c))
                {
                    Assert.AreEqual(tick, c.Tick);
                    Assert.Greater(c.Arg0, previousTickSeen);
                    previousTickSeen = c.Arg0;
                }

                queue.CloseTick(tick);
            }
        }
    }
}
