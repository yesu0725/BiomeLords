using UnityEngine;
using BiomeLords.Util;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Persistent marker + visual aura attached to every tamed Character
    /// caught in a Howl of the Pack activation. Despawns itself (and its
    /// child Light + particle) when:
    ///   • the host dies / is destroyed
    ///   • the local player no longer has GP_HowlOfThePack active
    ///   • Lifetime seconds have elapsed since attach
    ///
    /// Pure visual marker — the damage boost is applied by TamedWolfPatch
    /// reading the player's GP marker directly, not this component.
    /// </summary>
    public class HowlAura : MonoBehaviour
    {
        public float Lifetime = 60f;
        public Color AuraColor = new Color(1.0f, 0.40f, 0.10f);  // ember orange
        public float AuraIntensity = 2.5f;
        public float AuraRange = 2.5f;

        private float _spawnTime;
        private GameObject _auraGO;
        private int _gpHash;

        private void Awake()
        {
            _spawnTime = Time.time;
            _gpHash = GuardianPowerFactory.FenringLordGP.GetStableHashCode();

            // Spawn a child GameObject with a Light + a small loop particle.
            _auraGO = new GameObject("BiomeLords_HowlAura");
            _auraGO.transform.SetParent(transform, false);
            _auraGO.transform.localPosition = new Vector3(0f, 0.8f, 0f);

            var light = _auraGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = AuraColor;
            light.intensity = AuraIntensity;
            light.range     = AuraRange;

            // Try a vanilla looping particle for extra visual punch.
            foreach (var n in new[] { "fx_firepit", "vfx_firewisp", "fx_torch_basic" })
            {
                var src = Jotunn.Managers.PrefabManager.Instance.GetPrefab(n);
                if (src == null) continue;
                var inst = Object.Instantiate(src, _auraGO.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localScale    = Vector3.one * 0.4f;
                inst.name = "BiomeLords_HowlAuraFx";
                // Strip ZNetView so the spawned particle doesn't fight network sync.
                var nv = inst.GetComponent<ZNetView>();
                if (nv != null) Object.Destroy(nv);
                break;
            }

            // Burst FX at attach so the player notices their tames empower.
            // (No "fx_summon_start" — that's the green summon burst.)
            FxLibrary.TrySpawn("vfx_HitSparks",   transform.position + Vector3.up * 0.5f);
        }

        private void Update()
        {
            // Expire on lifetime.
            if (Time.time - _spawnTime >= Lifetime)
            {
                Cleanup();
                return;
            }
            // Expire if the GP marker disappears (player let it run out, swapped GP, died).
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead())
            {
                Cleanup();
                return;
            }
            var seman = p.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_gpHash))
            {
                Cleanup();
                return;
            }
        }

        private void OnDestroy()
        {
            if (_auraGO != null) Object.Destroy(_auraGO);
        }

        private void Cleanup()
        {
            if (_auraGO != null) Object.Destroy(_auraGO);
            Object.Destroy(this);
        }
    }
}
