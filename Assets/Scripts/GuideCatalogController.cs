using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class GuideCatalogController : MonoBehaviour
{
[Serializable]
public sealed class GuideEntry
{
    [Header("Label (Legacy / Japanese)")]
    public string title;

    [Header("Label - Japanese")]
    public string titleJapanese;

    [Header("Label - English")]
    public string titleEnglish;

    [Header("Label - Chinese Simplified")]
    public string titleChineseSimplified;

    [Header("TOC Button (目次ボタン)")]
    public Button tocButton;

    [Header("TOC Label TMP (目次ボタンの表示テキスト)")]
    public TextMeshProUGUI tocLabelTMP;

    [Header("TOC Unread Icon (未読アイコン)")]
    public GameObject unreadIcon;

    [Header("Persistent Read Key (既読管理キー)")]
    public string readKey;

    [Header("Content Root - Japanese")]
    public GameObject contentRoot;

    [Header("Content Root - English")]
    public GameObject contentRootEnglish;

    [Header("Content Root - Chinese Simplified")]
    public GameObject contentRootChineseSimplified;

    [Header("Close Button (このコンテンツ内に置く閉じるボタン)")]
    public Button closeButton;

    public string GetPersistentReadKey()
    {
        if (!string.IsNullOrWhiteSpace(readKey)) return readKey.Trim();
        if (!string.IsNullOrWhiteSpace(titleJapanese)) return titleJapanese.Trim();
        if (!string.IsNullOrWhiteSpace(title)) return title.Trim();
        if (!string.IsNullOrWhiteSpace(titleEnglish)) return titleEnglish.Trim();
        if (!string.IsNullOrWhiteSpace(titleChineseSimplified)) return titleChineseSimplified.Trim();
        return "";
    }
    public string GetLocalizedTitle()
    {
        var lm = LocalizationManager.Instance;

        if (lm == null)
        {
            if (!string.IsNullOrEmpty(titleJapanese)) return titleJapanese;
            return title ?? "";
        }

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(titleEnglish)) return titleEnglish;
                if (!string.IsNullOrEmpty(titleJapanese)) return titleJapanese;
                return title ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(titleChineseSimplified)) return titleChineseSimplified;
                if (!string.IsNullOrEmpty(titleJapanese)) return titleJapanese;
                return title ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                if (!string.IsNullOrEmpty(titleJapanese)) return titleJapanese;
                return title ?? "";
        }
    }

    public GameObject GetLocalizedContentRoot()
    {
        var lm = LocalizationManager.Instance;

        if (lm == null)
        {
            if (contentRoot != null) return contentRoot;
            if (contentRootEnglish != null) return contentRootEnglish;
            if (contentRootChineseSimplified != null) return contentRootChineseSimplified;
            return null;
        }

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (contentRootEnglish != null) return contentRootEnglish;
                if (contentRoot != null) return contentRoot;
                if (contentRootChineseSimplified != null) return contentRootChineseSimplified;
                return null;

            case LocalizationManager.Language.ChineseSimplified:
                if (contentRootChineseSimplified != null) return contentRootChineseSimplified;
                if (contentRoot != null) return contentRoot;
                if (contentRootEnglish != null) return contentRootEnglish;
                return null;

            case LocalizationManager.Language.Japanese:
            default:
                if (contentRoot != null) return contentRoot;
                if (contentRootEnglish != null) return contentRootEnglish;
                if (contentRootChineseSimplified != null) return contentRootChineseSimplified;
                return null;
        }
    }
}
    [Header("TOC Root (目次一覧のルート)")]
    [SerializeField] private GameObject tocRoot;

    [Header("Guide Entries (Inspector の List で + 追加していく)")]
    [SerializeField] private List<GuideEntry> entries = new List<GuideEntry>();

    private int _currentIndex = -1;
[Header("Trait Icon Replacement (Guide)")]
[SerializeField] private bool replaceTraitWordsWithIcons = true;

// 撃/瞬/癒 が全部入った TMP Sprite Asset（1つ）を割り当てる
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAsset = null;

// 置換対象文字
[SerializeField] private string traitWordGeki = "撃";
[SerializeField] private string traitWordShun = "瞬";
[SerializeField] private string traitWordIyu  = "癒";

// Sprite Asset 内の index（0始まり）
[SerializeField] private int traitSpriteIndexGeki = 0;
[SerializeField] private int traitSpriteIndexShun = 1;
[SerializeField] private int traitSpriteIndexIyu  = 2;

