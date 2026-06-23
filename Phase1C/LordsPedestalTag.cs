using System.Collections.Generic;
using UnityEngine;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Attached to every Lord's Pedestal instance. Self-registers in a static
    /// list so the RewardSystem can iterate active pedestals in O(N) without
    /// any expensive Physics.OverlapSphere calls.
    /// </summary>
    public class LordsPedestalTag : MonoBehaviour
    {
        public static readonly List<LordsPedestalTag> Active = new List<LordsPedestalTag>();

        public ItemStand Stand { get; private set; }

        private void Awake()
        {
            Stand = GetComponent<ItemStand>();
            Active.Add(this);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }
    }
}
