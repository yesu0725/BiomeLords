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

### `GammeltrollShellPatch` (`Patches/GammeltrollShellPatch.cs`)

**Target:** `Character.Damage` prefix
**What it does:** While `GammeltrollLordBrain.IsPetrified` (a replicated ZDO bool, so it is
correct on whichever peer processes the hit), every damage component except `m_pickaxe` is
multiplied by `ShellDamageFactor` (0.10) and `m_pushForce` / `m_staggerMultiplier` are zeroed.
Pickaxe is left alone on purpose — vanilla `TrollFrost` is Weak to it, so picks still land at
×1.5 after `ApplyResistance`. Same mitigate-don't-cancel shape as `LoxLordShieldPatch`.
**Gotcha:** `HitData.DamageTypes.GetTotalDamage()` *does* include chop and pickaxe, which is
why leaving `m_pickaxe` untouched is enough for the "break it with a pick" mechanic.

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

### `BlessingTooltipPatch` — `StatusEffect_GetTooltipString_BlessingValues` + `SE_Stats_GetTooltipString_BlessingValues` (`Patches/BlessingTooltipPatch.cs`)

**Targets:** `StatusEffect.GetTooltipString` postfix **and** `SE_Stats.GetTooltipString` postfix  
**What it does:** Keeps blessing / Forsaken Power descriptions in sync with the config. The
English tooltip strings in `ItemFactory` hold placeholders (`{weight_cap}`, `{extra_rows_text}`,
`{hearth_pct}`, `{refiner_pct}`, `{bait_save_pct}`, `{bonus_fish_pct}`, `{rally_radius}`,
`{rally_rested}`) instead of numbers. For SEs whose `m_tooltip` token belongs to us
(`StatusEffectFactory.ByName` ∪ `GuardianPowerFactory.ByName`), the postfix runs
`Localization.instance.Localize()` on the token itself and `BlessingTooltipValues.Fill()`
substitutes the live `LordConfig` values. Every vanilla display path — compendium
`TextsDialog.AddActiveEffects`, HUD hover, `ItemStand` guardian-power hover — calls
`GetTooltipString()` first and `Localize()` after, and `Localize()` on already-localised text
is a no-op, so the filled string survives.  
**Why both targets:** `GetTooltipString` is virtual and `SE_Stats` (every SE we register)
overrides it; Harmony patches a concrete method, so patching only the base would miss
every one of ours.  
**Why at display time:** SEMan clones the SE per player (`Object.Instantiate`), so rewriting
`m_tooltip` on the registered object on `SettingChanged` would not reach live instances, and
server-synced values would lag. Reading the config on every call costs a few string
replaces and needs no bookkeeping.  
**Adding a placeholder:** add the token to `Fill()` and use it in the string. Unknown
`{tokens}` are left as-is, so a typo shows up literally in-game rather than crashing.

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

