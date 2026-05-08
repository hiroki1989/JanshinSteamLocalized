using TMPro;
using UnityEngine;

public static class OfudaRarityColors
{
public static Color Get(string rarity)
{
    if (string.IsNullOrEmpty(rarity))
        return new Color32(255, 255, 255, 255);

    switch (rarity.Trim().ToLowerInvariant())
    {
        case "レジェンダリー":
        case "legendary":
            return new Color32(255, 140,   0, 255); // Orange

        case "エピック":
        case "epic":
            return new Color32(160,  80, 255, 255); // Purple

        case "レア":
        case "rare":
            return new Color32(255, 220,   0, 255); // Yellow

        case "コモン":
        case "common":
            return new Color32( 80, 160, 255, 255); // Blue

        case "ノーマル":
        case "normal":
            return new Color32(255, 255, 255, 255); // White

        default:
            return new Color32(255, 255, 255, 255);
    }
}
    public static void Apply(TextMeshProUGUI tmp, string rarity)
    {
        if (!tmp) return;
        tmp.color = Get(rarity);
    }
}
