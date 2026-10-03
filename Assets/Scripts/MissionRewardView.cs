using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MissionRewardView : MonoBehaviour
{
    public Image portrait,icon,accent;
    public GameObject portraitRoot;
    public TMP_Text heading,nameText,body,footer,buttonLabel;
    public Button confirm;
    public CanvasGroup group;
    public RectTransform card;
    public Sprite goldIcon;
    readonly List<BaseRaycaster> blocked=new List<BaseRaycaster>();
    bool accepted;
    public static IEnumerator Present(bool allowCurrent=true)
    {
        var prefab=Resources.Load<MissionRewardView>("DevilContracts/MissionRewardView");
        if(!prefab){Debug.LogError("Mission reward view missing; reward remains pending.");yield break;}
        var notice=MissionRewardSettlement.Begin(allowCurrent);if(notice==null)yield break;
        var view=Instantiate(prefab);
        foreach(var r in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))if(r.enabled&&!r.transform.IsChildOf(view.transform)){view.blocked.Add(r);r.enabled=false;}
        view.confirm.onClick.AddListener(()=>{AudioManager.Instance?.PlayClickSE();view.accepted=true;});
        while(notice!=null)
        {
            view.accepted=false;view.Configure(notice);view.confirm.interactable=false;
            if(notice.stage==2)AudioManager.Instance?.PlayCutin_PlayerSkill();else AudioManager.Instance?.PlayScoringStepGoldSE();
            for(float t=0;t<.65f;t+=Time.unscaledDeltaTime){float p=Mathf.SmoothStep(0,1,t/.65f);view.group.alpha=p;view.card.localScale=Vector3.one*Mathf.Lerp(.96f,1,p);yield return null;}
            view.group.alpha=1;view.card.localScale=Vector3.one;view.confirm.interactable=true;
            yield return new WaitUntil(()=>view.accepted);
            MissionRewardSettlement.Acknowledge(notice);notice=MissionRewardSettlement.Read()??MissionRewardSettlement.Begin(allowCurrent);
        }
        Destroy(view.gameObject);
        yield return null;
    }
    public void Configure(MissionRewardSettlement.Notice n)
    {
        var font=LocalizationManager.Instance.GetBodyFont();
        if(LocalizationManager.Instance.CurrentLanguage==LocalizationManager.Language.ChineseSimplified && (!font||!font.HasCharacter('恶',true,true)))font=Resources.Load<TMP_FontAsset>("Tutorial/Fonts/ChineseSimplified")??font;
        if(font)foreach(var t in GetComponentsInChildren<TMP_Text>(true)){t.font=font;t.fontSharedMaterial=font.material;}
        bool contract=n.stage==2;var d=contract?DevilContractCatalog.Get(n.devilId):null;
        portraitRoot.SetActive(contract);portrait.sprite=contract?DevilContractIcons.Portrait(n.devilId):null;
        icon.sprite=contract?DevilContractIcons.Get(n.devilId):goldIcon;icon.color=Color.white;
        heading.text=GameUIText.Get(contract?"悪魔の契約書を獲得":"ミッション達成",contract?"DEVIL CONTRACT ACQUIRED":"MISSION COMPLETE",contract?"获得恶魔契约书":"任务完成");
        nameText.text=contract?d.Name:n.missionName;
        nameText.color=contract?Color.Lerp(d.accent,JanshinPanelTheme.Ivory,.6f):JanshinPanelTheme.Gold;
        body.text=contract?d.title.Text+"\n\n<color=#E2C789>"+GameUIText.Get("恩恵","BENEFIT","恩惠")+"</color>\n"+d.benefit.Text+"\n\n<color=#D89187>"+GameUIText.Get("代償","PRICE","代价")+"</color>\n"+d.drawback.Text:
            "+"+n.gold.ToString("N0")+"\n"+GameUIText.Get("ミッション報酬を獲得","Mission reward received","已领取任务奖励");
        footer.text=contract?GameUIText.Get("契約書は永続。メニューの「悪魔の契約」で締結できる", "Keep this contract permanently. Sign it in Devil Contracts from the menu.","契约书永久保留。可在菜单的恶魔契约中签订。"):
            GameUIText.Get("所持Goldに加算しました","Added to your Gold","已加入持有金币");
        buttonLabel.text=GameUIText.Get("確認","CONTINUE","确认");
        accent.color=contract?d.accent:JanshinPanelTheme.Gold;
    }
    void OnDestroy(){foreach(var r in blocked)if(r)r.enabled=true;}
}
