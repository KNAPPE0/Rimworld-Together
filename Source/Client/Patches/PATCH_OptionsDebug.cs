using HarmonyLib;
using RimWorld;
using Shared.Misc;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Pages
{
    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(Dialog_Options), "DoWindowContents")]
    public static class Patch_Dialog_Options_DebugDump
    {
        private static float LastDumpTime = -999f;

        [HarmonyPostfix]
        public static void Postfix(Dialog_Options __instance, Rect inRect)
        {
            if (Current.ProgramState != ProgramState.Entry) return;
            if (__instance == null) return;

            if (Event.current.type != EventType.MouseDown) return;
            if (Time.realtimeSinceStartup - LastDumpTime < 0.75f) return;
            LastDumpTime = Time.realtimeSinceStartup;

            Printer.Warning("[OptionsDebug] ---- Dialog_Options field dump start ----");

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in __instance.GetType().GetFields(flags))
            {
                try
                {
                    object value = field.GetValue(__instance);
                    string text = value == null ? "null" : value.ToString();
                    Printer.Warning($"[OptionsDebug] FIELD {field.Name} | TYPE {field.FieldType.FullName} | VALUE {text}");
                }
                catch (Exception e)
                {
                    Printer.Warning($"[OptionsDebug] FIELD {field.Name} | ERROR {e.Message}");
                }
            }

            foreach (PropertyInfo prop in __instance.GetType().GetProperties(flags))
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                try
                {
                    object value = prop.GetValue(__instance, null);
                    string text = value == null ? "null" : value.ToString();
                    Printer.Warning($"[OptionsDebug] PROP {prop.Name} | TYPE {prop.PropertyType.FullName} | VALUE {text}");
                }
                catch
                {
                }
            }

            Printer.Warning("[OptionsDebug] ---- Dialog_Options field dump end ----");
        }
    }
}