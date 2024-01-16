using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace StatusEffectFilter;

[HarmonyPatch(typeof(Hud), nameof(Hud.UpdateStatusEffects))]
public static class HudUpdateStatusEffectsPatch
{
    static bool Prefix(ref List<StatusEffect> statusEffects)
    {
        var excludedStatusEffects = StatusEffectFilterPlugin.statusEffectConfig.ToggledStringValues();


        // Checking both localized and non-localized names
        statusEffects = statusEffects.Where(effect => !excludedStatusEffects.Contains(effect.m_name) && !excludedStatusEffects.Contains(Localization.instance.Localize(effect.m_name))).ToList();

        return true;
    }
}

public class ToggleStringListConfigEntry
{
    public readonly ConfigEntry<string> BaseConfigEntry;
    public event EventHandler<string[]> SettingChanged;

    readonly AutoCompleteBox _autoCompleteLabel = default;

    public ToggleStringListConfigEntry(ConfigFile config, string section, string key, string defaultValue, string description, Func<IEnumerable<string>> autoCompleteFunc = default)
    {
        BaseConfigEntry = config.BindInOrder(section, key, defaultValue, description, Drawer);
        BaseConfigEntry.SettingChanged += (sender, _) => SettingChanged?.Invoke(sender, ToggledStringValues());

        if (autoCompleteFunc != default)
        {
            _autoCompleteLabel = new(autoCompleteFunc);
        }
    }

    static readonly char[] _valueSeparator = { ',' };
    static readonly char[] _toggleSeperator = { '=' };

    readonly List<string> _valuesCache = new();
    string _valueText = string.Empty;

    public string[] ToggledStringValues()
    {
        _valuesCache.Clear();
        string[] values = BaseConfigEntry.Value.Split(_valueSeparator, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0, count = values.Length; i < count; ++i)
        {
            string[] parts = values[i].Split(_toggleSeperator, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2 && parts[1].Equals("On", StringComparison.OrdinalIgnoreCase))
            {
                _valuesCache.Add(parts[0]);
            }
        }

        return _valuesCache.ToArray();
    }

    public void Drawer(ConfigEntryBase configEntry)
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        _valuesCache.Clear();

        _valuesCache.AddRange(configEntry.BoxedValue.ToString().Split(_valueSeparator, StringSplitOptions.RemoveEmptyEntries));

