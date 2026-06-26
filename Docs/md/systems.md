# BiomeLords — Runtime Systems

See also: [architecture.md](architecture.md), [patches.md](patches.md)

---

## Scaling overview

All stat scaling — for both Biome Lords and vanilla Forsaken bosses — is driven by a
single value: **`effectiveTier`**.

```
effectiveTier = max(entity.nativeTier, LordDefeatStore.HighestDefeatedTier())
```

When no Lords have been killed, `HighestDefeatedTier()` returns 0 and every entity
uses its own native tier (vanilla behaviour). Each Lord kill raises the floor, so
all lower-tier entities scale up to match the highest Lord that has been defeated.

---

## Damage pipeline — Biome Lords (profile convergence)

Lords no longer multiply their cloned creature's native attack. Each Lord has a
**base attack profile** (`LordAttackProfile`) matched to its OWN cloned vanilla
creature's signature attack (no vanilla-boss matching) — a *single blended profile*
every swing deals. Neck Lord and Greydwarf Lord run their vanilla creature's
attack at +20%; Draugr Lord through Fallen Valkyrie Lord run it unmodified. Progression still
makes a Lord **converge** toward higher tiers, exactly parallel to HP:

```
profile = LordAttackProfile.Resolve(lordId, nativeTier, effectiveTier)
dmgMult = LordConfig.DamageMultiplier(lordId) × LordIntrinsic.DamageMultiplier(id)   // 1.0 by default
FinalDmg = profile × dmgMult
```

- At the native tier the Lord deals its **own** profile (its own vanilla creature's
  signature attack).
