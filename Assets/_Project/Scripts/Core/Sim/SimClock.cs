using System;

namespace IronHorizon.Core.Sim
{
    /// <summary>
    /// Converts real time into a whole number of simulation ticks at a fixed 30 ticks per second.
    /// Integer only: real time arrives in microseconds and is accumulated exactly, so no time is lost
    /// to rounding. Speed and pause only change how many ticks run per real second; each tick is
    /// identical, so the outcome of a match never depends on speed or frame rate.
    /// </summary>
    public sealed class SimClock
    {
        public const int TicksPerSecond = 30;
        public const int DefaultMaxTicksPerAdvance = 16;
        public const long MicrosecondsPerSecond = 1000000;

        // Accumulator unit: real microseconds x speed value (speed x 2) x ticks per second.
        // One tick therefore costs 2 x 1,000,000 units.
        private const long UnitsPerTick = 2 * MicrosecondsPerSecond;

        private long _accumulator;

        public SimClock(int maxTicksPerAdvance = DefaultMaxTicksPerAdvance)
        {
            if (maxTicksPerAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance), "Must be positive.");
            }

            MaxTicksPerAdvance = maxTicksPerAdvance;
            Speed = SimSpeed.Normal;
        }

        public SimSpeed Speed { get; private set; }

        public bool IsPaused { get; private set; }

        /// <summary>Upper bound on ticks returned by one <see cref="Advance"/> call, so a long hitch cannot freeze the game.</summary>
        public int MaxTicksPerAdvance { get; }

        /// <summary>Ticks skipped because of <see cref="MaxTicksPerAdvance"/>. The game slows down; results are unaffected.</summary>
        public long DroppedTicks { get; private set; }

        /// <summary>Progress toward the next tick in thousandths, for smoothing the view between ticks.</summary>
        public int NextTickProgressPermille => (int)(_accumulator * 1000 / UnitsPerTick);

        public void SetSpeed(SimSpeed speed)
        {
            switch (speed)
            {
                case SimSpeed.Half:
                case SimSpeed.Normal:
                case SimSpeed.Double:
                case SimSpeed.Quadruple:
                    Speed = speed;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(speed), "Unknown speed " + (int)speed);
            }
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        /// <summary>
        /// Adds real elapsed time and returns how many ticks are due now.
        /// Values above one second are clamped so a debugger pause does not trigger a burst of ticks.
        /// </summary>
        public int Advance(long realMicroseconds)
        {
            if (realMicroseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(realMicroseconds), "Time cannot run backwards.");
            }

            if (IsPaused)
            {
                return 0;
            }

            if (realMicroseconds > MicrosecondsPerSecond)
            {
                realMicroseconds = MicrosecondsPerSecond;
            }

            _accumulator += realMicroseconds * (long)Speed * TicksPerSecond;
            long due = _accumulator / UnitsPerTick;
            _accumulator -= due * UnitsPerTick;

            if (due > MaxTicksPerAdvance)
            {
                DroppedTicks += due - MaxTicksPerAdvance;
                due = MaxTicksPerAdvance;
            }

            return (int)due;
        }
    }
}
