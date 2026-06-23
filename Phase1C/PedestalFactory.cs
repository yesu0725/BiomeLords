using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;
using Jotunn.Configs;
using BiomeLords.Config;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Hall of the Lords (formerly Lord's Pedestal) — a cloned Yagluth
    /// Sacrifice Altar registered as a new Hammer-buildable piece.
    ///
    /// Why this works:
    ///   • The vanilla altar is structured as one OfferingBowl + multiple
    ///     child ItemStands (the 5 totem slots).
    ///   • We disable the OfferingBowl's boss-spawn fields (no more "summon
    ///     Yagluth" path on our cloned copy).
    ///   • Each child ItemStand gets a LordsPedestalTag so the existing
    ///     CanAttach / Interact / charges / destroy-on-last-use logic
    ///     applies independently to every slot.
    ///   • Cloning leaves the original Plains altar untouched — players who
    ///     find a real Yagluth altar in the wild still get the vanilla behavior.
    ///
    /// Important caveat surfaced in the piece description:
    ///   Destroying the altar (via Hammer middle-click) destroys EVERY mounted
    ///   trophy with it — there's no "remove" path otherwise.
    /// </summary>
    public static class PedestalFactory
    {
        public const string PedestalPrefab = "LordsPedestal";

        private static bool _registered;

        // Plain vanilla item stand cloned as our base. We then scale it up,
        // tint it, and parent a glowing Light + particle so it reads as a
        // distinct ceremonial pedestal — not just another item-stand.
        private static readonly string[] AltarCandidates =
        {
            "itemstandh",
            "itemstandv",
        };

        public static void RegisterAll()
        {
            if (_registered) return;
            string baseName = FindBasePrefab();
            if (baseName == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Could not find Yagluth altar prefab — pedestal not registered.");
                return;
            }
            Jotunn.Logger.LogInfo($"[BiomeLords] Hall of the Lords base prefab: {baseName}");

            var piece = new CustomPiece(PedestalPrefab, baseName, new PieceConfig
            {
                Name        = "$piece_lordspedestal",
                Description = "$piece_lordspedestal_desc",
                PieceTable  = "Hammer",
                Category    = "Misc",
                Requirements = ParseRecipe(LordConfig.HallRecipe.Value),
            });

            var prefab = piece.PiecePrefab;
            if (prefab != null)
            {
                ApplyVisualTreatment(prefab);

                // Tag every child ItemStand so the existing pedestal logic
                // (CanAttach / Interact / charges) applies to each slot.
                int tagged = 0;
                foreach (var stand in prefab.GetComponentsInChildren<ItemStand>(includeInactive: true))
                {
                    if (stand.gameObject.GetComponent<LordsPedestalTag>() == null)
                    {
                        stand.gameObject.AddComponent<LordsPedestalTag>();
                        tagged++;
                    }
                }
                Jotunn.Logger.LogInfo($"[BiomeLords] Lord's Pedestal: tagged {tagged} trophy slot(s).");

                // Render a fresh icon from the now-tinted-and-scaled prefab so
                // the Hammer menu shows our distinct version, not vanilla's.
                TryRenderPieceIcon(piece);
            }

            PieceManager.Instance.AddPiece(piece);
            _registered = true;
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered piece: {PedestalPrefab}");
        }

        /// <summary>
        /// Scale up, tint slightly gold (subtle — preserves wood texture),
        /// attach a soft golden point light, and parent a vanilla looping
        /// particle as a passive aura so the pedestal stands out from regular
        /// item stands at a glance.
        /// </summary>
        private static void ApplyVisualTreatment(GameObject prefab)
        {
            // 1.8x scale — distinctly bigger than vanilla item stands.
            prefab.transform.localScale = Vector3.one * 1.8f;

            // Deeper amber-gold so the tint is unambiguously different from
            // vanilla wood, while still letting some texture detail through.
            var goldTint = new Color(0.95f, 0.65f, 0.30f, 1f);
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var srcs = r.sharedMaterials;
                if (srcs == null || srcs.Length == 0) continue;
                var copies = new Material[srcs.Length];
                for (int i = 0; i < srcs.Length; i++)
                {
                    if (srcs[i] == null) continue;
                    var m = new Material(srcs[i]);
                    if (m.HasProperty("_Color")) m.color = goldTint;
                    copies[i] = m;
                }
                r.sharedMaterials = copies;
            }

            // Override the in-world hover name (vanilla ItemStand returns "Item Stand").
            var stand = prefab.GetComponent<ItemStand>();
            if (stand != null) stand.m_name = "$piece_lordspedestal";

            // Light aura — softer warm gold.
            var auraGO = new GameObject("LordsPedestal_Aura");
            auraGO.transform.SetParent(prefab.transform, worldPositionStays: false);
            auraGO.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var light = auraGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = new Color(1.0f, 0.75f, 0.30f);
            light.intensity = 1.6f;
            light.range     = 4.0f;

            // Smoke aura — gentle wisp rising from the pedestal. Tries
            // vanilla smoke prefabs in order, takes the first that exists.
            TryAttachAuraParticle(prefab.transform, new[]
            {
                "vfx_smoke",
                "fx_smoke",
                "vfx_swamp_mist",
                "fx_fader_arena_fissure_smoke",
            });
        }

        private static void TryAttachAuraParticle(Transform parent, string[] candidates)
        {
            foreach (var name in candidates)
            {
                var src = Jotunn.Managers.PrefabManager.Instance.GetPrefab(name);
                if (src == null) continue;
                var inst = Object.Instantiate(src, parent);
                inst.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                inst.transform.localScale    = Vector3.one * 0.6f;
                inst.name = "LordsPedestal_AuraFx";

                // Vanilla particle prefabs often carry a ZNetView for network
                // sync; nesting two ZNetViews under the pedestal triggers the
                // "Double ZNetview" warning and breaks both. Strip child nv's.
                foreach (var nv in inst.GetComponentsInChildren<ZNetView>(true))
                    Object.DestroyImmediate(nv);

                Jotunn.Logger.LogInfo($"[BiomeLords] Pedestal aura particle: {name}");
                return;
            }
        }

        /// <summary>
        /// Generate a 64x64 sprite from the visually-treated prefab and use it
        /// as the Hammer-menu icon for the piece. Falls back silently if the
        /// renderer isn't ready (the piece will then show vanilla item-stand
        /// icon — not catastrophic, just less distinct).
        /// </summary>
        private static void TryRenderPieceIcon(CustomPiece piece)
        {
            try
            {
                var req = new RenderManager.RenderRequest(piece.PiecePrefab)
                {
                    Width = 64, Height = 64, UseCache = true,
                };
                var sprite = RenderManager.Instance.Render(req);
                if (sprite != null && piece.Piece != null)
                {
                    piece.Piece.m_icon = sprite;
                    Jotunn.Logger.LogInfo("[BiomeLords] Lord's Pedestal: custom icon rendered.");
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Pedestal icon render failed: {ex.Message}");
            }
        }

        private static string FindBasePrefab()
        {
            foreach (var c in AltarCandidates)
                if (PrefabManager.Instance.GetPrefab(c) != null) return c;
            return null;
        }

        /// <summary>
        /// Parses a "Item:Amount,Item:Amount,..." string into Jotunn
        /// RequirementConfig entries. Silently skips malformed pairs and
        /// non-positive amounts. Falls back to the default recipe if the
        /// user wipes the config field.
        /// </summary>
        private static RequirementConfig[] ParseRecipe(string csv)
        {
            var list = new List<RequirementConfig>();
            if (!string.IsNullOrWhiteSpace(csv))
            {
                foreach (var part in csv.Split(','))
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var kv = trimmed.Split(':');
                    if (kv.Length != 2) continue;
                    var item = kv[0].Trim();
                    if (string.IsNullOrEmpty(item)) continue;
                    if (!int.TryParse(kv[1].Trim(), out var amount)) continue;
                    if (amount <= 0) continue;
                    list.Add(new RequirementConfig
                    {
                        Item    = item,
                        Amount  = amount,
                        Recover = true,
                    });
                }
            }

            if (list.Count == 0)
            {
                Jotunn.Logger.LogWarning(
                    "[BiomeLords] Hall recipe empty / unparseable — falling back to defaults.");
                list.Add(new RequirementConfig { Item = "Stone",        Amount = 40, Recover = true });
                list.Add(new RequirementConfig { Item = "FineWood",     Amount = 20, Recover = true });
                list.Add(new RequirementConfig { Item = "Flint",        Amount = 10, Recover = true });
                list.Add(new RequirementConfig { Item = "SurtlingCore", Amount = 3,  Recover = true });
            }

            return list.ToArray();
        }
    }
}