- Once `effectiveTier > nativeTier` it converges: it **keeps its own physical type**
  but sets that value to the higher tier's target magnitude (`ByTier`), and **adds**
  any tier elemental on top of its own (shared elements stack — in practice this table
  no longer carries elemental, so it's a pure magnitude bump).
  - e.g. Neck Lord (6 slash) at effectiveTier 3 (58 magnitude) → **58 slash**.

The resolved profile is baked per-instance into `LordProfileRegistry` at spawn
(`SummonService.ApplyScaling`) and on Awake (`LordAutoRegisterPatch`); `dmgMult` goes
into `LordDamageRegistry`. `LordDamageBoostPatch` **overwrites** the hit's combat
damage with `profile × dmgMult` (chop/pickaxe left as the native weapon's values).

Note: most "ability" damage hits (Bellow, Spit, Dive Bomb, Wind Gust, Soul Harvest,
etc.) also call `hit.SetAttacker(lordCharacter)`, so they too get overwritten by this
same blended profile rather than dealing their own distinct number — they vary in
shape/AoE/range/CC, not raw magnitude. A few abilities deliberately omit the attacker
to bypass this and deal their own independent number (Draugr Lord's Plague Cloud and
Wound bleed, both 5/s — no vanilla analog, left unchanged).

## Damage pipeline — vanilla Forsaken bosses (magnitude scaling)

Vanilla bosses keep their own full attack kit and damage/elemental types — only the
values increase, scaled to the same per-tier magnitude the Lords converge to:

```
dmgMult = LordAttackProfile.TierMagnitude(effectiveTier)
        / LordAttackProfile.TierMagnitude(nativeTier)
```

`VanillaBossScalingPatch` stores this in `LordDamageRegistry`; `LordDamageBoostPatch`
applies it via its legacy multiply path (no profile → uniform multiply, preserving
type ratios). When `effectiveTier == nativeTier` the multiplier is 1.0 and vanilla
damage is untouched.

> `TierMagnitude(tier)` = total combat damage of that tier's target profile:
> `6, 17, 58, 85, 100, 100, 160` for tiers 1–7 (monotonically non-decreasing — tiers 5
> and 6 are flat since each averages two Lords sharing that tier). This table now
> drives vanilla-boss damage scaling too, not just Lord convergence — a side effect
> worth knowing about: the relative scale-up a player sees from killing a higher-tier
> Lord changed along with the Lord rebase, since both pulls from the same `ByTier` table.

---

## TierTable (`Util/TierTable.cs`)

Static **HP-only** lookup indexed by tier (1–7). (Damage scaling no longer lives here —
Lords converge via `LordAttackProfile`, vanilla bosses scale via `TierMagnitude`.)

```
Tier 1 → HP   500   (Meadows / Eikthyr)
Tier 2 → HP  2500   (Black Forest / Elder)
Tier 3 → HP  5000   (Swamp / Bonemass)
Tier 4 → HP  7500   (Mountain / Moder)
Tier 5 → HP 10000   (Plains + Ocean / Yagluth)
Tier 6 → HP 12500   (Mistlands + Deep North / Queen)
Tier 7 → HP 25000   (Ashlands / Fallen Valkyrie)
```

Used as the HP target for scaled **vanilla bosses** (`HpFor(effectiveTier)`) and as the
**ratio basis** for Lord HP (see LordBaseStats). Lords also set their prefab `m_health` in
`CreatureFactory` to the matching base value; `SummonService.ApplyScaling` overrides it at
summon time with the scaled HP.

---

## LordBaseStats (`Util/LordBaseStats.cs`)

Per-Lord **base HP** at the Lord's native tier — its identity value, matched to the
biome boss. Lord HP at spawn:

```
targetHp = LordBaseStats.HpFor(lordId, tier)
         × (TierTable.HpFor(effectiveTier) / TierTable.HpFor(nativeTier))   // progression ratio
         × LordConfig.HealthMultiplier(lordId)
```

All 7 Lords are boss-backed, so each base equals `HpFor(nativeTier)` and this converges to
`HpFor(effectiveTier)`:

| Lord | Base HP | Note |
|---|---|---|
| neck_lord | 500 | Eikthyr |
| greydwarf_lord | 2500 | The Elder |
| draugr_lord | 5000 | Bonemass |
| fenring_lord | 7500 | Moder |
| lox_lord | 10000 | Yagluth |
| seeker_lord | 12500 | The Queen |
| faller_valkyrie_lord | 25000 | Fallen Valkyrie |

---

## LordAttackProfile (`Util/LordAttackProfile.cs`)

Holds each Lord's base attack profile, the per-tier convergence-target magnitudes, and
the convergence resolver. No vanilla-boss values anywhere in this table — every profile
is matched to the Lord's own cloned vanilla creature's signature attack (0★ damage).

**Base profiles (own, at native tier):**

| Lord | Vanilla creature | Signature attack (0★) | Profile |
|---|---|---|---|
| neck_lord | Neck | Bite, 5 slash | **6 slash** (×1.2) |
| greydwarf_lord | Greydwarf_Shaman | Scratch, 14 slash | **17 slash** (×1.2) |
| draugr_lord | Draugr_Elite | Sword swing, 58 slash | **58 slash** (×1.0) |
| fenring_lord | Fenring | Claw scratch, 85 slash | **85 slash** (×1.0) |
| lox_lord | Lox | Bite, 130 slash | **130 slash** (×1.0) |
| seeker_lord | Seeker | Claw thrust, 120 pierce | **120 pierce** (×1.0) |
| faller_valkyrie_lord | FallenValkyrie | Claw strike, 160 pierce | **160 pierce** (×1.0) |

Neck Lord and Greydwarf Lord run their vanilla creature's attack at +20%; Draugr Lord
through Fallen Valkyrie Lord run it unmodified — a deliberate per-Lord split, not a flat rule.

**Per-tier convergence targets** (`ByTier`) — each tier's own Lord profile total (one Lord
per tier):

| Tier | Magnitude | Basis |
|---|---|---|
| 1 | 6 | Neck Lord |
| 2 | 17 | Greydwarf Lord |
| 3 | 58 | Draugr Lord |
| 4 | 85 | Fenring Lord |
| 5 | 130 | Lox Lord |
| 6 | 120 | Seeker Lord |
| 7 | 160 | Fallen Valkyrie Lord |

- `Resolve(lordId, nativeTier, effectiveTier)` — the convergence blend (see Lord damage pipeline).
- `TryGetByTier(tier, out profile)` / `TierMagnitude(tier)` — used by vanilla boss scaling.

---

## LordProfileRegistry (`Util/LordProfileRegistry.cs`)

`Dictionary<int, DamageProfile>` keyed by `Character.GetInstanceID()`, parallel to
`LordDamageRegistry`. Holds the convergence-resolved profile per Lord instance, baked at
spawn (`SummonService`) and on Awake (`LordAutoRegisterPatch`), read by `LordDamageBoostPatch`.

---

## LordIntrinsic (`Util/LordIntrinsic.cs`)

Per-Lord baked damage multiplier, applied on top of the resolved profile. **All values
are now 1.0** — Lord damage comes from the matched profile, so no per-Lord intrinsic
amplification is needed. Retained as a live-tuning surface for ad-hoc balance experiments.

Runtime tuning: `biomelords_intrinsic <id> <value>` — writes directly into the dictionary
and refreshes alive instances via `DebugCommands.RefreshLordInstances` (re-resolves the
profile + mult). Changes are session-only; next launch resets to the baked defaults (1.0).

---

## LordDefeatStore (`Util/LordDefeatStore.cs`)

Tracks which Biome Lords have been killed and exposes `HighestDefeatedTier()` for
scaling queries. Storage mode is controlled by `LordConfig.GlobalLordDefeats`:

| Mode | `RecordDefeat` writes | `HighestDefeatedTier` reads |
|------|----------------------|-----------------------------|
| `false` (default) | `Player.m_localPlayer.AddUniqueKey(key)` | `player.HaveUniqueKey(key)` |
| `true` | `ZoneSystem.instance.SetGlobalKey(key)` | `ZoneSystem.instance.GetGlobalKey(key)` |

Key format: `"biomelords_defeated_<lordId>"` (e.g. `"biomelords_defeated_neck_lord"`).

**Per-player mode (default):** each player's progression is independent.
Killing the Greydwarf Lord only raises *your* scaling tier — other players on the
same server who haven't killed it yet see their own (lower) tier.

**Global mode:** any Lord kill on the server raises scaling for every player.
Use this for co-op worlds where progression should be shared.

`RecordDefeat` is called from `KillTrackerPatch.OnLordDeath`, immediately after the
Forsaken Power unique key is set on the killing player.

---

## LordDamageRegistry (`Util/LordDamageRegistry.cs`)

`Dictionary<int, float>` keyed by `Character.GetInstanceID()`. Holds the damage
**multiplier** (not the profile — that's in `LordProfileRegistry`).

- Written at spawn — for Lords by `SummonService` / `LordAutoRegisterPatch`
  (`config × intrinsic`, 1.0 by default); for vanilla bosses by `VanillaBossScalingPatch`
  (the magnitude ratio)
- `Has(character)` — true if the character has a registered multiplier (used by
  `LordDamageBoostPatch` to admit vanilla bosses without the `IsLord` guard)
- Read by `LordDamageBoostPatch` on every hit originating from a registered character
  (Lords: `profile × mult`; vanilla bosses: native damage `× mult`)

If `DebugCommands.RefreshLordInstances(lordId)` is called (e.g. after live tuning),
it re-walks `Character.GetAllCharacters()` and rewrites both the multiplier and the
resolved profile for every alive Lord of that id.

---

## RegisteredLords (`Util/RegisteredLords.cs`)

Three dictionaries populated at creature registration time:

```
PrefabNames       : HashSet<string>             — is this a Lord prefab?
EventByPrefab     : Dictionary<string, string>  — prefab name → event id (for event end-on-death)
LordIdByPrefab    : Dictionary<string, string>  — prefab name → BiomeLordDef.Id
```

`IsLord(Character)` and `LordIdFor(Character)` strip `(Clone)` from `gameObject.name` before lookup.

---

## KillStore (`Util/KillStore.cs`)

ZDO-persisted per-Lord kill counters. Keys are derived from `BiomeLordDef.Id`.

- Incremented by `KillTrackerPatch` when a kill target creature dies
- Read by `SummonService` to gate Horn usage
- Reset to 0 after a Lord is successfully summoned (or optionally on Lord kill)
- Gated by `LordConfig.EnableKillTracking` (admin-configurable master switch)

---

## PowerEffectsService (`Phase1D/PowerEffectsService.cs`)

Static service ticked every frame from `Player.Update` (via patch).
Handles all marker-based Forsaken Powers — those with no innate stats that need
custom per-frame logic.

### Tide's Grace (Neck Lord)

`TickTidesGrace` — while `GP_TidesGrace` is active and `Player.IsSwimming()`, restores
~25 stamina/s (`SwimStaminaPerSecond`) and pulses an ambient ripple FX. The +50% wet melee
bonus and Wet-debuff immunity live in patches (`Character_Damage_TidesGraceMelee`,
`NeckWetImmunityPatch`), not here.

### Howl of the Pack (Fenring Lord)

Transition detection: compares SE instance reference each tick.
On new activation (old ref ≠ new ref):
1. Snapshots `HaveStatusEffect(packBlessingHash)` for synergy duration
2. `EmpowerNearbyTames` — heals all tamed creatures within 30 m, attaches `HowlAura` MB
3. Computes pack size — 1 wolf normally; in synergy, Blood Magic scales it (≥50 → 2, 100 → 3)
4. `SpawnPhantomPack` → `SpawnPhantomWolf` per wolf — clones the Wolf prefab, tames it, keeps
   `Tameable` (set non-commandable) but strips `Procreation`, marks the ZDO non-persistent,
   sets follow target, applies violet tint, attaches `HowlAura` + `PhantomWolf` MBs. In synergy
   the `PhantomWolf.Invulnerable` flag is set (enforced by `PhantomWolfInvulnPatch`).

---

## HiveSightService (`Phase1D/HiveSightService.cs`)

Ticked by `PowerEffectsService.Tick()` while `GP_HiveSense` is active.

- Scans `Character.GetAllCharacters()` for non-tamed, non-player characters within 80 m
- Adds/refreshes minimap pins via reflection:
  ```csharp
  _addPinMethod.Invoke(Minimap.instance, new object[]{ pos, PinType.Enemy, name, false, false })
  ```
  Reflection used to avoid compiling against `Splatform.dll` (newer Valheim API that
  adds a `PlatformUserID` overload, causing ambiguous-match compile errors).
- Pin list maintained; dead/out-of-range creatures have their pins removed

---

## ValkyrieRallyService (`Phase1D/ValkyrieRallyService.cs`)

Drives Valkyrie's Rally — `GP_ValkyrieAscension` is now a marker-only SE; this service detects
activation and performs the one-shot group support burst. Ticked by
`PowerEffectsService.Tick()`.

**Activation detection:** same transition pattern as Howl of the Pack — compares the marker SE
instance reference each tick; fires exactly once per F-press, on old ref ≠ new ref.

**Broadcast:** on activation, the caster plays a local cinematic burst (`fx_summon_start`,
`fx_himminafl_aoe`, `vfx_lootspawn`) then calls
`ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "BiomeLords_ValkyrieRally", pos)`.
The RPC carries only the caster's position — each client decides locally whether its own
player is in range, then restores **itself**. This is what makes the heal land correctly on
remote clients (you cannot push HP/Stamina/Eitr changes onto another client's Player object
directly).

**Receiver (`OnRallyRpc`, runs on every client):** if the local player is within
`LordConfig.ValkyrieRallyRadius` (default 20 m) of the broadcast position, calls `ApplyRally`:
- `Heal(missingHp, true)`, `AddStamina(GetMaxStamina())`, `AddEitr(GetMaxEitr())` — full restore.
- `AddAdrenaline(GetMaxAdrenaline())` — only if `Humanoid.m_trinketItem` is non-null (read via
  `AccessTools.Field` since the field is private — any vanilla trinket counts).
- `GrantMaxStaffShield` — resolves the `StaffShield` item's `m_attackStatusEffect` (an
  `SE_Shield`) once, caches its hash + `m_maxQuality`; removes any existing shield, then
  `SEMan.AddStatusEffect(hash, true, itemLevel, 100f)` (4-arg overload runs `SetLevel`, so the
  absorb scales to max quality + skill 100).
- `GrantRested` — removes any existing `Rested` SE, re-adds it, overrides `m_ttl` to
  `LordConfig.ValkyrieRallyRestedSeconds` (default 1200 s / 20 min).

**RPC registration:** `RegisterRpc()` is called from the `Game.Start` postfix in
`ValkyrieRallyRpcPatch` (`ZRoutedRpc.instance` is recreated per world-join); guards against
duplicate registration on the same instance via `_registeredOn`.

---

## HowlAura (`Phase1D/HowlAura.cs`)

MonoBehaviour attached to each empowered tame and the Phantom Wolf during Howl activation.

- Spawns a child `Light` (point, colored, pulsing)
- Optionally attaches a loop particle
- Self-destructs when: Lifetime elapses, host character dies, or the marker SE expires

Phantom Wolf gets larger/brighter aura:
```
AuraColor     = (0.55, 0.35, 1.0)  // violet
AuraIntensity = 4.0
AuraRange     = 4.0
```

---

## PhantomWolf (`Phase1D/PhantomWolf.cs`)

MonoBehaviour attached to each phantom wolf `GameObject`.

- Counts down `Lifetime` seconds
- Despawns early if `Player.m_localPlayer` is gone (logout / death-to-menu) so phantoms never linger
- On despawn: spawns poof VFX, then tears down via `ZNetScene.instance.Destroy` (removes the ZDO)
  when networked, or a plain `Object.Destroy` otherwise
- `Invulnerable` flag (set in synergy) is read by `PhantomWolfInvulnPatch` to nullify incoming damage
- Combined with the non-persistent ZDO set at spawn, phantoms are never written to the save

---

## BlessingSystem (`Phase1C/BlessingSystem.cs`)

Handles the pedestal → player blessing flow:
- Player mounts a Lord trophy on a Lord's Pedestal
- Pedestal stores charge count in ZDO (`BlessingChargesPerTrophy` config, default 5)
- On interact: removes one charge, applies corresponding `SE_*Spirit` to nearby players
- Applying a new blessing removes all other blessing SEs (mutual exclusion via `BlessingHashes`)
- Trophy item is destroyed when charges reach 0

**Persistence:** the active blessing's SE name is stored in
`Player.m_customData["biomelords.blessing"]` (serialized with the character), so a blessing
**survives logout and death** — `Player_OnSpawned_BlessingPersistence` re-applies it from the
SE registry on every spawn (no pedestal charge consumed). A blessing only changes when the
player applies a different one. Switching away from Featherweight first collapses its extra
inventory rows (see below).

---

## FeatherweightInventory (`Util/FeatherweightInventory.cs`)

Drives the Fallen Valkyrie Lord blessing's two mechanics (both gated on
`SE_FallerValkyrieLordSpirit` being active):

