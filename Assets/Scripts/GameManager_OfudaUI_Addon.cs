// 新規ファイル: Assets/Scripts/GameManager_OfudaUI_Addon.cs
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public partial class GameManager
{
    [Header("=== Ofuda UI ===")]
    public RectTransform ofudaPanel;
    public TextMeshProUGUI ofudaListTMP;

    private Dictionary<string, OfudaDef> _ofudaMap; // id -> def

    private void EnsureOfudaMap()
    {
        if (_ofudaMap != null) return;
        try
        {
            var defs = OfudaCatalog.BuildFromExcel(OfudaExcelLoader.Load());
            _ofudaMap = defs.ToDictionary(d => d.id, d => d);
        }
        catch { _ofudaMap = new Dictionary<string, OfudaDef>(); }
    }

public void RefreshRunOfudaPanel()
{
    EnsureOfudaMap();
    TryAutoWireOfudaTMP();                  // ★ まず自動ワイヤ

    if (!ofudaListTMP)                     // ★ まだ見つからないなら明示表示して終了
    {
        Debug.LogWarning("[OfudaUI] ofudaListTMP 未割り当てです。OfudaPanel 配下に TextMeshProUGUI を置くか、Inspector で割り当ててください。");
        if (ofudaPanel)                   // パネルだけあるなら一応メッセージを表示
        {
            var fallback = ofudaPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (fallback) ofudaListTMP = fallback;
        }
        if (!ofudaListTMP) return;
    }

    string csv = PlayerPrefs.GetString("RunOfuda", "");
        var ids = csv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

if (ids.Count == 0)
{
    ofudaListTMP.text = LocalizationManager.Fixed("fixed.ofuda_none");
    return;
}

var sb = new System.Text.StringBuilder();
sb.AppendLine(LocalizationManager.Fixed("fixed.ofuda_owned"));
foreach (var id in ids)
{
    if (_ofudaMap != null && _ofudaMap.TryGetValue(id, out var def))
    {
        sb.Append("• ").Append(def.displayName);
        var desc = def.description;
        if (!string.IsNullOrEmpty(desc))
            sb.Append(": ").Append(desc);
        sb.AppendLine();
    }
    else
    {
        sb.Append("• ").Append(id).AppendLine();
    }
}
        ofudaListTMP.text = sb.ToString();
    }

    // ★追加：UI自動ワイヤリング
private void TryAutoWireOfudaTMP()
{
    if (!ofudaListTMP && ofudaPanel)
        ofudaListTMP = ofudaPanel.GetComponentInChildren<TextMeshProUGUI>(true);
}
// ★追加：起動直後に1回は描画（Inspector割当て忘れでも自動で拾う）

// ★追加：お札取得処理の末尾から呼べば、その場でUI反映される
public void NotifyOfudaChanged()
{
    try { RefreshRunOfudaPanel(); } catch {}
}

}
