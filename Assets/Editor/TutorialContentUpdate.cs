using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// One-shot migration. Japanese in existing pages remains authoritative.
internal static class TutorialContentUpdate
{
    const string Request = "Temp/TutorialQA/content-update-request";
    const string ContentPath = "Assets/Resources/Tutorial/FirstMatchTutorialContent.asset";
    const string PrefabPath = "Assets/Resources/Tutorial/FirstMatchTutorial.prefab";
    const string ScenePath = "Assets/Scenes/OtherScene.unity";

    [InitializeOnLoadMethod] static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += Run;
    }

    static void Translate(FirstMatchTutorialContent.Localized text, string en, string zh)
    { text.english = en; text.chineseSimplified = zh; }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Run; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try
        {
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new Exception("OtherScene has unsaved edits. Save it before applying the tutorial update.");
            var c = AssetDatabase.LoadAssetAtPath<FirstMatchTutorialContent>(ContentPath);
            var original = c.pages.Where(p => p.title.japanese != "パッシブスキル").ToArray();
            if (original.Length != 10) throw new Exception("Expected ten existing tutorial pages.");
            var ja = original.Select(p => p.title.japanese + "\u001f" + p.body.japanese + "\u001f" + p.hint.japanese).ToArray();
            string[] titlesEn = { "Your first battle", "Win hands to damage the gods", "Your hand and the draw area",
                "Swap, then Discard", "The basic winning shape", "Tenpai, Riichi & winning", "Enemy discards & calls",
                "Skills & MP", "Your equipped skill", "Let's start the match" };
            string[] titlesZh = { "初次战斗", "和牌，对诸神造成伤害", "手牌与摸牌区", "交换后点击“舍牌”", "和牌的基本牌型",
                "听牌、立直与和牌", "敌方舍牌与鸣牌", "技能与MP", "已装备的技能", "开始对局吧" };
            string[] bodiesEn = {
                "Welcome to Janshin -infinity-.\nMaster your skills, build winning hands, and defeat the gods.\n\nFirst, let's explore the screen and the steps leading to your first move.",
                "Winning a hand damages the gods based on your score and effects. Reduce their HP to 0 to win.\n\nWhen the gods win a hand, you take damage. Aim for valuable hands while protecting your HP.",
                "Your hand holds the tiles you use to build a winning hand. You start with 13 tiles.\n\nOn your turn, four tiles appear in the draw area. Swap the ones you need with tiles in your hand.",
                "Drag a tile you want from the draw area onto the tile you want to replace in your hand.\n\nWhen ready, press Discard to discard the tiles left in the draw area and end your turn. You can also continue without swapping.",
                "The basic shape is four sets of three tiles plus a pair.\nA set is a sequence in one suit (e.g. 2, 3, 4 of characters) or three identical tiles.\n\nYou also need a yaku. For example, Tanyao is a hand with no 1s, 9s, or honor tiles.",
                "Tenpai means you need one more tile to win. You can declare Riichi when eligible, but cannot freely change your hand afterward. You may stay in Tenpai without declaring Riichi to aim for a better hand.\n\nComplete your hand with a drawn tile for Tsumo, or a god's discard for Ron. Watch for the win button when you can win.",
                "Check enemy discards for Ron or a call. When eligible, you can claim tiles with Chi, Pon, or Kan.\n\nCalling prevents some yaku and stops you from declaring Riichi. You needn't force a call as a beginner; choose Skip when you don't need the tile.",
                "Skills spend MP to help build your hand, for example by converting tiles. The Dyer's Chromaflow converts a selected tile into a random tile of the most common suit in your hand when you press Skill. Use it to aim for a half flush or full flush, or to turn an unwanted tile into a useful one.\n\nYou cannot use a skill without enough MP.",
                "{skillName}\n\n{skillDescription}",
                "1. Check enemy discards; Skip if you need none.\n2. On your turn, swap tiles between the draw area and your hand.\n3. Check for skills and winning chances, then press Discard.\n\nStart by collecting matching tiles and sequences to build a winning hand."
            };
            string[] bodiesZh = {
                "欢迎来到《雀神 -infinity-》。\n熟练运用技能，组成和牌牌型，击败诸神吧。\n\n首先，来了解界面以及开始第一步操作的流程。",
                "玩家和牌时，会根据得分及效果对诸神造成伤害。将诸神的HP降至0即可获胜。\n\n诸神和牌时，你会受到伤害。在保护自身HP的同时，争取高分牌型吧。",
                "手牌是用来组成和牌牌型的牌。开始时有13张。\n\n轮到你时，摸牌区会出现4张牌。将其中需要的牌与手牌交换，整理牌型。",
                "将摸牌区中想要的牌，拖到手牌中想替换的牌上。\n\n整理好后，点击“舍牌”，弃掉摸牌区剩余的牌，进入诸神的回合。也可以不交换直接继续。",
                "基本牌型是“4组各3张的面子＋1对相同牌”。\n面子可以是同一花色的连续数字（如二、三、四万），或3张相同的牌。\n\n此外还需要“役”。例如断幺九，就是不使用一、九及字牌的牌型。",
                "只差1张牌即可和牌的状态称为“听牌”。满足条件即可立直，但宣告后便不能自由改变手牌。为了追求更好的牌型，也可以听牌后不立直。\n\n用自己摸到的牌完成牌型称为自摸，用诸神的舍牌完成则称为荣和。可以和牌时，请留意画面上的和牌按钮。",
                "查看敌方舍牌，判断能否荣和或鸣牌。满足条件时，可以通过吃、碰、杠取得牌。\n\n鸣牌后有些役无法成立，也无法立直。刚开始不必勉强鸣牌，不需要时点击“跳过”即可。",
                "技能通过消耗MP来变换牌等，帮助整理手牌。染色师的“引色”技能：选择目标牌并点击“技能”后，可将其变为手牌中数量最多的花色的随机牌。可用来追求混一色、清一色，或将不需要的牌变为有用的牌。\n\nMP不足时无法使用技能。",
                "{skillName}\n\n{skillDescription}",
                "1. 查看敌方舍牌，不需要时选择跳过。\n2. 轮到你时，交换摸牌区与手牌中的牌。\n3. 确认技能及和牌机会后，点击“舍牌”。\n\n先收集相同牌或连续数字的牌，以和牌为目标吧。"
            };
            string[] hintsEn = { "", "", "", "Before pressing Discard, check your hand and the remaining tiles.",
                "Example: a Tanyao hand. Other winning shapes include Seven Pairs.",
                "Dora increase your score, but Dora alone do not count as a yaku.",
                "You can Ron a god's discard even if you previously discarded that tile. Feel free to declare Riichi in Furiten.",
                "", "MP cost: {mpCost}", "Press Start Match to resume. This guide will not appear automatically next time." };
            string[] hintsZh = { "", "", "", "点击“舍牌”前，请确认手牌和剩余的牌。", "例：断幺九牌型。此外还有七对子等其他和牌牌型。",
                "宝牌可以增加得分，但仅有宝牌不构成役。",
                "即使自己曾舍出过这张牌，也能荣和诸神的舍牌。放心振听立直吧。", "", "消耗MP：{mpCost}",
                "点击“开始对局”继续。下次不会自动显示此指南。" };
            for (int i = 0; i < original.Length; i++)
            {
                Translate(original[i].title, titlesEn[i], titlesZh[i]);
                Translate(original[i].body, bodiesEn[i], bodiesZh[i]);
                Translate(original[i].hint, hintsEn[i], hintsZh[i]);
            }
            Translate(c.guideLabel, "JANSHIN / MATCH GUIDE", "JANSHIN / 对局指南");
            Translate(c.back, "Back", "返回"); Translate(c.skip, "Skip", "跳过"); Translate(c.next, "Next", "下一页");
            Translate(c.start, "Start Match", "开始对局"); Translate(c.skipStart, "Skip & start", "跳过并开始");
            Translate(c.skipTitle, "Skip the guide?", "要跳过说明吗？");
            Translate(c.skipBody, "This ends the tutorial and starts the match.\nThe guide will not appear automatically again.\n\nChoose Back to keep reading.",
                "结束教程并开始对局。\n下次不会自动显示此指南。\n\n如需继续阅读，请选择“返回”。");
            if (!c.pages.Any(p => p.title.japanese == "パッシブスキル"))
                c.pages.Insert(c.pages.IndexOf(original[7]) + 1, new FirstMatchTutorialContent.Step {
                    editorLabel = "パッシブスキル",
                    title = new FirstMatchTutorialContent.Localized("パッシブスキル", "Passive skills", "被动技能"),
                    body = new FirstMatchTutorialContent.Localized(
                        "装備しているだけで効果を発揮するスキルです。対応する役はキャラクターごとに異なります。\n\n<sprite=0> 撃：対応する役で和了すると、敵へのダメージが一定の割合で加算されます。\n<sprite=1> 癒：対応する役で和了すると、基礎点の一定割合のHPが回復します。\n<sprite=2> 瞬：対応する役で和了すると、基礎点の一定割合のMPが回復します。",
                        "Passives work simply by being equipped. Each character has different target yaku.\n\n<sprite=0> Geki: winning with its yaku adds a percentage damage bonus.\n<sprite=1> Iyu: winning with its yaku restores HP equal to a percentage of the base score.\n<sprite=2> Shun: winning with its yaku restores MP equal to a percentage of the base score.",
                        "被动技能只需装备即可生效。对应的役因角色而异。\n\n<sprite=0> 击：以对应的役和牌时，按一定比例增加对敌伤害。\n<sprite=1> 愈：以对应的役和牌时，回复相当于基础点数一定比例的HP。\n<sprite=2> 瞬：以对应的役和牌时，回复相当于基础点数一定比例的MP。"),
                    hint = new FirstMatchTutorialContent.Localized(
                        "早く倒すなら撃の役、HP・MPが減ったら癒・瞬の役を狙いましょう。",
                        "Aim for Geki to win quickly, or Iyu and Shun when HP or MP runs low.",
                        "想速战速决就争取击的役，HP或MP不足时则争取愈或瞬的役。"),
                    focusTargets = new[] { FirstMatchTutorialContent.FocusTarget.SkillInfo }
                });
            for (int i = 0; i < original.Length; i++)
                if (ja[i] != original[i].title.japanese + "\u001f" + original[i].body.japanese + "\u001f" + original[i].hint.japanese)
                    throw new Exception("Japanese was modified: " + i);
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssetIfDirty(c);

            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try {
                var icons = Resources.Load<TMP_SpriteAsset>("T_1_sword90_");
                if (!icons) throw new Exception("Missing passive icons.");
                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name == "Body" || text.name == "BodyWithExample") text.spriteAsset = icons;
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            } finally { PrefabUtility.UnloadPrefabContents(prefab); }
            InstallOption();
            File.WriteAllText("Temp/TutorialQA/content-update-results.txt", "SUCCESS: preserved all existing Japanese; translated 10 pages and navigation; added passive page and option reset.");
            typeof(FirstMatchTutorialQA).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }
        catch (Exception e) { File.WriteAllText("Temp/TutorialQA/content-update-results.txt", "FAIL " + e); Debug.LogException(e); }
    }

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("Tutorial/Fonts/Japanese");
        text.fontSize = fontSize; text.fontSizeMin = 18; text.fontSizeMax = fontSize; text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.color = new Color(.15f,.15f,.18f);
        return text;
    }
    static void InstallOption()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        var previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try {
            var panel = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="OptionPanel");
            if (panel.Find("TutorialReset")) return;
            var source = panel.GetComponentsInChildren<Button>(true).First(b=>b.name=="OptionBackButton");
            var row = Rect("TutorialReset", panel, new Vector2(0,-288), new Vector2(1000,100));
            var buttonRect = Rect("ResetButton", row, new Vector2(0,12), new Vector2(600,52));
            var sourceImage = source.GetComponent<Image>();
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.sprite = sourceImage.sprite; image.type = sourceImage.type; image.color = sourceImage.color;
            var button = buttonRect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.colors = source.colors;
            var label = Label("Label", buttonRect, Vector2.zero, new Vector2(560,46), 28);
            label.color = Color.white;
            var status = Label("Status", row, new Vector2(0,-38), new Vector2(1000,28), 21);
            var component = row.gameObject.AddComponent<TutorialResetOption>();
            var so = new SerializedObject(component);
            so.FindProperty("resetButton").objectReferenceValue = button;
            so.FindProperty("buttonLabel").objectReferenceValue = label;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            // Match any project-specific tutorial key while leaving player preferences untouched.
            var run = EditorSceneManager.OpenPreviewScene("Assets/Scenes/RunScene.unity");
            try {
                var gm = run.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameManager>(true)).First();
                so.FindProperty("completionKey").stringValue = new SerializedObject(gm).FindProperty("tutorialDoneKey").stringValue;
            } finally { EditorSceneManager.ClosePreviewScene(run); }
            so.ApplyModifiedPropertiesWithoutUndo();
            component.Refresh(LocalizationManager.Language.Japanese);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save OtherScene.");
        } finally {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
