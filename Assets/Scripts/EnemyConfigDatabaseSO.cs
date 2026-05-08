using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EnemyConfigEntrySO
{
    public int excelKey;
    public EnemyConfig config;
}

[CreateAssetMenu(menuName = "Mahhan2/MasterData/EnemyConfigDatabaseSO", fileName = "EnemyConfigDatabaseSO")]
public sealed class EnemyConfigDatabaseSO : ScriptableObject
{
    public List<EnemyConfigEntrySO> entries = new List<EnemyConfigEntrySO>();

    public Dictionary<int, EnemyConfig> ToDictionary()
    {
        var dict = new Dictionary<int, EnemyConfig>();
        if (entries == null) return dict;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (e.config == null) continue;
            dict[e.excelKey] = e.config;
        }
        return dict;
    }
}