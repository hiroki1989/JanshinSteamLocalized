using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class UIThemeApplier : MonoBehaviour
{
    private static UIThemeApplier _instance;

    [Header("日本語フォント")]
    public TMP_FontAsset japaneseTMPFont;
    public Font japaneseUGUIFont;

    [Header("英語フォント")]
    public TMP_FontAsset englishTMPFont;
    public Font englishUGUIFont;

    [Header("簡体字フォント")]
    public TMP_FontAsset chineseSimplifiedTMPFont;
    public Font chineseSimplifiedUGUIFont;

    [Header("共通設定")]
    public TMP_SpriteAsset defaultTMPSprite;       // 絵文字など
    public Material tmpMaterialPreset;             // 必要なら指定（任意）
    public bool applyToInactive = true;            // 非アクティブにも適用
void Awake()
{
    if (_instance != null && _instance != this)
    {
        Destroy(gameObject);
        return;
    }

    _instance = this;
    DontDestroyOnLoad(gameObject);

    SceneManager.sceneLoaded -= OnSceneLoaded;
    SceneManager.sceneLoaded += OnSceneLoaded;

    LocalizationManager.LanguageChanged -= OnLanguageChanged;
    LocalizationManager.LanguageChanged += OnLanguageChanged;

    ApplyAll();
}
private void OnLanguageChanged(LocalizationManager.Language language)
{
    ApplyAll();
}
void OnDestroy()
{
    if (_instance == this)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
        _instance = null;
    }
}
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAll();
    }
    void ApplyAll()
    {
        TMP_FontAsset targetTMPFont = GetCurrentTMPFont();
        Font targetUGUIFont = GetCurrentUGUIFont();

        if (targetTMPFont != null) TMP_Settings.defaultFontAsset = targetTMPFont;
        if (defaultTMPSprite != null) TMP_Settings.defaultSpriteAsset = defaultTMPSprite;

        // TextMeshProUGUI
        var tmps = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        foreach (var t in tmps)
        {
            if (t == null) continue;
            if (!applyToInactive && !t.gameObject.activeInHierarchy) continue;

            if (targetTMPFont != null) t.font = targetTMPFont;
            if (tmpMaterialPreset != null) t.fontMaterial = tmpMaterialPreset;

            t.ForceMeshUpdate();
        }

        // 旧 UI Text（まだ残っている場合）
        var texts = Resources.FindObjectsOfTypeAll<Text>();
        foreach (var tx in texts)
        {
            if (tx == null) continue;
            if (!applyToInactive && !tx.gameObject.activeInHierarchy) continue;

            if (targetUGUIFont != null) tx.font = targetUGUIFont;
        }
    }

    private TMP_FontAsset GetCurrentTMPFont()
    {
        var loc = LocalizationManager.Instance;
        if (loc == null)
        {
            return japaneseTMPFont;
        }

        switch (loc.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                return englishTMPFont != null ? englishTMPFont : japaneseTMPFont;

            case LocalizationManager.Language.ChineseSimplified:
                return chineseSimplifiedTMPFont != null ? chineseSimplifiedTMPFont : japaneseTMPFont;

            case LocalizationManager.Language.Japanese:
            default:
                return japaneseTMPFont;
        }
    }

    private Font GetCurrentUGUIFont()
    {
        var loc = LocalizationManager.Instance;
        if (loc == null)
        {
            return japaneseUGUIFont;
        }

        switch (loc.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                return englishUGUIFont != null ? englishUGUIFont : japaneseUGUIFont;

            case LocalizationManager.Language.ChineseSimplified:
                return chineseSimplifiedUGUIFont != null ? chineseSimplifiedUGUIFont : japaneseUGUIFont;

            case LocalizationManager.Language.Japanese:
            default:
                return japaneseUGUIFont;
        }
    }
}