**Target:** every `Inventory.Load` overload, prefix — selected by `TargetMethods()`.
Valheim 1.0 added `Load(ZPackage, bool)` beside `Load(ZPackage)`, so a name-only
`[HarmonyPatch(typeof(Inventory), nameof(Inventory.Load))]` now fails with
`Ambiguous match`. The two bodies are identical and every vanilla caller
(`Player`, `Container`, `ZDOMan`) uses the single-argument one, but hooking both costs
nothing and doesn't bet on which one a future patch or another mod routes through.  
**What it does:** Valheim saves item grid positions but **not** inventory dimensions, so the
player inventory always reloads at 8×4. This prefix detects the player inventory (name
`"Inventory"` **or** `"ComfyQuickSlotsInventory"`, width 8 — see CQS note below) and
pre-grows it to a safe ceiling **before** items are read, so items saved in Featherweight's
extra rows land in their slots instead of being compacted into the base grid — or
**destroyed** if the base grid is full (`Inventory.AddItem`). Height is then finalised on
spawn.  
**Incompatible mods:** no-ops entirely (`FeatherweightInventory.GrowForLoad` returns
immediately) when ExtraSlots or AzuExtendedPlayerInventory is loaded — see
[systems.md § Incompatible slot-expansion mods](systems.md#incompatible-slot-expansion-mods-extraslots-azuextendedplayerinventory).

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
**Incompatible mods:** `Reconcile`/`Collapse` (both funnel through `FeatherweightInventory.SetHeight`)
no-op when ExtraSlots or AzuExtendedPlayerInventory is loaded, so BiomeLords never resizes the
grid or crates items sitting in rows that actually belong to the other mod. See
[systems.md § Incompatible slot-expansion mods](systems.md#incompatible-slot-expansion-mods-extraslots-azuextendedplayerinventory).

### `FeatherweightCapacityPatches` (`Patches/FeatherweightCapacityPatch.cs`)

**Targets:** five `Inventory` prefixes (nested patch classes) —
`CanAddItem(ItemData, int)`, `AddItem(ItemData)`,
`AddItem(string, int, int, int, long, string, Vector2i, bool, bool, bool)`, `HaveEmptySlot()`,
`GetEmptySlots()`. Each calls `FeatherweightInventory.EnsureExpanded(__instance)`.

The string `AddItem` overload is matched by an explicit type array, so it is the one
patch here that a vanilla signature change breaks silently — 1.0 appended
`bool pickedUp, bool dropIfFullInv` and the patch was skipped
(`Could not find method … parameters (string, int, int, int, long, string, Vector2i, bool)`)
until the array was extended. When that happens crafting output stops landing in the
Featherweight rows while every other path still works.

**Why it exists.** Vanilla answers *"is there room?"* purely from `m_width * m_height`:

| Vanilla method | Check |
|---|---|
| `CanAddItem` | `FindFreeStackSpace(...) + (m_width * m_height - m_inventory.Count) * maxStack >= stack` |
| `HaveEmptySlot` | `m_inventory.Count < m_width * m_height` |
| `GetEmptySlots` | `m_height * m_width - m_inventory.Count` |
| `AddItem(ItemData)` | `FindEmptySlot(TopFirst(item))`, which scans `y` over `0 .. m_height` |

If the height has drifted back to the vanilla 4, all of those report a full inventory
while the extra rows sit visibly empty — auto-pickup (`Player.AutoPickup` gates on
`CanAddItem`), manual pickup (`Humanoid.Pickup` → `AddItem` → `"$msg_noroom"`), crafting
and container take-all all refuse. **Dragging an item in by hand still works**, because
the explicit-position `AddItem(item, pos)` overload only checks `GetItemAt(x, y)` and never
consults the height — which is exactly the asymmetry the bug report described.

`Reconcile` alone can't hold the invariant: it runs on spawn and on a blessing change, so
anything that rebuilds the inventory afterwards leaves the height stale until the next
spawn. These prefixes re-assert it at the point of use instead.

**Safety.** `EnsureExpanded` only ever **raises**, and only the local player's own
inventory while the blessing is active. Lowering stays exclusive to `Reconcile`/`Collapse`,
which crate the items beyond the new height first — a lowering call from here could
silently strand or destroy them. Every prefix is try/catch-wrapped so a failure degrades to
the un-corrected height rather than blocking a vanilla inventory operation.

**Cost.** `EnsureExpanded` short-circuits on a single int compare when the height is already
correct — the normal case — before touching `Player.m_localPlayer` or the status effect
lookup. That matters because `CanAddItem` runs inside `Player.AutoPickup`'s per-frame sweep
over nearby colliders. Container inventories cost two extra dereferences and no more.

**Incompatible mods:** returns immediately when ExtraSlots or AzuExtendedPlayerInventory is
loaded, like every other Featherweight height path.

**Debugging:** with `DebugLogging` on, logs `inventory height had drifted to N, restoring to M`
whenever it actually corrects something — the way to confirm the drift and see what triggers it.

**Priority.** Every prefix carries `[HarmonyPriority(Priority.First)]`. ComfyQuickSlots
prefixes four of the same five methods and returns `false` to answer them itself, and in
Harmony 2 a prefix returning `false` skips the prefixes *after* it. At equal priority CQS
loads first, so our height correction would never run under CQS at all. Going first also
means CQS computes its own answer from the corrected height.

### `FeatherweightEmptySlotPatches` (`Patches/FeatherweightEmptySlotPatch.cs`)

**Targets:** `Inventory.FindEmptySlot` postfix (always), plus a `Prepare`-gated postfix on
ComfyQuickSlots' own `QuickSlotsManager.GetEmptyInventorySlot(Inventory, bool)`, resolved by
name through `AccessTools.TypeByName` so BiomeLords never references CQS at compile time.
Both delegate to `FeatherweightInventory.FindExtraRowSlot`.

**Why it exists.** Correct capacity is only half the job — the extra rows must also be
*reachable*. `FeatherweightCapacityPatches` keeps `m_height` right, which is enough for
vanilla because `FindEmptySlot` walks `m_height`. ComfyQuickSlots replaces that search
(prefix returning `false`) with `GetEmptyInventorySlot`, which hardcodes `for (y = 0; y < 5;
y++)` — the vanilla 4 rows plus its own armor/quickslot row. Its `CanAddItem` and
`HasEmptyNonEquipmentSlot` replacements meanwhile compute `m_width * m_height - 5`, so they
*do* count the Featherweight rows. Capacity said yes, the slot-finder returned `(-1, -1)`,
and `AddItem` failed: with CQS installed the extra rows rendered and took hand-dragged items,
but auto-pickup, `Humanoid.Pickup` and craft output all reported "inventory full" the moment
rows 0-4 filled.

Patching CQS's method rather than only `Inventory.FindEmptySlot` also covers its other caller,
`Humanoid.UnequipItem`: that path checks `HasEmptyNonEquipmentSlot` (counts our rows, says
yes) and then moves the armour to whatever `GetEmptyInventorySlot` returns — `(-1, -1)` with
the base grid full, stranding the piece. The vanilla `FindEmptySlot` postfix stays as the
mod-agnostic safety net: it runs after *any* prefix that replaced the search, whatever the
plugin load order.

**Safety.** Both postfixes act only when the slot-finder already came back empty-handed, and
`FindExtraRowSlot` scans `y` over `BaseHeight .. m_height` only — rows that exist solely
because Featherweight is active. It never returns a base-grid slot, so another mod's reserved
row is never at risk (CQS's armor/quickslot row is `y = 4`, below the `BaseHeight` of 5 that
CQS's presence sets). With the blessing inactive the scan range is empty and both are no-ops,
as they are for container inventories and when an incompatible slot mod is loaded.

### `InventoryGui_Show_FeatherweightPanel` (`Patches/FeatherweightInventoryUiPatch.cs`)

**Target:** `InventoryGui.Show` postfix  
**What it does:** Stretches the player panel so the extra Featherweight rows sit inside the
normal frame. Height delta = `extraRows × m_playerGrid.m_elementSpace`
(`extraRows = inventory height − FeatherweightInventory.BaseHeight`, where `BaseHeight` is
CQS-aware: 4 vanilla / 5 under CQS).

`GrowDownward` stretches `m_player` and its backdrop image downward with the **top** edge
pinned (pivot-aware via `rt.pivot.y`), so the extra rows are framed. The slots themselves need
no work — `InventoryGrid` builds every cell from the same `m_elementPrefab` and auto-resizes
the grid root. **Skipped under CQS**, where the player backdrop is CQS's own `"ExtInvGrid"`
image (re-sized every grid refresh by CQS, clobbering anything set here);
`InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop` handles that backdrop instead.

**Scope:** this patch owns the **player** panel only. Keeping the chest window clear of the
extra rows is a separate, mod-agnostic concern — see
`InventoryGui_Show_StorageWindowPosition` below.  
**Gotcha:** Fully defensive (try/catch, null-checks) and uses a string-based `GetComponent`
to find the backdrop without referencing `UnityEngine.UI`. If the panel hierarchy differs it
degrades to "rows extend slightly past the frame" rather than throwing.  
**Incompatible mods:** returns immediately (no panel/backdrop changes at all) when
ExtraSlots or AzuExtendedPlayerInventory is loaded, since those mods already grow the player
grid — without this guard `extraRows` would be computed as large and positive even though
Featherweight itself contributes nothing, stretching a large empty backdrop below the real
slots. See [systems.md § Incompatible slot-expansion mods](systems.md#incompatible-slot-expansion-mods-extraslots-azuextendedplayerinventory).

### `InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop` (`Patches/FeatherweightInventoryUiPatch.cs`)

**Target:** `InventoryGrid.UpdateInventory` postfix, `[HarmonyAfter("com.bruce.valheim.comfyquickslots")]`  
**What it does:** No-ops unless ComfyQuickSlots is loaded. CQS draws the player-inventory
backdrop as its own `"ExtInvGrid"` image (cloned from vanilla `"Bkg"`) and re-applies a fixed
size to it on every grid refresh: `height = 300 + 75·num`, `anchoredPosition.y = -35·num`,
`width = 590`, with `num` hardcoded to `1` (the one armor/quickslot row CQS adds beyond
vanilla's 4). That formula has no notion of Featherweight's extra rows, so they'd render
below the frame. This patch re-applies the **same** formula to the **same** `"ExtInvGrid"`
transform but with the true `num = player inventory height − FeatherweightInventory.VanillaHeight`
(1 + any active Featherweight rows), running after CQS so its size is the one that sticks.
Identifies the player's own grid via `InventoryGrid.GetInventory() == player.GetInventory()`
(skips container/craft grids). Try/catch-wrapped; silently skips if `"ExtInvGrid"` doesn't
exist yet that frame.  
**Gotcha:** the magic constants (`300`, `75`, `35`, `590`) are copied from CQS's own
`InventoryGridPatch.UpdatePlayerGrid` — if a future CQS version changes its sizing formula,
this patch needs to be updated to match.

### `InventoryGui_Show_StorageWindowPosition` (`Patches/StorageWindowPositionPatch.cs`)

**Target:** `InventoryGui.Show` postfix  
**What it does:** Delegates to `Util/StorageWindowPosition.Apply`, which places the chest /
storage window (`m_container`) at its configured offset and attaches the `StorageWindowDragger`
component. Try/catch-wrapped; a failure leaves the window at its vanilla spot.

**Placement.** Two config values under section `UI` (deliberately **not** `IsAdminOnly`, so
they stay client-side and aren't overwritten by server sync):

| Key | Default | Meaning |
|---|---|---|
| `StorageUiOffsetColumns` | `0` | Horizontal offset from the vanilla spot, in inventory cell widths (+ = right) |
| `StorageUiOffsetRows` | `2` | Vertical offset from the vanilla spot, in inventory row heights (+ = down) |

Offsets are measured in **cells** (`m_playerGrid.m_elementSpace`), not pixels, so the same
setting looks identical at any UI scale or resolution. The default `2` drops the chest two rows
clear of the player inventory, matching the default `FallerValkyrieExtraRows` of 2 so the
Featherweight rows aren't hidden behind it. Both values are re-read on every `Show` and via
`SettingChanged`, so config-manager edits apply live.

**Dragging.** `StorageWindowDragger` (a `MonoBehaviour` on `m_container` implementing
`IBeginDragHandler` / `IDragHandler` / `IEndDragHandler`) lets the player grab the window
anywhere — backdrop, header, weight readout — and drop it anywhere on screen. On release the
final position is converted back to cell offsets and written to config, so it persists.
Pointer→panel math goes through `RectTransformUtility.ScreenPointToLocalPointInRectangle` on the
parent rect, which is correct under any canvas scale.

**Clamping (rewritten in 0.6.12).** `ClampToScreen` keeps at least `ScreenMargin` (48 **screen
pixels**) of the window reachable on each axis, so a stray drag or a wild config value can't
park it where it can't be grabbed back. Three rules, each of which the original got wrong:

1. **Measure in screen pixels, not the parent's rect.** The old `Clamp` compared the window's
   world corners against `parent.rect`. That only bounds it to the *screen* if the parent is
   the full-screen canvas — and `m_container`'s parent isn't. Everything past the parent's edge
   read as "off screen". `TryGetScreenRect` now runs each corner through
   `RectTransformUtility.WorldToScreenPoint` with the canvas camera (null for Screen Space
   Overlay) and tests against `Screen.width` / `Screen.height`. Correct under every render
   mode and canvas scale, with no assumption about the hierarchy left to be wrong.
2. **Never clamp mid-drag.** The old code clamped on *every* `OnDrag` event. Combined with (1)
   the window was pushed back toward the parent's bounds faster than it could be dragged away,
   which is what "it only moves in the top half of the screen" actually was. `SetDragged` now
   just sets the position; the clamp runs in `Apply` (config), `HookConfig.OnChanged` (live
   edit) and once in `CommitDragged` — drag **end** — before the position is read back into
   config, so what's saved is what the player is looking at.
3. **Refuse to clamp on bad numbers.** `TryGetScreenRect` bails on NaN/Infinity or a
   degenerate (< 1 px) rect rather than shoving the window somewhere on a measurement it
   doesn't understand; the margin is also capped at the window's own size so a small window
   can settle. The correction is converted back to `anchoredPosition` units by
   `TryScreenDeltaToLocal` — two `ScreenPointToLocalPointInRectangle` calls on the parent —
   so there's no `scaleFactor` arithmetic to get wrong.

This is the same design as `ContainerPanelPositioner` in Lost Scrolls II, which hit the
identical bug and was fixed the same way first.

**Lost Scrolls II interaction.** Lost Scrolls' companion inventory uses this exact vanilla
panel, and its `ContainerPanelPositioner.Enabled` returns **false whenever a plugin whose GUID
or name contains "biomelord" is loaded** — it hands the panel to us to avoid two positioners
fighting. So with both mods installed, BiomeLords is the *only* thing placing the chest
window **and** the companion inventory: a bug here shows up as a companion-inventory bug in
Lost Scrolls, and nothing Lost Scrolls does about that panel has any effect.

**Gotcha — slot drags.** Unity routes drag events to the nearest **ancestor** that handles them,
so a drag starting on an item slot would bubble up and move the window. `StartedOnSlots` checks
`pointerPressRaycast.gameObject.transform.IsChildOf(gui.ContainerGrid.transform)` and bails, so
slots keep their normal item behaviour.

**No mod detection.** Nothing here inspects the plugin list. Vanilla never writes
`m_container.anchoredPosition` (`UpdateContainer` only toggles active state), and mods that
reposition the container do so on the container *grid root*, a **child** of `m_container` — so
moving `m_container` carries the whole window (backdrop + header + grid) and preserves any other
mod's grid-relative offset. One placement is correct with or without such a mod. This replaced
the old ComfyQuickSlots-aware chest shift that used to live in
`InventoryGui_Show_FeatherweightPanel`.

**Caveat:** the offset is static, not derived from the live row count. If
`FallerValkyrieExtraRows` is raised above 2, raise `StorageUiOffsetRows` to match (or just drag
the window down).

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
> biome-themed effects (`vfx_swamp_mist` on the Draugr blessing, `fx_gdking_rootspawn` on both
> the Greydwarf blessing's tree-rest heal pulse and the Rootward Forsaken Power's activation
> burst). Say the word if those should go too.

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

### `HornTooltipPatch` — `ItemData_GetTooltip_Patch` (`Patches/HornTooltipPatch.cs`)

**Target:** `ItemDrop.ItemData.GetTooltip(ItemData, int, bool, float, int, bool)` postfix —
the static six-argument overload, matched by explicit type array. Valheim 1.0 appended the
trailing `bool appending`; without it in the array the patch is skipped.  
**What it does:** For the Lord's Horn only, a belt-and-braces `"Utility"` → `"Consumable"`
find/replace on the rendered tooltip in case Valheim labels the item by something other than
`m_itemType`, and — with `DebugLogging` on — logs the raw tooltip text once per session so you
can see where a stray label is coming from. (The kill-count / requirement lines are built in
`ItemFactory`, not here.)

---

## Pedestal patches

### `PedestalInteractPatch` (`Patches/PedestalInteractPatch.cs`)

Six patch classes on `ItemStand`, all gated on `GetComponent<LordsPedestalTag>()` so ordinary
item stands are untouched:

| Class | Target | Role |
|---|---|---|
| `ItemStand_Interact_Patch` | `Interact` prefix | Tap **E** → `BlessingSystem.TryGrant`; hold **E** with a trophy mounted → blocked with `$biomelords_pedestal_locked`; Shift+E / empty stand → vanilla |
| `ItemStand_CanAttach_LordsOnly` | `CanAttach` postfix | Only Lord trophies (`BlessingSystem.TryResolve`) may be mounted |
| `ItemStand_DropItem_LockTrophy` | `DropItem` prefix | Defence in depth — no path may drop a mounted trophy back to the world; our `ConsumeTrophy` goes through `DestroyAttachment` instead |
| `ItemStand_UseItem_ResetCharges` | `UseItem` postfix | Safety-net `ResetCharges` on a successful mount; owns `PlayMountCeremony` (golden pops + sparks ring — deliberately no green VFX) |
| `ItemStand_SetVisualItem_MountHook` | `SetVisualItem(int, int, int, int)` postfix | The canonical "trophy just got mounted" hook: `ResetCharges` + the ceremony, but only for a *fresh* mount — a ZDO key (`biomelords.ceremony_for`) stores the last-ceremonied trophy name, so the replay on world load matches and stays silent |
| `ItemStand_GetHoverText_Patch` | `GetHoverText` postfix | Hover overlay: charges remaining / cooldown / "spirit spent", plus the locked-until-consumed reminder |

**Valheim 1.0 — item stands store a hash now.** `ItemStand.GetAttachedItem()` returns the
trophy's `GetStableHashCode()` (an `int`) rather than its prefab name, and `SetVisualItem`
became `(int itemHash, int variant, int quality, int orientation)` — the old
`(string itemName, int variant, int quality)` is gone. Every lookup in this file is keyed on
the prefab name (that's what `BlessingSystem.ByTrophy` maps), so all of them go through
`BlessingSystem.ResolveAttachedName`, which resolves the hash via
`ObjectDB.instance.GetItemPrefab(int)` → `.name` and returns null for `0` / no ObjectDB / no
prefab. The `SetVisualItem` postfix takes `int itemHash` and resolves it the same way before
comparing against the ceremony key — so the key keeps storing the **name**, and pedestals
mounted before 1.0 carry over without re-firing their ceremony.

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

### `OakHealingTickPatch` — `Player_Update_BiomeLordsTicks` (`Patches/OakHealingTickPatch.cs`)

**Target:** `Player.Update` postfix  
**What it does:** Drives all per-frame BiomeLords tick logic for the local player by
calling `ForestEmbraceService.Tick()` (Greydwarf blessing tree-rest healing/comfort) and
`PowerEffectsService.Tick()` (marker-based Forsaken Power effects, including Tide's
Grace, Howl of the Pack, Hive Sight, Valkyrie's Rally, and Rootward).

### `IronVeinPatch` (`Patches/IronVeinPatch.cs`)

**Target:** `MineRock5.RPC_Hit`  
**What it does:** Related to Draugr blessing — preserves swamp resource logic.

### `ForestEmbraceComfortPatch` (`Patches/ForestEmbraceComfortPatch.cs`)

**Target:** `SE_Rested.CalculateComfortLevel`  
**What it does:** Adds bonus comfort when the Greydwarf Shaman Lord's blessing
(`SE_GreydwarfLordSpirit`, "Forest's Embrace") is active and the player has been sitting
near a mature tree for ≥30 s. Also backs `ForestEmbraceService.IsNearQualifyingTree` so
standing near a tree counts as shelter immediately, independent of the sit-timer gate.
Note: this patch predates the 0.6.3 rework and originally watched the Forsaken Power of
the same name — the tree-rest effect now lives on the blessing instead (see
`ForestEmbraceService`).

### `PlantHoverPatch` (`Patches/PlantHoverPatch.cs`) + `QuickSproutGrowthPatch`

**What they do:** Debug/admin helpers — show crop grow time in hover text
(gated by `LordConfig.ShowCropGrowTimes`).