        GUILayout.BeginHorizontal();
        bool toggleOnClicked = GUILayout.Button("Toggle On", GUILayout.ExpandWidth(true));
        bool toggleOffClicked = GUILayout.Button("Toggle Off", GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();

        bool hasChanged = false;
        int removeIndex = -1;

        for (int i = 0, count = _valuesCache.Count; i < count; ++i)
        {
            string[] parts = _valuesCache[i].Split(_toggleSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
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

            if (result != isToggled)
            {
                hasChanged = true;
                _valuesCache[i] = parts[0] + "=" + (result ? "On" : "Off");
            }
        }

        GUILayout.BeginHorizontal();

        _valueText = GUILayout.TextField(_valueText, GUILayout.ExpandWidth(true));
        GUILayout.Space(3f);

        if (GUILayout.Button("\u002B", GUILayout.MinWidth(40f), GUILayout.ExpandWidth(false)) && !string.IsNullOrWhiteSpace(_valueText) && _valueText.IndexOf('=') < 0)
        {
            _valuesCache.Add(_valueText + "=On");
            _valueText = string.Empty;
            hasChanged = true;
        }


        GUILayout.EndHorizontal();

        if (_autoCompleteLabel != null)
        {
            string result = _autoCompleteLabel.DrawBox(_valueText);

            if (!string.IsNullOrEmpty(result))
            {
                _valuesCache.Add(result + "=On");
                hasChanged = true;
            }
        }

        GUILayout.EndVertical();

        if (removeIndex >= 0)
        {
            _valuesCache.RemoveAt(removeIndex);
            hasChanged = true;
        }

        if (hasChanged)
        {
            configEntry.BoxedValue = string.Join(",", _valuesCache);
        }
    }

    public class AutoCompleteBox
    {
        readonly Func<IEnumerable<string>> _optionsFunc;
        List<string> _options;
        readonly List<string> _currentOptions;
        string _value;
        Vector2 _scrollPosition;
        List<string> _sortedOptionsCache;
        bool _isCacheInitialized = false;

        public AutoCompleteBox(Func<IEnumerable<string>> optionsFunc)
        {
            _optionsFunc = optionsFunc;
            _options = new List<string>();
            _currentOptions = new List<string>();
            _value = string.Empty;
            _scrollPosition = Vector2.zero;
            _sortedOptionsCache = new List<string>();
        }

        private void InitializeCacheIfNeeded()
        {
            if (!_isCacheInitialized)
            {
                _options = new(_optionsFunc());
                _sortedOptionsCache = _options.OrderBy(option => !option.StartsWith(Localization.instance.Localize(option))).ToList();
                _isCacheInitialized = true;
            }
        }

        public string DrawBox(string value)
        {
            InitializeCacheIfNeeded(); // Ensure cache is initialized

            if (_value != value)
            {
                _value = value;
                _currentOptions.Clear();

                // Show all options from the cache if no user input
                // Filter based on user input
                _currentOptions.AddRange(!string.IsNullOrEmpty(value)
                    ? _options.Where(option => option.StartsWith(value, StringComparison.InvariantCultureIgnoreCase))
                    : _sortedOptionsCache);

                _scrollPosition = Vector2.zero;
            }
            else if (string.IsNullOrEmpty(value) && _isCacheInitialized)
            {
                _currentOptions.Clear();
                // Populate _currentOptions with all options from the cache on first run
                _currentOptions.AddRange(_sortedOptionsCache);
            }

            return DrawCurrentOptions();
        }


        string DrawCurrentOptions()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.Height(120f));

            string result = string.Empty;

            foreach (string option in _currentOptions)
            {
                GUILayout.BeginHorizontal();

                var sprite = StatusEffectSpriteManager.Instance.GetSprite(option);
                if (sprite != null)
                {
                    Rect spriteRect = sprite.textureRect;
                    Rect renderRect = GUILayoutUtility.GetRect(spriteRect.width, spriteRect.height);

                    GUI.DrawTextureWithTexCoords(renderRect, sprite.texture, new Rect(spriteRect.x / sprite.texture.width, spriteRect.y / sprite.texture.height, spriteRect.width / sprite.texture.width, spriteRect.height / sprite.texture.height));

                    GUILayout.FlexibleSpace();
                }

                if (GUILayout.Button(option, GUILayout.MinWidth(40f)))
                {
                    result = option;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            return result;
        }
    }
}

public static class ConfigFileExtensions
{
    static readonly Dictionary<string, int> _sectionToSettingOrder = new();

    static int GetSettingOrder(string section)
    {
        if (!_sectionToSettingOrder.TryGetValue(section, out int order))
        {
            order = 0;
        }

        _sectionToSettingOrder[section] = order - 1;
        return order;
    }

    public static ConfigEntry<T> BindInOrder<T>(this ConfigFile config, string section, string key, T defaultValue, string description, Action<ConfigEntryBase> customDrawer = null,
        bool browsable = true, bool hideDefaultButton = false, bool hideSettingName = false)
    {
        return config.Bind(section, key, defaultValue, new ConfigDescription(description, null, new StatusEffectFilterPlugin.ConfigurationManagerAttributes
        {
            Browsable = true,
            CustomDrawer = customDrawer,
            HideDefaultButton = hideDefaultButton,
            Order = GetSettingOrder(section)
        }));
    }
}

public class StatusEffectSpriteManager
{
    private static StatusEffectSpriteManager _instance;
    public static StatusEffectSpriteManager Instance => _instance ?? (_instance = new StatusEffectSpriteManager());

    private Dictionary<string, Sprite> _sprites;

    private StatusEffectSpriteManager()
    {
        LoadSprites();
    }

    public void Initialize()
    {
        LoadSprites();
    }

    private void LoadSprites()
    {
        _sprites = new Dictionary<string, Sprite>();
        foreach (var statusEffect in ObjectDB.m_instance.m_StatusEffects)
        {
            if (statusEffect.m_icon != null && !string.IsNullOrEmpty(statusEffect.m_name))
            {
                string nonLocalized = statusEffect.m_name;
                string localized = Localization.instance.Localize(nonLocalized);
                Sprite icon = statusEffect.m_icon;
                if (!_sprites.ContainsKey(nonLocalized))
                {
                    _sprites[nonLocalized] = icon;
                }

                if (!_sprites.ContainsKey(localized))
                {
                    _sprites[localized] = icon;
                }
            }
        }
    }


    public Sprite GetSprite(string statusEffectName)
    {
        if (_sprites.TryGetValue(statusEffectName, out var sprite))
        {
            return sprite;
        }

        // Try with localized name if the non-localized name doesn't work
        string localized = Localization.instance.Localize(statusEffectName);
        if (_sprites.TryGetValue(localized, out sprite))
        {
            return sprite;
        }

        return null;
    }
}