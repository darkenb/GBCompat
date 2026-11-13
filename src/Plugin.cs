// BUG_GBCompat: GenesisBook 3.2.3 fixes for game version 0.10.35, applied at runtime.
// Derived from ProjectGenesis (创世之书) by the GenesisBook authors, licensed under GPL-3.0; this file and the rest of this mod
// are under the same licence. See README.md and LICENSE.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;

[assembly: InternalsVisibleTo("Check")]
[assembly: AssemblyVersion(GBCompat.Plugin.Version + ".0")]
[assembly: AssemblyFileVersion(GBCompat.Plugin.Version + ".0")]
[assembly: AssemblyInformationalVersion(GBCompat.Plugin.Version)]

namespace GBCompat
{
    // GenesisBook applies its patches in its own Awake, so the corrected ones must be in place before that. The "aaa" GUID
    // sorts first in BepInEx's load order and there is deliberately no BepInDependency on GenesisBook: with one, this
    // plugin would load after it. GenesisBook's DLL is loaded right before its Awake, which is when the fixes go in.
    [BepInPlugin(Guid, "BUG_GBCompat", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "aaa.bug.dsp.gbcompat", Version = "1.0.0";
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (IsGenesisBook(assembly)) { Log.LogError("GenesisBook was loaded before BUG_GBCompat, so its patches are already in place; nothing changed"); return; }
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs e)
        {
            if (!IsGenesisBook(e.LoadedAssembly)) return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            Fixes.Apply(e.LoadedAssembly);
        }

        static bool IsGenesisBook(Assembly assembly) => assembly.GetName().Name == "ProjectGenesis";
    }
}
