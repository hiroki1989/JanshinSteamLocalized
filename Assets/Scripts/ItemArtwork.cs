using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared, untinted item art. Each host owns its aura and body, contained inside its rect.</summary>
public sealed class ItemArtwork : MonoBehaviour
{
    static readonly Dictionary<string, Sprite> Cache = new();
    static readonly string[] Rarities = { "01_normal", "02_common", "03_rare", "04_epic", "05_legendary" };
    static readonly string[] Uniques = {
        "", "01_amaterasu", "02_susanoo", "03_bastet", "04_shiva", "05_anubis",
        "06_freyja", "07_poseidon", "08_odin", "09_luna", "10_zeus",
        "11_hades_dyer", "12_hades_calligrapher", "13_hades_capitalist"
    };
    [SerializeField] Image body, aura;
    public static Sprite Load(string path)
    {
        if (!Cache.TryGetValue(path, out var sprite) || !sprite)
            Cache[path] = sprite = Resources.Load<Sprite>("ItemArtwork/" + path);
        return sprite;
    }
    public static string OmamoriKey(PlayerData.OmamoriInstance item)
    {
        int kind = (int)item.uniqueKind;
        return item.isUnique && kind > 0 && kind < Uniques.Length
            ? "artifact_" + Uniques[kind]
            : "omamori_" + Rarities[Mathf.Clamp((int)item.rarity, 0, 4)];
    }
    public static string RarityKey(string rarity)
    {
        switch ((rarity ?? "").Trim().ToLowerInvariant())
        {
            case "common": case "コモン": return Rarities[1];
            case "rare": case "レア": return Rarities[2];
            case "epic": case "エピック": return Rarities[3];
            case "legendary": case "レジェンダリー": return Rarities[4];
            default: return Rarities[0];
        }
    }
    public static void Omamori(Image host, int id)
    {
        if (!host) return;
        if (id <= 0 || !PlayerData.TryGetOmamori(id, out var item)) { Clear(host); return; }
        Omamori(host, item);
    }
    public static void Omamori(Image host, PlayerData.OmamoriInstance item)
    {
        if (!host || item == null) { Clear(host); return; }
        string key = OmamoriKey(item), auraKey = null;
        if (item.isUnique)
        {
            int k = Mathf.Clamp((int)item.uniqueKind, 1, 11);
            auraKey = k == 11 ? "aura_11_hades" : "aura_" + Uniques[k];
        }
        else if (item.rarity == PlayerData.OmamoriRarity.Legendary) auraKey = "aura_00_legendary";
        Show(host, key, auraKey);
    }
    public static void Ofuda(Image host, string rarity)
    {
        if (!host) return;
        if (string.IsNullOrWhiteSpace(rarity)) { Clear(host); return; }
        string key = RarityKey(rarity);
        Show(host, "ofuda_" + key, key == Rarities[4] ? "aura_00_legendary" : null);
    }
    static void Show(Image host, string key, string auraKey)
    {
        var view = host.GetComponent<ItemArtwork>() ?? host.gameObject.AddComponent<ItemArtwork>();
        host.gameObject.SetActive(true);
        host.sprite = null;
        host.color = Color.clear;
        host.raycastTarget = false;
        view.aura = Child(host.transform, "ItemAura", view.aura);
        view.body = Child(host.transform, "ItemBody", view.body);
        view.body.sprite = Load("Body/" + key);
        view.body.gameObject.SetActive(view.body.sprite);
        view.aura.sprite = auraKey == null ? null : Load("Aura/" + auraKey);
        view.aura.gameObject.SetActive(view.aura.sprite);
        view.aura.color = new Color(1, 1, 1, .55f);
        Fit(view.aura.rectTransform, .03f);
        Fit(view.body.rectTransform, auraKey == null ? .06f : .15f);
    }
    public static void Clear(Image host)
    {
        if (!host) return;
        host.sprite = null; host.color = Color.clear; host.raycastTarget = false;
        var view = host.GetComponent<ItemArtwork>();
        if (!view) return;
        if (view.body) view.body.gameObject.SetActive(false);
        if (view.aura) view.aura.gameObject.SetActive(false);
    }
    static Image Child(Transform parent, string name, Image cached)
    {
        if (cached) return cached;
        var found = parent.Find(name);
        var image = found ? found.GetComponent<Image>() : null;
        if (!image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); image = go.GetComponent<Image>();
        }
        image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
    static void Fit(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.one * inset; rt.anchorMax = Vector2.one * (1 - inset);
        rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
    }
    public static Image EnsureIcon(Transform parent, string name = "ItemIcon") => Child(parent, name, null);
    public static void Rect(RectTransform rt, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        if (!rt) return;
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = new Vector2(.5f,.5f);
        rt.offsetMin = low; rt.offsetMax = high; rt.localScale = Vector3.one;
    }
    public static void Text(TMP_Text text, float maxSize = 28)
    {
        if (!text) return;
        text.enableAutoSizing = true; text.fontSizeMax = maxSize; text.fontSizeMin = Mathf.Min(16, maxSize);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.Left;
        text.margin = Vector4.zero;
    }
    public static void OwnedRow(GameObject row, Image icon, TMP_Text label)
    {
        if (!row || !icon) return;
        icon.transform.SetParent(row.transform, false);
        Rect(icon.rectTransform, new Vector2(0,.5f), new Vector2(0,.5f), new Vector2(12,-66), new Vector2(144,66));
        if (label)
        {
            Rect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(154,12), new Vector2(-58,-12));
            Text(label, 28);
            float width = ((RectTransform)row.transform).rect.width;
            if (width < 250 && row.transform.parent is RectTransform parent) width = parent.rect.width;
            float height = label.GetPreferredValues(label.text, Mathf.Max(220, width - 212), 1000).y + 30;
            var le = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = Mathf.Max(180, height); le.flexibleHeight = 0;
        }
        var mark = row.transform.Find("EquippedMark") as RectTransform;
        Rect(mark, new Vector2(1,.5f), new Vector2(1,.5f), new Vector2(-48,-20), new Vector2(-8,20));
        var bg = row.transform.Find("Background") as RectTransform;
        Rect(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }
    public static void ShopSlot(Image icon, TMP_Text name, TMP_Text price = null)
    {
        if (!icon || !name) return;
        Rect(icon.rectTransform, new Vector2(0,0), new Vector2(.32f,1), new Vector2(5,60), new Vector2(-3,-22));
        Rect(name.rectTransform, new Vector2(.32f,0), Vector2.one, new Vector2(4,62), new Vector2(-12,-16));
        Text(name, 28);
        if (price)
        {
            Rect(price.rectTransform, Vector2.zero, new Vector2(1,0), new Vector2(15,12), new Vector2(-15,56));
            Text(price, 28); price.alignment = TextAlignmentOptions.Center;
        }
    }
    public static void UniquePanel(GameObject panel, TMP_Text desc, TMP_Text title, int id)
    {
        if (!panel || !desc || id <= 0) return;
        var icon = EnsureIcon(desc.transform.parent, "UniqueItemIcon");
        Rect(icon.rectTransform, new Vector2(0,.25f), new Vector2(.28f,.8f), new Vector2(24,8), new Vector2(-10,-8));
        Rect(desc.rectTransform, new Vector2(.29f,.25f), new Vector2(.97f,.8f), Vector2.zero, Vector2.zero);
        Text(desc, 30);
        if (title) { Rect(title.rectTransform,new Vector2(.05f,.82f),new Vector2(.95f,.94f),Vector2.zero,Vector2.zero); Text(title,32); }
        Omamori(icon, id);
    }
}
