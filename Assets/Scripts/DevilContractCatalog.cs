using System;
using UnityEngine;

[CreateAssetMenu(menuName="Janshin/Devil Contracts/Catalog")]
public sealed class DevilContractCatalog : ScriptableObject
{
    [Serializable] public class Localized
    {
        [TextArea] public string ja,en,zh;
        public string Text=>(GameUIText.Get(ja,en,zh)??"").Replace("−","-");
    }
    [Serializable] public class Node { public Localized name,description; }
    [Serializable] public class Definition
    {
        public int id; public Localized devil,god,title,benefit,drawback,synergy;
        public string emblem; public Color accent;
        public Node[] mercy=new Node[10],power=new Node[10],resonance=new Node[10];
        public string Name=>devil.Text;
    }
    public Definition[] contracts;
    static DevilContractCatalog cached;
    public static DevilContractCatalog Shared=>cached?cached:(cached=Resources.Load<DevilContractCatalog>("DevilContracts/Catalog"));
    public static Definition Get(int id)=>Shared&&Shared.contracts!=null&&id>=0&&id<Shared.contracts.Length?Shared.contracts[id]:null;
}
