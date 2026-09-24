using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
    bool _consumableBloodPactAppliedThisScoring;
    public string RenderConsumableDescription(string text) => ReplaceTraitWordsWithIcons(text);
    Button _consumableButton;
    ConsumableWindow _consumableWindow;
    bool _consumableSealThisEnemyTurn, _consumableDealing, _consumablePreviousFreeze;
    int _consumableSlot=-1, _consumableHand=-1, _consumableDiscard=-1;
    string _consumableReplacement;
    static string ItemText(string ja,string en,string zh) => ConsumableWindow.T(ja,en,zh);

    void EnsureConsumableButton()
    {
        if (_consumableButton || !btnMenu) return;
        _consumableButton=Instantiate(btnMenu,btnMenu.transform.parent,false);
        _consumableButton.name="Button_ConsumableInventory";
        _consumableButton.onClick=new Button.ButtonClickedEvent();
        _consumableButton.onClick.AddListener(OpenConsumableInventory);
        var rt=(RectTransform)_consumableButton.transform;
        var source=(RectTransform)btnMenu.transform;
        var deckButton=btnMenu.transform.parent.Find("Deck") as RectTransform;
        var controls=new[]{rt,source,deckButton};
        for(int i=0;i<controls.Length;i++)if(controls[i])
        {
            controls[i].anchorMin=controls[i].anchorMax=controls[i].pivot=new Vector2(.5f,.5f);
            controls[i].anchoredPosition=new Vector2(-786,-407-i*50);controls[i].sizeDelta=new Vector2(280,42);
            foreach(var label in controls[i].GetComponentsInChildren<TMP_Text>(true))
            {
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
                label.rectTransform.offsetMin=new Vector2(8,2);label.rectTransform.offsetMax=new Vector2(-8,-2);
                label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=28;
            }
        }
        foreach(var localized in _consumableButton.GetComponentsInChildren<LocalizedTextUI>(true)) localized.enabled=false;
        RefreshConsumableButton();
    }
    bool CanUseConsumableNow()
    {
        return phase==Phase.Offer && offers.Count==4 && playerHP>0 && enemyHP>0 &&
            !_preparedForSceneUnload && !_defeatTransitionRunning && !_tutorialRunning &&
            !_tutorialDealingFirstDraw && !_consumableDealing && !_playerSkillCutinRunning &&
            !_playerSkillTransformRunning && !_rinshanDrawRunning && !_enemyWinDamageAnimating &&
            !_playerHasWonThisHand && !_enemyHasWonThisHand && !isMenuOpen && !_activeSkillPopup &&
            (!_freezeProgression || (_consumableWindow && !_consumablePreviousFreeze));
    }
    void RefreshConsumableButton()
    {
        if(!_consumableButton)return;
        var s=RunConsumables.Load();
        foreach(var t in _consumableButton.GetComponentsInChildren<TMP_Text>(true))
            t.text=ItemText("遺物","Relics","遗物")+" "+s.bag.Count+"/"+RunConsumables.Capacity;
        _consumableButton.interactable=CanUseConsumableNow() && !_consumableWindow;
    }
