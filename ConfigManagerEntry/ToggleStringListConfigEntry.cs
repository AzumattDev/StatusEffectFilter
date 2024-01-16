using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace StatusEffectFilter.ConfigManagerEntry;

public abstract class ToggleStringListConfigEntry
{
    internal static AutoCompleteBox AutoCompleteLabel = default;

    private static readonly char[] ValueSeparator = [','];
    private static readonly char[] ToggleSeperator = ['='];

    private static readonly List<string> ValuesCache = [];
    private static string _valueText = string.Empty;

    public static string[] ToggledStringValues()
    {
        return StatusEffectFilterPlugin.ExcludedStatusEffects.Value
            .Split(ValueSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(IsToggledOn)
            .Select(value => value.Split(ToggleSeperator)[0])
            .ToArray();
    }

    private static bool IsToggledOn(string value)
    {
        string[] parts = value.Split(ToggleSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[1].Equals("On", StringComparison.OrdinalIgnoreCase);
    }

    public static void Drawer(ConfigEntryBase configEntry)
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        ValuesCache.Clear();

        ValuesCache.AddRange(configEntry.BoxedValue.ToString().Split(ValueSeparator, StringSplitOptions.RemoveEmptyEntries));

        GUILayout.BeginHorizontal();
        bool toggleOnClicked = GUILayout.Button("Toggle On", GUILayout.ExpandWidth(true));
        bool toggleOffClicked = GUILayout.Button("Toggle Off", GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();

        bool hasChanged = false;
        int removeIndex = -1;

        for (int i = 0, count = ValuesCache.Count; i < count; ++i)
        {
            string[] parts = ValuesCache[i].Split(ToggleSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
            bool isToggled = parts.Length >= 2 && parts[1].Equals("On", StringComparison.OrdinalIgnoreCase);

            GUILayout.BeginHorizontal();

            bool result = GUILayout.Toggle(isToggled, parts[0], GUILayout.ExpandWidth(true));

            if (GUILayout.Button("\u2212", GUILayout.MinWidth(40f), GUILayout.ExpandWidth(false)))
            {
                removeIndex = i;
            }

            GUILayout.EndHorizontal();

            if (toggleOnClicked)
            {
                result = true;
            }
            else if (toggleOffClicked)
            {
                result = false;
            }

            if (result == isToggled) continue;
            hasChanged = true;
            ValuesCache[i] = $"{parts[0]}={(result ? "On" : "Off")}";
        }

        GUILayout.BeginHorizontal();

        _valueText = GUILayout.TextField(_valueText, GUILayout.ExpandWidth(true));
        GUILayout.Space(3f);

        if (GUILayout.Button("\u002B", GUILayout.MinWidth(40f), GUILayout.ExpandWidth(false)) && !string.IsNullOrWhiteSpace(_valueText) && _valueText.IndexOf('=') < 0)
        {
            ValuesCache.Add($"{_valueText}=On");
            _valueText = string.Empty;
            hasChanged = true;
        }


        GUILayout.EndHorizontal();

        if (AutoCompleteLabel != null)
        {
            string result = AutoCompleteLabel.DrawBox(_valueText);

            if (!string.IsNullOrEmpty(result))
            {
                ValuesCache.Add($"{result}=On");
                hasChanged = true;
            }
        }

        GUILayout.EndVertical();

        if (removeIndex >= 0)
        {
            ValuesCache.RemoveAt(removeIndex);
            hasChanged = true;
        }

        if (hasChanged)
        {
            configEntry.BoxedValue = string.Join(",", ValuesCache);
        }
    }
}