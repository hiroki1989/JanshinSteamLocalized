using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OfudaPriceBandSO
{
    public float maxProbSum;
    public string rarity;
    public float priceK;
    public int priceFixed;
}

[CreateAssetMenu(menuName = "Mahhan2/MasterData/OfudaCatalogSO", fileName = "OfudaCatalogSO")]
public sealed class OfudaCatalogSO : ScriptableObject
{
    public List<OfudaCondition> conditions = new List<OfudaCondition>();
    public List<OfudaEffect> effects = new List<OfudaEffect>();
    public List<OfudaPriceBandSO> priceMap = new List<OfudaPriceBandSO>();
}