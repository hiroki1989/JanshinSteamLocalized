using System.Linq;   
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuSkillListUI : MonoBehaviour
{
    [Header("Data")]
[SerializeField] private SkillSetAsset skillSet;      // 手動指定があるなら優先
[SerializeField] private string resourcesFolder = "SkillSets"; // 自動ロード用
private const string PrefKeySet = "EquippedSkillSetId";
// 追加：Resources/SkillSets から拾った “すべてのセット” を保持
private SkillSetAsset[] _allSets = System.Array.Empty<SkillSetAsset>();
    [TextArea] [SerializeField] private string defaultSkillDescription = "説明未設定のスキルです。";

    [System.Serializable]
    public class SkillDesc
    {
        public string activeSkillName;   // 例: "RandomMan"
        [TextArea] public string description;
    }
    [SerializeField] private SkillDesc[] skillDescriptions;

    [Header("UI")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button itemPrefab;
    [Tooltip("右側の説明欄は使わないので未設定でOK。割り当て済みならStartで自動的に非表示にします。")]
    [SerializeField] private TextMeshProUGUI detailTMP;

    [Header("Style")]
    [SerializeField] private Color rowColor = new Color(1,1,1,0.15f); // 行の背景色（常に一定）
    [SerializeField] private string selectedFrameChildName = "SelectedFrame"; // プレハブに枠Imageがある場合

    private int _selectedIndex = -1;

    void Reset()
    {
        scroll = GetComponentInChildren<ScrollRect>(true);
        if (scroll && !content) content = scroll.content;
    }

void Start()
{
    // 右側の説明欄は使わない
    if (detailTMP) detailTMP.gameObject.SetActive(false);

    // ★ 装備中セットを必ずロード（Inspectorの手動指定は無視）
  // ★ SkillSet_XXXX.asset を全部ロードして “一覧” として使う
_allSets = Resources.LoadAll<SkillSetAsset>(resourcesFolder)
                    ?.Where(s => s != null).ToArray()
           ?? System.Array.Empty<SkillSetAsset>();

if (_allSets.Length == 0)
{
    Debug.LogError("[MenuSkillListUI] No SkillSet found under Resources/SkillSets.");
    return;
}

if (!ValidateRefs()) return;
BuildList();

// 既存選択の復元（EquippedSkillSetId に一致する行を選択）
// 既存選択の復元（EquippedSkillSetId に一致する行を選択）
var equippedId = PlayerPrefs.GetString(PrefKeySet, "");
int idx = Mathf.Max(0, System.Array.FindIndex(_allSets, s => s && s.id == equippedId));
SelectIndex(idx);

// フォールバック（万一選択されていなければ先頭）
if (_selectedIndex < 0) SelectIndex(0);
}
bool ValidateRefs()
{
    if (!content)    { Debug.LogError("[MenuSkillListUI] content 未割り当て"); return false; }
    if (!itemPrefab) { Debug.LogError("[MenuSkillListUI] itemPrefab 未割り当て"); return false; }
    return true;
}

void BuildList()
{
    for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

    for (int i = 0; i < _allSets.Length; i++)
    {
        var set = _allSets[i];
        var btn = Instantiate(itemPrefab, content);
        btn.name = "SkillSet_" + (set ? set.id : "(null)");

        var bg = btn.GetComponent<Image>();
        if (bg) bg.color = rowColor;

        var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label)
        {
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 36;
            label.text = BuildRowText(set);   // ← セットを渡す
        }

        var frame = btn.transform.Find(selectedFrameChildName)?.GetComponent<Image>();
        if (frame) frame.enabled = false;

        var outline = btn.GetComponent<Outline>();
        if (!outline) outline = btn.gameObject.AddComponent<Outline>();
        outline.enabled = false;
        outline.effectColor = new Color(1f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(3, -3);

        int cap = i;
        btn.onClick.AddListener(() => SelectIndex(cap));
    }

    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
}


    int IndexOfSkill(string activeSkillName)
    {
        for (int i = 0; i < skillSet.activeSkills.Count; i++)
            if (string.Equals(skillSet.activeSkills[i].activeSkillName, activeSkillName, System.StringComparison.Ordinal))
                return i;
        return -1;
    }
void SelectIndex(int i)
{
    if (i < 0 || i >= _allSets.Length) return;
    _selectedIndex = i;

    // 見た目更新
    for (int k = 0; k < content.childCount; k++)
    {
        var btn = content.GetChild(k).GetComponent<Button>();
        if (!btn) continue;
        bool sel = (k == _selectedIndex);
        var frame = btn.transform.Find(selectedFrameChildName)?.GetComponent<Image>();
        if (frame) frame.enabled = sel;
        var outline = btn.GetComponent<Outline>();
        if (outline) outline.enabled = sel;
    }

    var set = _allSets[_selectedIndex];
    if (set)
    {
        PlayerPrefs.SetString(PrefKeySet, set.id);
        PlayerPrefs.Save();
    }
}
string BuildRowText(SkillSetAsset set)
{
    var sb = new StringBuilder();
    if (!set)
    {
        sb.AppendLine("<b>(null)</b>");
        sb.AppendLine("データが見つかりません。");
        return sb.ToString();
    }

    string dispName = set.GetLocalizedDisplayName();
    string desc = set.GetLocalizedDescription();

    sb.AppendLine($"<b>{(string.IsNullOrEmpty(dispName) ? set.id : dispName)}</b>");

    if (!string.IsNullOrEmpty(desc))
    {
        sb.AppendLine(desc);
    }

    return sb.ToString();
}
}
