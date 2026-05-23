using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;   // ★追加：Button / CanvasGroup 用
using TMPro;

// Minimal stage clear flow: on victory -> UpgradeScene, on finishing upgrade -> next battle.
// Safe even if called from UI Button.
public class StageClearManager : MonoBehaviour
{
    [SerializeField] private string upgradeSceneName = "UpgradeScene";
    [SerializeField] private string battleSceneName  = "RunScene";
    [SerializeField] private string menuSceneName = "MenuScene";   // 報酬OKの戻り先
    [SerializeField] private bool isRewardScene = false;           // このシーンが報酬画面ならON
[Header("Reward UI (報酬画面用)")]
[SerializeField] private TextMeshProUGUI rewardTitleTMP;       // 例：「報酬」
[SerializeField] private TextMeshProUGUI finalScoreTMP;        // ★最終スコア表示
[SerializeField] private TextMeshProUGUI omamoriNameTMP;       // お守り名
[SerializeField] private TextMeshProUGUI omamoriDescTMP;       // 効果の箇条書き

// ★追加：報酬表示用の背景Image（レア度色と連動）
[SerializeField] private UnityEngine.UI.Image omamoriRewardBackgroundImage;

// ★追加：お守りアイコン（装備/報酬に応じて表示＆Tint）
[SerializeField] private UnityEngine.UI.Image omamoriIconImage;

// ★追加：報酬表示用のお守りアイコンSprite（未指定なら所持一覧用を流用）
[SerializeField] private Sprite rewardOmamoriIconSprite;
// ★追加：所持数表示（例：21/20）
[SerializeField] private TextMeshProUGUI ownedCountTMP;

// ★追加：上限超え時の案内（任意。無くても動く）
[SerializeField] private TextMeshProUGUI overCapHintTMP;

[Header("Owned Omamori Panel (所持お守り)")]
[SerializeField] private Button ownedOmamoriButton;            // 「所持お守り」ボタン
[SerializeField] private GameObject ownedPanelRoot;            // 所持一覧パネルRoot
[SerializeField] private Transform ownedListParent;            // ScrollView/Content
[SerializeField] private GameObject ownedItemPrefab;           // Button+TMPの行プレハブ
[SerializeField] private Button discardButton;                 // 「破棄」ボタン
[SerializeField] private Button closeOwnedPanelButton;         // 閉じるボタン
[Header("Owned Omamori Icon (所持一覧のアイコン)")]
[SerializeField] private Sprite ownedOmamoriIconSprite;        // 所持一覧に表示するお守りアイコンSprite
[SerializeField] private string ownedOmamoriIconChildName = "Icon"; // 行プレハブ内のアイコンImage(子)の名前

[Header("Owned Omamori Row Visuals (Manual)")]
[SerializeField] private string ownedRowBackgroundChildName = "Background"; // 行プレハブ内の背景Image(任意)
[SerializeField] private Color ownedRowNormalBgColor = Color.white;
[SerializeField] private Color ownedRowSelectedBgColor = new Color(0.65f, 0.65f, 0.65f, 1f);
[SerializeField] private string ownedRowEquippedMarkChildName = "EquippedMark"; // 装備中アイコンImage(任意)

[Header("Owned Omamori List Text Style")]
[SerializeField] private TMP_FontAsset ownedListFont;            // 未指定ならプレハブ側のまま
[SerializeField] private Color ownedListTextColor = Color.white; // 本文色
[SerializeField] private Color ownedListSelectedTagColor = new Color(1f, 0.85f, 0.4f, 1f); // (選択中)
[SerializeField] private float ownedListFontSize = 28f;
[SerializeField] private int ownedListTagSizePercent = 80;
[SerializeField] private int ownedListDescSizePercent = 80;
[Header("Trait Icon Replacement (TMP) - StageClear")]
[SerializeField] private bool replaceTraitWordsWithIcons = true;

// 撃/瞬/癒 が全部入った TMP Sprite Asset（1つ）を割り当てる
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAsset = null;

// 置換対象の単語
[SerializeField] private string traitWordGeki = "撃";
[SerializeField] private string traitWordShun = "瞬";
[SerializeField] private string traitWordIyu  = "癒";

// Sprite Asset 内の index（0始まり）
[SerializeField] private int traitSpriteIndexGeki = 0;
[SerializeField] private int traitSpriteIndexShun = 1;
[SerializeField] private int traitSpriteIndexIyu  = 2;

// ★追加：Inspectorで各アイコン色を指定
[SerializeField] private Color traitIconColorGeki = Color.white;
[SerializeField] private Color traitIconColorShun = Color.white;
[SerializeField] private Color traitIconColorIyu  = Color.white;

// アイコンの大きさ（%） 100=通常。90にすると少し小さくなる
[SerializeField, Range(50, 150)] private int traitIconSizePercent = 100;

// アイコンの上下位置（em）。0.0で補正なし。-0.05 などで少し下げられる
[SerializeField] private float traitIconVOffsetEm = 0f;
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
private void ApplyTraitSpriteAssetToTMP(TextMeshProUGUI tmp)
{
    if (!tmp) return;
    if (traitIconsSpriteAsset == null) return;
    tmp.spriteAsset = traitIconsSpriteAsset;
}
private void SetOmamoriRewardBackgroundVisual(bool visible, Color color)
{
    if (!omamoriRewardBackgroundImage) return;

    omamoriRewardBackgroundImage.color = color;

    if (omamoriRewardBackgroundImage.gameObject.activeSelf != visible)
        omamoriRewardBackgroundImage.gameObject.SetActive(visible);
}
private string ReplaceTraitWordsWithIcons(string src)
{
    if (!replaceTraitWordsWithIcons) return src;
    if (string.IsNullOrEmpty(src)) return src;

    string WrapWithVOffset(string inner)
    {
        if (Mathf.Abs(traitIconVOffsetEm) < 0.0001f) return inner;
        return $"<voffset={traitIconVOffsetEm}em>{inner}</voffset>";
    }

    string ToHex(Color c)
    {
        return ColorUtility.ToHtmlStringRGBA(c);
    }

    string MakeTag(int spriteIndex, Color color)
    {
        if (spriteIndex < 0) return "";

        string spriteTag = $"<sprite={spriteIndex} tint=1 color=#{ToHex(color)}>";
        string baseTag;

        if (traitIconSizePercent != 100)
            baseTag = $"<size={traitIconSizePercent}%>{spriteTag}</size>";
        else
            baseTag = spriteTag;

        return WrapWithVOffset(baseTag);
    }

    // 「撃破」など別単語に食い込む誤置換を避けたいので、前後が “日本語文字の連なり” になりにくい場面を優先して置換する。
    // ただし「撃の」「瞬の」「癒の」のように「の」が続くのは置換対象にしたいので、後読み条件に「の」も許可する。
    string jp = "一-龯ぁ-ゔァ-ヴー々〆〤";
    string patGeki = $@"(?<![{jp}]){Regex.Escape(traitWordGeki)}(?=($|[^ {jp}]|の))".Replace(" ", "");
    string patShun = $@"(?<![{jp}]){Regex.Escape(traitWordShun)}(?=($|[^ {jp}]|の))".Replace(" ", "");
    string patIyu  = $@"(?<![{jp}]){Regex.Escape(traitWordIyu)}(?=($|[^ {jp}]|の))".Replace(" ", "");

    try
    {
        if (!string.IsNullOrEmpty(traitWordGeki))
            src = Regex.Replace(src, patGeki, MakeTag(traitSpriteIndexGeki, traitIconColorGeki));

        if (!string.IsNullOrEmpty(traitWordShun))
            src = Regex.Replace(src, patShun, MakeTag(traitSpriteIndexShun, traitIconColorShun));

        if (!string.IsNullOrEmpty(traitWordIyu))
            src = Regex.Replace(src, patIyu, MakeTag(traitSpriteIndexIyu, traitIconColorIyu));
    }
    catch
    {
        return src;
    }

    return src;
}
// 内部（一覧生成・選択）
private readonly List<GameObject> _ownedRows = new();
private int _selectedOwnedId = 0;

