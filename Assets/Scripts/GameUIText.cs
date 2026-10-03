using TMPro;
public static class GameUIText {
 public static string Get(string ja,string en,string zh) { var l=LocalizationManager.Instance.CurrentLanguage; return l==LocalizationManager.Language.English?en:l==LocalizationManager.Language.ChineseSimplified?zh:ja; }
 public static void Font(TMP_Text text) { if(text && TMP_Settings.defaultFontAsset) text.font=TMP_Settings.defaultFontAsset; }
}
