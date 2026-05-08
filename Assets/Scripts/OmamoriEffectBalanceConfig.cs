using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Mahhan2/お守り/効果バランス設定", fileName = "OmamoriEffectBalanceConfig")]
public class OmamoriEffectBalanceConfig : ScriptableObject
{
    [Serializable]
    public class EffectScaleRow
    {
        [InspectorName("効果タイプ")]
        public PlayerData.OmamoriEffect effect;

        [InspectorName("Lv1初期値(%)")]
        public float basePercentAtLevel1 = 10f;

        [InspectorName("Lvごとの上昇値(%)")]
        public float percentPerLevel = 5f;
    }

    [Header("効果ごとのスケーリング（%）")]
    [InspectorName("効果スケール一覧")]
    public List<EffectScaleRow> effectScales = new List<EffectScaleRow>();

    // 内部キャッシュ（Inspectorには出ない）
    Dictionary<PlayerData.OmamoriEffect, EffectScaleRow> _map;

    void OnEnable()
    {
        BuildMap();
    }

    void OnValidate()
    {
        BuildMap();
    }

    void BuildMap()
    {
        _map = new Dictionary<PlayerData.OmamoriEffect, EffectScaleRow>();
        if (effectScales == null) return;

        for (int i = 0; i < effectScales.Count; i++)
        {
            var row = effectScales[i];
            if (row == null) continue;
            _map[row.effect] = row;
        }
    }

    public bool TryGetScale(PlayerData.OmamoriEffect effect, out float baseAtLv1, out float perLv)
    {
        baseAtLv1 = 0f;
        perLv = 0f;

        if (_map == null) BuildMap();
        if (_map != null && _map.TryGetValue(effect, out var row) && row != null)
        {
            baseAtLv1 = row.basePercentAtLevel1;
            perLv = row.percentPerLevel;
            return true;
        }
        return false;
    }
}
