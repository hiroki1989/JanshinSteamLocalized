using System;                    // ★追加：StringComparison など
using System.Collections.Generic; // ★追加：List<> / Dictionary<> などのコレクション
using System.Linq;               // ★追加：Any / Where / Select など
using System.Reflection;         // ★追加：BindingFlags を使うため
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;            // Image, LayoutGroup 等
using TMPro;                     // TextMeshProUGUI 用

public partial class UpgradeManager : MonoBehaviour
{

    [Header("Next Scene")]
    [SerializeField] private string dialogueSceneName = "EnemyDialogue";

    [Header("Costs (Inspector 可変)")]
    [SerializeField] private int buyCost = 300;
    [SerializeField] private int rerollBuyCost = 100;
    [SerializeField] private int destroyCost = 100;
    [SerializeField] private int rerollDestroyCost = 50;
    [SerializeField] private int minDeckSize = 80;
    [Header("Costs Increase (per purchase)")]
    [SerializeField] private int buyCostIncrease = 0;
    [SerializeField] private int rerollBuyCostIncrease = 0;
    [SerializeField] private int destroyCostIncrease = 0;
    [SerializeField] private int rerollDestroyCostIncrease = 0;

[Header("UI Refs")]
[SerializeField] private TMP_Text goldTMP;
[SerializeField] private TMP_Text buyCostTMP;
[SerializeField] private TMP_Text rerollBuyCostTMP;
[SerializeField] private TMP_Text destroyCostTMP;
[SerializeField] private TMP_Text rerollDestroyCostTMP;
[Header("SE (Gold Spend)")]
[SerializeField] private AudioSource goldSpendSESource;   // 共通SEを鳴らすAudioSource
[SerializeField] private AudioClip goldSpendSEClip;       // ゴールド消費時の共通SE
// ▼Upgradeボタン（Inspector で割当推奨／未割当でも既存の割当は維持）
[SerializeField] private Button buyButton;
[SerializeField] private Button destroyButton;
[SerializeField] private Button rerollBuyButton;       // 購入側リロール
[SerializeField] private Button rerollDestroyButton;   // 破壊側リロール

// ▼左上の旧「0G」テキスト（任意）。割り当てた場合は自動で非表示にします。
[SerializeField] private TMP_Text obsoleteGoldTMP;


// ▼候補牌の「名前テキスト」は任意（未割当OK。使わないなら空でOK）
[SerializeField] private TMP_Text buyTileNameTMP;      // ★追加（参照の整合用）
[SerializeField] private TMP_Text destroyTileNameTMP;  // ★追加（参照の整合用）

// ▼候補牌の「画像」
[SerializeField] private Image    buyTileImage;
[SerializeField] private Image    destroyTileImage;

// === NEW: グループ表示用の親（子に Image を生成して横並び表示） ===
[SerializeField] private RectTransform buyGroupRoot;
[SerializeField] private RectTransform destroyGroupRoot;

[Header("Offer Group Layout")]
[SerializeField, Min(0f)] private float offerTileWidth = 0f;   // 0=自動（高さや画像アスペクトに任せる）
[SerializeField, Min(0f)] private float offerTileHeight = 64f; // 0=自動
[SerializeField] private bool  offerTileKeepAspect = true;      // 幅&高さの両方>0なら自動で矩形化（=false扱い）
[SerializeField] private float offerTileSpacing   = 0f;         // 牌どうしの間隔（px）
[Header("Deck View (AUTO)")]
[SerializeField] private GameObject deckPanel;         // 画面全体を覆うパネル（無ければ自動生成）
[SerializeField] private RectTransform deckPanelRoot;  // コンテンツ領域（空なら自動で Content を作る）

[Header("Deck View (MANUAL)")]
[SerializeField] private bool deckPanelUseManualUI = false;

// 手動UIで使う「パネル本体」「閉じるボタン」
[SerializeField] private GameObject manualDeckPanel;
[SerializeField] private Button manualDeckCloseButton;

// 牌34種ぶん（index 0..33）のアイコンと枚数テキスト
[SerializeField] private Image[] manualDeckTileIcons = new Image[34];
[SerializeField] private TMP_Text[] manualDeckTileCounts = new TMP_Text[34];


// スタイル（任意）
[SerializeField] private TMP_FontAsset deckFont;
[SerializeField] private int deckFontSize = 28;
[SerializeField] private int rowSpacing = 24;           // 行間
[SerializeField] private int cellSpacing = 10;          // セル横間隔
[SerializeField] private float tileAspectWH = 47f / 63f; // 牌の横/縦 ≈0.746
[SerializeField] private float rowLabelWidth = 64f;      // 行ラベル幅（萬/筒/索/字）

[Header("Deck View Labels")]
[SerializeField] private string[] suitRowLabels = new string[4] { "", "", "", "" };

[Header("Deck Panel Background")]
[SerializeField] private Sprite panelBackgroundSprite;                       // 背景スプライト（任意）
[SerializeField] private Color  panelBackgroundColor = Color.black;          // 背景色（スプライト無い時の色）
[SerializeField] private Image.Type panelBackgroundType = Image.Type.Sliced; // Sliced を推奨

[Header("Tile Area (Content)")]
[SerializeField] private bool   tileAreaUseFixedSize = false;                // true なら固定サイズで中央配置
[SerializeField] private Vector2 tileAreaFixedSize = new Vector2(0f, 0f);    // 固定サイズ（W,H）
[SerializeField] private Vector4 tileAreaPaddingLRBT = new Vector4(20,20,20,80);
// 左/右/上/下パディング（固定サイズを使わない時）

[Header("OK Button")]
[SerializeField] private Sprite  okButtonSprite;                             // ボタンの画像（任意）
[SerializeField] private Vector2 okButtonSize = new Vector2(220f, 60f);      // ボタンサイズ（W,H）
[SerializeField] private string  okButtonText = "OK";                        // ボタンテキスト

// === [Run Upgrades] Inspectorで調整可能にする ===
[Header("Run Upgrades")]
[SerializeField] private int hpUpCost  = 500;
[SerializeField] private int hpUpValue = 500;

[SerializeField] private int mpUpCost  = 500;
[SerializeField] private int mpUpValue = 500;

[SerializeField] private int castUpCost  = 500;
[SerializeField] private int castUpValue = 1;
    [Header("Run Upgrade Cost Increase (per purchase)")]
    [SerializeField] private int hpUpCostIncrease = 0;
    [SerializeField] private int mpUpCostIncrease = 0;
    [SerializeField] private int castUpCostIncrease = 0;

// 価格・現在値のラベル（任意）
[SerializeField] private TMP_Text hpUpLabel;
[SerializeField] private TMP_Text mpUpLabel;
[SerializeField] private TMP_Text castUpLabel;

// 購入ボタン
[SerializeField] private Button buyHpButton;
[SerializeField] private Button buyMpButton;
[SerializeField] private Button buyCastButton;

[Header("Split-Mode (Optional)")]
[SerializeField] private GameObject statusShopRoot; // ステータス強化UI一式の親（任意）
[SerializeField] private GameObject deckShopRoot;   // デッキ構築UI一式の親（任意）
[Header("Trait Yaku Unlock Prices (by difficulty)")]
[SerializeField] private int traitUnlockPriceEasy = 1200;
[SerializeField] private int traitUnlockPriceNormal = 1000;
[SerializeField] private int traitUnlockPriceHard = 750;
[SerializeField] private int traitUnlockPriceYakuman = 500;

[Header("Trait Yaku Upgrade Pricing")]
[SerializeField] private int traitUpgradeBaseCost = 500;
[SerializeField] private float traitUpgradeCostMultiplier = 1.5f;

[Header("Trait Yaku Upgrade Delta (per level)")]
[SerializeField] private float traitUpgradeDeltaGeki = 0.10f; // 例：撃の倍率加算
[SerializeField] private float traitUpgradeDeltaShun = 0.05f; // 例：瞬の回復%加算
[SerializeField] private float traitUpgradeDeltaIyu  = 0.02f; // 例：癒の回復%加算

[Header("Trait Yaku Shop UI")]
[SerializeField] private GameObject traitYakuShopRoot;
[SerializeField] private TMPro.TMP_Text traitTitleTMP;
[SerializeField] private TMPro.TMP_Text traitUnlockLabelTMP;
[SerializeField] private TMPro.TMP_Text traitUpgradeLabelTMP;
[SerializeField] private TMPro.TMP_Text traitUnlockOfferTMP;
[SerializeField] private TMPro.TMP_Text traitUpgradeOfferTMP;
[SerializeField] private Button traitUnlockButton;
[SerializeField] private Button traitUpgradeButton;
// ★追加：Trait（撃/瞬/癒）アイコン表示
[Header("Trait Yaku Shop Icons")]
[SerializeField] private Image traitUpgradeTraitIconImage; // 強化オファーに表示するアイコン（Image）
[SerializeField] private Sprite traitIconGeki;             // 撃アイコン
[SerializeField] private Sprite traitIconShun;             // 瞬アイコン
[SerializeField] private Sprite traitIconIyu;              // 癒アイコン
[SerializeField] private Color traitIconColorGeki = Color.white; // 撃アイコン色
[SerializeField] private Color traitIconColorShun = Color.white; // 瞬アイコン色
[SerializeField] private Color traitIconColorIyu  = Color.white; // 癒アイコン色

[Header("Gem Result Panel (宝石獲得結果)")]
[SerializeField] private GameObject gemResultPanelRoot;
[SerializeField] private TMPro.TMP_Text gemResultTMP;
[SerializeField] private Button gemResultOkButton;

[Header("SE (Upgrade Result)")]
[SerializeField] private AudioSource upgradeResultSESource;   // 結果系SEを鳴らすAudioSource
[SerializeField] private AudioClip gemGetSE;                  // 宝石獲得SE
[SerializeField] private AudioClip uniqueOmamoriGetSE;        // 神器獲得SE
// ★追加：Run回復（StatusRootで購入）
[Header("Run Heals (StatusRoot)")]
[SerializeField] private int healHpValue = 10;
[SerializeField] private int healHpCost = 100;
[SerializeField] private int healMpValue = 5;
[SerializeField] private int healMpCost = 100;
    [Header("Run Heal Cost Increase (per purchase)")]
    [SerializeField] private int healHpCostIncrease = 0;
    [SerializeField] private int healMpCostIncrease = 0;

[SerializeField] private UnityEngine.UI.Button buyHealHpButton;
[SerializeField] private UnityEngine.UI.Button buyHealMpButton;
[SerializeField] private TMPro.TMP_Text healHpLabel;
[SerializeField] private TMPro.TMP_Text healMpLabel;

[SerializeField] private TMP_Text currentHpTMP;
[SerializeField] private TMP_Text currentMpTMP;

[System.Serializable]
private class OmamoriEffectScaleEntry
{
    public PlayerData.OmamoriEffect effect;
    public float basePercentAtLevel1 = 3f;
    public float percentPerLevel = 1f;
}

[Header("Omamori Balance (Inspector -> PlayerData)")]
[SerializeField] private bool applyOmamoriEffectScalesOnAwake = true;
[SerializeField] private List<OmamoriEffectScaleEntry> omamoriEffectScales = new List<OmamoriEffectScaleEntry>();

private readonly Dictionary<Button, bool> _gemPrevInteractable = new Dictionary<Button, bool>();
private bool _gemPanelShowing = false;
private const string PrefKey_PendingGemRoll = "Gem_PendingRoll";
private const string PrefKey_PendingGemEnemyExcelKey = "Gem_PendingEnemyExcelKey";
private const string PrefKey_PendingGemEnemyName = "Gem_PendingEnemyName";
private const string PrefKey_PendingGemIsZeus = "Gem_PendingIsZeus";
private static string PrefKey_FirstDefeatedForExcelKey(int excelKey) => "Gem_FirstDefeated_" + excelKey.ToString();
// ===== Cost Scaling (per purchase) =====
private const string PrefKey_CostCount_Buy            = "Run_UpgradeCostCount_Buy";
private const string PrefKey_CostCount_RerollBuy      = "Run_UpgradeCostCount_RerollBuy";
private const string PrefKey_CostCount_Destroy        = "Run_UpgradeCostCount_Destroy";
private const string PrefKey_CostCount_RerollDestroy  = "Run_UpgradeCostCount_RerollDestroy";

private const string PrefKey_CostCount_HpUp           = "Run_UpgradeCostCount_HpUp";
private const string PrefKey_CostCount_MpUp           = "Run_UpgradeCostCount_MpUp";
private const string PrefKey_CostCount_CastUp         = "Run_UpgradeCostCount_CastUp";

private const string PrefKey_CostCount_HealHp         = "Run_UpgradeCostCount_HealHp";
private const string PrefKey_CostCount_HealMp         = "Run_UpgradeCostCount_HealMp";
private float __GetTraitUpgradeDeltaLocal(SkillSetAsset.Trait trait)
{
    switch (trait)
    {
        case SkillSetAsset.Trait.Geki: return Mathf.Max(0f, traitUpgradeDeltaGeki);
        case SkillSetAsset.Trait.Shun: return Mathf.Max(0f, traitUpgradeDeltaShun);
        case SkillSetAsset.Trait.Iyu:  return Mathf.Max(0f, traitUpgradeDeltaIyu);
        default: return 0f;
    }
}
private static string GetUpgradeTextOrFallback_Local(string key, string fallback)
{
    string v = GetUpgradeFixedText_Local(key);
    if (string.IsNullOrEmpty(v)) return fallback ?? "";
    if (string.Equals(v, "fixed." + key, StringComparison.Ordinal)) return fallback ?? "";
    return v;
}

private static void SetButtonChildText_Local(Button button, string text)
{
    if (button == null) return;

    try
    {
        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text ?? "";
    }
    catch
    {
    }
}
private static string NormalizeUpgradeYakuKey_Local(string yakuName)
{
    if (string.IsNullOrWhiteSpace(yakuName))
        return "";

    string s = yakuName.Trim().Replace("　", " ");
    s = s.Replace('（', '(').Replace('）', ')');

    int p0 = s.IndexOf('(');
    if (p0 >= 0)
        s = s.Substring(0, p0);

    s = s.Trim();
    if (string.IsNullOrWhiteSpace(s))
        return "";

    if (s.StartsWith("yaku.", StringComparison.OrdinalIgnoreCase))
    {
        string tail = s.Substring("yaku.".Length).Trim();
        return tail.ToUpperInvariant();
    }

    if (s.StartsWith("yakuman.", StringComparison.OrdinalIgnoreCase))
    {
        string tail = s.Substring("yakuman.".Length).Trim();
        return tail.ToUpperInvariant();
    }

    if (s == "風牌") return "YAKUHAI";
    if (s.StartsWith("風牌", StringComparison.Ordinal)) return "YAKUHAI";
    if (s == "役牌") return "YAKUHAI";
    if (s.StartsWith("役牌", StringComparison.Ordinal)) return "YAKUHAI";

    if (s == "平和" || s == "ピンフ" || s.Equals("Pinfu", StringComparison.OrdinalIgnoreCase)) return "PINFU";
    if (s == "タンヤオ" || s == "断么九" || s == "断幺九" || s.Equals("Tanyao", StringComparison.OrdinalIgnoreCase)) return "TANYAO";
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

private static bool IsLocalizationKeyHit_Local(string localized, string expectedRawKey)
{
    if (string.IsNullOrEmpty(localized))
        return false;

    if (string.IsNullOrEmpty(expectedRawKey))
        return true;

    return !string.Equals(localized, expectedRawKey, StringComparison.Ordinal);
}

private static string LocalizeUpgradeYakuDisplay_Local(string yakuName)
{
    if (string.IsNullOrWhiteSpace(yakuName))
        return "";

    string normalized = NormalizeUpgradeYakuKey_Local(yakuName);
    if (string.IsNullOrWhiteSpace(normalized))
        return yakuName.Trim();

    string yakuText = LocalizationManager.Yaku(normalized);
    if (IsLocalizationKeyHit_Local(yakuText, "yaku." + normalized))
        return yakuText;

    string yakumanText = LocalizationManager.Yakuman(normalized);
    if (IsLocalizationKeyHit_Local(yakumanText, "yakuman." + normalized))
        return yakumanText;

    return yakuName.Trim();
}
private void ApplyTraitOnlyLocalization_Local()
{
    if (traitTitleTMP)
        traitTitleTMP.text = GetUpgradeTextOrFallback_Local("trait_shop_title", "役強化");

    if (traitUnlockLabelTMP)
        traitUnlockLabelTMP.text = GetUpgradeTextOrFallback_Local("trait_unlock_label", "解放");

    if (traitUpgradeLabelTMP)
        traitUpgradeLabelTMP.text = GetUpgradeTextOrFallback_Local("trait_upgrade_label", "強化");

    if (traitUnlockButton)
        SetButtonChildText_Local(
            traitUnlockButton,
            GetUpgradeTextOrFallback_Local("trait_unlock_button", "解放"));

    if (traitUpgradeButton)
        SetButtonChildText_Local(
            traitUpgradeButton,
            GetUpgradeTextOrFallback_Local("trait_upgrade_button", "強化"));
}
private float __CalcTraitEffectAdd01(SkillSetAsset hostSet, SkillSetAsset.Trait trait, string yakuName, int lvForEffect)
{
    if (hostSet == null) return 0f;

    // 仕様：Lv0表示でも効果量はLv1相当
    lvForEffect = Mathf.Max(1, lvForEffect);

    float delta = 0f;
    try
    {
        switch (trait)
        {
            case SkillSetAsset.Trait.Geki: delta = Mathf.Max(0f, traitUpgradeDeltaGeki); break;
            case SkillSetAsset.Trait.Shun: delta = Mathf.Max(0f, traitUpgradeDeltaShun); break;
            case SkillSetAsset.Trait.Iyu:  delta = Mathf.Max(0f, traitUpgradeDeltaIyu);  break;
            default: delta = 0f; break;
        }
    }
    catch { delta = 0f; }

    // === 難易度別テーブルを hostSet から取得（GameManager と同じ “複数候補名” 方式） ===
    float[] table = null;
    bool tableIsMultiplier = false;

    try
    {
        var tp = hostSet.GetType();

        var flags = System.Reflection.BindingFlags.Instance
                  | System.Reflection.BindingFlags.Public
                  | System.Reflection.BindingFlags.NonPublic;

        float[] GetFloatArrayByAnyName(params string[] names)
        {
            if (names == null) return null;

            for (int i = 0; i < names.Length; i++)
            {
                var n = names[i];
                if (string.IsNullOrEmpty(n)) continue;

                var f = tp.GetField(n, flags);
                if (f == null) continue;

                var v = f.GetValue(hostSet) as float[];
                if (v != null) return v;
            }
            return null;
        }

        switch (trait)
        {
            case SkillSetAsset.Trait.Geki:
                table = GetFloatArrayByAnyName(
                    "gekiDamageMulByDiff",
                    "gekiMultiplierByDiff",
                    "gekiMulByDiff"
                );
                tableIsMultiplier = true; // 倍率（例: 1.20）
                break;

            case SkillSetAsset.Trait.Shun:
                table = GetFloatArrayByAnyName(
                    "shunMpHealMulByDiff",
                    "shunMpPctByDiff",
                    "shunMpRateByDiff"
                );
                tableIsMultiplier = false; // ％（例: 0.10）
                break;

            case SkillSetAsset.Trait.Iyu:
                table = GetFloatArrayByAnyName(
                    "iyuHealMulByDiff",
                    "iyuHealPctByDiff",
                    "iyuHealRateByDiff"
                );
                tableIsMultiplier = false; // ％（例: 0.10）
                break;
        }
    }
    catch
    {
        table = null;
        tableIsMultiplier = false;
    }

    // === difficulty を traitMap から取得してベース値を決める ===
    float add = 0f;

    if (table != null && table.Length > 0 && hostSet.traitMap != null)
    {
        int di = 0;

        try
        {
            var key = NormalizeUpgradeYakuKey_Local(yakuName);

            var entry = hostSet.traitMap.FirstOrDefault(t =>
                t != null &&
                t.trait == trait &&
                !string.IsNullOrEmpty(t.yakuName) &&
                string.Equals(NormalizeUpgradeYakuKey_Local(t.yakuName), key, StringComparison.Ordinal));

            if (entry != null)
                di = Mathf.Clamp((int)entry.difficulty, 0, table.Length - 1);
        }
        catch
        {
            di = 0;
        }

        float v = Mathf.Max(0f, table[Mathf.Clamp(di, 0, table.Length - 1)]);
        add = tableIsMultiplier ? Mathf.Max(0f, v - 1f) : v;
    }
    else
    {
        // テーブルが取れない場合：ここが 0% 連発の原因になっていた
        add = 0f;
    }

    // === Δ：Lv2から (Lv-1)×Δ を加算 ===
    if (delta > 0f)
    {
        int deltaLv = Mathf.Max(0, lvForEffect - 1);
        add += delta * deltaLv;
    }

    return Mathf.Max(0f, add);
}
private string __FormatPct(float add01)
{
    float pct = Mathf.Max(0f, add01) * 100f;
    if (Mathf.Abs(pct - Mathf.Round(pct)) < 0.0001f)
        return $"{Mathf.RoundToInt(pct)}%";
    return $"{pct:0.##}%";
}
private void PlayUpgradeResultSE(AudioClip clip)
{
    if (upgradeResultSESource != null && clip != null)
    {
        try { upgradeResultSESource.PlayOneShot(clip); } catch { }
    }
}
private const string PrefKey_TraitBonusPairs = "PF_LastSpecialTileTraitBonusPairs";

private static string GetUpgradeFixedText_Local(string key)
{
    return LocalizationManager.Fixed(key);
}
private static string BuildUpgradeWithTotalLabel_Local(string prefixKey, int addValue, int cost, int totalBonus)
{
    return GetUpgradeFixedText_Local(prefixKey)
         + addValue.ToString()
         + GetUpgradeFixedText_Local("label_separator")
         + cost.ToString()
         + GetUpgradeFixedText_Local("total_bonus_prefix")
         + totalBonus.ToString()
         + GetUpgradeFixedText_Local("total_bonus_suffix");
}

private static string BuildHealLabel_Local(string prefixKey, int addValue, int cost)
{
    return GetUpgradeFixedText_Local(prefixKey)
         + addValue.ToString()
         + GetUpgradeFixedText_Local("label_separator")
         + cost.ToString();
}

private static string BuildGemResultText_Local(string enemyName, int gained)
{
    string rewardText = GetUpgradeFixedText_Local("gem_gain_prefix")
                      + gained.ToString()
                      + GetUpgradeFixedText_Local("gem_gain_middle");

    if (!string.IsNullOrEmpty(enemyName))
        return enemyName + GetUpgradeFixedText_Local("enemy_defeat_suffix_emphatic") + "\n" + rewardText;

    return rewardText;
}

// ★追加：強化画面の現在HP/MP（現在値/最大値）表示を更新
private void RefreshCurrentHpMpText()
{
    // -------------------------
    // HP
    // -------------------------
    int hpCur = 0;
    int hpMax = 0;

    // まず PlayerPrefs（UpgradeScene中にGameManagerがいないケースがあるため）
    try
    {
        if (PlayerPrefs.HasKey("Run_PlayerHP"))
        {
            hpCur = PlayerPrefs.GetInt("Run_PlayerHP", 0);
        }
        if (PlayerPrefs.HasKey("Run_PlayerMaxHP"))
        {
            hpMax = PlayerPrefs.GetInt("Run_PlayerMaxHP", hpCur);
        }
    }
    catch { }

    // GameManager がいるならそちらを優先（より正確）
    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (gm)
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            var tp = gm.GetType();
            var fMax = tp.GetField("playerMaxHP", BF);
            var fCur = tp.GetField("playerHP", BF);
            if (fMax != null && fCur != null && fMax.FieldType == typeof(int) && fCur.FieldType == typeof(int))
            {
                hpMax = (int)fMax.GetValue(gm);
                hpCur = (int)fCur.GetValue(gm);
            }
        }
        catch { }
    }

