using HarmonyLib;

namespace Krimson_AutoCollect;

[HarmonyPatch(typeof(TrainingSlot), "TrainingEffect")]
public static class TrainingSlot_TrainingEffect_AutoCollectPatch
{
    private static void Postfix(TrainingSlot __instance)
    {
        if (__instance == null) return;
        if (!AutoCollectPlugin.EnableTrainingCollect.Value) return;
        if (!AutoCollectUtility.IsTrainingSlotReady(__instance)) return;

        try
        {
            if (AutoCollectUtility.TryCompleteTrainingSlot(__instance))
                AutoCollectUtility.CleanupTrainingSlotIcon(__instance);
        }
        catch { }
    }
}