    [Header("Omamori Reveal UI")]
    [SerializeField] private TextMeshProUGUI omamoriRarityTMP;     // ★レア度表示用テキスト
    [SerializeField] private GameObject      omamoriUnknownRoot;   // ★「？」を表示するルート
    [SerializeField] private CanvasGroup     omamoriDetailCanvasGroup; // ★中身表示用(フェード対象)
    [SerializeField] private Button          omamoriRevealButton;  // ★「確認」ボタン
    [SerializeField] private float           omamoriRevealFadeSeconds = 0.6f; // フェード時間

    [Header("Legendary 演出")]
    [SerializeField] private bool     useLegendaryAnimation = true;       // ON なら派手演出
    [SerializeField] private Animator legendaryAnimator;                  // 既にシーンにある Animator があれば指定
    [SerializeField] private string   legendaryTriggerName = "Play";      // 再生トリガー名
    [SerializeField] private Transform legendaryEffectParent;             // エフェクトの親（未指定なら自動）
    [SerializeField] private string   legendaryPrefabPath = "LegendaryOmamoriBurst"; // Resources 用パス
    [SerializeField] private float    legendaryEffectLifetime = 2.5f;     // 自動破棄時間

[Header("Reward Buttons")]
[SerializeField] private Button rewardOkButton;  // ★これを追加

[Header("SE (Reward Scene)")]
[SerializeField] private AudioSource rewardSESource;                 // SEを鳴らすAudioSource
[SerializeField] private AudioClip omamoriRevealSE_Normal;           // ノーマル
[SerializeField] private AudioClip omamoriRevealSE_Common;           // コモン
[SerializeField] private AudioClip omamoriRevealSE_Rare;             // レア
[SerializeField] private AudioClip omamoriRevealSE_Epic;             // エピック
[SerializeField] private AudioClip omamoriRevealSE_Legendary;        // レジェンダリー

[SerializeField] private AudioClip gemGetSE;                         // 宝石獲得
[SerializeField] private AudioClip uniqueOmamoriGetSE;               // 神器獲得（ユニークお守り）

[Header("Gem Result Panel (宝石獲得結果)")]
[SerializeField] private GameObject gemResultPanelRoot;
[SerializeField] private TextMeshProUGUI gemResultTMP;
[SerializeField] private Button gemResultOkButton;

[Header("Unique Omamori Result Panel (ユニークお守り獲得結果)")]
[SerializeField] private GameObject uniqueOmamoriResultPanelRoot;
[SerializeField] private TextMeshProUGUI uniqueOmamoriTitleTMP;
[SerializeField] private TextMeshProUGUI uniqueOmamoriDescTMP;
[SerializeField] private Button uniqueOmamoriOkButton;

private readonly Dictionary<Button, bool> _gemPrevInteractable = new Dictionary<Button, bool>();
private bool _gemPanelShowing = false;

private readonly Dictionary<Button, bool> _uniquePrevInteractable = new Dictionary<Button, bool>();
private bool _uniquePanelShowing = false;

private const string PrefKey_PendingGemRoll = "Gem_PendingRoll";
private const string PrefKey_PendingGemEnemyExcelKey = "Gem_PendingEnemyExcelKey";
private const string PrefKey_PendingGemEnemyName = "Gem_PendingEnemyName";
private const string PrefKey_PendingGemIsZeus = "Gem_PendingIsZeus";

    // 内部状態
    private int    _pendingOmamoriId = 0;
    private string _pendingOmamoriName;
    private string _pendingOmamoriDesc;
    private string _pendingOmamoriRarity;
    private bool   _omamoriRevealed = false;

    // 裏ボス（ハーデス）追加神器（100%）
    private int    _bonusUniqueOmamoriId = 0;
    private string _bonusUniqueOmamoriName;
    private string _bonusUniqueOmamoriDesc;
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
            if (e == null) continue;

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
    if (!isRewardScene) return;

    WireOwnedPanelUI();
    PopulateRewardUI();

    RefreshOwnedCountUI();
    UpdateRewardOkLock();

    // ★追加：ハーデス撃破の追加神器があるなら、シーン到達直後に結果パネルを出す
    if (TryShowBonusUniqueOmamoriPanel_OnRewardScene())
        return;

    // ★追加：ゼウス撃破後は報酬画面で宝石獲得結果パネルを表示（確定1個）
    TryProcessPendingGemReward_OnRewardScene();
}
private static string NormalizeRarityKey_Local(string rarityRaw)
{
    if (string.IsNullOrEmpty(rarityRaw)) return "";

    switch (rarityRaw)
    {
        case "レジェンダリー":
        case "Legendary":
            return "Legendary";

        case "エピック":
        case "Epic":
            return "Epic";

        case "レア":
        case "Rare":
            return "Rare";

        case "コモン":
        case "Common":
            return "Common";

        case "ノーマル":
        case "Normal":
            return "Normal";

        default:
            return rarityRaw;
    }
}
private static string GetStageClearFixedText_Local(string key)
{
    return LocalizationManager.Fixed(key);
}

