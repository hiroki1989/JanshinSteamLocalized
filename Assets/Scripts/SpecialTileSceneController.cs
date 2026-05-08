using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class SpecialTileSceneController : MonoBehaviour
{
    [Header("Costs (Inspector)")]
    [SerializeField] private int buyCostGems = 3;
    [SerializeField] private int expandSlotCostGems = 10;

    [Header("Test Mode")]
    [SerializeField] private bool testModeOverrideGems = false;
    [SerializeField] private int testGemsValue = 999;

    [Header("Roll Prob (Inspector)")]
    [SerializeField] private SpecialTileSystem.RollConfig rollConfig = new SpecialTileSystem.RollConfig
    {
        basePin5 = 1f, baseMan5 = 1f, baseSou5 = 1f,
        rarityNormal = 0.40f,
        rarityCommon = 0.30f,
        rarityRare = 0.18f,
        rarityEpic = 0.09f,
        rarityLegendary = 0.03f
    };

    [Header("Reveal UI")]
    [SerializeField] private TextMeshProUGUI gemsTMP;
    [SerializeField] private TextMeshProUGUI resultTMP;
    [SerializeField] private CanvasGroup resultGroup;
    [SerializeField] private Image resultTileImage;
    [SerializeField] private float revealFadeSeconds = 0.6f;
    [SerializeField] private string unknownText = "？";

[Header("Buttons")]
[SerializeField] private Button buyButton;
[SerializeField] private Button expandSlotButton;
[SerializeField] private Button unequipButton;
[SerializeField] private Button resultOkButton;

[Header("SE (Special Tile Buy)")]
[SerializeField] private AudioSource buySESource;                 // SEを鳴らすAudioSource
[SerializeField] private AudioClip buySE_Normal;                  // ノーマル
[SerializeField] private AudioClip buySE_Common;                  // コモン
[SerializeField] private AudioClip buySE_Rare;                    // レア
[SerializeField] private AudioClip buySE_Epic;                    // エピック
[SerializeField] private AudioClip buySE_Legendary;               // レジェンダリー
    [Header("Back")]
    [SerializeField] private Button backButton;
    [SerializeField] private string menuSceneName = "MenuScene";

    [Header("Equip UI (simple text)")]
    [SerializeField] private TextMeshProUGUI equippedTMP;
    [SerializeField] private TextMeshProUGUI ownedTMP;
    [SerializeField] private TextMeshProUGUI equippedCountTMP;

    // ★変更：装備枠は最大4の「スロット配列」にする（同種複数OK）
    [Header("Equipped Slots (max 4)")]
    [SerializeField] private Image[] equippedSlotImages = new Image[4];
    [SerializeField] private TextMeshProUGUI[] equippedSlotInfoTMPs = new TextMeshProUGUI[4];
[Header("Equipped Slot Row Highlight (Manual)")]
[SerializeField] private Image[] equippedSlotBackgroundImages = new Image[4]; // ★選択中ハイライト用の背景Image（ON/OFFする。最初は非表示推奨）
[SerializeField] private Color equippedSlotNormalBgColor = Color.white;       // ★未使用（残してOK）
[SerializeField] private Color equippedSlotSelectedBgColor = new Color(0.65f, 0.65f, 0.65f, 1f); // ★未使用（残してOK）
    [Header("Owned List UI (ScrollView)")]
    [SerializeField] private ScrollRect ownedScrollRect;
    [SerializeField] private Transform ownedListParent;
    [SerializeField] private GameObject ownedItemPrefab;

    [Header("Owned Header / Discard")]
    [SerializeField] private TextMeshProUGUI ownedCountTMP;
    [SerializeField] private Button discardButton;
    [Header("Owned Row Colors")]
    [SerializeField] private Color ownedRowNormalColor = Color.white;
    [SerializeField] private Color ownedRowEquippedColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color ownedRowSelectedColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("Owned Row Visuals (Manual)")]
    [SerializeField] private string ownedRowBackgroundChildName = "Background"; // 行プレハブ内の背景Image(任意)
    [SerializeField] private string ownedRowEquippedMarkChildName = "EquippedMark"; // 装備中アイコンImage(任意)

    [Header("Resources Path (no extension)")]
    [SerializeField] private string tileSpriteResourcesPath = "Tiles/"; // Resources/Tiles/Pin5_sp_common.png 等

    private List<SpecialTileSystem.Entry> _ownedCache = new List<SpecialTileSystem.Entry>();
    private System.Random _rng;

    private const int OwnedMax = 20;

    private SpecialTileSystem.Entry? _selectedOwnedEntry = null;
    private readonly List<GameObject> _ownedRowObjects = new List<GameObject>();

    private int _selectedEquippedSlot = -1;

    private void Awake()
    {
        _rng = new System.Random();

        if (testModeOverrideGems)
        {
            SpecialTileSystem.SetGems(testGemsValue);
        }

        Wire();
        RefreshAll();
        SetResultUnknown();
    }

    private static bool SameEntry(SpecialTileSystem.Entry a, SpecialTileSystem.Entry b)
    {
        return SpecialTileSystem.SameEntry(a, b);
    }
