using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using TMPro;
public class EnemyDialogueController : MonoBehaviour
{
[Serializable]
public class EnemyLine
{
    [Header("Body (Legacy)")]
    [TextArea(2, 5)] public string text;

    [Header("Body - Japanese")]
    [TextArea(2, 5)] public string textJapanese;

    [Header("Body - English")]
    [TextArea(2, 5)] public string textEnglish;

    [Header("Body - Chinese Simplified")]
    [TextArea(2, 5)] public string textChineseSimplified;

    public Sprite portrait; // optional

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
    [Serializable]
    public class EnemyDialogueSet
    {
        [Tooltip("enemy_config.xlsx の行キー（推奨）。一致したら最優先で使われます。未使用なら -1 のままでOK。")]
        public int excelKey = -1;

        [Tooltip("ExcelKey を使わない場合のフォールバック一致用（敵の素の名前：例）ゼウス、アマテラス）。")]
        public string enemyBaseName = "";

        [Tooltip("この敵の会話行（3行ごとに1パラグラフ表示）")]
        public List<EnemyLine> lines = new List<EnemyLine>();
    }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI enemyNameTMP;
    [SerializeField] private TextMeshProUGUI bodyTMP;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Button nextButton;
 [SerializeField] private Button skipButton;  // 「スキップ」ボタンも Next と同じ扱い

    [Header("Flow")]
    [SerializeField] private string battleSceneName = "RunScene";

    [Header("Enemy Info Panel")]
    [SerializeField] private GameObject enemyInfoPanel;
    [SerializeField] private TextMeshProUGUI enemyInfoNameTMP;
    [SerializeField] private TextMeshProUGUI enemyInfoHpTMP;
    [SerializeField] private TextMeshProUGUI enemyInfoDeckTMP;
[SerializeField] private TextMeshProUGUI enemyInfoFirstGemTMP;
[SerializeField] private TextMeshProUGUI enemyInfoSkillsTMP;

[SerializeField] private Button startBattleButton;
    [Header("Content")]
    [SerializeField] private List<EnemyLine> lines = new List<EnemyLine>(); // 共通/フォールバック

    [Header("Content (Per Enemy)")]
    [SerializeField] private List<EnemyDialogueSet> perEnemyDialogues = new List<EnemyDialogueSet>();
    [Serializable]
private class EnemySkillDisplayNameEntry
{
    public string skillId;       // enemy_config.xlsx の Skill?_Id（例: "妨害" / "jam"）

    [Header("Display Name (Legacy / Japanese)")]
    public string displayName;

    [Header("Display Name - Japanese")]
    public string displayNameJapanese;

    [Header("Display Name - English")]
    public string displayNameEnglish;

    [Header("Display Name - Chinese Simplified")]
    public string displayNameChineseSimplified;

    public string GetLocalizedDisplayName()
    {
        var lm = LocalizationManager.Instance;

        if (lm == null)
        {
            if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
            return displayName ?? "";
        }

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(displayNameEnglish)) return displayNameEnglish;
                if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
                return displayName ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(displayNameChineseSimplified)) return displayNameChineseSimplified;
                if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
                return displayName ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
                return displayName ?? "";
        }
    }
}

[Header("Enemy Skill Display Names (UI only)")]
[SerializeField] private List<EnemySkillDisplayNameEntry> enemySkillDisplayNameTable
    = new List<EnemySkillDisplayNameEntry>();

private static readonly Dictionary<string, string> s_enemySkillDisplayNameCache
    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

private void RefreshEnemySkillDisplayNameCache()
{
    s_enemySkillDisplayNameCache.Clear();

    if (enemySkillDisplayNameTable == null)
        return;

    for (int i = 0; i < enemySkillDisplayNameTable.Count; i++)
    {
        var e = enemySkillDisplayNameTable[i];
        if (e == null) continue;
        if (string.IsNullOrEmpty(e.skillId)) continue;

        string key = e.skillId.Trim();
        if (key.Length == 0) continue;

        string shown = e.GetLocalizedDisplayName();
        if (string.IsNullOrEmpty(shown)) continue;

        s_enemySkillDisplayNameCache[key] = shown;
    }
}

public static bool TryResolveSharedEnemySkillDisplayName(string rawSkillId, out string displayName)
{
    displayName = string.Empty;

    if (string.IsNullOrEmpty(rawSkillId))
        return false;

    string raw = rawSkillId.Trim();
    if (raw.Length == 0)
        return false;

    if (s_enemySkillDisplayNameCache.TryGetValue(raw, out var cached) && !string.IsNullOrEmpty(cached))
    {
        displayName = cached;
        return true;
    }

    return false;
}

    [Serializable]