private static string DetectRarityKeyFromText_Local(string name, string desc)
{
    string src = ((name ?? "") + " " + (desc ?? "")).ToLowerInvariant();

    if (src.Contains("legendary") || src.Contains("レジェンダリー")) return "Legendary";
    if (src.Contains("epic")      || src.Contains("エピック"))       return "Epic";
    if (src.Contains("rare")      || src.Contains("レア"))           return "Rare";
    if (src.Contains("common")    || src.Contains("コモン"))         return "Common";
    if (src.Contains("normal")    || src.Contains("ノーマル"))       return "Normal";

    return null;
}

private bool TryShowBonusUniqueOmamoriPanel_OnRewardScene()
{
    if (_bonusUniqueOmamoriId <= 0) return false;
    if (!uniqueOmamoriResultPanelRoot || !uniqueOmamoriOkButton) return false;

    ShowUniqueOmamoriResultPanel(_bonusUniqueOmamoriId);
    return true;
}

private void OnClickUniqueOmamoriOk()
{
    if (!_uniquePanelShowing) return;

    if (uniqueOmamoriResultPanelRoot) uniqueOmamoriResultPanelRoot.SetActive(false);
    RestoreAllButtons_AfterUniquePanel();
    _uniquePanelShowing = false;

    // ★神器パネルの次に（あれば）宝石パネルを出す
    TryProcessPendingGemReward_OnRewardScene();
}

private void ShowUniqueOmamoriResultPanel(int omamoriId)
{
    if (!uniqueOmamoriResultPanelRoot || !uniqueOmamoriOkButton) return;

    DisableAllButtons_ForUniquePanel();

    if (omamoriId <= 0)
    {
        if (uniqueOmamoriTitleTMP) uniqueOmamoriTitleTMP.text = GetStageClearFixedText_Local("unique_title_error");

        if (uniqueOmamoriDescTMP)
        {
            uniqueOmamoriDescTMP.text =
                GetStageClearFixedText_Local("unique_desc_error_line1") + "\n"
              + GetStageClearFixedText_Local("unique_desc_error_line2");
        }

        uniqueOmamoriResultPanelRoot.SetActive(true);
        _uniquePanelShowing = true;
        uniqueOmamoriOkButton.onClick.RemoveListener(OnClickUniqueOmamoriOk);
        uniqueOmamoriOkButton.onClick.AddListener(OnClickUniqueOmamoriOk);
        uniqueOmamoriOkButton.interactable = true;
        return;
    }
    string title = "";
    string desc = "";

    try { title = PlayerData.GetOmamoriName_RewardUI_Localized(omamoriId); } catch { title = ""; }
    try { desc  = PlayerData.GetOmamoriEffectsOnlyText_RewardUI_Localized(omamoriId, true); } catch { desc = ""; }

    if (uniqueOmamoriTitleTMP) uniqueOmamoriTitleTMP.text = title;

    if (uniqueOmamoriDescTMP)
    {
        ApplyTraitSpriteAssetToTMP(uniqueOmamoriDescTMP);
        uniqueOmamoriDescTMP.text = ReplaceTraitWordsWithIcons(desc);
    }

    uniqueOmamoriResultPanelRoot.SetActive(true);
    _uniquePanelShowing = true;
    uniqueOmamoriOkButton.onClick.RemoveListener(OnClickUniqueOmamoriOk);
    uniqueOmamoriOkButton.onClick.AddListener(OnClickUniqueOmamoriOk);
    uniqueOmamoriOkButton.interactable = true;
}

private void DisableAllButtons_ForUniquePanel()
{
    _uniquePrevInteractable.Clear();

    var buttons = GameObject.FindObjectsOfType<Button>(true);
    foreach (var b in buttons)
    {
        if (!b) continue;
        _uniquePrevInteractable[b] = b.interactable;
        b.interactable = false;
    }

    if (uniqueOmamoriOkButton) uniqueOmamoriOkButton.interactable = true;
}

private void RestoreAllButtons_AfterUniquePanel()
{
    foreach (var kv in _uniquePrevInteractable)
    {
        var b = kv.Key;
        if (!b) continue;
        b.interactable = kv.Value;
    }
    _uniquePrevInteractable.Clear();
}

