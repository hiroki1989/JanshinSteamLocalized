using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DevilContractView : MonoBehaviour
{
    public TMP_FontAsset gameFont;
    public Image[] entryIcons=new Image[11];
    public Image selectedIcon;
    public Image selectedPortrait;
    public Image[] nodeIcons=new Image[30];
    public Image confirmGem;
    public TMP_Text confirmCost;
    public Button[] entries=new Button[11],nodes=new Button[30];
    public TMP_Text title,subtitle,gems,devilName,contractName,benefit,drawback,synergy,mercyTitle,powerTitle,resonanceTitle,footer,status;
    public TMP_Text[] entryLabels=new TMP_Text[11],nodeTitles=new TMP_Text[30],nodeBodies=new TMP_Text[30],nodeStates=new TMP_Text[30];
    public Button equip,unequip,close;
    public GameObject confirmRoot;public TMP_Text confirmBody;public Button confirmYes,confirmNo;
    int selected,selectedSlot=-1;Action pending;readonly List<BaseRaycaster> blocked=new List<BaseRaycaster>();GameObject previousSelection;
    public static DevilContractView Open()
    {
        if(FindAnyObjectByType<DevilContractView>())return null;
        var p=Resources.Load<DevilContractView>("DevilContracts/ContractView");
        if(!p){Debug.LogError("Devil Contract view prefab is missing");return null;}
        AudioManager.Instance?.PlayClickSE();return Instantiate(p);
    }
    void Start()
    {
        previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        foreach(var r in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))if(r.enabled&&!r.transform.IsChildOf(transform)){blocked.Add(r);r.enabled=false;}
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        for(int i=0;i<entries.Length;i++){int id=i;entries[i].onClick.AddListener(()=>{selected=id;selectedSlot=-1;confirmRoot.SetActive(false);AudioManager.Instance?.PlayClickSE();Refresh();});}
        for(int i=0;i<nodes.Length;i++){int slot=i;nodes[i].onClick.AddListener(()=>SelectNode(slot));}
        equip.onClick.AddListener(()=>{if(DevilContracts.Equip(selected)){AudioManager.Instance?.PlayClickSE();Refresh();}});
        unequip.onClick.AddListener(()=>{DevilContracts.Equip(-1);Refresh();});close.onClick.AddListener(()=>Destroy(gameObject));
        confirmYes.onClick.AddListener(ApplySelectedNode);
        confirmNo.onClick.AddListener(()=>{pending=null;selectedSlot=-1;confirmRoot.SetActive(false);});
        confirmRoot.SetActive(false);DevilContracts.Changed+=Refresh;LocalizationManager.LanguageChanged+=LanguageChanged;
        var s=DevilContracts.Load();selected=s.equipped>=0?s.equipped:0;Refresh();
    }
    void LanguageChanged(LocalizationManager.Language language)=>Refresh();
    static string T(string ja,string en,string zh)=>GameUIText.Get(ja,en,zh);
    public void Refresh()
    {
        if(!DevilContractCatalog.Shared)return;
        var bodyFont=LocalizationManager.Instance.GetBodyFont()??gameFont??TMP_Settings.defaultFontAsset;
        if(LocalizationManager.Instance.CurrentLanguage==LocalizationManager.Language.ChineseSimplified && !bodyFont.HasCharacter('恶',true,true))bodyFont=Resources.Load<TMP_FontAsset>("Tutorial/Fonts/ChineseSimplified")??bodyFont;
        if(bodyFont)foreach(var text in GetComponentsInChildren<TMP_Text>(true)){text.font=bodyFont;text.fontSharedMaterial=bodyFont.material;}
        var save=DevilContracts.Load();var e=save.entries[selected];var d=DevilContractCatalog.Get(selected);bool revealed=e.unlocked!=0;
        const string hidden="？？？";
        if(!revealed){pending=null;confirmRoot.SetActive(false);confirmBody.text=hidden;}
        title.text=T("悪魔の契約","DEVIL CONTRACTS","恶魔契约");
        subtitle.text="";subtitle.gameObject.SetActive(false);
        gems.text=SpecialTileSystem.GetGems().ToString("N0");
        for(int i=0;i<entries.Length;i++){
            var def=DevilContractCatalog.Get(i);bool owned=save.entries[i].unlocked!=0;
            entryLabels[i].text=(owned?def.Name:hidden)+"\n<size=65%>"+(owned?(save.equipped==i?T("契約中","EQUIPPED","已签约"):def.title.Text):(i==10?hidden:def.god.Text)+T("のミッション達成で獲得"," — complete this god's mission"," — 完成该神的任务获取"))+"</size>";
            if(i<entryIcons.Length&&entryIcons[i]){entryIcons[i].gameObject.SetActive(owned);entryIcons[i].sprite=DevilContractIcons.Get(i);}
            entryLabels[i].alignment=TextAlignmentOptions.Center;
            entryLabels[i].color=owned?JanshinPanelTheme.Ivory:new Color(.6f,.62f,.65f);
            entries[i].GetComponent<Image>().color=i==selected?new Color(.21f,.13f,.08f,.95f):new Color(.025f,.021f,.03f,.88f);
        }
        if(selectedPortrait){selectedPortrait.gameObject.SetActive(revealed);selectedPortrait.sprite=revealed?DevilContractIcons.Portrait(selected):null;}
        if(selectedIcon){selectedIcon.gameObject.SetActive(revealed);selectedIcon.sprite=DevilContractIcons.Get(selected);}
        devilName.text=revealed?d.Name:hidden;devilName.color=revealed?d.accent:JanshinPanelTheme.Ivory;contractName.text=revealed?d.title.Text:hidden;
        benefit.text="<color=#E2C789>"+T("恩恵","BENEFIT","恩惠")+"</color>\n"+(revealed?d.benefit.Text:hidden);
        drawback.text="<color=#D89187>"+T("代償","PRICE","代价")+"</color>\n"+(revealed?d.drawback.Text:hidden);
        synergy.text=T("相性：","Synergy: ","协同：")+(revealed?d.synergy.Text:hidden);
        mercyTitle.text=T("代償を抑える道","PATH OF RESTRAINT","减轻代价之路");powerTitle.text=T("力を追求する道","PATH OF POWER","追求力量之路");
        if(resonanceTitle)resonanceTitle.text=T("相乗効果を育てる道","PATH OF RESONANCE","培育协同之路");
        for(int i=0;i<nodes.Length;i++){
            int branch=i/10;int depth=i%10+1;var node=(branch==0?d.mercy:branch==1?d.power:d.resonance)[depth-1];int unlocked=branch==0?e.mercy:branch==1?e.power:e.resonance,active=branch==0?e.activeMercy:branch==1?e.activePower:e.activeResonance;
            nodeTitles[i].text=!revealed?hidden:"<color=#D7B577>"+node.name.Text+"</color>";
            nodeBodies[i].text=revealed?node.description.Text:hidden;
            nodeStates[i].text=e.unlocked==0?hidden:active>=depth?T("有効","ACTIVE","生效"):unlocked>=depth?T("解放済み","UNLOCKED","已解锁"):T("未解放","LOCKED","未解锁");
            nodeTitles[i].gameObject.SetActive(false);nodeBodies[i].gameObject.SetActive(false);nodeStates[i].gameObject.SetActive(false);
            nodes[i].interactable=revealed;
            if(i<nodeIcons.Length&&nodeIcons[i]){nodeIcons[i].sprite=DevilContractIcons.Branch(branch);nodeIcons[i].gameObject.SetActive(revealed);nodeIcons[i].color=active>=depth?Color.white:unlocked>=depth?new Color(.78f,.84f,.86f):unlocked==depth-1?new Color(.88f,.72f,.42f):new Color(.31f,.32f,.35f);}
            nodeBodies[i].color=active>=depth?JanshinPanelTheme.Ivory:new Color(.64f,.66f,.69f);
            nodeStates[i].color=active>=depth?new Color(.91f,.75f,.43f):new Color(.65f,.72f,.77f);
            nodes[i].GetComponent<Image>().color=active>=depth?new Color(.16f,.105f,.055f,.95f):new Color(.025f,.021f,.03f,.88f);
        }
        equip.interactable=e.unlocked!=0&&save.equipped!=selected;
        equip.GetComponentInChildren<TMP_Text>().text=save.equipped==selected?T("契約中","EQUIPPED","已签约"):T("契約を締結する","SIGN CONTRACT","签订契约");
        unequip.GetComponentInChildren<TMP_Text>().text=T("契約を外す","UNEQUIP","解除装备");unequip.interactable=save.equipped>=0;
        close.GetComponentInChildren<TMP_Text>().text=T("戻る","BACK","返回");
        footer.text="";footer.gameObject.SetActive(false);
        status.text=save.equipped>=0?T("現在の契約：","Equipped: ","当前契约：")+DevilContractCatalog.Get(save.equipped).Name:T("契約なし","No contract equipped","未装备契约");
        confirmNo.GetComponentInChildren<TMP_Text>().text=T("閉じる","CLOSE","关闭");
        if(confirmRoot.activeSelf&&selectedSlot>=0)RefreshNodePopup();
    }
    void SelectNode(int slot)
    {
        if(slot<0||slot>=nodes.Length||DevilContracts.Load().entries[selected].unlocked==0)return;
        selectedSlot=slot;confirmRoot.SetActive(true);confirmRoot.transform.SetAsLastSibling();RefreshNodePopup();
    }
    void RefreshNodePopup()
    {
        int branch=selectedSlot/10,depth=selectedSlot%10+1;var e=DevilContracts.Load().entries[selected];
        int level=branch==0?e.mercy:branch==1?e.power:e.resonance,active=branch==0?e.activeMercy:branch==1?e.activePower:e.activeResonance;
        var d=DevilContractCatalog.Get(selected);var node=(branch==0?d.mercy:branch==1?d.power:d.resonance)[depth-1];
        bool owned=level>=depth,previous=level==depth-1,enough=SpecialTileSystem.GetGems()>=DevilContracts.Cost(depth);
        string note=owned?(active>=depth?T("有効","ACTIVE","生效"):T("解放済み","UNLOCKED","已解锁")):!previous?T("前の段階を解放してください","Unlock the previous node first","请先解锁上一阶段"):!enough?T("宝石が足りません","Not enough gems","宝石不足"):T("獲得後、有効化すると次の通常ランから適用されます","Acquire, then enable to apply from the next normal run","获取后启用，将从下一次普通挑战开始生效");
        confirmBody.text="<color=#E2C789>"+node.name.Text+"</color>\n\n"+node.description.Text+"\n\n<size=75%>"+note+"</size>";
        confirmGem.gameObject.SetActive(!owned);confirmCost.gameObject.SetActive(!owned);confirmCost.text=DevilContracts.Cost(depth).ToString();
        confirmYes.interactable=owned||(previous&&enough);
        confirmYes.GetComponentInChildren<TMP_Text>().text=owned?(active>=depth?T("停止する","DISABLE","停用"):T("有効にする","ENABLE","启用")):T("獲得する","ACQUIRE","获取");
    }
    void ApplySelectedNode()
    {
        if(selectedSlot<0)return;
        int branch=selectedSlot/10,depth=selectedSlot%10+1;var e=DevilContracts.Load().entries[selected];if(e.unlocked==0)return;
        int level=branch==0?e.mercy:branch==1?e.power:e.resonance,active=branch==0?e.activeMercy:branch==1?e.activePower:e.activeResonance;
        if(level>=depth){DevilContracts.SetBranchDepth(selected,branch,active>=depth?depth-1:depth);AudioManager.Instance?.PlayClickSE();}
        else if(level==depth-1&&DevilContracts.ResearchBranch(selected,branch))AudioManager.Instance?.PlayGoldSpendSE();
        Refresh();
    }
    void OnDestroy()
    {
        DevilContracts.Changed-=Refresh;LocalizationManager.LanguageChanged-=LanguageChanged;
        foreach(var r in blocked)if(r)r.enabled=true;if(EventSystem.current)EventSystem.current.SetSelectedGameObject(previousSelection);
    }
}
