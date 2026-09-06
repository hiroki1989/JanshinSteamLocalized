using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Janshin/First Match Tutorial Content")]
public sealed class FirstMatchTutorialContent : ScriptableObject
{
    [Serializable]
    public class Localized
    {
        [TextArea(2, 10)] public string japanese;
        [TextArea(2, 10)] public string english;
        [TextArea(2, 10)] public string chineseSimplified;
        public Localized() { }
        public Localized(string ja, string en, string zh) { japanese = ja; english = en; chineseSimplified = zh; }
        public string Get(LocalizationManager.Language language) =>
            language == LocalizationManager.Language.English ? english ?? "" :
            language == LocalizationManager.Language.ChineseSimplified ? chineseSimplified ?? "" : japanese ?? "";
    }

    public enum FocusTarget { PlayerHP, EnemyHP, Hand, Offer, Shanten, EnemyDiscard, Discard, MP, SkillButton, SkillInfo, PassiveSkills, Omamori, Ofuda }

    [Serializable]
    public class Step
    {
        public string editorLabel;
        public Localized title = new Localized();
        [Tooltip("Equipped skill placeholders: {skillName}, {skillDescription}, {mpCost}")]
        public Localized body = new Localized();
        public Localized hint = new Localized();
        public bool requiresEquippedSkill;
        [Tooltip("Use tighter line spacing and allow an 18 pt minimum for long pages.")] public bool compactBody;
        public FocusTarget[] focusTargets = Array.Empty<FocusTarget>();
        public string[] exampleTiles = Array.Empty<string>();
    }

    [Header("Pages (displayed in this order)")]
    public List<Step> pages = new List<Step>();

    [Header("Navigation and skip confirmation")]
    public Localized guideLabel = new Localized("JANSHIN / 対局ガイド", "JANSHIN / MATCH GUIDE", "JANSHIN / 对局指南");
    public Localized back = new Localized("戻る", "Back", "返回");
    public Localized skip = new Localized("スキップ", "Skip", "跳过");
    public Localized next = new Localized("次へ", "Next", "下一步");
    public Localized start = new Localized("対局を始める", "Start Match", "开始对局");
    public Localized skipStart = new Localized("スキップして開始", "Skip & start", "跳过并开始");
    public Localized skipTitle = new Localized("説明をスキップしますか？", "Skip the guide?", "跳过教程？");
    public Localized skipBody = new Localized(
        "チュートリアルを終了して対局を始めます。\nこの案内は次回から自動表示されません。\n\n説明を続ける場合は「戻る」を選んでください。",
        "This ends the tutorial and resumes the match.\nThe guide will not appear automatically again.\n\nChoose Back to keep reading.",
        "结束教程并开始对局。\n下次不会再自动显示本教程。\n\n选择返回可继续阅读。");

    public List<FirstMatchTutorialView.Page> Resolve(LocalizationManager.Language language,
        Func<FocusTarget, Transform> findTarget = null, bool hasSkill = true,
        string skillName = "{skillName}", string skillDescription = "{skillDescription}", string mpCost = "{mpCost}")
    {
        string Expand(Localized value) => (value == null ? "" : value.Get(language))
            .Replace("{skillName}", skillName).Replace("{skillDescription}", skillDescription).Replace("{mpCost}", mpCost);
        var resolved = new List<FirstMatchTutorialView.Page>();
        foreach (var step in pages)
        {
            if (step == null || (step.requiresEquippedSkill && !hasSkill)) continue;
            var targets = new List<Transform>();
            if (findTarget != null && step.focusTargets != null)
                foreach (var target in step.focusTargets)
                {
                    var found = findTarget(target);
                    if (found) targets.Add(found);
                }
            resolved.Add(new FirstMatchTutorialView.Page(Expand(step.title), Expand(step.body), Expand(step.hint), targets.ToArray())
                { CompactBody = step.compactBody, Tiles = step.exampleTiles != null && step.exampleTiles.Length > 0 ? step.exampleTiles : null });
        }
        return resolved;
    }
}