private static UnityEngine.Color GetRarityColorSafe_Local(string rarityKeyOrRaw, string japaneseTagText)
{
    // 1) 英語キーで試す
    string key = NormalizeRarityKey_Local(rarityKeyOrRaw);

    try
    {
        var c = OfudaRarityColors.Get(key);
        if (!(c == UnityEngine.Color.white && key != "Normal"))
            return c;
    }
    catch { }

    // 2) 日本語キーで試す
    if (!string.IsNullOrEmpty(japaneseTagText))
    {
        try
        {
            var c2 = OfudaRarityColors.Get(japaneseTagText);
            if (!(c2 == UnityEngine.Color.white && japaneseTagText != "ノーマル"))
                return c2;
        }
        catch { }
    }

    // 3) 保険：固定色
    switch (japaneseTagText)
    {
        case "レジェンダリー": return new UnityEngine.Color(1.00f, 0.55f, 0.00f);
        case "エピック":       return new UnityEngine.Color(0.60f, 0.20f, 1.00f);
        case "レア":           return new UnityEngine.Color(1.00f, 0.85f, 0.00f);
        case "コモン":         return new UnityEngine.Color(0.20f, 0.60f, 1.00f);
        case "ノーマル":       return UnityEngine.Color.white;
    }

    switch (key)
    {
        case "Legendary": return new UnityEngine.Color(1.00f, 0.55f, 0.00f);
        case "Epic":      return new UnityEngine.Color(0.60f, 0.20f, 1.00f);
        case "Rare":      return new UnityEngine.Color(1.00f, 0.85f, 0.00f);
        case "Common":    return new UnityEngine.Color(0.20f, 0.60f, 1.00f);
        case "Normal":    return UnityEngine.Color.white;
    }

    return UnityEngine.Color.white;
}
private void SetOmamoriIconVisual(int omamoriId, string rarityKeyOrRaw, string rarityJp)
{
    if (!omamoriIconImage) return;

    // ★Spriteを未設定ならここで補完（報酬用→無ければ所持一覧用）
    if (omamoriIconImage.sprite == null)
    {
        var sp = rewardOmamoriIconSprite ? rewardOmamoriIconSprite : ownedOmamoriIconSprite;
        if (sp) omamoriIconImage.sprite = sp;
    }
    omamoriIconImage.preserveAspect = true;

    // ★神器（ユニーク）は常に赤Tint（他レア度の色分けとは別枠）
    // まず ID が分かるなら PlayerData 側の判定を優先する
    if (omamoriId > 0)
    {
        try
        {
            if (PlayerData.TryGetOmamoriRarityColor(omamoriId, out var cById))
            {
                omamoriIconImage.color = cById;

                if (!omamoriIconImage.gameObject.activeSelf)
                    omamoriIconImage.gameObject.SetActive(true);

                return;
            }
        }
        catch { }
    }

    if (string.IsNullOrEmpty(rarityKeyOrRaw) && string.IsNullOrEmpty(rarityJp))
    {
        if (omamoriIconImage.gameObject.activeSelf)
            omamoriIconImage.gameObject.SetActive(false);
        return;
    }

    var c = GetRarityColorSafe_Local(rarityKeyOrRaw, rarityJp);
    omamoriIconImage.color = c;

    if (!omamoriIconImage.gameObject.activeSelf)
        omamoriIconImage.gameObject.SetActive(true);
}
private static string RarityToJp_Local(string rarityRaw)
{
    string rarityKey = NormalizeRarityKey_Local(rarityRaw);
    if (string.IsNullOrEmpty(rarityKey))
        return rarityRaw ?? "";

    string localized = LocalizationManager.Rarity(rarityKey);

    if (string.IsNullOrEmpty(localized))
        return rarityRaw ?? "";

    string keyLike = "rarity." + rarityKey;
    if (string.Equals(localized, keyLike, System.StringComparison.Ordinal))
        return rarityRaw ?? "";

    return localized;
}
    public void OnStageClear()
    {
        
        // Persist player's current HP so it carries into the next enemy battle
        try { var gm = GameObject.FindObjectOfType<GameManager>(); if (gm) gm.PersistRunPlayerHP(); } catch {}
// 次の敵へ（まだ戦闘には入らない）
        GameManager.AdvanceToNextEnemy();
        if (!string.IsNullOrEmpty(upgradeSceneName))
            SceneManager.LoadScene(upgradeSceneName);
        else
            OnFinishUpgrade(); // 保険
    }

    // 強化終了（UpgradeManagerからも呼べるようにpublic）
    public void OnFinishUpgrade()
    {
        // メニュー指定の会話導線があればそちらへ。なければ直接バトルへ。
        GameManager.StartNextBattleScene(battleSceneName);
    }
private void PopulateRewardUI()
{
    if (rewardTitleTMP && string.IsNullOrEmpty(rewardTitleTMP.text))
        rewardTitleTMP.text = GetStageClearFixedText_Local("reward_title");
    int id = PlayerData.LastGrantedOmamoriId;   // 直前のステージで付与されたお守りID

        _pendingOmamoriId = id;

        // 報酬なしのケース
    if (id == 0)
    {
        _pendingOmamoriId     = 0;
        _pendingOmamoriName   = null;
        _pendingOmamoriDesc   = null;
        _pendingOmamoriRarity = null;
        _omamoriRevealed      = true;   // ★確認不要なので“既に済み”扱い

        if (omamoriUnknownRoot) omamoriUnknownRoot.SetActive(false);

        if (omamoriDetailCanvasGroup)
        {
            omamoriDetailCanvasGroup.alpha          = 1f;
            omamoriDetailCanvasGroup.interactable   = false;
            omamoriDetailCanvasGroup.blocksRaycasts = false;
        }
        if (omamoriNameTMP)   omamoriNameTMP.text   = GetStageClearFixedText_Local("reward_none");
        if (omamoriDescTMP)   omamoriDescTMP.text   = "";
        if (omamoriRarityTMP) omamoriRarityTMP.text = "";
        if (omamoriRevealButton)
            omamoriRevealButton.gameObject.SetActive(false);

if (rewardOkButton)
{
    rewardOkButton.gameObject.SetActive(true);
    rewardOkButton.interactable = true;
}

// ★追加：所持数表示・OKロック
RefreshOwnedCountUI();
UpdateRewardOkLock();
SetOmamoriIconVisual(0, "", "");
SetOmamoriRewardBackgroundVisual(false, Color.white);

return;
    }
    _pendingOmamoriName   = PlayerData.GetOmamoriName_RewardUI_Localized(id);
    _pendingOmamoriDesc   = PlayerData.GetOmamoriEffectsOnlyText_RewardUI_Localized(id, true);

    if (!PlayerData.TryGetOmamoriRarityKey(id, out _pendingOmamoriRarity))
        _pendingOmamoriRarity = DetectRarityKeyFromText_Local(_pendingOmamoriName, _pendingOmamoriDesc);

    _bonusUniqueOmamoriId = 0;
    _bonusUniqueOmamoriName = null;
    _bonusUniqueOmamoriDesc = null;

    try
    {
        int bid = PlayerPrefs.GetInt("SecretHades_BonusUniqueOmamoriId", 0);
        if (bid > 0)
        {
            _bonusUniqueOmamoriId = bid;
            _bonusUniqueOmamoriName = PlayerData.GetOmamoriName_RewardUI_Localized(bid);
            _bonusUniqueOmamoriDesc = PlayerData.GetOmamoriEffectsOnlyText_RewardUI_Localized(bid, true);

            PlayerPrefs.SetInt("SecretHades_BonusUniqueOmamoriId", 0);
            PlayerPrefs.Save();
        }
    }
    catch { }
    _omamoriRevealed      = false;

    // ★メニューに戻るボタンは一旦「非表示」にしておく（押せない＆見えない）
    if (rewardOkButton)
    {
        rewardOkButton.gameObject.SetActive(false);
        // interactable は SetActive(false) で実質無効になるので、そのままでもOK
    }

    // 初期状態：? 表示＋中身は非表示
    if (omamoriUnknownRoot) omamoriUnknownRoot.SetActive(true);

        if (omamoriDetailCanvasGroup)
        {
            omamoriDetailCanvasGroup.gameObject.SetActive(true);
            omamoriDetailCanvasGroup.alpha         = 0f;   // 完全に透明
            omamoriDetailCanvasGroup.interactable  = false;
            omamoriDetailCanvasGroup.blocksRaycasts = false;
        }

// ?表示
if (omamoriNameTMP)   omamoriNameTMP.text   = GetStageClearFixedText_Local("reward_unknown_name");
if (omamoriDescTMP)   omamoriDescTMP.text   = "";
if (omamoriRarityTMP)
{
    omamoriRarityTMP.text  = "";
    omamoriRarityTMP.color = Color.white;
}
SetOmamoriRewardBackgroundVisual(false, Color.white);

// 「確認」ボタンを有効化
if (omamoriRevealButton)
    omamoriRevealButton.gameObject.SetActive(true);
    }