private static string GetSpecialTileFixedText_Local(string key)
{
    return LocalizationManager.Fixed(key);
}

private static string NormalizeSpecialTileYakuKey_Local(string s)
{
    if (string.IsNullOrEmpty(s)) return "";

    s = s.Trim().Replace("　", " ");
    s = s.Replace('（', '(').Replace('）', ')');

    int p0 = s.IndexOf('(');
    if (p0 >= 0) s = s.Substring(0, p0);

    s = s.Trim();
    if (string.IsNullOrEmpty(s)) return "";

    if (s.StartsWith("yaku.", StringComparison.OrdinalIgnoreCase))
        return s.Substring("yaku.".Length).Trim().ToUpperInvariant();

    if (s.StartsWith("yakuman.", StringComparison.OrdinalIgnoreCase))
        return s.Substring("yakuman.".Length).Trim().ToUpperInvariant();

    if (s == "風牌" || s.StartsWith("風牌", StringComparison.Ordinal) || s == "役牌" || s.StartsWith("役牌", StringComparison.Ordinal)) return "YAKUHAI";
    if (s == "平和" || s == "ピンフ" || s.Equals("Pinfu", StringComparison.OrdinalIgnoreCase)) return "PINFU";
    if (s == "タンヤオ" || s == "断么九" || s.Equals("Tanyao", StringComparison.OrdinalIgnoreCase)) return "TANYAO";
    if (s == "一盃口" || s.Equals("Iipeikou", StringComparison.OrdinalIgnoreCase)) return "IIPEIKOU";
    if (s == "二盃口" || s.Equals("Ryanpeikou", StringComparison.OrdinalIgnoreCase)) return "RYANPEIKOU";
    if (s == "三色同順" || s.Equals("Sanshoku_Doujun", StringComparison.OrdinalIgnoreCase)) return "SANSHOKU_DOUJUN";
    if (s == "一気通貫" || s.Equals("Ittsu", StringComparison.OrdinalIgnoreCase)) return "ITTSU";
    if (s == "チャンタ" || s.Equals("Chanta", StringComparison.OrdinalIgnoreCase)) return "CHANTA";
    if (s == "純チャン" || s.Equals("Junchan", StringComparison.OrdinalIgnoreCase)) return "JUNCHAN";
    if (s == "対々和" || s.Equals("Toitoi", StringComparison.OrdinalIgnoreCase)) return "TOITOI";
    if (s == "三暗刻" || s.Equals("Sanankou", StringComparison.OrdinalIgnoreCase)) return "SANANKOU";
    if (s == "三カンツ" || s == "三槓子" || s.Equals("Sankantsu", StringComparison.OrdinalIgnoreCase)) return "SANKANTSU";
    if (s == "三色同刻" || s.Equals("Sanshoku_Doukou", StringComparison.OrdinalIgnoreCase)) return "SANSHOKU_DOUKOU";
    if (s == "小三元" || s.Equals("Shousangen", StringComparison.OrdinalIgnoreCase)) return "SHOUSANGEN";
    if (s == "混老頭" || s.Equals("Honroutou", StringComparison.OrdinalIgnoreCase)) return "HONROUTOU";
    if (s == "混一色" || s == "ホンイツ" || s.Equals("Honitsu", StringComparison.OrdinalIgnoreCase)) return "HONITSU";
    if (s == "清一色" || s.Equals("Chinitsu", StringComparison.OrdinalIgnoreCase)) return "CHINITSU";
    if (s == "七対子" || s.Equals("Chiitoitsu", StringComparison.OrdinalIgnoreCase)) return "CHIITOITSU";
    if (s == "門前清自摸和" || s.Equals("Menzen_Tsumo", StringComparison.OrdinalIgnoreCase)) return "MENZEN_TSUMO";

    if (s == "国士無双" || s.Equals("Kokushi", StringComparison.OrdinalIgnoreCase)) return "KOKUSHI";
    if (s == "九蓮宝燈" || s.Equals("Chuuren_Poutou", StringComparison.OrdinalIgnoreCase)) return "CHUUREN_POUTOU";
    if (s == "大三元" || s.Equals("Daisangen", StringComparison.OrdinalIgnoreCase)) return "DAISANGEN";
    if (s == "大四喜" || s.Equals("Daisuushi", StringComparison.OrdinalIgnoreCase)) return "DAISUUSHI";
    if (s == "小四喜" || s.Equals("Shousuushi", StringComparison.OrdinalIgnoreCase)) return "SHOUSUUSHI";
    if (s == "字一色" || s.Equals("Tsuuiisou", StringComparison.OrdinalIgnoreCase)) return "TSUUIISOU";
    if (s == "清老頭" || s.Equals("Chinroutou", StringComparison.OrdinalIgnoreCase)) return "CHINROUTOU";
    if (s == "緑一色" || s.Equals("Ryuuiisou", StringComparison.OrdinalIgnoreCase)) return "RYUUIISOU";
    if (s == "四暗刻" || s.Equals("Suuankou", StringComparison.OrdinalIgnoreCase)) return "SUUANKOU";
    if (s == "四カンツ" || s == "四槓子" || s.Equals("Suukantsu", StringComparison.OrdinalIgnoreCase)) return "SUUKANTSU";

    return s.ToUpperInvariant();
}

