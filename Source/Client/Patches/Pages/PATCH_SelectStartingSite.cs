using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameClient.Hooks.TCPNetwork;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.PacketManagers;
using HarmonyLib;
using RimWorld;
using Shared.Misc;
using TCPNetwork;
using UnityEngine.SceneManagement;
using Verse;

namespace GameClient.Patches.Pages
{
    [HarmonyPatch(typeof(Page_SelectStartingSite), "PreOpen")]
    public static class PatchSettlements
    {
        [HarmonyPostfix]
        public static void DoPost()
        {
            try
            {
                if (Current.Game == null || Find.World == null)
                {
                    Printer.Warning("[StartingSite] Skipped patch because game/world was not ready.");
                    return;
                }

                if (!SessionHandler.IsGeneratingFreshWorld)
                {
                    try
                    {
                        PM_World.SetPlanetFeatures();
                    }
                    catch (Exception e)
                    {
                        Printer.Warning($"[StartingSite] Failed setting planet features: {e}");
                    }

                    try
                    {
                        PM_World.SetPlanetFactions();
                    }
                    catch (Exception e)
                    {
                        Printer.Warning($"[StartingSite] Failed setting planet factions: {e}");
                    }

                    try
                    {
                        if (Find.WindowStack != null)
                            Find.WindowStack.Add(new Dialog_AdvancedGameConfig());
                    }
                    catch (Exception e)
                    {
                        Printer.Warning($"[StartingSite] Failed opening Advanced Game Config: {e}");
                    }
                }

                try
                {
                    PlanetManager.BuildPlanet();
                }
                catch (Exception e)
                {
                    Printer.Warning($"[StartingSite] Failed rebuilding planet: {e}");
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[StartingSite] Unexpected PreOpen patch error: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoCustomBottomButtons")]
    public static class PathSelectStartingSitePage
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
        {
            const string disconnectText = "Disconnect";

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo helper = AccessTools.Method(typeof(PathSelectStartingSitePage), nameof(Helper));

            if (helper == null)
            {
                Printer.Warning("[StartingSite] Failed to find disconnect helper.");
                return codes;
            }

            int index = 0;
            for (; index < codes.Count; index++)
            {
                if (codes[index].operand is string str && str == "Back")
                {
                    codes[index] = new CodeInstruction(OpCodes.Ldstr, disconnectText);
                    break;
                }
            }

            bool foundFirst = false;
            for (; index < codes.Count; index++)
            {
                if (codes[index].opcode == OpCodes.Ldarg_0)
                {
                    if (!foundFirst)
                    {
                        foundFirst = true;
                        continue;
                    }

                    codes.InsertRange(index, new[]
                    {
                        new CodeInstruction(OpCodes.Call, helper),
                        new CodeInstruction(OpCodes.Ret)
                    });
                    break;
                }
            }

            return codes;
        }

        private static void Helper()
        {
            try
            {
                Network.ServerEndpoint?.Disconnect();
            }
            catch (Exception e)
            {
                Printer.Warning($"[StartingSite] Failed disconnecting: {e}");
            }

            try
            {
                SceneManager.LoadScene(0);
            }
            catch (Exception e)
            {
                Printer.Warning($"[StartingSite] Failed loading main menu: {e}");
            }
        }
    }
}