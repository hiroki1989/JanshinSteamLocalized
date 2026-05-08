using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events; // ★追加：UnityAction

public sealed class UpgradeSceneMenu : MonoBehaviour
{
    [Header("Menu Root (3択)")]
    [SerializeField] private GameObject menuRoot;

    [Header("Panels")]
    [SerializeField] private GameObject ofudaStoreRoot;   // UpgradeOfudaStore を含む親
    [SerializeField] private UpgradeManager upgradeManager; // ステータス／デッキ共用

[Header("Menu Buttons")]
[SerializeField] private Button ofudaButton;
[SerializeField] private Button statusButton;
[SerializeField] private Button deckButton;
[SerializeField] private Button traitYakuButton;

[Header("Back Buttons")]
[SerializeField] private Button backFromStatusButton;
[SerializeField] private Button backFromDeckButton;
[SerializeField] private Button backFromTraitButton;
[SerializeField] private Button backFromOfudaButton;
[Header("Gem Result Panel (宝石獲得結果)")]
[SerializeField] private GameObject gemResultPanelRoot;
[SerializeField] private TMPro.TMP_Text gemResultTMP;
[SerializeField] private Button gemResultOkButton;

[Header("Unique Omamori Result Panel (ユニークお守り獲得結果)")]
[SerializeField] private GameObject uniqueOmamoriResultPanelRoot;
[SerializeField] private TMPro.TMP_Text uniqueOmamoriTitleTMP;
[SerializeField] private TMPro.TMP_Text uniqueOmamoriDescTMP;
[SerializeField] private Button uniqueOmamoriOkButton;
[Header("SE (Upgrade Result)")]
[SerializeField] private AudioSource upgradeResultSESource;   // 結果系SEを鳴らすAudioSource
[SerializeField] private AudioClip gemGetSE;                  // 宝石獲得SE
[SerializeField] private AudioClip uniqueOmamoriGetSE;        // 神器獲得SE
private int _pendingUniqueOmamoriId = 0;
private PlayerData.UniqueOmamoriEffectKind _pendingUniqueOmamoriKind = PlayerData.UniqueOmamoriEffectKind.None;
private string _pendingUniqueEnemyName = "";
private bool _uniquePanelShowing = false;
private readonly System.Collections.Generic.Dictionary<Button, bool> _gemPrevInteractable
    = new System.Collections.Generic.Dictionary<Button, bool>();
private bool _gemPanelShowing = false;

private void Awake()
{
    // 参照が未設定でも可能な限り自動探索（後方互換）
    if (!menuRoot)        menuRoot = gameObject;
    if (!ofudaStoreRoot)  ofudaStoreRoot = FindObjectOfType<UpgradeOfudaStore>(true)?.gameObject;
    if (!upgradeManager)  upgradeManager = FindObjectOfType<UpgradeManager>(true);

    // 最初はメニューのみ表示
    SafeSetActive(ofudaStoreRoot, false);
    SafeSetActive(upgradeManager ? upgradeManager.gameObject : null, false);
    SafeSetActive(menuRoot, true);

    // ★重要：Inspectorに残っているOnClick（次の敵へ等）を実行時に確実に無効化して、こちらの処理だけにする
    WireExclusive(ofudaButton,     OnChooseOfuda);
    WireExclusive(statusButton,    OnChooseStatus);
    WireExclusive(deckButton,      OnChooseDeck);
    WireExclusive(traitYakuButton, OnChooseTraitYaku);

    WireExclusive(backFromStatusButton, OnBackToMenu);
    WireExclusive(backFromDeckButton,   OnBackToMenu);
    WireExclusive(backFromTraitButton,  OnBackToMenu);
    WireExclusive(backFromOfudaButton,  OnBackToMenu);

    // ★追加：SerializeField に割り当たっていない「戻る」ボタンが残っていても救う
    AutoWireAllBackButtons();
}
private void OnClickGemResultOk()
{
    if (!_gemPanelShowing) return;

    if (gemResultPanelRoot) gemResultPanelRoot.SetActive(false);
    _gemPanelShowing = false;

    if (TryShowPendingUniqueOmamoriResult())
    {
        return;
    }

    RestoreAllButtons_AfterGemPanel();
}
private void PlayUpgradeResultSE(AudioClip clip)
{
    if (upgradeResultSESource != null && clip != null)
    {
        try { upgradeResultSESource.PlayOneShot(clip); } catch { }
    }
}

private void OnClickUniqueOmamoriOk()
{
    if (!_uniquePanelShowing) return;

    if (uniqueOmamoriResultPanelRoot) uniqueOmamoriResultPanelRoot.SetActive(false);
    _uniquePanelShowing = false;
    _pendingUniqueOmamoriId = 0;
    _pendingUniqueOmamoriKind = PlayerData.UniqueOmamoriEffectKind.None;
    _pendingUniqueEnemyName = "";

    // まだ宝石パネルが残っているなら、全部のボタンは戻さず、
    // 宝石パネルのOKだけ再度押せるようにする
    if (_gemPanelShowing)
    {
        if (gemResultOkButton) gemResultOkButton.interactable = true;
        return;
    }

    RestoreAllButtons_AfterGemPanel();
}
private void ShowUniqueOmamoriResultPanel(int omamoriId, string enemyName)
{
    if (!uniqueOmamoriResultPanelRoot || !uniqueOmamoriOkButton) return;
    if (omamoriId <= 0) return;

    if (_gemPrevInteractable.Count == 0)
    {
        DisableAllButtons_ForGemPanel();
    }

    if (AudioManager.Instance)
    {
        AudioManager.Instance.PlayUniqueOmamoriGetSE();
    }

    string title = "";
    string desc = "";

    try { title = PlayerData.GetOmamoriName(omamoriId); } catch { title = ""; }
    try { desc  = PlayerData.GetOmamoriDesc(omamoriId); } catch { desc = ""; }

    if (uniqueOmamoriTitleTMP) uniqueOmamoriTitleTMP.text = title;
    if (uniqueOmamoriDescTMP)  uniqueOmamoriDescTMP.text  = desc;

    uniqueOmamoriResultPanelRoot.SetActive(true);
    uniqueOmamoriResultPanelRoot.transform.SetAsLastSibling();
    _uniquePanelShowing = true;

    // 同時表示中はユニークお守りを先に処理させる
    if (gemResultOkButton && _gemPanelShowing)
        gemResultOkButton.interactable = false;

    uniqueOmamoriOkButton.onClick.RemoveListener(OnClickUniqueOmamoriOk);
    uniqueOmamoriOkButton.onClick.AddListener(OnClickUniqueOmamoriOk);
    uniqueOmamoriOkButton.interactable = true;
}
private static string GetUpgradeSceneMenuFixedText_Local(string key)
{
    return LocalizationManager.Fixed(key);
}

private static string BuildGemResultText_UpgradeSceneMenu_Local(string enemyName, int gained)
{
    string rewardText = GetUpgradeSceneMenuFixedText_Local("gem_gain_prefix")
                      + gained.ToString()
                      + GetUpgradeSceneMenuFixedText_Local("gem_gain_middle");

    if (!string.IsNullOrEmpty(enemyName))
        return enemyName + GetUpgradeSceneMenuFixedText_Local("enemy_defeat_suffix_emphatic") + "\n" + rewardText;

    return rewardText;
}

private void ShowGemResultPanel(string enemyName, int gained)
{
    if (!gemResultPanelRoot || !gemResultOkButton) return;

    if (_gemPrevInteractable.Count == 0)
    {
        DisableAllButtons_ForGemPanel();
    }

    // SE：宝石獲得 ※AudioManagerに集約
    if (AudioManager.Instance)
    {
        AudioManager.Instance.PlayGemGetSE();
    }

    if (gemResultTMP)
    {
        gemResultTMP.text = BuildGemResultText_UpgradeSceneMenu_Local(enemyName, gained);
    }

    gemResultPanelRoot.SetActive(true);
    gemResultPanelRoot.transform.SetAsLastSibling();
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

private static void WireExclusive(Button btn, UnityAction action)
{
    if (!btn) return;

    // ★ここが肝：既存の全リスナー（Inspectorの永続リスナー含む）を一旦消す
    btn.onClick.RemoveAllListeners();

    // 念のため null 防止
    if (action != null)
        btn.onClick.AddListener(action);
}
private void AutoWireAllBackButtons()
{
    // menuRoot 配下（メニュー画面内）に戻るは普通いないが、念のため対象外にするならここで除外できる
    AutoWireBackButtonsUnder(ofudaStoreRoot);
    AutoWireBackButtonsUnder(upgradeManager ? upgradeManager.gameObject : null);
}

private void AutoWireBackButtonsUnder(GameObject root)
{
    if (!root) return;

    // root 配下の全 Button を拾う（非アクティブ含む）
    var buttons = root.GetComponentsInChildren<Button>(true);
    if (buttons == null) return;

    foreach (var b in buttons)
    {
        if (!b) continue;

        // 「戻る」ボタンだけを対象にする（買う/リロール等のボタンを巻き込まない）
        // ※あなたのシーン命名に合わせてキーワードは増やしてOK
        var n = (b.name ?? "").ToLowerInvariant();
        bool isBack =
            n.Contains("back") ||
            n.Contains("return") ||
            n.Contains("戻") ||
            n.Contains("modoru");

        if (!isBack) continue;

        // Inspector の永続リスナー含めて全削除 → OnBackToMenu のみに統一
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(OnBackToMenu);
    }
}

private void OnChooseTraitYaku()
{
    PlayerPrefs.SetString("UpgradeSectionMode", "TRAIT");
    PlayerPrefs.Save();

    // 既存3パネルの表示を落とす（あなたのUI構成に合わせて）
    SafeSetActive(menuRoot, false);
    SafeSetActive(ofudaStoreRoot, false);

    if (upgradeManager)
    {
        SafeSetActive(upgradeManager.gameObject, true);
        upgradeManager.ApplySectionMode(UpgradeManager.UpgradeSectionMode.TraitOnly);
    }
}

    private void OnChooseOfuda()
    {
        PlayerPrefs.SetString("UpgradeSectionMode", "ALL"); // OfudaはUpgradeManagerを使わないので保険的にALLへ
        PlayerPrefs.Save();

        SafeSetActive(menuRoot, false);
        SafeSetActive(upgradeManager ? upgradeManager.gameObject : null, false);
        SafeSetActive(ofudaStoreRoot, true);
    }

    private void OnChooseStatus()
    {
        PlayerPrefs.SetString("UpgradeSectionMode", "STATUS");
        PlayerPrefs.Save();

        SafeSetActive(menuRoot, false);
        SafeSetActive(ofudaStoreRoot, false);

        if (upgradeManager)
        {
            SafeSetActive(upgradeManager.gameObject, true);
            upgradeManager.ApplySectionMode(UpgradeManager.UpgradeSectionMode.StatusOnly);
        }
    }

    private void OnChooseDeck()
    {
        PlayerPrefs.SetString("UpgradeSectionMode", "DECK");
        PlayerPrefs.Save();

        SafeSetActive(menuRoot, false);
        SafeSetActive(ofudaStoreRoot, false);

        if (upgradeManager)
        {
            SafeSetActive(upgradeManager.gameObject, true);
            upgradeManager.ApplySectionMode(UpgradeManager.UpgradeSectionMode.DeckOnly);
        }
    }

    private static void SafeSetActive(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }
private void OnBackToMenu()
{
    // ★追加：UpgradeManager が握っている「外部Root参照」も含めて確実に閉じる
    if (upgradeManager)
    {
        upgradeManager.ForceCloseAllSectionRoots();
    }

    // “元の強化画面”＝メニューへ戻す
    SafeSetActive(ofudaStoreRoot, false);
    SafeSetActive(upgradeManager ? upgradeManager.gameObject : null, false);
    SafeSetActive(menuRoot, true);

    // 後方互換：起動時の復元用キーを “ALL” に戻しておく（任意だが事故が減る）
    PlayerPrefs.SetString("UpgradeSectionMode", "ALL");
    PlayerPrefs.Save();
}
private void Start()
{
    // UpgradeManager は最初メニュー構成の都合で非表示にしているため、
    // 宝石抽選はここ(UpgradeSceneMenu)で必ず消化して付与する。

    _pendingUniqueOmamoriId = 0;
    _pendingUniqueOmamoriKind = PlayerData.UniqueOmamoriEffectKind.None;
    _pendingUniqueEnemyName = "";

    try
    {
        int pend = PlayerPrefs.GetInt("UniqueOmamori_PendingRoll", 0);
        if (pend != 0)
        {
            _pendingUniqueOmamoriId = PlayerPrefs.GetInt("UniqueOmamori_PendingId", 0);
            _pendingUniqueOmamoriKind = (PlayerData.UniqueOmamoriEffectKind)PlayerPrefs.GetInt("UniqueOmamori_PendingKind", 0);
            _pendingUniqueEnemyName = PlayerPrefs.GetString("UniqueOmamori_PendingEnemyName", "");

            PlayerPrefs.DeleteKey("UniqueOmamori_PendingRoll");
            PlayerPrefs.DeleteKey("UniqueOmamori_PendingId");
            PlayerPrefs.DeleteKey("UniqueOmamori_PendingKind");
            PlayerPrefs.DeleteKey("UniqueOmamori_PendingEnemyName");
            PlayerPrefs.Save();
        }
    }
    catch
    {
        _pendingUniqueOmamoriId = 0;
        _pendingUniqueOmamoriKind = PlayerData.UniqueOmamoriEffectKind.None;
        _pendingUniqueEnemyName = "";
    }

    string enemyName = "";
    try { enemyName = PlayerPrefs.GetString("Gem_PendingEnemyName", ""); } catch { enemyName = ""; }

    int gained = 0;
    try { gained = UpgradeManager.ConsumePendingGemReward_NoUI_OnEnterUpgrade(); } catch { gained = 0; }

    if (gained > 0)
    {
        ShowGemResultPanel(enemyName, gained);
    }

    TryShowPendingUniqueOmamoriResult();
}
private bool TryShowPendingUniqueOmamoriResult()
{
    if (_pendingUniqueOmamoriKind == PlayerData.UniqueOmamoriEffectKind.None)
        return false;

    int grantedId = -1;
    try
    {
        grantedId = PlayerData.GrantUniqueOmamori(_pendingUniqueEnemyName, _pendingUniqueOmamoriKind, 1);
    }
    catch
    {
        grantedId = -1;
    }

    _pendingUniqueOmamoriId = (grantedId > 0) ? grantedId : 0;
    _pendingUniqueOmamoriKind = PlayerData.UniqueOmamoriEffectKind.None;

    if (_pendingUniqueOmamoriId <= 0)
        return false;

    ShowUniqueOmamoriResultPanel(_pendingUniqueOmamoriId, _pendingUniqueEnemyName);
    return true;
}
}
