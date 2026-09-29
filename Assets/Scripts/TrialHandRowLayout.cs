using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Scene-authored containers; only the geometry of the existing gameplay tiles is updated.
[ExecuteAlways]
public sealed class TrialHandRowLayout : MonoBehaviour
{
    public RectTransform hand;
    public RectTransform[] meldSlots;
    public Vector2 tileSize = new Vector2(70,98);
    public float handMeldGap=26, meldGap=16;
    public float CurrentScale { get; private set; } = 1;
    void LateUpdate() { Arrange(); }
    static RectTransform[] Tiles(RectTransform parent) => parent ? parent.Cast<Transform>().Where(t=>t.GetComponent<Button>() && t.gameObject.activeSelf).Cast<RectTransform>().ToArray() : new RectTransform[0];
    static bool Sideways(RectTransform tile) => Mathf.Abs(Mathf.DeltaAngle(tile.localEulerAngles.z,90))<1;
    public void Arrange()
    {
        if(!hand)return;
        var area=(RectTransform)transform;
        var concealed=Tiles(hand);
        var slots=(meldSlots??new RectTransform[0]).Where(s=>s && Tiles(s).Length>0).ToArray();
        float handWidth=concealed.Length*tileSize.x;
        float total=handWidth+(slots.Length>0 ? handMeldGap+meldGap*(slots.Length-1):0);
        foreach(var slot in slots)total+=Tiles(slot).Sum(t=>Sideways(t)?tileSize.y:tileSize.x);
        CurrentScale=Mathf.Min(1,Mathf.Max(1,area.rect.width)/Mathf.Max(1,total));
        float x=(area.rect.width-total*CurrentScale)*.5f;
        Position(hand,x,handWidth*CurrentScale,area.rect.height);
        PlaceTiles(concealed,CurrentScale);
        x+=handWidth*CurrentScale;
        if(slots.Length>0)x+=handMeldGap*CurrentScale;
        foreach(var slot in slots)
        {
            float width=Tiles(slot).Sum(t=>Sideways(t)?tileSize.y:tileSize.x)*CurrentScale;
            Position(slot,x,width,area.rect.height);PlaceTiles(Tiles(slot),CurrentScale);x+=width+meldGap*CurrentScale;
        }
    }
    static void Position(RectTransform r,float x,float w,float h)
    {
        r.anchorMin=r.anchorMax=new Vector2(0,0);r.pivot=Vector2.zero;
        r.localScale=Vector3.one;r.anchoredPosition=new Vector2(x,0);r.sizeDelta=new Vector2(w,h);
    }
    void PlaceTiles(RectTransform[] tiles,float scale)
    {
        float x=0;
        foreach(var tile in tiles)
        {
            bool rotated=Sideways(tile);
            float width=(rotated?tileSize.y:tileSize.x)*scale;
            float height=(rotated?tileSize.x:tileSize.y)*scale;
            tile.anchorMin=tile.anchorMax=Vector2.zero;tile.pivot=new Vector2(.5f,.5f);
            tile.sizeDelta=tileSize;tile.localScale=Vector3.one*scale;
            tile.anchoredPosition=new Vector2(x+width*.5f,height*.5f);x+=width;
        }
    }
}