private class EnemyDeckDisplayNameEntry
{
    public string deckId;    // enemy_config.xlsx の Deck 列に入っている文字列

    [Header("Display Name (Legacy / Japanese)")]
    public string displayName;

    [Header("Display Name - Japanese")]
    public string displayNameJapanese;

    [Header("Display Name - English")]
    public string displayNameEnglish;

    [Header("Display Name - Chinese Simplified")]
    public string displayNameChineseSimplified;

    public string GetLocalizedDisplayName()
    {
        var lm = LocalizationManager.Instance;

        if (lm == null)
        {
            if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
            return displayName ?? "";
        }

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(displayNameEnglish)) return displayNameEnglish;
                if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
                return displayName ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(displayNameChineseSimplified)) return displayNameChineseSimplified;
                if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
                return displayName ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
                return displayName ?? "";
        }
    }
}

[Header("Enemy Deck Display Names (UI only)")]
[SerializeField] private List<EnemyDeckDisplayNameEntry> enemyDeckDisplayNameTable
    = new List<EnemyDeckDisplayNameEntry>();
private List<EnemyLine> ResolveDialogueLinesForCurrentEnemy(string shownName)
{
    int idxAbs = GameManager.GetCurrentEnemyIndex();

    // 1) excelKey マッチを最優先
    int excelKey = EnemyConfigExcel.MapRuntimeIndexToExcelKey(idxAbs);
    if (excelKey >= 0 && perEnemyDialogues != null)
    {
        for (int i = 0; i < perEnemyDialogues.Count; i++)
        {
            var set = perEnemyDialogues[i];
            if (set != null && set.excelKey == excelKey && set.lines != null && set.lines.Count > 0)
            {
                return new List<EnemyLine>(set.lines);
            }
        }
    }

    // 2) enemyBaseName マッチ（Excelの素の名前を優先して作る）
    string baseName = "";

    if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && cfg != null && !string.IsNullOrEmpty(cfg.name))
    {
        baseName = cfg.name; // ループ接尾語なしの素の名前
    }
    else
    {
        baseName = StripLoopSuffix(shownName); // 保険
    }

    if (!string.IsNullOrEmpty(baseName) && perEnemyDialogues != null)
    {
        for (int i = 0; i < perEnemyDialogues.Count; i++)
        {
            var set = perEnemyDialogues[i];
            if (set == null) continue;

            if (!string.IsNullOrEmpty(set.enemyBaseName)
                && string.Equals(set.enemyBaseName.Trim(), baseName.Trim(), StringComparison.Ordinal))
            {
                if (set.lines != null && set.lines.Count > 0)
                {
                    return new List<EnemyLine>(set.lines);
                }
            }
        }
    }

    // 3) 見つからなければ共通/フォールバック
    if (lines != null && lines.Count > 0) return new List<EnemyLine>(lines);

    return null;
}
private string ResolveEnemySkillDisplayName(string rawSkillId)
{
    if (string.IsNullOrEmpty(rawSkillId)) return string.Empty;

    RefreshEnemySkillDisplayNameCache();

    if (TryResolveSharedEnemySkillDisplayName(rawSkillId, out var shown))
        return shown;

    string raw = rawSkillId.Trim();

    var lm = LocalizationManager.Instance;
    if (lm != null)
    {
        string localized = lm.GetEnemySkillDisplayName(raw);
        if (!string.IsNullOrEmpty(localized) && !string.Equals(localized, raw, StringComparison.Ordinal))
            return localized;
    }

    // 未登録ならIDをそのまま表示
    return raw;
}
private string ResolveEnemyDeckDisplayName(string rawDeckId)
{
    if (string.IsNullOrEmpty(rawDeckId)) return string.Empty;

    string raw = rawDeckId.Trim();
    string rawLower = raw.ToLowerInvariant();

    if (enemyDeckDisplayNameTable != null)
    {
        for (int i = 0; i < enemyDeckDisplayNameTable.Count; i++)
        {
            var e = enemyDeckDisplayNameTable[i];
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.deckId)) continue;

            string shown = e.GetLocalizedDisplayName();
            if (string.IsNullOrEmpty(shown)) continue;

            string key = e.deckId.Trim();
            if (key == raw) return shown;

            string keyLower = key.ToLowerInvariant();
            if (keyLower == rawLower) return shown;
        }
    }

    return raw;
}
private static string GetFixedText_Local(string key)
{
    var lm = LocalizationManager.Instance;
    if (lm == null) return key;
    return lm.GetFixedText(key);
}
    // 会話の現在位置（AngelDialogueController と同じ仕様）
    private int index = -1;
