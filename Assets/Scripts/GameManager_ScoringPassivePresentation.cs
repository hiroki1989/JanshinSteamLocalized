using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public partial class GameManager
{
    readonly Dictionary<string,List<SkillSetAsset.Trait>> scoringPassiveHits=new Dictionary<string,List<SkillSetAsset.Trait>>();
    void RecordScoringPassive(string canonical,SkillSetAsset.Trait trait,float amount)
    {
        if(amount<=0)return;
        if(!scoringPassiveHits.TryGetValue(canonical,out var traits))scoringPassiveHits[canonical]=traits=new List<SkillSetAsset.Trait>();
        if(!traits.Contains(trait))traits.Add(trait);
    }
    void DecorateScoringPassives(string roles,bool isPlayer,float geki,float shun,float iyu)
    {
        if(!scoringRoleValue)return;
        var pulse=scoringRoleValue.GetComponent<ScoringPassivePulse>();
        if(!isPlayer){if(pulse)pulse.Restart(false);return;}
        var replacements=new Dictionary<string,string>();
        foreach(var raw in _lastScoringYaku)
        {
            string canonical=NormalizeTraitJudgeYakuName_Local(raw);
            if(!scoringPassiveHits.TryGetValue(canonical,out var hits))continue;
            var applicable=hits.Where(t=>t==SkillSetAsset.Trait.Geki?geki>0:t==SkillSetAsset.Trait.Shun?shun>0:iyu>0).ToList();
            if(applicable.Count==0)continue;
            string shown=__LocalizeSpecialYakumanToken_Local(raw);
            if(string.IsNullOrEmpty(shown))continue;
            Color color=applicable[0]==SkillSetAsset.Trait.Geki?traitIconColorGeki:applicable[0]==SkillSetAsset.Trait.Shun?traitIconColorShun:traitIconColorIyu;
            string icons="";
            foreach(var t in applicable)
            {
                int index=t==SkillSetAsset.Trait.Geki?traitSpriteIndexGeki:t==SkillSetAsset.Trait.Shun?traitSpriteIndexShun:traitSpriteIndexIyu;
                var c=t==SkillSetAsset.Trait.Geki?traitIconColorGeki:t==SkillSetAsset.Trait.Shun?traitIconColorShun:traitIconColorIyu;
                icons+=$"<sprite={index} tint=1 color=#{ColorUtility.ToHtmlStringRGBA(c)}>";
            }
            replacements[shown]=$"<link=\"passive:{ColorUtility.ToHtmlStringRGB(color)}\">{shown} {icons}</link>";
        }
        if(!pulse)pulse=scoringRoleValue.gameObject.AddComponent<ScoringPassivePulse>();
        if(replacements.Count>0)
        {
            // Longest first prevents a shorter yaku name from matching inside another.
            string pattern=string.Join("|",replacements.Keys.OrderByDescending(s=>s.Length).Select(Regex.Escape));
            ApplyTraitSpriteAssetToTMP(scoringRoleValue);
            scoringRoleValue.richText=true;
            scoringRoleValue.text=Regex.Replace(roles,pattern,m=>replacements[m.Value]);
        }
        pulse.Restart(replacements.Count>0);
        YakumanTextPresentation.Apply(scoringRoleValue);
    }
}