    // 表示（最大値が取れないときは「現在=最大」として破綻しない表示にする）
    if (hpMax <= 0) hpMax = hpCur;
    if (currentHpTMP) currentHpTMP.text = $"{hpCur}/{hpMax}";

    // -------------------------
    // MP
    // -------------------------
    int mpCur = 0;
    int mpMax = 0;

    // PlayerPrefs 優先（GM不在対策）
    try
    {
        if (PlayerPrefs.HasKey("Run_PlayerMP"))
        {
            mpCur = PlayerPrefs.GetInt("Run_PlayerMP", 0);
        }
        if (PlayerPrefs.HasKey("Run_PlayerMaxMP"))
        {
            mpMax = PlayerPrefs.GetInt("Run_PlayerMaxMP", mpCur);
        }
    }
    catch { }

    // GameManager がいるなら、EffectiveMaxMP と _mp を使う（このファイル内でも同方針）
    if (gm)
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            var tp = gm.GetType();
            var miMax = tp.GetMethod("EffectiveMaxMP", BF);
            var fCur = tp.GetField("_mp", BF);

            if (miMax != null && miMax.ReturnType == typeof(int) && fCur != null && fCur.FieldType == typeof(int))
            {
                mpMax = (int)miMax.Invoke(gm, null);
                mpCur = (int)fCur.GetValue(gm);
            }
        }
        catch { }
    }

    if (mpMax <= 0) mpMax = mpCur;
    if (currentMpTMP) currentMpTMP.text = $"{mpCur}/{mpMax}";
}
private int __GetLastSpecialTileTraitBonusForYaku(string yakuName)
{
    if (string.IsNullOrEmpty(yakuName)) return 0;

    try
    {
        string target = NormalizeUpgradeYakuKey_Local(yakuName);
        if (string.IsNullOrEmpty(target)) return 0;

        int bonus = 0;
        Dictionary<string, int> map = null;

        try
        {
            map = SpecialTileSystem.GetEquippedTraitBonusMap();
        }
        catch
        {
            map = null;
        }

        if (map == null || map.Count <= 0)
            return 0;

        foreach (var kv in map)
        {
            string k = NormalizeUpgradeYakuKey_Local(kv.Key);
            if (string.IsNullOrEmpty(k)) continue;
            if (!string.Equals(k, target, StringComparison.OrdinalIgnoreCase)) continue;

            bonus += Mathf.Max(0, kv.Value);
        }

        return Mathf.Max(0, bonus);
    }
    catch
    {
        return 0;
    }
}
private int GetScaledCost(int baseCost, int increasePerPurchase, string countKey)
{
    int count = 0;
    try { count = Mathf.Max(0, PlayerPrefs.GetInt(countKey, 0)); } catch { count = 0; }

    long raw = (long)Mathf.Max(0, baseCost) + (long)Mathf.Max(0, increasePerPurchase) * (long)count;
    if (raw < 0) raw = 0;
    if (raw > int.MaxValue) raw = int.MaxValue;

    // Tier倍率：Tier2以降は Tier1 の価格から +0.3倍ずつ（Tier2=1.3x, Tier3=1.6x ...）
    int tier = 1;
    try { tier = Mathf.Max(1, PlayerPrefs.GetInt("PF_CurrentTier", 1)); } catch { tier = 1; }
    float mult = 1f + 0.3f * (tier - 1);

    double scaledD = (double)raw * (double)mult;
    long scaled = 0;
    try
    {
        scaled = (long)Math.Round(scaledD, MidpointRounding.AwayFromZero);
    }
    catch
    {
        scaled = raw;
    }

    if (scaled < 0) scaled = 0;
    if (scaled > int.MaxValue) scaled = int.MaxValue;
    return (int)scaled;
}

