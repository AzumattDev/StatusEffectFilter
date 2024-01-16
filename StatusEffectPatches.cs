using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace StatusEffectFilter;

[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateStatusEffects))]
public static class HudUpdateStatusEffectsPatch
{
    static bool Prefix(ref List<StatusEffect> statusEffects)
    {
        statusEffects = FilterStatusEffects(statusEffects);
        return true;
    }

    private static List<StatusEffect> FilterStatusEffects(List<StatusEffect> originalEffects)
    {
        string[] excludedStatusEffects = StatusEffectFilterPlugin.ExcludedStatusEffects.Value
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.EndsWith("=On"))
            .Select(s => s.Split('=')[0])
            .ToArray();

        return originalEffects.Where(effect => !excludedStatusEffects.Contains(effect.m_name) && !excludedStatusEffects.Contains(Localization.instance.Localize(effect.m_name))).ToList();
    }
}