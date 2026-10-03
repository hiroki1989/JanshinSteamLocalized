using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public partial class GameManager
{
    static string InspectText(string ja,string en,string zh)=>GameUIText.Get(ja,en,zh);
    public void RefreshBattleInspection(BattleInspectionHUD ui)
    {
        var run=DevilContracts.Run();
        bool contract=DevilContracts.Active && DevilContractCatalog.Get(run.id)!=null;
        ui.contractButton.gameObject.SetActive(contract);
        if(contract){ui.contractIcon.sprite=DevilContractIcons.Get(run.id);}
        ui.enemySkillsLabel.text=string.Join(" / ",_enemySkills.Where(s=>s!=null&&!string.IsNullOrEmpty(s.id)).Select(s=>EnemySkills_GetDisplayName(s.id)).Distinct());
        ui.enemySkillsButton.gameObject.SetActive(!string.IsNullOrEmpty(ui.enemySkillsLabel.text));
        var font=LocalizationManager.Instance.GetBodyFont()??TMP_Settings.defaultFontAsset;
        if(LocalizationManager.Instance.CurrentLanguage==LocalizationManager.Language.ChineseSimplified&&!font.HasCharacter('恶',true,true))font=Resources.Load<TMP_FontAsset>("Tutorial/Fonts/ChineseSimplified")??font;
        foreach(var label in new[]{ui.specialTilesLabel,ui.enemySkillsLabel})if(label.font!=font){label.font=font;label.fontSharedMaterial=font.material;}
        ui.specialTilesLabel.text=InspectText("特別牌","Special tiles","特殊牌");
        bool available=!_preparedForSceneUnload&&!_tutorialRunning&&!isMenuOpen&&phase!=Phase.Scoring&&!_enemySkillCutinRunning&&!_playerSkillCutinRunning;
        ui.contractButton.interactable=available;
        ui.enemySkillsButton.interactable=available;
        ui.specialTilesButton.interactable=available;
    }
    public string BuildBattleContractDescription()
    {
        if(!DevilContracts.Active)return "";
        var run=DevilContracts.Run();var d=DevilContractCatalog.Get(run.id);if(d==null)return "";
        var lines=new List<string>{d.title.Text,
            "<color=#E2C789>"+InspectText("基本の恩恵","Base benefit","基础恩惠")+"</color>\n"+d.benefit.Text,
            "<color=#D89187>"+InspectText("基本の代償","Base price","基础代价")+"</color>\n"+d.drawback.Text};
        if(run.mercyRank>0)lines.Add(d.mercy[Mathf.Clamp(run.mercyRank,1,10)-1].description.Text);
        if(run.powerRank>0)lines.Add(d.power[Mathf.Clamp(run.powerRank,1,10)-1].description.Text);
        if(run.resonanceRank>0)lines.Add(d.resonance[Mathf.Clamp(run.resonanceRank,1,10)-1].description.Text);
        if(!string.IsNullOrEmpty(run.temporaryYaku)){
            string icon=run.temporaryTrait==(int)SkillSetAsset.Trait.Geki?"撃":run.temporaryTrait==(int)SkillSetAsset.Trait.Shun?"瞬":"癒";
            lines.Add(InspectText("この局の一時解放：","Temporary passive for this hand: ","本局临时解锁：")+ReplaceTraitWordsWithIcons(icon)+" "+InspectYaku(run.temporaryYaku)+" Lv.1");
        }
        return ReplaceTraitWordsWithIcons(string.Join("\n\n",lines));
    }
    public void ShowBattleContract()
    {
        var run=DevilContracts.Run();var d=DevilContractCatalog.Get(run.id);
        if(!DevilContracts.Active || d==null)return;
        AudioManager.Instance?.PlayClickSE();ShowEquipmentDescription(d.Name,BuildBattleContractDescription());
    }
    public void ShowBattleEnemySkills()
    {
        var lines=new List<string>();
        foreach(var skill in _enemySkills.Where(s=>s!=null&&!string.IsNullOrEmpty(s.id)))
            lines.Add("<color=#E2C789>"+EnemySkills_GetDisplayName(skill.id)+"</color>\n"+BuildBattleEnemySkillDescription(skill));
        if(lines.Count==0)return;
        AudioManager.Instance?.PlayClickSE();ShowEquipmentDescription(InspectText("神のスキル","God's skills","神的技能"),string.Join("\n\n",lines));
    }
    public string BuildBattleEnemySkillDescription(EnemySkillConfig skill)
    {
        int x=Mathf.Max(0,skill.paramX),y=Mathf.Max(1,skill.paramY);
        int damage=Mathf.Max(1,Mathf.RoundToInt(Mathf.Max(1,skill.paramX)*GetCurrentTierMultiplier()));
        int poison=Mathf.Max(1,Mathf.RoundToInt(y*GetCurrentTierMultiplier()));
        string value;
        switch(EnemySkillNamesSO.Canonical(skill.id)){
            case "anger":value=InspectText($"{y}ターン、神の和了ダメージが{x}%増加",$"God's win damage +{x}% for {y} turns",$"持续{y}回合，神的和牌伤害增加{x}%");break;
            case "poison":value=InspectText($"{Mathf.Max(1,x)}ターン、毎ターンHPに{poison:N0}ダメージ",$"{poison:N0} HP damage each turn for {Mathf.Max(1,x)} turns",$"持续{Mathf.Max(1,x)}回合，每回合受到{poison:N0}点HP伤害");break;
            case "paralysis":value=InspectText($"{Mathf.Max(1,x)}ターン、アクティブスキル使用不可",$"Active skills disabled for {Mathf.Max(1,x)} turns",$"{Mathf.Max(1,x)}回合内无法使用主动技能");break;
            case "attack":value=InspectText($"プレイヤーのHPに{damage:N0}ダメージ",$"Deals {damage:N0} damage to the player's HP",$"对玩家造成{damage:N0}点HP伤害");break;
            case "defense":value=InspectText($"{y}ターン、プレイヤーの和了ダメージを{Mathf.Clamp(x,0,100)}%軽減",$"Player's win damage reduced by {Mathf.Clamp(x,0,100)}% for {y} turns",$"持续{y}回合，玩家的和牌伤害降低{Mathf.Clamp(x,0,100)}%");break;
            case "disturb":value=InspectText($"プレイヤーのMPを{x}減少",$"Reduces the player's MP by {x}",$"减少玩家{x}点MP");break;
            case "trick":value=InspectText($"手牌を{Mathf.Max(1,x)}枚入れ替え",$"Replaces {Mathf.Max(1,x)} tiles in the player's hand",$"替换玩家手牌中的{Mathf.Max(1,x)}张牌");break;
            default:value=EnemySkills_GetDisplayName(skill.id);break;
        }
        return value;
    }
    static string InspectYaku(string key)
    {
        return LocalizeSpecialTileYakuName_Local(key);
    }
    public string BuildBattleSpecialTileDescription()
    {
        var equipped=SpecialTileSystem.GetEquipped();var lines=new List<string>();
        lines.Add("<color=#E2C789>"+InspectText("パッシブスキル強化","Passive skill bonuses","被动技能强化")+"</color>");
        var bonuses=SpecialTileSystem.GetEquippedTraitBonusMap();
        var (ge,sh,iy,host)=GetCurrentSkillTraitYakuForScoring();
        foreach(var kv in bonuses.OrderBy(k=>k.Key)){
            var traits=new List<string>();
            if(ge.Any(y=>SpecialTilePassiveBonuses.Normalize(y)==kv.Key))traits.Add("撃");if(sh.Any(y=>SpecialTilePassiveBonuses.Normalize(y)==kv.Key))traits.Add("瞬");if(iy.Any(y=>SpecialTilePassiveBonuses.Normalize(y)==kv.Key))traits.Add("癒");
            string text=InspectYaku(kv.Key)+"　Lv.+"+kv.Value;
            lines.Add(traits.Count>0?ReplaceTraitWordsWithIcons(string.Join(" ",traits))+" "+text:"<color=#92969B>"+text+"</color>");
        }
        if(bonuses.Count==0)lines.Add(InspectText("強化効果なし","No passive bonuses","无强化效果"));
        lines.Add("\n<color=#E2C789>"+InspectText("レジェンダリー効果","Legendary effects","传奇效果")+"</color>");
        var effects=equipped.Where(e=>e.rarity==SpecialTileSystem.Rarity.Legendary&&e.effectId>0&&e.effectId<=6).Select(e=>e.effectId).Distinct().OrderBy(id=>id).ToList();
        // A previously won tile can reserve a defensive effect even after it leaves the hand.
        if(IsLegendaryDamageHalfActive()&&!effects.Contains(2))effects.Add(2);
        if(IsLegendaryHalfMpCostActive()&&!effects.Contains(5))effects.Add(5);
        foreach(int effect in effects){
            bool active=effect==2?IsLegendaryDamageHalfActive():effect==5?IsLegendaryHalfMpCostActive():effect==3?_legendaryGoldDoubleThisScoring&&phase==Phase.Scoring:effect==4?_legendaryTraitDoubleIfUnderManganThisScoring&&phase==Phase.Scoring:false;
            string text=GetGameFixedText_Local("special_tile_legendary_effect_"+effect)+"　"+(active?InspectText("発動中","Active","已生效"):InspectText("未発動","Inactive","未生效"));
            text=ReplaceTraitWordsWithIcons(text);
            if(!active)text=System.Text.RegularExpressions.Regex.Replace(text,"color=#[0-9A-Fa-f]{6,8}","color=#92969B");
            lines.Add("<color="+(active?"#EEA347":"#92969B")+">"+text+"</color>");
        }
        if(effects.Count==0)lines.Add(InspectText("効果なし","No legendary effects","无传奇效果"));
        if(effects.Count>0)lines.Add("\n"+InspectText("和了時の効果は、対象の特別牌を和了形に含めると発動します。","Win-triggered effects activate when the matching special tile is included in the winning hand.","和牌触发的效果仅在和牌形包含对应特殊牌时发动。"));
        return ReplaceTraitWordsWithIcons(string.Join("\n",lines));
    }
    public void ShowBattleSpecialTiles()
    {
        AudioManager.Instance?.PlayClickSE();ShowEquipmentDescription(InspectText("特別牌の装備効果","Equipped special tile effects","特殊牌装备效果"),BuildBattleSpecialTileDescription());
    }
}
