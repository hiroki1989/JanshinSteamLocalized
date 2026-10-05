using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public partial class GameManager
{
    readonly List<string> contractWinEffects=new List<string>();
    void RecordContractDelta(string ja,string en,string zh,int before,int after)
    {
        int delta=after-before;if(delta==0)return;
        contractWinEffects.Add(EquipmentText(ja,en,zh)+" "+(delta>0?"+":"")+delta.ToString("N0"));
    }
    void ConfigureAppliedScoringEffects(bool player)
    {
        var panel=player?scoringPanelPlayer:scoringPanelEnemy;if(!panel)return;
        var view=panel.GetComponentInChildren<AppliedScoringEffectsView>(true);if(!view)return;
        view.Clear();
        string dmg=EquipmentText("ダメージ","Damage","伤害"), hp=EquipmentText("HP回復","HP recovery","HP恢复"),mp=EquipmentText("MP回復","MP recovery","MP恢复");
        bool ge=player&&HasScoringEffect(scoringGekiValue),sh=player&&HasScoringEffect(scoringShunValue),iy=player&&HasScoringEffect(scoringIyuValue);
        if(ge)view.Add(null,ReplaceTraitWordsWithIcons("撃")+" "+dmg+" +"+scoringGekiValue.text);
        if(iy)view.Add(null,ReplaceTraitWordsWithIcons("癒")+" "+hp+" "+scoringIyuValue.text);
        if(sh)view.Add(null,ReplaceTraitWordsWithIcons("瞬")+" "+mp+" "+scoringShunValue.text);
        if(player){
            EnsureOfudaMap();
            foreach(var id in LoadRunOfudaIds()??new List<string>()){
                var parts=id.Split(new[]{"__"},StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length!=2||!Ofuda_Cond_Passes_Runtime(parts[0]))continue;
                string effect=parts[1],line="";
                if(IsOfudaScoreMultiplierEffect(effect)&&ResolveMultiplier(effect)>0&&!Mathf.Approximately(ResolveMultiplier(effect),1))line=dmg+" ×"+ResolveMultiplier(effect).ToString("0.###");
                else if(IsOfudaHpHealEffect(effect)&&ResolvePercent01(effect)>0&&HasScoringEffect(scoringOfudaHpValue))line=hp+" "+(ResolvePercent01(effect)*100).ToString("0.###")+"%";
                else if(IsOfudaMpHealEffect(effect)&&ResolvePercent01(effect)>0&&HasScoringEffect(scoringOfudaMpValue))line=mp+" "+(ResolvePercent01(effect)*100).ToString("0.###")+"%";
                if(line.Length==0)continue;
                var def=AppliedOfudaDefinition(id);
                view.Add(ItemArtwork.Load("Body/ofuda_"+ItemArtwork.RarityKey(def?.rarity)),line);
            }
        }
        var ids=PlayerData.EquippedOmamoriIds??new List<int>();
        foreach(int id in ids.Where(i=>i>0).Distinct()){
            if(!PlayerData.TryGetOmamori(id,out var item)||item==null)continue;
            var lines=new List<string>();
            foreach(var e in item.effects??new List<PlayerData.EffectEntry>()){
                float pct=Mathf.Round(e.amountPercent*100);if(pct<=0)continue;
                if(player&&e.type==PlayerData.OmamoriEffect.GekiDamagePercentUp&&ge)lines.Add(ReplaceTraitWordsWithIcons("撃")+" +"+pct+"%");
                if(player&&e.type==PlayerData.OmamoriEffect.ShunAddPercentUp&&sh)lines.Add(ReplaceTraitWordsWithIcons("瞬")+" +"+pct+"%");
                if(player&&e.type==PlayerData.OmamoriEffect.IyuHealPercentUp&&iy)lines.Add(ReplaceTraitWordsWithIcons("癒")+" +"+pct+"%");
                if(!player&&e.type==PlayerData.OmamoriEffect.DamageTakenPercentDown)lines.Add(dmg+" -"+pct+"%");
            }
            if(item.isUnique){
                if(player&&(item.uniqueKind==PlayerData.UniqueOmamoriEffectKind.Zeus_DamageUp30||roundNumber==1&&item.uniqueKind==PlayerData.UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50))
                    lines.Add(dmg+(item.uniqueKind==PlayerData.UniqueOmamoriEffectKind.Zeus_DamageUp30?" +30%":" +50%"));
                if(!player&&roundNumber==1&&item.uniqueKind==PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50)lines.Add(dmg+" -50%");
            }
            if(lines.Count>0)view.Add(ItemArtwork.Load("Body/"+ItemArtwork.OmamoriKey(item)),string.Join(" / ",lines));
        }
        // Read the skill that actually modified this win, not the current HUD timer.
        if(player&&_enemySkillLastAppliedDefenseRate>0f)
            AddAppliedEnemySkill(view,enemySkillDefenseIcon,"defense",dmg+" -"+(_enemySkillLastAppliedDefenseRate*100).ToString("0.###")+"%");
        if(!player&&_enemySkillLastAppliedAngerMultiplier>1f)
            AddAppliedEnemySkill(view,enemySkillAngerIcon,"anger",dmg+" +"+((_enemySkillLastAppliedAngerMultiplier-1)*100).ToString("0.###")+"%");
        if(player&&_consumableBloodPactAppliedThisScoring)
            view.Add(Resources.Load<Sprite>("Consumables/item_20"),dmg+" +50%");
        if(!player)foreach(var effect in incomingRelicScoreEffects)
            view.Add(Resources.Load<Sprite>("Consumables/item_"+effect.Key.ToString("00")),effect.Value);
        if(!player&&(_legendaryDamageHalfTriggeredThisScoring||IsLegendaryDamageHalfActive())){
            var sources=_legendaryDamageHalfTriggeredThisScoring?_legendaryDamageHalfTriggeredSourceTiles:_legendaryDamageHalfReservedSourceTiles;
            var source=sources.FirstOrDefault();
            view.Add(string.IsNullOrEmpty(source)?null:Resources.Load<Sprite>("Sprites/Tiles/"+StripTileIdForLogic(source)),EquipmentText("被ダメージ -50%","Incoming damage -50%","所受伤害 -50%"));
            if(scoringSpecialTileDamageEffectValue_Enemy){scoringSpecialTileDamageEffectValue_Enemy.text="";view.inactiveObjects.Add(scoringSpecialTileDamageEffectValue_Enemy.gameObject);}
        }
        if(DevilContracts.Active&&contractWinEffects.Count>0){var run=DevilContracts.Run();view.Add(DevilContractIcons.Get(run.id),string.Join(" / ",contractWinEffects));}
        HideEmptyScoreTotal(view,player?scoringAddedDamageValue:scoringAddedDamageValue_Enemy,"LabelscoringAddedDamageValue");
        HideEmptyScoreTotal(view,player?scoringTotalHpRecoverValue:scoringTotalHpRecoverValue_Enemy,"LabelscoringTotalHpRecoverValue");
        HideEmptyScoreTotal(view,player?scoringTotalMpRecoverValue:scoringTotalMpRecoverValue_Enemy,"LabelscoringTotalMpRecoverValue");
        view.Suppress();
    }
    readonly List<KeyValuePair<int,string>> incomingRelicScoreEffects=new List<KeyValuePair<int,string>>();
    void AddAppliedEnemySkill(AppliedScoringEffectsView view,GameObject hud,string skill,string effect)
    {
        var image=hud?hud.GetComponentInChildren<UnityEngine.UI.Image>(true):null;
        view.Add(image?image.sprite:null,EnemySkills_GetDisplayName(skill)+"  "+effect,image?(Color?)image.color:null);
    }
    // Preview has no persistent side effects. OK commits the same pipeline exactly once.
    int CalculateEnemyWinIncoming(int baseDamage,bool commit)
    {
        contractWinEffects.Clear();incomingRelicScoreEffects.Clear();
        int damage=Omamori_ModifyIncomingDamage(Mathf.Max(0,baseDamage));
        if(roundNumber==1&&PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50))
            damage=Mathf.RoundToInt(damage*.5f);
        damage=commit?TryConsumeLegendaryDamageHalfOnEnemyWin(damage):PreviewLegendaryDamageHalfOnEnemyWin(damage);
        int before=damage;damage=DevilContracts.Incoming(damage,commit);
        RecordContractDelta("被ダメージ","Incoming damage","所受伤害",before,damage);
        var state=RunConsumables.Load();
        if(damage>0){
            if(state.bloodPact){before=damage;damage=Mathf.CeilToInt(damage*1.25f);RecordIncomingRelic(20,before,damage);}
            if(state.shield){before=damage;damage=Mathf.CeilToInt(damage*.5f);state.shield=false;RecordIncomingRelic(13,before,damage);}
            if(damage>=playerHP&&playerHP>0&&state.effigy){before=damage;damage=playerHP-1;state.effigy=false;RecordIncomingRelic(15,before,damage);}
        }
        if(commit)RunConsumables.Save(state);
        return Mathf.Max(0,damage);
    }
    void RecordIncomingRelic(int id,int before,int after)
    {
        int delta=after-before;if(delta==0)return;
        incomingRelicScoreEffects.Add(new KeyValuePair<int,string>(id,EquipmentText("被ダメージ","Incoming damage","所受伤害")+" "+(delta>0?"+":"")+delta.ToString("N0")));
    }
    void HideEmptyScoreTotal(AppliedScoringEffectsView view,TMP_Text value,string labelName)
    {
        if(!value)return;bool show=HasScoringEffect(value);value.gameObject.SetActive(show);
        var label=value.transform.parent.Find(labelName);if(label)label.gameObject.SetActive(show);
        if(!show){view.inactiveObjects.Add(value.gameObject);if(label)view.inactiveObjects.Add(label.gameObject);}
    }
    static bool HasScoringEffect(TMP_Text text)=>text&&!string.IsNullOrWhiteSpace(text.text)&&text.text!="-"&&text.text!="ー"&&text.text!="0"&&text.text!="0％"&&text.text!="0%";
    OfudaDef AppliedOfudaDefinition(string id)
    {
        if(_ofudaMap.TryGetValue(id,out var def))return def;
        // Editor projects without the source CSV still have the shipped catalogue asset.
        var so=Resources.Load<OfudaCatalogSO>("OfudaCatalogSO");if(!so)return null;
        var catalog=new OfudaExcelLoader.Catalog{conditions=so.conditions,effects=so.effects};
        foreach(var band in so.priceMap)catalog.priceMap.Add((band.maxProbSum,band.rarity,band.priceK,band.priceFixed));
        return OfudaCatalog.BuildFromExcel(catalog).FirstOrDefault(d=>d.id==id);
    }
    List<GameObject> AppliedScoringStepRoots(bool player)
    {
        var source=player?scoringStepRoots_Player:scoringStepRoots_Enemy;
        var panel=player?scoringPanelPlayer:scoringPanelEnemy;
        var view=panel?panel.GetComponentInChildren<AppliedScoringEffectsView>(true):null;
        if(!view)return source;
        var list=new List<GameObject>();bool inserted=false;
        foreach(var go in source){
            if(view.Replaces(go)){if(!view.inactiveObjects.Contains(go)&&!inserted){list.AddRange(view.VisibleRows());inserted=true;}continue;}
            if(go)list.Add(go);
        }
        if(!inserted)list.AddRange(view.VisibleRows());
        return list;
    }
}
