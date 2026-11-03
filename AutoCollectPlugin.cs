using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Krimson_AutoCollect;

[BepInPlugin("krimson.fos.autocollect", "Krimson Auto Collect", "2.0.0")]
public class AutoCollectPlugin : BaseUnityPlugin
{
    public static AutoCollectPlugin Instance;
    public static ConfigEntry<bool> EnableDwellerLevelCollect;
    public static ConfigEntry<bool> EnableTrainingCollect;
    public static ConfigEntry<bool> EnablePregnancyCollect;
    public static ConfigEntry<float> ScanIntervalSeconds;
    public static ConfigEntry<bool> LogVerbose;

    internal static float CollectionTimer;
    private static float _cacheTimer;
    private const float CacheRefreshInterval = 30f;
    private static readonly List<TrainingRoom> _trainingRooms = new List<TrainingRoom>();

    private void Awake()
    {
        Instance = this;
        EnableDwellerLevelCollect = Config.Bind("Auto Collect", "EnableDwellerLevelCollect", true, "Automatically collect ready dweller level-ups.");
        EnableTrainingCollect = Config.Bind("Auto Collect", "EnableTrainingCollect", true, "Automatically collect completed training levels.");
        EnablePregnancyCollect = Config.Bind("Auto Collect", "EnablePregnancyCollect", true, "Automatically deliver ready pregnancies.");
        ScanIntervalSeconds = Config.Bind("Timing", "ScanIntervalSeconds", 5f, "How often the auto collect system scans the vault. Minimum is 1 second.");
        LogVerbose = Config.Bind("Debug", "LogVerbose", false, "Log each collected item.");

        new Harmony("krimson.fos.autocollect").PatchAll();

        // Предупреждение, если оригинальный плагин тоже загружен
        try
        {
            var conflict = Type.GetType("Krimson_AutoCollect_Compat.CompatAutoCollectPlugin, Krimson_AutoCollect_Compat");
            if (conflict != null)
                Logger.LogWarning("Detected a compat version of Krimson Auto Collect. Disable one of them to avoid double-collection!");
        }
        catch { }

        Logger.LogInfo("Krimson Auto Collect v2 (compat) loaded.");
    }

    internal static void RunAutoCollectTick(float elapsed)
    {
        float interval = ScanIntervalSeconds.Value;
        if (interval < 1f) interval = 1f;

        CollectionTimer += elapsed;
        _cacheTimer += elapsed;

        if (_cacheTimer >= CacheRefreshInterval)
        {
            _cacheTimer = 0f;
            RefreshTrainingRoomCache();
        }

        if (CollectionTimer < interval) return;
        CollectionTimer = 0f;

        try
        {
            if (EnableDwellerLevelCollect.Value) CollectReadyDwellerLevels();
            if (EnableTrainingCollect.Value) CollectReadyTrainingLevels();
            if (EnablePregnancyCollect.Value) DeliverReadyPregnancies();
        }
        catch (Exception e)
        {
            if (Instance != null) Instance.Logger.LogError($"Auto collect tick failed: {e}");
        }
    }

    private static void RefreshTrainingRoomCache()
    {
        try
        {
            var found = UnityEngine.Object.FindObjectsByType<TrainingRoom>(FindObjectsSortMode.None);
            _trainingRooms.Clear();
            foreach (var r in found) if (r != null) _trainingRooms.Add(r);
        }
        catch { }
    }

    private static void CollectReadyDwellerLevels()
    {
        if (!MonoSingleton<DwellerManager>.IsInstanceValid) return;
        foreach (var d in MonoSingleton<DwellerManager>.Instance.Dwellers)
        {
            if (d == null || d.Experience == null) continue;
            if (!AutoCollectUtility.IsDwellerLevelUpReady(d)) continue;
            try
            {
                if (AutoCollectUtility.TryCollectDwellerLevelUp(d))
                {
                    AutoCollectUtility.CleanupDwellerLevelUpIcon(d);
                    if (LogVerbose.Value) Instance.Logger.LogInfo("Collected dweller level-up.");
                }
            }
            catch (Exception e) { if (LogVerbose.Value) Instance.Logger.LogWarning($"Dweller collect failed: {e.Message}"); }
        }
    }

    private static void CollectReadyTrainingLevels()
    {
        if (_trainingRooms.Count == 0) RefreshTrainingRoomCache();
        foreach (var room in _trainingRooms)
        {
            if (room == null) continue;
            foreach (var slot in AutoCollectUtility.GetTrainingSlots(room))
            {
                if (slot == null || !AutoCollectUtility.IsTrainingSlotReady(slot)) continue;
                try
                {
                    if (AutoCollectUtility.TryCompleteTrainingSlot(slot))
                    {
                        AutoCollectUtility.CleanupTrainingSlotIcon(slot);
                        if (LogVerbose.Value) Instance.Logger.LogInfo("Collected training slot.");
                    }
                }
                catch (Exception e) { if (LogVerbose.Value) Instance.Logger.LogWarning($"Training collect failed: {e.Message}"); }
            }
        }
    }

    private static void DeliverReadyPregnancies()
    {
        if (!MonoSingleton<DwellerManager>.IsInstanceValid) return;
        int ready = 0, ok = 0;
        foreach (var d in MonoSingleton<DwellerManager>.Instance.Dwellers)
        {
            if (d == null || !AutoCollectUtility.IsDwellerReadyToDeliverBaby(d)) continue;
            ready++;
            try { if (AutoCollectUtility.TryDeliverBaby(d)) ok++; }
            catch (Exception e) { if (LogVerbose.Value) Instance.Logger.LogWarning($"Birth failed: {e.Message}"); }
        }
        if (ready > 0 && LogVerbose.Value)
            Instance.Logger.LogInfo($"Pregnancy: ready={ready}, triggered={ok}");
    }
}