public void OnClickRevealOmamori()
{
    if (_omamoriRevealed) return;

    // SE：お守り確認（レア度別） ※AudioManagerに集約
    if (AudioManager.Instance)
    {
        AudioManager.Instance.PlayOmamoriRevealSE_ByRarity(_pendingOmamoriRarity);
    }

    StartCoroutine(RevealOmamori_Co());
}
    // お守り中身のフェードイン表示
    private System.Collections.IEnumerator RevealOmamori_Co()
    {
        _omamoriRevealed = true;

        // 「？」側UIを閉じる
        if (omamoriUnknownRoot)
            omamoriUnknownRoot.SetActive(false);
        // 本当の内容を反映
        if (omamoriNameTMP)   omamoriNameTMP.text   = _pendingOmamoriName ?? "";
if (omamoriDescTMP)
{
    ApplyTraitSpriteAssetToTMP(omamoriDescTMP);

    string main = _pendingOmamoriDesc ?? "";
    omamoriDescTMP.text = ReplaceTraitWordsWithIcons(main);
}
        ApplyRarityVisual(_pendingOmamoriRarity);   // ★③レア度色を反映

        // フェードイン
        if (omamoriDetailCanvasGroup)
        {
            omamoriDetailCanvasGroup.gameObject.SetActive(true);
            omamoriDetailCanvasGroup.interactable  = true;
            omamoriDetailCanvasGroup.blocksRaycasts = true;

            float t   = 0f;
            float dur = Mathf.Max(0.01f, omamoriRevealFadeSeconds);
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / dur);
                omamoriDetailCanvasGroup.alpha = a;
                yield return null;
            }
            omamoriDetailCanvasGroup.alpha = 1f;
        }
    // 確認ボタンは1回押したら消しておく
    if (omamoriRevealButton)
        omamoriRevealButton.gameObject.SetActive(false);

if (rewardOkButton)
{
    rewardOkButton.gameObject.SetActive(true);
    rewardOkButton.interactable = true;
}

// ★追加：所持数表示・OKロック（21/20 の時は押せなくなる）
RefreshOwnedCountUI();
UpdateRewardOkLock();


    // Legendary のときだけ派手演出
    if (string.Equals(_pendingOmamoriRarity, "Legendary", System.StringComparison.OrdinalIgnoreCase))
    {
        PlayLegendaryEffect();
    }

    }

private string DetectRarityFromText(string name, string desc)
{
    return DetectRarityKeyFromText_Local(name, desc);
}
private void ApplyRarityVisual(string rarity)
{
    Color rarityCol = Color.white;
    bool hasColorFromId = false;

    // ★まずIDベースで色を取る。文字列解析に依存しない
    try
    {
        if (_pendingOmamoriId > 0 && PlayerData.TryGetOmamoriRarityColor(_pendingOmamoriId, out var cById))
        {
            rarityCol = cById;
            hasColorFromId = true;
        }
    }
    catch
    {
        hasColorFromId = false;
    }

    // IDで取れなかったときだけ、従来どおり文字列から判定
    if (!hasColorFromId)
    {
        if (string.IsNullOrEmpty(rarity))
        {
            SetOmamoriRewardBackgroundVisual(false, Color.white);

            if (omamoriRarityTMP)
            {
                omamoriRarityTMP.text = "";
                omamoriRarityTMP.color = Color.white;
            }

            if (omamoriNameTMP)
            {
                omamoriNameTMP.color = Color.white;
            }

            return;
        }

        string rarityJpFallback = RarityToJp_Local(rarity);
        rarityCol = GetRarityColorSafe_Local(rarity, rarityJpFallback);
    }

    string rarityJp = string.IsNullOrEmpty(rarity) ? "" : RarityToJp_Local(rarity);

    if (omamoriRarityTMP)
    {
        omamoriRarityTMP.text  = rarityJp;
        omamoriRarityTMP.color = rarityCol;
    }

    // 名前は従来の仕様を維持（Legendaryだけ色を付ける）
    if (omamoriNameTMP)
    {
        if (string.Equals(rarity, "Legendary", System.StringComparison.OrdinalIgnoreCase))
            omamoriNameTMP.color = new Color(1f, 0.85f, 0.3f, 1f);
        else
            omamoriNameTMP.color = rarityCol;
    }

    // ★アイコンTint：ID優先（神器は赤）＋通常はレア度色
    SetOmamoriIconVisual(_pendingOmamoriId, rarity, rarityJp);

    // ★背景ImageはIDベース色を優先して必ず出す
    Color bgColor = rarityCol;
    bgColor.a = 1f;
    SetOmamoriRewardBackgroundVisual(true, bgColor);

    // 念のため：神器でない通常ケースの時に「レア度色」を強制反映しておく
    if (omamoriIconImage != null)
    {
        if (_pendingOmamoriId != 999 && _pendingOmamoriId != 1000 && _pendingOmamoriId != 1001)
        {
            omamoriIconImage.color = rarityCol;
        }
    }
}
    // Legendary 演出（④）
    private void PlayLegendaryEffect()
    {
        if (!useLegendaryAnimation)
            return;

        try
        {
            // 1) Animator が指定されていればトリガーを叩く
            if (legendaryAnimator)
            {
                string trig = string.IsNullOrEmpty(legendaryTriggerName) ? "Play" : legendaryTriggerName;
                legendaryAnimator.SetTrigger(trig);
                return;
            }

            // 2) Resources からプレハブをロードして再生
            if (!string.IsNullOrEmpty(legendaryPrefabPath))
            {
                var prefab = Resources.Load<GameObject>(legendaryPrefabPath);
                if (prefab)
                {
                    Transform parent = legendaryEffectParent
                        ? legendaryEffectParent
                        : (omamoriDetailCanvasGroup ? omamoriDetailCanvasGroup.transform : this.transform);

                    var inst = GameObject.Instantiate(prefab, parent, false);
                    if (legendaryEffectLifetime > 0f)
                        GameObject.Destroy(inst, legendaryEffectLifetime);
                }
            }
        }
        catch { }
    }

