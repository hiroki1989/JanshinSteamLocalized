using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using System.Reflection;
using UnityEngine.EventSystems; // ★ 追加
using UnityEngine.InputSystem; // 新Input System

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;   // 新 Input System
#endif
public partial class GameManager : MonoBehaviour
{

    // ==== Omamori cache ====
    private bool _omamoriBaseApplied = false;
    private PlayerData.OmamoriStats _om; // キャッシュ

    private void RefreshOmamoriCache()
    {
        try
        {
            _om = PlayerData.GetEquippedStats();
        }
        catch
        {
            _om = default;
        }
    }

    // お札判定に使う直近の和了コンテキスト
    private List<string> _lastScoringYaku = new List<string>();
    private bool _lastScoringIsTsumo = false;
    private int  _lastScoringBasePoints = 0;   // 満貫以上などの判定用
private bool _currentScoringAttackerIsPlayer = true; // デフォルトはプレイヤー
    // === Scoring manual UI (optional) ===

    [Header("Scoring manual UI (optional)")]
    public Transform scoringPlayerTilesManual; // スコアパネル：プレイヤーの和了手牌描画先
    public Transform scoringEnemyTilesManual;  // スコアパネル：敵の和了手牌描画先
    public Transform scoringDoraManualRoot;    // スコアパネル：表/裏ドラの手動描画ルート
private string _scoringUsedTileLabel = null;  // ★追加：スコアパネル用に和了牌ラベルを一時保持

// ★追加：ツモ和了に使った「offers内のインデックス」（重複IDでもズレないようにする）
private int _lastPlayerTsumoOfferIndex = -1;

private string _continueAfterPlayerWin_ExcludedTileId = null;   // 和了牌（この牌だけ敵のロン/鳴き対象から除外）
private int _continueAfterPlayerWin_ExcludedDiscardIndex = -1;  // discards上でグレーアウトする「インデックス」
private bool _continueAfterPlayerWin_AddedDiscardsThisScoring = false; // 多重追加防止
private int _lastPlayerRonEnemyDiscardIndex = -1;              // プレイヤーがロン和了したときの敵捨て牌インデックス
private string _lastPlayerRonEnemyDiscardTileLogic = null;     // 上記の牌ID（ロジック用：*_sp やサフィックス除去済み）

// ★和了後もUI表示を維持するためのスナップショット（ロジック用の hand/melds を空にしても表示は残す）
private List<string> _playerWonHandSnapshot = null;
private List<List<string>> _playerWonMeldsSnapshot = null;

private List<string> _enemyWonHandSnapshot = null;

// ★敵ターン多重実行ガード（リーチ後の自動進行で稀に敵捨て牌が複数回出るのを防ぐ）
private bool _enemyTurnRunning = false;

// ★追加：BeginOfferPhase 多重実行ガード（スキップ連打で2回ツモが走るのを防ぐ）
private bool _beginOfferPhaseInProgress = false;

// --- Dora (simple manual binding) ---
public Transform           scoringDoraOmoteRow;   // 表ドラを並べる行（Image 子を並べておく）
public Transform           scoringDoraUraRow;     // 裏ドラを並べる行（Image 子を並べておく）
public TextMeshProUGUI     scoringDoraOmoteLabel; // 任意（「表ドラ」）
public TextMeshProUGUI     scoringDoraUraLabel;   // 任意（「裏ドラ」）
// === Scoring Panel (Manual value fields) ===
// 共通
[Header("Scoring Panel (Manual value fields)")]
[SerializeField] private TextMeshProUGUI scoringRoleValue;              // 「役」の中身
[SerializeField] private TextMeshProUGUI scoringFuHanValue;             // 「符・翻」の中身（満貫以上は符を隠すロジックは既存を踏襲）
[SerializeField] private TextMeshProUGUI scoringBasePointValue;         // 「点数」（= 基礎点）
[SerializeField] private TextMeshProUGUI scoringRoleValue_Enemy;      // 役（敵）
[SerializeField] private TextMeshProUGUI scoringFuHanValue_Enemy;     // 符・翻（敵）
[SerializeField] private TextMeshProUGUI scoringBasePointValue_Enemy; // 点数（敵）
[SerializeField] private GameObject      scoringPlayerOnlyRoot;         // プレイヤー和了用の親（任意）
[SerializeField] private GameObject      scoringEnemyOnlyRoot;          // 敵和了用の親（任意）
[SerializeField] private TextMeshProUGUI scoringGoldGainValue;  
private int _goldGainThisWin = 0;
private string _goldGainDisplayTextThisWin = "-";
// プレイヤー和了専用
[SerializeField] private TextMeshProUGUI scoringGekiValue;              // 「撃の効果」（倍率 xN.NNN）

[SerializeField] private TextMeshProUGUI scoringShunValue;              // 「瞬の効果」（倍率 xN.NNN）
[SerializeField] private TextMeshProUGUI scoringIyuValue;               // 「癒の効果」（倍率 xN.NNN）
[SerializeField] private TextMeshProUGUI scoringOfudaDmgValue;          // 「お札の効果によるダメージ増加」（N.N倍）
[SerializeField] private TextMeshProUGUI scoringOfudaHpValue;           // 「お札の効果によるHP回復」（%）
[SerializeField] private TextMeshProUGUI scoringOfudaMpValue;           // 「お札の効果によるMP回復」（%）
[SerializeField] private TextMeshProUGUI scoringFinalDamageToEnemyValue;// 「敵への最終ダメージ」

// 敵和了専用
[SerializeField] private TextMeshProUGUI scoringOmamoriReduceValue;     // 「お守りによるダメージ軽減」（%）
[SerializeField] private TextMeshProUGUI scoringFinalDamageToPlayerValue;// 「プレイヤーへの最終ダメージ」
// ===============================
//  Scoring Panel: Step-by-step reveal (演出用)
// ===============================
[Header("Scoring Panel (Step Reveal)")]
[SerializeField] private bool scoringStepRevealEnabled = true;

[Tooltip("各ステップの自動進行秒数（TimeScale無視）。クリックで次へ進めます。")]
[SerializeField] private float scoringStepInterval = 0.5f;
[Tooltip("点数計算パネルを表示してから、1個目のステップを出すまでの待ち秒数（TimeScale無視）。クリックでスキップ可能。")]
[SerializeField] private float scoringStepFirstDelay = 0.5f;
[Tooltip("プレイヤーのスコアパネル上で『次へ』を受け取るための透明ボタン（任意）。未指定なら入力検知で代替します。")]
[SerializeField] private Button scoringStepAdvanceButtonPlayer;

[Tooltip("敵のスコアパネル上で『次へ』を受け取るための透明ボタン（任意）。未指定なら入力検知で代替します。")]
[SerializeField] private Button scoringStepAdvanceButtonEnemy;
[Tooltip("プレイヤー和了のときに順番に表示するRoot（上から順番）。")]
[SerializeField] private List<GameObject> scoringStepRoots_Player = new List<GameObject>();

[Tooltip("敵和了のときに順番に表示するRoot（上から順番）。")]
[SerializeField] private List<GameObject> scoringStepRoots_Enemy = new List<GameObject>();
[Header("Scoring Panel (Step Reveal - Gold Root)")]
[Tooltip("プレイヤー点数計算パネルの段階表示で、取得ゴールド表示に該当するRoot。ここが表示された瞬間だけ別SEにする。")]
[SerializeField] private GameObject scoringStepGoldRoot_Player;
[Header("Scoring Panel (Step Reveal SE)")]
[Tooltip("ステップ表示ごとに鳴らすSE。未指定なら鳴らしません。")]
[SerializeField] private AudioSource scoringStepSESource;

[Tooltip("ステップ表示ごとに鳴らすSEクリップ（共通）。")]
[SerializeField] private AudioClip scoringStepSEClip;
[SerializeField] private GameObject playerSkillCutinRoot;                 // プレイヤースキル発動時のカットインルート
[SerializeField] private TextMeshProUGUI playerSkillCutinTextTMP;         // スキル名表示
[SerializeField] private UnityEngine.UI.Image playerSkillCutinImage;      // カットイン画像
[SerializeField] private CutinSpriteAnimator playerSkillCutinAnimator;    // プレイヤースキル発動アニメータ
[SerializeField] private CutinSpriteAnimator playerRiichiCutinAnimator;   // プレイヤーリーチアニメータ
[SerializeField] private CutinSpriteAnimator playerWinCutinAnimator;      // プレイヤーツモ/ロンアニメータ
[Header("Player Skill Transform FX (DyeMaster / Calligrapher)")]
[SerializeField] private Color dyeMasterTransformFxColor = new Color(0.2f, 0.7f, 1.0f, 1.0f);        // 染色師：変換演出の色
[SerializeField] private Color calligrapherTransformFxColor = new Color(0.9f, 0.2f, 1.0f, 1.0f);     // 書家：変換演出の色
[SerializeField] private Color capitalistTransformFxColor = new Color(1.0f, 0.85f, 0.25f, 1.0f);      // 資産家：変換演出の色

private bool _playerSkillCutinRunning = false;
private bool _playerSkillTransformRunning = false;
private Coroutine _playerSkillCutinCo = null;
[Header("Demo End Cutin")]
[SerializeField] private bool demoModeEnabled = true;
[SerializeField] private int demoEndAfterDefeatedEnemies = 3;
[SerializeField] private GameObject demoEndCutinRoot;
[SerializeField] private CanvasGroup demoEndCutinGroup;
[SerializeField] private TextMeshProUGUI demoEndCutinTMP;
[SerializeField] private Button demoEndCutinOkButton;
[SerializeField, TextArea] private string demoEndMessage = "Demo版はここまでです。面白かったらぜひレビューおよび製品版のウィッシュリスト登録・ご購入をご検討ください";
private string ResolveDemoEndMessage_Local()
{
    const string defaultJa = "Demo版はここまでです。面白かったらぜひレビューおよび製品版のウィッシュリスト登録・ご購入をご検討ください";

    if (string.IsNullOrEmpty(demoEndMessage))
        return GetGameFixedText_Local("demo_end_message");

    if (demoEndMessage == defaultJa)
        return GetGameFixedText_Local("demo_end_message");

    return demoEndMessage;
}
private bool _demoEndCutinRunning = false;
private static string GetGameFixedText_Local(string key)
{
    var lm = LocalizationManager.Instance;
    if (lm == null) return key;
    return lm.GetFixedText(key);
}

private static string FormatGameFixedText_Local(string key, params object[] args)
{
    string format = GetGameFixedText_Local(key);

    try
    {
        return string.Format(format, args);
    }
    catch
    {
        return format;
    }
}
private bool __ShouldShowDemoEndCutin()
{
    if (!demoModeEnabled) return false;
    if (demoEndAfterDefeatedEnemies <= 0) return false;

    try
    {
        int defeated = Mathf.Max(0, PlayerPrefs.GetInt("Run_DefeatedEnemyCount", 0));
        return defeated >= demoEndAfterDefeatedEnemies;
    }
    catch
    {
        return false;
    }
}
private bool _preparedForSceneUnload = false;
private IEnumerator __ShowDemoEndCutinThen(System.Action next)
{
    if (_demoEndCutinRunning)
    {
        yield break;
    }
    _demoEndCutinRunning = true;

    try
    {
        if (demoEndCutinRoot == null || demoEndCutinGroup == null || demoEndCutinTMP == null || demoEndCutinOkButton == null)
        {
            next?.Invoke();
            yield break;
        }

        bool confirmed = false;

        demoEndCutinTMP.text = ResolveDemoEndMessage_Local();
        demoEndCutinRoot.SetActive(true);
        demoEndCutinGroup.alpha = 0f;
        demoEndCutinGroup.interactable = true;
        demoEndCutinGroup.blocksRaycasts = true;
        demoEndCutinOkButton.onClick.RemoveAllListeners();
        demoEndCutinOkButton.onClick.AddListener(() => confirmed = true);

        yield return __Fade(demoEndCutinGroup, 0f, 1f, 0.1f);

        while (!confirmed)
        {
            yield return null;
        }

        yield return __Fade(demoEndCutinGroup, 1f, 0f, 0.1f);

        demoEndCutinRoot.SetActive(false);
        next?.Invoke();
    }
    finally
    {
        if (demoEndCutinOkButton != null)
        {
            demoEndCutinOkButton.onClick.RemoveAllListeners();
        }

        _demoEndCutinRunning = false;
    }
}
private int GetTraitEffectiveLevelForScoring(
    SkillSetAsset hostSet,
    string activeSkillName,
    SkillSetAsset.Trait trait,
    string yakuName)
{
    int baseLv = 0;

    try
    {
        if (hostSet != null && !string.IsNullOrEmpty(activeSkillName) && !string.IsNullOrEmpty(yakuName))
        {
            baseLv = Mathf.Max(0, hostSet.GetTraitYakuLevel(activeSkillName, trait, yakuName));
        }
    }
    catch
    {
        baseLv = 0;
    }

    int specialBonus = 0;

    try
    {
        string target = NormalizeTraitJudgeYakuName_Local(yakuName);

        if (!string.IsNullOrEmpty(target))
        {
            if (_specialTileTraitLvBonusThisScoring != null)
            {
                foreach (var kv in _specialTileTraitLvBonusThisScoring)
                {
                    string k = NormalizeTraitJudgeYakuName_Local(kv.Key);
                    if (string.IsNullOrEmpty(k)) continue;
                    if (!string.Equals(k, target, StringComparison.OrdinalIgnoreCase)) continue;

                    specialBonus += Mathf.Max(0, kv.Value);
                }
            }

            if (specialBonus <= 0)
            {
                Dictionary<string, int> equippedMap = null;

                try
                {
                    equippedMap = SpecialTileSystem.GetEquippedTraitBonusMap();
                }
                catch
                {
                    equippedMap = null;
                }

                if (equippedMap != null)
                {
                    foreach (var kv in equippedMap)
                    {
                        string k = NormalizeTraitJudgeYakuName_Local(kv.Key);
                        if (string.IsNullOrEmpty(k)) continue;
                        if (!string.Equals(k, target, StringComparison.OrdinalIgnoreCase)) continue;

                        specialBonus += Mathf.Max(0, kv.Value);
                    }
                }
            }
        }
    }
    catch
    {
        specialBonus = 0;
    }

    return Mathf.Max(0, baseLv + specialBonus);
}
private void AddSpecialTileTraitBonusPacked_Local(string packed)
{
    if (string.IsNullOrWhiteSpace(packed))
        return;

    string[] tokens = packed.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

    for (int i = 0; i < tokens.Length; i++)
    {
        string token = (tokens[i] ?? "").Trim();
        if (string.IsNullOrEmpty(token))
            continue;

        int eq = token.IndexOf('=');
        if (eq <= 0 || eq >= token.Length - 1)
            continue;

        string rawYaku = token.Substring(0, eq).Trim();
        string rawLv = token.Substring(eq + 1).Trim();

        if (!int.TryParse(rawLv, out int addLv))
            continue;

        addLv = Mathf.Max(0, addLv);
        if (addLv <= 0)
            continue;

        string normalized = NormalizeTraitJudgeYakuName_Local(rawYaku);
        if (string.IsNullOrEmpty(normalized))
            continue;

        if (_specialTileTraitLvBonusThisScoring.TryGetValue(normalized, out int cur))
            _specialTileTraitLvBonusThisScoring[normalized] = cur + addLv;
        else
            _specialTileTraitLvBonusThisScoring[normalized] = addLv;
    }
}
private static string BuildLocalizedCountYakuText(string key, int count)
{
    string format = GetGameFixedText_Local(key);

    try
    {
        return string.Format(format, count);
    }
    catch
    {
        return format + count.ToString();
    }
}

private static string BuildLocalizedBonusYakuText(string key, int value)
{
    string format = GetGameFixedText_Local(key);

    try
    {
        return string.Format(format, value);
    }
    catch
    {
        return format + value.ToString();
    }
}
private string GetActiveSkillDisplayNameSafe(ActiveSkill s)
{
    try
    {
        string skillName = s.ToString();

        SkillSetAsset hostSet = null;

        // 1) まず _skillSet がこのスキルの所属ならそれを使う
        if (_skillSet != null && _skillSet.activeSkills != null &&
            _skillSet.activeSkills.Any(e => e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, System.StringComparison.OrdinalIgnoreCase)))
        {
            hostSet = _skillSet;
        }

        // 2) 見つからなければ Resources/SkillSets を総当たり
        if (hostSet == null)
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            foreach (var sset in allSets)
            {
                if (sset == null || sset.activeSkills == null) continue;

                var entry = sset.activeSkills.FirstOrDefault(e =>
                    e != null && !string.IsNullOrEmpty(e.activeSkillName) &&
                    string.Equals(e.activeSkillName.Trim(), skillName, System.StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    hostSet = sset;
                    break;
                }
            }
        }

        if (hostSet != null && hostSet.activeSkills != null)
        {
            var entry = hostSet.activeSkills.FirstOrDefault(e =>
                e != null && !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, System.StringComparison.OrdinalIgnoreCase));

            if (entry != null && !string.IsNullOrEmpty(entry.displayName))
                return entry.displayName;
        }
    }
    catch { }

    return s.ToString();
}

private bool IsCapitalistEquippedForHadesRelic_Local()
{
    try
    {
        string equipped = PlayerPrefs.GetString("EquippedActiveSkill", "");
        equipped = string.IsNullOrEmpty(equipped) ? "" : equipped.Trim();

        if (string.Equals(equipped, "Capitalist", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(equipped, "資産家", StringComparison.OrdinalIgnoreCase))
            return true;

        if (LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.GetActiveSkillDisplayName("Capitalist");
            if (!string.IsNullOrEmpty(localized) &&
                string.Equals(equipped, localized.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }
    catch
    {
    }

    return false;
}

private static string NormalizeTraitJudgeYakuName_Local(string rawYakuName)
{
    if (string.IsNullOrEmpty(rawYakuName))
        return "";

    string s = rawYakuName.Trim();

    if (s.Length == 0)
        return "";

    s = s.Replace("　", " ");
    s = s.Replace('（', '(').Replace('）', ')');

    int p = s.IndexOf('(');
    if (p >= 0)
        s = s.Substring(0, p);

    s = s.Trim();

    if (s.Length == 0)
        return "";

    string compact = s.Replace(" ", "");

    if (compact.Contains("風牌")) return "役牌";
    if (compact.Contains("役牌")) return "役牌";
    if (compact == "白" || compact == "發" || compact == "発" || compact == "中") return "役牌";

    var lm = LocalizationManager.Instance;
    if (lm == null)
        return s;

    if (MatchesLocalizedYakuName_Local(s, lm.GetYakumanDisplayName("KOKUSHI"))) return "国士無双";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("CHIITOITSU"))) return "七対子";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("MENZEN_TSUMO"))) return "門前清自摸和";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("TANYAO"))) return "タンヤオ";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("PINFU"))) return "平和";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("YAKUHAI"))) return "役牌";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("IIPEIKOU"))) return "一盃口";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("RYANPEIKOU"))) return "二盃口";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("SANSHOKU_DOUJUN"))) return "三色同順";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("ITTSU"))) return "一気通貫";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("CHANTA"))) return "チャンタ";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("JUNCHAN"))) return "純チャン";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("TOITOI"))) return "対々和";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("SANANKOU"))) return "三暗刻";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("SANKANTSU"))) return "三カンツ";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("SANSHOKU_DOUKOU"))) return "三色同刻";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("SHOUSANGEN"))) return "小三元";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("HONROUTOU"))) return "混老頭";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("HONITSU"))) return "混一色";
    if (MatchesLocalizedYakuName_Local(s, lm.GetYakuDisplayName("CHINITSU"))) return "清一色";

    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("CHUUREN_POUTOU"))) return "九蓮宝燈";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("DAISANGEN"))) return "大三元";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("DAISUUSHI"))) return "大四喜";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("SHOUSUUSHI"))) return "小四喜";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("TSUUIISOU"))) return "字一色";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("CHINROUTOU"))) return "清老頭";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("RYUUIISOU"))) return "緑一色";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("SUUANKOU"))) return "四暗刻";
    if (MatchesLocalizedYakumanName_Local(s, lm.GetYakumanDisplayName("SUUKANTSU"))) return "四カンツ";

    return s;
}

private static bool MatchesLocalizedYakuName_Local(string raw, string localized)
{
    if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(localized))
        return false;

    string a = raw.Trim().Replace("　", " ");
    string b = localized.Trim().Replace("　", " ");

    a = Regex.Replace(a, @"\((.*?)\)", "").Trim();
    a = Regex.Replace(a, @"\s*[×xX]\s*\d+\s*$", "").Trim();
    a = Regex.Replace(a, @"\s+\d+\s*$", "").Trim();

    b = Regex.Replace(b, @"\((.*?)\)", "").Trim();
    b = Regex.Replace(b, @"\s*[×xX]\s*\d+\s*$", "").Trim();
    b = Regex.Replace(b, @"\s+\d+\s*$", "").Trim();

    return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
private static bool MatchesLocalizedYakumanName_Local(string raw, string localized)
{
    if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(localized))
        return false;

    string a = raw.Trim().Replace("　", " ");
    string b = localized.Trim().Replace("　", " ");

    return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
private static string __NormalizeWinKindToken(string s)
{
    if (string.IsNullOrEmpty(s)) return "";

    s = s.Trim();
    if (s.Length == 0) return "";

    string u = s.ToUpperInvariant();

    if (s.Contains("ツモ")) return "TSUMO";
    if (s.Contains("自摸")) return "TSUMO";
    if (u.Contains("TSUMO")) return "TSUMO";

    if (s.Contains("ロン")) return "RON";
    if (u.Contains("RON")) return "RON";

    return "";
}

private static string __NormalizeEnemyBaseNameToken(string s)
{
    if (string.IsNullOrEmpty(s)) return "";

    string n = s.Replace(" ", "").Replace("　", "").Trim();
    if (n.Length == 0) return "";

    string lower = n.ToLowerInvariant();

    if (n == "ハデス") return "HADES";
    if (lower == "hades") return "HADES";

    return n;
}

private static bool __IsNamedEnemyHades(string s)
{
    return string.Equals(__NormalizeEnemyBaseNameToken(s), "HADES", StringComparison.Ordinal);
}
private static bool __IsTsumoKind(string s)
{
    return string.Equals(__NormalizeWinKindToken(s), "TSUMO", StringComparison.Ordinal);
}

private static bool __IsRonKind(string s)
{
    return string.Equals(__NormalizeWinKindToken(s), "RON", StringComparison.Ordinal);
}
private void __SetScoringStepAdvanceButtonVisible(Button btn, bool visible)
{
    if (!btn) return;

    btn.gameObject.SetActive(visible);

    try
    {
        var cg = btn.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }
    }
    catch { }
}
private System.Collections.IEnumerator __ShowPlayerSkillCutin(string skillDisplayName)
{
    if (playerSkillCutinRoot == null)
        yield break;

    _playerSkillCutinRunning = true;
    UpdateButtons();

    if (playerSkillCutinTextTMP != null)
    {
        string cutinText = skillDisplayName;
        if (string.IsNullOrEmpty(cutinText))
            cutinText = GetGameFixedText_Local("skill_activated");

        playerSkillCutinTextTMP.text = cutinText;
    }

    try
    {
        if (playerSkillCutinImage != null)
        {
            Sprite sp = GetPlayerCutinFallbackSpriteSafe();

            if (sp != null)
            {
                playerSkillCutinImage.enabled = true;
                playerSkillCutinImage.sprite = sp;
                playerSkillCutinImage.preserveAspect = true;
            }
            else
            {
                playerSkillCutinImage.enabled = (playerSkillCutinImage.sprite != null);
            }
        }
    }
    catch { }

    var cg = playerSkillCutinRoot.GetComponent<CanvasGroup>();
    if (cg != null) cg.alpha = 1f;

    playerSkillCutinRoot.SetActive(true);

    PlayPlayerCutinAnimation(playerSkillCutinAnimator, "Skill", playerSkillCutinImage);

    if (AudioManager.Instance)
    {
        AudioManager.Instance.PlayCutin_PlayerSkill();
    }

    yield return StartCoroutine(WaitPlayerCutinAnimationOrSeconds(playerSkillCutinAnimator, 1.5f));

    if (playerSkillCutinRoot != null)
        playerSkillCutinRoot.SetActive(false);

    _playerSkillCutinRunning = false;
    UpdateButtons();
    _playerSkillCutinCo = null;
}
private string GetCurrentPlayerCutinSkillId()
{
    try
    {
        var skill = GetEquippedSkill();
        if (skill == ActiveSkill.None)
            return "Default";

        return skill.ToString();
    }
    catch
    {
        return "Default";
    }
}

private string BuildPlayerCutinClipId(string kind)
{
    string skillId = GetCurrentPlayerCutinSkillId();

    if (string.IsNullOrEmpty(kind))
        kind = "Default";

    return skillId + "_" + kind;
}

private Sprite GetPlayerCutinFallbackSpriteSafe()
{
    try
    {
        return GetPlayerCutinSpriteForCurrentSkill();
    }
    catch
    {
        return null;
    }
}

private void PlayPlayerCutinAnimation(CutinSpriteAnimator animator, string kind, Image fallbackImage = null)
{
    if (animator == null)
        return;

    string clipId = BuildPlayerCutinClipId(kind);
    Sprite fallbackSprite = GetPlayerCutinFallbackSpriteSafe();

    if (fallbackSprite == null && fallbackImage != null)
        fallbackSprite = fallbackImage.sprite;

    animator.Play(clipId, fallbackSprite);
}

private IEnumerator WaitPlayerCutinAnimationOrSeconds(CutinSpriteAnimator animator, float fallbackSeconds)
{
    if (animator != null)
    {
        while (animator.IsPlaying)
            yield return null;

        yield break;
    }

    yield return new WaitForSeconds(fallbackSeconds);
}
private void OnDisable()
{
    if (_preparedForSceneUnload)
        return;

    __PrepareForSceneUnload();
}

private void __PrepareForSceneUnload()
{
    __PrepareForSceneUnload(true);
}

private void __PrepareForSceneUnload(bool stopAllCoroutines = true)
{
    if (_preparedForSceneUnload)
        return;

    _preparedForSceneUnload = true;

    // 既に破棄途中でも落ちないように全体を防御
    try
    {
        // 個別管理しているコルーチン
        if (_playerSkillCutinCo != null)
        {
            StopCoroutine(_playerSkillCutinCo);
            _playerSkillCutinCo = null;
        }

        if (_scoringStepRevealCo != null)
        {
            StopCoroutine(_scoringStepRevealCo);
            _scoringStepRevealCo = null;
        }

        // 段階表示の内部状態
        _scoringStepAdvanceRequested = false;

        // 演出中フラグを解除
        _playerSkillCutinRunning = false;
        _playerSkillTransformRunning = false;
        _enemySkillCutinRunning = false;
        _enemyRiichiCutinRunning = false;
        _freezeProgression = false;

        // 透明進行ボタン・OKボタン状態を通常化
        try { __StopScoringStepReveal(); } catch { }

        // カットイン・演出UIを全部閉じる
        if (playerSkillCutinRoot != null)
            playerSkillCutinRoot.SetActive(false);

        if (winCutinRoot != null)
            winCutinRoot.SetActive(false);

        if (enemySkillCutinRoot != null)
            enemySkillCutinRoot.SetActive(false);

        if (specialTilePopupRoot != null)
            specialTilePopupRoot.SetActive(false);

        if (matchStartCutinGroup != null && matchStartCutinGroup.gameObject != null)
            matchStartCutinGroup.gameObject.SetActive(false);

        if (stopAllCoroutines)
            StopAllCoroutines();
    }
    catch
    {
    }
}
// 内部状態
private Coroutine _scoringStepRevealCo;
private bool _scoringStepAdvanceRequested = false;
private Button __GetScoringStepAdvanceButton(bool attackerIsPlayer)
{
    return attackerIsPlayer ? scoringStepAdvanceButtonPlayer : scoringStepAdvanceButtonEnemy;
}
private void __RequestAdvanceScoringStep()
{
    _scoringStepAdvanceRequested = true;
}
private void __StopScoringStepReveal()
{
    if (_scoringStepRevealCo != null)
    {
        StopCoroutine(_scoringStepRevealCo);
        _scoringStepRevealCo = null;
    }
    _scoringStepAdvanceRequested = false;

    // Advance ボタンのリスナーを外す（多重登録防止）
    if (scoringStepAdvanceButtonPlayer)
    {
        scoringStepAdvanceButtonPlayer.onClick.RemoveListener(__RequestAdvanceScoringStep);
    }
    if (scoringStepAdvanceButtonEnemy)
    {
        scoringStepAdvanceButtonEnemy.onClick.RemoveListener(__RequestAdvanceScoringStep);
    }

    // ★追加：透明の進行ボタンを必ず閉じる
    __SetScoringStepAdvanceButtonVisible(scoringStepAdvanceButtonPlayer, false);
    __SetScoringStepAdvanceButtonVisible(scoringStepAdvanceButtonEnemy, false);

    // OKボタンは通常状態に戻す（演出中に閉じた場合でも詰まらないように）
    __SetScoringOkButtonsInteractable(true);
}
private void StartPlayerSkillCutin(string skillDisplayName)
{
    // 既に再生中なら止めてからやり直す（フラグ固着を防ぐ）
    if (_playerSkillCutinCo != null)
    {
        StopCoroutine(_playerSkillCutinCo);
        _playerSkillCutinCo = null;
    }

    // UI未設定なら何もしない
    if (playerSkillCutinRoot == null)
        return;

    // 念のため最初に非表示へ（状態ズレ防止）
    playerSkillCutinRoot.SetActive(false);

    _playerSkillCutinCo = StartCoroutine(__ShowPlayerSkillCutin(skillDisplayName));
}
private void __StartScoringStepReveal(bool attackerIsPlayer)
{
    if (!scoringStepRevealEnabled) return;

    // 既に走っていたら止めてから再スタート
    __StopScoringStepReveal();

    // ステップ対象が無ければ何もしない（デグレ防止）
    var roots = attackerIsPlayer ? scoringStepRoots_Player : scoringStepRoots_Enemy;
    if (roots == null || roots.Count == 0)
    {
        return;
    }

    // クリックで進める（任意）：プレイヤー/敵で別ボタンを使う
    var advanceButton = __GetScoringStepAdvanceButton(attackerIsPlayer);

    // ★追加：開始前に両方とも閉じる
    __SetScoringStepAdvanceButtonVisible(scoringStepAdvanceButtonPlayer, false);
    __SetScoringStepAdvanceButtonVisible(scoringStepAdvanceButtonEnemy, false);

    if (advanceButton)
    {
        // ★追加：今回使う方だけ開く
        __SetScoringStepAdvanceButtonVisible(advanceButton, true);

        advanceButton.onClick.RemoveListener(__RequestAdvanceScoringStep);
        advanceButton.onClick.AddListener(__RequestAdvanceScoringStep);
    }

    // 演出中はOKボタンを押せないようにして、最後に解放する
    __SetScoringOkButtonsInteractable(false);

    _scoringStepRevealCo = StartCoroutine(__ScoringStepReveal_Co(attackerIsPlayer, advanceButton));
}
private IEnumerator __ScoringStepReveal_Co(bool attackerIsPlayer, Button advanceButton)
{
    var roots = attackerIsPlayer ? scoringStepRoots_Player : scoringStepRoots_Enemy;

    // すべて非表示で開始（パネル表示直後の“最初から見える”を防ぐ）
    for (int i = 0; i < roots.Count; i++)
    {
        if (roots[i]) roots[i].SetActive(false);
    }

    // ★追加：透明進行ボタンは、使う側だけ有効にする
    __SetScoringStepAdvanceButtonVisible(scoringStepAdvanceButtonPlayer, false);
    __SetScoringStepAdvanceButtonVisible(scoringStepAdvanceButtonEnemy, false);
    __SetScoringStepAdvanceButtonVisible(advanceButton, advanceButton != null);

    // ★追加：1個目の表示まで待つ（クリックでスキップ可）
    _scoringStepAdvanceRequested = false;
    float firstT = 0f;
    while (firstT < Mathf.Max(0f, scoringStepFirstDelay))
    {
        if (_scoringStepAdvanceRequested) break;

        if (!advanceButton)
        {
            if (Input.GetMouseButtonDown(0)) { _scoringStepAdvanceRequested = true; break; }
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                _scoringStepAdvanceRequested = true; break;
            }
        }

        firstT += Time.unscaledDeltaTime;
        yield return null;
    }

    // ステップを累積表示（0→1→2…と増やしていく）
    for (int step = 0; step < roots.Count; step++)
    {
        var go = roots[step];
        if (go) go.SetActive(true);

        // 表示ごとにSE
        if (AudioManager.Instance)
        {
            if (attackerIsPlayer)
            {
                // 1) ゴールドRootだけ別SE（プレイヤーのみ）
                if (scoringStepGoldRoot_Player != null && go == scoringStepGoldRoot_Player)
                {
                    AudioManager.Instance.PlayScoringStepGoldSE();
                }
                else
                {
                    // 2) ゴールド以外は「満貫未満 / 満貫以上役満未満 / 役満以上」で共通SE
                    int han = _lastPlayerWinHan;
                    int fu  = _lastPlayerWinFu;

                    if (han >= 13)
                    {
                        AudioManager.Instance.PlayScoringStepYakumanOrAboveSE();
                    }
                    else
                    {
                        bool manganOrAbove = IsManganOrAbove(han, fu);
                        if (manganOrAbove)
                        {
                            AudioManager.Instance.PlayScoringStepManganToYakumanSE();
                        }
                        else
                        {
                            AudioManager.Instance.PlayScoringStepUnderManganSE();
                        }
                    }
                }
            }
            else
            {
                // 敵側も AudioManager の段階SEを使う（敵パネルで鳴らない問題の修正）
                int han = _lastEnemyWinHan;
                int fu  = _lastEnemyWinFu;

                if (han >= 13)
                {
                    AudioManager.Instance.PlayScoringStepYakumanOrAboveSE();
                }
                else
                {
                    bool manganOrAbove = IsManganOrAbove(han, fu);
                    if (manganOrAbove)
                    {
                        AudioManager.Instance.PlayScoringStepManganToYakumanSE();
                    }
                    else
                    {
                        AudioManager.Instance.PlayScoringStepUnderManganSE();
                    }
                }
            }
        }
        else
        {
            // フォールバック（AudioManager未配置のシーンでも落とさない）
            if (scoringStepSESource && scoringStepSEClip)
            {
                try { scoringStepSESource.PlayOneShot(scoringStepSEClip); } catch { }
            }
        }

        // 次の表示まで待つ（クリックでスキップ可能）
        _scoringStepAdvanceRequested = false;

        float t = 0f;
        while (t < Mathf.Max(0f, scoringStepInterval))
        {
            // 透明ボタン（または入力）で進める
            if (_scoringStepAdvanceRequested) break;

            // 透明ボタン未指定でも、最低限マウス/タッチで進められるようにする
            if (!advanceButton)
            {
                if (Input.GetMouseButtonDown(0)) { _scoringStepAdvanceRequested = true; break; }

                // ★曖昧参照回避：UnityEngine.TouchPhase を明示
                if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
                {
                    _scoringStepAdvanceRequested = true; break;
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // 最後まで出たらOKボタンを有効化
    __SetScoringOkButtonsInteractable(true);

    // ★ミッション達成パネル表示（達成していればOKボタンを一旦無効化して達成パネルを出す）
    try { TryShowMissionCompletePanel(); } catch { }

    // ★追加：段階表示が終わったら透明進行ボタンを必ず閉じる
    __SetScoringStepAdvanceButtonVisible(advanceButton, false);

    _scoringStepRevealCo = null;
}
private void __SetScoringOkButtonsInteractable(bool enable)
{
    __SetScoringOkButtonsInteractable_Internal(scoringPanel, enable);
    __SetScoringOkButtonsInteractable_Internal(scoringPanelPlayer, enable);
    __SetScoringOkButtonsInteractable_Internal(scoringPanelEnemy, enable);
}

private void __SetScoringOkButtonsInteractable_Internal(GameObject root, bool enable)
{
    if (root == null) return;

    try
    {
        var btns = root.GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            if (b == null) continue;

            var nm = b.name ?? string.Empty;
            if (nm.Contains("OK") || nm.Contains("Ok") || nm.Contains("OkButton"))
            {
                b.interactable = enable;
            }
        }
    }
    catch { }
}
// ★追加：敵スキル等で点数（最終ダメージ）に影響した内容の表示（怒り/防御など）
//  - scoringEnemySkillEffectValue       : プレイヤー点数計算パネル側（防御など）
//  - scoringEnemySkillEffectValue_Enemy : 敵点数計算パネル側（怒りなど）
[SerializeField] private TextMeshProUGUI scoringEnemySkillEffectValue;
[SerializeField] private TextMeshProUGUI scoringEnemySkillEffectValue_Enemy;

// ★追加：怒り（敵側）/ 防御（プレイヤー側）のアイコン（効果がある時だけ表示）
[Header("Scoring Panel (Optional Icons)")]
[SerializeField] private UnityEngine.UI.Image scoringAngerIcon_Enemy;    // 敵点数計算パネル：怒りアイコン
[SerializeField] private UnityEngine.UI.Image scoringDefenseIcon_Player; // プレイヤー点数計算パネル：防御アイコン

// ★追加：合計値の表示（数値のみ。ラベルはUI側で配置）
[Header("Scoring Panel (Totals - numeric only)")]
[SerializeField] private TextMeshProUGUI scoringAddedDamageValue;       // プレイヤー：ダメージ加算量
[SerializeField] private TextMeshProUGUI scoringTotalHpRecoverValue;    // プレイヤー：HP回復量
[SerializeField] private TextMeshProUGUI scoringTotalMpRecoverValue;    // プレイヤー：MP回復量
[SerializeField] private TextMeshProUGUI scoringAddedDamageValue_Enemy;    // 敵：ダメージ加算量
[SerializeField] private TextMeshProUGUI scoringTotalHpRecoverValue_Enemy; // 敵：HP回復量
[SerializeField] private TextMeshProUGUI scoringTotalMpRecoverValue_Enemy; // 敵：MP回復量
// ★追加：敵スキル等で点数（最終ダメージ）に影響した内容の表示（怒り/防御など）

// === Trait Yaku Upgrade Delta (per level) bootstrap (RunScene) ===
// UpgradeSceneを経由しなくても、RunScene直行時にこのInspector値でPF_TraitUpgradeDelta_*を確定させる
[Header("Trait Yaku Upgrade Delta (per level) - RunScene Bootstrap")]
[SerializeField] private bool bootstrapWriteTraitUpgradeDeltaPrefsOnAwake = true;
[SerializeField] private float traitUpgradeDeltaGeki_RunScene = 0.10f;
[SerializeField] private float traitUpgradeDeltaShun_RunScene = 0.05f;
[SerializeField] private float traitUpgradeDeltaIyu_RunScene  = 0.02f;

// ==== Scoring panel roots (split panels) ====
// ★Inspectorでプレイヤー用/敵用それぞれのパネル(GameObject)を割り当て
[SerializeField] private GameObject scoringPanelPlayer; 
[SerializeField] private GameObject scoringPanelEnemy;

// ==== Enemy scoring panel (Dora rows) ====
// ★敵の点数計算パネル側の「表ドラ」「裏ドラ」表示行＆ラベル
[SerializeField] private RectTransform scoringDoraOmoteRowEnemy;
[SerializeField] private TMPro.TextMeshProUGUI scoringDoraOmoteLabelEnemy;
[SerializeField] private RectTransform scoringDoraUraRowEnemy;
[SerializeField] private TMPro.TextMeshProUGUI scoringDoraUraLabelEnemy;

// ==== Player Win Overlay (Manual UI) ====
[SerializeField] private bool useManualPlayerWinUI = true;
[SerializeField] private RectTransform playerWinOverlayManualRoot;
[SerializeField] private TextMeshProUGUI playerWinTitleManual;
[SerializeField] private TextMeshProUGUI playerWinBodyManual;
[SerializeField] private Button playerWinOkManual;
[Header("Enemy Hand (Concealed)")]
[SerializeField] private RectTransform enemyHandArea;   // 敵の手牌(13)表示の親（HorizontalLayoutGroup 推奨）
[SerializeField] private bool enemyShowHandFaceForDebug = false; // true=表表示、false=裏表示
[Header("Player Win Damage Animation")]
[SerializeField] private float playerWinDamageAnimSeconds = 1.0f;

[Tooltip("プレイヤーの和了で敵HPが減る演出中に鳴らすSE（必要なら1秒程度の音を用意）")]
[SerializeField] private UnityEngine.AudioClip playerWinDamageSEClip;

[Tooltip("上のSEを鳴らすAudioSource（UI用のSE Source等を割り当て）")]
[SerializeField] private UnityEngine.AudioSource playerWinDamageSESource;
private bool _pendingPlayerWinDamageToEnemy = false;
private int _pendingPlayerWinDamageBase = 0;
private int _pendingPlayerWinDamageFinal = 0;
private bool _pendingPlayerWinHpHeal = false;
private int _pendingPlayerWinHpHealAbs = 0;

// ★追加：プレイヤー和了時のMP回復も「スコアOK後」に反映する（瞬/お札）
private bool _pendingPlayerWinMpHeal = false;
private int _pendingPlayerWinMpHealAbs = 0;

private bool _playerWinDamageAnimating = false;
[Header("Enemy Skill Damage Animation")]
[SerializeField] private float enemySkillDamageAnimSeconds = 1.0f;

[Tooltip("敵スキルでプレイヤーHPが減る演出中に鳴らすSE（必要なら1秒程度の音を用意）")]
[SerializeField] private UnityEngine.AudioClip enemySkillDamageSEClip;

[Tooltip("上のSEを鳴らすAudioSource（UI用のSE Source等を割り当て）")]
[SerializeField] private UnityEngine.AudioSource enemySkillDamageSESource;
[SerializeField] private GameObject scoringSpecialTileEffectsRoot_Player;
[SerializeField] private TextMeshProUGUI scoringSpecialTileEffectsTMP_Player;
[SerializeField] private RectTransform scoringSpecialTileEffectTilesRoot_Player;
[SerializeField] private GameObject scoringSpecialTileEffectsRoot_Enemy;
[SerializeField] private TextMeshProUGUI scoringSpecialTileEffectsTMP_Enemy;
[SerializeField] private RectTransform scoringSpecialTileEffectTilesRoot_Enemy;

// ★追加：特別牌（レジェンダリー等）の「ダメージ増減」表示（例：特別牌：-50％）
[SerializeField] private TextMeshProUGUI scoringSpecialTileDamageEffectValue_Player;
[SerializeField] private TextMeshProUGUI scoringSpecialTileDamageEffectValue_Enemy;

[SerializeField] private GameObject specialTilePopupRoot;
[SerializeField] private TextMeshProUGUI specialTilePopupText;
[SerializeField] private Button specialTilePopupCloseButton;
[SerializeField] private TextMeshProUGUI legendaryOngoingEffectsTMP;

private bool _enemySkillDamageAnimating = false;
[Header("Unique Omamori Drop (per enemy defeated)")]
[SerializeField, Range(0f, 1f)] private float uniqueOmamoriDropChance = 0.05f; // 敵撃破ごとの当選確率（Inspectorで調整）
private bool _lastPlayerWinWasYakumanOrKazoe = false;
private int _lastPlayerWinHan = 0;
private int _lastPlayerWinFu = 0;
private bool _lastEnemyWinWasYakumanOrKazoe = false;
private int _lastEnemyWinHan = 0;
private int _lastEnemyWinFu = 0;
private const string PrefKey_PendingGemRoll = "Gem_PendingRoll";
private const string PrefKey_PendingGemEnemyExcelKey = "Gem_PendingEnemyExcelKey";
private const string PrefKey_PendingGemEnemyName = "Gem_PendingEnemyName";
private const string PrefKey_PendingGemIsZeus = "Gem_PendingIsZeus";

private const string PrefKey_PendingUniqueOmamoriRoll = "UniqueOmamori_PendingRoll";
private const string PrefKey_PendingUniqueOmamoriId   = "UniqueOmamori_PendingId";
private const string PrefKey_PendingUniqueOmamoriKind = "UniqueOmamori_PendingKind";
private const string PrefKey_PendingUniqueEnemyName   = "UniqueOmamori_PendingEnemyName";
private void PreparePendingGemRoll(bool isZeusEnemy)
{
    try
    {
        int runtimeIdx = 0;
        try { runtimeIdx = ProgressionFlowController.GetCurrentEnemyIndex(); } catch { runtimeIdx = 0; }

        int excelKey = -1;
        try { excelKey = EnemyConfigExcel.MapRuntimeIndexToExcelKey(runtimeIdx); } catch { excelKey = -1; }

        string enemyName = "";
        try { enemyName = GetCurrentEnemyNameFromExcelWithLoop(); } catch { enemyName = ""; }

        if (string.IsNullOrEmpty(enemyName))
        {
            try { enemyName = GetCurrentEnemyBaseNameForResources(); } catch { enemyName = ""; }
        }
if (string.IsNullOrEmpty(enemyName))
{
    enemyName = GetGameFixedText_Local("enemy_generic_name");
}
        // ★ここが必須：pending を立てないと Upgrade/Reward 側が処理しない
        PlayerPrefs.SetInt(PrefKey_PendingGemRoll, 1);


        PlayerPrefs.SetInt(PrefKey_PendingGemEnemyExcelKey, excelKey);
        PlayerPrefs.SetString(PrefKey_PendingGemEnemyName, enemyName);
        PlayerPrefs.SetInt(PrefKey_PendingGemIsZeus, isZeusEnemy ? 1 : 0);
        PlayerPrefs.Save();
    }
    catch { }
}
private void RefreshLegendaryOngoingEffectsTextUI()
{
    if (!legendaryOngoingEffectsTMP) return;

    var lines = new List<string>();

    if (IsLegendaryDamageHalfActive())
    {
        lines.Add(GetGameFixedText_Local("legendary_damage_half_ongoing"));
    }
    if (IsLegendaryHalfMpCostActive())
    {
        lines.Add(GetGameFixedText_Local("legendary_half_mp_cost_ongoing"));
    }

    legendaryOngoingEffectsTMP.text = string.Join("\n", lines);
    legendaryOngoingEffectsTMP.gameObject.SetActive(lines.Count > 0);
}
private void PreparePendingUniqueOmamoriRoll_OnEnemyDefeated()
{
    // 既にPendingが残っているなら上書きしない（事故防止）
    try
    {
        if (PlayerPrefs.GetInt(PrefKey_PendingUniqueOmamoriRoll, 0) != 0) return;
    }
    catch { }

    if (uniqueOmamoriDropChance <= 0f) return;

    // 判定には必ず内部名を使う
    string baseEnemyName = "";
    try { baseEnemyName = GetCurrentEnemyBaseNameForResources(); } catch { baseEnemyName = ""; }

    // 表示名は必要なら別で取るが、判定には使わない
    string displayEnemyName = "";
    try { displayEnemyName = GetCurrentEnemyNameFromExcelWithLoop(); } catch { displayEnemyName = ""; }

    string enemyNameForResolve = !string.IsNullOrEmpty(baseEnemyName) ? baseEnemyName : displayEnemyName;
    if (string.IsNullOrEmpty(enemyNameForResolve))
        return;

    try
    {
        string n = enemyNameForResolve.Trim();
        string lower = n.ToLowerInvariant();

        if (n.Contains("ハデス") || lower.Contains("hades"))
        {
            return;
        }
    }
    catch { }

    // 抽選
    float r = UnityEngine.Random.value;
    if (r >= Mathf.Clamp01(uniqueOmamoriDropChance)) return;

    var kind = ResolveUniqueOmamoriKindByEnemyName(enemyNameForResolve);
    if (kind == PlayerData.UniqueOmamoriEffectKind.None)
        return;

    // ここでは付与しない。UpgradeSceneMenu 側で付与して表示する。
    try
    {
        PlayerPrefs.SetInt(PrefKey_PendingUniqueOmamoriRoll, 1);
        PlayerPrefs.SetInt(PrefKey_PendingUniqueOmamoriId, 0);
        PlayerPrefs.SetInt(PrefKey_PendingUniqueOmamoriKind, (int)kind);
        PlayerPrefs.SetString(PrefKey_PendingUniqueEnemyName, enemyNameForResolve);
        PlayerPrefs.Save();
    }
    catch { }
}
[Header("Player Meld Live Update (PlayMode)")]
[SerializeField] private bool liveUpdatePlayerMeldLayoutInPlayMode = true;

private int _playerMeldLayoutLastHash = int.MinValue;

private int CalcPlayerMeldLayoutHash()
{
    unchecked
    {
        int h = 17;

        // 使う/使わない
        h = h * 31 + (useCustomPlayerMeldLayout ? 1 : 0);
        h = h * 31 + (liveUpdatePlayerMeldLayoutInPlayMode ? 1 : 0);

        // Slot参照（null/非nullだけでOK。Transformの位置はUI側で動かす想定）
        if (playerMeldSlots != null)
        {
            h = h * 31 + playerMeldSlots.Length;
            for (int i = 0; i < playerMeldSlots.Length; i++)
                h = h * 31 + (playerMeldSlots[i] != null ? 1 : 0);
        }

// Chi/Pon/Kan(Ankan/Minkan) の設定
h = h * 31 + HashMeldLayoutConfig(meldLayoutChi);
h = h * 31 + HashMeldLayoutConfig(meldLayoutPon);
h = h * 31 + HashMeldLayoutConfig(meldLayoutKan_Ankan);
h = h * 31 + HashMeldLayoutConfig(meldLayoutKan_Minkan);
        return h;
    }
}
private static string BuildSpecialTileLegendaryScoringLine_Local(string kind, int value = 0)
{
    var lm = LocalizationManager.Instance;
    var lang = (lm != null) ? lm.CurrentLanguage : LocalizationManager.Language.Japanese;

    switch (kind)
    {
        case "fx1":
            return $"<color=#FF0000>{GetGameFixedText_Local("special_tile_legendary_effect_1")}</color>";

        case "fx2":
            return $"<color=#FF0000>{GetGameFixedText_Local("special_tile_legendary_effect_2")}</color>";

        case "fx3":
            return $"<color=#FF0000>{GetGameFixedText_Local("special_tile_legendary_effect_3")}</color>";

        case "fx4":
            return $"<color=#FF0000>{GetGameFixedText_Local("special_tile_legendary_effect_4")}</color>";

        case "fx5":
            return $"<color=#FF0000>{GetGameFixedText_Local("special_tile_legendary_effect_5")}</color>";

        case "fx6":
            switch (lang)
            {
                case LocalizationManager.Language.English:
                    return $"<color=#FF0000>On Agari: +16 Fu ×{value}</color>";

                case LocalizationManager.Language.ChineseSimplified:
                    return $"<color=#FF0000>和了时：+16符 ×{value}</color>";

                case LocalizationManager.Language.Japanese:
                default:
                    return $"<color=#FF0000>和了時：符+16 ×{value}</color>";
            }

        case "fx5_triggered":
            switch (lang)
            {
                case LocalizationManager.Language.English:
                    return "<color=#FF0000>The reserved half MP cost for the next hand activated on this Agari</color>";

                case LocalizationManager.Language.ChineseSimplified:
                    return "<color=#FF0000>已预约的下一局MP消耗减半在此次和了时发动</color>";

                case LocalizationManager.Language.Japanese:
                default:
                    return "<color=#FF0000>予約されていた次局のMP消費半分がこの和了で発動</color>";
            }
    }

    return "";
}
private int HashMeldLayoutConfig(MeldLayoutConfig cfg)
{
    unchecked
    {
        if (cfg == null) return 0;
        int h = 23;

        h = h * 31 + cfg.tileSize.GetHashCode();
        h = h * 31 + cfg.tileScale.GetHashCode();

        if (cfg.tilePositions != null)
        {
            h = h * 31 + cfg.tilePositions.Length;
            for (int i = 0; i < cfg.tilePositions.Length; i++)
                h = h * 31 + cfg.tilePositions[i].GetHashCode();
        }

        return h;
    }
}

private PlayerData.UniqueOmamoriEffectKind ResolveUniqueOmamoriKindByEnemyName(string enemyName)
{
    // 1) まずは生文字で判定（日本語名）
    string raw = (enemyName ?? "").Trim();

    if (raw.Contains("アマテラス")) return PlayerData.UniqueOmamoriEffectKind.Amaterasu_HpPlus10000;
    if (raw.Contains("スサノオ"))   return PlayerData.UniqueOmamoriEffectKind.Susanoo_MpPlus10000;
    if (raw.Contains("バステト"))   return PlayerData.UniqueOmamoriEffectKind.Bastet_MpCostHalf;
    if (raw.Contains("シヴァ"))     return PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50;
    if (raw.Contains("アヌビス"))   return PlayerData.UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50;
    if (raw.Contains("フレイヤ"))   return PlayerData.UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2;
    if (raw.Contains("ポセイドン")) return PlayerData.UniqueOmamoriEffectKind.Poseidon_MpRegenDouble;
    if (raw.Contains("オーディン")) return PlayerData.UniqueOmamoriEffectKind.Odin_DisableEnemySkills;
    if (raw.Contains("ルーナ"))     return PlayerData.UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn;
    if (raw.Contains("ゼウス"))     return PlayerData.UniqueOmamoriEffectKind.Zeus_DamageUp30;

    // 2) 表記揺れ対策：+周回・空白・記号除去、英字小文字化
    string n = raw.ToLowerInvariant();

    // "+1" など周回表記を落とす（" +1" / "+1" 両対応）
    int plus = n.IndexOf('+');
    if (plus >= 0) n = n.Substring(0, plus);

    // 空白・全角空白・記号を軽く除去
    n = n.Replace(" ", "").Replace("　", "").Replace("-", "").Replace("_", "");

    // 3) 英字/ローマ字名でも判定
    if (n.Contains("amaterasu")) return PlayerData.UniqueOmamoriEffectKind.Amaterasu_HpPlus10000;
    if (n.Contains("susanoo") || n.Contains("susano")) return PlayerData.UniqueOmamoriEffectKind.Susanoo_MpPlus10000;
    if (n.Contains("bastet")) return PlayerData.UniqueOmamoriEffectKind.Bastet_MpCostHalf;
    if (n.Contains("shiva")) return PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50;
    if (n.Contains("anubis")) return PlayerData.UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50;
    if (n.Contains("freyja") || n.Contains("freya") || n.Contains("freija")) return PlayerData.UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2;
    if (n.Contains("poseidon")) return PlayerData.UniqueOmamoriEffectKind.Poseidon_MpRegenDouble;
    if (n.Contains("odin")) return PlayerData.UniqueOmamoriEffectKind.Odin_DisableEnemySkills;
    if (n.Contains("luna")) return PlayerData.UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn;
    if (n.Contains("zeus")) return PlayerData.UniqueOmamoriEffectKind.Zeus_DamageUp30;

    // 4) さらにカタカナ表記揺れ（例：フレイア/ルナ/シバ等）
    if (raw.Contains("フレイア") || raw.Contains("フレーヤ")) return PlayerData.UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2;
    if (raw.Contains("ルナ")) return PlayerData.UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn;
    if (raw.Contains("シバ")) return PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50;

    return PlayerData.UniqueOmamoriEffectKind.None;
}
private bool _enemyRevealHandNow = false; // 和了/流局のときだけ一時的に敵手牌を表表示

// ★敵ロンで実際に和了された「プレイヤーの捨て牌」だけを局終了までグレー固定にする
private readonly HashSet<int> _enemyRonGreyPlayerDiscardIndices = new HashSet<int>();

private void ApplyOmamoriBaseStatsOnce()
{
    RefreshOmamoriCache();

    if (_omamoriBaseApplied) return;
    _omamoriBaseApplied = true;

    // お守りで最大HP/MPが変わらないなら何もしない
    if (_om.maxHpUp == 0f && _om.maxMpUp == 0f) return;

    // ★追加：基礎最大HPを初回だけ記録（ここが二重適用防止の本体）
    if (_basePlayerMaxHP_ForOmamori < 0)
    {
        _basePlayerMaxHP_ForOmamori = playerMaxHP;
    }

    // ★重要：newPlayerMax は「基礎最大HP」から計算する（playerMaxHP を元にしない）
int newPlayerMax = Mathf.RoundToInt(_basePlayerMaxHP_ForOmamori * (1f + Mathf.Max(0f, _om.maxHpUp)));
try
{
    if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Amaterasu_HpPlus10000))
    {
        newPlayerMax += 10000;
    }
}
catch { }
newPlayerMax = Mathf.Max(1, newPlayerMax);

    // 以降は既存の更新ロジックを維持
    int hpDelta   = newPlayerMax - playerMaxHP;
    playerMaxHP   = newPlayerMax;
    playerHP      = (playerHP < 0) ? playerMaxHP
                   : Mathf.Clamp(playerHP + hpDelta, 0, playerMaxHP);

    UpdateHpUI();

    // 最大MPは EffectiveMaxMP() 側で参照される想定なのでここでは触らない
}

// お守り%上昇の二重適用防止：基礎最大HPを保持
private int _basePlayerMaxHP_ForOmamori = -1;


private static bool _ofudaValidated;

private void ValidateOfudaCatalogOnce()
{
    if (_ofudaValidated) return;
    _ofudaValidated = true;

    var cat = OfudaExcelLoader.Load();
    foreach (var c in cat.conditions)
    {
        if (c == null || string.IsNullOrEmpty(c.key)) continue;

        // いまのOfuda_Cond_Passesが拾える代表語が含まれないものは警告
        if (!(c.key.Contains("満貫以上") || c.key.Contains("跳満以上") || c.key.Contains("倍満以上") ||
              c.key.Contains("三倍満以上") || c.key.Contains("役満以上") ||
              c.key.Contains("3ターン以内") || c.key.Contains("カン") || c.key.Contains("ポン") || c.key.Contains("チー") ||
              c.key.Contains("ホンイツ") || c.key.Contains("清一色") || c.key.Contains("対々和") || c.key.Contains("七対子") || c.key.Contains("平和") ||
              c.key.Contains("HP") || c.key.Contains("MP")))
        {
            Debug.LogWarning($"[Ofuda] 未対応の条件キーかも: {c.key} / label={c.label}");
        }
    }

    foreach (var e in cat.effects)
    {
        if (e == null || string.IsNullOrEmpty(e.key)) continue;
        if (!(e.key.StartsWith("EFFECT:点数が") || e.key.StartsWith("EFFECT:HPを") || e.key.StartsWith("EFFECT:MPを")))
        {
            Debug.LogWarning($"[Ofuda] 未対応の効果キーかも: {e.key} / label={e.label}");
        }
    }
}
private void ApplyDamageToPlayer(int baseDamage, string reason = "")
{
    RefreshOmamoriCache();

    int dmg = Mathf.Max(0, baseDamage);

    // 被ダメ軽減（例: 0.20 = 20%軽減）
    dmg = Mathf.RoundToInt(dmg * (1f - Mathf.Clamp01(_om.dmgTakenDown)));
    if (dmg < 0) dmg = 0;

    // ★ユニーク：シヴァ（東1局のプレイヤーへのダメージ50%減少）
    try
    {
        if (roundNumber == 1 && PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50))
        {
            dmg = Mathf.RoundToInt(dmg * 0.5f);
        }
    }
    catch { }

playerHP = Mathf.Max(0, playerHP - dmg);

try
{
    if (dmg > 0 && AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayBattleDamageSE();
    }
}
catch { }

UpdateHpUI(); // UI即時反映
}

    // === メニュー→対局の装備お守り受け渡し（static） ===
[System.Serializable]
public struct OmamoriEntry
{
    public string name;
    public string desc;
}

private static readonly System.Collections.Generic.List<OmamoriEntry> s_equippedOmamoriForNextRun
    = new System.Collections.Generic.List<OmamoriEntry>();

/// <summary>次の対局用に、装備したお守り（表示用）をセットする（メニュー側から呼ぶ）</summary>
public static void SetEquippedOmamoriForNextRun(
    System.Collections.Generic.IEnumerable<OmamoriEntry> entries)
{
    s_equippedOmamoriForNextRun.Clear();
    if (entries == null) return;
    s_equippedOmamoriForNextRun.AddRange(entries);
}

/// <summary>現在の対局で表示するお守り（メニューから渡された内容）</summary>
public static System.Collections.Generic.IReadOnlyList<OmamoriEntry> GetEquippedOmamoriForNextRun()
    => s_equippedOmamoriForNextRun;

public class PlayerLoadout : MonoBehaviour
{
    // ★この名前にしておくと拾われます
    public List<CharmData> equippedOmamori = new();
}

public class CharmData
{
    // ★このプロパティ名で拾われます
    public string Name;
    public string Description;
}

  // --- 次回ラン開始時にHP全回復する仕組み ---
private const string KeyPendingFullHeal = "PF_PendingFullHeal";

// 任意の場所から「次回開始で全回復」フラグを立てる
public static void MarkFullHealNextRun()
{
    try { PlayerPrefs.SetInt(KeyPendingFullHeal, 1); PlayerPrefs.Save(); } catch {}
}

// GameManagerが存在すれば今すぐ全回復。存在しなければ次回起動時に全回復。
public static void FullHealNowOrNextRun()
{
    try
    {
        var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
if (gm != null)
{
    gm.playerHP = gm.playerMaxHP;

    // MP も最大値まで回復（MP未導入でも安全）
    try
    {
        var tp = gm.GetType();
        var miEff = tp.GetMethod("EffectiveMaxMP",
                   System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        var fMp   = tp.GetField("_mp",
                   System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (miEff != null && fMp != null && fMp.FieldType == typeof(int))
        {
            int max = (int)miEff.Invoke(gm, null);
            fMp.SetValue(gm, max);
        }
    }
    catch {}
    gm.UpdateHpUI();            // プロジェクトの関数名に合わせてください
    gm.RefreshTopUI();          // 無ければ省略可
try { PlayerPrefs.DeleteKey("Run_PlayerHP"); PlayerPrefs.DeleteKey("Run_PlayerMP"); PlayerPrefs.Save(); } catch {}

    PlayerPrefs.SetInt(KeyPendingFullHeal, 0);
    PlayerPrefs.Save();
}

        else
        {
            PlayerPrefs.SetInt(KeyPendingFullHeal, 1);
    try { PlayerPrefs.DeleteKey("Run_PlayerHP"); PlayerPrefs.Save(); } catch {}
        }
    }
    catch {}
}
// Awake/Start で呼ばれるが、ここでは"満タン化"や"フラグ消費"はしない。
// （最大値の確定：お守り/Run_HPBonus/Run_MPBonus 反映の後に、__ApplyRunBonusesAndRefreshUI() で一括処理する）
private void ApplyPendingFullHealIfAny()
{
    try
    {
        if (PlayerPrefs.GetInt(KeyPendingFullHeal, 0) == 1)
        {
            // ★ここでは最大値がまだ確定していない可能性があるため、
            //   HP/MP の値を触らず、持ち越しキーの掃除だけ行う。
            try
            {
                PlayerPrefs.DeleteKey("Run_PlayerHP");
                PlayerPrefs.DeleteKey("Run_PlayerMP");
                PlayerPrefs.Save();
            }
            catch {}
        }
    }
    catch {}
}



    // === Legacy effects toggles (keep serialized fields but no-op the logic) ===
private const bool DISABLE_LEGACY_PLAYER_COLOR_EFFECTS   = true;  // (A) 和了色効果を無効
private const bool DISABLE_LEGACY_ENEMY_DISCARD_EFFECTS  = true;  // (B) 敵捨て牌効果を無効

    private static GameManager _inst; // シングルトン用の参照（Awakeのガードで使用）
    // Returns the Image currently used to render the tile face.
    // Prefers RaiseOverlay's Art/Image when present & visible; otherwise falls back to base Art/Image.
    private Image GetVisibleArtImage(Transform tile)
    {
        if (!tile) return null;

        // 1) Prefer a RaiseOverlay if it exists and its art is enabled/visible
        var overlay = tile.Find("RaiseOverlay");
        if (overlay)
        {
            var ovArt = overlay.Find("Art");
            if (ovArt)
            {
                var ovImgTf = ovArt.Find("Image");
                var ovImg = ovImgTf ? ovImgTf.GetComponent<Image>() : null;
                if (ovImg && ovImg.enabled && ovImg.color.a > 0f)
                    return ovImg;
            }
            // Fallback: any Image under overlay that looks visible
            foreach (var im in overlay.GetComponentsInChildren<Image>(true))
                if (im.enabled && im.color.a > 0f) return im;
        }

        // 2) Base tile art
        var baseImg = FindArtImage(tile);
        if (baseImg && baseImg.enabled && baseImg.color.a > 0f)
            return baseImg;

        // 3) Last resort: any visible Image under this tile (excluding root)
        foreach (var im in tile.GetComponentsInChildren<Image>(true))
            if (im.transform != tile && im.enabled && im.color.a > 0f) return im;

        return baseImg; // may be null
    }
// ===== Top UI =====
    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI roundTMP;
    [SerializeField] private TextMeshProUGUI turnTMP;      // ★追加：ターン数「●ターン目」
    // プレイヤーの自風表示用（東家/南家/西家/北家 を表示するラベル）
    [SerializeField] private TextMeshProUGUI playerSeatTMP;
    // 敵の自風表示用（プレイヤーの下家）
    [SerializeField] private TextMeshProUGUI enemySeatTMP;
    [SerializeField] private TextMeshProUGUI targetTMP;
    [SerializeField] private TextMeshProUGUI scoreTMP;
    [SerializeField] private Image doraImage;
    [SerializeField] private RectTransform wanpaiArea;
    [SerializeField] private TextMeshProUGUI statusTMP;
    [Header("Shanten UI")]
    [SerializeField] private TextMeshProUGUI shantenTMP;
    [SerializeField] private Color shantenNormalColor = Color.white;
    [SerializeField] private Color shantenTenpaiColor = Color.yellow;

    [Header("Shanten UI (Riichi / Blink)")]
    [SerializeField] private Color shantenRiichiColor = Color.cyan;

    [Tooltip("テンパイ時のフェードアウト秒（1秒くらい推奨）")]
    [SerializeField] private float shantenBlinkFadeOutSeconds = 1.0f;

    [Tooltip("テンパイ時のフェードイン秒（1秒くらい推奨）")]
    [SerializeField] private float shantenBlinkFadeInSeconds = 1.0f;

    [Tooltip("明滅時の最小アルファ（0だと完全消えます）")]
    [SerializeField, Range(0f, 1f)] private float shantenBlinkMinAlpha = 0.15f;

    [Header("Enemy Riichi UI")]
    [SerializeField] private TextMeshProUGUI enemyRiichiStatusTMP;

    // TMPの背景として使うImage（またはその親）のGameObjectを割り当てる
    [SerializeField] private GameObject enemyRiichiStatusBGObject;

    [SerializeField] private Color enemyRiichiStatusColor = Color.cyan;

    [Header("Riichi Discard Highlight")]
    [SerializeField] private Color riichiDiscardHighlightColor = new Color(1f, 0.55f, 0f, 0.95f);

[Header("Board Parents")]
[SerializeField] private RectTransform handArea;

[SerializeField] private RectTransform offerArea;
[SerializeField] private RectTransform discardArea;
[SerializeField] private RectTransform enemyDiscardArea;
[SerializeField] private RectTransform meldArea;

[Header("Player Hand Area Position")]
[SerializeField] private Vector2 playerHandAreaPositionWithoutOpenMeld = Vector2.zero;
[SerializeField] private Vector2 playerHandAreaPositionWithOpenMeld = Vector2.zero;
// ===============================
//  Player Meld UI (Inspector-customizable)
// ===============================
[Header("Player Meld UI (Inspector-customizable)")]
[SerializeField] private bool useCustomPlayerMeldLayout = false;

[Tooltip("副露メンツ表示先スロット（最大4つ）。Slot0が一番左（または上）など、あなたのUI配置に合わせて使ってください。")]
[SerializeField] private RectTransform[] playerMeldSlots = new RectTransform[4];
private enum MeldLayoutKind
{
    Chi,
    Pon,
    Kan_Ankan,
    Kan_Minkan
}

[Serializable]
private class MeldLayoutConfig
{
    [Tooltip("この種類（チー/ポン/カン）の牌1枚のRectTransform.sizeDelta")]
    public Vector2 tileSize = new Vector2(90f, 120f);

    [Tooltip("この種類（チー/ポン/カン）の牌スケール（1.0=等倍）")]
    public float tileScale = 1.0f;

    [Tooltip("左からn番目の牌のアンカー位置（anchoredPosition）。要素0=左端、1=2枚目、2=3枚目、3=4枚目（カン用）")]
    public Vector2[] tilePositions = new Vector2[4]
    {
        new Vector2(0f,   0f),
        new Vector2(92f,  0f),
        new Vector2(184f, 0f),
        new Vector2(276f, 0f),
    };
}
[Header("Player Meld Layout Configs (per kind)")]
[SerializeField] private MeldLayoutConfig meldLayoutChi = new MeldLayoutConfig();
[SerializeField] private MeldLayoutConfig meldLayoutPon = new MeldLayoutConfig();
[SerializeField] private MeldLayoutConfig meldLayoutKan_Ankan = new MeldLayoutConfig();
[SerializeField] private MeldLayoutConfig meldLayoutKan_Minkan = new MeldLayoutConfig();

private bool IsCustomMeldLayoutReady()
{
    if (!useCustomPlayerMeldLayout) return false;
    if (playerMeldSlots == null || playerMeldSlots.Length == 0) return false;

    // Slotが1つも割り当てられていないなら無効扱い（事故防止）
    bool any = false;
    for (int i = 0; i < playerMeldSlots.Length; i++)
    {
        if (playerMeldSlots[i] != null) { any = true; break; }
    }
    return any;
}
private MeldLayoutConfig GetLayoutConfig(MeldLayoutKind kind)
{
    if (kind == MeldLayoutKind.Pon) return meldLayoutPon;
    if (kind == MeldLayoutKind.Kan_Ankan) return meldLayoutKan_Ankan;
    if (kind == MeldLayoutKind.Kan_Minkan) return meldLayoutKan_Minkan;
    return meldLayoutChi;
}

private void ClearChildren(Transform t)
{
    if (!t) return;
    for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
}

// ===== Match Start / Ryukyoku Cutin =====
    [Header("Match Start Cutin")]
    [SerializeField] private CanvasGroup matchStartCutinGroup;           // 「対局開始」／「流局」カットイン用のルート（CanvasGroup付き）
    [SerializeField] private TMPro.TextMeshProUGUI matchStartLabelTMP;   // ★追加：「東1局」「流局」などのテキスト
    [SerializeField] private float matchStartFadeInDuration = 0.4f;
    [SerializeField] private float matchStartHoldDuration   = 0.6f;
    [SerializeField] private float matchStartFadeOutDuration = 0.4f;
    [SerializeField] private AudioSource matchStartSESource;             // 開始SE用 AudioSource（Clip を Inspector で設定）
    [SerializeField] private AudioSource ryukyokuSESource;               // ★追加：「流局」用 SE（Inspector で指定）
    [Header("Enemy Skill Cutin")]
    [SerializeField] private CanvasGroup enemySkillCutinGroup;
    [SerializeField] private TMPro.TextMeshProUGUI enemySkillCutinLabelTMP;

[Header("Main Buttons")]
    [SerializeField] private Button btnConfirm;          // 捨てる
    [SerializeField] private TMPro.TextMeshProUGUI confirmTMP;
    [SerializeField] private Button btnSkill;
    [SerializeField] private TextMeshProUGUI skillTMP;
    [SerializeField] private Button btnSkip;             // スキップ
    [SerializeField] private TextMeshProUGUI skipTMP;
    [Header("Tenpai / Riichi")]
    [SerializeField] private Button btnTenpaiConfirm;    // 入替え確定
    [SerializeField] private Button btnRiichi;           // リーチ
    [SerializeField] private TextMeshProUGUI tenpaiBadgeTMP;

    [Header("Tenpai Waits UI (Player)")]
    [Tooltip("聴牌時に待ち牌表示をまとめてON/OFFするルート。未設定なら表示更新をしない。")]
    [SerializeField] private GameObject playerTenpaiWaitsRoot;

    [Tooltip("待ち牌を置くスロット（RectTransform）を左から順に指定。各スロットの位置/サイズはInspectorで自由に調整。")]
    [SerializeField] private List<RectTransform> playerTenpaiWaitSlots = new List<RectTransform>();

    [Tooltip("スロット未設定のとき、playerTenpaiWaitsRoot直下に tilePrefab を並べて生成して表示する（レイアウトはRoot側で調整）")]
    [SerializeField] private bool allowAutoLayoutWhenNoSlots = true;

    [Header("Tenpai Waits Tile Size (Player)")]


    // 待ち牌の再描画を最小化するためのキャッシュ
    private readonly HashSet<string> _tmpPlayerTenpaiWaits = new HashSet<string>();
    private string _lastPlayerTenpaiWaitsKey = "";

    [Tooltip("待ち牌表示で生成する牌のRectTransformサイズを上書きする。falseならtilePrefab/スロット側の見た目設定をそのまま使う。")]
    [SerializeField] private bool overridePlayerTenpaiWaitTileSize = false;

    [Tooltip("上書きする牌サイズ（px）。overridePlayerTenpaiWaitTileSize が true のときだけ有効。")]
    [SerializeField] private Vector2 playerTenpaiWaitTileSize = new Vector2(72f, 96f);

    [Header("Call / Win Buttons (Manual UI)")]
    [SerializeField] private Button btnPon;
    [SerializeField] private Button btnChi;
    [SerializeField] private Button btnKan;              // 鳴きカン（大明槓）
    [SerializeField] private Button btnKanFromHand;      // 手牌カン（暗槓/加槓）
    [SerializeField] private Button btnRon;
    [SerializeField] private Button btnTsumo;
    [SerializeField] private Button btnRonSkip;


    // ===== Scoring =====
// ===== Scoring =====
 [Header("Scoring Panel")]
    [SerializeField] private GameObject scoringPanel;
    [SerializeField] private TextMeshProUGUI scoringTMP;
    [SerializeField] private RectTransform scoringHandParent;

// --- Dora display (scoring panel, top-right) & ura-dora state ---
    [SerializeField] private RectTransform scoringDoraRoot; // 右上に並べる親（未割当なら自動生成）
    private readonly List<string> uraIndicators = new List<string>(); // リーチ後にめくる裏ドラ表示牌
    private readonly List<string> _uraIndicatorPool = new List<string>(); // ★追加：この局の裏ドラ表示牌（保留分）
    private bool _includeUraForScoring = false; // 和了時の最終得点に裏ドラを加算するかのフラグ
    // scoring OK button (auto-discover)
    private Button scoringOKButton;

// ===== Prefabs =====
    [Header("Prefabs")]
    [SerializeField] private GameObject tilePrefab;       // Tile prefab (with Button), child "Art/Image"
    [SerializeField] private GameObject callButtonPrefab;
// === NEW: Scoring Layout ===
    [HideInInspector] [SerializeField] private float scoringTileGapScale = 0.14f;
    [HideInInspector] [SerializeField] private float scoringGroupGapScale = 0.50f;
    [HideInInspector] [SerializeField] private float scoringMeldInnerGapScale = 0.24f;

// --- 追加: アンカンの裏面スプライト名（Resources/Sprites/Tiles/ 内） ---
[Header("Tiles Visual")]
[SerializeField] private string backTileSpriteName = "Back"; // 無ければ自動フォールバック

[Header("Scoring Panel Layout")]
[Tooltip("手牌と和了牌の間隔（牌幅に対する倍率）。0.1=牌幅の10%")]
[SerializeField] private float scoringWinTileGapInTiles = 0.10f;

[Tooltip("副露ブロック同士の間隔（牌幅に対する倍率）。0.05=牌幅の5%")]
[SerializeField] private float scoringMeldGapInTiles = 0.05f;

[Tooltip("プレイヤー点数計算パネルで、横向きにした拾い牌の下辺合わせ後に追加するY補正(px)")]
[SerializeField] private float scoringOpenCalledTileYOffset = 0f;

[HideInInspector] [SerializeField] private float scoringGroupGapMul = 0.40f;
[HideInInspector] [SerializeField] private float scoringMeldGapMul  = 0.20f;

private int GetScoringTileWidthSafe(RectTransform rtContainer)
{
    const int fallbackWidth = 54;

    try
    {
        if (tilePrefab != null)
        {
            var rootRt = tilePrefab.GetComponent<RectTransform>();
            if (rootRt != null && rootRt.rect.width >= 16f)
                return Mathf.RoundToInt(rootRt.rect.width);

            var artRt = tilePrefab.transform.Find("Art") as RectTransform;
            if (artRt != null && artRt.rect.width >= 16f)
                return Mathf.RoundToInt(artRt.rect.width);

            var artImageRt = tilePrefab.transform.Find("Art/Image") as RectTransform;
            if (artImageRt != null && artImageRt.rect.width >= 16f)
                return Mathf.RoundToInt(artImageRt.rect.width);
        }
    }
    catch { }

    try
    {
        if (rtContainer != null)
        {
            for (int i = 0; i < rtContainer.childCount; i++)
            {
                var childRt = rtContainer.GetChild(i) as RectTransform;
                if (childRt != null && childRt.rect.width >= 16f)
                    return Mathf.RoundToInt(childRt.rect.width);

                var artRt = rtContainer.GetChild(i).Find("Art") as RectTransform;
                if (artRt != null && artRt.rect.width >= 16f)
                    return Mathf.RoundToInt(artRt.rect.width);
            }
        }
    }
    catch { }

    return fallbackWidth;
}

private bool IsAnyScoringPanelActive()
{
    bool a = scoringPanel != null && scoringPanel.activeInHierarchy;
    bool b = scoringPanelPlayer != null && scoringPanelPlayer.activeInHierarchy;
    bool c = scoringPanelEnemy != null && scoringPanelEnemy.activeInHierarchy;
    return a || b || c;
}
private void ForceRebuildScoringLayouts()
{
    Canvas.ForceUpdateCanvases();

    var playerRt = scoringPlayerTilesManual as RectTransform;
    if (playerRt != null)
    {
        LayoutRebuilder.MarkLayoutForRebuild(playerRt);
        LayoutRebuilder.ForceRebuildLayoutImmediate(playerRt);
    }

    var enemyRt = scoringEnemyTilesManual as RectTransform;
    if (enemyRt != null)
    {
        LayoutRebuilder.MarkLayoutForRebuild(enemyRt);
        LayoutRebuilder.ForceRebuildLayoutImmediate(enemyRt);
    }

    if (scoringPanelPlayer != null)
    {
        var rt = scoringPanelPlayer.transform as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(rt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    if (scoringPanelEnemy != null)
    {
        var rt = scoringPanelEnemy.transform as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(rt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    if (scoringPanel != null)
    {
        var rt = scoringPanel.transform as RectTransform;
        if (rt != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(rt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    Canvas.ForceUpdateCanvases();
}

private void RebuildPlayerScoringTilesManualPreview()
{
    var rtContainer = scoringPlayerTilesManual as RectTransform;
    if (rtContainer == null) return;

    for (int i = rtContainer.childCount - 1; i >= 0; i--)
        Destroy(rtContainer.GetChild(i).gameObject);

    float manualX = 0f;
    int targetWidth = GetScoringTileWidthSafe(rtContainer);

    float groupGap = targetWidth * Mathf.Max(0f, scoringWinTileGapInTiles);
    float meldGap  = targetWidth * Mathf.Max(0f, scoringMeldGapInTiles);

    var scoringHLG = rtContainer.GetComponent<HorizontalLayoutGroup>();
    if (scoringHLG != null && scoringHLG.enabled)
    {
        scoringHLG.spacing = 0f;
        scoringHLG.childControlWidth = false;
        scoringHLG.childControlHeight = false;
        scoringHLG.childForceExpandWidth = false;
        scoringHLG.childForceExpandHeight = false;
    }

    bool usesLayout =
        (rtContainer.GetComponent<HorizontalLayoutGroup>() != null && rtContainer.GetComponent<HorizontalLayoutGroup>().enabled) ||
        (rtContainer.GetComponent<VerticalLayoutGroup>() != null && rtContainer.GetComponent<VerticalLayoutGroup>().enabled) ||
        (rtContainer.GetComponent<GridLayoutGroup>() != null && rtContainer.GetComponent<GridLayoutGroup>().enabled);

void AddScoringGap(float px)
{
    if (px <= 0f) return;

    if (usesLayout)
    {
        var spacer = new GameObject(
            "ScoringGap",
            typeof(RectTransform),
            typeof(LayoutElement)
        );
        spacer.transform.SetParent(rtContainer, false);

        var spacerRt = spacer.GetComponent<RectTransform>();
        if (spacerRt != null)
        {
            spacerRt.anchorMin = new Vector2(0f, 0.5f);
            spacerRt.anchorMax = new Vector2(0f, 0.5f);
            spacerRt.pivot = new Vector2(0f, 0.5f);
            spacerRt.sizeDelta = new Vector2(px, 1f);
            spacerRt.anchoredPosition = Vector2.zero;
        }

        var le = spacer.GetComponent<LayoutElement>();
        le.minWidth = px;
        le.preferredWidth = px;
        le.flexibleWidth = 0f;
        le.minHeight = 1f;
        le.preferredHeight = 1f;
        le.flexibleHeight = 0f;
    }
    else
    {
        manualX += px;
    }
}

    var concealedSource =
        (_playerWonHandSnapshot != null && _playerWonHandSnapshot.Count > 0)
        ? _playerWonHandSnapshot
        : hand;

    var concealed = new List<string>(concealedSource);
    concealed.Sort((a, b) => ToSortKey(a).CompareTo(ToSortKey(b)));

    foreach (var id in concealed)
    {
        CreateTileImage(rtContainer, id, ref manualX, targetWidth);
    }

    IList<IList<string>> open;
    if (_playerWonMeldsSnapshot != null && _playerWonMeldsSnapshot.Count > 0)
    {
        open = _playerWonMeldsSnapshot.Cast<IList<string>>().ToList();
    }
    else
    {
        open = GetOpenMeldsNormalized() ?? new List<IList<string>>();
    }

    __ApplyAppliedSpecialTileUiCacheToScoringPanel(true);

    if (scoringSpecialTileEffectsRoot_Enemy) scoringSpecialTileEffectsRoot_Enemy.SetActive(false);
    __SetTMP(scoringSpecialTileEffectsTMP_Enemy, "");

    if (scoringSpecialTileEffectTilesRoot_Enemy != null)
    {
        for (int i = scoringSpecialTileEffectTilesRoot_Enemy.childCount - 1; i >= 0; i--)
            Destroy(scoringSpecialTileEffectTilesRoot_Enemy.GetChild(i).gameObject);
    }
    if (open.Count > 0) AddScoringGap(groupGap);

    foreach (var meld in open)
    {
        var one = new List<string>(meld);

        bool isKan   = (one.Count == 4);
        bool hasStar = one.Exists(s => !string.IsNullOrEmpty(s) && s.EndsWith("*"));
        bool isAnkan = isKan && !hasStar;

        var display = new List<string>(one);
        bool anyStar = display.Any(x => !string.IsNullOrEmpty(x) && x.EndsWith("*"));

        bool IsConsecutiveChi(List<string> m)
        {
            if (m == null || m.Count != 3) return false;

            int[] ns = new int[3];
            int suit = -1;

            for (int t = 0; t < 3; t++)
            {
                string baseId = (!string.IsNullOrEmpty(m[t]) && m[t].EndsWith("*"))
                    ? m[t].Substring(0, m[t].Length - 1)
                    : m[t];

                if (!TryParseSuitNum(baseId, out suit, out ns[t])) return false;
            }

            Array.Sort(ns);
            return (ns[1] == ns[0] + 1) && (ns[2] == ns[1] + 1);
        }

        if (anyStar && IsConsecutiveChi(display))
        {
            int ri = display.FindIndex(x => x.EndsWith("*"));
            if (ri > 0)
            {
                var picked = display[ri];
                display.RemoveAt(ri);
                display.Insert(0, picked);
            }
        }

        for (int jj = 0; jj < display.Count; jj++)
        {
            string id = display[jj];
            bool starred = !string.IsNullOrEmpty(id) && id.EndsWith("*");
            string baseId = starred ? id.Substring(0, id.Length - 1) : id;

            CreateTileImage(rtContainer, baseId, ref manualX, targetWidth);

            if (rtContainer.childCount > 0)
            {
                var go = rtContainer.GetChild(rtContainer.childCount - 1).gameObject;

                if (isAnkan && (jj == 0 || jj == display.Count - 1))
                    TrySetBackSprite(go);

                if (starred)
                {
                    var art = go.transform.Find("Art") as RectTransform;
                    if (art != null)
                    {
                        float w = art.rect.width;
                        float h = art.rect.height;
                        float bottomAlignY = (w - h) * 0.5f;

                        art.localEulerAngles = new Vector3(0f, 0f, 90f);
                        art.localPosition = new Vector3(
                            art.localPosition.x,
                            bottomAlignY + scoringOpenCalledTileYOffset,
                            art.localPosition.z
                        );
                    }
                    else
                    {
                        var rootRt = go.transform as RectTransform;
                        if (rootRt != null)
                        {
                            float w = rootRt.rect.width;
                            float h = rootRt.rect.height;
                            float bottomAlignY = (w - h) * 0.5f;

                            go.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                            rootRt.anchoredPosition = new Vector2(
                                rootRt.anchoredPosition.x,
                                bottomAlignY + scoringOpenCalledTileYOffset
                            );
                        }
                        else
                        {
                            go.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                        }
                    }
                }
            }
        }

        AddScoringGap(meldGap);
    }

    if (!string.IsNullOrEmpty(_scoringUsedTileLabel))
    {
        AddScoringGap(groupGap);
        CreateTileImage(rtContainer, _scoringUsedTileLabel, ref manualX, targetWidth);
    }
}

private void RebuildActiveScoringTilesManualPreview()
{
    if (!Application.isPlaying) return;
    if (!IsAnyScoringPanelActive()) return;

    if (_currentScoringAttackerIsPlayer)
    {
        RebuildPlayerScoringTilesManualPreview();
    }
    else
    {
        EnemyAddon_PopulateEnemyScoringTilesManual();
    }

    ForceRebuildScoringLayouts();
}

#if UNITY_EDITOR
private void OnValidate()
{
    scoringWinTileGapInTiles = Mathf.Max(0f, scoringWinTileGapInTiles);
    scoringMeldGapInTiles = Mathf.Max(0f, scoringMeldGapInTiles);

    if (!Application.isPlaying) return;

    RebuildActiveScoringTilesManualPreview();
}
#endif

// --- 追加: 鳴き選択ボタンの一時ルート ---
private RectTransform _callChoiceRoot;  // null なら未生成
    [Header("Options")]
    [SerializeField] private float raisePixels = 24f;
[Header("Optional MP UI (manual)")]
[SerializeField] private TMPro.TextMeshProUGUI playerMPTMP; // 任意（設定しなければ何もしない）
[SerializeField] private UnityEngine.UI.Image  playerMPBar; // 任意（設定しなければ何もしない）
// === 追記: バー見た目/テキスト設定 ===
[Serializable]
private class UiBarConfig
{
    public Image.Type fillType = Image.Type.Filled;
    public Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
    [Range(0,3)] public int fillOrigin = 0;
    public bool overrideColor = true;
    public Color color = new Color(0.85f, 0.1f, 0.1f, 1f); // 既定は赤系
    [Tooltip("書式: {cur} と {max} を置換")] public string textFormat = "{cur}/{max}";
}

[Header("HP/MP UI Config")]
[SerializeField] private UiBarConfig playerHPConfig = new UiBarConfig();
[SerializeField] private UiBarConfig enemyHPConfig  = new UiBarConfig();
[SerializeField] private UiBarConfig playerMPConfig = new UiBarConfig();

[Header("Enemy Skill Status UI (optional)")]
[SerializeField] private Color enemySkillPoisonHpColor = new Color(0.6f, 0.2f, 0.8f, 1f);
[SerializeField] private Color enemySkillParalysisMpColor = new Color(1f, 0.9f, 0.2f, 1f);
[SerializeField] private GameObject enemySkillPoisonIcon;
[SerializeField] private GameObject enemySkillParalysisIcon;
[SerializeField] private GameObject enemySkillAngerIcon;
[SerializeField] private GameObject enemySkillDefenseIcon;

// EnemySkills_Addon 側で使用：通常色を復元するためのキャッシュ
private bool  _enemySkillStatusUiColorsCached = false;
private Color _enemySkillNormalHpBarColor = Color.white;
private Color _enemySkillNormalMpBarColor = Color.white;
// MP UI 表示が敵ターンで 0/0 になるデグレ対策：最後に取得できた値を保持して表示を固定する
private bool _mpUiCacheValid = false;
private int  _mpUiCachedCur = 0;
private int  _mpUiCachedMax = 0;

// === 追記: レイヤー制御（任意） ===
[Header("HP/MP UI Layering (optional)")]
[SerializeField] private Canvas hpMpCanvas;              // HP/MP を載せている Canvas（任意）
[SerializeField] private bool   overrideSorting = false; // true で sorting を上書き
[SerializeField] private string sortingLayerName = "Default";
[SerializeField] private int    sortingOrder = 0;

[Tooltip("SiblingIndex を直接指定（-1 で無効）")]
[SerializeField] private Transform playerHPRoot; // HP テキスト/バーを束ねている親（なければ Bar 自体）
[SerializeField] private Transform enemyHPRoot;  // 同上
[SerializeField] private Transform playerMPRoot; // 同上
[SerializeField] private int playerHPSibling = -1;
[SerializeField] private int enemyHPSibling  = -1;
[SerializeField] private int playerMPSibling = -1;


    // ===== Skills (Active) =====
    [Header("Skills (Active)")]
    [SerializeField] private bool useDebugSkillInGame = true; // true: Inspector選択を使用 / false: メニュー装備（PlayerPrefs）を使用
    [SerializeField] private ActiveSkill debugActiveSkill = ActiveSkill.None;
    [SerializeField] private int activeSkillCharges = 1; // ステージ中の使用回数

// 画面右の説明テキスト（いまスキル説明を出しているTMPを割り当て）
[SerializeField] private TMPro.TextMeshProUGUI rightInfoTMP;
// ==== Debug (Hand Editor) ====
[Header("Debug / Hand Editor")]
[SerializeField] private bool debugFeatureEnabled = true;         // falseならF1切替も手牌編集も無効
[SerializeField] private bool enableDebugMode = false;            // 現在のデバッグモードON/OFF
[SerializeField] private bool allowDebugAnyPhase = true;          // ← どのフェーズでも許可
[SerializeField] private KeyCode debugToggleKey = KeyCode.F1;     // ← 旧Input用の実行中トグル
// ===== Win Cut-in UI =====
[Header("Win Cut-in")]
[SerializeField] private GameObject       winCutinRoot;     // 画面中央のカットイン全体（CanvasGroup付き）
[SerializeField] private CanvasGroup      winCutinGroup;    // フェード用
[SerializeField] private TextMeshProUGUI  winCutinTMP;      // 「ツモ」「ロン」表示
[SerializeField] private Image            winCutinPortrait; // プレイヤー/敵の顔画像
[SerializeField] private Sprite           playerCutinSprite;
// ★勝利/敗北カットイン用スプライト（手動UIでInspectorから紐づけ）
[SerializeField] private Sprite           playerVictoryCutinSpriteManual;
[SerializeField] private Sprite           defeatCutinSpriteManual;
[SerializeField] private Sprite           enemyCutinSprite;

[Header("SE (Cutin)")]
[SerializeField] private AudioSource cutinSESource;                 // カットインSEを鳴らすAudioSource（共通）
[SerializeField] private AudioClip playerSkillCutinSEClip;          // プレイヤー：スキルカットイン
[SerializeField] private AudioClip playerRonCutinSEClip;            // プレイヤー：ロンカットイン
[SerializeField] private AudioClip playerTsumoCutinSEClip;          // プレイヤー：ツモカットイン
[SerializeField] private AudioClip enemySkillCutinSEClip;           // 敵：スキルカットイン
[SerializeField] private AudioClip enemyRonCutinSEClip;             // 敵：ロンカットイン
[SerializeField] private AudioClip enemyTsumoCutinSEClip;           // 敵：ツモカットイン
private RectTransform _dbgPanelRoot;                              // ← 簡易パネルのルート
private int _dbgEditingHandIndex = -1;                            // ← どの手牌スロットを編集中か

    private int _activeSkillChargesLeft = -1;
private bool _lastSkillApplied = false;
    // 「次ターンでツモる」予約タイル（BeginOfferPhaseで先頭に置く）
    private string _skillNextOfferTile = null;

    // 「敵の捨て牌の効果を無効化」フラグ（次の判定一度だけ）
    // スキル直後：このターンの[捨てる]で手牌1枚切りを抑止し、オファー4枚の通常処理に流すフラグ
    private bool _afterSkillNoHandDiscardOnce = false;
    private bool _suppressEnemyEffectsOnce = false;
    public enum ActiveSkill
    {
        None = 0,
        RandomMan,
        RandomSou,
        RandomPin,
        RandomHonor,
        RandomYaochu,     // 1/9 + 字牌
        RandomChunchan,   // 2..8
        DuplicateAndDiscardOther,
        EnhanceHand,      // 仮実装：手牌のランダム1枚を同スートの5に変換
        AddDoraIndicator,
        NullifyEnemyDiscardEffectsOnce,
        ForceDrawSelectedNextTurn,
        Capitalist
    }
private ActiveSkill GetEquippedSkill()
{
    // 常に装備から解決（メニュー装備を反映）
    return ResolveActiveSkillForMP();
}
private void UpdatePlayerHandAreaPositionByMeldState()
{
    if (handArea == null) return;

    bool hasOpenMeld =
        (_playerHasWonThisHand && _playerWonMeldsSnapshot != null)
            ? _playerWonMeldsSnapshot.Count > 0
            : (melds != null && melds.Count > 0);

    handArea.anchoredPosition =
        hasOpenMeld
            ? playerHandAreaPositionWithOpenMeld
            : playerHandAreaPositionWithoutOpenMeld;
}
// === Player cut-in: 装備スキルからカットイン画像を決定 ===
private void RefreshPlayerCutinSpriteFromSkill()
{
    try
    {
        var skill = GetEquippedSkill();
        // 装備スキル無しならカットインも無し
        if (skill == ActiveSkill.None)
        {
            playerCutinSprite = null;
            return;
        }

        // enum 名をそのままIDとして使う前提（例：RandomMan → PlayerCutins/RandomMan_cutin）
        string skillId = skill.ToString();
        string path = $"PlayerCutins/{skillId}_cutin";

        var sp = Resources.Load<Sprite>(path);
        if (sp != null)
        {
            playerCutinSprite = sp;
            Debug.Log($"[WinCutin] Player cut-in loaded: Resources/{path}");
        }
        else
        {
            // 見つからなかった場合は null のまま（カットイン画像なし）
            Debug.LogWarning($"[WinCutin] Player cut-in sprite not found at Resources/{path}");
            playerCutinSprite = null;
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning($"[WinCutin] RefreshPlayerCutinSpriteFromSkill failed: {ex.Message}");
    }
}
private void RefreshExistingPlayerTenpaiWaitTilesSize()
{
    if (!overridePlayerTenpaiWaitTileSize) return;

    // スロット方式
    if (playerTenpaiWaitSlots != null && playerTenpaiWaitSlots.Count > 0)
    {
        for (int i = 0; i < playerTenpaiWaitSlots.Count; i++)
        {
            var slot = playerTenpaiWaitSlots[i];
            if (!slot) continue;

            // slotの子（生成済みの牌）に適用
            for (int c = 0; c < slot.childCount; c++)
            {
                var child = slot.GetChild(c);
                if (!child) continue;
                ApplyPlayerTenpaiWaitTileSizeIfNeeded(child.gameObject);
            }

            // LayoutGroupがある場合に即時反映させる
            try { UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(slot); } catch { }
        }
        return;
    }

    // 自動レイアウト方式（Root直下）
    if (playerTenpaiWaitsRoot)
    {
        var rootTf = playerTenpaiWaitsRoot.transform;
        for (int c = 0; c < rootTf.childCount; c++)
        {
            var child = rootTf.GetChild(c);
            if (!child) continue;
            ApplyPlayerTenpaiWaitTileSizeIfNeeded(child.gameObject);
        }

        try { UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(playerTenpaiWaitsRoot.transform as RectTransform); } catch { }
    }
}
private void PlayCutinSE(AudioClip clip)
{
    if (cutinSESource != null && clip != null)
    {
        try { cutinSESource.PlayOneShot(clip); } catch { }
    }
}
    // ★追加: 勝利カットイン用スプライト (Resources/PlayerCutins/<Skill>_defeat) を取得
    private Sprite GetPlayerVictoryCutinSpriteForCurrentSkill()
    {
        try
        {
            var skill = GetEquippedSkill();
            if (skill == ActiveSkill.None) return null;

            // 例：RandomMan → Resources/PlayerCutins/RandomMan_defeat.png
            string skillId = skill.ToString();
            string path = $"PlayerCutins/{skillId}_defeat";

            var sp = Resources.Load<Sprite>(path);
            if (sp != null)
            {
                Debug.Log($"[WinCutin] Player VICTORY cut-in loaded: Resources/{path}");
                return sp;
            }

            // フォールバック：通常の和了カットインを使う
            return GetPlayerCutinSpriteForCurrentSkill();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WinCutin] GetPlayerVictoryCutinSpriteForCurrentSkill failed: {ex.Message}");
            return GetPlayerCutinSpriteForCurrentSkill();
        }
    }


    // ★追加: 敗北カットイン用スプライト (共通画像)
    private Sprite GetDefeatCutinSprite()
    {
        try
        {
            // 共通の敗北画像は Resources/PlayerCutins/Defeat.png を想定
            const string path = "PlayerCutins/Defeat";
            var sp = Resources.Load<Sprite>(path);
            if (sp != null)
            {
                Debug.Log($"[WinCutin] Defeat cut-in loaded: Resources/{path}");
                return sp;
            }

            // フォールバック：プレイヤー通常カットイン or 敵カットイン
            if (playerCutinSprite != null) return playerCutinSprite;
            return enemyCutinSprite;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[WinCutin] GetDefeatCutinSprite failed: {ex.Message}");
            return enemyCutinSprite;
        }
    }
private void UpdateRightInfoUI_Manual()
{
    var active = ResolveActiveSkillForMP();
    BuildActiveSkillInfoSplitTexts(
        active,
        out string skillNameText,
        out string skillDescText,
        out string traitGekiText,
        out string traitShunText,
        out string traitIyuText,
        out string legacySkillText);

    string skillActionNameText = BuildSkillActionNameText();

    skillNameText = BuildSkillInfoText();
    skillDescText = BuildSkillDescText();

    var sb = new System.Text.StringBuilder();

    if (!string.IsNullOrEmpty(skillNameText))
        sb.AppendLine(skillNameText);

    if (!string.IsNullOrEmpty(skillDescText))
        sb.AppendLine(skillDescText);

    if (!string.IsNullOrEmpty(traitGekiText))
        sb.AppendLine(traitGekiText);

    if (!string.IsNullOrEmpty(traitShunText))
        sb.AppendLine(traitShunText);

    if (!string.IsNullOrEmpty(traitIyuText))
        sb.AppendLine(traitIyuText);

    legacySkillText = sb.ToString().TrimEnd('\r', '\n');

    UpdateOmamoriRightInfo_Manual();

    BuildOfudaInfoText_Manual_Split(out string[] ofudaSplitTexts, out string[] ofudaRarityTags);

    EnemyAddon_SetSkillSplitInfo(
        skillNameText,
        skillActionNameText,
        skillDescText,
        traitGekiText,
        traitShunText,
        traitIyuText,
        legacySkillText);

    EnemyAddon_SetRightInfo(null, null, "");

    EnemyAddon_SetOfudaSplitInfo(ofudaSplitTexts);

    UpdateOmamoriIconUI_Manual();
    UpdateOfudaIconsUI_Manual(ofudaRarityTags);
}
private static string ExtractFirstToken(string s)
{
    if (string.IsNullOrEmpty(s)) return "";
    s = s.Trim();
    int sp = s.IndexOf(' ');
    if (sp < 0) return s;
    return s.Substring(0, sp).Trim();
}
private void UpdateOmamoriIconUI_Manual()
{
    if (!_omamoriIconImage) return;

    int id = 0;
    try { id = PlayerData.EquippedOmamori; } catch { id = 0; }

    if (id <= 0)
    {
        if (_omamoriIconImage.gameObject.activeSelf)
            _omamoriIconImage.gameObject.SetActive(false);
        return;
    }

    // ★神器（ユニーク）は必ず赤Tint（PlayerData 側で Color.red を返す）
    if (PlayerData.TryGetOmamoriRarityColor(id, out var cById))
    {
        _omamoriIconImage.color = cById;
    }
    else
    {
        // フォールバック（何らかの理由でID判定できない場合のみ）
        string rarityJp = "";
        try
        {
            string name = PlayerData.GetOmamoriName(id);
            rarityJp = ExtractFirstToken(name);
        }
        catch
        {
            rarityJp = "";
        }

        var c = GetRarityColorSafe(rarityJp, rarityJp);
        _omamoriIconImage.color = c;
    }

    if (!_omamoriIconImage.gameObject.activeSelf)
        _omamoriIconImage.gameObject.SetActive(true);
}
private void BuildOfudaInfoText_Manual_Split(out string[] ofudaTexts, out string[] rarityTags)
{
    ofudaTexts = new string[3] { "", "", "" };
    rarityTags = new string[3] { "", "", "" };

    try
    {
        var ids = OfudaRunInventory.LoadList();
        if (ids == null || ids.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                ofudaTexts[i] = GetGameFixedText_Local("placeholder_dash");
                rarityTags[i] = "";
            }
            return;
        }

        var cat = OfudaExcelLoader.Load();
        var defs = OfudaCatalog.BuildFromExcel(cat);
        if (defs == null || defs.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                ofudaTexts[i] = GetGameFixedText_Local("placeholder_dash");
                rarityTags[i] = "";
            }
            return;
        }

        var map = defs.ToDictionary(d => d.id, d => d);

        for (int slot = 0; slot < 3; slot++)
        {
            if (slot >= ids.Count)
            {
                ofudaTexts[slot] = GetGameFixedText_Local("placeholder_dash");
                rarityTags[slot] = "";
                continue;
            }

            var id = ids[slot];
            if (!map.TryGetValue(id, out var def) || def == null)
            {
                ofudaTexts[slot] = GetGameFixedText_Local("placeholder_dash");
                rarityTags[slot] = "";
                continue;
            }

            string rarity = def.rarity ?? "";

            string rarityLine = ColorizeRarityWord_NoBrackets(def.displayName, def.rarity);
            string nameLine = StripRarityPrefixBracket(def.displayName).Trim();

            ofudaTexts[slot] = rarityLine + "\n" + nameLine;
            rarityTags[slot] = rarity;
        }
    }
    catch
    {
        for (int i = 0; i < 3; i++)
        {
            ofudaTexts[i] = GetGameFixedText_Local("placeholder_dash");
            rarityTags[i] = "";
        }
    }
}
private void UpdateOfudaIconsUI_Manual(string[] rarityTags)
{
    if (_ofudaIconImages == null || _ofudaIconImages.Length == 0)
        return;

    for (int i = 0; i < _ofudaIconImages.Length; i++)
    {
        var img = _ofudaIconImages[i];
        if (!img) continue;

        string r = "";
        if (rarityTags != null && i < rarityTags.Length && rarityTags[i] != null)
            r = rarityTags[i];

        // 未装備扱い（空/ー/- すべて）
        bool empty =
            string.IsNullOrEmpty(r) ||
            r == "" ||
            r == "" ||
            r.Trim().Length == 0;

        if (empty)
        {
            // ★重要：GameObjectを非アクティブにしない（子TMPが消えるため）
            if (img.enabled) img.enabled = false;
            continue;
        }

        var c = GetRarityColorSafe(r, r);
        img.color = c;

        if (!img.enabled) img.enabled = true;
    }
}

private void BuildActiveSkillInfoSplitTexts(
    ActiveSkill s,
    out string skillNameText,
    out string skillDescText,
    out string traitGekiText,
    out string traitShunText,
    out string traitIyuText,
    out string legacySkillText)
{
skillNameText = GetGameFixedText_Local("equip_owned_empty");
skillDescText = GetGameFixedText_Local("equip_owned_empty");
traitGekiText = GetGameFixedText_Local("equip_owned_empty");
traitShunText = GetGameFixedText_Local("equip_owned_empty");
traitIyuText  = GetGameFixedText_Local("equip_owned_empty");
legacySkillText = GetGameFixedText_Local("rightinfo_no_skill_equipped");
    if (s == ActiveSkill.None)
        return;

    // 1) スキル名＋説明（既存の解決ロジックを流用）
    string name = GetActiveSkillDisplayName(s);
    string desc = GetActiveSkillDescription(s);

    if (!string.IsNullOrEmpty(name)) skillNameText = name;
    if (!string.IsNullOrEmpty(desc)) skillDescText = desc;

     // 2) 撃/瞬/癒の該当役（解放済みのみ）
     var ge = new List<string>();
     var sh = new List<string>();
     var iy = new List<string>();

     try
     {
         string skillName = s.ToString();

         SkillSetAsset hostSet = null;

         // 1) まず _skillSet がこのスキルの所属ならそれを使う
         if (_skillSet != null && _skillSet.activeSkills != null &&
             _skillSet.activeSkills.Any(e => e != null &&
                 string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase)))
         {
             hostSet = _skillSet;
         }

         // 2) 見つからなければ Resources/SkillSets を総当たり
         if (hostSet == null)
         {
             var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
             foreach (var sset in allSets)
             {
                 if (sset == null || sset.activeSkills == null) continue;

                 var entry = sset.activeSkills.FirstOrDefault(e =>
                     e != null && !string.IsNullOrEmpty(e.activeSkillName) &&
                     string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

                 if (entry != null)
                 {
                     hostSet = sset;
                     break;
                 }
             }
         }
         // 3) 全候補を取得（未解放も含む）
         //    ただし表示は「解放済み」または「特別牌で Lv0 以上になる」ものに絞る
         if (hostSet != null)
         {
             hostSet.EnsureInitialTraitUnlocks(skillName);

             var yakuTuple = hostSet.GetTraitYakuFor(skillName);

             if (yakuTuple.ge != null)
                 ge = yakuTuple.ge.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
             if (yakuTuple.sh != null)
                 sh = yakuTuple.sh.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
             if (yakuTuple.iy != null)
                 iy = yakuTuple.iy.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
         }

         else
         {
             // フォールバック（従来）：traitMap 全件
             if (_skillSet && _skillSet.traitMap != null && _skillSet.traitMap.Count > 0)
             {
                 ge = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Geki).Select(t => t.yakuName).ToList();
                 sh = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Shun).Select(t => t.yakuName).ToList();
                 iy = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Iyu).Select(t => t.yakuName).ToList();
             }

             // レベル取得のために hostSet が null のままなら _skillSet を使う
             if (hostSet == null && _skillSet != null)
                 hostSet = _skillSet;
         }

         // --- ここから「Lv.X」表記を付ける ---
         if (hostSet != null)
         {
List<string> FormatWithLevels(List<string> src, SkillSetAsset.Trait trait)
{
    var dst = new List<string>();
    if (src == null) return dst;

    const string COLOR_LOCKED = "#808080";   // Lv0（未解放扱い）＝グレー
    const string COLOR_UNLOCK = "#FFF2A8";   // Lv1以上＝薄黄色

    // Δ（Lv2から加算）を取得
    float deltaPerLevel = 0f;
    try
    {
        deltaPerLevel = GetTraitUpgradeDeltaFromPrefs(trait, hostSet);
    }
    catch
    {
        deltaPerLevel = 0f;
    }
// 難易度別テーブル（hostSet から取得）
// ★SerializeField(private) も拾えるように BindingFlags を付け、旧名/新名も複数候補で拾う
float[] table = null;
bool tableIsMultiplier = false; // Geki は true（1.20 などの倍率を保持）
try
{
    if (hostSet != null)
    {
        var tp = hostSet.GetType();

        var flags = System.Reflection.BindingFlags.Instance
                  | System.Reflection.BindingFlags.Public
                  | System.Reflection.BindingFlags.NonPublic;

        float[] GetFloatArrayByAnyName(params string[] names)
        {
            if (names == null) return null;

            for (int i = 0; i < names.Length; i++)
            {
                var n = names[i];
                if (string.IsNullOrEmpty(n)) continue;

                var f = tp.GetField(n, flags);
                if (f == null) continue;

                var v = f.GetValue(hostSet) as float[];
                if (v != null) return v;
            }
            return null;
        }

        switch (trait)
        {
            case SkillSetAsset.Trait.Geki:
                table = GetFloatArrayByAnyName(
                    "gekiDamageMulByDiff",
                    "gekiMultiplierByDiff",
                    "gekiMulByDiff"
                );
                tableIsMultiplier = true;  // ←倍率
                break;

            case SkillSetAsset.Trait.Shun:
                table = GetFloatArrayByAnyName(
                    "shunMpHealMulByDiff",
                    "shunMpPctByDiff",
                    "shunMpRateByDiff"
                );
                tableIsMultiplier = false; // ←％
                break;

            case SkillSetAsset.Trait.Iyu:
                table = GetFloatArrayByAnyName(
                    "iyuHealMulByDiff",
                    "iyuHealPctByDiff",
                    "iyuHealRateByDiff"
                );
                tableIsMultiplier = false; // ←％
                break;
        }
    }
}
catch
{
    table = null;
    tableIsMultiplier = false;
}

    float CalcEffectAdd(string yakuKey, int lvForEffect)
    {
        // Lv0表示でも効果量はLv1の値を出す仕様
        lvForEffect = Mathf.Max(1, lvForEffect);

        float add = 0f;

        if (table != null && table.Length > 0 && hostSet != null && hostSet.traitMap != null)
        {
            int di = 0;

            try
            {
                var entry = hostSet.traitMap.FirstOrDefault(t =>
                    t != null &&
                    t.trait == trait &&
                    !string.IsNullOrEmpty(t.yakuName) &&
                    string.Equals(t.yakuName.Trim(), yakuKey, StringComparison.Ordinal));

                if (entry != null)
                    di = Mathf.Clamp((int)entry.difficulty, 0, table.Length - 1);
            }
            catch { di = 0; }

            var v = Mathf.Max(0f, table[Mathf.Clamp(di, 0, table.Length - 1)]);
            add = tableIsMultiplier ? Mathf.Max(0f, v - 1f) : v;
        }
        else
        {
            add = 0f;
        }

        // Δ：Lv2から (Lv-1)×Δ を加算
        if (deltaPerLevel > 0f)
        {
            int deltaLv = Mathf.Max(0, lvForEffect - 1);
            add += deltaPerLevel * deltaLv;
        }

        return Mathf.Max(0f, add);
    }

    string FormatPct(float add01)
    {
        float pct = Mathf.Max(0f, add01) * 100f;
        if (Mathf.Abs(pct - Mathf.Round(pct)) < 0.0001f)
            return $"{Mathf.RoundToInt(pct)}%";
        return $"{pct:0.##}%";
    }

    foreach (var y in src)
    {
        string yaku = (y ?? "").Trim();
        if (string.IsNullOrEmpty(yaku)) continue;

        int baseLv = -1;
        try
        {
            // 想定：未解放 = -1
            baseLv = hostSet.GetTraitYakuLevel(skillName, trait, yaku);
        }
        catch
        {
            baseLv = -1;
        }

        // 特別牌（パッシブ）
        int bonusLv = 0;
        try
        {
            bonusLv = SpecialTileSystem.GetEquippedTraitBonusLv(yaku);
        }
        catch
        {
            bonusLv = 0;
        }

        // ★仕様：未解放(-1) + 特別牌(+1) => Lv0（未解放扱い）
        int effectiveLv = baseLv + Mathf.Max(0, bonusLv);

        // 表示条件：
        // - 解放済み（baseLv >= 0）なら表示
        // - 未解放でも特別牌でLv0以上になるなら表示（bonusLv > 0）
        if (baseLv < 0 && bonusLv <= 0)
            continue;

        // Lv0でも効果量はLv1の値を表示
        float add = CalcEffectAdd(yaku, effectiveLv);

        string color = (effectiveLv <= 0) ? COLOR_LOCKED : COLOR_UNLOCK;
        string text = $"{yaku} Lv.{effectiveLv} {FormatPct(add)}";

        dst.Add($"<color={color}>{text}</color>");
    }

    return dst;
}

             ge = FormatWithLevels(ge, SkillSetAsset.Trait.Geki);
             sh = FormatWithLevels(sh, SkillSetAsset.Trait.Shun);
             iy = FormatWithLevels(iy, SkillSetAsset.Trait.Iyu);
         }
         else
         {
             // hostSet が無い場合は役名だけ（クラッシュ回避）
             ge = ge.Select(x => (x ?? "").Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
             sh = sh.Select(x => (x ?? "").Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
             iy = iy.Select(x => (x ?? "").Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
         }
         // --- ここまで「Lv.X」表記 ---
     }
     catch { }
var geLocalized = LocalizeSkillPanelYakuList_Local(ge);
var shLocalized = LocalizeSkillPanelYakuList_Local(sh);
var iyLocalized = LocalizeSkillPanelYakuList_Local(iy);

traitGekiText = (geLocalized.Count > 0) ? string.Join(" / ", geLocalized) : GetGameFixedText_Local("equip_owned_empty");
traitShunText = (shLocalized.Count > 0) ? string.Join(" / ", shLocalized) : GetGameFixedText_Local("equip_owned_empty");
traitIyuText  = (iyLocalized.Count > 0) ? string.Join(" / ", iyLocalized) : GetGameFixedText_Local("equip_owned_empty");
    // 分割UIが未割当てだった場合に _skillInfoTMP へ出す用（既存の1枚表示）
    try
    {
        legacySkillText = BuildActiveSkillInfoLine(s);
    }
    catch
    {
legacySkillText = (!string.IsNullOrEmpty(name) ? name : GetGameFixedText_Local("active_skill_fallback")) + "\n" + desc;
    }
}
private string BuildOfudaInfoText_ForRightPanel()
{
    if (PlayerPrefs.HasKey(KeyRunOfudaJ) || PlayerPrefs.HasKey(KeyRunOfuda))
        return GetGameFixedText_Local("ofuda_owned");

    return GetGameFixedText_Local("ofuda_none");
}
private void __EnsureInitialTraitFirstYakuIsLv1(SkillSetAsset hostSet, string activeSkillName)
{
    if (hostSet == null || string.IsNullOrEmpty(activeSkillName)) return;

    try
    {
        // 全候補
        var all = hostSet.GetTraitYakuFor(activeSkillName);

        // 各Traitの先頭を「初期解放枠」とみなし、Lv1にする
        EnsureOne(SkillSetAsset.Trait.Geki, all.ge);
        EnsureOne(SkillSetAsset.Trait.Shun, all.sh);
        EnsureOne(SkillSetAsset.Trait.Iyu,  all.iy);

        void EnsureOne(SkillSetAsset.Trait t, List<string> list)
        {
            if (list == null || list.Count <= 0) return;

            string yaku = (list[0] ?? "").Trim();
            if (string.IsNullOrEmpty(yaku)) return;

            int lv = -1;
            try { lv = hostSet.GetTraitYakuLevel(activeSkillName, t, yaku); } catch { lv = -1; }

            // lv<0(未解放) なら Unlock してから Lv1 にする
            if (lv < 0)
            {
                try { hostSet.UnlockTraitYaku(activeSkillName, t, yaku); } catch { }
                lv = 0;
            }

            // Lv0なら +1 して Lv1
            if (lv == 0)
            {
                try { hostSet.AddTraitYakuLevel(activeSkillName, t, yaku, 1); } catch { }
            }
        }
    }
    catch { }
}
// === 右パネル（お守り）更新：Inspectorで割り当てたTMPへ流し込み ===
private void UpdateOmamoriRightInfo_Manual()
{
    string raw = BuildOmamoriInfoText();

    // ★重要：右パネル更新は頻繁に呼ばれるので、内容が変わった時だけ置換・反映する
    if (string.Equals(_cachedOmamoriRightInfoRaw, raw, StringComparison.Ordinal))
    {
        // 既に同じ内容なら、基本は何もしない（フリーズ原因を断つ）
        // ただし spriteAsset が別処理で上書きされた場合に備えて、適用だけは行う
        try
        {
            if (_omamoriInfoTMP)
            {
                ApplyTraitSpriteAssetToTMP(_omamoriInfoTMP);

                string renderedKeep = _cachedOmamoriRightInfoRendered ?? raw;
                if (!string.Equals(_omamoriInfoTMP.text, renderedKeep, StringComparison.Ordinal))
                {
                    _omamoriInfoTMP.richText = true;
                    _omamoriInfoTMP.text = renderedKeep;
                }
            }
        }
        catch { }

        return;
    }

    _cachedOmamoriRightInfoRaw = raw;

    // 置換（撃/瞬/癒 → <sprite=...>）
    string rendered = ReplaceTraitWordsWithIcons(raw);
    _cachedOmamoriRightInfoRendered = rendered;

    // TMPにSpriteAssetを適用してから反映
    try
    {
        if (_omamoriInfoTMP)
        {
            _omamoriInfoTMP.richText = true;
            ApplyTraitSpriteAssetToTMP(_omamoriInfoTMP);
        }
    }
    catch { }

    EnemyAddon_SetOmamoriInfo(rendered);
}

// --- ここから追記 ---
[SerializeField] private string _omamoriUITextOverride; // 手動でUIに出したい文字列（空なら自動組み立て）
private string _cachedOmamoriRightInfoRaw = null;
private string _cachedOmamoriRightInfoRendered = null;
[Header("Trait Icon Replacement (TMP)")]
[SerializeField] private bool replaceTraitWordsWithIcons = true;

// 「撃」「瞬」「癒」それぞれ別々にSpriteAssetを指定できるようにする
// それぞれの SpriteAsset に、該当アイコンを1つ（または複数でもOK）登録しておく想定
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAssetGeki = null;
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAssetShun = null;
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAssetIyu  = null;

// 置換対象の単語（基本はこのままでOK）
[SerializeField] private string traitWordGeki = "撃";
[SerializeField] private string traitWordShun = "瞬";
[SerializeField] private string traitWordIyu  = "癒";

[SerializeField] private int traitSpriteIndexGeki = 0;
[SerializeField] private int traitSpriteIndexShun = 0;
[SerializeField] private int traitSpriteIndexIyu  = 0;

// ★追加：Inspectorで各アイコン色を指定
[SerializeField] private Color traitIconColorGeki = Color.white;
[SerializeField] private Color traitIconColorShun = Color.white;
[SerializeField] private Color traitIconColorIyu  = Color.white;

// アイコンの大きさ（%） 100=通常。90にすると少し小さくなる
[SerializeField, Range(50, 150)] private int traitIconSizePercent = 100;
private TMP_SpriteAsset _traitSpriteAssetRuntime = null;
private int _traitSpriteAssetRuntimeKey = 0;
private void ApplyTraitSpriteAssetToTMP(TextMeshProUGUI tmp)
{
    if (!tmp) return;

    TMP_SpriteAsset primary = null;

    if (traitIconsSpriteAssetGeki != null) primary = traitIconsSpriteAssetGeki;
    else if (traitIconsSpriteAssetShun != null) primary = traitIconsSpriteAssetShun;
    else if (traitIconsSpriteAssetIyu != null) primary = traitIconsSpriteAssetIyu;

    if (primary == null) return;

    int key = 0;
    unchecked
    {
        key = key * 397 ^ (traitIconsSpriteAssetGeki ? traitIconsSpriteAssetGeki.GetInstanceID() : 0);
        key = key * 397 ^ (traitIconsSpriteAssetShun ? traitIconsSpriteAssetShun.GetInstanceID() : 0);
        key = key * 397 ^ (traitIconsSpriteAssetIyu  ? traitIconsSpriteAssetIyu.GetInstanceID()  : 0);
        key = key * 397 ^ (primary ? primary.GetInstanceID() : 0);
    }

    if (_traitSpriteAssetRuntime == null || _traitSpriteAssetRuntimeKey != key)
    {
        _traitSpriteAssetRuntimeKey = key;

        _traitSpriteAssetRuntime = Instantiate(primary);
        _traitSpriteAssetRuntime.name = primary.name + "_TraitRuntime";

        if (_traitSpriteAssetRuntime.fallbackSpriteAssets == null)
        {
            _traitSpriteAssetRuntime.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
        }
        else
        {
            _traitSpriteAssetRuntime.fallbackSpriteAssets.Clear();
        }

        void AddFallback(TMP_SpriteAsset a)
        {
            if (a == null) return;
            if (a == primary) return;
            if (_traitSpriteAssetRuntime.fallbackSpriteAssets.Contains(a)) return;
            _traitSpriteAssetRuntime.fallbackSpriteAssets.Add(a);
        }

        AddFallback(traitIconsSpriteAssetGeki);
        AddFallback(traitIconsSpriteAssetShun);
        AddFallback(traitIconsSpriteAssetIyu);
    }

    tmp.spriteAsset = _traitSpriteAssetRuntime;
}
private string ReplaceTraitWordsWithIcons(string src)
{
    if (!replaceTraitWordsWithIcons) return src;
    if (string.IsNullOrEmpty(src)) return src;

    // まず「撃/瞬/癒」が入っていないなら即return（無駄な処理を避ける）
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

    string ToHex(Color c)
    {
        return ColorUtility.ToHtmlStringRGBA(c);
    }

    string MakeTag(int spriteIndex, Color color)
    {
        if (spriteIndex < 0) return "";

        string spriteTag = $"<sprite={spriteIndex} tint=1 color=#{ToHex(color)}>";

        if (traitIconSizePercent != 100)
            return $"<size={traitIconSizePercent}%>{spriteTag}</size>";

        return spriteTag;
    }

    bool IsJapaneseChar(char ch)
    {
        if (ch >= '\u4E00' && ch <= '\u9FFF') return true; // Kanji
        if (ch >= '\u3040' && ch <= '\u309F') return true; // Hiragana
        if (ch >= '\u30A0' && ch <= '\u30FF') return true; // Katakana
        if (ch == 'ー' || ch == '々' || ch == '〆' || ch == '〤') return true;
        return false;
    }

    bool ShouldReplaceAt(int index)
    {
        char prev = index > 0 ? src[index - 1] : '\0';
        bool prevIsJp = (index > 0) && IsJapaneseChar(prev);

        char next = (index + 1 < src.Length) ? src[index + 1] : '\0';
        bool nextIsJp = (index + 1 < src.Length) && IsJapaneseChar(next);
        bool nextIsNo = (index + 1 < src.Length) && next == 'の';

        if (prevIsJp) return false;
        if (nextIsJp && !nextIsNo) return false;

        return true;
    }

    System.Text.StringBuilder sb = new System.Text.StringBuilder(src.Length + 16);

    for (int i = 0; i < src.Length; i++)
    {
        char c = src[i];

        if (c == '撃' && (traitWordGeki == "撃") && ShouldReplaceAt(i))
        {
            sb.Append(MakeTag(traitSpriteIndexGeki, traitIconColorGeki));
            continue;
        }
        if (c == '瞬' && (traitWordShun == "瞬") && ShouldReplaceAt(i))
        {
            sb.Append(MakeTag(traitSpriteIndexShun, traitIconColorShun));
            continue;
        }
        if (c == '癒' && (traitWordIyu == "癒") && ShouldReplaceAt(i))
        {
            sb.Append(MakeTag(traitSpriteIndexIyu, traitIconColorIyu));
            continue;
        }

        sb.Append(c);
    }

    return sb.ToString();
}
/// <summary>お守りUIの表示文を手動で上書きします（null/空なら上書き解除）。</summary>
public void SetOmamoriUIText(string text)
{
    _omamoriUITextOverride = string.IsNullOrEmpty(text) ? null : text;
    UpdateOmamoriRightInfo_Manual(); // すぐ反映
}
private string BuildOmamoriInfoText()
{
    try
    {
        int id = PlayerData.EquippedOmamori;
        if (id <= 0)
            return "";

        string desc = PlayerData.GetOmamoriDesc_Localized(id);
        if (string.IsNullOrEmpty(desc))
            return "";

        var lines = desc.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0)
            return "";

        string first = (lines[0] ?? "").Trim();
        first = RemoveParenEffectCount(first);
        first = first.Replace(" + 特殊", "");
        first = first.Replace(" + Special", "");
        first = first.Replace(" + 特殊", "");

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(ColorizeOmamoriRarityWordInLine(first));

        for (int i = 1; i < lines.Length; i++)
        {
            string ln = (lines[i] ?? "").Trim();
            if (string.IsNullOrEmpty(ln))
                continue;

            ln = StripEffectNumberPrefixIfAny(ln);

            sb.Append("\n");
            sb.Append(ln);
        }

        return sb.ToString();
    }
    catch
    {
        return GetGameFixedText_Local("placeholder_dash");
    }
}
private static string RemoveParenEffectCount(string s)
{
    if (string.IsNullOrEmpty(s))
        return s;

    int a = s.IndexOf("（効果", StringComparison.Ordinal);
    if (a < 0)
        return s;

    int b = s.IndexOf("）", a, StringComparison.Ordinal);
    if (b < 0)
        return s;

    return (s.Substring(0, a) + s.Substring(b + 1)).Trim();
}

private static string StripEffectNumberPrefixIfAny(string s)
{
    if (string.IsNullOrEmpty(s))
        return s;

    // 例: "効果5: xxxx" / "効果5 xxxx" を想定して先頭の「効果N...」を落とす
    if (!s.StartsWith("効果", StringComparison.Ordinal))
        return s;

    int cut = -1;

    // ":" があればそこまで落とす
    int colon = s.IndexOf(":", StringComparison.Ordinal);
    if (colon >= 0)
        cut = colon + 1;

    // ":" が無い場合は最初の空白まで落とす
    if (cut < 0)
    {
        int sp = s.IndexOf(' ');
        if (sp >= 0)
            cut = sp + 1;
    }

    if (cut < 0 || cut >= s.Length)
        return s;

    return s.Substring(cut).TrimStart();
}

private static string ColorizeOmamoriRarityWordInLine(string line)
{
    if (string.IsNullOrEmpty(line))
        return line;

    // 先頭トークン（最初の空白まで）をレア度として扱う
    int sp = line.IndexOf(' ');
    string head = (sp >= 0) ? line.Substring(0, sp) : line;
    string tail = (sp >= 0) ? line.Substring(sp) : "";

    var omamoriRarityColors = new Dictionary<string, Color32>
    {
        { "レジェンダリー", new Color32(255, 128, 0, 255) },
        { "エピック",       new Color32(128, 0, 255, 255) },
        { "レア",           new Color32(255, 255, 0, 255) },
        { "コモン",         new Color32(0, 128, 255, 255) },
        { "ノーマル",       new Color32(255, 255, 255, 255) }
    };

    if (!omamoriRarityColors.TryGetValue(head, out var color))
        return line;

    string hex = ColorUtility.ToHtmlStringRGB(color);
    return $"<color=#{hex}>{head}</color>{tail}";
}


private string ColorizeOmamoriRarityInFirstLine(string text)
{
    var lines = text.Split('\n');
    if (lines.Length == 0) return text;

    string first = lines[0];

    // 先頭が「【レジェンダリー】」形式にも対応
    if (first.StartsWith("【レジェンダリー】"))
        first = "【" + ColorizeRarityTag("Legendary", "レジェンダリー") + "】" + first.Substring("【レジェンダリー】".Length);
    else if (first.StartsWith("【エピック】"))
        first = "【" + ColorizeRarityTag("Epic", "エピック") + "】" + first.Substring("【エピック】".Length);
    else if (first.StartsWith("【レア】"))
        first = "【" + ColorizeRarityTag("Rare", "レア") + "】" + first.Substring("【レア】".Length);
    else if (first.StartsWith("【コモン】"))
        first = "【" + ColorizeRarityTag("Common", "コモン") + "】" + first.Substring("【コモン】".Length);
    else if (first.StartsWith("【ノーマル】"))
        first = "【" + ColorizeRarityTag("Normal", "ノーマル") + "】" + first.Substring("【ノーマル】".Length);

    // 先頭が「レジェンダリー …」形式にも対応（従来）
    else if (first.StartsWith("レジェンダリー"))
        first = ColorizeRarityTag("Legendary", "レジェンダリー") + first.Substring("レジェンダリー".Length);
    else if (first.StartsWith("エピック"))
        first = ColorizeRarityTag("Epic", "エピック") + first.Substring("エピック".Length);
    else if (first.StartsWith("レア"))
        first = ColorizeRarityTag("Rare", "レア") + first.Substring("レア".Length);
    else if (first.StartsWith("コモン"))
        first = ColorizeRarityTag("Common", "コモン") + first.Substring("コモン".Length);
    else if (first.StartsWith("ノーマル"))
        first = ColorizeRarityTag("Normal", "ノーマル") + first.Substring("ノーマル".Length);

    lines[0] = first;
    return string.Join("\n", lines);
}


private static string NormalizeRarityKey(string rarityRaw)
{
    if (string.IsNullOrEmpty(rarityRaw)) return rarityRaw;

    switch (rarityRaw)
    {
        case "レジェンダリー":
        case "Legendary":
            return "Legendary";

        case "エピック":
        case "Epic":
            return "Epic";

        case "レア":
        case "Rare":
            return "Rare";

        case "コモン":
        case "Common":
            return "Common";

        case "ノーマル":
        case "Normal":
            return "Normal";

        default:
            return rarityRaw;
    }
}

private static string ColorizeRarityTag(string rarity, string tagText)
{
    if (string.IsNullOrEmpty(tagText))
        return tagText;

    var c = GetRarityColorSafe(rarity, tagText);
    string hex = ColorUtility.ToHtmlStringRGB(c);
    return $"<color=#{hex}>{tagText}</color>";
}

private static Color GetRarityColorSafe(string rarityKeyOrRaw, string japaneseTagText)
{
    // 1) まず英語正規化キーで試す（Legendary/Epic/Rare/Common/Normal）
    string key = NormalizeRarityKey(rarityKeyOrRaw);

    // 2) OfudaRarityColors が英語キー対応ならこれでOK
    try
    {
        var c = OfudaRarityColors.Get(key);
        // Legendary等が白で返るケース（未対応）に備えてチェック
        if (!(c == Color.white && key != "Normal"))
            return c;
    }
    catch { }

    // 3) 次に日本語キーで試す（レジェンダリー/エピック/…）
    if (!string.IsNullOrEmpty(japaneseTagText))
    {
        try
        {
            var c2 = OfudaRarityColors.Get(japaneseTagText);
            if (!(c2 == Color.white && japaneseTagText != "ノーマル"))
                return c2;
        }
        catch { }
    }

    // 4) 最後の保険：固定色（他Sceneの意図と同じ配色）
    // Legendary=オレンジ / Epic=紫 / Rare=黄色 / Common=青 / Normal=白
    switch (japaneseTagText)
    {
        case "レジェンダリー": return new Color(1.00f, 0.55f, 0.00f); // orange
        case "エピック":       return new Color(0.60f, 0.20f, 1.00f); // purple
        case "レア":           return new Color(1.00f, 0.85f, 0.00f); // yellow
        case "コモン":         return new Color(0.20f, 0.60f, 1.00f); // blue
        case "ノーマル":       return Color.white;
    }

    switch (key)
    {
        case "Legendary": return new Color(1.00f, 0.55f, 0.00f);
        case "Epic":      return new Color(0.60f, 0.20f, 1.00f);
        case "Rare":      return new Color(1.00f, 0.85f, 0.00f);
        case "Common":    return new Color(0.20f, 0.60f, 1.00f);
        case "Normal":    return Color.white;
    }

    return Color.white;
}

private static bool TryParseRarityPrefix(string name, out string rarity)
{
    rarity = "";
    if (string.IsNullOrEmpty(name))
        return false;

    if (!name.StartsWith("【"))
        return false;

    int end = name.IndexOf("】", StringComparison.Ordinal);
    if (end <= 1)
        return false;

    rarity = name.Substring(1, end - 1);
    return !string.IsNullOrEmpty(rarity);
}

// OfudaDefから説明文っぽいメンバーを（存在すれば）拾うための保険
private static string TryGetDefDescription(object def)
{
    if (def == null)
        return "";

    string[] candidates =
    {
        "desc",
        "description",
        "effectDesc",
        "effectDescription",
        "detail",
        "text"
    };

    var t = def.GetType();

    foreach (var memberName in candidates)
    {
        var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(string))
        {
            var v = p.GetValue(def) as string;
            if (!string.IsNullOrEmpty(v))
                return v;
        }

        var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(string))
        {
            var v = f.GetValue(def) as string;
            if (!string.IsNullOrEmpty(v))
                return v;
        }
    }

    return "";
}

// 装備中お守りの (name, desc) を取得：
// 1) まず GameManager 自身のフィールド/プロパティを探索
// 2) 無ければシーン中の任意のコンポーネントから「equipped + (omamori|charm)」っぽいコレクションを探索
private System.Collections.Generic.List<(string name, string desc)> GetEquippedOmamoriForUI()
{
    var result = new System.Collections.Generic.List<(string, string)>();

    // ---- 1) GameManager 自身から探す ----
    if (TryCollectFromObject(this, ref result)) return result;

    // ---- 2) シーン内の他コンポーネントから探す（Loadout/Inventory 等）----
    //   ・"equipped" と "omamori/charm" を含むフィールド/プロパティを優先
    //   ・見つかった IEnumerable を走査し、各要素から name/desc を推測して抽出
    try
    {
        var monos = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
        foreach (var mb in monos)
        {
            if (mb == null) continue;
            var tn = mb.GetType().Name.ToLowerInvariant();
            // 候補：Omamori, Charm, Loadout, Inventory, Equipment などを含むやつを優先
            if (!(tn.Contains("omamori") || tn.Contains("charm") || tn.Contains("loadout") || tn.Contains("inventory") || tn.Contains("equip")))
                continue;

            if (TryCollectFromObject(mb, ref result))
                return result;
        }
    }
    catch {}

    // 何も見つからなければ空
    return result;

    // ===== ローカル関数群 =====
    static bool TryCollectFromObject(object obj, ref System.Collections.Generic.List<(string, string)> acc)
    {
        var t = obj.GetType();

        // 候補名：equippedOmamori / equippedCharms / playerCharms / omamoriList など
        string[] listNameHints = {
            "equippedomamori", "_equippedomamori", "playeromamori",
            "equippedcharms", "_equippedcharms", "playercharms",
            "omamorilist", "charmlist", "equipped", "_equipped"
        };

        // まずは「候補名を含む IEnumerable<string or object>」を優先的に拾う
        foreach (var mem in GetPublicAndPrivateMembers(t))
        {
            if (!IsEnumerable(mem.type)) continue;
            var lname = mem.name.ToLowerInvariant();
            if (!ContainsAny(lname, listNameHints)) continue;

            var enumerable = GetEnumerableValue(obj, mem);
            if (enumerable == null) continue;

            foreach (var it in enumerable)
            {
                if (it == null) continue;
                if (it is string sStr)
                {
                    // 文字列IDだけ持っている場合 → 名前として表示（説明は空）
                    acc.Add((sStr, ""));
                }
                else
                {
                    ExtractNameDesc(it, out var nm, out var ds);
                    if (!string.IsNullOrEmpty(nm) || !string.IsNullOrEmpty(ds))
                        acc.Add((nm, ds));
                }
            }
            if (acc.Count > 0) return true;
        }

        // 次に「IsEquipped=true の要素を持つコレクション」を一般探索（名前ヒント無し）
        foreach (var mem in GetPublicAndPrivateMembers(t))
        {
            if (!IsEnumerable(mem.type)) continue;

            var enumerable = GetEnumerableValue(obj, mem);
            if (enumerable == null) continue;

            bool foundAnyEquipped = false;
            var tmp = new System.Collections.Generic.List<(string, string)>();

            foreach (var it in enumerable)
            {
                if (it == null) continue;

                // 要素が「IsEquipped/Equipped」や「State/Status==Equipped」を持っていれば装備中とみなす
                bool equipped = GetBoolMember(it, new[] { "IsEquipped", "Equipped", "isEquipped" }, false)
                                || GetStringMember(it, new[] { "State", "Status" }).ToLowerInvariant() == "equipped";

                if (equipped)
                {
                    foundAnyEquipped = true;
                    ExtractNameDesc(it, out var nm, out var ds);
                    if (!string.IsNullOrEmpty(nm) || !string.IsNullOrEmpty(ds))
                        tmp.Add((nm, ds));
                }
            }
            if (foundAnyEquipped && tmp.Count > 0)
            {
                acc.AddRange(tmp);
                return true;
            }
        }

        return false;
    }

    static System.Collections.Generic.IEnumerable<(string name, System.Type type)> GetPublicAndPrivateMembers(System.Type tp)
    {
        const System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var f in tp.GetFields(BF))
            yield return (f.Name, f.FieldType);
        foreach (var p in tp.GetProperties(BF))
            yield return (p.Name, p.PropertyType);
    }

    static bool IsEnumerable(System.Type t)
        => typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(string);

    static System.Collections.IEnumerable GetEnumerableValue(object obj, (string name, System.Type type) mem)
    {
        const System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var tp = obj.GetType();
        var f = tp.GetField(mem.name, BF);
        if (f != null) return f.GetValue(obj) as System.Collections.IEnumerable;
        var p = tp.GetProperty(mem.name, BF);
        if (p != null) return p.GetValue(obj) as System.Collections.IEnumerable;
        return null;
    }

    static bool ContainsAny(string haystack, string[] needles)
    {
        foreach (var n in needles) if (haystack.Contains(n)) return true;
        return false;
    }

    static void ExtractNameDesc(object it, out string name, out string desc)
    {
        name = GetStringMember(it, new[] { "DisplayName", "Name", "Title", "displayName" });
        desc = GetStringMember(it, new[] { "Description", "Summary", "EffectText", "EffectsText", "effectsText", "description" });
    }

    static string GetStringMember(object inst, string[] cand)
    {
        const System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var tp = inst.GetType();
        foreach (var n in cand)
        {
            var p = tp.GetProperty(n, BF);
            if (p != null && p.PropertyType == typeof(string))
                return (string)p.GetValue(inst);
            var f = tp.GetField(n, BF);
            if (f != null && f.FieldType == typeof(string))
                return (string)f.GetValue(inst);
        }
        return "";
    }

    static bool GetBoolMember(object inst, string[] cand, bool fallback)
    {
        const System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var tp = inst.GetType();
        foreach (var n in cand)
        {
            var p = tp.GetProperty(n, BF);
            if (p != null && p.PropertyType == typeof(bool))
                return (bool)p.GetValue(inst);
            var f = tp.GetField(n, BF);
            if (f != null && f.FieldType == typeof(bool))
                return (bool)f.GetValue(inst);
        }
        return fallback;
    }

}
private string BuildActiveSkillInfoLine(ActiveSkill s)
{
if (s == ActiveSkill.None) return GetGameFixedText_Local("rightinfo_no_skill_equipped");

    var sb = new System.Text.StringBuilder();

    // 1) スキル名＋説明
    string name = GetActiveSkillDisplayName(s);
    string desc = GetActiveSkillDescription(s);
    sb.AppendLine($"<b>{name}</b>");
    sb.AppendLine(desc);

    // 2) 撃/瞬/癒の該当役（解放済みのみ）
    var ge = new List<string>();
    var sh = new List<string>();
    var iy = new List<string>();

    try
    {
        string skillName = s.ToString();

        SkillSetAsset hostSet = null;

        // 1) まず _skillSet がこのスキルの所属ならそれを使う
        if (_skillSet != null && _skillSet.activeSkills != null &&
            _skillSet.activeSkills.Any(e => e != null &&
                string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase)))
        {
            hostSet = _skillSet;
        }

        // 2) 見つからなければ Resources/SkillSets を総当たり
        if (hostSet == null)
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            foreach (var sset in allSets)
            {
                if (sset == null || sset.activeSkills == null) continue;

                var entry = sset.activeSkills.FirstOrDefault(e =>
                    e != null && !string.IsNullOrEmpty(e.activeSkillName) &&
                    string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    hostSet = sset;
                    break;
                }
            }
        }

         // 3) 全候補（未解放も含む）を取得
         if (hostSet != null)
         {
             hostSet.EnsureInitialTraitUnlocks(skillName);

             // ★仕様変更：各Traitの「最初の該当役」は最初から Lv1 にする
             __EnsureInitialTraitFirstYakuIsLv1(hostSet, skillName);

             var yakuTuple = hostSet.GetTraitYakuFor(skillName);

             if (yakuTuple.ge != null)
                 ge = yakuTuple.ge.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
             if (yakuTuple.sh != null)
                 sh = yakuTuple.sh.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
             if (yakuTuple.iy != null)
                 iy = yakuTuple.iy.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
         }

        else
        {
// フォールバック（従来）：traitMap 全件
if (_skillSet && _skillSet.traitMap != null && _skillSet.traitMap.Count > 0)
{
    ge = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Geki).Select(t => t.yakuName).ToList();
    sh = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Shun).Select(t => t.yakuName).ToList();
    iy = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Iyu).Select(t => t.yakuName).ToList();
}

        }
    }
    catch { }

    var geLocalized = LocalizeSkillPanelYakuList_Local(ge);
    var shLocalized = LocalizeSkillPanelYakuList_Local(sh);
    var iyLocalized = LocalizeSkillPanelYakuList_Local(iy);

    sb.AppendLine();
    sb.AppendLine(GetGameFixedText_Local("rightinfo_target_geki") + (geLocalized.Count > 0 ? string.Join(" / ", geLocalized) : GetGameFixedText_Local("none_plain")));
    sb.AppendLine(GetGameFixedText_Local("rightinfo_target_shun") + (shLocalized.Count > 0 ? string.Join(" / ", shLocalized) : GetGameFixedText_Local("none_plain")));
    sb.AppendLine(GetGameFixedText_Local("rightinfo_target_iyu") + (iyLocalized.Count > 0 ? string.Join(" / ", iyLocalized) : GetGameFixedText_Local("none_plain")));
    return sb.ToString();
}
string GetActiveSkillDisplayName(ActiveSkill s)
{
    // ★保証したい仕様：
    // 「Active Skill Name に enum 名（例：RandomHonor）を入れておけば、
    // 　右パネルは必ずその行の displayName を使う」
    //
    // そのために、「enum → SkillEntry」の解決を
    //  1) _skillSet
    //  2) SkillSets/SkillSet_SET_XXXX.asset
    //  3) SkillSets フォルダ内の全 SkillSetAsset
    // の順で総当たりします。

    SkillSetAsset.SkillEntry found = null;
    string key = s.ToString();

    try
    {
        // 1) 現在ロード済みの _skillSet から探す
        if (_skillSet && _skillSet.activeSkills != null)
        {
            found = _skillSet.activeSkills.FirstOrDefault(x =>
                x != null &&
                !string.IsNullOrEmpty(x.activeSkillName) &&
                string.Equals(x.activeSkillName.Trim(), key, StringComparison.OrdinalIgnoreCase));
        }

        // 2) 見つからなければ、enum 名から想定される SkillSetAsset をロード
        SkillSetAsset[] all = null;
        if (found == null)
        {
            string setId = "SET_" + key.ToUpperInvariant();
            SkillSetAsset hitSet = Resources.Load<SkillSetAsset>($"SkillSets/SkillSet_{setId}");

            if (!hitSet)
            {
                all = Resources.LoadAll<SkillSetAsset>("SkillSets");
                hitSet = all.FirstOrDefault(ss =>
                    ss &&
                    string.Equals((ss.id ?? string.Empty).Trim(), setId, StringComparison.OrdinalIgnoreCase));
            }

            if (hitSet && hitSet.activeSkills != null)
            {
                found = hitSet.activeSkills.FirstOrDefault(x =>
                    x != null &&
                    !string.IsNullOrEmpty(x.activeSkillName) &&
                    string.Equals(x.activeSkillName.Trim(), key, StringComparison.OrdinalIgnoreCase));
            }

            // 3) それでも見つからなければ、全 SkillSetAsset の activeSkills を総当たり
            if (found == null)
            {
                if (all == null)
                    all = Resources.LoadAll<SkillSetAsset>("SkillSets");

                foreach (var set in all)
                {
                    if (!set || set.activeSkills == null) continue;

                    var hit = set.activeSkills.FirstOrDefault(x =>
                        x != null &&
                        !string.IsNullOrEmpty(x.activeSkillName) &&
                        string.Equals(x.activeSkillName.Trim(), key, StringComparison.OrdinalIgnoreCase));

                    if (hit != null)
                    {
                        found = hit;
                        break;
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[SkillName] GetActiveSkillDisplayName asset lookup failed for {s}: {ex.Message}");
    }
    if (found != null)
    {
        string localized = found.GetLocalizedDisplayName();
        if (!string.IsNullOrEmpty(localized))
            return localized;
    }

    // フォールバック（Asset未解決時）
    switch (s)
    {
        case ActiveSkill.RandomMan:  return LocalizationManager.ActiveSkill("RandomMan");
        case ActiveSkill.EnhanceHand:return LocalizationManager.ActiveSkill("EnhanceHand");
        case ActiveSkill.RandomSou:  return GetGameFixedText_Local("skill_name_random_sou");
        case ActiveSkill.RandomPin:  return GetGameFixedText_Local("skill_name_random_pin");
        case ActiveSkill.RandomHonor:return GetGameFixedText_Local("skill_name_random_honor");
        case ActiveSkill.RandomYaochu: return GetGameFixedText_Local("skill_name_random_yaochu");
        case ActiveSkill.RandomChunchan:return GetGameFixedText_Local("skill_name_random_chunchan");
        case ActiveSkill.DuplicateAndDiscardOther: return GetGameFixedText_Local("skill_name_duplicate_and_discard_other");
        case ActiveSkill.AddDoraIndicator: return GetGameFixedText_Local("skill_name_add_dora_indicator");
        case ActiveSkill.NullifyEnemyDiscardEffectsOnce: return GetGameFixedText_Local("skill_name_nullify_enemy_discard_effects_once");
        case ActiveSkill.ForceDrawSelectedNextTurn: return GetGameFixedText_Local("skill_name_force_draw_selected_next_turn");
        default: return s.ToString();
    }
}

string GetActiveSkillDescription(ActiveSkill s)
{
    // 方針：
    // - まずロード済みの _skillSet を見る
    // - 見つからなければ従来通り「SET_◯◯」推定で Resources からも探す
    // - ただし照合は「activeSkillName/displayName の文字列」ではなく、
    //   SkillIdToEnum(...) による列挙値一致を優先する
    // - それでも見つからなければ従来フォールバック文を返す（Editorと同じ見え方にする）

    SkillSetAsset.SkillEntry found = null;
    string key = s.ToString();

    try
    {
        // 1) 現在ロード済みの _skillSet から探す
        if (_skillSet && _skillSet.activeSkills != null)
        {
            foreach (var x in _skillSet.activeSkills)
            {
                if (x == null) continue;

                // (a) 従来互換：enum名の完全一致
                if (!string.IsNullOrEmpty(x.activeSkillName) &&
                    string.Equals(x.activeSkillName.Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    found = x;
                    break;
                }

                // (b) 追加：activeSkillName を enum に変換して一致
                if (!string.IsNullOrEmpty(x.activeSkillName) &&
                    SkillIdToEnum(x.activeSkillName.Trim()) == s)
                {
                    found = x;
                    break;
                }

                // (c) 追加：displayName を enum に変換して一致（日本語名運用の吸収）
                if (!string.IsNullOrEmpty(x.displayName) &&
                    SkillIdToEnum(x.displayName.Trim()) == s)
                {
                    found = x;
                    break;
                }
            }
        }

        // 2) 見つからなければ、enum 名から想定される SkillSetAsset をロード
        SkillSetAsset[] all = null;
        if (found == null)
        {
            string setId = "SET_" + key.ToUpperInvariant();
            SkillSetAsset hitSet = Resources.Load<SkillSetAsset>($"SkillSets/SkillSet_{setId}");

            if (!hitSet)
            {
                all = Resources.LoadAll<SkillSetAsset>("SkillSets");
                hitSet = all.FirstOrDefault(ss =>
                    ss &&
                    string.Equals((ss.id ?? string.Empty).Trim(), setId, StringComparison.OrdinalIgnoreCase));
            }

            if (hitSet && hitSet.activeSkills != null)
            {
                foreach (var x in hitSet.activeSkills)
                {
                    if (x == null) continue;

                    if (!string.IsNullOrEmpty(x.activeSkillName) &&
                        string.Equals(x.activeSkillName.Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        found = x;
                        break;
                    }

                    if (!string.IsNullOrEmpty(x.activeSkillName) &&
                        SkillIdToEnum(x.activeSkillName.Trim()) == s)
                    {
                        found = x;
                        break;
                    }

                    if (!string.IsNullOrEmpty(x.displayName) &&
                        SkillIdToEnum(x.displayName.Trim()) == s)
                    {
                        found = x;
                        break;
                    }
                }
            }

            // 3) それでも見つからなければ、全 SkillSetAsset の activeSkills を総当たり
            if (found == null)
            {
                if (all == null)
                    all = Resources.LoadAll<SkillSetAsset>("SkillSets");

                foreach (var set in all)
                {
                    if (!set || set.activeSkills == null) continue;

                    foreach (var x in set.activeSkills)
                    {
                        if (x == null) continue;

                        if (!string.IsNullOrEmpty(x.activeSkillName) &&
                            string.Equals(x.activeSkillName.Trim(), key, StringComparison.OrdinalIgnoreCase))
                        {
                            found = x;
                            break;
                        }

                        if (!string.IsNullOrEmpty(x.activeSkillName) &&
                            SkillIdToEnum(x.activeSkillName.Trim()) == s)
                        {
                            found = x;
                            break;
                        }

                        if (!string.IsNullOrEmpty(x.displayName) &&
                            SkillIdToEnum(x.displayName.Trim()) == s)
                        {
                            found = x;
                            break;
                        }
                    }

                    if (found != null) break;
                }
            }
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[SkillName] GetActiveSkillDescription asset lookup failed for {s}: {ex.Message}");
    }

    if (found != null)
    {
        string localized = found.GetLocalizedDescription();
        if (!string.IsNullOrEmpty(localized))
            return localized;
    }

    // フォールバック（Asset未解決時）
    switch (s)
    {
        case ActiveSkill.RandomMan:  return GetGameFixedText_Local("skill_desc_random_man");
        case ActiveSkill.RandomSou:  return GetGameFixedText_Local("skill_desc_random_sou");
        case ActiveSkill.RandomPin:  return GetGameFixedText_Local("skill_desc_random_pin");
        case ActiveSkill.RandomHonor:return GetGameFixedText_Local("skill_desc_random_honor");
        case ActiveSkill.RandomYaochu: return GetGameFixedText_Local("skill_desc_random_yaochu");
        case ActiveSkill.RandomChunchan:return GetGameFixedText_Local("skill_desc_random_chunchan");
        case ActiveSkill.DuplicateAndDiscardOther: return GetGameFixedText_Local("skill_desc_duplicate_and_discard_other");
        case ActiveSkill.EnhanceHand: return GetGameFixedText_Local("skill_desc_enhance_hand");
        case ActiveSkill.AddDoraIndicator: return GetGameFixedText_Local("skill_desc_add_dora_indicator");
        case ActiveSkill.NullifyEnemyDiscardEffectsOnce: return GetGameFixedText_Local("skill_desc_nullify_enemy_discard_effects_once");
        case ActiveSkill.ForceDrawSelectedNextTurn: return GetGameFixedText_Local("skill_desc_force_draw_selected_next_turn");
        default: return "";
    }
}
    private void EnsureSkillInit()
    {
        if (_activeSkillChargesLeft < 0) _activeSkillChargesLeft = Mathf.Max(0, activeSkillCharges);
    }


    private bool ReplaceHandAt(int index, string newId)
    {
        if (index < 0 || index >= hand.Count) return false;
        hand[index] = newId;
        SortHand();
        RefreshHandUI();
        return true;
    }
    // Header: Round & Score
    // ===== Combat / HP System =====
    [Header("Combat / HP (new)")]
    // === NEW: Score (run) ===
[SerializeField] private int runScore = 0; // このラン（ゲームオーバーまで）の累計ダメージ

private void ResetRunScore()
{
    runScore = 0;
    SaveLastRunScore(); // 途中保存も兼ねる（安全）
    RefreshTopUI();     // 画面上部に表示中なら更新
}

private void AddScore(int add)
{
    if (add <= 0) return;

    runScore += add;
    SaveLastRunScore(); // 途中保存

    try
    {
        AchievementSystem.NotifyRunFinishedScore(runScore);
    }
    catch
    {
    }

    RefreshTopUI();
}
private void SaveLastRunScore()
{
    try
    {
        UnityEngine.PlayerPrefs.SetInt("LastRunScore", Mathf.Max(0, runScore));
        // ハイスコア更新
        int hi = UnityEngine.PlayerPrefs.GetInt("HighScore", 0);
        if (runScore > hi)
        {
            UnityEngine.PlayerPrefs.SetInt("HighScore", runScore);
        }
        UnityEngine.PlayerPrefs.Save();
    }
    catch { /* ignore in unstable contexts */ }
}
[SerializeField] private int playerMaxHP = 100;
// Inspector での最大HP編集を廃止（Excelで必ず上書きする）
private int enemyMaxHP = 100;

[SerializeField] private int playerHP = -1;
private int enemyHP = -1; // ← Inspector からの復元を完全に遮断
private bool _excelEnemyApplied = false;

[SerializeField] private float damageMultiplier = 1.0f;   // 基本倍率（和了ダメージ用）

    [SerializeField] private int extraPointsBase = 0;         // 追加点（常に加算）

    [Tooltip("マンズ和了時の一時的なダメージ倍率ボーナス（例：0.2 で +20%）")]
    [SerializeField] private float manzuWinMultiplierBonus = 0.2f;
    [Tooltip("ピンズ和了時の回復量（HP）")]
    [SerializeField] private int pinzuWinHeal = 5;
    [Tooltip("ソーズ和了時の追加点ボーナス")]
    [SerializeField] private int souzuWinExtraPoints = 200;

    [Header("Enemy Attack (every N turns)")]
    [SerializeField] private int enemyAttackIntervalTurns = 3;
    [SerializeField] private float enemyAttackMultiplier = 1.0f;

    [Tooltip("敵の3ターン分の捨て牌効果：数字ごとの基礎値（index 1..9 を使用）")]
    [SerializeField] private int[] enemyManDamageByNumber = new int[10];
    [SerializeField] private int[] enemyPinHealByNumber   = new int[10];
    [SerializeField] private int[] enemySouTsumoPenaltyByNumber = new int[10];

    // ★追加：敵スキルの発動までの残りターン
    //  - EnemySkills_Addon から更新され、カウントダウン UI にも利用される
    private int _enemySkillTurnsUntilNext = -1;

    // ========== 敵AIレベル設定（すべてInspectorで設定） ==========
    [System.Serializable]
    public class EnemyAILevelOverride
    {
        [Tooltip("敵のランタイムインデックス（0始まり）")]
        public int enemyIndex;
        [Tooltip("この敵のAIレベル (1=弱, 2=中, 3=強)")]
        [Range(1, 3)] public int aiLevel = 2;
    }

    [Header("Enemy AI Level Settings")]
    [Tooltip("デフォルトのAIレベル（オーバーライド未設定の敵に使用）: 1=弱, 2=中, 3=強")]
    [SerializeField, Range(1, 3)] private int enemyAIDefaultLevel = 2;

    [Tooltip("敵ごとのAIレベルを個別に指定（未設定の敵はデフォルトレベルを使用）")]
    [SerializeField] private List<EnemyAILevelOverride> enemyAILevelOverrides = new List<EnemyAILevelOverride>();

    [Header("Level 1 (弱)")]
    [Tooltip("レベル1: 聴牌速度の最適選択確率 (%)")]
    [SerializeField, Range(0, 100)] private int aiLevel1_SpeedOptimalPercent = 40;
    [Tooltip("レベル1: 聴牌形（翻数）の最適選択確率 (%)")]
    [SerializeField, Range(0, 100)] private int aiLevel1_ScoreOptimalPercent = 40;

    [Header("Level 2 (中)")]
    [Tooltip("レベル2: 聴牌速度の最適選択確率 (%)")]
    [SerializeField, Range(0, 100)] private int aiLevel2_SpeedOptimalPercent = 60;
    [Tooltip("レベル2: 聴牌形（翻数）の最適選択確率 (%)")]
    [SerializeField, Range(0, 100)] private int aiLevel2_ScoreOptimalPercent = 60;

    [Header("Level 3 (強)")]
    [Tooltip("レベル3: 聴牌速度の最適選択確率 (%)")]
    [SerializeField, Range(0, 100)] private int aiLevel3_SpeedOptimalPercent = 80;
    [Tooltip("レベル3: 聴牌形（翻数）の最適選択確率 (%)")]
    [SerializeField, Range(0, 100)] private int aiLevel3_ScoreOptimalPercent = 80;

    /// <summary>現在の敵のAIレベル（1-3）と速度/得点の最適選択確率（0-100）を返す</summary>
    private void GetCurrentEnemyAISettings(out int level, out int speedPct, out int scorePct)
    {
        level = Mathf.Clamp(enemyAIDefaultLevel, 1, 3);

        // Inspectorのオーバーライドリストから現在の敵インデックスを検索
        try
        {
            int runtimeIdx = ProgressionFlowController.GetCurrentEnemyIndex();
            if (enemyAILevelOverrides != null)
            {
                for (int i = 0; i < enemyAILevelOverrides.Count; i++)
                {
                    var ov = enemyAILevelOverrides[i];
                    if (ov != null && ov.enemyIndex == runtimeIdx)
                    {
                        level = Mathf.Clamp(ov.aiLevel, 1, 3);
                        break;
                    }
                }
            }
        }
        catch { }

        // レベルに応じた確率を返す
        switch (level)
        {
            case 1:
                speedPct = aiLevel1_SpeedOptimalPercent;
                scorePct = aiLevel1_ScoreOptimalPercent;
                break;
            case 2:
                speedPct = aiLevel2_SpeedOptimalPercent;
                scorePct = aiLevel2_ScoreOptimalPercent;
                break;
            case 3:
                speedPct = aiLevel3_SpeedOptimalPercent;
                scorePct = aiLevel3_ScoreOptimalPercent;
                break;
            default:
                speedPct = aiLevel2_SpeedOptimalPercent;
                scorePct = aiLevel2_ScoreOptimalPercent;
                break;
        }
    }

    [Header("Optional HP UI")]
// Assets/Scripts/GameManager.cs
    [SerializeField] private Image playerHPBar; // 赤ゲージ（Filled, Horizontal）
    [SerializeField] private Image enemyHPBar;  // 赤ゲージ（Filled, Horizontal）
    [SerializeField] private TMPro.TextMeshProUGUI playerHPTMP;
    [SerializeField] private TMPro.TextMeshProUGUI enemyHPTMP;
// === Enemy Portrait (Battle) ===
[Header("Enemy Portrait (Battle)")]
[SerializeField] private Image enemyPortraitImage;                    // 対局用の敵画像（UI.Image）
[SerializeField] private string battlePortraitFolder = "Sprites/Enemies/Battle"; // Resources 相対

    // Runtime for combat
    private readonly System.Collections.Generic.Queue<System.Collections.Generic.List<string>> _enemyTurnHistory = new();
    private int _enemyTurnCounter = 0;
    private int _pendingTsumoPenalty = 0; // 次の BeginOfferPhase で引く枚数を減らす
    // 敵の索子効果で予約された「次の和了の減点」
    private int _pendingNextWinPointPenalty = 0;


    [Header("Turn / Round Options")]
[SerializeField] private int maxTurnsPerHand = 20; // ← Inspector で設定可能（既定20）


// この局での自分ツモ回数（10回で流局）
private int _playerTsumoCountThisRound = 0;

// ★追加：1ターン目にプレイヤーが暗槓したか（天和/地和/人和の阻害条件）
private bool _playerDidAnkanOnFirstTurnThisHand = false;

// One side can win first; their hand becomes empty and their turns auto-discard.
private bool _playerHasWonThisHand = false;
private bool _enemyHasWonThisHand  = false;
private bool _playerWonThisHand
{
    get { return _playerHasWonThisHand; }
    set { _playerHasWonThisHand = value; }
}
private bool _enemyWonThisHand
{
    get { return _enemyHasWonThisHand; }
    set { _enemyHasWonThisHand = value; }
}
    private System.Collections.Generic.Stack<string> enemyDeck = new();
    [SerializeField] private int stageTarget = 20000;
    [SerializeField] private int[] enemyTargets = new int[] { 2000, 5000, 8000 }; // 1人目/2人目/3人目の目標スコア
[SerializeField] private bool isDealer = false;
    [SerializeField] private int totalScore = 0;
private int roundNumber = 1; // 東1局〜南4局（計8局）
private int maxRounds   = 8;


    // 直前の局が流局だったかどうか（次局開始時の敵手牌リセット方法に使う）
    private bool _lastHandWasRyukyoku = false;
[Header("Upgrade / Stage Flow (optional)")]
[SerializeField] private string upgradeSceneName = "UpgradeScene"; // 強化シーン名（未指定ならパネル優先）
[SerializeField] private string rewardSceneName  = "StageClearScene"; // 報酬シーン名（3人目撃破時）
[SerializeField] private GameObject upgradePanel; // 強化画面（存在しない場合は無視）
private bool pendingNextStage = false;

[Header("Omamori Reward (Run -> Reward)")]
[SerializeField] private int omamoriRewardMinDefeats = 3; // 何人倒したらお守り報酬が出るか（Inspectorで変更）

    // Header: Rogue-lite Items (Run)
    [System.Serializable]
    public class ItemDef
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
    }

    [Header("Rogue-lite Items (Run)")]
    [SerializeField] private List<ItemDef> itemCatalog = new List<ItemDef>(); // Inspectorで拡張可能

    // このラン（対局終了まで）に保持するアイテムID
    private HashSet<string> runItemIds = new HashSet<string>();

    // PlayerPrefs に保存するキー
    private const string RunItemsKey = "RunItems";

    private void LoadRunItems()
    {
        runItemIds.Clear();
        try
        {
            string raw = UnityEngine.PlayerPrefs.GetString(RunItemsKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var s in raw.Split(new char[]{','}, System.StringSplitOptions.RemoveEmptyEntries))
                    runItemIds.Add(s.Trim());
            }
        } catch {}
    }
    private void SaveRunItems()
    {
        try
        {
            string raw = string.Join(",", runItemIds.ToArray());
            UnityEngine.PlayerPrefs.SetString(RunItemsKey, raw);
            UnityEngine.PlayerPrefs.Save();
        } catch {}
    }
void ClearRunItems()
{
    runItemIds.Clear();
    try { UnityEngine.PlayerPrefs.DeleteKey(RunItemsKey); } catch {}

    // ★追加：流局など「ラン終了」時に、お札も必ずリセット
    try
    {
        UnityEngine.PlayerPrefs.DeleteKey(KeyRunOfuda);
        UnityEngine.PlayerPrefs.DeleteKey(KeyRunOfudaJ);
    }
    catch {}

    // ★追加：ローグライトの「該当役 解放/強化」をラン終了で初期化
    try
    {
        if (_skillSet != null) _skillSet.ResetAllTraitYakuProgress();
    }
    catch {}
}


    private bool HasRunItem(string id) { return !string.IsNullOrEmpty(id) && runItemIds.Contains(id); }

    // カタログにサンプル（サンプル１）を必ず用意（Inspectorで未設定でも動作）
    
    // 100個のサンプルアイテム（同一効果）を用意：Sample1..Sample100 / 表示名「サンプル1..サンプル100」
    private void EnsureSampleItems()
    {
        // 既存カタログに不足分だけ追加（重複回避）
        for (int i = 1; i <= 100; i++)
        {
            string id = "Sample" + i;
            bool has = false;
            for (int k = 0; k < itemCatalog.Count; k++)
            {
                var d = itemCatalog[k];
                if (d != null && d.id == id) { has = true; break; }
            }
            if (!has)
            {
                var d = new ItemDef();
                d.id = id;
                d.displayName = "サンプル" + i;
                d.description = "リーチの役が含まれていると点数1.5倍。";
                d.icon = null;
                itemCatalog.Add(d);
            }
        }
    }

    // 同じ効果（リーチ×1.5）アイテムを所持しているか。Sample1..Sample100 のいずれかを持っていれば true。
    private bool HasAnyRiichi15Item()
    {
        if (runItemIds == null || runItemIds.Count == 0) return false;
        foreach (var id in runItemIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (id.StartsWith("Sample", System.StringComparison.Ordinal))
            {
                int n;
                if (int.TryParse(id.Substring("Sample".Length), out n) && n >= 1 && n <= 100)
                    return true;
            }
        }
        return false;
    }
private void EnsureSampleItem()
    {
        bool has = false;
        foreach (var d in itemCatalog) if (d != null && d.id == "Sample1") { has = true; break; }
        if (!has)
        {
            var d = new ItemDef();
            d.id = "Sample1";
            d.displayName = "サンプル１";
            d.description = "リーチの役が含まれていると点数1.5倍。";
            d.icon = null;
            itemCatalog.Add(d);
        }
    }

    // リーチ役を含むかの軽量判定（yaku文字列に「リーチ / riichi / reach」を含む）
// 既存の関数を置き換え
private static bool ListContainsRiichi(List<string> yaku)
{
    if (yaku == null) return false;
    foreach (var raw in yaku)
    {
        if (string.IsNullOrEmpty(raw)) continue;
        var t = raw.Trim();
        var tl = t.ToLowerInvariant(); // 英語表記用

        // 英語・カナ
        if (tl.Contains("riichi") || tl.Contains("reach")) return true;
        if (t.Contains("リーチ") || t.Contains("ダブルリーチ") || t.Contains("Wリーチ")) return true;

        // 漢字表記
        if (t.Contains("立直") || t.Contains("ダブル立直")) return true;
    }
    return false;
}


    // ==== 強化（アイテム選択）UI ====
    private Transform itemOfferRoot; // upgradePanelの子に作るコンテナ
    private readonly List<string> _lastItemOfferIds = new List<string>(3);

    private void BuildItemOffersUI()
    {
        if (!upgradePanel) return;
        // ルートを探す/作る
        if (!itemOfferRoot)
        {
            var go = new GameObject("ItemOffers", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(upgradePanel.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(600, 320);
            itemOfferRoot = rt;
        }
        // クリア
        for (int i = itemOfferRoot.childCount-1; i>=0; i--) Destroy(itemOfferRoot.GetChild(i).gameObject);

        // 未所持から最大3つランダム
        var pool = new List<ItemDef>();
        foreach (var d in itemCatalog) if (d != null && !HasRunItem(d.id)) pool.Add(d);
        // シャッフル
        for (int i = pool.Count-1; i > 0; i--) { int j = rng.Next(i+1); var tmp = pool[i]; pool[i]=pool[j]; pool[j]=tmp; }
        int offerCount = Mathf.Min(3, pool.Count);
        _lastItemOfferIds.Clear();
        for (int i = 0; i < offerCount; i++) _lastItemOfferIds.Add(pool[i].id);

        float spacing = 200f;
        float startX = -spacing * (offerCount-1) * 0.5f;
        for (int i = 0; i < offerCount; i++)
        {
            var def = pool[i];
            var btnGO = new GameObject("Item_"+def.id, typeof(RectTransform), typeof(Button), typeof(Image));
            var rt = btnGO.GetComponent<RectTransform>();
            rt.SetParent(itemOfferRoot, false);
            rt.sizeDelta = new Vector2(180, 120);
            rt.anchoredPosition = new Vector2(startX + i*spacing, 0f);

            var img = btnGO.GetComponent<Image>();
            img.color = new Color(1f,1f,1f,0.15f);

            // ラベル
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = label.GetComponent<RectTransform>();
            lrt.SetParent(btnGO.transform, false);
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(170, 110);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.text = def.displayName + "\n<size=18>" + def.description + "</size>";

    var btn = btnGO.GetComponent<Button>();
    btn.interactable = !HasRunItem(def.id);   // ← 未所持なら押せる（無料）
    int cap = i;
    btn.onClick.AddListener(()=>OnPickItem(pool[cap].id));

        }

        // 注意書き
        var captionGO = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        var crt = captionGO.GetComponent<RectTransform>();
        crt.SetParent(itemOfferRoot, false);
        crt.anchorMin = new Vector2(0.5f, 1f);
        crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = new Vector2(0f, 30f);
        crt.sizeDelta = new Vector2(600, 40);
        var ctmp = captionGO.GetComponent<TextMeshProUGUI>();
ctmp.alignment = TextAlignmentOptions.Center;
ctmp.fontSize = 24;
ctmp.text = "強化：ランダム3種から1つ選択（重複入手なし）";


    }

private string GetActiveSkillActionNameSafe(ActiveSkill s)
{
    try
    {
        string skillName = s.ToString();

        SkillSetAsset hostSet = null;

        if (_skillSet != null && _skillSet.activeSkills != null &&
            _skillSet.activeSkills.Any(e => e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase)))
        {
            hostSet = _skillSet;
        }

        if (hostSet == null)
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            foreach (var sset in allSets)
            {
                if (sset == null || sset.activeSkills == null) continue;

                var entry = sset.activeSkills.FirstOrDefault(e =>
                    e != null &&
                    !string.IsNullOrEmpty(e.activeSkillName) &&
                    string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    hostSet = sset;
                    break;
                }
            }
        }

        if (hostSet != null && hostSet.activeSkills != null)
        {
            var entry = hostSet.activeSkills.FirstOrDefault(e =>
                e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                string localized = entry.GetLocalizedActionName();
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
        }
    }
    catch
    {
    }

    return "";
}
private void OnPickItem(string id)
{
    if (string.IsNullOrEmpty(id)) return;
    if (HasRunItem(id)) return; // 重複回避
    runItemIds.Add(id);
    SaveRunItems();
    // 次の敵へ
    StartNextStage();
}

    // ラン専用アイテムによるスコア補正（追加しやすいようにここに集約）
    private void ApplyRunItemScoringModifiers(List<string> yaku, ref float mult, ref int extra, List<string> lines)
    {
ApplyRunOfudaModifiers(yaku, ref mult, ref extra, lines);
    }
/// <summary>
/// 撃/瞬/癒の元係数（SkillSetAsset.GetTraitCoeffs の返り値）および
/// これまでに積み上げた multiplier/extra/healMul に、お守りの効果を合成する。
/// lines に文言を追加するとスコアパネルに追記しやすい。
/// </summary>
// yaku に「撃 / 癒 / 瞬」に該当する役名が含まれるかを簡易判定
private static bool YakuHas(List<string> yaku, params string[] keys)
{
    if (yaku == null || yaku.Count == 0) return false;
    foreach (var r in yaku)
    {
        if (string.IsNullOrEmpty(r)) continue;
        var t = r.Trim();
        var tl = t.ToLowerInvariant();
        foreach (var k in keys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            // 和名・ローマ字の両方をゆるく判定
            if (t.Contains(k) || tl.Contains(k.ToLowerInvariant())) return true;
        }
    }
    return false;
}

private void ApplyOmamoriScoringModifiers(
    List<string> yaku, SkillSetAsset.YakuDifficulty diff,
    ref float multiplier, ref int extra, ref float healMul, List<string> lines)
{
    RefreshOmamoriCache();

    // 役リストから “今回そのトレイトが出ているか” を判定
    bool hasGeki = YakuHas(yaku, "撃", "geki");
    bool hasIyu  = YakuHas(yaku, "癒", "iyu");
    bool hasShun = YakuHas(yaku, "瞬", "shun");

    // ---- “該当時だけ”%を乗せる ----
    if (hasGeki && _om.gekiDmgUp > 0f)
        multiplier *= (1f + _om.gekiDmgUp);   // 与ダメ倍率（撃）

    if (hasShun && _om.shunAddUp > 0f)
        extra = Mathf.RoundToInt(extra * (1f + _om.shunAddUp));  // 瞬の基礎加算

    if (hasIyu && _om.iyuHealUp > 0f)
        healMul *= (1f + _om.iyuHealUp);      // 回復倍率（癒）

    // UI行も“該当時だけ”追加
    if (lines != null)
    {
        if (hasGeki && _om.gekiDmgUp > 0f) lines.Add($"お守り（撃） +{Mathf.RoundToInt(_om.gekiDmgUp * 100f)}%");
        if (hasShun && _om.shunAddUp  > 0f) lines.Add($"お守り（瞬） +{Mathf.RoundToInt(_om.shunAddUp  * 100f)}%");
        if (hasIyu  && _om.iyuHealUp  > 0f) lines.Add($"お守り（癒） +{Mathf.RoundToInt(_om.iyuHealUp  * 100f)}%");
    }
}


[Header("Run / Enemy Progression (DEPRECATED: Excel only)")]
[HideInInspector, SerializeField] private int enemiesPerRun = 10;          // DEPRECATED: not used at runtime
[HideInInspector, SerializeField] private int firstEnemyMaxHP = 100;       // DEPRECATED: not used at runtime
[HideInInspector, SerializeField, Min(1f)] private float enemyHpGrowth = 1.2f; // DEPRECATED: not used at runtime


        private bool suppressTsumoThisOffer = false;

    private enum Phase { Offer, NeedDiscardN, EnemyTurn, ChoosingCall, NeedDiscardAfterCall, Scoring }
    private Phase phase = Phase.Offer;
private bool _autoSkipPending = false;
    // ★追加：リーチ中の Offer 自動確定専用（敵ターン自動とは分離してデグレ防止）
    private bool _autoConfirmOfferPending = false;

    // ★追加：和了／流局でスコア表示に入ったら自動進行コルーチンを凍結する（“たまに進む”事故防止）
    private bool _freezeProgression = false;
// ★追加：MP減少演出中（スキルMP消費／敵妨害MPダメージ）の一時フラグ
private bool _mpDecreaseAnimRunning = false;

// ★追加：この場面だけ「ボタンを暗くしない」ための退避
private readonly System.Collections.Generic.Dictionary<UnityEngine.UI.Button, UnityEngine.UI.ColorBlock> _noDimSavedColorBlocks
    = new System.Collections.Generic.Dictionary<UnityEngine.UI.Button, UnityEngine.UI.ColorBlock>();

private void SetNoDimButtons_Temporary(bool active)
{
    // ★重要：
    // メニュー系はこの制御の対象外にする。
    // 「無効化する対象だけ」を明示し、メニューボタンは一切ここで触らない。
    UnityEngine.UI.Button[] targets = new UnityEngine.UI.Button[]
    {
        btnConfirm,
        btnSkill,
        btnSkip,
        btnTenpaiConfirm,
        btnRiichi,
        btnPon,
        btnChi,
        btnKan,
        btnKanFromHand,
        btnRon,
        btnRonSkip,
        scoringOKButton
    };

    if (active)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var b = targets[i];
            if (!b) continue;

            if (!_noDimSavedColorBlocks.ContainsKey(b))
                _noDimSavedColorBlocks[b] = b.colors;

            var cb = b.colors;
            cb.disabledColor = cb.normalColor;
            b.colors = cb;
        }
    }
    else
    {
        foreach (var kv in _noDimSavedColorBlocks)
        {
            if (kv.Key) kv.Key.colors = kv.Value;
        }
        _noDimSavedColorBlocks.Clear();
    }
}

private readonly List<string> hand = new();
private readonly List<string> offers = new();
private readonly List<string> discards = new();
private readonly List<string> enemyDiscards = new();
private readonly List<List<string>> melds = new();
private readonly HashSet<int> selHand = new();
private readonly HashSet<int> selOffer = new();
private HashSet<int> enemyUsedIndices = new();
private HashSet<int> enemyEffectAppliedIndices = new HashSet<int>();
private bool canRonNow = false, canTsumoNow = false;
private readonly List<string> lastEnemyTurnTiles = new();
private int selectedEnemyIndex = -1;

// ★追加：過去差分で参照されている互換用（EnemyDiscard選択インデックス）
private int selectedEnemyDiscardIdx = -1;

private enum CallMode { None, Pon, Chi, KanOpen }
    private CallMode callMode = CallMode.None;
    private string callBaseTile = null;

    // 鳴き種類（ポン/チー/カン）選択段階で、どれを出すか保持する
    private bool _pendingCallPon = false;
    private bool _pendingCallChi = false;
    private bool _pendingCallKan = false;

    private int needDiscardCount = 0;
    private int discardedThisTurn = 0;
    private bool isTenpai = false;
    private bool isRiichi = false;

    // ★追加：リーチ宣言ターンの「左端捨て牌」をハイライトするためのインデックス（discards 上の index）
    private int _playerRiichiDiscardHighlightIndex = -1;

    // ★追加：敵のリーチ宣言ターンの「左端捨て牌」をハイライトするためのインデックス（enemyDiscards 上の index）
    private int _enemyRiichiDiscardHighlightIndex = -1;

    // ★追加：ダブル立直／一発判定用（プレイヤー側）
    private bool _playerIsDoubleRiichi = false;
    private bool _playerIppatsuEligible = false;
    private int _playerRiichiDeclaredTsumoCountThisRound = -1;

    // ★追加: プレイヤーのリーチカットインが再生中かどうか
    private bool _playerRiichiCutinRunning = false;

private Coroutine _playerRiichiCutinCo = null;
    // Wall & Dora
    private readonly Stack<string> deck = new();
    private System.Random rng = new System.Random();
    private readonly List<string> doraIndicators = new();
// ==== Menu Panel (RunScene) ====
[Header("Menu Panel")]
[SerializeField] private GameObject menuPanel;              // パネル本体（Canvas 内の全画面パネル）
[SerializeField] private Button btnMenu;                    // 右上などに置く [メニュー] トグルボタン（任意）
[SerializeField] private Button btnMenuOption;              // メニューパネル内 [Option]
[SerializeField] private Button btnMenuSuspend;             // メニューパネル内 [中断]
[SerializeField] private Button btnMenuExit;                // メニューパネル内 [終了]
[SerializeField] private Button btnMenuClose;               // メニューパネル内 [戻る/閉じる]
[SerializeField] private string menuSceneName = "MenuScene";// 終了時に戻るシーン名（必要に応じて Inspector で差し替え）

// ===== Riichi Cut-in (手動紐付け) =====
[Header("Riichi Cut-in")]
[SerializeField] private GameObject enemyRiichiCutinRoot;       // 敵リーチ時に表示するルート
[SerializeField] private TextMeshProUGUI enemyRiichiTextTMP;    // 「リーチ」テキスト
[SerializeField] private UnityEngine.UI.Image enemyRiichiImage; // 敵画像

// ★追加：敵スキル用カットイン UI（Inspector から手動で紐づけ）
[SerializeField] private GameObject        enemySkillCutinRoot;         // 敵スキル発動時のカットインルート
[SerializeField] private TextMeshProUGUI  enemySkillCutinTextTMP;      // 敵スキル名を表示するテキスト
[SerializeField] private UnityEngine.UI.Image enemySkillCutinImage;    // 敵スキルカットイン画像（敵リーチと同じ sprite を流用）

// ★追加：敵スキルのカウントダウン表示
[SerializeField] private TextMeshProUGUI enemySkillCountdownTMP;

[SerializeField] private GameObject playerRiichiCutinRoot;      // プレイ

[SerializeField] private TextMeshProUGUI playerRiichiTextTMP;   // 「リーチ」テキスト
[SerializeField] private UnityEngine.UI.Image playerRiichiImage;// プレイヤー画像

private bool isMenuOpen = false;
private bool _menuSuspendInProgress = false;

// Suspend（中断保存）用 PlayerPrefs キー
private const string PF_SUSPEND_JSON = "Run_SuspendJSON";
private const string PF_SUSPEND_FLAG = "Run_HasSuspend";
// ★追加：ローグライト用「Run開始済み」フラグ（初回Run開始時にカウンタを初期化するため）
private const string PF_RUN_STARTED = "Run_StartedFlagV1";

// ===== 中断復元で「装備/HP/MP/特別牌」を固定するための内部フラグ =====
private bool _suspendRestoredThisSession = false;

// SkillMP_Addon.Start() が PlayerPrefs を見て上書きしないようにするための “保留” 値
private bool _pendingSuspendLoadoutApply = false;
private string _pendingSuspendSkillSetId = "";
private string _pendingSuspendActiveSkillName = "";
private int _pendingSuspendPlayerMP = 0;

// Runボーナス(Shop強化/Run_HPBonus)の “加算が積み上がる” のを防ぐための基礎値
private bool _runBonusesApplied = false;
private int _basePlayerMaxHP_ForRunBonuses = -1;
// ===== 中断復元時に「装備/最大MP」を“ラン中だけ固定”するためのロック =====
private bool _suspendLoadoutLocked = false;
private bool _suspendResumedAlreadyApplied = false;
private string _suspendLockedSkillSetId = null;
private string _suspendLockedActiveSkillName = null;
private int _suspendLockedEffectiveMaxMP = -1;

private void Awake()
{
    if (_inst != null && _inst != this) { Destroy(gameObject); return; }
    _inst = this;

    // ★重要：中断復元がある場合、ここでお守り/Shopの上乗せを絶対にしない（復元スナップショットを優先）
    bool hasSuspendForAwake = false;
    try { hasSuspendForAwake = PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) == 1; } catch { hasSuspendForAwake = false; }

    if (!hasSuspendForAwake)
    {
        // お守り恒久補正（最大HPなど）を一度だけ適用
        ApplyOmamoriBaseStatsOnce();
    }

    // ★ お札CSVの自己診断（未対応キーを警告）
    ValidateOfudaCatalogOnce();

    // ★追加：UpgradeSceneを経由しない場合でもΔ(PF_TraitUpgradeDelta_*)をInspector値で確定させる
    __BootstrapTraitUpgradeDeltaPrefs_FromRunSceneInspector();

// PF_ResetRunOnLoad に依存すると、起動直後やメニュー開始で過去Runの値が残留し得るため、ここで確実に潰す。
// ※重要：報酬/強化シーンで GameManager が存在しても、ここでは LastGrantedOmamoriIdV1 を潰さない（=報酬が消えるため）
try
{
    string awakeSceneName_ForRunInit = "";
    try { awakeSceneName_ForRunInit = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; } catch { awakeSceneName_ForRunInit = ""; }

    // 報酬/強化シーンでは、Runの初期化を動かさない（報酬IDを消す事故防止）
    if (awakeSceneName_ForRunInit != rewardSceneName && awakeSceneName_ForRunInit != upgradeSceneName)
    {
        bool hasSuspend = PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) == 1;
        if (!hasSuspend)
        {
            int started = PlayerPrefs.GetInt(PF_RUN_STARTED, 0);

            int enemyIdx = 0;
            try { enemyIdx = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { enemyIdx = 0; }

            int ce0 = 0;
            int pce0 = 0;
            try { ce0 = Mathf.Max(0, PlayerPrefs.GetInt("CurrentEnemyIndex", 0)); } catch { ce0 = 0; }
            try { pce0 = Mathf.Max(0, PlayerPrefs.GetInt("PF_CurrentEnemyIndex", 0)); } catch { pce0 = 0; }

            bool isFirstEnemy = (enemyIdx == 0 && ce0 == 0 && pce0 == 0);

            if (started == 0 && isFirstEnemy)
            {
                PlayerPrefs.SetInt("Run_DefeatedEnemyCount", 0);
                PlayerPrefs.SetInt("Run_LastCountedEnemyIndex", -1);

                // ここで LastGrantedOmamoriIdV1 を 0 にしない（報酬シーンで報酬が消えるため）

                PlayerPrefs.SetInt(PF_RUN_STARTED, 1);
                PlayerPrefs.Save();
            }
        }
    }
}
catch { }
try
{
    // 中断復元が存在するなら、リセット要求は“無視”する
    bool hasSuspend = PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) == 1;

if (!hasSuspend && PlayerPrefs.GetInt("PF_ResetRunOnLoad", 0) == 1)
{
    // ★重要：テスト開始敵を含め、開始敵Indexは 0 固定にしない
    int startEnemyIdx = 0;
    try { startEnemyIdx = Mathf.Max(0, PlayerPrefs.GetInt("PF_CurrentEnemyIndex", 0)); } catch { startEnemyIdx = 0; }

    // 互換キーも揃える（値は「今の開始敵」を維持）
    try { PlayerPrefs.SetInt("PF_CurrentEnemyIndex", startEnemyIdx); } catch {}
    try { PlayerPrefs.SetInt("CurrentEnemyIndex",   startEnemyIdx); } catch {}

    // ※名前はここで空にしない（会話シーンで正しい敵名が出ているため、上書きすると逆に揺れる）
    // PlayerPrefs.SetString("PF_CurrentEnemyName", "");
    // PlayerPrefs.SetString("CurrentEnemyName", "");

    PlayerPrefs.DeleteKey("EnemiesDefeated");
    PlayerPrefs.DeleteKey("RunCleared");

    // ★追加：ローグライト用「Run内撃破数」を必ずリセット
    PlayerPrefs.SetInt("Run_DefeatedEnemyCount", 0);
    PlayerPrefs.SetInt("Run_LastCountedEnemyIndex", -1);

    // ★追加：前回の報酬お守りIDを必ず消す
    PlayerPrefs.SetInt("LastGrantedOmamoriIdV1", 0);

    // ★重要：PlayerData 側も「開始敵Index」に合わせる
    try { PlayerData.CurrentEnemy = startEnemyIdx; } catch {}

    // ★追加：新規ランとして完全初期化（お札・役強化・デッキ等を確実にリセット）
    roundNumber = 1;
    totalScore = 0;
    try { ClearRunItems(); } catch {}
    try { PlayerData.ResetDeckToDefault(); } catch {}
    try { ResetRunGold(); } catch {}
    try
    {
        runScore = 0;
        SaveLastRunScore();
    }
    catch { }

    ApplyHpMpLayering();

    // ここで初めてフラグを消す（途中で消すと完全初期化が走らない）
    PlayerPrefs.DeleteKey("PF_ResetRunOnLoad");
    PlayerPrefs.Save();
}
}
catch { /* ここで例外を出してゲームを止めない */ }
    // 敵ごとの目標スコアを適用（StageSelect で PlayerData.CurrentEnemy を 0,1,2 に設定）
    try {
        int enemyIdx = Mathf.Clamp(PlayerData.CurrentEnemy, 0, enemyTargets.Length-1);
        stageTarget = enemyTargets != null && enemyTargets.Length>0 ? enemyTargets[enemyIdx] : stageTarget;
    } catch { /* PlayerData 未定義でも実行可 */ }

    // 保険：東場は常に4局
    if (maxRounds < 4) maxRounds = 4;

// Excel最優先。読めなければ従来のInspectorスケーリングへ
// Excel必須。読めなければ適用を中止（フォールバック無し）
if (!TryApplyExcelEnemyConfigForCurrentIndex())
{
    Debug.LogError("[EnemyConfig] Excel required but not applied. Check StreamingAssets/enemy_config.xlsx and sheet name.");
    // ※以降の処理は続けるとしても、敵名UIには何も出さない
}



// 中断があれば TryLoadSuspendSnapshot() が優先される StartNextHand() を使う
EnsureTraitUnlocksAtBattleStart();
StartNextHand();
EnsureMenuPanelWiring(true);

EnsureBottomButtons();
if (btnConfirm)       btnConfirm.onClick.AddListener(OnClickConfirm);
if (btnSkill)         btnSkill.onClick.AddListener(OnClickSkill);
if (btnTenpaiConfirm) btnTenpaiConfirm.onClick.AddListener(OnClickTenpaiConfirm);
if (btnRiichi)        btnRiichi.onClick.AddListener(OnClickRiichi);
if (btnSkip)
{
    btnSkip.onClick.RemoveListener(OnClickSkipCall);
    btnSkip.onClick.AddListener(OnClickSkipCall);
}

if (scoringPanel) scoringPanel.SetActive(false);
WireScoringOK();
if (winCutinRoot)
{
    winCutinRoot.SetActive(true);

    if (winCutinGroup)
    {
        winCutinGroup.alpha = 0f;
        winCutinGroup.interactable = false;
        winCutinGroup.blocksRaycasts = false;
    }
}
try { ApplyPendingFullHealIfAny(); } catch {}
RefreshTopUI();
RefreshAll();
            UpdateHpUI();
            // === NEW: start of run score (FIX: 1人目だけリセット／以降は読み戻し) ===


            // Awake() の既存「1人目だけリセット／以降は読み戻し」の直後あたりに追記
{
    int enemyIdxSafe = 0;
    bool idxResolved = false;

    // 1) まず ProgressionFlowController から現在の敵インデックスを取得（新フロー）
    try
    {
        enemyIdxSafe = Mathf.Max(0, ProgressionFlowController.GetCurrentEnemyIndex());
        idxResolved = true;
    }
    catch { }

    // 2) 取れなかった場合だけ、旧 PlayerData.CurrentEnemy をフォールバック
    if (!idxResolved)
    {
        try { enemyIdxSafe = Mathf.Max(0, PlayerData.CurrentEnemy); }
        catch { enemyIdxSafe = 0; }
    }

    if (enemyIdxSafe <= 0)
    {
        ResetRunGold();         // ★ゴールドをリセット（新しいラン）
        scoreThisEnemy = 0;     // ★敵ごとのスコア初期化
        // 既存: ResetRunScore() はそのまま
    }
    else
    {
        LoadRunGold();          // ★2人目以降は引き継ぎ
        scoreThisEnemy = 0;
    }
    RefreshTopUI();
}
var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
if (sceneName != rewardSceneName && sceneName != upgradeSceneName) // 報酬/強化シーンでは操作しない
{
    try
    {
        runScore = Mathf.Max(0, UnityEngine.PlayerPrefs.GetInt("LastRunScore", 0));
    }
    catch
    {
        runScore = 0;
    }
    RefreshTopUI(); // 表示更新
}
// === Enemy Meld Addon bootstrap ===
try
{
    EnableEnemyMeldModeAddon();   // グレーハイライト等を駆動する常時ループを起動
}
catch (Exception e)
{
    Debug.LogWarning("[EnemyMeldModeAddon] Enable failed: " + e.Message);
}

// ★重要：中断データがある状態で RunScene に来た場合、メニュー変更（Shop強化/HP）をAwakeで上乗せしない
if (!hasSuspendForAwake)
{
    __ApplyRunBonusesAndRefreshUI();
}

if (specialTilePopupCloseButton)
{
    // OKボタンは廃止（非表示）
    specialTilePopupCloseButton.onClick.RemoveAllListeners();
    specialTilePopupCloseButton.gameObject.SetActive(false);
}
if (specialTilePopupRoot) specialTilePopupRoot.SetActive(false);
    }
private string _specialTilePopupShownId = null;

private void ToggleSpecialTilePopup(string rawTileId)
{
    // 互換のため残す（旧呼び出しがあっても動く）
    if (string.IsNullOrEmpty(rawTileId))
    {
        HideSpecialTilePopup();
        return;
    }

    string id = StripStar(rawTileId);
    if (!IsSpecialTileId(id))
    {
        HideSpecialTilePopup();
        return;
    }

    // 「今表示しているのと同じ特別牌」を再クリック → 閉じる
    if (specialTilePopupRoot && specialTilePopupRoot.activeSelf &&
        !string.IsNullOrEmpty(_specialTilePopupShownId) &&
        string.Equals(_specialTilePopupShownId, id, StringComparison.Ordinal))
    {
        HideSpecialTilePopup();
        return;
    }

    // それ以外は表示へ
    ShowSpecialTilePopup(id);
}
private void __BootstrapTraitUpgradeDeltaPrefs_FromRunSceneInspector()
{
    if (!bootstrapWriteTraitUpgradeDeltaPrefsOnAwake)
        return;

    try
    {
        // RunScene直行時に、Inspector値で必ずΔを確定させる
        // 既存のPlayerPrefs値があっても、Inspectorを変更した場合に反映されるよう上書きする
        PlayerPrefs.SetString("PF_TraitUpgradeDelta_Geki", traitUpgradeDeltaGeki_RunScene.ToString());
        PlayerPrefs.SetString("PF_TraitUpgradeDelta_Shun", traitUpgradeDeltaShun_RunScene.ToString());
        PlayerPrefs.SetString("PF_TraitUpgradeDelta_Iyu",  traitUpgradeDeltaIyu_RunScene.ToString());
        PlayerPrefs.Save();
    }
    catch
    {
        // 失敗してもゲームが落ちないように
    }
}

private void HideSpecialTilePopup()
{
    if (specialTilePopupRoot) specialTilePopupRoot.SetActive(false);
    _specialTilePopupShownId = null;
}

private void SetSpecialTilePopupForSelection(string rawTileId, bool isSelected)
{
    if (!specialTilePopupRoot || !specialTilePopupText) return;

    if (string.IsNullOrEmpty(rawTileId))
    {
        HideSpecialTilePopup();
        return;
    }

    string id = StripStar(rawTileId);

    // 特別牌じゃないものをクリックしたら閉じる（既存仕様維持）
    if (!IsSpecialTileId(id))
    {
        if (specialTilePopupRoot.activeSelf) HideSpecialTilePopup();
        return;
    }

    // 選択状態（上にスライド）でない限りは表示しない
    if (!isSelected)
    {
        // 同じ特別牌のポップアップが出ている場合のみ閉じる
        if (specialTilePopupRoot.activeSelf &&
            !string.IsNullOrEmpty(_specialTilePopupShownId) &&
            string.Equals(_specialTilePopupShownId, id, StringComparison.Ordinal))
        {
            HideSpecialTilePopup();
        }
        return;
    }

    // 選択状態なら表示
    ShowSpecialTilePopup(id);
}
private void ShowSpecialTilePopup(string id)
{
    if (!specialTilePopupRoot || !specialTilePopupText) return;

    try
    {
        if (!TryGetSpecialTileRarity(id, out var r))
        {
            HideSpecialTilePopup();
            return;
        }

        var lines = new List<string>();

        lines.Add(GetSpecialTileText_Local("specialtile.dora_plus_1"));

        int traitUp = GetTraitUpgradeCountForSpecialRarity(r);
        if (traitUp > 0)
        {
            if (TryGetSpecialTileUpgradeYakuFromId(id, out string yakuName) && !string.IsNullOrEmpty(yakuName))
            {
                lines.Add(BuildSpecialTileTraitUpgradeLine_Local(yakuName, traitUp));
            }
            else
            {
                lines.Add(BuildSpecialTileTraitUpgradeFallback_Local(traitUp));
            }
        }

        if (r == SpecialTileRarity.Legendary)
        {
            if (TryGetLegendaryEffectIndex(id, out int fx))
            {
                string t = "";

                if (fx == 1) t = GetSpecialTileText_Local("specialtile.legendary_fx_1");
                else if (fx == 2) t = GetSpecialTileText_Local("specialtile.legendary_fx_2");
                else if (fx == 3) t = GetSpecialTileText_Local("specialtile.legendary_fx_3");
                else if (fx == 4) t = GetSpecialTileText_Local("specialtile.legendary_fx_4");
                else if (fx == 5) t = GetSpecialTileText_Local("specialtile.legendary_fx_5");
                else if (fx == 6) t = GetSpecialTileText_Local("specialtile.legendary_fx_6");

                if (!string.IsNullOrEmpty(t))
                    lines.Add($"<color=#FF0000>{t}</color>");
            }
        }

        if (lines.Count == 0)
        {
            HideSpecialTilePopup();
            return;
        }

        specialTilePopupText.text = string.Join("\n", lines);
        specialTilePopupRoot.SetActive(true);
        _specialTilePopupShownId = id;
    }
    catch
    {
        HideSpecialTilePopup();
    }
}
private static string GetSpecialTileScoringLine_Local(string kind, int value = 0)
{
    var lm = LocalizationManager.Instance;
    var lang = (lm != null) ? lm.CurrentLanguage : LocalizationManager.Language.Japanese;

    switch (kind)
    {
        case "dora":
            switch (lang)
            {
                case LocalizationManager.Language.English: return $"• Special Tile: Dora +{value}";
                case LocalizationManager.Language.ChineseSimplified: return $"• 特别牌：宝牌+{value}";
                case LocalizationManager.Language.Japanese:
                default: return $"・特別牌：ドラ+{value}";
            }

        case "fx1":
            return $"・<color=#FF0000>{GetSpecialTileText_Local("specialtile.legendary_fx_1")}</color>";

        case "fx3":
            return $"・<color=#FF0000>{GetSpecialTileText_Local("specialtile.legendary_fx_3")}</color>";

        case "fx4":
            return $"・<color=#FF0000>{GetSpecialTileText_Local("specialtile.legendary_fx_4")}</color>";

        case "fx6":
            switch (lang)
            {
                case LocalizationManager.Language.English:
                    return $"• <color=#FF0000>On win: +16 Fu x{value}</color>";
                case LocalizationManager.Language.ChineseSimplified:
                    return $"• <color=#FF0000>和了时 +16符 ×{value}</color>";
                case LocalizationManager.Language.Japanese:
                default:
                    return $"・<color=#FF0000>和了時 +16符 ×{value}</color>";
            }

        case "fx5_triggered":
            switch (lang)
            {
                case LocalizationManager.Language.English:
                    return "• <color=#FF0000>The reserved half MP cost for the next hand activated on this win</color>";
                case LocalizationManager.Language.ChineseSimplified:
                    return "• <color=#FF0000>已预约的下一局MP消耗减半在此次和了时发动</color>";
                case LocalizationManager.Language.Japanese:
                default:
                    return "・<color=#FF0000>予約されていた次局のMP消費半分がこの和了で発動</color>";
            }
    }

    return "";
}
private readonly Dictionary<string, int> _specialTileTraitLvBonusThisScoring = new Dictionary<string, int>();
private int _specialTileTraitLvBonusTotalThisScoring = 0;
private static int GetTraitUpgradeCountForSpecialRarity(SpecialTileRarity rarity)
{
    switch (rarity)
    {
        case SpecialTileRarity.Common:     return 1;
        case SpecialTileRarity.Rare:       return 2;
        case SpecialTileRarity.Epic:       return 3;
        case SpecialTileRarity.Legendary:  return 3;
        case SpecialTileRarity.Normal:
        default:
            return 0;
    }
}
private void BuildSpecialTileTraitBonusForThisScoring(IList<string> concealed14Raw, IList<IList<string>> openMeldsRaw)
{
    _specialTileTraitLvBonusThisScoring.Clear();
    _specialTileTraitLvBonusTotalThisScoring = 0;

    var (geList, shList, iyList, _host) = GetCurrentSkillTraitYakuForScoring();
    var adopted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    void AddUnique(List<string> src)
    {
        if (src == null) return;

        for (int i = 0; i < src.Count; i++)
        {
            string s = NormalizeTraitJudgeYakuName_Local(src[i]);
            if (string.IsNullOrEmpty(s)) continue;

            if (s == "槍槓" || s == "嶺上開花")
                continue;

            adopted.Add(s);
        }
    }

    AddUnique(geList);
    AddUnique(shList);
    AddUnique(iyList);

    if (adopted.Count <= 0)
        return;

    Dictionary<string, int> equippedMap = null;

    try
    {
        equippedMap = SpecialTileSystem.GetEquippedTraitBonusMap();
    }
    catch
    {
        equippedMap = null;
    }

    if (equippedMap == null || equippedMap.Count <= 0)
        return;

    foreach (var kv in equippedMap)
    {
        string key = NormalizeTraitJudgeYakuName_Local(kv.Key);
        int addLv = Mathf.Max(0, kv.Value);

        if (string.IsNullOrEmpty(key) || addLv <= 0)
            continue;

        if (!adopted.Contains(key))
            continue;

        if (_specialTileTraitLvBonusThisScoring.TryGetValue(key, out int cur))
            _specialTileTraitLvBonusThisScoring[key] = cur + addLv;
        else
            _specialTileTraitLvBonusThisScoring[key] = addLv;

        _specialTileTraitLvBonusTotalThisScoring += addLv;
    }
}
private void BuildWall()
{
    var ids = new List<string>(200);
    try
    {
        var counts = PlayerData.GetDeckCountsCopy();
        for (int idx = 0; idx < 34; idx++)
        {
            int n = Mathf.Max(0, counts[idx]);
            string id = PlayerData.TileIdForIndex(idx);
            if (string.IsNullOrEmpty(id) || n <= 0) continue;
            for (int k = 0; k < n; k++) ids.Add(id);
        }
    }
    catch
    {
        // フォールバック：34種×各4枚（従来）
        string[] suits = { "Man","Pin","Sou" };
        foreach (var s in suits)
            for (int n=1; n<=9; n++)
                for (int k=0; k<4; k++) ids.Add($"{s}{n}");
        string[] honors = { "East","South","West","North","White","Green","Red" };
        foreach (var h in honors) for (int k=0; k<4; k++) ids.Add(h);
    }

    // ★追加：装備している特別牌は「足す」のではなく「ベース牌1枚と置換」する
    try { SpecialTileRuntime.ApplyEquippedToWallIds(ids); } catch { }

    for (int i = ids.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (ids[i], ids[j]) = (ids[j], ids[i]);
    }
    deck.Clear(); foreach (var id in ids) deck.Push(id);
}
    private void InitDoraIndicators(int count)
    {
        doraIndicators.Clear();
        uraIndicators.Clear();
        _uraIndicatorPool.Clear();

        // 仕様：表ドラ表示牌を先に取り出し、その直後の同枚数を「裏ドラ表示牌（保留）」として確保する。
        // これにより、同一局内で（先に片方が和了しても）表ドラ・裏ドラが変わらない。
        for (int i = 0; i < count && deck.Count > 0; i++) doraIndicators.Add(deck.Pop());
        for (int i = 0; i < count && deck.Count > 0; i++) _uraIndicatorPool.Add(deck.Pop());

        RefreshDoraUI();
    }

    private void AddKanIndicator()
    {
        if (deck.Count > 0)
        {
            doraIndicators.Add(deck.Pop());

            // カンで表ドラ表示牌が増えた分、対応する裏ドラ表示牌（保留）も1枚確保
            if (deck.Count > 0) _uraIndicatorPool.Add(deck.Pop());

            RefreshDoraUI();
        }
    }

    private void RefreshDoraUI()
    {
        // 仕様変更：ワン牌は「ドラ表示牌のみ」を表示する（裏向きの牌は出さない）
        if (!wanpaiArea) return;
        ClearChildren(wanpaiArea);
        // 並びは左から追加順。既存のdoraIndicatorsの内容をそのまま列挙して表示する
        try
        {
            // 1枚も無ければ何も描画しない
            if (doraIndicators == null || doraIndicators.Count == 0) return;

            // タイルの実寸を取得して軽く間隔を空ける
            var sampleRT = tilePrefab ? (tilePrefab.transform as UnityEngine.RectTransform) : null;
            float w = sampleRT ? Mathf.Max(1f, sampleRT.sizeDelta.x) : 64f;
            float gap = w * 0.05f; // 少しだけ間隔
            for (int i = 0; i < doraIndicators.Count; i++)
            {
                var go = UnityEngine.Object.Instantiate(tilePrefab, wanpaiArea);
                go.name = "DoraIndicator";
                SetTileSprite(go, doraIndicators[i]);
                if (go.TryGetComponent<UnityEngine.UI.Button>(out var b)) b.interactable = false;
                foreach (var img in go.GetComponentsInChildren<UnityEngine.UI.Image>(true)) img.raycastTarget = false;

                var rt = go.transform as UnityEngine.RectTransform;
                rt.anchorMin = new UnityEngine.Vector2(0f, 0.5f);
                rt.anchorMax = new UnityEngine.Vector2(0f, 0.5f);
                rt.pivot     = new UnityEngine.Vector2(0f, 0.5f);
                rt.anchoredPosition = new UnityEngine.Vector2(i * (w + gap), 0f);
            }
        }
        catch { /* UIだけなので失敗してもゲーム進行は止めない */ }

        // 右上などの小さな1枚用のdoraImageがあれば、最後の表示牌を載せておく
        if (doraImage)
        {
            if (doraIndicators != null && doraIndicators.Count > 0)
            {
                var last = doraIndicators[doraIndicators.Count - 1];
                var sp = Resources.Load<UnityEngine.Sprite>($"Sprites/Tiles/{last}");
                doraImage.sprite = sp;
                doraImage.enabled = (sp != null);
                doraImage.preserveAspect = true;
            }
            else
            {
                doraImage.enabled = false;
            }
        }
    }
private void ResetRoundStateForOpeningHand()
{
    // ★局内ターン数：敵のツモ番開始を 1ターン目として数える（敵ツモ番開始時に更新）
    _playerTsumoCountThisRound = 0;

    // ★敵側のターン基準も局開始で必ずリセット
    _enemyTurnCounter = 0;

    _pendingNextWinPointPenalty = 0;

    _legendaryDamageHalfTriggeredThisScoring = false;
    _legendaryHalfMpCostTriggeredThisScoring = false;

    _legendaryDamageHalfReservedSourceTiles.Clear();
    _legendaryHalfMpCostReservedSourceTiles.Clear();
    _legendaryDamageHalfTriggeredSourceTiles.Clear();
    _legendaryHalfMpCostTriggeredSourceTiles.Clear();

    // ★追加：天和/地和/人和の阻害条件（1ターン目暗槓）を局開始でリセット
    _playerDidAnkanOnFirstTurnThisHand = false;

    _playerHasWonThisHand = false;
    _enemyHasWonThisHand  = false;

    // ★追加：和了後表示維持用スナップショットを局開始で必ずクリア
    _playerWonHandSnapshot = null;
    _playerWonMeldsSnapshot = null;
    _enemyWonHandSnapshot = null;

    // ★追加：敵ターン多重実行ガードも局開始でリセット
    _enemyTurnRunning = false;

    // ★重要：スコアOK時に「勝ったターンのoffers(ツモ4枚)を捨て牌へ移す」処理の多重防止フラグを局開始で必ず戻す
    _continueAfterPlayerWin_AddedDiscardsThisScoring = false;

    hand.Clear(); offers.Clear(); discards.Clear(); enemyDiscards.Clear(); melds.Clear();
    // ★追加：捨て牌をクリアした局開始では、使用済みインデックスも必ずリセット
    enemyUsedIndices.Clear();
    // ★Add-on のロックもインスタンスID前提なので、局開始で安全にクリア
    _committedDiscardInstanceIDs.Clear();
    _enemyRonGreyPlayerDiscardIndices.Clear();
    selHand.Clear(); selOffer.Clear();
    selHand.Clear(); selOffer.Clear();
    isRiichi = false;
    isTenpai = false;

    // ★追加：流局後も待ち牌UIが残らないよう、局開始に入る時点で必ず消す
    UpdatePlayerTenpaiWaitsUI();

    // ★追加：次局演出が始まる時点で、前局のターン表示を残さない
    RefreshTopUI();

    // ★追加：リーチ宣言ターン捨て牌ハイライトも局開始で必ずリセット
    _playerRiichiDiscardHighlightIndex = -1;
    _enemyRiichiDiscardHighlightIndex = -1;

    // ★追加：ダブル立直／一発判定用リセット
    _playerIsDoubleRiichi = false;
    _playerIppatsuEligible = false;
    _playerRiichiDeclaredTsumoCountThisRound = -1;

    lastEnemyTurnTiles.Clear(); selectedEnemyIndex = -1;

    // ★追加：前局の「ロンで使った敵捨て牌」記録を必ずリセット
    _lastPlayerRonEnemyDiscardIndex = -1;
    _lastPlayerRonEnemyDiscardTileLogic = null;

    // ★追加：前局の「ツモで使ったoffersインデックス」を必ずリセット
    _lastPlayerTsumoOfferIndex = -1;

    enemyEffectAppliedIndices.Clear();
}

    private void DealOpeningHand()
    {
        // 共通のラウンド初期化
        ResetRoundStateForOpeningHand();

        // 一括で13枚配牌（通常の局開始用）
        for (int i = 0; i < 13 && deck.Count > 0; i++)
            hand.Add(deck.Pop());

        SortHand();
    }
private void __RecoverStalePlayerSkillBusyFlags()
{
    bool changed = false;

    if (_playerSkillCutinRunning)
    {
        if (playerSkillCutinRoot == null || !playerSkillCutinRoot.activeInHierarchy)
        {
            _playerSkillCutinRunning = false;
            changed = true;
        }
    }

    if (_playerSkillTransformRunning)
    {
        bool anyFxAlive = false;

        try
        {
            if (handArea != null)
            {
                for (int i = 0; i < handArea.childCount; i++)
                {
                    var tileTf = handArea.GetChild(i);
                    if (!tileTf) continue;

                    var fx = tileTf.Find("PlayerSkill_TrickFxOverlay");
                    if (fx != null && fx.gameObject.activeInHierarchy)
                    {
                        anyFxAlive = true;
                        break;
                    }
                }
            }
        }
        catch { }

        if (!anyFxAlive)
        {
            _playerSkillTransformRunning = false;
            changed = true;
        }
    }

    if (changed)
    {
        try { EnableAllHandButtons(true); } catch { }
        try { EnableAllOfferButtons(true); } catch { }
    }
}
private readonly List<int> _skillHandSelectionOrder = new List<int>();

private void UpdateSkillHandSelectionOrder(int index)
{
    _skillHandSelectionOrder.Remove(index);

    if (selHand != null && selHand.Contains(index))
    {
        _skillHandSelectionOrder.Add(index);
    }
}

private List<int> GetSkillHandSelectionOrder()
{
    var result = new List<int>();

    if (_skillHandSelectionOrder != null)
    {
        for (int i = 0; i < _skillHandSelectionOrder.Count; i++)
        {
            int idx = _skillHandSelectionOrder[i];
            if (selHand != null && selHand.Contains(idx) && idx >= 0 && idx < hand.Count)
            {
                if (!result.Contains(idx))
                    result.Add(idx);
            }
        }
    }

    if (selHand != null)
    {
        foreach (var idx in selHand)
        {
            if (idx >= 0 && idx < hand.Count && !result.Contains(idx))
                result.Add(idx);
        }
    }

    return result;
}
void BeginOfferPhase()
{
    // ★追加：多重実行ガード（スキップ連打やカットイン中の操作で2回呼ばれるのを防ぐ）
    if (_beginOfferPhaseInProgress) return;
    _beginOfferPhaseInProgress = true;

    // ★追加：敵ターンのリアクション用ハイライトは、次の自分ターン開始で必ず消す
    ClearEnemyDiscardHighlights_EndOfReactionTurn();
    // ★追加：敵ターンが終わって自分ターンに入ったのでガード解除
    _enemyTurnRunning = false;

    // ★重要：カットイン系の「進行ロック」フラグが何らかの理由で解除されず残ると、
    // 以降ずっと UpdateButtons() が [捨てる] を無効化して詰む。
    // 見た目のカットインが非表示なら、安全にロック解除して復帰させる。
    if (_enemySkillCutinRunning)
    {
        if (enemySkillCutinRoot == null || !enemySkillCutinRoot.activeInHierarchy)
        {
            _enemySkillCutinRunning = false;
        }
    }
    if (_enemyRiichiCutinRunning)
    {
        if (enemyRiichiCutinRoot == null || !enemyRiichiCutinRoot.activeInHierarchy)
        {
            _enemyRiichiCutinRunning = false;
        }
    }
    if (_playerRiichiCutinRunning)
    {
        if (playerRiichiCutinRoot == null || !playerRiichiCutinRoot.activeInHierarchy)
        {
            _playerRiichiCutinRunning = false;
        }
    }

    // ★追加：プレイヤースキル演出の busy フラグ残留もここで回復する
    __RecoverStalePlayerSkillBusyFlags();

    // ★追加：一発の有効期限（リーチ宣言後「次の自分ツモターン」まで）
    if (_playerIppatsuEligible && _playerRiichiDeclaredTsumoCountThisRound >= 0)
    {
        if (_playerTsumoCountThisRound > _playerRiichiDeclaredTsumoCountThisRound + 1)
        {
            _playerIppatsuEligible = false;
        }
    }

    // ★ターン数の表示を更新（●ターン目）
    RefreshTopUI();

    // ★追加：プレイヤーターン開始時の敵スキル処理（毒ダメージ・麻痺ターン経過）
    EnemySkills_OnPlayerTurnStart();

    // ★重要：毒/攻撃などでHPゲージ演出中（=凍結）なら、このターンの進行をここで止める。
    //         演出が終わったら「同じターンの続き」を再開する。
    if (_freezeProgression)
    {
        StartCoroutine(__BeginOfferPhase_WaitEnemyTurnStartEffectsThenContinue_Co());
        return;
    }

    __BeginOfferPhase_AfterEnemySkills();
}
private IEnumerator __BeginOfferPhase_WaitEnemyTurnStartEffectsThenContinue_Co()
{
    // 敵スキル（毒など）のHP演出が終わるまで待つ
    while (_freezeProgression)
        yield return null;

    // HP0なら既に敗北演出へ入っている想定なので何もしない
    if (Mathf.Max(0, playerHP) <= 0)
    {
        _beginOfferPhaseInProgress = false; // ★ガード解除
        yield break;
    }

    // 既にスコア/流局/敗北など別フェーズへ移っているなら何もしない
    if (phase == Phase.Scoring)
    {
        _beginOfferPhaseInProgress = false; // ★ガード解除
        yield break;
    }

    __BeginOfferPhase_AfterEnemySkills();
}
private void __BeginOfferPhase_AfterEnemySkills()
{
    // ★修正：敵ツモ番開始を1ターン目として数える仕様のため、
    // プレイヤーターン開始時点では「上限到達していても」そのターンはプレイヤーが行動できるようにする。
    // 流局判定は「次の敵ターンに入る直前」で行う（修正2）。
    if (_playerTsumoCountThisRound > Mathf.Max(1, maxTurnsPerHand))
    {
        _beginOfferPhaseInProgress = false; // ★ガード解除
        ShowRyukyoku();
        return;
    }

    offers.Clear();
    selOffer.Clear();
    int offersToDeal = 4;
    _pendingTsumoPenalty = 0; // 次の BeginOfferPhase で引く枚数を減らす（未使用）

    discardedThisTurn = 0;
    lastEnemyTurnTiles.Clear();
    selectedEnemyIndex = -1;

    phase = Phase.Offer;
    _beginOfferPhaseInProgress = false; // ★ガード解除（phase 変更後に解除して安全）
    suppressTsumoThisOffer = false;
    UpdateButtons();

    // まず空状態のUIを一度出す
    RefreshAll();

    // ★配牌を1枚ずつ入れて、その都度SE＆UI更新する
    StartCoroutine(__DealOfferTiles_Sequential_Co(offersToDeal));
}
private IEnumerator __DealOfferTiles_Sequential_Co(int offersToDeal)
{
    // スキル予約があれば先頭に置く（これも「入った瞬間」に鳴らす）
    if (!string.IsNullOrEmpty(_skillNextOfferTile))
    {
        offers.Add(_skillNextOfferTile);
        _skillNextOfferTile = null;

        try
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDealOfferTileSE();
            }
        }
        catch { }

        RefreshOfferUI();
        yield return new WaitForSeconds(0.05f);
    }

    int toDraw = Mathf.Min(offersToDeal - offers.Count, deck.Count);
    for (int i = 0; i < toDraw; i++)
    {
        offers.Add(deck.Pop());

        try
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDealOfferTileSE();
            }
        }
        catch { }

        RefreshOfferUI();
        yield return new WaitForSeconds(0.05f);
    }

    // ここから先は、従来 __BeginOfferPhase_AfterEnemySkills() の後半でやっていた内容
    UpdateButtons();

    // ★仕様変更：プレイヤーが既に和了済みなら、以降の自分ターンは自動で捨てる
    // （敵が和了するまで、または流局まで局を継続）
    if (_playerHasWonThisHand && !_enemyHasWonThisHand)
    {
        StartCoroutine(__AutoDiscardPlayerOfferAfterWin_Co());
        yield break;
    }
if (statusTMP) statusTMP.text = GetGameFixedText_Local("status_replace_to_tenpai_or_riichi");
    RefreshDiscardUI();

    ResetSelectionsAndUI();

    // ★MP UI 再接続：局切替や UI 再生成で参照が切れた場合の保険
    try { UpdateMpUI(); } catch { }

    TryRegenMP_TurnStart();
}
private System.Collections.IEnumerator __PlayDealOfferTilesSE_Co(int count)
{
    int n = Mathf.Max(0, count);

    for (int i = 0; i < n; i++)
    {
        try
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDealOfferTileSE();
            }
        }
        catch { }

        yield return new WaitForSeconds(0.05f);
    }
}
    // ===== Spec: after player already won, auto-discard on player's turns =====
private IEnumerator __AutoDiscardPlayerOfferAfterWin_Co()
{
    // 1 frame wait so UI is fully built
    yield return null;
    if (phase != Phase.Offer) yield break;
    if (!_playerHasWonThisHand || _enemyHasWonThisHand) yield break;

    // ★リーチ中の自動進行（AutoSkipOfferDuringRiichiIfNoWin(0.5f)）と同じテンポに合わせる
    const float AUTO_WAIT_SEC = 0.5f;
    yield return new WaitForSeconds(AUTO_WAIT_SEC);

    // ★凍結中（スコア表示中など）は絶対に次ターンへ進めない
    if (_freezeProgression) yield break;

    // OnClickConfirm() の Offer フェーズと同じ捨て処理
    var discardedIdsThisTurn = new List<string>();

    var thisTurnDiscardIndices = new List<int>();

    for (int i = 0; i < offers.Count; i++)
    {
        string id = offers[i];
        if (string.IsNullOrEmpty(id)) continue;

        discards.Add(id);
        discardedIdsThisTurn.Add(id);

        // “今ターン捨てたインデックス” を記録（敵の鳴き／ロン判定の都合）
        thisTurnDiscardIndices.Add(discards.Count - 1);
    }

    offers.Clear();
    selOffer.Clear();
    selHand.Clear();

    RefreshAll();

    // 敵がプレイヤー捨て牌でロンできるなら停止（＝敵和了 → スコアへ）
    // できないなら通常通り敵ターンへ
    bool enemyWillRon = EnemyAddon_TryRonOnPlayerDiscards(discardedIdsThisTurn);
    if (enemyWillRon) yield break;

    phase = Phase.EnemyTurn;
    UpdateButtons();
    RefreshAll();
    // ※このプロジェクトでは __EnemyTurn_Co は存在しないので、既存の遷移コルーチンを使う
    StartCoroutine(EnterEnemyTurnAfterPlayerAfterDelay(AUTO_WAIT_SEC));
}
private void ClearPlayerHandForContinueAfterWin()
{
    // ★このスコアOKで一度だけ「勝ったターンのツモ4枚」を捨て牌に移す（多重追加防止）
    if (!_continueAfterPlayerWin_AddedDiscardsThisScoring)
    {
        _continueAfterPlayerWin_AddedDiscardsThisScoring = true;

// ★和了牌は「スコア表示のラベル」だけでなく、「offersのインデックス」で特定する（重複ID対策）
_continueAfterPlayerWin_ExcludedTileId = _scoringUsedTileLabel;
_continueAfterPlayerWin_ExcludedDiscardIndex = -1;

if (offers != null && offers.Count > 0)
{
    int baseIndex = discards.Count;

    // ★この局で使った「offers内の和了インデックス」を優先する
    int excludedOfferIndex = (_lastPlayerTsumoOfferIndex >= 0 && _lastPlayerTsumoOfferIndex < offers.Count)
        ? _lastPlayerTsumoOfferIndex
        : -1;

    // discards に offers(ツモ4枚) を追加し、グレーアウト対象の「インデックス」を記録
    for (int i = 0; i < offers.Count; i++)
    {
        discards.Add(offers[i]);
    }

    // ★グレーアウト対象は「使った offers のインデックス」から一意に決める
    if (excludedOfferIndex >= 0)
    {
        _continueAfterPlayerWin_ExcludedTileId = offers[excludedOfferIndex];
        _continueAfterPlayerWin_ExcludedDiscardIndex = baseIndex + excludedOfferIndex;
    }
    else
    {
        // フォールバック：ID一致で「最初の1枚だけ」決める（複数除外しない）
        if (!string.IsNullOrEmpty(_continueAfterPlayerWin_ExcludedTileId))
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i] == _continueAfterPlayerWin_ExcludedTileId)
                {
                    _continueAfterPlayerWin_ExcludedDiscardIndex = baseIndex + i;
                    break;
                }
            }
        }
    }

    RefreshDiscardUI();

    // ★敵のロン/鳴き判定対象（和了牌だけは「その1枚だけ」除外する）
    try
    {
        var newlyDiscarded = new List<string>();

        if (excludedOfferIndex >= 0)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (i == excludedOfferIndex) continue;
                newlyDiscarded.Add(offers[i]);
            }
        }
        else
        {
            bool skippedOnce = false;
            for (int i = 0; i < offers.Count; i++)
            {
                var id = offers[i];
                if (!skippedOnce && !string.IsNullOrEmpty(_continueAfterPlayerWin_ExcludedTileId) && id == _continueAfterPlayerWin_ExcludedTileId)
                {
                    skippedOnce = true;
                    continue;
                }
                newlyDiscarded.Add(id);
            }
        }

        if (newlyDiscarded.Count > 0)
        {
            bool enemyWon = EnemyAddon_TryRonOnPlayerDiscards(newlyDiscarded);
        }
    }
    catch { }
}

    }

    // ★プレイヤー手牌は空にして局継続
    // ただし「表示」は残したいので、Clear 前にスナップショットを保持する
    if (_playerWonHandSnapshot == null) _playerWonHandSnapshot = new List<string>(hand);

    if (_playerWonMeldsSnapshot == null)
    {
        _playerWonMeldsSnapshot = new List<List<string>>();
        for (int i = 0; i < melds.Count; i++)
            _playerWonMeldsSnapshot.Add(new List<string>(melds[i]));
    }

    hand.Clear();
    melds.Clear();
    selHand.Clear();
    selOffer.Clear();

    // ここで一旦灰色化しておく（次の RefreshHandUI でも灰色化される）
    GreyOutTilesUnder(handArea);

    // ★重要：offers は“捨て牌化”したので空にする
    offers.Clear();

}
private void GreyOutTilesUnder(Transform root)
{
    if (!root) return;

    for (int i = 0; i < root.childCount; i++)
    {
        var tf = root.GetChild(i);
        SetTileGrey(tf, true);
        SetTileHighlight(tf, false);

        var btn = tf.GetComponent<UnityEngine.UI.Button>();
        if (btn) btn.interactable = false;
    }
}

private void RefreshTopUI()
{
        try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}
    try
{
    var shownEnemyName = GetCurrentEnemyNameFromExcelWithLoop();
    if (!string.IsNullOrEmpty(shownEnemyName))
    {
        SetEnemyNameOnUI(shownEnemyName);
    }
}
catch
{
}
    // 東〇局／南〇局（「（親/子）」の表記を削除）
    if (roundTMP)
        roundTMP.text = BuildRoundLabelForUI();

    // 現在ターン数/最大ターン数 単位
    if (turnTMP)
    {
        int turn = Mathf.Max(1, _playerTsumoCountThisRound);
        int maxTurn = Mathf.Max(1, maxTurnsPerHand);
        turnTMP.text = $"{turn}/{maxTurn} {GetGameFixedText_Local("turn_unit")}";
    }
    // 自風（東家／南家／西家／北家）を GetPlayerSeatWind() から反映
    if (playerSeatTMP)
    {
        // GetPlayerSeatWind() は "East" / "South" / "West" / "North" を返す実装になっている
        string seat = GetPlayerSeatWind();
        string seatLabel;
        switch (seat)
        {
    case "East":  seatLabel = GetGameFixedText_Local("seat_east"); break;
    case "South": seatLabel = GetGameFixedText_Local("seat_south"); break;
    case "West":  seatLabel = GetGameFixedText_Local("seat_west"); break;
    case "North": seatLabel = GetGameFixedText_Local("seat_north"); break;
    default:      seatLabel = GetGameFixedText_Local("seat_east"); break;
        }
        playerSeatTMP.text = seatLabel;
    }

    // 敵の自風（プレイヤーの下家）
    if (enemySeatTMP)
    {
        string seat = GetEnemySeatWind();
        string seatLabel;
        switch (seat)
        {
    case "East":  seatLabel = GetGameFixedText_Local("seat_east"); break;
    case "South": seatLabel = GetGameFixedText_Local("seat_south"); break;
    case "West":  seatLabel = GetGameFixedText_Local("seat_west"); break;
    case "North": seatLabel = GetGameFixedText_Local("seat_north"); break;
    default:      seatLabel = GetGameFixedText_Local("seat_south"); break;
        }
        enemySeatTMP.text = seatLabel;
    }
runGold = GameManager.RunCurrency.Get();
if (scoreTMP)
    scoreTMP.text = $"{GetGameFixedText_Local("score_label")}  {runScore:N0}          {runGold:N0}";

    // Keep HP UI always visible during match
    if (playerHPTMP && !playerHPTMP.gameObject.activeSelf) playerHPTMP.gameObject.SetActive(true);
    if (enemyHPTMP  && !enemyHPTMP .gameObject.activeSelf) enemyHPTMP .gameObject.SetActive(true);
    if (playerHPBar && !playerHPBar.gameObject.activeSelf) playerHPBar.gameObject.SetActive(true);
    if (enemyHPBar  && !enemyHPBar .gameObject.activeSelf) enemyHPBar .gameObject.SetActive(true);

    UpdateHpUI();

    // ★追加：右側3TMP（スキル/お守り/お札）を毎回更新して崩れを止める
    UpdateRightInfoUI_Manual();
}
private string BuildOfudaInfoText_Manual()
{
    try
    {
        // 装備中お札ID
        var ids = OfudaRunInventory.LoadList();
        if (ids == null || ids.Count == 0)
            return "";

        // Excel -> defs
        var cat = OfudaExcelLoader.Load();
        var defs = OfudaCatalog.BuildFromExcel(cat);
        if (defs == null || defs.Count == 0)
            return "";

        var map = defs.ToDictionary(d => d.id, d => d);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool any = false;

        foreach (var id in ids)
        {
            if (!map.TryGetValue(id, out var def) || def == null)
                continue;

            string rarityLine = ColorizeRarityWord_NoBrackets(def.displayName, def.rarity);
            string nameLine = StripRarityPrefixBracket(def.displayName).Trim();
            string descLine = (def.description ?? "").Trim();

            if (any) sb.Append("\n\n");
            sb.Append(rarityLine);
            sb.Append("\n");
            sb.Append(nameLine);

            if (!string.IsNullOrEmpty(descLine))
            {
                sb.Append("\n");
                sb.Append(descLine);
            }

            any = true;
        }

        return any ? sb.ToString() : "";
    }
    catch
    {
        return "";
    }
}
private static string StripRarityPrefixBracket(string displayName)
{
    if (string.IsNullOrEmpty(displayName))
        return displayName;

    if (!displayName.StartsWith("【", StringComparison.Ordinal))
        return displayName;

    int end = displayName.IndexOf("】", StringComparison.Ordinal);
    if (end < 0)
        return displayName;

    return displayName.Substring(end + 1);
}
private static string ColorizeRarityWord_NoBrackets(string displayName, string rarityFallback)
{
    string shownText = null;

    // 表示する文字列だけは displayName 側のローカライズ済み表記を使う
    if (!string.IsNullOrEmpty(displayName) && TryParseRarityPrefix(displayName, out var rarityInName))
        shownText = rarityInName;

    if (string.IsNullOrEmpty(shownText))
        shownText = rarityFallback;

    if (string.IsNullOrEmpty(shownText))
        return "";

    // 色判定は常に内部値 def.rarity を正とする
    var c = GetRarityColorSafe(rarityFallback, rarityFallback);
    string hex = ColorUtility.ToHtmlStringRGB(c);
    return $"<color=#{hex}>{shownText}</color>";
}
private static string ColorizeRarityPrefix(string displayName, string rarityFallback)
{
    if (string.IsNullOrEmpty(displayName))
        return displayName;

    int start = displayName.IndexOf("【", StringComparison.Ordinal);
    int end = displayName.IndexOf("】", StringComparison.Ordinal);

    if (start != 0 || end <= start)
        return displayName;

    string prefix = displayName.Substring(0, end + 1);
    string rest = displayName.Substring(end + 1);

    // 色判定は常に内部値 def.rarity を正とする
    string hex = ColorUtility.ToHtmlStringRGB(GetRarityColorSafe(rarityFallback, rarityFallback));
    return $"<color=#{hex}>{prefix}</color>{rest}";
}
private void RefreshAll()
{
    RefreshHandUI();
    RefreshOfferUI();
    RefreshDiscardUI();
    RefreshEnemyDiscardUI();

    UpdateEnemyRiichiStatusUI();

    // ★追加：敵捨て牌UIを作り直すとハイライトが消えるので、この条件のとき必ず再配線
    if (phase == Phase.EnemyTurn && lastEnemyTurnTiles != null && lastEnemyTurnTiles.Count > 0)
        WireEnemyTurnClickTargets();

    RefreshMeldUI();
    UpdateButtons();

    EvaluateWinUI_New();
    
    // 和了者の手牌は「表示維持＋グレー固定」
    if (_playerHasWonThisHand) GreyOutTilesUnder(handArea);

    if (_enemyHasWonThisHand)
    {
        // 非表示にされてしまう経路があるなら、勝っている間は強制表示
        try { if (enemyTenpaiHandManualRoot) enemyTenpaiHandManualRoot.gameObject.SetActive(true); } catch { }
        try { if (enemyMeldsManualRoot) enemyMeldsManualRoot.gameObject.SetActive(true); } catch { }

        // ★敵の手牌グレーアウトは「スコアOK後」だけ
        if (_enemyGreyOutHandAfterScoreOk)
        {
            try { GreyOutTilesUnder(enemyTenpaiHandManualRoot); } catch { }
            try { GreyOutTilesUnder(enemyMeldsManualRoot); } catch { }
        }
    }
}
void RefreshHandUI()
{
    UpdatePlayerHandAreaPositionByMeldState();

    var srcHand =
        (_playerHasWonThisHand && _playerWonHandSnapshot != null)
            ? _playerWonHandSnapshot
            : hand;

    ClearChildren(handArea);

    for (int i = 0; i < srcHand.Count; i++)
    {
        var go = Instantiate(tilePrefab, handArea);
        SetupTile(go, srcHand[i], i, isHand: true);

        if (debugFeatureEnabled && enableDebugMode && !_playerHasWonThisHand)
            DBG_AttachHandEditHook(go, i);
    }

    RebuildRaiseOverlays(handArea, selHand, srcHand);
    ApplyDoraHighlights(handArea, srcHand);

    if (_playerHasWonThisHand) GreyOutTilesUnder(handArea);

    UpdateShantenUI();
}
private void RefreshOfferUI()
{
    // ★追加：ツモ4枚の表示が「中央→左右に広がる」にならないよう、常に左詰めに固定する
    try
    {
        if (offerArea != null)
        {
            var hlg = offerArea.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childAlignment = TextAnchor.MiddleLeft;

                // ★重要：左右反転（見た目だけ順番が逆になる）を必ず解除する
                // これが true のままだと、offers[i] と offerArea.GetChild(i) が一致せず、黄色ハイライトがズレる
                hlg.reverseArrangement = false;
            }
        }
    }
    catch { }

    ClearChildren(offerArea);
    for (int i = 0; i < offers.Count; i++)
    {
        var go = Instantiate(tilePrefab, offerArea);
        SetupTile(go, offers[i], i, isOffer: true);
    }

    RebuildRaiseOverlays(offerArea, selOffer, offers);
    ApplyDoraHighlights(offerArea, offers);

    // ★重要：
    // 通常ターンのツモ4枚生成後にも、和了対象牌の黄色ハイライトを毎回再適用する
    RefreshOfferWinningHighlights();
}
private void RefreshDiscardUI()
{
    for (int i = discardArea.childCount - 1; i >= 0; i--) Destroy(discardArea.GetChild(i).gameObject);

    for (int i = 0; i < discards.Count; i++)
    {
        var go = Instantiate(tilePrefab, discardArea);

        // ★重要：tilePrefab の正しい描画（Art/Image）＆ルートImage透明化を通す
        SetupTile(go, discards[i], i, isHand:false, isOffer:false, clickable:false);

        go.name = $"PlayerDiscard_{i}_{discards[i]}";

        bool grey =
            _enemyRonGreyPlayerDiscardIndices.Contains(i) ||
            (i == _continueAfterPlayerWin_ExcludedDiscardIndex);

        SetTileGrey(go.transform, grey);
        SetTileHighlight(go.transform, false);

        // ★追加：リーチ宣言ターンの左端捨て牌をオレンジでハイライト（リーチ中のみ）
        bool riichiDisc =
            isRiichi &&
            _playerRiichiDiscardHighlightIndex >= 0 &&
            i == _playerRiichiDiscardHighlightIndex;

        SetTileRiichiDiscardHighlight(go.transform, riichiDisc);
    }
}
private bool IsChiitoitsuShape(List<string> tiles14)
{
    if (tiles14 == null) return false;
    if (tiles14.Count != 14) return false;

    var counts = tiles14.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

    if (counts.Count != 7) return false;

    foreach (var kv in counts)
    {
        if (kv.Value != 2) return false;
    }

    return true;
}

private bool IsAnyWinningShape(List<string> tiles14)
{
    if (tiles14 == null) return false;
    if (tiles14.Count % 3 != 2) return false;

    if (IsStandardWin(tiles14)) return true;
    if (IsChiitoitsuShape(tiles14)) return true;

    return false;
}
private void RefreshEnemyDiscardUI()
{
    ClearChildren(enemyDiscardArea);

    for (int i = 0; i < enemyDiscards.Count; i++)
    {
        var go = Instantiate(tilePrefab, enemyDiscardArea);
        SetupTile(go, enemyDiscards[i], -1, clickable:false);
        go.name = $"EnemyDiscard_{i}_{enemyDiscards[i]}";

        var btn = go.GetComponent<Button>();
        if (btn) btn.interactable = !enemyUsedIndices.Contains(i);

        if (enemyUsedIndices.Contains(i)) SetTileGrey(go.transform, true);

        // ★追加：敵のリーチ宣言ターンの左端捨て牌をオレンジでハイライト（敵がリーチ中のみ）
        bool riichiDisc =
            _enemyIsRiichi &&
            _enemyRiichiDiscardHighlightIndex >= 0 &&
            i == _enemyRiichiDiscardHighlightIndex;

        SetTileRiichiDiscardHighlight(go.transform, riichiDisc);
    }

    WireEnemyTurnClickTargets();
    RefreshEnemySelectionLift();

    EvaluateWinUI_New();
}
private void UpdateEnemyRiichiStatusUI()
{
    bool show =
        (_enemyIsRiichi) &&
        (phase != Phase.Scoring) &&
        (!_enemyHasWonThisHand);

    if (enemyRiichiStatusTMP != null)
    {
        enemyRiichiStatusTMP.gameObject.SetActive(show);

        if (show)
        {
            enemyRiichiStatusTMP.text = GetGameFixedText_Local("yaku.riichi_short");
            enemyRiichiStatusTMP.color = enemyRiichiStatusColor;
        }
        else
        {
            enemyRiichiStatusTMP.text = "";
        }
    }

    if (enemyRiichiStatusBGObject != null)
    {
        enemyRiichiStatusBGObject.SetActive(show);
    }
}
private void RefreshMeldUI()
{
    if (IsCustomMeldLayoutReady())
    {
        RefreshMeldUI_CustomSlots();
        UpdatePlayerHandAreaPositionByMeldState();
        return;
    }

    RefreshMeldUI_Legacy();
    UpdatePlayerHandAreaPositionByMeldState();
}
private void RefreshMeldUI_CustomSlots()
{
    // Slot側を全クリア
    for (int i = 0; i < playerMeldSlots.Length; i++)
    {
        if (playerMeldSlots[i] == null) continue;
        ClearChildren(playerMeldSlots[i]);
    }

    // ★和了後は snapshot を優先して表示維持
    var srcMelds =
        (_playerHasWonThisHand && _playerWonMeldsSnapshot != null)
            ? _playerWonMeldsSnapshot
            : melds;

    int slotIndex = 0;

    foreach (var m in srcMelds)
    {
        if (m == null || m.Count == 0) continue;

        // 空スロットを探す
        RectTransform slot = null;
        while (slotIndex < playerMeldSlots.Length)
        {
            if (playerMeldSlots[slotIndex] != null)
            {
                slot = playerMeldSlots[slotIndex];
                break;
            }
            slotIndex++;
        }
        if (slot == null) break; // 置き場が無いなら以降は表示しない（最大4メンツ想定）

        // --- 面子の種類を判定（既存ロジックと同じ） ---
        bool isKan = (m.Count == 4);
        bool anyStar = m.Any(x => x != null && x.EndsWith("*"));

        if (isKan && m.Count > 0)
        {
            string coreNoStar = (m[0] != null && m[0].EndsWith("*")) ? m[0].Substring(0, m[0].Length - 1) : m[0];
            string coreLogic = StripTileIdForLogic(coreNoStar);

            if (m.Any(x =>
            {
                if (x == null) return true;

                string tileNoStar = x.EndsWith("*") ? x.Substring(0, x.Length - 1) : x;
                string tileLogic = StripTileIdForLogic(tileNoStar);

                return tileLogic != coreLogic;
            }))
            {
                isKan = false;
            }
        }

        bool isAnkan = isKan && !anyStar; // アンカン＝4枚同一かつ「*」無し

        // === 表示用にチー/ポンの“横向きの位置”を統一（既存ロジックと同じ） ===
        var dm = new List<string>(m);
        int rotatedIndex = dm.FindIndex(x => x != null && x.EndsWith("*"));

        bool isPon =
            (dm.Count == 3 &&
             StripStar(dm[0]) == StripStar(dm[1]) &&
             StripStar(dm[1]) == StripStar(dm[2]));

        bool isChi = false;
        if (dm.Count == 3)
        {
            if (TryParseSuitNum(StripStar(dm[0]), out var s0, out var n0) &&
                TryParseSuitNum(StripStar(dm[1]), out var s1, out var n1) &&
                TryParseSuitNum(StripStar(dm[2]), out var s2, out var n2) &&
                s0 == s1 && s1 == s2)
            {
                var seq = new int[] { n0, n1, n2 };
                Array.Sort(seq);
                isChi = (seq[1] == seq[0] + 1) && (seq[2] == seq[1] + 1);
            }
        }

        // --- チー：拾い牌（星付き）を左端へ固定 ---
        if (isChi && rotatedIndex >= 0 && rotatedIndex < dm.Count)
        {
            var starTile = dm[rotatedIndex];
            dm.RemoveAt(rotatedIndex);
            dm.Insert(0, starTile);
            rotatedIndex = 0;
        }

        if (isPon && dm.Count >= 3)
        {
            int starIdx = dm.FindIndex(x => x != null && x.EndsWith("*"));

            if (starIdx < 0)
            {
                dm[1] = StripStar(dm[1]) + "*";
                rotatedIndex = 1;
            }
            else
            {
                if (starIdx != 1)
                {
                    var tmp = dm[1];
                    dm[1] = dm[starIdx];
                    dm[starIdx] = tmp;
                }
                rotatedIndex = 1;
            }
        }
// 種類に応じたレイアウト設定（アンカン/ミンカンを区別）
MeldLayoutKind kind = MeldLayoutKind.Chi;
if (isKan)
{
    kind = isAnkan ? MeldLayoutKind.Kan_Ankan : MeldLayoutKind.Kan_Minkan;
}
else if (isPon)
{
    kind = MeldLayoutKind.Pon;
}
else
{
    kind = MeldLayoutKind.Chi;
}

var cfg = GetLayoutConfig(kind);


        // --- 描画（slot 直下に absolute 配置） ---
        for (int j = 0; j < dm.Count; j++)
        {
            var id = dm[j];
            var go = Instantiate(tilePrefab, slot);

            var disp = id != null && id.EndsWith("*") ? id.Substring(0, id.Length - 1) : id;

            if (isAnkan && (j == 0 || j == dm.Count - 1))
            {
                SetupTile(go, disp, -1, clickable: false);
                TrySetBackSprite(go);
            }
            else
            {
                SetupTile(go, disp, -1, clickable: false);
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                // 位置（左から何番目…）
                if (cfg.tilePositions != null && cfg.tilePositions.Length >= 4)
                    rt.anchoredPosition = cfg.tilePositions[Mathf.Clamp(j, 0, 3)];
                else
                    rt.anchoredPosition = Vector2.zero;

                // サイズ（種類別）
                rt.sizeDelta = cfg.tileSize;

                // スケール（種類別）
                float sc = Mathf.Max(0.01f, cfg.tileScale);
                rt.localScale = new Vector3(sc, sc, 1f);
            }

            // 「*」は横向き
            if (id != null && id.EndsWith("*"))
                go.transform.localEulerAngles = new Vector3(0, 0, 90f);
            else
                go.transform.localEulerAngles = Vector3.zero;
        }

        // ★和了後は副露も灰色固定（slot単位で）
        if (_playerHasWonThisHand) GreyOutTilesUnder(slot);

        slotIndex++;
    }
}
private void ApplyPlayerTenpaiWaitTileSizeIfNeeded(GameObject tileGo)
{
    if (!overridePlayerTenpaiWaitTileSize) return;
    if (tileGo == null) return;

    // 1) 牌のルートRectTransform
    var rt = tileGo.GetComponent<RectTransform>();
    if (rt != null)
    {
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, playerTenpaiWaitTileSize.x);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, playerTenpaiWaitTileSize.y);
    }

    // 2) LayoutGroup配下でもサイズが反映されるよう LayoutElement も上書き
    var le = tileGo.GetComponent<UnityEngine.UI.LayoutElement>();
    if (le != null)
    {
        le.preferredWidth  = playerTenpaiWaitTileSize.x;
        le.preferredHeight = playerTenpaiWaitTileSize.y;
        le.minWidth        = playerTenpaiWaitTileSize.x;
        le.minHeight       = playerTenpaiWaitTileSize.y;
    }

    // 3) Prefab構造によっては「見た目」のRectTransformが子にあり、ルートを変えても見た目が変わらない場合がある
    //    そのため Art / Art/Image も同サイズにしておく（副作用が少ない範囲）
    var artTf = tileGo.transform.Find("Art");
    if (artTf)
    {
        var artRt = artTf.GetComponent<RectTransform>();
        if (artRt != null)
        {
            artRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, playerTenpaiWaitTileSize.x);
            artRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, playerTenpaiWaitTileSize.y);
        }

        var imgTf = artTf.Find("Image");
        if (imgTf)
        {
            var imgRt = imgTf.GetComponent<RectTransform>();
            if (imgRt != null)
            {
                imgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, playerTenpaiWaitTileSize.x);
                imgRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, playerTenpaiWaitTileSize.y);
            }

            // AspectRatioFitter が付いているとサイズが勝手に戻ることがあるので、待ち牌表示では無効化する
            var arf = imgTf.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (arf != null) arf.enabled = false;
        }
    }
}

private void RefreshMeldUI_Legacy()
{
    if (!meldArea) return;
    ClearChildren(meldArea);

    // ★和了後は snapshot を優先して表示維持
    var srcMelds =
        (_playerHasWonThisHand && _playerWonMeldsSnapshot != null)
            ? _playerWonMeldsSnapshot
            : melds;

    foreach (var m in srcMelds)
    {
        // --- 面子の種類を判定 ---
        bool isKan = (m != null && m.Count == 4);
        string coreId = null;
        bool anyStar = false;
        if (m != null && m.Count > 0)
        {
            // ★修正：特別牌混在でも崩れないよう、ロジック用同一視（StripTileIdForLogic）で比較する
            string coreNoStar = (m[0] != null && m[0].EndsWith("*")) ? m[0].Substring(0, m[0].Length - 1) : m[0];
            string coreLogic = StripTileIdForLogic(coreNoStar);

            anyStar = m.Any(x => x != null && x.EndsWith("*"));

            if (isKan && m.Any(x =>
            {
                if (x == null) return true;

                string tileNoStar = x.EndsWith("*") ? x.Substring(0, x.Length - 1) : x;
                string tileLogic = StripTileIdForLogic(tileNoStar);

                return tileLogic != coreLogic;
            }))
            {
                isKan = false;
            }

        }
        bool isAnkan = isKan && !anyStar; // アンカン＝4枚同一かつ「*」無し

        // === 表示用にチー/ポンの“横向きの位置”を統一 ===
        var dm = new List<string>(m); // m を壊さない表示用コピー
        int rotatedIndex = dm.FindIndex(x => x != null && x.EndsWith("*"));

        // ポン判定（同牌3枚：特別牌混在でも StripStar 後が同じならOK）
        bool isPon =
            (dm.Count == 3 &&
             StripStar(dm[0]) == StripStar(dm[1]) &&
             StripStar(dm[1]) == StripStar(dm[2]));

        // チー判定（同色連番 3 枚）
        bool isChi = false;
        if (dm.Count == 3)
        {
            if (TryParseSuitNum(StripStar(dm[0]), out var s0, out var n0) &&
                TryParseSuitNum(StripStar(dm[1]), out var s1, out var n1) &&
                TryParseSuitNum(StripStar(dm[2]), out var s2, out var n2) &&
                s0 == s1 && s1 == s2)
            {
                var seq = new int[] { n0, n1, n2 };
                Array.Sort(seq);
                isChi = (seq[1] == seq[0] + 1) && (seq[2] == seq[1] + 1);
            }
        }

        // --- チー：拾い牌（星付き）を左端へ固定 ---
        if (isChi && rotatedIndex >= 0 && rotatedIndex < dm.Count)
        {
            var starTile = dm[rotatedIndex];
            dm.RemoveAt(rotatedIndex);
            dm.Insert(0, starTile);
            rotatedIndex = 0;
        }

        if (isPon && dm.Count >= 3)
        {
            // ★特別牌IDを潰さない：表示用の正規化で tileId を置換しない
            // ただし横向き（*）は中央に来るように並べ替える

            int starIdx = dm.FindIndex(x => x != null && x.EndsWith("*"));

            if (starIdx < 0)
            {
                // 念のため：* が無ければ中央を横向きにする
                dm[1] = StripStar(dm[1]) + "*";
                rotatedIndex = 1;
            }
            else
            {
                // * を中央へ移動（tileId自体は保持）
                if (starIdx != 1)
                {
                    var tmp = dm[1];
                    dm[1] = dm[starIdx];
                    dm[starIdx] = tmp;
                }
                rotatedIndex = 1;
            }
        }

        // --- 描画 ---
        for (int j = 0; j < dm.Count; j++)
        {
            var id = dm[j];
            var go = Instantiate(tilePrefab, meldArea);

            var disp = id != null && id.EndsWith("*") ? id.Substring(0, id.Length - 1) : id;

            // アンカンは両端だけ裏面
            if (isAnkan && (j == 0 || j == dm.Count - 1))
            {
                SetupTile(go, disp, -1, clickable: false);
                TrySetBackSprite(go);
            }
            else
            {
                SetupTile(go, disp, -1, clickable: false);
            }

            // 「*」は横向き
            if (id != null && id.EndsWith("*"))
                go.transform.localEulerAngles = new Vector3(0, 0, 90f);
        }

        // 軽い区切り
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        var le = spacer.GetComponent<LayoutElement>(); le.minWidth = 10f; le.preferredWidth = 10f;
        spacer.transform.SetParent(meldArea, false);
    }
    // ★和了後は副露も灰色固定
    if (_playerHasWonThisHand) GreyOutTilesUnder(meldArea);
}

// --- 追加：裏面スプライトを当てる（無ければ薄く表示） ---
private void TrySetBackSprite(GameObject go)
{
    if (!go) return;
    var art = FindArtImage(go.transform);
    Sprite sp = null;
    try
    {
        var name = string.IsNullOrEmpty(backTileSpriteName) ? "Back" : backTileSpriteName;
        sp = Resources.Load<Sprite>($"Sprites/Tiles/{name}");
        if (!sp) sp = Resources.Load<Sprite>($"Sprites/Tiles/TileBack");
    } catch {}

    if (art && sp)
    {
        art.sprite = sp;
        art.enabled = true;
        art.color = Color.white;
    }
    else if (art) // フォールバック：薄くして“裏”っぽくする
    {
        art.color = new Color(1f,1f,1f,0.15f);
    }
}

private void EvaluateWinUI_New()
{
    canRonNow = false; canTsumoNow = false;
    if (phase == Phase.ChoosingCall) return; // do not override call UI

    // Ron: during enemy turn, check the last enemy tiles directly
    canRonNow = (phase == Phase.EnemyTurn) && CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);

    // Tsumo: during our offer/draw
if (phase == Phase.Offer && !suppressTsumoThisOffer)
{
    bool hasOfferWin = offers.Any(o => CanTsumoWith(o, out _, out _, out _, out _));
    bool hasSelectedWin = false;
    if (TryGetSelectedTsumoTile(out var selId))
        hasSelectedWin = CanTsumoWith(selId, out _, out _, out _, out _);
    canTsumoNow = hasOfferWin || hasSelectedWin;

    // NEW: リーチ中で和了牌が無いなら、自動で「捨てる確定」を予約
    // ★仕様：自動スキップ時の待ち時間は 0.5 秒
    if (isRiichi && !canTsumoNow)
        AutoSkipOfferDuringRiichiIfNoWin(0.5f);
}
    // メインの確定ボタンは常に「捨てる」固定。
    // ツモは専用ボタン(btnTsumo)でのみ表示する。
    if (confirmTMP && phase != Phase.Scoring)
    {
        confirmTMP.text = GetGameFixedText_Local("confirm_discard");
    }
}
    private void ResetSelectionsAndUI()
    {
        selHand.Clear(); selOffer.Clear(); selectedEnemyIndex = -1;
        RebuildRaiseOverlays(handArea, selHand, hand);
        RebuildRaiseOverlays(offerArea, selOffer, offers);
        RefreshEnemySelectionLift();
        UpdateButtons();
    
        if (phase != Phase.ChoosingCall) EnableAllHandButtons(true);
        WireEnemyTurnClickTargets();
    }
private void UpdateButtons()
{
    EnsureBottomButtons();

    if ((!btnMenuOption || !btnMenuSuspend || !btnMenuExit || !btnMenuClose) && menuPanel)
    {
        EnsureMenuPanelWiring(false);
    }
    else
    {
        EnsureMenuInnerButtonsAlwaysActive();
    }

    __RecoverStalePlayerSkillBusyFlags();

    // ★重要：見た目（menuPanelの表示）と内部フラグ（isMenuOpen）のズレを毎回補正する
    // これがズレると、メニュー内ボタンが interactable=false に戻されて押せなくなる
    isMenuOpen = IsMenuPanelVisibleState();
    bool isScoring = (phase == Phase.Scoring);

    // ★追加：プレイヤースキル（カットイン開始〜変換演出終了まで）は「全てのボタン操作」を無効化
    bool isPlayerSkillBusy = (_playerSkillCutinRunning || _playerSkillTransformRunning);

    // ★追加：無効中でもボタンの見た目は暗くしない
    SetNoDimButtons_Temporary(isPlayerSkillBusy);

    // ★追加：MP減少演出中（スキルMP消費／敵妨害MPダメージ）はボタンをロックし、見た目は暗くしない
    bool isMpDecreaseBusy = _mpDecreaseAnimRunning;
    if (isPlayerSkillBusy || isMpDecreaseBusy)
    {
        // この場面だけ「暗くしない」
        SetNoDimButtons_Temporary(true);

        if (btnConfirm)       btnConfirm.interactable       = false;
        if (btnSkill)         btnSkill.interactable         = false;
        if (btnSkip)          btnSkip.interactable          = false;
        if (btnTenpaiConfirm) btnTenpaiConfirm.interactable = false;
        if (btnRiichi)        btnRiichi.interactable        = false;
        if (btnPon)           btnPon.interactable           = false;
        if (btnChi)           btnChi.interactable           = false;
        if (btnKan)           btnKan.interactable           = false;
        if (btnKanFromHand)   btnKanFromHand.interactable   = false;
        if (btnRon)           btnRon.interactable           = false;
        if (btnRonSkip)       btnRonSkip.interactable       = false;
        if (scoringOKButton)  scoringOKButton.interactable  = false;

        // ★重要：
        // メニュー系は「スキル使用による無効化」の対象外。
        // ここで明示的に復旧・維持して、Inactive / disabled の残留を潰す。
        EnsureMenuInnerButtonsAlwaysActive();

        if (btnMenu)          btnMenu.interactable          = true;
        bool menuInner = isMenuOpen;
        if (btnMenuOption)    btnMenuOption.interactable    = menuInner;
        if (btnMenuSuspend)   btnMenuSuspend.interactable   = menuInner;
        if (btnMenuExit)      btnMenuExit.interactable      = menuInner;
        if (btnMenuClose)     btnMenuClose.interactable     = menuInner;

        return;
    }
    else
    {
        // 通常に戻ったら「暗くしない」の退避を解除
        SetNoDimButtons_Temporary(false);
    }

    // ★変更：リーチカットイン（プレイヤー／敵）＋敵スキルカットイン再生中は全ボタンをロック
    bool isAnyCutinRunning =
        _playerRiichiCutinRunning
        || _enemyRiichiCutinRunning
        || _enemySkillCutinRunning;

    if (isAnyCutinRunning)
    {
        if (btnConfirm)       btnConfirm.interactable       = false;
        if (btnSkill)         btnSkill.interactable         = false;

        // ★例外：敵捨て牌リアクション中（EnemyTurn/ChoosingCall）は、選択無しでも「スキップ」だけ押せるようにする
        if (btnSkip)
        {
            bool allowSkipDuringCutin = (phase == Phase.EnemyTurn || phase == Phase.ChoosingCall);
            btnSkip.gameObject.SetActive(allowSkipDuringCutin && phase != Phase.Scoring);
            btnSkip.interactable = allowSkipDuringCutin && phase != Phase.Scoring;
            if (skipTMP) skipTMP.text = GetGameFixedText_Local("button_skip");
        }

        if (btnTenpaiConfirm) btnTenpaiConfirm.interactable = false;
        if (btnRiichi)        btnRiichi.interactable        = false;
        if (btnPon)           btnPon.interactable           = false;
        if (btnChi)           btnChi.interactable           = false;
        if (btnKan)           btnKan.interactable           = false;
        if (btnKanFromHand)   btnKanFromHand.interactable   = false;
        if (btnRon)           btnRon.interactable           = false;
        if (btnRonSkip)       btnRonSkip.interactable       = false;
        if (scoringOKButton)  scoringOKButton.interactable  = false;

        // ★重要：
        // カットイン中でもメニュー内ボタンは残す。
        EnsureMenuInnerButtonsAlwaysActive();

        if (btnMenu)          btnMenu.interactable          = true;
        bool menuInner = isMenuOpen;
        if (btnMenuOption)    btnMenuOption.interactable    = menuInner;
        if (btnMenuSuspend)   btnMenuSuspend.interactable   = menuInner;
        if (btnMenuExit)      btnMenuExit.interactable      = menuInner;
        if (btnMenuClose)     btnMenuClose.interactable     = menuInner;

        return;
    }

    // ★仕様変更：プレイヤーがこの局で既に和了している場合、以降の自分操作は不要なのでロック
    // （BeginOfferPhase が自動捨て→敵ターンへ進める）
    if (_playerHasWonThisHand && !_enemyHasWonThisHand)
    {
        if (btnConfirm)       btnConfirm.interactable       = false;
        if (btnSkill)         btnSkill.interactable         = false;
        if (btnSkip)          btnSkip.interactable          = false;
        if (btnTenpaiConfirm) btnTenpaiConfirm.interactable = false;
        if (btnRiichi)        btnRiichi.interactable        = false;
        if (btnPon)           btnPon.interactable           = false;
        if (btnChi)           btnChi.interactable           = false;
        if (btnKan)           btnKan.interactable           = false;
        if (btnKanFromHand)   btnKanFromHand.interactable   = false;
        if (btnRon)           btnRon.interactable           = false;
        if (btnRonSkip)       btnRonSkip.interactable       = false;
        if (scoringOKButton)  scoringOKButton.interactable  = false;

        // メニューは操作可
        EnsureMenuInnerButtonsAlwaysActive();

        if (btnMenu)          btnMenu.interactable          = true;
        bool menuInner = isMenuOpen;
        if (btnMenuOption)    btnMenuOption.interactable    = menuInner;
        if (btnMenuSuspend)   btnMenuSuspend.interactable   = menuInner;
        if (btnMenuExit)      btnMenuExit.interactable      = menuInner;
        if (btnMenuClose)     btnMenuClose.interactable     = menuInner;
        return;
    }
    // Main confirm (捨てる)
    if (btnConfirm)
    {
bool hideDuringAutoRiichiOffer = (phase == Phase.Offer && isRiichi && _autoConfirmOfferPending);


        btnConfirm.gameObject.SetActive(!hideDuringAutoRiichiOffer);
        btnConfirm.interactable = !hideDuringAutoRiichiOffer &&
            !isScoring &&
            (phase == Phase.Offer || phase == Phase.NeedDiscardN || phase == Phase.NeedDiscardAfterCall);
    }

    HideLegacyCallButtons();


        // Skill button
        if (btnSkill)
        {
            // スコア計算中やメニュー中は非表示
            bool canShowSkillButton = !isScoring;
            bool canUseSkill = false;

            if (canShowSkillButton)
            {
                var skill = GetEquippedSkill();

                // スキル未装備ならボタン自体を隠す
                if (skill == ActiveSkill.None || _skillSet == null)
                {
                    canShowSkillButton = false;
                }
                else
                {
                    // --- 1) このターンの使用回数上限 ---
                    int perTurnLimit = Mathf.Max(0, GetMaxSkillCastsThisTurn());
                    bool underTurnLimit = (perTurnLimit == 0) || (_skillCastsUsedThisTurn < perTurnLimit);
                    int baseCost;
                    if (!TryGetActiveSkillMpCost(skill, out baseCost))
                    {
                        baseCost = 0; // セットに見つからなければ0消費
                    }

int finalCost = ComputeFinalSkillMpCost(baseCost);
bool hasMp = (_mp >= finalCost);

// --- 3) 対象手牌選択が必要なスキルかどうか ---
bool needsHand = SkillNeedsHandSelectionForUI(skill);
bool hasTargetSelection = !needsHand || selHand.Count == 1;

// --- 4) 使用可能フェーズ制限 ---
bool phaseAllowsSkill =
    phase == Phase.Offer ||
    phase == Phase.NeedDiscardAfterCall ||
    phase == Phase.NeedDiscardN;

// ここで「押せるかどうか」を決定
canUseSkill = phaseAllowsSkill && underTurnLimit && hasMp && hasTargetSelection;
                    // スキルが装備されている限り、ボタン自体は表示する
                    canShowSkillButton = true;
                }
            }

            // 表示／非表示
            btnSkill.gameObject.SetActive(canShowSkillButton);
            // 押せるかどうか（グレーバック制御）
            btnSkill.interactable = canUseSkill;

if (skillTMP) skillTMP.text = GetGameFixedText_Local("button_skill");
        }


    // Skip
    if (btnSkip)
    {
        bool showSkip = (phase == Phase.EnemyTurn || phase == Phase.ChoosingCall || (phase == Phase.Offer && canTsumoNow));
        btnSkip.gameObject.SetActive(showSkip && phase != Phase.Scoring);
        btnSkip.interactable = showSkip && phase != Phase.Scoring;
if (skipTMP) skipTMP.text = GetGameFixedText_Local("button_skip");
    }

UpdateTenpaiBadge(); // ← 追加（毎回安全に呼べる軽量実装）
    if (phase == Phase.ChoosingCall)
    {
        // callMode == None のとき：鳴き種類（ポン/チー/カン）を選ぶ段階
        if (callMode == CallMode.None)
        {
            if (btnPon)
            {
                btnPon.gameObject.SetActive(_pendingCallPon);
                btnPon.interactable = _pendingCallPon;
                btnPon.onClick.RemoveAllListeners();
                btnPon.onClick.AddListener(()=> { if (!string.IsNullOrEmpty(callBaseTile)) StartCallFromTile(CallMode.Pon, callBaseTile); });
            }
            if (btnChi)
            {
                btnChi.gameObject.SetActive(_pendingCallChi);
                btnChi.interactable = _pendingCallChi;
                btnChi.onClick.RemoveAllListeners();
                btnChi.onClick.AddListener(()=> { if (!string.IsNullOrEmpty(callBaseTile)) StartCallFromTile(CallMode.Chi, callBaseTile); });
            }
            if (btnKan)
            {
                btnKan.gameObject.SetActive(_pendingCallKan);
                btnKan.interactable = _pendingCallKan;
                btnKan.onClick.RemoveAllListeners();
                btnKan.onClick.AddListener(()=> { if (!string.IsNullOrEmpty(callBaseTile)) StartCallFromTile(CallMode.KanOpen, callBaseTile); });
            }
        }
        // callMode != None のとき：手牌選択後の「確定」段階（同じボタンで確定する）
        else
        {
            bool ok = IsCallSelectionSatisfied();

            if (btnPon)
            {
                bool show = (callMode == CallMode.Pon);
                btnPon.gameObject.SetActive(show);
                btnPon.interactable = show && ok;
                btnPon.onClick.RemoveAllListeners();
                btnPon.onClick.AddListener(ConfirmCall);
            }
            if (btnChi)
            {
                bool show = (callMode == CallMode.Chi);
                btnChi.gameObject.SetActive(show);
                btnChi.interactable = show && ok;
                btnChi.onClick.RemoveAllListeners();
                btnChi.onClick.AddListener(ConfirmCall);
            }
            if (btnKan)
            {
                bool show = (callMode == CallMode.KanOpen);
                btnKan.gameObject.SetActive(show);
                btnKan.interactable = show && ok;
                btnKan.onClick.RemoveAllListeners();
                btnKan.onClick.AddListener(ConfirmCall);
            }
        }

        if (btnTenpaiConfirm) btnTenpaiConfirm.gameObject.SetActive(false);
        if (btnRiichi)        btnRiichi.gameObject.SetActive(false);

        // ChoosingCall中は、ロン/ツモ側の表示はここで打ち切る
        if (btnRon)  btnRon.gameObject.SetActive(false);
        if (btnTsumo) btnTsumo.gameObject.SetActive(false);

        // ★修正：ChoosingCall中でもメニュー内ボタンは常に操作可能にする
        EnsureMenuInnerButtonsAlwaysActive();
        if (btnMenu)          btnMenu.interactable          = true;
        {
            bool menuInner = isMenuOpen;
            if (btnMenuOption)    btnMenuOption.interactable    = menuInner;
            if (btnMenuSuspend)   btnMenuSuspend.interactable   = menuInner;
            if (btnMenuExit)      btnMenuExit.interactable      = menuInner;
            if (btnMenuClose)     btnMenuClose.interactable     = menuInner;
        }

        return;
    }

    // Evaluate win availability (may update canRonNow/canTsumoNow)
    EvaluateWinUI_New();

// Ron button (manual)
if (btnRon)
{
    bool btnCanRon = canRonNow && phase == Phase.EnemyTurn;

    if (btnCanRon)
    {
        bool enable = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);
        btnRon.gameObject.SetActive(true);
        btnRon.interactable = enable;
        btnRon.onClick.RemoveAllListeners();
        btnRon.onClick.AddListener(OnClickWin);
    }
    else
    {
        btnRon.onClick.RemoveAllListeners();
        btnRon.gameObject.SetActive(false);
    }
}

// Tsumo button (manual)
if (btnTsumo)
{
    bool btnCanTsumo = canTsumoNow && !suppressTsumoThisOffer && phase == Phase.Offer;

    if (btnCanTsumo)
    {
        bool enable = TryGetSelectedTsumoTile(out _) || offers.Any(id => CanTsumoWith(id, out _, out _, out _, out _));
        btnTsumo.gameObject.SetActive(true);
        btnTsumo.interactable = enable;
        btnTsumo.onClick.RemoveAllListeners();
        btnTsumo.onClick.AddListener(OnClickWin);
    }
    else
    {
        btnTsumo.onClick.RemoveAllListeners();
        btnTsumo.gameObject.SetActive(false);
    }
}


// Tenpai/Riichi buttons
if (btnTenpaiConfirm) btnTenpaiConfirm.gameObject.SetActive(!isScoring && phase == Phase.Offer);
if (btnRiichi)
{
    bool isMenzen = IsClosedHand();
    bool showRiichi = (!isScoring && isTenpai && !isRiichi && isMenzen);

    btnRiichi.gameObject.SetActive(showRiichi);
    // ★修正：表示する条件のときは必ず押せる状態に戻す（カットインで false になったままを防ぐ）
    btnRiichi.interactable = showRiichi;
}
if (confirmTMP) { confirmTMP.text = isScoring ? confirmTMP.text : GetGameFixedText_Local("confirm_discard"); }
var kanButton = (btnKanFromHand != null) ? btnKanFromHand : btnKan;
if (kanButton != null)
{
    bool canHandKan = false;

    try
    {
        if (!isScoring)
        {
            if (isRiichi)
            {
                canHandKan = false;
            }
            else
            {
                // 2) プレイヤーの「自分のツモ番で、捨て牌待ち」のときだけ候補にする
                //    - Offer / NeedDiscardAfterCall / NeedDiscardN など「自分が捨てられる」フェーズ限定
                //    - コール選択中(callMode!=None)の間は出さない
                if (phase == Phase.Offer ||
                    phase == Phase.NeedDiscardAfterCall ||
                    phase == Phase.NeedDiscardN)
                {
                    if (callMode == CallMode.None)
                    {
                        // 手牌・面子の状態から、実際にカン候補があるかだけを見る
                        var ankan = FindAnkanCandidates();
                        var kakan = FindKakanCandidates();

                        bool hasAnkan = (ankan != null && ankan.Count > 0);
                        bool hasKakan = (kakan != null && kakan.Count > 0);

                        canHandKan = hasAnkan || hasKakan;
                    }
                }
            }
        }

    }
    catch
    {
        // 例外が出たときは安全側（カン不可扱い）に倒す
        canHandKan = false;
    }

    // ボタンの見た目／有効・無効とクリック先をまとめて制御
    kanButton.onClick.RemoveAllListeners();
    if (canHandKan)
        kanButton.onClick.AddListener(OnClickKanFromHand);

    kanButton.gameObject.SetActive(canHandKan);
    kanButton.interactable = canHandKan;
}

// ★修正：通常フローでもメニュー内ボタンの interactable を毎回正しく設定する
// （スキル演出等の早期リターンで false にされた後、通常フローに戻ったとき復旧されない問題の修正）
EnsureMenuInnerButtonsAlwaysActive();
if (btnMenu)          btnMenu.interactable          = true;
{
    bool menuInner = isMenuOpen;
    if (btnMenuOption)    btnMenuOption.interactable    = menuInner;
    if (btnMenuSuspend)   btnMenuSuspend.interactable   = menuInner;
    if (btnMenuExit)      btnMenuExit.interactable      = menuInner;
    if (btnMenuClose)     btnMenuClose.interactable     = menuInner;
}


}
// --- 追加：アンカン候補（手牌4枚） ---
private List<string> FindAnkanCandidates()
{
    // ★特別牌（*_sp 等）を含めてもカンできるように、ロジック用IDに正規化して集計する
    var res = new List<string>();
    var g = hand.GroupBy(x => StripTileIdForLogic(x));
    foreach (var gg in g)
    {
        if (string.IsNullOrEmpty(gg.Key)) continue;
        if (gg.Count() >= 4) res.Add(gg.Key); // 返すのは baseId（ロジック用ID）
    }
    return res;
}

// --- 追加：カカン候補（ポン面子 + 手牌1枚）---
private List<(int meldIndex, string id)> FindKakanCandidates()
{
    var res = new List<(int, string)>();
    for (int mi = 0; mi < melds.Count; mi++)
    {
        var m = melds[mi];
        if (m == null || m.Count != 3) continue;

        // ★ポン面子（3枚）が同一“ロジック牌”かを確認し、手牌側も特別牌を含めて検索する
        string baseId = StripTileIdForLogic(m[0]);
        if (string.IsNullOrEmpty(baseId)) continue;

        bool meldOk = m.All(x => StripTileIdForLogic(x) == baseId);
        bool has4th = hand.Any(h => StripTileIdForLogic(h) == baseId);

        if (meldOk && has4th) res.Add((mi, baseId)); // 返すのは baseId
    }
    return res;
}


private void OnClickKanFromHand()
{
    // 自分のツモ番で捨て牌待ちのフェーズ以外では発動しない
    if (!(phase == Phase.Offer ||
          phase == Phase.NeedDiscardAfterCall ||
          phase == Phase.NeedDiscardN))
        return;

    var kakan = FindKakanCandidates();
    if (kakan.Count > 0) { DoKakan(kakan[0].meldIndex, kakan[0].id); return; }

    var ankan = FindAnkanCandidates();
    if (ankan.Count > 0) { DoAnkan(ankan[0]); return; }
if (statusTMP) statusTMP.text = GetGameFixedText_Local("call_no_kan_candidate");
}
private void DoAnkan(string baseId)
{
    // ★追加：リーチ後はカン禁止（保険）
    if (isRiichi) return;

    // ★追加：1ターン目に暗槓したら、天和/地和/人和は成立しない
    if (_playerTsumoCountThisRound == 1)
    {
        _playerDidAnkanOnFirstTurnThisHand = true;
    }

    var picked = new List<string>(4);
    for (int i = hand.Count - 1; i >= 0 && picked.Count < 4; i--)
    {
        if (StripTileIdForLogic(hand[i]) == baseId)
        {
            picked.Add(hand[i]);
            hand.RemoveAt(i);
        }
    }

    // 念のため：4枚取れなければ何もしない（UI不整合防止）
    if (picked.Count != 4)
    {
        for (int i = picked.Count - 1; i >= 0; i--) hand.Add(picked[i]);
        SortHand(); RefreshHandUI();
        return;
    }

    // 面子追加（実際に抜いたIDを保持：特別牌の見た目/効果を維持）
    melds.Add(new List<string> { picked[0], picked[1], picked[2], picked[3] });

    SortHand(); RefreshHandUI(); RefreshMeldUI();

    // ドラ表示+嶺上表示牌
    AddKanIndicator();

StartCoroutine(__RinshanToHandFlow(1f, 1f, Phase.Offer, GetGameFixedText_Local("ankan_rinshan_draw")));
    // 直ちに Offer フェーズ継続扱い（UIを固めないため）
    phase = Phase.Offer;
    UpdateButtons();
}

private void DoKakan(int meldIndex, string baseId)
{
    // ★追加：リーチ後はカン禁止（保険）
    if (isRiichi) return;

    if (meldIndex < 0 || meldIndex >= melds.Count) return;
    // ★手牌から「ロジック一致の1枚」を除去（特別牌も対象）
    int idx = hand.FindIndex(x => StripTileIdForLogic(x) == baseId);
    if (idx < 0) return;

    string picked = hand[idx];
    hand.RemoveAt(idx);

    // 既存のポン面子へ4枚目を追加（実際に抜いたIDを保持）
    var m = melds[meldIndex];
    m.Add(picked);

    SortHand(); RefreshHandUI(); RefreshMeldUI();

    AddKanIndicator();

StartCoroutine(__RinshanToHandFlow(1f, 1f, Phase.Offer, GetGameFixedText_Local("kakan_rinshan_draw")));

    phase = Phase.Offer;
    UpdateButtons();
}
private void EnsureBottomButtons()
{
    // Ensure confirmTMP is bound
    if (!confirmTMP && btnConfirm)
        confirmTMP = btnConfirm.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

    // ★方針：callskip を自動生成しない。通常のスキップボタン(btnSkip)に統一する。
    // btnSkip は Inspector で割り当てるのが正。
    // ただし未割り当て事故の保険として、シーン内に既に存在するボタンを拾うだけは行う（生成はしない）。

    if (!btnSkip)
    {
        GameObject found = null;

        // プロジェクト側で通常スキップボタンの名前が決まっているなら、ここに追加してください
        found = GameObject.Find("Button_Skip");
        if (!found) found = GameObject.Find("SkipButton");
        if (!found) found = GameObject.Find("BtnSkip");

        // 旧残骸が残っている場合だけ拾う（ただし生成はしない）
        if (!found) found = GameObject.Find("Button_CallSkip");

        if (found)
        {
            btnSkip = found.GetComponent<Button>();
            skipTMP = btnSkip ? btnSkip.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        }
    }

    // 旧 callskip ボタンが別個に存在する場合は、通常スキップへ統一するため非表示
    var legacy = GameObject.Find("Button_CallSkip");
    if (legacy && btnSkip && legacy != btnSkip.gameObject)
    {
        legacy.SetActive(false);
    }

    if (btnSkill && btnSkip)
        btnSkip.transform.SetSiblingIndex(btnSkill.transform.GetSiblingIndex() + 1);

    // ★RemoveAllListeners は使わない（SE 等の既存配線を壊さない）
    // OnClickSkipCall だけ重複登録を防いで付ける
    if (btnSkip)
    {
        btnSkip.onClick.RemoveListener(OnClickSkipCall);
        btnSkip.onClick.AddListener(OnClickSkipCall);
    }

    // --- Discover manual call buttons if not assigned (safety) ---
    if (!btnPon)   { var f = GameObject.Find("Button_Pon");   if (f) btnPon = f.GetComponent<Button>(); }
    if (!btnChi)   { var f = GameObject.Find("Button_Chi");   if (f) btnChi = f.GetComponent<Button>(); }
    if (!btnKan)   { var f = GameObject.Find("Button_Kan");   if (f) btnKan = f.GetComponent<Button>(); }
    if (!btnRon)   { var f = GameObject.Find("Button_Ron");   if (f) btnRon = f.GetComponent<Button>(); }
    if (!btnTsumo) { var f = GameObject.Find("Button_Tsumo"); if (f) btnTsumo = f.GetComponent<Button>(); }
}

    private void HideLegacyCallButtons()
    {
        if (btnPon) { btnPon.gameObject.SetActive(false); btnPon.interactable=false; }
        if (btnChi) { btnChi.gameObject.SetActive(false); btnChi.interactable=false; }
        if (btnKan) { btnKan.gameObject.SetActive(false); btnKan.interactable=false; }
        if (btnKanFromHand) { btnKanFromHand.gameObject.SetActive(false); btnKanFromHand.interactable=false; }
        if (btnRon) { btnRon.gameObject.SetActive(false); btnRon.interactable=false; }
        if (btnRonSkip) { btnRonSkip.gameObject.SetActive(false); btnRonSkip.interactable=false; }
    }

    // ===== Tiles =====
    private void ClearChildren(RectTransform t)
    {
        if (!t) return;
        for (int i=t.childCount-1; i>=0; i--) Destroy(t.GetChild(i).gameObject);
    }
private void SetupTile(GameObject go, string id, int index, bool isHand=false, bool isOffer=false, bool clickable=true)
    {
        var baseId = (id!=null && id.EndsWith("*")) ? id.Substring(0,id.Length-1) : id;
        SetTileSprite(go, baseId);

        if (isOffer)
        {
            go.name = $"Offer_{index}";
        }

        var button = go.GetComponent<Button>();
        if (button)
        {
            bool block = (phase == Phase.Scoring) || isMenuOpen;
            button.interactable = clickable && !block;
            button.onClick.RemoveAllListeners();
            if (clickable && !block)
            {
                if (isHand)  button.onClick.AddListener(()=>OnHandTileClicked(index));
                else if (isOffer) button.onClick.AddListener(()=>OnOfferTileClicked(index));
            }
        }
    }
    private Transform FindOfferChildByLogicalIndex(int logicalIndex)
{
    if (!offerArea) return null;

    string targetName = $"Offer_{logicalIndex}";
    int cc = offerArea.childCount;

    for (int i = 0; i < cc; i++)
    {
        var child = offerArea.GetChild(i);
        if (!child) continue;

        if (child.name == targetName)
            return child;
    }

    if (logicalIndex >= 0 && logicalIndex < offerArea.childCount)
        return offerArea.GetChild(logicalIndex);

    return null;
}
private void RefreshOfferWinningHighlights()
{
    if (!offerArea) return;

    for (int i = 0; i < offers.Count; i++)
    {
        var tileTf = FindOfferChildByLogicalIndex(i);
        if (!tileTf) continue;

        bool win = false;
        try
        {
            win = CanTsumoWith(offers[i], out _, out _, out _, out _);
        }
        catch
        {
            win = false;
        }

        // Offer牌では、リーチ捨て牌用のオレンジ/赤系ハイライトは使わない
        SetTileRiichiDiscardHighlight(tileTf, false);

        // 和了対象牌だけ黄色ハイライト
        SetTileHighlight(tileTf, win);

        // スパークル演出は維持
        SetTileSparkle(tileTf, win);
    }
}
    private void SetTileSprite(GameObject go, string id)
    {
        Image artImg = null;
        var artTf = go.transform.Find("Art");
        if (artTf)
        {
            var imgChild = artTf.Find("Image");
            if (imgChild) artImg = imgChild.GetComponent<Image>();
        }
        if (!artImg)
        {
            foreach (var im in go.GetComponentsInChildren<Image>(true))
                if (im.transform != go.transform) { artImg = im; break; }
        }
var rootImg = go.GetComponent<Image>();

string key = id;
try
{
    // "*" と レジェ suffix を落として、レア度Spriteへ統一
    key = SpecialTileRuntime.SpriteKeyFromTileId(id);
}
catch
{
    key = id;
}

var sp = Resources.Load<Sprite>($"Sprites/Tiles/{key}");

foreach (var im in go.GetComponentsInChildren<Image>(true))
{
    // ★重要：CallHighlight（鳴き赤ハイライト）や RaiseOverlay 等は、牌アート更新で触ると一瞬で消える原因になるので除外
if (im != null)
{
    var n = im.transform.name;
    if (n == "CallHighlight" || n == "RaiseOverlay") continue;

    var pn = im.transform.parent ? im.transform.parent.name : "";
    if (pn == "CallHighlight" || pn == "RaiseOverlay") continue;
}

    if (im == artImg)
    {
        im.sprite = sp;
        im.enabled = (sp != null);
        im.preserveAspect = true;
        im.raycastTarget = false;
        im.color = Color.white;
    }
    else if (im == rootImg)
    {
        if (!im) continue;
        im.enabled = true;
        im.sprite = null;
        im.color  = new Color(1,1,1,0);
        im.raycastTarget = true;
    }
else
{
    // ★重要：CallHighlight（鳴き赤ハイライト）や RaiseOverlay（持ち上げ表示）は
    // SetTileHighlight / RebuildRaiseOverlays 側が管理するので、ここで潰さない
    bool managedOverlay = false;
    var t = im.transform;
    while (t != null && t != go.transform)
    {
        if (t.name == "CallHighlight" || t.name == "RaiseOverlay")
        {
            managedOverlay = true;
            break;
        }
        t = t.parent;
    }
    if (managedOverlay) continue;

    im.sprite = null;
    im.color = new Color(1,1,1,0);
    im.enabled = false;
    im.raycastTarget = false;
}

}


    }
    private Image FindArtImage(Transform tile)
    {
        var art = tile.Find("Art");
        if (art)
        {
            var img = art.Find("Image");
            if (img) return img.GetComponent<Image>();
        }
        foreach (var im in tile.GetComponentsInChildren<Image>(true))
            if (im.transform != tile) return im;
        return null;
    }
void SetTileHighlight(Transform tile, bool on)
{
    var img = GetVisibleArtImage(tile);
    if (!img) return;

    // ★過去に作った CallHighlightOverlay が残っていて見た目を壊す場合があるので必ず消す
    var artParent = img.transform.parent;
    if (artParent)
    {
        var callRoot = artParent.Find("CallHighlight");
        if (callRoot)
        {
            var old = callRoot.Find("CallHighlightOverlay");
            if (old) old.gameObject.SetActive(false);
        }
    }

    // ★黄色用Outlineを“黄色専用”で確保する
    var outs = img.GetComponents<Outline>();
    Outline yellow = null;
    for (int i = 0; i < outs.Length; i++)
    {
        var c = outs[i].effectColor;
        // 黄色っぽいOutlineだけを黄色用として再利用
        if (c.r > 0.8f && c.g > 0.8f && c.b < 0.3f)
        {
            yellow = outs[i];
            break;
        }
    }
    if (!yellow) yellow = img.gameObject.AddComponent<Outline>();

    yellow.effectColor = new Color(1f, 0.92f, 0.16f, 0.95f);
    yellow.effectDistance = new Vector2(4f, -4f);
    yellow.useGraphicAlpha = true;
    yellow.enabled = on;
}
// Dora-only soft glow (uses Shadow so it doesn't conflict with Outline sparkle)
private void SetTileDoraShadow(Transform tile, bool on)
{
    var img = GetVisibleArtImage(tile);
    if (!img) return;
    var shadow = img.GetComponent<Shadow>() ?? img.gameObject.AddComponent<Shadow>();
    shadow.effectColor = new Color(0.2f, 0.9f, 1f, 0.9f); // cyan-like glow
    shadow.effectDistance = new Vector2(0f, 0f);          // centered glow
    shadow.useGraphicAlpha = true;
    shadow.enabled = on;
}

// Check if a tile id (may end with '*') is Dora according to current indicators
private bool IsDoraTileId(string id)
{
    if (string.IsNullOrEmpty(id) || doraIndicators == null || doraIndicators.Count == 0) return false;
// ★特別牌（*_sp 等）や "*" を除去したロジックIDで判定する
id = StripTileIdForLogic(id);
if (string.IsNullOrEmpty(id)) return false;

for (int i = 0; i < doraIndicators.Count; i++)
{
    var d = NextDoraId(doraIndicators[i]); // NextDoraId 側でも indicator を正規化する
    if (!string.IsNullOrEmpty(d) && d == id) return true;
}

    return false;
}

// Apply Dora glow to each child under parent corresponding to src list
private void ApplyDoraHighlights(RectTransform parent, List<string> src)
{
    if (!parent || src == null) return;
    int n = System.Math.Min(parent.childCount, src.Count);
    for (int i = 0; i < n; i++)
    {
        bool isDora = IsDoraTileId(src[i]);
        SetTileDoraShadow(parent.GetChild(i), isDora);
    }
}
// Sparkle highlight (pulsing Outline alpha) -- used only for winning tiles and only this turn
private void SetTileSparkle(Transform tile, bool on)
{
    var img = GetVisibleArtImage(tile);
    if (!img) return;

    var outs = img.GetComponents<Outline>();
    Outline gold = null;
    for (int i = 0; i < outs.Length; i++)
    {
        var c = outs[i].effectColor;
        // 金っぽいOutlineを黄色用として再利用
        if (c.g > 0.7f && c.b < 0.4f) { gold = outs[i]; break; }
    }
    if (!gold) gold = img.gameObject.AddComponent<Outline>();

    gold.effectColor = new Color(1f, 0.9f, 0f, 0.9f);
    gold.effectDistance = new Vector2(4f, -4f);   // ★ここを10fにすれば太くなる（動作確認可能になる）
    gold.useGraphicAlpha = true;
    gold.enabled = on;
    if (on) StartCoroutine(SparkleRoutine(gold));
}

    private System.Collections.IEnumerator SparkleRoutine(Outline outline)
    {
        float t = 0f;
        while (outline && outline.enabled)
        {
            t += Time.deltaTime * 3f;
            var c = outline.effectColor;
            // ping-pong alpha between 0.4 and 1.0
            c.a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t));
            outline.effectColor = c;
            yield return null;
        }
    }

private void SetTileGrey(Transform tile, bool on)
{
    var img = GetVisibleArtImage(tile);
    if (!img) return;

    // 色だけ変える。Outline は SetTileHighlight / SetTileSparkle が管理するので触らない
    img.color = on ? new Color(0.7f, 0.7f, 0.7f, 1f) : Color.white;
}
// ★敵捨て牌（鳴き候補/ロン候補）の赤アウトラインを全解除
private void ClearEnemyDiscardCallHighlights()
{
    if (!enemyDiscardArea) return;

    for (int i = 0; i < enemyDiscardArea.childCount; i++)
    {
        var tileTf = enemyDiscardArea.GetChild(i);
        if (!tileTf) continue;

        // あなたの既存ロジック（赤アウトライン付与）と同じコンポーネントを落とす
        // ※「黄色ハイライトと同じOutline」方式に揃えている前提
        var outline = tileTf.GetComponent<UnityEngine.UI.Outline>();
        if (outline) outline.enabled = false;

        // RaiseOverlay 側に Outline を持っている場合も落とす（保険）
        var ro = tileTf.Find("RaiseOverlay");
        if (ro)
        {
            var o2 = ro.GetComponent<UnityEngine.UI.Outline>();
            if (o2) o2.enabled = false;
        }
    }
}


private void SetBaseArtVisible(Transform tile, bool visible)
{
    var artImg = FindArtImage(tile);
    if (!artImg) return;
   // ベース画像は常に有効にしておく（Button の raycast 対象を確保）
   artImg.enabled = true;
   // 表示/非表示はアルファで制御（クリックは常に通す）
   artImg.color = visible ? Color.white : new Color(1,1,1,0);
   // 念のため raycastTarget も有効にしておく（targetGraphic として使われる構成でも安全）
   artImg.raycastTarget = true;
    }
    private void RebuildRaiseOverlays(RectTransform parent, HashSet<int> selected, List<string> src)
    {
        if (!parent) return;

        // remove old overlays and restore base art
        for (int i = 0; i < parent.childCount; i++)
        {
            var baseTile = parent.GetChild(i);
            for (int j = baseTile.childCount - 1; j >= 0; j--)
            {
                if (baseTile.GetChild(j).name == "RaiseOverlay")
                    Destroy(baseTile.GetChild(j).gameObject);
            }
            SetBaseArtVisible(baseTile, true);
        }

        foreach (int idx in selected)
        {
            if (idx < 0 || idx >= parent.childCount || idx >= src.Count) continue;

            var baseTile = parent.GetChild(idx) as RectTransform;
            SetBaseArtVisible(baseTile, false);

            var ghost = Instantiate(tilePrefab, baseTile);
            ghost.name = "RaiseOverlay";
            SetTileSprite(ghost, src[idx]);

            if (ghost.TryGetComponent<Button>(out var b)) b.interactable = false;
            foreach (var img in ghost.GetComponentsInChildren<Image>(true)) img.raycastTarget = false;

            var rt = ghost.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            var baseRT   = baseTile;
            rt.sizeDelta = baseRT.sizeDelta;
            rt.anchoredPosition = new Vector2(0f, raisePixels);
            ghost.transform.SetAsLastSibling();
        }

        // ★重要：
        // Offer牌は RaiseOverlay の付け直しで「見えているImage」が切り替わる。
        // そのため、選択表示の再構築後に和了対象牌ハイライトを必ず再適用する。
        if (parent == offerArea)
        {
            RefreshOfferWinningHighlights();
        }
    }
    void SetTileCallTargetHighlight(Transform tile, bool on)
{
    var img = GetVisibleArtImage(tile);
    if (!img) return;

    // 鳴き対象牌用の赤Outlineを、色一致で専用管理する
    var outs = img.GetComponents<Outline>();
    Outline target = null;

    for (int i = 0; i < outs.Length; i++)
    {
        var c = outs[i].effectColor;

        // 赤っぽいOutlineだけをこの用途で再利用
        if (c.r > 0.7f && c.g < 0.3f && c.b < 0.3f)
        {
            target = outs[i];
            break;
        }
    }

    if (!target) target = img.gameObject.AddComponent<Outline>();

    target.effectColor = new Color(1f, 0f, 0f, 0.95f);
    target.effectDistance = new Vector2(4f, -4f);
    target.useGraphicAlpha = true;
    target.enabled = on;
}
public void OnHandTileClicked(int index)
{
    if (phase == Phase.Scoring) return;
    if (index < 0 || index >= hand.Count) return;

    if (phase == Phase.ChoosingCall)
    {
        if (callMode == CallMode.Chi)
        {
            // Allow deselect; for select, require dynamic Chi completion rule
            if (!selHand.Contains(index) && !IsSelectableForChiWithCurrent(hand[index], index)) return;
            ToggleSelect(selHand, index);
            RebuildRaiseOverlays(handArea, selHand, hand);
            EnableHandForChiDynamic();

            // 特別牌ポップアップ：選択状態（上にスライド）になった時だけ表示
            try
            {
                string id = hand[index];
                SetSpecialTilePopupForSelection(id, selHand.Contains(index));
            }
            catch { }

            UpdateButtons();
            return;
        }
        else
        {
            if (!selHand.Contains(index) && !IsSelectableForCurrentCall(hand[index])) return;
            ToggleSelect(selHand, index);
            RebuildRaiseOverlays(handArea, selHand, hand);
            EnableHandForCall(IsSelectableForCurrentCall);

            // 特別牌ポップアップ：選択状態（上にスライド）になった時だけ表示
            try
            {
                string id = hand[index];
                SetSpecialTilePopupForSelection(id, selHand.Contains(index));
            }
            catch { }

            UpdateButtons();
            return;
        }
    }
    if (phase == Phase.NeedDiscardAfterCall || phase == Phase.NeedDiscardN || phase == Phase.Offer)
    {
        ToggleSelect(selHand, index);
        UpdateSkillHandSelectionOrder(index);
        RebuildRaiseOverlays(handArea, selHand, hand);

        // 特別牌ポップアップ：選択状態（上にスライド）になった時だけ表示
        try
        {
            string id = hand[index];
            SetSpecialTilePopupForSelection(id, selHand.Contains(index));
        }
        catch { }

        UpdateButtons();
        return;
    }
}
public void OnOfferTileClicked(int index)
{
    // ★追加：敵スキル（毒/妨害など）ダメージ演出中は入力を受け付けない
    if (_enemySkillDamageAnimating) return;

    // ★追加：進行停止中も入力を受け付けない（毒処理中にここがtrueになるケース対策）
    if (_freezeProgression) return;

    if (phase != Phase.Offer || index < 0 || index >= offers.Count) return;

    ToggleSelect(selOffer, index);
    RebuildRaiseOverlays(offerArea, selOffer, offers);

    // 特別牌ポップアップ：選択状態（上にスライド）になった時だけ表示
    try
    {
        string id = offers[index];
        SetSpecialTilePopupForSelection(id, selOffer.Contains(index));
    }
    catch { }

    UpdateButtons();
}
public bool IsLegendaryDamageHalfActive()
{
    if (!_legendaryDamageHalfPending) return false;
    if (string.IsNullOrEmpty(_legendaryDamageHalfEnemyKey)) return false;

string curKey = GetCurrentEnemyKey_ForLegendary();

    if (!string.Equals(curKey, _legendaryDamageHalfEnemyKey, StringComparison.Ordinal))
    {
        // 敵が変わったら継続効果は無効化（安全側）
        _legendaryDamageHalfPending = false;
        _legendaryDamageHalfEnemyKey = null;
        return false;
    }
    return true;
}

private int PreviewLegendaryDamageHalfOnEnemyWin(int rawDamage)
{
    if (rawDamage <= 0) return 0;
    if (!IsLegendaryDamageHalfActive()) return rawDamage;

    // 表示上は半減後の整数値にする（ゲーム側の実適用は ScoreOK で TryConsume が行う）
    return Mathf.FloorToInt(rawDamage * 0.5f);
}

    private void ToggleSelect(HashSet<int> set, int idx)
    {
        if (set.Contains(idx)) set.Remove(idx); else set.Add(idx);
    }

    // ===== Bottom buttons =====
    
private void OnClickSkill()
{
    // ★追加：敵スキル（毒/妨害など）ダメージ演出中は入力を受け付けない
    if (_enemySkillDamageAnimating) return;

    // ★追加：進行停止中も入力を受け付けない
    if (_freezeProgression) return;

    _lastSkillApplied = false; // ← 追加：毎回リセット
    EnsureSkillInit();
    if (_activeSkillChargesLeft <= 0)
    {
if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_exhausted");
        return;
    }

    var skill = GetEquippedSkill();
    if (skill == ActiveSkill.None)
    {
if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_not_equipped");
        return;
    }
    var selectedHandList = GetSkillHandSelectionOrder();
    int selIdx = (selectedHandList.Count >= 1) ? selectedHandList[0] : -1;

    bool hasHadesDyeMaster =
        skill == ActiveSkill.RandomMan &&
        HasEquippedUniqueOmamori_RuntimeSafe(PlayerData.UniqueOmamoriEffectKind.Hades_DyeMaster);

    bool hasHadesCalligrapher =
        (skill == ActiveSkill.RandomHonor || skill == ActiveSkill.EnhanceHand) &&
        HasEquippedUniqueOmamori_RuntimeSafe(PlayerData.UniqueOmamoriEffectKind.Hades_Calligrapher);
    bool needsOneHand = skill == ActiveSkill.RandomMan
                     || skill == ActiveSkill.RandomSou
                     || skill == ActiveSkill.RandomPin
                     || skill == ActiveSkill.RandomHonor
                     || skill == ActiveSkill.RandomYaochu
                     || skill == ActiveSkill.RandomChunchan
                     || skill == ActiveSkill.DuplicateAndDiscardOther
                     || skill == ActiveSkill.ForceDrawSelectedNextTurn
                     || skill == ActiveSkill.EnhanceHand;
    if (needsOneHand && selectedHandList.Count != 1)
    {
if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_select_hand_target");
        return;
    }
    if ((hasHadesDyeMaster || hasHadesCalligrapher) && selectedHandList.Count > 2)
    {
if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_unique_max_two_selection");
        return;
    }

        System.Func<bool> apply = () => false;

        switch (skill)
        {
case ActiveSkill.RandomMan:
    apply = () =>
    {
        if (hasHadesDyeMaster)
        {
            return ApplyHadesDyeMasterSkill(selectedHandList);
        }

        // 通常の染色師
        string suit = GetMajorSuitExcludingIndex(selIdx);
        var newId = suit + rng.Next(1, 10);
        return ReplaceHandAt(selIdx, newId);
    };
    break;

            case ActiveSkill.RandomSou:
                apply = () =>
                {
                    var newId = "Sou" + rng.Next(1, 10);
                    return ReplaceHandAt(selIdx, newId);
                };
                break;
            case ActiveSkill.RandomPin:
                apply = () =>
                {
                    var newId = "Pin" + rng.Next(1, 10);
                    return ReplaceHandAt(selIdx, newId);
                };
                break;
            case ActiveSkill.RandomHonor:
                apply = () =>
                {
                    if (hasHadesCalligrapher)
                    {
                        return ApplyHadesCalligrapherSkill(selectedHandList);
                    }

                    string[] honors = { "East","South","West","North","White","Green","Red" };
                    var newId = honors[rng.Next(0, honors.Length)];
                    return ReplaceHandAt(selIdx, newId);
                };
                break;
            case ActiveSkill.RandomYaochu:
                apply = () =>
                {
                    string[] yaochu = { "Man1","Man9","Pin1","Pin9","Sou1","Sou9","East","South","West","North","White","Green","Red" };
                    var newId = yaochu[rng.Next(0, yaochu.Length)];
                    return ReplaceHandAt(selIdx, newId);
                };
                break;
            case ActiveSkill.RandomChunchan:
                apply = () =>
                {
                    string[] suits = { "Man","Pin","Sou" };
                    var newId = suits[rng.Next(0, suits.Length)] + rng.Next(2, 9);
                    return ReplaceHandAt(selIdx, newId);
                };
                break;
            case ActiveSkill.DuplicateAndDiscardOther:
                apply = () =>
                {
                    var id = hand[selIdx];
                    hand.Add(id); // 複製
                    SortHand(); RefreshHandUI();
                    phase = Phase.NeedDiscardN;
                    needDiscardCount = 1;
                    if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_duplicated_select_discard");
                    UpdateButtons();
                    return true;
                };
                break;
            case ActiveSkill.EnhanceHand:
                apply = () =>
                {
                    if (hasHadesCalligrapher)
                    {
                        return ApplyHadesCalligrapherSkill(selectedHandList);
                    }

                    // 通常の書家
                    var id = hand[selIdx];
                    if (!TryParseSuitNum(id, out var s, out var _)) return false;
                    if (s >= 3) return false;

                    var suit = (s == 0 ? "Man" : s == 1 ? "Pin" : "Sou");
                    var newId = suit + "5";
                    return ReplaceHandAt(selIdx, newId);
                };
                break;
            case ActiveSkill.AddDoraIndicator:
                apply = () =>
                {
                    AddKanIndicator();
                    if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_dora_plus_one");
                    return true;
                };
                break;
            case ActiveSkill.NullifyEnemyDiscardEffectsOnce:
                apply = () =>
                {
                    _suppressEnemyEffectsOnce = true;
                    if (statusTMP) statusTMP.text = "次の敵効果を無効化します";
                    return true;
                };
                break;
            case ActiveSkill.ForceDrawSelectedNextTurn:
                apply = () =>
                {
                    _skillNextOfferTile = hand[selIdx];
                    if (statusTMP) statusTMP.text = $"次ターンで {_skillNextOfferTile} をツモに追加";
                    return true;
                };
                break;
            case ActiveSkill.Capitalist:
                apply = () =>
                {
                    return ApplyCapitalistSkill();
                };
                break;
        }
bool ok = apply();
if (ok)
{
    _lastSkillApplied = true; // ← 追加：この呼び出しは成功

    if (AudioManager.Instance)
    {
        AudioManager.Instance.PlayCutin_PlayerSkill();
    }

    StartPlayerSkillCutin(GetActiveSkillActionNameSafe(skill));
    _activeSkillChargesLeft--;

    if (phase == Phase.Offer && skill != ActiveSkill.DuplicateAndDiscardOther)
    {
        _afterSkillNoHandDiscardOnce = true;
        selHand.Clear();
        selOffer.Clear();
        _skillHandSelectionOrder.Clear();
        RebuildRaiseOverlays(handArea, selHand, hand);
        RebuildRaiseOverlays(offerArea, selOffer, offers);
    }
    UpdateButtons();
}
    }
// ===== Bottom button handlers (confirm / tenpai confirm) =====
private void OnClickTenpaiConfirm()
{
    if (phase != Phase.Offer) return;
    // 現在の手でテンパイ判定を更新（UI反映のみ。牌移動などは行わない）
    isTenpai = IsTenpai(hand);
    UpdateTenpaiBadge();
    if (statusTMP) statusTMP.text = isTenpai
    ? GetGameFixedText_Local("tenpai_riichi_available")
    : GetGameFixedText_Local("not_tenpai");
    UpdateButtons();
}
private bool ApplyCapitalistSkill()
{
    if (offers == null || offers.Count <= 0)
    {
        if (statusTMP) statusTMP.text = "ツモ場に牌がありません";
        return false;
    }

    var targetIndices = new List<int>();
    var newIds = new List<string>();

    for (int i = 0; i < offers.Count; i++)
    {
        targetIndices.Add(i);
        newIds.Add(GetRandomTileIdForCapitalist());
    }

    _lastSkillApplied = true;
    StartCoroutine(__PlayerSkill_ConvertOfferTiles_WithMagicFx_Co(
        targetIndices,
        newIds,
        capitalistTransformFxColor
    ));

    return true;
}
private string GetRandomTileIdForCapitalist()
{
    int idx = rng.Next(0, 34);

    if (idx < 9) return "Man" + (idx + 1).ToString();
    if (idx < 18) return "Pin" + (idx - 8).ToString();
    if (idx < 27) return "Sou" + (idx - 17).ToString();

    string[] honors = { "East", "South", "West", "North", "White", "Green", "Red" };
    return honors[idx - 27];
}
private bool ApplyHadesDyeMasterSkill(List<int> selectedHandList)
{
    if (selectedHandList == null || selectedHandList.Count != 1) return false;

    int srcIdx = selectedHandList[0];
    if (srcIdx < 0 || srcIdx >= hand.Count) return false;

    string srcId = hand[srcIdx];
    if (!TryParseSuitNum(srcId, out int srcSuit, out int srcNum)) return false;
    if (srcSuit >= 3) return false;

    int[] counts = new int[3];

    for (int i = 0; i < hand.Count; i++)
    {
        if (i == srcIdx) continue;

        string id = hand[i];
        if (!TryParseSuitNum(id, out int suit, out int _)) continue;
        if (suit >= 3) continue;

        counts[suit]++;
    }

    int bestSuit = 0;
    int bestCount = counts[0];

    for (int s = 1; s < 3; s++)
    {
        if (counts[s] > bestCount)
        {
            bestCount = counts[s];
            bestSuit = s;
        }
    }

    string newId =
        (bestSuit == 0 ? "Man" :
         bestSuit == 1 ? "Pin" : "Sou") + srcNum.ToString();

    bool ok = ReplaceHandAt(srcIdx, newId);
    if (ok)
    {
        if (statusTMP) statusTMP.text = string.Format(GetGameFixedText_Local("relic_effect_transform"), newId);
    }
    return ok;
}
private bool HasEquippedUniqueOmamori_RuntimeSafe(PlayerData.UniqueOmamoriEffectKind kind)
{
    if (kind == PlayerData.UniqueOmamoriEffectKind.None)
        return false;

    var checkedIds = new HashSet<int>();

    try
    {
        var ids = PlayerData.EquippedOmamoriIds;
        if (ids != null)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                int id = Mathf.Max(0, ids[i]);
                if (id <= 0) continue;
                if (!checkedIds.Add(id)) continue;

                if (PlayerData.TryGetOmamori(id, out var o) && o != null)
                {
                    if (o.isUnique && o.uniqueKind == kind)
                        return true;
                }
            }
        }
    }
    catch { }

    try
    {
        int singleId = Mathf.Max(0, PlayerData.EquippedOmamori);
        if (singleId > 0 && checkedIds.Add(singleId))
        {
            if (PlayerData.TryGetOmamori(singleId, out var o) && o != null)
            {
                if (o.isUnique && o.uniqueKind == kind)
                    return true;
            }
        }
    }
    catch { }

    return false;
}
private bool ApplyHadesCalligrapherSkill(List<int> selectedHandList)
{
    if (selectedHandList == null || selectedHandList.Count != 1) return false;

    int srcIdx = selectedHandList[0];
    if (srcIdx < 0 || srcIdx >= hand.Count) return false;

    string srcId = hand[srcIdx];
    if (!TryParseSuitNum(srcId, out int srcSuit, out int _)) return false;
    if (srcSuit >= 3) return false;

    string[] honors = { "East", "South", "West", "North", "White", "Green", "Red" };
    int[] counts = new int[7];

    for (int i = 0; i < hand.Count; i++)
    {
        if (i == srcIdx) continue;

        string id = hand[i];
        int honorIndex = Array.IndexOf(honors, id);
        if (honorIndex >= 0)
        {
            counts[honorIndex]++;
        }
    }

    int maxCount = 0;
    for (int i = 0; i < counts.Length; i++)
    {
        if (counts[i] > maxCount)
            maxCount = counts[i];
    }

    string newId = null;

    if (maxCount <= 0)
    {
        newId = honors[rng.Next(0, honors.Length)];
    }
    else
    {
        if (maxCount >= 3)
        {
            if (statusTMP) statusTMP.text = GetGameFixedText_Local("relic_effect_cannot_activate_three_or_more_honors");
            return false;
        }

        var candidates = new List<string>();
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] == maxCount)
                candidates.Add(honors[i]);
        }

        if (candidates.Count == 0) return false;

        newId = candidates[rng.Next(0, candidates.Count)];
    }

    bool ok = ReplaceHandAt(srcIdx, newId);
    if (ok)
    {
        if (statusTMP) statusTMP.text = string.Format(GetGameFixedText_Local("relic_effect_transform"), newId);
    }
    return ok;
}
private void OnClickConfirm()
{
    if (phase == Phase.Scoring) return;

    // --- 手牌から捨てる系（鳴き後／一時的要求） ---
    if (phase == Phase.NeedDiscardAfterCall || phase == Phase.NeedDiscardN)
    {
        if (selHand.Count != 1)
        {
            if (statusTMP) statusTMP.text = GetGameFixedText_Local("select_one_hand_tile");
            return;
        }
        int idx = -1; foreach (var i in selHand) { idx = i; break; }
        if (idx < 0 || idx >= hand.Count) { if (statusTMP) statusTMP.text = GetGameFixedText_Local("skill_invalid_selection"); return; }

string id = hand[idx];
hand.RemoveAt(idx);
discards.Add(id);

try
{
    if (AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayDiscardTileSE();
    }
}
catch { }

SortHand();
RefreshHandUI();
RefreshDiscardUI();

selHand.Clear();
RebuildRaiseOverlays(handArea, selHand, hand);
        if (EnemyAddon_TryRonOnPlayerDiscard(id))
        {
            return;
        }

        // 次へ（敵ターンへ移行）
        phase = Phase.EnemyTurn;
        UpdateButtons(); // ★追加：敵の思考中は「捨てる」等のボタンを無効化
        StartCoroutine(EnterEnemyTurnAfterPlayerAfterDelay(0.5f));
        return;
    }

    // --- オファーフェーズ：使わなかった4枚を捨てる ---
    if (phase == Phase.Offer)
    {
        if (offers.Count > 0)
        {
            // 今回このターンでプレイヤーが捨てる牌のリストを控えておく
            var discardedThisTurn = new List<string>(offers);

            // ★追加：このターンの捨て牌が discards のどこから積まれるか（左端＝先頭＝baseIndex）
            int baseIndex = discards.Count;

foreach (var id in discardedThisTurn) discards.Add(id);

            // ★追加：プレイヤーのリーチ宣言ターンに捨てた4枚のうち「一番左」を記録（敵と同じ挙動）
            //   - すでに記録済みなら上書きしない
            //   - リーチ宣言ターンは「OnClickRiichi() が記録した _playerRiichiDeclaredTsumoCountThisRound」と
            //     現在ターンの _playerTsumoCountThisRound が一致していることで判定する
            if (isRiichi &&
                _playerRiichiDiscardHighlightIndex < 0 &&
                _playerRiichiDeclaredTsumoCountThisRound >= 0 &&
                _playerRiichiDeclaredTsumoCountThisRound == _playerTsumoCountThisRound)
            {
                _playerRiichiDiscardHighlightIndex = baseIndex;
            }

offers.Clear();
selOffer.Clear();
RefreshOfferUI();
try
{
    if (AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayDiscardTileSE();
    }
}
catch { }

RefreshDiscardUI();
            if (EnemyAddon_TryRonOnPlayerDiscards(discardedThisTurn))
            {
                // 敵和了演出に入っているので、このターンの進行はここで終了
                return;
            }
        }
        // 敵ターンへ
        phase = Phase.EnemyTurn;
        UpdateButtons(); // ★追加：敵ターンへ移行した瞬間に底のボタン群を無効化
        StartCoroutine(EnterEnemyTurnAfterPlayerAfterDelay(0.5f));
        return;
    }
}
private IEnumerator EnterEnemyTurnAfterPlayerAfterDelay(float seconds)
{
    yield return new WaitForSeconds(seconds);
    if (phase != Phase.EnemyTurn) yield break;
    EnterEnemyTurnAfterPlayer();
}
// ★追加：敵手牌のリーパイ（右→左表示はUI側、ここは内部順序の安定化）
private void EnemyRiipaiHand()
{
    _enemyHand.Sort((a, b) => EnemyTileSortKey(a).CompareTo(EnemyTileSortKey(b)));
}

// ★追加：牌IDから並び順キーを作る（萬→筒→索→字、1→9）
private int EnemyTileSortKey(string id)
{
    // 例: "m1", "p5", "s9", "E", "S", "W", "N", "P", "F", "C" などを想定
    // あなたのID体系が違う場合は、ここだけ合わせればOK
    if (string.IsNullOrEmpty(id)) return int.MaxValue;

    // 字牌（東南西北白發中）を後ろへ
    // ※既存のID体系に合わせて調整してください
    switch (id)
    {
        case "E": return 300 + 1;
        case "S": return 300 + 2;
        case "W": return 300 + 3;
        case "N": return 300 + 4;
        case "P": return 300 + 5; // 白
        case "F": return 300 + 6; // 發
        case "C": return 300 + 7; // 中
    }

    // 数牌：先頭1文字が m/p/s で、残りが数字…という前提
    char suit = id[0];
    int num = 0;
    if (id.Length >= 2) int.TryParse(id.Substring(1), out num);

    int suitBase = 0;
    if (suit == 'm') suitBase = 0;
    else if (suit == 'p') suitBase = 100;
    else if (suit == 's') suitBase = 200;
    else suitBase = 400;

    return suitBase + num;
}
// ===============================
//  Shanten (13 tiles) UI + Calc
//  - Uses existing TryToIndex34 / EnumerateAllTileIds34 / Shanten* methods
//  - Supports open melds as fixed meld count
// ===============================

private struct ShantenKey : System.IEquatable<ShantenKey>
{
    public ulong a;
    public ulong b;

    public bool Equals(ShantenKey other) => a == other.a && b == other.b;
    public override bool Equals(object obj) => obj is ShantenKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int ha = a.GetHashCode();
            int hb = b.GetHashCode();
            return (ha * 397) ^ hb;
        }
    }
}
private readonly Dictionary<ShantenKey, int> _shantenCache = new Dictionary<ShantenKey, int>(4096);
private readonly Dictionary<ShantenKey, int> _shanten14Cache = new Dictionary<ShantenKey, int>(4096);
private Coroutine _shantenBlinkCo;
private int CalcShanten14_TotalCached(List<string> concealedTiles, int fixedMeldCount)
{
    int[] c = new int[34];
    for (int i = 0; i < concealedTiles.Count; i++)
    {
        string logicId = StripTileIdForLogic(concealedTiles[i]);
        if (TryToIndex34(logicId, out int ix))
        {
            if (c[ix] < 4) c[ix]++;
        }
    }

    ShantenKey key = PackCountsToKey_WithFixedMelds(c, fixedMeldCount);
    if (_shanten14Cache.TryGetValue(key, out int cached))
        return cached;

    int sh14 = Shanten14_CalcByCounts_WithFixedMelds(c, fixedMeldCount);

    if (_shanten14Cache.Count > 8192) _shanten14Cache.Clear();
    _shanten14Cache[key] = sh14;

    return sh14;
}
private void StartShantenBlink()
{
    if (shantenTMP == null) return;
    if (_shantenBlinkCo != null) return;

    _shantenBlinkCo = StartCoroutine(ShantenBlink_Co());
}

private void StopShantenBlink(bool resetAlpha)
{
    if (_shantenBlinkCo != null)
    {
        StopCoroutine(_shantenBlinkCo);
        _shantenBlinkCo = null;
    }

    if (resetAlpha) SetShantenAlpha(1f);
}

private void SetShantenAlpha(float a01)
{
    if (shantenTMP == null) return;

    var c = shantenTMP.color;
    c.a = Mathf.Clamp01(a01);
    shantenTMP.color = c;
}

private IEnumerator ShantenBlink_Co()
{
    // 念のため、開始時にアルファを1へ
    SetShantenAlpha(1f);

    while (true)
    {
        float outSec = Mathf.Max(0.01f, shantenBlinkFadeOutSeconds);
        float inSec  = Mathf.Max(0.01f, shantenBlinkFadeInSeconds);
        float minA   = Mathf.Clamp01(shantenBlinkMinAlpha);

        // フェードアウト（1 -> minA）
        float t = 0f;
        while (t < outSec)
        {
            if (shantenTMP == null) { _shantenBlinkCo = null; yield break; }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / outSec);
            float a = Mathf.Lerp(1f, minA, k);
            SetShantenAlpha(a);
            yield return null;
        }

        // フェードイン（minA -> 1）
        t = 0f;
        while (t < inSec)
        {
            if (shantenTMP == null) { _shantenBlinkCo = null; yield break; }

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / inSec);
            float a = Mathf.Lerp(minA, 1f, k);
            SetShantenAlpha(a);
            yield return null;
        }
    }
}
private void UpdateShantenUI()
{
    if (shantenTMP == null) return;

    // スコア中や和了後は表示しない（誤表示/負荷回避）
    if (phase == Phase.Scoring || _playerHasWonThisHand)
    {
        StopShantenBlink(true);
        shantenTMP.text = "";
        SetShantenAlpha(1f);
        shantenTMP.color = shantenNormalColor;
        return;
    }

    if (hand == null || melds == null)
    {
        StopShantenBlink(true);
        shantenTMP.text = "";
        SetShantenAlpha(1f);
        shantenTMP.color = shantenNormalColor;
        return;
    }

    int fixedMeldCount = GetPlayerFixedMeldCount();
    int meldTileCount = GetPlayerMeldTileCount();
    int totalTiles = hand.Count + meldTileCount;

    // 「13枚 or 14枚」を表示対象にする
    if (!(totalTiles == 13 || totalTiles == 14))
    {
        StopShantenBlink(true);
        shantenTMP.text = "";
        SetShantenAlpha(1f);
        shantenTMP.color = shantenNormalColor;
        return;
    }

    // リーチ中は「リーチ」表示（明滅OFF）
    if (isRiichi)
    {
        StopShantenBlink(true);
        shantenTMP.text = GetGameFixedText_Local("shanten_riichi");
        shantenTMP.color = shantenRiichiColor;
        SetShantenAlpha(1f);
        return;
    }

    int sh;
    if (totalTiles == 13)
    {
        // 13枚：従来通り
        sh = CalcShanten13_TotalCached(hand, fixedMeldCount);
    }
    else
    {
        // 14枚：14枚としてのシャンテンを表示（ここがズレの原因だった）
        sh = CalcShanten14_TotalCached(hand, fixedMeldCount);
    }

    if (sh < 0)
    {
        StopShantenBlink(true);
        shantenTMP.text = GetGameFixedText_Local("shanten_agari");
        shantenTMP.color = shantenTenpaiColor;
        SetShantenAlpha(1f);
        return;
    }

    if (sh == 0)
    {
        shantenTMP.text = GetGameFixedText_Local("shanten_tenpai");
        shantenTMP.color = shantenTenpaiColor;
        StartShantenBlink();
    }
    else
    {
        StopShantenBlink(true);

        int clamped = Mathf.Min(5, sh);
        shantenTMP.text = clamped.ToString() + GetGameFixedText_Local("shanten_suffix");
        shantenTMP.color = shantenNormalColor;
        SetShantenAlpha(1f);
    }
}
// 14枚のとき：1枚捨てた後（13枚）で最も良いシャンテン（最小値）を返す
private int CalcShanten13_MinAfterDiscardCached(List<string> concealedTiles14, int fixedMeldCount)
{
    if (concealedTiles14 == null) return 6;
    if (concealedTiles14.Count != 14) return CalcShanten13_TotalCached(concealedTiles14, fixedMeldCount);

    int best = int.MaxValue;

    // 1枚ずつ「捨てた」想定で13枚にして、13枚シャンテンを評価
    for (int i = 0; i < concealedTiles14.Count; i++)
    {
        var tmp13 = new List<string>(13);
        for (int j = 0; j < concealedTiles14.Count; j++)
        {
            if (j == i) continue;
            tmp13.Add(concealedTiles14[j]);
        }

        int sh = CalcShanten13_TotalCached(tmp13, fixedMeldCount);
        if (sh < best) best = sh;

        // 既にテンパイ相当まで行ったら打ち切り（-1/0 など）
        if (best <= 0) break;
    }

    if (best == int.MaxValue) best = 6;
    return best;
}
private int GetPlayerMeldTileCount()
{
    int n = 0;
    for (int i = 0; i < melds.Count; i++)
    {
        if (melds[i] == null) continue;

        // ★null/空文字/空白は「牌なし」なので数えない
        for (int j = 0; j < melds[i].Count; j++)
        {
            string s = melds[i][j];
            if (string.IsNullOrWhiteSpace(s)) continue;
            n++;
        }
    }
    return n;
}

// 副露面子数（チー/ポン/カン）を固定面子として数える
private int GetPlayerFixedMeldCount()
{
    int cnt = 0;
    for (int i = 0; i < melds.Count; i++)
    {
        if (melds[i] == null) continue;

        // ★null/空文字/空白を除いた「実牌数」で判定する
        int realTiles = 0;
        for (int j = 0; j < melds[i].Count; j++)
        {
            string s = melds[i][j];
            if (string.IsNullOrWhiteSpace(s)) continue;
            realTiles++;
        }

        // 3枚以上あれば「面子」として扱う（カンは4枚）
        if (realTiles >= 3) cnt++;
    }

    if (cnt < 0) cnt = 0;
    if (cnt > 4) cnt = 4;
    return cnt;
}
// 13枚（total）用：
//  - これまでの方式（13枚に1枚足して「14枚シャンテン最小」+1）を基準として計算する
//  - それと並行して、門前なら七対子/国士の13枚シャンテンも計算し、進んでいる方（小さい方）を優先して返す
// ※fixedMeldCount は「すでに完成している副露面子数」
private int CalcShanten13_TotalCached(List<string> concealedTiles, int fixedMeldCount)
{
    int[] c = new int[34];
for (int i = 0; i < concealedTiles.Count; i++)
{
    string logicId = StripTileIdForLogic(concealedTiles[i]);
    if (TryToIndex34(logicId, out int ix))
    {
        if (c[ix] < 4) c[ix]++;
    }
}
    ShantenKey key = PackCountsToKey_WithFixedMelds(c, fixedMeldCount);
    if (_shantenCache.TryGetValue(key, out int cached))
        return cached;

    // ---- これまでの方式（基準）----
    // 13枚 → 1枚足して 14枚シャンテン（通常/七対子/国士を含む）を計算し、最小値 + 1 を 13枚シャンテンとする
    int minSh14 = int.MaxValue;

    foreach (var t in EnumerateAllTileIds34())
    {
        if (!TryToIndex34(t, out int addIx)) continue;
        if (c[addIx] >= 4) continue;

        c[addIx]++;
        int sh14 = Shanten14_CalcByCounts_WithFixedMelds(c, fixedMeldCount);
        c[addIx]--;

        if (sh14 < minSh14) minSh14 = sh14;
    }

    if (minSh14 == int.MaxValue) minSh14 = 8;

    int shBase13 = minSh14 + 1;

    // ---- 並行計算（門前のみ）----
    // 七対子/国士の13枚シャンテンも直接計算し、進んでいる方（小さい方）を優先
    int sh13 = shBase13;

    if (fixedMeldCount == 0)
    {
        int shChiitoi13 = Shanten_Chiitoi(c);
        int shKokushi13 = Shanten_Kokushi(c);

        if (shChiitoi13 < sh13) sh13 = shChiitoi13;
        if (shKokushi13 < sh13) sh13 = shKokushi13;
    }

    // キャッシュ肥大化対策（雑に上限でクリア）
    if (_shantenCache.Count > 8192) _shantenCache.Clear();
    _shantenCache[key] = sh13;

    return sh13;
}
// 通常形（一般手）だけの 13枚シャンテン
// 「1枚足して14枚の通常形シャンテンの最小」を取り、+1 して返す
private int CalcShanten13_NormalOnly_ByMinAdd1(int[] counts13)
{
    int minSh14 = int.MaxValue;

    foreach (var t in EnumerateAllTileIds34())
    {
        if (!TryToIndex34(t, out int addIx)) continue;
        if (counts13[addIx] >= 4) continue;

        counts13[addIx]++;
        int sh14 = Shanten_Normal(counts13);
        counts13[addIx]--;

        if (sh14 < minSh14) minSh14 = sh14;
    }

    if (minSh14 == int.MaxValue) minSh14 = 8;
    return minSh14 + 1;
}
// counts[34](3bit×34) を2つのulongへ詰め、さらに fixedMeldCount(0..4) を b の上位へ埋め込む
private ShantenKey PackCountsToKey_WithFixedMelds(int[] counts, int fixedMeldCount)
{
    ulong a = 0;
    ulong b = 0;

    int shift = 0;
    for (int i = 0; i <= 16; i++)
    {
        a |= ((ulong)(counts[i] & 7)) << shift;
        shift += 3;
    }

    shift = 0;
    for (int i = 17; i <= 33; i++)
    {
        b |= ((ulong)(counts[i] & 7)) << shift;
        shift += 3;
    }

    // b の上位3bitに固定面子数(0..7)を入れる（0..4しか使わない）
    b |= ((ulong)(fixedMeldCount & 7)) << 61;

    return new ShantenKey { a = a, b = b };
}

// 14枚（total）シャンテン（-1:アガリ、0:テンパイ…）を「副露面子固定」で計算
private int Shanten14_CalcByCounts_WithFixedMelds(int[] c, int fixedMeldCount)
{
    // 副露しているなら七対子・国士は成立しないので通常手のみ
    if (fixedMeldCount > 0)
    {
        return Shanten_Normal_WithFixedMelds(c, fixedMeldCount);
    }

    // 門前なら既存ロジックをそのまま使う（あなたのプロジェクトで正しく動作済み）
    return Shanten14_CalcByCounts(c);
}

// 一般手（4面子1雀頭）を「固定面子数 fixedMeldCount」込みで計算
private int Shanten_Normal_WithFixedMelds(int[] countsSrc, int fixedMeldCount)
{
    int fixedM = fixedMeldCount;
    if (fixedM < 0) fixedM = 0;
    if (fixedM > 4) fixedM = 4;

    int min = 8;

    // 作業用配列（countsSrc を汚さない）
    int[] work = new int[34];
    for (int i = 0; i < 34; i++) work[i] = countsSrc[i];

    void Dfs(int idx, int meld, int taatsu, int pair)
    {
        // meld が増えすぎるのは意味がない
        if (meld > 4) meld = 4;

        // taatsu は最大 (4 - meld)
        if (taatsu > 4 - meld) taatsu = 4 - meld;

        // 末尾：シャンテン計算
        if (idx >= 34)
        {
            int sh = 8 - meld * 2 - taatsu - pair;
            if (sh < min) min = sh;
            return;
        }

        // 空なら次へ
        if (work[idx] == 0)
        {
            Dfs(idx + 1, meld, taatsu, pair);
            return;
        }

        // 刻子
        if (work[idx] >= 3)
        {
            work[idx] -= 3;
            Dfs(idx, meld + 1, taatsu, pair);
            work[idx] += 3;
        }

        // 順子（数牌のみ）
        if (idx < 27)
        {
            int pos = idx % 9;
            if (pos <= 6 && work[idx] > 0 && work[idx + 1] > 0 && work[idx + 2] > 0)
            {
                work[idx]--; work[idx + 1]--; work[idx + 2]--;
                Dfs(idx, meld + 1, taatsu, pair);
                work[idx]++; work[idx + 1]++; work[idx + 2]++;
            }
        }

        // 雀頭（未使用なら）
        if (pair == 0 && work[idx] >= 2)
        {
            work[idx] -= 2;
            Dfs(idx, meld, taatsu, 1);
            work[idx] += 2;
        }

        // 対子ターツ
        if (work[idx] >= 2)
        {
            work[idx] -= 2;
            Dfs(idx, meld, taatsu + 1, pair);
            work[idx] += 2;
        }

        // 両面ターツ / 嵌張ターツ（数牌のみ）
        if (idx < 27)
        {
            int pos = idx % 9;

            // 両面
            if (pos <= 7 && work[idx] > 0 && work[idx + 1] > 0)
            {
                work[idx]--; work[idx + 1]--;
                Dfs(idx, meld, taatsu + 1, pair);
                work[idx]++; work[idx + 1]++;
            }

            // 嵌張
            if (pos <= 6 && work[idx] > 0 && work[idx + 2] > 0)
            {
                work[idx]--; work[idx + 2]--;
                Dfs(idx, meld, taatsu + 1, pair);
                work[idx]++; work[idx + 2]++;
            }
        }

        // 1枚落として次へ
        work[idx]--;
        Dfs(idx, meld, taatsu, pair);
        work[idx]++;
    }

    Dfs(0, fixedM, 0, 0);
    return min;
}

private void EnemyAI_SelectKeep13AndDrop4(
    List<string> tiles17,
    out List<string> keep13,
    out List<string> drop4)
{
    keep13 = new List<string>(13);
    drop4 = new List<string>(4);

    int n = tiles17.Count;
    if (n != 17)
    {
        keep13.AddRange(tiles17.Take(Mathf.Min(13, n)));
        drop4.AddRange(tiles17.Skip(keep13.Count));
        return;
    }

    // 13枚中の「字牌単騎（トイツ未満の字牌）」数を数える
    int CountHonorSingletons13(List<string> hand13)
    {
        int[] c = new int[34];
        for (int ii = 0; ii < hand13.Count; ii++)
        {
            if (TryToIndex34(StripTileIdForLogic(hand13[ii]), out int ix)) c[ix]++;
        }
        int singles = 0;
        for (int ix = 27; ix < 34; ix++)
        {
            if (c[ix] == 1) singles++;
        }
        return singles;
    }

    // 1) 「捨て候補」を"悪そうな牌"上位Kに絞る（10C4=210 通りなので軽い）
    const int K = 10;
    var idxs = new List<int>(n);
    for (int i = 0; i < n; i++) idxs.Add(i);

    idxs.Sort((a, b) =>
    {
        int ba = EnemyAI_TileBadness_Quick(tiles17, a);
        int bb = EnemyAI_TileBadness_Quick(tiles17, b);
        return bb.CompareTo(ba);
    });

    var cand = idxs.Take(K).ToList();
    if (cand.Count < 4)
    {
        keep13.AddRange(tiles17.Take(13));
        drop4.AddRange(tiles17.Skip(13));
        return;
    }

    // ======================================================
    //  2) 全候補を収集（速度軸・得点軸のスコアを個別に記録）
    // ======================================================
    var allCandidates = new List<(List<string> keep, List<string> drop,
                                  int sh, int honorSingles, int ukeire,
                                  int expected, int shape)>();

    for (int a = 0; a < cand.Count - 3; a++)
    for (int b = a + 1; b < cand.Count - 2; b++)
    for (int c = b + 1; c < cand.Count - 1; c++)
    for (int d = c + 1; d < cand.Count; d++)
    {
        int i = cand[a], j = cand[b], k = cand[c], l = cand[d];

        var keepT = new List<string>(13);
        var dropT = new List<string>(4);

        for (int t = 0; t < n; t++)
        {
            if (t == i || t == j || t == k || t == l) dropT.Add(tiles17[t]);
            else keepT.Add(tiles17[t]);
        }

        if (HasQuadOrMore13(keepT)) continue;

        int sh = ComputeShantenFor13_ByMinAdd1(keepT, out int ukeire, out int expected);
        int honorSingles = CountHonorSingletons13(keepT);
        int shape = ComputeTaatsuShapeScore13(keepT);

        allCandidates.Add((keepT, dropT, sh, honorSingles, ukeire, expected, shape));
    }

    // 候補なしフォールバック
    if (allCandidates.Count == 0)
    {
        var dropIdxSet = new HashSet<int>(cand.Take(4));
        for (int t = 0; t < n; t++)
        {
            if (dropIdxSet.Contains(t)) drop4.Add(tiles17[t]);
            else keep13.Add(tiles17[t]);
        }
        return;
    }

    // ======================================================
    //  3) AIレベルに基づく確率的選択
    //     ① 速度軸（shanten → 字牌単騎 → 受け入れ）
    //     ② 得点軸（期待点 → ターツ形）
    //     各軸について、確率Pで最適選択 / (1-P)でランダム選択
    // ======================================================
    GetCurrentEnemyAISettings(out int aiLevel, out int speedPct, out int scorePct);

    bool speedOptimal = (UnityEngine.Random.Range(0, 100) < speedPct);
    bool scoreOptimal = (UnityEngine.Random.Range(0, 100) < scorePct);

    // --- ① 速度軸フィルタ ---
    List<(List<string> keep, List<string> drop,
          int sh, int honorSingles, int ukeire,
          int expected, int shape)> filtered;

    if (speedOptimal)
    {
        // 最適：最小シャンテン → 字牌単騎最少 → 受け入れ最大 でフィルタ
        int bestSh = allCandidates.Min(x => x.sh);
        var g1 = allCandidates.Where(x => x.sh == bestSh).ToList();

        int bestHS = g1.Min(x => x.honorSingles);
        var g2 = g1.Where(x => x.honorSingles == bestHS).ToList();

        int bestUk = g2.Max(x => x.ukeire);
        filtered = g2.Where(x => x.ukeire == bestUk).ToList();
    }
    else
    {
        // 非最適：最良シャンテン+2 以内に緩める（極端な悪手は防止）
        int bestSh = allCandidates.Min(x => x.sh);
        filtered = allCandidates.Where(x => x.sh <= bestSh + 2).ToList();
        if (filtered.Count == 0) filtered = new List<(List<string>, List<string>, int, int, int, int, int)>(allCandidates);
    }

    // --- ② 得点軸で選択 ---
    (List<string> keep, List<string> drop,
     int sh, int honorSingles, int ukeire,
     int expected, int shape) chosen;

    if (scoreOptimal)
    {
        // 最適：期待点→形の順で最良を選ぶ
        chosen = filtered
            .OrderByDescending(x => x.expected)
            .ThenByDescending(x => x.shape)
            .First();
    }
    else
    {
        // 非最適：filtered からランダムに選ぶ
        chosen = filtered[UnityEngine.Random.Range(0, filtered.Count)];
    }

    keep13.AddRange(chosen.keep);
    drop4.AddRange(chosen.drop);
}
private int EnemyAI_TileBadness_Quick(List<string> tiles, int idx)
{
    // 34カウント
    int[] c = new int[34];
    for (int i = 0; i < tiles.Count; i++)
    {
        if (TryToIndex34(tiles[i], out int ix)) c[ix]++;
    }

    if (!TryToIndex34(tiles[idx], out int x)) return 0;

    bool isHonor = (x >= 27);
    bool isSuited = (x < 27);
    int suit = isSuited ? (x / 9) : -1;
    int num = isSuited ? (x % 9) + 1 : -1;

    // ===== 4枚以上同一牌の扱い（敵はカンしないが、3枚は刻子として残す） =====
    // 「この idx の1枚が、同牌グループの何枚目か」を確定させる（安定・決定的）
    // 4枚以上ある場合：
    //   字牌：4枚目以降を“余り”として bad +40（ただし単騎字牌+200は付けない）
    //   数牌：余りは“孤立牌”として扱う（bad +40 は付けない。つながり評価は効かせる）
    var occ = new List<int>(tiles.Count);
    for (int i = 0; i < tiles.Count; i++)
    {
        if (TryToIndex34(tiles[i], out int ix) && ix == x) occ.Add(i);
    }
    occ.Sort();

    bool isExtraFromQuadOrMore = false;
    if (occ.Count >= 4)
    {
        // 先頭3枚を「刻子として残す側」、4枚目以降を「余り」として扱う
        // idx が occ[3] 以降なら余り
        int pos = occ.IndexOf(idx);
        if (pos >= 3) isExtraFromQuadOrMore = true;
    }

    // effectiveCount は「この1枚を、評価上どう見なすか」
    //   4枚以上のとき：
    //     字牌：先頭3枚は count=3（刻子）、余りは count=1（ただし単騎字牌+200は抑止）
    //     数牌：先頭3枚は count=3（刻子）、余りは count=1
    int effectiveCount = c[x];
    if (occ.Count >= 4)
    {
        if (!isExtraFromQuadOrMore) effectiveCount = 3;
        else effectiveCount = 1;
    }

    // ===== 役牌対子（yakuhai pair）の判定：役牌の対子は常に -4 =====
    string seatWind = __EnemyAddon_GetEnemySeatWind_FromPlayerSeat();
    string roundWind = GetRoundWind();

    bool IsYakuhaiHonorIndex(int ix)
    {
        if (ix < 27) return false;

        // 三元牌
        if (ix == 31 || ix == 32 || ix == 33) return true;

        // 風牌（場風 / 自風）
        if (ix == 27) return (seatWind == "East" || roundWind == "East");
        if (ix == 28) return (seatWind == "South" || roundWind == "South");
        if (ix == 29) return (seatWind == "West" || roundWind == "West");
        if (ix == 30) return (seatWind == "North" || roundWind == "North");

        return false;
    }

    // ===== 対子の“何個目か”の扱い（七対子を減らす） =====
    // 非役牌の対子は「最も頭に向く1つ」だけ -4、残りは -1
    int primaryNonYakuhaiPairIndex = -1;
    {
        int bestScore = int.MinValue;

        for (int i = 0; i < 34; i++)
        {
            if (c[i] != 2) continue;
            if (IsYakuhaiHonorIndex(i)) continue;

            int score = 0;

            // 数牌なら「つながり」がある対子を頭候補として優先
            if (i < 27)
            {
                int s = i / 9;
                int baseIx = s * 9;

                int p = i - 1;
                int n = i + 1;
                int p2 = i - 2;
                int n2 = i + 2;

                if (p >= baseIx && c[p] > 0) score += 2;
                if (n < baseIx + 9 && c[n] > 0) score += 2;
                if (p2 >= baseIx && c[p2] > 0) score += 1;
                if (n2 < baseIx + 9 && c[n2] > 0) score += 1;

                int nn = (i % 9) + 1;
                if (nn == 1 || nn == 9) score -= 1; // 端は弱め
            }
            else
            {
                // 役牌でない字牌の対子は頭として弱め
                score -= 1;
            }

            // 同点なら index が小さい方を採用（決定的）
            if (score > bestScore || (score == bestScore && (primaryNonYakuhaiPairIndex < 0 || i < primaryNonYakuhaiPairIndex)))
            {
                bestScore = score;
                primaryNonYakuhaiPairIndex = i;
            }
        }
    }

    int bad = 0;

    // ===== 孤立牌 / 対子 / 刻子の評価（effectiveCount を使う） =====
    if (effectiveCount == 1) bad += 4;

    if (effectiveCount == 2)
    {
        // 役牌の対子は常に -4
        if (IsYakuhaiHonorIndex(x))
        {
            bad -= 4;
        }
        else
        {
            // 非役牌対子は「1つだけ -4、2つ目以降は -1」
            if (x == primaryNonYakuhaiPairIndex) bad -= 4;
            else bad -= 1;
        }
    }

    // 刻子(3枚)は強く保持
    if (effectiveCount == 3) bad -= 20;

    // ===== 4枚以上の“余り”にだけペナルティを付ける（字牌のみ） =====
    // 数牌は「刻子3 + 孤立牌1」で、孤立牌側にも隣接評価を効かせるため +40 は付けない
    if (occ.Count >= 4 && isExtraFromQuadOrMore && isHonor)
    {
        bad += 40;
    }

    // ★最重要：単騎字牌は最優先で捨てる（ただし「4枚以上の余り」を単騎扱いで+200にしない）
    if (isHonor && effectiveCount == 1 && !(occ.Count >= 4 && isExtraFromQuadOrMore))
    {
        bad += 200;
    }

    // 字牌は基本捨て寄り（ただし対子/刻子なら上で相殺される）
    if (isHonor) bad += 3;

    // 端牌は繋がりが弱いので少し捨て寄り
    if (isSuited && (num == 1 || num == 9)) bad += 2;

    // つながり（隣/2つ隣があるなら残し寄り）
    // ※余りの数牌（effectiveCount==1）にもここが効くので、
    //   「4枚目を隣とつなげてターツ/メンツに寄せる」挙動が出る
    if (isSuited)
    {
        int baseIx = suit * 9;
        int p = x - 1;
        int n = x + 1;
        int p2 = x - 2;
        int n2 = x + 2;

        if (p >= baseIx && c[p] > 0) bad -= 2;
        if (n < baseIx + 9 && c[n] > 0) bad -= 2;
        if (p2 >= baseIx && c[p2] > 0) bad -= 1;
        if (n2 < baseIx + 9 && c[n2] > 0) bad -= 1;
    }

    return bad;
}

// ★追加：同一牌4枚以上を禁止（敵はカンしない）
private bool EnemyAI_HasQuadOrMore(List<string> hand)
{
    var cnt = new Dictionary<string, int>();
    for (int i = 0; i < hand.Count; i++)
    {
        string baseId = StripTileIdForLogic(hand[i]); // IsTenpai と同じ正規化を流用:contentReference[oaicite:5]{index=5}
        if (!cnt.ContainsKey(baseId)) cnt[baseId] = 0;
        cnt[baseId]++;
        if (cnt[baseId] >= 4) return true;
    }
    return false;
}

// ★追加：13枚の形評価（シャンテン相当 ＞ 受け入れ相当 ＞ 点数相当 ＞ ターツ優先）
private int EnemyAI_EvaluateKeep13(List<string> hand)
{
    // 1) ターツ/面子としての評価（両面＞嵌張＞双碰＞単騎）
    // 2) 対子/刻子の偏りを抑える（同牌だらけを減点）
    // 3) 字牌単騎は強めに減点

    // counts: 正規化ID -> count
    var cnt = new Dictionary<string, int>();
    for (int i = 0; i < hand.Count; i++)
    {
        string baseId = StripTileIdForLogic(hand[i]); // IsTenpai と同じ:contentReference[oaicite:6]{index=6}
        if (!cnt.ContainsKey(baseId)) cnt[baseId] = 0;
        cnt[baseId]++;
    }

    int score = 0;

    // (A) 同牌偏重を抑制：3枚目まではOK、4枚目は前段で除外、3枚でも少し減点
    foreach (var kv in cnt)
    {
        if (kv.Value == 3) score -= 10;
        else if (kv.Value == 2) score += 8;  // 対子は有用
        else if (kv.Value == 1) score += 0;
    }

    // (B) 数牌のターツ優先（両面＞嵌張）
    // ここでは「連結の強さ」を点にする（面子完成に近いほど加点）
    foreach (var kv in cnt)
    {
        string id = kv.Key;
        if (!TryParseSuitNum(id, out int suit, out int num)) continue; // EnemyTileSortKey が使っている前提:contentReference[oaicite:7]{index=7}
        if (suit == 3) continue; // 字牌

        // 近傍の存在で加点（両面が最優先）
        string n1 = suit == 0 ? $"Man{num+1}" : suit == 1 ? $"Pin{num+1}" : $"Sou{num+1}";
        string p1 = suit == 0 ? $"Man{num-1}" : suit == 1 ? $"Pin{num-1}" : $"Sou{num-1}";
        string n2 = suit == 0 ? $"Man{num+2}" : suit == 1 ? $"Pin{num+2}" : $"Sou{num+2}";
        string p2 = suit == 0 ? $"Man{num-2}" : suit == 1 ? $"Pin{num-2}" : $"Sou{num-2}";

        bool hasP1 = num >= 2 && cnt.ContainsKey(p1);
        bool hasN1 = num <= 8 && cnt.ContainsKey(n1);
        bool hasP2 = num >= 3 && cnt.ContainsKey(p2);
        bool hasN2 = num <= 7 && cnt.ContainsKey(n2);

        // 両面（(n-1,n) or (n,n+1) が中張なら強い）
        if (hasP1) score += (num >= 3 ? 18 : 10); // 1-2の辺張は弱め
        if (hasN1) score += (num <= 7 ? 18 : 10);

        // 嵌張（n-2 or n+2）
        if (hasP2) score += 7;
        if (hasN2) score += 7;
    }

    // (C) 字牌単騎を減点（対子以上ならOK）
    foreach (var kv in cnt)
    {
        string id = kv.Key;
        if (TryParseSuitNum(id, out int suit, out int num) && suit == 3)
        {
            if (kv.Value == 1) score -= 12;
            else if (kv.Value == 2) score += 6;
            else if (kv.Value == 3) score += 3;
        }
    }

    // (D) 「聴牌しているか」を最優先で強く加点（＝シャンテン優先の近似）
    // IsTenpai は hand(13) で使える実装が添付内にある:contentReference[oaicite:8]{index=8}
    if (IsTenpai(hand))
        score += 5000;

    // 最後に安定のリーパイ用ソートキーで微調整（同点でのブレを減らす）
    score += hand.Sum(t => -EnemyTileSortKey(t)) / 1000;

    return score;
}


// ★追加：過去差分で StartCoroutine(__EnemyTurn_Co()) が呼ばれている互換用
private IEnumerator __EnemyTurn_Co()
{
    // 既存の敵ターン遷移と同じ待ちを入れてから処理へ
    yield return new WaitForSeconds(0.5f);
    if (phase != Phase.EnemyTurn) yield break;
    EnterEnemyTurnAfterPlayer();
}
void EnterEnemyTurnAfterPlayer()
{
    // ★追加：稀に EnemyTurn が重複実行されて、1ターンで複数回捨てることがあるのを防ぐ
    if (_enemyTurnRunning) return;

    // ★追加：敵ターンに入った時点でオファーフェーズのガードは確実に解除する
    _beginOfferPhaseInProgress = false;

    // ★修正：局のターン上限チェックは「次の敵ターンに入る直前」に行う。
    // ターンは「敵ツモ番開始を1ターン目」として数えているため、
    // ここで止めると局の最後が必ずプレイヤーターンで終わる。
    if (_enemyTurnCounter >= Mathf.Max(1, maxTurnsPerHand))
    {
        _enemyTurnRunning = false;
        ShowRyukyoku();
        return;
    }

    _enemyTurnRunning = true;

    // ★ここが今回の本命：
    //   - 局の「ターン数」は、敵のツモ番開始を 1ターン目としてカウントする
    //   - 次に回ってくる敵のツモ番開始で +1 していく
    _enemyTurnCounter++;
    _playerTsumoCountThisRound = _enemyTurnCounter;
    // ★追加：敵ツモ番開始の瞬間に、ターン表示も更新する（これが無いと表示がプレイヤー番でしか変わらない）
    RefreshTopUI();

    _currentScoringAttackerIsPlayer = false; // 以降は敵が攻撃側（敵和了はプレイヤーが被ダメ）
    EnableEnemyMeldModeAddon();

    int count = 4; // 常に4枚引く

    // すでに敵が和了済みの局：もう和了しない＆手牌は見せない（捨て牌は維持）
    if (_enemyHasWonThisHand)
    {
        var justDraw4 = new System.Collections.Generic.List<string>(count);
        for (int i = 0; i < count; i++)
            justDraw4.Add(DrawEnemyTile());

        _enemyHand.Clear();
        RefreshEnemyHandUI_FullRebuild();

        lastEnemyTurnTiles.Clear();
        lastEnemyTurnTiles.AddRange(justDraw4);
        enemyDiscards.AddRange(justDraw4);

        RefreshEnemyDiscardUI();
        WireEnemyTurnClickTargets();
        UpdateButtons();

AfterEnemyDiscardCommonFlow();
return;

    }

    // 1) 4枚ツモ
    var thisTurn = new System.Collections.Generic.List<string>(count);
    for (int i = 0; i < count; i++)
        thisTurn.Add(DrawEnemyTile());

    // =========================================================
    // A) リーチ後：一切入れ替えしない（4枚ツモ切り）＋和了判定
    // =========================================================
    if (_enemyIsRiichi)
    {
        // 仕様⑤：リーチ宣言ターンは和了しない
        if (_enemyRiichiDeclaredTurnCounter != _enemyTurnCounter)
        {
            // ツモ和了チェック（4枚の中に待ちがあれば和了）
            int baseIndexWin = enemyDiscards.Count;
            string bestWin = null;
            int bestScore = -1;
            int bestI = -1;

            for (int i = 0; i < thisTurn.Count; i++)
            {
string cand = StripTileIdForLogic(NormalizeEnemyTileId(thisTurn[i]));
if (string.IsNullOrEmpty(cand)) continue;
if (!_enemyRiichiWaits.Contains(cand)) continue;

int sc = EnemyAI_ComputeClosedHandScore(_enemyHand, cand, isTsumo: true);

                if (sc > bestScore)
                {
                    bestScore = sc;
                    bestWin = cand;
                    bestI = i;
                }
            }

            if (!string.IsNullOrEmpty(bestWin) && bestScore > 0 && bestI >= 0)
            {
                // このターンのツモ4枚も捨て牌に表示（要望どおり）
                lastEnemyTurnTiles.Clear();
                lastEnemyTurnTiles.AddRange(thisTurn);
                enemyDiscards.AddRange(thisTurn);

                // 和了牌のみグレーアウト
                enemyUsedIndices.Add(baseIndexWin + bestI);

                RefreshEnemyDiscardUI();
                WireEnemyTurnClickTargets();
                UpdateButtons();

                EnemyAI_DeclareEnemyTsumoWin(bestWin, bestScore);
                return;
            }
        }

        // 和了しないなら、ツモ4枚をそのまま捨てる（ツモ切り）
        lastEnemyTurnTiles.Clear();
        lastEnemyTurnTiles.AddRange(thisTurn);
        enemyDiscards.AddRange(thisTurn);

        RefreshEnemyDiscardUI();
        WireEnemyTurnClickTargets();
        UpdateButtons();

        ProcessEnemyAttackEffects();
        if (Mathf.Max(0, playerHP) <= 0)
        {
            StartDefeatTransitionIfNeeded();
            return;
        }

        AutoSkipEnemyIfNothing(0.5f);
        return;
    }

    // =========================================================
    // B) リーチ前：17→13入れ替え（あなたの既存AI）＋聴牌なら必ずリーチ
    // =========================================================

    // 2) 17枚を作る
    var tiles17 = new List<string>(17);
    tiles17.AddRange(_enemyHand);
    tiles17.AddRange(thisTurn);

    // 3) 残す13枚＆捨てる4枚を選ぶ
    List<string> keep13, drop4;
    EnemyAI_SelectKeep13AndDrop4(tiles17, out keep13, out drop4);

    // 4) 手牌更新＋リーパイ＋UI更新
    _enemyHand.Clear();
    _enemyHand.AddRange(keep13);
    EnemyRiipaiHand();
    RefreshEnemyHandUI_FullRebuild();

    // 5) 聴牌したら必ずリーチ（ここが今回の最重要）
    if (!_enemyIsRiichi && IsTenpai(_enemyHand))
    {
        _enemyIsInTenpai = true;
        _enemyIsInRiichi = true;               // ★追加：演出/判定がこちらを見る箇所があるため
        _enemyIsRiichi = true;
        _enemyRiichiDeclaredTurnCounter = _enemyTurnCounter;

        // 待ち牌を必ず構築（これが無いとロン/ツモが発生しない）
        FillRiichiWaitsFrom13(_enemyHand, _enemyRiichiWaits);

        // ★追加：既存の仕組みでリーチカットインを出す（UI未設定ならコルーチン側が安全に抜ける）
        if (!_enemyRiichiCutinRunning)
            StartCoroutine(EnemyAddon_ShowRiichiCutinThenEnterTenpaiUI());
    }
    // 6) 余り4枚を捨て牌へ
    lastEnemyTurnTiles.Clear();
    lastEnemyTurnTiles.AddRange(drop4);

    // ★追加：このターンの捨て牌が enemyDiscards のどこから積まれるか（左端＝先頭＝baseIndex）
    int baseIndex = enemyDiscards.Count;

    enemyDiscards.AddRange(drop4);
try
{
    if (AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayDiscardTileSE();
    }
}
catch { }
    // ★追加：敵のリーチ宣言ターンに捨てた4枚のうち「一番左」を記録（この経路が本命）
    if (_enemyIsRiichi &&
        _enemyRiichiDiscardHighlightIndex < 0 &&
        _enemyRiichiDeclaredTurnCounter >= 0 &&
        _enemyRiichiDeclaredTurnCounter == _enemyTurnCounter)
    {
        _enemyRiichiDiscardHighlightIndex = baseIndex;
    }

    // 履歴キュー（必要なら thisTurn を入れる：既存の enemyAttackIntervalTurns の都合）
    _enemyTurnHistory.Enqueue(new System.Collections.Generic.List<string>(thisTurn));
    while (_enemyTurnHistory.Count > Mathf.Max(1, enemyAttackIntervalTurns)) _enemyTurnHistory.Dequeue();

RefreshEnemyDiscardUI();
WireEnemyTurnClickTargets();
UpdateButtons();

ProcessEnemyAttackEffects();
if (Mathf.Max(0, playerHP) <= 0)
{
    StartDefeatTransitionIfNeeded();
    return;
}

// ★ここが本命：UIがまだ未生成/未配線でも、反応可能なら必ず停止する
if (PauseForPlayerReactionOnEnemyDiscard())
{
    // 次フレームでもう一度配線して赤ハイライトを確実に出す（生成タイミング吸収）
    StartCoroutine(RewireEnemyDiscardButtonsNextFrame());
    return;
}

AutoSkipEnemyIfNothing(0.5f);

}

// ★敵捨て牌に対して、プレイヤーがロン/鳴き可能なら「必ず」停止する（UI状態に依存しない）
private bool PauseForPlayerReactionOnEnemyDiscard()
{
    // このターンの敵捨て牌（lastEnemyTurnTiles）はここで必ず埋まっている
    if (lastEnemyTurnTiles == null || lastEnemyTurnTiles.Count == 0) return false;

    bool canRon = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _); // 内部で NormalizeEnemyTileId を使っている【turn55file5†L40-L50】
bool canPon = lastEnemyTurnTiles.Any(t => CanPonWithBase(NormalizeEnemyDiscardForAction(t)));
bool canChi = lastEnemyTurnTiles.Any(t => CanChiWithBase(NormalizeEnemyDiscardForAction(t)));
bool canKan = lastEnemyTurnTiles.Any(t => CanKanWithBase(NormalizeEnemyDiscardForAction(t)));

    if (!(canRon || canPon || canChi || canKan)) return false;

    // ★止める：敵ターンのまま、プレイヤーが敵捨て牌を選べる状態にする
    phase = Phase.EnemyTurn;
    selectedEnemyIndex = -1;
_autoSkipPending = false; // ★追加：停止中に勝手に進まないようにする
    RefreshEnemyDiscardUI();
    WireEnemyTurnClickTargets();
    EvaluateWinUI_New(); // Ron表示更新（phase==EnemyTurn で lastEnemyTurnTiles を見る）【turn55file14†L76-L83】
    UpdateButtons();

if (statusTMP) statusTMP.text = GetGameFixedText_Local("call_can_select_enemy_discard");
    return true;
}

// 次フレームにもう一度配線（赤ハイライトが出ない問題を潰す）
private System.Collections.IEnumerator RewireEnemyDiscardButtonsNextFrame()
{
    yield return null;
    WireEnemyTurnClickTargets();
    EvaluateWinUI_New();
    UpdateButtons();
}

private void RefreshEnemyHandUI_FullRebuild()
{
    if (!enemyHandArea) return;

    // ★追加：和了後は snapshot 優先
    var srcEnemyHand =
        (_enemyHasWonThisHand && _enemyWonHandSnapshot != null)
            ? _enemyWonHandSnapshot
            : _enemyHand;

    // ★追加：描画前に必ずリーパイ（元の手牌に対して）
    SortEnemyHandInPlace();

    for (int i = enemyHandArea.childCount - 1; i >= 0; i--)
        Destroy(enemyHandArea.GetChild(i).gameObject);

    for (int i = 0; i < srcEnemyHand.Count; i++)
    {
        var go = Instantiate(tilePrefab, enemyHandArea);
        SetupTile(go, srcEnemyHand[i], -1, clickable: false);

        if (!(enemyShowHandFaceForDebug || _enemyRevealHandNow))
            TrySetBackSprite(go);
    }

// ★敵が和了済みでも「スコアOK後」までは灰色化しない
if (_enemyHasWonThisHand && _enemyGreyOutHandAfterScoreOk) GreyOutTilesUnder(enemyHandArea);

}

private void EnemyRevealHandNow()
{
    _enemyRevealHandNow = true;
    try { RefreshEnemyHandUI_FullRebuild(); } catch {}
}

private string NormalizeEnemyTileId(string id)
{
    if (string.IsNullOrEmpty(id)) return id;

    // ★末尾だけでなく途中の "*" も落とす
    id = StripStar(id);

    // ★"Man2_xxx" 形式のサフィックスを落とす
    int us = id.IndexOf('_');
    if (us >= 0) id = id.Substring(0, us);

    return id;
}


// ★追加：敵手牌リーパイ用ソートキー（萬→筒→索→字）
private int EnemyHandSortKey(string rawId)
{
    string id = NormalizeEnemyTileId(rawId);

    // 数牌
    if (TryParseSuitNum(id, out int suit, out int num))
    {
        // TryParseSuitNum は suit: Man=0 Pin=1 Sou=2 の想定（あなたの実装に依存）
        // 0..2 を 0/100/200 にして数を足す
        return suit * 100 + num; // num は 1..9
    }

    // 字牌（東南西北白發中）
    switch (id)
    {
        case "East":  return 300 + 1;
        case "South": return 300 + 2;
        case "West":  return 300 + 3;
        case "North": return 300 + 4;
        case "White": return 300 + 5;
        case "Green": return 300 + 6;
        case "Red":   return 300 + 7;
    }

    return 999999; // 不明は最後
}

private void SortEnemyHandInPlace()
{
    _enemyHand.Sort((a, b) => EnemyHandSortKey(a).CompareTo(EnemyHandSortKey(b)));
}

// 配牌時：右から増えるように見せたいので、Hierarchy の先頭に挿し込む
private void RefreshEnemyHandUI_DealOne()
{
    if (!enemyHandArea) return;

    // 末尾（最新）を1枚だけ作る
    if (_enemyHand.Count <= 0) return;

    var id = _enemyHand[_enemyHand.Count - 1];
    var go = Instantiate(tilePrefab, enemyHandArea);
    SetupTile(go, id, -1, clickable: false);

    if (!enemyShowHandFaceForDebug)
        TrySetBackSprite(go);

    // 先頭へ＝右寄せレイアウト時に“右から埋まる”見え方に寄せる
    go.transform.SetAsFirstSibling();
}
private static string GetSpecialTileText_Local(string key)
{
    var lm = LocalizationManager.Instance;
    if (lm == null) return key;
    return lm.GetText(key);
}

private static string LocalizeSpecialTileYakuName_Local(string rawYakuName)
{
    string jp = NormalizeTraitJudgeYakuName_Local(rawYakuName);
    if (string.IsNullOrEmpty(jp))
        return rawYakuName ?? "";

    var lm = LocalizationManager.Instance;
    if (lm == null)
        return jp;

    switch (jp)
    {
        case "国士無双": return lm.GetYakumanDisplayName("KOKUSHI");
        case "七対子": return lm.GetYakuDisplayName("CHIITOITSU");
        case "門前清自摸和": return lm.GetYakuDisplayName("MENZEN_TSUMO");
        case "タンヤオ": return lm.GetYakuDisplayName("TANYAO");
        case "平和": return lm.GetYakuDisplayName("PINFU");
        case "役牌": return lm.GetYakuDisplayName("YAKUHAI");
        case "一盃口": return lm.GetYakuDisplayName("IIPEIKOU");
        case "二盃口": return lm.GetYakuDisplayName("RYANPEIKOU");
        case "三色同順": return lm.GetYakuDisplayName("SANSHOKU_DOUJUN");
        case "一気通貫": return lm.GetYakuDisplayName("ITTSU");
        case "チャンタ": return lm.GetYakuDisplayName("CHANTA");
        case "純チャン": return lm.GetYakuDisplayName("JUNCHAN");
        case "対々和": return lm.GetYakuDisplayName("TOITOI");
        case "三暗刻": return lm.GetYakuDisplayName("SANANKOU");
        case "三カンツ": return lm.GetYakuDisplayName("SANKANTSU");
        case "三色同刻": return lm.GetYakuDisplayName("SANSHOKU_DOUKOU");
        case "小三元": return lm.GetYakuDisplayName("SHOUSANGEN");
        case "混老頭": return lm.GetYakuDisplayName("HONROUTOU");
        case "混一色": return lm.GetYakuDisplayName("HONITSU");
        case "清一色": return lm.GetYakuDisplayName("CHINITSU");
        case "九蓮宝燈": return lm.GetYakumanDisplayName("CHUUREN_POUTOU");
        case "大三元": return lm.GetYakumanDisplayName("DAISANGEN");
        case "大四喜": return lm.GetYakumanDisplayName("DAISUUSHI");
        case "小四喜": return lm.GetYakumanDisplayName("SHOUSUUSHI");
        case "字一色": return lm.GetYakumanDisplayName("TSUUIISOU");
        case "清老頭": return lm.GetYakumanDisplayName("CHINROUTOU");
        case "緑一色": return lm.GetYakumanDisplayName("RYUUIISOU");
        case "四暗刻": return lm.GetYakumanDisplayName("SUUANKOU");
        case "四カンツ": return lm.GetYakumanDisplayName("SUUKANTSU");
        default: return jp;
    }
}

private static string BuildSpecialTileTraitUpgradeFallback_Local(int traitUp)
{
    var lm = LocalizationManager.Instance;
    switch (lm != null ? lm.CurrentLanguage : LocalizationManager.Language.Japanese)
    {
        case LocalizationManager.Language.English:
            return $"Yaku Upgrade +{traitUp}";
        case LocalizationManager.Language.ChineseSimplified:
            return $"役种强化 +{traitUp}";
        case LocalizationManager.Language.Japanese:
        default:
            return $"役強化＋{traitUp}";
    }
}

private static string BuildSpecialTileTraitUpgradeLine_Local(string rawYakuName, int traitUp)
{
    string yaku = LocalizeSpecialTileYakuName_Local(rawYakuName);
    if (string.IsNullOrEmpty(yaku))
        return BuildSpecialTileTraitUpgradeFallback_Local(traitUp);

    return $"{yaku} +{traitUp}";
}
private void CommitEnemyDiscardsAsThisTurn(System.Collections.Generic.List<string> discardsThisTurn)
{
    lastEnemyTurnTiles.Clear();

    // ★追加：このターンの捨て牌が enemyDiscards のどこから積まれるか（左端＝先頭＝baseIndex）
    int baseIndex = enemyDiscards.Count;

    for (int i = 0; i < discardsThisTurn.Count; i++)
    {
        var t = discardsThisTurn[i];
        lastEnemyTurnTiles.Add(t);
        enemyDiscards.Add(t);
    }

    // ★追加：敵のリーチ宣言ターンに捨てた4枚のうち「一番左」を記録
    //   - 宣言ターンは _enemyRiichiDeclaredTurnCounter と _enemyTurnCounter が一致する
    //   - すでに記録済みなら上書きしない
    if (_enemyIsRiichi &&
        _enemyRiichiDiscardHighlightIndex < 0 &&
        _enemyRiichiDeclaredTurnCounter >= 0 &&
        _enemyRiichiDeclaredTurnCounter == _enemyTurnCounter)
    {
        _enemyRiichiDiscardHighlightIndex = baseIndex;
    }

    _enemyTurnHistory.Enqueue(new System.Collections.Generic.List<string>(lastEnemyTurnTiles));
    while (_enemyTurnHistory.Count > Mathf.Max(1, enemyAttackIntervalTurns)) _enemyTurnHistory.Dequeue();

    RefreshEnemyDiscardUI();
    WireEnemyTurnClickTargets(); // ★追加：敵捨て牌クリックと赤ハイライトを確実に有効化
    UpdateButtons();
}
void SetTileRiichiDiscardHighlight(Transform tile, bool on)
{
    var img = GetVisibleArtImage(tile);
    if (!img) return;

    // ★赤ハイライト(SetTileHighlight)や他のOutlineと共存するため、色一致のOutlineだけを再利用する
    var outs = img.GetComponents<Outline>();
    Outline target = null;

    for (int i = 0; i < outs.Length; i++)
    {
        var c = outs[i].effectColor;
        // riichiDiscardHighlightColor に近いものを再利用（完全一致でなくても良い）
        if (Mathf.Abs(c.r - riichiDiscardHighlightColor.r) < 0.05f &&
            Mathf.Abs(c.g - riichiDiscardHighlightColor.g) < 0.05f &&
            Mathf.Abs(c.b - riichiDiscardHighlightColor.b) < 0.05f)
        {
            target = outs[i];
            break;
        }
    }

    if (!target) target = img.gameObject.AddComponent<Outline>();

    target.effectColor = riichiDiscardHighlightColor;
    target.effectDistance = new Vector2(4f, -4f);
    target.useGraphicAlpha = true;
    target.enabled = on;
}

private void AfterEnemyDiscardCommonFlow()
{
    // 敵効果の適用（必要回数に達していれば）
    ProcessEnemyAttackEffects();

    // 敵効果でプレイヤーHPが0以下になったら即敗北
    if (Mathf.Max(0, playerHP) <= 0)
    {
        StartDefeatTransitionIfNeeded();
        return;
    }

// プレイヤーが敵捨て牌で鳴ける/ロンできるなら停止（既存仕様維持）
bool canRon_After = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);
bool canPon_After = lastEnemyTurnTiles.Any(t => CanPonWithBase(NormalizeEnemyTileId(t)));
bool canChi_After = lastEnemyTurnTiles.Any(t => CanChiWithBase(NormalizeEnemyTileId(t)));
bool canKan_After = lastEnemyTurnTiles.Any(t => CanKanWithBase(NormalizeEnemyTileId(t)));

if (isRiichi && !canRon_After)
{
    canPon_After = false;
    canChi_After = false;
    canKan_After = false;
}
if (canRon_After || canPon_After || canChi_After || canKan_After)
{
    _autoSkipPending = false;       // ★追加：過去に予約したAutoSkipを確実に殺す

    // ★追加：ここからは「プレイヤーのリアクション待ち」なので、次の EnemyTurn 実行を許可する
    _enemyTurnRunning = false;

    RefreshEnemySelectionLift();
    WireEnemyTurnClickTargets();
    UpdateButtons();
    return;
}


AutoSkipEnemyIfNothing(0.5f);
}
private int EnemyAI_ComputeClosedHandScore(System.Collections.Generic.List<string> concealed13, string winTile, bool isTsumo)
{
    try
    {
        // enemy は鳴き無し前提（今回仕様）
        // CountDoraHits に渡す都合で「空の副露リスト」を用意（nullでも動くが統一）
        var openMelds = new System.Collections.Generic.List<System.Collections.Generic.IList<string>>();

        // ★修正：敵の自風/場風を固定しない
        string seatWind  = __EnemyAddon_GetEnemySeatWind_FromPlayerSeat(); // GameManager_EnemyMeldMode_Addon_PlayerScorer.cs に既に存在
        string roundWind = GetRoundWind();

        // raw13（ドラ/特別牌ドラ判定用：_sp を保持したまま）
        var concealedRaw13 = new System.Collections.Generic.List<string>(13);

        if (concealed13 != null)
        {
            for (int i = 0; i < concealed13.Count; i++)
            {
                var tRaw = concealed13[i];
                if (!string.IsNullOrEmpty(tRaw)) concealedRaw13.Add(tRaw);
            }
        }
        if (concealedRaw13.Count > 13) concealedRaw13 = concealedRaw13.GetRange(0, 13);

        // logic13（役判定用：_sp と * を除去）
        var concealedLogic13 = new System.Collections.Generic.List<string>(13);
        for (int i = 0; i < concealedRaw13.Count; i++)
        {
            var t = StripTileIdForLogic(concealedRaw13[i]);
            if (!string.IsNullOrEmpty(t)) concealedLogic13.Add(t);
        }
        if (concealedLogic13.Count > 13) concealedLogic13 = concealedLogic13.GetRange(0, 13);

        string winLogic = StripTileIdForLogic(winTile);
        if (string.IsNullOrEmpty(winLogic)) return 0;

        // ★重要：まず「和了形かどうか」を14枚で判定（役の有無とは別）
        // 七対子も含めて判定する
        var snapshot14Logic = new System.Collections.Generic.List<string>(14);
        snapshot14Logic.AddRange(concealedLogic13);
        snapshot14Logic.Add(winLogic);

        if (!IsAnyWinningShape(snapshot14Logic))
            return 0;

        // 役判定（役が無い場合でも、リーチ中なら立直で成立させる）
        var (hanEval, fuEval, _) = YakuEvaluator.Evaluate(concealedLogic13, openMelds, winLogic, isTsumo, isClosed: true, seatWind: seatWind, roundWind: roundWind);

        int hanTotal = hanEval;

        // Evaluate が 0翻でも、「敵がリーチ中」なら立直1翻で成立させる
        bool forcedRiichiOnly = false;
        if (hanTotal <= 0)
        {
            if (_enemyIsRiichi)
            {
                hanTotal = 1;
                forcedRiichiOnly = true;
                if (fuEval <= 0) fuEval = 30; // 念のため最低符
            }
            else
            {
                return 0;
            }
        }
        else
        {
            // 既に役あり + リーチ中なら立直+1（役満(>=13)には足さない）
            if (_enemyIsRiichi && hanTotal < 13)
            {
                hanTotal += 1;
            }
        }

        // ★ドラ加算（役満(>=13)には加算しない）
        if (hanTotal < 13)
        {
            // raw14（ドラ計算用：和了牌も含める）
            var tiles14Raw = new System.Collections.Generic.List<string>(14);
            tiles14Raw.AddRange(concealedRaw13);
            tiles14Raw.Add(winTile);

            // 表ドラ
            int normalDora = CountDoraHits(tiles14Raw, openMelds, doraIndicators);
            if (normalDora > 0) hanTotal += normalDora;

            // 特別牌ドラ（*_sp を1枚=ドラ+1 として扱う既存仕様に合わせる）
            try
            {
int spBonus = CountSpecialTileDoraBonusForScoring(tiles14Raw, openMelds);
if (spBonus > 0) hanTotal += spBonus;

            }
            catch { }

            // 裏ドラ（敵がリーチしている場合のみ）
            // AI の探索で RevealUraDoraIfEligible() を呼ぶと副作用があるので、
            // 局開始時に確保している _uraIndicatorPool から必要枚数だけ “一時的に” 使って数える。
            if (_enemyIsRiichi)
            {
                int need = (doraIndicators != null) ? doraIndicators.Count : 0;
                if (need > 0 && _uraIndicatorPool != null && _uraIndicatorPool.Count > 0)
                {
                    var tmpUra = new System.Collections.Generic.List<string>(need);
                    for (int i = 0; i < need && i < _uraIndicatorPool.Count; i++)
                        tmpUra.Add(_uraIndicatorPool[i]);

                    int uraCount = CountDoraHits(tiles14Raw, openMelds, tmpUra);
                    if (uraCount > 0) hanTotal += uraCount;
                }
            }
        }

        // ★追加：レジェンダリー効果 L6（和了時 +16符）を fuEval に反映（推定値と確定表示のズレ防止）
        try
        {
            var tiles14Raw_ForFx = new System.Collections.Generic.List<string>(14);
            tiles14Raw_ForFx.AddRange(concealedRaw13);
            tiles14Raw_ForFx.Add(winTile);

            int cnt = CountLegendaryEffectTilesInScoringPool(tiles14Raw_ForFx, openMelds, 6);
            int addFu = (cnt > 0) ? 16 * cnt : 0;
            if (addFu > 0)
                fuEval = ApplySpecialFuBonusAndRoundUp(fuEval, addFu);
        }
        catch { }

        bool enemyIsDealer = true;   // ★敵は常に親扱い

        var sr = Scoring.TryScoreWin(fuEval, hanTotal, isTsumo, enemyIsDealer);
        return sr.totalPoints;
    }
    catch
    {
        return 0;
    }
}
private void EnemyAI_SetEnemyAddonLastScoreForWin(string winTile, bool isTsumo, int finalScore)
{
    try
    {
        var openMelds = new System.Collections.Generic.List<System.Collections.Generic.IList<string>>();

        string seatWind  = __EnemyAddon_GetEnemySeatWind_FromPlayerSeat();
        string roundWind = GetRoundWind();

        // raw13/raw14（ドラ/特別牌ドラ用）
        var raw13 = new System.Collections.Generic.List<string>(13);
        if (_enemyHand != null)
        {
            for (int i = 0; i < _enemyHand.Count; i++)
            {
                var t = _enemyHand[i];
                if (!string.IsNullOrEmpty(t)) raw13.Add(t);
            }
        }
        if (raw13.Count > 13) raw13 = raw13.GetRange(0, 13);

        var raw14 = new System.Collections.Generic.List<string>(14);
        raw14.AddRange(raw13);
        raw14.Add(winTile);

        // logic14（表示用 breakdown 取得。あなたの現行コードがこの overload を使っているので踏襲）
        var logic14 = new System.Collections.Generic.List<string>(14);
        for (int i = 0; i < raw13.Count; i++)
        {
            var t = StripTileIdForLogic(raw13[i]);
            if (!string.IsNullOrEmpty(t)) logic14.Add(t);
        }
        if (logic14.Count > 13) logic14 = logic14.GetRange(0, 13);

        string winLogic = StripTileIdForLogic(winTile);
        if (!string.IsNullOrEmpty(winLogic))
            logic14.Add(winLogic);

        var (hanEval, fuEval, breakdown) =
            YakuEvaluator.Evaluate(logic14, openMelds, winLogic, isTsumo: isTsumo, isClosed: true, seatWind: seatWind, roundWind: roundWind);

        int hanTotal = hanEval;

        // breakdown から役部分だけ抽出 → " + " 区切りで配列化（いまの表示フォーマットを維持）
        string yakuPart = breakdown ?? "";
        int bar = yakuPart.IndexOf('|');
        if (bar >= 0) yakuPart = yakuPart.Substring(0, bar).Trim();

        var yakuParts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(yakuPart))
        {
            foreach (var p in yakuPart.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries))
            {
                var s = p.Trim();
                if (!string.IsNullOrEmpty(s)) yakuParts.Add(s);
            }
        }
        // ★敵の役表示では「役なし」は不要（表示しない）
        yakuParts.RemoveAll(x => x != null && x.Contains("役なし"));
// ★追加：天和（あなたの仕様）
// 天和：敵のみ。敵が最初の敵ターンにツモ和了したときに成立（暗槓/スキル等の阻害条件は付けない）
// ※役満扱い（+13）
{
    bool isFirstTurn = (_enemyWinDeclaredTurnCounter == 1);

    bool tenhou = isTsumo && isFirstTurn; // 敵のみ（このメソッド自体が敵和了の役表示組み立て）

    if (tenhou)
    {
        bool IsYakumanName_Enemy(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            if (s.Contains("九蓮")) return true;
            if (s.Contains("国士")) return true;
            if (s.Contains("四暗刻")) return true;
            if (s.Contains("大三元")) return true;
            if (s.Contains("字一色")) return true;
            if (s.Contains("緑一色")) return true;
            if (s.Contains("清老頭")) return true;
            if (s.Contains("小四喜")) return true;
            if (s.Contains("大四喜")) return true;
            if (s.Contains("四カンツ")) return true;
            if (s.Contains("四槓子")) return true;
            if (s.Contains("天和")) return true;
            if (s.Contains("地和")) return true;
            if (s.Contains("人和")) return true;

            return false;
        }

        // 念のため重複防止
        yakuParts.RemoveAll(x => x != null && (x.Contains("天和") || x.Contains("地和") || x.Contains("人和")));

        // 非・数え役満仕様：
        // 天和が付く場合は通常役は残さず、既存の役満だけ残す
        var yakumanOnly = yakuParts.Where(IsYakumanName_Enemy).ToList();
        yakumanOnly.Insert(0, "天和(+13)");
        yakuParts = yakumanOnly;

        int yakumanCount = yakuParts.Count(x => IsYakumanName_Enemy(x));
        if (yakumanCount <= 0) yakumanCount = 1;

        hanTotal = 13 * yakumanCount;
        fuEval = 0;
    }
}
        // ダブル立直／一発／立直（役満には足さない）
        // ※敵の和了種別は「直近の和了フラグ」を優先（演出・スナップショットとズレないように）
        bool enemyWonWithRiichi = _enemyLastWinWasRiichi || _enemyIsRiichi;
        if (enemyWonWithRiichi && hanTotal < 13)
        {
            bool isDoubleRiichi = (_enemyRiichiDeclaredTurnCounter == 1);

            string riichiText = GetGameFixedText_Local("yaku.riichi");
            string doubleRiichiText = GetGameFixedText_Local("yaku.double_riichi");
            string ippatsuText = GetGameFixedText_Local("yaku.ippatsu");

            // 既存の立直表記があれば消して、ここで統一して入れ直す（重複・表記ゆれ防止）
            yakuParts.RemoveAll(x =>
                x != null &&
                (
                    x.Contains("立直") ||
                    x.Contains("リーチ") ||
                    x.Contains("ダブル立直") ||
                    x.Contains("ダブルリーチ") ||
                    x.Contains("Wリーチ") ||
                    x.Contains(riichiText) ||
                    x.Contains(doubleRiichiText) ||
                    x.Contains(ippatsuText)
                ));

            int riichiHan = isDoubleRiichi ? 2 : 1;
            string riichiLabel = isDoubleRiichi
                ? doubleRiichiText
                : riichiText;

            yakuParts.Insert(0, riichiLabel);
            hanTotal += riichiHan;

            bool isIppatsu =
                _enemyRiichiDeclaredTurnCounter >= 0 &&
                (
                    (!isTsumo && _enemyWinDeclaredTurnCounter == _enemyRiichiDeclaredTurnCounter) ||
                    (isTsumo && _enemyWinDeclaredTurnCounter == _enemyRiichiDeclaredTurnCounter + 1)
                );

            if (isIppatsu)
            {
                bool hasIppatsuName = yakuParts.Exists(x => x != null && x.Contains(ippatsuText));
                if (!hasIppatsuName)
                {
                    int insertIndex = (yakuParts.Count >= 1) ? 1 : 0;
                    yakuParts.Insert(insertIndex, ippatsuText);
                }
                hanTotal += 1;
            }
        }
        // ドラ（役満には足さない）
        if (hanTotal < 13)
        {
            int normalDora = CountDoraHits(raw14, openMelds, doraIndicators);
if (normalDora > 0)
{
    hanTotal += normalDora;
    yakuParts.Add(BuildLocalizedCountYakuText("yaku.dora_count", normalDora));
}

            try
            {
                int spBonus = CountSpecialTileDoraBonusForScoring(raw14, openMelds);
if (spBonus > 0)
{
    hanTotal += spBonus;
    yakuParts.Add(BuildLocalizedCountYakuText("yaku.red_dora_count", spBonus));
}
            }
            catch { }

            // 裏ドラ（確定表示用＝ここでは RevealUraDoraIfEligible を呼んでOK）
            if (enemyWonWithRiichi)
            {
                try
                {
                    _includeUraForScoring = true;
                    RevealUraDoraIfEligible();
                }
                catch { }
int uraCount = CountDoraHits(raw14, openMelds, uraIndicators);
if (uraCount > 0)
{
    hanTotal += uraCount;
    yakuParts.Add(BuildLocalizedCountYakuText("yaku.ura_dora_count", uraCount));
}
            }
        }

        // ★追加：レジェンダリー効果 L6（和了時 +16符）を fuEval に反映（表示と実点の一致）
        try
        {
            int cnt = CountLegendaryEffectTilesInScoringPool(raw14, openMelds, 6);
            int addFu = (cnt > 0) ? 16 * cnt : 0;
            if (addFu > 0)
                fuEval = ApplySpecialFuBonusAndRoundUp(fuEval, addFu);
        }
        catch { }

        // ★点数は引数finalScoreを信用しない。
        //  いま確定した「符・翻・親子・ロン/ツモ」から再計算して、表示と実点を一致させる。
        bool enemyIsDealer = true;   // ★敵は常に親扱い
        var sr = Scoring.TryScoreWin(fuEval, hanTotal, isTsumo: isTsumo, isDealer: enemyIsDealer);

        EnemyAddon_LastPoints   = sr.totalPoints;
        EnemyAddon_LastHan      = hanTotal;
        EnemyAddon_LastFu       = sr.fu;

        // ★区切りは「・」ではなくスペースだけ
        EnemyAddon_LastYakuText = (yakuParts.Count == 0) ? "" : string.Join(" ", yakuParts);

        // DoraText は現状 UI が参照していないなら空でOK（残留表示が嫌なら空固定にしておく）
        EnemyAddon_LastDoraText = "";
    }
    catch
    {
        // 表示用セットが失敗しても、和了フロー自体は止めない（デグレ防止）
    }
}
private void EnemyAI_DeclareEnemyTsumoWin(string winTile, int score)
{
    // ★和了が発生した瞬間に、どの状況でも自動進行を止める（リーチ中／既に勝利済みでも同じ）
    _autoSkipPending = false;
    _autoConfirmOfferPending = false;

    // ★保険：自動コルーチンが phase を見て先に進まないよう、即座にスコア状態へ固定
    phase = Phase.Scoring;
    _freezeProgression = true;

    // 既存の敵和了フローに合わせるため、EnemyAddon 側が参照しているフラグも更新
    _enemyLastWinTileId = winTile;
    _enemyLastWinWasRiichi = _enemyIsRiichi;   // ★修正：常に true は誤り
    _enemyLastWinWasTsumo = true;

    // ★重要：
    // 一発判定・立直判定・天和判定などの参照元になるので、
    // スコア計算や役表示組み立てより前に、この和了ターンを確定しておく。
    _enemyWinDeclaredTurnCounter = _enemyTurnCounter;

    // ★まず“基礎スコア”を計算（翻/符/ドラ/親子込み）
    try
    {
        int fixedScore = EnemyAI_ComputeClosedHandScore(_enemyHand, winTile, isTsumo: true);
        if (fixedScore > 0) score = fixedScore;
    }
    catch { }

    // =========================================================
    // ★敵スコアパネルに表示する「役/翻/符/点」を確実にセット
    // （ここで EnemyAddon_LastPoints が最終確定点になるようにしている）
    // =========================================================
    EnemyAI_SetEnemyAddonLastScoreForWin(winTile, isTsumo: true, finalScore: score);

    // ★表示点（score）とダメージ元（hpDmg）を、確定した点数に統一
    score = EnemyAddon_LastPoints;
    int hpDmg = Mathf.Max(1, score);

try
{
    bool prevAttackerIsPlayer = _currentScoringAttackerIsPlayer;
    _currentScoringAttackerIsPlayer = false;

    int dummyMp = 0;
    int dummyHp = 0;
    EnemySkills_ModifyDamageBeforeApply(ref hpDmg, ref dummyMp, ref dummyHp);

    _currentScoringAttackerIsPlayer = prevAttackerIsPlayer;
}
catch { }

// ★追加：敵和了のスコア表示は、必ず「敵が攻撃側」として確定させる（段階表示/敵専用UI切替のため）
_currentScoringAttackerIsPlayer = false;

    int prevPl = playerHP;

    int prevEn = enemyHP;

    FinalizeEnemyWin_ShowScoringAndCleanup(score, hpDmg, prevPl, prevEn);
}
private void EnemyAI_DeclareEnemyRonWin(string winTile, int score)
{
    // ★和了が発生した瞬間に、どの状況でも自動進行を止める（リーチ中／既に勝利済みでも同じ）
    _autoSkipPending = false;
    _autoConfirmOfferPending = false;

    // ★保険：自動コルーチンが phase を見て先に進まないよう、即座にスコア状態へ固定
    phase = Phase.Scoring;
    _freezeProgression = true;

    _enemyLastWinTileId = winTile;
    _enemyLastWinWasRiichi = _enemyIsRiichi;   // ★修正：常に true は誤り
    _enemyLastWinWasTsumo = false;

    // ★重要：
    // 一発判定・立直判定の参照前に、この和了ターンを確定しておく。
    // 敵ロンは「リーチ直後のプレイヤー捨て牌」で発生し得るので、ここが後ろだと古い値を読む。
    _enemyWinDeclaredTurnCounter = _enemyTurnCounter;

    // ★まず“基礎スコア”を計算（翻/符/ドラ/親子込み）
    try
    {
        int fixedScore = EnemyAI_ComputeClosedHandScore(_enemyHand, winTile, isTsumo: false);
        if (fixedScore > 0) score = fixedScore;
    }
    catch { }

    // =========================================================
    // ★敵スコアパネルに表示する「役/翻/符/点」を確実にセット
    // =========================================================
    EnemyAI_SetEnemyAddonLastScoreForWin(winTile, isTsumo: false, finalScore: score);

    // ★表示点（score）とダメージ元（hpDmg）を、確定した点数に統一
    score = EnemyAddon_LastPoints;
    int hpDmg = Mathf.Max(1, score);

try
{
    bool prevAttackerIsPlayer = _currentScoringAttackerIsPlayer;
    _currentScoringAttackerIsPlayer = false;

    int dummyMp = 0;
    int dummyHp = 0;
    EnemySkills_ModifyDamageBeforeApply(ref hpDmg, ref dummyMp, ref dummyHp);

    _currentScoringAttackerIsPlayer = prevAttackerIsPlayer;
}
catch { }

// ★追加：敵和了のスコア表示は、必ず「敵が攻撃側」として確定させる（段階表示/敵専用UI切替のため）
_currentScoringAttackerIsPlayer = false;

    int prevPl = playerHP;

    int prevEn = enemyHP;

    FinalizeEnemyWin_ShowScoringAndCleanup(score, hpDmg, prevPl, prevEn);
}
// 13枚から待ち牌を抽出（“和了できる牌”だけ入れる）
private void FillRiichiWaitsFrom13(System.Collections.Generic.List<string> hand13, System.Collections.Generic.HashSet<string> outWaits)
{
    outWaits.Clear();
    foreach (var t in EnumerateAllTileIds34())
    {
        int sc = EnemyAI_ComputeClosedHandScore(hand13, t, isTsumo:true);
        if (sc > 0) outWaits.Add(t);
    }
}
private void ChooseBest13From17(
    System.Collections.Generic.List<string> tiles17,
    out System.Collections.Generic.List<string> bestKeep13,
    out System.Collections.Generic.List<string> bestDrop4,
    out int bestShanten13,
    out int bestUkeire,
    out int bestExpectedScore)
{
    bestKeep13 = new System.Collections.Generic.List<string>(13);
    bestDrop4  = new System.Collections.Generic.List<string>(4);

    bestShanten13 = int.MaxValue;
    bestUkeire = -1;
    bestExpectedScore = -1;

    int bestShapeScore = int.MinValue;
    int bestAnkoKeepScore = int.MinValue;

    int n = tiles17.Count;

    // ---- 210通り化：捨て候補を最大10インデックスに絞る（10C4=210）----
    System.Collections.Generic.List<int> BuildDiscardCandidateIndices(System.Collections.Generic.List<string> tiles, int maxCount)
    {
        // baseId -> count（正規化して偏り検出）
        var cnt = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < tiles.Count; i++)
        {
            var id = NormalizeEnemyTileId(tiles[i]);
            cnt.TryGetValue(id, out int c);
            cnt[id] = c + 1;
        }

        // 4枚同一（カン候補）は敵はカンしないので優先して候補に入れる
        var fixedIdx = new System.Collections.Generic.HashSet<int>();
        foreach (var kv in cnt)
        {
            if (kv.Value >= 4)
            {
                for (int i = 0; i < tiles.Count; i++)
                    if (NormalizeEnemyTileId(tiles[i]) == kv.Key) fixedIdx.Add(i);
            }
        }

        int BadnessAt(int idx)
        {
            string id = NormalizeEnemyTileId(tiles[idx]);
            int c = cnt[id];

            int bad = 0;

            // 重複は基本残したい（= bad 下げ）
            if (c >= 2) bad -= 40;
            if (c == 3) bad -= 15;   // 暗刻はより残したい
            if (c >= 4) bad += 500;  // 4枚は絶対避ける（敵はカンしない）

            // 字牌単騎はより切りやすく（孤立牌の字牌が残りがちな対策）
            if (TryParseSuitNum(id, out int suit, out int num))
            {
                if (suit == 3)
                {
                    if (c == 1) bad += 75;     // 35→75（字牌単騎は強く捨て候補へ）
                    else if (c == 2) bad -= 12;
                    else if (c == 3) bad -= 8;
                    return bad;
                }

                // 数牌：近傍が無い孤立牌は捨てやすいが、字牌単騎よりは残す（③の要望）
                string prefix = (suit == 0) ? "Man" : (suit == 1) ? "Pin" : "Sou";

                bool hasN1 = cnt.ContainsKey(prefix + (num - 1));
                bool hasP1 = cnt.ContainsKey(prefix + (num + 1));
                bool hasN2 = cnt.ContainsKey(prefix + (num - 2));
                bool hasP2 = cnt.ContainsKey(prefix + (num + 2));

                // 両面・嵌張のタネがあるほど残したい（= bad 下げ）
                if (hasN1) bad -= 20; // 18→20
                if (hasP1) bad -= 20; // 18→20
                if (hasN2) bad -= 10; // 8→10
                if (hasP2) bad -= 10; // 8→10

                // 完全孤立でも「字牌単騎よりは弱く」候補に上がる程度にする
                if (c == 1 && !(hasN1 || hasP1 || hasN2 || hasP2))
                    bad += 16; // 22→16（字牌単騎を先に切りやすく）

                if ((num == 1 || num == 9) && c == 1 && !(hasN1 || hasP1 || hasN2 || hasP2))
                    bad += 14; // 18→14
            }

            return bad;
        }

        var scored = new System.Collections.Generic.List<System.Tuple<int, int>>();
        for (int i = 0; i < tiles.Count; i++)
        {
            if (fixedIdx.Contains(i)) continue;
            scored.Add(System.Tuple.Create(i, BadnessAt(i)));
        }

        scored.Sort((a, b) =>
        {
            int cmp = b.Item2.CompareTo(a.Item2);
            if (cmp != 0) return cmp;
            return a.Item1.CompareTo(b.Item1);
        });

        var result = new System.Collections.Generic.List<int>(maxCount);

        foreach (var fi in fixedIdx)
        {
            result.Add(fi);
            if (result.Count >= maxCount) return result;
        }

        for (int i = 0; i < scored.Count && result.Count < maxCount; i++)
            result.Add(scored[i].Item1);

        if (result.Count < 4)
        {
            for (int i = 0; i < tiles.Count && result.Count < 4; i++)
                if (!result.Contains(i)) result.Add(i);
        }

        return result;
    }

    int ComputeAnkoKeepScore13(System.Collections.Generic.List<string> hand13)
    {
        // 暗刻を強く評価。字牌単騎は減点（③）
        var cnt = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < hand13.Count; i++)
        {
            var id = NormalizeEnemyTileId(hand13[i]);
            cnt.TryGetValue(id, out int c);
            cnt[id] = c + 1;
        }

        int score = 0;
        foreach (var kv in cnt)
        {
            int c = kv.Value;
            if (c == 3) score += 200;   // 暗刻は大きく加点（①）
            else if (c == 2) score += 35;
            else if (c >= 4) score -= 9999; // 念のため（4枚は絶対NG）

            // 字牌単騎は減点（孤立牌の字牌を残しがちな問題）
            if (c == 1 && TryParseSuitNum(kv.Key, out int suit, out int num) && suit == 3)
                score -= 25;
        }
        return score;
    }

    var cand = BuildDiscardCandidateIndices(tiles17, 10);
    int m = cand.Count;

    for (int ai = 0; ai < m - 3; ai++)
    for (int bi = ai + 1; bi < m - 2; bi++)
    for (int ci = bi + 1; ci < m - 1; ci++)
    for (int di = ci + 1; di < m; di++)
    {
        int i = cand[ai], j = cand[bi], k = cand[ci], l = cand[di];

        var keep = new System.Collections.Generic.List<string>(13);
        var drop = new System.Collections.Generic.List<string>(4);

        for (int idx = 0; idx < n; idx++)
        {
            if (idx == i || idx == j || idx == k || idx == l) drop.Add(tiles17[idx]);
            else keep.Add(tiles17[idx]);
        }

        if (HasQuadOrMore13(keep))
            continue;

        int sh = ComputeShantenFor13_ByMinAdd1(keep, out int ukeire, out int expScore);
        int shapeScore = ComputeTaatsuShapeScore13(keep);
        int ankoKeepScore = ComputeAnkoKeepScore13(keep);

        bool better = false;

        if (sh < bestShanten13)
        {
            better = true;
        }
        else if (sh == bestShanten13)
        {
            // ★テンパイ（sh==0）は「点数→暗刻保持→受け入れ→形」の順で最適化（②＋①）
            if (sh == 0)
            {
                if (expScore > bestExpectedScore) better = true;
                else if (expScore == bestExpectedScore && ankoKeepScore > bestAnkoKeepScore) better = true;
                else if (expScore == bestExpectedScore && ankoKeepScore == bestAnkoKeepScore && ukeire > bestUkeire) better = true;
                else if (expScore == bestExpectedScore && ankoKeepScore == bestAnkoKeepScore && ukeire == bestUkeire && shapeScore > bestShapeScore) better = true;
            }
            else
            {
                // ★非テンパイは「暗刻保持→受け入れ→形」の順（①を優先）
                if (ankoKeepScore > bestAnkoKeepScore) better = true;
                else if (ankoKeepScore == bestAnkoKeepScore && ukeire > bestUkeire) better = true;
                else if (ankoKeepScore == bestAnkoKeepScore && ukeire == bestUkeire && shapeScore > bestShapeScore) better = true;
            }
        }

        if (better)
        {
            bestShanten13 = sh;
            bestUkeire = ukeire;
            bestExpectedScore = expScore;
            bestShapeScore = shapeScore;
            bestAnkoKeepScore = ankoKeepScore;

            bestKeep13.Clear(); bestKeep13.AddRange(keep);
            bestDrop4.Clear();  bestDrop4.AddRange(drop);
        }
    }
}

private bool IsMenuPanelVisibleState()
{
    if (!menuPanel) return false;

    if (!menuPanel.activeInHierarchy)
        return false;

    try
    {
        var cg = menuPanel.GetComponent<CanvasGroup>();
        if (cg != null)
            return cg.alpha > 0.001f && cg.interactable && cg.blocksRaycasts;
    }
    catch { }

    return true;
}

private void EnsureMenuInnerButtonsAlwaysActive()
{
    void EnsureButtonAlive(Button b)
    {
        if (!b) return;

        if (!b.gameObject.activeSelf)
            b.gameObject.SetActive(true);

        if (!b.enabled)
            b.enabled = true;
    }

    EnsureButtonAlive(btnMenuOption);
    EnsureButtonAlive(btnMenuSuspend);
    EnsureButtonAlive(btnMenuExit);
    EnsureButtonAlive(btnMenuClose);
}

private void SetMenuPanelVisibleState(bool visible)
{
    if (!menuPanel) return;

    if (!menuPanel.activeSelf)
        menuPanel.SetActive(true);

    try
    {
        var cg = menuPanel.GetComponent<CanvasGroup>();
        if (cg == null) cg = menuPanel.AddComponent<CanvasGroup>();

        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }
    catch { }

    EnsureMenuInnerButtonsAlwaysActive();
}
// 13枚のシャンテンを「1枚足して14枚のシャンテン最小 + 1」で定義し、受け入れ枚数も同時に出す（高速版）
private int ComputeShantenFor13_ByMinAdd1(System.Collections.Generic.List<string> hand13, out int ukeire, out int expectedScore)
{
    ukeire = 0;
    expectedScore = 0;

    // 34カウントへ（List生成をやめる）
    int[] c = new int[34];
    for (int i = 0; i < hand13.Count; i++)
    {
        if (TryToIndex34(hand13[i], out int ix)) c[ix]++;
    }

    int minSh14 = int.MaxValue;
    var effTiles = new System.Collections.Generic.List<string>();

    foreach (var t in EnumerateAllTileIds34())
    {
        if (!TryToIndex34(t, out int addIx)) continue;

        c[addIx]++;
        int sh14 = Shanten14_CalcByCounts(c);
        c[addIx]--;

        if (sh14 < minSh14)
        {
            minSh14 = sh14;
            effTiles.Clear();
            effTiles.Add(t);
        }
        else if (sh14 == minSh14)
        {
            effTiles.Add(t);
        }
    }

    ukeire = effTiles.Count;

    int sh13 = minSh14 + 1;
    if (sh13 == 0)
    {
        int best = 0;
        for (int i = 0; i < effTiles.Count; i++)
        {
            int sc = EnemyAI_ComputeClosedHandScore(hand13, effTiles[i], isTsumo:true);
            if (sc > best) best = sc;
        }
        expectedScore = best;
    }

    return sh13;
}

// 14枚のシャンテン（-1:アガリ、0:テンパイ…）高速版：34カウントで計算
private int Shanten14_Calc(System.Collections.Generic.List<string> tiles14)
{
    int[] c = new int[34];
    for (int i = 0; i < tiles14.Count; i++)
    {
        if (TryToIndex34(tiles14[i], out int ix)) c[ix]++;
    }
    return Shanten14_CalcByCounts(c);
}
// 34カウントからシャンテンを返す（-1:アガリ）
private int Shanten14_CalcByCounts(int[] c)
{
    int shNormal  = Shanten_Normal(c);
    int shChiitoi = Shanten_Chiitoi(c);
    int shKokushi = Shanten_Kokushi(c);

    int sh = shNormal;
    if (shChiitoi < sh) sh = shChiitoi;
    if (shKokushi < sh) sh = shKokushi;
    return sh;
}

// 国士無双
private int Shanten_Kokushi(int[] c)
{
    int[] orphans = new int[] { 0,8, 9,17, 18,26, 27,28,29,30,31,32,33 };

    int uniq = 0;
    bool hasPair = false;

    for (int i = 0; i < orphans.Length; i++)
    {
        int ix = orphans[i];
        if (c[ix] > 0) uniq++;
        if (c[ix] >= 2) hasPair = true;
    }

    int sh = 13 - uniq - (hasPair ? 1 : 0);
    return sh;
}

// 七対子
private int Shanten_Chiitoi(int[] c)
{
    int pairs = 0;
    int uniq = 0;
    for (int i = 0; i < 34; i++)
    {
        if (c[i] > 0) uniq++;
        pairs += (c[i] / 2);
    }
    if (pairs > 7) pairs = 7;

    int sh = 6 - pairs + System.Math.Max(0, 7 - uniq);
    return sh;
}

// 一般手（4面子1雀頭）
private int Shanten_Normal(int[] c)
{
    int min = 8;

    // 再帰探索（面子/ターツ/雀頭）
    void Dfs(int idx, int meld, int taatsu, int pair)
    {
        // ターツは最大 (4 - meld)
        if (taatsu > 4 - meld) taatsu = 4 - meld;

        // 末尾：シャンテン計算
        if (idx >= 34)
        {
            int sh = 8 - meld * 2 - taatsu - pair;
            if (sh < min) min = sh;
            return;
        }

        // 空なら次へ
        if (c[idx] == 0)
        {
            Dfs(idx + 1, meld, taatsu, pair);
            return;
        }

        // 牌が余りすぎても意味がないので簡易枝刈り
        if (meld > 4) meld = 4;

        // 刻子
        if (c[idx] >= 3)
        {
            c[idx] -= 3;
            Dfs(idx, meld + 1, taatsu, pair);
            c[idx] += 3;
        }

        // 順子（数牌のみ）
        if (idx < 27)
        {
            int suitBase = (idx / 9) * 9;
            int pos = idx % 9;
            if (pos <= 6 && c[idx] > 0 && c[idx + 1] > 0 && c[idx + 2] > 0)
            {
                c[idx]--; c[idx + 1]--; c[idx + 2]--;
                Dfs(idx, meld + 1, taatsu, pair);
                c[idx]++; c[idx + 1]++; c[idx + 2]++;
            }
        }

        // 雀頭（未使用なら）
        if (pair == 0 && c[idx] >= 2)
        {
            c[idx] -= 2;
            Dfs(idx, meld, taatsu, 1);
            c[idx] += 2;
        }

        // 対子ターツ（雀頭とは別枠の不完全形）
        if (c[idx] >= 2)
        {
            c[idx] -= 2;
            Dfs(idx, meld, taatsu + 1, pair);
            c[idx] += 2;
        }

        // 両面ターツ（隣）
        if (idx < 27)
        {
            int suitBase = (idx / 9) * 9;
            int pos = idx % 9;
            if (pos <= 7 && c[idx] > 0 && c[idx + 1] > 0)
            {
                c[idx]--; c[idx + 1]--;
                Dfs(idx, meld, taatsu + 1, pair);
                c[idx]++; c[idx + 1]++;
            }

            // 嵌張ターツ（1つ飛び）
            if (pos <= 6 && c[idx] > 0 && c[idx + 2] > 0)
            {
                c[idx]--; c[idx + 2]--;
                Dfs(idx, meld, taatsu + 1, pair);
                c[idx]++; c[idx + 2]++;
            }
        }

        // 1枚落として次へ
        c[idx]--;
        Dfs(idx, meld, taatsu, pair);
        c[idx]++;
    }

    // 作業用配列を汚さないようコピーしてから回す
    int[] work = new int[34];
    for (int i = 0; i < 34; i++) work[i] = c[i];

    // work を使うため、Dfs 内で参照する c を差し替える
    int[] saved = c;
    c = work;
    Dfs(0, 0, 0, 0);
    c = saved;

    return min;
}
private bool TryToIndex34(string rawId, out int idx)
{
    idx = -1;
    string id = NormalizeEnemyTileId(rawId);

    if (TryParseSuitNum(id, out int suit, out int num))
    {
        // suit: Man=0 Pin=1 Sou=2 Honor=3（TryParseSuitNum 側の仕様）
        if (suit == 3)
        {
            // num: 1..7 を 27..33（東南西北白發中）に割り当て
            idx = 27 + (num - 1);
            return (idx >= 27 && idx < 34);
        }

        idx = suit * 9 + (num - 1);
        return (idx >= 0 && idx < 27);
    }

    // TryParseSuitNum が拾えない表記ゆれ対策（フル名）
    switch (id)
    {
        case "East":  idx = 27; return true;
        case "South": idx = 28; return true;
        case "West":  idx = 29; return true;
        case "North": idx = 30; return true;
        case "White": idx = 31; return true;
        case "Green": idx = 32; return true;
        case "Red":   idx = 33; return true;
    }

    // さらに短縮表記にも対応（E,S,W,N,P,F,C）
    switch (id)
    {
        case "E": idx = 27; return true;
        case "S": idx = 28; return true;
        case "W": idx = 29; return true;
        case "N": idx = 30; return true;
        case "P": idx = 31; return true;
        case "F": idx = 32; return true;
        case "C": idx = 33; return true;
    }

    return false;
}
    // ===== Spec: Enemy win damage is applied AFTER enemy scoring panel is closed =====
    [Header("Enemy Win Damage Animation")]
    [SerializeField] private float enemyWinDamageAnimSeconds = 1.0f;

    [Tooltip("敵の和了でプレイヤーHPが減る演出中に鳴らすSE（必要なら1秒程度の音を用意）")]
    [SerializeField] private AudioClip enemyWinDamageSEClip;

    [Tooltip("上のSEを鳴らすAudioSource（UI用のSE Source等を割り当て）")]
    [SerializeField] private AudioSource enemyWinDamageSESource;

    private bool _pendingEnemyWinDamage = false;
    private int _pendingEnemyWinDamageBase = 0;
    private int _pendingEnemyWinDamageFinal = 0;
    private bool _enemyWinDamageAnimating = false;

// ★追加：同一牌4枚以上を禁止（敵はカンしない）
private bool HasQuadOrMore13(System.Collections.Generic.List<string> hand13)
{
    var cnt = new Dictionary<string, int>();
    for (int i = 0; i < hand13.Count; i++)
    {
        var id = NormalizeEnemyTileId(hand13[i]);
        if (!cnt.ContainsKey(id)) cnt[id] = 0;
        cnt[id]++;
        if (cnt[id] >= 4) return true;
    }
    return false;
}

// ★追加：ターツ形の良さをスコア化（両面＞嵌張＞双碰＞単騎）
// ※厳密分解ではなく「残り牌のつながり」の評価で十分効きます
private int ComputeTaatsuShapeScore13(System.Collections.Generic.List<string> hand13)
{
    int[] c = new int[34];
    for (int i = 0; i < hand13.Count; i++)
    {
        if (TryToIndex34(hand13[i], out int ix)) c[ix]++;
    }

    int score = 0;

    // 数牌だけで“つながり”を見る（0-26）
    for (int suit = 0; suit < 3; suit++)
    {
        int baseIx = suit * 9;

        // 両面（n,n+1）…待ちが広くなりやすいので高評価
        for (int n = 0; n <= 7; n++)
        {
            int a = baseIx + n;
            int b = baseIx + n + 1;
            int m = System.Math.Min(c[a], c[b]);
            if (m > 0) score += 6 * m; // ★両面
        }

        // 嵌張（n,n+2）
        for (int n = 0; n <= 6; n++)
        {
            int a = baseIx + n;
            int b = baseIx + n + 2;
            int m = System.Math.Min(c[a], c[b]);
            if (m > 0) score += 4 * m; // ★嵌張
        }
    }

    // 双碰（対子）…両面より弱い
    for (int i = 0; i < 34; i++)
    {
        if (c[i] >= 2) score += 2; // ★双碰候補
    }

    // 単騎（孤立牌）…減点（繋がり無し）
    // “周辺も無い1枚”をざっくり検出
    for (int i = 0; i < 34; i++)
    {
        if (c[i] != 1) continue;

        bool hasNeighbor = false;

        if (i < 27)
        {
            int suit = i / 9;
            int pos  = i % 9;
            int baseIx = suit * 9;

            if (pos - 2 >= 0 && c[baseIx + pos - 2] > 0) hasNeighbor = true;
            if (pos - 1 >= 0 && c[baseIx + pos - 1] > 0) hasNeighbor = true;
            if (pos + 1 <= 8 && c[baseIx + pos + 1] > 0) hasNeighbor = true;
            if (pos + 2 <= 8 && c[baseIx + pos + 2] > 0) hasNeighbor = true;
        }

        if (!hasNeighbor) score -= 3; // ★単騎寄りは減点
    }

    return score;
}

private System.Collections.Generic.IEnumerable<string> EnumerateAllTileIds34()
{
    for (int n = 1; n <= 9; n++)
    {
        yield return "Man" + n;
        yield return "Pin" + n;
        yield return "Sou" + n;
    }
    yield return "East";
    yield return "South";
    yield return "West";
    yield return "North";
    yield return "White";
    yield return "Green";
    yield return "Red";
}

private void WireEnemyTurnClickTargets()
{
    // Build per-ID quota: the set of tile IDs that belong to *this enemy turn*.
    var quota = new Dictionary<string, int>();
    foreach (var t in lastEnemyTurnTiles)
    {
        if (string.IsNullOrEmpty(t)) continue;
        if (!quota.ContainsKey(t)) quota[t] = 0;
        quota[t]++;
    }

    int listCount = enemyDiscards.Count;
    int childCount = enemyDiscardArea ? enemyDiscardArea.childCount : 0;

    // Pass A) Decide which *list indices* are this-turn targets (ID-based, newest優先)
    var isCurrentByList = new bool[listCount];
    for (int li = listCount - 1; li >= 0; li--)
    {
        var id = enemyDiscards[li];
        if (string.IsNullOrEmpty(id)) { isCurrentByList[li] = false; continue; }
        if (quota.TryGetValue(id, out var q) && q > 0)
        {
            isCurrentByList[li] = true;
            quota[id] = q - 1;
        }
    }

    // Pass B) Wire each UI child using its embedded list index in name "EnemyDiscard_{index}_..."
    for (int ci = 0; ci < childCount; ci++)
    {
        if (!enemyDiscardArea) break;
        if (ci >= enemyDiscardArea.childCount) break;

        var tileTf = enemyDiscardArea.GetChild(ci);
        if (!tileTf) continue;

        var btn = EnsureTileButton(tileTf);
        if (!btn) continue;

        btn.onClick.RemoveAllListeners();

        int listIdx = ci;
        try
        {
            // name は "EnemyDiscard_{index}_{id...}" だが、id は '_' を含むことがあるので復元しない
            var nm = tileTf.gameObject.name;
            var us = nm.Split('_');
            if (us.Length >= 2)
            {
                int parsed;
                if (int.TryParse(us[1], out parsed)) listIdx = parsed; // ★index だけ拾う
            }
        }
        catch { }

        if (listIdx < 0 || listIdx >= listCount)
        {
            btn.interactable = false;
            SetTileGrey(tileTf, false);
            SetTileHighlight(tileTf, false);
            SetTileSparkle(tileTf, false);
            continue;
        }

string tId = enemyDiscards[listIdx];

bool used = enemyUsedIndices.Contains(listIdx);
bool isCurrent = isCurrentByList[listIdx];

btn.interactable = isCurrent && !used;

bool ronNow = false;
bool actionable = false;

string baseId = NormalizeEnemyDiscardForAction(tId);
if (btn.interactable && !string.IsNullOrEmpty(baseId))
{
    ronNow = CanRonWith(baseId, out _, out _, out _, out _);
    actionable = ronNow || CanPonWithBase(baseId) || CanKanWithBase(baseId) || CanChiWithBase(baseId);
}

// リーチ中はロン以外の鳴きは不可
if (isRiichi && !ronNow)
{
    btn.interactable = false;
    actionable = false;
}

if (used)
{
    SetTileGrey(tileTf, true);
    SetTileCallTargetHighlight(tileTf, false);
    SetTileSparkle(tileTf, false);
}
else
{
    SetTileGrey(tileTf, false);

    // ★このターンの捨て牌（＝クリック可能）だけ赤ハイライト/スパークル対象にする
    SetTileCallTargetHighlight(tileTf, btn.interactable && actionable && !ronNow);
    SetTileSparkle(tileTf, btn.interactable && ronNow);
}

        int captured = listIdx; // pass logical index (matches enemyDiscards)
        btn.onClick.AddListener(() => OnEnemyTileClicked(captured));
    }
}
private UnityEngine.UI.Button EnsureTileButton(Transform tileTf)
{
    // Be resilient to transient destruction during UI rebuilds.
    if (tileTf == null) return null;

    var root = tileTf.gameObject;
    if (!root) return null;

    // Prefer a Button on the root.
    var btn = root.GetComponent<UnityEngine.UI.Button>();
    if (!btn)
    {
        // Do NOT search children (may traverse destroyed grandchildren while rebuilding)
        btn = root.AddComponent<UnityEngine.UI.Button>();
    }

    // ルート側にも透明Graphicを確保（Selectableの前提を満たす）
    var rootImg = root.GetComponent<UnityEngine.UI.Image>();
    if (!rootImg) rootImg = root.AddComponent<UnityEngine.UI.Image>();
    rootImg.color = new UnityEngine.Color(1, 1, 1, 0);
    rootImg.enabled = true;

    // ★重要：当たり判定は「牌のSprite全体」にしたいので、
    // ルートではなく Art/Image を raycast 対象にする
    rootImg.raycastTarget = false;
    btn.targetGraphic = rootImg;

    // Art/Image を raycast 対象に戻す（SetTileSprite が false にしているためここで上書き）
    var artImg = FindArtImage(tileTf);
    if (artImg)
    {
        artImg.enabled = true;
        artImg.raycastTarget = true;

        // alphaHitTestMinimumThreshold は、テクスチャが readable / 非crunch 等の条件が必要で例外になり得るため触らない
        // （今回の要件「Sprite全体をクリック可能にする」には不要）
    }

    btn.transition = UnityEngine.UI.Selectable.Transition.None;
    btn.interactable = true; // actual interactable will be set by caller
    return btn;
}
private Transform FindEnemyDiscardChildByListIndex(int listIndex)
    {
        if (!enemyDiscardArea) return null;
        int cc = enemyDiscardArea.childCount;
        for (int i = 0; i < cc; i++)
        {
            var child = enemyDiscardArea.GetChild(i);
            if (!child) continue;
            var nm = child.gameObject.name;
            // Expected format: EnemyDiscard_{index}_{id}
            if (!string.IsNullOrEmpty(nm) && nm.StartsWith("EnemyDiscard_"))
            {
                var us = nm.Split('_');
                if (us.Length >= 3)
                {
                    int parsed;
                    if (int.TryParse(us[1], out parsed) && parsed == listIndex)
                        return child;
                }
            }
        }
        // Fallback: if names are unavailable, try direct index (best-effort)
        if (listIndex >= 0 && listIndex < enemyDiscardArea.childCount)
            return enemyDiscardArea.GetChild(listIndex);
        return null;
    }


    private void RefreshEnemySelectionLift()
    {
        int start = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
        int n = enemyDiscards.Count;
        for (int i=start; i<n; i++)
        {
            if (!enemyDiscardArea) break;
            if (i >= enemyDiscardArea.childCount) break;
            var tf = enemyDiscardArea.GetChild(i);
            if (!tf) continue;
            var art = tf.Find("Art") as RectTransform;
            if (!art && tf.childCount>0) art = tf.GetChild(0) as RectTransform;
            if (art)
            {
                var p = art.anchoredPosition; p.y = 0f; art.anchoredPosition = p;
            }
        }
        if (selectedEnemyIndex >= 0 && enemyDiscardArea && selectedEnemyIndex < enemyDiscardArea.childCount)
        {
            var tf = enemyDiscardArea.GetChild(selectedEnemyIndex);
            if (!tf) return;
            var art = tf.Find("Art") as RectTransform;
            if (!art && tf.childCount>0) art = tf.GetChild(0) as RectTransform;
            if (art)
            {
                var p = art.anchoredPosition; p.y = raisePixels; art.anchoredPosition = p;
            }
        }
    }
private void AutoSkipEnemyIfNothing(float waitSec)
{
    waitSec = Mathf.Max(0.9f, waitSec); // ★端末差の取りこぼし防止で最低待機を少し伸ばす
    // ★重要：ここでは「カットイン中だから判定しない(return)」をしない。
    //        代わりに、予約した _AutoSkip 側でカットイン終了まで待つ。

    // 1) 理論上の行動可（宣言はここ1回だけ）
    bool canRon = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);
    bool canPon = lastEnemyTurnTiles.Any(t => CanPonWithBase(NormalizeEnemyDiscardForAction(t)));
    bool canChi = lastEnemyTurnTiles.Any(t => CanChiWithBase(NormalizeEnemyDiscardForAction(t)));
    bool canKan = lastEnemyTurnTiles.Any(t => CanKanWithBase(NormalizeEnemyDiscardForAction(t)));

    // ★リーチ中はポン/チー/カン不可
    if (isRiichi)
    {
        canPon = false;
        canChi = false;
        canKan = false;
    }

    // ★元仕様：敵捨て牌に対してロン/鳴き候補があるなら、UI状態に依存せず必ず停止
    if (phase == Phase.EnemyTurn && (canRon || canPon || canChi || canKan))
    {
        _autoSkipPending = false;           // 進行予約キャンセル
        RefreshEnemySelectionLift();
        WireEnemyTurnClickTargets();        // 赤ハイライト&クリック配線
        return;
    }

    // 2) 実クリック可能な牌があるか（このターンの牌 & 未使用 & interactable）
    bool anyClickable = false;
    if (enemyDiscardArea)
    {
        int start = Mathf.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
        for (int i = start; i < enemyDiscards.Count; i++)
        {
            var go = (i < enemyDiscardArea.childCount) ? enemyDiscardArea.GetChild(i).gameObject : null;
            if (!go) continue;
            if (_committedDiscardInstanceIDs.Contains(go.GetInstanceID())) continue;

            var b = go.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (b != null && b.interactable)
            {
                anyClickable = true;
                break;
            }
        }
    }

    // 3) 候補もクリック対象も無いなら自動進行予約
    if (_autoSkipPending) return; // 多重起動防止
    _autoSkipPending = true;
    StartCoroutine(_AutoSkip(waitSec));
}
private System.Collections.IEnumerator _AutoSkip(float t)
{
    // まず通常通り、指定秒だけ待つ
    yield return new WaitForSeconds(t);

    // ★待機中にカットイン（プレイヤーリーチ/敵リーチ/敵スキル）が始まった場合は、それが終わるまで待つ
    while (_playerRiichiCutinRunning || _enemyRiichiCutinRunning || _enemySkillCutinRunning)
        yield return null;

    // その間にプレイヤーが何か行動していればキャンセル
    if (!_autoSkipPending) yield break;

    _autoSkipPending = false;

    // ★凍結中（スコア表示中など）は絶対に次ターンへ進めない
    if (_freezeProgression) yield break;

    // ★重要：ここで最終チェック（敵捨て牌でロン/鳴き候補が出ていたら、絶対に進めない）
    if (phase == Phase.EnemyTurn)
    {
        bool canRon = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);
        bool canPon = lastEnemyTurnTiles != null && lastEnemyTurnTiles.Any(x => CanPonWithBase(NormalizeEnemyDiscardForAction(x)));
        bool canChi = lastEnemyTurnTiles != null && lastEnemyTurnTiles.Any(x => CanChiWithBase(NormalizeEnemyDiscardForAction(x)));
        bool canKan = lastEnemyTurnTiles != null && lastEnemyTurnTiles.Any(x => CanKanWithBase(NormalizeEnemyDiscardForAction(x)));

        if (isRiichi)
        {
            canPon = false;
            canChi = false;
            canKan = false;
        }

        if (canRon || canPon || canChi || canKan)
        {
            RefreshEnemyDiscardUI();
            WireEnemyTurnClickTargets();
            EvaluateWinUI_New();
            UpdateButtons();

            if (statusTMP) statusTMP.text = "敵の捨て牌へのリアクション待ち（自動進行停止）";
            yield break;
        }

        // まだ敵ターン中で、候補が無いならプレイヤーのツモへ
        BeginOfferPhase();
    }
}
private void OnClickRiichi()
{
    if (phase != Phase.Offer || !isTenpai || isRiichi) return;

    // ★追加：リーチ宣言ターンを記録（ダブル立直／一発判定に使用）
    _playerRiichiDeclaredTsumoCountThisRound = _playerTsumoCountThisRound;

    // ★追加：一発は「リーチ宣言～次の自分ツモ」まで有効
    _playerIppatsuEligible = true;

    // ★追加：ダブル立直（簡易判定：その局の自分1ターン目でリーチしたらダブル立直扱い）
    // ※このプロジェクトのターン定義は BeginOfferPhase() の _playerTsumoCountThisRound++ に一致
    _playerIsDoubleRiichi = (_playerTsumoCountThisRound == 1);

    // リーチ宣言
    isRiichi = true;

    // ★このターンは「ツモ」選択を出さない（= 自動捨てを徹底する）
    suppressTsumoThisOffer = true;

    UpdateTenpaiBadge();
    if (statusTMP) statusTMP.text = "リーチ！";
    RefreshAll();

    // ★このターンのツモ4牌（入替え後のOffer）は必ず自動で捨てる
    //   カットイン中なら、カットインが終わるまで待ってから確定する
    if (!_autoConfirmOfferPending)
    {
        _autoConfirmOfferPending = true;
        StartCoroutine(_AutoConfirmOfferAfter(0f));
    }

// リーチ演出
_playerRiichiCutinCo = StartCoroutine(__ShowPlayerRiichiCutin());
}


    // NEW: リーチ中の自分のツモ番で、和了牌が無いなら一定秒後に自動で「捨てる確定」して敵ターンへ

 // 手牌の中に「アンカン可能な4枚組」が1つでもあるかどうかを判定する。
// ※ 対局中UIと同様に、「*」付きはさらし牌として扱い、
//    コアID（末尾の'*'を除いた文字列）で 4 枚揃っているかを見る。
private bool HasAnyAnkanCandidateInHand()
{
    if (hand == null || hand.Count == 0) return false;

    var counts = new Dictionary<string,int>();
    foreach (var id in hand)
    {
        if (string.IsNullOrEmpty(id)) continue;
        var core = id.EndsWith("*") ? id.Substring(0, id.Length - 1) : id;
        if (string.IsNullOrEmpty(core)) continue;
        if (!counts.ContainsKey(core)) counts[core] = 0;
        counts[core]++;
    }

    foreach (var kv in counts)
    {
        if (kv.Value >= 4) return true;
    }
    return false;
}
private void AutoSkipOfferDuringRiichiIfNoWin(float waitSec)
{
    if (phase != Phase.Offer || !isRiichi) return;

    // このツモ番でツモ抑制が立っていたら自動進行しない（従来フラグを尊重）
    if (suppressTsumoThisOffer) return;

    // 和了牌の有無を安全に再判定（EvaluateWinUI_New と同等の軽量チェック）
    bool hasWin = false;
    try
    {
        // 選択ツモが有効ならそれもチェック
        if (TryGetSelectedTsumoTile(out var selId) && !string.IsNullOrEmpty(selId))
            hasWin |= CanTsumoWith(selId, out _, out _, out _, out _);

        // 4枚オファー内に当たりがあるか
        if (!hasWin && offers != null && offers.Count > 0)
            hasWin |= offers.Any(id => CanTsumoWith(id, out _, out _, out _, out _));
    }
    catch { /* 無害化 */ }

    // ★修正：リーチ後はカン不可なので、手牌4枚同一があっても自動進行は止めない
    if (hasWin) return;

    if (_autoConfirmOfferPending) return;     // 多重起動防止（Offer自動確定専用）
    _autoConfirmOfferPending = true;
    StartCoroutine(_AutoConfirmOfferAfter(waitSec));
}
private IEnumerator _AutoConfirmOfferAfter(float t)
{
    yield return new WaitForSeconds(t);

    // ★リーチカットイン（プレイヤー/敵）＋敵スキルカットインの最中は、絶対に Offer を自動確定しない
    while (_playerRiichiCutinRunning || _enemyRiichiCutinRunning || _enemySkillCutinRunning)
        yield return null;

    // 多重起動防止ロックが立っていなければ何もしない
    if (!_autoConfirmOfferPending) yield break;

    // Offer フェーズ以外に移行していたらロック解除して終了
    if (phase != Phase.Offer)
    {
        _autoConfirmOfferPending = false;
        yield break;
    }

    // ★重要：
    // リーチ宣言ターンに suppressTsumoThisOffer が立っている場合は、
    // そのターンのツモ4枚に和了牌が含まれていても必ず自動確定して捨てる。
    // つまり、このターンだけは hasWin を見て停止してはいけない。
    if (!suppressTsumoThisOffer)
    {
        bool hasWin = false;
        try
        {
            if (TryGetSelectedTsumoTile(out var selId) && !string.IsNullOrEmpty(selId))
                hasWin |= CanTsumoWith(selId, out _, out _, out _, out _);

            if (!hasWin && offers != null && offers.Count > 0)
                hasWin |= offers.Any(id => CanTsumoWith(id, out _, out _, out _, out _));
        }
        catch { }

        if (hasWin)
        {
            _autoConfirmOfferPending = false;

            // ボタン表示を最新化（ツモボタンが出る／自動ツモ切りが止まる）
            EvaluateWinUI_New();
            UpdateButtons();

            if (statusTMP) statusTMP.text = "和了可能です（自動ツモ切り停止）";
            yield break;
        }
    }

    // Offer の自動確定（＝「捨てる」確定して敵ターンへ）
    _autoConfirmOfferPending = false;
    OnClickConfirm();
}
private void OnEnemyTileClicked(int idx)
{
    // 敵ターン中 あるいは 鳴き選択中でも処理する
    if (!(phase == Phase.EnemyTurn || phase == Phase.ChoosingCall)) return;
    if (idx < 0 || idx >= enemyDiscards.Count) return;

int start = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);

// そのターンの捨て牌(idx>=start)は、採用ロックが立っていてもプレイヤーの鳴き/ロン判断対象にする
if (idx < start || enemyUsedIndices.Contains(idx)) return;


    // --- トグル：同じ牌をもう一度押したら選択解除 ---
    if (selectedEnemyIndex == idx)
    {
        selHand.Clear();
        // ←追加：手牌側の自動選択(ゴースト)も見た目ごと解除
        RebuildRaiseOverlays(handArea, selHand, hand);

        callMode = CallMode.None;
        callBaseTile = null;
        selectedEnemyIndex = -1;
        ClearCallChoiceButtons();               // ミニボタンを閉じる
        if (phase == Phase.ChoosingCall) phase = Phase.EnemyTurn;
        RefreshEnemySelectionLift();
        UpdateButtons();
        if (statusTMP) statusTMP.text = "選択を解除しました";
        return;
    }


    // 新しく選択
    selectedEnemyIndex = idx;
    RefreshEnemySelectionLift();
    UpdateButtons();

string t = enemyDiscards[idx];
string tLogic = NormalizeEnemyDiscardForAction(t);

if (isRiichi)
{
    if (!CanRonWith(tLogic, out _, out _, out var _, out _))
    {
        if (statusTMP) statusTMP.text = "リーチ中は鳴けません";
        return;
    }
}

if (CanRonWith(tLogic, out _, out _, out var _, out _))
    return; // ボタン側で処理

string baseLogic = NormalizeEnemyDiscardForAction(t);
bool canPon = CanPonWithBase(baseLogic);
bool canChi = CanChiWithBase(baseLogic);
bool canKan = CanKanWithBase(baseLogic);

    // 複数可なら選択UIを表示
    int optionCount = (canPon?1:0) + (canChi?1:0) + (canKan?1:0);
    if (optionCount >= 2)
    {
        ShowCallChoiceButtons(t, canPon, canChi, canKan);
        if (statusTMP) statusTMP.text = "鳴きを選んでください";
        return;
    }

    // 単一可なら直で開始
    if (canPon)      StartCallFromTile(CallMode.Pon, t);
    else if (canKan) StartCallFromTile(CallMode.KanOpen, t);
    else if (canChi) StartCallFromTile(CallMode.Chi, t);
    else             { if (statusTMP) statusTMP.text = "この牌では鳴けません"; }
}

private void StartCallFromTile(CallMode mode, string baseTile)
{
    int start = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
    int baseIdx = -1;
    for (int i = enemyDiscards.Count - 1; i >= start; i--)
        if (enemyDiscards[i] == baseTile) { baseIdx = i; break; }
    if (baseIdx < 0) return;

    // 自動生成UIは廃止：手動UI用の候補フラグだけ消す
    _pendingCallPon = false;
    _pendingCallChi = false;
    _pendingCallKan = false;

    callMode = mode;
    callBaseTile = baseTile;
    selHand.Clear();
    phase = Phase.ChoosingCall;

    selHand.Clear();
    phase = Phase.ChoosingCall;
    if (callMode == CallMode.Chi) EnableHandForChiDynamic(); else EnableHandForCall(IsSelectableForCurrentCall);
    if (statusTMP)
        statusTMP.text = mode switch {
            CallMode.Pon=>"ポン：同じ牌を2枚選んで確定",
            CallMode.KanOpen=>"カン：同じ牌を3枚選んで確定",
            CallMode.Chi=>"チー：順子になる2枚選んで確定",
            _=>"" };

    // ★追加：副露候補を自動選択（左から最初の成立組み合わせ）
    AutoPreselectForCurrentCall();

    UpdateButtons();

    }
private void OnClickSkipCall()
    {
    ClearCallChoiceButtons(); // ← 追加

    if (phase == Phase.EnemyTurn || phase == Phase.ChoosingCall)
    {
        // ★追加：連打防止 ― スキップボタンを即座に無効化し、
        //         走行中の AutoSkip コルーチンも無効化して二重 BeginOfferPhase を防ぐ
        if (btnSkip) btnSkip.interactable = false;
        _autoSkipPending = false;

        selHand.Clear();
        callMode = CallMode.None;
        callBaseTile = null;
        selectedEnemyIndex = -1;
        ClearEnemyDiscardHighlights_EndOfReactionTurn();
        BeginOfferPhase();
        return;
    }
        // Skip Tsumo during our offer turn
        if (phase == Phase.Offer)
        {
            suppressTsumoThisOffer = true;
            if (statusTMP) statusTMP.text = "ツモを見送りました（このターンは通常操作が可能です）";
            UpdateButtons();
            return;
        }
}
private void ClearEnemyDiscardHighlights_EndOfReactionTurn()
{
    if (!enemyDiscardArea) return;

    for (int i = 0; i < enemyDiscardArea.childCount; i++)
    {
        var tileTf = enemyDiscardArea.GetChild(i);

        // まず通常ルートでOFF（SparkleのCoroutine停止もここで行われる想定）
        SetTileSparkle(tileTf, false);
        SetTileCallTargetHighlight(tileTf, false);

        // ★保険：RaiseOverlay / Art / Image など、どこにOutlineが付いてても全消し
        var outlines = tileTf.GetComponentsInChildren<UnityEngine.UI.Outline>(true);
        foreach (var o in outlines) o.enabled = false;
    }
}
    private void EnableAllHandButtons(bool on = true)
    {
        int n = Math.Min(hand.Count, handArea.childCount);
        for (int i = 0; i < n; i++)
        {
            var btn = handArea.GetChild(i).GetComponent<Button>();
            if (btn) btn.interactable = on;
        }
    }
    private void EnableAllOfferButtons(bool on = true)
{
    if (offerArea == null) return;

    int n = Math.Min(offers.Count, offerArea.childCount);
    for (int i = 0; i < n; i++)
    {
        var btn = offerArea.GetChild(i).GetComponent<Button>();
        if (btn) btn.interactable = on;
    }
}
    private void EnableHandForCall(Func<string,bool> allowTile)
    {
        int n = Math.Min(hand.Count, handArea.childCount);
        for (int i = 0; i < n; i++)
        {
            var btn = handArea.GetChild(i).GetComponent<Button>();
            if (!btn) continue;
            bool ok = allowTile(hand[i]);
            btn.interactable = ok;
            if (!ok) selHand.Remove(i);
        }
    }
private bool IsSelectableForCurrentCall(string tileId)
{
    if (string.IsNullOrEmpty(callBaseTile)) return false;

    string baseLogic = StripTileIdForLogic(callBaseTile);
    string myLogic   = StripTileIdForLogic(tileId);

    if (callMode == CallMode.Pon || callMode == CallMode.KanOpen)
        return myLogic == baseLogic;

    if (callMode == CallMode.Chi)
    {
        if (!TryParseSuitNum(baseLogic, out var s, out int n)) return false;
        if (!TryParseSuitNum(myLogic,   out var s2, out int n2)) return false;
        if (s != s2) return false;
        int d = n2 - n;
        return d >= -2 && d <= 2 && d != 0;
    }
    return false;
}


    // Dynamic check for Chi: allow only tiles that complete a straight with the chosen enemy tile
    // and (if one tile is already selected) only the 2nd tile that completes the straight.
    private bool IsSelectableForChiWithCurrent(string candidateId, int candidateIndex)
    {
        // Always allow deselection of an already selected index
        if (selHand.Contains(candidateIndex)) return true;

if (string.IsNullOrEmpty(callBaseTile)) return false;

string baseLogic = StripTileIdForLogic(callBaseTile);
string candLogic = StripTileIdForLogic(candidateId);

if (!TryParseSuitNum(baseLogic, out var s, out int nb)) return false;
if (!TryParseSuitNum(candLogic, out var s2, out int n2)) return false;

        if (s != s2) return false;

        // No selection yet: allow nb±1, nb±2 within 1..9
        if (selHand.Count == 0)
        {
            int d = n2 - nb;
            if (d == 0 || d < -2 || d > 2) return false;
            return n2 >= 1 && n2 <= 9;
        }

        // One already selected: only allow the tile that, together with base and the selected one, makes consecutive numbers
        if (selHand.Count == 1)
        {
            var oneIdx = 0;
            foreach (var i in selHand) { oneIdx = i; break; }
            var chosenIdLogic = StripTileIdForLogic(hand[oneIdx]);
if (!TryParseSuitNum(chosenIdLogic, out var s3, out int n3)) return false;

            if (s3 != s) return false;
            var ns = new System.Collections.Generic.List<int> { nb, n2, n3 }; ns.Sort();
            return ns[0] + 1 == ns[1] && ns[1] + 1 == ns[2];
        }

        // Already two selected -> do not allow a third selection
        return false;
    }

    private void EnableHandForChiDynamic()
    {
        int n = System.Math.Min(hand.Count, handArea.childCount);
        for (int i = 0; i < n; i++)
        {
            var btn = handArea.GetChild(i).GetComponent<UnityEngine.UI.Button>();
            if (!btn) continue;
            bool ok = selHand.Contains(i) || IsSelectableForChiWithCurrent(hand[i], i);
            btn.interactable = ok;
        }
    }

    private bool IsCallSelectionSatisfied()
    {
        if (callMode == CallMode.None) return false;
        var picked = selHand.Count;
string baseLogic = StripTileIdForLogic(callBaseTile);

if (callMode == CallMode.Pon)
    return picked == 2 && selHand.All(i => StripTileIdForLogic(hand[i]) == baseLogic);

if (callMode == CallMode.KanOpen)
    return picked == 3 && selHand.All(i => StripTileIdForLogic(hand[i]) == baseLogic);

if (callMode == CallMode.Chi)
{
    if (selHand.Count != 2) return false;
    var ids = selHand.Select(i => StripTileIdForLogic(hand[i])).ToList();

    bool ValidChi(IList<string> chosen, string baseT)
    {
        if (chosen.Count != 2) return false;
        if (!TryParseSuitNum(baseT, out int s, out int nb)) return false;
        if (!TryParseSuitNum(chosen[0], out int s1, out int n1)) return false;
        if (!TryParseSuitNum(chosen[1], out int s2, out int n2)) return false;
        if (s != s1 || s != s2) return false;
        var ns = new List<int> { nb, n1, n2 }; ns.Sort();
        return ns[0] + 1 == ns[1] && ns[1] + 1 == ns[2];
    }

    return ValidChi(ids, baseLogic);
}

        return false;
    }
    
private void ConfirmCall()
{
    // ★追加：敵スキル「麻痺」中は鳴き（チー／ポン／カン）不可
    if (EnemySkills_IsPlayerParalyzed())
    {
        if (statusTMP) statusTMP.text = "麻痺中のため鳴きはできません";
        return;
    }

    // ★追加：リーチ中は鳴き（チー／ポン／カン）不可
    if (isRiichi)
    {
        if (statusTMP) statusTMP.text = "リーチ中は鳴けません";
        return;
    }
    if (callMode == CallMode.None || string.IsNullOrEmpty(callBaseTile)) { if (statusTMP) statusTMP.text = "鳴きの対象がありません"; return; }
      if (hand == null || hand.Count == 0) { if (statusTMP) statusTMP.text = "手牌がありません"; return; }

    // --- CHI ---
    if (callMode == CallMode.Chi)
    {
    if (selHand.Count != 2) { if (statusTMP) statusTMP.text = "チーは2枚選択してください"; return; }
    var pickedIdx = selHand.Where(i => i >= 0 && i < hand.Count).Distinct().OrderByDescending(x => x).ToList();
    var pickedIds = pickedIdx.Select(i => hand[i]).ToList();

    bool ValidChi(string baseT, string a, string b)
    {
        if (!TryParseSuitNum(baseT, out int s0, out int n0)) return false;
        if (!TryParseSuitNum(a, out int s1, out int n1)) return false;
        if (!TryParseSuitNum(b, out int s2, out int n2)) return false;
        if (s0 != s1 || s0 != s2) return false;
        var ns = new System.Collections.Generic.List<int> { n0, n1, n2 }; ns.Sort();
        return ns[0] + 1 == ns[1] && ns[1] + 1 == ns[2];
    }
    if (!ValidChi(callBaseTile, pickedIds[0], pickedIds[1]))
    {
        if (statusTMP) statusTMP.text = "選択が順子になっていません";
        return;
    }

    // Remove from hand
    foreach (var i in pickedIdx) hand.RemoveAt(i);

    // ★修正：拾い牌を左端に固定して '*'、残り２枚だけをソートして右側へ
    var rest = new System.Collections.Generic.List<string> { pickedIds[0], pickedIds[1] };
    rest.Sort((a, b) => ToSortKey(a).CompareTo(ToSortKey(b)));
var baseNoStar = StripStar(callBaseTile);
var three = new System.Collections.Generic.List<string> { baseNoStar + "*", rest[0], rest[1] };

    melds.Add(three);
    RefreshMeldUI();


            // Mark the exact enemy discard we clicked as used (prefer selectedEnemyIndex)
            int baseIdx = selectedEnemyIndex;
            if (baseIdx < 0 || baseIdx >= enemyDiscards.Count)
            {
                int start = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
                for (int i = enemyDiscards.Count - 1; i >= start; i--)
                    if (enemyDiscards[i] == callBaseTile) { baseIdx = i; break; }
            }
            if (baseIdx >= 0 && baseIdx < enemyDiscards.Count) { enemyUsedIndices.Add(baseIdx); RefreshEnemyDiscardUI(); }

            // exclude the used enemy discard from future enemy-effects counting
            if (_enemyTurnHistory.Count > 0)
            {
                var last = _enemyTurnHistory.Last();
                if (last.Contains(callBaseTile)) last.Remove(callBaseTile);
            }

            // Advance to discard-after-call
            SortHand();
            selHand.Clear();
            callMode = CallMode.None;
            callBaseTile = null;
            phase = Phase.NeedDiscardAfterCall;
            EnableAllHandButtons(true);
            RefreshHandUI();
            UpdateButtons();
            if (statusTMP) statusTMP.text = "チー成立：一枚切ってください";
            return;
        }

        // --- PON/KAN (fallback to previous logic) ---
        if (!IsCallSelectionSatisfied()) { if (statusTMP) statusTMP.text = "選択が不足しています"; return; }

        var pickedIdx2 = selHand.Where(i => i >= 0 && i < hand.Count).Distinct().OrderByDescending(x => x).ToList();
        var pickedIds2 = pickedIdx2.Select(i => hand[i]).ToList();

        int baseIdx2 = selectedEnemyIndex;
        if (baseIdx2 < 0 || baseIdx2 >= enemyDiscards.Count)
        {
            int start2 = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
            for (int i = enemyDiscards.Count - 1; i >= start2; i--)
                if (enemyDiscards[i] == callBaseTile) { baseIdx2 = i; break; }
        }

        foreach (var i in pickedIdx2) { if (i >= 0 && i < hand.Count) hand.RemoveAt(i); }
var calledNoStar = StripStar(callBaseTile);   // 敵捨て牌（通常牌）想定
var calledStar   = calledNoStar + "*";

// pickedIds2 は「手牌から選んだ実ID（特別牌IDを保持）」なので、それを meld に入れる
var meld2 = new System.Collections.Generic.List<string>();

if (callMode == CallMode.Pon)
{
    // ポン：捨て牌(横) + 手牌2枚（特別牌なら特別牌のまま保持）
    meld2.Add(calledStar);
    meld2.Add(pickedIds2[0]);
    meld2.Add(pickedIds2[1]);
}
else if (callMode == CallMode.KanOpen)
{
    // 明槓：捨て牌(横) + 手牌3枚（特別牌なら特別牌のまま保持）
    meld2.Add(calledStar);
    meld2.Add(pickedIds2[0]);
    meld2.Add(pickedIds2[1]);
    meld2.Add(pickedIds2[2]);

    // ミンカン：ドラ表示 + （必要ならお守り追加）
    AddKanIndicator();
    try { Omamori_TryAddExtraDoraAfterKan(); } catch {}

    // ★嶺上は「手牌へ10枚目として右端に追加」→0.5s 後に並び替え
    StartCoroutine(__RinshanToHandFlow(1f, 1f, Phase.NeedDiscardAfterCall, "明槓 → リンシャン牌をツモ"));
}

if (meld2.Count > 0)
{
    melds.Add(meld2);
    RefreshMeldUI();
}

        if (baseIdx2 >= 0 && baseIdx2 < enemyDiscards.Count) { enemyUsedIndices.Add(baseIdx2); RefreshEnemyDiscardUI(); }

            // exclude the used enemy discard from future enemy-effects counting
      

      if (_enemyTurnHistory.Count > 0)
            {
                var last = _enemyTurnHistory.Last();
                if (last.Contains(callBaseTile)) last.Remove(callBaseTile);
            }

SortHand();
RefreshHandUI();
selHand.Clear();
ResetSelectionsAndUI();
callMode = CallMode.None;
callBaseTile = null;
// ★明槓は鳴き扱い：鳴き後の捨て要求フェーズを維持
phase = Phase.NeedDiscardAfterCall;
EnableAllHandButtons(true);
if (statusTMP) statusTMP.text = "鳴き：一枚切ってください";
UpdateButtons();
    }

    // ===== Helpers: parsing, sorting, mapping =====
    public static string IndexToId(int index)
    {
        // 0..8 = Man1..9, 9..17 = Pin1..9, 18..26 = Sou1..9, 27..33 = honors (East,South,West,North,White,Green,Red)
        if (index >= 0 && index < 9) return $"Man{index+1}";
        if (index >= 9 && index < 18) return $"Pin{index-9+1}";
        if (index >= 18 && index < 27) return $"Sou{index-18+1}";
        string[] honors = { "East","South","West","North","White","Green","Red" };
        if (index >= 27 && index < 34) return honors[index-27];
        return null;
    }
// ★鳴き/ロン/赤ハイライト/停止判定用：敵捨て牌を同じ形にそろえる
private string NormalizeEnemyDiscardForAction(string raw)
{
    if (string.IsNullOrEmpty(raw)) return raw;
    // "_" など敵捨て牌由来のサフィックスを落とす（既存仕様）
    var core = NormalizeEnemyTileId(raw);
    // "*" などロジックに不要な記号を落とす（クリック側と統一）
    core = StripTileIdForLogic(core);
    return core;
}
private bool TryParseSuitNum(string id, out int suit, out int num)
{
    suit = -1; num = -1;
    if (string.IsNullOrEmpty(id)) return false;

    // ★ロジック用：* と _sp を落とした形で判定
    id = StripTileIdForLogic(id);

    // ★最優先で字牌を確定（"South" が "Sou" に誤判定されるのを防ぐ）
    {
        string key = (id ?? "").Trim();
        string lower = key.ToLowerInvariant();

        if (lower == "east"  || lower == "e" || lower == "ton"   || key == "東") { suit = 3; num = 1; return true; }
        if (lower == "south" || lower == "s" || lower == "nan"   || key == "南") { suit = 3; num = 2; return true; }
        if (lower == "west"  || lower == "w" || lower == "sha"   || key == "西") { suit = 3; num = 3; return true; }
        if (lower == "north" || lower == "n" || lower == "pei"   || key == "北") { suit = 3; num = 4; return true; }
        if (lower == "white" || lower == "p" || lower == "haku"  || key == "白") { suit = 3; num = 5; return true; }
        if (lower == "green" || lower == "f" || lower == "hatsu" || key == "發" || key == "発") { suit = 3; num = 6; return true; }
        if (lower == "red"   || lower == "c" || lower == "chun"  || key == "中") { suit = 3; num = 7; return true; }
    }

    // 数牌
    if (id.StartsWith("Man")) { suit = 0; }
    else if (id.StartsWith("Pin")) { suit = 1; }
    else if (id.StartsWith("Sou")) { suit = 2; }
    else
    {
        return false;
    }

    // numeric part
    var m = System.Text.RegularExpressions.Regex.Match(id, @"\d+");
    if (!m.Success) return false;
    if (!int.TryParse(m.Value, out num)) return false;
    return num >= 1 && num <= 9;
}

// ★ADD: 選択した手牌 index を除外して、萬/筒/索の多数派スートを返す（同数時は Man > Pin > Sou）
private string GetMajorSuitExcludingIndex(int excludeIndex)
{
    int man = 0, pin = 0, sou = 0;
    for (int i = 0; i < hand.Count; i++)
    {
        if (i == excludeIndex) continue;
        var id = hand[i];
        if (id.StartsWith("Man")) man++;
        else if (id.StartsWith("Pin")) pin++;
        else if (id.StartsWith("Sou")) sou++;
        // 字牌はカウントしない
    }
    if (man >= pin && man >= sou) return "Man";
    if (pin >= man && pin >= sou) return "Pin";
    return "Sou";
}

    private int ToSortKey(string id)
    {
        if (string.IsNullOrEmpty(id)) return 9999;
        if (id.EndsWith("*")) id = id.Substring(0, id.Length-1);
        if (TryParseSuitNum(id, out int s, out int n))
        {
            if (s == 3) return 300 + n; // honors
            return s*100 + n;
        }
        return 9999;
    }

    private void SortHand()
    {
        hand.Sort((a,b) => ToSortKey(a).CompareTo(ToSortKey(b)));
    }
private bool IsTenpai(List<string> tiles)
{
    // ★聴牌判定はロジック用IDに正規化（Man5_sp → Man5、"*" も除去）
    var logicTiles = tiles.Select(StripTileIdForLogic).ToList();

    // ★七対子の聴牌（13枚時点で「6対子 + 1枚単騎」）も聴牌扱いにする
    //   例: [1,2,2,2,2,2,2] の形だけ true（トリプルが混ざる形は七対子の聴牌ではない）
    if (IsChiitoitsuTenpai13(logicTiles)) return true;

    // ★追加：国士無双の聴牌（13枚時点）
    //   - 13面待ち（13種すべて揃って重複なし）
    //   - 単騎待ち（12種 + どれか1種が対子）
    if (IsKokushiTenpai13(logicTiles)) return true;

    // naive: try every tile id; if any makes a win, consider tenpai
    for (int idx = 0; idx < 34; idx++)
    {
        string candidate = IndexToId(idx);
        if (string.IsNullOrEmpty(candidate)) continue;

        var snapshot = new System.Collections.Generic.List<string>(logicTiles);
        snapshot.Add(candidate);

        snapshot.Sort((a, b) => ToSortKey(a).CompareTo(ToSortKey(b)));

        if (IsStandardWin(snapshot)) return true;
    }
    return false;
}
// ★国士無双の聴牌判定（13枚）
// 13枚の時点で以下のどちらかなら true：
// 1) 13種すべて揃っている（重複なし） -> 13面待ち
// 2) 12種揃っていて、どれか1種が2枚ある -> 不足1種待ち
private bool IsKokushiTenpai13(List<string> tiles13)
{
    if (tiles13 == null) return false;
    if (tiles13.Count != 13) return false;

    // 正規化の結果が空のものが混ざっている場合は判定しない
    if (tiles13.Any(t => string.IsNullOrEmpty(t))) return false;

    // 国士の対象13種（このプロジェクトのID表記に合わせる）
    // 数牌：Man1/Man9 Pin1/Pin9 Sou1/Sou9
    // 字牌：East/South/West/North/White/Green/Red
    var required = new HashSet<string>
    {
        "Man1","Man9",
        "Pin1","Pin9",
        "Sou1","Sou9",
        "East","South","West","North",
        "White","Green","Red"
    };

    // 国士の対象外が混ざっていたら国士聴牌にはならない
    for (int i = 0; i < tiles13.Count; i++)
    {
        if (!required.Contains(tiles13[i])) return false;
    }

    var groups = tiles13.GroupBy(t => t).ToList();
    int unique = groups.Count;

    // 13面待ち：13種すべて揃い、各1枚（= 重複なし）
    if (unique == 13)
    {
        // 念のため全部1枚か確認
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Count() != 1) return false;
        }
        return true;
    }

    // 単騎待ち：12種 + どれか1種が2枚（= 1対子）
    if (unique == 12)
    {
        int pairCount = 0;
        for (int i = 0; i < groups.Count; i++)
        {
            int c = groups[i].Count();
            if (c == 2) pairCount++;
            else if (c == 1) { }
            else return false; // 3枚以上は国士の13枚聴牌として不正
        }
        return pairCount == 1;
    }

    return false;
}

// ★七対子の聴牌判定（13枚）
// 13枚の時点で「6対子 + 1枚（単騎）」のときだけ true
private bool IsChiitoitsuTenpai13(List<string> tiles13)
{
    if (tiles13 == null) return false;
    if (tiles13.Count != 13) return false;

    // 正規化の結果が空のものが混ざっている場合は、ここでは判定しない
    if (tiles13.Any(t => string.IsNullOrEmpty(t))) return false;

    var counts = tiles13
        .GroupBy(t => t)
        .Select(g => g.Count())
        .OrderBy(c => c)
        .ToArray();

    // 13枚の七対子聴牌は [1,2,2,2,2,2,2] のみ
    if (counts.Length != 7) return false;
    if (counts[0] != 1) return false;
    for (int i = 1; i < counts.Length; i++)
        if (counts[i] != 2) return false;

    return true;
}
    private void UpdateTenpaiBadge()
    {
        if (tenpaiBadgeTMP)
        {
            isTenpai = IsTenpai(hand);
            tenpaiBadgeTMP.text = isTenpai ? "聴" : "";
            tenpaiBadgeTMP.color = isTenpai ? new Color(1f,0.3f,0.3f,1f) : new Color(1f,1f,1f,0.3f);
        }
        else
        {
            // バッジUIが無くても、内部状態と待ち牌UIは更新したい
            isTenpai = IsTenpai(hand);
        }

        UpdatePlayerTenpaiWaitsUI();
    }
private void ComputePlayerTenpaiWaits(HashSet<string> outWaits)
{
    outWaits.Clear();

    // そもそも聴牌でなければ空
    if (!IsTenpai(hand)) return;

    // 34種の牌から「ツモ or ロンで和了できる牌」を抽出する
    foreach (var t in EnumerateAllTileIds34())
    {
        bool ok = false;

        try
        {
            if (CanTsumoWith(t, out _, out _, out _, out _)) ok = true;
        }
        catch { }

        if (!ok)
        {
            try
            {
                if (CanRonWith(t, out _, out _, out _, out _)) ok = true;
            }
            catch { }
        }

        if (ok) outWaits.Add(t);
    }
}

private int PlayerWaitTileSortKey(string id)
{
    // 既存の並び順ロジックが使えるならそれを流用（Man->Pin->Sou->字牌）
    // EnemyTileSortKey は "Man1" 等のIDで動く前提の実装が同ファイル内にあるため、それを使う
    try { return EnemyTileSortKey(id); } catch { }

    // フォールバック（万→筒→索→字、数字昇順）
    if (TryParseSuitNum(id, out int suit, out int num))
    {
        // suit: 0=Man 1=Pin 2=Sou 3=Honors
        int baseKey = suit * 100;
        return baseKey + num;
    }
    return 999999;
}
private void UpdatePlayerTenpaiWaitsUI()
{
    if (!playerTenpaiWaitsRoot) return;

    // スコア中などで表示したくないならここで制御可能（今回は「聴牌なら常に表示」を採用）
    if (!isTenpai)
    {
        playerTenpaiWaitsRoot.SetActive(false);

        // 表示物が残らないよう掃除
        if (playerTenpaiWaitSlots != null && playerTenpaiWaitSlots.Count > 0)
        {
            for (int i = 0; i < playerTenpaiWaitSlots.Count; i++)
            {
                var slot = playerTenpaiWaitSlots[i];
                if (!slot) continue;
                ClearChildren(slot);
            }
        }
        else
        {
            if (allowAutoLayoutWhenNoSlots)
                ClearChildren(playerTenpaiWaitsRoot.transform);
        }

        _lastPlayerTenpaiWaitsKey = "";
        return;
    }

    ComputePlayerTenpaiWaits(_tmpPlayerTenpaiWaits);

    var waits = new List<string>(_tmpPlayerTenpaiWaits);
    waits.Sort((a, b) => PlayerWaitTileSortKey(a).CompareTo(PlayerWaitTileSortKey(b)));
    string key = string.Join(",", waits);
    if (key == _lastPlayerTenpaiWaitsKey)
    {
        // 中身が同じでも、Inspectorでサイズを変えたときに反映できるように
        // 既存の表示物へサイズ再適用は行う
        playerTenpaiWaitsRoot.SetActive(waits.Count > 0);
        RefreshExistingPlayerTenpaiWaitTilesSize();
        return;
    }

    _lastPlayerTenpaiWaitsKey = key;

    playerTenpaiWaitsRoot.SetActive(waits.Count > 0);

    // スロット方式（Inspectorで位置を1つずつ決める）
    if (playerTenpaiWaitSlots != null && playerTenpaiWaitSlots.Count > 0)
    {
        for (int i = 0; i < playerTenpaiWaitSlots.Count; i++)
        {
            var slot = playerTenpaiWaitSlots[i];
            if (!slot) continue;

            ClearChildren(slot);

            if (i >= waits.Count) continue;

            var go = Instantiate(tilePrefab, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // クリック不要なので clickable=false
            SetupTile(go, waits[i], i, isHand:false, isOffer:false, clickable:false);

            // 待ち牌用サイズ上書き（Inspector設定）
            ApplyPlayerTenpaiWaitTileSizeIfNeeded(go);

            // 念のため強調やグレー解除
            try { SetTileHighlight(go.transform, false); } catch { }
            try { SetTileGrey(go.transform, false); } catch { }

        }
        return;
    }

    // スロット未設定の場合は、Root直下に並べる（Root側にHorizontalLayoutGroup等を付けて調整）
    if (allowAutoLayoutWhenNoSlots)
    {
        ClearChildren(playerTenpaiWaitsRoot.transform);

        for (int i = 0; i < waits.Count; i++)
        {
            var go = Instantiate(tilePrefab, playerTenpaiWaitsRoot.transform);
            SetupTile(go, waits[i], i, isHand:false, isOffer:false, clickable:false);

            // 待ち牌用サイズ上書き（Inspector設定）
            ApplyPlayerTenpaiWaitTileSizeIfNeeded(go);

            try { SetTileHighlight(go.transform, false); } catch { }
            try { SetTileGrey(go.transform, false); } catch { }

        }
    }
}

private bool CanPonWithBase(string baseId)
{
    // baseId と同じ“正規化牌”が手牌に2枚以上あるか
    int cnt = hand.Count(h => StripTileIdForLogic(h) == baseId);
    return cnt >= 2;
}

private bool CanKanWithBase(string baseId)
{
    // baseId と同じ“正規化牌”が手牌に3枚以上あるか
    int cnt = hand.Count(h => StripTileIdForLogic(h) == baseId);
    return cnt >= 3;
}

private bool CanChiWithBase(string baseId)
{
    // 数牌のみ。 baseId の前後で順子が作れるか（手牌側は正規化比較）
    if (!TryParseSuitNum(baseId, out int suit, out int num)) return false;
    if (suit == 3) return false; // 字牌

    bool Has(string id) => hand.Any(h => StripTileIdForLogic(h) == id);

    // n-2,n-1
    string id_m2 = suit == 0 ? $"Man{num - 2}" : suit == 1 ? $"Pin{num - 2}" : $"Sou{num - 2}";
    string id_m1 = suit == 0 ? $"Man{num - 1}" : suit == 1 ? $"Pin{num - 1}" : $"Sou{num - 1}";
    if (num >= 3 && Has(id_m2) && Has(id_m1)) return true;

    // n-1,n+1
    string id_p1 = suit == 0 ? $"Man{num + 1}" : suit == 1 ? $"Pin{num + 1}" : $"Sou{num + 1}";
    if (num >= 2 && num <= 8 && Has(id_m1) && Has(id_p1)) return true;

    // n+1,n+2
    string id_p2 = suit == 0 ? $"Man{num + 2}" : suit == 1 ? $"Pin{num + 2}" : $"Sou{num + 2}";
    if (num <= 7 && Has(id_p1) && Has(id_p2)) return true;

    return false;
}
private bool EvaluateWinInternal(string winningTile, bool isTsumo,
    out int fu, out int han, out List<string> yaku, out int score)
{
    fu = 0; han = 0; yaku = null; score = 0;
    if (string.IsNullOrEmpty(winningTile)) return false;

    // 14枚（面前）を組み立て
    var temp = new List<string>(hand);
    temp.Add(winningTile);

    string winTile = DetectWinTileFrom(temp);

    var concealed14 = temp;
    IList<IList<string>> openMelds = GetOpenMeldsNormalized(); // ★型を明示
    // アンカンのみの鳴きは面前継続とみなす
    bool isClosed   = IsClosedHand();

    // ★修正：局ごとに回る自風／場風を使う
    string seatWind  = GetPlayerSeatWind();  // "East"/"South"/"West"/"North"
    string roundWind = GetRoundWind();       // "East" 固定

var concealed14Logic = concealed14.Select(StripTileIdForLogic).ToList();
IList<IList<string>> openMeldsLogic = null;
if (openMelds != null)
{
    var tmp = new List<IList<string>>();
    foreach (var m in openMelds)
    {
        if (m == null) { tmp.Add(null); continue; }
        tmp.Add(m.Select(StripTileIdForLogic).ToList());
    }
    openMeldsLogic = tmp;
}
string winTileLogic = StripTileIdForLogic(winTile);

    // 役/符をまず評価（ここではドラはまだ加えない）
    var eval = YakuEvaluator.Evaluate(
        concealed14Logic, openMeldsLogic, winTileLogic,
        isTsumo: isTsumo, isClosed: isClosed,
        seatWind: seatWind, roundWind: roundWind);
    int baseHan = eval.han;   // ★ベース役（ドラ抜き）
    fu  = eval.fu;
    yaku = ParseYakuList(eval.breakdown);

    // ★修正：リーチ加算は「和了形（分解できる）」の場合だけ行う
    // YakuEvaluator は「和了形に分解できない」場合 breakdown="" を返す
    bool isAgariShape = !string.IsNullOrEmpty(eval.breakdown);
    // ★追加：天和／地和／人和（あなたの仕様）

if (isAgariShape && isClosed)
{
    bool isFirstTurn = (_playerTsumoCountThisRound == 1);

    bool canSpecialYakuman = isFirstTurn && !_playerDidAnkanOnFirstTurnThisHand;

    bool chihou = (canSpecialYakuman && isTsumo);   // 地和（プレイヤーのツモ）
    bool renhou = (canSpecialYakuman && !isTsumo);  // 人和（プレイヤーのロン）

    if (chihou || renhou)
    {
        if (yaku == null) yaku = new List<string>();

        // まず既存の天和/地和/人和表記を除去
        yaku = yaku.Where(x => !(x.Contains("天和") || x.Contains("地和") || x.Contains("人和"))).ToList();

        // 非・数え役満仕様：
        // 地和/人和が付く場合は、通常役は残さず、既存の役満だけ残す
        var yakumanOnly = yaku.Where(IsYakumanName).ToList();

        if (chihou) yakumanOnly.Insert(0, "地和(+13)");
        else        yakumanOnly.Insert(0, "人和(+13)");

        yaku = yakumanOnly;

        int yakumanCount = yaku.Count(x => IsYakumanName(x));
        if (yakumanCount <= 0) yakumanCount = 1;

        baseHan = 13 * yakumanCount;
        fu = 0;
    }
}
    // ★リーチ中なら、ロンでも「立直」を成立させる（ただし和了形のときだけ）
    // ★追加：ダブル立直／一発もここで加算する
    if (isRiichi && isClosed && baseHan < 13 && isAgariShape)
    {
        if (yaku == null) yaku = new List<string>();

        // 念のため、既存の立直/リーチ表示は一旦消して作り直す（重複防止）
        yaku = yaku.Where(x => !(x.Contains("立直") || x.Contains("リーチ"))).ToList();

        int riichiHan = _playerIsDoubleRiichi ? 2 : 1;
        string riichiLabel = _playerIsDoubleRiichi
            ? GetGameFixedText_Local("yaku.double_riichi")
            : GetGameFixedText_Local("yaku.riichi");
        yaku.Insert(0, riichiLabel);
        if (baseHan <= 0) baseHan = riichiHan;
        else baseHan += riichiHan;

        if (fu <= 0) fu = 30;

        bool ippatsu = false;
        if (_playerIppatsuEligible && _playerRiichiDeclaredTsumoCountThisRound >= 0)
        {
            ippatsu = (_playerTsumoCountThisRound == _playerRiichiDeclaredTsumoCountThisRound + 1);
        }
        if (ippatsu)
        {
            string ippatsuText = LocalizationManager.Fixed("yaku.ippatsu");
            bool hasIppatsuName = yaku.Any(x => x != null && x.Contains(ippatsuText));
            if (!hasIppatsuName)
            {
                yaku.Insert(1, ippatsuText);
                baseHan += 1;
            }
        }
    }


    if (baseHan <= 0)
    {
        han = 0; score = 0;
        return false;
    }

    bool playerIsDealer = false; // ★プレイヤーは常に子扱い

bool IsYakumanName(string s)
{
    if (string.IsNullOrEmpty(s)) return false;
    // 最低限：九蓮宝燈
    if (s.Contains("九蓮")) return true;

    // 代表的な役満（必要なら増やしてOK）
    if (s.Contains("国士")) return true;
    if (s.Contains("四暗刻")) return true;
    if (s.Contains("大三元")) return true;
    if (s.Contains("字一色")) return true;
    if (s.Contains("緑一色")) return true;
    if (s.Contains("清老頭")) return true;
    if (s.Contains("小四喜")) return true;
    if (s.Contains("大四喜")) return true;
    if (s.Contains("四カンツ")) return true;
    if (s.Contains("四槓子")) return true;
    if (s.Contains("天和")) return true;
    if (s.Contains("地和")) return true;
    if (s.Contains("人和")) return true;
    return false;
}
// YakuEvaluator の breakdown が「役満」表記のときは非・数え役満（国士/四暗刻/四槓子など）とみなす。
// 数え役満は通常「| 〇翻」表記になるので除外できる。
bool isNonKazoeYakumanByBreakdown =
    !string.IsNullOrEmpty(eval.breakdown) &&
    eval.breakdown.Contains("役満") &&
    !eval.breakdown.Contains("翻");

bool hasYakuman = (yaku != null && yaku.Any(IsYakumanName)) || isNonKazoeYakumanByBreakdown;

if (hasYakuman)
{
    // ★表示も計算も「役満のみ」
    if (yaku == null) yaku = new List<string>();

    var yakumanOnly = yaku.Where(IsYakumanName).ToList();
    int yakumanCount = yakumanOnly.Count;

    // breakdown 上は役満なのに yaku 抽出で拾えなかった場合の保険
    if (yakumanCount <= 0 && isNonKazoeYakumanByBreakdown)
    {
        yakumanCount = Math.Max(1, baseHan / 13);
    }

    if (yakumanCount <= 0) yakumanCount = 1;

    yaku = yakumanOnly;
    han = 13 * yakumanCount;
    fu = 0;

    var srYakuman = Scoring.TryScoreWin(fu, han, isTsumo: isTsumo, isDealer: playerIsDealer);

    score = srYakuman.totalPoints;
    return true;
}
// ---- 以降は得点計算用にドラを加算（表示も従来どおり） ----
int doraCount = CountDoraHits(concealed14, openMelds, doraIndicators);

// ★特別牌：1枚につきドラ+1（ノーマル含む）
//    ※後ろに _common / _rare / _epic / _legendary / _L1 などが付いてもカウントされる
int spBonus = 0;
try { spBonus = CountSpecialTileDoraBonusForScoring(concealed14, openMelds); } catch { spBonus = 0; }
if (spBonus > 0) doraCount += spBonus;

// ★変更：レア度による符ボーナスは廃止
// ★追加：レジェンダリー効果 L6（和了時 +16符）のみ符に反映
int legendaryFuBonus = 0;
try
{
    int cnt = CountLegendaryEffectTilesInScoringPool(concealed14, openMelds, 6);
    if (cnt > 0) legendaryFuBonus = 16 * cnt;
}
catch { legendaryFuBonus = 0; }

if (legendaryFuBonus > 0)
{
    fu = ApplySpecialFuBonusAndRoundUp(fu, legendaryFuBonus);
    if (yaku == null) yaku = new List<string>();
    yaku.Add($"レジェンダリー効果：符+{legendaryFuBonus}");
}

// ★追加：このスコアリング中の「役強化（Lv+1）」を抽選して保持（プレイヤー和了時のみ意味がある）
try
{
    BuildSpecialTileTraitBonusForThisScoring(concealed14, openMelds);
}
catch
{
    _specialTileTraitLvBonusThisScoring.Clear();
    _specialTileTraitLvBonusTotalThisScoring = 0;
}

han = baseHan + doraCount;
if (doraCount > 0)
{
    if (yaku == null) yaku = new List<string>();
    yaku.Add($"ドラ×{doraCount}");
}
if (spBonus > 0)
{
    if (yaku == null) yaku = new List<string>();
    yaku.Add($"赤ドラ×{spBonus}");
}

// ★修正：uraIndicators は RevealUraDoraIfEligible() で「表示牌」を積んでいるため、
//        表ドラと同じく NextDoraId で進めて CountDoraHits で数える。
if (_includeUraForScoring && uraIndicators != null && uraIndicators.Count > 0)
{
    int uraCount = CountDoraHits(concealed14, openMelds, uraIndicators);
    if (uraCount > 0)
    {
        han += uraCount;
        if (yaku == null) yaku = new List<string>();
        yaku.Add($"裏ドラ×{uraCount}");
    }
}

    // score
var sr = Scoring.TryScoreWin(fu, han, isTsumo: isTsumo, isDealer: playerIsDealer);
    score = sr.totalPoints;

    return true;
}

// ツモ和了用のラッパ
private bool CanTsumoWith(string winningTile, out int fu, out int han, out List<string> yaku, out int score)
{
    return EvaluateWinInternal(winningTile, true, out fu, out han, out yaku, out score);
}
private bool CanRonWith(string winningTile, out int fu, out int han, out List<string> yaku, out int score)
{
    return EvaluateWinInternal(winningTile, false, out fu, out han, out yaku, out score);
}
// 和了牌の特定（hand に対して 14枚側で +1 になっている牌）
private string DetectWinTileFrom(List<string> tiles14)
{
    var diff = new Dictionary<string, int>();
    foreach (var s in tiles14)
    {
        if (!diff.ContainsKey(s)) diff[s] = 0;
        diff[s]++;
    }
    foreach (var s in hand)
    {
        if (!diff.ContainsKey(s)) diff[s] = 0;
        diff[s]--;
    }
    foreach (var kv in diff)
        if (kv.Value > 0) return kv.Key;
    return tiles14.Count > 0 ? tiles14[tiles14.Count - 1] : null;
}
private static string StripTileIdForLogic(string id)
{
    if (string.IsNullOrEmpty(id)) return id;

    // 「*」は位置に関係なく除去
    id = StripStar(id);

    // ★重要："Man5_sp_xxx" / "Man5_xxx" 等をすべてベース牌へ正規化
    int us = id.IndexOf('_');
    if (us >= 0) id = id.Substring(0, us);

    return id;
}

private List<IList<string>> GetOpenMeldsNormalized()
{
    var list = new List<IList<string>>();
    if (melds == null) return list;

    string NormalizeKeepStar(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        bool hadStar = s.EndsWith("*");
        string core = hadStar ? s.Substring(0, s.Length - 1) : s;

// ★"Man5_sp_xxx" / "Man5_xxx" 等をすべてベース牌へ正規化（星は維持する）
int us = core.IndexOf('_');
if (us >= 0) core = core.Substring(0, us);


        return hadStar ? (core + "*") : core;
    }

    foreach (var m in melds)
    {
        if (m == null) continue;

        var clean = m
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(NormalizeKeepStar)
            .ToList();

        if (clean.Count >= 3) list.Add(clean);
    }

    return list;
}


// === Menzen / Open helpers ===
private bool HasAnyOpenMeld()
{
    if (melds == null) return false;
    // タイル末尾の '*' が1枚でも含まれるメンツがあれば「副露あり」
    return melds.Any(m => m != null && m.Any(x => !string.IsNullOrEmpty(x) && x.EndsWith("*")));
}

private bool IsClosedHand() => !HasAnyOpenMeld();

// breakdown 文字列から、画面表示用の「役名」だけのリストを抽出
private List<string> ParseYakuList(string breakdown)
{
    var result = new List<string>();
    if (string.IsNullOrEmpty(breakdown)) return result;
    // "立直(+1) + 平和(+1) + 一盃口(+1) | 3翻 30符" のような形式を想定
    var left = breakdown.Split('|')[0].Trim();
    if (string.IsNullOrEmpty(left)) return result;
    foreach (var part in left.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries))
    {
        var p = part.Trim();
        if (!string.IsNullOrEmpty(p)) result.Add(p);
    }
    return result;
}
private (int fu, int han, List<string> yaku) EvaluateYakuAndHan(List<string> tiles14, bool isTsumo)
{
    if (tiles14 == null) tiles14 = new List<string>();
    var sorted = new List<string>(tiles14);
    sorted.Sort((a, b) => ToSortKey(a).CompareTo(ToSortKey(b)));

    // ★役判定用：特別牌サフィックス(_sp)と「*」を落としたIDで渡す
    var sortedLogic = sorted.Select(StripTileIdForLogic).ToList();

    string winTile = DetectWinTileFrom(sorted);
    string winTileLogic = StripTileIdForLogic(winTile);

    // ★副露は「*」で明鳴きを判定するため、GetOpenMeldsNormalized は * を保持する必要がある
    IList<IList<string>> openMelds = GetOpenMeldsNormalized();

    // ★修正：門前判定は常に IsClosedHand() に統一（副露= '*' を確実に反映）
    bool isClosed = IsClosedHand();

    // ★ここを修正：局ごとに回る自風／場風を使う
    string seatWind = GetPlayerSeatWind();   // "East"/"South"/"West"/"North"
    string roundWind = GetRoundWind();       // 今は "East" 固定

    // ★役判定は logic のみで行う
    var ev = YakuEvaluator.Evaluate(sortedLogic, openMelds, winTileLogic,
        isTsumo: isTsumo, isClosed: isClosed, seatWind: seatWind, roundWind: roundWind);
var yakuList = ParseYakuList(ev.breakdown);
// ★敵和了では「役なし」を表示しない
if (yakuList != null) yakuList.RemoveAll(y => y.Contains("役なし"));
// ★追加：地和／人和（あなたの仕様）
// 地和：プレイヤーのみ。1ターン目ツモ和了で成立。ただし1ターン目に暗槓していたら不成立。
// 人和：プレイヤー/敵。1ターン目ロン和了で成立。ただし1ターン目に暗槓していたら不成立。
// ※役満扱い（+13）
{
    bool isFirstTurn = (_playerTsumoCountThisRound == 1);
    bool canSpecialYakuman = isFirstTurn && isClosed && !_playerDidAnkanOnFirstTurnThisHand;

    bool chihou = (canSpecialYakuman && isTsumo);
    bool renhou = (canSpecialYakuman && !isTsumo);

    if (chihou || renhou)
    {
        if (yakuList == null) yakuList = new List<string>();

        // 念のため重複防止
        yakuList.RemoveAll(x => x != null && (x.Contains("天和") || x.Contains("地和") || x.Contains("人和")));

        if (chihou) yakuList.Insert(0, "地和(+13)");
        else        yakuList.Insert(0, "人和(+13)");

        // ★既存役満があっても上にさらに重ねる
        ev.han += 13;
        ev.fu = 0;
    }
}

bool IsYakumanName(string s)
{
    if (string.IsNullOrEmpty(s))
        return false;

    string t = s.Trim();

    if (t.Length == 0)
        return false;

    t = t.Replace("　", " ");
    t = t.Replace('（', '(').Replace('）', ')');

    int p = t.IndexOf('(');
    if (p >= 0)
        t = t.Substring(0, p).Trim();

    bool MatchYakuman(string key, string jp)
    {
        if (!string.IsNullOrEmpty(jp) && t.Contains(jp))
            return true;

        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                string localized = lm.GetYakumanDisplayName(key);
                if (!string.IsNullOrEmpty(localized) &&
                    !string.Equals(localized, "yakuman." + key, StringComparison.Ordinal) &&
                    t.Contains(localized))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    if (MatchYakuman("CHUUREN_POUTOU", "九蓮宝燈")) return true;
    if (MatchYakuman("KOKUSHI", "国士無双")) return true;
    if (MatchYakuman("SUUANKOU", "四暗刻")) return true;
    if (MatchYakuman("DAISANGEN", "大三元")) return true;
    if (MatchYakuman("TSUUIISOU", "字一色")) return true;
    if (MatchYakuman("RYUUIISOU", "緑一色")) return true;
    if (MatchYakuman("CHINROUTOU", "清老頭")) return true;
    if (MatchYakuman("SHOUSUUSHI", "小四喜")) return true;
    if (MatchYakuman("DAISUUSHI", "大四喜")) return true;
    if (MatchYakuman("SUUKANTSU", "四カンツ")) return true;

    if (t.Contains("四槓子")) return true;
    if (MatchYakuman("TENHOU", "天和")) return true;
    if (MatchYakuman("CHIHOU", "地和")) return true;
    if (MatchYakuman("RENHOU", "人和")) return true;

    return false;
}
bool hasNonKazoeYakuman = (yakuList != null && yakuList.Any(IsYakumanName));

if (hasNonKazoeYakuman)
{
    // ★表示も役の構成も「役満のみ」
    yakuList = yakuList.Where(IsYakumanName).ToList();
}
if (!hasNonKazoeYakuman && isRiichi && isClosed && ev.han >= 0)
    {
        if (yakuList == null) yakuList = new List<string>();

        string riichiText = GetGameFixedText_Local("yaku.riichi");
        string doubleRiichiText = GetGameFixedText_Local("yaku.double_riichi");
        string ippatsuText = GetGameFixedText_Local("yaku.ippatsu");

        // 既存の立直表示（breakdown由来／過去の強制付与）を消して作り直す
        yakuList = yakuList.Where(x =>
            x != null &&
            !(
                x.Contains("立直") ||
                x.Contains("リーチ") ||
                x.Contains("ダブル立直") ||
                x.Contains("ダブルリーチ") ||
                x.Contains("Wリーチ") ||
                x.Contains(riichiText) ||
                x.Contains(doubleRiichiText) ||
                x.Contains(ippatsuText)
            )
        ).ToList();

        int riichiHan = _playerIsDoubleRiichi ? 2 : 1;
        string riichiLabel = _playerIsDoubleRiichi ? doubleRiichiText : riichiText;
        yakuList.Insert(0, riichiLabel);

        if (ev.han <= 0) ev.han = riichiHan;
        else ev.han += riichiHan;

        bool ippatsu = false;
        if (_playerIppatsuEligible && _playerRiichiDeclaredTsumoCountThisRound >= 0)
        {
            ippatsu = (_playerTsumoCountThisRound == _playerRiichiDeclaredTsumoCountThisRound + 1);
        }
        if (ippatsu)
        {
            bool hasIppatsuName = yakuList.Any(x => x != null && x.Contains(ippatsuText));
            if (!hasIppatsuName)
            {
                yakuList.Insert(1, ippatsuText);
                ev.han += 1;
            }
        }
    }
if (!hasNonKazoeYakuman)
{
    int normalDora = CountDoraHits(sorted, openMelds, doraIndicators);
    if (normalDora > 0)
    {
        ev.han += normalDora;
        if (yakuList == null) yakuList = new List<string>();

        yakuList.Add(BuildLocalizedCountYakuText("yaku.dora_count", normalDora));
    }

    // ★特別牌：1枚につきドラ+1（ノーマル含む）
    //    ※後ろに _common / _rare / _epic / _legendary / _L1 などが付いてもカウントされる
    int spBonus = 0;
    try { spBonus = CountSpecialTileDoraBonusForScoring(sorted, melds); } catch { spBonus = 0; }

    if (spBonus > 0)
    {
        ev.han += spBonus;
        if (yakuList == null) yakuList = new List<string>();
        yakuList.Add(BuildLocalizedCountYakuText("yaku.red_dora_count", spBonus));
    }

    if (_includeUraForScoring && uraIndicators != null && uraIndicators.Count > 0)
    {
        int uraCount = CountDoraHits(sorted, openMelds, uraIndicators);
        if (uraCount > 0)
        {
            ev.han += uraCount;
            if (yakuList == null) yakuList = new List<string>();
            yakuList.Add(BuildLocalizedCountYakuText("yaku.ura_dora_count", uraCount));
        }
    }
}

int legendaryFuBonus = 0;
try
{
    int cnt = CountLegendaryEffectTilesInScoringPool(sorted, melds, 6);
    if (cnt > 0) legendaryFuBonus = 16 * cnt;
}
catch { legendaryFuBonus = 0; }

if (legendaryFuBonus > 0)
{
    ev.fu = ApplySpecialFuBonusAndRoundUp(ev.fu, legendaryFuBonus);
    if (yakuList == null) yakuList = new List<string>();
    yakuList.Add(BuildLocalizedBonusYakuText("yaku.legendary_fu_bonus", legendaryFuBonus));
}

return (ev.fu, ev.han, yakuList);
}
private bool TryGetSelectedTsumoTile(out string tileId)
{
    tileId = null;
    if (phase != Phase.Offer) return false;

    // Offer: allow any selected offer tile that actually wins (not limited to exactly one)
    if (selOffer.Count >= 1)
    {
        foreach (var oi in selOffer)
        {
            if (oi >= 0 && oi < offers.Count)
            {
                string t = offers[oi];
                if (CanTsumoWith(t, out _, out _, out _, out _))
                {
                    tileId = t;

                    // ★重要：このターンの「offers内のどの位置でツモ和了したか」を記録（重複IDでもズレない）
                    _lastPlayerTsumoOfferIndex = oi;

                    return true;
                }
            }
        }
    }

    // Hand fallback: if scene places drawn tile directly in hand, allow selected winning hand tile
    if (hand.Count % 3 == 2 && selHand.Count >= 1)
    {
        foreach (var hi in selHand)
        {
            if (hi >= 0 && hi < hand.Count)
            {
                string t = hand[hi];
                if (CanTsumoWith(t, out _, out _, out _, out _))
                {
                    tileId = t;

                    // offers由来ではないので -1 に戻す
                    _lastPlayerTsumoOfferIndex = -1;

                    return true;
                }
            }
        }
    }

    return false;
}

private bool CanRonWithAny(System.Collections.Generic.IEnumerable<string> candidates, out string used, out int fu, out int han, out List<string> yaku, out int score)
{
    used = null; fu = 0; han = 0; yaku = null; score = 0;
    if (candidates == null) return false;

    foreach (var t in candidates)
    {
        // ★敵捨て牌のクリック判定と同じ正規化でロン判定する（"*" や "_" を確実に排除）
        var core = NormalizeEnemyDiscardForAction(t);
        core = StripTileIdForLogic(core);

        if (string.IsNullOrEmpty(core)) continue;

        if (CanRonWith(core, out fu, out han, out yaku, out score))
        {
            used = core;
            return true;
        }
    }

    return false;
}

private bool IsStandardWin(List<string> tiles14)
    {
        if (tiles14.Count % 3 != 2) return false;
        var counts = tiles14.GroupBy(x=>x).ToDictionary(g=>g.Key, g=>g.Count());
        foreach (var kv in counts.ToList())
        {
            if (kv.Value >= 2)
            {
                counts[kv.Key] -= 2;
                if (RemoveAllMelds(counts)) return true;
                counts[kv.Key] += 2;
            }
        }
        return false;
    }
    private bool RemoveAllMelds(Dictionary<string,int> counts)
    {
        if (counts.Values.All(v=>v==0)) return true;
        string first = counts.First(kv=>kv.Value>0).Key;
        if (counts[first] >= 3)
        {
            counts[first] -= 3;
            if (RemoveAllMelds(counts)) return true;
            counts[first] += 3;
        }
        if (TryParseSuitNum(first, out int s, out int n) && s != 3)
        {
            string suit = (s==0?"Man":s==1?"Pin":"Sou");
            string a = $"{suit}{n+1}";
            string b = $"{suit}{n+2}";
            int av=0, bv=0;
            counts.TryGetValue(a, out av);
            counts.TryGetValue(b, out bv);
            if (n<=7 && av>0 && bv>0)
            {
                counts[first]--; counts[a]--; counts[b]--;
                if (RemoveAllMelds(counts)) return true;
                counts[first]++; counts[a]++; counts[b]++;
            }
        }
        return false;
    }
// ===== Yaku evaluation (delegate to YakuEvaluator) =====

// ===== Yaku evaluation (delegate to YakuEvaluator) =====

private bool CanTsumoWithAny(System.Collections.Generic.IEnumerable<string> candidates, out string used, out int fu, out int han, out List<string> yaku, out int score)
    {
        used = null; fu=0; han=0; yaku=null; score=0;
        foreach (var t in candidates)
        {
            if (CanTsumoWith(t, out fu, out han, out yaku, out score)) { used = t; return true; }
        }

        return false;
    }
    private int CalcPoints(int fu, int han, bool dealer)
    {
        // Delegate to unified scoring engine (default: treat as Ron here).
        // Detailed Tsumo/Ron routing is handled at call sites for final display.
        var _sr = Scoring.TryScoreWin(fu, han, isTsumo: false, isDealer: dealer);
        return _sr.totalPoints;
    }

private void OnClickWin()
{
    // Ron ...
    if (phase == Phase.EnemyTurn)
    {
if (selectedEnemyIndex >= 0 && selectedEnemyIndex < enemyDiscards.Count)
{
    string usedRaw = enemyDiscards[selectedEnemyIndex];
    string used = NormalizeEnemyDiscardForAction(usedRaw); // ★ロジック用に正規化（*_sp 等を除去）

    string usedForScoringPool = StripStar(usedRaw);
    if (string.IsNullOrEmpty(usedForScoringPool))
        usedForScoringPool = usedRaw;
    if (string.IsNullOrEmpty(usedForScoringPool))
        usedForScoringPool = used;

    if (CanRonWith(used, out var fuR, out var hanR, out var yakuR, out var _scoreR))
    {
        // ★追加：ロン成立時点で「敵のどの捨て牌で和了したか」を確実に保存
        _lastPlayerRonEnemyDiscardIndex = selectedEnemyIndex;
        _lastPlayerRonEnemyDiscardTileLogic = used;

        var temp = new List<string>(hand);
        temp.Add(usedForScoringPool);
        temp.Sort((a,b)=>ToSortKey(a).CompareTo(ToSortKey(b)));

        // ★ 立直していれば裏ドラをめくる→最終計算のみ裏ドラを数える
// ---- レジェンダリー専用効果（プレイヤー和了時のみ）----
// この和了の都度リセット
_legendaryGoldDoubleThisScoring = false;
_legendaryTraitDoubleIfUnderManganThisScoring = false;

// L1〜L5 の有無を、和了牌プール（面前14+副露）で判定
int l1 = 0, l2 = 0, l3 = 0, l4 = 0, l5 = 0;
try
{
    // temp は面前14枚（hand + usedForScoringPool）で、melds は副露
    l1 = CountLegendaryEffectTilesInScoringPool(temp, melds, 1);
    l2 = CountLegendaryEffectTilesInScoringPool(temp, melds, 2);
    l3 = CountLegendaryEffectTilesInScoringPool(temp, melds, 3);
    l4 = CountLegendaryEffectTilesInScoringPool(temp, melds, 4);
    l5 = CountLegendaryEffectTilesInScoringPool(temp, melds, 5);
}
catch { l1 = l2 = l3 = l4 = l5 = 0; }

// ① 表ドラ・裏ドラを追加で1枚ずつ（プレイヤー和了のみ）
//    表ドラ追加は AddKanIndicator と同じ流れ（表＋対応する裏保留も1枚確保される）
if (l1 > 0)
{
    AddKanIndicator();
}
// ② 直後の敵和了ダメージ半減（同じ敵の間だけ、1回消費）
if (l2 > 0)
{
    _legendaryDamageHalfPending = true;
    _legendaryDamageHalfEnemyKey = GetCurrentEnemyKey_ForLegendary();

    _legendaryDamageHalfReservedSourceTiles.Clear();
    _legendaryDamageHalfReservedSourceTiles.AddRange(
        __CollectLegendaryEffectTileIdsInScoringPool(temp, melds, 2)
    );
}

// ③ 獲得GOLD2倍（ShowScoring内のゴールド計算で参照される）
if (l3 > 0)
{
    _legendaryGoldDoubleThisScoring = true;
}

// ④ 満貫未満なら撃/瞬/癒が2倍（ShowScoring内で参照される）
if (l4 > 0)
{
    _legendaryTraitDoubleIfUnderManganThisScoring = true;
}
// ⑤ 次局のMP消費半分（同じ敵の間だけ / 次の局だけ）
if (l5 > 0)
{
    _legendaryHalfMpCostPending = true;
    _legendaryHalfMpCostEnemyKey = GetCurrentEnemyKey_ForLegendary();
    _legendaryHalfMpCostTargetRound = roundNumber + 1;

    _legendaryHalfMpCostReservedSourceTiles.Clear();
    _legendaryHalfMpCostReservedSourceTiles.AddRange(
        __CollectLegendaryEffectTileIdsInScoringPool(temp, melds, 5)
    );
}
// ★追加：継続効果の表示を更新
RefreshLegendaryOngoingEffectsTextUI();

// ★裏ドラを開く条件：立直 または レジェンダリー①
_includeUraForScoring = (isRiichi || (l1 > 0));
if (_includeUraForScoring)
{
    RevealUraDoraIfEligible();
}

        var eval = EvaluateYakuAndHan(temp, false);

var _srRon = Scoring.TryScoreWin(eval.fu, eval.han, isTsumo: false, isDealer: isDealer);
_currentScoringAttackerIsPlayer = true;   // 追加：プレイヤーが和了

__PrepareAppliedSpecialTileUiCacheForPlayerScoring(temp, melds, eval.han, eval.fu, eval.yaku);

ShowScoring(
    "ロン",
    used,
    eval.fu,
    eval.han,
    eval.yaku,
    _srRon.totalPoints
);
_includeUraForScoring = false; // ★後始末
return;

    }
}

        if (statusTMP) statusTMP.text = "ロン可能な牌をクリックして選択してください";
        return;
    }
    // Tsumo ...
    if (phase == Phase.Offer)
    {
        if (suppressTsumoThisOffer) { if (statusTMP) statusTMP.text = "このターンはツモを見送りました"; return; }
        if (TryGetSelectedTsumoTile(out var usedT))
        {
            var temp = new List<string>(hand);
            temp.Add(usedT);
            temp.Sort((a,b)=>ToSortKey(a).CompareTo(ToSortKey(b)));

            // ★ ツモでも立直中なら裏ドラ
// ---- レジェンダリー専用効果（プレイヤー和了時のみ）----
// この和了の都度リセット
_legendaryGoldDoubleThisScoring = false;
_legendaryTraitDoubleIfUnderManganThisScoring = false;

// L1〜L5 の有無を、和了牌プール（面前14+副露）で判定
int l1 = 0, l2 = 0, l3 = 0, l4 = 0, l5 = 0;
try
{
    // temp は面前14枚（hand + usedT）で、melds は副露
    l1 = CountLegendaryEffectTilesInScoringPool(temp, melds, 1);
    l2 = CountLegendaryEffectTilesInScoringPool(temp, melds, 2);
    l3 = CountLegendaryEffectTilesInScoringPool(temp, melds, 3);
    l4 = CountLegendaryEffectTilesInScoringPool(temp, melds, 4);
    l5 = CountLegendaryEffectTilesInScoringPool(temp, melds, 5);
}
catch { l1 = l2 = l3 = l4 = l5 = 0; }

// ① 表ドラ・裏ドラを追加で1枚ずつ（プレイヤー和了のみ）
if (l1 > 0)
{
    AddKanIndicator();
}

// ② 直後の敵和了ダメージ半減（同じ敵の間だけ、1回消費）
if (l2 > 0)
{
    _legendaryDamageHalfPending = true;
    _legendaryDamageHalfEnemyKey = GetCurrentEnemyKey_ForLegendary();

    _legendaryDamageHalfReservedSourceTiles.Clear();
    _legendaryDamageHalfReservedSourceTiles.AddRange(
        __CollectLegendaryEffectTileIdsInScoringPool(temp, melds, 2)
    );
}

// ③ 獲得GOLD2倍（ShowScoring内のゴールド計算で参照される）
if (l3 > 0)
{
    _legendaryGoldDoubleThisScoring = true;
}

// ④ 満貫未満なら撃/瞬/癒が2倍（ShowScoring内で参照される）
if (l4 > 0)
{
    _legendaryTraitDoubleIfUnderManganThisScoring = true;
}
// ⑤ 次局のMP消費半分（同じ敵の間だけ / 次の局だけ）
if (l5 > 0)
{
    _legendaryHalfMpCostPending = true;
    _legendaryHalfMpCostEnemyKey = GetCurrentEnemyKey_ForLegendary();
    _legendaryHalfMpCostTargetRound = roundNumber + 1;

    _legendaryHalfMpCostReservedSourceTiles.Clear();
    _legendaryHalfMpCostReservedSourceTiles.AddRange(
        __CollectLegendaryEffectTileIdsInScoringPool(temp, melds, 5)
    );
}
// ★追加：継続効果の表示を更新
RefreshLegendaryOngoingEffectsTextUI();

// ★裏ドラを開く条件：立直 または レジェンダリー①
_includeUraForScoring = (isRiichi || (l1 > 0));
if (_includeUraForScoring)
{
    RevealUraDoraIfEligible();
}


            var eval = EvaluateYakuAndHan(temp, true);
            // ★保険：立直中は役「立直」を重複なく強制付与（+1飜も確実に加算）
var _srTsumo = Scoring.TryScoreWin(eval.fu, eval.han, isTsumo: true, isDealer: isDealer);
_currentScoringAttackerIsPlayer = true;   // 追加：プレイヤーが和了

__PrepareAppliedSpecialTileUiCacheForPlayerScoring(temp, melds, eval.han, eval.fu, eval.yaku);

ShowScoring(
    "ツモ",
    usedT,
    eval.fu,
    eval.han,
    eval.yaku,
    _srTsumo.totalPoints
);
_includeUraForScoring = false; // ★後始末
return;

        }
if (statusTMP) statusTMP.text = GetGameFixedText_Local("select_tsumo_tile");
return;
    }
}
private void ShowRyukyoku()
{
    // ★この局が流局であることを記録（次局開始時の敵手牌リセットに使用）
    _lastHandWasRyukyoku = true;

    // ★変更：点数計算パネルではなく「流局」カットインを表示し、その後自動で次局等へ進む
    StartCoroutine(__RyukyokuCutinAndNextHand_Co());
}
private System.Collections.IEnumerator __RyukyokuCutinAndNextHand_Co()
{
    // スコアリング相当のフェーズとして扱い、ボタン類を無効化
    phase = Phase.Scoring;

    // まずUIは現状値で同期
    UpdateHpUI();

    // ★ノーテン罰：テンパイしていない側は 1000 ダメージ（演出付き）
    const int notenPenalty = 1000;

    bool playerTenpai = true;
    bool enemyTenpai = true;

    // プレイヤー（既に和了済みなら対象外）
    if (!_playerHasWonThisHand)
    {
        try { playerTenpai = IsTenpai(hand); } catch { playerTenpai = false; }
    }

    // 敵（既に和了済みなら対象外）
    // 敵のテンパイ判定は「テンパイフラグ or リーチ中」をテンパイ扱いにする
    if (!_enemyHasWonThisHand)
    {
        try { enemyTenpai = _enemyIsInTenpai || _enemyIsRiichi; } catch { enemyTenpai = false; }
    }

    // ★追加：敵がリーチしていてテンパイ扱いなら、流局演出前に手牌をオープンする
    if (!_enemyHasWonThisHand && enemyTenpai && _enemyIsRiichi)
    {
        try { EnemyRevealHandNow(); } catch { }
    }

    bool applyPlayerPenalty = (!_playerHasWonThisHand) && !playerTenpai;
    bool applyEnemyPenalty  = (!_enemyHasWonThisHand)  && !enemyTenpai;

if (!applyPlayerPenalty && !applyEnemyPenalty)
{
    yield return __ShowTextCutin_Co(GetGameFixedText_Local("ryukyoku"), ryukyokuSESource);
        __ProceedAfterRyukyoku();
        yield break;
    }

    // ★ノーテン側の手牌を「全部裏返し」にする（裏面Spriteへ）
    if (applyPlayerPenalty)
    {
        __SetAllTilesToBack(handArea);
    }
    if (applyEnemyPenalty)
    {
        __SetAllTilesToBack(enemyHandArea);
    }

    // ★ノーテン罰ダメージ演出（和了ダメージと同様のゲージ減少演出）
    yield return StartCoroutine(__Ryukyoku_ApplyNotenPenaltyDamage_ThenContinue_Co(
        applyPlayerPenalty ? notenPenalty : 0,
        applyEnemyPenalty  ? notenPenalty : 0
    ));
    // ★ダメージ演出が終わったら流局カットインへ
    yield return __ShowTextCutin_Co(GetGameFixedText_Local("ryukyoku"), ryukyokuSESource);

    // ★重要：流局は OnClickScoreOK() に流さず、必ず次局開始へ進める
    __ProceedAfterRyukyoku();
}
private void __SetAllTilesToBack(RectTransform area)
{
    if (!area) return;

    for (int i = 0; i < area.childCount; i++)
    {
        var child = area.GetChild(i);
        if (!child) continue;
        TrySetBackSprite(child.gameObject);
    }
}
private static string GetScoringDoraLabel_Local(bool isUra)
{
    return GetGameFixedText_Local(isUra ? "ura_dora_label" : "omote_dora_label");
}
private System.Collections.IEnumerator __Ryukyoku_ApplyNotenPenaltyDamage_ThenContinue_Co(int playerPenalty, int enemyPenalty)
{
    int startPlayerHP = Mathf.Max(0, playerHP);
    int startEnemyHP  = Mathf.Max(0, enemyHP);

    int dmgToPlayer = Mathf.Max(0, playerPenalty);
    int dmgToEnemy  = Mathf.Max(0, enemyPenalty);

    int endPlayerHP = Mathf.Max(0, startPlayerHP - dmgToPlayer);
    int endEnemyHP  = Mathf.Max(0, startEnemyHP  - dmgToEnemy);

try
{
    if (dmgToEnemy > 0 && AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayBattleDamageSE();
    }
}
catch { }
    if (dmgToPlayer > 0 && enemyWinDamageSESource != null && enemySkillDamageSEClip != null)
    {
        try { enemyWinDamageSESource.PlayOneShot(enemySkillDamageSEClip); } catch {}
    }

    float durEnemyHp  = (dmgToEnemy  > 0) ? Mathf.Max(0f, playerWinDamageAnimSeconds) : 0f;
    float durPlayerHp = (dmgToPlayer > 0) ? Mathf.Max(0f, enemyWinDamageAnimSeconds) : 0f;
    float dur = Mathf.Max(durEnemyHp, durPlayerHp);

    // 0秒以下なら即時反映
    if (dur <= 0f)
    {
        playerHP = endPlayerHP;
        enemyHP  = endEnemyHP;
        UpdateHpUI();
        yield break;
    }

    float t = 0f;
    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        if (dmgToPlayer > 0)
        {
            int dispPlayerHP = Mathf.RoundToInt(Mathf.Lerp(startPlayerHP, endPlayerHP, p));
            __UpdatePlayerHpUI_VisualOnly(dispPlayerHP);
        }

        if (dmgToEnemy > 0)
        {
            int dispEnemyHP = Mathf.RoundToInt(Mathf.Lerp(startEnemyHP, endEnemyHP, p));
            __UpdateEnemyHpUI_VisualOnly(dispEnemyHP);
        }

        yield return null;
    }

    // 最終値を確定
    playerHP = endPlayerHP;
    enemyHP  = endEnemyHP;

    UpdateHpUI();
}
private bool _legendaryTraitDoubleIfUnderManganThisScoring = false;

private static bool IsManganOrAbove(int han, int fu)
{
    // 役満以上
    if (han >= 13) return true;

    // 満貫条件の代表（一般的な判定）
    if (han >= 5) return true;
    if (han == 4 && fu >= 40) return true;
    if (han == 3 && fu >= 70) return true;

    return false;
}

private static string GetYakumanTierName_Local(int yakumanCount)
{
    yakumanCount = Mathf.Max(1, yakumanCount);

    switch (yakumanCount)
    {
        case 1: return GetGameFixedText_Local("limit_yakuman");
        case 2: return GetGameFixedText_Local("limit_double_yakuman");
        case 3: return GetGameFixedText_Local("limit_triple_yakuman");
        case 4: return GetGameFixedText_Local("limit_quadruple_yakuman");
        case 5: return GetGameFixedText_Local("limit_quintuple_yakuman");
        default:
            return FormatGameFixedText_Local("limit_multi_yakuman_format", yakumanCount);
    }
}

private static string GetScoreTierName_Local(int han, int fu)
{
    if (han >= 13)
    {
        int yakumanCount = Mathf.Max(1, han / 13);
        return GetYakumanTierName_Local(yakumanCount);
    }

    if (han >= 11) return GetGameFixedText_Local("limit_sanbaiman");
    if (han >= 8)  return GetGameFixedText_Local("limit_baiman");
    if (han >= 6)  return GetGameFixedText_Local("limit_haneman");

    if (IsManganOrAbove(han, fu))
        return GetGameFixedText_Local("limit_mangan");

    return "";
}

private static string BuildScoringHanFuText_Local(int han, int fu)
{
    if (han <= 0 && fu <= 0)
        return "";

    string tierName = GetScoreTierName_Local(han, fu);
    if (!string.IsNullOrEmpty(tierName))
        return FormatGameFixedText_Local("han_limit_format", han, tierName);

    if (han > 0 && fu > 0)
        return FormatGameFixedText_Local("han_fu_format", han, fu);

    if (han > 0)
        return FormatGameFixedText_Local("han_only_format", han);

    return "";
}
private void ShowScoring(
    string winKind,
    string usedTile,
    int fu,
    int han,
    List<string> yakuNames,
    int totalPoints)
{
        RefreshScoringDoraUI(); // ★スコア表示直前に必ずドラUIを再構築（裏ドラ含む）
    // ★ 追加: お札条件評価用の文脈をセット
    try
    {
        _lastScoringYaku    = (yakuNames != null) ? new List<string>(yakuNames) : new List<string>();
        _lastScoringIsTsumo = string.Equals(winKind, "ツモ", StringComparison.OrdinalIgnoreCase);
    }
    catch {}

    try
    {
        if (_currentScoringAttackerIsPlayer)
        {
            var achievementYaku = new List<string>();

            if (yakuNames != null)
            {
                for (int i = 0; i < yakuNames.Count; i++)
                {
                    string raw = yakuNames[i];
                    if (string.IsNullOrEmpty(raw)) continue;

                    string canonical = NormalizeTraitJudgeYakuName_Local(raw);
                    if (string.IsNullOrEmpty(canonical))
                        canonical = raw;

                    achievementYaku.Add(canonical);
                }
            }

            AchievementSystem.NotifyPlayerWin(achievementYaku, 0);
        }
    }
    catch {}

RefreshScoringDoraUI(); // ★スコアパネル表示直前にドラ欄（表＋裏）を強制更新
try
{
    if (_currentScoringAttackerIsPlayer)
    {
        _lastPlayerWinHan = han;
        _lastPlayerWinFu = fu;
        _lastPlayerWinWasYakumanOrKazoe = (han >= 13);

        _lastEnemyWinHan = 0;
        _lastEnemyWinFu = 0;
        _lastEnemyWinWasYakumanOrKazoe = false;
    }
    else
    {
        _lastEnemyWinHan = han;
        _lastEnemyWinFu = fu;
        _lastEnemyWinWasYakumanOrKazoe = (han >= 13);

        _lastPlayerWinHan = 0;
        _lastPlayerWinFu = 0;
        _lastPlayerWinWasYakumanOrKazoe = false;
    }
}
catch
{
    _lastPlayerWinHan = 0;
    _lastPlayerWinFu = 0;
    _lastPlayerWinWasYakumanOrKazoe = false;

    _lastEnemyWinHan = 0;
    _lastEnemyWinFu = 0;
    _lastEnemyWinWasYakumanOrKazoe = false;
}
    // 1) 役リストから「撃/瞬/癒 の該当があるか」を判定
    bool hasGeki = HasTraitForAnyYaku(yakuNames, SkillSetAsset.Trait.Geki);

    bool hasShun = HasTraitForAnyYaku(yakuNames, SkillSetAsset.Trait.Shun);
    bool hasIyu  = HasTraitForAnyYaku(yakuNames, SkillSetAsset.Trait.Iyu);
    // 2) ベース係数（未連携環境でも破綻しないデフォルトを用意）
    float baseGekiMul = 1.2f;   // 従来：単一の x1.2 等
    float baseShunRate = 0.10f; // 従来：単一の 0.10 等
    float baseIyuRate  = 0.00f;

    try
    {
        if (_skillSet)
        {
            var tp = _skillSet.GetType();
            var m = tp.GetMethod("GetTraitCoeffs", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if (m != null)
            {
                object[] args = new object[] { 0f, 0f, 0f };
                m.Invoke(_skillSet, args);
                baseGekiMul = Mathf.Max(0.01f, Convert.ToSingle(args[0]));
                baseShunRate = Mathf.Max(0f,    Convert.ToSingle(args[1]));
                baseIyuRate  = Mathf.Max(0f,    Convert.ToSingle(args[2]));
            }
        }
    }
    catch { }

float totalGekiPct = SumTraitPctByYaku(yakuNames, SkillSetAsset.Trait.Geki, (baseGekiMul - 1f));
float totalShunPct = SumTraitPctByYaku(yakuNames, SkillSetAsset.Trait.Shun, baseShunRate);
float totalIyuPct  = SumTraitPctByYaku(yakuNames, SkillSetAsset.Trait.Iyu,  baseIyuRate);

// “今回出ていないトレイト”は0扱い
if (!hasGeki) totalGekiPct = 0f;
if (!hasShun) totalShunPct = 0f;
if (!hasIyu ) totalIyuPct  = 0f;

// ★レジェンダリー④：満貫未満の和了なら 撃/瞬/癒 の効果が2倍
//    ※判定は「和了の han/fu（ドラ込みの表示値）」で行う
if (_legendaryTraitDoubleIfUnderManganThisScoring)
{
    if (!IsManganOrAbove(han, fu))
    {
        totalGekiPct *= 2f;
        totalShunPct *= 2f;
        totalIyuPct  *= 2f;
    }
}
// ★お守りの「撃/瞬/癒 効果X%上昇」は、“効果(率)そのもの”にだけ乗算する
//   例：撃40% かつ お守り撃+4% → 0.40 * 1.04 = 0.416（=41.6%）
//   ※発動していないトレイト（hasGeki=false 等）は、上で 0 扱いにされているので影響なし
if (_currentScoringAttackerIsPlayer)
{
    try
    {
        // 念のため最新のお守りキャッシュを参照（既に更新されているなら実質NOP）
        RefreshOmamoriCache();

        if (hasGeki && _om.gekiDmgUp > 0f) totalGekiPct *= (1f + Mathf.Max(0f, _om.gekiDmgUp));
        if (hasShun && _om.shunAddUp  > 0f) totalShunPct *= (1f + Mathf.Max(0f, _om.shunAddUp));
        if (hasIyu  && _om.iyuHealUp  > 0f) totalIyuPct  *= (1f + Mathf.Max(0f, _om.iyuHealUp));
    }
    catch { }
}
// 最終係数へ反映（★お守り反映後の率を使う）
float geKi = Mathf.Max(0f, damageMultiplier) * (1f + Mathf.Max(0f, totalGekiPct));
float shun = Mathf.Max(0f, totalShunPct);
float iyu  = Mathf.Max(0f, totalIyuPct);

    // 4) 表示用「基礎点」
int basePointForDisplay = totalPoints;

int ofudaBasePointForCondition = 0;
try
{
    var srForOfuda = Scoring.TryScoreWin(
        fu,
        han,
        isTsumo: string.Equals(winKind, "ツモ", StringComparison.OrdinalIgnoreCase),
        isDealer: isDealer
    );
    ofudaBasePointForCondition = Mathf.Max(0, srForOfuda.basePoint);
}
catch
{
    ofudaBasePointForCondition = Mathf.Max(0, totalPoints);
}

_lastScoringBasePoints = ofudaBasePointForCondition;

    // 5) 回復量（ダメージ×率）
    int mpRecovered = Mathf.RoundToInt(Mathf.Max(0, totalPoints) * shun);
    int hpRecovered = Mathf.RoundToInt(Mathf.Max(0, totalPoints) * iyu);

// --- お札のスコア補正（撃/瞬/癒とは独立）---
float ofudaMult = 1f;
int   ofudaExtra = 0;
try
{
    ApplyRunOfudaModifiers(yakuNames, ref ofudaMult, ref ofudaExtra, null);
}
catch {}

// ★お札を反映してから最終ダメージを決定
int   baseWithOfuda = Mathf.Max(0, basePointForDisplay + ofudaExtra);
float totalMult     = Mathf.Max(0f, geKi) * Mathf.Max(0f, ofudaMult);
int   finalDamage   = Mathf.RoundToInt(baseWithOfuda * totalMult);

// ★仕様変更：プレイヤーのツモ和了はダメージ25%減、ロン和了は100%
if (string.Equals(winKind, "ツモ", StringComparison.OrdinalIgnoreCase))
{
    finalDamage = Mathf.Max(1, Mathf.RoundToInt(finalDamage * 0.75f));
}

// （任意）スコアパネル用の説明行を付けたい場合は lines を用意しているならそこに加える。
// ここでは yakuNames に混ぜない方針なので、ShowScoring(10引数版)の直前で
// 例：lines?.Add($"お札：加点+{ofudaExtra} / 倍率x{ofudaMult:0.###}");


// ここでは yakuNames に混ぜない方針なので、ShowScoring(10引数版)の直前で
// 例：lines?.Add($"お札：加点+{ofudaExtra} / 倍率x{ofudaMult:0.###}");

// ★修正：いきなりスコアパネルを出さず、カットイン→スコア表示のコルーチンへ
StartCoroutine(__WinCutInThenShowScoring(
    winKind,          /* 「ロン」 or 「ツモ」 */
    true,             /* isPlayer: プレイヤーの和了なので true */
    finalDamage,      /* 最終ダメージ（=点） */
    basePointForDisplay,
    geKi, shun, iyu,  /* 撃/瞬/癒（倍率／回復率） */
    mpRecovered,      /* 瞬の回復量 */
    hpRecovered,      /* 癒の回復量 */
    yakuNames,        /* 役名一覧 */
    null,             /* used: 旧インターフェイス用。未使用なので null でOK */
    fu, han,
    winKind,
    usedTile          /* 画面表示用の和了牌ラベル */
));

}

// --- 旧来の数合わせ用：瞬/癒が無い呼び出しに対応（Shun/Iyuは0扱い） ---
private void ShowScoring(
    int totalPoints,
    int han,
    int fu,
    List<string> yakuNames,
    int basePointForDisplay,
    float traitGekiMultiplier,
    int mpRecovered,
    int hpRecovered)
{
    ShowScoring(totalPoints, han, fu, yakuNames,
        basePointForDisplay,
        traitGekiMultiplier,
        0f,   // traitShunRate（未指定は0）
        0f,   // traitIyuRate（未指定は0）
        mpRecovered,
        hpRecovered);
}
private void ShowScoring(
    int totalPoints,
    int han,
    int fu,
    List<string> yakuNames,
    int basePointForDisplay,
    float traitGekiMultiplier,
    float traitShunRate,
    float traitIyuRate,
    int mpRecovered,
    int hpRecovered)
{
    // ★追加：この呼び出しは流局ではなく「誰かの和了」である
    _lastHandWasRyukyoku = false;

    // 安全ガード（手動UIを使わない場合のみ必須）
    bool useManual = __UseManualScoringUI();
    if (!useManual && (!scoringTMP || !scoringPanel)) return;

    RefreshScoringDoraUI(); // ★最終表示の直前に必ずドラ欄（表＋裏）を再構築
    // 表示用整形

// 表示用整形（敵のときは内部確定結果を必ず使う。役なしは表示しない）
string yakuLine = "";
if (!_currentScoringAttackerIsPlayer && !string.IsNullOrEmpty(EnemyAddon_LastYakuText))
{
    yakuLine = __LocalizeSpecialYakumanToken_Local((EnemyAddon_LastYakuText ?? "").Replace("役:", "役："));
}
else if (yakuNames != null && yakuNames.Count > 0)
{
    var localizedYakuNames = yakuNames.Select(__LocalizeSpecialYakumanToken_Local).ToList();
    yakuLine = "役： " + string.Join("　", localizedYakuNames);
}
string hanfu = "";
if (!_currentScoringAttackerIsPlayer)
{
    hanfu = BuildScoringHanFuText_Local(EnemyAddon_LastHan, EnemyAddon_LastFu);
}
else
{
    hanfu = BuildScoringHanFuText_Local(han, fu);
}
var sb = new System.Text.StringBuilder(256);
if (!string.IsNullOrEmpty(yakuLine)) sb.AppendLine(yakuLine);
if (!string.IsNullOrEmpty(hanfu)) sb.AppendLine(hanfu);

// ★仕様変更：ツモ和了ダメージ減少時のラベルを基礎点に表示（フォールバック）（プレイヤー25%減／敵50%減）
{
    string _tsumoFbBase = "";
    int _fbDisplayBasePt = Mathf.Max(0, basePointForDisplay);
    bool _isTsumoFb = _currentScoringAttackerIsPlayer ? _lastScoringIsTsumo : _enemyLastWinWasTsumo;
    if (_isTsumoFb)
    {
        float fbTsumoRate = _currentScoringAttackerIsPlayer ? 0.75f : 0.5f;
        string fbPctStr = _currentScoringAttackerIsPlayer ? "25" : "50";
        _fbDisplayBasePt = Mathf.Max(1, Mathf.RoundToInt(_fbDisplayBasePt * fbTsumoRate));
        var _lmFb2 = LocalizationManager.Instance;
        var _langFb2 = (_lmFb2 != null) ? _lmFb2.CurrentLanguage : LocalizationManager.Language.Japanese;
        switch (_langFb2)
        {
            case LocalizationManager.Language.English:           _tsumoFbBase = $" (Tsumo -{fbPctStr}%)"; break;
            case LocalizationManager.Language.ChineseSimplified: _tsumoFbBase = $"（自摸和　减{fbPctStr}%）"; break;
            default:                                             _tsumoFbBase = $"（ツモあがり　{fbPctStr}％減）"; break;
        }
    }
    sb.AppendLine($"{GetGameFixedText_Local("base_point_label")}　{_fbDisplayBasePt}{_tsumoFbBase}");
}
int finalDamageForApply = Mathf.Max(0, totalPoints);

// ★追加：レジェンダリー「直後の敵和了ダメージ半減」を“表示の最終ダメージ”にも反映する
// 実ダメージは別経路で半減されているが、UI表示が totalPoints のままになっていたためここで補正する
if (!_currentScoringAttackerIsPlayer)
{
    finalDamageForApply = PreviewLegendaryDamageHalfOnEnemyWin(finalDamageForApply);
}

int traitMpHeal = Mathf.Max(0, mpRecovered);
int traitHpHeal = Mathf.Max(0, hpRecovered);

int ofudaHpHealAbs = 0;
int ofudaMpHealAbs = 0;
if (_currentScoringAttackerIsPlayer)
{
    // ★追加：敵スキル（防御など）が「プレイヤー和了の最終ダメージ」に影響する場合はここで反映
    // （従来どおり：EnemySkills_ModifyDamageBeforeApply があればそれを適用）
    try
    {
        int dmgBeforeSkill = finalDamageForApply;
        EnemySkills_ModifyDamageBeforeApply(ref finalDamageForApply, ref traitHpHeal, ref traitMpHeal);
        if (dmgBeforeSkill != finalDamageForApply)
        {
            sb.AppendLine($"敵スキルで最終ダメージ変化　{dmgBeforeSkill:#,0} → {finalDamageForApply:#,0}");
        }
    }
    catch { }
// ★ユニーク：アヌビス（東1局の敵へのダメージ50%上昇）／ゼウス（敵へのダメージ30%上昇）
try
{
    if (roundNumber == 1 && PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50))
    {
        finalDamageForApply = Mathf.RoundToInt(finalDamageForApply * 1.5f);
    }
    if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Zeus_DamageUp30))
    {
        finalDamageForApply = Mathf.RoundToInt(finalDamageForApply * 1.3f);
    }
}
catch { }

    // お札の回復は「別変数」に保持（個別欄は％表示、合計欄は数値表示に回す）
    try
    {
        int dummyDamage = finalDamageForApply;
        ApplyRunOfuda_PostScoring(ref dummyDamage, ref ofudaHpHealAbs, ref ofudaMpHealAbs, null);

        // ★重要：現状のお札回復が「最大HP/最大MP基準」になっているため、
        //         いったん “何%だったか” に戻して、基礎点(basePointForDisplay)基準に変換する。
        int maxHP = Mathf.Max(1, playerMaxHP);
        int maxMP = Mathf.Max(1, EffectiveMaxMP());

        float ofudaHpRate = (maxHP > 0) ? ((float)ofudaHpHealAbs / (float)maxHP) : 0f;
        float ofudaMpRate = (maxMP > 0) ? ((float)ofudaMpHealAbs / (float)maxMP) : 0f;

        ofudaHpHealAbs = Mathf.RoundToInt(Mathf.Max(0, basePointForDisplay) * Mathf.Max(0f, ofudaHpRate));
        ofudaMpHealAbs = Mathf.RoundToInt(Mathf.Max(0, basePointForDisplay) * Mathf.Max(0f, ofudaMpRate));
    }
    catch { ofudaHpHealAbs = 0; ofudaMpHealAbs = 0; }
}

int finalMpHeal = Mathf.Max(0, traitMpHeal + ofudaMpHealAbs);
int finalHpHeal = Mathf.Max(0, traitHpHeal + ofudaHpHealAbs);

// 表示
sb.AppendLine($"撃：x{Mathf.Max(0f, traitGekiMultiplier):0.###}");
sb.AppendLine($"ダメージ　{finalDamageForApply}");
sb.AppendLine($"瞬：x{Mathf.Max(0f, traitShunRate):0.###}");
sb.AppendLine($"MP回復　{finalMpHeal}");
sb.AppendLine($"癒：x{Mathf.Max(0f, traitIyuRate):0.###}");
sb.AppendLine($"HP回復　{finalHpHeal}");

// ★追加: Ofuda summary（プレイヤー和了時のみ表示）
if (_currentScoringAttackerIsPlayer)
{
    try
    {
        float ofudaMultOnly = 1f;
        int   ofudaExtraIgn = 0;
        ApplyRunOfudaModifiers(yakuNames, ref ofudaMultOnly, ref ofudaExtraIgn, null);

        int tmpDmg = 0; // 使わない
        int ofudaHpOnly = 0;
        int ofudaMpOnly = 0;
        ApplyRunOfuda_PostScoring(ref tmpDmg, ref ofudaHpOnly, ref ofudaMpOnly, null);

        int maxHP = Mathf.Max(1, playerMaxHP);
        int maxMP = Mathf.Max(1, ReflectGetInt("playerMaxMP", 0)); // 無い環境は0→%は0扱い
        int hpPct = (maxHP > 0) ? Mathf.RoundToInt(ofudaHpOnly * 100f / maxHP) : 0;
        int mpPct = (maxMP > 0) ? Mathf.RoundToInt(ofudaMpOnly * 100f / maxMP) : 0;

        sb.AppendLine($"お札の効果　点数倍率　{ofudaMultOnly:0.0}倍");
        sb.AppendLine($"　　　　　　HP回復　{hpPct}％");
        sb.AppendLine($"　　　　　　MP回復　{mpPct}％");
    }
    catch { /* 表示だけなので、例外は握りつぶす */ }
}

if (finalDamageForApply > 0)
{
    if (_currentScoringAttackerIsPlayer)
    {
        // プレイヤー和了 → 敵が被ダメ（HP反映はスコアOK後に演出付きで行う）
        _pendingPlayerWinDamageToEnemy = (finalDamageForApply > 0);
        _pendingPlayerWinDamageBase = Mathf.Max(0, finalDamageForApply);
        _pendingPlayerWinDamageFinal = Mathf.Max(0, finalDamageForApply);
    }
    else
    {
        // 敵和了：点数計算パネルの表示は「実際に適用される最終ダメージ」を表示する
        // まずは既に確定している _pendingEnemyWinDamageFinal を優先
        if (_pendingEnemyWinDamage && _pendingEnemyWinDamageFinal > 0)
        {
            finalDamageForApply = Mathf.Max(0, _pendingEnemyWinDamageFinal);
        }
        else
        {
            finalDamageForApply = Mathf.Max(0, finalDamageForApply);
        }

        // レジェンダリー②（直後の敵和了ダメージ半減）が有効な場合、表示値だけ半減後にする（ここでは消費しない）
        finalDamageForApply = PreviewLegendaryDamageHalfOnEnemyWin(finalDamageForApply);

// 敵側ではダメージ適用はしない（スコアOK直後に演出付きで適用する）
// ここは UI 表示のための値だけ整える
traitGekiMultiplier = 1f;
traitShunRate       = 0f;
traitIyuRate        = 0f;
finalMpHeal         = 0;
finalHpHeal         = 0;
// ★HPやPlayerPrefs（Run_PlayerHP）は触らない★

    }


    // ランスコアはプレイヤー和了時のみ加算する
    if (_currentScoringAttackerIsPlayer)
    {
        AddScore(finalDamageForApply);
        scoreThisEnemy += finalDamageForApply;
    }
    UpdateHpUI();
}
// MP回復（瞬/お札）
// ★仕様変更：プレイヤー和了によるMP回復は、和了した瞬間に反映せず、
//            点数計算パネルを閉じたタイミング（敵HP減少/HP回復演出と同じタイミング）で
//            ゲージがグーっと回復する演出を行ってから確定させる。
if (_currentScoringAttackerIsPlayer)
{
    _pendingPlayerWinMpHeal = (finalMpHeal > 0);
    _pendingPlayerWinMpHealAbs = Mathf.Max(0, finalMpHeal);
}
else
{
    _pendingPlayerWinMpHeal = false;
    _pendingPlayerWinMpHealAbs = 0;
}
// HP回復（癒）
// ★仕様変更：プレイヤー和了によるHP回復は、和了した瞬間に反映せず、
//            点数計算パネルを閉じたタイミング（敵HP減少演出と同じタイミング）で
//            ゲージがグーっと回復する演出を行ってから確定させる。
if (_currentScoringAttackerIsPlayer)
{
    _pendingPlayerWinHpHeal = (finalHpHeal > 0);
    _pendingPlayerWinHpHealAbs = Mathf.Max(0, finalHpHeal);
}
else
{
    _pendingPlayerWinHpHeal = false;
    _pendingPlayerWinHpHealAbs = 0;
}
// ★ここで GOLD 計算＆表示追記（和了ごと）
_goldGainThisWin = 0;
_goldGainDisplayTextThisWin = "-";

// まず「この和了で敵に与えた最終ダメージ」をベースにGOLDを算出する
if (_currentScoringAttackerIsPlayer && finalDamageForApply > 0)
{
    float r = UnityEngine.Random.Range(0.01f, 0.0400001f); // [0.05, 0.08]
    int goldBase = finalDamageForApply;

    int goldGainBase = Mathf.RoundToInt(goldBase * r);
    if (goldGainBase < 1) goldGainBase = 1;

    int goldGain = goldGainBase;
    var mulTexts = new List<string>();

    bool capitalistSkillActive = false;
    try
    {
        capitalistSkillActive = IsCapitalistEquippedForHadesRelic_Local();
    }
    catch
    {
        capitalistSkillActive = false;
    }

    // 資産家のパッシブ：和了によるGold取得量2倍
    if (capitalistSkillActive)
    {
        goldGain = Mathf.Max(1, Mathf.RoundToInt(goldGain * 2f));
        mulTexts.Add("2");
    }

    // 既存のユニークお守り分岐は残す
    bool hadesCapitalistRelicActive = false;
    try
    {
        hadesCapitalistRelicActive =
            HasEquippedUniqueOmamori_RuntimeSafe(PlayerData.UniqueOmamoriEffectKind.Hades_Capitalist) &&
            capitalistSkillActive;
    }
    catch
    {
        hadesCapitalistRelicActive = false;
    }

    if (hadesCapitalistRelicActive)
    {
        goldGain = Mathf.Max(1, Mathf.RoundToInt(goldGain * 2f));
        mulTexts.Add("2");
    }

    // ★レジェンダリー③：その和了による獲得GOLDが2倍
    if (_legendaryGoldDoubleThisScoring)
    {
        goldGain *= 2;
        mulTexts.Add("2");
    }

    _goldGainThisWin = goldGain;

    if (mulTexts.Count > 0)
        _goldGainDisplayTextThisWin = $"{goldGainBase:#,0}×{string.Join("×", mulTexts)}＝{goldGain:#,0}";
    else
        _goldGainDisplayTextThisWin = $"+{goldGain:#,0}";

    runGold += goldGain;
    SaveRunGold();

    sb.AppendLine();
    sb.AppendLine($"GOLD 獲得　{_goldGainDisplayTextThisWin}　（この和了のダメージ {goldBase:N0} × {r:0.###}）");
    sb.AppendLine($"所持 GOLD　{runGold:N0}");
}
// ここから下は「敵を倒したかどうか」の判定だけに使う（GOLD付与には使わない）
int enemyHpAfterScoring = enemyHP;
if (_currentScoringAttackerIsPlayer && _pendingPlayerWinDamageToEnemy && _pendingPlayerWinDamageFinal > 0)
{
    enemyHpAfterScoring = Mathf.Max(0, enemyHP - _pendingPlayerWinDamageFinal);
}

if (Mathf.Max(0, enemyHpAfterScoring) <= 0)
{
    scoreThisEnemy = 0; // 次の敵に備えてリセット
}

// --- [ADD] 敵和了時だけ：お守りの被ダメ軽減を必ず表示（0%も表示） ---
// 敵和了時だけ：お守りの“実効”軽減率を表示（0%でも必ず表示）
if (!_currentScoringAttackerIsPlayer)
{
    int baseDmg = Mathf.Max(0, basePointForDisplay);
    int applied = Mathf.Max(0, finalDamageForApply); // 既に適用済みの実ダメージ
    int pct = (baseDmg > 0)
        ? Mathf.Clamp(Mathf.RoundToInt((1f - (float)applied / baseDmg) * 100f), 0, 100)
        : 0;
    sb.AppendLine($"被ダメ軽減（お守り）　{pct}%（{baseDmg:#,0} → {applied:#,0}）");
}

    // 反映
// 反映（手動UIが紐付いている場合を優先）
if (__UseManualScoringUI())
{
    // 役（yakuLine は "役： ..." 形式なのでラベルを剥がす）
    string rolesOnly = string.IsNullOrEmpty(yakuLine) ? "" : yakuLine.Replace("役：", "").Replace("役:", "").Trim();

// お札の要約（プレイヤー和了のみ）
float ofudaMultOnly = 1f; int ofudaHpOnly = 0; int ofudaMpOnly = 0;
if (_currentScoringAttackerIsPlayer)
{
    try
    {
        // 乗算だけ拾う（現行踏襲）
        float dmgMul = 1f; int add = 0;
        ApplyRunOfudaModifiers(yakuNames, ref dmgMul, ref add, null);
        ofudaMultOnly = Mathf.Max(0f, dmgMul);

        // ★OfudaEvalContext を使わず、既存と同じ “ref 引数＋null ctx” で素の回復量を取得
        int tmpDmg = 0;
        int ofudaHpRaw = 0, ofudaMpRaw = 0;
        ApplyRunOfuda_PostScoring(ref tmpDmg, ref ofudaHpRaw, ref ofudaMpRaw, null);

// ★手動UIは％表示なので、ここで％を算出して渡す
int maxHP = Mathf.Max(1, playerMaxHP);
int maxMP = Mathf.Max(1, EffectiveMaxMP());
int hpPct = (maxHP > 0) ? Mathf.RoundToInt(ofudaHpRaw * 100f / maxHP) : 0;
int mpPct = (maxMP > 0) ? Mathf.RoundToInt(ofudaMpRaw * 100f / maxMP) : 0;

        ofudaHpOnly = hpPct;
        ofudaMpOnly = mpPct;
    }
    catch {}
}


    // 敵和了時の「お守り軽減％」は、既存の計算結果を使う（sb 構築部と同じロジック）
    int omamoriPct = -1;
    if (!_currentScoringAttackerIsPlayer)
    {
        try
        {
            // 既存の基礎点→軽減後ダメージの計算と同条件でパーセンテージを作成
            int baseDmg = Mathf.Max(0, basePointForDisplay);
            int applied = Mathf.Max(0, totalPoints);
            int save    = Mathf.Max(0, baseDmg - applied);
            omamoriPct  = (baseDmg > 0) ? Mathf.RoundToInt(save * 100f / baseDmg) : 0;
        }
        catch { omamoriPct = -1; }
    }
    // ★ここ：scoringTMP が無い環境でも落ちないようにする
    if (scoringTMP)
    {
        scoringTMP.text = ""; // 従来のまとめテキストは空に（重複表示防止）
    }

    __ApplyScoringManualUI(
        rolesOnly, hanfu, basePointForDisplay,
        traitGekiMultiplier, traitShunRate, traitIyuRate,
        finalDamageForApply, finalHpHeal, finalMpHeal,
        ofudaMultOnly, ofudaHpOnly, ofudaMpOnly,
        omamoriPct,
        _currentScoringAttackerIsPlayer
    );
}
else
{
    // フォールバック：従来どおりまとめテキストを表示
    if (scoringTMP)
    {
        scoringTMP.text = sb.ToString();
    }
}
if (_currentScoringAttackerIsPlayer)
{
    __SetTMP(scoringGoldGainValue, (_goldGainThisWin > 0) ? _goldGainDisplayTextThisWin : "-");
}
else
{
    __SetTMP(scoringGoldGainValue, "");
}
// === 特別牌効果（箇条書き＋該当牌） ===
if (_currentScoringAttackerIsPlayer)
{
    __ApplyAppliedSpecialTileUiCacheToScoringPanel(true);

    if (scoringSpecialTileEffectsRoot_Enemy) scoringSpecialTileEffectsRoot_Enemy.SetActive(false);
    __SetTMP(scoringSpecialTileEffectsTMP_Enemy, "");

    if (scoringSpecialTileEffectTilesRoot_Enemy != null)
    {
        for (int i = scoringSpecialTileEffectTilesRoot_Enemy.childCount - 1; i >= 0; i--)
            Destroy(scoringSpecialTileEffectTilesRoot_Enemy.GetChild(i).gameObject);
    }
}
else
{
    if (scoringSpecialTileEffectsRoot_Player) scoringSpecialTileEffectsRoot_Player.SetActive(false);
    if (scoringSpecialTileEffectsRoot_Enemy) scoringSpecialTileEffectsRoot_Enemy.SetActive(false);
    __SetTMP(scoringSpecialTileEffectsTMP_Player, "");
    __SetTMP(scoringSpecialTileEffectsTMP_Enemy, "");

    if (scoringSpecialTileEffectTilesRoot_Player != null)
    {
        for (int i = scoringSpecialTileEffectTilesRoot_Player.childCount - 1; i >= 0; i--)
            Destroy(scoringSpecialTileEffectTilesRoot_Player.GetChild(i).gameObject);
    }

    if (scoringSpecialTileEffectTilesRoot_Enemy != null)
    {
        for (int i = scoringSpecialTileEffectTilesRoot_Enemy.childCount - 1; i >= 0; i--)
            Destroy(scoringSpecialTileEffectTilesRoot_Enemy.GetChild(i).gameObject);
    }

    __ClearAppliedSpecialTileUiCache();
}
// ★ここ：scoringPanel を使っていない（手動UIのみ）場合でも落ちないようにする
if (scoringPanel)
{
    scoringPanel.SetActive(true);
}

if (_currentScoringAttackerIsPlayer)
{
    RebuildPlayerScoringTilesManualPreview();
}
else
{
    EnemyAddon_PopulateEnemyScoringTilesManual();
}

ForceRebuildScoringLayouts();
    RefreshScoringDoraUI();   // ← 追加：右上に「表/裏ドラ」を描画
    phase = Phase.Scoring;

    // 次へ（OK）ボタンのリスナーを付け直す
    WireScoringOK();

    // ★追加：段階表示の演出（必要なときだけ）
    __StartScoringStepReveal(_currentScoringAttackerIsPlayer);
}
private bool _legendaryGoldDoubleThisScoring = false;
private bool _legendaryDamageHalfTriggeredThisScoring = false;
private bool _legendaryHalfMpCostTriggeredThisScoring = false;

private readonly List<string> _legendaryDamageHalfReservedSourceTiles = new List<string>();
private readonly List<string> _legendaryHalfMpCostReservedSourceTiles = new List<string>();
private readonly List<string> _legendaryDamageHalfTriggeredSourceTiles = new List<string>();
private readonly List<string> _legendaryHalfMpCostTriggeredSourceTiles = new List<string>();
private readonly List<string> _appliedSpecialTileUiLinesThisScoring = new List<string>();
private readonly List<string> _appliedSpecialTileUiTilesThisScoring = new List<string>();
// 現在のアクティブスキルに対して、撃/瞬/癒の「該当役」一覧を取得する。
// 右側のスキル説明パネルで使っているロジックと同等のものを、
// ダメージ計算用に切り出したもの。
private (List<string> ge, List<string> sh, List<string> iy, SkillSetAsset hostSet)
GetCurrentSkillTraitYakuForScoring()
{
    var ge = new List<string>();
    var sh = new List<string>();
    var iy = new List<string>();
    SkillSetAsset hostSet = null;

    try
    {
        // 実際に発動しているスキル（メニュー保存を最優先）
        var active    = ResolveActiveSkillForMP();
        var skillName = active.ToString();

        if (string.IsNullOrEmpty(skillName))
            return (ge, sh, iy, _skillSet);

        // 1) まず現在の _skillSet が、このスキルを持っているならそれを優先
        if (_skillSet != null && _skillSet.activeSkills != null &&
            _skillSet.activeSkills.Any(e =>
                e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase)))
        {
            hostSet = _skillSet;
        }

        // 2) 見つからなければ、Resources/SkillSets から所属 SkillSet を総当たり検索
        if (hostSet == null)
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            foreach (var s in allSets)
            {
                if (s == null || s.activeSkills == null) continue;

                var entry = s.activeSkills.FirstOrDefault(e =>
                    e != null &&
                    !string.IsNullOrEmpty(e.activeSkillName) &&
                    string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    hostSet = s;
                    break;
                }
            }
        }

        if (hostSet != null)
        {
            // ★変更：スキルの「該当役（全候補）」を取る
            //        （未解放は Lv=0 のままなので、計算側で弾く）
            var yakuTuple = hostSet.GetTraitYakuFor(skillName);

            if (yakuTuple.ge != null)
                ge = yakuTuple.ge
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();

            if (yakuTuple.sh != null)
                sh = yakuTuple.sh
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();

            if (yakuTuple.iy != null)
                iy = yakuTuple.iy
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
        }

        // 3) 最後の保険：hostSet が見つからない場合は、従来どおり _skillSet.traitMap 全件
        else if (_skillSet != null)
        {
            if (_skillSet.traitMap != null && _skillSet.traitMap.Count > 0)
            {
                ge = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Geki).Select(t => t.yakuName).ToList();
                sh = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Shun).Select(t => t.yakuName).ToList();
                iy = _skillSet.traitMap.Where(t => t != null && t.trait == SkillSetAsset.Trait.Iyu).Select(t => t.yakuName).ToList();
            }

            hostSet = _skillSet;
        }

    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[TraitScoring] GetCurrentSkillTraitYakuForScoring failed: {ex.Message}");
    }

    return (ge, sh, iy, hostSet);
}
private void __ClearAppliedSpecialTileUiCache()
{
    _appliedSpecialTileUiLinesThisScoring.Clear();
    _appliedSpecialTileUiTilesThisScoring.Clear();
}

private List<string> __CollectSpecialTileBaseIdsInScoringPool(List<string> concealed14Raw, List<List<string>> openMeldsRaw)
{
    var list = new List<string>();

    void AddOne(string raw)
    {
        if (!IsSpecialTileId(raw)) return;

        string baseId = StripTileIdForLogic(raw);
        if (string.IsNullOrEmpty(baseId)) return;

        list.Add(baseId);
    }

    if (concealed14Raw != null)
    {
        for (int i = 0; i < concealed14Raw.Count; i++)
            AddOne(concealed14Raw[i]);
    }

    if (openMeldsRaw != null)
    {
        for (int m = 0; m < openMeldsRaw.Count; m++)
        {
            var meld = openMeldsRaw[m];
            if (meld == null) continue;

            for (int i = 0; i < meld.Count; i++)
                AddOne(meld[i]);
        }
    }

    return list;
}

private List<string> __CollectLegendaryEffectTileIdsInScoringPool(List<string> concealed14Raw, List<List<string>> openMeldsRaw, int effectIndex)
{
    var list = new List<string>();

    void AddOne(string raw)
    {
        if (!TryGetLegendaryEffectIndex(raw, out int idx)) return;
        if (idx != effectIndex) return;

        string baseId = StripTileIdForLogic(raw);
        if (string.IsNullOrEmpty(baseId)) return;

        list.Add(baseId);
    }

    if (concealed14Raw != null)
    {
        for (int i = 0; i < concealed14Raw.Count; i++)
            AddOne(concealed14Raw[i]);
    }

    if (openMeldsRaw != null)
    {
        for (int m = 0; m < openMeldsRaw.Count; m++)
        {
            var meld = openMeldsRaw[m];
            if (meld == null) continue;

            for (int i = 0; i < meld.Count; i++)
                AddOne(meld[i]);
        }
    }

    return list;
}

private void __AddAppliedSpecialTileUiLine(string line, IEnumerable<string> tileIds)
{
    if (string.IsNullOrEmpty(line)) return;

    _appliedSpecialTileUiLinesThisScoring.Add(line);

    if (tileIds == null) return;

    foreach (var id in tileIds)
    {
        if (string.IsNullOrEmpty(id)) continue;
        _appliedSpecialTileUiTilesThisScoring.Add(id);
    }
}

private void __PrepareAppliedSpecialTileUiCacheForPlayerScoring(
    List<string> concealed14Raw,
    List<List<string>> openMeldsRaw,
    int han,
    int fu,
    List<string> yakuNames)
{
    __ClearAppliedSpecialTileUiCache();

    var fx1Tiles = __CollectLegendaryEffectTileIdsInScoringPool(concealed14Raw, openMeldsRaw, 1);
    if (fx1Tiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx1"),
            fx1Tiles);
    }

    var fx2Tiles = __CollectLegendaryEffectTileIdsInScoringPool(concealed14Raw, openMeldsRaw, 2);
    if (fx2Tiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx2"),
            fx2Tiles);
    }

    var fx3Tiles = __CollectLegendaryEffectTileIdsInScoringPool(concealed14Raw, openMeldsRaw, 3);
    if (fx3Tiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx3"),
            fx3Tiles);
    }

    var fx4Tiles = __CollectLegendaryEffectTileIdsInScoringPool(concealed14Raw, openMeldsRaw, 4);
    if (fx4Tiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx4"),
            fx4Tiles);
    }

    var fx5Tiles = __CollectLegendaryEffectTileIdsInScoringPool(concealed14Raw, openMeldsRaw, 5);
    if (fx5Tiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx5"),
            fx5Tiles);
    }

    int fx6Count = 0;
    try
    {
        fx6Count = CountLegendaryEffectTilesInScoringPool(concealed14Raw, openMeldsRaw, 6);
    }
    catch
    {
        fx6Count = 0;
    }

    var fx6Tiles = __CollectLegendaryEffectTileIdsInScoringPool(concealed14Raw, openMeldsRaw, 6);
    if (fx6Count > 0 && fx6Tiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx6", fx6Count),
            fx6Tiles);
    }

    if (_legendaryHalfMpCostTriggeredThisScoring && _legendaryHalfMpCostTriggeredSourceTiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            BuildSpecialTileLegendaryScoringLine_Local("fx5_triggered"),
            _legendaryHalfMpCostTriggeredSourceTiles);
    }
}
private void __ApplyAppliedSpecialTileUiCacheToScoringPanel(bool isPlayer)
{
    var root = isPlayer ? scoringSpecialTileEffectsRoot_Player : scoringSpecialTileEffectsRoot_Enemy;
    var tmp = isPlayer ? scoringSpecialTileEffectsTMP_Player : scoringSpecialTileEffectsTMP_Enemy;
    var tileRoot = isPlayer ? scoringSpecialTileEffectTilesRoot_Player : scoringSpecialTileEffectTilesRoot_Enemy;

    bool hasAny = _appliedSpecialTileUiLinesThisScoring.Count > 0;

    if (root) root.SetActive(hasAny);
    __SetTMP(tmp, hasAny ? string.Join("\n", _appliedSpecialTileUiLinesThisScoring) : "");

    if (tileRoot == null) return;

    for (int i = tileRoot.childCount - 1; i >= 0; i--)
        Destroy(tileRoot.GetChild(i).gameObject);

    if (!hasAny) return;

    float x = 0f;
    int targetWidth = GetScoringTileWidthSafe(tileRoot);

    var hlg = tileRoot.GetComponent<HorizontalLayoutGroup>();
    if (hlg != null && hlg.enabled)
    {
        hlg.spacing = 0f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
    }

    for (int i = 0; i < _appliedSpecialTileUiTilesThisScoring.Count; i++)
    {
        var id = _appliedSpecialTileUiTilesThisScoring[i];
        if (string.IsNullOrEmpty(id)) continue;

        CreateTileImage(tileRoot, id, ref x, targetWidth);
    }
}
private void EnsureTraitUnlocksAtBattleStart()
{
    try
    {
        var active = ResolveActiveSkillForMP();
        var skillName = active.ToString();
        if (string.IsNullOrEmpty(skillName)) return;

        SkillSetAsset hostSet = null;

        // 1) まず現在の _skillSet が、このスキルを持っているならそれを優先
        if (_skillSet != null && _skillSet.activeSkills != null &&
            _skillSet.activeSkills.Any(e =>
                e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase)))
        {
            hostSet = _skillSet;
        }

        // 2) 見つからなければ、Resources/SkillSets から所属 SkillSet を総当たり検索
        if (hostSet == null)
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            foreach (var s in allSets)
            {
                if (s == null || s.activeSkills == null) continue;

                var entry = s.activeSkills.FirstOrDefault(e =>
                    e != null &&
                    !string.IsNullOrEmpty(e.activeSkillName) &&
                    string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    hostSet = s;
                    break;
                }
            }
        }

        // 初期解放（撃/瞬/癒 各1つ）
        if (hostSet != null)
        {
            hostSet.EnsureInitialTraitUnlocks(skillName);
        }
    }
    catch { }
}
float SumTraitPctByYaku(List<string> yakuNames, SkillSetAsset.Trait trait, float fallbackPctPerHit)
{
    if (yakuNames == null || yakuNames.Count == 0)
        return 0f;

    var (geList, shList, iyList, hostSet) = GetCurrentSkillTraitYakuForScoring();

    List<string> keys = null;
    switch (trait)
    {
        case SkillSetAsset.Trait.Geki: keys = geList; break;
        case SkillSetAsset.Trait.Shun: keys = shList; break;
        case SkillSetAsset.Trait.Iyu:  keys = iyList; break;
        default: return 0f;
    }

    if (keys == null || keys.Count == 0)
        return 0f;

    var hitSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < yakuNames.Count; i++)
    {
        string norm = NormalizeTraitJudgeYakuName_Local(yakuNames[i]);
        if (!string.IsNullOrEmpty(norm))
            hitSet.Add(norm);
    }

    string activeSkillName = "";
    try
    {
        var active = ResolveActiveSkillForMP();
        activeSkillName = active.ToString();
    }
    catch
    {
        activeSkillName = "";
    }

    float deltaPerLevel = 0f;
    try
    {
        deltaPerLevel = Mathf.Max(0f, GetTraitUpgradeDeltaFromPrefs(trait, hostSet));
    }
    catch
    {
        deltaPerLevel = 0f;
    }

    float total = 0f;
    var countedTraitNormSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var keyRaw in keys)
    {
        string key = (keyRaw ?? "").Trim();
        if (string.IsNullOrEmpty(key))
            continue;

        string traitNorm = NormalizeTraitJudgeYakuName_Local(key);
        if (string.IsNullOrEmpty(traitNorm))
            continue;

        if (!hitSet.Contains(traitNorm))
            continue;

        if (countedTraitNormSet.Contains(traitNorm))
            continue;

        int effectiveLv = GetTraitEffectiveLevelForScoring(hostSet, activeSkillName, trait, key);
        if (effectiveLv <= 0)
            continue;

        float add = Mathf.Max(0f, fallbackPctPerHit);

        if (hostSet != null && hostSet.traitMap != null)
        {
            var entry = hostSet.traitMap.FirstOrDefault(t =>
                t != null &&
                t.trait == trait &&
                !string.IsNullOrWhiteSpace(t.yakuName) &&
                string.Equals(
                    NormalizeTraitJudgeYakuName_Local(t.yakuName),
                    traitNorm,
                    StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                switch (trait)
                {
                    case SkillSetAsset.Trait.Geki:
                    {
                        int di = Mathf.Clamp((int)entry.difficulty, 0, hostSet.gekiMultiplierByDiff.Length - 1);
                        add = Mathf.Max(0f, hostSet.gekiMultiplierByDiff[di] - 1f);
                        break;
                    }

                    case SkillSetAsset.Trait.Shun:
                    {
                        int di = Mathf.Clamp((int)entry.difficulty, 0, hostSet.shunMpPctByDiff.Length - 1);
                        add = Mathf.Max(0f, hostSet.shunMpPctByDiff[di]);
                        break;
                    }

                    case SkillSetAsset.Trait.Iyu:
                    {
                        int di = Mathf.Clamp((int)entry.difficulty, 0, hostSet.iyuHealMulByDiff.Length - 1);
                        add = Mathf.Max(0f, hostSet.iyuHealMulByDiff[di]);
                        break;
                    }
                }
            }
        }

        if (deltaPerLevel > 0f)
        {
            int deltaLv = Mathf.Max(0, effectiveLv - 1);
            add += deltaPerLevel * deltaLv;
        }

        total += Mathf.Max(0f, add);
        countedTraitNormSet.Add(traitNorm);
    }

    return Mathf.Max(0f, total);
}
private void BuildSpecialTileTraitBonusForThisScoring_AllYakuRoll(IList<string> concealed14Raw, IList<IList<string>> openMeldsRaw)
{
    _specialTileTraitLvBonusThisScoring.Clear();
    _specialTileTraitLvBonusTotalThisScoring = 0;

    List<SpecialTileSystem.Entry> equipped = null;

    try
    {
        equipped = SpecialTileSystem.GetEquipped();
    }
    catch
    {
        equipped = null;
    }

    if (equipped == null || equipped.Count <= 0)
        return;

    for (int i = 0; i < equipped.Count; i++)
    {
        AddSpecialTileTraitBonusPacked_Local(equipped[i].traitBonusPacked);
    }

    foreach (var kv in _specialTileTraitLvBonusThisScoring)
    {
        if (kv.Value > 0)
            _specialTileTraitLvBonusTotalThisScoring += kv.Value;
    }
}
private float GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait trait, SkillSetAsset hostSet)
{
    // hostSet側にデフォルト値を持たせているならそれを fallback にできる
    float fallback = 0.05f;
    if (trait == SkillSetAsset.Trait.Iyu) fallback = 0.02f;

    // PlayerPrefs 優先キー（UpgradeManager で保存）
    string key = "PF_TraitUpgradeDelta_Geki";
    if (trait == SkillSetAsset.Trait.Shun) key = "PF_TraitUpgradeDelta_Shun";
    if (trait == SkillSetAsset.Trait.Iyu)  key = "PF_TraitUpgradeDelta_Iyu";

    try
    {
        var s = PlayerPrefs.GetString(key, "");
        if (!string.IsNullOrEmpty(s) && float.TryParse(s, out float v))
            return Mathf.Max(0f, v);
    }
    catch { }

    return Mathf.Max(0f, fallback);
}
private bool HasTraitForAnyYaku(List<string> yakuNames, SkillSetAsset.Trait trait)
{
    if (yakuNames == null || yakuNames.Count == 0)
        return false;

    try
    {
        var (geList, shList, iyList, hostSet) = GetCurrentSkillTraitYakuForScoring();

        List<string> keys = null;
        switch (trait)
        {
            case SkillSetAsset.Trait.Geki: keys = geList; break;
            case SkillSetAsset.Trait.Shun: keys = shList; break;
            case SkillSetAsset.Trait.Iyu:  keys = iyList; break;
        }

        if (keys == null || keys.Count == 0)
            return false;

        string activeSkillName = "";
        try
        {
            var active = ResolveActiveSkillForMP();
            activeSkillName = active.ToString();
        }
        catch
        {
            activeSkillName = "";
        }

        foreach (var keyRaw in keys)
        {
            var key = (keyRaw ?? "").Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            int effectiveLv = GetTraitEffectiveLevelForScoring(hostSet, activeSkillName, trait, key);
            if (effectiveLv <= 0)
                continue;

            string normKey = NormalizeTraitJudgeYakuName_Local(key);
            if (string.IsNullOrEmpty(normKey))
                continue;

            foreach (var y in yakuNames)
            {
                if (string.IsNullOrEmpty(y))
                    continue;

                string normY = NormalizeTraitJudgeYakuName_Local(y);
                if (string.IsNullOrEmpty(normY))
                    continue;

                if (string.Equals(normY, normKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
    }
    catch
    {
    }

    return false;
}
private void WireScoringOK()
{
    // 旧来の scoringPanel ルート
    WireScoringOK_Internal(scoringPanel);

    // 手動UI用のプレイヤー／敵パネル配下も保険で見る
    WireScoringOK_Internal(scoringPanelPlayer);
    WireScoringOK_Internal(scoringPanelEnemy);
}

private void WireScoringOK_Internal(GameObject root)
{
    if (root == null) return;

    try
    {
        var btns = root.GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            if (b == null) continue;

            var nm = b.name ?? string.Empty;
            if (nm.Contains("OK") || nm.Contains("Ok") || nm.Contains("OkButton"))
            {
                b.onClick.RemoveListener(OnClickScoreOK);
                b.onClick.AddListener(OnClickScoreOK);
            }
        }
    }
    catch
    {
        // ここで例外が出ても対局が止まらないように握りつぶし
    }
}
private void OnClickScoreOK()
{
    try
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayScoringPanelOkSE();
        }
    }
    catch { }

    __StopScoringStepReveal();
    // ★修正: 旧来の scoringPanel に加えて、手動UIパネルも必ず閉じる
    if (scoringPanel)       scoringPanel.SetActive(false);
    if (scoringPanelPlayer) scoringPanelPlayer.SetActive(false);
    if (scoringPanelEnemy)  scoringPanelEnemy.SetActive(false);

    // ★カットイン（ツモ・ロン＋画像）もここで閉じる
    if (winCutinRoot)
        winCutinRoot.SetActive(false);
    if (winCutinGroup)
        winCutinGroup.alpha = 0f;

    // ★追加：和了（カットイン→点数パネルOK）のタイミングで、待ち牌表示を確実に非表示にする
    if (playerTenpaiWaitsRoot)
    {
        playerTenpaiWaitsRoot.SetActive(false);

        // 表示物が残らないよう掃除
        if (playerTenpaiWaitSlots != null && playerTenpaiWaitSlots.Count > 0)
        {
            for (int i = 0; i < playerTenpaiWaitSlots.Count; i++)
            {
                var slot = playerTenpaiWaitSlots[i];
                if (!slot) continue;
                ClearChildren(slot);
            }
        }
        else
        {
            if (allowAutoLayoutWhenNoSlots)
                ClearChildren(playerTenpaiWaitsRoot.transform);
        }

        // 次に聴牌になったとき必ず再生成されるようキャッシュもリセット
        _lastPlayerTenpaiWaitsKey = "";
    }

    // ★裏ドラ状態と表示をリセット（次局へ持ち越さない）
    _includeUraForScoring = false;

    uraIndicators.Clear();

    if (scoringDoraRoot)
    {
        for (int i = scoringDoraRoot.childCount - 1; i >= 0; i--)
            Destroy(scoringDoraRoot.GetChild(i).gameObject);
    }

    // Ensure any enemy scoring overlays are fully cleared & detect enemy scoring
    bool _wasEnemyScoring = false;
    try { _wasEnemyScoring = Addon_WasEnemyScoringAndReset(); } catch { _wasEnemyScoring = false; }
    try { if (_wasEnemyScoring) Addon_ClearEnemyScoringOverlays(); } catch {}

if (!_wasEnemyScoring && ((_pendingPlayerWinDamageToEnemy && _pendingPlayerWinDamageFinal > 0) || (_pendingPlayerWinHpHeal && _pendingPlayerWinHpHealAbs > 0) || (_pendingPlayerWinMpHeal && _pendingPlayerWinMpHealAbs > 0)))
    {
        // 演出中は次のターン／次局／勝利演出へ進めない
        _freezeProgression = true;

        // 既に演出中なら二重起動しない
        if (_playerWinDamageAnimating) return;
if (playerWinDamageAnimSeconds <= 0f)
{
    if (_pendingPlayerWinDamageToEnemy && _pendingPlayerWinDamageFinal > 0)
    {
        enemyHP = Mathf.Max(0, enemyHP - Mathf.Max(0, _pendingPlayerWinDamageFinal));
    }

    if (_pendingPlayerWinHpHeal && _pendingPlayerWinHpHealAbs > 0)
    {
        playerHP = Mathf.Min(playerHP + Mathf.Max(0, _pendingPlayerWinHpHealAbs), playerMaxHP);
    }

    // ★追加：MPも即時反映
    if (_pendingPlayerWinMpHeal && _pendingPlayerWinMpHealAbs > 0)
    {
        _mp = ClampToEffectiveMaxMP(_mp + Mathf.Max(0, _pendingPlayerWinMpHealAbs));
    }

    _pendingPlayerWinDamageToEnemy = false;
    _pendingPlayerWinDamageBase = 0;
    _pendingPlayerWinDamageFinal = 0;

    _pendingPlayerWinHpHeal = false;
    _pendingPlayerWinHpHealAbs = 0;

    // ★追加：MP保留をクリア
    _pendingPlayerWinMpHeal = false;
    _pendingPlayerWinMpHealAbs = 0;

    UpdateHpUI();
    UpdateMpUI();

    // ★修正：ダメージ確定後に中断データを上書き保存（古いHP復元バグ防止）
    TryAutoSaveSuspendSnapshot();

    _freezeProgression = false;
    __ProceedAfterScoreOK_Internal(_wasEnemyScoring);
    return;
}
        StartCoroutine(__PlayerWin_ApplyPendingDamageToEnemy_ThenProceedScoreOK_Co(_wasEnemyScoring));
        return;
    }

    if (_wasEnemyScoring && _pendingEnemyWinDamage && _pendingEnemyWinDamageFinal > 0)
    {
        // 演出中は次のターン／次局／敗北演出へ進めない
        _freezeProgression = true;

        // ★保険：スコアOK直後に残っている Invoke / 自動進行トリガを全キャンセル（演出中の並行進行を防ぐ）
        try { CancelInvoke(); } catch {}

        // ★レジェンダリー②：直後の敵和了ダメージ半減（1回だけ消費）
        _pendingEnemyWinDamageFinal = TryConsumeLegendaryDamageHalfOnEnemyWin(_pendingEnemyWinDamageFinal);

        // 既に演出中なら二重起動しない
        if (_enemyWinDamageAnimating) return;

        // 演出時間が0以下なら即時反映して続行
        if (enemyWinDamageAnimSeconds <= 0f)
        {
            playerHP = Mathf.Max(0, playerHP - Mathf.Max(0, _pendingEnemyWinDamageFinal));

            _pendingEnemyWinDamage = false;
            _pendingEnemyWinDamageBase = 0;
            _pendingEnemyWinDamageFinal = 0;

            UpdateHpUI();

            // ★修正：ダメージ確定後に中断データを上書き保存（古いHP復元バグ防止）
            TryAutoSaveSuspendSnapshot();

            _freezeProgression = false;
            __ProceedAfterScoreOK_Internal(_wasEnemyScoring);
            return;
        }

        StartCoroutine(__EnemyWin_ApplyPendingDamage_ThenProceedScoreOK_Co(_wasEnemyScoring));
        return;
    }

    // 通常は従来通り「次へ進める」ので凍結解除して続行
    _freezeProgression = false;
    __ProceedAfterScoreOK_Internal(_wasEnemyScoring);
}
// 例： "Pin5_sp_honitsu_common" の "honitsu" を取り出して表示名（混一色）に変換する
private static bool TryGetSpecialTileUpgradeYakuFromId(string rawId, out string yakuDisplayName)
{
    yakuDisplayName = null;

    rawId = StripStar(rawId);
    if (string.IsNullOrEmpty(rawId)) return false;
    if (!IsSpecialTileId(rawId)) return false;

    var parts = rawId.Split('_');
    int spIndex = -1;
    for (int i = 0; i < parts.Length; i++)
    {
        if (string.Equals(parts[i], "sp", StringComparison.OrdinalIgnoreCase))
        {
            spIndex = i;
            break;
        }
    }
    if (spIndex < 0) return false;

    // sp の次から、レア度トークン（common/rare/epic/legendary/normal）や Lx が来るまでを読む
    for (int i = spIndex + 1; i < parts.Length; i++)
    {
        var t = (parts[i] ?? "").Trim();
        if (string.IsNullOrEmpty(t)) continue;

        // レア度トークンに当たったら「役名は無い」とみなして終了
        if (string.Equals(t, "normal", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "n", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "common", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "c", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "rare", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "r", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "epic", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "e", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "legendary", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "l", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // L1 などが来たらここで終了
        if ((t.Length >= 2) && (t[0] == 'L' || t[0] == 'l'))
        {
            int n;
            if (int.TryParse(t.Substring(1), out n) && n > 0)
                return false;
        }

        // ここに来たトークンを「役名トークン」とみなす
        yakuDisplayName = NormalizeSpecialTileYakuTokenToDisplay(t);
        return !string.IsNullOrEmpty(yakuDisplayName);
    }

    return false;
}

private static string NormalizeSpecialTileYakuTokenToDisplay(string token)
{
    if (string.IsNullOrEmpty(token)) return token;

    // すでに日本語名で入っているならそのまま
    bool hasNonAscii = false;
    for (int i = 0; i < token.Length; i++)
    {
        if (token[i] > 127) { hasNonAscii = true; break; }
    }
    if (hasNonAscii) return token;

    string k = token.Trim().ToLowerInvariant();

    // よく使う役はここで日本語表示へ
    if (k == "honitsu" || k == "honnitsu") return "混一色";
    if (k == "chinitsu" || k == "chinnitsu") return "清一色";
    if (k == "tanyao") return "タンヤオ";
    if (k == "pinfu") return "平和";
    if (k == "iipeikou") return "一盃口";
    if (k == "ryanpeikou") return "二盃口";
    if (k == "toitoi") return "対々和";
    if (k == "sanankou") return "三暗刻";
    if (k == "sanshoku" || k == "sanshokudoujun") return "三色同順";
    if (k == "ittsu" || k == "ikkitsuukan") return "一気通貫";
    if (k == "yakuhai") return "役牌";
    if (k == "chiitoi" || k == "chitoitsu" || k == "chitoi") return "七対子";

    // 変換表に無いものは、そのまま出す（仕様追加されても壊れにくい）
    return token;
}
// ===== Legendary effect (temporary / per-enemy state) =====
private bool _legendaryDamageHalfPending = false;
private string _legendaryDamageHalfEnemyKey = null;

private bool _legendaryHalfMpCostPending = false;
private string _legendaryHalfMpCostEnemyKey = null;

// ★追加：「次の局だけ」適用するためのターゲット局番号（roundNumber）
private int _legendaryHalfMpCostTargetRound = -1;

// 現在「MP消費半分」が有効か（同じ敵の間だけ + 指定局のみ）
public bool IsLegendaryHalfMpCostActive()
{
    if (!_legendaryHalfMpCostPending) return false;

    // 破損・旧データ防止
    if (_legendaryHalfMpCostTargetRound < 0)
    {
        _legendaryHalfMpCostPending = false;
        _legendaryHalfMpCostEnemyKey = null;
        RefreshLegendaryOngoingEffectsTextUI();
        return false;
    }

    // 同じ敵の間だけ有効
    string nowKey = GetCurrentEnemyKey_ForLegendary();
    if (!string.Equals(_legendaryHalfMpCostEnemyKey, nowKey, StringComparison.Ordinal))
    {
        _legendaryHalfMpCostPending = false;
        _legendaryHalfMpCostEnemyKey = null;
        _legendaryHalfMpCostTargetRound = -1;

        // ★追加：無効化したので継続表示を更新
        RefreshLegendaryOngoingEffectsTextUI();

        return false;
    }

    // 「次の局」(targetRound) の間だけ有効
    if (roundNumber != _legendaryHalfMpCostTargetRound)
    {
        // 既にターゲット局を過ぎていたら消滅
        if (roundNumber > _legendaryHalfMpCostTargetRound)
        {
            _legendaryHalfMpCostPending = false;
            _legendaryHalfMpCostEnemyKey = null;
            _legendaryHalfMpCostTargetRound = -1;
            RefreshLegendaryOngoingEffectsTextUI();
        }
        return false;
    }

    return true;
}

// 現在の敵を一意に識別するキー（最低限：currentEnemyIndex + 敵名）
private string GetCurrentEnemyKey_ForLegendary()
{
    string name = "";
    try { name = GetCurrentEnemyNameFromExcelWithLoop(); } catch { name = ""; }
    int idx = 0;
    try { idx = currentEnemyIndex; } catch { idx = 0; }
    return $"{idx}:{name}";
}
private int TryConsumeLegendaryDamageHalfOnEnemyWin(int rawDamage)
{
    int dmg = Mathf.Max(0, rawDamage);

    if (!_legendaryDamageHalfPending) return dmg;

    // 同じ敵の間だけ有効
    string nowKey = GetCurrentEnemyKey_ForLegendary();
    if (!string.Equals(_legendaryDamageHalfEnemyKey, nowKey, StringComparison.Ordinal))
    {
        // 敵が変わっていたら破棄
        _legendaryDamageHalfPending = false;
        _legendaryDamageHalfEnemyKey = null;
        _legendaryDamageHalfReservedSourceTiles.Clear();
        _legendaryDamageHalfTriggeredSourceTiles.Clear();

        // ★追加：破棄したので継続表示を更新
        RefreshLegendaryOngoingEffectsTextUI();

        return dmg;
    }

    int halved = Mathf.FloorToInt(dmg * 0.5f);

    // 0ダメになりうるのが嫌なら最低1にしたい場合はここで調整
    // if (dmg > 0 && halved < 1) halved = 1;

    _legendaryDamageHalfTriggeredThisScoring = true;
    _legendaryDamageHalfTriggeredSourceTiles.Clear();
    _legendaryDamageHalfTriggeredSourceTiles.AddRange(_legendaryDamageHalfReservedSourceTiles);

    _legendaryDamageHalfPending = false;
    _legendaryDamageHalfEnemyKey = null;
    _legendaryDamageHalfReservedSourceTiles.Clear();

    // ★追加：消費したので継続表示を更新
    RefreshLegendaryOngoingEffectsTextUI();

    return halved;
}
private System.Collections.IEnumerator __EnemyWin_ApplyPendingDamage_ThenProceedScoreOK_Co(bool wasEnemyScoring)
{
    _enemyWinDamageAnimating = true;

    int startHP = Mathf.Max(0, playerHP);
    int dmg     = Mathf.Max(0, _pendingEnemyWinDamageFinal);
    int endHP   = Mathf.Max(0, startHP - dmg);
try
{
    if (dmg > 0 && AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayBattleDamageSE();
    }
}
catch { }
    float dur = Mathf.Max(0.01f, enemyWinDamageAnimSeconds);
    float t = 0f;

    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        int dispHP = Mathf.RoundToInt(Mathf.Lerp(startHP, endHP, p));
        __UpdatePlayerHpUI_VisualOnly(dispHP);

        yield return null;
    }

    // 最終値を確定
    playerHP = endHP;

    _pendingEnemyWinDamage = false;
    _pendingEnemyWinDamageBase = 0;
    _pendingEnemyWinDamageFinal = 0;

    UpdateHpUI();

    _enemyWinDamageAnimating = false;

    // ★修正：ダメージ確定後に中断データを上書き保存（古いHP復元バグ防止）
    TryAutoSaveSuspendSnapshot();

    // 演出が終わったので次へ進める
    _freezeProgression = false;
    __ProceedAfterScoreOK_Internal(wasEnemyScoring);
}

private void __UpdatePlayerHpUI_VisualOnly(int displayHP)
{
    int hp = Mathf.Clamp(displayHP, 0, playerMaxHP);

    if (playerHPTMP)
    {
        if (!playerHPTMP.gameObject.activeSelf) playerHPTMP.gameObject.SetActive(true);
        playerHPTMP.text = playerHPConfig.textFormat
            .Replace("{cur}", hp.ToString())
            .Replace("{max}", playerMaxHP.ToString());
    }

    if (playerHPBar)
    {
        if (!playerHPBar.gameObject.activeSelf) playerHPBar.gameObject.SetActive(true);
        float f = (playerMaxHP > 0) ? (float)hp / playerMaxHP : 0f;
        playerHPBar.type       = playerHPConfig.fillType;
        playerHPBar.fillMethod = playerHPConfig.fillMethod;
        playerHPBar.fillOrigin = playerHPConfig.fillOrigin;
        playerHPBar.fillAmount = Mathf.Clamp01(f);

        // 既存：通常色の上書き
        if (playerHPConfig.overrideColor) playerHPBar.color = playerHPConfig.color;

        // ★修正：毒が有効な間は、演出中も必ず毒色を維持する
        // （__UpdatePlayerHpUI_VisualOnly が毎フレーム通常色に戻してしまうのを防ぐ）
        try
        {
            if (_enemySkillPoisonTurnRemaining > 0)
            {
                Color c = enemySkillPoisonHpColor;
                if (c.a <= 0.001f) c.a = 1f;
                playerHPBar.color = c;
            }
        }
        catch { }
    }
}
private void __UpdateEnemyHpUI_VisualOnly(int displayHP)
{
    int hp = Mathf.Clamp(displayHP, 0, enemyMaxHP);

    if (enemyHPTMP)
    {
        if (!enemyHPTMP.gameObject.activeSelf) enemyHPTMP.gameObject.SetActive(true);
        enemyHPTMP.text = enemyHPConfig.textFormat
            .Replace("{cur}", hp.ToString())
            .Replace("{max}", enemyMaxHP.ToString());
    }

    if (enemyHPBar)
    {
        if (!enemyHPBar.gameObject.activeSelf) enemyHPBar.gameObject.SetActive(true);
        float f = (enemyMaxHP > 0) ? (float)hp / enemyMaxHP : 0f;
        enemyHPBar.type       = enemyHPConfig.fillType;
        enemyHPBar.fillMethod = enemyHPConfig.fillMethod;
        enemyHPBar.fillOrigin = enemyHPConfig.fillOrigin;
        enemyHPBar.fillAmount = Mathf.Clamp01(f);
        if (enemyHPConfig.overrideColor) enemyHPBar.color = enemyHPConfig.color;
    }
}

private void __ProceedAfterScoreOK_Internal(bool _wasEnemyScoring)
{
// ★修正：敗北演出が既に走っている場合は絶対に次局へ進めない
if (_defeatTransitionRunning) return;

if (Mathf.Max(0, playerHP) <= 0)
{
    // ラン一時要素（通貨/お札/ラン限定強化など）はここで従来通りクリア
    ClearRunEphemeral();

StartDefeatTransitionIfNeeded();
    return;
}

    // 以降の「次局へ」処理は必ずここに集約する（ローカル変数名の重複（CS0136）と、局番号が進まない問題を防ぐ）
    void GoNextRoundOrDefeat()
    {
        int nextRound = roundNumber + 1;
        bool roundsFinished = (nextRound > maxRounds);

        // 未撃破のまま最大局数を終了 → 敗北（敗北カットイン→報酬へ）
        if (roundsFinished)
        {
            __EnsureOmamoriAtLeastOneForReward();

StartDefeatTransitionIfNeeded();
            return;
        }
        roundNumber = nextRound;
        RefreshTopUI();
        StartNextHand();
    }

    // If the last scoring was triggered by ENEMY's win:
    if (_wasEnemyScoring)
    {
        // ★修正: 敵専用の勝利オーバーレイ／カットインも確実に閉じる
        try { CloseEnemyWinPanel(); } catch {}

        // ★敵が和了したことを記録
        _enemyHasWonThisHand = true;

        // ★追加：ここ（=敵点数計算パネルを閉じた瞬間）から敵手牌を灰色化する
        _enemyGreyOutHandAfterScoreOk = true;
        try { RefreshEnemyHandUI_FullRebuild(); } catch {}
        try { RefreshAll(); } catch {}

        // ここでもう一度保険（スコア閉じ後に0になっているケース）
        if (Mathf.Max(0, playerHP) <= 0)
        {
            StartDefeatTransitionIfNeeded();
            return;
        }

        // ★仕様変更：敵が和了したら即座にその局を終了して次局へ
        GoNextRoundOrDefeat();
        return;

    }

    // ここまでに totalScore は加算済み。敵撃破かを判定
    bool defeatedThisEnemy = false;
    try { defeatedThisEnemy = (enemyHP <= 0) || pendingNextStage; } catch { defeatedThisEnemy = false; }

// 1) 敵撃破した場合：次の敵へ or クリア
if (defeatedThisEnemy)
{
// ★追加：敵撃破で継続効果を消滅
_legendaryDamageHalfPending = false;
_legendaryDamageHalfEnemyKey = null;
_legendaryHalfMpCostPending = false;
_legendaryHalfMpCostEnemyKey = null;
_legendaryHalfMpCostTargetRound = -1;
RefreshLegendaryOngoingEffectsTextUI();
// ★追加：次の敵へ移行するため、現在のHP/MPをRun_*に保存（満タン化はしない）
try
{
    PlayerPrefs.SetInt("Run_PlayerHP", Mathf.Max(0, playerHP));

    // ★追加：強化画面で「現在値/最大値」を正しく出すため、最大HPも保存
    PlayerPrefs.SetInt("Run_PlayerMaxHP", Mathf.Max(1, playerMaxHP));

    // MPは GameManager_SkillMP_Addon 側の _mp を保存（存在しない場合もあるので安全に）
    int curMpSaved = -1;
    try
    {
        var fMp = this.GetType().GetField("_mp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (fMp != null && fMp.FieldType == typeof(int))
        {
            int curMp = Mathf.Max(0, (int)fMp.GetValue(this));
            PlayerPrefs.SetInt("Run_PlayerMP", curMp);
            curMpSaved = curMp;
        }
    }
    catch { /* MP未導入でも安全 */ }

    // ★追加：強化画面で「現在値/最大値」を正しく出すため、最大MPも保存
    // RunSceneの表示と合わせるため、EffectiveMaxMP() が呼べるならそれを使う
    try
    {
        var miEff = this.GetType().GetMethod("EffectiveMaxMP",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (miEff != null && miEff.ReturnType == typeof(int))
        {
            int maxMp = Mathf.Max(0, (int)miEff.Invoke(this, null));
            PlayerPrefs.SetInt("Run_PlayerMaxMP", maxMp);
        }
        else
        {
            // EffectiveMaxMP が無い環境でも「現在=最大」になって表示崩れしないよう最低限入れる
            if (curMpSaved >= 0) PlayerPrefs.SetInt("Run_PlayerMaxMP", curMpSaved);
        }
    }
    catch
    {
        if (curMpSaved >= 0) PlayerPrefs.SetInt("Run_PlayerMaxMP", curMpSaved);
    }

    PlayerPrefs.Save();
}
catch { /* 保存できなくても進行は止めない */ }
        int enemyIdx = 0;
        try { enemyIdx = Mathf.Max(0, ProgressionFlowController.GetCurrentEnemyIndex()); }
        catch
        {
            try { enemyIdx = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { enemyIdx = 0; }
        }
        // ★追加：ローグライト用「Run内撃破数」加算（同じ敵を二重カウントしない）
        try
        {
            int lastCounted = PlayerPrefs.GetInt("Run_LastCountedEnemyIndex", -1);
            if (lastCounted != enemyIdx)
            {
                int runDefeated = Mathf.Max(0, PlayerPrefs.GetInt("Run_DefeatedEnemyCount", 0));
                runDefeated += 1;
                PlayerPrefs.SetInt("Run_DefeatedEnemyCount", runDefeated);
                PlayerPrefs.SetInt("Run_LastCountedEnemyIndex", enemyIdx);
                PlayerPrefs.Save();
            }
        }
        catch { }

        try
        {
            string defeatedEnemyBaseName = GetCurrentEnemyBaseNameForResources();
            PlayerData.NotifyEnemyDefeatedForSkillUnlocks(defeatedEnemyBaseName);
        }
        catch { }

        // ★ゼウス判定：敵数や「最後の敵」ではなく、固有名詞で判定する
        bool isZeusEnemy = false;
        try
        {
            // まずはExcel/進行から “素の名前” を取る（+周回サフィックス無し）
            var baseName = GetCurrentEnemyBaseNameForResources();
            if (!string.IsNullOrEmpty(baseName))
            {
                var n = baseName.Replace(" ", "").Replace("　", "").Trim();
                var lower = n.ToLowerInvariant();
                if (n == "ゼウス" || lower == "zeus") isZeusEnemy = true;
            }
        }
        catch { isZeusEnemy = false; }
        PreparePendingGemRoll(isZeusEnemy);

        bool goSecretHadesRoute = false;
        try
        {
            // 「役満（数え役満含む。13翻以上）の和了でゼウスを倒した」
            goSecretHadesRoute = isZeusEnemy && _lastPlayerWinWasYakumanOrKazoe;
        }
        catch { goSecretHadesRoute = false; }

        // ★ユニークお守り抽選（強化画面で表示するため、Upgradeに行く通常敵のタイミングで行う）
        if (!isZeusEnemy)
        {
            PreparePendingUniqueOmamoriRoll_OnEnemyDefeated();
        }
        bool isTierClear = false;
        try { isTierClear = GameManager.IsCurrentTierLastLevel(); } catch { isTierClear = false; }

        bool hadesDefeatedThisBattle = false;
        try
        {
            int runtimeEnemyIdxNow = 0;
            try { runtimeEnemyIdxNow = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { runtimeEnemyIdxNow = 0; }

            int excelKeyNow = EnemyConfigExcel.MapRuntimeIndexToExcelKey(runtimeEnemyIdxNow);
            bool isHadesEnemyNow = (excelKeyNow == EnemyConfigExcel.SecretBossExcelKey);

            if (!isHadesEnemyNow)
            {
                try
                {
                    var baseEnemyNameNow = GetCurrentEnemyBaseNameForResources();
                    if (__IsNamedEnemyHades(baseEnemyNameNow))
                        isHadesEnemyNow = true;
                }
                catch { }
            }

            hadesDefeatedThisBattle = isHadesEnemyNow;
        }
        catch { hadesDefeatedThisBattle = false; }
if (isZeusEnemy || isTierClear || hadesDefeatedThisBattle)
{
    // ★実績：Tierクリア / ハデス撃破 をここで拾う（演出や遷移より前）
    try
    {
        if (isTierClear)
        {
            int tierNow = 1;
            try { tierNow = Mathf.Max(1, GameManager.GetCurrentTier()); } catch { tierNow = 1; }

            string skillName = "";
            try
            {
                var sk = GetEquippedSkill();
                skillName = (sk != null) ? sk.ToString() : "";
            }
            catch { skillName = ""; }

            AchievementSystem.NotifyTierCleared(tierNow, skillName);
        }

        if (hadesDefeatedThisBattle)
        {
            AchievementSystem.NotifyHadesDefeated();
        }
    }
    catch { }

    // ★実績：Run合計スコアでスコア実績を判定（Tierクリア扱いで報酬へ行く場合）
    try { AchievementSystem.NotifyRunFinishedScore(runScore); } catch { }

    // Tierクリアなら次Tier解放（ハーデスはTierクリア扱いにしない）
    if (isTierClear)
    {
        try { GameManager.UnlockNextTierIfCleared(); } catch {}
    }

            // ★報酬付与条件を満たしていれば、ここでお守りを確定させる
            __EnsureOmamoriAtLeastOneForReward();


            // ★勝利カットイン →（ゼウス役満撃破なら裏ルート）→ 天使会話 → 報酬
            StartCoroutine(__ShowPlayerVictoryCutinThen(() =>
            {
                var inst = ProgressionFlowController.Instance;
                if (inst == null)
                {
                    inst = UnityEngine.Object.FindObjectOfType<ProgressionFlowController>(true);
                    if (inst == null)
                    {
                        var go = new GameObject("ProgressionFlow");
                        inst = go.AddComponent<ProgressionFlowController>();
                    }
                }
                if (inst != null)
                {
                    if (hadesDefeatedThisBattle)
                    {
                        try { PlayerPrefs.SetInt("PF_SecretHadesRoute", 0); PlayerPrefs.Save(); } catch {}
                        inst.GoFromSecretHadesClearToSecretAngelClear();
                    }
                    else if (goSecretHadesRoute)
                    {
                        try { PlayerPrefs.SetInt("PF_SecretHadesRoute", 1); PlayerPrefs.Save(); } catch {}
                        inst.GoFromZeusClearToSecretAngelIntro();
                    }
                    else
                    {
                        inst.GoFromBattleClearToRewardViaAngel();
                    }
                }
                else
                {
                    SceneManager.LoadScene(rewardSceneName);
                }
            }));
        }
else
{
    // 通常敵：勝利後は強化(Upgrade)へ
    StartCoroutine(__ShowPlayerVictoryCutinThen(() =>
    {
        try
        {
            // ★追加：直前和了の「特別牌：役強化Lv+」内訳を UpgradeScene 用に保存（表記用）
            // 例: "役牌=1;混一色=2"
            const string PrefKey_TraitBonusPairs = "PF_LastSpecialTileTraitBonusPairs";

            if (_specialTileTraitLvBonusThisScoring != null && _specialTileTraitLvBonusThisScoring.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in _specialTileTraitLvBonusThisScoring)
                {
                    var k = (kv.Key ?? "").Trim();
                    int v = kv.Value;
                    if (string.IsNullOrEmpty(k) || v <= 0) continue;

                    // セパレータ文字が混ざっても壊れにくいよう最低限置換
                    k = k.Replace(";", " ").Replace("=", " ");
                    parts.Add(k + "=" + v.ToString());
                }

                PlayerPrefs.SetString(PrefKey_TraitBonusPairs, string.Join(";", parts));
                PlayerPrefs.Save();
            }
            else
            {
                // ボーナス無しなら空にしておく（古い値が残るのを防止）
                PlayerPrefs.SetString(PrefKey_TraitBonusPairs, "");
                PlayerPrefs.Save();
            }
        }
        catch
        {
            // 表記用なので失敗しても進行は止めない
        }

        var inst = ProgressionFlowController.Instance;
        if (inst == null)
        {
            inst = UnityEngine.Object.FindObjectOfType<ProgressionFlowController>(true);
            if (inst == null)
            {
                var go = new GameObject("ProgressionFlow");
                inst = go.AddComponent<ProgressionFlowController>();
            }
        }
        if (__ShouldShowDemoEndCutin())
        {
            StartCoroutine(__ShowDemoEndCutinThen(() =>
            {
                try
                {
                    // ★Demo終了でも、通常の報酬画面と同じように
                    //  直前に報酬お守りを確定させる
                    __EnsureOmamoriAtLeastOneForReward();
                }
                catch
                {
                }

                if (inst != null)
                {
                    inst.GoFromBattleClearToRewardViaAngel();
                }
                else
                {
                    SceneManager.LoadScene(rewardSceneName);
                }
            }));
        }
        else
        {
            if (inst != null)
            {
                inst.GoFromBattleWinToUpgrade();
            }
            else
            {
                SceneManager.LoadScene(upgradeSceneName);
            }
        }
    }));
}
        return;
    }

    if (_playerHasWonThisHand && !_enemyHasWonThisHand && !_lastHandWasRyukyoku)
    {
        // ★仕様変更：プレイヤーが和了したら即座にその局を終了して次局へ
        GoNextRoundOrDefeat();
        return;
    }

    // それ以外（敵未撃破・局数も残っている） → 次局へ
    GoNextRoundOrDefeat();
}
    // ===== Round / Stage helpers =====
    private void AdvanceRound()
    {
        roundNumber = Mathf.Min(roundNumber + 1, maxRounds);
        RefreshTopUI();
    }
// 東1: 北家 → 東2: 西家 → 東3: 南家 → 東4: 東家 …を繰り返す
private string GetPlayerSeatWind()
{
    // 風の並び: 0=East,1=South,2=West,3=North （YakuEvaluator側の前提に合わせる）
    string[] winds = { "East", "South", "West", "North" };

    int n = Mathf.Max(1, roundNumber);   // 東1以上にクリップ
    // 東1(n=1)のとき North(3) から始めて、局が進むごとに 3→2→1→0… と回す
    int step = (n - 1) % 4;              // 0,1,2,3,0,1,2,3,...
    int index = (3 - step + 4) % 4;      // 3,2,1,0,3,2,1,0,...

    return winds[index];                 // "East"/"South"/"West"/"North"
}

// 敵の自風（プレイヤーの下家）を返す
private string GetEnemySeatWind()
{
    string p = GetPlayerSeatWind();

    // 風の並び: 0=East,1=South,2=West,3=North
    int pi;
    switch (p)
    {
        case "East":  pi = 0; break;
        case "South": pi = 1; break;
        case "West":  pi = 2; break;
        case "North": pi = 3; break;
        default:      pi = 0; break;
    }

    int ei = (pi + 1) % 4; // 下家 = 次の風
    switch (ei)
    {
        case 0: return "East";
        case 1: return "South";
        case 2: return "West";
        case 3: return "North";
    }
    return "South";
}
private string GetRoundWind()
{
    return (roundNumber <= 4) ? "East" : "South";
}
private int GetRoundNumberInCurrentWind()
{
    // 東1〜東4: 1〜4、南1〜南4: 1〜4
    return (roundNumber <= 4) ? roundNumber : (roundNumber - 4);
}

private string BuildRoundLabelForUI()
{
    string wind = GetRoundWind();
    int num = GetRoundNumberInCurrentWind();

    string windText;
    if (wind == "South")
        windText = GetGameFixedText_Local("round_wind_south");
    else
        windText = GetGameFixedText_Local("round_wind_east");

    string format = GetGameFixedText_Local("round_label_format");

    try
    {
        return string.Format(format, windText, num);
    }
    catch
    {
        return $"{windText}{num}{GetGameFixedText_Local("round_suffix")}";
    }
}
// ★直前局の勝者（敵なら true / プレイヤーなら false）
private bool _addonLastHandWinnerWasEnemy = false;
private void StartNextHand()
{
    _playerHasWonThisHand = false;
    _enemyHasWonThisHand  = false;

    // ★追加：敵手牌の灰色化はスコアOK後にだけ行う
    _enemyGreyOutHandAfterScoreOk = false;

    // 新しい局を開始するのでフラグはここで折っておく
    _lastHandWasRyukyoku         = false;
    _addonLastHandWinnerWasEnemy = false;

    // ★重要：サスペンド復帰中なら、リセット処理の前に復元して抜ける
    if (TryLoadSuspendSnapshot())
        return;

    // ★仕様変更：次局に移行したら「両者の手牌・捨て牌を必ずリセット」する
    // （流局でも、両者和了でも同じ。片和了での次局移行は発生しない）
    EnemyAddon_ResetStateForNewHand_Full();
    _enemyRevealHandNow = false;

    // ★NEW：壁構築～「対局開始」カットイン～4枚ずつ配牌～ツモ4枚表示まで
    //       すべてをコルーチンで実行する
    StartCoroutine(__MatchStartIntroAndDeal_Co());
}
private readonly System.Collections.Generic.List<string> _enemyHand = new System.Collections.Generic.List<string>(13);
private bool _enemyIsRiichi = false;
private readonly System.Collections.Generic.HashSet<string> _enemyRiichiWaits = new System.Collections.Generic.HashSet<string>();
private System.Collections.IEnumerator __MatchStartIntroAndDeal_Co()
{
    // 壁を構築
    BuildWall();

    // ドラを初期化（1枚目を表示）
    InitDoraIndicators(1);

    // 局の状態だけクリア（配牌はこの後アニメーションしながら行う）
    ResetRoundStateForOpeningHand();

    // ★敵手牌（新仕様）もここで初期化
    _enemyHand.Clear();
    _enemyIsRiichi = false;
    _enemyRiichiDeclaredTurnCounter = -999;
    _enemyRiichiWaits.Clear();

    // 敵山が空なら作る（念のため）
    if (enemyDeck == null || enemyDeck.Count == 0) BuildEnemyDeck();

    // ★追加：UIもこのタイミングで一度まっさらにしておく
    //         → 「対局開始」カットインが出る前に牌が一切表示されないようにする
    RefreshHandUI();
    RefreshOfferUI();
    RefreshDiscardUI();
    RefreshEnemyDiscardUI();
    RefreshMeldUI();
    RefreshEnemyHandUI_FullRebuild();

    // ===== 「局開始」テキストカットイン（東1局／東2局...） =====
    // 「東n局」の文字と、対局開始SEを使ってカットイン表示
    yield return __ShowTextCutin_Co(BuildRoundLabelForCutin(), matchStartSESource);

    // ===== 配牌（プレイヤーも敵も 4+4+4+1 のグループで配る）=====
    // ※「間隔はプレイヤーと同じ秒数」＝現行の 0.5 秒区切りを踏襲
    int totalToDeal = Mathf.Min(13, deck.Count);

    for (int i = 0; i < totalToDeal; i++)
    {
        // --- プレイヤーに1枚 ---
        hand.Add(deck.Pop());
        RefreshHandUI();

        // --- 敵に1枚（敵は専用山から）---
        // 13枚揃うまで引く
        if (_enemyHand.Count < 13)
        {
            _enemyHand.Add(DrawEnemyTile());
            RefreshEnemyHandUI_DealOne(); // 右から増える演出（後述）
        }

bool isLast = (i == totalToDeal - 1);
bool isGroupEnd = ((i + 1) % 4 == 0); // 4,8,12枚目

// ★追加：初期配牌（4+4+4+1）の“グループ投入”SE（計4回：4枚目/8枚目/12枚目/13枚目）
try
{
    if (AudioManager.Instance != null && (isGroupEnd || isLast))
    {
        AudioManager.Instance.PlayOpeningHandDealGroupSE();
    }
}
catch { }

if (isGroupEnd && !isLast)
{
    yield return new WaitForSecondsRealtime(0.5f);
}

    }

    // 全13枚配り終えたらリーパイ（ソート）して最終配置を表示
    SortHand();
    RefreshHandUI();

    // 敵手牌は内部的には保持するが、表示は（表/裏設定に従って）固定リビルド
    RefreshEnemyHandUI_FullRebuild();

    // 配牌完了後、1秒待ってからツモ牌4枚を表示
    yield return new WaitForSecondsRealtime(1f);

    // MP/UI 更新（局開始直後に見た目だけ整える）
    UpdateMpUI();
    // ★仕様変更：局の開始は「敵のツモ番」からにする
    phase = Phase.EnemyTurn;
    UpdateButtons();
    RefreshAll();

    // ★追加：1局ごとの自動セーブ（アプリを落としても、この局の開始時点から復元できる）
    TryAutoSaveSuspendSnapshot();

    StartCoroutine(EnterEnemyTurnAfterPlayerAfterDelay(0.5f));
}
private void TryAutoSaveSuspendSnapshot()
{
    try
    {
        // ★自動セーブは「復帰フラグを立てない」。データだけ更新する。
        SaveSuspendSnapshot(false);
    }
    catch
    {
        // 自動セーブ失敗で進行を止めない
    }
}

private void OnApplicationPause(bool pause)
{
    // pause=true のとき（バックグラウンドへ移行）に保存
    if (pause)
    {
        // ★ここは本当の中断なので「復帰フラグを立てる」
        try { SaveSuspendSnapshot(true); } catch {}
    }
}

private void OnApplicationQuit()
{
    // 終了直前にも念のため保存
    // ★ここも本当の中断なので「復帰フラグを立てる」
    try { SaveSuspendSnapshot(true); } catch {}
}
private void EnterUpgradeFlow()
{
    pendingNextStage = true;

    // ★HP/MPを「持ち越し＋対局ごとの回復量(Recover HP/MP Per Battle)」込みで保存
    //   → SkillSetAsset.recoverHpPerBattle / recoverMpPerBattle を加算して上限クランプし、
    //     Run_PlayerHP / Run_PlayerMP の両方を保存する
    try { PersistRunPlayerHP(true); } catch {}

    if (upgradePanel) { upgradePanel.SetActive(true); BuildItemOffersUI(); }

    // 任意のマネージャに通知（存在しなくてもOK）
    SendMessage("OnStageClear", SendMessageOptions.DontRequireReceiver);

    // 強化画面が無い場合は即次ステージへ
    if (!upgradePanel)
    {
        StartNextStage();
    }
}
void StartNextStage()
{
    // Excel から敵名・HP・デッキ重み等を確実に適用（失敗しても Inspector には絶対に戻さない）
    if (!TryApplyExcelEnemyConfigForCurrentIndex())
    {
        Debug.LogError("[GameManager] Excelの敵設定の適用に失敗しました。enemy_config.xlsx / シート名 / EnemyIndex を確認してください。");
        // ここで無理にHPを捏造しない（Inspectorには戻らない）
    }

    SetEnemyNameOnUI(GetCurrentEnemyNameFromExcelWithLoop());

    // ★重要：次戦開始時は「未初期化」に戻してから UpdateHpUI() を走らせる
    //         → UpdateHpUI() 内の Run_PlayerHP 復元ロジック（playerHP < 0）を確実に通す
    playerHP = -1;
    enemyHP  = -1;

    UpdateHpUI();

    // ★ここを変更：通常の局開始ではなく、「対局開始」カットイン＋配牌アニメーション付きで開始
    StartCoroutine(__MatchStartIntroAndDeal_Co());
}

private void UpdateHpUI() {
        // ★追加：Excel未適用なら1回だけここで適用（Inspectorの値に戻るのを防ぐ）
        try {
            bool resuming = false;
            try { resuming = PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) == 1; } catch {}
            if (!_excelEnemyApplied && !resuming) {
                TryApplyExcelEnemyConfigForCurrentIndex();
            }
        } catch {}
if (playerHP < 0)
{
    bool shouldFullHeal = false;
    try { shouldFullHeal = PlayerPrefs.GetInt("PF_PendingFullHeal", 0) == 1; } catch {}

    if (shouldFullHeal)
    {
        playerHP = playerMaxHP;

        // ★重要：PF_PendingFullHeal の消費は __ApplyRunBonusesAndRefreshUI() に一本化する
        // （ここで消すと「最大値更新→満タン化」の順序が崩れて最大値にならない）
    }

    else if (PlayerPrefs.HasKey("Run_PlayerHP"))
    {
        // 敵撃破→次戦の “持ち越しHP” を復元
        int saved = PlayerPrefs.GetInt("Run_PlayerHP", -1);
        if (saved >= 0) playerHP = Mathf.Clamp(saved, 0, playerMaxHP);
    }
else
{
    // ★変更：新規ランで未初期化なら満タンで開始（0始動は即ゲームオーバーになるため）
    playerHP = playerMaxHP;
    // 念のため持ち越しHPキーも消しておく
    try { PlayerPrefs.DeleteKey("Run_PlayerHP"); PlayerPrefs.Save(); } catch {}
}

}

// 敵HPの初期センチネルは従来どおり満タンでOK
if (enemyHP < 0) enemyHP = enemyMaxHP;

    // Ensure HP UI objects are active and visible at top (they should be placed under a top Canvas group in scene)
 // テキスト: Inspector 書式に従って出力（最前面化はしない）
if (playerHPTMP) {
    if (!playerHPTMP.gameObject.activeSelf) playerHPTMP.gameObject.SetActive(true);
    playerHPTMP.text = playerHPConfig.textFormat
        .Replace("{cur}", playerHP.ToString())
        .Replace("{max}", playerMaxHP.ToString());
}
if (enemyHPTMP) {
    if (!enemyHPTMP.gameObject.activeSelf) enemyHPTMP.gameObject.SetActive(true);
    enemyHPTMP.text = enemyHPConfig.textFormat
        .Replace("{cur}", enemyHP.ToString())
        .Replace("{max}", enemyMaxHP.ToString());
}

// バー: Inspector 設定に従って描画（色/方向/原点）
if (playerHPBar)
{
    if (!playerHPBar.gameObject.activeSelf) playerHPBar.gameObject.SetActive(true);
    float f = (playerMaxHP > 0) ? (float)playerHP / playerMaxHP : 0f;
    playerHPBar.type       = playerHPConfig.fillType;
    playerHPBar.fillMethod = playerHPConfig.fillMethod;
    playerHPBar.fillOrigin = playerHPConfig.fillOrigin;
    playerHPBar.fillAmount = Mathf.Clamp01(f);
    if (playerHPConfig.overrideColor) playerHPBar.color = playerHPConfig.color;
}
if (enemyHPBar)
{
    if (!enemyHPBar.gameObject.activeSelf) enemyHPBar.gameObject.SetActive(true);
    float f = (enemyMaxHP > 0) ? (float)enemyHP / enemyMaxHP : 0f;
    enemyHPBar.type       = enemyHPConfig.fillType;
    enemyHPBar.fillMethod = enemyHPConfig.fillMethod;
    enemyHPBar.fillOrigin = enemyHPConfig.fillOrigin;
    enemyHPBar.fillAmount = Mathf.Clamp01(f);
    if (enemyHPConfig.overrideColor) enemyHPBar.color = enemyHPConfig.color;
}

// ※ 旧コードの SetAsLastSibling() は削除。
//    前後関係は Inspector の「Layering 設定」で統一的に管理します。

        UpdateMpUI_IfAssigned();
        try { EnemySkills_RefreshStatusEffectsUI(); } catch {}
}
private void UpdateMpUI_IfAssigned()
{
    // MP フィールド名をリフレクションで探す（他アドオンと疎結合）
    try
    {
        var tp = this.GetType();
        int mp    = GetIntLike(tp, this, new[]{ "playerMP", "currentMP", "_playerMP", "_currentMP" }, 0);
        int mpMax = GetIntLike(tp, this, new[]{ "playerMaxMP", "maxMP", "_playerMaxMP", "_maxMP" }, 0);

        // ★表示復活：MP UI が非表示になっていても、存在するなら必ず再表示する
        if (playerMPTMP != null && !playerMPTMP.gameObject.activeSelf)
        {
            playerMPTMP.gameObject.SetActive(true);
        }
        if (playerMPBar != null && !playerMPBar.gameObject.activeSelf)
        {
            playerMPBar.gameObject.SetActive(true);
        }

        // ★重要：敵ターンなどで mpMax が 0 になる瞬間があるため、
        //         mpMax が取れないフレームでは表示を更新せず、最後の正常値を維持する
        if (mpMax > 0)
        {
            _mpUiCacheValid = true;
            _mpUiCachedCur  = mp;
            _mpUiCachedMax  = mpMax;
        }

        // キャッシュがまだ無い & mpMax も取れない場合は、何も上書きしない（0/0 にしない）
        if (!_mpUiCacheValid)
        {
            return;
        }

        int dispCur = _mpUiCachedCur;
        int dispMax = _mpUiCachedMax;

        // 数値テキストは常にキャッシュ表示
        if (playerMPTMP != null)
        {
            playerMPTMP.text = playerMPConfig.textFormat
                .Replace("{cur}", dispCur.ToString())
                .Replace("{max}", dispMax.ToString());
        }

        // ===== ここが本当の原因：Slider構成なら Slider.value/maxValue を更新しないとゲージが動かない =====
        UnityEngine.UI.Slider slider = null;

        try
        {
            if (playerMPBar != null) slider = playerMPBar.GetComponent<UnityEngine.UI.Slider>();
            if (slider == null && playerMPBar != null) slider = playerMPBar.GetComponentInParent<UnityEngine.UI.Slider>(true);
        }
        catch { slider = null; }

        if (slider != null)
        {
            // Slider方式のゲージは Image.fillAmount ではなく Slider.value/maxValue を更新する
            try
            {
                slider.maxValue = Mathf.Max(1f, (float)dispMax);
                slider.value    = Mathf.Clamp((float)dispCur, 0f, slider.maxValue);
            }
            catch { }

            // fillRect が存在するなら、見えない系（disabled/透明/非active）を復旧しておく
            try
            {
                if (slider.fillRect != null)
                {
                    var fillImg = slider.fillRect.GetComponent<UnityEngine.UI.Image>();
                    if (fillImg != null)
                    {
                        if (!fillImg.gameObject.activeSelf) fillImg.gameObject.SetActive(true);
                        if (!fillImg.enabled) fillImg.enabled = true;
                        try { fillImg.canvasRenderer.SetAlpha(1f); } catch { }

                        // 麻痺などの色変更が「実体（Fill）」に効くように参照を差し替える（Inspectorは変更しない）
                        if (playerMPBar != fillImg)
                        {
                            playerMPBar = fillImg;
                        }
                    }
                }
            }
            catch { }

            // Sliderルートの場合、ここで終了（以降の Image.fillAmount 方式で上書きしない）
            return;
        }

        // ===== Sliderではない場合だけ Image.fillAmount 方式で更新 =====
        UnityEngine.UI.Image barImg = playerMPBar;

        // playerMPBar が枠/背景（sprite無し）を指していると描画されないため、
        // sprite を持つ Image を子から拾う
        if (barImg != null && barImg.sprite == null)
        {
            try
            {
                var imgs = barImg.GetComponentsInChildren<UnityEngine.UI.Image>(true);

                UnityEngine.UI.Image best = null;
                UnityEngine.UI.Image fallback = null;

                for (int i = 0; i < imgs.Length; i++)
                {
                    var img = imgs[i];
                    if (img == null) continue;
                    if (img.sprite == null) continue;

                    if (fallback == null) fallback = img;

                    var n = img.gameObject.name;
                    if (!string.IsNullOrEmpty(n) && n.IndexOf("fill", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        best = img;
                        break;
                    }
                }

                if (best != null) barImg = best;
                else if (fallback != null) barImg = fallback;
            }
            catch { }
        }

        if (barImg != null)
        {
            if (!barImg.gameObject.activeSelf) barImg.gameObject.SetActive(true);
            if (!barImg.enabled) barImg.enabled = true;
            try { barImg.canvasRenderer.SetAlpha(1f); } catch { }

            // fillAmount を使う場合だけ Filled を強制
            barImg.type       = UnityEngine.UI.Image.Type.Filled;
            barImg.fillMethod = playerMPConfig.fillMethod;
            barImg.fillOrigin = playerMPConfig.fillOrigin;

            float f = (dispMax > 0) ? ((float)dispCur / dispMax) : 0f;
            barImg.fillAmount = Mathf.Clamp01(f);

            if (playerMPConfig.overrideColor)
            {
                barImg.color = playerMPConfig.color;
            }
            else
            {
                var c = barImg.color;
                if (c.a <= 0.001f)
                {
                    c.a = 1f;
                    barImg.color = c;
                }
            }

            // EnemySkills_RefreshStatusEffectsUI が “本体” を色変更できるように参照差し替え（Inspectorは変更しない）
            if (playerMPBar != barImg)
            {
                playerMPBar = barImg;
            }
        }
    }
    catch
    {
        /* 何もしない（MP 未導入でも安全） */
    }

    int GetIntLike(Type t, object inst, string[] names, int fallback)
    {
        foreach (var n in names)
        {
            var f = t.GetField(n, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(inst);
            var p = t.GetProperty(n, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(inst);
        }
        return fallback;
    }
}
private void __ApplyRunBonusesAndRefreshUI()
{
    // ★中断データが残っている状態では、ここで上乗せしない（中断スナップショットが正）
    try
    {
        if (PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) == 1) return;
    }
    catch { }

    bool shouldFullHeal = false;
    try { shouldFullHeal = PlayerPrefs.GetInt("PF_PendingFullHeal", 0) == 1; } catch { }

    // --- 永続ボーナス（Shopの宝石強化） ---
    int permHpBonus = 0;
    int permMpBonus = 0;
    try { permHpBonus = PlayerPrefs.GetInt("Perm_HPBonus", 0); } catch { permHpBonus = 0; }
    try { permMpBonus = PlayerPrefs.GetInt("Perm_MPBonus", 0); } catch { permMpBonus = 0; }

    // --- HP（Runボーナス + 永続ボーナス）---
    int hpBonus = 0;
    try { hpBonus = PlayerPrefs.GetInt("Run_HPBonus", 0); } catch { hpBonus = 0; }

    int totalHpBonus = 0;
    try { totalHpBonus = Mathf.Max(0, hpBonus) + Mathf.Max(0, permHpBonus); } catch { totalHpBonus = 0; }

    // ★重要：積み上げ禁止。必ず「基礎最大HP」から再計算する
    if (_basePlayerMaxHP_ForRunBonuses < 0)
    {
        _basePlayerMaxHP_ForRunBonuses = playerMaxHP;
    }
    playerMaxHP = Mathf.Max(1, _basePlayerMaxHP_ForRunBonuses + totalHpBonus);

    if (shouldFullHeal)
    {
        playerHP = playerMaxHP;
    }
    else if (PlayerPrefs.HasKey("Run_PlayerHP"))
    {
        int savedHp = PlayerPrefs.GetInt("Run_PlayerHP", -1);
        if (savedHp >= 0) playerHP = Mathf.Clamp(savedHp, 0, playerMaxHP);
    }

    // --- MP ---
    // ※EffectiveMaxMP() 側がRun/Perm/お守り等を含む前提なので、ここでpermを足して二重にしない
    try
    {
        var tp = this.GetType();
        var miEff = tp.GetMethod("EffectiveMaxMP",
                   System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var fMp = tp.GetField("_mp",
                   System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        if (miEff != null && fMp != null && fMp.FieldType == typeof(int))
        {
            int max = (int)miEff.Invoke(this, null);

            if (shouldFullHeal)
            {
                fMp.SetValue(this, Mathf.Max(0, max));
            }
            else if (PlayerPrefs.HasKey("Run_PlayerMP"))
            {
                int saved = PlayerPrefs.GetInt("Run_PlayerMP", -1);
                if (saved >= 0) fMp.SetValue(this, Mathf.Clamp(saved, 0, Mathf.Max(0, max)));
            }
        }
    }
    catch { }

    if (shouldFullHeal)
    {
        try { PlayerPrefs.SetInt("PF_PendingFullHeal", 0); PlayerPrefs.Save(); } catch { }
    }

    UpdateHpUI();
    UpdateMpUI_IfAssigned();
}
private void BuildEnemyDeck()
{
    int idx = 0;
    bool idxResolved = false;
    try { idx = ProgressionFlowController.GetCurrentEnemyIndex(); idxResolved = true; } catch {}
    if (!idxResolved)
    {
        try { idx = Mathf.Max(0, PlayerData.CurrentEnemy); idxResolved = true; } catch {}
    }

    // ★敵デッキはExcelのみ参照
    if (!EnemyConfigExcel.TryGetForRuntimeIndex(idx, out var cfg))
    {
        Debug.LogError("[EnemyDeck] Excel enemy_config.xlsx から敵設定を取得できませんでした。idx=" + idx);
        enemyDeck.Clear();
        return;
    }

    var pool = new List<string>(200);

    // --- 1) Excelの重みから基本プールを作る（Man/Pin/Sou 1..9） ---
    for (int n = 1; n <= 9; n++)
    {
        int wM = (cfg.weightMan != null && cfg.weightMan.Length > n) ? Mathf.Max(0, cfg.weightMan[n]) : 0;
        for (int k = 0; k < wM; k++) pool.Add("Man" + n);

        int wP = (cfg.weightPin != null && cfg.weightPin.Length > n) ? Mathf.Max(0, cfg.weightPin[n]) : 0;
        for (int k = 0; k < wP; k++) pool.Add("Pin" + n);

        int wS = (cfg.weightSou != null && cfg.weightSou.Length > n) ? Mathf.Max(0, cfg.weightSou[n]) : 0;
        for (int k = 0; k < wS; k++) pool.Add("Sou" + n);
    }

    // --- 2) 字牌（七種）をExcelのhonors重み分追加 ---
    string[] honors = { "East","South","West","North","White","Green","Red" };
    int wH = Mathf.Max(0, cfg.weightHonors);
    for (int k = 0; k < wH; k++)
        foreach (var h in honors) pool.Add(h);

    // ★Excelが全部0で空なら、その敵は「デッキ無し」扱いで止める（Excelのみ参照を徹底）
    if (pool.Count == 0)
    {
        Debug.LogError("[EnemyDeck] Excelの重みが全て0のため、敵デッキを作れません。idx=" + idx);
        enemyDeck.Clear();
        return;
    }

    // --- 3) プールをシャッフル ---
    for (int i = pool.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (pool[i], pool[j]) = (pool[j], pool[i]);
    }

    // --- 4) 重みがそのまま枚数になる（例：重み4＝各牌4枚＝通常の麻雀） ---
    enemyDeck.Clear();
    int copies = 1;
    var big = new List<string>(pool.Count * copies);
    for (int c = 0; c < copies; c++) big.AddRange(pool);

    for (int i = big.Count - 1; i > 0; i--)
    {
        int j = rng.Next(i + 1);
        (big[i], big[j]) = (big[j], big[i]);
    }
    for (int i = 0; i < big.Count; i++) enemyDeck.Push(big[i]);
}
    private string DrawEnemyTile()
    {
        if (enemyDeck==null || enemyDeck.Count==0) BuildEnemyDeck();
        return enemyDeck.Count>0 ? enemyDeck.Pop() : "Back";
    }
// Wrapper: play enemy discard VFX for the current attack window, then wait and advance to player's draw
private IEnumerator PlayEnemyDiscardVFXAndThenProceed(System.Collections.Generic.List<int> indices)
{
    // 敵の捨て牌VFX
    yield return PlayEnemyDiscardVFXForAttackWindow(indices);

    // 少し間をおく
    yield return new WaitForSeconds(0.5f);

// カットイン中は進行しない（既存仕様維持）
while (_enemyRiichiCutinRunning || _enemySkillCutinRunning)
{
    yield return null;
}

// ★ダメージ演出など進行凍結中は進行しない
while (_freezeProgression)
{
    yield return null;
}


    // ★重要：canRonNow は古いことがあるので、ここで「いまの lastEnemyTurnTiles」から再計算
    bool canRonNowLocal = false;
    if (phase == Phase.EnemyTurn)
    {
        canRonNowLocal = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);
    }

    // 鳴き・ロンの対象牌がない場合のみ、プレイヤー側のオファー／ツモ番へ移行
    if (phase == Phase.EnemyTurn && !canRonNowLocal)
        BeginOfferPhase();
}

/// <summary>
/// 敵スキル発動までの残りターン数を UI に反映する。
/// EnemySkills_Addon 側から呼び出す想定。
/// </summary>
public void EnemySkills_UpdateCountdownUI(int turnsUntilNext)
{
    _enemySkillTurnsUntilNext = Mathf.Max(0, turnsUntilNext);

    if (!enemySkillCountdownTMP) return;

    if (_enemySkillTurnsUntilNext <= 0)
    {
        // 0以下なら非表示（テキストも消す）
        enemySkillCountdownTMP.text = "";
        enemySkillCountdownTMP.gameObject.SetActive(false);
    }
    else
    {
        enemySkillCountdownTMP.gameObject.SetActive(true);
        enemySkillCountdownTMP.text = $"スキルまで あと{_enemySkillTurnsUntilNext}ターン";
    }
}
/// <summary>
/// 敵スキル発動時のカットインを再生するエントリポイント。
/// EnemySkills_Addon 側から StartCoroutine(...) で呼び出す想定。
/// </summary>
public Coroutine EnemySkills_PlayCutin(string enemyName, string skillDisplayName)
{
    if (!gameObject.activeInHierarchy)
        return null;

    return StartCoroutine(__EnemySkills_ShowCutin_Co(enemyName, skillDisplayName));
}
private IEnumerator __EnemySkills_ShowCutin_Co(string enemyName, string skillDisplayName)
{
    // 進行ロック開始
    _enemySkillCutinRunning = true;
    UpdateButtons();

    // ===== カットイン用画像をロード =====
    // リーチのカットインが参照しているのと同じファイル名
    // EnemyCutins/{enemyName}_cutin を直接 Resources から読み込む
    Sprite cutinSprite = null;

    if (!string.IsNullOrEmpty(enemyName))
    {
        // 「アマテラス +1」などのループ数サフィックスを削る
        const string loopSuffixMarker = " +";
        int loopIdx = enemyName.IndexOf(loopSuffixMarker, System.StringComparison.Ordinal);
        if (loopIdx > 0)
        {
            enemyName = enemyName.Substring(0, loopIdx);
        }
        enemyName = enemyName.Trim();

        string path = $"EnemyCutins/{enemyName}_cutin";
        cutinSprite = Resources.Load<Sprite>(path);
    }

    // スキル用カットイン Image にだけ設定する
    // （Inspector の画像をフォールバックとして使わないように、失敗時は null を入れる）
    if (enemySkillCutinImage != null)
    {
        if (cutinSprite != null)
        {
            enemySkillCutinImage.sprite = cutinSprite;
            enemySkillCutinImage.preserveAspect = true;
        }
        else
        {
            enemySkillCutinImage.sprite = null;
        }
    }

    // ===== テキスト設定 =====
    // 旧 UI(enemySkillCutinTextTMP) / 新 UI(enemySkillCutinLabelTMP) の両方に入れておく
    if (enemySkillCutinTextTMP != null)
    {
        enemySkillCutinTextTMP.text = skillDisplayName ?? string.Empty;
    }
    if (enemySkillCutinLabelTMP != null)
    {
        enemySkillCutinLabelTMP.text = skillDisplayName ?? string.Empty;
    }

    // UI が用意されていない場合は、軽く待ってロックだけ解除
    if (enemySkillCutinRoot == null)
    {
        yield return new WaitForSeconds(1.5f);
        _enemySkillCutinRunning = false;
        UpdateButtons();
        yield break;
    }

// ===== カットイン表示 =====

// ★追加：カットインが下のUI（スキップ等）をブロックしないように、raycastTarget を一時的に無効化
UnityEngine.UI.Graphic[] skillCutinGraphics = null;
bool[] prevRaycastTargets = null;
try
{
    skillCutinGraphics = enemySkillCutinRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
    prevRaycastTargets = new bool[skillCutinGraphics.Length];
    for (int i = 0; i < skillCutinGraphics.Length; i++)
    {
        prevRaycastTargets[i] = skillCutinGraphics[i].raycastTarget;
        skillCutinGraphics[i].raycastTarget = false;
    }
}
catch { }

// ===== カットイン表示 =====

skillCutinGraphics = null;
prevRaycastTargets = null;

try
{
    skillCutinGraphics = enemySkillCutinRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
    prevRaycastTargets = new bool[skillCutinGraphics.Length];
    for (int i = 0; i < skillCutinGraphics.Length; i++)
    {
        prevRaycastTargets[i] = skillCutinGraphics[i].raycastTarget;
        skillCutinGraphics[i].raycastTarget = false;
    }
}
catch { }
enemySkillCutinRoot.SetActive(true);

// ★カットインが「表示された瞬間」にSE（AudioManagerへ集約）
if (AudioManager.Instance)
{
    AudioManager.Instance.PlayCutin_EnemySkill();
}

// フェード用 CanvasGroup は「付いていれば使う」だけ。自動追加はしない。
CanvasGroup cg = enemySkillCutinRoot.GetComponent<CanvasGroup>();

// ★追加：CanvasGroup がある場合も raycast をブロックしないようにする
if (cg != null)
{
    cg.blocksRaycasts = false;
    cg.interactable = false;
}

    float fadeIn  = Mathf.Max(0.01f, matchStartFadeInDuration);
    float hold    = Mathf.Max(0f,     matchStartHoldDuration);
    float fadeOut = Mathf.Max(0.01f,  matchStartFadeOutDuration);

    if (cg != null)
    {
        // フェードイン
        cg.alpha = 0f;
        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        cg.alpha = 1f;

        // ホールド
        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);
        }

        // フェードアウト
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(1f - t / fadeOut);
            yield return null;
        }
        cg.alpha = 0f;
    }
    else
    {
        // CanvasGroup が無ければ、そのまま一定時間だけ表示（フェードなし）
        float total = fadeIn + hold + fadeOut;
        if (total > 0f)
        {
            yield return new WaitForSeconds(total);
        }
    }
enemySkillCutinRoot.SetActive(false);

// ★追加：raycastTarget を元に戻す
try
{
    if (skillCutinGraphics != null && prevRaycastTargets != null)
    {
        int n = Mathf.Min(skillCutinGraphics.Length, prevRaycastTargets.Length);
        for (int i = 0; i < n; i++)
        {
            if (skillCutinGraphics[i] != null)
                skillCutinGraphics[i].raycastTarget = prevRaycastTargets[i];
        }
    }
}
catch { }

// ★追加：raycastTarget を元に戻す
try
{
    if (skillCutinGraphics != null && prevRaycastTargets != null)
    {
        int n = Mathf.Min(skillCutinGraphics.Length, prevRaycastTargets.Length);
        for (int i = 0; i < n; i++)
        {
            if (skillCutinGraphics[i] != null)
                skillCutinGraphics[i].raycastTarget = prevRaycastTargets[i];
        }
    }
}
catch { }


    // 進行ロック解除
    _enemySkillCutinRunning = false;
    UpdateButtons();
}
// Utility: wait for given seconds then advance to player's draw if still appropriate
private IEnumerator BeginOfferAfterDelay(float seconds)
{
    // まず既存どおり一定時間待つ
    yield return new WaitForSeconds(seconds);

    // カットイン中は進行しない（既存仕様維持）
    while (_enemyRiichiCutinRunning || _enemySkillCutinRunning)
    {
        yield return null;
    }

// ★ここで「いまの lastEnemyTurnTiles から」行動可否を再計算して判断する
bool canRonNowLocal = false;
bool canCallNowLocal = false;

if (phase == Phase.EnemyTurn)
{
    canRonNowLocal = CanRonWithAny(lastEnemyTurnTiles, out _, out _, out _, out _, out _);

    // 鳴きも見る（敵捨て牌の正規化を揃える）
    bool canPon = lastEnemyTurnTiles.Any(t => CanPonWithBase(NormalizeEnemyDiscardForAction(t)));
    bool canChi = lastEnemyTurnTiles.Any(t => CanChiWithBase(NormalizeEnemyDiscardForAction(t)));
    bool canKan = lastEnemyTurnTiles.Any(t => CanKanWithBase(NormalizeEnemyDiscardForAction(t)));

    if (isRiichi) { canPon = false; canChi = false; canKan = false; } // 既存方針踏襲
    canCallNowLocal = (canPon || canChi || canKan);
}

// ★ロン or 鳴き があるなら進行しない（＝停止維持）
if (phase == Phase.EnemyTurn && (canRonNowLocal || canCallNowLocal))
    yield break;

// 候補が無い場合だけ進行
if (phase == Phase.EnemyTurn)
    BeginOfferPhase();

}
private void ProcessEnemyAttackEffects()
{
    if (DISABLE_LEGACY_ENEMY_DISCARD_EFFECTS)
    {
        // カウンタやUI整合だけ軽く合わせ、効果は一切発生させない
        int skillTurnCounter = _enemyTurnCounter;

        // ★追加：敵がこのターンにツモ和了する見込みなら、先に「和了ターン」としてマークしてスキル発動を抑止する
        if (EnemyAddon_WillEnemyWinThisEnemyTurn())
        {
            _enemyWinDeclaredTurnCounter = skillTurnCounter;
        }

        EnemySkills_OnEnemyTurn(skillTurnCounter);

        UpdateHpUI();

        StartCoroutine(BeginOfferAfterDelay(0.5f));
        return;
    }

    int skillTurnCounter2 = _enemyTurnCounter;

    // ★追加：敵がこのターンにツモ和了する見込みなら、先に「和了ターン」としてマークしてスキル発動を抑止する
    if (EnemyAddon_WillEnemyWinThisEnemyTurn())
    {
        _enemyWinDeclaredTurnCounter = skillTurnCounter2;
    }

    EnemySkills_OnEnemyTurn(skillTurnCounter2);

    int interval = Mathf.Max(1, enemyAttackIntervalTurns);

    if (_suppressEnemyEffectsOnce)
    {
        _suppressEnemyEffectsOnce = false;

        StartCoroutine(BeginOfferAfterDelay(0.5f));
        return;
    }

    if ((_enemyTurnCounter % interval) != 0)
    {
        StartCoroutine(BeginOfferAfterDelay(0.5f));
        return;
    }

    // （以降はあなたの既存処理のまま）
}


    private IEnumerator SpawnFloatingNumber(Transform tileTf, int amount, Color color, float duration = 2f)
{
    if (!tileTf) yield break;

    // Find the root canvas to place the floating text independent of layout groups
    Canvas canvas = tileTf.GetComponentInParent<Canvas>();
    if (!canvas) yield break;
    RectTransform canvasRT = canvas.transform as RectTransform;
    if (!canvasRT) yield break;

    RectTransform tileRT = tileTf as RectTransform;
    if (!tileRT) yield break;

    // Compute tile top-center in screen space
    Vector3[] corners = new Vector3[4];
    tileRT.GetWorldCorners(corners);
    Vector3 worldTopCenter = (corners[1] + corners[2]) * 0.5f; // top edge center (1=top-left, 2=top-right in Unity's order)
    Vector3 screenTopCenter = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldTopCenter);

    // Vertical offset so it does not overlap the tile (>=16 px)
    float tilePixelHeight = Vector3.Distance(
        RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[1]),
        RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[0])
    );
    float offsetY = Mathf.Max(16f, tilePixelHeight * 0.4f); // slightly above top edge
    Vector2 screenPosWithOffset = new Vector2(screenTopCenter.x, screenTopCenter.y + offsetY);

    // Convert to canvas local point
    Vector2 localPoint;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPosWithOffset, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);

    // Create object under canvas
    var go = new GameObject("FloatingValue", typeof(RectTransform));
    go.transform.SetParent(canvasRT, false);
    var grt = go.GetComponent<RectTransform>();

    // Centered, anchored by absolute canvas position
    grt.anchorMin = new Vector2(0.5f, 0.5f);
    grt.anchorMax = new Vector2(0.5f, 0.5f);
    grt.pivot     = new Vector2(0.5f, 0.5f);
    grt.anchoredPosition = localPoint;
    grt.sizeDelta = new Vector2(tileRT.rect.width, tileRT.rect.height);
    go.transform.SetAsLastSibling();

    var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
    tmp.raycastTarget = false;
    tmp.alignment = TMPro.TextAlignmentOptions.Center;
    tmp.fontSize = 42f;
    tmp.text = (amount >= 0 ? "+" : "") + amount.ToString();
    tmp.color = color;
    tmp.enableWordWrapping = false;

    float t = 0f;
    while (t < duration)
    {
        if (!grt || !tmp) break;
        float u = t / Mathf.Max(0.0001f, duration);
        var c = tmp.color; c.a = 1f - u; tmp.color = c;
        t += Time.deltaTime;
        yield return null;
    }

    if (go) GameObject.Destroy(go);
}

    


    // === Added helpers (compile-safe, no spec change) ===
    private enum EnemyEffectVfxKind { Damage, Heal, ScorePenalty }


private int MajoritySuitOnWin(System.Collections.Generic.List<string> concealed14)
{
    // 0=Man,1=Pin,2=Sou,3=Honors
    int man=0, pin=0, sou=0, hon=0;
    if (concealed14 != null)
    {
        foreach (var id in concealed14)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (TryParseSuitNum(id, out var s, out var _))
            {
                if (s == 0) man++;
                else if (s == 1) pin++;
                else if (s == 2) sou++;
            }
            else
            {
                hon++;
            }
        }
    }
    int suit = 0; int max = man;
    if (pin > max) { max = pin; suit = 1; }
    if (sou > max) { max = sou; suit = 2; }
    if (hon > max) { suit = 3; }
    return suit;
}
public bool CanAfford(int price) => price <= Mathf.Max(0, runGold);
public bool TrySpendGold(int price)
{
    if (price < 0) price = 0;
    if (runGold < price) return false;
    runGold -= price;
    SaveRunGold();
    RefreshTopUI();
    return true;
}

private System.Collections.IEnumerator FlashTileEffect(Transform tileTf, EnemyEffectVfxKind kind, float duration = 2f)
{
    if (!tileTf) yield break;
    var img = GetVisibleArtImage(tileTf);
    if (!img) img = tileTf.GetComponentInChildren<UnityEngine.UI.Image>(true);
    if (!img) yield break;

    // Color by kind
    Color target = Color.white;
    switch (kind)
    {
        case EnemyEffectVfxKind.Damage:      target = new Color(1f, 0.15f, 0.15f, 0.7f); break;
        case EnemyEffectVfxKind.Heal:        target = new Color(0.15f, 1f, 0.25f, 0.7f); break;
        case EnemyEffectVfxKind.ScorePenalty:target = new Color(0.7f, 0.2f, 1f, 0.7f);  break;
    }

    var origColor = img.color;
    // Optional outline if present
    var outline = img.GetComponent<UnityEngine.UI.Outline>();
    if (outline) { outline.enabled = true; }

    float t = 0f;
    while (t < duration)
    {
        if (!img || !tileTf) yield break;
        float u = t / Mathf.Max(0.0001f, duration);
        img.color = Color.Lerp(origColor, target, Mathf.Sin(u * Mathf.PI)); // pure color pulse, no jump
        t += Time.deltaTime;
        yield return null;
    }

    if (img) img.color = origColor;
    if (outline) outline.enabled = false;
}



    // === VFX: Enemy discard effects for the last window (1 sec) ===
    private System.Collections.IEnumerator PlayEnemyDiscardVFXForLastWindow()
{
    if (!enemyDiscardArea) yield break;
    int count = lastEnemyTurnTiles != null ? lastEnemyTurnTiles.Count : 0;
    if (count <= 0) yield break;

    // Wait briefly for UI to finish spawning (defensive; no spec change)
    int _tries = 0;
    while (enemyDiscardArea && enemyDiscardArea.childCount < Mathf.Min(count, enemyDiscards != null ? enemyDiscards.Count : 0) && _tries < 3)
    {
        _tries++;
        yield return null;
    }

    int listCount = enemyDiscards != null ? enemyDiscards.Count : 0;

    // Build per-ID quota for this window
    var isCurrentByList = new bool[listCount];
    var quota = new System.Collections.Generic.Dictionary<string, int>();
    for (int i = 0; i < count; i++)
    {
        var t = lastEnemyTurnTiles[i];
        if (string.IsNullOrEmpty(t)) continue;
        if (!quota.ContainsKey(t)) quota[t] = 0;
        quota[t]++;
    }
    for (int li = listCount - 1; li >= 0; li--)
    {
        var id = enemyDiscards[li];
        if (string.IsNullOrEmpty(id)) { isCurrentByList[li] = false; continue; }
        int q;
        if (quota.TryGetValue(id, out q) && q > 0)
        {
            isCurrentByList[li] = true;
            quota[id] = q - 1;
        }
    }
    var idxList = new System.Collections.Generic.List<int>(count);
    for (int li = 0; li < listCount; li++)
        if (isCurrentByList[li]) idxList.Add(li);

    float m = Mathf.Max(0f, enemyAttackMultiplier);

    int n = Mathf.Min(count, idxList.Count);
    for (int i = 0; i < n; i++)
    {
        string id = lastEnemyTurnTiles[i];
        if (string.IsNullOrEmpty(id)) continue;
        int suit, num;
        if (!TryParseSuitNum(id, out suit, out num)) continue;
        if (num < 1 || num > 9) continue;

        var tf = FindEnemyDiscardChildByListIndex(idxList[i]);
        if (!tf) continue;

        if (suit == 0) // Man -> player damage
        {
            int val = (enemyManDamageByNumber != null && enemyManDamageByNumber.Length > num) ? enemyManDamageByNumber[num] : 1;
            val = Mathf.Max(0, Mathf.RoundToInt(val * m));
            if (val > 0)
            {
                StartCoroutine(FlashTileEffect(tf, EnemyEffectVfxKind.Damage, 2f));
                StartCoroutine(SpawnFloatingNumber(tf, -val, new Color(1f, 0.2f, 0.2f, 1f), 2f));
            }
        }
        else if (suit == 1) // Pin -> enemy heal
        {
            int val = (enemyPinHealByNumber != null && enemyPinHealByNumber.Length > num) ? enemyPinHealByNumber[num] : 1;
            val = Mathf.Max(0, Mathf.RoundToInt(val * m));
            if (val > 0)
            {
                StartCoroutine(FlashTileEffect(tf, EnemyEffectVfxKind.Heal, 2f));
                StartCoroutine(SpawnFloatingNumber(tf, +val, new Color(0.2f, 1f, 0.3f, 1f), 2f));
            }
        }
        else if (suit == 2) // Sou -> next-win score penalty (toxic look)
        {
            int val = (enemySouTsumoPenaltyByNumber != null && enemySouTsumoPenaltyByNumber.Length > num) ? enemySouTsumoPenaltyByNumber[num] : 0;
            val = Mathf.Max(0, Mathf.RoundToInt(val * m));
            if (val > 0)
            {
                StartCoroutine(FlashTileEffect(tf, EnemyEffectVfxKind.ScorePenalty, 2f));
                StartCoroutine(SpawnFloatingNumber(tf, -val, new Color(0.7f, 0.2f, 1f, 1f), 2f));
            }
        }
    }
    yield return new WaitForSeconds(2.0f);
}

    // NEW: Apply VFX to all tiles in the current attack window (list indices resolved to actual UI children)
    private System.Collections.IEnumerator PlayEnemyDiscardVFXForAttackWindow(System.Collections.Generic.List<int> listIndices)
    {
        if (!enemyDiscardArea) yield break;
        if (listIndices == null || listIndices.Count == 0) yield break;

        // Wait up to a few frames for UI children to spawn (defensive; no spec change)
        int _tries = 0;
        while (enemyDiscardArea && enemyDiscardArea.childCount < enemyDiscards.Count && _tries < 3)
        {
            _tries++;
            yield return null;
        }

        float m = Mathf.Max(0f, enemyAttackMultiplier);

        for (int k = 0; k < listIndices.Count; k++)
        {
            int listIdx = listIndices[k];
            if (listIdx < 0 || listIdx >= enemyDiscards.Count) continue;
            string id = enemyDiscards[listIdx];
            if (string.IsNullOrEmpty(id)) continue;
            int suit, num;
            if (!TryParseSuitNum(id, out suit, out num)) continue;
            if (num < 1 || num > 9) continue;

            var tf = FindEnemyDiscardChildByListIndex(listIdx);
            if (!tf) continue;

            if (suit == 0) // Man -> player damage
            {
                int val = (enemyManDamageByNumber != null && enemyManDamageByNumber.Length > num) ? enemyManDamageByNumber[num] : 1;
                val = Mathf.Max(0, Mathf.RoundToInt(val * m));
                if (val > 0)
                {
                    StartCoroutine(FlashTileEffect(tf, EnemyEffectVfxKind.Damage, 2f));
                    StartCoroutine(SpawnFloatingNumber(tf, -val, new Color(1f, 0.2f, 0.2f, 1f), 2f));
                }
            }
            else if (suit == 1) // Pin -> enemy heal
            {
                int val = (enemyPinHealByNumber != null && enemyPinHealByNumber.Length > num) ? enemyPinHealByNumber[num] : 1;
                val = Mathf.Max(0, Mathf.RoundToInt(val * m));
                if (val > 0)
                {
                    StartCoroutine(FlashTileEffect(tf, EnemyEffectVfxKind.Heal, 2f));
                    StartCoroutine(SpawnFloatingNumber(tf, +val, new Color(0.2f, 1f, 0.3f, 1f), 2f));
                }
            }
            else if (suit == 2) // Sou -> next-win score penalty (toxic look)
            {
                int val = (enemySouTsumoPenaltyByNumber != null && enemySouTsumoPenaltyByNumber.Length > num) ? enemySouTsumoPenaltyByNumber[num] : 0;
                val = Mathf.Max(0, Mathf.RoundToInt(val * m));
                if (val > 0)
                {
                    StartCoroutine(FlashTileEffect(tf, EnemyEffectVfxKind.ScorePenalty, 2f));
                    StartCoroutine(SpawnFloatingNumber(tf, -val, new Color(0.7f, 0.2f, 1f, 1f), 2f));
                }
            }
        }
        yield return new WaitForSeconds(2.0f);
    }
// ===== Special Tile helpers (v2: rarity + legendary) =====
private static string StripStar(string id)
{
    if (string.IsNullOrEmpty(id)) return id;

    // 末尾/途中の '*' を落とす（既存仕様）
    int p = id.IndexOf('*');
    if (p >= 0) id = id.Substring(0, p);

    return id;
}
private enum SpecialTileRarity
{
    Normal,
    Common,
    Rare,
    Epic,
    Legendary
}
// 勝利牌プール（面前14 + 副露）に「レジェンダリー効果 Lx」が含まれる枚数を数える
private int CountLegendaryEffectTilesInScoringPool(IList<string> concealed14Raw, IList<IList<string>> openMeldsRaw, int effectIndex)
{
    int c = 0;

    if (concealed14Raw != null)
    {
        for (int i = 0; i < concealed14Raw.Count; i++)
        {
            if (TryGetLegendaryEffectIndex(concealed14Raw[i], out int idx) && idx == effectIndex)
                c++;
        }
    }

    if (openMeldsRaw != null)
    {
        foreach (var m in openMeldsRaw)
        {
            if (m == null) continue;
            for (int j = 0; j < m.Count; j++)
            {
                if (TryGetLegendaryEffectIndex(m[j], out int idx2) && idx2 == effectIndex)
                    c++;
            }
        }
    }

    return c;
}

// melds が List<List<string>> の場合の受け口（既存のキャスト方針と合わせる）
private int CountLegendaryEffectTilesInScoringPool(IList<string> concealed14Raw, List<List<string>> openMeldsRaw, int effectIndex)
{
    IList<IList<string>> casted = null;

    if (openMeldsRaw != null)
    {
        var tmp = new List<IList<string>>();
        for (int i = 0; i < openMeldsRaw.Count; i++)
        {
            tmp.Add(openMeldsRaw[i]);
        }
        casted = tmp;
    }

    return CountLegendaryEffectTilesInScoringPool(concealed14Raw, casted, effectIndex);
}

// "_sp" を含むものを「特別牌」とみなす（後ろに "_common" "_L1" 等が付いてもOK）
private static bool IsSpecialTileId(string rawId)
{
    rawId = StripStar(rawId);
    if (string.IsNullOrEmpty(rawId)) return false;
    return rawId.IndexOf("_sp", StringComparison.OrdinalIgnoreCase) >= 0;
}

// 例： "Pin5_sp", "Pin5_sp_common", "Pin5_sp_rare", "Pin5_sp_legendary_L1" などを想定
// "_sp" があるなら true を返す（レア度トークンが無ければ Normal 扱い）
private static bool TryGetSpecialTileRarity(string rawId, out SpecialTileRarity rarity)
{
    rarity = SpecialTileRarity.Normal;

    rawId = StripStar(rawId);
    if (string.IsNullOrEmpty(rawId)) return false;

    // 特別牌でなければ false
    if (!IsSpecialTileId(rawId)) return false;

    // "_" 分割して "sp" の次のトークン以降を読む
    var parts = rawId.Split('_');
    int spIndex = -1;
    for (int i = 0; i < parts.Length; i++)
    {
        if (string.Equals(parts[i], "sp", StringComparison.OrdinalIgnoreCase))
        {
            spIndex = i;
            break;
        }
    }

    // "_sp" という文字列は含むが、分割で sp が取れないケースは保険で Normal
    if (spIndex < 0) { rarity = SpecialTileRarity.Normal; return true; }

    for (int i = spIndex + 1; i < parts.Length; i++)
    {
        var t = parts[i];
        if (string.IsNullOrEmpty(t)) continue;

        if (string.Equals(t, "normal", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "n", StringComparison.OrdinalIgnoreCase))
        {
            rarity = SpecialTileRarity.Normal;
            return true;
        }
        if (string.Equals(t, "common", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "c", StringComparison.OrdinalIgnoreCase))
        {
            rarity = SpecialTileRarity.Common;
            return true;
        }
        if (string.Equals(t, "rare", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "r", StringComparison.OrdinalIgnoreCase))
        {
            rarity = SpecialTileRarity.Rare;
            return true;
        }
        if (string.Equals(t, "epic", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "e", StringComparison.OrdinalIgnoreCase))
        {
            rarity = SpecialTileRarity.Epic;
            return true;
        }
        if (string.Equals(t, "legendary", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "l", StringComparison.OrdinalIgnoreCase))
        {
            rarity = SpecialTileRarity.Legendary;
            return true;
        }

        // 例: "L1" "L2" ... が付いている場合、レジェンダリーとみなす
        if ((t.Length >= 2) && (t[0] == 'L' || t[0] == 'l'))
        {
            int n;
            if (int.TryParse(t.Substring(1), out n) && n > 0)
            {
                rarity = SpecialTileRarity.Legendary;
                return true;
            }
        }
    }

    // トークンが無ければ Normal
    rarity = SpecialTileRarity.Normal;
    return true;
}

// レジェンダリー専用効果番号を取得（"L1"～"L5" を想定）
// 付いていなければ false
private static bool TryGetLegendaryEffectIndex(string rawId, out int effectIndex)
{
    effectIndex = 0;

    rawId = StripStar(rawId);
    if (string.IsNullOrEmpty(rawId)) return false;

    if (!IsSpecialTileId(rawId)) return false;

    var parts = rawId.Split('_');
    for (int i = 0; i < parts.Length; i++)
    {
        var t = parts[i];
        if (string.IsNullOrEmpty(t)) continue;

        if ((t.Length >= 2) && (t[0] == 'L' || t[0] == 'l'))
        {
            int n;
            if (int.TryParse(t.Substring(1), out n) && n > 0)
            {
                effectIndex = n;
                return true;
            }
        }
    }
    return false;
}

private static int GetFuBonusForSpecialRarity(SpecialTileRarity rarity)
{
    switch (rarity)
    {
        case SpecialTileRarity.Common:     return 4;
        case SpecialTileRarity.Rare:       return 8;
        case SpecialTileRarity.Epic:       return 16;
        case SpecialTileRarity.Legendary:  return 32;
        case SpecialTileRarity.Normal:
        default:
            return 0;
    }
}

// 特別牌ドラ(+1)は「和了手牌(面前14枚) + 副露(melds)」に含まれる枚数ぶん
private int CountSpecialTileDoraBonusForScoring(IList<string> concealed14Raw, IList<IList<string>> openMeldsRaw)
{
    int count = 0;

    if (concealed14Raw != null)
    {
        for (int i = 0; i < concealed14Raw.Count; i++)
        {
            if (IsSpecialTileId(concealed14Raw[i])) count++;
        }
    }

    if (openMeldsRaw != null)
    {
        foreach (var m in openMeldsRaw)
        {
            if (m == null) continue;
            for (int j = 0; j < m.Count; j++)
            {
                if (IsSpecialTileId(m[j])) count++;
            }
        }
    }

    return count;
}
private int CountSpecialTileDoraBonusForScoring(IList<string> concealed14Raw, List<List<string>> openMeldsRaw)
{
    IList<IList<string>> casted = null;

    if (openMeldsRaw != null)
    {
        var tmp = new List<IList<string>>();
        for (int i = 0; i < openMeldsRaw.Count; i++)
        {
            tmp.Add(openMeldsRaw[i]); // List<string> は IList<string> を実装している
        }
        casted = tmp;
    }

    return CountSpecialTileDoraBonusForScoring(concealed14Raw, casted);
}
private int CountSpecialTileFuBonusForScoring(IList<string> concealed14Raw, List<List<string>> openMeldsRaw)
{
    IList<IList<string>> casted = null;

    if (openMeldsRaw != null)
    {
        var tmp = new List<IList<string>>();
        for (int i = 0; i < openMeldsRaw.Count; i++)
        {
            tmp.Add(openMeldsRaw[i]);
        }
        casted = tmp;
    }

    return CountSpecialTileFuBonusForScoring(concealed14Raw, casted);
}

// 特別牌の符ボーナスは「和了手牌(面前14枚) + 副露(melds)」に含まれる枚数ぶん加算
private int CountSpecialTileFuBonusForScoring(IList<string> concealed14Raw, IList<IList<string>> openMeldsRaw)
{
    int bonus = 0;

    if (concealed14Raw != null)
    {
        for (int i = 0; i < concealed14Raw.Count; i++)
        {
            if (TryGetSpecialTileRarity(concealed14Raw[i], out var r))
                bonus += GetFuBonusForSpecialRarity(r);
        }
    }

    if (openMeldsRaw != null)
    {
        foreach (var m in openMeldsRaw)
        {
            if (m == null) continue;
            for (int j = 0; j < m.Count; j++)
            {
                if (TryGetSpecialTileRarity(m[j], out var r))
                    bonus += GetFuBonusForSpecialRarity(r);
            }
        }
    }

    return bonus;
}

// 符の丸め（+符後に10符単位切り上げ）
// baseFu=25(七対子固定) で addFu=0 の時は 25 を維持。それ以外は通常の丸め。
private static int ApplySpecialFuBonusAndRoundUp(int baseFu, int addFu)
{
    int a = Mathf.Max(0, addFu);

    if (baseFu == 25 && a == 0)
        return 25;

    int fu = Mathf.Max(0, baseFu) + a;

    if (fu < 20) fu = 20;

    // 10符単位切り上げ
    int rounded = ((fu + 9) / 10) * 10;
    return rounded;
}


private void DoKakan(string baseId)
{
    // 手牌から1枚除去
    int idx = hand.FindIndex(h => StripStar(h) == baseId);
    if (idx < 0) { if (statusTMP) statusTMP.text = "加槓の対象が見つかりません"; return; }
    hand.RemoveAt(idx);
    // 既存のポン面子を4枚に拡張
    for (int mi = 0; mi < melds.Count; mi++)
    {
        var m = melds[mi];
        if (m != null && m.Count == 3)
        {
            string a = StripStar(m[0]); string b = StripStar(m[1]); string c = StripStar(m[2]);
            if (a == baseId && b == baseId && c == baseId)
            {
                m.Add(baseId);
                break;
            }
        }
    }
    RefreshMeldUI();
    // ドラ表示追加＋リンシャン1枚をオファーへ
    AddKanIndicator();
                    // ★お守り（レジェンダリー特殊）：ドラ表示牌をさらに +1
                try { Omamori_TryAddExtraDoraAfterKan(); } catch {}
    if (deck.Count > 0) { var t = deck.Pop(); offers.Add(t); }
    RefreshOfferUI();
    SortHand(); RefreshHandUI();
    selHand.Clear(); ResetSelectionsAndUI();
    if (statusTMP) statusTMP.text = "加槓：リンシャン牌を引きました";
    UpdateButtons();
}
// --- Dora helper (moved inside class) ---
private string NextDoraId(string indicator)
{
    if (string.IsNullOrEmpty(indicator)) return null;

    // "*" や "_sp_xxx" などを落として、ロジック用のIDに統一
    indicator = StripTileIdForLogic(indicator);
    if (string.IsNullOrEmpty(indicator)) return null;

    // Suited tiles (Man/Pin/Sou 1..9) だけをここで扱う
    if (TryParseSuitNum(indicator, out var suit, out int num) && suit != 3)
    {
        int next = num + 1;
        if (next > 9) next = 1;

        string suitStr = (suit == 0) ? "Man" : (suit == 1) ? "Pin" : "Sou";
        return suitStr + next;
    }

    // Honors（字牌）はここで進める
    switch (indicator)
    {
        case "East":  return "South";
        case "South": return "West";
        case "West":  return "North";
        case "North": return "East";
        case "White": return "Green"; // 發
        case "Green": return "Red";   // 中
        case "Red":   return "White"; // 白
    }

    return null;
}

// --- Run persistence helpers (minimal) ---
// applyBattleRecovery=true : 敵を倒して次の敵に進むときなど、対局ごとの回復量を反映させる
// applyBattleRecovery=false: 中断保存など、「その時点のHP/MP」をそのまま持ち越したい場合
public void PersistRunPlayerHP(bool applyBattleRecovery)
{
    try
    {
        // 現在装備中のSkillSetを取得（MPアドオン側でロード済み）
        var skillSet = GetEquippedSkillSet();

        // ベースは「今回の対局終了時点」のHP / MP
        int nextHp = Mathf.Max(0, playerHP);
        int nextMp = Mathf.Max(0, _mp);

        // SkillSet 側で「対局ごとの回復量」が設定されていれば加算
        if (applyBattleRecovery && skillSet != null)
        {
            int hpBonus = Mathf.Max(0, skillSet.recoverHpPerBattle);
            int mpBonus = Mathf.Max(0, skillSet.recoverMpPerBattle);

            if (hpBonus > 0)
            {
                nextHp = Mathf.Min(playerMaxHP, nextHp + hpBonus);
            }

            if (mpBonus > 0)
            {
                // MP の上限はお守り補正込みの実効最大MPにクランプ
                nextMp = Mathf.Min(EffectiveMaxMP(), nextMp + mpBonus);
            }
        }

        PlayerPrefs.SetInt("Run_PlayerHP", nextHp);
        PlayerPrefs.SetInt("Run_PlayerMP", nextMp);
        PlayerPrefs.Save();
    }
    catch {}
}

// 既存の呼び出しとの互換用（デフォルトは「対局ごとの回復量あり」）
public void PersistRunPlayerHP()
{
    PersistRunPlayerHP(true);
}
// GameManager.cs にある GetEquippedSkillSet() をこの内容に差し替え
private SkillSetAsset GetEquippedSkillSet()
{
    // MPアドオン（GameManager_SkillMP_Addon.cs）でロード済み
    return _skillSet;
}
// GameManager.cs 修正後 BuildRightInfoText

private string BuildRightInfoText()
{
    var sb = new System.Text.StringBuilder();

    // 実際に発動するスキル（メニュー保存を最優先）
    var active    = ResolveActiveSkillForMP();
    var skillName = active.ToString();

    // 表示名＋説明（ここは既に Asset から正しく取れている）
    sb.AppendLine($"<b>{GetActiveSkillDisplayName(active)}</b>");
    string desc = GetActiveSkillDescription(active);
    if (!string.IsNullOrEmpty(desc)) sb.AppendLine(desc);

    // ==========================
    // 撃／瞬／癒 の「該当役」を、装備中スキルに対応する SkillSetAsset から取得
    //  - SkillSetAsset.GetTraitYakuFor(activeSkillName) を使用
    //  - 見つからない場合だけ、従来どおり _skillSet.traitMap をフォールバック
    // ==========================
    var ge = new List<string>();
    var sh = new List<string>();
    var iy = new List<string>();

    try
    {
        SkillSetAsset hostSet = null;

        // 1) まず現在の _skillSet が、このスキルを持っているならそれを優先
        if (_skillSet != null && _skillSet.activeSkills != null &&
            _skillSet.activeSkills.Any(e =>
                e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase)))
        {
            hostSet = _skillSet;
        }

        // 2) 見つからなければ、Resources/SkillSets から所属 SkillSet を総当たり検索
        if (hostSet == null)
        {
            var allSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
            foreach (var s in allSets)
            {
                if (s == null || s.activeSkills == null) continue;

                var entry = s.activeSkills.FirstOrDefault(e =>
                    e != null &&
                    !string.IsNullOrEmpty(e.activeSkillName) &&
                    string.Equals(e.activeSkillName.Trim(), skillName, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    hostSet = s;
                    break;
                }
            }
        }
if (hostSet != null)
{
    // ★ 解放済みのみ取得する（初回は EnsureInitialTraitUnlocks() が効いて各1つだけ解放される）
    var yakuTuple = hostSet.GetUnlockedTraitYakuFor(skillName);

    if (yakuTuple.ge != null)
        ge = yakuTuple.ge
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

    if (yakuTuple.sh != null)
        sh = yakuTuple.sh
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

    if (yakuTuple.iy != null)
        iy = yakuTuple.iy
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
}

        // 3) 最後の保険：どうしても hostSet が見つからない場合だけ、従来どおり _skillSet.traitMap を使用
        else if (_skillSet != null && _skillSet.traitMap != null && _skillSet.traitMap.Count > 0)
        {
            ge = _skillSet.traitMap
                .Where(t => t != null && t.trait == SkillSetAsset.Trait.Geki)
                .Select(t => t.yakuName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            sh = _skillSet.traitMap
                .Where(t => t != null && t.trait == SkillSetAsset.Trait.Shun)
                .Select(t => t.yakuName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            iy = _skillSet.traitMap
                .Where(t => t != null && t.trait == SkillSetAsset.Trait.Iyu)
                .Select(t => t.yakuName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[RightInfo] GetTraitYakuFor failed for {skillName}: {ex.Message}");
    }

    // 該当役が 1つでもあれば「常に」表示（プレイヤーのスキル説明として固定）
    if ((ge.Count + sh.Count + iy.Count) > 0)
    {
        sb.AppendLine();
        sb.AppendLine("<b>撃の該当役</b>：" + (ge.Count > 0 ? string.Join(" / ", ge) : "なし"));
        sb.AppendLine("<b>瞬の該当役</b>：" + (sh.Count > 0 ? string.Join(" / ", sh) : "なし"));
        sb.AppendLine("<b>癒の該当役</b>：" + (iy.Count > 0 ? string.Join(" / ", iy) : "なし"));
    }

    return sb.ToString();
}

// === ここから追加（選んだスキルを確実に発動させる受け口） ===

// MPアドオン（GameManager_SkillMP_Addon.cs）の TryInvokeEquippedSkill() が最優先で探す入り口。
// ここに来る時点で「現在装備している ActiveSkill s」は確定済み。
private void UseActiveSkill(ActiveSkill s)
{
    DispatchSkillByName(s);
}

// 列挙名から既存の効果メソッドへディスパッチ（引数なしメソッドを優先）
private void DispatchSkillByName(ActiveSkill s)
{
    var name  = s.ToString();
    var flags = System.Reflection.BindingFlags.Instance
             |  System.Reflection.BindingFlags.Public
             |  System.Reflection.BindingFlags.NonPublic;

    // よくある命名パターンを優先して総当り
    string[] candidates =
    {
        name,                     // 例: RandomMan()
        "Skill_" + name,          // 例: Skill_RandomMan()
        "Use" + name,             // 例: UseRandomMan()
        "Use_" + name,            // 例: Use_RandomMan()
        "Cast" + name,            // 例: CastRandomMan()
        "Cast_" + name,           // 例: Cast_RandomMan()
        "Activate" + name,        // 例: ActivateRandomMan()
        "Activate_" + name,       // 例: Activate_RandomMan()
        "On" + name               // 例: OnRandomMan()
    };

    foreach (var cand in candidates)
    {
        var mi = GetType().GetMethod(cand, flags);
        if (mi != null && mi.GetParameters().Length == 0)
        {
            Debug.Log($"[SKILL_MP] Dispatch -> {mi.Name}");
            mi.Invoke(this, null);
            return;
        }
    }

    // 最後の手段：部分一致で拾う（引数なし）
    var fuzzy = GetType().GetMethods(flags)
        .FirstOrDefault(m => m.GetParameters().Length == 0 &&
                             m.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
    if (fuzzy != null)
    {
        Debug.Log($"[SKILL_MP] Dispatch fuzzy -> {fuzzy.Name}");
        fuzzy.Invoke(this, null);
        return;
    }

    Debug.LogWarning("[SKILL_MP] No skill method found for " + name + "  （OnClickSkill() にフォールバック）");

    // 旧フローにフォールバック（必要な場合のみ）
    var old = GetType().GetMethod("OnClickSkill", flags);
    old?.Invoke(this, null);
}

// === 追加ここまで ===
// ===== Currency (Run Gold) =====
[SerializeField] private int runGold = 0;        // ラン内の所持ゴールド（表示・購入に使用）
[SerializeField] private int scoreThisEnemy = 0; // 「この敵」で稼いだスコア（倒した瞬間にゴールド化）

private const string KeyRunGold   = "Run_Gold";
private const string KeyRunOfuda  = "RunOfuda";
private const string KeyRunOfudaJ = "RunOfuda_LastJSON";

private void ResetRunGold()
{
    int startGold = 0;

    try
    {
        if (GetEquippedSkill() == ActiveSkill.Capitalist)
        {
            startGold = 3000;
        }
    }
    catch
    {
        startGold = 0;
    }

    runGold = Mathf.Max(0, startGold);
    RunCurrency.Set(runGold);
}
private void LoadRunGold()
{
    runGold = RunCurrency.Get();
}

private void SaveRunGold()
{
    RunCurrency.Set(runGold);
}
private void ClearRunEphemeral()
{
    try
    {
        // 通貨
        runGold = 0;
        GameManager.RunCurrency.Set(0);
        PlayerPrefs.DeleteKey(KeyRunGold);

        // ★ミッション状態リセット
        try { MissionSystem.ResetForNewRun(); MissionSystem.ClearRunSeed(); } catch { }

        // お札（ラン中のみ有効）
        PlayerPrefs.DeleteKey(KeyRunOfuda);
        PlayerPrefs.DeleteKey(KeyRunOfudaJ);

        // ★追加：ラン中購入の恒常ボーナス（このラン限り）
        PlayerPrefs.DeleteKey("Run_HPBonus");
        PlayerPrefs.DeleteKey("Run_MPBonus");
        PlayerPrefs.DeleteKey("Run_SkillCastsBonus");

        // ★追加：強化画面の「購入のたびに値上げ」カウンタは Run 終了で必ずリセット
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_Buy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_RerollBuy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_Destroy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_RerollDestroy");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HpUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_MpUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_CastUp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HealHp");
        PlayerPrefs.DeleteKey("Run_UpgradeCostCount_HealMp");

        // ★追加：ローグライトの「該当役 解放/強化」をラン終了で初期化（保険）
        try
        {
            if (_skillSet != null) _skillSet.ResetAllTraitYakuProgress();
        }
        catch {}

        PlayerPrefs.Save();
    }
    catch {}
}

// 手牌タイルにフックを付与
private void DBG_AttachHandEditHook(GameObject tileGO, int handIndex)
{
    if (!tileGO) return;
    var hook = tileGO.GetComponent<DBG_HandEditHook>();
    if (!hook) hook = tileGO.AddComponent<DBG_HandEditHook>();
    hook.Init(this, handIndex);
}

private void Update()
{
    // 新InputSystem: ESC でメニュー開閉
    var kb = Keyboard.current;
    if (kb != null && kb.escapeKey.wasPressedThisFrame)
        ToggleMenuPanel(!isMenuOpen);

    // 実行中のみ
    if (!Application.isPlaying) return;

    if (!debugFeatureEnabled)
    {
        if (enableDebugMode)
        {
            enableDebugMode = false;
            DBG_HidePanel();
            RefreshHandUI();
        }
    }
    else
    {
        bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
        // 新 Input System（Player Settings の "Input System Package"）
        // 現仕様どおり F1 固定
        if (kb != null)
        {
            if (kb.f1Key.wasPressedThisFrame) togglePressed = true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        // 旧 Input Manager（"Input Manager (Old)"）
        if (UnityEngine.Input.GetKeyDown(debugToggleKey))
            togglePressed = true;
#endif

        if (togglePressed)
        {
            enableDebugMode = !enableDebugMode;
            if (!enableDebugMode) DBG_HidePanel();
            // ハンドUIを再生成してフックを付け直す
            RefreshHandUI();
        }
    }

    // ★追加：特別牌ポップアップは「どこでもクリック」で閉じる（ただしポップアップ自身の上は除外）
    if (specialTilePopupRoot && specialTilePopupRoot.activeSelf)
    {
#if ENABLE_INPUT_SYSTEM
        bool clicked = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        bool clicked = UnityEngine.Input.GetMouseButtonDown(0);
#endif
        if (clicked)
        {
            bool clickedOnPopup = false;

            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                {
                    var ped = new UnityEngine.EventSystems.PointerEventData(es);
#if ENABLE_INPUT_SYSTEM
                    var pos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)UnityEngine.Input.mousePosition;
#else
                    var pos = (Vector2)UnityEngine.Input.mousePosition;
#endif
                    ped.position = pos;

                    var results = new List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(ped, results);

                    foreach (var rr in results)
                    {
                        if (rr.gameObject == null) continue;
                        if (rr.gameObject.transform == specialTilePopupRoot.transform ||
                            rr.gameObject.transform.IsChildOf(specialTilePopupRoot.transform))
                        {
                            clickedOnPopup = true;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (!clickedOnPopup)
            {
                specialTilePopupRoot.SetActive(false);
                _specialTilePopupShownId = null;
            }
        }
    }
    // ===== Player Meld Layout: live update in PlayMode =====
    if (Application.isPlaying &&
        liveUpdatePlayerMeldLayoutInPlayMode &&
        IsCustomMeldLayoutReady())
    {
        int h = CalcPlayerMeldLayoutHash();
        if (h != _playerMeldLayoutLastHash)
        {
            _playerMeldLayoutLastHash = h;

            // Inspector変更を反映するため、副露UIを作り直す
            RefreshMeldUI();
        }
    }
}

// 指定インデックスの手牌を newId に差し替え（ReplaceHandAt を活用）
private void DBG_ReplaceHand(int index, string newId)
{
    if (!debugFeatureEnabled) return;
    if (!enableDebugMode) return;
    if (!allowDebugAnyPhase && phase != (Phase)Enum.Parse(typeof(Phase), "Offer")) return;
    if (string.IsNullOrEmpty(newId)) return;
    if (index < 0 || index >= hand.Count) return;

    // 既存の安全な置換ユーティリティ
    ReplaceHandAt(index, newId);

    // 勝敗UI/ボタン状態も更新（既存の安全な流れ）
    UpdateButtons();
    EvaluateWinUI_New();
}
// パネル（選牌UI）を表示
private void DBG_ShowPanelForIndex(int handIndex, Vector3 screenPos)
{
    _dbgEditingHandIndex = handIndex;
    if (!_dbgPanelRoot) _dbgPanelRoot = DBG_BuildPanel(); // 無ければ生成
    if (_dbgPanelRoot)
    {
        _dbgPanelRoot.gameObject.SetActive(true);

        // 固定位置＋固定サイズを使う（Inspectorで指定）
        if (dbgPanelUseFixedPosition)
        {
            _dbgPanelRoot.anchoredPosition = dbgPanelFixedAnchoredPosition;
            _dbgPanelRoot.sizeDelta = dbgPanelFixedSizeDelta;
            return;
        }

        // 従来どおり：クリック位置に出す（サイズは固定サイズを適用しておく）
        _dbgPanelRoot.sizeDelta = dbgPanelFixedSizeDelta;

        var canvas = _dbgPanelRoot.GetComponentInParent<Canvas>();
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out local);
        _dbgPanelRoot.anchoredPosition = local;
    }
}
// パネルを隠す
private void DBG_HidePanel()
{
    if (_dbgPanelRoot) _dbgPanelRoot.gameObject.SetActive(false);
    _dbgEditingHandIndex = -1;
}

// 簡易パネル生成（Man1-9 / Pin1-9 / Sou1-9 / Honors）
private RectTransform DBG_BuildPanel()
{
    if (_dbgPanelReady && _dbgPanelRoot) return _dbgPanelRoot;

    // 1) Canvas を確実に用意
    Canvas rootCanvas = null;
    if (dbgPanelParent) rootCanvas = dbgPanelParent.GetComponentInParent<Canvas>();
    if (!rootCanvas)
    {
        rootCanvas = FindObjectOfType<Canvas>();
        if (!rootCanvas)
        {
            var go = new GameObject("DBG_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rootCanvas = go.GetComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
    }
    // 2) EventSystem も確実に用意
    if (!FindObjectOfType<EventSystem>())
    {
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        // New Input System 使用時でも StandaloneInputModule は問題なし（旧入力を読まない）
    }

    // 3) 親Transformの決定
    RectTransform parentRT = dbgPanelParent ? dbgPanelParent :
                             (rootCanvas ? rootCanvas.transform as RectTransform : null);
    if (!parentRT) return null; // これ以上できない

    // 4) 既存のパネルがあれば流用
    if (_dbgPanelRoot) { _dbgPanelReady = true; return _dbgPanelRoot; }

    // 5) パネルの生成
    var panelGO = new GameObject("DBG_HandEditor", typeof(RectTransform), typeof(Image));
    _dbgPanelRoot = panelGO.GetComponent<RectTransform>();
    _dbgPanelRoot.SetParent(parentRT, false);
    _dbgPanelRoot.anchorMin = new Vector2(1f, 1f);
    _dbgPanelRoot.anchorMax = new Vector2(1f, 1f);
    _dbgPanelRoot.pivot     = new Vector2(1f, 1f);
    _dbgPanelRoot.anchoredPosition = new Vector2(-20f, -20f);
    _dbgPanelRoot.sizeDelta = new Vector2(520f, 620f);
    var bg = panelGO.GetComponent<Image>();
    bg.color = new Color(0f, 0f, 0f, 0.6f);
    panelGO.AddComponent<Mask>();

    // 6) ScrollView & Grid
    var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
    var scrollRT = scrollGO.GetComponent<RectTransform>();
    scrollRT.SetParent(_dbgPanelRoot, false);
    scrollRT.anchorMin = new Vector2(0f, 0f);
    scrollRT.anchorMax = new Vector2(1f, 1f);
    scrollRT.offsetMin = new Vector2(10f, 10f);
    scrollRT.offsetMax = new Vector2(-10f, -60f);
    scrollGO.GetComponent<Image>().color = new Color(0,0,0,0.25f);

    var contentGO = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
    var contentRT = contentGO.GetComponent<RectTransform>();
    contentRT.SetParent(scrollRT, false);
    contentRT.anchorMin = new Vector2(0f, 1f);
    contentRT.anchorMax = new Vector2(0f, 1f);
    contentRT.pivot     = new Vector2(0f, 1f);
    contentRT.anchoredPosition = Vector2.zero;

    var grid = contentGO.GetComponent<GridLayoutGroup>();
    grid.cellSize = new Vector2(80f, 100f);
    grid.spacing  = new Vector2(6f, 6f);
    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    grid.constraintCount = 5;
    _dbgGrid = grid;

    var sr = scrollGO.GetComponent<ScrollRect>();
    sr.content = contentRT;
    sr.horizontal = false;
    sr.vertical = true;
    sr.viewport = scrollRT;

    // 7) 閉じるボタン
    var closeGO = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
    var closeRT = closeGO.GetComponent<RectTransform>();
    closeRT.SetParent(_dbgPanelRoot, false);
    closeRT.anchorMin = new Vector2(1f, 0f);
    closeRT.anchorMax = new Vector2(1f, 0f);
    closeRT.pivot     = new Vector2(1f, 0f);
    closeRT.anchoredPosition = new Vector2(-10f, 10f);
    closeRT.sizeDelta = new Vector2(90f, 40f);
    closeGO.GetComponent<Image>().color = new Color(0.2f,0.2f,0.2f,0.9f);
    var btn = closeGO.GetComponent<Button>();
    btn.onClick.AddListener(()=> { if (_dbgPanelRoot) _dbgPanelRoot.gameObject.SetActive(false); });

    // 8) 牌ボタンを全種生成
    PopulateAllTilesForDebug(contentRT);

    _dbgPanelReady = true;
    return _dbgPanelRoot;
}


// ====== タイル側に付与する小さなフック ======
private class DBG_HandEditHook : MonoBehaviour, IPointerClickHandler
{
    private GameManager gm;
    private int index;

    public void Init(GameManager gm, int handIndex)
    {
        this.gm = gm; this.index = handIndex;
    }

public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
{
    if (!gm || !gm.debugFeatureEnabled || !gm.enableDebugMode) return;

    // Offerのみ許可の場合のガード
    if (!gm.allowDebugAnyPhase)
    {
        // GameManager.Phase は private enum なので、フェーズ文字列でチェック
        // （同一クラス内なので直接アクセス可）
        if (gm.phase.ToString() != "Offer") return;
    }

    // クリック位置にパネルを出す
    gm.DBG_ShowPanelForIndex(index, eventData.position);
}
}

// ==== Debug Hand Editor (safe defaults) ====
[SerializeField] private RectTransform dbgPanelParent;          // ここに指定があれば優先配置
[SerializeField] private GameObject   dbgTileButtonPrefab;      // 任意のButtonプレハブ（あれば使う）
private GridLayoutGroup _dbgGrid;
private bool _dbgPanelReady;

// デバッグ手牌編集パネルの表示位置を固定する設定
[SerializeField] private bool dbgPanelUseFixedPosition = true;

// 固定座標（親のRectTransform基準のanchoredPosition）
[SerializeField] private Vector2 dbgPanelFixedAnchoredPosition = new Vector2(-20f, -20f);

// 固定サイズ（_dbgPanelRoot.sizeDelta に適用）
[SerializeField] private Vector2 dbgPanelFixedSizeDelta = new Vector2(520f, 620f);
private static readonly string[] _ALL_TILES =
{
    // Man
    "Man1","Man2","Man3","Man4","Man5","Man6","Man7","Man8","Man9",
    // Pin
    "Pin1","Pin2","Pin3","Pin4","Pin5","Pin6","Pin7","Pin8","Pin9",
    // Sou
    "Sou1","Sou2","Sou3","Sou4","Sou5","Sou6","Sou7","Sou8","Sou9",
    // Honors
    "East","South","West","North","White","Green","Red"
};

private void PopulateAllTilesForDebug(RectTransform contentRT)
{
    if (!contentRT) return;

    foreach (Transform c in contentRT) Destroy(c.gameObject);

    foreach (var id in _ALL_TILES)
    {
        GameObject b;
        if (dbgTileButtonPrefab)
        {
            b = Instantiate(dbgTileButtonPrefab, contentRT);
        }
        else
        {
            // シンプルなボタンをコードで生成
            b = new GameObject($"BTN_{id}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = b.GetComponent<RectTransform>();
            rt.SetParent(contentRT, false);
            rt.sizeDelta = new Vector2(80f, 100f);

            // テキスト表示（TMP が無くても動くように両対応）
            TMPro.TextMeshProUGUI tmp = null;
            try
            {
                var textGO = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                var trt = textGO.GetComponent<RectTransform>();
                trt.SetParent(rt, false);
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(78f, 98f);
                tmp = textGO.GetComponent<TMPro.TextMeshProUGUI>();
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 24f;
                tmp.text = id;
            }
            catch
            {
                var textGO = new GameObject("LegacyText", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                var trt = textGO.GetComponent<RectTransform>();
                trt.SetParent(rt, false);
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(78f, 98f);
                var lt = textGO.GetComponent<UnityEngine.UI.Text>();
                lt.alignment = TextAnchor.MiddleCenter;
                lt.fontSize = 18;
                lt.text = id;
                lt.color = Color.white;
            }

            // Sprite（あれば差し替え）
            try
            {
                var sp = Resources.Load<Sprite>($"Sprites/Tiles/{id}");
                if (sp) b.GetComponent<Image>().sprite = sp;
            } catch {}
        }

        // クリック → 「選択中の手牌スロット」をこの牌に置き換え
        var button = b.GetComponent<Button>();
        if (button == null) button = b.AddComponent<Button>();
        string capturedId = id;
button.onClick.AddListener(() =>
{
    int hi = _dbgEditingHandIndex;                 // ← ここを統一
    if (hi >= 0 && hi < hand.Count)
    {
        ReplaceHandAt(hi, capturedId);             // 任意牌に置換
        UpdateButtons();
        EvaluateWinUI_New();
    }
    // パネルは閉じなくても良ければこのまま。閉じたいなら DBG_HidePanel();
});
    }

    // コンテンツ高さをだいたいで確保（GridLayout が自動で広げやすいように）
    LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
}
private int _dbgLastPickedHandIndex = -1;

// === 共通ラン通貨（GOLD） ===
// PlayerPrefs("RunGold") に保存して Upgrade/戦闘どちらのシーンからも同じ値を使う。
public static class RunCurrency
{
    private const string Key = "RunGold";

    public static int Get()
        => UnityEngine.PlayerPrefs.GetInt(Key, 0);

    public static void Set(int value)
    {
        int v = Mathf.Max(0, value);
        UnityEngine.PlayerPrefs.SetInt(Key, v);
        UnityEngine.PlayerPrefs.Save();
    }

    public static bool Spend(int cost)
    {
        if (cost <= 0) return true;
        int g = Get();
        if (g < cost) return false;
        Set(g - cost);
        return true;
    }

    public static void Add(int add)
    {
        if (add <= 0) return;
        Set(Get() + add);
    }
}
// ===== In-Game Deck View (same as UpgradeScene) =====
[Header("Deck View (In-Game)")]
[SerializeField] private GameObject inGameDeckPanel;        // 画面全体を覆う
[SerializeField] private RectTransform inGameDeckPanelRoot;  // コンテンツ領域（自動生成）
[Header("Deck View (In-Game MANUAL)")]
[SerializeField] private bool inGameDeckUseManualUI = false;
[SerializeField] private GameObject manualInGameDeckPanel;
[SerializeField] private Button manualInGameDeckCloseButton;
[SerializeField] private Image[] manualInGameDeckTileIcons = new Image[34];
[SerializeField] private TMP_Text[] manualInGameDeckTileCounts = new TMP_Text[34];

// スタイル（任意）
[SerializeField] private TMP_FontAsset inGameDeckFont;
[SerializeField] private int   inGameDeckFontSize = 28;
[SerializeField] private int   inGameRowSpacing   = 24;
[SerializeField] private int   inGameCellSpacing  = 10;
[SerializeField] private float inGameTileAspectWH = 47f / 63f;
[SerializeField] private float inGameRowLabelWidth= 64f;

// ラベルや背景など
[SerializeField] private string[] inGameSuitRowLabels = new string[4] { "萬","筒","索","字" };
[SerializeField] private Sprite    inGamePanelBgSprite;
[SerializeField] private Color     inGamePanelBgColor = Color.black;
[SerializeField] private Image.Type inGamePanelBgType = Image.Type.Sliced;

[SerializeField] private bool     inGameTileAreaFixed = false;
[SerializeField] private Vector2  inGameTileAreaFixedSize = new Vector2(0,0);
[SerializeField] private Vector4  inGameTileAreaPaddingLRBT = new Vector4(20,20,20,80);

[SerializeField] private Sprite   inGameOkSprite;
[SerializeField] private Vector2  inGameOkSize = new Vector2(220,60);
[SerializeField] private string   inGameOkText = "OK";

// 内部
private GridLayoutGroup[] _igRowGrids = new GridLayoutGroup[4];
private TMP_Text[] _igRowLabelTexts = new TMP_Text[4];
private readonly int[] _igRowCols = new int[]{ 9,9,9,7 };
private Image[] _igManIcons, _igPinIcons, _igSouIcons, _igHonorIcons;
private TMP_Text[] _igManCount, _igPinCount, _igSouCount, _igHonorCount;
private Button _igOkButton;
private bool _igBuilt = false;
public void OnClickToggleDeckPanel_InGame()
{
    EnsureInGameDeckBuilt();

    var panel = GetActiveInGameDeckPanelGO();
    if (!panel) return;

    bool next = !panel.activeSelf;
    panel.SetActive(next);
    if (next)
    {
        panel.transform.SetAsLastSibling();
        IG_RefreshCounts();
        IG_RefreshIcons();
        IG_Reflow();
    }
}

private GameObject GetActiveInGameDeckPanelGO()
{
    if (inGameDeckUseManualUI && manualInGameDeckPanel) return manualInGameDeckPanel;
    return inGameDeckPanel;
}
private void EnsureInGameDeckBuilt()
{
    if (inGameDeckUseManualUI)
    {
        if (_igBuilt && manualInGameDeckPanel && manualInGameDeckPanel.activeInHierarchy) return;

        if (!manualInGameDeckPanel) return;

        if (manualInGameDeckCloseButton)
        {
            manualInGameDeckCloseButton.onClick.RemoveAllListeners();
            manualInGameDeckCloseButton.onClick.AddListener(() =>
            {
                if (manualInGameDeckPanel) manualInGameDeckPanel.SetActive(false);
            });
        }

        if (manualInGameDeckTileIcons != null && manualInGameDeckTileIcons.Length >= 34 &&
            manualInGameDeckTileCounts != null && manualInGameDeckTileCounts.Length >= 34)
        {
            _igManIcons = new Image[9];
            _igManCount = new TMP_Text[9];
            _igPinIcons = new Image[9];
            _igPinCount = new TMP_Text[9];
            _igSouIcons = new Image[9];
            _igSouCount = new TMP_Text[9];
            _igHonorIcons = new Image[7];
            _igHonorCount = new TMP_Text[7];

            for (int i = 0; i < 9; i++)
            {
                _igManIcons[i] = manualInGameDeckTileIcons[i];
                _igManCount[i] = manualInGameDeckTileCounts[i];

                _igPinIcons[i] = manualInGameDeckTileIcons[9 + i];
                _igPinCount[i] = manualInGameDeckTileCounts[9 + i];

                _igSouIcons[i] = manualInGameDeckTileIcons[18 + i];
                _igSouCount[i] = manualInGameDeckTileCounts[18 + i];
            }
            for (int i = 0; i < 7; i++)
            {
                _igHonorIcons[i] = manualInGameDeckTileIcons[27 + i];
                _igHonorCount[i] = manualInGameDeckTileCounts[27 + i];
            }
        }

        _igBuilt = true;
        return;
    }

    // ===== AUTO: 既存の自動生成（元の処理を維持）=====
    if (_igBuilt && inGameDeckPanel && inGameDeckPanel.activeInHierarchy) return;

    // パネル（無ければCanvas直下）
    if (!inGameDeckPanel)
    {
        var cv = GameObject.FindObjectOfType<Canvas>();
        if (!cv) return;

        inGameDeckPanel = new GameObject("DeckPanel(InGame)", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)inGameDeckPanel.transform;
        rt.SetParent(cv.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = inGameDeckPanel.GetComponent<Image>();
        bg.sprite = inGamePanelBgSprite;
        bg.type   = inGamePanelBgType;
        bg.color  = inGamePanelBgSprite ? Color.white : inGamePanelBgColor;
        inGameDeckPanel.SetActive(false);
    }
    else
    {
        var rt = inGameDeckPanel.GetComponent<RectTransform>() ?? inGameDeckPanel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = inGameDeckPanel.GetComponent<Image>() ?? inGameDeckPanel.AddComponent<Image>();
        bg.sprite = inGamePanelBgSprite;
        bg.type   = inGamePanelBgType;
        bg.color  = inGamePanelBgSprite ? Color.white : inGamePanelBgColor;
    }

    // Content
    RectTransform contentRT;
    {
        var existed = inGameDeckPanel.transform.Find("Content") as RectTransform;
        contentRT = existed ? existed : new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        contentRT.SetParent(inGameDeckPanel.transform, false);

        if (inGameTileAreaFixed && inGameTileAreaFixedSize.x > 0 && inGameTileAreaFixedSize.y > 0)
        {
            contentRT.anchorMin = contentRT.anchorMax = new Vector2(0.5f,0.5f);
            contentRT.pivot = new Vector2(0.5f,0.5f);
            contentRT.sizeDelta = inGameTileAreaFixedSize;
            contentRT.anchoredPosition = Vector2.zero;
        }
        else
        {
            var p = inGameTileAreaPaddingLRBT; // L,R,T,B
            contentRT.anchorMin = new Vector2(0,0);
            contentRT.anchorMax = new Vector2(1,1);
            contentRT.offsetMin = new Vector2(p.x, p.w);
            contentRT.offsetMax = new Vector2(-p.y, -p.z);
        }
    }
    inGameDeckPanelRoot = contentRT;

    // クリア & 縦レイアウト
    IG_ClearChildren(inGameDeckPanelRoot);
    var vlg = inGameDeckPanelRoot.GetComponent<VerticalLayoutGroup>() ?? inGameDeckPanelRoot.gameObject.AddComponent<VerticalLayoutGroup>();
    vlg.spacing = inGameRowSpacing;
    vlg.childAlignment = TextAnchor.UpperLeft;
    vlg.childControlWidth = false; vlg.childControlHeight = false;
    vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

    // 行を構築
    string L(int i, string def) =>
        (inGameSuitRowLabels != null && inGameSuitRowLabels.Length > i && !string.IsNullOrEmpty(inGameSuitRowLabels[i])) ? inGameSuitRowLabels[i] : def;

    IG_BuildRow(0, L(0,"萬"),  0, 9);
    IG_BuildRow(1, L(1,"筒"),  9, 9);
    IG_BuildRow(2, L(2,"索"), 18, 9);
    IG_BuildRow(3, L(3,"字"), 27, 7);

    // OKボタン
    if (!_igOkButton)
    {
        var btnGO = new GameObject("Button_OK", typeof(RectTransform), typeof(Image), typeof(Button));
        var brt = (RectTransform)btnGO.transform;
        brt.SetParent(inGameDeckPanel.transform, false);
        brt.anchorMin = new Vector2(0.5f,0f);
        brt.anchorMax = new Vector2(0.5f,0f);
        brt.pivot     = new Vector2(0.5f,0f);
        brt.anchoredPosition = new Vector2(0,10);
        brt.sizeDelta = inGameOkSize;

        var img = btnGO.GetComponent<Image>();
        img.sprite = inGameOkSprite;
        img.type   = Image.Type.Sliced;
        img.color  = inGameOkSprite ? Color.white : Color.white;

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var lrt = (RectTransform)labelGO.transform;
        lrt.SetParent(btnGO.transform, false);
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f,0.5f);
        lrt.sizeDelta = new Vector2(Mathf.Max(200f, inGameOkSize.x-20f), Mathf.Max(40f, inGameOkSize.y-20f));
        var lbl = labelGO.GetComponent<TextMeshProUGUI>();
        if (inGameDeckFont) lbl.font = inGameDeckFont;
        lbl.fontSize = Mathf.Max(24, inGameDeckFontSize);
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.text = string.IsNullOrEmpty(inGameOkText)? "OK" : inGameOkText;

        _igOkButton = btnGO.GetComponent<Button>();
        _igOkButton.onClick.RemoveAllListeners();
        _igOkButton.onClick.AddListener(()=> { if (inGameDeckPanel) inGameDeckPanel.SetActive(false); });
    }

    _igBuilt = true;
    IG_Reflow();
}
private void IG_Reflow()
{
    if (inGameDeckUseManualUI) return;
    if (!inGameDeckPanelRoot) return;

    LayoutRebuilder.ForceRebuildLayoutImmediate(inGameDeckPanelRoot);
    var area = inGameDeckPanelRoot.rect;
    if (area.width <= 0 || area.height <= 0) return;

    float colsMax = 9f;
    float horizontalSpace = area.width - inGameRowLabelWidth - (colsMax - 1) * inGameCellSpacing;
    float cellW_ByWidth = Mathf.Floor(horizontalSpace / colsMax);

    float rows = 4f;
    float countTextH = Mathf.Max(18f, inGameDeckFontSize - 6f);
    float verticalSpace = area.height - (rows - 1) * inGameRowSpacing;

    float iconH_ByWidth = cellW_ByWidth / Mathf.Max(0.1f, inGameTileAspectWH);
    float cellH_ByWidth = iconH_ByWidth + countTextH + 6f;
    float cellH_ByHeight = Mathf.Floor(verticalSpace / rows);

    if (cellH_ByWidth > cellH_ByHeight)
    {
        float iconH = cellH_ByHeight - (countTextH + 6f);
        float cellW = Mathf.Floor(iconH * inGameTileAspectWH);
        IG_ApplyCell(new Vector2(cellW, cellH_ByHeight), iconH, Mathf.RoundToInt(Mathf.Clamp(iconH*0.22f, 16f, 28f)));
    }
    else
    {
        float iconH = iconH_ByWidth;
        IG_ApplyCell(new Vector2(cellW_ByWidth, cellH_ByWidth), iconH, Mathf.RoundToInt(Mathf.Clamp(iconH*0.22f, 16f, 28f)));
    }
    LayoutRebuilder.ForceRebuildLayoutImmediate(inGameDeckPanelRoot);
}

private void IG_ApplyCell(Vector2 cellSizeXY, float iconH, int countFont)
{
    for (int r = 0; r < _igRowGrids.Length; r++)
    {
        var g = _igRowGrids[r]; if (!g) continue;
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = _igRowCols[r];
        g.cellSize = cellSizeXY;
        g.spacing = new Vector2(inGameCellSpacing, 6f);
    }

    void SetIcons(Image[] arr)
    {
        if (arr == null) return;
        float iconW = iconH * inGameTileAspectWH;
        for (int i = 0; i < arr.Length; i++)
        {
            var img = arr[i]; if (!img) continue;
            var rt = (RectTransform)img.transform;
            rt.sizeDelta = new Vector2(iconW, iconH);
        }
    }
    SetIcons(_igManIcons); SetIcons(_igPinIcons); SetIcons(_igSouIcons); SetIcons(_igHonorIcons);

    void SetCounts(TMP_Text[] arr)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            var t = arr[i]; if (!t) continue;
            t.fontSize = countFont;
        }
    }
    SetCounts(_igManCount); SetCounts(_igPinCount); SetCounts(_igSouCount); SetCounts(_igHonorCount);

    for (int i = 0; i < _igRowLabelTexts.Length; i++)
        if (_igRowLabelTexts[i]) _igRowLabelTexts[i].fontSize = Mathf.Clamp(countFont + 6, 20, 36);
}

private void IG_BuildRow(int rowIndex, string label, int startIdx, int length)
{
    var rowGO = new GameObject($"Row_{label}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
    var rowRT = (RectTransform)rowGO.transform;
    rowRT.SetParent(inGameDeckPanelRoot, false);

    var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
    hlg.spacing = inGameCellSpacing;
    hlg.childAlignment = TextAnchor.UpperLeft;
    hlg.childControlWidth = false; hlg.childControlHeight = false;
    hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

    var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
    var labelRT = (RectTransform)labelGO.transform; labelRT.SetParent(rowRT, false);
    var tmp = labelGO.GetComponent<TextMeshProUGUI>();
    if (inGameDeckFont) tmp.font = inGameDeckFont;
    tmp.fontSize = inGameDeckFontSize;
    tmp.text = label;
    tmp.alignment = TextAlignmentOptions.MidlineRight;
    var le = labelGO.GetComponent<LayoutElement>();
    le.minWidth = le.preferredWidth = inGameRowLabelWidth;
    _igRowLabelTexts[rowIndex] = tmp;

    var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
    var gridRT = (RectTransform)gridGO.transform; gridRT.SetParent(rowRT, false);
    var grid = gridGO.GetComponent<GridLayoutGroup>();
    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
    grid.constraintCount = length;
    grid.spacing = new Vector2(inGameCellSpacing, 6);
    grid.childAlignment = TextAnchor.UpperLeft;
    grid.startAxis = GridLayoutGroup.Axis.Horizontal;
    _igRowGrids[rowIndex] = grid;

    if (rowIndex == 0) { _igManIcons = new Image[9]; _igManCount = new TMP_Text[9]; }
    if (rowIndex == 1) { _igPinIcons = new Image[9]; _igPinCount = new TMP_Text[9]; }
    if (rowIndex == 2) { _igSouIcons = new Image[9]; _igSouCount = new TMP_Text[9]; }
    if (rowIndex == 3) { _igHonorIcons = new Image[7]; _igHonorCount = new TMP_Text[7]; }

    for (int i = 0; i < length; i++)
    {
        int tileIndex = startIdx + i;

        var cell = new GameObject($"Cell_{tileIndex}", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var cellRT = (RectTransform)cell.transform; cellRT.SetParent(gridRT, false);
        var v = cell.GetComponent<VerticalLayoutGroup>();
        v.spacing = 6;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = false; v.childControlHeight = false;
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;

        var imgGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var imgRT = (RectTransform)imgGO.transform; imgRT.SetParent(cellRT, false);
        var img = imgGO.GetComponent<Image>(); img.preserveAspect = true;

        var txtGO = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        var txtRT = (RectTransform)txtGO.transform; txtRT.SetParent(cellRT, false);
        var t = txtGO.GetComponent<TextMeshProUGUI>();
        if (inGameDeckFont) t.font = inGameDeckFont;
        t.alignment = TextAlignmentOptions.Center;

        if (rowIndex == 0) { _igManIcons[i] = img; _igManCount[i] = t; }
        if (rowIndex == 1) { _igPinIcons[i] = img; _igPinCount[i] = t; }
        if (rowIndex == 2) { _igSouIcons[i] = img; _igSouCount[i] = t; }
        if (rowIndex == 3) { _igHonorIcons[i] = img; _igHonorCount[i] = t; }
    }
}

private void IG_RefreshCounts()
{
    var c = PlayerData.GetDeckCountsCopy();
    if (_igManCount != null)   for (int i=0; i<Mathf.Min(9,_igManCount.Length);   i++) if (_igManCount[i])   _igManCount[i].text   = c[i].ToString();
    if (_igPinCount != null)   for (int i=0; i<Mathf.Min(9,_igPinCount.Length);   i++) if (_igPinCount[i])   _igPinCount[i].text   = c[9+i].ToString();
    if (_igSouCount != null)   for (int i=0; i<Mathf.Min(9,_igSouCount.Length);   i++) if (_igSouCount[i])   _igSouCount[i].text   = c[18+i].ToString();
    if (_igHonorCount != null) for (int i=0; i<Mathf.Min(7,_igHonorCount.Length); i++) if (_igHonorCount[i]) _igHonorCount[i].text = c[27+i].ToString();
}

private void IG_RefreshIcons()
{
    IG_ApplyIcons(_igManIcons,0);
    IG_ApplyIcons(_igPinIcons,9);
    IG_ApplyIcons(_igSouIcons,18);
    IG_ApplyIcons(_igHonorIcons,27);
}

private void IG_ApplyIcons(Image[] row, int startIndex)
{
    if (row == null) return;
    for (int i = 0; i < row.Length; i++)
    {
        var img = row[i]; if (!img) continue;
        int idx = startIndex + i;
        var sp = LoadTileSpriteByIndex(idx); // UpgradeManager と同じローダを使用
        img.enabled = (sp != null);
        img.sprite  = sp;
        img.preserveAspect = true;
    }
}

private void IG_ClearChildren(Transform t)
{
    if (!t) return;
    for (int i = t.childCount - 1; i >= 0; i--)
        Destroy(t.GetChild(i).gameObject);
}
// GameManager.cs （クラス GameManager の中に追加）
// 0..33 の牌インデックスから対応スプライトを取得する簡易ヘルパ。
// Resources/Sprites/Tiles/<牌ID> を読み込みます（例: "Man1", "Pin9", "East" など）。
private Sprite LoadTileSpriteByIndex(int index)
{
    string id = IndexToId(index);              // 既存のヘルパ（public static string IndexToId(int)）
    if (string.IsNullOrEmpty(id)) return null;
    return Resources.Load<Sprite>($"Sprites/Tiles/{id}");
}

private void AddRunItem(string id)
{
    if (string.IsNullOrEmpty(id)) return;
    if (runItemIds.Add(id))
    {
        SaveRunItems(); // PlayerPrefs へ保存
    }
}
private void ShowCallChoiceButtons(string baseTile, bool showPon, bool showChi, bool showKan)
{
    // 自動生成は廃止：手動UI（btnPon/btnChi/btnKan）で表示する
    _pendingCallPon = showPon;
    _pendingCallChi = showChi;
    _pendingCallKan = showKan;

    callBaseTile = baseTile;
    callMode = CallMode.None;
    selHand.Clear();

    // 「鳴き種類を選ぶ段階」も ChoosingCall を使う（callMode==None で判別）
    phase = Phase.ChoosingCall;

    UpdateButtons();
}

private void ClearCallChoiceButtons()
{
    _pendingCallPon = false;
    _pendingCallChi = false;
    _pendingCallKan = false;

    // 鳴き種類選択段階（callMode==None）の途中キャンセルを戻す
    if (phase == Phase.ChoosingCall && callMode == CallMode.None)
    {
        callBaseTile = null;
        selectedEnemyIndex = -1;
        phase = Phase.EnemyTurn;
    }
}

private IEnumerator __RinshanToHandFlow(float delayBeforeDraw, float delayBeforeSort, Phase next, string statusAfter)
{
    // 0.5s 待機 → 手牌右端に追加
    yield return new WaitForSeconds(delayBeforeDraw);

    if (deck.Count > 0)
    {
        var rinshan = deck.Pop();
        hand.Add(rinshan);          // ★右端（未ソート）に入る
        RefreshHandUI();
        UpdateButtons();
    }

    // さらに 0.5s 後に並び替え
    yield return new WaitForSeconds(delayBeforeSort);

    SortHand();
    RefreshHandUI();

    // 進行フェーズを確定
    phase = next;
    UpdateButtons();

    if (statusTMP && !string.IsNullOrEmpty(statusAfter))
        statusTMP.text = statusAfter;
}
private void RevealUraDoraIfEligible()
{
    // このフラグは OnClickWin 内で isRiichi の時だけ true にしてから呼ぶ運用
    if (!_includeUraForScoring) return;

    // ★重要：同一局内で、裏ドラ表示牌は「最初に確保したもの」を使い回す（2回目以降は再抽選しない）
    if (uraIndicators != null && uraIndicators.Count > 0) return;

    uraIndicators.Clear();

    int need = (doraIndicators != null) ? doraIndicators.Count : 0;
    if (need <= 0) return;

    // まずは局開始時/カン時に確保しておいた保留分から供給
    for (int i = 0; i < need && i < _uraIndicatorPool.Count; i++)
        uraIndicators.Add(_uraIndicatorPool[i]);

    // 念のため保険：保留が足りない場合は従来通り deck から補完（通常運用では発生しない想定）
    for (int i = uraIndicators.Count; i < need && deck.Count > 0; i++)
        uraIndicators.Add(deck.Pop());
}

// 牌集合 pool（面前14枚+副露）に対し、与えられた「表示牌リスト（表/裏）」のドラ進行でヒット枚数を数える。
private int CountDoraHits(IList<string> concealedSorted14, IList<IList<string>> openMelds, IList<string> indicators)
{
    if (indicators == null || indicators.Count == 0) return 0;

    var pool = new List<string>(concealedSorted14 ?? Array.Empty<string>());
    if (openMelds != null)
        foreach (var m in openMelds)
            if (m != null) pool.AddRange(m);

    int hits = 0;

foreach (var indRaw in indicators)
{
    // 指標側：* と _sp を落としてから Next
    var ind = StripTileIdForLogic(indRaw);

    var dora = NextDoraId(ind);
    if (string.IsNullOrEmpty(dora)) continue;

    foreach (var t in pool)
    {
        // 牌側：* と _sp を落として一致判定
        var tt = StripTileIdForLogic(t);
        if (tt == dora) hits++;
    }
}

    return hits;
}

// ★追加：実ドラ（すでに Next 済みの面子）をそのまま数える版
private int CountActualDoraHits(IList<string> concealed14, IList<IList<string>> openMelds, IList<string> actualDora)
{
    var tiles = new List<string>();
    if (concealed14 != null) tiles.AddRange(concealed14.Select(Normalize));
    if (openMelds != null)   tiles.AddRange(openMelds.SelectMany(m => m.Select(Normalize)));

    if (actualDora == null) return 0;
    var actual = actualDora.Select(Normalize).ToList();

    int c = 0;
    foreach (var t in tiles)
        if (actual.Contains(t)) c++;
    return c;
}
private void RefreshScoringDoraUI()
{
    // 1) まずは「超シンプル手動方式」：Inspector で紐付けた行にスプライトをはめるだけ
    if (scoringDoraOmoteRow || scoringDoraUraRow)
    {
if (scoringDoraOmoteLabel) scoringDoraOmoteLabel.text = GetScoringDoraLabel_Local(false);
if (scoringDoraUraLabel)   scoringDoraUraLabel.text   = GetScoringDoraLabel_Local(true);
string Strip(string s) => StripStar(s);

// ★表示牌(indicators) をそのまま返す（実ドラへ変換しない）
IEnumerable<string> IndicatorsFrom(IEnumerable<string> indicators)
{
    if (indicators == null) yield break;
    foreach (var ind in indicators)
    {
        var id = Strip(ind);
        if (!string.IsNullOrEmpty(id)) yield return id;
    }
}



        var omote = IndicatorsFrom(doraIndicators); // 表ドラ表示牌（そのまま）
        var ura   = IndicatorsFrom(uraIndicators);  // 裏ドラ表示牌（そのまま。リーチ時のみ RevealUraDoraIfEligible() 済）

        if (scoringDoraOmoteRow) SetRowTiles(scoringDoraOmoteRow, omote);
        if (scoringDoraUraRow)   SetRowTiles(scoringDoraUraRow,   ura);
        return;
    }

    // 2) フォールバック：従来の「手動ルートに動的生成」ロジック（既存コードをそのまま）
    Transform dstRoot = scoringDoraManualRoot;
    if (!dstRoot) return;

    for (int i = dstRoot.childCount - 1; i >= 0; i--)
        Destroy(dstRoot.GetChild(i).gameObject);

    RectTransform MakeRow(string rowName)
    {
        var go = new GameObject(rowName, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(dstRoot, false);
        rt.sizeDelta = new Vector2(800, 96);
        return rt;
    }
    void PutLabel(RectTransform row, string text)
    {
        var t = new GameObject("Label_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = t.GetComponent<RectTransform>();
        rt.SetParent(row, false);
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot    = new Vector2(0, 0.5f);   rt.anchoredPosition = new Vector2(0, 0);
        var tmp = t.GetComponent<TextMeshProUGUI>(); tmp.text = text; tmp.fontSize = 32;
    }
    void PutTiles(RectTransform row, IEnumerable<string> ids)
    {
        float x = 140f;
        const int width = 60;
        if (ids == null) return;
        foreach (var raw in ids)
        {
            var id = (!string.IsNullOrEmpty(raw) && raw.EndsWith("*")) ? raw.Substring(0, raw.Length - 1) : raw;
            CreateTileImage(row, id, ref x, width);
        }
    }

    // 表ドラ行
// 表ドラ行
var rowOmote = MakeRow("Row_Omote");
PutLabel(rowOmote, GetScoringDoraLabel_Local(false));
var omoteList = new System.Collections.Generic.List<string>();
if (doraIndicators != null)
    foreach (var ind in doraIndicators)
    {
        var id = (!string.IsNullOrEmpty(ind) && ind.EndsWith("*")) ? ind.Substring(0, ind.Length - 1) : ind;
        if (!string.IsNullOrEmpty(id)) omoteList.Add(id);
    }
PutTiles(rowOmote, omoteList);

// 裏ドラ行（リーチ時のみ `uraIndicators` が事前にめくられる）
var rowUra = MakeRow("Row_Ura");
PutLabel(rowUra, GetScoringDoraLabel_Local(true));
var uraList = new System.Collections.Generic.List<string>();
if (uraIndicators != null)
    foreach (var ind in uraIndicators)
    {
        var id = (!string.IsNullOrEmpty(ind) && ind.EndsWith("*")) ? ind.Substring(0, ind.Length - 1) : ind;
        if (!string.IsNullOrEmpty(id)) uraList.Add(id);
    }
PutTiles(rowUra, uraList);

}

// シンプル：行（Transform）配下の Image を左から順に使って牌スプライトをはめる。
// 子 Image が足りなければその場で増やし、余った分は非表示にする。
private void SetRowTiles(Transform row, IEnumerable<string> tileIds)
{
    if (!row) return;
    var imgs = new System.Collections.Generic.List<Image>();
    foreach (Transform c in row)
    {
        var img = c.GetComponent<Image>();
        if (img) imgs.Add(img);
    }

    int i = 0;
    foreach (var id in tileIds)
    {
        Image img;
        if (i < imgs.Count) img = imgs[i];
        else
        {
            var go = new GameObject($"Tile_{i+1}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            img = go.GetComponent<Image>();
            imgs.Add(img);
        }

        // ★ここで key を必ず作る（特別牌ならレア度Spriteへ変換）
        string key = id;
        try
        {
            key = SpecialTileRuntime.SpriteKeyFromTileId(id);
        }
        catch
        {
            key = id;
        }

        var sp = Resources.Load<Sprite>($"Sprites/Tiles/{key}");
        img.enabled = (sp != null);
        img.sprite = sp;
        img.preserveAspect = true;
        i++;
    }
    for (int j = i; j < imgs.Count; j++) if (imgs[j]) imgs[j].enabled = false;
}


private TextMeshProUGUI CreateTopRightText(string text)
{
    var go = new GameObject("TMP_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
    var tmp = go.GetComponent<TextMeshProUGUI>();
    tmp.text = text;
    tmp.fontSize = 20;
    tmp.alignment = TextAlignmentOptions.MidlineRight;
    tmp.enableWordWrapping = false;
    tmp.raycastTarget = false;
    return tmp;
}
// 表ドラ/裏ドラの「表示牌リスト」から「実ドラ（次の牌）リスト」を作る単一の正規化ヘルパ
private List<string> GetActualDoraFromIndicators(IList<string> indicators)
{
    var list = new List<string>(indicators?.Count ?? 0);
    if (indicators != null)
    {
        foreach (var ind in indicators)
        {
            var d = NextDoraId(ind);  // ← 既存の正しい進行（1..9, 東南西北, 白發中）【参照：:contentReference[oaicite:6]{index=6}】
            if (!string.IsNullOrEmpty(d)) list.Add(d);
        }
    }
    return list;
}
private IEnumerator __Fade(CanvasGroup g, float from, float to, float dur)
{
    if (!g) yield break;
    float t = 0f;
    g.alpha = from;
    while (t < dur) {
        t += Time.deltaTime;
        g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
        yield return null;
    }
    g.alpha = to;
}
private IEnumerator __WinCutInThenShowScoring(
    string label, bool isPlayer,
    int finalDamage, int finalBasePoints, float geKi, float shun, float iyu,
    int finalMpHeal, int finalHpHeal,
    System.Collections.Generic.List<string> yakuLines,
    System.Collections.Generic.List<string> used,
    int fu, int han, string baseWinKind, string usedTileLabel)
{
    _scoringUsedTileLabel = usedTileLabel;
    var prevPhase = phase;
    phase = Phase.Scoring;

    if (isPlayer) _playerHasWonThisHand = true;
    else _enemyHasWonThisHand = true;

    if (!isPlayer && _enemyWonHandSnapshot == null)
        _enemyWonHandSnapshot = new List<string>(_enemyHand);

    _enemyTurnRunning = false;
if (winCutinTMP)
{
    bool isTsumoLabel = __IsTsumoKind(baseWinKind) || __IsTsumoKind(label);
    bool isRonLabel = __IsRonKind(baseWinKind) || __IsRonKind(label);

    if (isTsumoLabel)
    {
        winCutinTMP.text = GetGameFixedText_Local("win_tsumo");
    }
    else if (isRonLabel)
    {
        winCutinTMP.text = GetGameFixedText_Local("win_ron");
    }
    else if (!string.IsNullOrEmpty(label) && (label.Contains("勝利") || label.Equals("Victory", StringComparison.OrdinalIgnoreCase)))
    {
        winCutinTMP.text = GetGameFixedText_Local("player_victory");
    }
    else if (!string.IsNullOrEmpty(label) && (label.Contains("敗北") || label.Equals("Defeat", StringComparison.OrdinalIgnoreCase)))
    {
        winCutinTMP.text = GetGameFixedText_Local("player_defeat");
    }
    else
    {
        winCutinTMP.text = label;
    }
}
    if (winCutinPortrait)
    {
        Sprite sp;

        if (isPlayer)
        {
            sp = GetPlayerCutinFallbackSpriteSafe();
        }
        else
        {
            sp = enemyCutinSprite;
        }

        winCutinPortrait.enabled = (sp != null);
        if (sp != null)
            winCutinPortrait.sprite = sp;
        winCutinPortrait.preserveAspect = true;
    }

    if (winCutinRoot)
    {
        bool isTsumo = __IsTsumoKind(baseWinKind) || __IsTsumoKind(label);
        bool isRon = __IsRonKind(baseWinKind) || __IsRonKind(label);

        if (!isTsumo && !isRon)
            isRon = true;

        winCutinRoot.SetActive(true);

        if (AudioManager.Instance)
        {
            if (isPlayer)
            {
                if (isTsumo) AudioManager.Instance.PlayCutin_PlayerTsumo();
                else if (isRon) AudioManager.Instance.PlayCutin_PlayerRon();
            }
            else
            {
                if (isTsumo) AudioManager.Instance.PlayCutin_EnemyTsumo();
                else if (isRon) AudioManager.Instance.PlayCutin_EnemyRon();
            }
        }

        if (isPlayer)
        {
            string kind = isTsumo ? "Tsumo" : "Ron";

            if (winCutinGroup)
                winCutinGroup.alpha = 1f;

            PlayPlayerCutinAnimation(playerWinCutinAnimator, kind, winCutinPortrait);

            yield return StartCoroutine(WaitPlayerCutinAnimationOrSeconds(playerWinCutinAnimator, 3.0f));
        }
        else
        {
            if (winCutinGroup)
            {
                winCutinGroup.alpha = 0f;
                yield return new WaitForSeconds(0.3f);
                yield return __Fade(winCutinGroup, 0f, 1f, 0.1f);
                yield return new WaitForSeconds(1.0f);
                yield return __Fade(winCutinGroup, 1f, 0f, 0.1f);

                winCutinRoot.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
                winCutinRoot.SetActive(false);
            }
        }
    }
    else
    {
        yield return new WaitForSeconds(0.3f);
    }
    ShowScoring(
        finalDamage,
        han,
        fu,
        yakuLines,
        finalBasePoints,
        geKi,
        shun,
        iyu,
        finalMpHeal,
        finalHpHeal
    );
}
private void __ProceedAfterRyukyoku()
{
    // ★ノーテン罰などでHPが0になっている可能性があるので、敗北チェックはここで行う
    if (Mathf.Max(0, playerHP) <= 0)
    {
        // ラン一時要素（通貨/お札/ラン限定強化など）はここで従来通りクリア
        ClearRunEphemeral();

StartDefeatTransitionIfNeeded();
        return;
    }

    // ★流局は「1局消化」なので、次局へ進める（roundNumber を進めて StartNextHand）
    int nextRound = roundNumber + 1;
    bool roundsFinished = (nextRound > maxRounds);

    // 未撃破のまま最大局数を終了 → 敗北（敗北カットイン→報酬へ）
    if (roundsFinished)
    {
        __EnsureOmamoriAtLeastOneForReward();

StartDefeatTransitionIfNeeded();
        return;
    }

    roundNumber = nextRound;
    RefreshTopUI();
    StartNextHand();
}
private bool _victoryCutinTransitionRunning = false;
private bool _defeatTransitionRunning = false;

private void StartDefeatTransitionIfNeeded()
{
    if (_defeatTransitionRunning)
        return;

    _defeatTransitionRunning = true;
    StartCoroutine(__ShowDefeatCutinThenGoToReward());
}
private Sprite __GetDefeatCutinPortraitSprite_Local()
{
    if (defeatCutinSpriteManual != null)
        return defeatCutinSpriteManual;

    return GetDefeatCutinSprite();
}

private Sprite __GetVictoryCutinPortraitSprite_Local()
{
    if (playerVictoryCutinSpriteManual != null)
        return playerVictoryCutinSpriteManual;

    return GetPlayerVictoryCutinSpriteForCurrentSkill();
}
private IEnumerator __ShowDefeatCutinThen(System.Action next)
{
    if (winCutinRoot == null || winCutinGroup == null || winCutinTMP == null)
    {
        next?.Invoke();
        yield break;
    }

    winCutinTMP.text = GetGameFixedText_Local("player_defeat");

    try
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleResultDefeatSE();
    }
    catch { }

if (winCutinPortrait != null)
{
    var sp = __GetDefeatCutinPortraitSprite_Local();

    winCutinPortrait.sprite = sp;
    winCutinPortrait.enabled = (sp != null);
    winCutinPortrait.preserveAspect = true;
}

    if (!winCutinRoot.activeSelf)
    {
        winCutinRoot.SetActive(true);
    }

    winCutinGroup.alpha = 0f;
    winCutinGroup.interactable = true;
    winCutinGroup.blocksRaycasts = true;

    yield return __Fade(winCutinGroup, 0f, 1f, 0.1f);
    yield return new WaitForSecondsRealtime(3.0f);
    yield return __Fade(winCutinGroup, 1f, 0f, 0.1f);

    winCutinGroup.alpha = 0f;
    winCutinGroup.interactable = false;
    winCutinGroup.blocksRaycasts = false;

    next?.Invoke();
}
private IEnumerator __ShowPlayerVictoryCutinThen(System.Action next)
{
    if (_victoryCutinTransitionRunning)
    {
        yield break;
    }
    _victoryCutinTransitionRunning = true;

    try
    {
        if (winCutinRoot == null || winCutinGroup == null || winCutinTMP == null)
        {
            next?.Invoke();
            yield break;
        }

        winCutinTMP.text = GetGameFixedText_Local("player_victory");

        try
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBattleResultVictorySE();
        }
        catch { }

if (winCutinPortrait != null)
{
    var sp = __GetVictoryCutinPortraitSprite_Local();

    winCutinPortrait.sprite = sp;
    winCutinPortrait.enabled = (sp != null);
    winCutinPortrait.preserveAspect = true;
}
        if (!winCutinRoot.activeSelf)
        {
            winCutinRoot.SetActive(true);
        }

        winCutinGroup.alpha = 0f;
        winCutinGroup.interactable = true;
        winCutinGroup.blocksRaycasts = true;

        yield return __Fade(winCutinGroup, 0f, 1f, 0.1f);
        yield return new WaitForSecondsRealtime(3.0f);
        yield return __Fade(winCutinGroup, 1f, 0f, 0.1f);

        winCutinGroup.alpha = 0f;
        winCutinGroup.interactable = false;
        winCutinGroup.blocksRaycasts = false;

        next?.Invoke();
    }
    finally
    {
        _victoryCutinTransitionRunning = false;
    }
}
private void ApplyHpMpLayering()
{
    // Canvas の sorting 上書き
    if (hpMpCanvas && overrideSorting)
    {
        hpMpCanvas.overrideSorting = true;
        hpMpCanvas.sortingLayerName = sortingLayerName;
        hpMpCanvas.sortingOrder     = sortingOrder;
    }

    // SiblingIndex の適用（-1 は無視）
    void SetIndex(Transform t, int idx)
    {
        if (!t || !t.parent || idx < 0) return;
        t.SetSiblingIndex(Mathf.Clamp(idx, 0, t.parent.childCount - 1));
    }
    SetIndex(playerHPRoot ? playerHPRoot : (playerHPBar ? playerHPBar.transform : null), playerHPSibling);
    SetIndex(enemyHPRoot  ? enemyHPRoot  : (enemyHPBar  ? enemyHPBar.transform  : null), enemyHPSibling);
    SetIndex(playerMPRoot ? playerMPRoot : (playerMPBar ? playerMPBar.transform : null), playerMPSibling);
}
private bool TryApplyExcelEnemyConfigForCurrentIndex()
{
    int idx = 0;
    bool idxResolved = false;
    try { idx = ProgressionFlowController.GetCurrentEnemyIndex(); idxResolved = true; } catch {}
    if (!idxResolved)
    {
        try { idx = Mathf.Max(0, PlayerData.CurrentEnemy); idxResolved = true; } catch {}
    }

    if (!EnemyConfigExcel.TryGetForRuntimeIndex(idx, out var cfg))
        return false;

    float tierMult = 1f;
    try { tierMult = GetCurrentTierMultiplier(); } catch { tierMult = 1f; }

    // --- 最大HP（Excel値にTier倍率を掛ける） ---
    enemyMaxHP = Mathf.Max(1, Mathf.RoundToInt(cfg.maxHP * tierMult));

    // ★重要：中断復元中は enemyHP を満タン上書きしない（復元した enemyHP を優先）
    //        通常の新規戦闘開始（enemyHP < 0 のセンチネル）だけ満タン化する
    if (!_suspendRestoredThisSession)
    {
        enemyHP = enemyMaxHP;
    }
    else
    {
        if (enemyHP < 0)
        {
            enemyHP = enemyMaxHP;
        }
        else
        {
            enemyHP = Mathf.Clamp(enemyHP, 0, enemyMaxHP);
        }
    }

    // ★重要：Excelで重みを更新した直後に、敵山を必ず再構築する
    //         （既に作成済みのenemyDeckに古い字牌が残るのを防ぐ）
    try
    {
        BuildEnemyDeck();
    } catch {}

    // --- バトル用ポートレート（名前で自動ロード） ---
    try { TryLoadEnemyBattlePortraitByName(cfg.name); } catch {}

    // --- 名前のUI反映（会話と同じ表記：+周回サフィックス）
    try
    {
        var shownName = GetCurrentEnemyNameFromExcelWithLoop();
        SetEnemyNameOnUI(shownName);
    }
    catch {}

    // ★先にフラグを立てる。
    // これより後の UpdateHpUI / RefreshTopUI が内部で敵設定UI更新を触っても、
    // 「まだExcel未適用」と判定されて TryApplyExcelEnemyConfigForCurrentIndex() が再入しないようにする。
    _excelEnemyApplied = true;

    UpdateHpUI();
    RefreshTopUI();
    return true;
}
[SerializeField] private TMPro.TextMeshProUGUI enemyNameTMP;
[SerializeField] private TMPro.TextMeshProUGUI[] enemyNameTMPs;

private void SetEnemyNameOnUI(string name)
{
    if (enemyNameTMP != null)
    {
        enemyNameTMP.text = name ?? "";
    }

    if (enemyNameTMPs != null && enemyNameTMPs.Length > 0)
    {
        for (int i = 0; i < enemyNameTMPs.Length; i++)
        {
            var tmp = enemyNameTMPs[i];
            if (tmp == null) continue;
            tmp.text = name ?? "";
        }
    }
}

private string ResolveCurrentEnemyNameForBattleUI()
{
    try
    {
        int idxAbs = 0;
        try { idxAbs = ProgressionFlowController.GetCurrentEnemyIndex(); } catch { idxAbs = 0; }

        if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && cfg != null)
        {
            var all = EnemyConfigExcel.LoadAll();
            int count = (all != null) ? all.Count : 0;
            int loop = (count > 0) ? (idxAbs / count) : 0;

            string localizedName = cfg.GetLocalizedDisplayNameWithLoop(loop);
            if (!string.IsNullOrEmpty(localizedName))
                return localizedName;

            if (!string.IsNullOrEmpty(cfg.name))
            {
                var lm = LocalizationManager.Instance;
                if (lm != null)
                    return lm.GetEnemyDisplayName((loop > 0) ? $"{cfg.name} +{loop}" : cfg.name);

                return (loop > 0) ? $"{cfg.name} +{loop}" : cfg.name;
            }
        }
    }
    catch
    {
    }

    try
    {
        string raw = ProgressionFlowController.GetCurrentEnemyName();
        if (!string.IsNullOrEmpty(raw))
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
                return lm.GetEnemyDisplayName(raw);

            return raw;
        }
    }
    catch
    {
    }

    return string.Empty;
}

private void RefreshEnemyNameUIFromCurrentConfig()
{
    string shown = ResolveCurrentEnemyNameForBattleUI();
    SetEnemyNameOnUI(shown);
}

private string GetCurrentEnemyNameFromExcelWithLoop()
{
    return ResolveCurrentEnemyNameForBattleUI();
}
private string GetCurrentEnemyBaseNameForResources()
{
    int idxAbs = 0;
    try { idxAbs = ProgressionFlowController.GetCurrentEnemyIndex(); } catch {}

    try
    {
        if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && !string.IsNullOrEmpty(cfg.name))
            return cfg.name;
    }
    catch {}

    try
    {
        var nm = ProgressionFlowController.GetCurrentEnemyName();
        if (!string.IsNullOrEmpty(nm)) return nm;
    }
    catch {}

    return null;
}
private void AutoPreselectForPon_Leftmost()
{
    if (string.IsNullOrEmpty(callBaseTile)) return;

    string baseLogic = StripTileIdForLogic(callBaseTile);

    for (int i = 0; i < hand.Count && selHand.Count < 2; i++)
    {
        string myLogic = StripTileIdForLogic(hand[i]);
        if (myLogic == baseLogic) selHand.Add(i);
    }
}

private void AutoPreselectForChi_Leftmost()
{
    if (string.IsNullOrEmpty(callBaseTile)) return;

    string baseLogic = StripTileIdForLogic(callBaseTile);
    if (!TryParseSuitNum(baseLogic, out var suitB, out var numB)) return;

    // 左から順に、成立する2枚を選ぶ（特別牌でもOK）
    for (int i = 0; i < hand.Count; i++)
    {
        if (selHand.Contains(i)) continue;

        string logicI = StripTileIdForLogic(hand[i]);
        if (!TryParseSuitNum(logicI, out var suit, out var num)) continue;
        if (suit != suitB) continue;

        int d = num - numB;
        if (d == 0 || d < -2 || d > 2) continue;

        selHand.Add(i);

        for (int j = i + 1; j < hand.Count; j++)
        {
            if (selHand.Contains(j)) continue;

            string logicJ = StripTileIdForLogic(hand[j]);
            if (!TryParseSuitNum(logicJ, out var suit2, out var num2)) continue;
            if (suit2 != suitB) continue;

            var ns = new List<int> { numB, num, num2 };
            ns.Sort();

            if (ns[0] + 1 == ns[1] && ns[1] + 1 == ns[2])
            {
                selHand.Add(j);
                return;
            }
        }

        selHand.Remove(i);
    }
}

private void AutoPreselectForCurrentCall()
{
    if (callMode == CallMode.Pon) AutoPreselectForPon_Leftmost();
    else if (callMode == CallMode.Chi) AutoPreselectForChi_Leftmost();
    else if (callMode == CallMode.KanOpen)
    {
        // ミンカン（開槓）は左から3枚
        selHand.Clear();
        var idxs = new List<int>();
        for (int i = 0; i < hand.Count && idxs.Count < 3; i++)
            if (hand[i] == callBaseTile) idxs.Add(i);
        if (idxs.Count == 3)
        {
            selHand.UnionWith(idxs);
        }
    }

    // ★デグレ修正：
    // Pon/Chi は selHand を更新するだけで見た目が同期されていなかったため、
    // 自動選択後に必ず RaiseOverlay（持ち上げ）を再構築して UI に反映する。
    // （selHand が空でも呼ぶことで、前回のゴースト表示を確実に消せる）
    RebuildRaiseOverlays(handArea, selHand, hand);
}

    // 敵の採用（Add-onでロック）された捨て牌か？
    private bool IsDiscardLockedForEnemyAdoption(int listIdx)
    {
        try
        {
            if (!enemyDiscardArea) return false;
            if (listIdx < 0 || listIdx >= enemyDiscardArea.childCount) return false;
            var go = enemyDiscardArea.GetChild(listIdx).gameObject;
            // Add-on 側の _committedDiscardInstanceIDs を参照（partial なので参照可）
            return _committedDiscardInstanceIDs != null && _committedDiscardInstanceIDs.Contains(go.GetInstanceID());
        }
        catch { return false; }
    }
private void EnsureMenuPanelWiring(bool forceClose)
{
    // 毎回、現在の MenuPanel を取り直す。
    // 局進行中に UI が再生成・差し替えされても、古い参照を握り続けないため。
    try
    {
        var found = GameObject.Find("MenuPanel");
        if (found) menuPanel = found;
    }
    catch { }

    // メニュー内ボタンは、毎回「現在の MenuPanel 配下」から最新インスタンスを取り直す。
    // null のときだけ拾う方式だと、東2局以降などで古い Button 参照に listener を付け直してしまう。
    if (menuPanel)
    {
        try
        {
            UnityEngine.UI.Button foundOption = null;
            UnityEngine.UI.Button foundSuspend = null;
            UnityEngine.UI.Button foundExit = null;
            UnityEngine.UI.Button foundClose = null;

            var buttons = menuPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var b in buttons)
            {
                if (!b) continue;

                if (foundOption == null && b.name == "Button_MenuOption")
                {
                    foundOption = b;
                    continue;
                }

                if (foundSuspend == null && b.name == "Button_Suspend")
                {
                    foundSuspend = b;
                    continue;
                }

                if (foundExit == null && b.name == "Button_Exit")
                {
                    foundExit = b;
                    continue;
                }

                if (foundClose == null && b.name == "Button_MenuClose")
                {
                    foundClose = b;
                    continue;
                }
            }

            btnMenuOption = foundOption;
            btnMenuSuspend = foundSuspend;
            btnMenuExit = foundExit;
            btnMenuClose = foundClose;
        }
        catch { }
    }
    else
    {
        btnMenuOption = null;
        btnMenuSuspend = null;
        btnMenuExit = null;
        btnMenuClose = null;
    }

    // メニューボタン本体（パネル外）は毎回取り直しておく
    try
    {
        var found = GameObject.Find("Button_Menu");
        btnMenu = found ? found.GetComponent<UnityEngine.UI.Button>() : null;
    }
    catch
    {
        btnMenu = null;
    }

    // forceClose の時だけ既定で閉じる
    if (forceClose)
    {
        SetMenuPanelVisibleState(false);
        isMenuOpen = false;
    }
    else
    {
        EnsureMenuInnerButtonsAlwaysActive();
        isMenuOpen = IsMenuPanelVisibleState();
    }

    // 現在見えている最新ボタンに対して、毎回 listener を付け直す
    if (btnMenu)
    {
        btnMenu.onClick.RemoveAllListeners();
        btnMenu.onClick.AddListener(() => ToggleMenuPanel(!isMenuOpen));
    }

    if (btnMenuOption)
    {
        btnMenuOption.onClick.RemoveAllListeners();
        btnMenuOption.onClick.AddListener(OnClickMenuOption);
    }

    if (btnMenuSuspend)
    {
        btnMenuSuspend.onClick.RemoveAllListeners();
        btnMenuSuspend.onClick.AddListener(OnClickMenuSuspend);
    }

    if (btnMenuExit)
    {
        btnMenuExit.onClick.RemoveAllListeners();
        btnMenuExit.onClick.AddListener(OnClickMenuExit);
    }

    if (btnMenuClose)
    {
        btnMenuClose.onClick.RemoveAllListeners();
        btnMenuClose.onClick.AddListener(() => ToggleMenuPanel(false));
    }

    EnsureMenuInnerButtonsAlwaysActive();
}
private void ToggleMenuPanel(bool open)
{
    // ★UIがAwake後に生成されるケースでも、メニューを開く瞬間に配線を保証する
    EnsureMenuPanelWiring(false);

    // ★追加：メニューを開く瞬間にも stale なプレイヤースキル演出ロックを回復
    __RecoverStalePlayerSkillBusyFlags();

    isMenuOpen = open;

    if (menuPanel)
    {
        if (!menuPanel.activeSelf)
            menuPanel.SetActive(true);

        SetMenuPanelVisibleState(open);

        if (open)
        {
            // ★重要：同一階層内の最前面へ
            try { menuPanel.transform.SetAsLastSibling(); } catch {}

            // ★重要：別Canvasや全画面オーバーレイが前にいても必ず勝つため、
            // menuPanel を「独立Canvas」にして sortingOrder を最大にする
            try
            {
                var c = menuPanel.GetComponent<Canvas>();
                if (c == null) c = menuPanel.AddComponent<Canvas>();

                c.overrideSorting = true;
                c.sortingOrder = 10000;

                // RaycastをこのCanvasで確実に拾う
                var gr = menuPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (gr == null) gr = menuPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                gr.enabled = true;
            }
            catch { }
        }
    }

    EnsureMenuInnerButtonsAlwaysActive();

    // メニュー中は各種ボタンを触れないように再描画
    UpdateButtons();
    RefreshHandUI();       // 念のため
    RefreshOfferUI();
    RefreshDiscardUI();
    RefreshEnemyDiscardUI();
}
private void OnUILoaded()
{
    // ★UI自動生成後にボタン配線をやり直す（forceCloseしない）
    EnsureMenuPanelWiring(false);
}

// --- Option（中身は今後拡張。とりあえずプレースホルダー）
private void OnClickMenuOption()
{
    if (statusTMP) statusTMP.text = "Option は未実装です（後で追加してください）";
}
[System.Serializable]
private class SuspendSnapshot
{
    public int roundNumber;

    // ===== HP/MP（現在値と最大値を必ず保存）=====
    public int playerHP;
    public int playerMaxHP;

    // お守り%上昇の二重適用を防ぐための基礎最大HPも保存
    public int basePlayerMaxHP_ForOmamori;

    // Shop/Runボーナスの二重加算を防ぐための基礎最大HPも保存
    public int basePlayerMaxHP_ForRunBonuses;

    public int playerMP;

    // ===== 装備（スキル/お守り/特別牌）=====
    public string equippedSkillSetId;
    public string equippedActiveSkillName;

// お守り装備（本体：PlayerData.EquippedOmamori は int 1つ）
public int equippedOmamoriId;

// 互換のため残す（将来複数装備にしても壊れない）
public string equippedOmamoriIdsCsv;
    // 特別牌装備（UID群をCSVで保存。型が不明でも復元できるようにする）
    public string equippedSpecialTileUidsCsv;

    // ===== 敵HP =====
    public int enemyHP, enemyMaxHP;

    // プレイヤー側
    public List<string> deck;              // プレイヤー牌山（Stack を List 化）
    public List<string> hand;
    public List<string> offers;
    public List<string> discards;
    public List<List<string>> melds;

    // 敵側
    public List<string> enemyDiscards;
    public List<string> enemyDeck;         // 敵山（Stack を List 化）
    public List<string> enemyHand;         // 敵手牌

    // ドラ
    public List<string> doraIndicators;
    public List<string> uraIndicators;

    // 裏ドラ保留分と、裏ドラ加算フラグ
    public List<string> uraIndicatorPool;
    public bool includeUraForScoring;

    // 状態
    public bool isRiichi, isTenpai, suppressTsumoThisOffer;
    public string phase;
    public int currentEnemyIndex;

    // 敵の内部状態
    public bool enemyIsRiichi;
    public int enemyTurnCounter;
    public int enemyRiichiDeclaredTurnCounter;
    public int enemyWinDeclaredTurnCounter;
    public List<string> enemyRiichiWaits;
}
private void OnClickMenuSuspend()
{
    if (_menuSuspendInProgress)
        return;

    _menuSuspendInProgress = true;

    try
    {
        if (btnMenuSuspend)
            btnMenuSuspend.interactable = false;

        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);
    }
    catch { }

    try
    {
        Time.timeScale = 1f;
        isMenuOpen = false;
        SetMenuPanelVisibleState(false);
    }
    catch { }
    try
    {
        __PrepareForSceneUnload();
    }
    catch (Exception e)
    {
        Debug.LogWarning("[MenuSuspend] __PrepareForSceneUnload failed: " + e);
    }

    try
    {
        SaveSuspendSnapshot(true);
    }
    catch (Exception e)
    {
        Debug.LogError("[MenuSuspend] SaveSuspendSnapshot failed: " + e);
    }

    try
    {
        string dst = string.IsNullOrEmpty(menuSceneName) ? "MenuScene" : menuSceneName;
        UnityEngine.SceneManagement.SceneManager.LoadScene(dst, UnityEngine.SceneManagement.LoadSceneMode.Single);
        return;
    }
    catch (Exception e)
    {
        Debug.LogError("[MenuSuspend] LoadScene failed: " + e);
    }

    try
    {
        if (btnMenuSuspend)
            btnMenuSuspend.interactable = true;
    }
    catch { }

    _menuSuspendInProgress = false;
}
private void SaveSuspendSnapshot(bool markAsSuspend)
{
    var ss = new SuspendSnapshot();

    ss.roundNumber = roundNumber;

    // ===== HP/MP（現在/最大）=====
    ss.playerHP = Mathf.Max(0, playerHP);
    ss.playerMaxHP = Mathf.Max(1, playerMaxHP);

    ss.basePlayerMaxHP_ForOmamori = _basePlayerMaxHP_ForOmamori;
    ss.basePlayerMaxHP_ForRunBonuses = _basePlayerMaxHP_ForRunBonuses;

    // MP（GameManager_SkillMP_Addon の _mp をそのまま保存）
    ss.playerMP = Mathf.Max(0, _mp);

    // ===== 装備（スキル）=====
    ss.equippedSkillSetId = "";
    ss.equippedActiveSkillName = "";
    try { ss.equippedSkillSetId = PlayerPrefs.GetString("EquippedSkillSetId", ""); } catch { ss.equippedSkillSetId = ""; }
    try { ss.equippedActiveSkillName = PlayerPrefs.GetString("EquippedActiveSkill", ""); } catch { ss.equippedActiveSkillName = ""; }

// ===== 装備（お守り/特別牌）=====

// ★お守りは int 1つが本体（PlayerPrefs: EquippedOmamoriIdV1）
ss.equippedOmamoriId = 0;
try { ss.equippedOmamoriId = Mathf.Max(0, PlayerData.EquippedOmamori); } catch { ss.equippedOmamoriId = 0; }

// 互換のため残す（ただし現状は空でもOK）
ss.equippedOmamoriIdsCsv = DumpPlayerDataIntIdsCsv_Safe(new string[]
{
    "EquippedOmamoriIds",
    "equippedOmamoriIds",
    "EquippedOmamoriList",
    "equippedOmamoriList"
});
    ss.equippedSpecialTileUidsCsv = DumpPlayerDataStringIdsCsv_Safe(new string[]
    {
        "EquippedSpecialTileUids",
        "equippedSpecialTileUids",
        "EquippedSpecialTiles",
        "equippedSpecialTiles",
        "EquippedSpecialTileIds",
        "equippedSpecialTileIds"
    });

    // ===== 敵HP =====
    ss.enemyHP = Mathf.Max(0, enemyHP);
    ss.enemyMaxHP = Mathf.Max(1, enemyMaxHP);

    // ===== 盤面 =====
    ss.deck = new List<string>(deck.ToArray());
    ss.hand = new List<string>(hand);
    ss.offers = new List<string>(offers);
    ss.discards = new List<string>(discards);
    ss.melds = new List<List<string>>(melds.Select(m => new List<string>(m)));

    ss.enemyDiscards = new List<string>(enemyDiscards);
    ss.enemyDeck = new List<string>(enemyDeck.ToArray());
    ss.enemyHand = new List<string>(_enemyHand);

    ss.doraIndicators = new List<string>(doraIndicators);
    ss.uraIndicators = new List<string>(uraIndicators);

    ss.uraIndicatorPool = new List<string>(_uraIndicatorPool);
    ss.includeUraForScoring = _includeUraForScoring;

    ss.isRiichi = isRiichi;
    ss.isTenpai = isTenpai;
    ss.suppressTsumoThisOffer = suppressTsumoThisOffer;
    ss.phase = phase.ToString();

    try { ss.currentEnemyIndex = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { ss.currentEnemyIndex = 0; }

    ss.enemyIsRiichi = _enemyIsRiichi;
    ss.enemyTurnCounter = _enemyTurnCounter;
    ss.enemyRiichiDeclaredTurnCounter = _enemyRiichiDeclaredTurnCounter;
    ss.enemyWinDeclaredTurnCounter = _enemyWinDeclaredTurnCounter;
    ss.enemyRiichiWaits = new List<string>(_enemyRiichiWaits);

    string json = JsonUtility.ToJson(ss);

    try
    {
        PlayerPrefs.SetString(PF_SUSPEND_JSON, json);

        if (markAsSuspend)
        {
            PlayerPrefs.SetInt(PF_SUSPEND_FLAG, 1);
            PlayerPrefs.SetInt("PF_ResumeDirect", 1);
            PlayerPrefs.SetString("PF_ResumeScene", "RunScene");
        }
        else
        {
            PlayerPrefs.SetInt(PF_SUSPEND_FLAG, 0);
        }
        PersistRunPlayerHP(false);

        // ★追加：中断復元が失敗しても敵HPだけは戻せるように、敵HPも別キーに退避
        try
        {
            PlayerPrefs.SetInt("Run_EnemyHP", Mathf.Max(0, enemyHP));
            PlayerPrefs.SetInt("Run_EnemyMaxHP", Mathf.Max(1, enemyMaxHP));
        }
        catch { }

        try { SaveRunGold(); } catch {}
        PlayerPrefs.Save();
    }
    catch {}

    if (statusTMP) statusTMP.text = markAsSuspend ? "中断データを保存しました" : "";
}
private bool TryLoadSuspendSnapshot()
{
    try
    {
        if (PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) != 1) return false;

        string json = PlayerPrefs.GetString(PF_SUSPEND_JSON, "");

        // ★追加：JSONが空/壊れている等で復元ができない場合でも、
        //        敵HPだけは保険キーから戻して「全回復に見える」事故を防ぐ
        if (string.IsNullOrEmpty(json))
        {
            try
            {
                if (PlayerPrefs.HasKey("Run_EnemyHP"))
                {
                    int savedEnemyHp = PlayerPrefs.GetInt("Run_EnemyHP", -1);
                    int savedEnemyMax = PlayerPrefs.GetInt("Run_EnemyMaxHP", enemyMaxHP);
                    if (savedEnemyMax > 0) enemyMaxHP = Mathf.Max(1, savedEnemyMax);
                    if (savedEnemyHp >= 0) enemyHP = Mathf.Clamp(savedEnemyHp, 0, enemyMaxHP);
                    UpdateHpUI();
                }
            }
            catch { }

            return false;
        }

        var ss = JsonUtility.FromJson<SuspendSnapshot>(json);
        if (ss == null) return false;
        // ===== このセッションは「中断復元」扱い。以後の上乗せ処理を絶対に止める =====
        _suspendRestoredThisSession = true;

        // ===== 装備（スキル）を PlayerPrefs に先に戻す（SkillMP_Addon.Start() がこれを読む）=====
        try { PlayerPrefs.SetString("EquippedSkillSetId", ss.equippedSkillSetId ?? ""); } catch {}
        try { PlayerPrefs.SetString("EquippedActiveSkill", ss.equippedActiveSkillName ?? ""); } catch {}
        try { PlayerPrefs.Save(); } catch {}

        // SkillMP_Addon.Start() が “メニュー変更後の装備” を書き込まないよう、保留値をセット
        _pendingSuspendLoadoutApply = true;
        _pendingSuspendSkillSetId = ss.equippedSkillSetId ?? "";
        _pendingSuspendActiveSkillName = ss.equippedActiveSkillName ?? "";
        _pendingSuspendPlayerMP = Mathf.Max(0, ss.playerMP);

        // ===== お守り/特別牌の装備を PlayerData に戻す（存在するフィールド/プロパティだけ安全に反映）=====
        ApplyPlayerDataIntIdsCsv_Safe(new string[]
        {
            "EquippedOmamoriIds",
            "equippedOmamoriIds",
            "EquippedOmamoriList",
            "equippedOmamoriList",
            "EquippedOmamori",
            "equippedOmamori"
        }, ss.equippedOmamoriIdsCsv ?? "");
// ★お守り本体（int）を確実に中断時へ戻す（ここが今のバグの核心）
try { PlayerData.EquippedOmamori = Mathf.Max(0, ss.equippedOmamoriId); } catch { }
try { PlayerPrefs.Save(); } catch { }
        ApplyPlayerDataStringIdsCsv_Safe(new string[]
        {
            "EquippedSpecialTileUids",
            "equippedSpecialTileUids",
            "EquippedSpecialTiles",
            "equippedSpecialTiles",
            "EquippedSpecialTileIds",
            "equippedSpecialTileIds"
        }, ss.equippedSpecialTileUidsCsv ?? "");

        // ===== 敵インデックス復元 =====
        try { PlayerData.CurrentEnemy = Mathf.Max(0, ss.currentEnemyIndex); } catch {}

        // ===== 数値（HP/最大HP）を完全復元。Shop強化の上乗せは絶対にしない =====
        roundNumber = Mathf.Max(1, ss.roundNumber);

        playerMaxHP = Mathf.Max(1, ss.playerMaxHP);
        playerHP = Mathf.Clamp(Mathf.Max(0, ss.playerHP), 0, playerMaxHP);

        _basePlayerMaxHP_ForOmamori = ss.basePlayerMaxHP_ForOmamori;
        _basePlayerMaxHP_ForRunBonuses = ss.basePlayerMaxHP_ForRunBonuses;

        _omamoriBaseApplied = true;
        _runBonusesApplied = true;

        enemyMaxHP = Mathf.Max(1, ss.enemyMaxHP);
        enemyHP = Mathf.Clamp(ss.enemyHP, 0, enemyMaxHP);

        // ===== 盤面復元 =====
        deck.Clear();
        if (ss.deck != null)
        {
            for (int i = ss.deck.Count - 1; i >= 0; i--) deck.Push(ss.deck[i]);
        }

        enemyDeck.Clear();
        if (ss.enemyDeck != null)
        {
            for (int i = ss.enemyDeck.Count - 1; i >= 0; i--) enemyDeck.Push(ss.enemyDeck[i]);
        }

        hand.Clear();
        if (ss.hand != null) hand.AddRange(ss.hand);

        offers.Clear();
        if (ss.offers != null) offers.AddRange(ss.offers);

        discards.Clear();
        if (ss.discards != null) discards.AddRange(ss.discards);

        enemyDiscards.Clear();
        if (ss.enemyDiscards != null) enemyDiscards.AddRange(ss.enemyDiscards);

        _enemyHand.Clear();
        if (ss.enemyHand != null) _enemyHand.AddRange(ss.enemyHand);

        _enemyIsRiichi = ss.enemyIsRiichi;
        _enemyTurnCounter = ss.enemyTurnCounter;
        _enemyRiichiDeclaredTurnCounter = ss.enemyRiichiDeclaredTurnCounter;
        _enemyWinDeclaredTurnCounter = ss.enemyWinDeclaredTurnCounter;

        _enemyRiichiWaits.Clear();
        if (ss.enemyRiichiWaits != null)
        {
            for (int i = 0; i < ss.enemyRiichiWaits.Count; i++)
            {
                var w = ss.enemyRiichiWaits[i];
                if (!string.IsNullOrEmpty(w)) _enemyRiichiWaits.Add(w);
            }
        }

        enemyUsedIndices.Clear();
        _committedDiscardInstanceIDs.Clear();

        melds.Clear();
        if (ss.melds != null)
        {
            foreach (var m in ss.melds) melds.Add(new List<string>(m ?? new List<string>()));
        }

        doraIndicators.Clear();
        if (ss.doraIndicators != null) doraIndicators.AddRange(ss.doraIndicators);

        uraIndicators.Clear();
        if (ss.uraIndicators != null) uraIndicators.AddRange(ss.uraIndicators);

        _uraIndicatorPool.Clear();
        if (ss.uraIndicatorPool != null) _uraIndicatorPool.AddRange(ss.uraIndicatorPool);
        _includeUraForScoring = ss.includeUraForScoring;

        isRiichi = ss.isRiichi;
        isTenpai = ss.isTenpai;
        suppressTsumoThisOffer = ss.suppressTsumoThisOffer;

        if (System.Enum.TryParse<Phase>(ss.phase, out var p)) phase = p;
        else phase = Phase.Offer;

        // ===== UI更新 =====
        SortHand();
        RefreshHandUI();
        RefreshOfferUI();
        RefreshDiscardUI();
        RefreshEnemyDiscardUI();
        RefreshEnemyHandUI_FullRebuild();
        RefreshDoraUI();

        UpdateHpUI();
        RefreshTopUI();
        UpdateButtons();

        // ===== フラグ消費（保存データはここで破棄）=====
        PlayerPrefs.DeleteKey(PF_SUSPEND_FLAG);
        PlayerPrefs.DeleteKey(PF_SUSPEND_JSON);

        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");
        PlayerPrefs.Save();

        if (statusTMP) statusTMP.text = "中断データから再開しました";
        return true;
    }
    catch
    {
        return false;
    }
}
private static string DumpPlayerDataIntIdsCsv_Safe(string[] candidateNames)
{
    try
    {
        var t = typeof(PlayerData);

        foreach (var name in candidateNames)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p != null)
            {
                var v = p.GetValue(null);
                var csv = __ConvertIntEnumerableToCsv(v);
                if (!string.IsNullOrEmpty(csv)) return csv;
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                var v = f.GetValue(null);
                var csv = __ConvertIntEnumerableToCsv(v);
                if (!string.IsNullOrEmpty(csv)) return csv;
            }
        }
    }
    catch { }
    return "";
}

private static void ApplyPlayerDataIntIdsCsv_Safe(string[] candidateNames, string csv)
{
    try
    {
        var t = typeof(PlayerData);
        var ids = __ParseIntCsv(csv);

        foreach (var name in candidateNames)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p != null && p.CanWrite)
            {
                if (p.PropertyType == typeof(int[])) { p.SetValue(null, ids); return; }
                if (p.PropertyType == typeof(List<int>)) { p.SetValue(null, ids.ToList()); return; }
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                if (f.FieldType == typeof(int[])) { f.SetValue(null, ids); return; }
                if (f.FieldType == typeof(List<int>)) { f.SetValue(null, ids.ToList()); return; }
            }
        }
    }
    catch { }
}

private static string DumpPlayerDataStringIdsCsv_Safe(string[] candidateNames)
{
    try
    {
        var t = typeof(PlayerData);

        foreach (var name in candidateNames)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p != null)
            {
                var v = p.GetValue(null);
                var csv = __ConvertStringEnumerableToCsv(v);
                if (!string.IsNullOrEmpty(csv)) return csv;
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                var v = f.GetValue(null);
                var csv = __ConvertStringEnumerableToCsv(v);
                if (!string.IsNullOrEmpty(csv)) return csv;
            }
        }
    }
    catch { }
    return "";
}

private static void ApplyPlayerDataStringIdsCsv_Safe(string[] candidateNames, string csv)
{
    try
    {
        var t = typeof(PlayerData);
        var ids = __ParseStringCsv(csv);

        foreach (var name in candidateNames)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p != null && p.CanWrite)
            {
                if (p.PropertyType == typeof(string[])) { p.SetValue(null, ids); return; }
                if (p.PropertyType == typeof(List<string>)) { p.SetValue(null, ids.ToList()); return; }
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                if (f.FieldType == typeof(string[])) { f.SetValue(null, ids); return; }
                if (f.FieldType == typeof(List<string>)) { f.SetValue(null, ids.ToList()); return; }
            }
        }
    }
    catch { }
}

private static string __ConvertIntEnumerableToCsv(object v)
{
    try
    {
        if (v == null) return "";
        if (v is int[] arr) return string.Join(",", arr.Select(x => Mathf.Max(0, x).ToString()));
        if (v is List<int> list) return string.Join(",", list.Select(x => Mathf.Max(0, x).ToString()));
        if (v is IEnumerable<int> en) return string.Join(",", en.Select(x => Mathf.Max(0, x).ToString()));
    }
    catch { }
    return "";
}

private static string __ConvertStringEnumerableToCsv(object v)
{
    try
    {
        if (v == null) return "";
        if (v is string[] arr) return string.Join(",", arr.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0));
        if (v is List<string> list) return string.Join(",", list.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0));
        if (v is IEnumerable<string> en) return string.Join(",", en.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0));
    }
    catch { }
    return "";
}

private static int[] __ParseIntCsv(string csv)
{
    if (string.IsNullOrEmpty(csv)) return new int[0];
    var parts = csv.Split(',');
    var outList = new List<int>();
    for (int i = 0; i < parts.Length; i++)
    {
        int n;
        if (int.TryParse(parts[i], out n))
        {
            n = Mathf.Max(0, n);
            if (n > 0) outList.Add(n);
        }
    }
    return outList.ToArray();
}

private static string[] __ParseStringCsv(string csv)
{
    if (string.IsNullOrEmpty(csv)) return new string[0];
    var parts = csv.Split(',');
    var outList = new List<string>();
    for (int i = 0; i < parts.Length; i++)
    {
        var s = (parts[i] ?? "").Trim();
        if (s.Length > 0) outList.Add(s);
    }
    return outList.ToArray();
}
private static int TryGetEquippedOmamoriId_Safe()
{
    try
    {
        var t = typeof(PlayerData);

        // property
        var p = t.GetProperty("EquippedOmamori", BindingFlags.Public | BindingFlags.Static);
        if (p != null && p.PropertyType == typeof(int))
        {
            return Mathf.Max(0, (int)p.GetValue(null));
        }

        // field
        var f = t.GetField("EquippedOmamori", BindingFlags.Public | BindingFlags.Static);
        if (f != null && f.FieldType == typeof(int))
        {
            return Mathf.Max(0, (int)f.GetValue(null));
        }
    }
    catch { }
    return 0;
}

private static string TryGetEquippedOmamoriIdsCsv_Safe()
{
    try
    {
        var t = typeof(PlayerData);

        // よくありそうな名前を優先して拾う（List<int> / int[] / IEnumerable<int>）
        string[] candidates =
        {
            "EquippedOmamoriIds",
            "EquippedOmamoriList",
            "EquippedOmamori",
            "equippedOmamoriIds",
            "equippedOmamori"
        };

        foreach (var name in candidates)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (p != null)
            {
                var v = p.GetValue(null);
                string csv = ConvertIntEnumerableToCsv(v);
                if (!string.IsNullOrEmpty(csv)) return csv;
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (f != null)
            {
                var v = f.GetValue(null);
                string csv = ConvertIntEnumerableToCsv(v);
                if (!string.IsNullOrEmpty(csv)) return csv;
            }
        }
    }
    catch { }
    return "";
}

private static string ConvertIntEnumerableToCsv(object v)
{
    try
    {
        if (v == null) return "";
        if (v is int[] arr)
        {
            return string.Join(",", arr.Select(x => Mathf.Max(0, x).ToString()));
        }
        if (v is System.Collections.Generic.List<int> list)
        {
            return string.Join(",", list.Select(x => Mathf.Max(0, x).ToString()));
        }
        if (v is System.Collections.Generic.IEnumerable<int> en)
        {
            return string.Join(",", en.Select(x => Mathf.Max(0, x).ToString()));
        }
    }
    catch { }
    return "";
}

private static void TryApplyEquippedOmamoriFromSuspend_Safe(int singleId, string csv)
{
    try
    {
        var t = typeof(PlayerData);

        // 1) 単体IDをセット（存在するなら）
        {
            var p = t.GetProperty("EquippedOmamori", BindingFlags.Public | BindingFlags.Static);
            if (p != null && p.PropertyType == typeof(int) && p.CanWrite)
            {
                p.SetValue(null, Mathf.Max(0, singleId));
            }

            var f = t.GetField("EquippedOmamori", BindingFlags.Public | BindingFlags.Static);
            if (f != null && f.FieldType == typeof(int))
            {
                f.SetValue(null, Mathf.Max(0, singleId));
            }
        }

        // 2) 複数装備がある場合に備えて、IDs の配列/リストも戻す（存在するなら）
        if (!string.IsNullOrEmpty(csv))
        {
            var ids = csv.Split(',')
                         .Select(s => { int.TryParse(s, out var n); return Mathf.Max(0, n); })
                         .Where(n => n > 0)
                         .ToArray();

            // ありがちな名前だけ試す（無ければ何もしない）
            string[] candidates =
            {
                "EquippedOmamoriIds",
                "EquippedOmamoriList",
                "equippedOmamoriIds",
                "equippedOmamori"
            };

            foreach (var name in candidates)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                if (p != null && p.CanWrite)
                {
                    if (p.PropertyType == typeof(int[])) { p.SetValue(null, ids); return; }
                    if (p.PropertyType == typeof(List<int>)) { p.SetValue(null, ids.ToList()); return; }
                }

                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (f != null)
                {
                    if (f.FieldType == typeof(int[])) { f.SetValue(null, ids); return; }
                    if (f.FieldType == typeof(List<int>)) { f.SetValue(null, ids.ToList()); return; }
                }
            }
        }
    }
    catch { }
}
// --- Exit（終了）：ラン進行を保存せず、ローグライト要素をリセットして終了 ---
private void OnClickMenuExit()
{
    try
    {
        ClearRunEphemeral();

        // ★中断データは必ず破棄
        PlayerPrefs.DeleteKey(PF_SUSPEND_FLAG);
        PlayerPrefs.DeleteKey(PF_SUSPEND_JSON);

        // ★前ランのHP/MP持ち越しは必ず破棄（ここが無いとMPが残る）
        PlayerPrefs.DeleteKey("Run_PlayerHP");
        PlayerPrefs.DeleteKey("Run_PlayerMP");

        // ★次回のバトル開始時は最大まで回復（上限補正込み）
        PlayerPrefs.SetInt("PF_PendingFullHeal", 1);

        // ★敵進行を「最初の敵」に確実に戻す（会話シーン表示の残骸も潰す）
        try { ProgressionFlowController.ForceResetToFirstEnemy(); } catch {}

        // 起動時リセットフラグ（保険）
        PlayerPrefs.SetInt("PF_ResetRunOnLoad", 1);

        // ★追加：Run開始フラグを落として、次回Run開始時に初期化が走るようにする
        PlayerPrefs.SetInt(PF_RUN_STARTED, 0);

        PlayerPrefs.Save();

    }
    catch {}

    try { UnityEngine.SceneManagement.SceneManager.LoadScene(menuSceneName); } catch {}
}

private static string Normalize(string s)
{
    if (string.IsNullOrEmpty(s)) return s;

    // '*' は末尾/途中どちらでも落とす（StripStarに寄せて一貫させる）
    s = StripStar(s);

    // StripStar内で '_' 以降（例: "_sp"）も落ちるので、ここではそのまま返す
    return s;
}
// --- Manual Scoring UI helpers (new) ---
private static void __SetTMP(TextMeshProUGUI t, string v) { if (t) t.text = v ?? ""; }
private static void __SetActive(GameObject go, bool v) { if (go) go.SetActive(v); }

private static string __NormalizeYakuDisplayText(string s)
{
    if (string.IsNullOrEmpty(s)) return "";

    // "役:" / "役：" のラベルが混ざっている場合は剥がす
    s = s.Replace("役：", "").Replace("役:", "").Trim();

    // "(+1)" のような括弧内の '+' は残し、役間の区切り '+' だけをスペース区切りにする
    // 例: "立直(+1) + 一発(+1) + 平和(+1)" → "立直(+1)　一発(+1)　平和(+1)"
    s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+\+\s+", "　");
    s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+\+\s*", "　");
    s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\+\s+", "　");

    // 残った半角スペースも「スペース区切り」に寄せる（見た目統一）
    s = System.Text.RegularExpressions.Regex.Replace(s, @"[ \t]{2,}", " ");
    s = s.Replace(" ", "　");
    s = System.Text.RegularExpressions.Regex.Replace(s, @"　{2,}", "　");

    return s.Trim(' ', '　');
}

private static string __GetYakumanDisplayNameSafe_Local(string key, string fallback)
{
    var lm = LocalizationManager.Instance;
    if (lm == null) return fallback;

    string value = lm.GetYakumanDisplayName(key);
    if (string.IsNullOrEmpty(value)) return fallback;
    if (string.Equals(value, "yakuman." + key, StringComparison.Ordinal)) return fallback;

    return value;
}

private static string __LocalizeSpecialYakumanToken_Local(string s)
{
    if (string.IsNullOrEmpty(s)) return "";

    string result = s;

    string tenhou = __GetYakumanDisplayNameSafe_Local("TENHOU", "天和");
    string chihou = __GetYakumanDisplayNameSafe_Local("CHIHOU", "地和");
    string renhou = __GetYakumanDisplayNameSafe_Local("RENHOU", "人和");

    result = result.Replace("天和(+13)", tenhou + "(+13)");
    result = result.Replace("地和(+13)", chihou + "(+13)");
    result = result.Replace("人和(+13)", renhou + "(+13)");

    result = result.Replace("天和", tenhou);
    result = result.Replace("地和", chihou);
    result = result.Replace("人和", renhou);

    return result;
}
private bool __UseManualScoringUI()
{
    // 何か1つでも手動UIが紐付いていれば true
    return scoringRoleValue || scoringFuHanValue || scoringBasePointValue ||
           scoringRoleValue_Enemy || scoringFuHanValue_Enemy || scoringBasePointValue_Enemy ||
           scoringGekiValue || scoringShunValue || scoringIyuValue ||
           scoringOfudaDmgValue || scoringOfudaHpValue || scoringOfudaMpValue ||
           scoringFinalDamageToEnemyValue || scoringFinalDamageToPlayerValue ||
           scoringOmamoriReduceValue || scoringEnemySkillEffectValue || scoringEnemySkillEffectValue_Enemy ||
           scoringPlayerOnlyRoot || scoringEnemyOnlyRoot ||
           scoringAngerIcon_Enemy || scoringDefenseIcon_Player ||
           scoringAddedDamageValue || scoringTotalHpRecoverValue || scoringTotalMpRecoverValue ||
           scoringAddedDamageValue_Enemy || scoringTotalHpRecoverValue_Enemy || scoringTotalMpRecoverValue_Enemy ||
           scoringGoldGainValue ||
           scoringSpecialTileDamageEffectValue_Player || scoringSpecialTileDamageEffectValue_Enemy;
}

private void __ApplyScoringManualUI(
    string roles, string fuHan, int basePt,
    float traitGekiMultiplier, float traitShunRate, float traitIyuRate,
    int finalDamageForApply, int finalHpHeal, int finalMpHeal,
    float ofudaMultOnly, int ofudaHpPct, int ofudaMpPct,
    int omamoriPct,
    bool isPlayer)
{
    // 旧シグネチャ互換：呼び出し側の引数変更を最小化するためのラッパー
    float gePct = Mathf.Max(0f, traitGekiMultiplier - 1f); // x1.2 → 0.2（20%）
    float shPct = Mathf.Max(0f, traitShunRate);
    float iyPct = Mathf.Max(0f, traitIyuRate);

    // ★仕様変更：ツモ和了時は基礎点の表示を減少させる（プレイヤー25%減／敵50%減）
    bool isTsumoForBase = isPlayer ? _lastScoringIsTsumo : _enemyLastWinWasTsumo;
    int displayBasePt = basePt;
    if (isTsumoForBase)
    {
        float tsumoRate = isPlayer ? 0.75f : 0.5f;
        displayBasePt = Mathf.Max(1, Mathf.RoundToInt(basePt * tsumoRate));
    }

    // 「ダメージ加算量」＝最終ダメージ - 基礎点（半減後）（マイナスも許容：防御スキルの減算を合計に反映するため）
    int addedDamageAmount = Mathf.Max(0, finalDamageForApply) - Mathf.Max(0, displayBasePt);


    int totalHpHeal = Mathf.Max(0, finalHpHeal);
    int totalMpHeal = Mathf.Max(0, finalMpHeal);

    __ApplyScoringManualUI(
        roles, fuHan, displayBasePt,
        gePct, shPct, iyPct,
        0, 0,
        ofudaMultOnly, ofudaHpPct, ofudaMpPct,
        omamoriPct,
        finalDamageForApply,
        addedDamageAmount,
        totalHpHeal, totalMpHeal,
        isPlayer
    );
}

private void __ApplyScoringManualUI(
    string roles, string fuHan, int basePt,
    float traitGekiPct, float traitShunPct, float traitIyuPct,
    int traitHpHeal, int traitMpHeal,
    float ofudaMultOnly, int ofudaHpPct, int ofudaMpPct,
    int omamoriPct,
    int finalDamageForApply,
    int addedDamageAmount,
    int totalHpHeal, int totalMpHeal,
    bool isPlayer)
{
    // 役表示（敵側は「+」区切りを禁止してスペースへ正規化）
    string rolesDispRaw = isPlayer ? (roles ?? "") : __NormalizeYakuDisplayText(roles ?? "");
    string rolesDisp = __LocalizeSpecialYakumanToken_Local(rolesDispRaw);
__SetTMP(scoringRoleValue, rolesDisp);
__SetTMP(scoringFuHanValue, fuHan ?? "");

// ★仕様変更：ツモ和了ダメージ減少時のラベルを基礎点に表示（プレイヤー25%減／敵50%減）
string _tsumoBaseLabel = "";
{
    bool isTsumoWin = isPlayer ? _lastScoringIsTsumo : _enemyLastWinWasTsumo;
    if (isTsumoWin)
    {
        string pctStr = isPlayer ? "25" : "50";
        var _lmB = LocalizationManager.Instance;
        var _langB = (_lmB != null) ? _lmB.CurrentLanguage : LocalizationManager.Language.Japanese;
        switch (_langB)
        {
            case LocalizationManager.Language.English:           _tsumoBaseLabel = $" (Tsumo -{pctStr}%)"; break;
            case LocalizationManager.Language.ChineseSimplified: _tsumoBaseLabel = $"（自摸和　减{pctStr}%）"; break;
            default:                                             _tsumoBaseLabel = $"（ツモあがり　{pctStr}％減）"; break;
        }
    }
}
__SetTMP(scoringBasePointValue, (basePt > 0) ? $"{basePt}{_tsumoBaseLabel}" : "");

if (!isPlayer)
{
    __SetTMP(scoringRoleValue_Enemy, rolesDisp);
    __SetTMP(scoringFuHanValue_Enemy, fuHan ?? "");
    __SetTMP(scoringBasePointValue_Enemy, (basePt > 0) ? $"{basePt}{_tsumoBaseLabel}" : "");
}

    if (isPlayer)
    {
        // --- 撃/瞬/癒：%だけ表示。効果が無ければ「-」 ---
        __SetTMP(scoringGekiValue, (traitGekiPct > 0f) ? $"{traitGekiPct * 100f:0.###}％" : "-");
        __SetTMP(scoringShunValue, (traitShunPct > 0f) ? $"{traitShunPct * 100f:0.###}％" : "-");
        __SetTMP(scoringIyuValue,  (traitIyuPct  > 0f) ? $"{traitIyuPct  * 100f:0.###}％" : "-");

        // --- お札：%だけ表示。効果が無ければ「-」 ---
        float ofudaDmgPct = Mathf.Max(0f, ofudaMultOnly - 1f);
        __SetTMP(scoringOfudaDmgValue, (ofudaDmgPct > 0f) ? $"{ofudaDmgPct * 100f:0.###}％" : "-");
        __SetTMP(scoringOfudaHpValue,  (ofudaHpPct > 0) ? $"{ofudaHpPct}％" : "-");
        __SetTMP(scoringOfudaMpValue,  (ofudaMpPct > 0) ? $"{ofudaMpPct}％" : "-");
// ★特別牌：ダメージ増減（プレイヤー和了＝敵へのダメージ増減）
// 現状のレジェンダリー定義では「敵へのダメージ増減」系が無いので "-" 固定（将来追加に備え枠だけ作る）
__SetTMP(scoringSpecialTileDamageEffectValue_Player, "-");
__SetTMP(scoringSpecialTileDamageEffectValue_Enemy, "");

// 敵名（空ならフォールバック）
string enemyName = GetCurrentEnemyNameFromExcelWithLoop();
if (string.IsNullOrEmpty(enemyName)) enemyName = GetGameFixedText_Local("enemy_generic_name");

__SetTMP(
    scoringFinalDamageToEnemyValue,
    FormatGameFixedText_Local(
        "damage_to_enemy_format",
        enemyName,
        Mathf.Max(0, finalDamageForApply)
    )
);
        // --- 合計値（数値のみ） ---
        __SetTMP(scoringAddedDamageValue, addedDamageAmount.ToString());

        __SetTMP(scoringTotalHpRecoverValue, Mathf.Max(0, totalHpHeal).ToString());
        __SetTMP(scoringTotalMpRecoverValue, Mathf.Max(0, totalMpHeal).ToString());

        __SetTMP(scoringAddedDamageValue_Enemy, "");
        __SetTMP(scoringTotalHpRecoverValue_Enemy, "");
        __SetTMP(scoringTotalMpRecoverValue_Enemy, "");
__SetTMP(scoringGoldGainValue, (_goldGainThisWin > 0) ? _goldGainDisplayTextThisWin : "-");
        // ★防御は「プレイヤー和了（プレイヤー攻撃）」側の点数計算パネルに表示する
        string effPlayer = "-";
        if (_enemySkillLastAppliedDefenseRate > 0f)
        {
            effPlayer = $"-{_enemySkillLastAppliedDefenseRate * 100f:0.###}%";
        }
        __SetTMP(scoringEnemySkillEffectValue, effPlayer);
        __SetTMP(scoringEnemySkillEffectValue_Enemy, "");


        // ★アイコン表示（防御がある時だけ）
        if (scoringDefenseIcon_Player)
            scoringDefenseIcon_Player.gameObject.SetActive(_enemySkillLastAppliedDefenseRate > 0f);
        if (scoringAngerIcon_Enemy)
            scoringAngerIcon_Enemy.gameObject.SetActive(false);
    }
    else
    {
        // 敵の和了時（プレイヤー被ダメ）
        __SetTMP(scoringGekiValue, "-");
        __SetTMP(scoringShunValue, "-");
        __SetTMP(scoringIyuValue,  "-");
        __SetTMP(scoringOfudaDmgValue, "-");
        __SetTMP(scoringOfudaHpValue,  "-");
        __SetTMP(scoringOfudaMpValue,  "-");
        __SetTMP(scoringFinalDamageToEnemyValue, "");

        // ★表示用：レジェンダリー②（直後の敵和了ダメージ半減）が有効なら「表示上のダメージ」も半減後にする
        int displayDamageToPlayer = Mathf.Max(0, finalDamageForApply);
        displayDamageToPlayer = PreviewLegendaryDamageHalfOnEnemyWin(displayDamageToPlayer);

        // お守り（効果が無ければ「-」）
        __SetTMP(scoringOmamoriReduceValue, (omamoriPct > 0) ? $"{omamoriPct}％" : "-");
        __SetTMP(scoringFinalDamageToPlayerValue,
            (displayDamageToPlayer >= 0)
                ? $"プレイヤーへのダメージ　{displayDamageToPlayer}"
                : "プレイヤーへのダメージ　0");

        // --- 合計値（数値のみ） ---
        __SetTMP(scoringAddedDamageValue_Enemy, Mathf.Max(0, addedDamageAmount).ToString());
        __SetTMP(scoringTotalHpRecoverValue_Enemy, Mathf.Max(0, totalHpHeal).ToString());
        __SetTMP(scoringTotalMpRecoverValue_Enemy, Mathf.Max(0, totalMpHeal).ToString());

        __SetTMP(scoringAddedDamageValue, "");
        __SetTMP(scoringTotalHpRecoverValue, "");
        __SetTMP(scoringTotalMpRecoverValue, "");

if (scoringSpecialTileDamageEffectValue_Enemy)
{
    string sp = "-";
    if (IsLegendaryDamageHalfActive())
    {
        var lm = LocalizationManager.Instance;
        switch (lm != null ? lm.CurrentLanguage : LocalizationManager.Language.Japanese)
        {
            case LocalizationManager.Language.English:
                sp = "Special Tile: -50%";
                break;
            case LocalizationManager.Language.ChineseSimplified:
                sp = "特别牌：-50％";
                break;
            case LocalizationManager.Language.Japanese:
            default:
                sp = "特別牌：-50％";
                break;
        }
    }
    __SetTMP(scoringSpecialTileDamageEffectValue_Enemy, sp);
}
        if (scoringSpecialTileDamageEffectValue_Player)
        {
            __SetTMP(scoringSpecialTileDamageEffectValue_Player, "");
        }

        // ★怒り/防御など「点数（最終ダメージ）に影響した内容」を表示（LastAppliedのみ）
        string eff = "";

        if (_enemySkillLastAppliedAngerMultiplier > 1f)
        {
            float pct = Mathf.Max(0f, (_enemySkillLastAppliedAngerMultiplier - 1f) * 100f);
            eff += $"怒り　+{pct:0.###}％";
        }

        if (_enemySkillLastAppliedDefenseRate > 0f)
        {
            if (!string.IsNullOrEmpty(eff)) eff += "　/　";
            eff += $"防御　-{_enemySkillLastAppliedDefenseRate * 100f:0.###}％";
        }

        __SetTMP(scoringEnemySkillEffectValue, "");
        __SetTMP(scoringEnemySkillEffectValue_Enemy, eff);

        // ★アイコン表示（怒りがある時だけ）
        if (scoringAngerIcon_Enemy)
            scoringAngerIcon_Enemy.gameObject.SetActive(_enemySkillLastAppliedAngerMultiplier > 1f);
        if (scoringDefenseIcon_Player)
            scoringDefenseIcon_Player.gameObject.SetActive(false);
    }

    // ★重要：TMPが非表示だと「書いてるのに見えない」ので、空でない時は必ず表示
    if (scoringEnemySkillEffectValue)
        scoringEnemySkillEffectValue.gameObject.SetActive(!string.IsNullOrEmpty(scoringEnemySkillEffectValue.text));
    if (scoringEnemySkillEffectValue_Enemy)
        scoringEnemySkillEffectValue_Enemy.gameObject.SetActive(!string.IsNullOrEmpty(scoringEnemySkillEffectValue_Enemy.text));

    if (scoringPanelPlayer) scoringPanelPlayer.SetActive(isPlayer);
    if (scoringPanelEnemy)  scoringPanelEnemy.SetActive(!isPlayer);
}

private void RefreshScoringDoraUI_Enemy(System.Collections.Generic.IList<string> omote)
{
    // ===== Enemy Scoring Panel: Omote + Ura indicators (manual rows) =====
    // omote は引数が空なら既存の doraIndicators を使う
    var omoteSrc = (omote != null && omote.Count > 0) ? omote : doraIndicators;
    bool hasOmote = (omoteSrc != null && omoteSrc.Count > 0);

    // ura は RevealUraDoraIfEligible() が積んだ uraIndicators をそのまま表示
    var uraSrc = uraIndicators;
    bool hasUra = (uraSrc != null && uraSrc.Count > 0);

    // --- Omote ---
    if (scoringDoraOmoteLabelEnemy)
    {
        scoringDoraOmoteLabelEnemy.text = GetScoringDoraLabel_Local(false);
        // ★ラベルは常に表示（テキストが出ない系の事故を防ぐ）
        scoringDoraOmoteLabelEnemy.gameObject.SetActive(true);
    }

    if (scoringDoraOmoteRowEnemy) scoringDoraOmoteRowEnemy.gameObject.SetActive(hasOmote);
    if (scoringDoraOmoteRowEnemy)
    {
        foreach (Transform c in scoringDoraOmoteRowEnemy) Destroy(c.gameObject);
        if (hasOmote)
        {
            float dummy = 0f;
            foreach (var raw in omoteSrc)
            {
                var id = (!string.IsNullOrEmpty(raw) && raw.EndsWith("*")) ? raw.Substring(0, raw.Length - 1) : raw;
                CreateTileImage(scoringDoraOmoteRowEnemy, id, ref dummy, 60);
            }
        }
    }

    // --- Ura ---
    if (scoringDoraUraLabelEnemy)
    {
        scoringDoraUraLabelEnemy.text = GetScoringDoraLabel_Local(true);
        // ★ラベルは常に表示（テキストが出ない系の事故を防ぐ）
        scoringDoraUraLabelEnemy.gameObject.SetActive(true);
    }

    if (scoringDoraUraRowEnemy) scoringDoraUraRowEnemy.gameObject.SetActive(hasUra);
    if (scoringDoraUraRowEnemy)
    {
        foreach (Transform c in scoringDoraUraRowEnemy) Destroy(c.gameObject);
        if (hasUra)
        {
            float dummy = 0f;
            foreach (var raw in uraSrc)
            {
                var id = (!string.IsNullOrEmpty(raw) && raw.EndsWith("*")) ? raw.Substring(0, raw.Length - 1) : raw;
                CreateTileImage(scoringDoraUraRowEnemy, id, ref dummy, 60);
            }
        }
    }
}

    // 敵の和了点数計算パネルを閉じたときの処理
    // ・敵和了カットイン／オーバーレイを閉じる
    // ・敵の点数計算パネルを閉じる
    // ※重要：同一局が継続する仕様のため、この時点では「敵の捨て牌／敵デッキ」はリセットしない
    private void CloseEnemyWinPanel()
    {
        // 1) 敵和了オーバーレイを閉じる
        if (_enemyWinOverlay) _enemyWinOverlay.gameObject.SetActive(false);
        Addon_ClearEnemyScoringOverlays();

        // 2) 敵用の点数計算パネル本体を閉じる
        if (scoringPanelEnemy)
        {
            scoringPanelEnemy.SetActive(false);
        }

        // 3) ここで敵の捨て牌・敵デッキ・敵ターン履歴はリセットしない
        //    （局が終わるまで継続させる仕様）
        //
        //    ※敵の手牌の扱い（空にする/自動捨て等）は既存仕様に任せる。
    }


/// <summary>
/// 敵の名前を基にカットイン画像をロードして表示する
/// </summary>
public void TryLoadEnemyBattlePortraitByName(string enemyName)
{
    if (string.IsNullOrEmpty(enemyName)) return;

    // ループ数のサフィックス（" +1" など）を取り除く
    // 表示名が「アマテラス +1」のような形を想定
    const string loopSuffixMarker = " +";
    int loopIdx = enemyName.IndexOf(loopSuffixMarker, System.StringComparison.Ordinal);
    if (loopIdx > 0)
    {
        enemyName = enemyName.Substring(0, loopIdx);
    }
    enemyName = enemyName.Trim();

    // Resourcesフォルダから画像をロード
    string path = $"EnemyCutins/{enemyName}_cutin";
    Sprite enemyCutinSprite = Resources.Load<Sprite>(path);

    if (enemyCutinSprite != null)
    {
        // 敵画像のカットインを表示するGameObjectを取得
        GameObject enemyPortrait = null;

        // ① 手動オーバーレイの子から探す（非アクティブでも OK）
        if (enemyWinOverlayManualRoot != null)
        {
            var t = enemyWinOverlayManualRoot.transform.Find("EnemyPortrait");
            if (t != null) enemyPortrait = t.gameObject;
        }

        // ② それでも見つからない場合は、従来どおりシーン全体から検索（アクティブのみ）
        if (enemyPortrait == null)
        {
            enemyPortrait = GameObject.Find("EnemyPortrait"); // 敵画像のGameObject名を指定
        }

        if (enemyPortrait != null)
        {
            enemyPortrait.SetActive(true); // 子オブジェクト自体はアクティブにしておく
             var imageComponent = enemyPortrait.GetComponent<Image>();
             if (imageComponent != null)
             {
                 imageComponent.sprite = enemyCutinSprite; // スプライトを設定
                 enemyPortrait.SetActive(true); // カットインを表示準備
             }

            else
            {
#if UNITY_EDITOR
                Debug.LogWarning("EnemyPortraitにImageコンポーネントがありません。");
#endif
            }
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("EnemyPortraitオブジェクトが見つかりません。");
#endif
        }
    }
    else
    {
#if UNITY_EDITOR
        Debug.LogWarning($"カットイン画像が見つかりません: {path}");
#endif
    }
}


public IEnumerator ShowEnemyCutinAfterDelay(string enemyName)
{
    yield return new WaitForSeconds(0.5f); // 0.5秒間待機

    // カットイン画像をロードして表示
    TryLoadEnemyBattlePortraitByName(enemyName);
}
public void HideEnemyCutin()
{
     GameObject enemyPortrait = GameObject.Find("EnemyPortrait");
     if (enemyPortrait != null)
     {
         enemyPortrait.SetActive(false); // カットインを非表示
            // Debug.Log("敵画像のカットインを非表示にしました。");
     }

    else
    {
        Debug.LogWarning("EnemyPortraitオブジェクトが見つかりません。");
    }
}
// 2025/11/15 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

private void UpdateScoringPanelUI()
{
// 敵：役
if (scoringRoleValue_Enemy)
    scoringRoleValue_Enemy.text = __LocalizeSpecialYakumanToken_Local(__NormalizeYakuDisplayText(EnemyAddon_LastYakuText ?? ""));
// 敵：符・翻
if (scoringFuHanValue_Enemy)
{
    scoringFuHanValue_Enemy.text = BuildScoringHanFuText_Local(EnemyAddon_LastHan, EnemyAddon_LastFu);
}

// 敵：基本点（合計点）
if (scoringBasePointValue_Enemy)
    scoringBasePointValue_Enemy.text = $"{EnemyAddon_LastPoints}";
UpdateScoringPanelUI_EnemyExtra();

}
private void UpdateScoringPanelUI_EnemyExtra()
{
    // ===== 怒り/防御（最終ダメージに影響した内容） =====
    if (scoringEnemySkillEffectValue_Enemy)
    {
        string eff = "";

        if (_enemySkillLastAppliedAngerMultiplier > 1f)
        {
            float pct = Mathf.Max(0f, (_enemySkillLastAppliedAngerMultiplier - 1f) * 100f);
            eff = $"+{pct:0.###}％";
        }

        if (_enemySkillLastAppliedDefenseRate > 0f)
        {
            if (!string.IsNullOrEmpty(eff)) eff += "　/　";
            eff += $"-{_enemySkillLastAppliedDefenseRate * 100f:0.###}％";
        }

        if (string.IsNullOrEmpty(eff)) eff = "-";

        scoringEnemySkillEffectValue_Enemy.text = eff;
        scoringEnemySkillEffectValue_Enemy.gameObject.SetActive(true);
    }

    // ===== 特別牌：ダメージ増減（敵和了＝プレイヤー被ダメに効く） =====
    if (scoringSpecialTileDamageEffectValue_Enemy)
    {
        string sp = "-";
        if (IsLegendaryDamageHalfActive())
        {
            sp = "特別牌：-50％";
        }

        scoringSpecialTileDamageEffectValue_Enemy.text = sp;
        scoringSpecialTileDamageEffectValue_Enemy.gameObject.SetActive(true);
    }
    if (scoringSpecialTileDamageEffectValue_Player)
    {
        scoringSpecialTileDamageEffectValue_Player.text = "";
        scoringSpecialTileDamageEffectValue_Player.gameObject.SetActive(false);
    }

    // 敵パネル更新時はプレイヤー側の枠は消しておく（残留表示防止）
    if (scoringEnemySkillEffectValue)
    {
        scoringEnemySkillEffectValue.text = "";
        scoringEnemySkillEffectValue.gameObject.SetActive(false);
    }
}

    // スキルが「手牌から1枚選んでから発動」タイプかどうか（UI用の簡易判定）
    private bool SkillNeedsHandSelectionForUI(ActiveSkill s)
    {
        return
            s == ActiveSkill.RandomMan ||
            s == ActiveSkill.RandomSou ||
            s == ActiveSkill.RandomPin ||
            s == ActiveSkill.RandomHonor ||
            s == ActiveSkill.RandomYaochu ||
            s == ActiveSkill.RandomChunchan ||
            s == ActiveSkill.DuplicateAndDiscardOther ||
            s == ActiveSkill.EnhanceHand ||
            s == ActiveSkill.ForceDrawSelectedNextTurn;
    }
// 敵の和了点数計算パネルを閉じたあと、
// 「そのターンの敵の捨て牌」に対してプレイヤーが鳴き／ロンできるかを確認する。
// ・候補があれば EnemyTurn 状態に戻してプレイヤー入力待ち
// ・候補が無ければ 0.5 秒待って自動でプレイヤーのツモ番へ
private IEnumerator __EnemyWin_PostScoringReaction_Co()
{
    // すでにゲームオーバーなら何もしない
    if (Mathf.Max(0, playerHP) <= 0)
        yield break;

    // 捨て牌情報がなければ、そのまま 0.5 秒後にツモ番へ
    if (enemyDiscards == null || lastEnemyTurnTiles == null || enemyDiscardArea == null)
    {
        yield return new WaitForSeconds(0.5f);
        BeginOfferPhase();
        yield break;
    }

    int start = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
    if (start < 0 || start >= enemyDiscards.Count)
    {
        yield return new WaitForSeconds(0.5f);
        BeginOfferPhase();
        yield break;
    }

    // 敵ターンとして捨て牌クリック状態を復元
    phase = Phase.EnemyTurn;
    selectedEnemyIndex = -1;
    RefreshEnemyDiscardUI();
    WireEnemyTurnClickTargets();
    UpdateButtons();
    EvaluateWinUI_New();

    bool hasCandidate = false;
    int cc = enemyDiscardArea.childCount;
// スコア描画に使った和了牌ラベル（存在すれば）
string usedLabel = _scoringUsedTileLabel;

for (int li = start; li < enemyDiscards.Count && li < cc; li++)
{


// 2) 「スコア描画に使った和了牌ラベル」(usedLabel) は、
//    “実際にそのターン捨てられた牌ではない”場合だけ対象外にする。
//    ※ID一致だけで除外すると、本物の捨て牌（同ID）まで除外してロン不能になる。
try
{
    if (!string.IsNullOrEmpty(usedLabel) && enemyDiscards[li] == usedLabel)
    {
        bool wasActuallyDiscardedThisTurn = false;
        if (lastEnemyTurnTiles != null)
        {
            for (int k = 0; k < lastEnemyTurnTiles.Count; k++)
            {
                if (lastEnemyTurnTiles[k] == usedLabel) { wasActuallyDiscardedThisTurn = true; break; }
            }
        }
        if (!wasActuallyDiscardedThisTurn)
            continue; // 仮ラベルだけ除外
    }
}
catch { }


    var child = enemyDiscardArea.GetChild(li);
    var btn = child ? child.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
    if (btn != null && btn.interactable)
    {
        hasCandidate = true;
        break;
    }
}
    if (hasCandidate)
    {
        if (statusTMP) statusTMP.text = "敵の捨て牌に対して鳴き／ロンできます";
        // プレイヤーの操作を待つだけ（自動進行しない）
        yield break;
    }

    // 対象牌が無い場合は 0.5 秒待って自動でプレイヤーのツモ番へ
    yield return new WaitForSeconds(0.5f);
    if (phase == Phase.EnemyTurn && Mathf.Max(0, playerHP) > 0)
    {
        if (statusTMP) statusTMP.text = "";
        BeginOfferPhase();
    }
}
private System.Collections.IEnumerator __ShowPlayerRiichiCutin()
{
    if (!playerRiichiCutinRoot)
        yield break;

    _playerRiichiCutinRunning = true;

    if (playerRiichiTextTMP)
        playerRiichiTextTMP.text = GetGameFixedText_Local("shanten_riichi");

    try
    {
        if (playerRiichiImage != null)
        {
            Sprite sp = GetPlayerCutinFallbackSpriteSafe();

            if (sp != null)
            {
                playerRiichiImage.enabled = true;
                playerRiichiImage.sprite = sp;
                playerRiichiImage.preserveAspect = true;
            }
            else
            {
                playerRiichiImage.enabled = (playerRiichiImage.sprite != null);
            }
        }
    }
    catch { }

    var cg = playerRiichiCutinRoot.GetComponent<CanvasGroup>();
    if (cg != null) cg.alpha = 1f;

    playerRiichiCutinRoot.SetActive(true);

    if (AudioManager.Instance)
    {
        AudioManager.Instance.PlayCutin_PlayerRiichi();
    }

    PlayPlayerCutinAnimation(playerRiichiCutinAnimator, "Riichi", playerRiichiImage);

    yield return StartCoroutine(WaitPlayerCutinAnimationOrSeconds(playerRiichiCutinAnimator, 2f));

    playerRiichiCutinRoot.SetActive(false);
    _playerRiichiCutinRunning = false;
    _playerRiichiCutinCo = null;
}
private string BuildRoundLabelForCutin()
{
    string wind = GetRoundWind();
    int num = GetRoundNumberInCurrentWind();

    string windText;
    if (wind == "South")
        windText = GetGameFixedText_Local("round_wind_south");
    else
        windText = GetGameFixedText_Local("round_wind_east");

    string format = GetGameFixedText_Local("round_label_format");

    try
    {
        return string.Format(format, windText, num);
    }
    catch
    {
        return $"{windText}{num}{GetGameFixedText_Local("round_suffix")}";
    }
}
private System.Collections.IEnumerator __PlayerWin_ApplyPendingDamageToEnemy_ThenProceedScoreOK_Co(bool wasEnemyScoring)
{
    _playerWinDamageAnimating = true;

    int startEnemyHP = Mathf.Max(0, enemyHP);
    int dmgToEnemy   = (_pendingPlayerWinDamageToEnemy) ? Mathf.Max(0, _pendingPlayerWinDamageFinal) : 0;
    int endEnemyHP   = Mathf.Max(0, startEnemyHP - dmgToEnemy);

    int startPlayerHP = Mathf.Max(0, playerHP);
    int healToPlayer  = (_pendingPlayerWinHpHeal) ? Mathf.Max(0, _pendingPlayerWinHpHealAbs) : 0;
    int endPlayerHP   = Mathf.Clamp(startPlayerHP + healToPlayer, 0, playerMaxHP);

    // ★追加：MP回復（瞬/お札）も同じタイミングでアニメ
    int startPlayerMP = Mathf.Max(0, _mp);
    int healToMP      = (_pendingPlayerWinMpHeal) ? Mathf.Max(0, _pendingPlayerWinMpHealAbs) : 0;
    int endPlayerMP   = ClampToEffectiveMaxMP(startPlayerMP + healToMP);

try
{
    if (dmgToEnemy > 0 && AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayBattleDamageSE();
    }
}
catch { }
    float dur = Mathf.Max(0.01f, playerWinDamageAnimSeconds);
    float t = 0f;

    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        if (dmgToEnemy > 0)
        {
            int dispEnemyHP = Mathf.RoundToInt(Mathf.Lerp(startEnemyHP, endEnemyHP, p));
            __UpdateEnemyHpUI_VisualOnly(dispEnemyHP);
        }

        if (healToPlayer > 0)
        {
            int dispPlayerHP = Mathf.RoundToInt(Mathf.Lerp(startPlayerHP, endPlayerHP, p));
            __UpdatePlayerHpUI_VisualOnly(dispPlayerHP);
        }

        if (healToMP > 0)
        {
            int dispPlayerMP = Mathf.RoundToInt(Mathf.Lerp(startPlayerMP, endPlayerMP, p));
            __UpdatePlayerMpUI_VisualOnly(dispPlayerMP);
        }

        yield return null;
    }

    // 最終値を確定
    enemyHP  = endEnemyHP;
    playerHP = endPlayerHP;

    // ★追加：MPも確定
    _mp = endPlayerMP;

    _pendingPlayerWinDamageToEnemy = false;
    _pendingPlayerWinDamageBase = 0;
    _pendingPlayerWinDamageFinal = 0;

    _pendingPlayerWinHpHeal = false;
    _pendingPlayerWinHpHealAbs = 0;

    // ★追加：MP保留をクリア
    _pendingPlayerWinMpHeal = false;
    _pendingPlayerWinMpHealAbs = 0;

    UpdateHpUI();
    UpdateMpUI();

    _playerWinDamageAnimating = false;

    // ★修正：ダメージ確定後に中断データを上書き保存する。
    //   OnApplicationPause で保存された古いスナップショット（flag=1）を
    //   最新のHP状態で更新し、次局 StartNextHand() で TryLoadSuspendSnapshot() が
    //   古いHPを復元してしまうバグを防ぐ。
    TryAutoSaveSuspendSnapshot();

    // 演出が終わったので次へ進める
    _freezeProgression = false;
    __ProceedAfterScoreOK_Internal(wasEnemyScoring);
}
private void __UpdatePlayerMpUI_VisualOnly(int displayMP)
{
    int effMax = Mathf.Max(1, EffectiveMaxMP());
    int mp = Mathf.Clamp(displayMP, 0, effMax);

    // 1) 旧来の（手動割り当て）Slider/Text も追従
    if (mpTMP != null) mpTMP.text = $"MP {mp}/{effMax}";
    if (mpSlider != null)
    {
        if (!Mathf.Approximately(mpSlider.maxValue, effMax))
            mpSlider.maxValue = effMax;
        mpSlider.value = mp;
    }
    if (mpFillImage != null)
    {
        float f = (effMax > 0) ? (float)mp / effMax : 0f;
        mpFillImage.fillAmount = Mathf.Clamp01(f);
    }

    // 2) 手動UI（Image/TMP）
    if (playerMPTMP)
    {
        string fmt = (playerMPConfig != null && !string.IsNullOrEmpty(playerMPConfig.textFormat))
            ? playerMPConfig.textFormat : "{cur}/{max}";
        playerMPTMP.text = fmt.Replace("{cur}", mp.ToString()).Replace("{max}", effMax.ToString());
    }

    if (playerMPBar)
    {
        if (!playerMPBar.gameObject.activeSelf) playerMPBar.gameObject.SetActive(true);

        float f = (effMax > 0) ? (float)mp / effMax : 0f;

        if (playerMPConfig != null)
        {
            playerMPBar.type = playerMPConfig.fillType;
            if (playerMPBar.type == UnityEngine.UI.Image.Type.Filled)
            {
                playerMPBar.fillMethod = playerMPConfig.fillMethod;
                playerMPBar.fillOrigin = playerMPConfig.fillOrigin;
                playerMPBar.fillAmount = Mathf.Clamp01(f);
            }

            if (playerMPConfig.overrideColor) playerMPBar.color = playerMPConfig.color;
        }
        else
        {
            playerMPBar.type       = UnityEngine.UI.Image.Type.Filled;
            playerMPBar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            playerMPBar.fillOrigin = 0;
            playerMPBar.fillAmount = Mathf.Clamp01(f);
        }

        // ★麻痺が有効な間は、演出中も必ず麻痺色を維持する（EnemySkills_Addon の仕様を壊さない）
        try
        {
            if (_enemySkillParalysisTurnRemaining > 0)
            {
                Color c = enemySkillParalysisMpColor;
                if (c.a <= 0.001f) c.a = 1f;
                playerMPBar.color = c;
            }
        }
        catch { }
    }
}
    // 任意のテキストとSEで、matchStartCutinGroup を使ったカットインを表示する共通コルーチン
    private System.Collections.IEnumerator __ShowTextCutin_Co(string text, AudioSource seSource)
    {
        if (!matchStartCutinGroup)
            yield break;

        // ラベル更新
        if (matchStartLabelTMP)
            matchStartLabelTMP.text = text;

        matchStartCutinGroup.gameObject.SetActive(true);
        matchStartCutinGroup.alpha = 0f;

        // SE 再生（指定があれば）
        if (seSource != null)
        {
            seSource.Play();
        }

        float t = 0f;

        // フェードイン
        if (matchStartFadeInDuration > 0f)
        {
            while (t < matchStartFadeInDuration)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / matchStartFadeInDuration);
                matchStartCutinGroup.alpha = a;
                yield return null;
            }
        }
        else
        {
            matchStartCutinGroup.alpha = 1f;
        }

        // 表示維持（TimeScale 無関係）
        if (matchStartHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(matchStartHoldDuration);
        }

        // フェードアウト
        t = 0f;
        if (matchStartFadeOutDuration > 0f)
        {
            while (t < matchStartFadeOutDuration)
            {
                t += UnityEngine.Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / matchStartFadeOutDuration);
                matchStartCutinGroup.alpha = a;
                yield return null;
            }
        }

        matchStartCutinGroup.alpha = 0f;
        matchStartCutinGroup.gameObject.SetActive(false);
    }
/// <summary>
/// ActiveSkill enum → Asset 内の activeSkillName とマッチさせるための候補 ID を返す。
/// 例：RandomMan → { "RandomMan", "染色師" }
/// </summary>
private IEnumerable<string> GetSkillIdCandidates(ActiveSkill s)
{
    // 1) enum 名（従来の内部ID）
    yield return s.ToString();

    // 2) Asset に登録されている displayName / activeSkillName があれば追加
    if (_skillSet && _skillSet.activeSkills != null)
    {
        foreach (var e in _skillSet.activeSkills)
        {
            if (e == null) continue;

            // activeSkillName（内部ID）
            if (!string.IsNullOrEmpty(e.activeSkillName))
                yield return e.activeSkillName;

            // displayName（表示用の日本語名）
            if (!string.IsNullOrEmpty(e.displayName))
                yield return e.displayName;
        }
    }
}
// ===== Special Tile (v2) simple counters for scoring =====

private static bool __IsSpecialTileId(string rawId)
{
    rawId = StripStar(rawId);
    if (string.IsNullOrEmpty(rawId)) return false;
    return rawId.IndexOf("_sp", StringComparison.OrdinalIgnoreCase) >= 0;
}

private static int __CountSpecialTiles(IEnumerable<string> tilesRaw)
{
    if (tilesRaw == null) return 0;
    int c = 0;
    foreach (var t in tilesRaw)
    {
        if (__IsSpecialTileId(t)) c++;
    }
    return c;
}

private int CountSpecialTileDoraBonusForScoring(IEnumerable<string> concealed14Raw, IEnumerable<IEnumerable<string>> openMeldsRaw)
{
    int count = 0;

    count += __CountSpecialTiles(concealed14Raw);

    if (openMeldsRaw != null)
    {
        foreach (var m in openMeldsRaw)
        {
            if (m == null) continue;
            count += __CountSpecialTiles(m);
        }
    }

    return count;
}

private static string NormalizeSpecialTile(string id)
{
    if (string.IsNullOrEmpty(id)) return id;

    if (id.EndsWith("_sp"))
        return id.Replace("_sp", "");

    return id;
}
private IEnumerator __ShowDefeatCutinThenGoToReward()
{
    bool sceneTransitionStarted = false;

    // ★追加：敗北が確定したら、進行を完全停止してスコア相当フェーズに固定
    _freezeProgression = true;
    phase = Phase.Scoring;
    try { UpdateButtons(); } catch { }

    // ★実績：Run合計スコア（このrunScore）でスコア実績を判定
    try { AchievementSystem.NotifyRunFinishedScore(runScore); } catch { }

    // まずはカットイン（3秒表示）
    yield return __ShowDefeatCutinThen(null);

    // ★お守り：敗北時も最低1つ付与（倒した敵数に応じてロール）
    __EnsureOmamoriAtLeastOneForReward();

    // ラン内アイテム等はクリア（既存仕様に合わせる）
    try { ClearRunItems(); } catch { }

    // シーン離脱前の明示クリーンアップ
    // ここで StopAllCoroutines すると、この敗北遷移コルーチン自身まで止まってしまうので false を渡す
    __PrepareForSceneUnload(false);

    // 進行制御があればそちらを優先（既存の敗北→報酬フロー）
    var inst = ProgressionFlowController.Instance;
    if (inst != null)
    {
        inst.GoFromBattleLoseToReward();
        sceneTransitionStarted = true;
        yield break;
    }

    // フォールバック：従来の rewardSceneName
    try
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(rewardSceneName);
        sceneTransitionStarted = true;
    }
    catch
    {
    }

    if (!sceneTransitionStarted)
        _defeatTransitionRunning = false;
}
private void __EnsureOmamoriAtLeastOneForReward()
{
    try
    {
        // ★ローグライト仕様：このRunで倒した人数だけを見る（過去Runの進行値は見ない）
        int defeatedThisRun = 0;
        try { defeatedThisRun = Mathf.Max(0, PlayerPrefs.GetInt("Run_DefeatedEnemyCount", 0)); } catch { defeatedThisRun = 0; }

        // ★条件：Inspectorで設定した人数以上倒していれば、お守りを報酬として付与する
        int need = 0;
        try { need = Mathf.Max(0, omamoriRewardMinDefeats); } catch { need = 0; }

        // need==0 の場合は「常に付与」扱い
        if (need > 0 && defeatedThisRun < need)
        {
            PlayerPrefs.SetInt("LastGrantedOmamoriIdV1", 0);
            PlayerPrefs.Save();
            return;
        }

        int levelForLottery = defeatedThisRun;

        try
        {
            int randomOffset = UnityEngine.Random.Range(-2, 3); // -2, -1, 0, 1, 2
            levelForLottery = defeatedThisRun + randomOffset;
        }
        catch
        {
            levelForLottery = defeatedThisRun;
        }

        levelForLottery = Mathf.Max(1, levelForLottery);

        // ★追加：前回の報酬IDが残らないよう、抽選前に必ずクリア
        PlayerPrefs.SetInt("LastGrantedOmamoriIdV1", 0);
        PlayerPrefs.Save();

        // 付与ロールは「レベル」に連動
        PlayerData.GrantRandomOmamori(levelForLottery);

    }
    catch { }
}

private int currentEnemyIndex
{
    get
    {
        try { return Mathf.Max(0, PlayerData.CurrentEnemy); }
        catch { return 0; }
    }
    set
    {
        try { PlayerData.CurrentEnemy = Mathf.Max(0, value); }
        catch { }
    }
}
private string BuildSpecialTileEffectsBulletText_ForScoring(List<string> concealed14Raw, List<List<string>> openMeldsRaw)
{
    var lines = new List<string>();

    // 1) ドラ+1（特別牌枚数ぶん）※ノーマルもドラ+1
    int specialCount = 0;

    if (concealed14Raw != null)
    {
        for (int i = 0; i < concealed14Raw.Count; i++)
            if (IsSpecialTileId(concealed14Raw[i])) specialCount++;
    }

    if (openMeldsRaw != null)
    {
        for (int m = 0; m < openMeldsRaw.Count; m++)
        {
            var meld = openMeldsRaw[m];
            if (meld == null) continue;
            for (int i = 0; i < meld.Count; i++)
                if (IsSpecialTileId(meld[i])) specialCount++;
        }
    }

    if (specialCount > 0)
        lines.Add($"・特別牌：ドラ+{specialCount}");

    // 2) ★変更：符ボーナス（レア度）表示は廃止
    //    役強化（Lv+1）を表示（プレイヤー和了時のみ意味がある）
    if (_currentScoringAttackerIsPlayer)
    {
        try
        {
            if (_specialTileTraitLvBonusTotalThisScoring > 0)
            {
                lines.Add($"・特別牌：役強化 Lv+1 ×{_specialTileTraitLvBonusTotalThisScoring}");

                if (_specialTileTraitLvBonusThisScoring != null && _specialTileTraitLvBonusThisScoring.Count > 0)
                {
                    foreach (var kv in _specialTileTraitLvBonusThisScoring)
                    {
                        var k = (kv.Key ?? "").Trim();
                        int v = kv.Value;
                        if (string.IsNullOrEmpty(k) || v <= 0) continue;
                        lines.Add($"　　{k} Lv+{v}");
                    }
                }
            }
        }
        catch { }
    }

    // 3) レジェンダリー専用効果（赤字）
    for (int fx = 1; fx <= 6; fx++)
    {
        int cnt = 0;
        try
        {
            cnt = CountLegendaryEffectTilesInScoringPool(concealed14Raw, openMeldsRaw, fx);
        }
        catch { cnt = 0; }

        if (cnt <= 0) continue;

        string t = "";
        if (fx == 1) t = "和了時：表ドラ・裏ドラを追加で1枚ずつ開く（プレイヤーのみ）";
        else if (fx == 2) t = "和了直後の敵和了ダメージを半減（敵を倒したら消滅）";
        else if (fx == 3) t = "その和了の獲得GOLDが2倍";
        else if (fx == 4) t = "満貫未満の和了なら撃・瞬・癒が2倍";
        else if (fx == 5) t = "次の局はMP消費量が半分（敵を倒したら消滅）";
        else if (fx == 6) t = "和了時 +16符";

        lines.Add($"・<color=#FF0000>{t}</color>");
    }

    if (lines.Count == 0) return "";
    return string.Join("\n", lines);
}

}
