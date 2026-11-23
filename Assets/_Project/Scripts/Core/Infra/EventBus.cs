using System;
using System.Collections.Generic;
using IronHorizon.Core.Sim;

namespace IronHorizon.Core.Infra
{
    public delegate void SimEventHandler<T>(in T evt) where T : struct;

    /// <summary>
    /// One-way channel from the simulation to the view. Simulation systems publish struct events
    /// during a tick; subscribers receive them only after the tick has completed (register the bus
    /// with <see cref="Simulation.AddTickListener"/>). Subscribers must never change simulation state.
    ///
    /// Order: events of one type arrive in publish order. Different types are delivered type by type,
    /// in the order each type was first used. Events published while delivering wait for the next flush.
    /// After warm-up, publishing and flushing allocate no memory.
    /// </summary>
    public sealed class EventBus : ITickListener
    {
        private readonly List<IChannel> _channels = new List<IChannel>();
        private readonly Dictionary<Type, IChannel> _byType = new Dictionary<Type, IChannel>();
        private bool _flushing;

        public void Publish<T>(in T evt) where T : struct
        {
            GetChannel<T>().Publish(in evt);
        }

        public void Subscribe<T>(SimEventHandler<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            GetChannel<T>().Subscribe(handler);
        }

        public bool Unsubscribe<T>(SimEventHandler<T> handler) where T : struct
        {
            return GetChannel<T>().Unsubscribe(handler);
        }

        public int PendingCount<T>() where T : struct
        {
            return GetChannel<T>().PendingCount;
        }

        /// <summary>Delivers every pending event. Called automatically after each tick.</summary>
        public void Flush()
        {
            if (_flushing)
            {
                throw new InvalidOperationException("EventBus.Flush cannot be called from an event handler.");
            }

            _flushing = true;
            try
            {
                for (int i = 0; i < _channels.Count; i++)
                {
                    _channels[i].Deliver();
                }
            }
            finally
            {
                _flushing = false;
            }
        }

        void ITickListener.OnTickCompleted(int tick)
        {
            Flush();
        }

        private Channel<T> GetChannel<T>() where T : struct
        {
            if (_byType.TryGetValue(typeof(T), out IChannel existing))
            {
                return (Channel<T>)existing;
            }

            var channel = new Channel<T>();
            _byType.Add(typeof(T), channel);
            _channels.Add(channel);
            return channel;
        }

        private interface IChannel
        {
            void Deliver();
        }

        private sealed class Channel<T> : IChannel where T : struct
        {
            private List<T> _pending = new List<T>(16);
            private List<T> _delivering = new List<T>(16);
            private SimEventHandler<T>[] _handlers = Array.Empty<SimEventHandler<T>>();

            public int PendingCount => _pending.Count;

            public void Publish(in T evt)
            {
                _pending.Add(evt);
            }

            public void Subscribe(SimEventHandler<T> handler)
            {
                // Copy-on-write so changing subscriptions during delivery is safe.
                var next = new SimEventHandler<T>[_handlers.Length + 1];
                Array.Copy(_handlers, next, _handlers.Length);
                next[_handlers.Length] = handler;
                _handlers = next;
            }

            public bool Unsubscribe(SimEventHandler<T> handler)
            {
                int index = Array.IndexOf(_handlers, handler);
                if (index < 0)
                {
                    return false;
                }

                var next = new SimEventHandler<T>[_handlers.Length - 1];
                Array.Copy(_handlers, 0, next, 0, index);
                Array.Copy(_handlers, index + 1, next, index, _handlers.Length - index - 1);
                _handlers = next;
                return true;
            }

            public void Deliver()
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                List<T> batch = _pending;
                _pending = _delivering;
                _delivering = batch;

                SimEventHandler<T>[] handlers = _handlers;
                for (int i = 0; i < batch.Count; i++)
                {
                    T evt = batch[i];
                    for (int h = 0; h < handlers.Length; h++)
                    {
                        handlers[h](in evt);
                    }
                }

                batch.Clear();
            }
        }
    }
}
