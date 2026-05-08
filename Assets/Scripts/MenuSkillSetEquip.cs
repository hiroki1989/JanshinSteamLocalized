using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class MenuSkillSetEquip : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private TextMeshProUGUI descTMP;
    [SerializeField] private SkillSetAsset[] allSets;

    private const string PrefKey = "EquippedSkillSetId";

    void Start()
    {
        if (dropdown == null)
        {
            Debug.LogWarning("[MenuSkillSetEquip] Dropdown 未割り当てのため無効化します（プルダウンUIを使わないならこのコンポーネントを外してください）。");
            enabled = false;
            return;
        }

        dropdown.ClearOptions();
        System.Collections.Generic.List<string> opts =
            (allSets != null)
                ? allSets.Where(s => s).Select(s => s.GetLocalizedDisplayName()).ToList()
                : null;

        if (opts == null || opts.Count == 0)
        {
            dropdown.AddOptions(new System.Collections.Generic.List<string> { "（セットなし）" });
            if (descTMP) descTMP.text = "";
            return;
        }

        dropdown.AddOptions(opts);

        var saved = PlayerPrefs.GetString(PrefKey, "");
        int idx = Mathf.Max(0, System.Array.FindIndex(allSets, s => s && s.id == saved));
        dropdown.SetValueWithoutNotify(idx);
        UpdateDesc(idx);

        dropdown.onValueChanged.AddListener(i =>
        {
            var set = (i>=0 && i<allSets.Length) ? allSets[i] : null;
            if (set) { PlayerPrefs.SetString(PrefKey, set.id); PlayerPrefs.Save(); }
            UpdateDesc(i);
        });
    }
    private void UpdateDesc(int i)
    {
        if (!descTMP) return;
        var s = (allSets != null && i>=0 && i<allSets.Length) ? allSets[i] : null;
descTMP.text = s ? s.GetLocalizedDescription() : "";
    }
}
