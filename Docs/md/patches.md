# BiomeLords — Harmony Patches

All patches live in `Patches/`. They are applied per-class in `Plugin.Awake`:
```csharp
new HarmonyLib.PatchClassProcessor(_harmony, t).Patch();
```
A thrown exception skips only that class — the rest of the mod loads normally.

See also: [systems.md](systems.md), [architecture.md](architecture.md)

---

## Kill tracking

### `KillTrackerPatch` (`Patches/KillTrackerPatch.cs`)

**Target:** `Character.Damage` prefix + postfix  
**What it does:**
- Prefix captures whether the target was alive before the hit.
- Postfix, if the target just died, attributes the kill to the HitData attacker.
- For regular kill-target creatures: increments `KillStore` for the relevant Lord.
- For Lord deaths (`RegisteredLords.IsLord`): calls `OnLordDeath` which —
  1. Ends the tied world event via `RandEventSystem.ResetRandomEvent()`
  2. Adds `PowerClaimSystem.DefeatKey(def.Id)` as a player unique key (FP claim token)
  3. Calls `LordDefeatStore.RecordDefeat(def.Id)` to persist the defeat for scaling
  4. Resets the Lord's kill counters via `KillStore`
  5. Auto-grants the Lord's Forsaken Power via `PowerClaimSystem.GrantOnDefeat`
  6. Plays the per-Lord themed death VFX

**Gated by:** `LordConfig.EnableKillTracking`

---

## Lord combat & boss scaling

### `LordDamageBoostPatch` (`Patches/LordDamageBoostPatch.cs`)

**Target:** `Character.Damage` prefix  
**What it does:** Three paths, evaluated in order:

1. **Greydwarf Lord ranged poison orb guard** — When `lordId == "greydwarf_lord"` and
   `hit.m_damage.m_poison > 0 && hit.m_damage.m_blunt == 0` (the vanilla ranged orb
   arrives with only poison set), rewrites the hit as pure 36 poison (`poisonMag × mult`;
   36 = vanilla Greydwarf Shaman poison spit, 30, ×1.2), clears all other fields, and
   returns early. The `blunt == 0` guard ensures this path
   only runs for the ranged orb — after Poison Nova fires, the melee profile carries both
   blunt AND poison, so the standard profile path handles post-nova melee correctly.

2. **Lord** (has a resolved profile in `LordProfileRegistry`, or falls back to its own
   `LordAttackProfile`): **overwrites** the hit's combat damage with `profile × mult`
   (`mult` = `LordDamageRegistry.Get`, default 1.0). chop/pickaxe are left as the native
   weapon's values; generic `m_damage` is zeroed.

3. **Vanilla boss** (registered in `LordDamageRegistry` but no profile): legacy path —
   multiplies all native damage fields by `mult` (the magnitude ratio), preserving the
   boss's own damage/elemental types.

All paths also set `m_pushForce ×= 1.5` and `m_backstabBonus = 1` (ignore backstab cheese).  
**Guard:** Runs only when `RegisteredLords.IsLord(attacker)` OR `LordDamageRegistry.Has(attacker)`.  
**Note:** Runs before TamedWolfPatch — no collision since they guard on different conditions.  
**Gotcha — heal aoe:** The vanilla `Greydwarf_Shaman` has a `greydwarf_shaman_heal` attack item whose AoE fires through `Character.Damage` with the Lord as attacker, causing this patch to convert it to 60 blunt and hit everything in range. Fix: `CreatureFactory.BuildGreydwarfShamanLord` strips the heal item from `m_defaultItems` before cloning and replays its animation + VFX manually via `GreydwarfLordBrain.HealRoutine` (with all `Aoe`/`Projectile` components stripped from spawned effects).

### `LordAutoRegisterPatch` — `Character_Awake_LordAutoRegister` (`Patches/LordAutoRegisterPatch.cs`)