private void IncrementPurchaseCount(string countKey)
{
    try
    {
        int count = Mathf.Max(0, PlayerPrefs.GetInt(countKey, 0));
        PlayerPrefs.SetInt(countKey, count + 1);
        PlayerPrefs.Save();
    }
    catch { }
}

// ★excelKey が取れない場合のフォールバック：敵名で初回撃破判定する
private static string PrefKey_FirstDefeatedForEnemyName(string enemyBaseName) => "Gem_FirstDefeated_Name_" + enemyBaseName;

// ★excelKey が取れる場合は、まずこちらで初回撃破判定する
private static string PrefKey_FirstDefeatedForEnemyExcelKey(int excelKey) => "Gem_FirstDefeated_Excel_" + excelKey;

// "アマテラス +1" のような周回サフィックスを初回判定キーから除外する
private static string StripLoopSuffix(string name)
{
    if (string.IsNullOrEmpty(name)) return name;

    int p = name.LastIndexOf(" +", StringComparison.Ordinal);
    if (p < 0) return name;

    string tail = name.Substring(p + 2);
    for (int i = 0; i < tail.Length; i++)
    {
        if (tail[i] < '0' || tail[i] > '9') return name;
    }

    return name.Substring(0, p).TrimEnd();
}
public enum UpgradeSectionMode { All, StatusOnly, DeckOnly, TraitOnly }

private int CurrentGold => GameManager.RunCurrency.Get();

private GridLayoutGroup[] _rowGrids = new GridLayoutGroup[4];
private TMP_Text[] _rowLabelTexts = new TMP_Text[4];
private readonly int[] _rowCols = new int[] { 9, 9, 9, 7 }; // 各行の列数(萬/筒/索/字)
private Image[] _manIcons,  _pinIcons,  _souIcons,  _honorIcons;
private TMP_Text[] _manCount, _pinCount, _souCount, _honorCount;
private bool _deckBuilt = false;

// 自動生成したOKボタン（再生成防止）
private Button _deckOkButton;


// 旧フィールドは互換のため残すが未使用（リロール時などで初期化にのみ利用）
private int offerBuyIndex   = -1;
private int offerDestroyIdx = -1;

private System.Random rng = new System.Random();

// === NEW: オファーは「3枚 or 4枚」の牌インデックス群 ===
private List<int> offerBuyGroup     = new List<int>();   // 例) [0,1,2] = 萬1-2-3
private List<int> offerDestroyGroup = new List<int>();   // 例) [27,28,29,30] = 東南西北

private void Awake()
{
    if (!applyOmamoriEffectScalesOnAwake) return;
    ApplyOmamoriEffectScalesToPlayerData();
}

private void ApplyOmamoriEffectScalesToPlayerData()
{
    var rows = new List<PlayerData.RuntimeOmamoriEffectScale>();

    if (omamoriEffectScales != null)
    {
        foreach (var e in omamoriEffectScales)
        {
            rows.Add(new PlayerData.RuntimeOmamoriEffectScale
            {
                effect = e.effect,
                basePercentAtLevel1 = e.basePercentAtLevel1,
                percentPerLevel = e.percentPerLevel
            });
        }
    }

    PlayerData.SetRuntimeEffectScales(rows);
}
private void Start()
{
EnsureOffers();
EnsureWalletLoaded();
WireButtonsIfAssigned();
HideObsoleteGoldIfAssigned();
RefreshUI();
RefreshCurrentHpMpText();
    // ★追加：分割表示モード (PlayerPrefs) を起動時に適用。未設定なら All（後方互換）。
    var m = (PlayerPrefs.GetString("UpgradeSectionMode", "ALL") ?? "ALL").ToUpperInvariant();
    if      (m == "STATUS") ApplySectionMode(UpgradeSectionMode.StatusOnly);
    else if (m == "DECK")   ApplySectionMode(UpgradeSectionMode.DeckOnly);
    else                    ApplySectionMode(UpgradeSectionMode.All);

    // RunScene(=GameManager) が読む上昇幅を保存
    PlayerPrefs.SetString("PF_TraitUpgradeDelta_Geki", traitUpgradeDeltaGeki.ToString());
    PlayerPrefs.SetString("PF_TraitUpgradeDelta_Shun", traitUpgradeDeltaShun.ToString());
    PlayerPrefs.SetString("PF_TraitUpgradeDelta_Iyu",  traitUpgradeDeltaIyu.ToString());
    PlayerPrefs.Save();

    // ボタン配線（仕様変更：解放購入は廃止、レベルアップのみ）
    if (traitUnlockButton)
    {
        traitUnlockButton.gameObject.SetActive(false);
    }
    if (traitUnlockOfferTMP)
    {
        traitUnlockOfferTMP.text = "";
    }

    if (traitUpgradeButton) traitUpgradeButton.onClick.AddListener(OnClickTraitUpgradeBuy);

    // UpgradeSectionMode が TRAIT の場合、最初から TraitOnly を反映
    var modeStr = PlayerPrefs.GetString("UpgradeSectionMode", "");
    if (modeStr == "TRAIT")
    {
        ApplySectionMode(UpgradeSectionMode.TraitOnly);
    }

    ApplyTraitOnlyLocalization_Local();

    // Traitオファーを作成（TraitOnly でも All でも呼べる）
    RefreshTraitOffers();

    // ★追加：宝石の取得抽選は UpgradeScene に入った直後に行う（当選時のみ結果パネル）
    TryProcessPendingGemReward_OnEnterUpgrade();
    BuildSelectedTileShopUI();
}
private void OnEnable()
{
    LocalizationManager.LanguageChanged += OnLanguageChanged_Local;
}

private void OnDisable()
{
    LocalizationManager.LanguageChanged -= OnLanguageChanged_Local;
    CloseSelectedTileShop();
}

private void OnLanguageChanged_Local(LocalizationManager.Language language)
{
    ApplyTraitOnlyLocalization_Local();
    RefreshTraitOffers();
    RefreshUI();
    RefreshUpgradeLabels();
    RefreshSelectedTileShopLabels();
}
public void OnClickBuy()
{
    int costNow = GetScaledCost(buyCost, buyCostIncrease, PrefKey_CostCount_Buy);
if (!TrySpendGold(costNow)) return; // 残高チェック＋減算 + 共通SE

    // 購入が成立したので回数を加算（次回から値上げ）
    IncrementPurchaseCount(PrefKey_CostCount_Buy);

    // グループ内の各牌を1枚ずつ購入
    if (offerBuyGroup != null)
        foreach (var idx in offerBuyGroup) PlayerData.AddToDeck(idx, +1);

    // 次の購入オファーをランダム生成
    offerBuyGroup = MakeRandomOfferGroup();
    RefreshUI();
}
public void OnClickRerollBuy()
{
    int costNow = GetScaledCost(rerollBuyCost, rerollBuyCostIncrease, PrefKey_CostCount_RerollBuy);
if (!TrySpendGold(costNow)) return;

    IncrementPurchaseCount(PrefKey_CostCount_RerollBuy);

    offerBuyGroup = MakeRandomOfferGroup();
    RefreshUI();
}
public void OnClickDestroy()
{
    // デッキ下限を割らないよう、実行前に合計枚数チェック（3or4枚まとめて減る）
    int after = PlayerData.TotalDeckCount() - (offerDestroyGroup?.Count ?? 0);
    if (after < Mathf.Max(1, minDeckSize)) return;

    // 全ての牌が1枚以上あるグループのみ実行
    if (!CanRemoveGroup(offerDestroyGroup)) 
    {
        // 取り除けるグループに差し替えて再表示（通貨は消費しない）
        offerDestroyGroup = MakeRandomDestroyableGroup();
        RefreshUI();
        return;
    }

    int costNow = GetScaledCost(destroyCost, destroyCostIncrease, PrefKey_CostCount_Destroy);
if (!TrySpendGold(costNow)) return; // 残高チェック＋減算 + 共通SE

    IncrementPurchaseCount(PrefKey_CostCount_Destroy);

    foreach (var idx in offerDestroyGroup) PlayerData.AddToDeck(idx, -1);

    // 次の破壊オファー（必ず破壊可能なもの）
    offerDestroyGroup = MakeRandomDestroyableGroup();
    RefreshUI();
}
public void OnClickRerollDestroy()
{
    int costNow = GetScaledCost(rerollDestroyCost, rerollDestroyCostIncrease, PrefKey_CostCount_RerollDestroy);
if (!TrySpendGold(costNow)) return;

    IncrementPurchaseCount(PrefKey_CostCount_RerollDestroy);

    offerDestroyGroup = MakeRandomDestroyableGroup();
    RefreshUI();
}
public void OnClickToggleDeckPanel()
{
    EnsureDeckPanelBuilt();

    var panel = GetActiveDeckPanelGO();
    if (!panel) return;

    bool next = !panel.activeSelf;
    panel.SetActive(next);
    if (next)
    {
        RefreshDeckCountLabels();
        RefreshDeckIconRows();
        ReflowDeckLayout(); // AUTO時だけ効く（MANUAL時は中でreturn）
    }
}

private GameObject GetActiveDeckPanelGO()
{
    if (deckPanelUseManualUI && manualDeckPanel) return manualDeckPanel;
    return deckPanel;
}

    /// <summary>強化完了（従来どおりRunSceneへは遷移しない）。</summary>
    public void OnFinishUpgrade()
    {
        var next = string.IsNullOrEmpty(dialogueSceneName) ? "EnemyDialogue" : dialogueSceneName;
        if (SceneManager.GetActiveScene().name != next)
            SceneManager.LoadScene(next);
    }