// サイズ（%）
[SerializeField, Range(50, 150)] private int traitIconSizePercent = 100;

// 上下位置（em）
[SerializeField] private float traitIconVOffsetEm = 0f;

// 同じTMPに何度も同じ文字を入れないためのキャッシュ
private readonly Dictionary<TMP_Text, string> _guideTmpLastRaw = new Dictionary<TMP_Text, string>();
private readonly Dictionary<TMP_Text, string> _guideTmpLastRendered = new Dictionary<TMP_Text, string>();

private void ApplyTraitSpriteAssetToTMP(TMP_Text tmp)
{
    if (!tmp) return;
    if (traitIconsSpriteAsset == null) return;
    tmp.spriteAsset = traitIconsSpriteAsset;
}
private void RefreshLocalizedLabels()
{
    if (entries == null) return;

    for (int i = 0; i < entries.Count; i++)
    {
        var e = entries[i];
        if (e == null) continue;

        if (e.tocLabelTMP)
        {
            e.tocLabelTMP.text = e.GetLocalizedTitle();
        }
    }
}

private void RefreshUnreadIcons()
{
    if (entries == null) return;

    for (int i = 0; i < entries.Count; i++)
    {
        var e = entries[i];
        if (e == null) continue;
        if (!e.unreadIcon) continue;

        string key = e.GetPersistentReadKey();
        bool unread = !PlayerData.HasReadGuide(key);

        if (e.unreadIcon.activeSelf != unread)
        {
            e.unreadIcon.SetActive(unread);
        }
    }
}

private void RefreshAnyUnreadCache()
{
    bool anyUnread = false;

    if (entries != null)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            string key = e.GetPersistentReadKey();
            if (string.IsNullOrEmpty(key)) continue;

            if (!PlayerData.HasReadGuide(key))
            {
                anyUnread = true;
                break;
            }
        }
    }

    PlayerData.SetHasAnyUnreadGuide(anyUnread);
}

private void RefreshReadStateUI()
{
    RefreshUnreadIcons();
    RefreshAnyUnreadCache();
}

private void MarkGuideAsRead(int index)
{
    if (entries == null) return;
    if (index < 0 || index >= entries.Count) return;

    var e = entries[index];
    if (e == null) return;

    string key = e.GetPersistentReadKey();
    if (string.IsNullOrEmpty(key)) return;

    if (PlayerData.HasReadGuide(key)) return;

    PlayerData.MarkGuideAsRead(key);
}
private string ReplaceTraitWordsWithIcons_Fast(string src)
{
    if (!replaceTraitWordsWithIcons) return src;
    if (string.IsNullOrEmpty(src)) return src;

    // まず対象文字が含まれていないなら即return
    bool hasAny = false;
    for (int i = 0; i < src.Length; i++)
    {
        char c = src[i];
        if (c == '撃' || c == '瞬' || c == '癒')
        {
            hasAny = true;
            break;
        }
    }
    if (!hasAny) return src;

    string WrapWithVOffset(string inner)
    {
        if (Mathf.Abs(traitIconVOffsetEm) < 0.0001f) return inner;
        return $"<voffset={traitIconVOffsetEm}em>{inner}</voffset>";
    }

    string MakeTag(int spriteIndex)
    {
        if (spriteIndex < 0) return "";
        string baseTag;
        if (traitIconSizePercent != 100)
            baseTag = $"<size={traitIconSizePercent}%><sprite={spriteIndex}></size>";
        else
            baseTag = $"<sprite={spriteIndex}>";
        return WrapWithVOffset(baseTag);
    }

    System.Text.StringBuilder sb = new System.Text.StringBuilder(src.Length + 16);

for (int i = 0; i < src.Length; i++)
{
    char c = src[i];

    if (c == '撃' && traitWordGeki == "撃")
    {
        // 「撃破」だけは絶対に置換しない
        if (i + 1 < src.Length && src[i + 1] == '破')
        {
            sb.Append(c);
            continue;
        }

        sb.Append(MakeTag(traitSpriteIndexGeki));
        continue;
    }

    if (c == '瞬' && traitWordShun == "瞬")
    {
        sb.Append(MakeTag(traitSpriteIndexShun));
        continue;
    }

    if (c == '癒' && traitWordIyu == "癒")
    {
        sb.Append(MakeTag(traitSpriteIndexIyu));
        continue;
    }

    sb.Append(c);
}

return sb.ToString();
}
private void ApplyTraitIconsToContentRoot(GameObject contentRoot)
{
    if (!replaceTraitWordsWithIcons) return;
    if (!contentRoot) return;

    // contentRoot 配下の TMP を全部対象にする（非アクティブも含む）
    var tmps = contentRoot.GetComponentsInChildren<TMP_Text>(true);
    if (tmps == null || tmps.Length == 0) return;

    for (int i = 0; i < tmps.Length; i++)
    {
        var tmp = tmps[i];
        if (!tmp) continue;

        tmp.richText = true;
        ApplyTraitSpriteAssetToTMP(tmp);

        string raw = tmp.text ?? "";

        // キャッシュが同じなら更新しない
        if (_guideTmpLastRaw.TryGetValue(tmp, out string lastRaw) && string.Equals(lastRaw, raw, StringComparison.Ordinal))
        {
            continue;
        }

        _guideTmpLastRaw[tmp] = raw;

        string rendered = ReplaceTraitWordsWithIcons_Fast(raw);
        _guideTmpLastRendered[tmp] = rendered;

        if (!string.Equals(tmp.text, rendered, StringComparison.Ordinal))
        {
            tmp.text = rendered;
        }
    }
}
private System.Collections.IEnumerator ApplyTraitIconsNextFrame(GameObject root)
{
    // 何らかの初期化で text が後から上書きされるケースに備えて数フレーム追従
    for (int k = 0; k < 3; k++)
    {
        yield return null;
        ApplyTraitIconsToContentRoot(root);
    }
}
private void Awake()
{
    WireButtons();
    RefreshLocalizedLabels();
    RefreshReadStateUI();
}
private void OnEnable()
{
    OnEnableLanguageHook();
    RefreshLocalizedLabels();
    RefreshReadStateUI();

    // ガイドルートに入ったら必ず目次を見せる
    CloseCurrentContent();
}
private void OnDisable()
{
    LocalizationManager.LanguageChanged -= HandleLanguageChanged;
}