**Target:** `Character.Awake` postfix  
**What it does:** For any Lord that wakes without going through `SummonService.ApplyScaling`
(cheat-spawn, save+reload mid-fight), bakes its convergence-resolved profile into
`LordProfileRegistry` and its `config × intrinsic` mult into `LordDamageRegistry` — using
the same `LordAttackProfile.Resolve` + `LordDefeatStore` progression source as the Horn
spawn path, so every spawn path converges identically. Skips if already registered.

### `VanillaBossScalingPatch` (`Patches/VanillaBossScalingPatch.cs`)

**Target:** `Character.Start` postfix  
**What it does:** On any character with `m_boss == true` whose prefab name matches a
known Forsaken boss, checks `LordDefeatStore.HighestDefeatedTier()`. If the effective
tier exceeds the boss's native tier:
- Sets `Humanoid.SetMaxHealth` / `SetHealth` to `TierTable.HpFor(effectiveTier)`
- Registers a magnitude-ratio damage multiplier in `LordDamageRegistry` (keeps the boss's
  own damage/elemental types; only the values increase):
  ```
  dmgMult = LordAttackProfile.TierMagnitude(effectiveTier)
          / LordAttackProfile.TierMagnitude(nativeTier)
  ```

**Native tier table:**
| Prefab | Tier |
|--------|------|
| `Eikthyr` | 1 |
| `gd_king` | 2 |
| `Bonemass` | 3 |
| `Dragon` | 4 |
| `GoblinKing` | 5 |
| `SeekerQueen` | 6 |
| `FallenValkyrie` | 7 |

**No-op when clean:** If no Lords have been killed, `HighestDefeatedTier()` returns 0,
`effectiveTier == nativeTier`, and the patch returns early — vanilla bosses are
completely unmodified until the first Lord is killed.

### `NeckLordBlockPatch` (`Patches/NeckLordBlockPatch.cs`)

**Target:** `Character.Damage` prefix  
**What it does:** While the Neck Lord's `NeckLordBrain.IsBlocking` is `true`, cancels any
blockable hit (`hit.m_blockable == true`) directed at the Neck Lord (returns `false` to
skip `Character.Damage`). On cancel, spawns `fx_GoblinShieldHit` at the hit point.  
**Gated by:** `brain.IsBlocking` — active only below 50% HP when the reactive block window
is open (player attacking within 6 m). Window lasts 2.5 s; 12 s inter-cooldown after it closes.

### `LoxLordShieldPatch` (`Patches/LoxLordShieldPatch.cs`)

**Target:** `Character.Damage` prefix  
**What it does:** Mitigates (does not cancel) incoming damage while either of the Lox
Lord's defensive windows is active — reads `LoxLordBrain.IsLastStand` first (Unyielding
Bulwark, 80% reduction), then `IsShielded` (Bone Bulwark, 65% reduction). Unlike
`NeckLordBlockPatch`'s full cancel, this multiplies every damage field (blunt through
spirit) by `1 - reduction`, blockable or not, then lets the reduced hit through.  
**Gated by:** `LoxLordBrain.IsShielded` (reactive, ~18 s cooldown, pops for 2.5 s when a
player is within 5 m) or `IsLastStand` (one-time at ≤40% HP, 5 s root).

### `LordStaggerPatch` (`Patches/LordStaggerPatch.cs`)

**Target:** `Character.AddStagger` prefix  
**What it does:** Cancels stagger on any Lord (returns false). Lords are immune to stagger
by design — their boss bar and large HP pool already make them a sustained fight.

### `PowerCombatPatch` — `Character_Damage_TidesGraceMelee` (`Patches/PowerCombatPatch.cs`)

**Target:** `Character.Damage` prefix  
**What it does:** Tide's Grace (Neck Lord). When the attacker is the local player swinging a
melee skill (Swords/Knives/Clubs/Polearms/Spears/Axes/Unarmed) while carrying `GP_TidesGrace`
**and** the `Wet` status (`SEMan.s_statusEffectWet`), scales the hit's blunt/slash/pierce ×1.5 (+50%).

### `NeckWetImmunityPatch` (`Patches/NeckWetImmunityPatch.cs`)

