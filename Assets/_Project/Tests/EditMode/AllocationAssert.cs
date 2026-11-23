using System;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
#endif

namespace IronHorizon.Tests
{
    /// <summary>Asserts that an action allocates no managed memory (GDD section 11: 0 bytes per frame in battle).</summary>
    internal static class AllocationAssert
    {
        public static void NoAllocation(Action action, string message)
        {
            action(); // warm-up: JIT and lazy init are not part of the steady state

#if UNITY_5_3_OR_NEWER
            Assert.That(() => action(), Is.Not.AllocatingGCMemory(), message);
#else
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, message + " (allocated " + allocated + " bytes)");
#endif
        }
    }
}
