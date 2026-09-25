using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class NormalScoringPortrait : MonoBehaviour
{
    public Func<Sprite> resolve;
    Image portrait;
    RectTransform content;
    RectTransform card;
    void OnEnable() { Refresh(); }
    public void Refresh()
    {
        if (resolve == null) return;
        var panel = (RectTransform)transform;
        if (!portrait)
        {
            var children = new Transform[transform.childCount];
            for(int i=0;i<children.Length;i++) children[i]=transform.GetChild(i);
            content = new GameObject("ScoringContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(panel,false); content.anchorMin=Vector2.zero;content.anchorMax=Vector2.one;content.sizeDelta=Vector2.zero;
            foreach(var child in children)child.SetParent(content,false);
            content.localScale=Vector3.one*.78f;
            content.anchoredPosition=new Vector2(panel.rect.width*.1f,0);
            portrait=new GameObject("WinningCharacter",typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            portrait.transform.SetParent(panel,false);portrait.raycastTarget=false;portrait.preserveAspect=true;
            portrait.rectTransform.anchorMin=portrait.rectTransform.anchorMax=new Vector2(.5f,.5f);
            card=content.Find("Card") as RectTransform;
        }
        portrait.sprite=resolve();portrait.enabled=portrait.sprite;
        PlacePortrait();
    }
    void LateUpdate(){PlacePortrait();}
    void PlacePortrait()
    {
        if(!portrait||!card)return;
        // The decorative card is narrower than its full-screen panel container.
        // Align to that card, leaving the text column clear on its right.
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(transform,card);
        float width=bounds.size.x*.36f;
        portrait.rectTransform.sizeDelta=new Vector2(width,bounds.size.y*1.05f);
        portrait.rectTransform.anchoredPosition=new Vector2(bounds.min.x-width*.04f,bounds.center.y+bounds.size.y*.02f);
    }
}
