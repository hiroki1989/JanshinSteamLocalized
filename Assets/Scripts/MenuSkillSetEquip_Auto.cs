
using UnityEngine;
using TMPro;
using System.Linq;

/// <summary>
/// Drop-in replacement for MenuSkillSetEquip:
/// - Works even if "All Sets" is empty by auto-loading SkillSetAsset from Resources/SkillSets
/// - Still supports manual assignment (manual list takes precedence)
/// - Shows description and saves PlayerPrefs("EquippedSkillSetId") on change
/// Usage:
/// 1) Put your SkillSet_*.asset under Assets/Resources/SkillSets/
/// 2) Attach this to your SkillSetSelectPanel and wire Dropdown / DescTMP
/// 3) (Optional) Fill AllSets to override auto-load order
/// </summary>
public class MenuSkillSetEquip_Auto : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private TextMeshProUGUI descTMP;

    [Header("Manual list (optional). If empty, auto-loads from Resources/SkillSets")]
    [SerializeField] private SkillSetAsset[] allSets;

    [Header("Resources folder to auto-load from (under Assets/Resources)")]
    [SerializeField] private string resourcesFolder = "SkillSets";

    private const string PrefKey = "EquippedSkillSetId";
    private SkillSetAsset[] _activeList = new SkillSetAsset[0];

    private void Awake()
    {
        if (!dropdown)
        {
            Debug.LogError("[MenuSkillSetEquip_Auto] TMP_Dropdown is not assigned.");
        }
    }

    void Start()
    {
        // 1) Resolve list
        if (allSets != null && allSets.Length > 0 && allSets.Any(s => s))
        {
            _activeList = allSets.Where(s => s != null).ToArray();
        }
        else
        {
            // Auto-load from Resources/SkillSets
            var loaded = Resources.LoadAll<SkillSetAsset>(resourcesFolder);
            _activeList = loaded != null ? loaded.Where(s => s != null).ToArray() : new SkillSetAsset[0];
            if (_activeList.Length == 0)
            {
                Debug.LogWarning($"[MenuSkillSetEquip_Auto] No SkillSetAsset found. Place assets under Resources/{resourcesFolder} or assign AllSets.");
            }
        }

        // 2) Populate dropdown
        dropdown.ClearOptions();
        var opts = _activeList.Select(s => s ? s.GetLocalizedDisplayName() : "(null)").ToList();
        dropdown.AddOptions(opts);

        // 3) Restore saved selection
        var saved = PlayerPrefs.GetString(PrefKey, "");
        int idx = Mathf.Max(0, System.Array.FindIndex(_activeList, s => s && s.id == saved));
        dropdown.SetValueWithoutNotify(idx);
        UpdateDesc(idx);
if (_activeList.Length > 0 && _activeList[idx])
{
    PlayerPrefs.SetString(PrefKey, _activeList[idx].id);
    PlayerPrefs.Save();
}
        // 4) Bind change
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(i =>
        {
            if (i < 0 || i >= _activeList.Length) return;
            var set = _activeList[i];
            if (set)
            {
                PlayerPrefs.SetString(PrefKey, set.id);
                PlayerPrefs.Save();
                UpdateDesc(i);
                Debug.Log($"[MenuSkillSetEquip_Auto] Equipped: {set.GetLocalizedDisplayName()} (id={set.id})");
            }
        });
    }

    private void UpdateDesc(int i)
    {
        if (!descTMP) return;
        var s = (i >= 0 && i < _activeList.Length) ? _activeList[i] : null;
        descTMP.text = s ? s.GetLocalizedDescription() : "";
    }
}
