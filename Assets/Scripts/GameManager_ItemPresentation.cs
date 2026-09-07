using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
    [Header("Win lightning / 和了牌への落雷")]
    [SerializeField, Range(.6f, 2f)] private float winStrikeDuration = 1.05f;
    [Tooltip("和了牌に雷が落ちる瞬間の効果音。未設定なら標準の雷音を再生します。プレイヤー・敵の両方に適用。")]
    [SerializeField] private AudioClip winStrikeSound;
    private SkillDescriptionPopup _activeSkillPopup;
    private int _winStrikeEnemyDiscardIndex = -1;
    private int _winStrikePlayerDiscardIndex = -1;

    private void WireActiveSkillDescription()
    {
        var text = _skillActionNameTMP ? _skillActionNameTMP : _skillNameTMP;
        if (!text) return;
        text.raycastTarget = true;
        var tap = text.GetComponent<SkillDescriptionTap>() ?? text.gameObject.AddComponent<SkillDescriptionTap>();
        tap.Open = ShowActiveSkillDescription;
    }
    public void ShowActiveSkillDescription()
    {
        if (_activeSkillPopup || _tutorialRunning || phase == Phase.Scoring || isMenuOpen) return;
        var skill = ResolveActiveSkillForMP();
        if (skill == ActiveSkill.None) return;
        bool previousFreeze = _freezeProgression;
        _freezeProgression = true;
        _activeSkillPopup = SkillDescriptionPopup.Show(
            transform, BuildSkillActionNameText(), GetActiveSkillDescription(skill),
            () => { if (this) { _freezeProgression = previousFreeze; _activeSkillPopup = null; } });
    }
    private RectTransform ResolveWinStrikeTarget(bool player, bool tsumo, string tile)
    {
        if (player && tsumo)
        {
            if (_lastPlayerTsumoOfferIndex >= 0 && _lastPlayerTsumoOfferIndex < offers.Count &&
                StripTileIdForLogic(offers[_lastPlayerTsumoOfferIndex]) == StripTileIdForLogic(tile))
                return StrikeArt(FindOfferChildByLogicalIndex(_lastPlayerTsumoOfferIndex));
            foreach (int i in selHand)
                if (handArea && i >= 0 && i < hand.Count && i < handArea.childCount && StripTileIdForLogic(hand[i]) == StripTileIdForLogic(tile))
                    return StrikeArt(handArea.GetChild(i));
            int index = offers.FindIndex(t => StripTileIdForLogic(t) == StripTileIdForLogic(tile));
            return index < 0 ? null : StrikeArt(FindOfferChildByLogicalIndex(index));
        }
        var list = player || tsumo ? enemyDiscards : discards;
        var area = player || tsumo ? enemyDiscardArea : discardArea;
        string prefix = player || tsumo ? "EnemyDiscard_" : "PlayerDiscard_";
        int exact = player ? _lastPlayerRonEnemyDiscardIndex : tsumo ? _winStrikeEnemyDiscardIndex : _winStrikePlayerDiscardIndex;
        if (exact < 0 || exact >= list.Count || StripTileIdForLogic(list[exact]) != StripTileIdForLogic(tile))
            exact = list.FindLastIndex(t => StripTileIdForLogic(t) == StripTileIdForLogic(tile));
        if (!area || exact < 0) return null;
        // Search from the end because the preceding UI rebuild can still have deferred Destroy children.
        for (int i = area.childCount - 1; i >= 0; i--)
            if (area.GetChild(i).name.StartsWith(prefix + exact + "_")) return StrikeArt(area.GetChild(i));
        return null;
    }
    static RectTransform StrikeArt(Transform tile)
    {
        if (!tile) return null;
        return (tile.Find("Art/Image") ?? tile.Find("Art") ?? tile) as RectTransform;
    }
    private IEnumerator PlayWinStrike(bool player, bool tsumo, string tile)
    {
        if (_activeSkillPopup) _activeSkillPopup.Close();
        // The caller can rebuild the discard views later in this same frame.
        yield return null;
        var target = ResolveWinStrikeTarget(player, tsumo, tile);
        if (!target)
        {
            if (player && tsumo) RefreshOfferUI();
            else if (player || tsumo) RefreshEnemyDiscardUI();
            else RefreshDiscardUI();
            yield return null;
            target = ResolveWinStrikeTarget(player, tsumo, tile);
        }
        if (!target) { Debug.LogWarning("[WinLightning] Winning tile view unavailable."); yield return new WaitForSeconds(.25f); yield break; }
        yield return WinTileLightning.Play(target, transform, winStrikeDuration, winStrikeSound);
    }
}
