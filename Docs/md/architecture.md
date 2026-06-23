# BiomeLords — Code Architecture

See also: [systems.md](systems.md), [patches.md](patches.md)

## Namespace map

```
BiomeLords               Plugin.cs — entry point
BiomeLords.Config        LordConfig — BepInEx config wrappers
BiomeLords.Data          BiomeLordDef + LordRegistry — static design data
BiomeLords.Util          Shared utilities (TierTable, LordBaseStats, LordAttackProfile, intrinsic, registries, FX helpers)
BiomeLords.Phase1B       Creature + item + event + summon factories
BiomeLords.Phase1C       Trophy + pedestal + blessing + guardian power factories
BiomeLords.Phase1D       Brain MonoBehaviours + per-FP services + debug commands
BiomeLords.Patches       All Harmony patch classes
```

## Phase breakdown

| Phase | What was built |
|---|---|
| 1A | Kill tracking, config binding, LordRegistry |
| 1B | Creature prefabs, Lord's Horn item, world events, summon gating |
| 1C | Trophies, Lord's Pedestal, blessing SEs, guardian powers, power claim on kill |
| 1D | Brain AIs, power-effects services, advanced patches, debug tooling |

## Key class roles

### Entry point

**`Plugin.cs`**
- Binds config (`LordConfig.Bind`)
- Per-class Harmony patching in a try/catch loop — one bad target skips that class only,
  the rest of the mod still loads
- Subscribes to `PrefabManager.OnVanillaPrefabsAvailable` for factory orchestration
- Registration order is deterministic and matters (SEs before creatures, trophies before drops)

### Data layer

**`BiomeLordDef` / `LordRegistry`** (`Data/BiomeLordDef.cs`)
- Static definitions: id, display name, base prefab, biome, tier, kill requirement, kill targets
- `LordRegistry.All` is iterated by `LordConfig` to bind per-Lord config entries

### Factory layer (`Phase1B`, `Phase1C`)

Each factory's `RegisterAll()` is called once in `OnVanillaPrefabsAvailable`.

| Factory | Output |
|---|---|
| `CreatureFactory` | Cloned + tinted Lord prefabs with brain MBs attached |
| `ItemFactory` | Lord's Horn item + tooltip patches |
| `EventFactory` | World events (weather + music per biome) |
| `TrophyFactory` | 7 Lord trophy items |
| `PedestalFactory` | Lord's Pedestal buildable piece |
| `StatusEffectFactory` | 7 blessing SEs (`SE_*Spirit`) |
| `GuardianPowerFactory` | 7 Forsaken Powers (`GP_*`) |
| `SubEffectFactory` | Sub-SEs: `SE_ForestSitting` (Greydwarf Forest's Embrace) |

### Runtime systems (`Phase1D`, `Util`)

See [systems.md](systems.md) for implementation details.

| Class | Role |
|---|---|
| `TierTable` | Global tier **HP** lookup (tiers 1–7); HP target for vanilla bosses + ratio basis for Lords |
| `LordBaseStats` | Per-Lord base HP at native tier (identity value, matched to the biome boss) |
| `LordAttackProfile` | Per-Lord + per-tier convergence-target attack profiles (matched to each Lord's own vanilla creature, not a vanilla boss); `Resolve` (convergence) + `TierMagnitude` |
| `LordIntrinsic` | Per-Lord baked damage multiplier (all 1.0; live-tuning surface), runtime-editable |
| `LordDamageRegistry` | Per-instance damage **mult** keyed by `Character.GetInstanceID()` |
| `LordProfileRegistry` | Per-instance convergence-resolved attack profile (parallel to `LordDamageRegistry`) |
| `LordDefeatStore` | Persists which Lords have been killed; drives `HighestDefeatedTier()` for scaling |
| `RegisteredLords` | Runtime set of Lord prefab names + event/id mappings |
| `KillStore` | ZDO-persisted kill counters |
| `PowerEffectsService` | Marker-based FP tick (Tides Grace, Howl) |
| `HiveSightService` | Minimap pins for Hive Sight FP |
| `ValkyrieRallyService` | One-shot group restore burst (HP/Stamina/Eitr/Adrenaline/Shield/Rested) for Valkyrie's Rally FP, broadcast via routed RPC |
| `ForestEmbraceService` | Tree-healing ticks for Forest's Embrace FP |
| `BlessingSystem` | Pedestal → player SE grant logic; persists active blessing in `m_customData` |
| `PowerClaimSystem` | Lord kill → FP auto-grant |
| `FeatherweightInventory` | Featherweight blessing: raised carry cap + extra inventory rows + CargoCrate spill on switch |

### Brain MonoBehaviours

Each Lord has a `MonoBehaviour` attached to its prefab in `CreatureFactory`.
All brains follow the same pattern:

```
Awake()  → cache references, set initial cooldown timers
Update() → early-exit if !_nview.IsOwner() (network safety)
           → TryFrenzy / TrySummonMinions / Try[abilities] / TickFrenzyAura
```

Minion spawning uses `Math.Min(batchSize, maxNearby - aliveCount)` to cap the batch —
no over-spawning even if the timer fires while the cap is already hit.

### Harmony patches (`Patches/`)

See [patches.md](patches.md) for the complete list and gotchas.

Each patch class is processed individually in `Plugin.Awake`:
```csharp
new HarmonyLib.PatchClassProcessor(_harmony, t).Patch();
```
A thrown exception skips only that class. The next Plugin launch starts fresh,
so failed classes are retried automatically after a Valheim update.

## Startup flow (detailed)

```
Plugin.Awake()
  ├─ LordConfig.Bind(Config)              ← per-Lord kill/hp/dmg config entries created
  ├─ Harmony per-class patching (loop)    ← patches applied; failures logged, not fatal
  └─ PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable

Game loads vanilla prefabs
  └─ OnVanillaPrefabsAvailable()
       ├─ StatusEffectFactory.RegisterAll()   ← SE_*Spirit registered into ItemManager
       ├─ GuardianPowerFactory.RegisterAll()  ← GP_* registered into ItemManager
       ├─ SubEffectFactory.RegisterAll()      ← sub-SEs registered
       ├─ TrophyFactory.RegisterAll()         ← trophy items registered
       ├─ CreatureFactory.RegisterAll()       ← Lord prefabs + brains registered
       │    └─ RegisteredLords.Register()     ← runtime name/event/id mappings populated
       ├─ ItemFactory.RegisterAll()           ← Lord's Horn registered
       ├─ PedestalFactory.RegisterAll()       ← Pedestal piece registered
       ├─ DebugCommands.RegisterAll()         ← biomelords_intrinsic command registered
       └─ ObjectDB_Awake_BiomeLordsInject.InjectAll()  ← push SEs into ObjectDB
            (ObjectDB.Awake fires before this event, so SEs need manual injection)
```

## ObjectDB injection note

`ObjectDB.Awake` runs before `OnVanillaPrefabsAvailable`, meaning the postfix patch
(`ObjectDBSEInjectPatch`) fires with empty registries. After all factories run,
`InjectAll()` is called explicitly to push the now-populated SE lists into `ObjectDB`.