- **Raised carry cap, no penalty** — `Player_GetMaxCarryWeight_Featherweight` reports
  `FallerValkyrieWeightCap` (default 1000). Vanilla keys all over-encumbrance penalties off
  `IsEncumbered()` (`weight > GetMaxCarryWeight()`), so below the cap there is no slowdown,
  crouch-walk, stamina drain, dodge-lock or encumbered animation. The HUD weight readout is
  rewritten to show the player's **base** capacity, not the cap
  (`InventoryGui_UpdateInventoryWeight_Featherweight`).
- **+`FallerValkyrieExtraRows` inventory rows** (default 2). Valheim doesn't persist inventory
  dimensions, so: `Inventory_Load_FeatherweightExpand` pre-grows the player inventory before
  items load (preventing extra-row items being compacted/destroyed); the spawn re-apply calls
  `Reconcile()` to set the final height; `InventoryGui_Show_FeatherweightPanel` stretches the
  window backdrop to wrap the rows. Switching away spills the extra rows into one or more
  `CargoCrate`s (`Collapse()` — multiple default crates, never an over-sized one, since a
  Container rebuilds from prefab size on reload). On death the tombstone copies the inventory
  size, so nothing is lost.

### ComfyQuickSlots compatibility

[ComfyQuickSlots](https://github.com/ComfyMods/ComfyQuickSlots) (`com.bruce.valheim.comfyquickslots`)
forces the player inventory to **5 rows** and permanently claims grid row `y == 4` (elements
`32–39`) for armor + quickslot bindings — and it renames the inventory to
`"ComfyQuickSlotsInventory"`. `FeatherweightInventory` was originally hardcoded to a 4-row
vanilla base, which under CQS caused three failures: `InventoryGrid.UpdateInventory` crashing
(`ArgumentOutOfRangeException`, CQS reading `m_elements[32..39]` against a 32-element grid
collapsed by `Reconcile`/`Collapse`), the player's equipped armor being treated as "stray"
(`gridPos.y >= 4`) and spilled into a CargoCrate, and Featherweight's own extra-row items
silently lost on load (the renamed inventory failed `IsPlayerInventory`'s name check, so
`GrowForLoad` never ran).

Fix, entirely inside `FeatherweightInventory`:
- `BaseHeight` is now a runtime property, not a constant: `VanillaHeight` (4) normally,
  `VanillaHeight + 1` (5) when CQS is detected via
  `BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.bruce.valheim.comfyquickslots")`
  (cached after first read, exposed as `ComfyQuickSlotsLoaded`). `ExpandedHeight`,
  `LoadCeiling`, `Reconcile`, `Collapse`, and the stray-item cutoff all key off `BaseHeight`, so
  under CQS its armor row is never stripped/crated and Featherweight rows stack **above** it
  (`y ≥ 5`).
- `IsPlayerInventory` accepts either `"Inventory"` (vanilla) or `"ComfyQuickSlotsInventory"`
  (CQS) by name, so the load pre-grow fires either way.
- `Plugin.cs` declares a `BepInDependency(..., SoftDependency)` on CQS's GUID purely so its
  plugin info is registered (in `Chainloader.PluginInfos`) before BiomeLords' own patches run;
  BiomeLords has no hard dependency and works identically without CQS installed.

**Panel backdrop reconciliation** (`Patches/FeatherweightInventoryUiPatch.cs`): CQS draws its
own backdrop image (`"ExtInvGrid"`, cloned from vanilla `"Bkg"`) and re-sizes it itself on every
`InventoryGrid.UpdateInventory` using a hardcoded `num = 1` (rows beyond vanilla 4) →
`height = 300 + 75·num`, `anchoredPosition.y = -35·num`, `width = 590`. That clobbered
BiomeLords' own panel resize and ignored active Featherweight rows. Resolution: when CQS is
loaded, `InventoryGui_Show_FeatherweightPanel` no-ops (stops touching `gui.m_player`/the
vanilla backdrop) and a new `[HarmonyAfter("com.bruce.valheim.comfyquickslots")]` postfix,
`InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop`, re-applies CQS's *own* formula to
CQS's *own* `"ExtInvGrid"` but with the true `num = height − VanillaHeight` (1 + active
Featherweight rows), so the single backdrop always covers exactly the rows actually present.
No-op without CQS; fully try/catch-wrapped.

