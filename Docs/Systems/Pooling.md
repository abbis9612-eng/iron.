# Pooling

GDD reference: section 0 rule 9 (pooling is mandatory for everything spawned in battle),
section 11 (0 bytes of garbage per frame), section 10 (caps on effects).
Code: `Assets/_Project/Scripts/Core/Infra/Pooling`.

## Classes

| Class | Use |
| --- | --- |
| `ObjectPool<T>` | Any plain C# object. Engine-independent, also tested by `Tests.DotNet`. |
| `PrefabPool<T>` | Prefab instances of a component type: `PrefabPool<VisualEffect>` for effects, `PrefabPool<AudioSource>` for sounds, `PrefabPool<Transform>` for anything else. Instances sit under one parent and are switched with SetActive. |

## Behaviour

- **Warm-up:** `Prewarm(n)` creates objects before the battle. Get/Release after that allocate nothing.
- **Cap:** a pool never holds more than `MaxSize` objects. When all are in use, `TryGet` returns false;
  the caller decides what to drop (for example the oldest smoke column, GDD 10.1). `Get` throws instead.
- **Cold creations:** objects created on demand after warm-up are counted in `ColdCreations`.
  Above zero after a battle means the warm-up number is too low.
- **Safety:** releasing the same object twice, or an object from elsewhere, throws.
  Objects are tracked by identity, so Unity objects' custom equality does not confuse the pool.

## Example

```csharp
var explosions = new PrefabPool<VisualEffect>(explosionPrefab, maxSize: 20, prewarm: 20, root: vfxRoot);
if (explosions.TryGet(out VisualEffect fx))
{
    fx.transform.position = hitPoint;
    fx.Play();
}
// later, when the effect has finished:
explosions.Release(fx);
```

Warm-up sizes and caps per effect type come from data, not from code (GDD 10.1 table).

## Tests

| Test | Proves |
| --- | --- |
| `ObjectPoolTests.GetAndRelease_AfterPrewarm_AllocateNothing` | 100 gets and releases after warm-up allocate 0 bytes |
| `ObjectPoolTests.Cap_IsNeverExceeded` | The cap holds |
| `ObjectPoolTests.DoubleRelease_AndForeignObjects_AreRejected` | Misuse fails loudly |
| `PrefabPoolTests.Instances_AreToggledNotDestroyed_AndAllocateNothing` | Unity only: no instance created or destroyed, 0 bytes |

## Still needed in Unity

Rule 11 asks for a test scene per system. A pooling demo scene (spawn and recycle 500 effects with a
GC counter on screen) must be built through the editor, not as text (rule 16).