private static string LocalizeSpecialTileYakuName_Local(string raw)
{
    if (string.IsNullOrEmpty(raw)) return "";

    string normalized = NormalizeSpecialTileYakuKey_Local(raw);
    if (string.IsNullOrEmpty(normalized)) return "";

    string yaku = LocalizationManager.Yaku(normalized);
    if (!string.IsNullOrEmpty(yaku) &&
        !string.Equals(yaku, "yaku." + normalized, StringComparison.Ordinal))
    {
        return yaku;
    }

    string yakuman = LocalizationManager.Yakuman(normalized);
    if (!string.IsNullOrEmpty(yakuman) &&
        !string.Equals(yakuman, "yakuman." + normalized, StringComparison.Ordinal))
    {
        return yakuman;
    }

    return raw;
}

private static string GetSpecialTileRarityLabel_Local(SpecialTileSystem.Rarity r)
{
    string key = SpecialTileSystem.RarityKey(r);
    string localized = LocalizationManager.Rarity(key);

    if (!string.IsNullOrEmpty(localized))
    {
        string directKey = "rarity." + key;
        if (!string.Equals(localized, directKey, StringComparison.Ordinal))
            return localized;
    }

    if (r == SpecialTileSystem.Rarity.Normal) return "ノーマル";
    if (r == SpecialTileSystem.Rarity.Common) return "コモン";
    if (r == SpecialTileSystem.Rarity.Rare) return "レア";
    if (r == SpecialTileSystem.Rarity.Epic) return "エピック";
    if (r == SpecialTileSystem.Rarity.Legendary) return "レジェンダリー";
    return key;
}
    private static bool IsEquippedExact(List<SpecialTileSystem.Entry> equipped, SpecialTileSystem.Entry e)
    {
        if (equipped == null) return false;
        for (int i = 0; i < equipped.Count; i++)
        {
            if (SameEntry(equipped[i], e)) return true;
        }
        return false;
    }

    private Image FindIconImage(GameObject row)
    {
        if (!row) return null;
        var t = row.transform.Find("Icon");
        if (t) return t.GetComponent<Image>();
        return row.GetComponentInChildren<Image>(true);
    }

    private TextMeshProUGUI FindEffectTMP(GameObject row)
    {
        if (!row) return null;
        var t = row.transform.Find("EffectTMP");
        if (t) return t.GetComponent<TextMeshProUGUI>();
        return row.GetComponentInChildren<TextMeshProUGUI>(true);
    }
    private void OnClickOwnedRow(SpecialTileSystem.Entry entry)
    {
        _selectedOwnedEntry = entry;

        int slots = SpecialTileSystem.GetEquipSlotsUnlocked();
        var eq = SpecialTileSystem.GetEquipped() ?? new List<SpecialTileSystem.Entry>();

        // ★同一個体の多重装備禁止：
        //  既に装備中なら「装備中スロットを選択する」だけにする
        int alreadyIdx = -1;
        for (int i = 0; i < eq.Count; i++)
        {
            if (SameEntry(eq[i], entry))
            {
                alreadyIdx = i;
                break;
            }
        }
        if (alreadyIdx >= 0)
        {
            _selectedEquippedSlot = alreadyIdx;
            RefreshAll();
            return;
        }
        // ★空き枠があるなら「入れ替え」ではなく、空き枠に装備する
        //  eq.Count == slots でも、中身が空（uidが空）な枠があるケースがあるため、先に空きを探す
        int emptyIdx = -1;
// 解放済みスロット(0..slots-1)の中で、最初の空きを探す
for (int i = 0; i < slots; i++)
{
    // リストがそこまで無いなら、そこが空き
    if (i >= eq.Count)
    {
        emptyIdx = i;
        break;
    }

    // ★その枠の TileId が空なら「空き」として扱う
    //  Entry に uid が無いので TileId() で判定する
    if (string.IsNullOrEmpty(eq[i].TileId()))
    {
        emptyIdx = i;
        break;
    }
}
        if (emptyIdx >= 0)
        {
            // 空きが末尾（i>=eq.Count）ならAppendでOK
            if (emptyIdx >= eq.Count)
            {
                SpecialTileSystem.TryEquipAppend(entry);
            }
            else
            {
                // 空きが途中にある場合は、その空き枠へ差し込む（入れ替えではなく空き埋め）
                SpecialTileSystem.TryEquipReplaceAt(emptyIdx, entry);
            }

            _selectedEquippedSlot = emptyIdx; // どこに入ったか選択も合わせる（ハイライト維持）
        }
        else
        {
            // 空きが無い＝満杯なら「選択中の装備スロット」に置換。未選択なら末尾。
            int idx = (_selectedEquippedSlot >= 0) ? _selectedEquippedSlot : (slots - 1);
            SpecialTileSystem.TryEquipReplaceAt(idx, entry);
            _selectedEquippedSlot = idx;
        }

        RefreshAll();
    }