[SerializeField] private float typewriterCharInterval = 0.04f;
[SerializeField] private float typewriterFadeDuration = 0.12f;
[SerializeField] private bool ignoreRichTextTagsInTypewriter = true;

private Coroutine typingCoroutine = null;
private bool isTyping = false;
private string currentFullBody = "";
private bool _battleStartRequested = false;

private void ReplaceButtonClick(Button button, UnityAction action)
{
    if (!button || action == null)
        return;

    button.onClick = new Button.ButtonClickedEvent();
    button.onClick.AddListener(action);
}

private void Awake()
{
    ReplaceButtonClick(nextButton, OnClickNext);
    ReplaceButtonClick(skipButton, OnClickNext);
    ReplaceButtonClick(startBattleButton, OnClickStartBattle);

    if (enemyInfoPanel)
    {
        enemyInfoPanel.SetActive(false);
    }
}

[SerializeField] private string dialoguePortraitFolder = "Sprites/Enemies/Dialogue";
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
private void Start()
{
    // Excel優先で会話相手の表示名を決定
    string shown = GetCurrentEnemyNameFromExcelWithLoop();
    if (enemyNameTMP) enemyNameTMP.text = shown;

    // ★敵別会話（Inspector設定）を優先して採用
    var resolved = ResolveDialogueLinesForCurrentEnemy(shown);
    if (resolved != null && resolved.Count > 0)
    {
        lines = resolved;
    }
if (lines == null || lines.Count == 0)
{
    lines = new List<EnemyLine>{
        new EnemyLine
        {
            textJapanese = "人の子よ、ここまで来たか。",
            textEnglish = "Child of man, so you have made it this far.",
            textChineseSimplified = "人类之子啊，你竟已来到这里。"
        },
        new EnemyLine
        {
            textJapanese = "我に勝てるか、試してみるがよい。",
            textEnglish = "Come, let us see whether you can defeat me.",
            textChineseSimplified = "来吧，让我看看你是否能够战胜我。"
        }
    };
}
    // 行ごとportrait未指定なら規約で自動ロード（lines が確定してから）
    TryAutoSetPortrait(shown);

    OnClickNext(); // show first
}

private string GetCurrentEnemyNameFromExcelWithLoop()
{
    int idxAbs = 0;
    try { idxAbs = ProgressionFlowController.GetCurrentEnemyIndex(); } catch { idxAbs = 0; }

    int count = 0;
    try { count = EnemyConfigExcel.GetNormalEnemyCount(); } catch { count = 0; }
    if (count <= 0) count = 1;

    int key = idxAbs;
    try { key = EnemyConfigExcel.MapRuntimeIndexToExcelKey(idxAbs); } catch { key = idxAbs; }

    if (EnemyConfigExcel.IsSecretBossKey(key))
    {
        // 裏ボスは周回表記を付けない
        if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfgSecret) && cfgSecret != null)
        {
            string localizedSecretName = cfgSecret.GetLocalizedDisplayName();
            if (!string.IsNullOrEmpty(localizedSecretName))
                return localizedSecretName;
        }
        return "???";
    }

    // 通常敵：周回表記
    int loop = idxAbs / count;
    if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && cfg != null)
    {
        string localizedName = cfg.GetLocalizedDisplayNameWithLoop(loop);
        if (!string.IsNullOrEmpty(localizedName))
            return localizedName;
    }

    return "???";
}

private void TryAutoSetPortrait(string shownName)
{
    if (!portraitImage) return;

    // 行ごとにportrait指定があればそちらを優先
    bool anyLineHasPortrait = false;
    if (lines != null)
        foreach (var ln in lines) if (ln != null && ln.portrait) { anyLineHasPortrait = true; break; }
    if (anyLineHasPortrait) return;

// Excelの素の名前を最優先（無ければ従来の shownName）
string baseName = shownName;
if (EnemyConfigExcel.TryGetForRuntimeIndex(ProgressionFlowController.GetCurrentEnemyIndex(), out var cfg)
    && !string.IsNullOrEmpty(cfg.name))
{
    baseName = cfg.name; // ループ接尾語なしの“素の名前”
}

var key = EnemyConfigExcel.SanitizeForResource(baseName);
var sp  = Resources.Load<Sprite>($"{dialoguePortraitFolder}/{key}");
if (sp)
{
    portraitImage.sprite = sp;
    portraitImage.preserveAspect = true;
    portraitImage.enabled = true;
}

    portraitImage.enabled = (sp != null);
    portraitImage.preserveAspect = true;
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
        // 行が存在しない場合はすぐに敵情報パネル or 対局へ
        if (enemyInfoPanel)
        {
            ShowEnemyInfoPanel();
            return;
        }

        GoToBattleViaProgression();
        return;
    }

    // 次に表示する最初の行インデックス（3行で1パラグラフ）
    int start = index + 1;

    // もうこれ以上表示する行が無ければ敵情報パネル or 対局へ
    if (start >= lines.Count)
    {
        if (enemyInfoPanel)
        {
            ShowEnemyInfoPanel();
            return;
        }

        GoToBattleViaProgression();
        return;
    }
