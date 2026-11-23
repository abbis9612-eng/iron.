using System;

namespace IronHorizon.Core.Sim
{
    /// <summary>
    /// One player or AI order. Every change to the simulation enters as a command stamped with the tick
    /// it executes on (GDD section 0, rule 6). Fixed-size and integer-only so it can be stored in a
    /// replay and compared exactly. The meaning of the arguments depends on <see cref="Type"/>.
    /// </summary>
    public readonly struct SimCommand : IEquatable<SimCommand>
    {
        public readonly int Tick;
        public readonly ushort Type;
        public readonly byte Player;
        public readonly int Arg0;
        public readonly int Arg1;
        public readonly int Arg2;
        public readonly int Arg3;
        public readonly int Arg4;
        public readonly int Arg5;

        public SimCommand(int tick, ushort type, byte player,
            int arg0 = 0, int arg1 = 0, int arg2 = 0, int arg3 = 0, int arg4 = 0, int arg5 = 0)
        {
            Tick = tick;
            Type = type;
            Player = player;
            Arg0 = arg0;
            Arg1 = arg1;
            Arg2 = arg2;
            Arg3 = arg3;
            Arg4 = arg4;
            Arg5 = arg5;
        }

        public SimCommand WithTick(int tick)
        {
            return new SimCommand(tick, Type, Player, Arg0, Arg1, Arg2, Arg3, Arg4, Arg5);
        }

        public bool Equals(SimCommand other)
        {
            return Tick == other.Tick && Type == other.Type && Player == other.Player
                && Arg0 == other.Arg0 && Arg1 == other.Arg1 && Arg2 == other.Arg2
                && Arg3 == other.Arg3 && Arg4 == other.Arg4 && Arg5 == other.Arg5;
        }

        public override bool Equals(object obj)
        {
            return obj is SimCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Tick;
                h = (h * 397) ^ Type;
                h = (h * 397) ^ Player;
                h = (h * 397) ^ Arg0;
                h = (h * 397) ^ Arg1;
                h = (h * 397) ^ Arg2;
                h = (h * 397) ^ Arg3;
                h = (h * 397) ^ Arg4;
                h = (h * 397) ^ Arg5;
                return h;
            }
        }

        public override string ToString()
        {
            return "Cmd(tick " + Tick + ", type " + Type + ", player " + Player + ", args "
                + Arg0 + "," + Arg1 + "," + Arg2 + "," + Arg3 + "," + Arg4 + "," + Arg5 + ")";
        }
    }
}
