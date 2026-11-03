using HarmonyLib;
using UnityEngine;

namespace Krimson_AutoCollect;

[HarmonyPatch(typeof(Vault), "Update")]
public static class Vault_Update_AutoCollectPatch
{
    private static void Postfix()
    {
        if (AutoCollectPlugin.Instance == null) return;
        if (!(AutoCollectPlugin.EnableDwellerLevelCollect.Value
              || AutoCollectPlugin.EnableTrainingCollect.Value
              || AutoCollectPlugin.EnablePregnancyCollect.Value))
            return;

        AutoCollectPlugin.RunAutoCollectTick(Time.deltaTime);
    }
}