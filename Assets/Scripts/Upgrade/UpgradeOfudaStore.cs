using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class UpgradeOfudaStore : MonoBehaviour
{
[Header("UI Roots")]
[SerializeField] private TextMeshProUGUI currencyTMP;
[SerializeField] private Button nextButton;        // 「次へ」（既存の UpgradeNextButton と併用OK）

[Header("Equipped Ofuda UI (Manual)")]
[SerializeField] private Image[] equippedOfudaIconImages = new Image[3];               // 装備中お札アイコン（最大3）
[SerializeField] private TextMeshProUGUI[] equippedOfudaTMPs = new TextMeshProUGUI[3]; // 装備中お札テキスト（最大3）

[System.Serializable]
private class OfferSlotUI
{
    public Button button;
    public Image backgroundImage;
    public Image iconImage;
    public TextMeshProUGUI nameTMP;
    public TextMeshProUGUI priceTMP;
}

[Header("Offer Slots (Manual)")]
[SerializeField] private OfferSlotUI[] offerSlots = new OfferSlotUI[3];

[Header("Offer Ofuda Icon (Manual Slot Common Sprite)")]
[SerializeField] private Sprite ofudaIconSprite; // オファー枠に表示するお札アイコンのSprite

[Header("Config")]
[SerializeField] private string excelPathOverride = null; // StreamingAssets 以外に置くとき
[SerializeField] private int offerCount = 3;

[SerializeField] private UpgradeOfudaSlotsPanel ownedSlotsPanel; // 所持スロット表示（購入後に更新）
// 所持通貨は GameManager.RunCurrency に統一
private int Currency
{
    get => GameManager.RunCurrency.Get();
    set { GameManager.RunCurrency.Set(Mathf.Max(0, value)); RefreshCurrencyUI(); }
}
    // 各オファー枠ごとの購入済みフラグ
    private bool[] _purchasedSlots = new bool[3];

    private List<OfudaDef> _candidatePool;
    private List<OfudaDef> _offering = new();
    private Dictionary<string, OfudaDef> _defMap = new Dictionary<string, OfudaDef>();
void Awake()
{
    if (!ownedSlotsPanel)
        ownedSlotsPanel = Object.FindAnyObjectByType<UpgradeOfudaSlotsPanel>();

    RefreshCurrencyUI();
    RebuildCatalogLocalized();

    _purchasedSlots = new bool[Mathf.Max(offerCount, 3)];

    BuildOffers();
    RefreshEquippedOfudaUI();

    if (nextButton) nextButton.interactable = true;
}

private void OnEnable()
{
    LocalizationManager.LanguageChanged += OnLanguageChanged;
}

private void OnDisable()
{
    LocalizationManager.LanguageChanged -= OnLanguageChanged;
}

private void OnLanguageChanged(LocalizationManager.Language language)
{
    RebuildCatalogLocalized();
    BuildOffers();
    RefreshEquippedOfudaUI();

    if (ownedSlotsPanel)
        ownedSlotsPanel.RefreshUI();
}

private void RebuildCatalogLocalized()
{
    var cat = OfudaExcelLoader.Load(excelPathOverride);
    _candidatePool = OfudaCatalog.BuildFromExcel(cat);

    _defMap.Clear();
    if (_candidatePool != null)
    {
        for (int i = 0; i < _candidatePool.Count; i++)
        {
            var d = _candidatePool[i];
            if (d == null) continue;
            if (string.IsNullOrEmpty(d.id)) continue;
            if (!_defMap.ContainsKey(d.id))
                _defMap.Add(d.id, d);
        }
    }
}
private void RefreshCurrencyUI()
{
    if (currencyTMP) currencyTMP.text = $" {GameManager.RunCurrency.Get():#,0}";
}
private void BuildOffers()
{
    _offering.Clear();

    var cat = OfudaExcelLoader.Load(excelPathOverride);
    var exclude = new HashSet<string>();
    {
        var cur = PlayerPrefs.GetString("RunOfuda", "");
        if (!string.IsNullOrEmpty(cur))
            foreach (var s in cur.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                exclude.Add(s.Trim());
    }

    _offering = OfudaCatalog.PickOffers(cat, offerCount, exclude, new System.Random());
    _purchasedSlots = new bool[Mathf.Max(_offering.Count, 3)];

    RefreshOfferSlotsUI();
}
private void RefreshOfferSlotsUI()
{
    for (int i = 0; i < offerSlots.Length; i++)
    {
        var slot = offerSlots[i];
        bool hasOffer = i < _offering.Count;
        OfudaDef o = hasOffer ? _offering[i] : null;

        if (slot == null)
            continue;

        if (slot.button)
        {
            slot.button.onClick.RemoveAllListeners();
        }

        if (!hasOffer || o == null)
        {
            if (slot.backgroundImage)
            {
                slot.backgroundImage.color = new Color(1f, 1f, 1f, 0.10f);
            }

            if (slot.iconImage)
            {
                if (slot.iconImage.gameObject.activeSelf)
                    slot.iconImage.gameObject.SetActive(false);
            }

            if (slot.nameTMP)
            {
                slot.nameTMP.text = GetEmptySlotText();
            }

            if (slot.priceTMP)
            {
                slot.priceTMP.text = "";
            }

            if (slot.button)
            {
                slot.button.interactable = false;
            }

            continue;
        }

        Color rarityColor = GetRarityColor(o.rarity);
        Color bgColor = rarityColor;
        bgColor.a = 1.00f;

        if (slot.backgroundImage)
        {
            slot.backgroundImage.color = bgColor;
        }

        if (slot.iconImage)
        {
            if (ofudaIconSprite)
                slot.iconImage.sprite = ofudaIconSprite;

            slot.iconImage.preserveAspect = true;
            slot.iconImage.color = rarityColor;

            if (!slot.iconImage.gameObject.activeSelf)
                slot.iconImage.gameObject.SetActive(true);
        }

        if (slot.nameTMP)
        {
            slot.nameTMP.text = ColorizeRarityPrefix(o.displayName, o.rarity);
        }

        if (slot.priceTMP)
        {
            slot.priceTMP.text = $"　 {o.price:#,0}";
        }

        bool bought = (_purchasedSlots != null && i < _purchasedSlots.Length && _purchasedSlots[i]);
        bool canBuy = !bought && Currency >= o.price && !OfudaRunInventory.IsFull;

        if (slot.button)
        {
            slot.button.interactable = canBuy;

            int cap = i;
            slot.button.onClick.AddListener(() => TryBuy(cap));
        }
    }
}
    private void TryBuy(int index)
    {
        if (index < 0 || index >= _offering.Count) return;

        if (_purchasedSlots == null || _purchasedSlots.Length < _offering.Count)
            _purchasedSlots = new bool[Mathf.Max(_offering.Count, 3)];

        if (_purchasedSlots[index]) return;

        var o = _offering[index];
        if (Currency < o.price) return;

        if (OfudaRunInventory.IsFull) return;

        if (!OfudaRunInventory.TryAdd(o.id)) return;

        Currency -= o.price;
        _purchasedSlots[index] = true;

        PlayerPrefs.SetString("RunOfuda_LastJSON", JsonUtility.ToJson(o));
        PlayerPrefs.Save();

        if (ownedSlotsPanel) ownedSlotsPanel.RefreshUI();

        RefreshEquippedOfudaUI();
        RefreshCurrencyUI();
RefreshOfferSlotsUI();
    }
private void SavePurchasedOfuda(OfudaDef def)
{
    // RunOfuda への追加は OfudaRunInventory.TryAdd(...) で管理する（最大3枠保証）
    PlayerPrefs.SetString("RunOfuda_LastJSON", JsonUtility.ToJson(def));
    PlayerPrefs.Save();
}

    private int WeightedPick(List<OfudaDef> pool)
    {
        // 逆数重み：希少（prob和が小さい）ほど拾いにくい
        double sumW = 0;
        foreach (var x in pool)
        {
            double w = 1.0 / Mathf.Max(0.0001f, x.combinedProb);
            sumW += w;
        }
        double r = Random.value * sumW;
        foreach (var x in pool)
        {
            double w = 1.0 / Mathf.Max(0.0001f, x.combinedProb);
            if (r < w) return pool.IndexOf(x);
            r -= w;
        }
        return pool.Count - 1;
    }
private static string ColorizeRarityPrefix(string displayName, string rarity)
{
    if (string.IsNullOrEmpty(displayName)) return displayName;

    int start = displayName.IndexOf('【');
    int end = displayName.IndexOf('】');

    if (start != 0 || end <= start)
        return displayName;

    string prefix = displayName.Substring(0, end + 1);
    string rest = displayName.Substring(end + 1);

    string hex = ColorUtility.ToHtmlStringRGB(GetRarityColor(rarity));
    return $"<color=#{hex}>{prefix}</color>{rest}";
}
private static Color GetRarityColor(string rarity)
{
    if (string.IsNullOrEmpty(rarity))
        return new Color32(255, 255, 255, 255);

    switch (rarity.Trim().ToLowerInvariant())
    {
        case "レジェンダリー":
        case "legendary":
            return new Color32(255, 140,   0, 255); // オレンジ

        case "エピック":
        case "epic":
            return new Color32(160,  80, 255, 255); // 紫

        case "レア":
        case "rare":
            return new Color32(255, 220,   0, 255); // 黄

        case "コモン":
        case "common":
            return new Color32( 80, 160, 255, 255); // 青

        case "ノーマル":
        case "normal":
        default:
            return new Color32(255, 255, 255, 255); // ノーマル(白)
    }
}
private static string GetEmptySlotText()
{
    if (LocalizationManager.Instance == null)
        return "-";

    switch (LocalizationManager.Instance.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
            return "None";

        case LocalizationManager.Language.ChineseSimplified:
            return "无";

        default:
            return "-";
    }
}
private void RefreshEquippedOfudaUI()
{
    var ids = OfudaRunInventory.LoadList();

    for (int i = 0; i < 3; i++)
    {
        string id = "";
        if (ids != null && i < ids.Count && ids[i] != null)
            id = ids[i];

        bool has = !string.IsNullOrEmpty(id);

        OfudaDef def = null;
        if (has && _defMap != null)
            _defMap.TryGetValue(id, out def);

        // テキスト
        if (equippedOfudaTMPs != null && i < equippedOfudaTMPs.Length && equippedOfudaTMPs[i])
        {
            if (!has || def == null)
            {
                equippedOfudaTMPs[i].text = GetEmptySlotText();
            }
            else
            {
                // 既存と同様：レア度prefixを色付きにして表示
                var coloredName = ColorizeRarityPrefix(def.displayName, def.rarity);
                equippedOfudaTMPs[i].text = coloredName;
            }
        }

        // アイコン
        if (equippedOfudaIconImages != null && i < equippedOfudaIconImages.Length && equippedOfudaIconImages[i])
        {
            if (!has || def == null)
            {
                if (equippedOfudaIconImages[i].gameObject.activeSelf)
                    equippedOfudaIconImages[i].gameObject.SetActive(false);
            }
            else
            {
                // Spriteはオファー用と同じものを流用（装備中でも同じ見た目にする）
                if (ofudaIconSprite)
                    equippedOfudaIconImages[i].sprite = ofudaIconSprite;

                equippedOfudaIconImages[i].preserveAspect = true;
                equippedOfudaIconImages[i].color = GetRarityColor(def.rarity);

                if (!equippedOfudaIconImages[i].gameObject.activeSelf)
                    equippedOfudaIconImages[i].gameObject.SetActive(true);
            }
        }
    }
}
}