int end = Mathf.Min(lines.Count, start + 3);

// 1パラグラフ分のテキストを結合
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

StartTypewriter(body);

    if (portraitImage)
    {
        // 先頭行に portrait があればそれで上書き
        var first = lines[start];
        if (first != null && first.portrait)
        {
            portraitImage.sprite = first.portrait;
        }
        // 無ければ TryAutoSetPortrait で設定された画像を維持
        portraitImage.enabled = (portraitImage.sprite != null);
        portraitImage.preserveAspect = true;
    }

    // 「前回表示した最後の行インデックス」を更新
    index = end - 1;
}
    /// <summary>
    /// 敵情報パネルを開き、名前・HP・デッキ構成(AF列 Deck)を表示する。
    /// </summary>
    private void ShowEnemyInfoPanel()
    {
        // 会話ボタンは無効化
        if (nextButton) nextButton.interactable = false;
        if (skipButton) skipButton.interactable = false;

        // パネルを表示
        if (enemyInfoPanel) enemyInfoPanel.SetActive(true);

        // 名前（天井周回込みの表示名）
        if (enemyInfoNameTMP)
        {
            enemyInfoNameTMP.text = GetCurrentEnemyNameFromExcelWithLoop();
        }

        // HP と Deck 構成を enemy_config から取得
        int idxAbs = GameManager.GetCurrentEnemyIndex();
        if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg))
        {
// HP: Tier倍率を反映した値を表示
if (enemyInfoHpTMP)
{
    float tierMult = 1f;
    try { tierMult = GameManager.GetCurrentTierMultiplier(); } catch { tierMult = 1f; }

    int shownHp = Mathf.Max(1, Mathf.RoundToInt(cfg.maxHP * tierMult));
    string hpLabel = GetFixedText_Local("HP");
    enemyInfoHpTMP.text = $"{hpLabel} {shownHp}";
}
            // デッキ構成: AF列 Deck の文字列をローカライズして表示
            if (enemyInfoDeckTMP)
            {
                string deckText = cfg.deck ?? string.Empty;
                enemyInfoDeckTMP.text = ResolveEnemyDeckDisplayName(deckText);
            }
        }
// 初回撃破報酬（宝石×1） 取得済み/未取得
if (enemyInfoFirstGemTMP)
{
    string shownName = GetCurrentEnemyNameFromExcelWithLoop();
    string localizedBaseName = StripLoopSuffix(shownName);
    string rawBaseName = (cfg != null && !string.IsNullOrEmpty(cfg.name)) ? cfg.name.Trim() : "";
    int excelKey = EnemyConfigExcel.MapRuntimeIndexToExcelKey(idxAbs);

    bool obtained = IsFirstDefeatRewardObtained(excelKey, localizedBaseName, rawBaseName);

    string rewardLabel = GetFixedText_Local("初回撃破報酬（宝石×1）");
    string obtainedText = GetFixedText_Local("取得済み");
    string notObtainedText = GetFixedText_Local("未取得");

    enemyInfoFirstGemTMP.text = $"{rewardLabel}　{(obtained ? obtainedText : notObtainedText)}";
}
// スキル（enemy_config.xlsx の Skill1/Skill2 から自動取得）
if (enemyInfoSkillsTMP)
{
    enemyInfoSkillsTMP.text = BuildEnemySkillsText(cfg);
}

    }
// ===== Enemy Info Panel: First-defeat reward status =====
private static string PrefKey_FirstDefeatedForExcelKey(int excelKey) => "Gem_FirstDefeated_Excel_" + excelKey.ToString();
private static string PrefKey_FirstDefeatedForExcelKey_Legacy(int excelKey) => "Gem_FirstDefeated_" + excelKey.ToString();
private static string PrefKey_FirstDefeatedForEnemyName(string enemyBaseName) => "Gem_FirstDefeated_Name_" + enemyBaseName;

