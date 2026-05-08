using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedTextUI : MonoBehaviour
{
[Serializable]
public class LocalizedEntry
{
    [TextArea(2, 8)]
    public string text;
}
    [Header("Fallback")]
    [SerializeField] private bool fallbackToJapaneseIfEmpty = true;

    [Header("English")]
    [SerializeField] private LocalizedEntry english = new LocalizedEntry();

    [Header("Chinese Simplified")]
    [SerializeField] private LocalizedEntry chineseSimplified = new LocalizedEntry();
private TMP_Text targetText;
private string japaneseText;
    private void Reset()
    {
        CaptureTargetDefaults();
    }

    private void Awake()
    {
        CaptureTargetDefaults();
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
        LocalizationManager.LanguageChanged += HandleLanguageChanged;
        RefreshNow();
    }

    private void OnDisable()
    {
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(LocalizationManager.Language language)
    {
        RefreshNow();
    }

public void RefreshNow()
{
    if (targetText == null)
    {
        targetText = GetComponent<TMP_Text>();
    }

    if (targetText == null) return;

    var loc = LocalizationManager.Instance;
    if (loc == null)
    {
        ApplyJapaneseDefaults();
        return;
    }

    switch (loc.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
        {
            bool hasText = !string.IsNullOrEmpty(english.text);
            if (!hasText && fallbackToJapaneseIfEmpty)
            {
                ApplyJapaneseDefaults();
            }
            else
            {
                ApplyEntry(english);
            }
            break;
        }

        case LocalizationManager.Language.ChineseSimplified:
        {
            bool hasText = !string.IsNullOrEmpty(chineseSimplified.text);
            if (!hasText && fallbackToJapaneseIfEmpty)
            {
                ApplyJapaneseDefaults();
            }
            else
            {
                ApplyEntry(chineseSimplified);
            }
            break;
        }

        case LocalizationManager.Language.Japanese:
        default:
            ApplyJapaneseDefaults();
            break;
    }
}
private void CaptureTargetDefaults()
{
    if (targetText == null)
    {
        targetText = GetComponent<TMP_Text>();
    }

    if (targetText == null) return;

    if (string.IsNullOrEmpty(japaneseText))
    {
        japaneseText = targetText.text;
    }
}
private void ApplyJapaneseDefaults()
{
    if (targetText == null) return;

    targetText.text = japaneseText ?? "";
}

private void ApplyEntry(LocalizedEntry entry)
{
    if (targetText == null || entry == null) return;

    targetText.text = entry.text ?? "";
}
}