**Target:** `SE_Stats.ModifyDamageMods` and `SE_Stats.ModifyStaminaRegen` prefixes (nested patch classes)  
**What it does:** Tide's Grace. Skips the `Wet` effect's damage-modifier and stamina-regen
contributions for the local player while `GP_TidesGrace` is active. Gated on `__instance is SE_Wet`
+ `se.m_character == Player.m_localPlayer`. The player still counts as `Wet` (so the +50% melee
bonus fires) but suffers none of Wet's debuffs.

### `PhantomWolfInvulnPatch` (`Patches/PhantomWolfInvulnPatch.cs`)

**Target:** `Character.Damage` prefix (returns `bool`)  
**What it does:** Howl of the Pack synergy. If the defender carries a `PhantomWolf` component
marked `Invulnerable`, swallows the hit entirely (returns `false`, plays a spark FX) so the
synergy phantom wolves ignore all incoming damage.

---

## Tamed wolf patches

### `TamedWolfPatch` — `Character_Damage_PackWhisperer` (`Patches/TamedWolfPatch.cs`)

**Target:** `Character.Damage` prefix  
**What it does:**
- If player has Pack Whisperer (`SE_FenringLordSpirit`) and the defender is a tamed wolf
  within 30 m: scales all hit damage by `WhispererDmgTakenMult = 0.50` (−50%)
- If player also has Howl of the Pack (`GP_HowlOfThePack`): scales by `SynergyDmgTakenMult = 0.15` (−85%)
- If player has Howl only and the **attacker** is a tamed wolf within 30 m: scales by `HowlDmgDealtMult = 2.00` (+100%)

**Constants:**
```csharp
WhispererDmgTakenMult = 0.50f  // blessing alone
SynergyDmgTakenMult   = 0.15f  // blessing + Howl active together
HowlDmgDealtMult      = 2.00f  // Howl only — outgoing boost
```

### `PackBreedingPatch` (`Patches/PackBreedingPatch.cs`)

Contains two patch classes that both key off Pack Whisperer (`SE_FenringLordSpirit`):

**`Procreation_Procreate_PackBreed`**
- **Target:** `Procreation.Procreate` (parameterless) via `[HarmonyTargetMethod]`
- **What it does:** While the player has Pack Whisperer active, re-invokes the procreate method
  once immediately after the original — effectively 2× breeding rate.
- **Gotcha:** `UpdateProcreation(float dt)` does not exist in the installed Valheim build.
  Using `[HarmonyTargetMethod]` to select the correct parameterless overload is required.
- **Re-entry guard:** Thread-static bool prevents the second call from triggering a third.

**`Tameable_DecreaseRemainingTime_PackWhisperer`**
- **Target:** `Tameable.DecreaseRemainingTime(float time)` (private) via `[HarmonyTargetMethod]` + `AccessTools.Method`
- **What it does:** While the local player has Pack Whisperer active within 30 m and the
  creature is not yet tamed, re-invokes `DecreaseRemainingTime` with the same `time` value
  immediately after the original — effectively doubling taming speed.
- **Re-entry guard:** Thread-static bool, same pattern as the procreate patch above.
- **Verification:** use `biomelords_tame_time [radius]` (see [development.md](development.md))
  to confirm remaining tame time drops twice as fast with the blessing active.

### `FenringVampPatch` — `Character_Damage_FenringVamp` (`Patches/FenringVampPatch.cs`)

**Target:** `Character.Damage` postfix  
**What it does:** When the attacker is a Fenring Lord with an active Vampiric Strike buff
(`FenringLordBrain.IsVampActive`), heals it for `hit.GetTotalDamage() * VampHealFraction`
(80%). Runs as a postfix so `hit.GetTotalDamage()` reflects damage after armor reduction;
fully-blocked hits (0 total damage) heal nothing.  
**Gotcha:** `VampHealFraction` is a public const on `FenringLordBrain` — keep the two files
in sync if the absorb rate changes.

---

## Blessing effect patches

