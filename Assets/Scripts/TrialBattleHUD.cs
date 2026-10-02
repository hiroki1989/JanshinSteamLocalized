using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Data binding only. Every panel, row, portrait and button is authored in RunScene.</summary>
public sealed class TrialBattleHUD : MonoBehaviour
{
    [Serializable] public class CharacterVisual
    {
        public string key;
        public Sprite portrait;
        public Sprite background;
    }
    public Image background;
    public CharacterVisual[] gods;
    public CharacterVisual[] players;
    public Button relicButton;
    public TMP_Text goldAmount;
    public TMP_Text skillButtonLabel;
    public TrialHandRowLayout handRow;
    public RectTransform doraArea;
    public Vector2 doraTileSize=new Vector2(70,98);
    public Image[] interactiveFrames;
    public GameObject waitsRoot;
    public TMP_Text waitsCaption;
    void LateUpdate()
    {
        if(waitsCaption)waitsCaption.gameObject.SetActive(waitsRoot && waitsRoot.activeSelf);
        if(doraArea && doraArea.childCount>0)
        {
            float scale=Mathf.Min(1,doraArea.rect.width/(doraArea.childCount*doraTileSize.x));
            for(int i=0;i<doraArea.childCount;i++)
            {
                var r=doraArea.GetChild(i) as RectTransform;if(!r)continue;
                r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);
                r.sizeDelta=doraTileSize;r.localScale=Vector3.one*scale;r.anchoredPosition=new Vector2(i*doraTileSize.x*scale,0);
            }
        }
        foreach(var frame in interactiveFrames??new Image[0])
        {
            if(!frame)continue;
            var button=frame.GetComponentInParent<Button>();
            if(!button)continue;
            bool available=button && button.IsInteractable();
            frame.color=available?new Color(.9f,.75f,.48f,1):new Color(.24f,.25f,.25f,.7f);
            foreach(var text in button.GetComponentsInChildren<TMP_Text>())text.color=available?new Color(.96f,.92f,.81f):new Color(.38f,.40f,.41f);
        }
    }
    public TMP_Text[] charmDescriptions;
    public Image[] charmIcons;
    public Sprite ResolvePortrait(string key, bool player)
    {
        var entries = player ? players : gods;
        if(entries == null) return null;
        foreach(var entry in entries)
            if(entry != null && entry.key == key)
            {
                if(!player && background && entry.background) background.sprite = entry.background;
                return entry.portrait;
            }
        return null;
    }
    public void RefreshCharms()
    {
        var ids = PlayerData.EquippedOmamoriIds;
        for(int i=0; i<charmDescriptions.Length; i++)
        {
            int id = ids != null && i < ids.Count ? ids[i] : 0;
            if(charmDescriptions[i])
            {
                string value = id > 0 ? PlayerData.GetOmamoriText_EquipUI_Localized(id, true) : EmptySlotText();
                GameManager.ApplyTraitSpriteAssetToTMPAnywhere(charmDescriptions[i]);
                charmDescriptions[i].richText = true;
                value = GameManager.RenderConsumableDescriptionAnywhere(value);
                if(charmDescriptions[i].text != value) charmDescriptions[i].text = value;
            }
            if(i < charmIcons.Length && charmIcons[i]) ItemArtwork.Omamori(charmIcons[i], id);
        }
    }
    public static string EmptySlotText()
    {
        return "ー";
    }
}