void OpenConsumableInventory()
{
    if(_consumableWindow || !CanUseConsumableNow())return;
    _consumablePreviousFreeze=_freezeProgression;
    _freezeProgression=true;
    Sprite frame=Resources.Load<Sprite>("Consumables/PanelFrame");
    _consumableWindow=ConsumableWindow.Open(transform,ItemText("所持遺物","Relics","持有遗物"),frame);
    _consumableWindow.Detail.color=Color.white; // 説明文（Detail）を白色に
    _consumableWindow.Closed=()=>{_consumableWindow=null;if(this){_freezeProgression=_consumablePreviousFreeze;RefreshConsumableButton();}};
    _consumableWindow.Confirm.onClick.AddListener(UseSelectedConsumable);
    _consumableSlot=-1;
    RefreshConsumableInventory();
}
    void RefreshConsumableInventory()
    {
        if(!_consumableWindow)return;
        var s=RunConsumables.Load();
        _consumableWindow.Summary.text=ItemText("1ターン1個まで使用可能・ターンは進みません。敗北するとすべて失います。","Use one relic per turn without ending it. All relics are lost on defeat.","每回合可使用1个，不推进回合。失败后失去全部遗物。");
        _consumableWindow.Items(s.bag,i=>{_consumableSlot=i;_consumableHand=-1;_consumableDiscard=-1;_consumableReplacement=null;RefreshConsumableInventory();},false,null,_consumableSlot);
        _consumableWindow.ClearChoices();
        _consumableWindow.Confirm.interactable=false;
        if(_consumableSlot<0 || _consumableSlot>=s.bag.Count)
        {
            _consumableWindow.Detail.text=s.bag.Count==0?ItemText("遺物を持っていません。強化画面でGoldを使って購入できます。","No relics. Buy them with Gold on the upgrade screen.","尚未持有遗物。可在强化画面消耗Gold购买。"):ItemText("使用したい遺物を選んでください。","Select a relic to use.","请选择要使用的遗物。");
            _consumableWindow.Status.text=ConsumableEffectsSummary(s);return;
        }
        int id=s.bag[_consumableSlot];var d=RunConsumables.Get(id);
        GameManager.ApplyTraitSpriteAssetToTMPAnywhere(_consumableWindow.Detail);
        _consumableWindow.Detail.richText=true;
        _consumableWindow.Detail.text=d.Name+"\n"+GameManager.RenderConsumableDescriptionAnywhere(d.Description);
        string unavailable=ConsumableUnavailable(id,s);
        if(unavailable!=null){_consumableWindow.Status.text=unavailable;return;}
        if(id==17||id==18||id==19) BuildConsumableTargets(id);
        bool ready=id<17||id>19||(_consumableHand>=0&&(id==19?_consumableDiscard>=0:!string.IsNullOrEmpty(_consumableReplacement)));
        _consumableWindow.Confirm.interactable=ready;
        _consumableWindow.Status.text=ready?ItemText("「使用する」で確定します。","Press Use to confirm.","点击「使用」确认。"):ItemText("対象の牌と変更先を選んでください。","Choose the tile and its replacement.","请选择目标牌及替换牌。");
    }
    string ConsumableUnavailable(int id,RunConsumables.State s)
    {
        if(!CanUseConsumableNow())return ItemText("自分のツモ番で使用できます。","Use during your draw turn.","可在己方摸牌回合使用。");
        if(s.usedThisTurn)return ItemText("このターンは使用済みです。","A relic has already been used this turn.","本回合已使用过遗物。");
        bool noEffect=(id==1&&playerHP>=playerMaxHP)||(id==2&&_mp>=EffectiveMaxMP())||
            (id==3&&playerHP>=playerMaxHP&&_mp>=EffectiveMaxMP())||(id==4&&_mp>=EffectiveMaxMP())||
            (id==5&&s.regeneration>0)||(id==6&&s.geki)||(id==7&&s.shun)||(id==8&&s.iyu)||
            (id==10&&s.freeCast)||(id==12&&s.enemySeal>0)||(id==13&&s.shield)||
            (id==14&&_enemySkillPoisonTurnRemaining<=0&&_enemySkillParalysisTurnRemaining<=0)||
            (id==15&&s.effigy)||(id==20&&s.bloodPact);
        if(noEffect)return ItemText("回復不要、または同じ効果が発動中のため使用できません。","No applicable effect, or this effect is already active.","无需恢复，或相同效果已生效，无法使用。");
        if(id>=16&&id<=19&&isRiichi)return ItemText("立直中は使用できません。","Unavailable in riichi.","立直中不可使用。");
        if((id==17||id==18)&&!hand.Any(IsConsumableOrdinaryTile))return ItemText("変更できる通常の数牌がありません。","No ordinary numbered tiles to change.","没有可改变的普通数牌。");
        if(id==19&&!Enumerable.Range(0,discards.Count).Any(IsConsumableDiscardAvailable))return ItemText("交換できる捨て牌がありません。","No available discard to swap.","没有可交换的弃牌。");
        if(id>=6&&id<=8&&!ConsumableHasUnlockedTrait(id==6?SkillSetAsset.Trait.Geki:id==7?SkillSetAsset.Trait.Shun:SkillSetAsset.Trait.Iyu))
            return ItemText("対応するパッシブスキルが未解放です。","No matching passive has been unlocked.","尚未解锁对应被动技能。");
        return null;
    }
    bool ConsumableHasUnlockedTrait(SkillSetAsset.Trait trait)
    {
        string name=ResolveActiveSkillForMP().ToString();
        var host=_skillSet;
        if(!host)host=Resources.LoadAll<SkillSetAsset>("SkillSets").FirstOrDefault(x=>x.activeSkills!=null&&x.activeSkills.Any(a=>a!=null&&a.activeSkillName==name));
        if(!host)return false;
        var all=host.GetTraitYakuFor(name);
        var yaku=trait==SkillSetAsset.Trait.Geki?all.ge:trait==SkillSetAsset.Trait.Shun?all.sh:all.iy;
        return yaku!=null&&yaku.Any(y=>host.GetTraitYakuLevel(name,trait,y)>0);
    }
    bool IsConsumableOrdinaryTile(string tile)
    {
        return !string.IsNullOrEmpty(tile)&&tile==StripTileIdForLogic(tile)&&
            TryToIndex34(tile,out int index)&&index<27;
    }
    bool IsConsumableDiscardAvailable(int index)
    {
        if(index<0||index>=discards.Count)return false;
        if(discardArea)
            foreach(Transform child in discardArea)
                if(child.name.StartsWith("PlayerDiscard_"+index+"_")&&_committedDiscardInstanceIDs.Contains(child.gameObject.GetEntityId()))return false;
        return true;
    }
    void BuildConsumableTargets(int id)
    {
        var w=_consumableWindow;
        for(int i=0;i<hand.Count;i++)
        {
            int index=i; bool valid=id==19||IsConsumableOrdinaryTile(hand[i]);
            var b=ItemTileButton(w.Choices,hand[i],new Vector2((i-(hand.Count-1)*.5f)*83,57),new Vector2(76,90),()=>{_consumableHand=index;_consumableReplacement=null;RefreshConsumableInventory();});
            b.interactable=valid;b.GetComponent<Image>().color=i==_consumableHand?new Color(1,.77f,.25f):Color.white;
        }
        if(_consumableHand<0)return;
        if(id==19)
        {
            var indices=Enumerable.Range(0,discards.Count).Where(IsConsumableDiscardAvailable).ToArray();
            // Up to 48 discards, paged to keep each target readable.
            int pageSize=16, pages=Mathf.Max(1,Mathf.CeilToInt(indices.Length/(float)pageSize));
            _consumableDiscardPage=Mathf.Clamp(_consumableDiscardPage,0,pages-1);
            var page=indices.Skip(_consumableDiscardPage*pageSize).Take(pageSize).ToArray();
            for(int j=0;j<page.Length;j++)
            {
                int index=page[j];var b=ItemTileButton(w.Choices,discards[index],new Vector2((j-(page.Length-1)*.5f)*76,-47),new Vector2(68,82),()=>{_consumableDiscard=index;RefreshConsumableInventory();});
                b.GetComponent<Image>().color=index==_consumableDiscard?new Color(1,.77f,.25f):Color.white;
            }
            if(pages>1){ConsumableWindow.Button("Previous",w.Choices,"‹",new Vector2(-700,-47),new Vector2(64,70),()=>{_consumableDiscardPage--;RefreshConsumableInventory();}).interactable=_consumableDiscardPage>0;
                ConsumableWindow.Button("Next",w.Choices,"›",new Vector2(700,-47),new Vector2(64,70),()=>{_consumableDiscardPage++;RefreshConsumableInventory();}).interactable=_consumableDiscardPage<pages-1;}
            return;
        }
        TryToIndex34(hand[_consumableHand],out int tileIndex);
        int suit=tileIndex/9, rank=tileIndex%9+1;
        var choices=new List<string>();
        string[] suits={"Man","Pin","Sou"};
        if(id==17){for(int s=0;s<3;s++)if(s!=suit)choices.Add(suits[s]+rank);}
        else {if(rank>1)choices.Add(suits[suit]+(rank-1));if(rank<9)choices.Add(suits[suit]+(rank+1));}
        for(int j=0;j<choices.Count;j++)
        {
            string tile=choices[j];var b=ItemTileButton(w.Choices,tile,new Vector2((j-(choices.Count-1)*.5f)*125,-47),new Vector2(78,90),()=>{_consumableReplacement=tile;RefreshConsumableInventory();});
            b.GetComponent<Image>().color=tile==_consumableReplacement?new Color(1,.77f,.25f):Color.white;
        }
    }
    int _consumableDiscardPage;
    Button ItemTileButton(Transform parent,string tile,Vector2 pos,Vector2 size,Action click)
    {
        var b=ConsumableWindow.Button("Tile_"+tile,parent,"",pos,size,click);
        var image=ConsumableWindow.Rect("Artwork",b.transform,Vector2.zero,size-new Vector2(8,8)).gameObject.AddComponent<Image>();
        image.sprite=Resources.Load<Sprite>("Sprites/Tiles/"+tile);image.preserveAspect=true;image.raycastTarget=false;
        if(!image.sprite){image.enabled=false;b.GetComponentInChildren<TMP_Text>().text=tile;}
        return b;
    }
    void UseSelectedConsumable()
    {
        var s=RunConsumables.Load();
        if(_consumableSlot<0||_consumableSlot>=s.bag.Count)return;
        int id=s.bag[_consumableSlot];
        if(ConsumableUnavailable(id,s)!=null){RefreshConsumableInventory();return;}
        if(id>=17&&id<=19)
        {
            if(_consumableHand<0||_consumableHand>=hand.Count)return;
            if(id==19){if(!IsConsumableDiscardAvailable(_consumableDiscard))return;}
            else if(!IsConsumableOrdinaryTile(hand[_consumableHand])||string.IsNullOrEmpty(_consumableReplacement))return;
        }
        int startPlayerHP=playerHP;
        int startPlayerMP=_mp;
        ApplyConsumableEffect(id,s);
        s.bag.RemoveAt(_consumableSlot);s.usedThisTurn=true;RunConsumables.Save(s);
        try { if(AudioManager.Instance) AudioManager.Instance.PlayCutin_PlayerSkill(); } catch {}
        _consumableWindow.Close();
        selHand.Clear();selOffer.Clear();RefreshAll();UpdateSkillInfoUI();UpdateButtons();
        if(startPlayerHP!=playerHP || startPlayerMP!=_mp)
        {
            _consumableDealing=true;
            StartCoroutine(PlayConsumableResourceAnimation(startPlayerHP,startPlayerMP,enemyHP<=0));
        }
        else
        {
            UpdateHpUI();UpdateMpUI();
            if(enemyHP<=0){_lastPlayerWinWasYakumanOrKazoe=false;_freezeProgression=true;phase=Phase.Scoring;__ProceedAfterScoreOK_Internal(false);}
            else TryAutoSaveSuspendSnapshot();
        }
    }

    private System.Collections.IEnumerator PlayConsumableResourceAnimation(int startHP,int startMP,bool enemyDefeated)
    {
        int endHP=Mathf.Clamp(playerHP,0,playerMaxHP);
        int endMP=ClampToEffectiveMaxMP(_mp);
        bool hpDecreased=endHP<startHP;
        bool mpDecreased=endMP<startMP;
        try { if((hpDecreased||mpDecreased)&&AudioManager.Instance) AudioManager.Instance.PlayBattleDamageSE(); } catch {}
        float duration=1f,t=0f;
        while(t<duration)
        {
            t+=Time.deltaTime;
            float p=Mathf.Clamp01(t/duration);
            __UpdatePlayerHpUI_VisualOnly(Mathf.RoundToInt(Mathf.Lerp(startHP,endHP,p)));
            __UpdatePlayerMpUI_VisualOnly(Mathf.RoundToInt(Mathf.Lerp(startMP,endMP,p)));
            yield return null;
        }
        playerHP=endHP;_mp=endMP;UpdateHpUI();UpdateMpUI();_consumableDealing=false;
        if(enemyDefeated){_lastPlayerWinWasYakumanOrKazoe=false;_freezeProgression=true;phase=Phase.Scoring;__ProceedAfterScoreOK_Internal(false);}
        else TryAutoSaveSuspendSnapshot();
    }
    void ApplyConsumableEffect(int id,RunConsumables.State s)
    {
        switch(id)
        {
            case 1: ConsumableHeal(.3f,0);break;
            case 2: ConsumableHeal(0,.4f);break;
            case 3: ConsumableHeal(.2f,.2f);break;
            case 4: playerHP=Mathf.Max(1,playerHP-Mathf.CeilToInt(playerHP*.2f));ConsumableHeal(0,.6f);break;
            case 5: ConsumableHeal(.08f,0);s.regeneration=4;break;
            case 6:s.geki=true;break;case 7:s.shun=true;break;case 8:s.iyu=true;break;
            case 9:s.castsBonus++;break;case 10:s.freeCast=true;break;
            case 11:enemyHP=Mathf.Max(0,enemyHP-1000);break;
            case 12:s.enemySeal=5;break;case 13:s.shield=true;break;
            case 14:_enemySkillPoisonTurnRemaining=0;_enemySkillPoisonDamagePerTurn=0;_enemySkillParalysisTurnRemaining=0;EnemySkills_RefreshStatusEffectsUI();break;
            case 15:s.effigy=true;break;
            case 16:
                var pool=deck.ToList();pool.AddRange(offers);deck.Clear();offers.Clear();
                for(int n=pool.Count-1;n>0;n--){int r=UnityEngine.Random.Range(0,n+1);(pool[n],pool[r])=(pool[r],pool[n]);}
                offers.AddRange(pool.Take(4));for(int n=pool.Count-1;n>=4;n--)deck.Push(pool[n]);break;
            case 17:case 18:hand[_consumableHand]=_consumableReplacement;break;
            case 19:(hand[_consumableHand],discards[_consumableDiscard])=(discards[_consumableDiscard],hand[_consumableHand]);suppressTsumoThisOffer=true;break;
            case 20:s.bloodPact=true;break;
        }
    }
    void ConsumableHeal(float hp,float mp)
    {
        playerHP=Mathf.Min(playerMaxHP,playerHP+Mathf.CeilToInt(playerMaxHP*hp));
        _mp=ClampToEffectiveMaxMP(_mp+Mathf.CeilToInt(EffectiveMaxMP()*mp));
    }
    void ConsumablesOnPlayerTurn()
    {
        var s=RunConsumables.Load();s.usedThisTurn=false;s.castsBonus=0;_consumableBloodPactAppliedThisScoring=false;
        if(s.regeneration>0){s.regeneration--;ConsumableHeal(.08f,0);UpdateHpUI();}
        RunConsumables.Save(s);
    }
    void ConsumablesOnEnemyTurn()
    {
        var s=RunConsumables.Load();_consumableSealThisEnemyTurn=s.enemySeal>0;
        if(s.enemySeal>0){s.enemySeal--;RunConsumables.Save(s);}
    }
    int ConsumablesModifyIncoming(int damage,bool enemyWin)
    {
        var s=RunConsumables.Load();int result=RunConsumables.IncomingDamage(s,damage,playerHP,enemyWin);
        RunConsumables.Save(s);return result;
    }
    void ConsumablesConsumeFreeCast()
    {
        var s=RunConsumables.Load();if(s.freeCast){s.freeCast=false;RunConsumables.Save(s);}
    }
    int ConsumablesModifyOutgoingWin(int damage)
    {
        if(!_currentScoringAttackerIsPlayer)return damage;
        var s=RunConsumables.Load();if(!s.bloodPact)return damage;
        _consumableBloodPactAppliedThisScoring=true;
        s.bloodPact=false;RunConsumables.Save(s);return Mathf.CeilToInt(damage*1.5f);
    }
    string ConsumableEffectsSummary(RunConsumables.State s)
    {
        var list=new List<string>();
        if(s.enemySeal>0)list.Add(RunConsumables.Get(12).Name+" "+s.enemySeal);
        if(s.regeneration>0)list.Add(RunConsumables.Get(5).Name+" "+s.regeneration);
        foreach(var pair in new[]{(s.geki,6),(s.shun,7),(s.iyu,8),(s.freeCast,10),(s.shield,13),(s.effigy,15),(s.bloodPact,20)})if(pair.Item1)list.Add(RunConsumables.Get(pair.Item2).Name);
        return list.Count==0?"":ItemText("効果中：","Active: ","生效中：")+string.Join(" / ",list);
    }

    private void AppendConsumableScoringEffectToPanel(bool playerScoring)
    {
        var state=RunConsumables.Load();
        int itemId=playerScoring?(state.bloodPact||_consumableBloodPactAppliedThisScoring?20:0):(state.shield?13:0);
        if(scoringDefenseIcon_Player)
        {
            scoringDefenseIcon_Player.gameObject.SetActive(itemId!=0);
            scoringDefenseIcon_Player.sprite=itemId==0?null:Resources.Load<Sprite>("Consumables/item_"+itemId.ToString("00"));
            scoringDefenseIcon_Player.preserveAspect=true;
        }
        string effect="";
        if(itemId!=0)
        {
            var def=RunConsumables.Get(itemId);
            effect=def.Name+(playerScoring?" +50%":" -50%");
        }
        var target=playerScoring?scoringEnemySkillEffectValue:scoringEnemySkillEffectValue_Enemy;
        if(target && !string.IsNullOrEmpty(effect))
        {
            string existing=target.text;
            target.text=string.IsNullOrEmpty(existing)||existing=="-"?effect:existing+"\n"+effect;
            target.gameObject.SetActive(true);
        }
    }
}