private void PlayBuySE_ByRarity(SpecialTileSystem.Rarity rarity)
{
    if (buySESource == null) return;

    AudioClip clip = null;
    switch (rarity)
    {
        case SpecialTileSystem.Rarity.Legendary: clip = buySE_Legendary; break;
        case SpecialTileSystem.Rarity.Epic:      clip = buySE_Epic;      break;
        case SpecialTileSystem.Rarity.Rare:      clip = buySE_Rare;      break;
        case SpecialTileSystem.Rarity.Common:    clip = buySE_Common;    break;
        case SpecialTileSystem.Rarity.Normal:    clip = buySE_Normal;    break;
        default:                                 clip = buySE_Normal;    break;
    }

    if (clip != null)
    {
        try { buySESource.PlayOneShot(clip); } catch { }
    }
}
    private void RefreshOwnedScrollList()
    {
        for (int i = 0; i < _ownedRowObjects.Count; i++)
        {
            if (_ownedRowObjects[i]) Destroy(_ownedRowObjects[i]);
        }
        _ownedRowObjects.Clear();

        if (!ownedListParent || !ownedItemPrefab) return;

        var equipped = SpecialTileSystem.GetEquipped();

        for (int i = 0; i < _ownedCache.Count; i++)
        {
            var e = _ownedCache[i];

            var row = Instantiate(ownedItemPrefab, ownedListParent);
            _ownedRowObjects.Add(row);

            var icon = FindIconImage(row);
            var fxTMP = FindEffectTMP(row);

            if (icon)
            {
                var sp = LoadTileSprite(e.TileId());
                icon.sprite = sp;
                icon.enabled = (sp != null);
            }
            if (fxTMP) fxTMP.text = BuildEntryText(e);

            var btn = row.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                SpecialTileSystem.Entry captured = e;
                btn.onClick.AddListener(() => OnClickOwnedRow(captured));
            }

            bool isEquipped = IsEquippedExact(equipped, e);
            bool isSelected = _selectedOwnedEntry.HasValue && SameEntry(_selectedOwnedEntry.Value, e);

            var bg = FindOwnedRowBackgroundImage(row);
            if (bg)
            {
                if (isSelected) bg.color = ownedRowSelectedColor;
                else if (isEquipped) bg.color = ownedRowEquippedColor;
                else bg.color = ownedRowNormalColor;
            }

            var eqMark = FindOwnedRowEquippedMarkImage(row);
            if (eqMark)
            {
                bool show = isEquipped;
                if (eqMark.gameObject.activeSelf != show) eqMark.gameObject.SetActive(show);
            }
        }
    }
    private Image FindOwnedRowBackgroundImage(GameObject rowGo)
    {
        if (!rowGo) return null;

        if (!string.IsNullOrEmpty(ownedRowBackgroundChildName))
        {
            var t = rowGo.transform.Find(ownedRowBackgroundChildName);
            if (t)
            {
                var img = t.GetComponent<Image>();
                if (img) return img;
            }
        }

        return rowGo.GetComponent<Image>();
    }

    private Image FindOwnedRowEquippedMarkImage(GameObject rowGo)
    {
        if (!rowGo) return null;
        if (string.IsNullOrEmpty(ownedRowEquippedMarkChildName)) return null;

        var t = rowGo.transform.Find(ownedRowEquippedMarkChildName);
        if (!t) return null;

        return t.GetComponent<Image>();
    }
    private void OnClickDiscardSelected()
    {
        if (!_selectedOwnedEntry.HasValue) return;

        SpecialTileSystem.DiscardOwned(_selectedOwnedEntry.Value);
        _selectedOwnedEntry = null;

        RefreshAll();
    }

    private void Wire()
    {
        if (buyButton)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnClickBuy);
        }
        if (expandSlotButton)
        {
            expandSlotButton.onClick.RemoveAllListeners();
            expandSlotButton.onClick.AddListener(OnClickExpandSlot);
        }
        if (unequipButton)
        {
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(OnClickUnequip);
        }
        if (resultOkButton)
        {
            resultOkButton.onClick.RemoveAllListeners();
            resultOkButton.onClick.AddListener(OnClickResultOk);
        }
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnClickBackToMenu);
        }

        // 装備スロット：クリックで解除対象選択
        for (int i = 0; i < equippedSlotImages.Length; i++)
        {
            HookEquippedSelect(equippedSlotImages[i], i);
        }

        if (discardButton)
        {
            discardButton.onClick.RemoveAllListeners();
            discardButton.onClick.AddListener(OnClickDiscardSelected);
        }
    }

    private void HookEquippedSelect(Image img, int slotIndex)
    {
        if (!img) return;

        var b = img.GetComponent<Button>();
        if (!b) b = img.GetComponentInParent<Button>();
        if (!b) return;

        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => OnClickSelectEquipped(slotIndex));
    }
    private void OnClickSelectEquipped(int slotIndex)
    {
        _selectedEquippedSlot = slotIndex;
        RefreshUnequipButton();
        RefreshEquippedSlots(); // ★追加：選択中ハイライト更新
    }
    private void OnClickUnequip()
    {
        if (_selectedEquippedSlot < 0) return;

        SpecialTileSystem.UnequipAt(_selectedEquippedSlot);

        // 解除後、選択が末尾を越える場合はリセット
        var eq = SpecialTileSystem.GetEquipped() ?? new List<SpecialTileSystem.Entry>();
        if (_selectedEquippedSlot >= eq.Count) _selectedEquippedSlot = -1;

        RefreshAll();
    }

    private void RefreshUnequipButton()
    {
        if (!unequipButton) return;

        var eq = SpecialTileSystem.GetEquipped() ?? new List<SpecialTileSystem.Entry>();
        bool can = (_selectedEquippedSlot >= 0 && _selectedEquippedSlot < eq.Count);
        unequipButton.interactable = can;
    }

    private void OnClickResultOk()
    {
        SetResultUnknown();
    }

    private void RefreshAll()
    {
        if (gemsTMP) gemsTMP.text = $"{SpecialTileSystem.GetGems()}";

        var owned = SpecialTileSystem.GetOwned();
        _ownedCache = (owned != null) ? new List<SpecialTileSystem.Entry>(owned) : new List<SpecialTileSystem.Entry>();
        if (ownedTMP) ownedTMP.text = BuildOwnedText(owned);

        int ownedCount = (owned != null) ? owned.Count : 0;
        if (ownedCountTMP) ownedCountTMP.text = $"{ownedCount}/{OwnedMax}";

        var eq = SpecialTileSystem.GetEquipped();
        if (equippedTMP) equippedTMP.text = BuildEquippedText(eq);

        int slots = SpecialTileSystem.GetEquipSlotsUnlocked();
        int eqCount = (eq != null) ? eq.Count : 0;
        if (equippedCountTMP) equippedCountTMP.text = $"{eqCount}/{slots}";

        bool canBuy = (SpecialTileSystem.GetGems() >= buyCostGems) && (ownedCount < OwnedMax);
        if (buyButton) buyButton.interactable = canBuy;

        if (expandSlotButton) expandSlotButton.interactable =
            (SpecialTileSystem.GetEquipSlotsUnlocked() < 4 && SpecialTileSystem.GetGems() >= expandSlotCostGems);

        RefreshEquippedSlots();
        RefreshOwnedScrollList();
        RefreshUnequipButton();
    }
    private void SetResultUnknown()
    {
        if (resultTMP) resultTMP.text = unknownText;

        if (resultTileImage)
        {
            resultTileImage.enabled = false;
            resultTileImage.sprite = null;
        }

        if (resultGroup)
        {
            resultGroup.alpha = 0f;
            resultGroup.interactable = false;
            resultGroup.blocksRaycasts = false;
        }
    }

    private void OnClickBuy()
    {
        if (!SpecialTileSystem.CanAddOwned(1))
        {
            if (resultTMP) resultTMP.text = $"所持上限({OwnedMax})に達しています。破棄してから購入してください。";

            if (resultTileImage)
            {
                resultTileImage.enabled = false;
                resultTileImage.sprite = null;
            }

            if (resultGroup)
            {
                resultGroup.alpha = 1f;
                resultGroup.interactable = false;
                resultGroup.blocksRaycasts = false;
            }
            RefreshAll();
            return;
        }

        if (!SpecialTileSystem.TryConsumeGems(buyCostGems))
        {
            RefreshAll();
            return;
        }

        SetResultUnknown();

var e = SpecialTileSystem.Roll(rollConfig, _rng);
SpecialTileSystem.AddOwned(e);

// SE：購入（レア度別） ※AudioManagerに集約
if (AudioManager.Instance)
{
    AudioManager.Instance.PlaySpecialTileBuySE_ByRarity(e.rarity);
}

StopAllCoroutines();
StartCoroutine(RevealResult_Co(e));

RefreshAll();
    }
    private IEnumerator RevealResult_Co(SpecialTileSystem.Entry e)
    {
        string text = BuildEntryText(e);
        if (resultTMP) resultTMP.text = text;

        if (resultTileImage)
        {
            var sp = LoadTileSprite(e.TileId());
            resultTileImage.sprite = sp;
            resultTileImage.enabled = (sp != null);
        }

        float dur = Mathf.Max(0.01f, revealFadeSeconds);
        float t = 0f;

        if (resultGroup)
        {
            resultGroup.alpha = 0f;
            resultGroup.interactable = true;
            resultGroup.blocksRaycasts = true;

            while (t < dur)
            {
                t += Time.deltaTime;
                resultGroup.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            resultGroup.alpha = 1f;
        }
    }

    private void OnClickExpandSlot()
    {
        SpecialTileSystem.TryIncreaseEquipSlots(expandSlotCostGems);
        RefreshAll();
    }

    public void OnClickBackToMenu()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[SpecialTileSceneController] menuSceneName is empty.");
        }
    }

    private void RefreshEquippedSlots()
    {
        var eq = SpecialTileSystem.GetEquipped() ?? new List<SpecialTileSystem.Entry>();
        int slots = SpecialTileSystem.GetEquipSlotsUnlocked();

        for (int i = 0; i < equippedSlotImages.Length; i++)
        {
            var img = equippedSlotImages[i];
            var info = (equippedSlotInfoTMPs != null && i < equippedSlotInfoTMPs.Length) ? equippedSlotInfoTMPs[i] : null;

            if (!img) continue;
            bool hasEntry = (i < slots && i < eq.Count);

            if (hasEntry)
            {
                string id = eq[i].TileId();
                var sp = LoadTileSprite(id);
                if (sp)
                {
                    img.sprite = sp;
                    img.enabled = true;
                }
                if (info) info.text = BuildEntryText(eq[i]);
            }
            else
            {
                img.enabled = false;
                img.sprite = null;
                if (info) info.text = "";
            }
// ★装備枠の選択中ハイライト：色変更ではなく「背景ImageをON/OFF」する
// ここでONにする背景Imageは、各装備枠に手動で置いたハイライト用ImageをInspectorで割り当てる想定
var bg = (equippedSlotBackgroundImages != null && i < equippedSlotBackgroundImages.Length)
    ? equippedSlotBackgroundImages[i]
    : null;

if (bg)
{
    bool unlocked = (i < slots);
    bool shouldOn = unlocked && (i == _selectedEquippedSlot) && hasEntry;

    if (bg.gameObject.activeSelf != shouldOn)
        bg.gameObject.SetActive(shouldOn);
}
        }
    }

    private Sprite LoadTileSprite(string tileIdNoExt)
    {
        if (string.IsNullOrEmpty(tileIdNoExt)) return null;

        // ★Legendary効果 suffix を落として Sprite を共通化
        string spriteKey = SpecialTileRuntime.SpriteKeyFromTileId(tileIdNoExt);

        string path = string.IsNullOrEmpty(tileSpriteResourcesPath)
            ? spriteKey
            : (tileSpriteResourcesPath + spriteKey);

        var sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            Debug.LogWarning($"[SpecialTileSceneController] Sprite not found: Resources.Load<Sprite>(\"{path}\")");
        }
        return sp;
    }
