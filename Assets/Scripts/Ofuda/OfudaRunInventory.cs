using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ラン中お札（PlayerPrefs "RunOfuda"）の入出力を一元化するユーティリティ。
/// ・順序を保持（左→右）
/// ・最大3枠
/// ・重複IDは追加しない
/// </summary>
public static class OfudaRunInventory
{
    public const string PrefKey = "RunOfuda";
    public const int MaxSlots = 3;

    public static List<string> LoadList()
    {
        var raw = PlayerPrefs.GetString(PrefKey, "");
        var list = new List<string>();

        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var s in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var id = (s ?? "").Trim();
                if (string.IsNullOrEmpty(id)) continue;
                if (list.Contains(id)) continue;          // 重複排除（先勝ち）
                list.Add(id);
                if (list.Count >= MaxSlots) break;        // ★ 3枠で打ち切り
            }
        }

        return list;
    }

    public static void SaveList(List<string> list)
    {
        if (list == null) list = new List<string>();

        // null/空/重複除外、最大3枠
        var cleaned = new List<string>();
        foreach (var s in list)
        {
            var id = (s ?? "").Trim();
            if (string.IsNullOrEmpty(id)) continue;
            if (cleaned.Contains(id)) continue;
            cleaned.Add(id);
            if (cleaned.Count >= MaxSlots) break;
        }

        var raw = string.Join(",", cleaned);
        PlayerPrefs.SetString(PrefKey, raw);
        PlayerPrefs.Save();
    }

    public static int Count => LoadList().Count;
    public static bool IsFull => Count >= MaxSlots;

    /// <summary>末尾に追加（左→右で埋まる）。満杯なら false。</summary>
    public static bool TryAdd(string ofudaId)
    {
        var id = (ofudaId ?? "").Trim();
        if (string.IsNullOrEmpty(id)) return false;

        var list = LoadList();
        if (list.Contains(id)) return false;      // 同じお札は増やさない（仕様上の安全策）
        if (list.Count >= MaxSlots) return false; // ★ 満杯

        list.Add(id);
        SaveList(list);
        return true;
    }

    /// <summary>指定スロットを破棄。残りは左詰めになる。</summary>
    public static bool RemoveAt(int index)
    {
        var list = LoadList();
        if (index < 0 || index >= list.Count) return false;

        list.RemoveAt(index);
        SaveList(list);
        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }
}
