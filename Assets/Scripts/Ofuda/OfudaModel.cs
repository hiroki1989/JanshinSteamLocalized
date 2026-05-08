using System;
using System.Collections.Generic;

[Serializable]
public sealed class OfudaCondition
{
    public string key;   // 例: "COND:Riichi", "COND:満貫以上", "COND:HP<50%" 等
    public string label; // 表示名
    public float prob;   // 出現確率（0.00–1.00）
}

[Serializable]
public sealed class OfudaEffect
{
    public string key;    // 例: "EFFECT:ダメージ倍率", "EFFECT:HPをxx%回復" 等
    public string label;  // 表示名
    public float prob;    // 出現確率（0.00–1.00）
    public float magnitude; // 効果量（任意）
    public string unit;     // "","%","pt" など（任意）
}

[Serializable]
public sealed class OfudaDef
{
    public string id;                // "CONDKEY__EFFKEY"
    public OfudaCondition condition; // 条件
    public OfudaEffect effect;       // 効果
    public float combinedProb;       // condition.prob + effect.prob
    public int price;                // 価格
    public string rarity;            // 表示用レア度
    public string displayName;       // UI表示名
    public string description;       // UI説明
}
