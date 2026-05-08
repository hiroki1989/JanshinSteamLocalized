using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

[Serializable]
public class DialogueLine
{
    [Header("Speaker (Legacy)")]
    public string speaker;

    [Header("Speaker - Japanese")]
    public string speakerJapanese;

    [Header("Speaker - English")]
    public string speakerEnglish;

    [Header("Speaker - Chinese Simplified")]
    public string speakerChineseSimplified;

    [Header("Body (Legacy)")]
    [TextArea(2, 5)] public string text;

    [Header("Body - Japanese")]
    [TextArea(2, 5)] public string textJapanese;

    [Header("Body - English")]
    [TextArea(2, 5)] public string textEnglish;

    [Header("Body - Chinese Simplified")]
    [TextArea(2, 5)] public string textChineseSimplified;

    public Sprite portrait; // optional override

    public string GetLocalizedSpeaker()
    {
        var lm = LocalizationManager.Instance;
        if (lm == null)
        {
            if (!string.IsNullOrEmpty(speakerJapanese)) return speakerJapanese;
            return speaker ?? "";
        }

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(speakerEnglish)) return speakerEnglish;
                if (!string.IsNullOrEmpty(speakerJapanese)) return speakerJapanese;
                return speaker ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(speakerChineseSimplified)) return speakerChineseSimplified;
                if (!string.IsNullOrEmpty(speakerJapanese)) return speakerJapanese;
                return speaker ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                if (!string.IsNullOrEmpty(speakerJapanese)) return speakerJapanese;
                return speaker ?? "";
        }
    }

    public string GetLocalizedText()
    {
        var lm = LocalizationManager.Instance;
        if (lm == null)
        {
            if (!string.IsNullOrEmpty(textJapanese)) return textJapanese;
            return text ?? "";
        }

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(textEnglish)) return textEnglish;
                if (!string.IsNullOrEmpty(textJapanese)) return textJapanese;
                return text ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(textChineseSimplified)) return textChineseSimplified;
                if (!string.IsNullOrEmpty(textJapanese)) return textJapanese;
                return text ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                if (!string.IsNullOrEmpty(textJapanese)) return textJapanese;
                return text ?? "";
        }
    }
}
public class AngelDialogueController : MonoBehaviour
{
[Header("UI")]
[SerializeField] private TextMeshProUGUI speakerTMP;
[SerializeField] private TextMeshProUGUI bodyTMP;
[SerializeField] private Image portraitImage;
[SerializeField] private Button nextButton;
[SerializeField] private Button skipButton;  // ★追加：スキップボタン用
    [Header("Flow")]
    [SerializeField] private string battleSceneName = "RunScene";

[Header("Content")]
[SerializeField] private List<DialogueLine> lines = new List<DialogueLine>(); // Start（通常開始）用

[Header("Content - Defeat")]
[SerializeField] private List<DialogueLine> defeatLines = new List<DialogueLine>(); // 敗北用（Inspectorで設定）

[Header("Content - Clear")]
[SerializeField] private List<DialogueLine> clearLines = new List<DialogueLine>(); // 通常クリア用（Inspectorで設定）

[Header("Content - Secret Hades Intro")]
[SerializeField] private List<DialogueLine> secretHadesIntroLines = new List<DialogueLine>(); // ハデス戦前会話用（Inspectorで設定）

[Header("Content - Secret True Clear")]
[SerializeField] private List<DialogueLine> secretHadesClearLines = new List<DialogueLine>(); // 真のクリア会話用（Inspectorで設定）

[Header("Secret Reward (Guaranteed Unique Omamori)")]
[SerializeField] private bool grantGuaranteedSecretRewardOnTrueClear = true;
[SerializeField] private string dyeMasterSkillName = "染色師";
[SerializeField] private string calligrapherSkillName = "書家";
[SerializeField] private string capitalistSkillName = "資産家";
[SerializeField] private string equippedActiveSkillPrefKey = "EquippedActiveSkill";
[SerializeField] private string pendingUniqueRollPrefKey = "UniqueOmamori_PendingRoll";
[SerializeField] private string pendingUniqueIdPrefKey = "UniqueOmamori_PendingId";
[SerializeField] private string pendingUniqueEnemyNamePrefKey = "UniqueOmamori_PendingEnemyName";
private int index = -1;

[SerializeField] private float typewriterCharInterval = 0.04f;
[SerializeField] private float typewriterFadeDuration = 0.12f;
[SerializeField] private bool ignoreRichTextTagsInTypewriter = true;

private Coroutine typingCoroutine = null;
private bool isTyping = false;
private string currentFullBody = "";

private const string KeyAngelMode = "PF_AngelDialogueMode"; // "Start" / "Defeat" / "Clear" / "SecretHadesIntro" / "SecretHadesClear"
private void Awake()
{
    // 「次へ」ボタン（存在する場合）
    if (nextButton)
    {
        nextButton.onClick.RemoveAllListeners();
        nextButton.onClick.AddListener(OnClickNext);
    }

    // 「スキップ」ボタン（存在する場合）
    if (skipButton)
    {
        skipButton.onClick.RemoveAllListeners();
        skipButton.onClick.AddListener(OnClickNext);
    }
}

private static string AngelFixedText(string key)
{
    var loc = LocalizationManager.Instance;
    if (loc == null)
        return key;

    return loc.GetFixedText(key);
}

private static string AngelFormatText(string key, params object[] args)
{
    var loc = LocalizationManager.Instance;
    if (loc == null)
        return key;

    return loc.FormatText("fixed." + key, args);
}

private static string AngelSpeakerName()
{
    return AngelFixedText("angel_speaker_name");
}

private IEnumerator TypeBodyText(string fullText)
{
    isTyping = true;
    currentFullBody = fullText ?? "";

    if (!bodyTMP)
    {
        isTyping = false;
        typingCoroutine = null;
        yield break;
    }

    bodyTMP.text = currentFullBody;
    bodyTMP.ForceMeshUpdate();

    TMP_TextInfo textInfo = bodyTMP.textInfo;
    int totalChars = textInfo.characterCount;

    if (totalChars == 0)
    {
        isTyping = false;
        typingCoroutine = null;
        yield break;
    }

    for (int i = 0; i < totalChars; i++)
    {
        if (!textInfo.characterInfo[i].isVisible)
            continue;

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            Color32[] colors = textInfo.meshInfo[m].colors32;
            if (colors == null || colors.Length == 0) continue;
        }

        int matIndexInit = textInfo.characterInfo[i].materialReferenceIndex;
        int vertexIndexInit = textInfo.characterInfo[i].vertexIndex;
        Color32[] initColors = textInfo.meshInfo[matIndexInit].colors32;

        initColors[vertexIndexInit + 0].a = 0;
        initColors[vertexIndexInit + 1].a = 0;
        initColors[vertexIndexInit + 2].a = 0;
        initColors[vertexIndexInit + 3].a = 0;
    }

