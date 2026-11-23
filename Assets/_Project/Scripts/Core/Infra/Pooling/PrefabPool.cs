#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronHorizon.Core.Infra
{
    /// <summary>
    /// Pool of prefab instances for anything spawned during battle: projectiles' views, VFX
    /// (<c>PrefabPool&lt;VisualEffect&gt;</c>), sounds (<c>PrefabPool&lt;AudioSource&gt;</c>), decals.
    /// Instances live under one parent and are toggled with SetActive instead of Instantiate/Destroy.
    /// </summary>
    public sealed class PrefabPool<T> : IDisposable where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _root;
        private readonly ObjectPool<T> _pool;

        public PrefabPool(T prefab, int maxSize, int prewarm, Transform root)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            _prefab = prefab;
            _root = root;
            _pool = new ObjectPool<T>(Create, maxSize, OnGet, OnRelease);
            _pool.Prewarm(prewarm);
        }

        public int MaxSize => _pool.MaxSize;
        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;
        public int ColdCreations => _pool.ColdCreations;

        public bool TryGet(out T item)
        {
            return _pool.TryGet(out item);
        }

        public T Get()
        {
            return _pool.Get();
        }

        public void Release(T item)
        {
            _pool.Release(item);
        }

        /// <summary>Destroys the waiting instances. Instances still in use are left to their owners.</summary>
        public void Dispose()
        {
            while (_pool.CountInactive > 0)
            {
                _pool.TryGet(out T item);
                if (item == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(item.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(item.gameObject);
                }
            }
        }

        private T Create()
        {
            T item = Object.Instantiate(_prefab, _root);
            item.gameObject.SetActive(false);
            return item;
        }

        private static void OnGet(T item)
        {
            item.gameObject.SetActive(true);
        }

        private void OnRelease(T item)
        {
            item.gameObject.SetActive(false);
            if (item.transform.parent != _root)
            {
                item.transform.SetParent(_root, false);
            }
        }
    }
}
#endif
