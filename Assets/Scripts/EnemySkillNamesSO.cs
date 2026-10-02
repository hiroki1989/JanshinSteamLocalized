using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Janshin/Enemy Skill Display Names")]
public sealed class EnemySkillNamesSO : ScriptableObject
{
    [Serializable] public sealed class Entry {public string id,japanese,english,chineseSimplified;}
    public List<Entry> entries=new List<Entry>();
    static EnemySkillNamesSO cached;
    public static string Canonical(string raw)
    {
        switch((raw??"").Trim().ToLowerInvariant()){
            case "怒り":case "いかり":case "ikari":return "anger";
            case "毒":case "どく":case "doku":return "poison";
            case "麻痺":case "まひ":case "mahi":return "paralysis";
            case "攻撃":case "こうげき":return "attack";
            case "防御":case "防禦":case "防御力":case "ぼうぎょ":case "defence":return "defense";
            case "妨害":case "ぼうがい":case "jam":return "disturb";
            case "細工":case "さいく":case "saiku":return "trick";
            default:return (raw??"").Trim().ToLowerInvariant();
        }
    }
    public static bool TryName(string raw,out string name)
    {
        name=null;if(!cached)cached=Resources.Load<EnemySkillNamesSO>("EnemySkillNames");if(!cached)return false;
        string id=Canonical(raw);
        foreach(var e in cached.entries){
            if(Canonical(e.id)!=id)continue;
            var lang=LocalizationManager.Instance?LocalizationManager.Instance.CurrentLanguage:LocalizationManager.Language.Japanese;
            name=lang==LocalizationManager.Language.English?e.english:lang==LocalizationManager.Language.ChineseSimplified?e.chineseSimplified:e.japanese;
            if(string.IsNullOrWhiteSpace(name))name=e.japanese;
            return !string.IsNullOrWhiteSpace(name);
        }
        return false;
    }
}
