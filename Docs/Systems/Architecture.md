# Architecture — folders and assembly boundaries

GDD reference: section 12.

## Layout

```text
Assets/_Project/
  Data/  Art/  Audio/  VFX/  Scenes/
  Scripts/
    Core/Sim/      IronHorizon.Core.Sim       deterministic kernel (integer only)
    Core/Infra/    IronHorizon.Core.Infra     EventBus, Pooling
    Combat/ Movement/ Economy/ Vision/ AI/    simulation systems
    Presentation/ UI/ Tools/                  view side
  Tests/EditMode/  IronHorizon.Tests.EditMode
  Tests/PlayMode/  IronHorizon.Tests.PlayMode
Tests.DotNet/      second test runner for engine-independent code (outside Unity)
```

## Rules enforced by `ArchitectureTests`

| Rule | Check |
| --- | --- |
| Core/Sim references only Unity.Mathematics, Unity.Collections, Unity.Burst | asmdef `references` |
| Simulation assemblies (Core/Sim, Combat, Movement, Economy, Vision, AI) never reference Presentation, UI, Tools, AI Navigation, Physics | asmdef `references` |
| No non-deterministic APIs in simulation code: UnityEngine.Random, System.Random, Time.*, Physics, Rigidbody, NavMesh, CharacterController, DateTime.Now, Stopwatch | source scan (comments ignored) |
| Core/Sim uses no floating point types (GDD 12.1) | source scan |

A violation fails the test with the file name and the rule.

## Two test runners

- **Unity:** `unity test` runs EditMode and PlayMode suites. This is the official gate.
- **dotnet:** `dotnet test Tests.DotNet` compiles Core/Sim, Core/Infra and the EditMode tests
  without Unity. It lets the agent verify kernel code on machines without the editor.
  Files that need UnityEngine are wrapped in `#if UNITY_5_3_OR_NEWER` so the dotnet runner skips them.
  The project pins C# 9 because that is what Unity compiles.

## Required packages

The Core assemblies reference `com.unity.mathematics`, `com.unity.collections` and `com.unity.burst`.
They must be listed in `Packages/manifest.json` (all three are named in GDD section 2).
