using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// One stable role identity for stored bonuses, battle displays, scoring and the shop.
public static class SpecialTilePassiveBonuses
{
    static readonly string[] roles={
        "PINFU|平和|ピンフ|Pinfu", "TANYAO|タンヤオ|断么九|断幺九|断ヤオ九|Tanyao",
        "IIPEIKOU|一盃口|一杯口|Iipeikou", "RYANPEIKOU|二盃口|二杯口|Ryanpeikou",
        "SANSHOKU_DOUJUN|三色同順|三色同顺|Sanshoku Doujun", "ITTSU|一気通貫|一气通贯|Ittsu",
        "CHANTA|チャンタ|混全帯么九|混全带幺九|Chanta", "JUNCHAN|純チャン|純全帯么九|纯全带幺九|Junchan",
        "TOITOI|対々和|対対和|对对和|Toitoi", "SANANKOU|三暗刻|Sanankou",
        "SANKANTSU|三カンツ|三槓子|三杠子|Sankantsu", "SUUKANTSU|四カンツ|四槓子|四杠子|Suukantsu",
        "SANSHOKU_DOUKOU|三色同刻|Sanshoku Doukou", "SHOUSANGEN|小三元|Shousangen",
        "HONROUTOU|混老頭|混老头|Honroutou", "HONITSU|混一色|ホンイツ|Honitsu",
        "CHINITSU|清一色|Chinitsu", "CHIITOITSU|七対子|七对子|Chiitoitsu",
        "YAKUHAI|役牌|風牌|风牌|白|發|発|中|Yakuhai",
        "KOKUSHI|国士無双|国士无双|Kokushi|Kokushi Musou",
        "SUUANKOU|四暗刻|Suuankou", "DAISANGEN|大三元|Daisangen", "TSUUIISOU|字一色|Tsuuiisou",
        "RYUUIISOU|緑一色|绿一色|Ryuuiisou", "CHINROUTOU|清老頭|清老头|Chinroutou",
        "SHOUSUUSHI|小四喜|Shousuushi", "DAISUUSHI|大四喜|Daisuushi",
        "CHUUREN_POUTOU|九蓮宝燈|九蓮宝灯|九莲宝灯|Chuuren Poutou",
        "MENZEN_TSUMO|門前清自摸和|门前清自摸和|Menzen Tsumo",
        "RIICHI|立直|リーチ|Riichi", "IPPATSU|一発|一发|Ippatsu",
        "RINSHAN|嶺上開花|岭上开花|Rinshan Kaihou", "CHANKAN|槍槓|抢杠|Chankan"
    };
    static readonly Dictionary<string,string> aliases=BuildAliases();
    static string Compact(string value)=>Regex.Replace(value??"",@"[\s_\-]","").ToUpperInvariant();
    static Dictionary<string,string> BuildAliases(){
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var role in roles){var values=role.Split('|');foreach(var value in values)result[Compact(value)]=values[1];}
        return result;
    }
    public static string Normalize(string raw){
        if(string.IsNullOrWhiteSpace(raw))return "";
        string value=raw.Trim().Replace('（','(').Replace('）',')');
        int suffix=value.IndexOf('(');if(suffix>=0)value=value.Substring(0,suffix).Trim();
        value=Regex.Replace(value,@"\s*[×xX]\s*\d+\s*$","").Trim();
        if(value.StartsWith("yakuman.",StringComparison.OrdinalIgnoreCase))value=value.Substring(8);
        else if(value.StartsWith("yaku.",StringComparison.OrdinalIgnoreCase))value=value.Substring(5);
        if(value.StartsWith("風牌",StringComparison.Ordinal)||value.StartsWith("风牌",StringComparison.Ordinal)||value.StartsWith("役牌",StringComparison.Ordinal))return "役牌";
        if(aliases.TryGetValue(Compact(value),out var canonical))return canonical;
        // Old saves may contain a localized display name rather than the canonical key.
        var lm=LocalizationManager.Instance;
        if(lm!=null)foreach(var role in roles){var values=role.Split('|');
            if(Compact(value)==Compact(lm.GetYakuDisplayName(values[0]))||Compact(value)==Compact(lm.GetYakumanDisplayName(values[0])))return values[1];
        }
        return value;
    }
}
