using System;

namespace IronHorizon.Core.Sim
{
    /// <summary>
    /// Holds future commands ordered by tick. Commands for the same tick keep the order they were
    /// enqueued in, so a replay that enqueues the same list reproduces the same order.
    /// A command for a tick that has already run is rejected: it could never execute on time.
    /// </summary>
    public sealed class CommandQueue
    {
        private SimCommand[] _items;
        private int _head;
        private int _count;

        public CommandQueue(int initialCapacity = 256)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Must be positive.");
            }

            _items = new SimCommand[initialCapacity];
        }

        public int Count => _count;

        /// <summary>The last tick whose commands have been taken. New commands must target a later tick.</summary>
        public int ClosedTick { get; private set; }

        public void Enqueue(in SimCommand command)
        {
            if (command.Tick <= ClosedTick)
            {
                throw new InvalidOperationException(
                    "Command for tick " + command.Tick + " arrived after tick " + ClosedTick + " already ran: " + command);
            }

            EnsureSpaceAtEnd();

            // Upper bound: insert after every command with the same or an earlier tick (stable order).
            int lo = _head;
            int hi = _head + _count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_items[mid].Tick <= command.Tick)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            int end = _head + _count;
            if (lo < end)
            {
                Array.Copy(_items, lo, _items, lo + 1, end - lo);
            }

            _items[lo] = command;
            _count++;
        }

        /// <summary>Takes the next command for <paramref name="tick"/>, in enqueue order.</summary>
        public bool TryDequeue(int tick, out SimCommand command)
        {
            if (tick <= ClosedTick)
            {
                throw new InvalidOperationException("Tick " + tick + " is already closed.");
            }

            if (_count > 0 && _items[_head].Tick == tick)
            {
                command = _items[_head];
                _items[_head] = default;
                _head++;
                _count--;
                if (_count == 0)
                {
                    _head = 0;
                }

                return true;
            }

            command = default;
            return false;
        }

        /// <summary>Marks <paramref name="tick"/> as executed. Fails if any of its commands were not taken.</summary>
        public void CloseTick(int tick)
        {
            if (tick <= ClosedTick)
            {
                throw new InvalidOperationException("Tick " + tick + " is already closed.");
            }

            if (_count > 0 && _items[_head].Tick <= tick)
            {
                throw new InvalidOperationException("Command was never executed: " + _items[_head]);
            }

            ClosedTick = tick;
        }

        private void EnsureSpaceAtEnd()
        {
            if (_head + _count < _items.Length)
            {
                return;
            }

            if (_head > 0)
            {
                Array.Copy(_items, _head, _items, 0, _count);
                Array.Clear(_items, _count, _head);
                _head = 0;
                return;
            }

            var bigger = new SimCommand[_items.Length * 2];
            Array.Copy(_items, bigger, _count);
            _items = bigger;
        }
    }
}
