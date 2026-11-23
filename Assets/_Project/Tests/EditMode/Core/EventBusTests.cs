using System;
using System.Collections.Generic;
using IronHorizon.Core.Infra;
using IronHorizon.Core.Sim;
using NUnit.Framework;

namespace IronHorizon.Tests.Core
{
    public class EventBusTests
    {
        private struct ShotFired
        {
            public int Tick;
            public int Shooter;
        }

        private struct UnitDestroyed
        {
            public int Unit;
        }

        private sealed class Shooter : ISimSystem
        {
            private readonly EventBus _bus;
            public bool SawDeliveryDuringTick;
            public int Delivered;

            public Shooter(EventBus bus)
            {
                _bus = bus;
            }

            public void Step(int tick)
            {
                int before = Delivered;
                _bus.Publish(new ShotFired { Tick = tick, Shooter = 1 });
                _bus.Publish(new ShotFired { Tick = tick, Shooter = 2 });
                SawDeliveryDuringTick |= Delivered != before;
            }
        }

        [Test]
        public void Events_AreDeliveredAfterTheTick_InPublishOrder()
        {
            var sim = new Simulation(seed: 1);
            var bus = new EventBus();
            var shooter = new Shooter(bus);
            sim.AddSystem(shooter);
            sim.AddTickListener(bus);

            var received = new List<(int tick, int shooter, int simTickAtDelivery)>();
            bus.Subscribe((in ShotFired e) =>
            {
                shooter.Delivered++;
                received.Add((e.Tick, e.Shooter, sim.Tick));
            });

            sim.RunUntil(3);

            Assert.IsFalse(shooter.SawDeliveryDuringTick, "Events must not reach the view mid-tick.");
            Assert.AreEqual(6, received.Count);
            for (int i = 0; i < received.Count; i++)
            {
                int tick = i / 2 + 1;
                Assert.AreEqual(tick, received[i].tick);
                Assert.AreEqual(i % 2 + 1, received[i].shooter);
                Assert.AreEqual(tick, received[i].simTickAtDelivery, "Delivered after its own tick completed.");
            }
        }

        [Test]
        public void EventPublishedByHandler_WaitsForNextFlush()
        {
            var bus = new EventBus();
            int destroyed = 0;
            bus.Subscribe((in ShotFired e) => bus.Publish(new UnitDestroyed { Unit = e.Shooter }));
            bus.Subscribe((in UnitDestroyed e) => destroyed++);

            bus.Publish(new ShotFired { Shooter = 4 });
            bus.Flush();
            Assert.AreEqual(1, destroyed, "UnitDestroyed channel is flushed after ShotFired in the same pass.");

            bus.Subscribe((in UnitDestroyed e) => bus.Publish(new UnitDestroyed { Unit = e.Unit + 1 }));
            bus.Publish(new UnitDestroyed { Unit = 1 });
            bus.Flush();
            Assert.AreEqual(1, bus.PendingCount<UnitDestroyed>(), "Re-published event waits for the next flush.");
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            SimEventHandler<ShotFired> handler = (in ShotFired e) => count++;
            bus.Subscribe(handler);
            bus.Publish(new ShotFired());
            bus.Flush();
            Assert.IsTrue(bus.Unsubscribe(handler));
            bus.Publish(new ShotFired());
            bus.Flush();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void FlushFromHandler_Throws()
        {
            var bus = new EventBus();
            bus.Subscribe((in ShotFired e) => bus.Flush());
            bus.Publish(new ShotFired());
            Assert.Throws<InvalidOperationException>(() => bus.Flush());
        }

        [Test]
        public void PublishAndFlush_AllocateNothingAfterWarmUp()
        {
            var bus = new EventBus();
            int sum = 0;
            bus.Subscribe((in ShotFired e) => sum += e.Shooter);

            AllocationAssert.NoAllocation(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    bus.Publish(new ShotFired { Shooter = i });
                }

                bus.Flush();
            }, "EventBus publish + flush");
        }
    }
}
