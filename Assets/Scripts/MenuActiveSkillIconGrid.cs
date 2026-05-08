using System.Linq;
using System.Collections.Generic;   // ★ 追加
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuActiveSkillIconGrid : MonoBehaviour
{
    [Header("Where to place icon-only buttons")]
[SerializeField] private RectTransform gridRoot;              // （任意）レイアウト用。なくてもOK
[SerializeField] private GridLayoutGroup grid;                // （任意）
[SerializeField] private Button[] wiredButtons;               // ★ 追加：UIで手動割当するボタン配列
[SerializeField] private Color selectedTint = new Color(1,1,1,0.25f);


    [Header("Always-visible description area")]
    [SerializeField] private TextMeshProUGUI descriptionTMP;      // ★ 常時表示の説明欄

    [Header("Data")]
    [SerializeField] private string resourcesFolder = "SkillSets";
    private const string PrefKeySet = "EquippedSkillSetId";
    private SkillSetAsset _set;

    private string[] _skillNames = new string[0];
    private int _selectedIndex = -1;
private Dictionary<string, SkillSetAsset.SkillEntry> _entryMap = new Dictionary<string, SkillSetAsset.SkillEntry>();
private Dictionary<string, SkillSetAsset> _setBySkillName = new Dictionary<string, SkillSetAsset>();

void Awake()
{
    // gridRoot / grid は任意。割り当てがあればレイアウト初期化、なければ何もしない
    if (gridRoot)
    {
        if (!grid) grid = gridRoot.GetComponent<GridLayoutGroup>() ?? gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.spacing = new Vector2(8, 8);
        grid.childAlignment = TextAnchor.UpperLeft;
    }
}


    void Start()
    {
        // 現在選択中の SkillSet を読み出し
        var setId = PlayerPrefs.GetString(PrefKeySet, "");
        if (!string.IsNullOrEmpty(setId))
        {
            var all = Resources.LoadAll<SkillSetAsset>(resourcesFolder) ?? new SkillSetAsset[0];
            _set = all.FirstOrDefault(s => s && s.id == setId);
        }
        if (!_set)
        {
            // 予備: 先頭を採用
            _set = Resources.LoadAll<SkillSetAsset>(resourcesFolder)?.FirstOrDefault(s => s) ?? null;
        }
WireButtons();

        // 以前装備していた ActiveSkill をハイライト＆説明反映
        var saved = PlayerPrefs.GetString(SkillPrefs.KeyEquippedActiveSkill, "");
        var idx = System.Array.FindIndex(_skillNames, n => n == saved);
        if (idx < 0 && _skillNames.Length > 0) idx = 0;
        SelectIndex(idx);
    }

    void ClearGrid()
    {
        for (int i = gridRoot.childCount - 1; i >= 0; i--) Destroy(gridRoot.GetChild(i).gameObject);
    }
void WireButtons()
{
    // ★ 全 SkillSet.asset を合算してから有無を判定する
    var allSets = Resources.LoadAll<SkillSetAsset>(resourcesFolder) ?? new SkillSetAsset[0];
    // set と entry のペアで集める（どのセットに属しているかを保持）
    var pairs = allSets
        .Where(s => s && s.activeSkills != null)
        .SelectMany(s => s.activeSkills
            .Where(e => e != null && !string.IsNullOrEmpty(e.activeSkillName))
            .Select(e => (set: s, entry: e)))
        .ToList();

    if (pairs.Count == 0)
    {
        _skillNames = System.Array.Empty<string>();
        if (descriptionTMP) descriptionTMP.text = "スキル未定義";
        return;
    }

    _skillNames = pairs.Select(p => p.entry.activeSkillName).Distinct().ToArray();

    // 名前→エントリ（代表1件）
    _entryMap = pairs
        .GroupBy(p => p.entry.activeSkillName)
        .ToDictionary(g => g.Key, g => g.First().entry);

    // 名前→所属セット（代表1件）…撃/瞬/癒の該当役を引くために使う
    _setBySkillName = pairs
        .GroupBy(p => p.entry.activeSkillName)
        .ToDictionary(g => g.Key, g => g.First().set);

    // ★ UIに手置きしたボタンへ配線（画像は触らない）
    if (wiredButtons == null || wiredButtons.Length == 0) return;

int n = Mathf.Min(wiredButtons.Length, _skillNames.Length); // ★ スキル数まで配線
for (int i = 0; i < n; i++)
{
    var b = wiredButtons[i];
    if (!b) continue;

    int cap = i;
    b.onClick.RemoveAllListeners();
    b.onClick.AddListener(() => SelectIndex(cap));
    SetSelectedVisual(b, false);
}

// 余りは誤クリックで混乱しないように無効化
for (int i = n; i < wiredButtons.Length; i++)
{
    if (wiredButtons[i]) wiredButtons[i].interactable = false;
}
// 仕上げ（保険）：全枠の Raycast を切る
foreach (var b in wiredButtons)
{
    if (!b) continue;
    var fr = b.transform.Find(selectedFrameChildName)?.GetComponent<Image>();
    if (fr) fr.raycastTarget = false;
}

}


    Button CreateIconButton(Transform parent)
    {
        var go = new GameObject("IconButton", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(96, 96);
        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.fadeDuration = 0.05f;
        btn.colors = colors;
        return btn;
    }

[SerializeField] private string selectedFrameChildName = "SelectedFrame"; // 任意の子Image名

void SetSelectedVisual(Button b, bool selected)
{
    if (!b) return;

    // ● 枠ImageがあればそれをON/OFF（推奨）
var frame = b.transform.Find(selectedFrameChildName)?.GetComponent<Image>();
if (frame)
{
    frame.enabled = selected;
    frame.raycastTarget = false;   // ★ クリックを塞がない
}


    // ● ない場合はOutlineで代用（自動付与）
    if (!frame)
    {
        var ol = b.GetComponent<Outline>() ?? b.gameObject.AddComponent<Outline>();
        ol.enabled = selected;
        ol.effectColor = new Color(1f, 1f, 1f, 0.95f);
        ol.effectDistance = new Vector2(3, -3);
    }

    // ※ 画像色は一切いじらない（アイコン“何も表示しない”を壊さないため）
}
void SelectIndex(int i)
{
    if (_skillNames == null || _skillNames.Length == 0) return;
    if (i < 0 || i >= _skillNames.Length) return;

    _selectedIndex = i;

    if (wiredButtons != null)
    {
        for (int k = 0; k < wiredButtons.Length; k++)
        {
            var b = wiredButtons[k];
            SetSelectedVisual(b, k == _selectedIndex);
        }
    }

    var skillName = _skillNames[_selectedIndex];
    SkillPrefs.Equip(skillName);

    if (descriptionTMP)
        descriptionTMP.text = BuildSkillDescriptionForUI(skillName);
}
string BuildSkillDescriptionForUI(string activeSkillEnumName)
{
    SkillSetAsset.SkillEntry entry = null;
    _entryMap?.TryGetValue(activeSkillEnumName, out entry);

    if (entry != null)
    {
        string localized = entry.GetLocalizedDescription();
        if (!string.IsNullOrEmpty(localized))
            return localized;
    }

    if (_setBySkillName != null &&
        _setBySkillName.TryGetValue(activeSkillEnumName, out var hostSet) &&
        hostSet != null)
    {
        string localized = hostSet.GetLocalizedDescription();
        if (!string.IsNullOrEmpty(localized))
            return localized;
    }

    return GetDefaultDescLocalized(activeSkillEnumName);
}
string GetDefaultDescLocalized(string enumName)
{
    var lm = LocalizationManager.Instance;
    var lang = (lm != null) ? lm.CurrentLanguage : LocalizationManager.Language.Japanese;

    switch (lang)
    {
        case LocalizationManager.Language.English:
            switch (enumName)
            {
                case "RandomMan": return "Transforms the selected tile into a random tile of the suit you have the most of in your hand (excluding the selected tile). Ties are broken in the order Characters > Dots > Bamboo.";
                case "RandomSou": return "Transforms one tile in your hand into a Bamboo tile.";
                case "RandomPin": return "Transforms one tile in your hand into a Dots tile.";
                case "RandomHonor": return "Transforms one tile in your hand into an honor tile.";
                case "RandomYaochu": return "Transforms one tile in your hand into a terminal or honor tile (1 / 9 / honor).";
                case "RandomChunchan": return "Transforms one tile in your hand into a simple tile (2 to 8).";
                case "DuplicateAndDiscardOther": return "Creates one additional copy of the selected tile and discards another tile.";
                case "EnhanceHand": return "Transforms the selected tile into a 5 of the same suit and enhances it.";
                case "AddDoraIndicator": return "Adds one Dora indicator.";
                case "NullifyEnemyDiscardEffectsOnce": return "Negates the enemy's discard effect once.";
                case "ForceDrawSelectedNextTurn": return "Draws the selected tile at the start of the next turn.";
                default: return "";
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch (enumName)
            {
                case "RandomMan": return "将选中的牌变为手牌中数量最多花色（不含该牌）的随机数牌。数量相同时顺序为万＞筒＞索。";
                case "RandomSou": return "将手牌中的1张牌变为索子。";
                case "RandomPin": return "将手牌中的1张牌变为筒子。";
                case "RandomHonor": return "将手牌中的1张牌变为字牌。";
                case "RandomYaochu": return "将手牌中的1张牌变为幺九牌（1 / 9 / 字牌）。";
                case "RandomChunchan": return "将手牌中的1张牌变为中张牌（2～8）。";
                case "DuplicateAndDiscardOther": return "复制选中的牌1张，并弃掉另一张牌。";
                case "EnhanceHand": return "将选中的牌变为同花色的5并强化。";
                case "AddDoraIndicator": return "追加1张宝牌指示牌。";
                case "NullifyEnemyDiscardEffectsOnce": return "使下一次敌人的弃牌效果无效一次。";
                case "ForceDrawSelectedNextTurn": return "在下一回合开始时摸到选中的牌。";
                default: return "";
            }

        case LocalizationManager.Language.Japanese:
        default:
            switch (enumName)
            {
                case "RandomMan": return "選んだ牌を、（選んだ牌を除く）自分の手牌で最も多い色（萬/筒/索）のランダムな牌に変える（同数時は 萬＞筒＞索）。";
                case "RandomSou": return "手牌の1枚を索子に変える。";
                case "RandomPin": return "手牌の1枚を筒子に変える。";
                case "RandomHonor": return "手牌の1枚を字牌に変える。";
                case "RandomYaochu": return "手牌の1枚を么九牌（1/9/字）に変える。";
                case "RandomChunchan": return "手牌の1枚を中張牌（2〜8）に変える。";
                case "DuplicateAndDiscardOther": return "選んだ牌をもう1枚生成し、他の1枚を捨てる。";
                case "EnhanceHand": return "選んだ牌を同スートの5に変換して強化。";
                case "AddDoraIndicator": return "ドラ表示牌を1枚追加する。";
                case "NullifyEnemyDiscardEffectsOnce": return "次の敵の捨て牌効果を一度だけ無効化。";
                case "ForceDrawSelectedNextTurn": return "選んだ牌を次ターンの最初にツモる。";
                default: return "";
            }
    }
}
    // セットが外部で変更された場合に呼べる
public void RefreshFromSetId(string setId)
{
    var all = Resources.LoadAll<SkillSetAsset>(resourcesFolder) ?? new SkillSetAsset[0];
    _set = all.FirstOrDefault(s => s && s.id == setId);
    WireButtons();       // ★ 生成ではなく配線
    SelectIndex(0);
}

}