static string BuildEntryText(SpecialTileSystem.Entry e)
{
    string rarityKey = SpecialTileSystem.RarityKey(e.rarity);
    string rarityLabel = RarityColoredText(rarityKey, e.rarity);

    string doraText = GetSpecialTileFixedText_Local("special_tile_dora_plus_one");
    string fx = "";

    if (e.rarity == SpecialTileSystem.Rarity.Normal)
    {
        fx = doraText;
    }
    else
    {
        string bonusText = "";
        try
        {
            var dict = SpecialTileSystem.UnpackTraitBonus(e.traitBonusPacked);
            var parts = new List<string>();
            foreach (var kv in dict)
            {
                string k = (kv.Key ?? "").Trim();
                int v = Mathf.Max(0, kv.Value);
                if (string.IsNullOrEmpty(k) || v <= 0) continue;
                parts.Add($"{LocalizeSpecialTileYakuName_Local(k)}+{v}");
            }
            bonusText = (parts.Count > 0) ? string.Join(" ", parts) : "";
        }
        catch
        {
            bonusText = "";
        }

        if (e.rarity == SpecialTileSystem.Rarity.Legendary)
        {
            string lfx = LegendaryEffectText(e.effectId);
            if (!string.IsNullOrEmpty(bonusText))
                fx = $"{doraText} / {bonusText}\n<color=#ff3333>{lfx}</color>";
            else
                fx = $"{doraText}\n<color=#ff3333>{lfx}</color>";
        }
        else
        {
            if (!string.IsNullOrEmpty(bonusText))
                fx = $"{doraText} / {bonusText}";
            else
                fx = doraText;
        }
    }

    return $"{rarityLabel}\n{fx}";
}
private static string LegendaryEffectText(int effectId)
{
    if (effectId == 1) return GetSpecialTileFixedText_Local("special_tile_legendary_effect_1");
    if (effectId == 2) return GetSpecialTileFixedText_Local("special_tile_legendary_effect_2");
    if (effectId == 3) return GetSpecialTileFixedText_Local("special_tile_legendary_effect_3");
    if (effectId == 4) return GetSpecialTileFixedText_Local("special_tile_legendary_effect_4");
    if (effectId == 5) return GetSpecialTileFixedText_Local("special_tile_legendary_effect_5");
    if (effectId == 6) return GetSpecialTileFixedText_Local("special_tile_legendary_effect_6");
    return GetSpecialTileFixedText_Local("special_tile_legendary_effect_unknown");
}
private static string NormalizeRarityKey(string rarityRaw)
{
    if (string.IsNullOrEmpty(rarityRaw)) return rarityRaw;

    string r = rarityRaw.Trim();

    // lower-case inputs (special tile ids etc.)
    if (r == "legendary") return "Legendary";
    if (r == "epic") return "Epic";
    if (r == "rare") return "Rare";
    if (r == "common") return "Common";
    if (r == "normal") return "Normal";

    // japanese labels
    if (r == "レジェンダリー") return "Legendary";
    if (r == "エピック") return "Epic";
    if (r == "レア") return "Rare";
    if (r == "コモン") return "Common";
    if (r == "ノーマル") return "Normal";

    // already normalized english labels
    if (r == "Legendary") return "Legendary";
    if (r == "Epic") return "Epic";
    if (r == "Rare") return "Rare";
    if (r == "Common") return "Common";
    if (r == "Normal") return "Normal";

    return r;
}

