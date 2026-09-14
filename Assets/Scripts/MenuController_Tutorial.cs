using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class MenuController
{
    public const string MenuTutorialDoneKey = "FirstMenuTutorialDoneV1";
    FirstMatchTutorialView menuTutorial;
    FirstMatchTutorialContent menuTutorialContent;
    readonly List<BaseRaycaster> menuTutorialRaycasters = new List<BaseRaycaster>();
    GameObject menuTutorialSelection;

    IEnumerator RunMenuTutorial()
    {
        // Let Start, initial language selection, and unlock notices finish first.
        yield return null;
        while (_initialLanguageSelectionShowing || (skillUnlockPopupRoot && skillUnlockPopupRoot.activeInHierarchy))
            yield return null;
        if (PlayerPrefs.GetInt(MenuTutorialDoneKey, 0) != 0) yield break;
        var prefab = Resources.Load<FirstMatchTutorialView>("Tutorial/FirstMatchTutorial");
        if (!prefab) { Debug.LogError("Menu tutorial prefab is missing."); yield break; }
        menuTutorial = Instantiate(prefab, transform, false);
        menuTutorial.name = "FirstMenuTutorial";
        menuTutorialContent = Instantiate(prefab.contentAsset);
        menuTutorialContent.guideLabel = new FirstMatchTutorialContent.Localized("JANSHIN / メニューガイド", "JANSHIN / MENU GUIDE", "JANSHIN / 菜单指南");
        menuTutorialContent.skipStart = new FirstMatchTutorialContent.Localized("説明を終了", "Close guide", "结束教程");
        menuTutorialContent.skipBody = new FirstMatchTutorialContent.Localized(
            "メニューの説明を終了します。\n「その他」のオプションから、いつでも再表示できます。",
            "Close the menu guide. You can enable it again in the options under Other.",
            "结束菜单教程。可随时在「其他」的选项中重新启用。");
        menuTutorial.contentAsset = menuTutorialContent;
        menuTutorialSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var raycaster in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))
        {
            if (!raycaster.enabled || raycaster.transform.IsChildOf(menuTutorial.transform)) continue;
            menuTutorialRaycasters.Add(raycaster);
            raycaster.enabled = false;
        }
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        menuTutorial.Build(BuildMenuTutorialPages(), LocalizationManager.Instance.GetBodyFont(),
            LocalizationManager.Instance.CurrentLanguage, FinishMenuTutorial);
    }

    List<FirstMatchTutorialView.Page> BuildMenuTutorialPages()
    {
        var language = LocalizationManager.Instance.CurrentLanguage;
        string T(string ja, string en, string zh) => language == LocalizationManager.Language.English ? en : language == LocalizationManager.Language.ChineseSimplified ? zh : ja;
        var buttons = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray();
        var pages = new List<FirstMatchTutorialView.Page>();
        void Add(string title, string body, string hint, string buttonName)
        {
            var button = buttons.FirstOrDefault(b => b.gameObject.scene == gameObject.scene && b.name == buttonName);
            if (!button) Debug.LogWarning("Menu tutorial target missing: " + buttonName);
            pages.Add(new FirstMatchTutorialView.Page(title, body, hint, button ? new Transform[] { button.transform } : new Transform[0]));
        }
        Add(T("神々との対局へ", "Challenge the gods", "挑战众神"),
            T("ようこそ、雀神へ！\n「対局」から神々への挑戦を始めます。麻雀の役を作り、スキルや装備を活かして敵を倒しましょう。\n\nまずは、挑戦を支えるメニューを見ていきましょう。",
              "Welcome to Janshin!\nChoose Battle to challenge the gods. Build mahjong hands and use your skills and equipment to defeat them.\n\nFirst, let's explore the menus that help you prepare.",
              "欢迎来到雀神！\n点击「对局」挑战众神。组合麻将役种，运用技能与装备击败敌人。\n\n先来了解能帮助你做好准备的菜单吧。"),
            T("対局の操作は、初回の対局でもご案内します。", "Your first match also includes a gameplay tutorial.", "首次对局时还会介绍对局操作。"), "Button_Play");
        Add(T("スキル：キャラクターを選ぶ", "Skills: choose your character", "技能：选择角色"),
            T("「スキル」では、使用するキャラクターを選べます。\n\n最初に選べるのは「染色師」だけです。他のキャラクターは、プレイを進めることで解放されていきます。キャラクターごとのスキルを活かした戦い方を探しましょう。",
              "Choose your character in Skills.\n\nOnly the Dye Master is available at first. Other characters unlock as you play. Discover strategies that make the most of each character's skills.",
              "在「技能」中选择使用的角色。\n\n最初只能选择「染色师」。随着游玩进度，其他角色将逐步解锁。尝试发挥各角色技能的战术吧。"),
            T("まずは染色師で挑戦してみましょう。", "Start your journey with the Dye Master.", "先使用染色师开始挑战吧。"), "Button_SkillSet");
        Add(T("お守り：装備で有利に", "Charms: equip an advantage", "护身符：装备助力战斗"),
            T("敵を倒すと、報酬としてお守りを手に入れられます。\n\n「お守り」で持っているお守りの効果を確認し、装備しましょう。スキルや戦い方に合うお守りを選ぶと、対局を有利に進められます。",
              "Defeat enemies to earn charms as rewards.\n\nOpen Charms to inspect and equip them. Choose effects that complement your skills and strategy to gain an advantage in battle.",
              "击败敌人可获得护身符奖励。\n\n在「护身符」中查看效果并装备。选择适合技能与战术的护身符，让对局更加有利。"),
            T("手に入れたら、装備するのを忘れずに。", "Remember to equip the charms you earn.", "获得护身符后，别忘了装备。"), "Button_Equip");
        Add(T("特別牌：牌とパッシブを強化", "Special tiles: boost your build", "特殊牌：强化牌与被动技能"),
            T("「特別牌」では、宝石を使って特別牌を入手できます。\n\n装備した特別牌は赤ドラとして使えるほか、パッシブスキルを強化できます。狙いたい役やキャラクターに合わせて装備を選びましょう。",
              "Spend gems in Special Tiles to obtain special tiles.\n\nEquipped special tiles serve as red dora and can strengthen passive skills. Choose tiles that suit your character and the yaku you aim for.",
              "在「特殊牌」中消耗宝石获取特殊牌。\n\n装备后可作为赤宝牌使用，还能强化被动技能。根据角色与目标役种选择装备吧。"),
            T("特別牌も、入手したあとに装備して使います。", "Equip your special tiles after obtaining them.", "特殊牌也需要在获取后装备使用。"), "Button_SpecialTile");
        Add(T("強化：HP・MPを伸ばす", "Upgrades: increase HP and MP", "强化：提升HP与MP"),
            T("「強化」では、宝石を使ってHP・MPを強化できます。\n\nHPを増やして倒されにくくしたり、MPを増やしてスキルを使いやすくしたりと、自分の戦い方に合わせて成長させましょう。",
              "Spend gems in Upgrades to improve HP and MP.\n\nIncrease HP to survive longer, or MP to support your skills. Develop your character to suit your playstyle.",
              "在「强化」中消耗宝石提升HP与MP。\n\n提升HP可以更好地生存，提升MP有助于使用技能。根据自己的战术选择强化方向吧。"),
            T("宝石の使い道を考えながら、少しずつ強くなりましょう。", "Plan how to spend your gems and grow stronger over time.", "规划宝石的用途，逐步变强吧。"), "Button_Shop");
        Add(T("その他：オプションとガイド", "Other: options and guides", "其他：选项与指南"),
            T("「その他」では、オプションの変更やガイドの確認ができます。\n\nルールや仕組みを確認したいときはガイドを開いてみましょう。チュートリアルも、オプションから再表示できます。",
              "Open Other to change options and read the guides.\n\nVisit the guides whenever you want to review the rules or game systems. You can also enable the tutorials again in the options.",
              "在「其他」中修改选项或查看指南。\n\n想确认规则与游戏机制时，可以阅读指南。也可以在选项中重新启用教程。"),
            T("説明を読み直したくなったら、ここへ。", "Come here whenever you need a refresher.", "需要回顾说明时，就来这里吧。"), "Button_Other");
        Add(T("では、対局に進みましょう！", "Let's start a battle!", "开始对局吧！"),
            T("準備ができたら、神々に挑戦しましょう。\n\n最初は負けても大丈夫。敵を倒して報酬を集め、装備や強化を積み重ねて、少しずつ先へ進みましょう！",
              "You're ready to challenge the gods.\n\nIt's okay to lose at first. Defeat enemies, collect rewards, and improve your equipment and stats to push further with each attempt!",
              "准备好挑战众神了。\n\n最初失败也没关系。击败敌人、收集奖励、积累装备与强化，一步步走得更远吧！"),
            T("「対局を始める」で、挑戦の選択画面へ進みます。", "Select Start Match to choose your challenge.", "点击「开始对局」进入挑战选择界面。"), "Button_Play");
        return pages;
    }

    void FinishMenuTutorial()
    {
        bool startMatch = menuTutorial && !menuTutorial.WasSkipped;
        PlayerPrefs.SetInt(MenuTutorialDoneKey, 1);
        PlayerPrefs.Save();
        CleanupMenuTutorial();
        if (startMatch) OnClickStartBattleFlow();
    }

    void CleanupMenuTutorial()
    {
        foreach (var raycaster in menuTutorialRaycasters) if (raycaster) raycaster.enabled = true;
        menuTutorialRaycasters.Clear();
        if (menuTutorial) { menuTutorial.gameObject.SetActive(false); FirstMatchTutorialView.Release(menuTutorial.gameObject); }
        if (menuTutorialContent) FirstMatchTutorialView.Release(menuTutorialContent);
        menuTutorial = null;
        menuTutorialContent = null;
        if (EventSystem.current && menuTutorialSelection && menuTutorialSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(menuTutorialSelection);
        menuTutorialSelection = null;
    }
    void OnDisable() { CleanupMenuTutorial(); }
}