---

## Item icon rendering (`Phase1B/ItemFactory.cs`)

The Lord's Horn icon is generated at runtime, not authored as a sprite asset, via
Jotunn's `RenderManager.Render(new RenderManager.RenderRequest(item.ItemPrefab) { ... })`
— rendered *after* the prefab is cloned (`TankardAnniversary`) and retinted, so the
icon always matches the current visuals.

**Icon cache gotcha:** Jotunn caches rendered icons on disk keyed by
`prefabName-version-cacheRevision.png` (`Paths.IconCachePath`), not by a content hash
of the mesh/materials. If `RenderRequest.TargetPlugin` is left unset, `version` falls
back to the *game* version only — so a stale icon rendered under an earlier clone
source (e.g. the original `Wishbone` clone) could be served forever via `UseCache =
true`, surviving across restarts until the game version or Jotunn's internal
`cacheRevision` changes. Fixed by setting `TargetPlugin = Plugin.Instance.Info.Metadata`,
which ties the cache key to `BiomeLords`' own mod version so bumping `ModVersion`
forces a fresh render.

---

## PowerClaimSystem (`Phase1C/PowerClaimSystem.cs`)

Runs on Lord death:
- Identifies the Lord by `RegisteredLords.LordIdFor(character)`
- Calls `Player.SetGuardianPower(gpName)` on the killing player
- Shows a HUD message
- Ends the world event via `RandEventSystem.Instance.ResetEvent()`
