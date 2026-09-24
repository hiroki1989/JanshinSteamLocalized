using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName="Janshin/外伝モード UI設定")]
public sealed class GaidenUISettings : ScriptableObject
{
    [Header("チュートリアル")]
    public string tutorialTitle="外伝モードの遊び方";
    [TextArea(5,18)] public string[] tutorialPages;
    public string previousLabel="前へ", nextLabel="次へ", startLabel="始める";
    [Header("Tier選択：外伝モード入口")]
    public Sprite buttonSprite;
    public Image.Type buttonImageType=Image.Type.Sliced;
    public Color buttonColor=new Color(.025f,.115f,.13f,.98f);
    public Sprite frameSprite;
    public Color frameColor=new Color(.90f,.73f,.38f);
    public bool showFrame=true, showSeals=true;
    public Color sealColor=new Color(.9f,.72f,.36f);
    public Color labelColor=new Color(1f,.96f,.84f);
    [TextArea(2,4)] public string buttonLabel="<size=20><color=#E7BE70>特 別 対 局</color></size>\n外伝モード";
    public Vector2 buttonPosition=new Vector2(-35,0),buttonSize=new Vector2(300,120);
    [Range(12,60)] public float buttonFontSize=30;
    [HideInInspector] public AudioClip normalStrikeSound;
    [HideInInspector] public float normalStrikeDuration=1.05f;
    static GaidenUISettings cached;
    public static GaidenUISettings Current {
        get {
            if(!cached)cached=Resources.Load<GaidenUISettings>("SeventeenSteps/GaidenUISettings");
            if(!cached)cached=CreateInstance<GaidenUISettings>();
            return cached;
        }
    }
}
