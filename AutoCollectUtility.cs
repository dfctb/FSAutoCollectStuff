using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Krimson_AutoCollect;

public static class AutoCollectUtility
{
    private static readonly string[] LevelUpPanelTypeMemberNames = new string[8] {
        "PanelType", "panelType", "IconPanelType", "iconPanelType",
        "m_panelType", "m_iconPanelType", "LevelUpPanelType", "levelUpPanelType"
    };

    // ---------- Training slots ----------
    public static IEnumerable<TrainingSlot> GetTrainingSlots(TrainingRoom room)
    {
        if (room == null) yield break;
        var found = new HashSet<TrainingSlot>();
        var type = room.GetType();
        while (type != null)
        {
            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object v; try { v = f.GetValue(room); } catch { continue; }
                foreach (var s in ExtractTrainingSlots(v))
                    if (s != null && found.Add(s)) yield return s;
            }
            foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (p.GetIndexParameters().Length != 0) continue;
                object v; try { v = p.GetValue(room, null); } catch { continue; }
                foreach (var s in ExtractTrainingSlots(v))
                    if (s != null && found.Add(s)) yield return s;
            }
            type = type.BaseType;
        }
    }

    private static IEnumerable<TrainingSlot> ExtractTrainingSlots(object value)
    {
        if (value == null) yield break;
        if (value is TrainingSlot ts) { yield return ts; yield break; }
        if (!(value is IEnumerable e) || value is string) yield break;
        foreach (var o in e) if (o is TrainingSlot t) yield return t;
    }

    // ---------- Ready checks ----------
    public static bool IsExperienceReadyForLevelUp(object experience)
    {
        if (experience == null) return false;
        return ReadAnyBool(experience,
            "NeedsLevelUp", "needsLevelUp", "m_needsLevelUp", "m_needsLvlUp",
            "m_needLevelUp", "NeedLevelUp", "ReadyToLevelUp", "CanLevelUp");
    }

    public static bool IsDwellerLevelUpReady(Dweller d) =>
        d != null && d.Experience != null && IsExperienceReadyForLevelUp(d.Experience);

    public static bool IsTrainingSlotReady(TrainingSlot slot)
    {
        if (slot == null) return false;
        return ReadAnyBool(slot,
            "NeedsLevelUp", "needsLevelUp", "m_needsLevelUp", "m_needsLvlUp",
            "m_needLevelUp", "Ready", "IsReady", "Completed", "IsCompleted");
    }

    public static bool IsDwellerReadyToDeliverBaby(Dweller d)
    {
        if (d == null) return false;
        try { return d.Pregnant && d.BabyReady; }
        catch { return ReadAnyBool(d, "BabyReady", "babyReady", "m_babyReady"); }
    }

    // ---------- Collection with fallbacks ----------

    /// <summary>
    /// Пытается собрать level-up жителя. Сначала пробует IconPanelCallBack,
    /// затем LevelUP напрямую (обход заблокированного FSQuestLoot префикса).
    /// </summary>
    public static bool TryCollectDwellerLevelUp(Dweller dweller)
    {
        if (dweller == null || !IsDwellerLevelUpReady(dweller)) return false;

        // 1) Обычный путь
        if (InvokeAnyVoidOrReturn(dweller, "IconPanelCallBack", "IconPanelCallback"))
            if (!IsDwellerLevelUpReady(dweller)) return true;

        if (dweller.Experience != null)
        {
            if (InvokeAnyVoidOrReturn(dweller.Experience, "IconPanelCallBack", "IconPanelCallback"))
                if (!IsDwellerLevelUpReady(dweller)) return true;

            // 2) Прямой LevelUP — обходит префиксы FSQuestLoot
            try
            {
                dweller.Experience.LevelUP();
                if (!IsDwellerLevelUpReady(dweller)) return true;
            }
            catch { }

            // 3) Рефлексия по альтернативным именам
            if (InvokeAnyVoidOrReturn(dweller.Experience,
                    "LevelUP", "LevelUp", "ApplyLevelUp", "DoLevelUp", "OnLevelUp"))
                if (!IsDwellerLevelUpReady(dweller)) return true;
        }

        return !IsDwellerLevelUpReady(dweller);
    }

    /// <summary>
    /// Пытается завершить тренировку. Аналогично обходит блокировки.
    /// </summary>
    public static bool TryCompleteTrainingSlot(TrainingSlot slot)
    {
        if (slot == null || !IsTrainingSlotReady(slot)) return false;

        // 1) Стандартный путь
        try
        {
            slot.IconPanelCallBack();
            if (!IsTrainingSlotReady(slot)) return true;
        }
        catch { }

        // 2) Методы самого слота
        if (InvokeAnyVoidOrReturn(slot,
                "LevelUP", "LevelUp", "Complete", "Finish",
                "TrainingComplete", "OnTrainingComplete", "ConsumeResult"))
            if (!IsTrainingSlotReady(slot)) return true;

        // 3) Через жителя
        var dweller = GetAnyMemberValue(slot,
            "Dweller", "dweller", "m_dweller",
            "AssignedDweller", "assignedDweller", "m_assignedDweller");
        if (dweller != null)
        {
            if (InvokeAnyVoidOrReturn(dweller, "IconPanelCallBack", "IconPanelCallback"))
                if (!IsTrainingSlotReady(slot)) return true;

            var exp = GetAnyMemberValue(dweller, "Experience", "experience", "m_experience");
            if (exp != null)
            {
                if (InvokeAnyVoidOrReturn(exp, "LevelUP", "LevelUp"))
                    if (!IsTrainingSlotReady(slot)) return true;
            }
        }

        return !IsTrainingSlotReady(slot);
    }

    /// <summary>
    /// Запускает иконку готовых родов, с несколькими fallback-ами.
    /// </summary>
    public static bool TryTriggerDwellerBabyIcon(Dweller dweller)
    {
        if (dweller == null) return false;

        try { dweller.TryTriggerIconPanel(); return true; } catch { }

        if (InvokeAnyVoidOrReturn(dweller,
                "TryTriggerIconPanel", "TriggerIconPanel",
                "IconPanelCallBack", "IconPanelCallback"))
            return true;

        if (InvokeAnyVoidOrReturn(dweller,
                "DoBirth", "Birth", "DeliverBaby", "Deliver", "OnBabyReady"))
            return true;

        return false;
    }

    public static bool TryDeliverBaby(Dweller dweller) =>
        IsDwellerReadyToDeliverBaby(dweller) && TryTriggerDwellerBabyIcon(dweller);

    // ---------- Cleanup ----------
    public static void CleanupDwellerLevelUpIcon(Dweller dweller)
    {
        if (dweller == null || dweller.Experience == null) return;
        WriteAnyBool(dweller.Experience, false,
            "NeedsLevelUp", "needsLevelUp", "m_needsLevelUp", "m_needsLvlUp",
            "m_needLevelUp", "NeedLevelUp", "ReadyToLevelUp", "CanLevelUp");
        var panelType = GetLevelUpPanelType(dweller);
        ClearAllLikelyDwellerIconPanels(dweller, panelType);
        InvokeAny(dweller.Experience, "Refresh", "RefreshIconPanel", "UpdateIconPanel", "UpdateIconPanels");
        InvokeAny(dweller, "Refresh", "RefreshIconPanel", "UpdateIconPanel", "UpdateIconPanels", "UpdateIcons");
    }

    public static void CleanupTrainingSlotIcon(TrainingSlot slot)
    {
        if (slot == null) return;
        WriteAnyBool(slot, false,
            "NeedsLevelUp", "needsLevelUp", "m_needsLevelUp", "m_needsLvlUp",
            "m_needLevelUp", "Ready", "IsReady", "Completed", "IsCompleted");
        var dweller = GetAnyMemberValue(slot,
            "Dweller", "dweller", "m_dweller",
            "AssignedDweller", "assignedDweller", "m_assignedDweller");
        var panelType = InvokeAny(slot, "GetPanelType", "get_PanelType", "GetIconPanelType");
        if (dweller != null && panelType != null)
            InvokeWithOneArg(dweller, panelType, "RemoveIconPanel", "RemovePanel", "HideIconPanel", "ClearIconPanel");
        InvokeAny(slot, "Refresh", "UpdateIconPanel", "RefreshIconPanel", "RemoveIconPanel", "HideIconPanel");
        InvokeAny(dweller, "Refresh", "UpdateIconPanel", "RefreshIconPanel", "UpdateIconPanels");
    }

    public static object GetLevelUpPanelType(Dweller dweller)
    {
        if (dweller == null) return null;
        if (dweller.Experience != null)
        {
            var v = InvokeAny(dweller.Experience, "GetPanelType", "get_PanelType", "GetIconPanelType", "get_IconPanelType");
            if (v != null) return v;
            v = GetAnyMemberValue(dweller.Experience, LevelUpPanelTypeMemberNames);
            if (v != null) return v;
        }
        var v2 = InvokeAny(dweller, "GetPanelType", "get_PanelType", "GetIconPanelType", "get_IconPanelType");
        return v2 ?? GetAnyMemberValue(dweller, LevelUpPanelTypeMemberNames);
    }

    public static void ClearAllLikelyDwellerIconPanels(Dweller dweller, object panelType)
    {
        if (dweller == null) return;
        if (panelType != null)
        {
            InvokeAnyWithArgs(dweller, new object[] { panelType },
                "RemoveIconPanel", "RemovePanel", "HideIconPanel", "ClearIconPanel", "RemoveIcon", "HideIcon");
            InvokeAnyWithArgs(dweller, new object[] { panelType, true },
                "RemoveIconPanel", "RemovePanel", "HideIconPanel", "ClearIconPanel");
            InvokeAnyWithArgs(dweller, new object[] { panelType, false },
                "RemoveIconPanel", "RemovePanel", "HideIconPanel", "ClearIconPanel");
        }
        InvokeAny(dweller,
            "RemoveIconPanel", "RemovePanel", "HideIconPanel", "ClearIconPanel",
            "RemoveLevelUpIcon", "HideLevelUpIcon", "ClearLevelUpIcon",
            "Refresh", "RefreshIconPanel", "RefreshIconPanels",
            "UpdateIconPanel", "UpdateIconPanels", "UpdateIcons");
        if (dweller.Experience != null)
            InvokeAny(dweller.Experience,
                "RemoveIconPanel", "RemovePanel", "HideIconPanel", "ClearIconPanel",
                "RemoveLevelUpIcon", "HideLevelUpIcon", "ClearLevelUpIcon",
                "Refresh", "RefreshIconPanel", "RefreshIconPanels",
                "UpdateIconPanel", "UpdateIconPanels", "UpdateIcons");
    }

    // ---------- Reflection helpers ----------
    public static bool ReadAnyBool(object instance, params string[] names)
    {
        if (instance == null) return false;
        foreach (var n in names)
        {
            var v = GetAnyMemberValue(instance, n);
            if (v is bool b) return b;
        }
        return false;
    }

    public static void WriteAnyBool(object instance, bool value, params string[] names)
    {
        if (instance == null) return;
        var t = instance.GetType();
        while (t != null)
        {
            foreach (var n in names)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool))
                    try { f.SetValue(instance, value); } catch { }
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(bool) && p.GetIndexParameters().Length == 0 && p.CanWrite)
                    try { p.SetValue(instance, value, null); } catch { }
            }
            t = t.BaseType;
        }
    }

    public static object GetAnyMemberValue(object instance, params string[] names)
    {
        if (instance == null || names == null) return null;
        var t = instance.GetType();
        while (t != null)
        {
            foreach (var n in names)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.GetIndexParameters().Length == 0)
                    try { return p.GetValue(instance, null); } catch { }
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                    try { return f.GetValue(instance); } catch { }
            }
            t = t.BaseType;
        }
        return null;
    }

    public static object InvokeAny(object instance, params string[] methodNames)
    {
        if (instance == null || methodNames == null) return null;
        var t = instance.GetType();
        while (t != null)
        {
            foreach (var n in methodNames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m != null)
                    try { return m.Invoke(instance, null); } catch { }
            }
            t = t.BaseType;
        }
        return null;
    }

    public static bool InvokeAnyWithArgs(object instance, object[] args, params string[] methodNames)
    {
        if (instance == null || args == null || methodNames == null) return false;
        var t = instance.GetType();
        while (t != null)
        {
            foreach (var n in methodNames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != n) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != args.Length) continue;
                    bool ok = true;
                    for (int i = 0; i < ps.Length; i++)
                        if (args[i] != null && !ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
                    if (!ok) continue;
                    try { m.Invoke(instance, args); return true; } catch { }
                }
            }
            t = t.BaseType;
        }
        return false;
    }

    public static bool InvokeWithOneArg(object instance, object arg, params string[] methodNames)
    {
        if (instance == null || arg == null || methodNames == null) return false;
        var t = instance.GetType();
        while (t != null)
        {
            foreach (var n in methodNames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != n) continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    try
                    {
                        if (!ps[0].ParameterType.IsAssignableFrom(arg.GetType())) continue;
                        m.Invoke(instance, new object[] { arg });
                        return true;
                    }
                    catch { }
                }
            }
            t = t.BaseType;
        }
        return false;
    }

    public static bool InvokeAnyVoidOrReturn(object instance, params string[] methodNames)
    {
        if (instance == null || methodNames == null) return false;
        var t = instance.GetType();
        while (t != null)
        {
            foreach (var n in methodNames)
            {
                if (string.IsNullOrEmpty(n)) continue;
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (m != null)
                    try { m.Invoke(instance, null); return true; } catch { }
            }
            t = t.BaseType;
        }
        return false;
    }
}