private static bool IsFirstDefeatRewardObtained(int excelKey, string localizedBaseName, string rawBaseName)
{
    bool obtained = false;

    if (excelKey >= 0)
    {
        obtained |= PlayerPrefs.GetInt(PrefKey_FirstDefeatedForExcelKey(excelKey), 0) == 1;
        obtained |= PlayerPrefs.GetInt(PrefKey_FirstDefeatedForExcelKey_Legacy(excelKey), 0) == 1;
    }

    if (!string.IsNullOrEmpty(localizedBaseName))
        obtained |= PlayerPrefs.GetInt(PrefKey_FirstDefeatedForEnemyName(localizedBaseName), 0) == 1;

    if (!string.IsNullOrEmpty(rawBaseName))
        obtained |= PlayerPrefs.GetInt(PrefKey_FirstDefeatedForEnemyName(rawBaseName), 0) == 1;

    return obtained;
}
// "アマテラス +1" のような周回サフィックスを除外
private static string StripLoopSuffix(string name)
{
    if (string.IsNullOrEmpty(name)) return name;

    int p = name.LastIndexOf(" +", StringComparison.Ordinal);
    if (p < 0) return name;

    string tail = name.Substring(p + 2);
    for (int i = 0; i < tail.Length; i++)
    {
        if (tail[i] < '0' || tail[i] > '9') return name;
    }

    return name.Substring(0, p).TrimEnd();
}
private string BuildEnemySkillsText(EnemyConfig cfg)
{
    string skillLabel = GetFixedText_Local("スキル");
    string noneLabel = GetFixedText_Local("なし");

    if (cfg == null || cfg.skills == null || cfg.skills.Count == 0)
    {
        return skillLabel + "　" + noneLabel;
    }

    var sb = new StringBuilder();
    sb.Append(skillLabel);

    foreach (var sk in cfg.skills)
    {
        if (sk == null || string.IsNullOrEmpty(sk.id)) continue;
        sb.Append("\n　");
        sb.Append(FormatSkillOneLine(sk));
    }

    return sb.ToString();
}
private string FormatSkillOneLine(EnemySkillConfig sk)
{
    // 例：
    // 攻撃　プレイヤーHPに500ダメージ
    // 麻痺　3ターンスキル使用不可
    string rawId = sk.id.Trim();
    string shownId = ResolveEnemySkillDisplayName(rawId);
    string key = rawId.ToLowerInvariant();

    int x = sk.paramX;
    int y = sk.paramY;

    if (key == "攻撃" || key == "attack")
        return string.Format(GetFixedText_Local("{0}　プレイヤーHPに{1}ダメージ"), shownId, x);
    if (key == "麻痺" || key == "paralysis")
        return string.Format(GetFixedText_Local("{0}　{1}ターンスキル使用不可"), shownId, x);
    if (key == "毒" || key == "poison")
        return string.Format(GetFixedText_Local("{0}　{1}ターン毎ターン{2}ダメージ"), shownId, x, y);
    if (key == "怒り" || key == "anger")
        return string.Format(GetFixedText_Local("{0}　次の和了ダメージ +{1}%"), shownId, x);
    if (key == "防御" || key == "defense")
        return string.Format(GetFixedText_Local("{0}　次のプレイヤー和了ダメージ {1}%減少"), shownId, x);
    if (key == "妨害" || key == "jam")
        return string.Format(GetFixedText_Local("{0}　プレイヤーMPを{1}減少"), shownId, x);
    if (key == "細工" || key == "trick")
        return string.Format(GetFixedText_Local("{0}　手牌を{1}枚入れ替え"), shownId, x);

    return shownId;
}
public void OnClickStartBattle()
{
    if (_battleStartRequested)
        return;

    _battleStartRequested = true;

    if (startBattleButton)
        startBattleButton.interactable = false;

    GoToBattleViaProgression();
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
private void GoToBattleViaProgression()
{
    RefreshEnemySkillDisplayNameCache();

    // ★必ず ProgressionFlowController 経由で遷移（回復/引き継ぎフックを通す）
    var inst = ProgressionFlowController.Instance;
    if (inst == null)
    {
        inst = GameObject.FindObjectOfType<ProgressionFlowController>(true);
        if (inst == null)
        {
            var go = new GameObject("ProgressionFlow");
            inst = go.AddComponent<ProgressionFlowController>();
        }
    }
    int idxAbs = GameManager.GetCurrentEnemyIndex();
    if (idxAbs == 0)
    {
        PlayerPrefs.DeleteKey("Run_PlayerHP");
        PlayerPrefs.DeleteKey("Run_PlayerMP");
        PlayerPrefs.SetInt("PF_PendingFullHeal", 1);
        PlayerPrefs.Save();
    }

    inst.GoFromEnemyConversationToBattle();
}

}
