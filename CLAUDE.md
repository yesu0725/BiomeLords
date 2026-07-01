# BiomeLords — Project Overview

**Version:** 0.6.2  
**GUID:** `com.taeguk.BiomeLords`  
**Framework:** BepInEx + Jotunn + HarmonyLib  
**Valheim compatibility:** `EveryoneMustHaveMod`, `VersionStrictness.Minor`

## What the mod does

BiomeLords adds 7 boss-tier "Lord" variants — one per biome — using **only vanilla assets**
(no custom models, no asset bundles). Each Lord is a scaled, tinted clone of a vanilla
creature with a custom brain MonoBehaviour, tier-scaled stats, a unique world-event
summon via the **Lord's Horn** item, a trophy drop, a passive **Blessing** (from mounting
the trophy on a Lord's Pedestal), and a combat **Forsaken Power** (auto-granted on kill).

## Core design rules

- Vanilla-only assets — `PrefabManager.Instance.CreateClonedPrefab` for everything.
- Never duplicate a vanilla skill bonus or vanilla Forsaken Power effect.
- All config is admin-only and server-sync'd via Jotunn.
- Per-Lord balance is isolated: TierTable (global) × LordConfig multiplier × LordIntrinsic.

## Tech stack

| Library | Role |
|---|---|
| BepInEx 5 | Plugin host |
| Jotunn | Prefab/item/SE/creature registration, server config sync |
| HarmonyLib | Runtime method patching |
| ReportLab (Python) | PDF handbook generation (`Docs/generate_lord_handbook.py`) |

## Directory layout

```
BiomeLords/
├── Plugin.cs                   Entry point — Harmony patching + factory orchestration
├── Config/LordConfig.cs        Admin-only BepInEx config (all Lords)
├── Data/BiomeLordDef.cs        Static design data + LordRegistry (the 7 definitions)
├── Util/                       Shared runtime utilities
│   ├── TierTable.cs            Tier-based HP/damage multipliers
│   ├── LordIntrinsic.cs        Per-Lord baked damage overrides
│   ├── LordDamageRegistry.cs   Per-instance damage mult (keyed by instanceID)
│   ├── RegisteredLords.cs      Runtime set of Lord prefab names
│   ├── KillStore.cs            ZDO-persisted kill counters
│   ├── PlayerProgress.cs       Per-player progression helpers
│   ├── FxLibrary.cs            Vanilla VFX helper
│   ├── IconAssignment.cs       SE icon candidates per blessing
│   ├── SpriteTinter.cs         Material tint helper
│   ├── FeatherweightInventory.cs  Featherweight: carry cap + extra rows + CargoCrate spill
│   └── ConfigurationManagerAttributes.cs  Admin-only config attribute
├── Phase1B/                    Creatures, items, events, summons
│   ├── CreatureFactory.cs      Builds all 7 Lord prefabs
│   ├── ItemFactory.cs          Lord's Horn + tooltip patches
│   ├── EventFactory.cs         World event registration
│   ├── SummonService.cs        Horn-use → summon logic
│   └── LordFx.cs               Per-Lord spawn FX
├── Phase1C/                    Trophies, pedestals, blessings, guardian powers
│   ├── TrophyFactory.cs        7 Lord trophies
│   ├── PedestalFactory.cs      Lord's Pedestal piece
│   ├── StatusEffectFactory.cs  7 blessing SEs
│   ├── GuardianPowerFactory.cs 7 Forsaken Powers
│   ├── SubEffectFactory.cs     Sub-effects (Plunge, Sprite)
│   ├── BlessingSystem.cs       Pedestal → player blessing logic
│   ├── PowerClaimSystem.cs     Kill → FP grant
│   ├── LordsPedestalTag.cs     Pedestal MonoBehaviour
│   └── PowerCombatPatch.cs     FP combat effects
├── Phase1D/                    Brain MonoBehaviours + advanced services
│   ├── NeckLordBrain.cs        (in Phase1B/ — Neck is special)
│   ├── GreydwarfLordBrain.cs
│   ├── DraugrLordBrain.cs
│   ├── FenringLordBrain.cs
│   ├── LoxLordBrain.cs
│   ├── SeekerLordBrain.cs
│   ├── FallerValkyrieLordBrain.cs
│   ├── PowerEffectsService.cs  Marker-based FP tick logic
│   ├── HiveSightService.cs     Minimap pin service (Seeker FP)
│   ├── ValkyrieRallyService.cs Group restore burst for Valkyrie's Rally FP
│   ├── ForestEmbraceService.cs Greydwarf FP tree-healing
│   ├── HowlAura.cs             Visual aura MB for Howl of the Pack
│   ├── PhantomWolf.cs          Auto-despawn MB for phantom wolf
│   ├── PlagueCloud.cs          Draugr Lord plague cloud MB
│   └── DebugCommands.cs        biomelords_intrinsic console command
├── Patches/                    All Harmony patches
└── Docs/
    ├── generate_lord_handbook.py  ReportLab PDF generator
    ├── BiomeLords_Handbook.pdf    Generated handbook
    └── md/                        Detailed developer docs (this folder)
```

## Startup sequence

1. `Plugin.Awake` — bind config, iterate all assembly types for per-class Harmony patching
2. `PrefabManager.OnVanillaPrefabsAvailable` fires:
   - `StatusEffectFactory.RegisterAll()` — blessings (SE_*Spirit)
   - `GuardianPowerFactory.RegisterAll()` — Forsaken Powers (GP_*)
   - `SubEffectFactory.RegisterAll()` — sub-effects
   - `TrophyFactory.RegisterAll()` — trophies
   - `CreatureFactory.RegisterAll()` — Lord prefabs
   - `ItemFactory.RegisterAll()` — Lord's Horn
   - `PedestalFactory.RegisterAll()` — Lord's Pedestal piece
   - `DebugCommands.RegisterAll()` — console commands
   - `ObjectDB_Awake_BiomeLordsInject.InjectAll()` — push SEs into ObjectDB

## Build & deploy

```powershell
# Build
cd "E:\Valheim Modding\ValheimBiomeLords\Github\BiomeLords"
dotnet build -c Release

# Deploy (auto-copies on each build via post-build in .csproj, or run manually)
$src  = "bin\Release\netstandard2.1\BiomeLords.dll"
$dest = "C:\Users\yesu0725\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\Mod Test Profile\BepInEx\plugins\BiomeLords\BiomeLords.dll"
Copy-Item $src $dest -Force

# Regenerate PDF handbook
python Docs/generate_lord_handbook.py
```

## Sub-documents

| File | Contents |
|---|---|
| [Docs/md/lords.md](Docs/md/lords.md) | All 7 Lords — stats, abilities, FP, blessing, drops |
| [Docs/md/architecture.md](Docs/md/architecture.md) | Namespace layout, class roles, startup flow |
| [Docs/md/systems.md](Docs/md/systems.md) | Runtime systems: TierTable, LordIntrinsic, registries, services |
| [Docs/md/patches.md](Docs/md/patches.md) | Every Harmony patch and known gotchas |
| [Docs/md/development.md](Docs/md/development.md) | Build, deploy, testing checklist, admin commands |
| [Docs/md/design-rules.md](Docs/md/design-rules.md) | Hard constraints and design philosophy |
