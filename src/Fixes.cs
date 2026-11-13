using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace GBCompat
{
    internal static class Fixes
    {
        internal const string For = "3.2.3";
        /// <summary>Patch methods replaced, -1 when nothing was patched</summary>
        internal static int Replaced = -1;
        static readonly Dictionary<MethodBase, MethodInfo> Redirects = new Dictionary<MethodBase, MethodInfo>();

        // Kept apart from Plugin: this method's body names GenesisBook types, which may only be resolved once it is loaded.
        internal static void Apply(Assembly genesis)
        {
            string version = genesis.GetType("ProjectGenesis.ProjectGenesis")?.GetField("VERSION")?.GetValue(null) as string;
            if (version != For)
            {
                Plugin.Log.LogInfo("GenesisBook " + version + " is not " + For + ", nothing patched");
                return;
            }
            var harmony = new Harmony(Plugin.Guid);
            try
            {
                int replaced = RedirectAll(harmony, genesis, typeof(Upstream));
                harmony.PatchAll(typeof(Additions));
                Replaced = replaced;
                Plugin.Log.LogInfo($"GenesisBook {version}: {replaced} patch methods replaced with their corrected versions");
            }
            catch (Exception e)
            {
                // All or nothing: a mix of old and corrected vein patches would generate broken planets.
                Plugin.Log.LogError("GenesisBook " + version + " could not be patched, it is left as it is. " + e);
                // Removing patches rebuilds the methods, which can fail as well; that must not stop plugin loading.
                try { harmony.UnpatchSelf(); } catch (Exception undo) { Plugin.Log.LogError("Removing the partial patches failed: " + undo.Message); }
                Redirects.Clear();
            }
        }

        /// <summary>Each public static method of a class nested in <paramref name="replacements"/> takes over the GenesisBook
        /// method with the same class name, method name and parameters</summary>
        /// <remarks>GenesisBook's own patch methods stay where they are in its patch order; only their bodies change.</remarks>
        internal static int RedirectAll(Harmony harmony, Assembly genesis, Type replacements)
        {
            int count = 0;
            foreach (Type ours in replacements.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type theirs = genesis.GetType("ProjectGenesis.Patches." + ours.Name) ?? throw new TypeLoadException("ProjectGenesis.Patches." + ours.Name);
                foreach (MethodInfo to in ours.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    Type[] parameters = to.GetParameters().Select(p => p.ParameterType).ToArray();
                    MethodInfo from = AccessTools.DeclaredMethod(theirs, to.Name, parameters);
                    if (from == null || !from.IsStatic || from.ReturnType != to.ReturnType) throw new MissingMethodException(theirs.FullName, to.Name);
                    Redirects[from] = to;
                    harmony.Patch(from, transpiler: new HarmonyMethod(typeof(Fixes), nameof(CallReplacement)));
                    count++;
                }
            }
            return count;
        }

        static IEnumerable<CodeInstruction> CallReplacement(MethodBase original)
        {
            MethodInfo to = Redirects[original];
            for (int i = 0; i < to.GetParameters().Length; i++) yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)i);
            yield return new CodeInstruction(OpCodes.Call, to);
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }

    /// <summary>Branch and arithmetic matches for either opcode form</summary>
    internal static class Op
    {
        static CodeMatch Any(params OpCode[] codes) => new CodeMatch(i => codes.Contains(i.opcode));
        internal static CodeMatch Br => Any(OpCodes.Br, OpCodes.Br_S);
        internal static CodeMatch BrTrue => Any(OpCodes.Brtrue, OpCodes.Brtrue_S);
        internal static CodeMatch BrFalse => Any(OpCodes.Brfalse, OpCodes.Brfalse_S);
        internal static CodeMatch Beq => Any(OpCodes.Beq, OpCodes.Beq_S);
        internal static CodeMatch BneUn => Any(OpCodes.Bne_Un, OpCodes.Bne_Un_S);
        internal static CodeMatch Bge => Any(OpCodes.Bge, OpCodes.Bge_S);
        internal static CodeMatch BgtUn => Any(OpCodes.Bgt_Un, OpCodes.Bgt_Un_S);
        internal static CodeMatch Add => Any(OpCodes.Add);
        internal static CodeMatch Sub => Any(OpCodes.Sub);
        internal static CodeMatch LdLoc => new CodeMatch(i => i.IsLdloc());
    }
}
