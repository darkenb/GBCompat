// Corrected bodies for GenesisBook 3.2.3 patch methods, taken from ProjectGenesis 3.2.4 (GPL-3.0), where its authors adapted
// them to game version 0.10.35. Each class mirrors the GenesisBook class of the same name; see Fixes.RedirectAll.
// Modified by darkenb in November 2026: moved into BUG_GBCompat and redirected onto the GenesisBook 3.2.3 methods (GPL-3.0 section 5a).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CommonAPI.Systems;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming

namespace GBCompat
{
    internal static class Upstream
    {
        const sbyte VeinTypeCount = global::ProjectGenesis.Patches.AddVeinPatches.VeinTypeCount;

        public static class AddVeinPatches
        {
            public static IEnumerable<CodeInstruction> PlanetAlgorithm_GenerateVeins_ResizeVeinList_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                // for (int type = 1; type < 15; type++)
                // match with the branch: since 0.10.35 "veinCount * 15 / 100" (underwater oil percentage) comes first
                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)15), Op.Bge);
                matcher.SetOperandAndAdvance(VeinTypeCount);
                return matcher.InstructionEnumeration();
            }

            public static IEnumerable<CodeInstruction> PlanetAlgorithm_GenerateVeins_RemoveVeinPositionBias_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                // if (eVeinType != EVeinType.Oil) pos += birthPoint;
                // match the whole statement: since 0.10.35 an earlier "if (eVeinType == EVeinType.Oil)" exists (underwater oil)
                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldloc_S), new CodeMatch(OpCodes.Ldc_I4_7), Op.Beq,
                    new CodeMatch(OpCodes.Ldloc_S), new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Vector3), "op_Addition")));
                matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Nop)).SetInstructionAndAdvance(new CodeInstruction(OpCodes.Nop))
                    .SetOpcodeAndAdvance(OpCodes.Br_S);
                return matcher.InstructionEnumeration();
            }

            public static IEnumerable<CodeInstruction> PlanetAlgorithm_GenerateVeins_RemoveHeightLimit_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                // float height = data.QueryHeight(pos);
                // if (<height checks>) continue;   (0.10.35 reworked them for underwater oil, subclasses add their own ones)
                // bool tooClose = false;
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(PlanetRawData), nameof(PlanetRawData.QueryHeight))),
                    new CodeMatch(ci => ci.IsStloc()));

                CodeMatcher matcher2 = matcher.Clone();
                matcher2.MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_0), new CodeMatch(OpCodes.Stloc_S));
                Label label = matcher2.Labels.First();

                // 0.10.35+, base algorithm only: vanilla underwater oil, at most kUnderwaterOilPercentage extra oil veins
                // if (eVeinType == EVeinType.Oil && height <= planet.radius - 1f) { underwaterOilCount--; veinCount++; }
                matcher2.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetData), nameof(PlanetData.radius))),
                    new CodeMatch(OpCodes.Ldc_R4, 1f), Op.Sub, Op.BgtUn, new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldc_I4_1), Op.Sub, new CodeMatch(OpCodes.Stloc_S), new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldc_I4_1), Op.Add, new CodeMatch(OpCodes.Stloc_S));

                matcher.Advance(1);
                if (matcher2.IsValid)
                {
                    // oil keeps the vanilla height checks (so the vanilla underwater oil rules apply), other veins skip them:
                    // if (eVeinType != EVeinType.Oil) goto tooClose = false;
                    CodeInstruction ldVeinType = matcher.Clone().MatchForward(false, Op.LdLoc, new CodeMatch(OpCodes.Ldc_I4_7), Op.BneUn).Instruction;
                    matcher.InsertAndAdvance(new CodeInstruction(ldVeinType.opcode, ldVeinType.operand), new CodeInstruction(OpCodes.Ldc_I4_7),
                        new CodeInstruction(OpCodes.Bne_Un, label));
                }
                else
                {
                    // jump from the height store straight to "tooClose = false", skipping every height check
                    matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Br, label));
                }

                // if (planet.waterItemId == 0 || height >= planet.radius ...) data.AddVeinData(vein);  ->  always add
                matcher.MatchForward(true, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetData), nameof(PlanetData.waterItemId))), Op.BrFalse);
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Pop));
                matcher.SetOpcodeAndAdvance(OpCodes.Br);
                return matcher.InstructionEnumeration();
            }
        }

        public static class GammaGeneratorPatches
        {
            public static IEnumerable<CodeInstruction> PowerGeneratorComponent_GameTick_Gamma_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.catalystPoint))),
                    new CodeMatch(OpCodes.Ldc_I4_1), new CodeMatch(OpCodes.Sub));
                // catalystPoint cost * 10
                // since 0.10.35 the proliferator level is settled per catalyst item (catalystCount / catalystInc),
                // so consuming the items 10 times faster also consumes their proliferator points 10 times faster
                matcher.Advance(1).SetAndAdvance(OpCodes.Ldc_I4_S, (sbyte)10);
                return matcher.InstructionEnumeration();
            }
        }

        public static class BirthStarPatches
        {
            public static IEnumerable<CodeInstruction> StarGen_CreateStarPlanets_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                // inited: goto final gen
                matcher.MatchForward(false, Op.Br, new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(StarGen), nameof(StarGen.pGas))));
                object finalLabel = matcher.Operand;
                // BirthStar pGas inited, goto core gen; followed by the check for ESpectrType.M
                matcher.MatchForward(false, Op.Br, new CodeMatch(OpCodes.Ldarg_1),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(StarData), nameof(StarData.spectr))), Op.BrTrue);
                matcher.Operand = finalLabel;
                // insert ModifyBirthStar call; 0.10.35 added gameDesc to PlanetGen.CreatePlanet
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldarg_2), // gameDesc
                    new CodeInstruction(OpCodes.Ldloc_0), // dotNet35Random2
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BirthStarPatches), nameof(ModifyBirthStar))));
                return matcher.InstructionEnumeration();
            }

            // Not public: a helper, not a replacement (GenesisBook 3.2.3's ModifyBirthStar has no gameDesc).
            internal static void ModifyBirthStar(GalaxyData galaxy, StarData star, GameDesc gameDesc, DotNet35Random dotNet35Random2)
            {
                star.planetCount = 5;
                star.planets = new PlanetData[star.planetCount];
                int[][] themes = { new[] { 9 }, new[] { 21 }, new[] { 1 }, new[] { 7 }, new[] { 20 } };
                int[] orbitAround = { 0, 0, 2, 2, 0 }, orbitIndex = { 1, 2, 1, 2, 3 }, number = { 1, 2, 1, 2, 3 };
                for (int i = 0; i < 5; i++)
                {
                    int infoSeed = dotNet35Random2.Next(), genSeed = dotNet35Random2.Next();
                    star.planets[i] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, themes[i], i, orbitAround[i], orbitIndex[i], number[i], i == 1, infoSeed, genSeed);
                }
            }
        }

        public static class UILootFilterPatches
        {
            // vanilla types: 0 item page, 1 building page, 2 enemy drop (UILootFilter.kTypeDrop)
            // tab pages added by TabSystem use their tabIndex (3, 4, ...) as type, and show items of the page with the same index
            const int DropType = 2;
            static readonly FieldInfo currentTypeField = AccessTools.Field(typeof(UILootFilter), nameof(UILootFilter.currentType));

            // GenesisBook's own list: its OnTypeClicked_Postfix (unchanged) selects these tabs.
            static List<UITabButton> Tabs
            {
                get => global::ProjectGenesis.Patches.UILootFilterPatches._tabs;
                set => global::ProjectGenesis.Patches.UILootFilterPatches._tabs = value;
            }

            public static void Create(UILootFilter __instance)
            {
                TabData[] allTabs = TabSystem.GetAllTabs();
                Tabs = new List<UITabButton>();
                // button slots (x = slot * 70 - 54): 1 item, 2 building, then the tab pages, then enemy drop
                var index = 2;
                foreach (TabData tabData in allTabs)
                {
                    if (tabData == null) continue;
                    index = tabData.tabIndex;
                    GameObject gameObject = Object.Instantiate(TabSystem.GetTabPrefab(), __instance.filterTrans, false);
                    ((RectTransform)gameObject.transform).anchoredPosition = new Vector2(index * 70 - 54, -72f);
                    UITabButton component = gameObject.GetComponent<UITabButton>();
                    Sprite newIcon = Resources.Load<Sprite>(tabData.tabIconPath);
                    component.Init(newIcon, tabData.tabName, tabData.tabIndex, __instance.OnTypeButtonClick);
                    Tabs.Add(component);
                }
                // enemy drop button goes after the tab pages
                __instance.typeButton3.transform.localPosition = new Vector3((index + 1) * 70 - 54, -40, 0);
            }

            public static void OnTypeClicked_Prefix(int type) => UILootFilter.showAll = type == DropType;

            static IEnumerable<CodeInstruction> RowLength(IEnumerable<CodeInstruction> instructions, int times)
            {
                var matcher = new CodeMatcher(instructions);
                for (int i = 0; i < times; i++)
                {
                    matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)14));
                    matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                        .SetInstructionAndAdvance(Transpilers.EmitDelegate<Func<UILootFilter, int>>(filter => filter.currentType == DropType ? 14 : 17));
                }
                return matcher.InstructionEnumeration();
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => RowLength(instructions, 2);

            public static IEnumerable<CodeInstruction> TestMouseIndex_Transpiler(IEnumerable<CodeInstruction> instructions) => RowLength(instructions, 4);

            // 3.2.4 merged the three currentType transpilers into one, also applied to _OnUpdate (see Additions)
            public static IEnumerable<CodeInstruction> UILootFilter_OnBoxMouseDown_currentTypeField_Transpiler(IEnumerable<CodeInstruction> instructions) =>
                CurrentType(instructions);

            public static IEnumerable<CodeInstruction> UILootFilter_SetMaterialProps_currentTypeField_Transpiler(IEnumerable<CodeInstruction> instructions) =>
                CurrentType(instructions);

            public static IEnumerable<CodeInstruction> UILootFilter_RefreshIcons_currentTypeField_Transpiler(IEnumerable<CodeInstruction> instructions) =>
                PickPage(CurrentType(instructions));

            // vanilla treats "currentType < 2" as pick filter pages, tab pages have larger types: "currentType != 2" instead
            internal static IEnumerable<CodeInstruction> CurrentType(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                CodeMatch[] matches =
                {
                    new CodeMatch(OpCodes.Ldarg_0), new CodeMatch(OpCodes.Ldfld, currentTypeField), new CodeMatch(OpCodes.Ldc_I4_2),
                    new CodeMatch(i => i.opcode == OpCodes.Blt || i.opcode == OpCodes.Blt_S || i.opcode == OpCodes.Bge || i.opcode == OpCodes.Bge_S),
                };
                matcher.MatchForward(true, matches);
                do
                {
                    OpCode opcode = matcher.Opcode;
                    if (opcode == OpCodes.Blt) opcode = OpCodes.Bne_Un;
                    else if (opcode == OpCodes.Blt_S) opcode = OpCodes.Bne_Un_S;
                    else if (opcode == OpCodes.Bge) opcode = OpCodes.Beq;
                    else opcode = OpCodes.Beq_S;
                    matcher.SetOpcodeAndAdvance(opcode);
                    matcher.MatchForward(true, matches);
                }
                while (matcher.IsValid);
                return matcher.InstructionEnumeration();
            }

            // gridPage == currentType + 1  ->  gridPage == GetPickPage(this)
            static IEnumerable<CodeInstruction> PickPage(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldarg_0), new CodeMatch(OpCodes.Ldfld, currentTypeField), new CodeMatch(OpCodes.Ldc_I4_1), Op.Add);
                matcher.Advance(1).SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(UILootFilterPatches), nameof(GetPickPage)))
                    .SetAndAdvance(OpCodes.Nop, null).SetAndAdvance(OpCodes.Nop, null);
                return matcher.InstructionEnumeration();
            }

            internal static int GetPickPage(UILootFilter filter) => filter.currentType < DropType ? filter.currentType + 1 : filter.currentType;

            public static void RefreshWindow_Postfix(UILootFilter __instance)
            {
                __instance.contentTrans.sizeDelta = __instance.currentType == DropType ? new Vector2(644f, 414f) : new Vector2(782f, 322f);
                bool show = !__instance.showDropOnly;
                foreach (UITabButton uiTabButton in Tabs) uiTabButton.gameObject.SetActive(show);
            }

            public static IEnumerable<CodeInstruction> UILootFilter_SetMaterialProps_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions);
                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_8));
                matcher.SetOpcodeAndAdvance(OpCodes.Ldc_I4_7);
                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 14f));
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                    .SetInstructionAndAdvance(Transpilers.EmitDelegate<Func<UILootFilter, float>>(filter => filter.currentType == DropType ? 14f : 17f));
                return matcher.InstructionEnumeration();
            }
        }

        public static class UIOptionWindowPatches
        {
            public static void UIOptionWindow_OnRevertButtonClick_Postfix(int idx)
            {
                // 4: misc page, -1: revert all (0.10.35+)
                if (idx == 4 || idx == -1) global::ProjectGenesis.Patches.UIOptionWindowPatches.Reset();
            }
        }
    }

    /// <summary>Patches 3.2.4 added on top of 3.2.3's</summary>
    internal static class Additions
    {
        [HarmonyPatch(typeof(UILootFilter), nameof(UILootFilter._OnUpdate)), HarmonyTranspiler]
        static IEnumerable<CodeInstruction> UILootFilter_OnUpdate_currentTypeField_Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Upstream.UILootFilterPatches.CurrentType(instructions);

        /// <summary>0.10.35: when switching games, GameData.Destroy() clears mainPlayer but the camera still renders one more
        /// frame, so vanilla OnCameraPostRender throws a NullReferenceException. Skip that frame.</summary>
        [HarmonyPatch(typeof(SectorModel), nameof(SectorModel.OnCameraPostRender)), HarmonyPrefix]
        static bool SectorModel_OnCameraPostRender_Prefix()
        {
            Player player = GameMain.mainPlayer;
            return player != null && player.controller != null && player.controller.actionBuild != null;
        }

        /// <summary>GenesisBook's PostAddDataAction rebuilds the starmap instancing manager while the main menu demo may already run;
        /// the old one is freed, but the demo's SectorModel keeps it, so the next Dark Fog unit built there throws in
        /// RegisterModelIndex. Hand the new manager to the running sector; its renderers are created on demand.</summary>
        [HarmonyPatch(typeof(GameMain), "CreateStarmapGPUInstancing"), HarmonyPostfix]
        static void GameMain_CreateStarmapGPUInstancing_Postfix()
        {
            SectorModel model = GameMain.data?.spaceSector?.model;
            if (model != null && model.starmapgpuiManager != null) model.starmapgpuiManager = GameMain.starmapgpuiManager;
        }

        const int DarkFogGravitonLensItem = 1211, DarkFogGravitonLensRecipe = 162, AccumulatorFull = 2207;

        [HarmonyPatch(typeof(global::ProjectGenesis.Utils.JsonDataUtils), nameof(global::ProjectGenesis.Utils.JsonDataUtils.ImportJson)), HarmonyPostfix]
        static void JsonDataUtils_ImportJson_Postfix(int[] tableID)
        {
            // 0.10.35: the dark fog graviton lens sits next to the graviton lens in vanilla, where GenesisBook has other protos;
            // move it below the dark fog items (grid 4717 on GenesisBook's second tab)
            int lensGrid = (tableID[1] - 4) * 1000 + 4717;
            if (LDB.items.Exist(DarkFogGravitonLensItem)) LDB.items.Select(DarkFogGravitonLensItem).GridIndex = lensGrid;
            if (LDB.recipes.Exist(DarkFogGravitonLensRecipe)) LDB.recipes.Select(DarkFogGravitonLensRecipe).GridIndex = lensGrid;
            if (LDB.items.Exist(AccumulatorFull)) LDB.items.Select(AccumulatorFull).GridIndex = 2115;
        }
    }
}
