using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class SeventeenStepsController
{
    const string SuspendKey="GaidenSuspendV1";
    static bool resumeRequested;
    RectTransform menuOverlay;
    UnityEngine.UI.Button gameMenuButton;
    void Update(){if(gameMenuButton)gameMenuButton.interactable=CanSuspend;}
    [Serializable] public sealed class SuspendedRound
    {
        public SeventeenStepsMode.State run;
        public List<int> deck,enemyDeck,enemyHand,enemyCandidates,playerHand,candidates,playerDiscards,enemyDiscards,selected,required,designatedKeys,designatedValues;
        public int turn,skillUses,target,dyeSuit,dora,ura;
        public bool building,playerRiichi,enemyRiichi,missedRon,enemyMissedRon;
    }
    public static bool HasSuspendedRound=>PlayerPrefs.HasKey(SuspendKey);
    public static void ClearSuspendedRound(){PlayerPrefs.DeleteKey(SuspendKey);PlayerPrefs.Save();}
    public static void ResumeSavedRound(){resumeRequested=true;Open();}
    bool CanSuspend=>!busy&&!dealing&&!roundEnded&&!modal&&enemyReady&&(building||playerTurn);
    void OpenGameMenu()
    {
        if(menuOverlay)return;
        // Real-time cutins/dealing must finish before the menu can safely interrupt.
        if(!CanSuspend)return;
        menuOverlay=SeventeenStepsUI.Modal(root,"メニュー");
        SeventeenStepsUI.Button(menuOverlay,"対局に戻る",new Vector2(0,160),new Vector2(620,85),CloseGameMenu);
        SeventeenStepsUI.Button(menuOverlay,"中断してメニューへ",new Vector2(0,45),new Vector2(620,85),SuspendRound);
        SeventeenStepsUI.Button(menuOverlay,"今回の挑戦を終了",new Vector2(0,-70),new Vector2(620,85),()=>{
            Destroy(menuOverlay.gameObject);menuOverlay=SeventeenStepsUI.Modal(root,"今回の挑戦を終了？");
            SeventeenStepsUI.Label(menuOverlay,"獲得済みの遺物は保持。\n現在の対局は保存されない。",new Vector2(0,70),new Vector2(1100,170),36);
            SeventeenStepsUI.Button(menuOverlay,"戻る",new Vector2(-280,-200),new Vector2(330,70),()=>{CloseGameMenu();OpenGameMenu();});
            SeventeenStepsUI.Button(menuOverlay,"終了する",new Vector2(280,-200),new Vector2(330,70),Exit);
        });
        SeventeenStepsUI.Label(menuOverlay,"中断後は「外伝モード」から再開",new Vector2(0,-220),new Vector2(1100,65),28);
    }
    void CloseGameMenu(){if(menuOverlay){menuOverlay.gameObject.SetActive(false);Destroy(menuOverlay.gameObject);menuOverlay=null;}}
    void SuspendRound()
    {
        if(!CanSuspend)return;
        var data=new SuspendedRound{run=SeventeenStepsMode.Current,deck=deck,enemyDeck=enemyDeck,enemyHand=enemyHand,enemyCandidates=enemyCandidates,playerHand=playerHand,candidates=candidates,playerDiscards=playerDiscards,enemyDiscards=enemyDiscards,selected=selected.ToList(),required=required.ToList(),designatedKeys=designatedValues.Keys.ToList(),designatedValues=designatedValues.Values.ToList(),turn=turn,skillUses=skillUses,target=target,dyeSuit=dyeSuit,dora=doraIndicator,ura=uraIndicator,building=building,playerRiichi=playerRiichi,enemyRiichi=enemyRiichi,missedRon=missedRon,enemyMissedRon=enemyMissedRon};
        PlayerPrefs.SetString(SuspendKey,JsonUtility.ToJson(data));PlayerPrefs.Save();
        SeventeenStepsMode.LeaveMode();SceneManager.LoadScene("MenuScene");
    }
    bool RestoreSuspendedRound()
    {
        if(!resumeRequested)return false;resumeRequested=false;
        var s=JsonUtility.FromJson<SuspendedRound>(PlayerPrefs.GetString(SuspendKey,""));
        if(s==null||s.run==null||s.deck==null||s.enemyHand==null)return false;
        SeventeenStepsMode.Current=s.run;PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,SeventeenStepsMode.ModeValue);SeventeenStepsMode.Save();
        deck=s.deck;enemyDeck=s.enemyDeck;enemyHand=s.enemyHand;enemyCandidates=s.enemyCandidates;playerHand=s.playerHand;
        candidates.Clear();candidates.AddRange(s.candidates);playerDiscards.Clear();playerDiscards.AddRange(s.playerDiscards);enemyDiscards.Clear();enemyDiscards.AddRange(s.enemyDiscards);
        selected.Clear();selected.UnionWith(s.selected);required.Clear();required.UnionWith(s.required);designatedValues.Clear();for(int i=0;i<s.designatedKeys.Count;i++)designatedValues[s.designatedKeys[i]]=s.designatedValues[i];
        turn=s.turn;skillUses=s.skillUses;target=s.target;dyeSuit=s.dyeSuit;doraIndicator=s.dora;uraIndicator=s.ura;building=s.building;playerRiichi=s.playerRiichi;enemyRiichi=s.enemyRiichi;missedRon=s.missedRon;enemyMissedRon=s.enemyMissedRon;
        waits=SeventeenStepsRules.Waits(playerHand);enemyWaits=SeventeenStepsRules.Waits(enemyHand);enemyReady=true;playerTurn=!building;busy=dealing=roundEnded=false;
        portrait.sprite=SeventeenStepsUI.EnemyArt(SeventeenStepsMode.EnemyIndex);portrait.color=Color.white;playerPortrait.sprite=SeventeenStepsUI.CharacterArt(SeventeenStepsMode.SelectedCharacter);
        AudioManager.Instance?.PlaySeventeenBattleBgm(SeventeenStepsMode.IsFinalEnemy);
        ClearSuspendedRound();if(building)RenderSelection();else RenderBattle();return true;
    }
}
