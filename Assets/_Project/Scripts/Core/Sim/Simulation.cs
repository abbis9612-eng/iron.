using System;
using System.Collections.Generic;

namespace IronHorizon.Core.Sim
{
    /// <summary>
    /// The deterministic match loop. One tick = execute that tick's commands in order, step every
    /// system in registration order, then notify tick listeners (for example the EventBus).
    /// Same seed + same commands = same match, at any speed or frame rate (GDD 12.1).
    /// </summary>
    public sealed class Simulation
    {
        private readonly List<ISimSystem> _systems = new List<ISimSystem>();
        private readonly List<ITickListener> _listeners = new List<ITickListener>();
        private readonly Dictionary<ushort, ICommandHandler> _handlers = new Dictionary<ushort, ICommandHandler>();

        public Simulation(ulong seed, int maxTicksPerAdvance = SimClock.DefaultMaxTicksPerAdvance, int commandCapacity = 256)
        {
            Seed = seed;
            Random = new SimRandom(seed);
            Clock = new SimClock(maxTicksPerAdvance);
            Commands = new CommandQueue(commandCapacity);
        }

        public ulong Seed { get; }

        /// <summary>Last completed tick. 0 before the first tick; the first tick is 1.</summary>
        public int Tick { get; private set; }

        public SimClock Clock { get; }

        public CommandQueue Commands { get; }

        public SimRandom Random { get; }

        public void AddSystem(ISimSystem system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (Tick > 0)
            {
                throw new InvalidOperationException("Systems must be registered before the first tick.");
            }

            _systems.Add(system);
        }

        public void RegisterHandler(ushort commandType, ICommandHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handlers.ContainsKey(commandType))
            {
                throw new InvalidOperationException("Command type " + commandType + " already has a handler.");
            }

            _handlers.Add(commandType, handler);
        }

        public void AddTickListener(ITickListener listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            _listeners.Add(listener);
        }

        public bool RemoveTickListener(ITickListener listener)
        {
            return _listeners.Remove(listener);
        }

        /// <summary>Queues a command. Its tick must be later than <see cref="Tick"/>.</summary>
        public void Schedule(in SimCommand command)
        {
            Commands.Enqueue(command);
        }

        /// <summary>Feeds real time to the clock and runs every tick that is due. Returns ticks run.</summary>
        public int Update(long realMicroseconds)
        {
            int due = Clock.Advance(realMicroseconds);
            for (int i = 0; i < due; i++)
            {
                Step();
            }

            return due;
        }

        /// <summary>Runs exactly one tick, ignoring the clock. Used by tests, replays and headless matches.</summary>
        public void Step()
        {
            int tick = Tick + 1;

            while (Commands.TryDequeue(tick, out SimCommand command))
            {
                if (!_handlers.TryGetValue(command.Type, out ICommandHandler handler))
                {
                    throw new InvalidOperationException("No handler registered for " + command);
                }

                handler.Execute(in command);
            }

            Commands.CloseTick(tick);

            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Step(tick);
            }

            Tick = tick;

            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i].OnTickCompleted(tick);
            }
        }

        public void RunUntil(int tick)
        {
            while (Tick < tick)
            {
                Step();
            }
        }
    }
}