public void OnClickRewardOK()
{
    // ★追加：上限超えなら終了させない
    if (PlayerData.OwnedOmamori.Count > PlayerData.MaxOwnedOmamori)
    {
        RefreshOwnedCountUI();
        UpdateRewardOkLock();
        return;
    }

    // クリア後は“次の対局にデッキ構成を持ち越さない”
    try { PlayerData.ResetDeckToDefault(); } catch {}

    ResetEnemyProgressionSafe(); // ★既存：敵進行を確実に初期化
    if (!string.IsNullOrEmpty(menuSceneName))
        SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    else
        Debug.LogWarning("[StageClearManager] menuSceneName is empty.");
}

private void WireOwnedPanelUI()
{
    if (ownedOmamoriButton)
    {
        ownedOmamoriButton.onClick.RemoveAllListeners();
        ownedOmamoriButton.onClick.AddListener(OpenOwnedPanel);
    }

    if (closeOwnedPanelButton)
    {
        closeOwnedPanelButton.onClick.RemoveAllListeners();
        closeOwnedPanelButton.onClick.AddListener(CloseOwnedPanel);
    }

    if (discardButton)
    {
        discardButton.onClick.RemoveAllListeners();
        discardButton.onClick.AddListener(DiscardSelectedOwnedOmamori);
    }

    if (ownedPanelRoot)
        ownedPanelRoot.SetActive(false);
}

private void RefreshOwnedCountUI()
{
    if (!ownedCountTMP) return;

    int count = PlayerData.OwnedOmamori.Count;
    int max = PlayerData.MaxOwnedOmamori;
    ownedCountTMP.text = $"{count}/{max}";
}
private void UpdateRewardOkLock()
{
    bool over = PlayerData.OwnedOmamori.Count > PlayerData.MaxOwnedOmamori;

    if (rewardOkButton)
        rewardOkButton.interactable = !over;

    if (overCapHintTMP)
        overCapHintTMP.text = over ? GetStageClearFixedText_Local("over_cap_hint") : "";

    if (discardButton)
        discardButton.interactable = (_selectedOwnedId != 0);
}
private void OpenOwnedPanel()
{
    if (!ownedPanelRoot) return;
    ownedPanelRoot.SetActive(true);
    RebuildOwnedList();
    UpdateRewardOkLock();
}

private void CloseOwnedPanel()
{
    if (!ownedPanelRoot) return;
    ownedPanelRoot.SetActive(false);
}
private void RebuildOwnedList()
{
    // 既存行を破棄
    foreach (var go in _ownedRows) if (go) Destroy(go);
    _ownedRows.Clear();

    if (!ownedListParent || !ownedItemPrefab) return;

    var owned = new List<int>(PlayerData.OwnedOmamori);
    owned.Sort();

    // 選択中が消えていたらクリア
    if (_selectedOwnedId != 0 && !PlayerData.OwnedOmamori.Contains(_selectedOwnedId))
        _selectedOwnedId = 0;

    if (owned.Count == 0)
    {
        CreatePlainRowInOwnedList(GetStageClearFixedText_Local("owned_none"));
        UpdateRewardOkLock();
        return;
    }
    foreach (int id in owned)
    {
        var go = Instantiate(ownedItemPrefab, ownedListParent);
        _ownedRows.Add(go);

        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.minHeight = 180;
        le.preferredHeight = 180;
        le.flexibleHeight = 0;
        le.flexibleWidth = 0;

        var btn = go.GetComponent<Button>();
        var label = go.GetComponentInChildren<TextMeshProUGUI>();

        string name = PlayerData.GetOmamoriName(id);

        bool isSelected = (_selectedOwnedId == id);

        if (label)
        {
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.Left;
            label.richText = true;

            if (ownedListFont) label.font = ownedListFont;
            if (ownedListFontSize > 0f) label.fontSize = ownedListFontSize;
            label.color = ownedListTextColor;
            string selHex = ColorUtility.ToHtmlStringRGBA(ownedListSelectedTagColor);
            string selectedLabel = GetStageClearFixedText_Local("selected_tag");

            string selectedTag = isSelected
                ? $"  <size={ownedListTagSizePercent}%><color=#{selHex}>({selectedLabel})</color></size>"
                : "";

            // ★ローカライズ済み本文（1行目：レア度色＋Lv、2行目以降：-XXX）
            string uiText = PlayerData.GetOmamoriText_EquipUI_Localized(id, true);

            // タグは1行目にだけ付けたいので、先頭行と残りに分ける
            int nl = uiText.IndexOf('\n');
            string firstLine = (nl >= 0) ? uiText.Substring(0, nl) : uiText;
            string restLines = (nl >= 0) ? uiText.Substring(nl + 1) : "";

            firstLine = $"{firstLine}{selectedTag}";
ApplyTraitSpriteAssetToTMP(label);

if (!string.IsNullOrEmpty(restLines))
{
    string composed = $"{firstLine}\n<size={ownedListDescSizePercent}%>{restLines}</size>";
    label.text = ReplaceTraitWordsWithIcons(composed);
}
else
{
    label.text = ReplaceTraitWordsWithIcons(firstLine);
}
        }

        // ★追加：所持一覧のアイコン（行プレハブ内の Image 名が ownedOmamoriIconChildName のものを探す）
        var icon = FindOwnedRowIconImage(go);
        if (icon)
        {
            // Sprite
            if (ownedOmamoriIconSprite) icon.sprite = ownedOmamoriIconSprite;
            icon.preserveAspect = true;

            // Tint（PlayerData 側の色を使う：神器もここで赤にできる）
            if (PlayerData.TryGetOmamoriRarityColor(id, out var c))
            {
                icon.color = c;
                if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
            }
            else
            {
                if (icon.gameObject.activeSelf) icon.gameObject.SetActive(false);
            }

        }
        // ★背景グレーアウト（選択中）＋装備中アイコン表示
        ApplyOwnedRowVisual(go, isSelected, PlayerData.EquippedOmamori == id);

        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                _selectedOwnedId = id;
                RebuildOwnedList();     // 選択表示更新
                UpdateRewardOkLock();   // 破棄ボタンの有効化
            });
        }
    }

    RefreshOwnedCountUI();
    UpdateRewardOkLock();

    var rt = ownedListParent as RectTransform;
    if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
}
private void ApplyOwnedRowVisual(GameObject rowGo, bool isSelected, bool isEquipped)
{
    if (!rowGo) return;

    var bg = FindOwnedRowBackgroundImage(rowGo);
    if (bg)
    {
        bg.color = isSelected ? ownedRowSelectedBgColor : ownedRowNormalBgColor;
    }

    var eqMark = FindOwnedRowEquippedMarkImage(rowGo);
    if (eqMark)
    {
        bool show = isEquipped;
        if (eqMark.gameObject.activeSelf != show) eqMark.gameObject.SetActive(show);
    }
}

