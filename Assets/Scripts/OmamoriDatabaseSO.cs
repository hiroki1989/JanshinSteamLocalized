using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OmamoriRarityRowSO
{
    public PlayerData.OmamoriRarity rarity;
    public float baseW;
    public float perDefeat;
    public int count;
}

[Serializable]
public sealed class OmamoriEffectRowSO
{
    public PlayerData.OmamoriEffect effect;
    public int w;
}

[Serializable]
public sealed class OmamoriSpecialRowSO
{
    public PlayerData.OmamoriSpecial sp;
    public int w;
}

[CreateAssetMenu(menuName = "Mahhan2/MasterData/OmamoriDatabaseSO", fileName = "OmamoriDatabaseSO")]
public sealed class OmamoriDatabaseSO : ScriptableObject
{
    public List<OmamoriRarityRowSO> rarityRows = new List<OmamoriRarityRowSO>();
    public List<OmamoriEffectRowSO> effectRows = new List<OmamoriEffectRowSO>();
    public List<OmamoriSpecialRowSO> specialRows = new List<OmamoriSpecialRowSO>();

    public float baseAtLv1 = 5f;
    public float perLv = 1f;
    public int rangeMinus = 2;
    public int rangePlus = 2;
    public bool allowDup = false;
}