private static Color GetRarityColorSafe(string rarityKeyOrRaw, string japaneseLabel)
{
    // 1) english normalized key (Legendary/Epic/Rare/Common/Normal)
    string key = NormalizeRarityKey(rarityKeyOrRaw);

    try
    {
        var c = OfudaRarityColors.Get(key);
        // if returned white for non-normal, treat as unsupported
        if (!(c == Color.white && key != "Normal"))
            return c;
    }
    catch { }

    // 2) japanese label key
    if (!string.IsNullOrEmpty(japaneseLabel))
    {
        try
        {
            var c2 = OfudaRarityColors.Get(japaneseLabel);
            if (!(c2 == Color.white && japaneseLabel != "ノーマル"))
                return c2;
        }
        catch { }
    }

    // 3) final fallback fixed colors
    switch (japaneseLabel)
    {
        case "レジェンダリー": return new Color(1.00f, 0.55f, 0.00f); // orange
        case "エピック":       return new Color(0.60f, 0.20f, 1.00f); // purple
        case "レア":           return new Color(1.00f, 0.85f, 0.00f); // yellow
        case "コモン":         return new Color(0.20f, 0.60f, 1.00f); // blue
        case "ノーマル":       return Color.white;
    }

    switch (key)
    {
        case "Legendary": return new Color(1.00f, 0.55f, 0.00f);
        case "Epic":      return new Color(0.60f, 0.20f, 1.00f);
        case "Rare":      return new Color(1.00f, 0.85f, 0.00f);
        case "Common":    return new Color(0.20f, 0.60f, 1.00f);
        case "Normal":    return Color.white;
    }

    return Color.white;
}

private static string RarityColoredText(string rarityKey, SpecialTileSystem.Rarity r)
{
    string label = GetSpecialTileRarityLabel_Local(r);

    Color c = GetRarityColorSafe(rarityKey, label);
    string hex = ColorUtility.ToHtmlStringRGB(c);
    return $"<color=#{hex}>{label}</color>";
}
private static string BuildOwnedText(List<SpecialTileSystem.Entry> owned)
{
    if (owned == null || owned.Count == 0) return GetSpecialTileFixedText_Local("special_tile_owned_none");
    return GetSpecialTileFixedText_Local("special_tile_owned_header") + "\n" + string.Join("\n", owned.ConvertAll(BuildEntryText));
}
private static string BuildEquippedText(List<SpecialTileSystem.Entry> eq)
{
    int slots = SpecialTileSystem.GetEquipSlotsUnlocked();
    if (eq == null) eq = new List<SpecialTileSystem.Entry>();

    string s = string.Format(GetSpecialTileFixedText_Local("special_tile_equipped_slots_format"), eq.Count, slots) + "\n";
    if (eq.Count == 0) return s + GetSpecialTileFixedText_Local("special_tile_equipped_none");
    return s + string.Join("\n", eq.ConvertAll(BuildEntryText));
}
}
