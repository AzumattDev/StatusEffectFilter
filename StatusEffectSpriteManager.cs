using System.Collections.Generic;
using UnityEngine;

namespace StatusEffectFilter;

public class StatusEffectSpriteManager
{
    private static StatusEffectSpriteManager _instance = null!;
    public static StatusEffectSpriteManager Instance => _instance ??= new StatusEffectSpriteManager();

    private Dictionary<string, Sprite> _sprites = null!;

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
        foreach (StatusEffect? statusEffect in ObjectDB.m_instance.m_StatusEffects)
        {
            AddSprite(statusEffect.m_name, statusEffect.m_icon);
            AddSprite(Localization.instance.Localize(statusEffect.m_name), statusEffect.m_icon);
        }
    }

    private void AddSprite(string name, Sprite icon)
    {
        if (!string.IsNullOrEmpty(name) && icon != null && !_sprites.ContainsKey(name))
        {
            _sprites[name] = icon;
        }
    }


    public Sprite GetSprite(string statusEffectName)
    {
        if (_sprites.TryGetValue(statusEffectName, out Sprite? sprite))
        {
            return sprite;
        }

        // Try with localized name if the non-localized name doesn't work
        string localized = Localization.instance.Localize(statusEffectName);
        return _sprites.TryGetValue(localized, out sprite) ? sprite : null;
    }
}