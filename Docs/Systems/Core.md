# Core — the deterministic kernel

GDD reference: section 0 rule 6, section 12.1. Code: `Assets/_Project/Scripts/Core/Sim` and `Core/Infra/EventBus.cs`.

Same seed + same commands = the same match, tick for tick, at any game speed and any frame rate.

## Pieces

| Class | Job |
| --- | --- |
| `SimClock` | Turns real time (microseconds) into whole ticks at 30 per second. Speeds 0.5x, 1x, 2x, 4x and pause. Integer accumulator, so no time is lost to rounding. Caps ticks per frame (default 16) so a long hitch slows the game instead of freezing it; skipped ticks are counted in `DroppedTicks`. |
| `SimCommand` | One order: target tick, type, player, six integer arguments. Fixed size and integer-only so it can be saved in a replay and compared exactly. |
| `CommandQueue` | Future commands sorted by tick; same-tick commands keep enqueue order. Rejects a command for a tick that already ran. Fails loudly if a command was skipped. |
| `SimRandom` | xoshiro128** seeded by SplitMix64. The only randomness allowed in the simulation. Unbiased `NextInt`, `Range`, `Chance(n, d)`. State can be saved and restored. |
| `Simulation` | The match loop. |
| `EventBus` (Core/Infra) | One-way simulation → view messages, delivered after each tick. |

## One tick

1. Take every command for this tick, in order, and run its handler.
2. Step every system in registration order.
3. Mark the tick complete (`Simulation.Tick`).
4. Notify tick listeners. The EventBus is one: it delivers that tick's events to the view.

Tick numbering: `Simulation.Tick` is the last completed tick, 0 before the match starts. A command must
target `Tick + 1` or later. The input layer should stamp player commands with `Tick + 1`.

## Rules for code that uses the kernel

- No floats outside Burst jobs (GDD 12.1). Core/Sim is integer-only, and a test enforces it.
- No `UnityEngine.Random`, `System.Random`, `Time.*`, physics or NavMesh in simulation folders. A test enforces it.
- Systems are registered before the first tick. Their order is part of the result.
- View code may read events but must never change simulation state.
- Event types are structs. Order is guaranteed per type; across types, delivery goes type by type.

## Tests (EditMode, also run by `Tests.DotNet`)

| Test | Proves |
| --- | --- |
| `SimRandomTests.SameSeed_GivesSameSequence` | Same seed = same numbers (100,000 draws) |
| `SimRandomTests.MatchesReferenceImplementation` | Output equals an independent C implementation, so it cannot drift silently |
| `CommandQueueTests.Command_ExecutesOnItsExactTick` | A command runs on its tick exactly |
| `CommandQueueTests.SameTickCommands_KeepEnqueueOrder` | Stable order inside a tick |
| `SimulationDeterminismTests.GameSpeedAndFrameRate_DoNotChangeTheResult` | 200 units, 900 ticks: direct stepping, 1x at 60 fps, 4x with uneven frames, and mixed speeds with pauses all give the same hash on every tick |
| `SimulationDeterminismTests.SteadyStateTick_AllocatesNothing` | A tick allocates 0 bytes |
| `EventBusTests.*` | Delivery after the tick, publish order, no allocation |

## Known limits

- Commands carry six integers. Orders for many units at once (a selection of 40 tanks) need a
  bigger payload; to be decided before phase 1 (see OpenQuestions).
- Cross-machine determinism (Intel vs AMD) is tested in T7.
