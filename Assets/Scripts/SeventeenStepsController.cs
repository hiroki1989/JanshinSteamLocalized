using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class SeventeenStepsController : MonoBehaviour
{
    public static SeventeenStepsController Active {get;private set;}
    public const int SkillUsesPerRound=2;
    public const string TutorialDoneKey="SeventeenStepsTutorialDoneV1";
    bool enemyMissedRon;
    readonly System.Random random=new System.Random();
    RectTransform root,content;TextMeshProUGUI status,score;Image portrait,playerPortrait;
    List<int> deck,enemyDeck,enemyHand,enemyCandidates;
    readonly HashSet<int> selected=new HashSet<int>(),required=new HashSet<int>();
    readonly List<int> candidates=new List<int>(),playerDiscards=new List<int>(),enemyDiscards=new List<int>();
    List<int> playerHand=new List<int>();HashSet<int> waits=new HashSet<int>(),enemyWaits=new HashSet<int>();
    bool building,busy,roundEnded,playerTurn,enemyReady;int turn,skillUses,target=-1,tutorialPage,dyeSuit;
    readonly Dictionary<int,int> designatedValues=new Dictionary<int,int>();
    RectTransform modal;string notice="";int offersRemaining;SeventeenStepsOfuda.Definition[] offers;
    public static void Open()
    {
        var previous=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("SeventeenStepsScene");SceneManager.SetActiveScene(scene);
        var owner=new GameObject("SeventeenStepsController");SceneManager.MoveGameObjectToScene(owner,scene);
        owner.AddComponent<SeventeenStepsController>().Begin();SceneManager.UnloadSceneAsync(previous);
    }
    public void Begin()
    {
        Active=this;Time.timeScale=1;root=SeventeenStepsUI.CreateCanvas(transform,"外伝モード");
        var shade=SeventeenStepsUI.Rect("BattleDimmer",root,Vector2.zero,new Vector2(10000,10000)).gameObject.AddComponent<Image>();shade.color=new Color(0,0,0,.38f);shade.raycastTarget=false;shade.transform.SetSiblingIndex(2);
        content=SeventeenStepsUI.Rect("Board",root,Vector2.zero,new Vector2(1920,1080));
        score=SeventeenStepsUI.Label(root,"",new Vector2(800,385),new Vector2(290,80),32);
        status=SeventeenStepsUI.Label(root,"",new Vector2(0,-450),new Vector2(1250,40),27);
        portrait=SeventeenStepsUI.Picture(root,null,new Vector2(-800,185),new Vector2(300,350));portrait.color=Color.clear;
        SeventeenStepsUI.Navigation(root,"遊び方",new Vector2(790,490),new Vector2(230,58),()=>{if(!busy&&!modal)Tutorial(0);});

        SeventeenStepsUI.Navigation(root,"終了する",new Vector2(770,-470),new Vector2(230,58),ConfirmExit);
        string art=SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.DyeMaster?"RandomMan_victory":SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.Calligrapher?"RandomHonor_victory":"Capitalist_victory";
        playerPortrait=SeventeenStepsUI.Picture(root,SeventeenStepsUI.CharacterArt(SeventeenStepsMode.SelectedCharacter),new Vector2(800,155),new Vector2(290,350));
        ShowOfudaOffers();
    }
    void OnDestroy(){if(Active==this)Active=null;}
    void NewModal(string title){CloseModal();modal=SeventeenStepsUI.Modal(root,title);}
    void CloseModal(){if(modal){modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;}}
    void Tutorial(int page)
    {
        var settings=GaidenUISettings.Current;
        var pages=settings.tutorialPages;
        if(pages==null||pages.Length==0){CloseModal();if(deck==null)ShowOfudaOffers();return;}
        page=Mathf.Clamp(page,0,pages.Length-1);
        tutorialPage=page;NewModal(settings.tutorialTitle+"　"+(page+1)+" / "+pages.Length);
        var body=SeventeenStepsUI.Label(modal,pages[page],new Vector2(0,20),new Vector2(1320,430),42);body.alignment=TextAlignmentOptions.TopLeft;body.fontSizeMin=36;
        if(page>0)SeventeenStepsUI.Button(modal,settings.previousLabel,new Vector2(-370,-260),new Vector2(270,70),()=>Tutorial(page-1));
        SeventeenStepsUI.Button(modal,page==pages.Length-1?settings.startLabel:settings.nextLabel,new Vector2(370,-260),new Vector2(270,70),()=>{if(page<pages.Length-1)Tutorial(page+1);else{PlayerPrefs.SetInt(TutorialDoneKey,1);PlayerPrefs.Save();CloseModal();if(deck==null)ShowOfudaOffers();}});
    }
    void ShowOfudaOffers()
    {
        portrait.sprite=SeventeenStepsUI.EnemyArt(SeventeenStepsMode.EnemyIndex);portrait.color=portrait.sprite?Color.white:Color.clear;
        AudioManager.Instance?.PlaySeventeenBattleBgm(SeventeenStepsMode.IsFinalEnemy);
        offers=SeventeenStepsOfuda.Offer(random,SeventeenStepsMode.Current.ofuda);
        offersRemaining=SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.Capitalist?2:1;
        RenderOffers();UpdateScore();
    }
    void RenderOffers()
    {
        NewModal("お札を選択　あと"+offersRemaining+"枚 / 装備上限3枚");
        for(int i=0;i<offers.Length;i++){
            var d=offers[i];if(d==null)continue;var b=SeventeenStepsUI.Button(modal,"",new Vector2((i-1)*440,30),new Vector2(405,310),()=>ChooseOfuda(d));
            DecorateOfuda(b,d);
        }
        for(int i=0;i<SeventeenStepsMode.Current.ofuda.Count;i++){
            int slot=i;var equipped=SeventeenStepsOfuda.All[SeventeenStepsMode.Current.ofuda[i]];
            SeventeenStepsUI.Button(modal,equipped.name+" ×"+equipped.Multiplier.ToString("0.0")+"　破棄",new Vector2((i-1)*440,-205),new Vector2(405,64),()=>ConfirmDiscardOfuda(slot));
        }
        SeventeenStepsUI.Button(modal,"獲得せず進む",new Vector2(0,-290),new Vector2(330,65),()=>{CloseModal();StartCoroutine(TravelToGod());});
    }
    void ConfirmDiscardOfuda(int slot){
        if(slot<0||slot>=SeventeenStepsMode.Current.ofuda.Count)return;
        var d=SeventeenStepsOfuda.All[SeventeenStepsMode.Current.ofuda[slot]];
        NewModal("装備お札を破棄？");SeventeenStepsUI.Label(modal,d.name+" ×"+d.Multiplier.ToString("0.0")+"\n破棄したお札は戻らない",new Vector2(0,60),new Vector2(1100,160),34);
        SeventeenStepsUI.Button(modal,"戻る",new Vector2(-260,-220),new Vector2(330,70),RenderOffers);
        SeventeenStepsUI.Button(modal,"破棄",new Vector2(260,-220),new Vector2(330,70),()=>{if(slot<SeventeenStepsMode.Current.ofuda.Count){SeventeenStepsMode.Current.ofuda.RemoveAt(slot);SeventeenStepsMode.Save();}RenderOffers();});
    }
    void ChooseOfuda(SeventeenStepsOfuda.Definition d)
    {
        if(d==null||offers==null||!offers.Contains(d)||offersRemaining<=0)return;
        if(SeventeenStepsMode.Current.ofuda.Count<3){TakeOfuda(d,-1);return;}
        NewModal("入れ替えるお札を選択");
        for(int i=0;i<3;i++){int slot=i;var old=SeventeenStepsOfuda.All[SeventeenStepsMode.Current.ofuda[i]];SeventeenStepsUI.Button(modal,old.Description,new Vector2((i-1)*440,30),new Vector2(405,200),()=>TakeOfuda(d,slot));}
        SeventeenStepsUI.Button(modal,"戻る",new Vector2(0,-260),new Vector2(300,65),RenderOffers);
    }
    void TakeOfuda(SeventeenStepsOfuda.Definition d,int replace)
    {
        if(d==null||offers==null||!offers.Contains(d)||offersRemaining<=0)return;
        if(replace>=0)SeventeenStepsMode.Current.ofuda[replace]=d.id;else SeventeenStepsMode.Current.ofuda.Add(d.id);
        SeventeenStepsMode.Save();offers[Array.IndexOf(offers,d)]=null;
        if(--offersRemaining>0){RenderOffers();return;}CloseModal();StartCoroutine(TravelToGod());
    }
    IEnumerator StartRound()
    {
        building=true;busy=false;enemyReady=false;roundEnded=false;playerTurn=false;turn=0;skillUses=SkillUsesPerRound;target=-1;notice="";
        selected.Clear();required.Clear();candidates.Clear();playerDiscards.Clear();enemyDiscards.Clear();
        SeventeenStepsRules.Deal(random,out deck,out enemyDeck,out doraIndicator,out uraIndicator);playerRiichi=false;enemyRiichi=false;missedRon=false;enemyMissedRon=false;dealing=true;playerHand.Clear();
        var indexes=Enumerable.Range(0,34).ToList();SeventeenStepsRules.Shuffle(indexes,random);
        designatedValues.Clear();foreach(int i in indexes.Take(SeventeenStepsMode.RequiredTiles)){required.Add(i);designatedValues[i]=deck[i];}
        SeventeenStepsUI.Clear(content);status.text="配牌";
        // Pure enumeration and key-only scoring run off-thread while the player builds a hand.
        var enemyCopy=enemyDeck.ToArray();int rank=SeventeenStepsMode.EnemyHandRank;
        var task=Task.Run(()=>SeventeenStepsRules.SelectRank(SeventeenStepsRules.TenpaiShapes(enemyCopy).Select(shape=>SeventeenStepsRules.RankWithDora(shape,doraIndicator)),rank));
        yield return ShowCutin("東"+SeventeenStepsMode.Round+"局",null,()=>AudioManager.Instance?.PlaySE(Resources.Load<AudioClip>("Audio/バーン")),1.3f);
        yield return DealAnimation();dealing=false;RenderSelection();
        if(PlayerPrefs.GetInt(TutorialDoneKey,0)==0){Tutorial(0);while(modal)yield return null;}
        while(!task.IsCompleted)yield return null;
        if(task.IsFaulted){Debug.LogException(task.Exception);status.text="敵の構築に失敗。終了して再試行。";yield break;}
        var best=task.Result;
        enemyHand=best?.hand??enemyDeck.Take(13).ToList();enemyCandidates=enemyDeck.ToList();foreach(int t in enemyHand)enemyCandidates.Remove(t);
        enemyWaits=SeventeenStepsRules.Waits(enemyHand);enemyRiichi=false;enemyReady=true;busy=false;RenderSelection();
    }
    string CharacterName=>SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.DyeMaster?"染色師":SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.Calligrapher?"書家":"資産家";
    void UpdateScore(){score.text="";}
    void RenderSelection()
    {
        SeventeenStepsUI.Clear(content);UpdateScore();
        status.text=notice;
        RenderOpponent(true);RenderIndicators(content);RenderWinds();
        SeventeenStepsUI.TablePanel(content,new Vector2(0,30),new Vector2(1250,110));
        SeventeenStepsUI.TablePanel(content,new Vector2(0,-160),new Vector2(1250,250));
        int n=0;foreach(int i in selected.OrderBy(i=>deck[i])){int idx=i;var tile=Tile(content,deck[i],new Vector2(-438+n++*73,30),new Vector2(65,92),()=>{AudioManager.Instance?.PlaySelectTileSE();target=idx;notice="";RenderSelection();},required.Contains(i));HighlightTarget(tile,target==idx);}
        var sortedDeck=Enumerable.Range(0,34).OrderBy(i=>deck[i]).ThenBy(i=>i).ToArray();
        for(int i=0;i<34;i++){int idx=sortedDeck[i];var tile=Tile(content,deck[idx],new Vector2(-560+i%17*70,-100-i/17*120),new Vector2(62,90),()=>Toggle(idx),required.Contains(idx));if(selected.Contains(idx))tile.GetComponent<Image>().color=new Color(.65f,.78f,.7f);HighlightTarget(tile,target==idx);}
        if(selected.Count==13){var hand=selected.OrderBy(i=>deck[i]).ThenBy(i=>i).Select(i=>deck[i]).ToList();RenderWaitTiles(hand);RenderHandPreview(hand);}
        var confirm=SeventeenStepsUI.Navigation(content,enemyReady?"リーチ":"敵の構築中",new Vector2(-340,-480),new Vector2(350,70),ConfirmHand);bool ready=selected.Count==13&&SeventeenStepsRules.CanDeclare(selected.Select(i=>deck[i]).ToList(),doraIndicator,false,UnusedDesignated());confirm.interactable=!busy&&enemyReady&&ready;
        if(selected.Count==13&&!ready)SeventeenStepsUI.Navigation(content,"リーチせず開始",new Vector2(-340,-480),new Vector2(350,70),ConfirmHand).interactable=!busy&&enemyReady;
        var cancel=SeventeenStepsUI.Navigation(content,"選択を全解除",new Vector2(50,-480),new Vector2(300,70),()=>{if(busy||dealing)return;selected.Clear();target=-1;notice="";RenderSelection();});cancel.interactable=!busy&&!dealing;
        SkillButton();
    }
    int UnusedDesignated()=>required.Count(i=>!selected.Contains(i)||(designatedValues.TryGetValue(i,out int original)&&deck[i]!=original));
    void HighlightTarget(Button tile,bool active){if(!active)return;var mark=tile.gameObject.AddComponent<Outline>();mark.effectColor=new Color(1,.05f,.05f);mark.effectDistance=new Vector2(5,-5);tile.GetComponent<Image>().color=new Color(1,.6f,.6f);}
    void Toggle(int i){if(busy||dealing)return;AudioManager.Instance?.PlaySelectTileSE();notice="";target=i;if(selected.Contains(i))selected.Remove(i);else if(selected.Count<13)selected.Add(i);RenderSelection();}
    void ConfirmHand()
    {
        if(busy||dealing||!building||!enemyReady||selected.Count!=13)return;
        playerHand=selected.OrderBy(i=>deck[i]).ThenBy(i=>i).Select(i=>deck[i]).ToList();candidates.Clear();candidates.AddRange(Enumerable.Range(0,34).Where(i=>!selected.Contains(i)).Select(i=>deck[i]));
        waits=SeventeenStepsRules.Waits(playerHand);building=false;target=-1;busy=true;playerRiichi=false;RenderBattle();StartCoroutine(BeginDiscardPhase());
    }
    void SkillButton(){
        if(SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.Capitalist)return;
        bool available=!dealing&&!playerRiichi&&!busy&&!roundEnded&&skillUses>0&&(building||playerTurn);
        if(SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.DyeMaster){
            for(int i=0;i<3;i++){int suit=i;var b=Tile(content,i*9,new Vector2(725+i*65,-400),new Vector2(52,72),()=>{AudioManager.Instance?.PlayClickSE();dyeSuit=suit;if(building)RenderSelection();else RenderBattle();});b.interactable=available;HighlightTarget(b,dyeSuit==i);}
        }
        var button=SeventeenStepsUI.Navigation(content,(SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.DyeMaster?"色寄せ":"筆写")+"　"+skillUses+"/"+SkillUsesPerRound,new Vector2(780,-320),new Vector2(310,75),UseSkill);button.interactable=available&&target>=0;
    }
    void UseSkill(){
        if(dealing||playerRiichi||busy||roundEnded||(!building&&!playerTurn)||SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.Capitalist||skillUses<=0||target<0)return;
        var indices=selected.OrderBy(i=>deck[i]).ThenBy(i=>i).ToList();if(!building&&target>=indices.Count)return;int index=building?target:indices[target];
        if(!building&&!selected.Contains(index))return;
        int tile=deck[index];var pool=SeventeenStepsMode.SelectedCharacter==SeventeenStepsMode.Character.Calligrapher?Enumerable.Range(27,7):Enumerable.Range(dyeSuit*9,9);
        var choices=pool.Where(t=>t!=tile&&deck.Count(v=>v==t)<4).ToArray();if(choices.Length==0){notice="変換できる牌なし";return;}
        int next=choices[random.Next(choices.Length)];deck[index]=next;skillUses--;AudioManager.Instance?.PlayPlayerSkillTransformSE();notice=TileLabel(tile)+" → "+TileLabel(next);
        if(building)RenderSelection();else{indices=selected.OrderBy(i=>deck[i]).ThenBy(i=>i).ToList();target=indices.IndexOf(index);playerHand=indices.Select(i=>deck[i]).ToList();waits=SeventeenStepsRules.Waits(playerHand);RenderBattle();}
    }
    void RenderBattle(){
        SeventeenStepsUI.Clear(content);UpdateScore();status.text=notice;
        RenderOpponent(false);RenderIndicators(content);RenderWinds();
        SeventeenStepsUI.TablePanel(content,new Vector2(0,75),new Vector2(1250,70),true);
        SeventeenStepsUI.TablePanel(content,new Vector2(0,0),new Vector2(1250,70));
        River(enemyDiscards,75);River(playerDiscards,0);
        SeventeenStepsUI.TablePanel(content,new Vector2(0,-83),new Vector2(1250,90));
        SeventeenStepsUI.TablePanel(content,new Vector2(0,-235),new Vector2(1250,205));
        for(int i=0;i<playerHand.Count;i++){int index=i;var tile=Tile(content,playerHand[i],new Vector2(-408+i*68,-83),new Vector2(60,80),()=>{AudioManager.Instance?.PlaySelectTileSE();target=index;RenderBattle();});HighlightTarget(tile,target==index);}
        for(int i=0;i<candidates.Count;i++){int index=i;var tile=Tile(content,candidates[i],new Vector2(-500+i%11*100,-185-i/11*92),new Vector2(59,80),()=>Discard(index));tile.interactable=playerTurn&&!busy&&!roundEnded;}
        RenderWaitTiles(playerHand);SkillButton();
    }
    RectTransform enemyRonTarget,playerRonTarget;
    int highlightedRonRiver; // 1: enemy discard (player ron), 2: player discard (enemy ron)
    void River(List<int> tiles,float y){
        for(int i=0;i<tiles.Count;i++){
            var tile=Tile(content,tiles[i],new Vector2(-560+i*70,y),new Vector2(43,60),null);
            if(i==tiles.Count-1){if(ReferenceEquals(tiles,enemyDiscards))enemyRonTarget=(RectTransform)tile.transform;else playerRonTarget=(RectTransform)tile.transform;}
            if(i==tiles.Count-1&&((highlightedRonRiver==1&&ReferenceEquals(tiles,enemyDiscards))||(highlightedRonRiver==2&&ReferenceEquals(tiles,playerDiscards)))){
                var outline=tile.gameObject.AddComponent<Outline>();
                outline.effectColor=new Color(1f,.92f,.16f,.95f);
                outline.effectDistance=new Vector2(4f,-4f);outline.useGraphicAlpha=true;
            }
        }
    }
    void RenderWinds(){
        SeventeenStepsUI.InfoBacking(content,building&&selected.Count==13?new Vector2(-460,492):new Vector2(0,440),new Vector2(250,60));
        var roundLabel=SeventeenStepsUI.Label(content,"東"+Mathf.Clamp(SeventeenStepsMode.Round,1,SeventeenStepsMode.MaxRoundsPerEnemy)+"局",(building&&selected.Count==13?new Vector2(-460,492):new Vector2(0,440)),new Vector2(250,60),36);
        SeventeenStepsUI.BlackOutline(roundLabel);

        SeventeenStepsUI.InfoBacking(content,new Vector2(-800,405),new Vector2(295,95));
        RenderCharacterName(SeventeenStepsMode.Enemies[SeventeenStepsMode.EnemyIndex],new Vector2(-800,430));
        SeventeenStepsUI.Label(content,SeventeenStepsMode.Current.enemyScore.ToString("N0")+" 点",new Vector2(-800,380),new Vector2(285,45),32);
        SeventeenStepsUI.InfoBacking(content,new Vector2(800,385),new Vector2(295,95));
        RenderCharacterName(CharacterName,new Vector2(800,410));
        SeventeenStepsUI.Label(content,SeventeenStepsMode.Current.playerScore.ToString("N0")+" 点",new Vector2(800,360),new Vector2(285,45),32);
        var windPrefab=Resources.Load<GameObject>("SeventeenSteps/NormalSeatWind");
        if(windPrefab){var wind=Instantiate(windPrefab,content,false);wind.SetActive(true);var rt=(RectTransform)wind.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(800,-55);rt.sizeDelta=new Vector2(170,65);wind.GetComponent<TMP_Text>().text=new[]{"北家","西家","南家","東家"}[(Math.Max(1,SeventeenStepsMode.Round)-1)%4];}
        RenderEquippedOfuda();
    }
    void RenderCharacterName(string name,Vector2 position){
        var prefab=Resources.Load<GameObject>("SeventeenSteps/NormalCharacterName");
        if(!prefab){SeventeenStepsUI.Label(content,name,position,new Vector2(285,55),36);return;}
        var obj=Instantiate(prefab,content,false);obj.SetActive(true);var rt=(RectTransform)obj.transform;
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.localScale=Vector3.one;rt.anchoredPosition=position;rt.sizeDelta=new Vector2(285,55);
        var label=obj.GetComponent<TMP_Text>();label.text=name;label.enableAutoSizing=true;label.fontSizeMin=25;label.fontSizeMax=36;label.alignment=TextAlignmentOptions.Center;
    }
    void RenderEquippedOfuda(){
        var prefab=Resources.Load<GameObject>("SeventeenSteps/NormalOfudaPanel");if(!prefab)return;
        var panel=Instantiate(prefab,content,false);panel.SetActive(true);var rt=(RectTransform)panel.transform;
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(-800,-285);rt.localScale=Vector3.one*.9f;
        var backing=panel.GetComponent<Image>();if(backing)backing.color=Color.clear;
        foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true))SeventeenStepsUI.BlackOutline(text);
        var labels=panel.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t=>t.name.StartsWith("OfudaListTMP")).OrderBy(t=>t.name).ToArray();
        for(int i=0;i<3;i++){
            var icon=panel.GetComponentsInChildren<Image>(true).FirstOrDefault(t=>t.name=="IconOfuda"+(i+1));
            if(i>=SeventeenStepsMode.Current.ofuda.Count){if(i<labels.Length)labels[i].text="―";if(icon)icon.gameObject.SetActive(false);continue;}
            var d=SeventeenStepsOfuda.All[SeventeenStepsMode.Current.ofuda[i]];
            if(icon){
                icon.gameObject.SetActive(true);ItemArtwork.Ofuda(icon,d.Rarity);
                float top=1f-(i*.29f+.13f),bottom=top-.27f;
                ItemArtwork.Rect(icon.rectTransform,new Vector2(0,bottom),new Vector2(.25f,top),new Vector2(4,4),new Vector2(-2,-4));
            }
            if(i<labels.Length){labels[i].text=d.Rarity+"\n"+d.name+"　×"+d.Multiplier.ToString("0.0");labels[i].font=SeventeenStepsUI.BodyFont(labels[i].text);float top=1f-(i*.29f+.13f),bottom=top-.27f;ItemArtwork.Rect(labels[i].rectTransform,new Vector2(.26f,bottom),new Vector2(1,top),new Vector2(2,4),new Vector2(-8,-4));SeventeenStepsUI.BlackOutline(labels[i]);labels[i].enableAutoSizing=true;labels[i].fontSizeMin=16;labels[i].fontSizeMax=24;OfudaRarityColors.Apply(labels[i],d.Rarity);var color=ColorUtility.ToHtmlStringRGB(labels[i].color);labels[i].color=Color.white;labels[i].text="<color=#"+color+">"+d.Rarity+"</color>\n"+d.name+"　×"+d.Multiplier.ToString("0.0");}
        }
    }
    void RenderWaitTiles(IList<int> hand){
        var w=SeventeenStepsRules.Waits(hand).OrderBy(t=>t).ToArray();
        SeventeenStepsUI.InfoBacking(content,new Vector2(0,-398),new Vector2(1250,95));
        SeventeenStepsUI.Label(content,w.Length==0?"ノーテン":"待ち牌",new Vector2(-535,-398),new Vector2(150,60),27);
        for(int i=0;i<w.Length;i++)Tile(content,w[i],new Vector2(-420+i*65,-398),new Vector2(48,66),null);
        if(w.Length>0&&(!SeventeenStepsRules.CanDeclare(hand,doraIndicator,false,UnusedDesignated())||missedRon||waits.Overlaps(playerDiscards)))SeventeenStepsUI.Label(content,missedRon||waits.Overlaps(playerDiscards)?"フリテン":"満貫未満",new Vector2(520,-398),new Vector2(170,55),25);
    }
    IEnumerator EnemyTurn()
    {
        playerTurn=false;busy=true;notice="";RenderBattle();yield return new WaitForSecondsRealtime(.9f);while(modal)yield return null;turn++;
        int index=SeventeenStepsRules.SafeDiscard(enemyCandidates,enemyDeck,enemyDiscards,playerDiscards,random);int tile=enemyCandidates[index];enemyCandidates.RemoveAt(index);enemyDiscards.Add(tile);AudioManager.Instance?.PlayDiscardTileSE();
        if(enemyDiscards.Count==1&&SeventeenStepsRules.CanDeclare(enemyHand,doraIndicator,true)){enemyRiichi=true;RenderBattle();yield return ShowCutin("リーチ",CutinArt(false),()=>AudioManager.Instance?.PlayCutin_EnemyRiichi(),1.7f);}
        var win=SeventeenStepsRules.EvaluateRound(playerHand,tile,playerRiichi,doraIndicator,uraIndicator,false,false,playerRiichi&&playerDiscards.Count==1&&enemyDiscards.Count==2,enemyDiscards.Count==17);
        win=SeventeenStepsRules.ApplyDesignatedPenalty(win,UnusedDesignated());
        if(waits.Contains(tile)&&!SeventeenStepsRules.MeetsMinimum(playerHand,tile,playerRiichi,doraIndicator,false,UnusedDesignated(),playerRiichi&&playerDiscards.Count==1&&enemyDiscards.Count==2,enemyDiscards.Count==17))missedRon=true;
        if(waits.Contains(tile)&&!missedRon&&!waits.Overlaps(playerDiscards)&&win.points>0){RenderBattle();yield return RonChoice(win,tile);yield break;}
        playerTurn=true;busy=false;RenderBattle();
    }
    void Discard(int index)
    {
        if(!playerTurn||busy||roundEnded||modal||index<0||index>=candidates.Count)return;
        playerTurn=false;busy=true;int tile=candidates[index];candidates.RemoveAt(index);playerDiscards.Add(tile);AudioManager.Instance?.PlayDiscardTileSE();
        StartCoroutine(AfterPlayerDiscard(tile));
    }
    IEnumerator AfterPlayerDiscard(int tile)
    {
        RenderBattle();
        if(playerDiscards.Count==1&&SeventeenStepsRules.CanDeclare(playerHand,doraIndicator,false,UnusedDesignated())){playerRiichi=true;yield return ShowCutin("リーチ",CutinArt(true),()=>AudioManager.Instance?.PlayCutin_PlayerRiichi(),2f);}
        var win=SeventeenStepsRules.EvaluateRound(enemyHand,tile,enemyRiichi,doraIndicator,uraIndicator,false,true,enemyRiichi&&enemyDiscards.Count==1&&playerDiscards.Count==1,playerDiscards.Count==17);
        if(enemyWaits.Contains(tile)&&!SeventeenStepsRules.MeetsMinimum(enemyHand,tile,enemyRiichi,doraIndicator,true,0,enemyRiichi&&enemyDiscards.Count==1&&playerDiscards.Count==1,playerDiscards.Count==17))enemyMissedRon=true;
        if(enemyWaits.Contains(tile)&&!enemyMissedRon&&!enemyWaits.Overlaps(enemyDiscards)&&win.points>0){yield return EnemyRonAfterPause(win,tile);yield break;}
        if(turn>=17){yield return FinishRound(false,null,-1);yield break;}yield return EnemyTurn();
    }
    IEnumerator FinishRound(bool playerWon,SeventeenStepsRules.Win win,int tile)
    {
        if(roundEnded)yield break;roundEnded=true;busy=true;RenderBattle();int points=win==null?0:playerWon?SeventeenStepsOfuda.Apply(win,SeventeenStepsMode.Current.ofuda):win.points;
        if(win!=null){var settings=GaidenUISettings.Current;yield return WinTileLightning.Play(playerWon?enemyRonTarget:playerRonTarget,transform,settings.normalStrikeDuration,settings.normalStrikeSound);yield return NormalRonCutin(playerWon);}
        highlightedRonRiver=0;
        SeventeenStepsMode.RecordRound(win==null?(playerRiichi&&!enemyRiichi?1000:0):(playerWon?points:0),win==null?(enemyRiichi&&!playerRiichi?1000:0):(playerWon?0:points));UpdateScore();
        yield return ScorePresentation(playerWon,win,tile,points);
    }

    void Continue()
    {
        if(!roundEnded)return;roundEnded=false;CloseModal();
        if(!SeventeenStepsMode.EnemyMatchFinished){StartCoroutine(StartRound());return;}
        StartCoroutine(ShowMatchResult());
    }
    IEnumerator ShowMatchResult(){
        bool won=SeventeenStepsMode.PlayerWonEnemy;
        yield return ShowCutin(won?"勝利":"敗北",ResultArt(won),()=>{if(won)AudioManager.Instance?.PlayBattleResultVictorySE();else AudioManager.Instance?.PlayBattleResultDefeatSE();},2.5f);
        int item=won?SeventeenStepsMode.GrantEnemyReward(random):0;
        NewModal(won?"勝利！":"挑戦終了");
        SeventeenStepsUI.Label(modal,"プレイヤー "+SeventeenStepsMode.Current.playerScore.ToString("N0")+"点　/　敵 "+SeventeenStepsMode.Current.enemyScore.ToString("N0")+"点\n"+(won?"獲得："+RunConsumables.Get(item)?.Name+(SeventeenStepsMode.IsFinalEnemy?" ＋ 宝石2個":""):"獲得済み遺物を持ち帰る"),new Vector2(0,125),new Vector2(1300,180),34);
        if(item>0){SeventeenStepsUI.Picture(modal,RunConsumables.Get(item).Icon,new Vector2(-420,-80),new Vector2(210,210));SeventeenStepsUI.Label(modal,RunConsumables.Get(item).Description,new Vector2(150,-80),new Vector2(850,210),32);}
        SeventeenStepsUI.Button(modal,won&&!SeventeenStepsMode.IsFinalEnemy?"次の敵へ":"メニューへ",new Vector2(0,-290),new Vector2(420,70),()=>{if(won&&!SeventeenStepsMode.IsFinalEnemy){AdvanceEnemyAfterAd();}else { AppReviewRequest.RewardAccepted(); Exit(); }});
    }
    bool advancingAfterAd;
    void AdvanceEnemyAfterAd(){
        if(advancingAfterAd)return;
        advancingAfterAd=true;busy=true;
        // Remove the result controls before invoking the shared ad manager.
        // An unavailable ad completes synchronously, so guard both click and callback.
        CloseModal();
        bool completed=false;
        Action proceed=()=>{
            if(completed)return;completed=true;
            if(!this||Active!=this)return;
            SeventeenStepsMode.AdvanceEnemy();
            advancingAfterAd=false;busy=false;
            ShowOfudaOffers();
        };
        var manager=InterstitialAdManager.Instance;
        if(manager)manager.ShowAdIfReady(proceed);else proceed();
    }
    void ShowEquippedOfuda(){
        NewModal("装備お札（最大3枚）");
        SeventeenStepsUI.Label(modal,SeventeenStepsMode.Current.ofuda.Count==0?"未装備":string.Join("\n\n",SeventeenStepsMode.Current.ofuda.Select(id=>SeventeenStepsOfuda.All[id].Description)),new Vector2(0,50),new Vector2(1260,380),36);
        SeventeenStepsUI.Button(modal,"閉じる",new Vector2(0,-290),new Vector2(350,70),CloseModal);
    }
    void ConfirmExit(){NewModal("今回の挑戦を終了？");SeventeenStepsUI.Label(modal,"獲得済み遺物は保持。\n現在の対局は保存されない。",new Vector2(0,70),new Vector2(1100,170),36);SeventeenStepsUI.Button(modal,"戻る",new Vector2(-280,-200),new Vector2(330,70),()=>{CloseModal();if(deck==null)ShowOfudaOffers();});SeventeenStepsUI.Button(modal,"終了する",new Vector2(280,-200),new Vector2(330,70),Exit);}
    void Exit(){SeventeenStepsMode.LeaveMode();Time.timeScale=1;SceneManager.LoadScene("MenuScene");}
    public static string TileLabel(int t)=>t<27?(t%9+1)+new[]{"萬","筒","索"}[t/9]:new[]{"東","南","西","北","白","發","中"}[t-27];
    static Color RarityColor(int r)=>new[]{Color.gray,Color.cyan,new Color(.25f,.55f,1),new Color(.65f,.35f,.9f),new Color(1,.7f,.15f)}[r];
    static Button Tile(Transform parent,int tile,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action,bool marked=false){var b=SeventeenStepsUI.Button(parent,"",pos,size,null);if(action!=null)b.onClick.AddListener(action);var image=b.GetComponent<Image>();image.sprite=Resources.Load<Sprite>("Sprites/Tiles/"+SeventeenStepsRules.Tiles[tile]);image.preserveAspect=true;image.color=Color.white;if(!image.sprite)b.GetComponentInChildren<TMP_Text>().text=TileLabel(tile);if(marked){var o=b.gameObject.AddComponent<Outline>();o.effectColor=new Color(1,.65f,.12f);o.effectDistance=new Vector2(4,4);}return b;}
}