    for (int m = 0; m < textInfo.meshInfo.Length; m++)
    {
        bodyTMP.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    for (int i = 0; i < totalChars; i++)
    {
        if (!textInfo.characterInfo[i].isVisible)
            continue;

        int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
        int vertexIndex = textInfo.characterInfo[i].vertexIndex;
        Color32[] colors = textInfo.meshInfo[matIndex].colors32;

        float t = 0f;
        float fadeDuration = Mathf.Max(0.0001f, typewriterFadeDuration);

        while (t < fadeDuration)
        {
            byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt((t / fadeDuration) * 255f), 0, 255);

            colors[vertexIndex + 0].a = alpha;
            colors[vertexIndex + 1].a = alpha;
            colors[vertexIndex + 2].a = alpha;
            colors[vertexIndex + 3].a = alpha;

            bodyTMP.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        colors[vertexIndex + 0].a = 255;
        colors[vertexIndex + 1].a = 255;
        colors[vertexIndex + 2].a = 255;
        colors[vertexIndex + 3].a = 255;

        bodyTMP.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

        yield return new WaitForSecondsRealtime(typewriterCharInterval);
    }

    bodyTMP.text = currentFullBody;
    bodyTMP.ForceMeshUpdate();
    bodyTMP.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

    isTyping = false;
    typingCoroutine = null;
}
private void StartTypewriter(string fullText)
{
    if (typingCoroutine != null)
    {
        StopCoroutine(typingCoroutine);
        typingCoroutine = null;
    }

    currentFullBody = fullText ?? "";
    typingCoroutine = StartCoroutine(TypeBodyText(currentFullBody));
}

private void CompleteCurrentTyping()
{
    if (typingCoroutine != null)
    {
        StopCoroutine(typingCoroutine);
        typingCoroutine = null;
    }

    isTyping = false;

    if (bodyTMP)
    {
        bodyTMP.text = currentFullBody ?? "";
        bodyTMP.ForceMeshUpdate();
        bodyTMP.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
private string GetCurrentEnemyNameFromExcelWithLoop()
{
    int idxAbs = GameManager.GetCurrentEnemyIndex();

    if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && cfg != null)
    {
        var all = EnemyConfigExcel.LoadAll();
        int count = (all != null) ? all.Count : 0;
        int loop  = (count > 0) ? (idxAbs / count) : 0;

        string localizedName = cfg.GetLocalizedDisplayNameWithLoop(loop);
        if (!string.IsNullOrEmpty(localizedName))
            return localizedName;
    }

    // Excelが取得できない場合は“何も表示しない”（保険なし）
    return string.Empty;
}
private void Start()
{
    string mode = PlayerPrefs.GetString(KeyAngelMode, "Start");

    if (mode == "SecretHadesIntro")
    {
        if (secretHadesIntroLines != null && secretHadesIntroLines.Count > 0)
        {
            lines = new List<DialogueLine>(secretHadesIntroLines);
        }
        else
        {
            lines = new List<DialogueLine>
            {
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_secret_hades_intro_1")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_secret_hades_intro_2")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_secret_hades_intro_3")},
            };
        }

        OnClickNext();
        return;
    }
    if (mode == "SecretHadesClear")
    {
        if (secretHadesClearLines != null && secretHadesClearLines.Count > 0)
        {
            lines = new List<DialogueLine>(secretHadesClearLines);
        }
        else
        {
            lines = new List<DialogueLine>
            {
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_secret_hades_clear_1")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_secret_hades_clear_2")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_secret_hades_clear_3")},
            };
        }

        OnClickNext();
        return;
    }
    if (mode == "Defeat")
    {
        if (defeatLines != null && defeatLines.Count > 0)
        {
            lines = new List<DialogueLine>(defeatLines);
        }
        else
        {
            lines = new List<DialogueLine>
            {
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_defeat_1")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_defeat_2")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_defeat_3")},
            };
        }

