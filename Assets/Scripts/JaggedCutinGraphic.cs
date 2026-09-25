using UnityEngine;
using UnityEngine.UI;
public sealed class JaggedCutinGraphic : MaskableGraphic
{
    public bool spikes;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        Vector2[] p=spikes?new[]{new Vector2(0,.27f),new Vector2(.27f,.30f),new Vector2(.09f,.02f),new Vector2(.43f,.29f),new Vector2(.31f,0),new Vector2(.64f,.32f),new Vector2(1,.65f),new Vector2(.77f,.69f),new Vector2(.93f,.91f),new Vector2(.59f,.7f),new Vector2(.73f,1),new Vector2(.40f,.71f)}:
        new[]{new Vector2(0,.28f),new Vector2(.10f,.38f),new Vector2(.04f,.43f),new Vector2(.13f,.48f),new Vector2(.025f,.75f),new Vector2(.33f,.72f),new Vector2(.59f,.74f),new Vector2(.98f,.58f),new Vector2(.90f,.51f),new Vector2(1,.46f),new Vector2(.91f,.40f),new Vector2(.965f,.25f),new Vector2(.68f,.26f),new Vector2(.37f,.29f),new Vector2(.075f,.25f)};
        vh.AddVert(new Vector3(r.center.x,r.center.y),color,Vector2.zero);
        foreach(var v in p)vh.AddVert(new Vector3(r.x+v.x*r.width,r.y+v.y*r.height),color,Vector2.zero);
        for(int i=0;i<p.Length;i++)vh.AddTriangle(0,i+1,(i+1)%p.Length+1);
    }
}
