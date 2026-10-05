using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rows and scroll viewport are authored in RunScene. Runtime only binds the winning effects.
public sealed class AppliedScoringEffectsView : MonoBehaviour
{
    [Serializable] public class Row { public GameObject root; public Image icon; public TextMeshProUGUI text; }
    public Row[] rows;
    public GameObject[] replacedObjects;
    [NonSerialized] public readonly List<GameObject> inactiveObjects=new List<GameObject>();
    public ScrollRect scroll;
    public GameObject specialEffectsRoot;
    public TMP_Text specialEffectsText;
    public GameObject[] specialEffectsDecorations;
    public int Count { get; private set; }
    public void Clear()
    {
        Count=0; inactiveObjects.Clear();foreach(var row in rows)row.root.SetActive(false);
        if(scroll)scroll.verticalNormalizedPosition=1;
        Suppress();
    }
    public void Add(Sprite icon,string body,Color? iconColor=null)
    {
        if(string.IsNullOrWhiteSpace(body)||Count>=rows.Length)return;
        var row=rows[Count++];row.text.text=body;
        GameManager.ApplyTraitSpriteAssetToTMPAnywhere(row.text);
        float width=scroll&&scroll.viewport?scroll.viewport.rect.width-92:558;
        var layout=row.root.GetComponent<LayoutElement>();
        if(layout)layout.preferredHeight=Mathf.Max(64,row.text.GetPreferredValues(body,Mathf.Max(300,width),2000).y+12);
        row.icon.sprite=icon;row.icon.preserveAspect=true;row.icon.color=iconColor??Color.white;row.icon.gameObject.SetActive(icon);
        row.root.SetActive(true);
    }
    public List<GameObject> VisibleRows()
    {
        var list=new List<GameObject>();for(int i=0;i<Count;i++)list.Add(rows[i].root);return list;
    }
    public bool Replaces(GameObject go)=>Array.IndexOf(replacedObjects,go)>=0||inactiveObjects.Contains(go);
    void LateUpdate(){Suppress();if(specialEffectsRoot&&specialEffectsText){string value=specialEffectsText.text;bool active=!string.IsNullOrWhiteSpace(value)&&value!="-"&&value!="ー";specialEffectsRoot.SetActive(active);foreach(var decoration in specialEffectsDecorations??Array.Empty<GameObject>())if(decoration)decoration.SetActive(active);}}
    public void Suppress(){foreach(var go in replacedObjects)if(go)go.SetActive(false);foreach(var go in inactiveObjects)if(go)go.SetActive(false);}
}
