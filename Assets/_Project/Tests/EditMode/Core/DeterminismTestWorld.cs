using IronHorizon.Core.Sim;

namespace IronHorizon.Tests.Core
{
    /// <summary>
    /// A tiny fake game used to prove determinism: units wander using SimRandom and obey two commands.
    /// Its hash covers every unit position plus the random generator state.
    /// </summary>
    internal sealed class DeterminismTestWorld : ISimSystem, ICommandHandler, ITickListener
    {
        public const ushort TeleportCommand = 1; // arg0 unit, arg1 x, arg2 y
        public const ushort FreezeCommand = 2;   // arg0 unit, arg1 ticks

        private readonly Simulation _sim;
        private readonly int[] _x;
        private readonly int[] _y;
        private readonly int[] _frozenUntil;

        public DeterminismTestWorld(Simulation sim, int unitCount, int maxTick)
        {
            _sim = sim;
            _x = new int[unitCount];
            _y = new int[unitCount];
            _frozenUntil = new int[unitCount];
            HashAtTick = new ulong[maxTick + 1];

            for (int i = 0; i < unitCount; i++)
            {
                _x[i] = sim.Random.Range(-1000, 1000);
                _y[i] = sim.Random.Range(-1000, 1000);
            }

            sim.AddSystem(this);
            sim.RegisterHandler(TeleportCommand, this);
            sim.RegisterHandler(FreezeCommand, this);
            sim.AddTickListener(this);
        }

        /// <summary>State hash recorded after each completed tick (index = tick).</summary>
        public ulong[] HashAtTick { get; }

        public void Step(int tick)
        {
            for (int i = 0; i < _x.Length; i++)
            {
                if (_frozenUntil[i] >= tick)
                {
                    continue;
                }

                _x[i] += _sim.Random.Range(-3, 4);
                _y[i] += _sim.Random.Range(-3, 4);
            }
        }

        public void Execute(in SimCommand command)
        {
            int unit = command.Arg0;
            if (command.Type == TeleportCommand)
            {
                _x[unit] = command.Arg1;
                _y[unit] = command.Arg2;
            }
            else
            {
                _frozenUntil[unit] = _sim.Tick + 1 + command.Arg1;
            }
        }

        public void OnTickCompleted(int tick)
        {
            if (tick < HashAtTick.Length)
            {
                HashAtTick[tick] = ComputeHash();
            }
        }

        public ulong ComputeHash()
        {
            unchecked
            {
                ulong h = 14695981039346656037UL; // FNV-1a 64
                for (int i = 0; i < _x.Length; i++)
                {
                    h = Mix(h, (uint)_x[i]);
                    h = Mix(h, (uint)_y[i]);
                    h = Mix(h, (uint)_frozenUntil[i]);
                }

                SimRandomState s = _sim.Random.State;
                h = Mix(h, s.S0);
                h = Mix(h, s.S1);
                h = Mix(h, s.S2);
                h = Mix(h, s.S3);
                return h;
            }
        }

        private static ulong Mix(ulong h, uint value)
        {
            unchecked
            {
                for (int b = 0; b < 4; b++)
                {
                    h ^= (byte)(value >> (8 * b));
                    h *= 1099511628211UL;
                }

                return h;
            }
        }
    }
}
