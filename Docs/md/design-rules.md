# BiomeLords — Design Rules & Constraints

These are the hard constraints the mod must never violate, plus the philosophy behind them.

See also: [lords.md](lords.md), [development.md](development.md)

---

## Hard rules

### 1. Vanilla-only assets

**Rule:** No custom models, no custom textures, no asset bundles.

All Lord prefabs are created via `PrefabManager.Instance.CreateClonedPrefab(vanilla, newName)`.
Visual distinction comes from:
- Scale multiplier (`transform.localScale`)
- Material color/emission tint on `Renderer` components
- Child `BiomeLords_Aura` Light (point light, colored per biome)
- Vanilla VFX prefabs for spawn/death/ability FX

**Why:** Keeps the mod lightweight (no assets to maintain or bundle),
avoids Unity import pipeline issues, and ensures the mod survives Valheim updates
that change model formats.

The Lord's Horn item (`ItemFactory`) follows the same rule: cloned from the vanilla
`TankardAnniversary` (previously `Wishbone`), retinted teal/cyan so it reads as a
distinct relic rather than a recolored vanilla tankard, and stripped of its vanilla
equip/wield behavior (`m_itemType` forced to `Consumable`, `m_attachOverride` cleared,
`m_animationState` set to `Unarmed`) so it can only be used from inventory, never wielded.

### 2. No duplicate vanilla skills or vanilla Forsaken Powers

**Rule:** A BiomeLords Forsaken Power or Blessing must never grant something
that vanilla already provides — either as a skill bonus or as a vanilla FP effect.

Examples of things removed for this reason:
- Bull Rush (Lox Lord FP): removed Clubs skill bonus (duplicates vanilla skill XP) and
  Blunt/Slash/Pierce physical resists (duplicates Eikthyr FP)
- Pack Whisperer (Fenring blessing): removed +25% wolf damage dealt
  (vanilla taming + skill already handles this axis)

**Why:** Players should feel each Lord reward is genuinely new, not redundant with
something they already have. Duplicate effects also confuse stacking math and produce
unintended multiplicative power spikes.

### 3. Server-authoritative config

**Rule:** All config entries must use:
```csharp
new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true })
```

This makes Jotunn push server values to all clients.
Clients cannot override locally — this prevents desync on shared servers.

**Applies to:** Kill requirements, HP/damage multipliers, blessing effect magnitudes,
pedestal charge counts, hall recipe.

### 4. Per-Lord balance isolation

**Rule:** Never tune a shared tier constant (`TierTable.HpFor`, `LordAttackProfile.ByTier`)
to fix a single Lord.

Use `LordBaseStats` for a Lord's base HP and `LordAttackProfile` (its own profile) for its
base attack. Use `LordConfig.HealthMultiplier` / `DamageMultiplier` for admin-configurable
per-Lord tweaks, and `LordIntrinsic` for a baked per-Lord damage knob.

**Why:** The shared tier curve and per-tier convergence-target profiles are the
convergence targets for *every* Lord at that tier and the scaling basis for vanilla
bosses. Changing them affects all Lords at that tier (and vanilla bosses) simultaneously
— a nerf to the Lox Lord should not inadvertently nerf vanilla Yagluth, which scales off
the same tier-5 curve.

### 5. Kill-gating must be biome-appropriate

**Rule:** Kill targets are vanilla creatures that naturally inhabit the Lord's biome.
The Lord's Horn must be used within that biome to summon.

Kill requirements should feel like a progression gate (hunt the biome first),
not a grind. Reference values:
- Meadows (Neck): 20 — easy starter
- Black Forest (Greydwarf): 30 — slightly more due to 3 valid targets
- Ashlands (Fallen Valkyrie): 25 — endgame gate; hunt the Charred (Melee/Archer/Mage)

### 6. Blessing mutual exclusivity

**Rule:** Only one Lord Blessing can be active at a time.

`BlessingSystem` removes all SEs in `StatusEffectFactory.BlessingHashes` before
applying the new one. Adding a new blessing must add its hash to `BlessingHashes`.

**Why:** Prevents blessing stacking into degenerate builds and gives each
blessing a meaningful choice cost.

### 7. No permanent Frost (or equivalent long-CC)

**Rule:** Any status effect applied by a Lord that slows or disables the player
must have a visible, finite TTL.

The Fenring Lord's Frost SE is capped at 10 s (`FrostTtl = 10f` in `FenringLordBrain`).
Vanilla Frost is much longer and felt punishing in testing.

**Why:** Permanent CC at boss difficulty removes player agency and is frustrating,
not challenging. A 10 s window is enough to register the mechanic without locking
the player in slow-mode indefinitely.

---

## Design philosophy

### Each Lord rewards its biome context

- The Blessing should be useful in or near the Lord's biome
  (e.g. Hearth Master for Plains farming, Featherweight for hauling Ashlands ore/loot, Iron Vein for Swamp mining)
- The Forsaken Power should feel thematically connected to the creature
  (Howl of the Pack → wolves; Tide's Grace → water; Plague Bearer → swamp poison;
  Valkyrie's Rally → a Valkyrie's role as battlefield restorer/escort to Valhalla)

### Marker SEs over stat bloat

Where possible, FPs are "marker only" SEs with no innate stat modifiers.
The actual behavior is implemented in patch code or service ticks.

This is cleaner than stacking `m_mods` / `m_percentigeDamageModifiers` for complex effects,
easier to debug (log the service tick), and easier to tune without rebuilding the SE.

### FP cooldown matches vanilla (20 min)

All Forsaken Powers use `Cooldown = 1200f` (20 minutes) — the same as Eikthyr, Elder, etc.
This anchors them in the familiar Valheim rhythm and avoids trivializing encounters
if players can spam FPs.

### One-shot / armed-state ability guards

Brains gate their burst abilities with explicit per-activation flags rather than relying on
cooldowns alone — e.g. Draugr Lord's Plague Cloud / Undying Surge once-flags, Lox Lord's
`IsLastStand`, and the Fallen Valkyrie Lord's Soul Harvest arm/disarm hysteresis (arms below
30% HP, locks out again above 50%). This gives each power a meaningful decision point and
prevents it from re-firing every frame the HP threshold is crossed.

### Brains are network-owner-only

All `MonoBehaviour.Update()` methods in Brain classes gate on `_nview.IsOwner()`.
This prevents all clients from running AI logic simultaneously,
which would produce duplicated summons, stacked AoE triggers, and desync.
