using UnityEngine;

// The portrait assets use character-specific eye rectangles in the Sprite Editor.
// Loading those sprites also keeps Inspector previews and the battle HUD identical.
public static class UnifiedCharacterPortrait
{
    public static Sprite Load(string key, bool player)
    {
        return Resources.Load<Sprite>((player ? "PlayerPortraits/" : "EnemyPortraits/") + key + "_portrait");
    }
}
