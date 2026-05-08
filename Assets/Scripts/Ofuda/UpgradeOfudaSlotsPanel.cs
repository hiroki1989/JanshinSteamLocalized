using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UpgradeScene の「所持お札 3枠」表示＆選択＆破棄。
/// ※購入処理側は OfudaRunInventory.TryAdd(...) を呼べば満杯制御が効く。
/// </summary>
public sealed class UpgradeOfudaSlotsPanel : MonoBehaviour
{
    [Header("Slots (Left to Right)")]
    [SerializeField] private Button[] slotButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] slotNameTexts = new TextMeshProUGUI[3];

    [Header("Selected Detail (Optional)")]
    [SerializeField] private TextMeshProUGUI selectedNameTMP;
    [SerializeField] private TextMeshProUGUI selectedDescTMP;

    [Header("Capacity (Optional)")]
    [SerializeField] private TextMeshProUGUI capacityTMP;

    [Header("Actions")]
    [SerializeField] private Button discardButton;

    private int _selectedIndex = -1;
    private Dictionary<string, OfudaDef> _ofudaMap;

    private void Awake()
    {
        if (discardButton) discardButton.onClick.AddListener(OnClickDiscard);

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int idx = i;
            if (slotButtons[idx])
                slotButtons[idx].onClick.AddListener(() => SelectSlot(idx));
        }

        BuildOfudaMapIfNeeded();
        RefreshUI();
    }
private void OnEnable()
{
    BuildOfudaMapIfNeeded();
    LocalizationManager.LanguageChanged += OnLanguageChanged;
    RefreshUI();
}

private void OnDisable()
{
    LocalizationManager.LanguageChanged -= OnLanguageChanged;
}

private void OnLanguageChanged(LocalizationManager.Language language)
{
    _ofudaMap = null;
    BuildOfudaMapIfNeeded();
    RefreshUI();
}
    private void BuildOfudaMapIfNeeded()
    {
        if (_ofudaMap != null) return;

        try
        {
            var cat = OfudaExcelLoader.Load();
            var defs = OfudaCatalog.BuildFromExcel(cat);
            _ofudaMap = defs.ToDictionary(d => d.id, d => d);
        }
        catch
        {
            _ofudaMap = new Dictionary<string, OfudaDef>();
        }
    }

public void RefreshUI()
{
    var list = OfudaRunInventory.LoadList(); // ★最大3枠＆順序保証
    for (int i = 0; i < 3; i++)
    {
        string id = (i < list.Count) ? list[i] : null;

        if (slotNameTexts != null && i < slotNameTexts.Length && slotNameTexts[i])
        {
            if (!string.IsNullOrEmpty(id) && _ofudaMap.TryGetValue(id, out var def))
                slotNameTexts[i].text = ColorizeRarityPrefix(def.displayName, def.rarity);
            else if (!string.IsNullOrEmpty(id))
                slotNameTexts[i].text = id; // フォールバック
            else
                slotNameTexts[i].text = GetEmptySlotText();
        }

        if (slotButtons != null && i < slotButtons.Length && slotButtons[i])
            slotButtons[i].interactable = !string.IsNullOrEmpty(id);
    }

    if (capacityTMP)
        capacityTMP.text = GetCapacityText(list.Count, 3);

    // 選択が無効になったら解除
    if (_selectedIndex >= list.Count) _selectedIndex = -1;

    ApplySelectedDetail(list);

    if (discardButton)
        discardButton.interactable = (_selectedIndex >= 0 && _selectedIndex < list.Count);
}
    private void SelectSlot(int index)
    {
        var list = OfudaRunInventory.LoadList();
        if (index < 0 || index >= list.Count) return;
        _selectedIndex = index;
        ApplySelectedDetail(list);

        if (discardButton)
            discardButton.interactable = true;
    }

    private void ApplySelectedDetail(List<string> list)
    {
        if (!selectedNameTMP && !selectedDescTMP) return;

        if (_selectedIndex < 0 || _selectedIndex >= list.Count)
        {
            if (selectedNameTMP) selectedNameTMP.text = "";
            if (selectedDescTMP) selectedDescTMP.text = "";
            return;
        }

        var id = list[_selectedIndex];
        if (_ofudaMap.TryGetValue(id, out var def))
        {
            if (selectedNameTMP) selectedNameTMP.text = ColorizeRarityPrefix(def.displayName, def.rarity);
            if (selectedDescTMP) selectedDescTMP.text = def.description;
        }
        else
        {
            if (selectedNameTMP) selectedNameTMP.text = id;
            if (selectedDescTMP) selectedDescTMP.text = "";
        }
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

    string hex = ColorUtility.ToHtmlStringRGB(OfudaRarityColors.Get(rarity));
    return $"<color=#{hex}>{prefix}</color>{rest}";
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
private static string GetCapacityText(int current, int max)
{
    if (LocalizationManager.Instance == null)
        return $"{current}/{max}";

    switch (LocalizationManager.Instance.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
            return $"{current}/{max}";

        case LocalizationManager.Language.ChineseSimplified:
            return $"{current}/{max}";

        default:
            return $"{current}/{max}";
    }
}
    private void OnClickDiscard()
    {
        if (_selectedIndex < 0) return;
        if (!OfudaRunInventory.RemoveAt(_selectedIndex)) return;

        // 左詰めされるので選択解除
        _selectedIndex = -1;
        RefreshUI();

        // （任意）RunSceneの所持表示を即時更新したい場合
        try
        {
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm) gm.NotifyOfudaChanged();
        }
        catch { }
    }
}
