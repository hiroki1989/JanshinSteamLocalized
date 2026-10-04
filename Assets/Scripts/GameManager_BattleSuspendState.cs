using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public partial class GameManager
{
    [Serializable] private class BattleTileRow { public List<string> tiles = new List<string>(); }
    [Serializable] private class BattleSavedField
    {
        public string name, kind, text;
        public int integer;
        public float number;
        public bool flag;
        public List<int> integers;
        public List<string> strings;
        public List<BattleTileRow> rows;
    }
    // Explicit gameplay state only: scene objects, coroutines and UI caches are never persisted.
    private static readonly string[] BattleSuspendFields = (
        "totalScore runScore runGold _mp _playerTsumoCountThisRound _playerDidAnkanOnFirstTurnThisHand " +
        "_pendingTsumoPenalty _pendingNextWinPointPenalty _skillCastsUsedThisTurn _activeSkillChargesLeft " +
        "_skillNextOfferTile _afterSkillNoHandDiscardOnce _suppressEnemyEffectsOnce " +
        "needDiscardCount discardedThisTurn canRonNow canTsumoNow selectedEnemyIndex selectedEnemyDiscardIdx " +
        "callMode callBaseTile _pendingCallPon _pendingCallChi _pendingCallKan selHand selOffer " +
        "_skillHandSelectionOrder _playerWonHandSnapshot _playerWonMeldsSnapshot _enemyWonHandSnapshot " +
        "_enemyLastWinWasRiichi _enemyLastWinWasTsumo _enemyLastWinTileId " +
        "_playerRiichiDiscardHighlightIndex _enemyRiichiDiscardHighlightIndex _playerIsDoubleRiichi " +
        "_playerIppatsuEligible _playerRiichiDeclaredTsumoCountThisRound __riichiLatched " +
        "enemyEffectAppliedIndices _enemyRonGreyPlayerDiscardIndices _enemyTurnHistory " +
        "_enemySkillTurnsUntilNext _enemySkillTurnCounters _enemySkillsOwnerRuntimeIndex " +
        "_enemySkillAngerMultiplier _enemySkillPlayerDamageDownRate _enemySkillAngerTurnRemaining " +
        "_enemySkillDefenseTurnRemaining _enemySkillPoisonTurnRemaining _enemySkillPoisonDamagePerTurn " +
        "_enemySkillParalysisTurnRemaining _legendaryDamageHalfPending _legendaryDamageHalfEnemyKey " +
        "_legendaryHalfMpCostPending _legendaryHalfMpCostEnemyKey _legendaryHalfMpCostTargetRound " +
        "_legendaryDamageHalfReservedSourceTiles _legendaryHalfMpCostReservedSourceTiles runItemIds " +
        "_enemyCommittedMelds _enemyCommittedPair _meldCommittedIndices _enemyHasBaseHand _enemyTaatsu " +
        "_enemyTaatsuIdx0 _enemyTaatsuIdx1 _enemyIsInTenpai _enemyIsInRiichi _lastProcessedEnemyDiscardCount " +
        "_observedRoundNumber _observedEnemyIndex _addonLocalTsumoPenalty _playerHasWonThisHand _enemyHasWonThisHand " +
        "_autoSkipPending _autoConfirmOfferPending _suspendEnemyTurnDelayPending"
    ).Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
    private bool _suspendEnemyTurnDelayPending;
    private bool CanSuspendCurrentPosition()
    {
        return playerHP > 0 && enemyHP > 0 && hand.Count > 0 && phase != Phase.Scoring &&
            !_preparedForSceneUnload && !_defeatTransitionRunning && !_enemyTurnRunning &&
            !_beginOfferPhaseInProgress && !_playerSkillCutinRunning && !_playerSkillTransformRunning &&
            !_enemySkillCutinRunning && !_enemyRiichiCutinRunning && !_playerRiichiCutinRunning &&
            !_rinshanDrawRunning && !_tutorialDealingFirstDraw && !_consumableDealing &&
            !_playerWinDamageAnimating && !_enemyWinDamageAnimating && !_enemySkillDamageAnimating;
    }
    private List<BattleSavedField> CaptureBattleSuspendFields()
    {
        var result = new List<BattleSavedField>();
        foreach (string name in BattleSuspendFields)
        {
            var field = typeof(GameManager).GetField(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (field == null) continue;
            var value = field.GetValue(this); var saved = new BattleSavedField { name=name };
            if (field.FieldType == typeof(bool)) { saved.kind="bool"; saved.flag=(bool)value; }
            else if (field.FieldType == typeof(int) || field.FieldType.IsEnum) { saved.kind="int"; saved.integer=Convert.ToInt32(value); }
            else if (field.FieldType == typeof(float)) { saved.kind="float"; saved.number=(float)value; }
            else if (field.FieldType == typeof(string)) { saved.kind="string"; saved.text=(string)value; }
            else if (value is IEnumerable<int> ints) { saved.kind="ints"; saved.integers=ints.ToList(); }
            else if (value is IEnumerable<string> strings) { saved.kind="strings"; saved.strings=strings.ToList(); }
            else if (value is IEnumerable<List<string>> rows) { saved.kind="rows"; saved.rows=rows.Select(r=>new BattleTileRow{tiles=new List<string>(r)}).ToList(); }
            else if (value == null) { saved.kind="null"; }
            else throw new InvalidOperationException("Unsupported battle state: "+name);
            result.Add(saved);
        }
        return result;
    }
    private void RestoreBattleSuspendFields(List<BattleSavedField> fields)
    {
        if (fields == null) return;
        foreach (var saved in fields)
        {
            if (!BattleSuspendFields.Contains(saved.name)) continue;
            var field=typeof(GameManager).GetField(saved.name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (field==null) continue;
            switch(saved.kind)
            {
                case "bool": field.SetValue(this,saved.flag); break;
                case "int": field.SetValue(this,field.FieldType.IsEnum?Enum.ToObject(field.FieldType,saved.integer):(object)saved.integer); break;
                case "float": field.SetValue(this,saved.number); break;
                case "string": field.SetValue(this,saved.text); break;
                case "null": if(!field.IsInitOnly)field.SetValue(this,null); break;
                default:
                    object target=field.GetValue(this);
                    if(target==null){target=Activator.CreateInstance(field.FieldType);field.SetValue(this,target);}
                    target.GetType().GetMethod("Clear").Invoke(target,null);
                    string addName=target.GetType().Name.StartsWith("Queue")?"Enqueue":"Add";
                    var add=target.GetType().GetMethod(addName);
                    if(saved.kind=="ints") foreach(var n in saved.integers) add.Invoke(target,new object[]{n});
                    if(saved.kind=="strings") foreach(var s in saved.strings) add.Invoke(target,new object[]{s});
                    if(saved.kind=="rows") foreach(var row in saved.rows) add.Invoke(target,new object[]{new List<string>(row.tiles)});
                    break;
            }
        }
    }
    private void ResumeBattleContinuations()
    {
        _freezeProgression=false; isMenuOpen=false;
        if (_suspendEnemyTurnDelayPending) { _suspendEnemyTurnDelayPending=false; StartCoroutine(EnterEnemyTurnAfterPlayerAfterDelay(.5f)); }
        else if (_autoSkipPending) StartCoroutine(_AutoSkip(.5f));
        if (_autoConfirmOfferPending) StartCoroutine(_AutoConfirmOfferAfter(.5f));
        if (phase==Phase.ChoosingCall && callMode!=CallMode.None)
        {
            if(callMode==CallMode.Chi)EnableHandForChiDynamic();else EnableHandForCall(IsSelectableForCurrentCall);
        }
        UpdateMpUI(); EnemySkills_UpdateCountdownUI(); EnemySkills_RefreshStatusEffectsUI();
        UpdateRightInfoUI_Manual(); RefreshMissionDisplayText(); RefreshConsumableButton();
    }
}