private void EnsureOffers()
{
    if (offerBuyGroup == null || offerBuyGroup.Count == 0)
        offerBuyGroup = MakeRandomOfferGroup();

    if (offerDestroyGroup == null || offerDestroyGroup.Count == 0)
        offerDestroyGroup = MakeRandomDestroyableGroup();
}

    private int PickRandomFromDeck()
    {
        var counts = PlayerData.GetDeckCountsCopy();
        int total = 0; for (int i = 0; i < 34; i++) total += Mathf.Max(0, counts[i]);
        if (total <= 0) return -1;
        int r = rng.Next(total);
        for (int i = 0; i < 34; i++)
        {
            r -= Mathf.Max(0, counts[i]);
            if (r < 0) return i;
        }
        return -1;
    }

private void RefreshUI()
{
    EnsureOffers();
    EnsureWalletLoaded();

    int buyCostNow = GetScaledCost(buyCost, buyCostIncrease, PrefKey_CostCount_Buy);
    int rerollBuyCostNow = GetScaledCost(rerollBuyCost, rerollBuyCostIncrease, PrefKey_CostCount_RerollBuy);

    if (buyCostTMP)       buyCostTMP.text = buyCostNow.ToString();
    if (rerollBuyCostTMP) rerollBuyCostTMP.text = rerollBuyCostNow.ToString();

    bool canDestroy = PlayerData.TotalDeckCount() > Mathf.Max(1, minDeckSize);

    int destroyCostNow = GetScaledCost(destroyCost, destroyCostIncrease, PrefKey_CostCount_Destroy);
    int rerollDestroyCostNow = GetScaledCost(rerollDestroyCost, rerollDestroyCostIncrease, PrefKey_CostCount_RerollDestroy);
    if (destroyCostTMP)       destroyCostTMP.text = canDestroy ? destroyCostNow.ToString() : GetUpgradeFixedText_Local("deck_empty_cost");
    if (rerollDestroyCostTMP) rerollDestroyCostTMP.text = canDestroy ? rerollDestroyCostNow.ToString() : GetUpgradeFixedText_Local("deck_empty_cost");
    // 候補牌（テキストは任意／画像は別途設定済みを想定）
// 画像も Resources から自動ロード（Inspectorの配列不要）
if (buyTileImage)
{
    var sp = LoadTileSpriteByIndex(Mathf.Clamp(offerBuyIndex, 0, 33));
    buyTileImage.sprite = sp;
    buyTileImage.enabled = (sp != null);
    buyTileImage.preserveAspect = true;
}
if (destroyTileImage)
{
    var c2 = PlayerData.GetDeckCountsCopy();
    if (offerDestroyIdx < 0 || c2[offerDestroyIdx] <= 0) offerDestroyIdx = PickRandomFromDeck();
    var sp2 = (offerDestroyIdx >= 0) ? LoadTileSpriteByIndex(offerDestroyIdx) : null;
    destroyTileImage.sprite = sp2;
    destroyTileImage.enabled = (sp2 != null);
    destroyTileImage.preserveAspect = true;
}


if (deckPanel && deckPanel.activeSelf)
{
    EnsureDeckPanelBuilt();
    RefreshDeckCountLabels();
    RefreshDeckIconRows();
    ReflowDeckLayout(); // ★ 表示中は都度調整
}
    RebuildOfferGroupUI(buyGroupRoot,     buyTileImage,     offerBuyGroup);
    RebuildOfferGroupUI(destroyGroupRoot, destroyTileImage, offerDestroyGroup);
    RefreshGoldText();  // ← goldTMP直書きよりこちらを呼ぶ
}


private void RefreshDeckCountLabels()
{
    var c = PlayerData.GetDeckCountsCopy();
    // 生成済みの配列に流し込む（未生成のときは何もしない）
    if (_manCount != null)   for (int i = 0; i < Mathf.Min(9, _manCount.Length);   i++) if (_manCount[i])   _manCount[i].text   = c[i].ToString();
    if (_pinCount != null)   for (int i = 0; i < Mathf.Min(9, _pinCount.Length);   i++) if (_pinCount[i])   _pinCount[i].text   = c[9  + i].ToString();
    if (_souCount != null)   for (int i = 0; i < Mathf.Min(9, _souCount.Length);   i++) if (_souCount[i])   _souCount[i].text   = c[18 + i].ToString();
    if (_honorCount != null) for (int i = 0; i < Mathf.Min(7, _honorCount.Length); i++) if (_honorCount[i]) _honorCount[i].text = c[27 + i].ToString();
}
private void EnsureDeckPanelBuilt()
{
    if (deckPanelUseManualUI)
    {
        // MANUAL: Inspector で割り当てられたUIを使う（自動生成しない）
        if (_deckBuilt && manualDeckPanel && manualDeckPanel.activeInHierarchy) return;

        if (!manualDeckPanel) return;

        // OK/Close ボタン
        if (manualDeckCloseButton)
        {
            manualDeckCloseButton.onClick.RemoveAllListeners();
            manualDeckCloseButton.onClick.AddListener(() =>
            {
                if (manualDeckPanel) manualDeckPanel.SetActive(false);
            });
        }

        // 34枠を _man/_pin/_sou/_honor にマップ（Refresh系の既存処理を流用するため）
        if (manualDeckTileIcons != null && manualDeckTileIcons.Length >= 34 &&
            manualDeckTileCounts != null && manualDeckTileCounts.Length >= 34)
        {
            _manIcons = new Image[9];
            _manCount = new TMP_Text[9];
            _pinIcons = new Image[9];
            _pinCount = new TMP_Text[9];
            _souIcons = new Image[9];
            _souCount = new TMP_Text[9];
            _honorIcons = new Image[7];
            _honorCount = new TMP_Text[7];

            for (int i = 0; i < 9; i++)
            {
                _manIcons[i] = manualDeckTileIcons[i];
                _manCount[i] = manualDeckTileCounts[i];

                _pinIcons[i] = manualDeckTileIcons[9 + i];
                _pinCount[i] = manualDeckTileCounts[9 + i];

                _souIcons[i] = manualDeckTileIcons[18 + i];
                _souCount[i] = manualDeckTileCounts[18 + i];
            }
            for (int i = 0; i < 7; i++)
            {
                _honorIcons[i] = manualDeckTileIcons[27 + i];
                _honorCount[i] = manualDeckTileCounts[27 + i];
            }
        }

        _deckBuilt = true;
        return;
    }

    // ===== AUTO: 既存の自動生成（元の処理を維持）=====
    if (_deckBuilt && deckPanel && deckPanel.activeInHierarchy) return;

    // 1) パネル（無ければCanvas直下に作る）—フルスクリーン＆不透明
    if (!deckPanel)
    {
        var cv = GameObject.FindObjectOfType<Canvas>();
        if (!cv) return;

        deckPanel = new GameObject("DeckPanel", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)deckPanel.transform;
        rt.SetParent(cv.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = deckPanel.GetComponent<Image>();
        bg.sprite = panelBackgroundSprite;
        bg.type   = panelBackgroundType;
        bg.color  = panelBackgroundSprite ? Color.white : panelBackgroundColor; // 画像あり→白、無し→指定色
        bg.raycastTarget = true;

        deckPanel.SetActive(false);
    }
    else
    {
        var rt = deckPanel.GetComponent<RectTransform>() ?? deckPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = deckPanel.GetComponent<Image>() ?? deckPanel.AddComponent<Image>();
        bg.sprite = panelBackgroundSprite;
        bg.type   = panelBackgroundType;
        bg.color  = panelBackgroundSprite ? Color.white : panelBackgroundColor; // 画像あり→そのまま見せる
        bg.raycastTarget = true;
    }

    // 2) コンテンツ領域(上部)とOKボタン(最下部)
    RectTransform contentRT;
    {
        var existed = deckPanel.transform.Find("Content") as RectTransform;
        contentRT = existed ? existed : new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        contentRT.SetParent(deckPanel.transform, false);
        if (tileAreaUseFixedSize && tileAreaFixedSize.x > 0f && tileAreaFixedSize.y > 0f)
        {
            // 固定サイズで中央配置
            contentRT.anchorMin = contentRT.anchorMax = new Vector2(0.5f, 0.5f);
            contentRT.pivot     = new Vector2(0.5f, 0.5f);
            contentRT.sizeDelta = tileAreaFixedSize;
            contentRT.anchoredPosition = Vector2.zero;
        }
        else
        {
            // 画面いっぱいからパディングで内側へ（L/R/T/B）
            var p = tileAreaPaddingLRBT; // x=L, y=R, z=T, w=B
            contentRT.anchorMin = new Vector2(0f, 0f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.offsetMin = new Vector2(p.x, p.w);
            contentRT.offsetMax = new Vector2(-p.y, -p.z);
        }
    }
    deckPanelRoot = contentRT;

    // 子をクリアして縦並びのコンテナを準備
    ClearChildren(deckPanelRoot);
    var vlg = deckPanelRoot.GetComponent<VerticalLayoutGroup>() ?? deckPanelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
    vlg.spacing = rowSpacing;
    vlg.childAlignment = TextAnchor.UpperLeft;
    vlg.childControlWidth = false; vlg.childControlHeight = false;
    vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
    string L(int i, string def) =>
        (suitRowLabels != null && suitRowLabels.Length > i && !string.IsNullOrEmpty(suitRowLabels[i]))
            ? suitRowLabels[i] : def;

    BuildRow(0, L(0, GetUpgradeFixedText_Local("suit_man")),   0, 9);
    BuildRow(1, L(1, GetUpgradeFixedText_Local("suit_pin")),   9, 9);
    BuildRow(2, L(2, GetUpgradeFixedText_Local("suit_sou")),  18, 9);
    BuildRow(3, L(3, GetUpgradeFixedText_Local("suit_honor")), 27, 7);

    // 3) 最下部OKボタン
    if (!_deckOkButton)
    {
        var btnGO = new GameObject("Button_OK", typeof(RectTransform), typeof(Image), typeof(Button));
        var brt = (RectTransform)btnGO.transform;
        brt.SetParent(deckPanel.transform, false);
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot     = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 10f);

        // 見た目（Inspector反映）
        brt.sizeDelta = okButtonSize;
        var img = btnGO.GetComponent<Image>();
        img.sprite = okButtonSprite;
        img.type   = Image.Type.Sliced;
        img.color  = okButtonSprite ? Color.white : Color.white;

        // ラベル生成
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = (RectTransform)labelGO.transform;
        lrt.SetParent(btnGO.transform, false);
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(Mathf.Max(200f, okButtonSize.x - 20f), Mathf.Max(40f, okButtonSize.y - 20f));

        var lbl = labelGO.GetComponent<TextMeshProUGUI>();
        if (TMPro.TMP_Settings.defaultFontAsset) lbl.font = TMPro.TMP_Settings.defaultFontAsset;
        lbl.fontSize  = Mathf.Max(24, deckFontSize);
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.text      = string.IsNullOrEmpty(okButtonText) ? GetUpgradeFixedText_Local("ok") : okButtonText;

        _deckOkButton = btnGO.GetComponent<Button>();
        _deckOkButton.onClick.RemoveAllListeners();
        _deckOkButton.onClick.AddListener(() => { if (deckPanel) deckPanel.SetActive(false); });
    }
    else
    {
        // 既存ボタンにもInspectorの値を反映（サイズ/画像/テキスト）
        var brt = _deckOkButton.GetComponent<RectTransform>();
        brt.sizeDelta = okButtonSize;

        var img = _deckOkButton.GetComponent<Image>();
        img.sprite = okButtonSprite;
        img.type   = Image.Type.Sliced;
        img.color  = okButtonSprite ? Color.white : Color.white;

        var lbl = _deckOkButton.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl)
        {
            if (TMPro.TMP_Settings.defaultFontAsset) lbl.font = TMPro.TMP_Settings.defaultFontAsset;
            lbl.fontSize  = Mathf.Max(24, deckFontSize);
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.text      = string.IsNullOrEmpty(okButtonText) ? GetUpgradeFixedText_Local("ok") : okButtonText;
        }
    }

    _deckBuilt = true;
    ReflowDeckLayout(); // ← 呼び出しはそのまま
}
private void ReflowDeckLayout()
{
    if (deckPanelUseManualUI) return;
    if (!deckPanelRoot) return;

    LayoutRebuilder.ForceRebuildLayoutImmediate(deckPanelRoot);
    var area = deckPanelRoot.rect;
    if (area.width <= 0 || area.height <= 0) return;

    // 横制約：最大列数9、左にラベル幅を引く
    float colsMax = 9f;
    float horizontalSpace = area.width - rowLabelWidth - (colsMax - 1) * cellSpacing;
    float cellW_ByWidth = Mathf.Floor(horizontalSpace / colsMax);

    // 縦制約：4行。数字の高さを加味
    float rows = 4f;
    float countTextH = Mathf.Max(18f, deckFontSize - 6f);
    float verticalSpace = area.height - (rows - 1) * rowSpacing;

    float iconH_ByWidth = cellW_ByWidth / Mathf.Max(0.1f, tileAspectWH);
    float cellH_ByWidth = iconH_ByWidth + countTextH + 6f;
    float cellH_ByHeight = Mathf.Floor(verticalSpace / rows);

    if (cellH_ByWidth > cellH_ByHeight)
    {
        float iconH = cellH_ByHeight - (countTextH + 6f);
        float cellW = Mathf.Floor(iconH * tileAspectWH);
        ApplyCellSizeToAllRows(new Vector2(cellW, cellH_ByHeight), iconH, Mathf.RoundToInt(Mathf.Clamp(iconH * 0.22f, 16f, 28f)));
    }
    else
    {
        float iconH = iconH_ByWidth;
        ApplyCellSizeToAllRows(new Vector2(cellW_ByWidth, cellH_ByWidth), iconH, Mathf.RoundToInt(Mathf.Clamp(iconH * 0.22f, 16f, 28f)));
    }

    LayoutRebuilder.ForceRebuildLayoutImmediate(deckPanelRoot);
}

