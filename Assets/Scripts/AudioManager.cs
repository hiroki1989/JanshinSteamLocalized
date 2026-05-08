using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string PrefKey_BgmVolume = "PF_Audio_BGM_Volume";
    private const string PrefKey_SeVolume  = "PF_Audio_SE_Volume";

    // ==============================
    //  シーンごとの BGM 設定
    // ==============================
    [System.Serializable]
    public class SceneBgmEntry
    {
        public string sceneName;   // 例: "MenuScene", "RunScene"
        public AudioClip bgmClip;  // そのシーンで流したい BGM
    }

[Header("Scene BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private float bgmVolume = 0.8f;
    [SerializeField] private List<SceneBgmEntry> sceneBgms = new List<SceneBgmEntry>();

    [Header("Battle BGM Override (RunScene)")]
    [Tooltip("通常対局BGMは Scene BGM の RunScene 設定を使い、ハデス戦だけ差し替える。")]
    [SerializeField] private string runSceneNameForBattleBgmOverride = "RunScene";
    [Tooltip("ハデス戦（裏ボス）中だけ流すBGM。未指定なら通常BGMのまま。")]
    [SerializeField] private AudioClip hadesBattleBgmClip;

    [Tooltip("ゼウス戦中だけ流すBGM。未指定なら通常BGMのまま。")]
    [SerializeField] private AudioClip zeusBattleBgmClip;

    private Dictionary<string, AudioClip> _sceneBgmMap = new Dictionary<string, AudioClip>();
    // ==============================
    //  汎用 SE 設定
    // ==============================
    [Header("SE")]
    [SerializeField] private AudioSource seSource;
    [SerializeField] private float seVolume = 1.0f;
[Header("SE Clips (UI / Battle Common)")]
[SerializeField] private AudioClip seClick;
[SerializeField] private AudioClip seBack;
[SerializeField] private AudioClip seConfirm;
[SerializeField] private AudioClip seCancel;

[SerializeField] private AudioClip seTileDiscard;        // 牌を捨てる
[SerializeField] private AudioClip seTileDraw;           // ツモ/引く
[SerializeField] private AudioClip seTileSelect;         // 選択
[SerializeField] private AudioClip seTileSwap;           // 入替
[SerializeField] private AudioClip seDealOfferTile;      // 配牌（4枚）：1枚ごと
[SerializeField] private AudioClip seBattleDamage;       // ダメージ共通
[SerializeField] private AudioClip seScoringPanelOk;     // 点数計算パネルOK

[SerializeField] private AudioClip seVictory;            // 勝利
[SerializeField] private AudioClip seDefeat;             // 敗北
[SerializeField] private AudioClip clickSE;  // 牌を切る音
[SerializeField] private AudioClip discardTileSE;  // 牌を切る音
[SerializeField] private AudioClip drawTileSE;     // 牌を引く音
[SerializeField] private AudioClip selectTileSE;   // 牌を選択したときの音
[SerializeField] private AudioClip swapTileSE;     // 牌を入れ替えたときの音
[Header("SE Clips (Battle Events)")]
[SerializeField] private AudioClip battleDamageSE;        // 対局中：プレイヤーor敵がダメージを受けたとき（共通）
[SerializeField] private AudioClip dealOfferTileSE;       // 配牌（4つずつ）：1枚が手牌（オファー枠）に入るたび
[SerializeField] private AudioClip openingHandDealGroupSE; // 初期配牌（4+4+4+1）：1グループ投入ごと（計4回）
[SerializeField] private AudioClip scoringPanelOkSE;      // 点数計算パネル：OK/確認ボタン
[SerializeField] private AudioClip victorySE;             // 勝利SE（今回は前の依頼分も使えるように）
[SerializeField] private AudioClip defeatSE;              // 敗北SE（今回は前の依頼分も使えるように）
[Header("SE Clips (Scoring Step Reveal)")]
[SerializeField] private AudioClip scoringStepGoldSE;             // ゴールド表示だけ別
[SerializeField] private AudioClip scoringStepUnderManganSE;      // ①満貫未満
[SerializeField] private AudioClip scoringStepManganToYakumanSE;  // ②満貫以上役満未満
[SerializeField] private AudioClip scoringStepYakumanOrAboveSE;   // ③役満以上
[Header("SE Clips (Cutin: In-Match)")]
[SerializeField] private AudioClip playerSkillCutinSEClip;   // プレイヤー：スキル（※表示カットインが無い場合はスキル発動タイミングで鳴らす用途）
[SerializeField] private AudioClip playerSkillTransformSEClip; // プレイヤー：手牌変換（魔法）演出SE（染色師/書家 共通）
[SerializeField] private AudioClip playerRonCutinSEClip;     // プレイヤー：ロン
[SerializeField] private AudioClip playerTsumoCutinSEClip;   // プレイヤー：ツモ
[SerializeField] private AudioClip enemyRonCutinSEClip;      // 敵：ロン
[SerializeField] private AudioClip enemyTsumoCutinSEClip;    // 敵：ツモ
[SerializeField] private AudioClip enemySkillCutinSEClip;    // 敵：スキル
[SerializeField] private AudioClip playerRiichiCutinSEClip;  // プレイヤー：リーチ
[SerializeField] private AudioClip enemyRiichiCutinSEClip;   // 敵：リーチ
[Header("SE Clips (Battle Result)")]
[SerializeField] private AudioClip battleResultVictorySEClip; // 勝利（撃破）
[SerializeField] private AudioClip battleResultDefeatSEClip;  // 敗北
[Header("SE Clips (Reward Omamori Reveal By Rarity)")]
[SerializeField] private AudioClip omamoriRevealSE_Normal;
[SerializeField] private AudioClip omamoriRevealSE_Common;
[SerializeField] private AudioClip omamoriRevealSE_Rare;
[SerializeField] private AudioClip omamoriRevealSE_Epic;
[SerializeField] private AudioClip omamoriRevealSE_Legendary;

[Header("SE Clips (Special Tile Buy By Rarity)")]
[SerializeField] private AudioClip specialTileBuySE_Normal;
[SerializeField] private AudioClip specialTileBuySE_Common;
[SerializeField] private AudioClip specialTileBuySE_Rare;
[SerializeField] private AudioClip specialTileBuySE_Epic;
[SerializeField] private AudioClip specialTileBuySE_Legendary;

[Header("SE Clips (Upgrade)")]
[SerializeField] private AudioClip gemGetSE;           // 強化画面：宝石獲得
[SerializeField] private AudioClip uniqueOmamoriGetSE; // 強化画面：神器獲得
[SerializeField] private AudioClip goldSpendSEClip;    // 強化画面：ゴールド消費（共通）
    // （必要なら名前指定で鳴らす用に辞書も用意しておける）
    [Header("Optional: Named SE Clips")]
    [SerializeField] private List<AudioClip> seClips = new List<AudioClip>();
    private Dictionary<string, AudioClip> _seClipMap = new Dictionary<string, AudioClip>();
public void PlayPlayerSkillTransformSE() => PlaySE(playerSkillTransformSEClip);
    // ==============================
    //  ライフサイクル
    // ==============================
    public void PlaySE_Click()          => PlaySE_Internal(seClick);
public void PlaySE_Back()           => PlaySE_Internal(seBack);
public void PlaySE_Confirm()        => PlaySE_Internal(seConfirm);
public void PlaySE_Cancel()         => PlaySE_Internal(seCancel);

public void PlaySE_TileDiscard()    => PlaySE_Internal(seTileDiscard);
public void PlaySE_TileDraw()       => PlaySE_Internal(seTileDraw);
public void PlaySE_TileSelect()     => PlaySE_Internal(seTileSelect);
public void PlaySE_TileSwap()       => PlaySE_Internal(seTileSwap);

public void PlaySE_DealOfferTile()  => PlaySE_Internal(seDealOfferTile);
public void PlaySE_BattleDamage()   => PlaySE_Internal(seBattleDamage);
public void PlaySE_ScoringPanelOk() => PlaySE_Internal(seScoringPanelOk);

public void PlaySE_Victory()        => PlaySE_Internal(seVictory);
public void PlaySE_Defeat()         => PlaySE_Internal(seDefeat);

    private void OnValidate()
    {
        bgmVolume = Mathf.Clamp01(bgmVolume);
        seVolume = Mathf.Clamp01(seVolume);

        if (bgmSource) bgmSource.volume = bgmVolume;
        if (seSource) seSource.volume = seVolume;
    }

    private void Awake()
    {
        // シングルトン & 永続化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // シーン名→BGM のマップ構築
        _sceneBgmMap.Clear();
        foreach (var e in sceneBgms)
        {
            if (e != null && !string.IsNullOrEmpty(e.sceneName) && e.bgmClip != null)
            {
                _sceneBgmMap[e.sceneName] = e.bgmClip;
            }
        }

        // 名前→SEクリップ のマップ構築（必要なら）
        _seClipMap.Clear();
        foreach (var c in seClips)
        {
            if (c != null) _seClipMap[c.name] = c;
        }

        bgmVolume = LoadVolume01(PrefKey_BgmVolume, bgmVolume);
        seVolume  = LoadVolume01(PrefKey_SeVolume, seVolume);

        ApplyCurrentVolumesToSources();

        // 最初のシーンの BGM を適用
        var active = SceneManager.GetActiveScene();
        ApplySceneBgm(active.name);

        // シーン切り替え監視
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        ApplySceneBgm(newScene.name);
    }

private void ApplySceneBgm(string sceneName)
{
    if (!_sceneBgmMap.TryGetValue(sceneName, out var clip) || clip == null)
    {
        // 設定が無いシーンは何もしない（前の BGM をそのまま流し続ける）
        return;
    }

    // RunScene の時だけ「特定ボス戦BGM」へ差し替え（未指定なら通常BGM）
    AudioClip finalClip = clip;
    if (sceneName == runSceneNameForBattleBgmOverride)
    {
        // 優先度：ハデス（裏ボス） > ゼウス
        if (hadesBattleBgmClip != null && IsHadesBattleNow())
        {
            finalClip = hadesBattleBgmClip;
        }
        else if (zeusBattleBgmClip != null && IsZeusBattleNow())
        {
            finalClip = zeusBattleBgmClip;
        }
    }

    PlayBGM(finalClip);
}

private bool IsHadesBattleNow()
{
    // GameManager 側の「ハデス判定」と同じ方針：
    // PF_SecretHadesRoute が有効で、現在の敵が SecretBossExcelKey のとき。:contentReference[oaicite:3]{index=3}
    bool isSecretRouteNow = false;
    try { isSecretRouteNow = PlayerPrefs.GetInt("PF_SecretHadesRoute", 0) == 1; } catch { isSecretRouteNow = false; }
    if (!isSecretRouteNow) return false;

    int runtimeEnemyIdxNow = 0;
    try { runtimeEnemyIdxNow = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { runtimeEnemyIdxNow = 0; }

    int excelKeyNow = 0;
    try { excelKeyNow = EnemyConfigExcel.MapRuntimeIndexToExcelKey(runtimeEnemyIdxNow); } catch { excelKeyNow = 0; }

    bool isHadesEnemyNow = (excelKeyNow == EnemyConfigExcel.SecretBossExcelKey);
    return isHadesEnemyNow;
}

private bool IsZeusBattleNow()
{
    // GameManager 側の「ゼウス判定」と同じ方針：
    // 現在敵の名前が「ゼウス」または "zeus" のとき。
    int runtimeEnemyIdxNow = 0;
    try { runtimeEnemyIdxNow = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { runtimeEnemyIdxNow = 0; }

    EnemyConfig cfg = null;
    try
    {
        if (!EnemyConfigExcel.TryGetForRuntimeIndex(runtimeEnemyIdxNow, out cfg))
        {
            cfg = null;
        }
    }
    catch { cfg = null; }

    string enemyName = "";
    try { enemyName = (cfg != null) ? (cfg.name ?? "") : ""; } catch { enemyName = ""; }
    if (string.IsNullOrEmpty(enemyName)) return false;

    string lower = "";
    try { lower = enemyName.Trim().ToLowerInvariant(); } catch { lower = ""; }

    if (enemyName.Trim() == "ゼウス") return true;
    if (lower == "zeus") return true;

    return false;
}
    public void PlayBGM(string clipName, bool loop = true)
    {
        if (_seClipMap.TryGetValue(clipName, out var clip))
        {
            PlayBGM(clip, loop);
        }
        // 名前登録してない場合は無視
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (!bgmSource || clip == null) return;

        // 同じBGMがすでに流れていれば何もしない
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    public void StopBGM(float fadeTime = 0f)
    {
        if (!bgmSource) return;

        if (fadeTime <= 0f)
        {
            bgmSource.Stop();
            return;
        }
        StartCoroutine(FadeOutBGM(fadeTime));
    }

    public float GetBgmVolume01()
    {
        return Mathf.Clamp01(bgmVolume);
    }

    public float GetSeVolume01()
    {
        return Mathf.Clamp01(seVolume);
    }

    public int GetBgmVolume100()
    {
        return Mathf.RoundToInt(Mathf.Clamp01(bgmVolume) * 100f);
    }

    public int GetSeVolume100()
    {
        return Mathf.RoundToInt(Mathf.Clamp01(seVolume) * 100f);
    }

    public void SetBgmVolume01(float value, bool save = true)
    {
        bgmVolume = Mathf.Clamp01(value);
        if (bgmSource) bgmSource.volume = bgmVolume;

        if (save)
        {
            SaveVolume01(PrefKey_BgmVolume, bgmVolume);
        }
    }

    public void SetSeVolume01(float value, bool save = true)
    {
        seVolume = Mathf.Clamp01(value);
        if (seSource) seSource.volume = seVolume;

        if (save)
        {
            SaveVolume01(PrefKey_SeVolume, seVolume);
        }
    }

    public void SetBgmVolume100(float value, bool save = true)
    {
        SetBgmVolume01(value / 100f, save);
    }

    public void SetSeVolume100(float value, bool save = true)
    {
        SetSeVolume01(value / 100f, save);
    }

    public void ReloadSavedVolumes()
    {
        bgmVolume = LoadVolume01(PrefKey_BgmVolume, bgmVolume);
        seVolume  = LoadVolume01(PrefKey_SeVolume, seVolume);
        ApplyCurrentVolumesToSources();
    }

    private void ApplyCurrentVolumesToSources()
    {
        if (bgmSource) bgmSource.volume = bgmVolume;
        if (seSource)  seSource.volume  = seVolume;
    }

    private static float LoadVolume01(string key, float defaultValue)
    {
        try
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, Mathf.Clamp01(defaultValue)));
        }
        catch
        {
            return Mathf.Clamp01(defaultValue);
        }
    }

    private static void SaveVolume01(string key, float value)
    {
        try
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
        catch { }
    }

    private System.Collections.IEnumerator FadeOutBGM(float duration)
    {
        float start = bgmSource.volume;
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            bgmSource.volume = Mathf.Lerp(start, 0f, t);
            yield return null;
        }
        bgmSource.Stop();
        bgmSource.volume = bgmVolume;
    }

    // ==============================
    //  SE 制御（用途別）
    // ==============================
    private void PlaySE_Internal(AudioClip clip)
    {
        if (!seSource || clip == null) return;
        seSource.PlayOneShot(clip, seVolume);
    }
    public void PlayScoringStepGoldSE()            => PlaySE_Internal(scoringStepGoldSE);
public void PlayScoringStepUnderManganSE()     => PlaySE_Internal(scoringStepUnderManganSE);
public void PlayScoringStepManganToYakumanSE() => PlaySE_Internal(scoringStepManganToYakumanSE);
public void PlayScoringStepYakumanOrAboveSE()  => PlaySE_Internal(scoringStepYakumanOrAboveSE);
public void PlaySE(AudioClip clip, float volumeScale = 1f)
{
    if (!seSource || clip == null) return;

    float v = Mathf.Clamp01(seVolume * Mathf.Clamp01(volumeScale));
    seSource.PlayOneShot(clip, v);
}
public void PlayClickSE()       => PlaySE_Internal(clickSE);
public void PlayDiscardTileSE() => PlaySE_Internal(discardTileSE);
public void PlayDrawTileSE()    => PlaySE_Internal(drawTileSE);
public void PlaySelectTileSE()  => PlaySE_Internal(selectTileSE);
public void PlaySwapTileSE()    => PlaySE_Internal(swapTileSE);

public void PlayBattleDamageSE()   => PlaySE_Internal(battleDamageSE);
public void PlayDealOfferTileSE()  => PlaySE_Internal(dealOfferTileSE);
public void PlayOpeningHandDealGroupSE() => PlaySE_Internal(openingHandDealGroupSE);

public void PlayScoringPanelOkSE() => PlaySE_Internal(scoringPanelOkSE);
public void PlayVictorySE()        => PlaySE_Internal(victorySE);
public void PlayDefeatSE()         => PlaySE_Internal(defeatSE);
    // 名前指定で SE を鳴らしたい場合（任意）
    public void PlaySEByName(string clipName)
    {
        if (!_seClipMap.TryGetValue(clipName, out var clip)) return;
        PlaySE_Internal(clip);
    }
    // ==============================
//  このスレッド分：用途別SE API
// ==============================
private static string NormalizeRarityKey(string raw)
{
    raw = (raw ?? "").Trim();

    // 日本語入力もざっくり吸収
    if (raw.Contains("レジェ")) return "Legendary";
    if (raw.Contains("エピ"))   return "Epic";
    if (raw.Contains("レア"))   return "Rare";
    if (raw.Contains("コモン")) return "Common";
    if (raw.Contains("ノーマ")) return "Normal";

    // 英語キー吸収
    string r = raw.ToLowerInvariant();
    if (r == "legendary") return "Legendary";
    if (r == "epic")      return "Epic";
    if (r == "rare")      return "Rare";
    if (r == "common")    return "Common";
    if (r == "normal")    return "Normal";

    return raw;
}

public void PlayGoldSpendSE()
{
    PlaySE(goldSpendSEClip);
}

public void PlayGemGetSE()
{
    PlaySE(gemGetSE);
}

public void PlayUniqueOmamoriGetSE()
{
    PlaySE(uniqueOmamoriGetSE);
}

public void PlayOmamoriRevealSE_ByRarity(string rarityRaw)
{
    string key = NormalizeRarityKey(rarityRaw);

    AudioClip clip = null;
    switch (key)
    {
        case "Legendary": clip = omamoriRevealSE_Legendary; break;
        case "Epic":      clip = omamoriRevealSE_Epic;      break;
        case "Rare":      clip = omamoriRevealSE_Rare;      break;
        case "Common":    clip = omamoriRevealSE_Common;    break;
        case "Normal":    clip = omamoriRevealSE_Normal;    break;
        default:          clip = omamoriRevealSE_Normal;    break;
    }
    PlaySE(clip);
}

public void PlaySpecialTileBuySE_ByRarity(SpecialTileSystem.Rarity rarity)
{
    AudioClip clip = null;
    switch (rarity)
    {
        case SpecialTileSystem.Rarity.Legendary: clip = specialTileBuySE_Legendary; break;
        case SpecialTileSystem.Rarity.Epic:      clip = specialTileBuySE_Epic;      break;
        case SpecialTileSystem.Rarity.Rare:      clip = specialTileBuySE_Rare;      break;
        case SpecialTileSystem.Rarity.Common:    clip = specialTileBuySE_Common;    break;
        case SpecialTileSystem.Rarity.Normal:    clip = specialTileBuySE_Normal;    break;
        default:                                 clip = specialTileBuySE_Normal;    break;
    }
    PlaySE(clip);
}

public void PlayCutin_PlayerRon()   => PlaySE(playerRonCutinSEClip);
public void PlayCutin_PlayerTsumo() => PlaySE(playerTsumoCutinSEClip);
public void PlayCutin_EnemyRon()    => PlaySE(enemyRonCutinSEClip);
public void PlayCutin_EnemyTsumo()  => PlaySE(enemyTsumoCutinSEClip);

public void PlayCutin_EnemySkill()  => PlaySE(enemySkillCutinSEClip);

public void PlayCutin_PlayerRiichi() => PlaySE(playerRiichiCutinSEClip);
public void PlayCutin_EnemyRiichi()  => PlaySE(enemyRiichiCutinSEClip);

public void PlayCutin_PlayerSkill() => PlaySE(playerSkillCutinSEClip);
public void PlayBattleResultVictorySE() => PlaySE(battleResultVictorySEClip);
public void PlayBattleResultDefeatSE()  => PlaySE(battleResultDefeatSEClip);
}