#if UNITY_5_3_OR_NEWER
using IronHorizon.Core.Infra;
using NUnit.Framework;
using UnityEngine;

namespace IronHorizon.Tests.Infra
{
    public class PrefabPoolTests
    {
        private GameObject _prefab;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PooledPrefab");
            _root = new GameObject("PoolRoot");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void Instances_AreToggledNotDestroyed_AndAllocateNothing()
        {
            using (var pool = new PrefabPool<Transform>(_prefab.transform, maxSize: 16, prewarm: 16, root: _root.transform))
            {
                Assert.AreEqual(16, _root.transform.childCount);
                var held = new Transform[16];

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
                }, "PrefabPool Get/Release");

                Assert.AreEqual(16, _root.transform.childCount, "No instance was created or destroyed.");
                Assert.AreEqual(0, pool.ColdCreations);

                Transform t = pool.Get();
                Assert.IsTrue(t.gameObject.activeSelf);
                pool.Release(t);
                Assert.IsFalse(t.gameObject.activeSelf);
            }
        }
    }
}
#endif
