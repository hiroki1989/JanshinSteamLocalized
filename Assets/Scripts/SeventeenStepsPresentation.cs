using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SeventeenStepsController
{

    // Preview the actual scoring rules for every possible wait, before committing the hand.
    void RenderHandPreview(List<int> hand){
        var possible=SeventeenStepsRules.Waits(hand).OrderBy(t=>t).ToArray();
        SeventeenStepsUI.InfoBacking(content,new Vector2(0,410),new Vector2(1250,135));
        if(possible.Length==0){SeventeenStepsUI.Label(content,"役・翻数プレビュー　テンパイなし",new Vector2(0,410),new Vector2(1200,100),28);return;}
        bool riichi=SeventeenStepsRules.CanDeclare(hand,doraIndicator,false,UnusedDesignated());
        var scores=possible.Select(t=>SeventeenStepsRules.ApplyDesignatedPenalty(
            SeventeenStepsRules.EvaluateRound(hand,t,riichi,doraIndicator,-1),UnusedDesignated())).ToArray();
        var perWait=scores.Select(w=>System.Text.RegularExpressions.Regex.Replace(w.detail??"",@"\s*\|\s*\d+翻\s*\d+符","")
            .Replace(" + ","\n").Replace("混全帯么九","チャンタ").Replace("純全帯么九","純チャン").Replace("断么九","タンヤオ")
            .Split(new[]{'\n','\r'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct().ToArray()).ToArray();
        var roles=perWait.SelectMany(a=>a).Distinct().ToArray();
        int low=scores.Min(w=>w.han),high=scores.Max(w=>w.han);
        string range=low==high?low+"翻":low+"～"+high+"翻";
        var heading=SeventeenStepsUI.Label(content,"役・翻数　"+range+"　"+(riichi?"リーチ後想定":"リーチなし")+"・裏ドラ等を除く　<color=#FFBA66>橙：待ち次第</color>",new Vector2(0,456),new Vector2(1220,30),24);
        heading.color=Color.white;
        if(roles.Length==0)roles=new[]{"成立役なし"};
        int rows=(roles.Length+2)/3;
        for(int column=0;column<3;column++){
            var lines=roles.Skip(column*rows).Take(rows).Select(role=>perWait.All(a=>a.Contains(role))?role:"<color=#FFBA66>"+role+"（候補）</color>");
            var label=SeventeenStepsUI.Label(content,string.Join("\n",lines),new Vector2((column-1)*410,393),new Vector2(400,90),24);
            label.color=Color.white;label.alignment=TextAlignmentOptions.TopLeft;
            label.enableAutoSizing=true;label.fontSizeMin=6;label.fontSizeMax=24;
            label.overflowMode=TextOverflowModes.Overflow;
        }
    }


    IEnumerator TravelToGod(){
        busy=true;CloseModal();
        modal=SeventeenStepsUI.Rect("JourneyMap",root,Vector2.zero,new Vector2(1920,1080));
        modal.gameObject.AddComponent<Image>().color=new Color(.035f,.075f,.085f,1);
        var fade=modal.gameObject.AddComponent<CanvasGroup>();fade.alpha=0;
        var map=SeventeenStepsUI.Rect("Map",modal,Vector2.zero,new Vector2(1740,820));
        map.gameObject.AddComponent<Image>().color=new Color(.13f,.23f,.23f);
        Frame(map,new Vector2(1740,820));
        // Layered silhouettes and small trees give the route a miniature map appearance.
        for(int i=0;i<15;i++){
            var hill=SeventeenStepsUI.Rect("Mountain",map,new Vector2(-790+i*115,250+(i%3)*38),new Vector2(155,155));
            hill.localRotation=Quaternion.Euler(0,0,45);hill.gameObject.AddComponent<Image>().color=new Color(.17f+i%2*.03f,.30f,.29f);
        }
        for(int i=0;i<28;i++){
            float x=-780+(i*157)%1560,y=-280+(i*113)%430;
            var tree=SeventeenStepsUI.Rect("Forest",map,new Vector2(x,y),new Vector2(19,32));tree.localRotation=Quaternion.Euler(0,0,45);
            tree.gameObject.AddComponent<Image>().color=new Color(.08f,.17f,.16f,.75f);
        }
        var nodes=new[]{new Vector2(-600,-65),new Vector2(-205,95),new Vector2(205,-70),new Vector2(600,70)};
        for(int leg=0;leg<3;leg++)for(int j=0;j<22;j++){
            var dot=SeventeenStepsUI.Rect("Path",map,Vector2.Lerp(nodes[leg],nodes[leg+1],j/21f),new Vector2(12,8));
            dot.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(nodes[leg+1].y-nodes[leg].y,nodes[leg+1].x-nodes[leg].x)*Mathf.Rad2Deg);
            dot.gameObject.AddComponent<Image>().color=new Color(.83f,.71f,.43f,.8f);
        }
        int current=SeventeenStepsMode.EnemyIndex;
        for(int i=0;i<4;i++){
            var shrine=SeventeenStepsUI.Rect("Shrine",map,nodes[i],new Vector2(80,36));shrine.gameObject.AddComponent<Image>().color=i<current?new Color(.52f,.46f,.28f):new Color(.78f,.70f,.50f);
            var god=SeventeenStepsUI.Picture(map,SeventeenStepsUI.EnemyArt(i),nodes[i]+new Vector2(0,108),new Vector2(170,200));god.color=i==current?Color.white:new Color(.65f,.7f,.7f,.6f);
            var name=SeventeenStepsUI.Label(map,SeventeenStepsMode.Enemies[i],nodes[i]+new Vector2(0,-58),new Vector2(260,48),29);SeventeenStepsUI.BlackOutline(name);
        }
        var heading=SeventeenStepsUI.Label(modal,"神々の道",new Vector2(0,455),new Vector2(900,70),48);SeventeenStepsUI.BlackOutline(heading);
        var caption=SeventeenStepsUI.Label(modal,SeventeenStepsMode.Enemies[current]+"のもとへ",new Vector2(0,-455),new Vector2(1100,65),36);
        var walker=SeventeenStepsUI.Picture(map,SeventeenStepsUI.CharacterArt(SeventeenStepsMode.SelectedCharacter),Vector2.zero,new Vector2(170,205));
        Vector2 from=current==0?new Vector2(-810,-180):nodes[current-1];Vector2 to=nodes[current]+new Vector2(-95,0);
        float elapsed=0;
        while(elapsed<7.5f){
            elapsed+=Time.unscaledDeltaTime;fade.alpha=Mathf.Min(Mathf.Clamp01(elapsed/.5f),Mathf.Clamp01((7.5f-elapsed)/.5f));
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.7f)/5.2f));
            walker.rectTransform.anchoredPosition=Vector2.Lerp(from,to,t)+new Vector2(0,75+(t>0&&t<1?Mathf.Abs(Mathf.Sin(elapsed*9))*13:0));
            walker.rectTransform.localRotation=Quaternion.Euler(0,0,t>0&&t<1?Mathf.Sin(elapsed*9)*3:0);
            if(elapsed>6)caption.text=SeventeenStepsMode.Enemies[current]+"との対局";
            yield return null;
        }
        CloseModal();yield return StartRound();
    }

    int doraIndicator,uraIndicator;
    bool playerRiichi,enemyRiichi,missedRon,dealing;
    int ronDecision;
    const float DealGroupInterval=.5f;
    Sprite BackSprite=>Resources.Load<Sprite>("Tiles/Back");
    Sprite CutinArt(bool player)=>player?SeventeenStepsUI.CharacterArt(SeventeenStepsMode.SelectedCharacter):SeventeenStepsUI.EnemyArt(SeventeenStepsMode.EnemyIndex);
    Sprite ResultArt(bool won)=>SeventeenStepsUI.CharacterArt(SeventeenStepsMode.SelectedCharacter);
    IEnumerator ShowCutin(string title,Sprite sprite,Action sound,float duration){
        CloseModal();modal=SeventeenStepsUI.Rect("CutinOnly",root,Vector2.zero,new Vector2(10000,10000));modal.gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);sound?.Invoke();
        // Normal RiichiCutinRoot uses a 2283 x 932 image on the 1920 x 1080 canvas.
        var art=SeventeenStepsUI.Picture(modal,sprite,Vector2.zero,new Vector2(900,900));
        var label=SeventeenStepsUI.Label(modal,title,new Vector2(0,sprite?-82:0),new Vector2(1680,210),110);
        StyleCutinText(label);
        Sprite[] frames=sprite&&sprite.name.StartsWith("RandomMan_frame_")?Resources.LoadAll<Sprite>("PlayerCutins").Where(s=>s.name.StartsWith("RandomMan_frame_")).OrderBy(s=>s.name).ToArray():Array.Empty<Sprite>();
        float start=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-start<duration){float elapsed=Time.realtimeSinceStartup-start;art.rectTransform.anchoredPosition=new Vector2(Mathf.Lerp(-260,0,Mathf.Clamp01(elapsed/.25f)),0);if(frames.Length>0)art.sprite=frames[Math.Min(frames.Length-1,(int)(elapsed*10))];yield return null;}
        CloseModal();yield return new WaitForSecondsRealtime(.25f);
    }
    static void StyleCutinText(TMP_Text text){
        text.color=Color.white;
        var material=new Material(text.fontSharedMaterial);
        material.SetColor(ShaderUtilities.ID_FaceColor,Color.white);
        material.SetColor(ShaderUtilities.ID_OutlineColor,Color.black);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth,.22f);
        material.EnableKeyword("OUTLINE_ON");text.fontMaterial=material;text.outlineColor=Color.black;text.outlineWidth=.22f;
        text.UpdateMeshPadding();
    }
    IEnumerator DealAnimation(){
        for(int dealt=0;dealt<34;dealt+=4){
            int count=Math.Min(4,34-dealt);SeventeenStepsUI.Clear(content);
            status.text="";
            SeventeenStepsUI.TablePanel(content,new Vector2(0,300),new Vector2(1250,76),true);
            SeventeenStepsUI.TablePanel(content,new Vector2(0,188),new Vector2(1250,143),true);
            SeventeenStepsUI.TablePanel(content,new Vector2(0,30),new Vector2(1250,110));
            SeventeenStepsUI.TablePanel(content,new Vector2(0,-160),new Vector2(1250,250));
            RenderWinds();
            for(int i=0;i<dealt;i++){
                Tile(content,deck[i],DealPosition(i,false),new Vector2(63,86),null);
                SeventeenStepsUI.Picture(content,BackSprite,DealPosition(i,true),new Vector2(55,72));
            }
            for(int i=0;i<12;i++)SeventeenStepsUI.Picture(content,BackSprite,new Vector2(-360+i*42,65),new Vector2(40,56));
            var flying=new List<RectTransform>();var destinations=new List<Vector2>();
            for(int side=0;side<2;side++)for(int j=0;j<count;j++){
                var img=SeventeenStepsUI.Picture(content,side==0?Resources.Load<Sprite>("Sprites/Tiles/"+SeventeenStepsRules.Tiles[deck[dealt+j]]):BackSprite,new Vector2(-140,65),new Vector2(63,86));
                flying.Add(img.rectTransform);destinations.Add(DealPosition(dealt+j,side==1));
            }
            AudioManager.Instance?.PlayOpeningHandDealGroupSE();float start=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-start<DealGroupInterval){float t=Mathf.SmoothStep(0,1,(Time.realtimeSinceStartup-start)/.32f);for(int k=0;k<flying.Count;k++)flying[k].anchoredPosition=Vector2.Lerp(new Vector2(-140,65),destinations[k],t);yield return null;}
        }
        yield return new WaitForSecondsRealtime(.35f);
    }
    Vector2 DealPosition(int i,bool enemy)=>new Vector2(-560+i%17*70,enemy?220-i/17*76:-100-i/17*120);
    void RenderOpponent(bool construction){
        SeventeenStepsUI.TablePanel(content,new Vector2(0,300),new Vector2(1250,76),true);
        SeventeenStepsUI.TablePanel(content,new Vector2(0,188),new Vector2(1250,143),true);
        for(int i=0;i<(construction&&!enemyReady?0:13);i++)SeventeenStepsUI.Picture(content,BackSprite,new Vector2(-336+i*56,300),new Vector2(48,65));
        int count=construction?(enemyReady?21:34):(enemyCandidates?.Count??21);
        for(int i=0;i<count;i++)SeventeenStepsUI.Picture(content,BackSprite,new Vector2(-560+i%17*70,220-i/17*67),new Vector2(44,58));
    }
    void RenderIndicators(Transform host,bool revealUra=false){
        SeventeenStepsUI.InfoBacking(host,new Vector2(800,-150),new Vector2(280,45));
        SeventeenStepsUI.Label(host,"ドラ表示牌",new Vector2(800,-150),new Vector2(270,40),23);
        Tile(host,doraIndicator,new Vector2(755,-212),new Vector2(52,72),null);
        if(revealUra)Tile(host,uraIndicator,new Vector2(835,-212),new Vector2(52,72),null);
        else SeventeenStepsUI.Picture(host,BackSprite,new Vector2(835,-212),new Vector2(52,72));
    }
    IEnumerator NormalRonCutin(bool player){
        CloseModal();modal=SeventeenStepsUI.Rect("RonCutinOnce",root,Vector2.zero,new Vector2(10000,10000));modal.gameObject.AddComponent<Image>().color=new Color(0,0,0,.65f);
        var prefab=Resources.Load<GameObject>("SeventeenSteps/NormalWinCutin");
        if(!prefab){Debug.LogError("NormalWinCutin prefab is missing");CloseModal();yield break;}
        var cutin=Instantiate(prefab,modal,false);cutin.SetActive(true);
        var art=cutin.GetComponentsInChildren<Image>(true).First(i=>i.name=="WinCutinPortrait");art.sprite=CutinArt(player);art.enabled=art.sprite;art.preserveAspect=true;art.rectTransform.sizeDelta=new Vector2(900,900);art.rectTransform.anchoredPosition=Vector2.zero;
        var text=cutin.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="WinCutinLabel");text.text="ロン";StyleCutinText(text);
        var group=cutin.GetComponent<CanvasGroup>();if(!group)group=cutin.AddComponent<CanvasGroup>();
        if(player){group.alpha=1;AudioManager.Instance?.PlayCutin_PlayerRon();yield return new WaitForSecondsRealtime(3f);}
        else{AudioManager.Instance?.PlayCutin_EnemyRon();group.alpha=0;yield return new WaitForSecondsRealtime(.3f);yield return FadeRon(group,0,1,.1f);yield return new WaitForSecondsRealtime(1f);yield return FadeRon(group,1,0,.1f);}
        CloseModal();
    }
    IEnumerator FadeRon(CanvasGroup group,float from,float to,float seconds){float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<seconds){group.alpha=Mathf.Lerp(from,to,(Time.realtimeSinceStartup-start)/seconds);yield return null;}group.alpha=to;}
    IEnumerator BeginDiscardPhase(){
        yield return new WaitForSecondsRealtime(.4f);yield return EnemyTurn();
    }
    IEnumerator RonChoice(SeventeenStepsRules.Win win,int tile){
        busy=true;playerTurn=false;ronDecision=0;highlightedRonRiver=1;RenderBattle();CloseModal();
        modal=SeventeenStepsUI.Rect("RonActions",root,Vector2.zero,new Vector2(1920,1080));
        SeventeenStepsUI.Navigation(modal,"ロン",new Vector2(380,-480),new Vector2(250,70),()=>ChooseRon(true));
        SeventeenStepsUI.Navigation(modal,"スキップ",new Vector2(45,-480),new Vector2(250,70),()=>ChooseRon(false));
        while(ronDecision==0)yield return null;CloseModal();
        if(ronDecision==1){yield return FinishRound(true,win,tile);yield break;}
        highlightedRonRiver=0;missedRon=true;playerTurn=true;busy=false;notice="ロン見送り：この局はフリテン";RenderBattle();
    }
    void ChooseRon(bool accept){if(ronDecision==0)ronDecision=accept?1:2;}
    IEnumerator EnemyRonAfterPause(SeventeenStepsRules.Win win,int tile){highlightedRonRiver=2;RenderBattle();yield return new WaitForSecondsRealtime(.8f);yield return FinishRound(false,win,tile);}
    void DecorateOfuda(Button button,SeventeenStepsOfuda.Definition d){
        var host=button.transform;button.GetComponent<Image>().color=new Color(.12f,.15f,.16f);
        var art=SeventeenStepsUI.Picture(host,null,new Vector2(-110,15),new Vector2(140,210));ItemArtwork.Ofuda(art,d.Rarity);
        Frame(host,new Vector2(405,310));
        var rarity=SeventeenStepsUI.Label(host,d.Rarity,new Vector2(70,110),new Vector2(220,48),27);OfudaRarityColors.Apply(rarity,d.Rarity);
        SeventeenStepsUI.Label(host,d.name+"のお札",new Vector2(70,45),new Vector2(220,90),29);
        SeventeenStepsUI.Label(host,d.Description,new Vector2(70,-72),new Vector2(220,120),25);
    }
    static void Frame(Transform host,Vector2 size){var frame=SeventeenStepsUI.Picture(host,Resources.Load<Sprite>("Consumables/PanelFrame"),Vector2.zero,size);frame.type=Image.Type.Sliced;frame.preserveAspect=false;frame.pixelsPerUnitMultiplier=5;}
    IEnumerator ScorePresentation(bool playerWon,SeventeenStepsRules.Win win,int tile,int points){
        NewModal(win==null?"流局":playerWon?"和了・点数計算":"敵の和了・点数計算");
        if(win!=null){
            var winner=SeventeenStepsUI.Picture(modal,CutinArt(playerWon),new Vector2(590,-105),new Vector2(210,200));
            winner.name="WinningCharacter";winner.raycastTarget=false;
        }
        var shownHand=win!=null&&playerWon?playerHand:enemyHand;
        for(int i=0;i<shownHand.Count;i++)Tile(modal,shownHand[i],new Vector2(-560+i*77,210),new Vector2(66,90),null);
        if(tile>=0)Tile(modal,tile,new Vector2(540,210),new Vector2(66,90),null,true);
        if(win==null){SeventeenStepsUI.Label(modal,(playerRiichi!=enemyRiichi?(playerRiichi?"プレイヤー　+1,000点":"敵　+1,000点"):"両者　0点"),new Vector2(0,15),new Vector2(1180,160),35);}
        else{
            SeventeenStepsUI.Label(modal,"ドラ表示牌",new Vector2(-500,110),new Vector2(260,45),25);Tile(modal,doraIndicator,new Vector2(-520,28),new Vector2(62,84),null);
            bool riichi=playerWon?playerRiichi:enemyRiichi;
            if(riichi){SeventeenStepsUI.Label(modal,"裏ドラ表示牌",new Vector2(-500,-60),new Vector2(260,45),25);Tile(modal,uraIndicator,new Vector2(-520,-140),new Vector2(62,84),null);}
            string summary=System.Text.RegularExpressions.Regex.Replace(win.detail??"",@"\s*\|\s*\d+翻\s*\d+符","").Replace(" + ","\n");
            summary=summary.Replace("混全帯么九","チャンタ").Replace("純全帯么九","純チャン").Replace("断么九","タンヤオ");
            string[] lines=summary.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).OrderBy(line=>line.StartsWith("リーチ")||line.StartsWith("立直")?0:line.StartsWith("一発")?1:line.StartsWith("ドラ")?3:line.StartsWith("裏ドラ")?4:line.StartsWith("指定牌")?5:2).ToArray();
            var yaku=SeventeenStepsUI.Label(modal,"",new Vector2(-40,10),new Vector2(590,290),27);yaku.alignment=TextAlignmentOptions.TopLeft;yaku.fontSizeMin=16;
            // Reveal each yaku before the hand total, then talismans and final points.
            foreach(var line in lines){yield return new WaitForSecondsRealtime(1f);yaku.text+=line+"\n";ScoreSound(win.points);}
            yield return new WaitForSecondsRealtime(1f);
            SeventeenStepsUI.Label(modal,win.han+"翻　"+win.fu+"符\n基本点　"+win.points.ToString("N0"),new Vector2(420,65),new Vector2(400,130),34);ScoreSound(win.points);
            yield return new WaitForSecondsRealtime(1f);
            var triggered=playerWon?SeventeenStepsMode.Current.ofuda.Select(id=>SeventeenStepsOfuda.All[id]).Where(d=>win.keys.Contains(d.key)).ToArray():Array.Empty<SeventeenStepsOfuda.Definition>();
            SeventeenStepsUI.Label(modal,triggered.Length==0?"お札加点　なし":string.Join("\n",triggered.Select(d=>d.name+" ×"+d.Multiplier.ToString("0.0"))),new Vector2(280,-75),new Vector2(270,160),28);ScoreSound(win.points);
            yield return new WaitForSecondsRealtime(1f);ScoreSound(points);
            SeventeenStepsUI.Label(modal,"獲得点数　"+points.ToString("N0")+"点",new Vector2(0,-250),new Vector2(1200,75),42);
        }
        SeventeenStepsUI.Button(modal,SeventeenStepsMode.EnemyMatchFinished?"対局結果":"次の局へ",new Vector2(0,-325),new Vector2(430,65),()=>{AudioManager.Instance?.PlayScoringPanelOkSE();Continue();});
    }
    void ScoreSound(int points){if(points>=32000)AudioManager.Instance?.PlayScoringStepYakumanOrAboveSE();else if(points>=8000)AudioManager.Instance?.PlayScoringStepManganToYakumanSE();else AudioManager.Instance?.PlayScoringStepUnderManganSE();}
}
