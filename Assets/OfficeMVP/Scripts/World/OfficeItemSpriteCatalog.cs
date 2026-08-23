using System.Collections.Generic;
using UnityEngine;

public static class OfficeItemSpriteCatalog
{
    private static readonly Dictionary<string, string> ItemSpritePaths = new Dictionary<string, string>
    {
        { "americano_coupon", "OfficeMVP/Items/americano_coupon" },
        { "department_store_voucher", "OfficeMVP/Items/department_store_voucher" },
        { "luxury_handbag", "OfficeMVP/Items/luxury_handbag" },
    };

    private static readonly Dictionary<string, string> PersonSpritePaths = new Dictionary<string, string>
    {
        { "representative_person", "OfficeMVP/Items/representative_person" },
        { "team_leader_person", "OfficeMVP/Items/team_leader_person" },
        { "division_head_person", "OfficeMVP/Items/division_head_person" },
    };

    private static readonly Dictionary<string, Sprite> CachedSprites = new Dictionary<string, Sprite>();

    public static Sprite Load(string objectId)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            return null;
        }

        if (CachedSprites.TryGetValue(objectId, out var cached))
        {
            return cached;
        }

        Sprite sprite = null;
        if (ItemSpritePaths.TryGetValue(objectId, out var itemPath))
        {
            sprite = Resources.Load<Sprite>(itemPath)
                ?? CreateSingleSprite(Resources.Load<Texture2D>(itemPath));
        }
        else if (PersonSpritePaths.TryGetValue(objectId, out var personPath))
        {
            sprite = Resources.Load<Sprite>(personPath)
                ?? CreateSingleSprite(Resources.Load<Texture2D>(personPath));
        }

        if (sprite != null)
        {
            CachedSprites[objectId] = sprite;
        }

        return sprite;
    }

    public static bool IsPersonItem(string objectId)
    {
        return !string.IsNullOrEmpty(objectId) && PersonSheetPaths.ContainsKey(objectId);
    }

    private static Sprite CreateSingleSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(1, texture.width),
            0,
            SpriteMeshType.FullRect
        );
    }
}