private void ApplyCellSizeToAllRows(Vector2 cellSizeXY, float iconHeight, int countFont)
{
    // Grid設定
    for (int r = 0; r < _rowGrids.Length; r++)
    {
        var g = _rowGrids[r]; if (!g) continue;
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = _rowCols[r];    // 字牌は7列
        g.cellSize = cellSizeXY;
        g.spacing = new Vector2(cellSpacing, 6f);
    }

    // 画像のRect（preserveAspect は維持）
    void SetIcons(Image[] arr)
    {
        if (arr == null) return;
        float iconW = iconHeight * tileAspectWH;
        for (int i = 0; i < arr.Length; i++)
        {
            var img = arr[i]; if (!img) continue;
            var rt = (RectTransform)img.transform;
            rt.sizeDelta = new Vector2(iconW, iconHeight);
        }
    }
    SetIcons(_manIcons); SetIcons(_pinIcons); SetIcons(_souIcons); SetIcons(_honorIcons);

    // 数字フォント
    void SetCounts(TMP_Text[] arr)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            var t = arr[i]; if (!t) continue;
            t.fontSize = countFont;
        }
    }
    SetCounts(_manCount); SetCounts(_pinCount); SetCounts(_souCount); SetCounts(_honorCount);

    // 行ラベルも追従
    for (int i = 0; i < _rowLabelTexts.Length; i++)
        if (_rowLabelTexts[i]) _rowLabelTexts[i].fontSize = Mathf.Clamp(countFont + 6, 20, 36);
}

