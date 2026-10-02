using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;

public static class YakumanTextPresentation
{
    static IEnumerable<string> Names()
    {
        var names=new List<string>{"国士無双十三面待ち","純正九蓮宝燈","四暗刻単騎","国士無双","九蓮宝燈","九蓮宝燈","大三元","大四喜","小四喜","字一色","清老頭","緑一色","四暗刻","四カンツ","四槓子","天和","地和","人和"};
        var lm=LocalizationManager.Instance;
        if(lm)foreach(var key in new[]{"KOKUSHI","CHUUREN_POUTOU","DAISANGEN","DAISUUSHI","SHOUSUUSHI","TSUUIISOU","CHINROUTOU","RYUUIISOU","SUUANKOU","SUUKANTSU","TENHOU","CHIHOU","RENHOU"}){
            string name=lm.GetYakumanDisplayName(key);if(!string.IsNullOrEmpty(name)&&!name.Contains("."))names.Add(name);
        }
        return names.Distinct().OrderByDescending(s=>s.Length);
    }
    public static string Wrap(string text)
    {
        string pattern=string.Join("|",Names().Select(Regex.Escape));
        return Regex.Replace(text??"","<link=[^>]+>.*?</link>|"+pattern,m=>{
            if(m.Value.StartsWith("<link="))return Regex.IsMatch(m.Value,pattern)?m.Value.Replace("passive:","yakuman:"):m.Value;
            return "<link=\"yakuman:FFD386:"+m.Value+"\">"+m.Value+"</link>";
        });
    }
    public static void Apply(TMP_Text text,bool restart=true)
    {
        if(!text)return;
        text.richText=true;text.text=Wrap(text.text);
        var pulse=text.GetComponent<ScoringPassivePulse>();
        bool has=text.text.Contains("yakuman:")||text.text.Contains("passive:");
        if(!pulse&&has){pulse=text.gameObject.AddComponent<ScoringPassivePulse>();restart=true;}
        if(pulse&&restart)pulse.Restart(has);
    }
}