private Image FindOwnedRowBackgroundImage(GameObject rowGo)
{
    if (!rowGo) return null;

    if (!string.IsNullOrEmpty(ownedRowBackgroundChildName))
    {
        var tr = rowGo.transform.Find(ownedRowBackgroundChildName);
        if (tr)
        {
            var img = tr.GetComponent<Image>();
            if (img) return img;
        }
    }

    return rowGo.GetComponent<Image>();
}

private Image FindOwnedRowEquippedMarkImage(GameObject rowGo)
{
    if (!rowGo) return null;
    if (string.IsNullOrEmpty(ownedRowEquippedMarkChildName)) return null;

    var tr = rowGo.transform.Find(ownedRowEquippedMarkChildName);
    if (!tr) return null;

    return tr.GetComponent<Image>();
}
private Image FindOwnedRowIconImage(GameObject row)
{
    if (!row) return null;

    // 1) 子の名前一致（推奨：Icon）
    if (!string.IsNullOrEmpty(ownedOmamoriIconChildName))
    {
        var tr = row.transform.Find(ownedOmamoriIconChildName);
        if (tr)
        {
            var img = tr.GetComponent<Image>();
            if (img) return img;
        }
    }

    // 2) 見つからない場合は、行の子にある Image を総当たり（ボタン背景は除外）
    var all = row.GetComponentsInChildren<Image>(true);
    foreach (var img in all)
    {
        if (!img) continue;
        if (img.gameObject == row) continue; // ルートImage（背景）を避ける
        return img;
    }

    return null;
}

private static string ExtractOmamoriRarityJpFromName(string name)
{
    if (string.IsNullOrEmpty(name)) return "";

    // PlayerData.GetOmamoriName は先頭がレア度（例：「レア Lv.3 ...」）になっている前提
    var t = name.Trim();
    int sp = t.IndexOf(' ');
    string head = (sp >= 0) ? t.Substring(0, sp).Trim() : t;

    // 念のため「Lv.」等を弾く
    if (head.StartsWith("Lv")) return "";

    return head;
}

private void DiscardSelectedOwnedOmamori()
{
    if (_selectedOwnedId == 0) return;

    PlayerData.DiscardOwnedOmamori(_selectedOwnedId);
    _selectedOwnedId = 0;

    RebuildOwnedList();     // 一覧更新
    RefreshOwnedCountUI();  // 21/20 → 20/20 など
    UpdateRewardOkLock();   // OK解除
}