private void BuildRow(int rowIndex, string label, int startIdx, int length)
{
    // 行コンテナ（横並び：左ラベル＋右Grid）
    var rowGO = new GameObject($"Row_{label}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
    var rowRT = (RectTransform)rowGO.transform;
    rowRT.SetParent(deckPanelRoot, false);

    var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
    hlg.spacing = cellSpacing;
    hlg.childAlignment = TextAnchor.UpperLeft;
    hlg.childControlWidth = false; hlg.childControlHeight = false;
    hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

    // 左ラベル
    var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
    var labelRT = (RectTransform)labelGO.transform; labelRT.SetParent(rowRT, false);
    var tmp = labelGO.GetComponent<TextMeshProUGUI>();
    if (TMPro.TMP_Settings.defaultFontAsset) tmp.font = TMPro.TMP_Settings.defaultFontAsset;
    tmp.fontSize = deckFontSize;
    tmp.text = label;
    tmp.alignment = TextAlignmentOptions.MidlineRight;
    var le = labelGO.GetComponent<LayoutElement>();
    le.minWidth = le.preferredWidth = rowLabelWidth;
    _rowLabelTexts[rowIndex] = tmp;

    // 右Grid（列固定）
    var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
    var gridRT = (RectTransform)gridGO.transform; gridRT.SetParent(rowRT, false);
    var grid = gridGO.GetComponent<GridLayoutGroup>();
    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    grid.constraintCount = length;
    grid.spacing = new Vector2(cellSpacing, 6);
    grid.childAlignment = TextAnchor.UpperLeft;
    grid.startAxis = GridLayoutGroup.Axis.Horizontal;
    _rowGrids[rowIndex] = grid;

    // セルを並べる（画像＋枚数）
    if (rowIndex == 0) { _manIcons = new Image[9]; _manCount = new TMP_Text[9]; }
    if (rowIndex == 1) { _pinIcons = new Image[9]; _pinCount = new TMP_Text[9]; }
    if (rowIndex == 2) { _souIcons = new Image[9]; _souCount = new TMP_Text[9]; }
    if (rowIndex == 3) { _honorIcons = new Image[7]; _honorCount = new TMP_Text[7]; }

    for (int i = 0; i < length; i++)
    {
        int tileIndex = startIdx + i;

        var cell = new GameObject($"Cell_{tileIndex}", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var cellRT = (RectTransform)cell.transform; cellRT.SetParent(gridRT, false);
        var v = cell.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = false; v.childControlHeight = false;
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;

        // 画像
        var imgGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var imgRT = (RectTransform)imgGO.transform; imgRT.SetParent(cellRT, false);
        var img = imgGO.GetComponent<Image>(); img.preserveAspect = true;

        // 枚数
        var txtGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        var txtRT = (RectTransform)txtGO.transform; txtRT.SetParent(cellRT, false);
        var t = txtGO.GetComponent<TextMeshProUGUI>();
        if (TMPro.TMP_Settings.defaultFontAsset) t.font = TMPro.TMP_Settings.defaultFontAsset;
        t.alignment = TextAlignmentOptions.Center;

        // 配列へ
        if (rowIndex == 0) { _manIcons[i] = img; _manCount[i] = t; }
        if (rowIndex == 1) { _pinIcons[i] = img; _pinCount[i] = t; }
        if (rowIndex == 2) { _souIcons[i] = img; _souCount[i] = t; }
        if (rowIndex == 3) { _honorIcons[i] = img; _honorCount[i] = t; }
    }
}

// 画像の更新
private void RefreshDeckIconRows()
{
    ApplyIcons(_manIcons,   0);
    ApplyIcons(_pinIcons,   9);
    ApplyIcons(_souIcons,  18);
    ApplyIcons(_honorIcons,27);
}

private void ApplyIcons(Image[] row, int startIndex)
{
    if (row == null) return;
    for (int i = 0; i < row.Length; i++)
    {
        var img = row[i];
        int idx = startIndex + i;
        if (!img) continue;

        var sp = LoadTileSpriteByIndex(idx);
        img.enabled = (sp != null);
        img.sprite  = sp;
        img.preserveAspect = true;
    }
}


// 既存のユーティリティに合わせた DestroyAll
private void ClearChildren(Transform t)
{
    if (!t) return;
    for (int i = t.childCount - 1; i >= 0; i--)
        Destroy(t.GetChild(i).gameObject);
}
// --- Tile sprite loader (対局中と同じ方式でロード) ---
private static Sprite LoadTileSpriteByIndex(int index)
{
    // PlayerData のユーティリティがあれば優先し、無ければ GameManager の公開APIを使用
    string id = null;
    try { id = PlayerData.TileIdForIndex(index); } catch {}
    if (string.IsNullOrEmpty(id))
    {
        try { id = GameManager.IndexToId(index); } catch {}
    }
    return string.IsNullOrEmpty(id) ? null : Resources.Load<Sprite>($"Sprites/Tiles/{id}");
}
private static Sprite LoadTileSpriteById(string tileId)
{
    return string.IsNullOrEmpty(tileId) ? null : Resources.Load<Sprite>($"Sprites/Tiles/{tileId}");
}
private bool _walletLoaded = false;
private void EnsureWalletLoaded()
{
    if (_walletLoaded) return;
    // RunCurrency は PlayerPrefs を直接参照するため事前ロード不要。
    // 存在チェック兼ねて一度 Get() だけ呼んでおく。
    _ = GameManager.RunCurrency.Get();
    _walletLoaded = true;
}

    // NEW: 共通通貨の数値をラベルへ反映
private void RefreshGoldText()
{
    if (goldTMP) goldTMP.text = $"{GameManager.RunCurrency.Get():N0}";
}

private void WireButtonsIfAssigned()
{
    if (buyButton)
    {
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnClickBuy);
    }
    if (destroyButton)
    {
        destroyButton.onClick.RemoveAllListeners();
        destroyButton.onClick.AddListener(OnClickDestroy);
    }
    if (rerollBuyButton)
    {
        rerollBuyButton.onClick.RemoveAllListeners();
        rerollBuyButton.onClick.AddListener(OnClickRerollBuy);
    }
    if (rerollDestroyButton)
    {
        rerollDestroyButton.onClick.RemoveAllListeners();
        rerollDestroyButton.onClick.AddListener(OnClickRerollDestroy);
    }

    // ★追加：Run Upgrades
    if (buyHpButton)
    {
        buyHpButton.onClick.RemoveAllListeners();
        buyHpButton.onClick.AddListener(OnClickBuyHpUp);
    }
    if (buyMpButton)
    {
        buyMpButton.onClick.RemoveAllListeners();
        buyMpButton.onClick.AddListener(OnClickBuyMpUp);
    }
    if (buyCastButton)
    {
        buyCastButton.onClick.RemoveAllListeners();
        buyCastButton.onClick.AddListener(OnClickBuyCastUp);
    }

    // ★追加：Run Heals（StatusRoot）
    if (buyHealHpButton)
    {
        buyHealHpButton.onClick.RemoveAllListeners();
        buyHealHpButton.onClick.AddListener(OnClickBuyHealHp);
    }
    if (buyHealMpButton)
    {
        buyHealMpButton.onClick.RemoveAllListeners();
        buyHealMpButton.onClick.AddListener(OnClickBuyHealMp);
    }
}
private void RefreshUpgradeLabels()
{
    // 現在の累積ボーナスを読み、価格と併記
    int hp = 0, mp = 0, sc = 0;
    try { hp = Mathf.Max(0, PlayerPrefs.GetInt("Run_HPBonus", 0)); } catch {}
    try { mp = Mathf.Max(0, PlayerPrefs.GetInt("Run_MPBonus", 0)); } catch {}
    try { sc = Mathf.Max(0, PlayerPrefs.GetInt("Run_SkillCastsBonus", 0)); } catch {}

    int hpCostNow   = GetScaledCost(hpUpCost,   hpUpCostIncrease,   PrefKey_CostCount_HpUp);
    int mpCostNow   = GetScaledCost(mpUpCost,   mpUpCostIncrease,   PrefKey_CostCount_MpUp);
    int castCostNow = GetScaledCost(castUpCost, castUpCostIncrease, PrefKey_CostCount_CastUp);

    if (hpUpLabel)   hpUpLabel.text   = BuildUpgradeWithTotalLabel_Local("hp_up_prefix", hpUpValue, hpCostNow, hp);
    if (mpUpLabel)   mpUpLabel.text   = BuildUpgradeWithTotalLabel_Local("mp_up_prefix", mpUpValue, mpCostNow, mp);
    if (castUpLabel) castUpLabel.text = BuildUpgradeWithTotalLabel_Local("cast_up_prefix", castUpValue, castCostNow, sc);

    int healHpCostNow = GetScaledCost(healHpCost, healHpCostIncrease, PrefKey_CostCount_HealHp);
    int healMpCostNow = GetScaledCost(healMpCost, healMpCostIncrease, PrefKey_CostCount_HealMp);

    if (healHpLabel) healHpLabel.text = BuildHealLabel_Local("heal_hp_prefix", healHpValue, healHpCostNow);
    if (healMpLabel) healMpLabel.text = BuildHealLabel_Local("heal_mp_prefix", healMpValue, healMpCostNow);

    // ★追加：現在HP/MP（現在値/最大値）を更新
    RefreshCurrentHpMpText();
}
private void OnClickBuyHealHp()
{
    EnsureWalletLoaded();

    int add = Mathf.Max(0, healHpValue);
    if (add <= 0) return;

    // 先に「回復できる状態か」を判定（満タンなら買えない）
    if (!CanHealHpNow(add)) return;

    int costNow = GetScaledCost(healHpCost, healHpCostIncrease, PrefKey_CostCount_HealHp);
if (!TrySpendGold(Mathf.Max(0, costNow))) return;

    IncrementPurchaseCount(PrefKey_CostCount_HealHp);

    ApplyHealHpNowIfPossible(add);

    RefreshGoldText();
    RefreshUpgradeLabels();
}
private void OnClickBuyHealMp()
{
    EnsureWalletLoaded();

    int add = Mathf.Max(0, healMpValue);
    if (add <= 0) return;

    // 先に「回復できる状態か」を判定（満タンなら買えない）
    if (!CanHealMpNow(add)) return;

    int costNow = GetScaledCost(healMpCost, healMpCostIncrease, PrefKey_CostCount_HealMp);
if (!TrySpendGold(Mathf.Max(0, costNow))) return;

    IncrementPurchaseCount(PrefKey_CostCount_HealMp);

    ApplyHealMpNowIfPossible(add);

    RefreshGoldText();
    RefreshUpgradeLabels();
}

private bool CanHealHpNow(int add)
{
    // 1) Run中の持ち越しHPがあればそれを基準に判定
    // 2) GameManager が居るなら playerHP/playerMaxHP から判定
    try
    {
        if (PlayerPrefs.HasKey("Run_PlayerHP"))
        {
            int cur = PlayerPrefs.GetInt("Run_PlayerHP", 0);
            // Maxが無い場合は「常に回復可能」とみなす（後でGM側でClampされる想定）
            if (PlayerPrefs.HasKey("Run_PlayerMaxHP"))
            {
                int max = PlayerPrefs.GetInt("Run_PlayerMaxHP", cur);
                return cur < max;
            }
            return true;
        }
    }
    catch { }

    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (!gm) return true;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    try
    {
        var tp = gm.GetType();
        var fMax = tp.GetField("playerMaxHP", BF);
        var fCur = tp.GetField("playerHP", BF);
        if (fMax != null && fCur != null && fMax.FieldType == typeof(int) && fCur.FieldType == typeof(int))
        {
            int max = (int)fMax.GetValue(gm);
            int cur = (int)fCur.GetValue(gm);
            return cur < max;
        }
    }
    catch { }

    return true;
}

private bool CanHealMpNow(int add)
{
    try
    {
        if (PlayerPrefs.HasKey("Run_PlayerMP"))
        {
            int cur = PlayerPrefs.GetInt("Run_PlayerMP", 0);
            if (PlayerPrefs.HasKey("Run_PlayerMaxMP"))
            {
                int max = PlayerPrefs.GetInt("Run_PlayerMaxMP", cur);
                return cur < max;
            }
            return true;
        }
    }
    catch { }

    // MP は GameManager_SkillMP_Addon 側の _mp を想定（Run_PlayerMP も存在）:contentReference[oaicite:7]{index=7}
    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (!gm) return true;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    try
    {
        var tp = gm.GetType();
        // EffectiveMaxMP() が呼べるならそれを優先
        var miMax = tp.GetMethod("EffectiveMaxMP", BF);
        var fCur = tp.GetField("_mp", BF);

        if (miMax != null && miMax.ReturnType == typeof(int) && fCur != null && fCur.FieldType == typeof(int))
        {
            int max = (int)miMax.Invoke(gm, null);
            int cur = (int)fCur.GetValue(gm);
            return cur < max;
        }
    }
    catch { }

    return true;
}

private void ApplyHealHpNowIfPossible(int add)
{
    if (add <= 0) return;

    // まず Run_PlayerHP に積む（UpgradeSceneでGMが居ないケースのため）
    try
    {
        int cur = 0;
        if (PlayerPrefs.HasKey("Run_PlayerHP")) cur = PlayerPrefs.GetInt("Run_PlayerHP", 0);
        int next = cur + add;

        if (PlayerPrefs.HasKey("Run_PlayerMaxHP"))
        {
            int max = PlayerPrefs.GetInt("Run_PlayerMaxHP", next);
            next = Mathf.Min(max, next);
        }

        PlayerPrefs.SetInt("Run_PlayerHP", next);
        PlayerPrefs.Save();
    }
    catch { }

    // GameManager が居るなら、その場でHP UI更新まで行う（既存のTryUpdateHpUiと同じ思想）:contentReference[oaicite:8]{index=8}
    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (!gm) return;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    try
    {
        var tp = gm.GetType();
        var fMax = tp.GetField("playerMaxHP", BF);
        var fCur = tp.GetField("playerHP", BF);
        if (fMax != null && fCur != null && fMax.FieldType == typeof(int) && fCur.FieldType == typeof(int))
        {
            int max = (int)fMax.GetValue(gm);
            int cur = (int)fCur.GetValue(gm);
            int next = Mathf.Min(max, cur + add);
            fCur.SetValue(gm, next);
        }
    }
    catch { }

    TryUpdateHpUi();
}

private void ApplyHealMpNowIfPossible(int add)
{
    if (add <= 0) return;

    // まず Run_PlayerMP に積む（UpgradeSceneでGMが居ないケースのため）
    try
    {
        int cur = 0;
        if (PlayerPrefs.HasKey("Run_PlayerMP")) cur = PlayerPrefs.GetInt("Run_PlayerMP", 0);
        int next = cur + add;

        if (PlayerPrefs.HasKey("Run_PlayerMaxMP"))
        {
            int max = PlayerPrefs.GetInt("Run_PlayerMaxMP", next);
            next = Mathf.Min(max, next);
        }

        PlayerPrefs.SetInt("Run_PlayerMP", next);
        PlayerPrefs.Save();
    }
    catch { }

    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (!gm) return;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    try
    {
        var tp = gm.GetType();
        var miMax = tp.GetMethod("EffectiveMaxMP", BF);
        var fCur = tp.GetField("_mp", BF);
        if (miMax != null && miMax.ReturnType == typeof(int) && fCur != null && fCur.FieldType == typeof(int))
        {
            int max = (int)miMax.Invoke(gm, null);
            int cur = (int)fCur.GetValue(gm);
            int next = Mathf.Min(max, cur + add);
            fCur.SetValue(gm, next);
        }
    }
    catch { }

    // MP UI は UpdateHpUI から UpdateMpUI_IfAssigned() が呼ばれる前提がコメントにあります:contentReference[oaicite:9]{index=9}
    TryUpdateHpUi();
}
private void OnClickBuyHpUp()
{
    EnsureWalletLoaded();

    int costNow = GetScaledCost(hpUpCost, hpUpCostIncrease, PrefKey_CostCount_HpUp);
if (!TrySpendGold(Mathf.Max(0, costNow))) return;

    IncrementPurchaseCount(PrefKey_CostCount_HpUp);

    int cur = 0; try { cur = PlayerPrefs.GetInt("Run_HPBonus", 0); } catch {}
    int add = Mathf.Max(0, hpUpValue);
    try { PlayerPrefs.SetInt("Run_HPBonus", cur + add); PlayerPrefs.Save(); } catch {}

    ApplyHpBonusNowIfPossible(add); // 対局中でも即UI反映
    RefreshGoldText();
    RefreshUpgradeLabels();
}
private void OnClickBuyMpUp()
{
    EnsureWalletLoaded();

    int costNow = GetScaledCost(mpUpCost, mpUpCostIncrease, PrefKey_CostCount_MpUp);
if (!TrySpendGold(Mathf.Max(0, costNow))) return;

    IncrementPurchaseCount(PrefKey_CostCount_MpUp);

    int cur = 0; try { cur = PlayerPrefs.GetInt("Run_MPBonus", 0); } catch {}
    int add = Mathf.Max(0, mpUpValue);
    try { PlayerPrefs.SetInt("Run_MPBonus", cur + add); PlayerPrefs.Save(); } catch {}

    // ★追加：対局中なら現在MPを最大まで即時回復し、UIも更新
    RefillMpIfPossible();

    // 既存のUI更新（HP側の更新経由でMPラベルも再描画）
    TryUpdateHpUi();
    RefreshGoldText();
    RefreshUpgradeLabels();
}
private void OnClickBuyCastUp()
{
    EnsureWalletLoaded();

    int costNow = GetScaledCost(castUpCost, castUpCostIncrease, PrefKey_CostCount_CastUp);
if (!TrySpendGold(Mathf.Max(0, costNow))) return;

    IncrementPurchaseCount(PrefKey_CostCount_CastUp);

    int cur = 0; try { cur = PlayerPrefs.GetInt("Run_SkillCastsBonus", 0); } catch {}
    int add = Mathf.Max(0, castUpValue);
    try { PlayerPrefs.SetInt("Run_SkillCastsBonus", cur + add); PlayerPrefs.Save(); } catch {}

    // 上限は次のターン以降の判定で即有効化される（GetMaxSkillCastsThisTurn）
    TryUpdateHpUi(); // MPラベル等も更新
    RefreshGoldText();
    RefreshUpgradeLabels();
}

private void ApplyHpBonusNowIfPossible(int add)
{
    if (add <= 0) return;
    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (!gm) return;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    try
    {
        var tp = gm.GetType();
        var fMax = tp.GetField("playerMaxHP", BF);
        var fCur = tp.GetField("playerHP", BF);
        if (fMax != null && fCur != null && fMax.FieldType == typeof(int) && fCur.FieldType == typeof(int))
        {
            int max = (int)fMax.GetValue(gm);
            int cur = (int)fCur.GetValue(gm);
            max += add;
            cur = Mathf.Min(max, cur + add);
            fMax.SetValue(gm, max);
            fCur.SetValue(gm, cur);
        }
    }
    catch {}

    TryUpdateHpUi();
}

private void TryUpdateHpUi()
{
    var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
    if (!gm) return;

    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    try
    {
        var mi = gm.GetType().GetMethod("UpdateHpUI", BF);
        mi?.Invoke(gm, null);    // UpdateHpUI -> UpdateMpUI_IfAssigned() も呼ばれる実装です
    }
    catch {}
}

private void HideObsoleteGoldIfAssigned()
{
    if (obsoleteGoldTMP)
        obsoleteGoldTMP.transform.root.gameObject.SetActive(false);
}
// === NEW: 3枚 or 4枚のいずれかをランダム生成 ===
// 数牌: 同一スーツ 1-2-3 / 4-5-6 / 7-8-9
// 字牌: 東南西北(4枚) or 白發中(3枚)
private List<int> MakeRandomOfferGroup()
{
    // 0..2=数牌(1-3/4-6/7-9), 3=三元牌, 4=風牌
    int pick = rng.Next(5);
    if (pick <= 2)
    {
        int suit  = rng.Next(3);                // 0=萬,1=筒,2=索
        int start = new int[]{1,4,7}[pick];     // 1 or 4 or 7
        int baseIdx = suit * 9;                 // 0,9,18
        return new List<int> { baseIdx+(start-1), baseIdx+start, baseIdx+(start+1) };
    }
    else if (pick == 3)
    {
        // 白(31) 發(32) 中(33)  ※ 27..33: 東南西北白發中
        return new List<int> { 27+4, 27+5, 27+6 }; // 31,32,33
    }
    else
    {
        // 東(27) 南(28) 西(29) 北(30) の4枚
        return new List<int> { 27, 28, 29, 30 };
    }
}

// デッキから「必ず取り除ける」セットのみ返す（最大50回試行）
private List<int> MakeRandomDestroyableGroup()
{
    for (int i=0; i<50; i++)
    {
        var g = MakeRandomOfferGroup();
        int after = PlayerData.TotalDeckCount() - g.Count;
        if (after >= Mathf.Max(1, minDeckSize) && CanRemoveGroup(g)) return g;
    }
    // どうしても見つからない場合は空（ボタンが押せない見た目になる）
    return new List<int>();
}

private bool CanRemoveGroup(List<int> g)
{
    if (g == null || g.Count == 0) return false;
    var c = PlayerData.GetDeckCountsCopy();
    foreach (var idx in g)
    {
        if (idx < 0 || idx >= 34) return false;
        if (c[idx] <= 0) return false;
    }
    return true;
}
private void RebuildOfferGroupUI(RectTransform root, Image fallback, List<int> group)
{
    // root を使う場合（推奨）
    if (root)
    {
        ClearChildren(root);
        var h = EnsureHLG(root, offerTileSpacing); // ★Inspectorの間隔を適用

foreach (var idx in (group ?? System.Linq.Enumerable.Empty<int>()))
{
    var sp = LoadTileSpriteByIndex(idx);
    var go = new GameObject($"Offer_{idx}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
    var rt = (RectTransform)go.transform;
    rt.SetParent(root, false);

    var img = go.GetComponent<Image>();
    img.sprite = sp;

    // 幅・高さの指定に応じてアスペクトを制御
    bool bothFixed = (offerTileWidth > 0f) && (offerTileHeight > 0f);
    img.preserveAspect = offerTileKeepAspect && !bothFixed;

    var le = go.GetComponent<LayoutElement>();

    // 高さだけ指定 & アスペクト維持 → 幅を高さ×牌アスペクトで自動算出
    float autoW = (offerTileHeight > 0f && offerTileKeepAspect && !bothFixed)
                    ? offerTileHeight * Mathf.Max(0.1f, tileAspectWH) : -1f;

    // preferred に反映（-1 は未指定扱い）
    le.preferredWidth  = (offerTileWidth  > 0f) ? offerTileWidth  : autoW;
    le.preferredHeight = (offerTileHeight > 0f) ? offerTileHeight : -1f;

    // ★ RectTransform の sizeDelta は触らない（Layout に任せる）
}


        if (fallback) { fallback.enabled = false; }
        return;
    }

    // 後方互換：単一Imageに先頭のみ表示（グループRoot未割当のとき）
    if (fallback)
    {
        var sp = (group != null && group.Count > 0) ? LoadTileSpriteByIndex(group[0]) : null;
        fallback.enabled = (sp != null);
        fallback.sprite  = sp;

        bool bothFixed = (offerTileWidth  > 0f) && (offerTileHeight > 0f);
        fallback.preserveAspect = offerTileKeepAspect && !bothFixed;

        var frt = fallback.rectTransform;
        if (offerTileWidth  > 0f || offerTileHeight > 0f)
        {
            var w = (offerTileWidth  > 0f) ? offerTileWidth  : frt.sizeDelta.x;
            var h = (offerTileHeight > 0f) ? offerTileHeight : frt.sizeDelta.y;
            frt.sizeDelta = new Vector2(w, h);
        }
    }
}

private HorizontalLayoutGroup EnsureHLG(RectTransform root, float spacing)
{
    var h = root.GetComponent<HorizontalLayoutGroup>();
    if (!h) h = root.gameObject.AddComponent<HorizontalLayoutGroup>();

    h.spacing = spacing;
    h.childAlignment = TextAnchor.MiddleLeft;
    h.childControlWidth = true;
    h.childControlHeight = false;
    h.childForceExpandWidth = false;
    h.childForceExpandHeight = false;
    h.padding = new RectOffset(0, 0, 0, 0);

    return h;
}
public void ApplySectionMode(UpgradeSectionMode mode)
{
    // ★「選んだセクションだけ」表示する（重なり防止）
    bool showStatus = false;
    bool showDeck   = false;
    bool showTrait  = false;

    switch (mode)
    {
        case UpgradeSectionMode.StatusOnly:
            showStatus = true;
            break;

        case UpgradeSectionMode.DeckOnly:
            showDeck = true;
            break;

        case UpgradeSectionMode.TraitOnly:
            showTrait = true;
            break;

        case UpgradeSectionMode.All:
        default:
            // もし「All＝全部表示」じゃなく「メニューから選ぶ運用」にしたいなら false のままでOK
            // ただ後方互換のため、従来Allはステータス＋デッキを表示に寄せる
            showStatus = true;
            showDeck   = true;
            break;
    }

    if (statusShopRoot) statusShopRoot.SetActive(showStatus);
    if (deckShopRoot)   deckShopRoot.SetActive(showDeck);
    if (traitYakuShopRoot) traitYakuShopRoot.SetActive(showTrait);

    // デッキ関連のオーバーレイは、デッキを表示しない時は閉じる（重なり防止）
    if (!showDeck && deckPanel && deckPanel.activeSelf)
        deckPanel.SetActive(false);

    // ラベル等を最新化
    RefreshUpgradeLabels();
    RefreshUI();
}


private void RefillMpIfPossible()
{
    try
    {
        var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
        if (!gm) return;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var tp    = gm.GetType();
        var miEff = tp.GetMethod("EffectiveMaxMP", BF);
        var fMp   = tp.GetField("_mp", BF);
        var miUpd = tp.GetMethod("UpdateMpUI", BF);

        if (miEff != null && fMp != null && fMp.FieldType == typeof(int))
        {
            int max = (int)miEff.Invoke(gm, null);
            fMp.SetValue(gm, max);          // ← 現在MP＝最大へ
            miUpd?.Invoke(gm, null);        // ← MP UI再描画
        }
    }
    catch { /* 対局シーン外なら何もしない */ }
}
private SkillSetAsset _traitHostSet;
private string _traitActiveSkillName;

private SkillSetAsset.Trait _unlockOfferTrait;
private string _unlockOfferYakuName;

private SkillSetAsset.Trait _upgradeOfferTrait;
private string _upgradeOfferYakuName;

private void RefreshTraitOffers()
{
    ResolveTraitContext(out _traitHostSet, out _traitActiveSkillName);
    if (_traitHostSet == null || string.IsNullOrEmpty(_traitActiveSkillName))
    {
        if (traitUnlockOfferTMP) traitUnlockOfferTMP.text = GetUpgradeFixedText_Local("trait_load_failed");
        if (traitUpgradeOfferTMP) traitUpgradeOfferTMP.text = GetUpgradeFixedText_Local("trait_load_failed");
        _unlockOfferYakuName = null;
        _upgradeOfferYakuName = null;

        if (traitUpgradeTraitIconImage)
        {
            traitUpgradeTraitIconImage.sprite = null;
            traitUpgradeTraitIconImage.enabled = false;
        }

        return;
    }

    // 全候補（スキルに設定された該当役＝“全件”）
    _traitHostSet.EnsureInitialTraitUnlocks(_traitActiveSkillName);

    // ★仕様変更：各Traitの先頭は最初からLv1
    __EnsureInitialTraitFirstYakuIsLv1(_traitHostSet, _traitActiveSkillName);

    var all = _traitHostSet.GetTraitYakuFor(_traitActiveSkillName);

    // ★仕様変更：解放購入は廃止し「強化（レベルアップ）のみ」
    PickRandomFromAll(all, out _upgradeOfferTrait, out _upgradeOfferYakuName);

    if (traitUpgradeOfferTMP)
    {
        if (string.IsNullOrEmpty(_upgradeOfferYakuName))
        {
                        traitUpgradeOfferTMP.text = GetUpgradeFixedText_Local("trait_upgrade_none");

            if (traitUpgradeTraitIconImage)
            {
                traitUpgradeTraitIconImage.sprite = null;
                traitUpgradeTraitIconImage.enabled = false;
            }
        }
        else
        {
int rawLv = _traitHostSet.GetTraitYakuLevel(_traitActiveSkillName, _upgradeOfferTrait, _upgradeOfferYakuName);

// 仕様：未解放(-1)は Lv0 扱いで購入できる（購入で Lv1 へ）
int displayLv = Mathf.Max(0, rawLv);

// ★追加：特別牌の「役強化Lv+」を表記だけ足し込む（内部Lvは変えない）
int specialBonus = __GetLastSpecialTileTraitBonusForYaku(_upgradeOfferYakuName);

// 表示上のLv（= 通常Lv + 特別牌ボーナス）
int displayLvWithSpecial = Mathf.Max(0, displayLv + Mathf.Max(0, specialBonus));

int nextLvWithSpecial = displayLvWithSpecial + 1;

string currentPct = __FormatPct(__CalcTraitEffectAdd01(
    _traitHostSet,
    _upgradeOfferTrait,
    _upgradeOfferYakuName,
    displayLvWithSpecial));

string nextPct = __FormatPct(__CalcTraitEffectAdd01(
    _traitHostSet,
    _upgradeOfferTrait,
    _upgradeOfferYakuName,
    nextLvWithSpecial));

int cost = CalcUpgradeCost(displayLv);

traitUpgradeOfferTMP.text =
    $"{LocalizeUpgradeYakuDisplay_Local(_upgradeOfferYakuName)}\n" +
    string.Format(
        GetUpgradeFixedText_Local("trait_upgrade_level_line_format"),
        displayLvWithSpecial,
        currentPct,
        nextLvWithSpecial,
        nextPct) + "\n" +
    string.Format(
        GetUpgradeFixedText_Local("trait_upgrade_cost_line_format"),
        cost);
// ★アイコン：Traitに応じて差し替え＋色もInspector値を適用
if (traitUpgradeTraitIconImage)
{
    Sprite sp = null;
    Color iconColor = Color.white;

    switch (_upgradeOfferTrait)
    {
        case SkillSetAsset.Trait.Geki:
            sp = traitIconGeki;
            iconColor = traitIconColorGeki;
            break;

        case SkillSetAsset.Trait.Shun:
            sp = traitIconShun;
            iconColor = traitIconColorShun;
            break;

        case SkillSetAsset.Trait.Iyu:
            sp = traitIconIyu;
            iconColor = traitIconColorIyu;
            break;

        default:
            sp = null;
            iconColor = Color.white;
            break;
    }

    traitUpgradeTraitIconImage.sprite = sp;
    traitUpgradeTraitIconImage.color = iconColor;
    traitUpgradeTraitIconImage.enabled = (sp != null);
}
        }

    }

}

private void PickRandomFromAll(
    (List<string> ge, List<string> sh, List<string> iy) all,
    out SkillSetAsset.Trait outTrait,
    out string outYakuName
)
{
    var list = new List<(SkillSetAsset.Trait t, string y)>();

    AddAll(list, SkillSetAsset.Trait.Geki, all.ge);
    AddAll(list, SkillSetAsset.Trait.Shun, all.sh);
    AddAll(list, SkillSetAsset.Trait.Iyu,  all.iy);

    if (list.Count == 0)
    {
        outTrait = SkillSetAsset.Trait.Geki;
        outYakuName = null;
        return;
    }

    var pick = list[UnityEngine.Random.Range(0, list.Count)];
    outTrait = pick.t;
    outYakuName = pick.y;
}
private void AddAll(List<(SkillSetAsset.Trait, string)> dst, SkillSetAsset.Trait t, List<string> all)
{
    var a = (all ?? new List<string>())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => NormalizeUpgradeYakuKey_Local(x))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

    foreach (var y in a) dst.Add((t, y));
}
private void ResolveTraitContext(out SkillSetAsset hostSet, out string activeSkillName)
{
    hostSet = null;
    activeSkillName = "";

    // SkillSetSceneController が保存した想定キー
    activeSkillName = PlayerPrefs.GetString("EquippedActiveSkill", "");

    var setId = PlayerPrefs.GetString("EquippedSkillSetId", "");
    if (!string.IsNullOrEmpty(setId))
    {
        // あなたのプロジェクトの読み方に合わせる：
        // 例：Resources/SkillSets から id 一致の SkillSetAsset を探す
        var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
        foreach (var s in allSets)
        {
            if (s == null) continue;
            if (string.Equals(s.id, setId, StringComparison.OrdinalIgnoreCase))
            {
                hostSet = s;
                break;
            }
        }
    }

    // setId で見つからない場合、activeSkillName 所属から逆引き
    if (hostSet == null && !string.IsNullOrEmpty(activeSkillName))
    {
        // ★ outパラメータ activeSkillName をラムダで参照しない（CS1628回避）
        var skillName = activeSkillName;

        var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
        foreach (var s in allSets)
        {
            if (s == null || s.activeSkills == null) continue;
            if (s.activeSkills.Any(e => e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                hostSet = s;
                break;
            }
        }
    }
}

private void PickRandomFromLocked(
    (List<string> ge, List<string> sh, List<string> iy) all,
    (List<string> ge, List<string> sh, List<string> iy) unlocked,
    out SkillSetAsset.Trait outTrait,
    out string outYakuName
)
{
    var locked = new List<(SkillSetAsset.Trait t, string y)>();

    AddLocked(locked, SkillSetAsset.Trait.Geki, all.ge, unlocked.ge);
    AddLocked(locked, SkillSetAsset.Trait.Shun, all.sh, unlocked.sh);
    AddLocked(locked, SkillSetAsset.Trait.Iyu,  all.iy, unlocked.iy);

    if (locked.Count == 0)
    {
        outTrait = SkillSetAsset.Trait.Geki;
        outYakuName = null;
        return;
    }

    var pick = locked[UnityEngine.Random.Range(0, locked.Count)];
    outTrait = pick.t;
    outYakuName = pick.y;
}
private void AddLocked(List<(SkillSetAsset.Trait, string)> locked, SkillSetAsset.Trait t, List<string> all, List<string> unlocked)
{
    var a = (all ?? new List<string>())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => NormalizeUpgradeYakuKey_Local(x))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

    var u = (unlocked ?? new List<string>())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => NormalizeUpgradeYakuKey_Local(x))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

    foreach (var y in a)
    {
        if (!u.Any(z => string.Equals(z, y, StringComparison.OrdinalIgnoreCase)))
            locked.Add((t, y));
    }
}
private void PickRandomFromUnlocked(
    (List<string> ge, List<string> sh, List<string> iy) unlocked,
    out SkillSetAsset.Trait outTrait,
    out string outYakuName
)
{
    var list = new List<(SkillSetAsset.Trait t, string y)>();

    AddUnlocked(list, SkillSetAsset.Trait.Geki, unlocked.ge);
    AddUnlocked(list, SkillSetAsset.Trait.Shun, unlocked.sh);
    AddUnlocked(list, SkillSetAsset.Trait.Iyu,  unlocked.iy);

    if (list.Count == 0)
    {
        outTrait = SkillSetAsset.Trait.Geki;
        outYakuName = null;
        return;
    }

    var pick = list[UnityEngine.Random.Range(0, list.Count)];
    outTrait = pick.t;
    outYakuName = pick.y;
}
private void AddUnlocked(List<(SkillSetAsset.Trait, string)> dst, SkillSetAsset.Trait t, List<string> unlocked)
{
    var u = (unlocked ?? new List<string>())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => NormalizeUpgradeYakuKey_Local(x))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToList();

    foreach (var y in u) dst.Add((t, y));
}
private void OnClickTraitUnlockBuy()
{
    if (_traitHostSet == null || string.IsNullOrEmpty(_traitActiveSkillName)) return;
    if (string.IsNullOrEmpty(_unlockOfferYakuName)) return;

    int price = CalcUnlockPrice(_traitHostSet, _unlockOfferYakuName);
    if (!TrySpendGold(price)) return;

    _traitHostSet.UnlockTraitYaku(_traitActiveSkillName, _unlockOfferTrait, _unlockOfferYakuName);

    RefreshTraitOffers();
}
private void OnClickTraitUpgradeBuy()
{
    if (_traitHostSet == null || string.IsNullOrEmpty(_traitActiveSkillName)) return;
    if (string.IsNullOrEmpty(_upgradeOfferYakuName)) return;

    int rawLv = _traitHostSet.GetTraitYakuLevel(_traitActiveSkillName, _upgradeOfferTrait, _upgradeOfferYakuName);

    // 未解放(-1)は Lv0 扱いで価格計算
    int displayLv = Mathf.Max(0, rawLv);
    int cost = CalcUpgradeCost(displayLv);

    if (!TrySpendGold(cost)) return;

    // ★仕様：Lv0→Lv1で効果が発動する
    // もし未解放(-1)なら、まず Unlock して Lv0 を作ってから +1
    if (rawLv < 0)
    {
        _traitHostSet.UnlockTraitYaku(_traitActiveSkillName, _upgradeOfferTrait, _upgradeOfferYakuName);
    }

    _traitHostSet.AddTraitYakuLevel(_traitActiveSkillName, _upgradeOfferTrait, _upgradeOfferYakuName, 1);

    RefreshTraitOffers();
}
private void __EnsureInitialTraitFirstYakuIsLv1(SkillSetAsset hostSet, string activeSkillName)
{
    if (hostSet == null || string.IsNullOrEmpty(activeSkillName)) return;

    try
    {
        var all = hostSet.GetTraitYakuFor(activeSkillName);

        EnsureOne(SkillSetAsset.Trait.Geki, all.ge);
        EnsureOne(SkillSetAsset.Trait.Shun, all.sh);
        EnsureOne(SkillSetAsset.Trait.Iyu,  all.iy);

        void EnsureOne(SkillSetAsset.Trait t, List<string> list)
        {
            if (list == null || list.Count <= 0) return;

            string yaku = (list[0] ?? "").Trim();
            if (string.IsNullOrEmpty(yaku)) return;

            int lv = -1;
            try { lv = hostSet.GetTraitYakuLevel(activeSkillName, t, yaku); } catch { lv = -1; }

            if (lv < 0)
            {
                try { hostSet.UnlockTraitYaku(activeSkillName, t, yaku); } catch { }
                lv = 0;
            }

            if (lv == 0)
            {
                try { hostSet.AddTraitYakuLevel(activeSkillName, t, yaku, 1); } catch { }
            }
        }
    }
    catch { }
}
private bool TrySpendGold(int amount)
{
    EnsureWalletLoaded(); // 既存：RunCurrency.Get() を一度呼ぶ安全策 :contentReference[oaicite:4]{index=4}
    amount = Mathf.Max(0, amount);
    if (amount <= 0) return true;

    // 既存の購入処理と同じ通貨系を使用（RunCurrency.Spend）
    bool ok = GameManager.RunCurrency.Spend(amount);
    if (ok)
    {
        RefreshGoldText(); // UI 反映（既存）:contentReference[oaicite:5]{index=5}

        // SE：ゴールド消費（共通） ※AudioManagerに集約
        if (AudioManager.Instance)
        {
            AudioManager.Instance.PlayGoldSpendSE();
        }
    }
    return ok;
}
private int CalcUnlockPrice(SkillSetAsset hostSet, string yakuName)
{
    var diff = hostSet.GetDifficultyForYaku(yakuName);
    switch (diff)
    {
        case SkillSetAsset.YakuDifficulty.Easy:   return traitUnlockPriceEasy;
        case SkillSetAsset.YakuDifficulty.Hard:   return traitUnlockPriceHard;
        case SkillSetAsset.YakuDifficulty.Yakuman:return traitUnlockPriceYakuman;
        default:                                  return traitUnlockPriceNormal;
    }
}
private int CalcUpgradeCost(int currentLv)
{
    currentLv = Mathf.Max(0, currentLv);
    float cost = traitUpgradeBaseCost * Mathf.Pow(Mathf.Max(1f, traitUpgradeCostMultiplier), currentLv);
    return Mathf.Max(0, Mathf.RoundToInt(cost));
}
// 追加：UpgradeSceneMenu から呼んで「外部Root参照」も含めて確実に閉じる
public void ForceCloseAllSectionRoots()
{
    if (statusShopRoot) statusShopRoot.SetActive(false);
    if (deckShopRoot) deckShopRoot.SetActive(false);
    if (traitYakuShopRoot) traitYakuShopRoot.SetActive(false);

    // デッキ関連のオーバーレイも閉じる（重なり防止）
    if (deckPanel) deckPanel.SetActive(false);
}
private void OnClickGemResultOk()
{
    if (!_gemPanelShowing) return;

    if (gemResultPanelRoot) gemResultPanelRoot.SetActive(false);
    RestoreAllButtons_AfterGemPanel();
    _gemPanelShowing = false;
}