        OnClickNext();
        return;
    }

    if (mode == "Clear")
    {
        if (clearLines != null && clearLines.Count > 0)
        {
            lines = new List<DialogueLine>(clearLines);
        }
        else
        {
            lines = new List<DialogueLine>
            {
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_clear_1")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_clear_2")},
                new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_clear_3")},
            };
        }

        OnClickNext();
        return;
    }
    if (lines == null || lines.Count == 0)
    {
        var enemyName = GetCurrentEnemyNameFromExcelWithLoop();
        lines = new List<DialogueLine>
        {
            new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFixedText("angel_start_1")},
        };

        if (!string.IsNullOrEmpty(enemyName))
            lines.Add(new DialogueLine{ speaker=AngelSpeakerName(), text=AngelFormatText("angel_start_enemy_1", enemyName)});
    }
    OnClickNext();
}
public void OnClickNext()
{
    if (isTyping)
    {
        CompleteCurrentTyping();
        return;
    }

    if (lines == null || lines.Count == 0)
    {
        string mode = PlayerPrefs.GetString(KeyAngelMode, "Start");

        if (mode == "SecretHadesClear")
        {
            PrepareGuaranteedSecretTrueClearRewardIfNeeded();
        }

        var flow = ProgressionFlowController.Instance;
        if (flow != null)
        {
            if (mode == "SecretHadesIntro")
            {
                flow.GoFromSecretAngelIntroToHadesEnemyConversation();
            }
            else if (mode == "Start")
            {
                flow.GoFromAngelToEnemyConversation();
            }
            else
            {
                flow.GoFromAngelToReward();
            }
            return;
        }

        string nextScene = PlayerPrefs.GetString("PF_AngelDialogueNextScene", "");
        if (!string.IsNullOrEmpty(nextScene))
        {
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            SceneManager.LoadScene(string.IsNullOrEmpty(battleSceneName) ? "RunScene" : battleSceneName);
        }
        return;
    }

    int start = index + 1;
    if (start >= lines.Count)
    {
        string mode = PlayerPrefs.GetString(KeyAngelMode, "Start");

        if (mode == "SecretHadesClear")
        {
            PrepareGuaranteedSecretTrueClearRewardIfNeeded();
        }

        var flow = ProgressionFlowController.Instance;
        if (flow != null)
        {
            if (mode == "SecretHadesIntro")
            {
                flow.GoFromSecretAngelIntroToHadesEnemyConversation();
            }
            else if (mode == "Start")
            {
                flow.GoFromAngelToEnemyConversation();
            }
            else
            {
                flow.GoFromAngelToReward();
            }
            return;
        }

        string nextScene = PlayerPrefs.GetString("PF_AngelDialogueNextScene", "");
        if (!string.IsNullOrEmpty(nextScene))
        {
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            SceneManager.LoadScene(string.IsNullOrEmpty(battleSceneName) ? "RunScene" : battleSceneName);
        }
        return;
    }

int end = Mathf.Min(lines.Count, start + 3);

string body = "";
for (int i = start; i < end; i++)
{
    var line = lines[i];
    string localizedText = (line != null) ? line.GetLocalizedText() : "";

    if (!string.IsNullOrEmpty(localizedText))
    {
        if (!string.IsNullOrEmpty(body))
            body += "\n";
        body += localizedText;
    }
}
var firstLine = lines[start];

if (speakerTMP) speakerTMP.text = firstLine != null ? firstLine.GetLocalizedSpeaker() : "";
StartTypewriter(body);
    if (portraitImage)
    {
        var portrait = (firstLine != null) ? firstLine.portrait : null;
        portraitImage.sprite = portrait;
        portraitImage.enabled = (portraitImage.sprite != null);
        portraitImage.preserveAspect = true;
    }

    index = end - 1;
}
private void PrepareGuaranteedSecretTrueClearRewardIfNeeded()
{
    if (!grantGuaranteedSecretRewardOnTrueClear) return;

    int alreadyGrantedId = 0;
    try { alreadyGrantedId = PlayerPrefs.GetInt("SecretHades_BonusUniqueOmamoriId", 0); } catch { alreadyGrantedId = 0; }
    if (alreadyGrantedId > 0) return;

    string equippedSkill = "";
    try { equippedSkill = PlayerPrefs.GetString(equippedActiveSkillPrefKey, ""); } catch { equippedSkill = ""; }

    equippedSkill = (equippedSkill ?? "").Trim();

    if (string.IsNullOrEmpty(equippedSkill))
        return;

    string rewardEnemyName = "ハデス";

    try
    {
        int normalRewardId = 0;
        try { normalRewardId = PlayerPrefs.GetInt("LastGrantedOmamoriIdV1", 0); } catch { normalRewardId = 0; }

        var playerDataType = typeof(PlayerData);
        var enumType = playerDataType.GetNestedType("UniqueOmamoriEffectKind", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (enumType == null) return;

        string enumName = ResolveSecretRewardEnumNameByEquippedSkill(equippedSkill);
        if (string.IsNullOrEmpty(enumName)) return;

        object enumValue = null;
        try
        {
            enumValue = Enum.Parse(enumType, enumName, true);
        }
        catch
        {
            Debug.LogWarning("[AngelDialogue] Secret true-clear unique reward enum was not found: " + enumName);
            return;
        }

        var grantMethod = playerDataType.GetMethod(
            "GrantUniqueOmamori",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (grantMethod == null) return;

        object grantResult = grantMethod.Invoke(null, new object[] { rewardEnemyName, enumValue, 1 });

        int grantedId = 0;
        if (grantResult is int intId)
            grantedId = intId;

        if (grantedId > 0)
        {
            PlayerPrefs.SetInt("SecretHades_BonusUniqueOmamoriId", grantedId);
        }

        PlayerPrefs.SetInt("LastGrantedOmamoriIdV1", normalRewardId);

        PlayerPrefs.SetInt(pendingUniqueRollPrefKey, 0);
        PlayerPrefs.SetInt(pendingUniqueIdPrefKey, 0);
        PlayerPrefs.SetString(pendingUniqueEnemyNamePrefKey, "");

        PlayerPrefs.Save();
    }
    catch (Exception ex)
    {
        Debug.LogWarning("[AngelDialogue] PrepareGuaranteedSecretTrueClearRewardIfNeeded failed: " + ex.Message);
    }
}
private string ResolveSecretRewardEnumNameByEquippedSkill(string equippedSkill)
{
    string s = (equippedSkill ?? "").Trim();

    bool IsSkillNameMatch(string rawValue, string inspectorDisplayName, string canonicalKey)
    {
        if (string.IsNullOrEmpty(rawValue))
            return false;

        if (!string.IsNullOrEmpty(inspectorDisplayName) &&
            string.Equals(rawValue, inspectorDisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(rawValue, canonicalKey, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var loc = LocalizationManager.Instance;
            if (loc != null)
            {
                string localized = loc.GetActiveSkillDisplayName(canonicalKey);
                if (!string.IsNullOrEmpty(localized) &&
                    string.Equals(rawValue, localized.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
        }

        return false;
    }

    if (IsSkillNameMatch(s, dyeMasterSkillName, "RandomMan"))
        return "Hades_DyeMaster";

    if (IsSkillNameMatch(s, calligrapherSkillName, "EnhanceHand") ||
        IsSkillNameMatch(s, calligrapherSkillName, "RandomHonor"))
        return "Hades_Calligrapher";

    if (IsSkillNameMatch(s, capitalistSkillName, "Capitalist"))
        return "Hades_Capitalist";

    return string.Empty;
}
    public void OnClickSkip()
    {
        if (isTyping)
        {
            CompleteCurrentTyping();
            return;
        }

        OnClickNext();
    }

}
