﻿using System.Collections.Generic;
using System.Linq;
using ADV;
using ADV.Commands.Chara;
using ADV.Commands.Game;
using BepInEx.Logging;
using ChaCustom;
using GeBoCommon.Utilities;
using HarmonyLib;
using Manager;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = ADV.Commands.Base.Text;

namespace GameDressForSuccessPlugin
{
    partial class GameDressForSuccess
    {
        internal class Hooks
        {
            private const string TogglePrefix = "tglCoorde";
            private const string AutoToggleName = TogglePrefix + "00";


            internal static ManualLogSource Logger => Instance?.Logger;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MapChange), "Do")]
            internal static void StartTravelingHook(MapChange __instance)
            {
                if (!Enabled.Value || Instance == null) return;
                __instance.SafeProc(
                    i => i.scenario.SafeProc(
                        s => s.currentHeroine.SafeProc(h => Instance.TravelingStart(h))));
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ADV.Commands.Effect.SceneFade), "Do")]
            [HarmonyPatch(typeof(Text), "Do")]
            internal static void StopTravelingHook(CommandBase __instance)
            {
                if (!Enabled.Value || Instance == null) return;
                __instance.SafeProc(
                    i => i.scenario.SafeProc(
                        s => s.currentHeroine.SafeProc(h => Instance.TravelingDone(h))));
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Coordinate), "Do")]
            internal static void CoordinateDoPostfix(Coordinate __instance)
            {
                Logger.DebugLogDebug($"CoordinateDoPostfix: monitoringChange={Instance?._monitoringChange}");
                if (Instance == null || !Enabled.Value || !Instance._monitoringChange || __instance == null) return;


                var typeField = Traverse.Create(__instance).Field("type");

                if (!typeField.FieldExists()) return;

                Instance?.DressPlayer(typeField.GetValue<ChaFileDefine.CoordinateType>());
            }

            #region Right Click Clothing Support

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Toggle), nameof(Toggle.OnPointerClick))]
            internal static void ToggleOnPointerClickPrefix(Toggle __instance,
                ref PointerEventData eventData, out Toggle __state)
            {
                Logger.DebugLogDebug("ToggleOnPointerClickPrefix");
                __state = null;
                if (!Enabled.Value ||
                    eventData.button != PointerEventData.InputButton.Right ||
                    __instance == null || __instance.name == null || !__instance.name.StartsWith(TogglePrefix))
                {
                    return;
                }

                // everything after this point will fire the default left-click event
                eventData.button = PointerEventData.InputButton.Left;


                if (__instance.name == AutoToggleName) return;

                var autoEnabled = (CustomBase.IsInstance() &&
                                   Singleton<CustomBase>.Instance.autoClothesState) ||
                                  (Game.IsInstance() &&
                                   Singleton<Game>.Instance.Player.changeClothesType < 0);

                if (!autoEnabled) return;

                var togglesField = Traverse.Create(__instance.group).Field("m_Toggles");

                __state = togglesField.FieldExists()
                    ? togglesField.GetValue<List<Toggle>>().FirstOrDefault(t => t.name == AutoToggleName)
                    : null;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Toggle), nameof(Toggle.OnPointerClick))]
            internal static void ToggleOnPointerClickPostfix(Toggle __state)
            {
                Logger.DebugLogDebug("ToggleOnPointerClickPostfix");
                if (__state == null) return;
                // if we get here, click the automatic button afterwards
                __state.OnSubmit(null);
            }

            #endregion

            /*
            [HarmonyPrefix]
            [HarmonyPatch(typeof(CommandBase), "Do")]
            internal static void CommandBaseDoPrefix(CommandBase __instance)
            {
                var variable = AccessTools.Field(__instance.GetType(), "variable")?.GetValue(__instance) as string;
                var value = AccessTools.Field(__instance.GetType(), "value")?.GetValue(__instance) as string;
                Logger?.LogError(
                    $"{__instance}: scenario={__instance.scenario}, variable={variable}, value={value}");
            }
            */
        }
    }
}
