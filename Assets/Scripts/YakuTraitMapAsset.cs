using UnityEngine;

[CreateAssetMenu(menuName="Mahjan/Yaku Trait Map", fileName="YakuTraitMap")]
public class YakuTraitMapAsset : ScriptableObject
{
    public enum Trait { Geki, Shun, Iyu } // 撃/瞬/癒
    [System.Serializable]
    public class Entry {
        public string yakuName;                   // 例: "立直", "平和", "一盃口", "混一色", ...
        public Trait trait;
        public SkillSetAsset.YakuDifficulty difficulty = SkillSetAsset.YakuDifficulty.Normal;
    }
    public Entry[] entries;

    public bool TryGet(string yakuName, out Trait trait, out SkillSetAsset.YakuDifficulty diff)
    {
        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.yakuName) && yakuName.Contains(e.yakuName))
            { trait = e.trait; diff = e.difficulty; return true; }
        }
        trait = default; diff = default; return false;
    }
}
