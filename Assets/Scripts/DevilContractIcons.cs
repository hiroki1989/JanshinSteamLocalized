using UnityEngine;

public static class DevilContractIcons
{
    static readonly Sprite[] icons=new Sprite[11];
    static readonly Sprite[] portraits=new Sprite[11];
    static readonly Sprite[] branches=new Sprite[3];
    public static Sprite Get(int id)
    {
        if(id<0 || id>=icons.Length)return null;
        return icons[id]?icons[id]:(icons[id]=Resources.Load<Sprite>("DevilContracts/Symbols/Devil_"+id));
    }
    public static Sprite Portrait(int id)=>id<0||id>=11?null:portraits[id]?portraits[id]:(portraits[id]=Resources.Load<Sprite>("DevilContracts/Icons/Devil_"+id));
    public static Sprite Branch(int id)=>id<0||id>=3?null:branches[id]?branches[id]:(branches[id]=Resources.Load<Sprite>("DevilContracts/Symbols/Branch_"+id));
}