// 所持一覧のプレースホルダ
private void CreatePlainRowInOwnedList(string message)
{
    var go = new GameObject("Row_Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
    var rt = go.GetComponent<RectTransform>();
    rt.SetParent(ownedListParent, false);
    rt.anchorMin = new Vector2(0, 1);
    rt.anchorMax = new Vector2(1, 1);
    rt.offsetMin = new Vector2(0, 0);
    rt.offsetMax = new Vector2(0, 0);
    rt.sizeDelta = new Vector2(0, 120);

    var le = go.GetComponent<LayoutElement>();
    le.minHeight = le.preferredHeight = 120;
var tmp = go.GetComponent<TextMeshProUGUI>();
tmp.alignment = TextAlignmentOptions.Left;
tmp.enableWordWrapping = true;
tmp.richText = true;

ApplyTraitSpriteAssetToTMP(tmp);
tmp.text = ReplaceTraitWordsWithIcons(message);

if (ownedListFont) tmp.font = ownedListFont;
tmp.color = ownedListTextColor;
tmp.fontSize = (ownedListFontSize > 0f) ? ownedListFontSize : 28f;
    _ownedRows.Add(go);
}

private void OnEnable()
{
    if (!isRewardScene) return;
    try
    {
        int last = PlayerPrefs.GetInt("LastRunScore", 0);
        if (finalScoreTMP) finalScoreTMP.text = $"{GetStageClearFixedText_Local("final_score_prefix")}{last:N0}";
    }
    catch {}
}
private void ResetEnemyProgressionSafe()
{
    try
    {
        // 互換キー／新キーともに 0 / 空文字 に統一
        PlayerPrefs.SetInt   ("CurrentEnemyIndex", 0);
        PlayerPrefs.SetString("CurrentEnemyName",  "");
        PlayerPrefs.SetInt   ("PF_CurrentEnemyIndex", 0);
        PlayerPrefs.SetString("PF_CurrentEnemyName", "");

        // ラン持ち越し系の掃除
        PlayerPrefs.DeleteKey("EnemiesDefeated");
        PlayerPrefs.DeleteKey("RunCleared");
        PlayerPrefs.DeleteKey("Run_PlayerHP");
        PlayerPrefs.DeleteKey("Run_PlayerMP");

        // 新規Run扱いに戻すので、Run中の成長値も必ず破棄
        PlayerPrefs.DeleteKey("Run_HPBonus");
        PlayerPrefs.DeleteKey("Run_MPBonus");
        PlayerPrefs.DeleteKey("Run_SkillCastsBonus");

        // ★追加：強化画面の「購入のたびに値上げ」カウンタは Run 終了で必ずリセット
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_Buy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_RerollBuy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_Destroy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_RerollDestroy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HpUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_MpUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_CastUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HealHp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HealMp");

        // 次回開始は必ず全快
        PlayerPrefs.SetInt("PF_PendingFullHeal", 1);

        // “新規ラン扱い”を明示（RunシーンAwakeの最終防衛線があればそちらも起動）
        PlayerPrefs.SetInt("PF_ResetRunOnLoad", 1);

        // ★追加：ローグライト用 Runフラグ＆カウンタも確実にリセット
        PlayerPrefs.SetInt("Run_StartedFlagV1", 0);
        PlayerPrefs.SetInt("Run_DefeatedEnemyCount", 0);
        PlayerPrefs.SetInt("Run_LastCountedEnemyIndex", -1);
        PlayerPrefs.SetInt("LastGrantedOmamoriIdV1", 0);
        // ★追加：中断データ（自動セーブ含む）は新規Run開始時に必ず破棄
        PlayerPrefs.DeleteKey("Run_HasSuspend");
        PlayerPrefs.DeleteKey("Run_SuspendJSON");
        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");

        // ★ミッション状態リセット
        try { MissionSystem.ResetForNewRun(); MissionSystem.ClearRunSeed(); } catch { }

        PlayerPrefs.Save();

    }
    catch {}

    // 進行常駐の同期元（会話→対局の逆上書き対策）：あれば 0 に強制
    try { ProgressionFlowController.ForceResetToFirstEnemy(); } catch {}

    // GameManager/PlayerData 側の静的プロパティが存在する場合も 0 固定（なければ握り潰し）
    try { GameManager.SetCurrentEnemyIndex(0); } catch {}
    try { GameManager.SetLoopCount(0); } catch {}
    try {
        var t = typeof(PlayerData);
        var p1 = t.GetProperty("CurrentEnemyIndex", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if (p1 != null) p1.SetValue(null, 0, null);
        var p2 = t.GetProperty("CurrentEnemy", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if (p2 != null) p2.SetValue(null, 0, null);
    } catch {}
}
public static void ResetEnemyProgressionNow()
{
    try {
        // 直接 PlayerPrefs と常駐進行を初期化
        PlayerPrefs.SetInt   ("CurrentEnemyIndex", 0);
        PlayerPrefs.SetString("CurrentEnemyName",  "");
        PlayerPrefs.SetInt   ("PF_CurrentEnemyIndex", 0);
        PlayerPrefs.SetString("PF_CurrentEnemyName", "");
        PlayerPrefs.DeleteKey("EnemiesDefeated");
        PlayerPrefs.DeleteKey("RunCleared");
        PlayerPrefs.DeleteKey("Run_PlayerHP");

        // 新規Run扱いに戻すので、Run中の成長値も必ず破棄
        PlayerPrefs.DeleteKey("Run_HPBonus");
        PlayerPrefs.DeleteKey("Run_MPBonus");
        PlayerPrefs.DeleteKey("Run_SkillCastsBonus");

        // ★追加：強化画面の「購入のたびに値上げ」カウンタは Run 終了で必ずリセット
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_Buy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_RerollBuy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_Destroy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_RerollDestroy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HpUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_MpUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_CastUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HealHp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HealMp");

        // ★追加：該当役の解放/強化を全SkillSetぶん確実にリセット
        try
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            if (allSets != null)
            {
                foreach (var set in allSets)
                {
                    if (set == null) continue;
                    set.ResetAllTraitYakuProgress();
                }
            }
        }
        catch {}

        // ★追加：前Runの特別牌による役強化表示の残骸も消す
        PlayerPrefs.DeleteKey("PF_LastSpecialTileTraitBonusPairs");

        PlayerPrefs.SetInt("PF_PendingFullHeal", 1);
        PlayerPrefs.SetInt("PF_ResetRunOnLoad", 1);

        // ★追加：ローグライト用 Runフラグ＆カウンタも確実にリセット
        PlayerPrefs.SetInt("Run_StartedFlagV1", 0);
        PlayerPrefs.SetInt("Run_DefeatedEnemyCount", 0);
        PlayerPrefs.SetInt("Run_LastCountedEnemyIndex", -1);
        PlayerPrefs.SetInt("LastGrantedOmamoriIdV1", 0);

        // ★追加：中断データ（自動セーブ含む）は新規Run開始時に必ず破棄
        PlayerPrefs.DeleteKey("Run_HasSuspend");
        PlayerPrefs.DeleteKey("Run_SuspendJSON");
        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");
        PlayerPrefs.Save();

    } catch {}

    try { ProgressionFlowController.ForceResetToFirstEnemy(); } catch {}
    try { GameManager.SetCurrentEnemyIndex(0); } catch {}
    try { GameManager.SetLoopCount(0); } catch {}
    try {
        var t = typeof(PlayerData);
        var p1 = t.GetProperty("CurrentEnemyIndex", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if (p1 != null) p1.SetValue(null, 0, null);
        var p2 = t.GetProperty("CurrentEnemy", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if (p2 != null) p2.SetValue(null, 0, null);
    } catch {}
}

// === 追記ここまで ===
private void OnClickGemResultOk()
{
    if (!_gemPanelShowing) return;

    if (gemResultPanelRoot) gemResultPanelRoot.SetActive(false);
    RestoreAllButtons_AfterGemPanel();
    _gemPanelShowing = false;
}

private void TryProcessPendingGemReward_OnRewardScene()
{
    int pending = 0;
    try { pending = PlayerPrefs.GetInt(PrefKey_PendingGemRoll, 0); } catch { pending = 0; }
    if (pending != 1) return;

    bool isZeus = false;
    try { isZeus = PlayerPrefs.GetInt(PrefKey_PendingGemIsZeus, 0) == 1; } catch { isZeus = false; }

    // 報酬画面で宝石パネルを出すのは「ゼウス撃破後」だけ
    if (!isZeus)
    {
        ClearPendingGemRoll();
        return;
    }

    string enemyName = "";
    try { enemyName = PlayerPrefs.GetString(PrefKey_PendingGemEnemyName, ""); } catch { enemyName = ""; }

    // ③ゼウスは初回でなくても確定で1個
    try { SpecialTileSystem.AddGems(1); } catch { }

    ClearPendingGemRoll();

    ShowGemResultPanel(enemyName, 1);
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
private void ShowGemResultPanel(string enemyName, int gained)
{
    if (!gemResultPanelRoot || !gemResultOkButton) return;

    DisableAllButtons_ForGemPanel();

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
private static string BuildGemResultText_Local(string enemyName, int gained)
{
    string gainText = GetStageClearFixedText_Local("gem_gain_prefix")
                    + gained.ToString()
                    + GetStageClearFixedText_Local("gem_gain_suffix");

    if (!string.IsNullOrEmpty(enemyName))
        return enemyName + GetStageClearFixedText_Local("enemy_defeat_suffix") + " \n" + gainText;

    return gainText;
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
private void PlayRewardSE(AudioClip clip)
{
    if (rewardSESource != null && clip != null)
    {
        try { rewardSESource.PlayOneShot(clip); } catch { }
    }
}

private void PlayOmamoriRevealSE_ByRarity(string rarityRaw)
{
    string key = NormalizeRarityKey_Local(rarityRaw);

    AudioClip clip = null;
    switch (key)
    {
        case "Legendary": clip = omamoriRevealSE_Legendary; break;
        case "Epic":      clip = omamoriRevealSE_Epic;      break;
        case "Rare":      clip = omamoriRevealSE_Rare;      break;
        case "Common":    clip = omamoriRevealSE_Common;    break;
        case "Normal":    clip = omamoriRevealSE_Normal;    break;
        default:          clip = omamoriRevealSE_Normal;    break; // 不明時はNormal扱い
    }

    PlayRewardSE(clip);
}
}