### `FisherBoonPatch` (`Patches/FisherBoonPatch.cs`)

**Targets:** `FishingFloat.Setup` postfix + `ItemDrop.Pickup` prefix  
**What it does:**
- `Setup` postfix: 50% chance to not consume bait if player has `SE_NeckLordSpirit`
- `Pickup` prefix: 25% chance to yield a bonus fish if player has `SE_NeckLordSpirit`

### `HearthMasterPatch` (`Patches/HearthMasterPatch.cs`)

**Target:** `Player.EatFood` postfix  
**What it does:** Finds the just-eaten food entry by `m_shared` reference,
multiplies `f.m_time` (duration) by `LordConfig.HearthMasterMultiplier` (default 2.0,
i.e. +100% longer).  
Only fires if the player has `SE_LoxLordSpirit` active.

### `RefinersTouchPatch` (`Patches/RefinersTouchPatch.cs`)

**Target:** `Smelter.Spawn(string ore, int stack)` postfix  
**What it does:** `RefinersTouchChance` (default 50%) chance to spawn an additional copy
of the **refined output** when a Smelter / Blast Furnace / Spinning Wheel / Eitr Refinery
completes a product within 30 m of a player who has `SE_SeekerLordSpirit`. Mirrors vanilla
`Smelter.Spawn`: looks up the matching `ItemConversion` in `__instance.m_conversion`,
instantiates `m_to` at `m_outputPoint`, sets the stack, and calls `ItemDrop.OnCreateNew`.  
**Gotcha 1:** Parameter is named `ore`, not `name`. An earlier version used `name` and
also had a non-existent `spawnQueueCount` parameter — both caused IL compile errors.  
**Gotcha 2:** `ore` is the **input** item name (the conversion's `m_from`, e.g. `CopperOre`),
NOT the produced item. An earlier version spawned `PrefabManager.GetPrefab(ore)` directly —
which dropped raw input ore instead of the refined output, skipped `ItemDrop.OnCreateNew`
(leaving an uninitialised ghost), and so appeared to do nothing. Always resolve `m_to` from
the conversion table.

## Featherweight (Fallen Valkyrie Lord blessing)

The Featherweight blessing (`SE_FallerValkyrieLordSpirit`) carries **no** `SE_Stats`
modifiers — both halves are implemented by the patches below.

### `Player_GetMaxCarryWeight_Featherweight` (`Patches/FeatherweightEncumbrancePatch.cs`)

**Target:** `Player.GetMaxCarryWeight` postfix  
**What it does:** While the blessing is active, raises the reported max carry weight to
`LordConfig.FallerValkyrieWeightCap` (default 1000). Vanilla keys every over-encumbrance
penalty off `IsEncumbered()` (`GetTotalWeight() > GetMaxCarryWeight()`), so this single
postfix removes the slowdown, crouch-walk, run-lock, stamina-drain, dodge-lock and
encumbered animation below the cap, and keeps auto-pickup working up to it — no other
behaviour patches needed. Only raises the value, never clamps a legitimately higher one.  
**Gotcha:** Vanilla never speed-gates *attacks* by encumbrance, so attack speed is unchanged
either way.

### `InventoryGui_UpdateInventoryWeight_Featherweight` (same file)

**Target:** `InventoryGui.UpdateInventoryWeight` postfix  
**What it does:** Rewrites the HUD weight readout to show the player's **base** capacity
(`m_maxCarryWeight` + `SEMan.ModifyMaxCarryWeight`, which excludes the cap since the SE adds
no carry weight) instead of the raised 1000 cap. The cap is surfaced via the blessing
tooltip/compendium, not the number.  
**Gotcha:** `m_weight` is a `TMP_Text`; we set its `text` via reflection to avoid a
compile-time dependency on `Unity.TextMeshPro`.

### `Inventory_Load_FeatherweightExpand` (`Patches/InventoryExpandLoadPatch.cs`)

**Target:** `Inventory.Load` prefix  
**What it does:** Valheim saves item grid positions but **not** inventory dimensions, so the
player inventory always reloads at 8×4. This prefix detects the player inventory (name
`"Inventory"`, width 8) and pre-grows it to a safe ceiling **before** items are read, so
items saved in Featherweight's extra rows land in their slots instead of being compacted
into the base grid — or **destroyed** if the base grid is full (`Inventory.AddItem`). Height
is then finalised on spawn.

### `Player_OnSpawned_BlessingPersistence` (`Patches/BlessingPersistencePatch.cs`)

**Target:** `Player.OnSpawned` postfix (local player)  
**What it does:** Re-applies the persisted active blessing — stored in
`Player.m_customData["biomelords.blessing"]` by `BlessingSystem` — from the SE registry
(no pedestal charge consumed), so **all** blessings now survive logout and death. Then calls
`FeatherweightInventory.Reconcile` to set the final inventory height (expanded if
Featherweight is active, base otherwise), crating any items left beyond it.  
**Crate logic:** Switching away from Featherweight (`BlessingSystem.RemoveOtherBlessings`)
collapses the extra rows and spills their contents into one or more `CargoCrate`s at the
player's feet, mirroring `Container.DropAllItems(m_destroyedLootPrefab)`. The prefab is
fetched as `ZNetScene.GetPrefab("CargoCrate")`, falling back to the Cart's own
`Container.m_destroyedLootPrefab`. **Overflow → more crates, not a bigger crate:** when a
crate fills up a fresh one is spawned (fanned out in a small arc). We deliberately do *not*
enlarge a single crate — a `Container` rebuilds its inventory from the **prefab** width/height
on world reload, so an over-sized crate would lose the surplus items on reload. Ground-drop
is only a last resort if the CargoCrate prefab can't be found at all.

### `InventoryGui_Show_FeatherweightPanel` (`Patches/FeatherweightInventoryUiPatch.cs`)

**Target:** `InventoryGui.Show` postfix  
**What it does:** Stretches the player panel (`m_player`) and its backdrop image to cover
Featherweight's extra rows so they sit inside the normal inventory frame. The slots
themselves need no work — `InventoryGrid` builds every cell from the same `m_elementPrefab`
and auto-resizes the grid root, so the extra rows already have identical slot art, key
bindings and tooltips. Height delta = `extraRows × m_elementSpace`, computed from cached
vanilla base sizes so repeated opens / blessing toggles stay stable.  
**Gotcha:** Fully defensive (try/catch, null-checks) and uses a string-based `GetComponent`
to find the backdrop without referencing `UnityEngine.UI`. If the panel hierarchy differs it
degrades to "rows extend slightly past the frame" rather than throwing.

### Ceremony / power VFX — no green

The two green/teal vanilla prefabs `fx_summon_start` (green summon burst) and `vfx_prespawn`
(teal-green spawn swirl) are excluded from every Lord ceremony, blessing and Forsaken Power:
- **Forsaken Power use** — `GuardianPowerFactory` GP_* `m_startEffects` (gold/flash/roar only),
  plus the per-power activation bursts in `HowlAura`, `PowerEffectsService` (phantom wolf) and
  `ValkyrieRallyService`.
- **Forsaken Power claim (on Lord kill)** — `PowerClaimSystem.PlayAwardFx`.
- **Trophy placement** — `ItemStand_SetVisualItem_MountHook` → `PlayMountCeremony` (non-green
  golden ceremony: `vfx_lootspawn` pops + `vfx_HitSparks` ring), once per fresh mount.
- **Blessing grant + persistence re-apply** — the 7 blessing SE `m_startEffects` in
  `StatusEffectFactory` no longer include `fx_summon_start`.

> Still green elsewhere by design: the **Lord summon/spawn** burst (`LordFx`) and a couple of
> biome-themed blessing effects (`vfx_swamp_mist` on the Draugr blessing, `fx_gdking_rootspawn`
> on the Greydwarf blessing). Say the word if those should go too.

---

## World events

### `EventRegisterPatch` (`Patches/EventRegisterPatch.cs`)

**Target:** `RandEventSystem.Awake` postfix  
**What it does:** Injects all 7 Lord world events into the game's random event list.
Each event specifies biome, weather variant, and music.

### `HornUsePatch` (`Patches/HornUsePatch.cs`)

**Target:** `ItemDrop.ItemData.GetTooltip` postfix + `Player.UseItem`  
**What it does:** On Horn use, verifies kill count ≥ requirement, verifies correct biome,
then calls `SummonService.Summon(lord)` to start the event and spawn the Lord.

### `HornTooltipPatch` (`Patches/HornTooltipPatch.cs`)

**Target:** `ItemDrop.ItemData.GetTooltip` postfix  
**What it does:** Appends current kill count / requirement to the Horn's tooltip text.

---

## Pedestal patches

### `PedestalInteractPatch` (`Patches/PedestalInteractPatch.cs`)

**Target:** `Piece.Interact` postfix  
**What it does:** Detects Lord Pedestal interaction, triggers `BlessingSystem` to grant
the blessing and decrement the trophy's charge count.

### `PedestalProtectPatch` (`Patches/PedestalProtectPatch.cs`)

**Target:** `WearNTear.Damage` prefix  
**What it does:** Makes the Lord's Pedestal indestructible (blocks all damage).

### `PedestalRegisterPatch` (`Patches/PedestalRegisterPatch.cs`)

**Target:** `ZNetScene.Awake` postfix  
**What it does:** Ensures the Pedestal prefab is registered in `ZNetScene` for network sync.

---

## Misc patches

### `ObjectDBSEInjectPatch` — `ObjectDB_Awake_BiomeLordsInject` (`Patches/ObjectDBSEInjectPatch.cs`)

**Target:** `ObjectDB.Awake` postfix  
**What it does:** Calls `InjectAll()` — adds all registered BiomeLords SEs into
`ObjectDB.instance.m_StatusEffects`. Also called explicitly after factory registration
in `OnVanillaPrefabsAvailable` because `ObjectDB.Awake` fires first.

### `ValkyrieRallyRpcPatch` — `Game_Start_ValkyrieRallyRpc` (`Patches/ValkyrieRallyRpcPatch.cs`)

**Target:** `Game.Start` postfix  
**What it does:** Registers the `BiomeLords_ValkyrieRally` routed RPC on the current
`ZRoutedRpc.instance` so Valkyrie's Rally (`GP_ValkyrieAscension`) can broadcast its group
restore burst. `ZRoutedRpc.instance` is recreated per world-join, so this re-registers each
time; `ValkyrieRallyService.RegisterRpc()` no-ops if already registered on the current
instance.

### `PlagueBearerPatch` (`Patches/PlagueBearerPatch.cs`)

**Target:** `Character.Damage` prefix  
**What it does:** Passive poison resistance from the Draugr Lord blessing — reduces
incoming poison damage while `SE_DraugrLordSpirit` is active.

### `OakHealingTickPatch` (`Patches/OakHealingTickPatch.cs`)

**Target:** `Player.Update` postfix  
**What it does:** While `SE_GreydwarfLordSpirit` active, ticks slow HP regen if the
player is near a large Oak tree (checks for `Destructible` with matching prefab name).

### `IronVeinPatch` (`Patches/IronVeinPatch.cs`)

**Target:** `MineRock5.RPC_Hit`  
**What it does:** Related to Draugr blessing — preserves swamp resource logic.

### `ForestEmbraceComfortPatch` (`Patches/ForestEmbraceComfortPatch.cs`)

**Target:** `SE_Rested.CalculateComfortLevel`  
**What it does:** Adds bonus comfort when Forest's Embrace FP is active and the player
has been sitting near trees for ≥60 s.

### `PlantHoverPatch` (`Patches/PlantHoverPatch.cs`) + `QuickSproutGrowthPatch`

**What they do:** Debug/admin helpers — show crop grow time in hover text
(gated by `LordConfig.ShowCropGrowTimes`).
