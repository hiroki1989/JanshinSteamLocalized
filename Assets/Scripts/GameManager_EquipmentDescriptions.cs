using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
    string EquipmentText(string ja, string en, string zh)
    {
        var lang = LocalizationManager.Instance.CurrentLanguage;
        return lang == LocalizationManager.Language.English ? en : lang == LocalizationManager.Language.ChineseSimplified ? zh : ja;
    }

    void WireEquipmentDescriptions()
    {
        BindDescription(tutorialPassiveFocus, ShowPassiveDescription);
        BindDescription(_skillTraitGekiTMP ? _skillTraitGekiTMP.transform : null, ShowPassiveDescription);
        BindDescription(_skillTraitShunTMP ? _skillTraitShunTMP.transform : null, ShowPassiveDescription);
        BindDescription(_skillTraitIyuTMP ? _skillTraitIyuTMP.transform : null, ShowPassiveDescription);
        BindDescription(_omamoriInfoTMP ? _omamoriInfoTMP.transform.parent : null, ShowOmamoriDescription);
        BindDescription(ofudaPanel, ShowOfudaDescription);
        foreach (var text in _ofudaInfoTMPs ?? new TMPro.TextMeshProUGUI[0])
            if (text) BindDescription(text.transform, ShowOfudaDescription);
        foreach (var icon in _ofudaIconImages ?? new Image[0])
            if (icon) BindDescription(icon.transform, ShowOfudaDescription);
    }

    static void BindDescription(Transform target, Action open)
    {
        if (!target) return;
        var graphic = target.GetComponent<Graphic>();
        if (!graphic && target is RectTransform)
        {
            var image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            graphic = image;
        }
        if (graphic) graphic.raycastTarget = true;
        var tap = target.GetComponent<SkillDescriptionTap>() ?? target.gameObject.AddComponent<SkillDescriptionTap>();
        tap.Open = open;
    }

    void ShowEquipmentDescription(string title, string body)
    {
        if (_activeSkillPopup || _tutorialRunning || phase == Phase.Scoring || isMenuOpen) return;
        bool previousFreeze = _freezeProgression;
        _freezeProgression = true;
        _activeSkillPopup = SkillDescriptionPopup.Show(transform, title, body,
            () => { if (this) { _freezeProgression = previousFreeze; _activeSkillPopup = null; } });
        foreach (var text in _activeSkillPopup.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            ApplyTraitSpriteAssetToTMP(text);
    }

    public void ShowPassiveDescription()
    {
        BuildActiveSkillInfoSplitTexts(ResolveActiveSkillForMP(), out _, out _, out var geki, out var shun, out var iyu, out _);
        string body = EquipmentText(
            "対応する役で和了すると、自動で効果が発動します。MPは消費しません。\nLv.0の役は未解放です。\n\n【撃】敵へのダメージを増加\n",
            "Win with a listed yaku to trigger its passive effect without spending MP.\nYaku at Lv.0 are locked.\n\n[Strike] Increases damage to the enemy\n",
            "以对应役种和牌时自动发动效果，不消耗MP。\nLv.0的役种尚未解锁。\n\n【击】增加对敌人的伤害\n") + geki
            + EquipmentText("\n\n【瞬】MPを回復\n", "\n\n[Flash] Restores MP\n", "\n\n【瞬】恢复MP\n") + shun
            + EquipmentText("\n\n【癒】HPを回復\n", "\n\n[Heal] Restores HP\n", "\n\n【愈】恢复HP\n") + iyu;
        ShowEquipmentDescription(EquipmentText("パッシブスキル", "Passive skills", "被动技能"), body);
    }

    public void ShowOmamoriDescription()
    {
        string body = PlayerData.EquippedOmamori > 0 ? PlayerData.GetOmamoriDesc_Localized(PlayerData.EquippedOmamori) : "";
        ShowEquipmentDescription(EquipmentText("装備お守り", "Equipped charm", "装备的护身符"),
            string.IsNullOrWhiteSpace(body) ? EquipmentText("お守りを装備していません。", "No charm equipped.", "未装备护身符。") : body);
    }

    public void ShowOfudaDescription()
    {
        var ids = OfudaRunInventory.LoadList();
        var descriptions = new List<string>();
        if (ids != null && ids.Count > 0)
        {
            var definitions = OfudaCatalog.BuildFromExcel(OfudaExcelLoader.Load());
            foreach (var id in ids.Take(3))
            {
                var def = definitions.FirstOrDefault(d => d.id == id);
                if (def == null) continue;
                descriptions.Add(ColorizeRarityWord_NoBrackets(def.displayName, def.rarity) + "\n" +
                    (string.IsNullOrWhiteSpace(def.description) ? StripRarityPrefixBracket(def.displayName) : def.description));
            }
        }
        ShowEquipmentDescription(EquipmentText("装備お札", "Equipped talismans", "装备的符札"),
            descriptions.Count == 0 ? EquipmentText("お札を装備していません。", "No talismans equipped.", "未装备符札。") : string.Join("\n\n", descriptions));
    }
}
