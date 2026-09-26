# BiomeLords — Project Overview

**Version:** 0.6.15  
**GUID:** `com.taeguk.BiomeLords`  
**Framework:** BepInEx + Jotunn + HarmonyLib  
**Valheim compatibility:** `EveryoneMustHaveMod`, `VersionStrictness.Minor`

## What the mod does

BiomeLords adds 8 boss-tier "Lord" variants — one per biome, Meadows through the Deep North — using **only vanilla assets**
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
├── Data/BiomeLordDef.cs        Static design data + LordRegistry (the 8 definitions)
├── Util/                       Shared runtime utilities
│   ├── TierTable.cs            Tier-based HP/damage multipliers
│   ├── LordIntrinsic.cs        Per-Lord baked damage overrides
│   ├── LordDamageRegistry.cs   Per-instance damage mult (keyed by instanceID)
│   ├── RegisteredLords.cs      Runtime set of Lord prefab names
│   ├── KillStore.cs            Per-character kill counters (Player.m_customData)
│   ├── LordDefeatStore.cs      Lord defeats → scaling tier (player unique keys, or world global keys if GlobalLordDefeats)
│   ├── FxLibrary.cs            Vanilla VFX helper
│   ├── IconAssignment.cs       SE icon candidates per blessing
│   ├── SpriteTinter.cs         Material tint helper
│   ├── FeatherweightInventory.cs  Featherweight: carry cap + extra rows + CargoCrate spill
│   ├── StorageWindowPosition.cs   Chest/storage window placement + click-and-drag
│   └── ConfigurationManagerAttributes.cs  Admin-only config attribute
├── Phase1B/                    Creatures, items, events, summons
│   ├── CreatureFactory.cs      Builds all 8 Lord prefabs + NeckLordMinion
│   ├── ItemFactory.cs          Lord's Horn + tooltip patches
│   ├── EventFactory.cs         World event registration
│   ├── SummonService.cs        Horn-use → summon logic
│   └── LordFx.cs               Per-Lord spawn FX
├── Phase1C/                    Trophies, pedestals, blessings, guardian powers
│   ├── TrophyFactory.cs        8 Lord trophies
│   ├── PedestalFactory.cs      Lord's Pedestal piece
│   ├── StatusEffectFactory.cs  8 blessing SEs
│   ├── GuardianPowerFactory.cs 8 Forsaken Powers
│   ├── SubEffectFactory.cs     Sub-effects (ForestSit, PetrifiedSkin)
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
│   ├── GammeltrollLordBrain.cs  Deep North (Valheim 1.0) — Petrify / Shatter / Fimbul Fury
│   ├── PowerEffectsService.cs  Marker-based FP tick logic
│   ├── HiveSightService.cs     Minimap pin service (Seeker FP)
│   ├── ValkyrieRallyService.cs Group restore burst for Valkyrie's Rally FP
│   ├── ForestEmbraceService.cs Greydwarf blessing tree-rest healing (Forest's Embrace)
│   ├── RootwardService.cs      Greydwarf FP — heal + summon protective TentaRoots
│   ├── PetrifyService.cs       Gammeltroll FP — stone skin then frost shatter
│   ├── FimbulHideService.cs    Gammeltroll blessing — no deep-snow slow + snow shedding
│   ├── HowlAura.cs             Visual aura MB for Howl of the Pack
│   ├── PhantomWolf.cs          Auto-despawn MB for phantom wolf
│   ├── PlagueCloud.cs          Draugr Lord plague cloud MB
│   └── DebugCommands.cs        biomelords_intrinsic console command
├── Patches/                    All Harmony patches
└── Docs/
    ├── generate_lord_handbook.py  ReportLab PDF generator
    ├── BiomeLords_Handbook.pdf    Generated handbook
    ├── tools/verify_patch_targets.py  Offline Harmony-target/param check vs a decompiled assembly_valheim — run after every Valheim update
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

> **Reference assemblies.** Valheim 1.0 ships no BepInEx inside the Steam install, so the
> csproj takes `assembly_valheim` / UnityEngine from `$(ValheimPath)` but BepInEx, 0Harmony
> and Jotunn from `$(GaleProfilePath)` — the Gale "TG Mods Only" profile. Point
> `GaleProfilePath` at whichever profile holds the Jotunn version you build against.

```powershell
# Build
cd "E:\Valheim Modding\ValheimBiomeLords\Github\BiomeLords"
dotnet build -c Release

# Deploy — automatic on every build via the CopyToPlugins target in .csproj,
# to exactly two places:
#   %APPDATA%\com.kesomannen.gale\valheim\profiles\HB Test\BepInEx\plugins\TaegukGaming-BiomeLords
#   C:\Program Files (x86)\Steam\steamapps\common\Valheim dedicated modded test server\BepInEx\plugins\TaegukGaming-BiomeLords
# No other profile (TG Mods Only, r2modman) receives builds — copy by hand if needed:
$src  = "bin\Release\netstandard2.1\BiomeLords.dll"
$dest = "C:\Users\yesu0725\AppData\Roaming\com.kesomannen.gale\valheim\profiles\HB Test\BepInEx\plugins\TaegukGaming-BiomeLords\BiomeLords.dll"
Copy-Item $src $dest -Force

# Regenerate PDF handbook
python Docs/generate_lord_handbook.py
```

## Sub-documents

| File | Contents |
|---|---|
| [Docs/md/lords.md](Docs/md/lords.md) | All 8 Lords — stats, abilities, FP, blessing, drops |
| [Docs/md/architecture.md](Docs/md/architecture.md) | Namespace layout, class roles, startup flow |
| [Docs/md/systems.md](Docs/md/systems.md) | Runtime systems: TierTable, LordIntrinsic, registries, services |
| [Docs/md/patches.md](Docs/md/patches.md) | Every Harmony patch and known gotchas |
| [Docs/md/development.md](Docs/md/development.md) | Build, deploy, testing checklist, admin commands |
| [Docs/md/design-rules.md](Docs/md/design-rules.md) | Hard constraints and design philosophy |
