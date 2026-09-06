using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class GameManager : MonoBehaviour
{
    // Keep the original serialized fields so existing scenes retain their settings.
    [Serializable]
    public class TutorialPanelEntry
    {
        public GameObject panel;
        public Button nextButton;
    }

    [Header("First Match Tutorial")]
    [SerializeField] private bool tutorialEnabled = true;
    [SerializeField] private FirstMatchTutorialView tutorialPrefab;
    [SerializeField] private FirstMatchTutorialContent tutorialContent;
    [SerializeField, HideInInspector] private List<TutorialPanelEntry> tutorialPanels = new List<TutorialPanelEntry>();
    [SerializeField, HideInInspector] private GameObject tutorialDimRoot;
    [SerializeField] private string tutorialDoneKey = "FirstMatchTutorialDoneV1";

    private bool _tutorialRunning;
    private bool _tutorialFirstDrawReached;
    private bool _tutorialDealingFirstDraw;
    private bool _tutorialPreviousFreeze;
    private float _tutorialPreviousTimeScale;
    private GameObject _tutorialPreviousSelection;
    private FirstMatchTutorialView _tutorialView;
    private readonly List<Behaviour> _tutorialDisabledRaycasters = new List<Behaviour>();

    private string TutorialPreferenceKey => string.IsNullOrWhiteSpace(tutorialDoneKey)
        ? "FirstMatchTutorialDoneV1" : tutorialDoneKey;

    private bool __ShouldShowFirstMatchTutorial()
    {
        return tutorialEnabled && !_tutorialRunning
            && PlayerPrefs.GetInt(TutorialPreferenceKey, 0) == 0;
    }

    private IEnumerator __RunFirstMatchTutorial_Co()
    {
        if (_tutorialRunning) yield break;
        _tutorialPreviousFreeze = _freezeProgression;
        _tutorialPreviousTimeScale = Time.timeScale;
        _tutorialPreviousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        _tutorialRunning = true;
        _freezeProgression = true;
        Time.timeScale = 0f;
        bool finished = false;
        try
        {
            // Also isolate keyboard/gamepad selection and canvases with a higher sorting layer.
            foreach (var raycaster in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))
            {
                if (!raycaster.enabled) continue;
                _tutorialDisabledRaycasters.Add(raycaster);
                raycaster.enabled = false;
            }
            var prefab = tutorialPrefab ? tutorialPrefab : Resources.Load<FirstMatchTutorialView>("Tutorial/FirstMatchTutorial");
            if (!prefab) throw new InvalidOperationException("FirstMatchTutorial prefab is missing.");
            _tutorialView = Instantiate(prefab, transform, false);
            _tutorialView.name = "FirstMatchTutorial";
            if (tutorialContent) _tutorialView.contentAsset = tutorialContent;
            var font = LocalizationManager.Instance.GetBodyFont();
            if (!font && statusTMP) font = statusTMP.font;
            if (!font && skillInfoTMP) font = skillInfoTMP.font;
            _tutorialView.Build(BuildFirstMatchTutorialPages(), font,
                LocalizationManager.Instance.CurrentLanguage, () => finished = true);
            while (!finished && _tutorialView) yield return null;
            if (finished)
            {
                PlayerPrefs.SetInt(TutorialPreferenceKey, 1);
                PlayerPrefs.Save();
            }
        }
        finally
        {
            __CleanupFirstMatchTutorial();
        }
    }

    // Called explicitly on scene unload as well: stopped coroutines must not leave time frozen.
    private void __CleanupFirstMatchTutorial()
    {
        if (!_tutorialRunning) return;
        _tutorialRunning = false;
        if (_tutorialView)
        {
            _tutorialView.gameObject.SetActive(false);
            FirstMatchTutorialView.Release(_tutorialView.gameObject);
            _tutorialView = null;
        }
        foreach (var raycaster in _tutorialDisabledRaycasters)
            if (raycaster) raycaster.enabled = true;
        _tutorialDisabledRaycasters.Clear();
        Time.timeScale = _tutorialPreviousTimeScale;
        _freezeProgression = _tutorialPreviousFreeze;
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(
                _tutorialPreviousSelection && _tutorialPreviousSelection.activeInHierarchy
                    ? _tutorialPreviousSelection : null);
        _tutorialPreviousSelection = null;
        if (Application.isPlaying && isActiveAndEnabled) UpdateButtons();
    }

    private List<FirstMatchTutorialView.Page> BuildFirstMatchTutorialPages()
    {
        var content = tutorialContent ? tutorialContent : _tutorialView && _tutorialView.contentAsset ? _tutorialView.contentAsset : Resources.Load<FirstMatchTutorialContent>("Tutorial/FirstMatchTutorialContent");
        if (!content) throw new InvalidOperationException("FirstMatchTutorialContent is missing.");
        var active = SkillIdToEnum(PlayerPrefs.GetString(PrefKeyActive, CanonicalSkillIdRandomMan));
        int cost = 0;
        if (_skillSet) TryGetActiveSkillMpCost(active, out cost);
        return content.Resolve(LocalizationManager.Instance.CurrentLanguage, FindTutorialFocusTarget, _skillSet,
            _skillSet ? GetActiveSkillDisplayName(active) : "",
            _skillSet ? GetActiveSkillDescription(active) : "",
            _skillSet ? ComputeFinalSkillMpCost(cost).ToString() : "");
    }

    [SerializeField] private RectTransform tutorialPassiveFocus;
    [SerializeField] private RectTransform tutorialEnemyDiscardFocus;
    private Transform FindTutorialFocusTarget(FirstMatchTutorialContent.FocusTarget target)
    {
        switch (target)
        {
            case FirstMatchTutorialContent.FocusTarget.PlayerHP: return playerHPBar ? playerHPBar.transform : playerHPTMP ? playerHPTMP.transform : null;
            case FirstMatchTutorialContent.FocusTarget.EnemyHP: return enemyHPBar ? enemyHPBar.transform : enemyHPTMP ? enemyHPTMP.transform : null;
            case FirstMatchTutorialContent.FocusTarget.Hand: return handArea;
            case FirstMatchTutorialContent.FocusTarget.Offer: return offerArea;
            case FirstMatchTutorialContent.FocusTarget.Shanten: return shantenTMP ? shantenTMP.transform : handArea;
            case FirstMatchTutorialContent.FocusTarget.EnemyDiscard: return tutorialEnemyDiscardFocus ? tutorialEnemyDiscardFocus : enemyDiscardArea;
            case FirstMatchTutorialContent.FocusTarget.Discard: return discardArea;
            case FirstMatchTutorialContent.FocusTarget.MP: return mpSlider ? mpSlider.transform : mpTMP ? mpTMP.transform : null;
            case FirstMatchTutorialContent.FocusTarget.SkillButton: return btnSkill ? btnSkill.transform : null;
            case FirstMatchTutorialContent.FocusTarget.SkillInfo: return skillInfoTMP ? skillInfoTMP.transform : btnSkill ? btnSkill.transform : null;
            case FirstMatchTutorialContent.FocusTarget.PassiveSkills: return tutorialPassiveFocus;
            case FirstMatchTutorialContent.FocusTarget.Omamori: return _omamoriInfoTMP ? _omamoriInfoTMP.transform.parent : null;
            case FirstMatchTutorialContent.FocusTarget.Ofuda: return ofudaPanel;
            default: return null;
        }
    }

    private List<FirstMatchTutorialView.Page> BuildDefaultFirstMatchTutorialPages()
    {
        var language = LocalizationManager.Instance.CurrentLanguage;
        string T(string ja, string en, string zh) =>
            language == LocalizationManager.Language.English ? en :
            language == LocalizationManager.Language.ChineseSimplified ? zh : ja;
        var pages = new List<FirstMatchTutorialView.Page>();
        void Add(string title, string body, string hint, params Transform[] targets) =>
            pages.Add(new FirstMatchTutorialView.Page(title, body, hint, targets));

        Add(T("はじめての対局", "Your first match", "第一次对局"),
            T("ようこそ、雀神へ。\n麻雀の手を作り、スキルを使って敵のHPを削る対局です。\n\nまずは画面の見方と、最初の一手までの流れを確認しましょう。",
              "Welcome to Janshin.\nBuild mahjong hands and use skills to defeat your opponent.\n\nLet's learn the table and the flow of your first turn.",
              "欢迎来到雀神。\n组合麻将牌型，运用技能削减敌人的HP。\n\n先来了解界面与第一回合的操作。"),
            T("説明中は対局が停止します。自分のペースで進められます。",
              "The match is paused. Take your time.", "讲解期间对局暂停，可以按自己的节奏阅读。"));
        Add(T("HPを守り、敵を倒そう", "Watch both HP bars", "保护HP，击败敌人"),
            T("和了すると、得点や効果に応じて敵にダメージを与えます。敵のHPを0にすれば勝利です。\n\n敵の和了では自分がダメージを受けます。自分のHPを守りながら、高い手を狙いましょう。",
              "Winning a hand deals damage based on its score and effects. Reduce the enemy's HP to zero to win.\n\nEnemy wins damage you. Aim for valuable hands while protecting your HP.",
              "和牌后，根据得分与效果对敌人造成伤害。将敌人的HP降至0即可获胜。\n\n敌人和牌时，你会受到伤害。保护自己的HP，同时争取高分牌型。"),
            T("HPは残り体力、MPはスキルに使う力です。", "HP is health. MP powers your skills.", "HP是体力，MP用于发动技能。"),
            playerHPRoot ? playerHPRoot : playerHPTMP ? playerHPTMP.transform : null,
            enemyHPRoot ? enemyHPRoot : enemyHPTMP ? enemyHPTMP.transform : null);
        Add(T("手牌とツモ場", "Your hand and draw area", "手牌与摸牌区"),
            T("手牌は、和了の形を作るための牌です。最初は13枚から始まります。\n\n自分の番になるとツモ場に4枚の牌が出ます。この中の必要な牌と手牌を交換して、手を整えます。",
              "Your hand starts with 13 tiles. Build it toward a winning shape.\n\nOn your turn, four tiles appear in the draw area. Exchange useful tiles with your hand.",
              "手牌从13张开始，目标是将它们组合成和牌形。\n\n轮到你时，摸牌区会出现4张牌。将需要的牌与手牌交换，改善牌型。"),
            T("いまは配牌直後です。4枚のツモ牌は自分の番に表示されます。",
              "The opening deal is complete. The four draw tiles appear on your turn.",
              "现在刚完成配牌。4张摸牌将在你的回合出现。"), handArea, offerArea);
        Add(T("交換してから「捨てる」", "Exchange, then discard", "交换后再弃牌"),
            T("ツモ場の欲しい牌を、手牌の交換したい牌へドラッグします。\n\n手が整ったら「捨てる」でツモ場に残った牌を捨て、敵の番へ進みます。交換せずに進むこともできます。",
              "Drag a tile you want from the draw area onto the tile you want to replace in your hand.\n\nWhen ready, choose Discard to discard the remaining draw-area tiles and end your turn. You may also discard without exchanging.",
              "将摸牌区中需要的牌拖到想替换的手牌上。\n\n调整完成后，点击弃牌，弃掉摸牌区剩余的牌并结束回合。也可以不交换直接弃牌。"),
            T("「捨てる」を押す前に、手牌と残った牌を確認しましょう。",
              "Check your hand and the remaining tiles before discarding.",
              "弃牌前，请检查手牌与摸牌区剩余的牌。"), offerArea, handArea);
        Add(T("和了の基本形", "Build a winning shape", "和牌的基本形"),
            T("基本は「3枚の組を4つ ＋ 同じ牌2枚のペア」です。\n3枚の組は、同じ種類の連番（例：二・三・四萬）か、同じ牌3枚で作ります。\n\nさらに「役」が必要です。たとえばタンヤオは、1・9と字牌を使わない手です。",
              "The basic shape is four groups of three plus one identical pair.\nA group is a sequence in one suit (such as 2-3-4) or three identical tiles.\n\nYou also need a yaku. For example, All Simples uses no 1s, 9s or honor tiles.",
              "基本形是四组面子加一对将牌。\n面子可以是同花色的连续三张（如二三四万），或三张相同的牌。\n\n此外还需要役。例如断幺九不使用一、九与字牌。"),
            T("例：タンヤオの形。七対子など、別の和了形もあります。",
              "Example: All Simples. Special shapes such as Seven Pairs also exist.",
              "示例：断幺九。七对子等特殊和牌形也存在。"));
        pages[pages.Count - 1].Tiles = new[] { "Man2", "Man3", "Man4", "Pin3", "Pin4", "Pin5", "Sou4", "Sou5", "Sou6", "Man6", "Man6", "Man6", "Pin8", "Pin8" };
        Add(T("テンパイ・リーチ・和了", "Ready hands and winning", "听牌、立直与和牌"),
            T("あと1枚で和了できる状態が「テンパイ」です。条件を満たすとリーチできますが、宣言後は手を自由に変えられません。\n\n自分のツモ牌で完成すればツモ、敵の捨て牌で完成すればロンです。和了できるときは、画面に出る和了ボタンを確認しましょう。",
              "A hand one tile from winning is ready (tenpai). You can declare riichi when its conditions are met, but you can no longer freely change your hand.\n\nWin by tsumo with your own draw, or ron with an enemy discard. Watch for the available win button.",
              "差一张即可和牌的状态称为听牌。满足条件时可以立直，但立直后不能自由改变手牌。\n\n用自己摸到的牌完成牌型是自摸，用敌人的弃牌完成是荣和。请留意出现的和牌按钮。"),
            T("ドラは得点を増やす牌ですが、ドラだけでは役になりません。",
              "Dora increases the score, but does not count as a yaku by itself.",
              "宝牌可以增加得分，但只有宝牌并不能构成役。"), shantenTMP ? shantenTMP.transform : handArea);
        Add(T("敵の捨て牌と鳴き", "Enemy discards and calls", "敌人的弃牌与鸣牌"),
            T("敵の捨て牌は、ロンや鳴きの判断に使います。条件が合えば、チー・ポン・カンで牌を取り込めます。\n\n鳴くと作れなくなる役もあるため、最初は無理に鳴かず、不要なときは「スキップ」で進めて大丈夫です。",
              "Enemy discards may let you win by ron or call chi, pon or kan when allowed.\n\nCalling can make some yaku unavailable. You don't need to call every time: choose Skip when you don't want the tile.",
              "敌人的弃牌可能让你荣和，或在满足条件时吃、碰、杠。\n\n鸣牌会使部分役不再成立。初学时无需勉强鸣牌，不需要时选择跳过即可。"),
            T("自分の捨て牌も重要です。フリテンなどでロンできない場合があります。",
              "Your own discards matter too: furiten can prevent ron.",
              "自己的弃牌也很重要，振听等情况会导致无法荣和。"), enemyDiscardArea, discardArea);
        Add(T("スキルとMP", "Skills and MP", "技能与MP"),
            T("スキルは、牌の変換などで手作りを助けます。効果に応じて対象の牌を選び、「スキル」で発動します。\n\nMPが不足していると使えません。使用できるタイミングや回数にも制限があるため、ボタンと説明を確認しましょう。",
              "Skills help build your hand, for example by transforming tiles. Select a target when required, then use Skill.\n\nYou need enough MP. Skills also have timing and use limits; check the button and description.",
              "技能可以通过变换牌等效果帮助你组合手牌。按效果要求选择目标牌后，点击技能发动。\n\nMP不足时无法使用。技能还有发动时机与次数限制，请查看按钮与说明。"),
            T("効果の発動に成功したときにMPを消費します。",
              "MP is spent when the effect activates successfully.",
              "效果成功发动时才会消耗MP。"),
            mpSlider ? mpSlider.transform : mpTMP ? mpTMP.transform : null, btnSkill ? btnSkill.transform : null);
        if (_skillSet)
        {
            var active = SkillIdToEnum(PlayerPrefs.GetString(PrefKeyActive, CanonicalSkillIdRandomMan));
            int cost;
            TryGetActiveSkillMpCost(active, out cost);
            Add(T("装備中のスキル", "Your equipped skill", "当前装备的技能"),
                GetActiveSkillDisplayName(active) + "\n\n" + GetActiveSkillDescription(active),
                T("消費MP：", "MP cost: ", "消耗MP：") + ComputeFinalSkillMpCost(cost),
                skillInfoTMP ? skillInfoTMP.transform : btnSkill ? btnSkill.transform : null);
        }
        Add(T("対局を始めよう", "You're ready to play", "开始对局吧"),
            T("1. 敵の捨て牌を見て、必要がなければスキップ。\n2. 自分の番でツモ場と手牌を交換。\n3. スキルや和了のチャンスを確認して「捨てる」。\n\nまずは同じ牌や連番を集め、和了を目指しましょう。",
              "1. Check enemy discards; skip if you don't need them.\n2. Exchange draw-area tiles with your hand on your turn.\n3. Check skills and winning opportunities before discarding.\n\nStart by collecting pairs and sequences.",
              "1. 查看敌人的弃牌，不需要时跳过。\n2. 自己的回合交换摸牌区与手牌。\n3. 检查技能与和牌机会后弃牌。\n\n先从收集相同牌与连续牌开始，向和牌前进吧。"),
            T("「対局を始める」で再開します。この案内は次回から自動表示されません。",
              "Start Match resumes play. This guide will not appear automatically again.",
              "点击开始对局即可继续。下次不会再自动显示本教程。"));
        return pages;
    }
}