private void OnEnableLanguageHook()
{
    LocalizationManager.LanguageChanged -= HandleLanguageChanged;
    LocalizationManager.LanguageChanged += HandleLanguageChanged;
}

private void HandleLanguageChanged(LocalizationManager.Language language)
{
    RefreshLocalizedLabels();
    RefreshReadStateUI();

    if (_currentIndex >= 0)
    {
        ShowIndex(_currentIndex);
    }
}
    private void WireButtons()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            int captured = i;
            var e = entries[i];
            if (e == null) continue;

            if (e.tocButton)
            {
                e.tocButton.onClick.RemoveAllListeners();
                e.tocButton.onClick.AddListener(() => ShowIndex(captured));
            }

            if (e.closeButton)
            {
                e.closeButton.onClick.RemoveAllListeners();
                e.closeButton.onClick.AddListener(CloseCurrentContent);
            }
        }
    }
private void HideAllContents()
{
    if (entries == null) return;

    for (int i = 0; i < entries.Count; i++)
    {
        var e = entries[i];
        if (e == null) continue;

        if (e.contentRoot) e.contentRoot.SetActive(false);
        if (e.contentRootEnglish) e.contentRootEnglish.SetActive(false);
        if (e.contentRootChineseSimplified) e.contentRootChineseSimplified.SetActive(false);
    }

    _currentIndex = -1;
}
public void ShowIndex(int index)
{
    if (entries == null) return;
    if (entries.Count == 0) return;
    if (index < 0 || index >= entries.Count) return;

    GameObject selectedRoot = null;

    for (int i = 0; i < entries.Count; i++)
    {
        var e = entries[i];
        if (e == null) continue;

        GameObject localizedRoot = e.GetLocalizedContentRoot();
        bool active = (i == index);

        if (e.contentRoot) e.contentRoot.SetActive(false);
        if (e.contentRootEnglish) e.contentRootEnglish.SetActive(false);
        if (e.contentRootChineseSimplified) e.contentRootChineseSimplified.SetActive(false);

        if (active && localizedRoot != null)
        {
            localizedRoot.SetActive(true);
            selectedRoot = localizedRoot;
        }
    }

    _currentIndex = index;

    MarkGuideAsRead(index);
    RefreshReadStateUI();

    if (tocRoot) tocRoot.SetActive(false);

    ApplyTraitIconsToContentRoot(selectedRoot);
    StartCoroutine(ApplyTraitIconsNextFrame(selectedRoot));
}
    public void CloseCurrentContent()
    {
        HideAllContents();

        if (tocRoot) tocRoot.SetActive(true);
    }
}