private void TryProcessPendingGemReward_OnEnterUpgrade()
{
    int pending = 0;
    try { pending = PlayerPrefs.GetInt(PrefKey_PendingGemRoll, 0); } catch { pending = 0; }
    if (pending != 1) return;

    bool isZeus = false;
    try { isZeus = PlayerPrefs.GetInt(PrefKey_PendingGemIsZeus, 0) == 1; } catch { isZeus = false; }

    // ゼウスは報酬画面で確定表示するので Upgrade 側では処理しない
    if (isZeus)
    {
        ClearPendingGemRoll();
        return;
    }

    string enemyName = "";
    try { enemyName = PlayerPrefs.GetString(PrefKey_PendingGemEnemyName, ""); } catch { enemyName = ""; }

    int gained = ConsumePendingGemReward_NoUI_OnEnterUpgrade();

    if (gained <= 0) return;

    ShowGemResultPanel(enemyName, gained);
}

private void ClearPendingGemRoll()
{
    try
    {
        PlayerPrefs.SetInt(PrefKey_PendingGemRoll, 0);
        PlayerPrefs.SetInt(PrefKey_PendingGemEnemyExcelKey, -1);
        PlayerPrefs.SetString(PrefKey_PendingGemEnemyName, "");
        PlayerPrefs.SetInt(PrefKey_PendingGemIsZeus, 0);
        PlayerPrefs.Save();
    }
    catch { }
}
public static int ConsumePendingGemReward_NoUI_OnEnterUpgrade()
{
    int pending = 0;
    try { pending = PlayerPrefs.GetInt(PrefKey_PendingGemRoll, 0); } catch { pending = 0; }
    if (pending != 1) return 0;

    bool isZeus = false;
    try { isZeus = PlayerPrefs.GetInt(PrefKey_PendingGemIsZeus, 0) == 1; } catch { isZeus = false; }

    // ゼウスは StageClear(報酬) 側で確定付与・表示するので、Upgrade では触らない
    if (isZeus) return 0;

    int excelKey = -1;
    try { excelKey = PlayerPrefs.GetInt(PrefKey_PendingGemEnemyExcelKey, -1); } catch { excelKey = -1; }

    string enemyName = "";
    try { enemyName = PlayerPrefs.GetString(PrefKey_PendingGemEnemyName, ""); } catch { enemyName = ""; }

    string enemyBaseName = StripLoopSuffix((enemyName ?? "").Trim());

    bool firstDefeatReward = false;

    if (excelKey >= 0)
    {
        string firstKey = PrefKey_FirstDefeatedForEnemyExcelKey(excelKey);
        int already = 0;
        try { already = PlayerPrefs.GetInt(firstKey, 0); } catch { already = 0; }

        if (already == 0)
        {
            firstDefeatReward = true;
            PlayerPrefs.SetInt(firstKey, 1);
        }
    }
    else if (!string.IsNullOrEmpty(enemyBaseName))
    {
        string firstKey = PrefKey_FirstDefeatedForEnemyName(enemyBaseName);
        int already = 0;
        try { already = PlayerPrefs.GetInt(firstKey, 0); } catch { already = 0; }

        if (already == 0)
        {
            firstDefeatReward = true;
            PlayerPrefs.SetInt(firstKey, 1);
        }
    }

    int gained = 0;

    // 初回撃破報酬
    if (firstDefeatReward)
    {
        gained += 1;
    }

    // 通常抽選
    if (UnityEngine.Random.value < 0.05f)
    {
        gained += 1;
    }

    try { PlayerPrefs.Save(); } catch { }

    // Pending はここで必ず消す（多重取得防止）
    ClearPendingGemRoll_Static();

    if (gained <= 0) return 0;

    try { SpecialTileSystem.AddGems(gained); } catch { }

    return gained;
}
private static void ClearPendingGemRoll_Static()
{
    try
    {
        PlayerPrefs.SetInt(PrefKey_PendingGemRoll, 0);
        PlayerPrefs.SetInt(PrefKey_PendingGemEnemyExcelKey, -1);
        PlayerPrefs.SetString(PrefKey_PendingGemEnemyName, "");
        PlayerPrefs.SetInt(PrefKey_PendingGemIsZeus, 0);
        PlayerPrefs.Save();
    }
    catch { }
}
private void ShowGemResultPanel(string enemyName, int gained)
{
    if (!gemResultPanelRoot || !gemResultOkButton) return;

    DisableAllButtons_ForGemPanel();

    // ★追加：宝石獲得SE
    PlayUpgradeResultSE(gemGetSE);

    if (gemResultTMP)
    {
        gemResultTMP.text = BuildGemResultText_Local(enemyName, gained);
    }

    gemResultPanelRoot.SetActive(true);
    _gemPanelShowing = true;

    gemResultOkButton.onClick.RemoveListener(OnClickGemResultOk);
    gemResultOkButton.onClick.AddListener(OnClickGemResultOk);
    gemResultOkButton.interactable = true;
}
private void DisableAllButtons_ForGemPanel()
{
    _gemPrevInteractable.Clear();

    var buttons = GameObject.FindObjectsOfType<Button>(true);
    foreach (var b in buttons)
    {
        if (!b) continue;
        _gemPrevInteractable[b] = b.interactable;
        b.interactable = false;
    }

    if (gemResultOkButton) gemResultOkButton.interactable = true;
}

private void RestoreAllButtons_AfterGemPanel()
{
    foreach (var kv in _gemPrevInteractable)
    {
        var b = kv.Key;
        if (!b) continue;
        b.interactable = kv.Value;
    }
    _gemPrevInteractable.Clear();
}

}
