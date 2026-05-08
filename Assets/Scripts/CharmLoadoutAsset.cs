using UnityEngine;

[CreateAssetMenu(menuName="Mahjan/Charm Loadout", fileName="CharmLoadout")]
public class CharmLoadoutAsset : ScriptableObject
{
    [Header("攻撃値に対する係数")]
    public float attackMul = 1f;   // 乗算（⑩）
    public int   attackAdd = 0;    // 加算
    [Header("回復値に対する係数")]
    public float healMul   = 1f;   // 乗算
    public int   healAdd   = 0;    // 加算
}
