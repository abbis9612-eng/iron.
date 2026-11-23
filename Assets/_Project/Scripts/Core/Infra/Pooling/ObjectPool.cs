using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace IronHorizon.Core.Infra
{
    /// <summary>
    /// Generic pool with warm-up and a hard cap (GDD section 0, rule 9). After <see cref="Prewarm"/>,
    /// taking and returning objects allocates no memory. When every object is in use, <see cref="TryGet"/>
    /// returns false instead of growing past <see cref="MaxSize"/>: the caller decides what to drop
    /// (for example the oldest smoke column, GDD 10.1).
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _inactiveSet;

        public ObjectPool(Func<T> create, int maxSize, Action<T> onGet = null, Action<T> onRelease = null)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Must be positive.");
            }

            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;
            MaxSize = maxSize;
            _inactive = new Stack<T>(maxSize);
            _inactiveSet = new HashSet<T>(ReferenceComparer.Instance);
        }

        public int MaxSize { get; }

        /// <summary>Objects created so far (in use + waiting).</summary>
        public int CountAll { get; private set; }

        public int CountInactive => _inactive.Count;

        public int CountActive => CountAll - _inactive.Count;

        /// <summary>Objects created on demand after warm-up. Above zero means the warm-up count is too low.</summary>
        public int ColdCreations { get; private set; }

        /// <summary>Creates objects until <paramref name="count"/> exist (never more than <see cref="MaxSize"/>).</summary>
        public void Prewarm(int count)
        {
            if (count > MaxSize)
            {
                count = MaxSize;
            }

            while (CountAll < count)
            {
                T item = CreateItem();
                _onRelease?.Invoke(item);
                _inactive.Push(item);
                _inactiveSet.Add(item);
            }
        }

        public bool TryGet(out T item)
        {
            if (_inactive.Count > 0)
            {
                item = _inactive.Pop();
                _inactiveSet.Remove(item);
            }
            else if (CountAll < MaxSize)
            {
                item = CreateItem();
                ColdCreations++;
            }
            else
            {
                item = null;
                return false;
            }

            _onGet?.Invoke(item);
            return true;
        }

        public T Get()
        {
            if (!TryGet(out T item))
            {
                throw new InvalidOperationException(
                    "Pool of " + typeof(T).Name + " is exhausted (" + MaxSize + " in use). Use TryGet to handle the cap.");
            }

            return item;
        }

        public void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (_inactiveSet.Contains(item))
            {
                throw new InvalidOperationException(typeof(T).Name + " was released twice.");
            }

            if (_inactive.Count >= CountAll)
            {
                throw new InvalidOperationException(typeof(T).Name + " does not belong to this pool.");
            }

            _onRelease?.Invoke(item);
            _inactive.Push(item);
            _inactiveSet.Add(item);
        }

        private T CreateItem()
        {
            T item = _create();
            if (item == null)
            {
                throw new InvalidOperationException("Pool factory for " + typeof(T).Name + " returned null.");
            }

            CountAll++;
            return item;
        }

        // Identity comparison: pooled objects may override Equals (Unity objects do).
        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
