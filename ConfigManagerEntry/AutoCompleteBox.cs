using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StatusEffectFilter.ConfigManagerEntry;

public class AutoCompleteBox
{
    private static Func<IEnumerable<string>> _optionsFunc = null!;
    private static List<string> _options = null!;
    private readonly List<string> _currentOptions;
    private string _value;
    private Vector2 _scrollPosition;
    private static List<string> _sortedOptionsCache = null!;
    private static bool _isCacheInitialized = false;

    public AutoCompleteBox(Func<IEnumerable<string>> optionsFunc)
    {
        _optionsFunc = optionsFunc;
        _options = [.._optionsFunc()];
        _currentOptions = [.._options];
        _value = string.Empty;
        _scrollPosition = Vector2.zero;
        _sortedOptionsCache = [.._options.OrderBy(option => !option.StartsWith(Localization.instance.Localize(option)))];
        _isCacheInitialized = true;
    }

    internal static void InitializeCacheIfNeeded()
    {
        if (_isCacheInitialized) return;
        _options = [.._optionsFunc()];
        _sortedOptionsCache = _options.OrderBy(option => !option.StartsWith(Localization.instance.Localize(option))).ToList();
        _isCacheInitialized = true;
    }

    public string DrawBox(string value)
    {
        if (_value != value)
        {
            _value = value;
            UpdateCurrentOptions();

            return DrawCurrentOptions();
        }

        return DrawCurrentOptions();
    }

    private void UpdateCurrentOptions()
    {
        _currentOptions.Clear();
        IEnumerable<string> optionsToDisplay = string.IsNullOrEmpty(_value) ? _sortedOptionsCache : FilterOptions(_value);
        _currentOptions.AddRange(optionsToDisplay);
        _scrollPosition = Vector2.zero;
    }

    private IEnumerable<string> FilterOptions(string filter)
    {
        return _options.Where(option => option.StartsWith(filter, StringComparison.InvariantCultureIgnoreCase));
    }

    private string DrawCurrentOptions()
    {
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.Height(250f));

        string result = string.Empty;

        foreach (string option in _currentOptions)
        {
            GUILayout.BeginHorizontal();

            Sprite? sprite = StatusEffectSpriteManager.Instance.GetSprite(option);
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