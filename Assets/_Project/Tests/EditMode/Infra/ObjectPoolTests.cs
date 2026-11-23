using System;
using System.Collections.Generic;
using IronHorizon.Core.Infra;
using NUnit.Framework;

namespace IronHorizon.Tests.Infra
{
    public class ObjectPoolTests
    {
        private sealed class Shell
        {
            public bool Active;
        }

        private static ObjectPool<Shell> NewPool(int max)
        {
            return new ObjectPool<Shell>(() => new Shell(), max, s => s.Active = true, s => s.Active = false);
        }

        [Test]
        public void Prewarm_CreatesObjectsUpFront()
        {
            ObjectPool<Shell> pool = NewPool(50);
            pool.Prewarm(20);
            Assert.AreEqual(20, pool.CountAll);
            Assert.AreEqual(20, pool.CountInactive);
            Assert.AreEqual(0, pool.CountActive);

            pool.Prewarm(500);
            Assert.AreEqual(50, pool.CountAll, "Warm-up never exceeds the cap.");
        }

        [Test]
        public void GetAndRelease_AfterPrewarm_AllocateNothing()
        {
            ObjectPool<Shell> pool = NewPool(100);
            pool.Prewarm(100);
            var held = new Shell[100];

            AllocationAssert.NoAllocation(() =>
            {
                for (int i = 0; i < held.Length; i++)
                {
                    held[i] = pool.Get();
                }

                for (int i = 0; i < held.Length; i++)
                {
                    pool.Release(held[i]);
                }
            }, "ObjectPool Get/Release");

            Assert.AreEqual(0, pool.ColdCreations);
        }

        [Test]
        public void Cap_IsNeverExceeded()
        {
            ObjectPool<Shell> pool = NewPool(3);
            pool.Get();
            pool.Get();
            pool.Get();
            Assert.IsFalse(pool.TryGet(out Shell none));
            Assert.IsNull(none);
            Assert.Throws<InvalidOperationException>(() => pool.Get());
            Assert.AreEqual(3, pool.CountAll);
            Assert.AreEqual(3, pool.ColdCreations, "Created on demand without warm-up.");
        }

        [Test]
        public void Callbacks_RunOnGetAndRelease()
        {
            ObjectPool<Shell> pool = NewPool(2);
            Shell s = pool.Get();
            Assert.IsTrue(s.Active);
            pool.Release(s);
            Assert.IsFalse(s.Active);
            Assert.AreSame(s, pool.Get(), "Released objects are reused.");
        }

        [Test]
        public void DoubleRelease_AndForeignObjects_AreRejected()
        {
            ObjectPool<Shell> pool = NewPool(4);
            Shell s = pool.Get();
            pool.Release(s);
            Assert.Throws<InvalidOperationException>(() => pool.Release(s));
            Assert.Throws<InvalidOperationException>(() => pool.Release(new Shell()));
            Assert.Throws<ArgumentNullException>(() => pool.Release(null));
        }

        [Test]
        public void ObjectsWithCustomEquality_AreTrackedByIdentity()
        {
            var pool = new ObjectPool<AlwaysEqual>(() => new AlwaysEqual(), 4);
            var items = new List<AlwaysEqual> { pool.Get(), pool.Get(), pool.Get() };
            foreach (AlwaysEqual item in items)
            {
                Assert.DoesNotThrow(() => pool.Release(item));
            }
        }

        private sealed class AlwaysEqual
        {
            public override bool Equals(object obj)
            {
                return obj is AlwaysEqual;
            }

            public override int GetHashCode()
            {
                return 1;
            }
        }
    }
}
