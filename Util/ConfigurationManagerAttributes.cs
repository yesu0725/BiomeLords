// ReSharper disable InconsistentNaming
// This is the community-standard "ConfigurationManagerAttributes" class.
// Jotunn's SynchronizationManager looks for IsAdminOnly via duck-typing on this
// class name, so the namespace doesn't matter — only that the class exists with
// IsAdminOnly as a public bool.

using System;

namespace BiomeLords.Util
{
    public sealed class ConfigurationManagerAttributes
    {
        /// <summary>When true, Jotunn pushes the server's value to clients on connect
        /// and locks client overrides. Only server admins can change it.</summary>
        public bool? IsAdminOnly;

        public bool? Browsable;
        public string Category;
        public string DispName;
        public int? Order;
        public bool? ReadOnly;
        public Action<global::BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
    }
}