public static class SeventeenStepsUI
{
    public static Sprite CharacterArt(SeventeenStepsMode.Character character)=>Resources.Load<Sprite>("SeventeenSteps/Characters/"+(character==SeventeenStepsMode.Character.DyeMaster?"Dyer":character==SeventeenStepsMode.Character.Calligrapher?"Calligrapher":"Capitalist"));
    public static Sprite EnemyArt(int index)=>Resources.Load<Sprite>("SeventeenSteps/Characters/"+new[]{"Amaterasu","Anubis","Poseidon","Zeus"}[Mathf.Clamp(index,0,3)]);
    static TMP_FontAsset preparedBodyFont;
    public static TMP_FontAsset BodyFont(string text){
        if(!preparedBodyFont){
            var template=TMP_Settings.defaultFontAsset;if(!template)return null;
            if(!template.sourceFontFile)return template;
            preparedBodyFont=TMP_FontAsset.CreateFontAsset(template.sourceFontFile,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            preparedBodyFont.name="Seventeen Steps 玉ねぎ";preparedBodyFont.fallbackFontAssetTable=new List<TMP_FontAsset>();
        }
        if(!string.IsNullOrEmpty(text))preparedBodyFont.TryAddCharacters(text,out string missing);return preparedBodyFont;
    }
    public static RectTransform CreateCanvas(Transform owner,string title){
        if(!owner.GetComponent<AudioListener>())owner.gameObject.AddComponent<AudioListener>();
        var camera=new GameObject("UI Background Camera").AddComponent<Camera>();camera.transform.SetParent(owner,false);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=0;camera.depth=-100;
        var root=Rect("Canvas",owner,Vector2.zero,new Vector2(1920,1080));var c=root.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=5000;
        var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var bg=Rect("Background",root,Vector2.zero,new Vector2(10000,10000)).gameObject.AddComponent<Image>();bg.color=new Color(.045f,.08f,.085f);
        var shared=Picture(root,Resources.LoadAll<Sprite>("Sprites/Tiles/Image_fx (5)").FirstOrDefault(),Vector2.zero,new Vector2(1920,1080));shared.preserveAspect=false;shared.color=Color.white;
        if(EventSystem.current)EventSystem.current.gameObject.SetActive(false);
        {var es=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
        
        if(title=="外伝モード")InfoBacking(root,new Vector2(0,492),new Vector2(360,65));
        var header=Label(root,title,new Vector2(0,492),new Vector2(1380,65),42);if(title=="外伝モード")BlackOutline(header);return root;
    }
    public static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
    static readonly Dictionary<TMP_FontAsset,Material> outlinedMaterials=new Dictionary<TMP_FontAsset,Material>();
    public static void BlackOutline(TMP_Text text){
        if(!text||!text.font)return;
        if(!outlinedMaterials.TryGetValue(text.font,out var material)||!material){
            material=new Material(text.font.material);material.name="Seventeen Black Outline";
            material.SetColor(ShaderUtilities.ID_OutlineColor,Color.black);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth,.2f);material.EnableKeyword("OUTLINE_ON");
            outlinedMaterials[text.font]=material;
        }
        text.fontSharedMaterial=material;text.UpdateMeshPadding();
    }
    public static TextMeshProUGUI Label(Transform p,string value,Vector2 pos,Vector2 size,float font){var t=Rect("Text",p,pos,size).gameObject.AddComponent<TextMeshProUGUI>();t.font=BodyFont(value);t.text=value;t.color=new Color(.97f,.94f,.85f);t.fontSize=font;t.enableAutoSizing=true;t.fontSizeMin=font*.8f;t.fontSizeMax=font;t.raycastTarget=false;t.alignment=TextAlignmentOptions.Center;return t;}
    public static Button Button(Transform p,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action){var r=Rect("Button",p,pos,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.94f,.93f,.84f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var t=Label(r,text,Vector2.zero,size-new Vector2(20,10),30);t.color=new Color(.07f,.09f,.10f);if(action!=null)b.onClick.AddListener(()=>{AudioManager.Instance?.PlayClickSE();action();});return b;}
    public static Image Picture(Transform p,Sprite sprite,Vector2 pos,Vector2 size){var image=Rect("Artwork",p,pos,size).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;if(!sprite)image.color=Color.clear;return image;}
    public static RectTransform Modal(Transform p,string title){var shade=Rect("Modal",p,Vector2.zero,new Vector2(10000,10000));shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.88f);var panel=Rect("Panel",shade,Vector2.zero,new Vector2(1470,800));panel.gameObject.AddComponent<Image>().color=new Color(.07f,.11f,.12f);var frame=Picture(panel,Resources.Load<Sprite>("Consumables/PanelFrame"),Vector2.zero,new Vector2(1470,800));frame.type=Image.Type.Sliced;frame.preserveAspect=false;frame.pixelsPerUnitMultiplier=5;Label(shade,title,new Vector2(0,320),new Vector2(1320,80),40);return shade;}
    public static void TablePanel(Transform parent,Vector2 position,Vector2 size,bool enemy=false){var r=Rect("TablePanel",parent,position,size);var image=r.gameObject.AddComponent<Image>();image.color=enemy?new Color(.60f,.32f,.32f,.83f):new Color(.96f,.94f,.82f,.90f);image.raycastTarget=false;Frame(r,size);}
    public static void InfoBacking(Transform parent,Vector2 pos,Vector2 size){var image=Rect("InfoBacking",parent,pos,size).gameObject.AddComponent<Image>();image.color=new Color(0,0,0,.65f);image.raycastTarget=false;}
    public static void Frame(Transform parent,Vector2 size){var frame=Picture(parent,Resources.Load<Sprite>("Consumables/PanelFrame"),Vector2.zero,size);frame.type=Image.Type.Sliced;frame.preserveAspect=false;frame.pixelsPerUnitMultiplier=8;}
    public static void Paper(Transform parent,Vector2 size){var image=parent.GetComponent<Image>();if(!image)image=parent.gameObject.AddComponent<Image>();image.color=new Color(.97f,.96f,.87f,.97f);Frame(parent,size);}
    public static Button Navigation(Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action){var b=Button(parent,text,pos,size,action);var im=b.GetComponent<Image>();im.sprite=Resources.LoadAll<Sprite>("Sprites/Tiles/ゲーム用ボタンUI向けの横長の墨の筆致（").FirstOrDefault(sprite=>sprite.name.EndsWith("_47"));im.color=Color.black;var t=b.GetComponentInChildren<TMP_Text>();t.font=TMP_Settings.defaultFontAsset;t.color=Color.white;return b;}
    public static void Clear(Transform parent){for(int i=parent.childCount-1;i>=0;i--){var g=parent.GetChild(i).gameObject;g.SetActive(false);UnityEngine.Object.Destroy(g);}}
}
