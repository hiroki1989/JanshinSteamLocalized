// ------------------------------------------------------------------------------
// GameManager_EnemyMeldMode_Addon.cs  (no Awake/Update conflict, compile-safe)
// - Prevents reusing already-committed enemy discards within the same局
// - Keeps grey highlight until the 局 ends (discards reset / round change)
// - Resets locks next 局
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public partial class GameManager : MonoBehaviour
{

private int ApplyDamageToPlayer_WithOmamori(int baseDamage, string reason /* "enemy_win" など */)
{
    int final = Omamori_ModifyIncomingDamage(baseDamage); // 装備中お守りを考慮した最終ダメージ

    // ★ユニーク：シヴァ（東1局のプレイヤーへのダメージ50%減少）
    try
    {
        if (roundNumber == 1 && PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50))
        {
            final = Mathf.RoundToInt(final * 0.5f);
        }
    }
    catch { }
int before = playerHP;
playerHP = Mathf.Max(0, playerHP - final);

try
{
    if (final > 0 && reason != "enemy_win" && AudioManager.Instance != null)
    {
        AudioManager.Instance.PlayBattleDamageSE();
    }
}
catch { }

return final;
}

// お守りだけの被ダメ軽減率を安全に取り出す（存在すれば List/Array/Enumerable の各要素から合算）
private float GetOmamoriDamageDownOnly_Safe()
{
    try
    {
        var pdType = typeof(PlayerData);
        // 候補になり得る「現在のランで装備しているお守り」格納先名
        string[] listNames = {
            "EquippedOmamori", "equippedOmamori", "runEquippedOmamori",
            "currentOmamori", "carriedOmamori", "omamoriList"
        };
        foreach (var name in listNames)
        {
            var f = pdType.GetField(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
            if (f == null) f = pdType.GetField(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            object holder = (f != null) ? f.GetValue(null) : null; // static 優先
            var p = pdType.GetProperty(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance);
            if (holder == null && p != null) holder = p.GetValue(null, null);
            if (holder == null) continue;

            // IEnumerable を総当りして "dmgTakenDown" や "damageDown" を合計
            if (holder is System.Collections.IEnumerable en)
            {
                float sum = 0f; bool touched = false;
                foreach (var item in en)
                {
                    if (item == null) continue;
                    var it = item.GetType();
                    // 候補プロパティ／フィールド名
                    string[] cand = { "dmgTakenDown", "damageTakenDown", "dmgDown", "damageDown" };
                    foreach (var cn in cand)
                    {
                        var pf = it.GetProperty(cn, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                        if (pf != null && pf.PropertyType == typeof(float))
                        {
                            sum += (float)pf.GetValue(item, null);
                            touched = true; break;
                        }
                        var ff = it.GetField(cn, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                        if (ff != null && ff.FieldType == typeof(float))
                        {
                            sum += (float)ff.GetValue(item);
                            touched = true; break;
                        }
                    }
                }
                if (touched) return sum;
            }
        }
    }
    catch { }
    return 0f;
}
private void EnemyAddon_SetRightInfo(string skillText, string omamoriText, string ofudaText)
{
    if (_skillInfoTMP && skillText != null)
    {
        _skillInfoTMP.richText = true;
        _skillInfoTMP.text = skillText;
    }
if (_omamoriInfoTMP && omamoriText != null)
{
    _omamoriInfoTMP.richText = true;
    _omamoriInfoTMP.text = omamoriText;
}

    if (_ofudaInfoTMP && ofudaText != null)
    {
        _ofudaInfoTMP.richText = true;
        _ofudaInfoTMP.text = ofudaText;
    }
}
private void EnemyAddon_SetSkillSplitInfo(
    string skillNameText,
    string skillActionNameText,
    string skillDescText,
    string traitGekiText,
    string traitShunText,
    string traitIyuText,
    string legacyCombinedSkillText = null)
{
    bool hasSplit =
        _skillNameTMP != null ||
        _skillActionNameTMP != null ||
        _skillDescTMP != null ||
        _skillTraitGekiTMP != null ||
        _skillTraitShunTMP != null ||
        _skillTraitIyuTMP != null;

    if (hasSplit)
    {
        if (_skillNameTMP       && skillNameText       != null) _skillNameTMP.text       = skillNameText;
        if (_skillActionNameTMP && skillActionNameText != null) _skillActionNameTMP.text = skillActionNameText;
        if (_skillDescTMP       && skillDescText       != null) _skillDescTMP.text       = skillDescText;
        if (_skillTraitGekiTMP  && traitGekiText       != null) _skillTraitGekiTMP.text  = traitGekiText;
        if (_skillTraitShunTMP  && traitShunText       != null) _skillTraitShunTMP.text  = traitShunText;
        if (_skillTraitIyuTMP   && traitIyuText        != null) _skillTraitIyuTMP.text   = traitIyuText;

        if (_skillInfoTMP && legacyCombinedSkillText != null) _skillInfoTMP.text = "";
    }
    else
    {
        if (_skillInfoTMP && legacyCombinedSkillText != null) _skillInfoTMP.text = legacyCombinedSkillText;
    }
}

    [Header("Enemy Hand (Manual UI)")]
    [SerializeField] private bool useManualEnemyMeldsUI = false;

    // 手動用の親。子に Group1..Group4, Pair を置き、その下に Slot1..Slot3(または2) を配置する想定。
    [SerializeField] private RectTransform enemyMeldsManualRoot = null;

    // ★追加: 敵リーチ時の「聴牌手牌」表示用の親
    //  - 子には HorizontalLayoutGroup 等で、13枚を横一列に並べる想定
    [SerializeField] private RectTransform enemyTenpaiHandManualRoot = null;

private readonly string[] _manualGroups = { "Group1", "Group2", "Group3", "Group4", "Pair" };

private static string EnemyAddon_FixedText_Local(string key)
{
    var lm = LocalizationManager.Instance;
    if (lm == null) return key;
    return lm.GetFixedText(key);
}
private bool EnemyAddon_CanRonOnTileShapeOnly(string discardedId)
{
    string winLogic = StripTileIdForLogic(discardedId);
    if (string.IsNullOrEmpty(winLogic)) return false;

    var snapshot14 = new List<string>();

    if (_enemyHand != null)
    {
        for (int i = 0; i < _enemyHand.Count; i++)
        {
            string t = StripTileIdForLogic(_enemyHand[i]);
            if (!string.IsNullOrEmpty(t))
                snapshot14.Add(t);
        }
    }

    snapshot14.Add(winLogic);

    return IsAnyWinningShape(snapshot14);
}
private void EnemyAddon_SetOmamoriInfo(string text)
{
    if (_omamoriInfoTMP)
    {
        _omamoriInfoTMP.richText = true;
        _omamoriInfoTMP.text = text ?? "";
    }
}
    private float ComputeArtAdvanceWidth(Sprite sp, float targetTileWidthPx, float extraGapPx)
    {
        try
        {
            float factor = 1f;
            if (sp) factor = sp.rect.width > 0f ? (sp.textureRect.width / sp.rect.width) : 1f;
            return targetTileWidthPx * factor + extraGapPx;
        }
        catch { return targetTileWidthPx + extraGapPx; }
    }
    private void Addon_RefreshEnemySelectionLift()
    {
        try
        {
            // If there is a dedicated highlighter method elsewhere, call it via reflection to avoid behavior drift.
            var mi = typeof(GameManager).GetMethod("RefreshEnemySelectionLift",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if (mi != null) { mi.Invoke(this, null); return; }

            // Fallback: do nothing (no visible change when not supported).
        }
        catch {}
    }

    private string EnemyAddon_NormalizeYakuLikeToken(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string s = raw.Trim();
        string lower = s.ToLowerInvariant();

        if (s.Contains("平和") || s.Contains("ピンフ") || lower.Contains("pinfu"))
            return "PINFU";

        if (s.Contains("門前清自摸和") || s.Contains("ツモ") || s.Contains("自摸") || lower.Contains("tsumo"))
            return "TSUMO";

        if (s.Contains("立直") || s.Contains("リーチ") || lower.Contains("riichi"))
            return "RIICHI";

        if (s.Contains("役満") || lower.Contains("yakuman"))
            return "YAKUMAN";

        return "";
    }

    private bool EnemyAddon_TextHasYakuLike(string raw, string canonicalToken)
    {
        if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(canonicalToken)) return false;
        return string.Equals(
            EnemyAddon_NormalizeYakuLikeToken(raw),
            canonicalToken,
            StringComparison.Ordinal);
    }
    private bool EnemyAddon_ListHasYakuLike(IEnumerable<string> list, string canonicalToken)
    {
        if (list == null || string.IsNullOrEmpty(canonicalToken)) return false;

        foreach (var s in list)
        {
            if (EnemyAddon_TextHasYakuLike(s, canonicalToken))
                return true;
        }
        return false;
    }
private string EnemyAddon_NormalizeDisplayedYakuName(string raw)
{
    if (string.IsNullOrEmpty(raw)) return "";

    string s = raw.Trim();
    s = Regex.Replace(s, @"\s+", " ").Trim();

    if (EnemyAddon_TextHasYakuLike(s, "TSUMO"))
        return EnemyAddon_FixedText_Local("win_tsumo");

    return s;
}
    private List<string> EnemyAddon_ExtractDisplayedYakuNames(string breakdown, int doraCount)
    {
        var result = new List<string>();

        string headLocal = breakdown ?? string.Empty;
        int bar = headLocal.IndexOf('|');
        if (bar >= 0) headLocal = headLocal.Substring(0, bar);

        headLocal = headLocal.Trim();
        if (headLocal.Length > 0)
        {
            // まず「(+数字)」だけを安全に除去する。
            // 例:
            //   平和(+1) + 清一色(+6)   →   平和 + 清一色
            //   役牌×2(+2)              →   役牌×2
            //   九蓮宝燈(+13)           →   九蓮宝燈
            string namesOnly = Regex.Replace(headLocal, @"\(\+\d+\)", "").Trim();

            // 役の区切りは " + " を優先して解釈する。
            // breakdown の現在仕様では役同士はこの形で連結されているので、
            // 単純な '+' 分割より安全。
            string[] parts = namesOnly.Split(
                new string[] { " + " },
                StringSplitOptions.RemoveEmptyEntries
            );

            for (int i = 0; i < parts.Length; i++)
            {
                string name = EnemyAddon_NormalizeDisplayedYakuName(parts[i]);
                if (!string.IsNullOrEmpty(name))
                    result.Add(name);
            }

            // もし 1 件も取れなかった場合だけ、最後の保険として全体を 1 件扱いする
            if (result.Count == 0 && !string.IsNullOrEmpty(namesOnly))
            {
                string fallbackName = EnemyAddon_NormalizeDisplayedYakuName(namesOnly);
                if (!string.IsNullOrEmpty(fallbackName))
                    result.Add(fallbackName);
            }
        }
if (doraCount > 0)
    result.Add(string.Format(EnemyAddon_FixedText_Local("dora_count_format"), doraCount));
        return result;
    }

    private bool EnemyAddon_HasYakuKey(IEnumerable<string> keys, string targetKey)
    {
        if (keys == null || string.IsNullOrEmpty(targetKey)) return false;

        foreach (var k in keys)
        {
            if (string.Equals(k, targetKey, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private bool EnemyAddon_HasAnyYakumanKey(IEnumerable<string> keys)
    {
        if (keys == null) return false;

        foreach (var k in keys)
        {
            if (!string.IsNullOrEmpty(k))
                return true;
        }
        return false;
    }

    // Safely add to tsumo penalty, using whichever backing field exists in the build.
    private void AddonAddTsumoPenaltySafely(int v)
    {
        try
        {
            var flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            string[] names = { "tsumoPenalty", "tsumoPenaltyCount", "enemyTsumoPenalty", "tsumoPenaltyStacks" };
            foreach (var nm in names)
            {
                var f = typeof(GameManager).GetField(nm, flags);
                if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(short) || f.FieldType == typeof(long)))
                {
                    int cur = System.Convert.ToInt32(f.GetValue(this));
                    f.SetValue(this, cur + System.Math.Max(0, v));
                    return;
                }
            }
        }
        catch {}
        _addonLocalTsumoPenalty += System.Math.Max(0, v);
    }
    private int _addonLocalTsumoPenalty = 0; // local fallback; read only by this addon logic

    // Create a tile image into a parent. If a layout group exists, we let it place the child.
    // Otherwise we position tiles horizontally using 'x' (pixels).
private void CreateTileImage(Transform parent, string tileId, ref float x, int targetWidthPx)
{
    if (!parent) return;
    RectTransform rt = null;
    try
    {
        // Prefer using existing tile prefab if available
        if (tilePrefab)
        {
            var inst = UnityEngine.Object.Instantiate(tilePrefab, parent);
            SetTileSprite(inst, tileId);
            foreach (var img in inst.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                img.raycastTarget = false;
            rt = inst.transform as RectTransform;
        }
        else
        {
            var go = new GameObject("Tile_" + (tileId ?? "null"),
                                    typeof(RectTransform),
                                    typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.preserveAspect = true;
            var sp = GetTileSprite(tileId);
            if (sp) img.sprite = sp;
            rt = go.transform as RectTransform;
        }
    }
    catch { }
if (rt)
{
        // Size based on sprite aspect to avoid visual left/right paddings when spacing=0
        float w = System.Math.Max(1, targetWidthPx);
        float h;
        try {
            var imgForAspect = (rt != null) ? (rt.GetComponent<UnityEngine.UI.Image>()) : null;
            var spLocal = imgForAspect ? imgForAspect.sprite : null;
            if (spLocal)
            {
                float ratio = spLocal.rect.height / Mathf.Max(1f, spLocal.rect.width);
                h = w * ratio;
            }
            else
            {
                h = w * 1.35f; // fallback
            }
        } catch { h = w * 1.35f; }

        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   h);

        // If prefab path, try to stretch the art to fill width precisely
        try {
            var art = rt.Find("Art");
            if (art)
            {
                var artImgTf = art.Find("Image") as RectTransform;
                if (artImgTf)
                {
                    artImgTf.anchorMin = new UnityEngine.Vector2(0, 0.5f);
                    artImgTf.anchorMax = new UnityEngine.Vector2(1, 0.5f);
                    artImgTf.pivot     = new UnityEngine.Vector2(0.5f, 0.5f);
                    artImgTf.offsetMin = new UnityEngine.Vector2(0, -h * 0.5f);
                    artImgTf.offsetMax = new UnityEngine.Vector2(0,  h * 0.5f);
                }
            }
        } catch {}

bool hasLayout = parent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() ||
                 parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>()   ||
                 parent.GetComponent<UnityEngine.UI.GridLayoutGroup>();

bool isScoringManualParent =
    parent == scoringPlayerTilesManual ||
    parent == scoringEnemyTilesManual;

// ★追加：スコアパネル配下では牌自身にも LayoutElement を付けて幅を固定する
if (isScoringManualParent)
{
    var le = rt.GetComponent<UnityEngine.UI.LayoutElement>();
    if (le == null) le = rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();

    le.minWidth = w;
    le.preferredWidth = w;
    le.flexibleWidth = 0f;

    le.minHeight = h;
    le.preferredHeight = h;
    le.flexibleHeight = 0f;
}

if (!hasLayout)
{
    rt.anchorMin = new UnityEngine.Vector2(0, 0.5f);
    rt.anchorMax = new UnityEngine.Vector2(0, 0.5f);
    rt.pivot     = new UnityEngine.Vector2(0, 0.5f);
    rt.anchoredPosition = new UnityEngine.Vector2(x, 0);

    if (isScoringManualParent)
    {
        x += w;
    }
    else
    {
        x += w + Mathf.Max(0f, enemyOverlayManualTileGapPx);
    }
}
}
    }

    // Resolve a tile sprite by id. We try several strategies to avoid coupling.
    private Sprite GetTileSprite(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        // 1) Ask SetTileSprite pipeline by creating a temporary Image (most consistent look), but avoid allocations:
        try
        {
            // If SetTileSprite exists, we emulate by grabbing a sprite from tilePrefab's Image after setting
            if (tilePrefab != null)
            {
                var temp = UnityEngine.Object.Instantiate(tilePrefab);
                try
                {
                    SetTileSprite(temp, id);
                    var img = temp.GetComponentInChildren<UnityEngine.UI.Image>(true);
                    if (img && img.sprite) return img.sprite;
                }
                finally { UnityEngine.Object.Destroy(temp); }
            }
        } catch {}

        // 2) Find already-loaded sprites with matching name
        try
        {
            var all = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i=0;i<all.Length;i++)
            {
                var s = all[i];
                if (!s) continue;
                if (string.Equals(s.name, id, System.StringComparison.OrdinalIgnoreCase)) return s;
            }
        } catch {}

        return null;
    }

    // addon: track if last scoring belonged to enemy (to avoid progressing round)
    private bool _addonLastScoringWasEnemy = false;

    // ===== Addon state =====
    // Index-based set (legacy). May become stale if child order changes, so we also keep instanceIDs.
    private readonly HashSet<int> _meldCommittedIndices = new HashSet<int>();
    // Robust lock: GameObject instanceIDs of discard tiles used in enemy melds during the current 局
    private readonly HashSet<int> _committedDiscardInstanceIDs = new HashSet<int>();

    private readonly List<List<string>> _enemyCommittedMelds = new List<List<string>>();
    private List<string> _enemyCommittedPair = null;

    // ★直近の敵和了がリーチ／ツモだったかどうか
    //  - 点数計算（EnemyAddon_ComputeScoreDetailLikePlayer → __EnemyAddon_FallbackMahjongScoreDetail）
    //  - 役行の表示（BuildYakuLine）
    //  - カットインの「ツモ／ロン」タイトル
    // などから参照する。
    private bool _enemyLastWinWasRiichi = false;
private bool _enemyLastWinWasTsumo  = false;

    // ★追加：直近の敵和了牌（ロン/ツモ共通）
    private string _enemyLastWinTileId = null;
    // ★追加: 敵の手牌構築ステート
    //  3メンツ1雀頭まで作れたか / ターツを確定したか / テンパイ中か / リーチ宣言済みか
    private bool _enemyHasBaseHand       = false;          // 3メンツ＋1雀頭が完成済み
    private List<string> _enemyTaatsu    = null;           // テンパイ用のターツ2枚（tileId×2）
    private int _enemyTaatsuIdx0         = -1;             // ターツの1枚目が出た敵捨て牌 index
    private int _enemyTaatsuIdx1         = -1;             // ターツの2枚目が出た敵捨て牌 index
    private bool _enemyIsInTenpai        = false;          // テンパイ状態か
    private bool _enemyIsInRiichi        = false;          // リーチ宣言済みか
    // ★追加: 敵リーチのカットイン演出が再生中かどうか
    private bool _enemyRiichiCutinRunning = false;
    private int _lastProcessedEnemyDiscardCount = 0;
    private int _observedRoundNumber = -1;
    private int _observedEnemyIndex  = -1;
    //   敵が和了したとき／敵が入れ替わったときの手牌リセットは
    //   __EnemyWin_ShowCutinAndScoring_Flow_Co および Addon_DetectStageChange() 側で行う。
    // ※同じ敵との対局中では「敵の手牌構築ステート」は維持し、
    //   捨て牌ロック／監視まわりだけを局単位でリセットする。
    //   敵が和了したとき／敵が入れ替わったときの手牌リセットは
    //   __EnemyWin_ShowCutinAndScoring_Flow_Co および Addon_DetectStageChange() 側で行う。
private void EnemyAddon_ResetStateForNewHand()
{
    // --- 捨て牌ロック情報のみリセット ---
    _meldCommittedIndices.Clear();
    _committedDiscardInstanceIDs.Clear();

    // ★重要★
    // 監視値を -1 に戻すと、次フレームの Addon_DetectStageChange() で
    // enemyChanged が必ず true になり、テンパイ/リーチ状態が false に戻されて
    // 立直用UIが破棄される。
    // → “同じ敵との対局中”の次局開始では、現在値へ同期して誤発火を防ぐ。
    _observedRoundNumber = Addon_GetIntField("roundNumber", _observedRoundNumber);
    _observedEnemyIndex  = Addon_GetCurrentEnemyIndex();
    _lastProcessedEnemyDiscardCount = Addon_GetEnemyDiscardCount();
    if (_enemyHasWonThisHand)
    {
        return;
    }
    // 「同じ敵との対局中」は敵の手牌・リーチ状態をそのまま維持したいので、
    // ここでは手動レイアウト用の UI を破壊／作り直さない。
    // どのUIを表示するかだけ、現在のリーチ状態に合わせて切り替える。
    if (enemyMeldsManualRoot)
    {
        enemyMeldsManualRoot.gameObject.SetActive(!_enemyIsInRiichi);
    }

    if (enemyTenpaiHandManualRoot)
    {
        enemyTenpaiHandManualRoot.gameObject.SetActive(_enemyIsInRiichi);
    }
}
/// <summary>
/// 流局など、「敵の手牌を完全に作り直したい」場合用のフルリセット。
/// ベース手牌／副露／リーチ・聴牌状態をすべて初期化したうえで、
/// 既存の軽量リセット（捨て牌ロック＋UI）も併用する。
/// </summary>
private void EnemyAddon_ResetStateForNewHand_Full()
{
    // --- 手牌構築ステートを完全リセット ---
    _enemyHasBaseHand    = false;
    _enemyCommittedMelds.Clear();
    _enemyCommittedPair  = null;

    _enemyTaatsu         = null;
    _enemyTaatsuIdx0     = -1;
    _enemyTaatsuIdx1     = -1;

    _enemyIsInRiichi     = false;
    _enemyIsInTenpai     = false;

    // --- ここが重要：見た目も「次局開始直後」に即リセットする ---
    // リーチ用手牌 UI を即クローズ（子要素も破棄）
    if (enemyTenpaiHandManualRoot)
    {
        for (int i = enemyTenpaiHandManualRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(enemyTenpaiHandManualRoot.GetChild(i).gameObject);
        enemyTenpaiHandManualRoot.gameObject.SetActive(false);
    }

    // 通常用(4メンツ1雀頭) UI を表示
    if (enemyMeldsManualRoot)
        enemyMeldsManualRoot.gameObject.SetActive(true);

    // 通常UI側の中身も即時再描画して「次フレーム待ち」を無くす
    try { RefreshEnemyMeldsPanel(); } catch { /* 失敗しても進行は止めない */ }
    try { GreyCommittedDiscards(); } catch { /* 同上 */ }

    // --- 捨て牌ロック／監視カウンタ等の既存リセットは併用 ---
    EnemyAddon_ResetStateForNewHand();
}

    // UI refs
    private RectTransform _enemyMeldsPanel;
    private RectTransform _enemyMeldsContent;

    private RectTransform _enemyWinOverlay;
    private TextMeshProUGUI _enemyWinTitle;
    private RectTransform _enemyWinTiles;
    private TextMeshProUGUI _enemyWinBody;
    private Button _enemyWinOk;
    
    // ===== Enemy Meld Row Layout (Inspector adjustable) =====
    [Header("Enemy Meld Row Layout")]
    [SerializeField] private float enemyMeldRowWidth = 0f;         // px; 0 = auto (no width constraint, pure left-justify)
    [SerializeField] private float enemyMeldTileWidth = 54f;       // px; base tile width for enemy meld row
    [SerializeField] private float enemyMeldGroupGapInTiles = 0.05f; // gap as fraction of tile width (default 0.05 = 5% of a tile)
    [SerializeField] private float enemyMeldIntraTileGapInTiles = 0f; // within-group spacing as fraction of tile width (default 0 = none)
     [SerializeField] private bool  enemyMeldAutoAdjustGapWithinWidth = true;
     [SerializeField] private float enemyOverlayManualTileGapPx = 4f; // px; used when placing tiles by x (no layout group)

     // ★追加: 敵リーチ時の聴牌表示の牌幅(px)
     [SerializeField] private float enemyTenpaiManualTileWidthPx = 54f;
 // if RowWidth>0, shrink gap (never expand) to fit
// Tunables
[SerializeField] private int damagePerHan = 1000;
[SerializeField] private int baseRonDamage = 1000;
[SerializeField] private bool useAutoRightInfoPanels = false; // ★追加：既定 false（手動運用）

// ★追加：旧仕様（捨て牌からメンツ/雀頭を拾う＆捨て牌グレーアウト）を動かすか
// 新仕様（敵も配牌＋4枚ツモで13枚構成）では false 推奨
[SerializeField] private bool enableLegacyEnemyHandFromDiscards = false;

[SerializeField] private TMPro.TextMeshProUGUI _skillInfoTMP;    // ★Inspector で手動割り当て（旧：1枠でまとめ表示）

// ★追加：RunSceneのスキル説明を6分割で表示する（Inspectorで割り当て）
[SerializeField] private TMPro.TextMeshProUGUI _skillNameTMP;        // ①職業名
[SerializeField] private TMPro.TextMeshProUGUI _skillActionNameTMP;  // ②発動能力名
[SerializeField] private TMPro.TextMeshProUGUI _skillDescTMP;        // ③スキル内容
[SerializeField] private TMPro.TextMeshProUGUI _skillTraitGekiTMP;   // ④撃の該当役（項目名は書かない）
[SerializeField] private TMPro.TextMeshProUGUI _skillTraitShunTMP;   // ⑤瞬の該当役（項目名は書かない）
[SerializeField] private TMPro.TextMeshProUGUI _skillTraitIyuTMP;    // ⑥癒の該当役（項目名は書かない）
[SerializeField] private TMPro.TextMeshProUGUI _omamoriInfoTMP;  // ★Inspector で手動割り当て
[SerializeField] private TMPro.TextMeshProUGUI _ofudaInfoTMP;     // ★Inspector で手動割り当て

// ==== Right Info Icons / Split Ofuda (Manual UI) ====
// お守りアイコン（装備時だけ表示、Tintをレア度色へ）
[SerializeField] private UnityEngine.UI.Image _omamoriIconImage;

// お札：最大3つ装備 → アイコン3つ＋テキスト3つ（未装備スロットは非表示/空表示）
[SerializeField] private UnityEngine.UI.Image[] _ofudaIconImages = new UnityEngine.UI.Image[3];
[SerializeField] private TMPro.TextMeshProUGUI[] _ofudaInfoTMPs = new TMPro.TextMeshProUGUI[3];

// ==== Enemy Win Overlay (Manual UI) ====
[SerializeField] private bool useManualEnemyWinUI = true;
[SerializeField] private RectTransform enemyWinOverlayManualRoot; // 背景全体（黒半透明を自前で設定）
[SerializeField] private TextMeshProUGUI enemyWinTitleManual;     // タイトル（例：「敵の和了」）
[SerializeField] private TextMeshProUGUI enemyWinBodyManual;      // 説明文
[SerializeField] private Button enemyWinOkManual;                  // OK ボタン
[SerializeField] private RectTransform enemyWinTilesManual;  // スコアパネル：敵の和了手牌描画先

// ▼追加：未割当て時に毎フレーム再試行しないための一度きりガード
private bool _enemyWinOverlayInitAttempted = false;
             // OK ボタン

    // Lifecycle
    private bool _addonInitialized = false;
    private bool _addonEnabled = false;

    public void EnableEnemyMeldModeAddon()
    {
        if (_addonEnabled) return;
        _addonEnabled = true;
        StartCoroutine(__Addon_Loop());
    }

    private System.Collections.IEnumerator __Addon_Loop()
    {
        yield return null;
        while (true)
        {
            __Addon_InitializeIfNeeded();
            __Addon_Tick();
            yield return null;
        }
    }

    private void __Addon_InitializeIfNeeded()
    {
        if (_addonInitialized) return;
        _addonInitialized = true;

        // Disable legacy 3-turn system if present
        try
        {
            var f = typeof(GameManager).GetField("enemyAttackIntervalTurns",
                BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (f != null) f.SetValue(this, int.MaxValue/2);
        } catch {}

        EnsureEnemyMeldsPanel();
        EnsureEnemyWinOverlay();
        RefreshEnemyMeldsPanel();
        GreyCommittedDiscards();

        _observedRoundNumber = Addon_GetIntField("roundNumber", -1);
        _observedEnemyIndex  = Addon_GetCurrentEnemyIndex();
        _lastProcessedEnemyDiscardCount = Addon_GetEnemyDiscardCount();
    if (useAutoRightInfoPanels)
    {
        EnsureEnemyMeldsPanel();
        EnsureEnemyWinOverlay();
        RefreshEnemyMeldsPanel();
        GreyCommittedDiscards();
    }

    _observedRoundNumber = Addon_GetIntField("roundNumber", -1);
    _observedEnemyIndex  = Addon_GetCurrentEnemyIndex();
    _lastProcessedEnemyDiscardCount = Addon_GetEnemyDiscardCount();
}
private bool _addonUiDirty = true;
private bool _addonGreyDirty = true;
private bool _lastEnemyIsInRiichi_ForTick = false;
private void EnemyAddon_SetOfudaSplitInfo(string[] ofudaTexts)
{
    bool hasSplit =
        _ofudaInfoTMPs != null &&
        _ofudaInfoTMPs.Length > 0 &&
        _ofudaInfoTMPs.Any(t => t != null);

    if (!hasSplit)
        return;

    for (int i = 0; i < _ofudaInfoTMPs.Length; i++)
    {
        var t = _ofudaInfoTMPs[i];
        if (!t) continue;

        t.richText = true;

        string s = "";
        if (ofudaTexts != null && i < ofudaTexts.Length && ofudaTexts[i] != null)
            s = ofudaTexts[i];

        t.text = s;
    }

    // 旧1枠が残っている場合は空にして二重表示を防ぐ
    if (_ofudaInfoTMP) _ofudaInfoTMP.text = "";
}

private void __Addon_Tick()
{
    __Addon_InitializeIfNeeded();

    // ----------------------------
    // ステージ変化/リセット検知
    // ----------------------------
    bool roundOrEnemyChanged = Addon_DetectStageChange();
    if (roundOrEnemyChanged)
    {
        _addonUiDirty = true;
        _addonGreyDirty = true;
    }

    // ----------------------------
    // 敵捨て牌数の変化（=敵が捨てたタイミング）だけ処理
    // ----------------------------
    int cur = Addon_GetEnemyDiscardCount();
    if (cur != _lastProcessedEnemyDiscardCount)
    {
        _lastProcessedEnemyDiscardCount = cur;

        if (enableLegacyEnemyHandFromDiscards)
        {
            // 旧：捨て牌→メンツ推定モード（必要な場合のみ）
            TryProgressEnemyHandFromDiscards();
        }

        // 捨て牌が増えた/減った瞬間だけUI更新
        _addonUiDirty = true;
        _addonGreyDirty = true;
    }

    // ----------------------------
    // リーチ状態が変わった時だけUI更新
    // ----------------------------
    if (_lastEnemyIsInRiichi_ForTick != _enemyIsInRiichi)
    {
        _lastEnemyIsInRiichi_ForTick = _enemyIsInRiichi;
        _addonUiDirty = true;
    }

    // ----------------------------
    // 重いUI処理は「Dirtyの時だけ」
    // ----------------------------
    if (_addonGreyDirty)
    {
        GreyCommittedDiscards();
        _addonGreyDirty = false;
    }

    if (_addonUiDirty)
    {
        if (!_enemyIsInRiichi)
        {
            RefreshEnemyMeldsPanel();
        }
        _addonUiDirty = false;
    }

    // 軽い処理はそのまま
    Addon_RefreshEnemySelectionLift();
}
private int AddonSafe_GetIntByNames(string[] names, int fallback)
{
    try
    {
        var flags = System.Reflection.BindingFlags.Instance
                  | System.Reflection.BindingFlags.Public
                  | System.Reflection.BindingFlags.NonPublic;

        for (int i = 0; i < names.Length; i++)
        {
            var n = names[i];
            var f = GetType().GetField(n, flags);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(this);

            var p = GetType().GetProperty(n, flags);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead)
                return (int)p.GetValue(this, null);
        }
    }
    catch { /* 何もしないでfallback */ }

    return fallback;
}

private int AddonSafe_GetRoundNumber()
{
    // プロジェクト側の実装揺れに耐える候補名
    return AddonSafe_GetIntByNames(
        new[] { "roundNumber", "_roundNumber", "_currentRound", "_currentRoundNumber" },
        _observedRoundNumber
    );
}

private int AddonSafe_GetEnemyIndex()
{
    return AddonSafe_GetIntByNames(
        new[] { "_currentEnemyIndex", "currentEnemyIndex", "_enemyIndex", "enemyIndex" },
        _observedEnemyIndex
    );
}
private bool Addon_DetectStageChange()
{
    int nowRound = AddonSafe_GetRoundNumber();
    int nowEnemy = AddonSafe_GetEnemyIndex();

    bool roundChanged = (nowRound != _observedRoundNumber);
    bool enemyChanged = (nowEnemy != _observedEnemyIndex);
    bool roundOrEnemyChanged = (roundChanged || enemyChanged);

    // 「敵捨て牌が0に戻った」＝局切替の可能性
    bool discardsReset = (_lastProcessedEnemyDiscardCount > 0) && (Addon_GetEnemyDiscardCount() == 0);

    if (roundOrEnemyChanged || discardsReset)
    {
        _observedRoundNumber = nowRound;
        _observedEnemyIndex  = nowEnemy;

        _lastProcessedEnemyDiscardCount = Addon_GetEnemyDiscardCount();
        _meldCommittedIndices.Clear();
        _committedDiscardInstanceIDs.Clear();

        // ★重い処理は Dirty に寄せる
        _addonGreyDirty = true;
        _addonUiDirty = true;
    }

    if (enemyChanged)
    {
        _enemyHasBaseHand = false;

        // このファイルに実在する構造をリセット（_enemyMentsu/_enemyJantou は存在しない）
        _enemyCommittedMelds.Clear();
        _enemyCommittedPair = null;

        _enemyTaatsu = null;
        _enemyTaatsuIdx0 = -1;
        _enemyTaatsuIdx1 = -1;

        _enemyIsInTenpai = false;
        _enemyIsInRiichi = false;
        _enemyRiichiWaits.Clear();

        // Tenpai UI は破棄（敵切替時だけなのでOK）
        if (enemyTenpaiHandManualRoot != null)
        {
            for (int i = enemyTenpaiHandManualRoot.childCount - 1; i >= 0; i--)
                Destroy(enemyTenpaiHandManualRoot.GetChild(i).gameObject);
        }

        _addonUiDirty = true;
        _addonGreyDirty = true;
    }

    return (roundOrEnemyChanged || discardsReset);
}


    // Helper record for discards triplet
    private struct DiscardTrip
    {
        public int idx;
        public string id;
        public GameObject go;
        public DiscardTrip(int i, string s, GameObject g){ idx=i; id=s; go=g; }
    }
// Build current snapshot of discards with GameObject refs
private List<DiscardTrip> Addon_BuildDiscardTrips()
{
    var ids = Addon_ReadEnemyDiscardIds_Snapshot();
    var list = new List<DiscardTrip>();
    if (ids == null) return list;

    var container = LocateEnemyDiscardContainer();
    for (int i = 0; i < ids.Count; i++)
    {
        GameObject go = null;
        if (container && i < container.childCount) go = container.GetChild(i).gameObject;
        list.Add(new DiscardTrip(i, ids[i], go));
    }
    return list;
}


    // ===== Core: progress enemy hand from discards =====
    private void TryProgressEnemyHandFromDiscards()
    {
        if (_enemyHasWonThisHand) return;
        var trips = Addon_BuildDiscardTrips();
        if (trips.Count == 0) return;

var unused = new List<DiscardTrip>();

// ★重要：当ターンに敵が捨てた4枚（lastEnemyTurnTiles分）は、プレイヤーの鳴き/ロン判定のため
// Add-on側で「採用ロック」しない（= CommitGroup の対象にしない）
int curDiscardCount = Addon_GetEnemyDiscardCount(); // trips.Count と同じ前提
int startThisTurn = Math.Max(0, curDiscardCount - (lastEnemyTurnTiles != null ? lastEnemyTurnTiles.Count : 0));

foreach (var t in trips)
{
    // ★当ターン捨て牌は除外（ここが阻害の本丸）
    if (t.idx >= startThisTurn) continue;

    if (t.go != null && _committedDiscardInstanceIDs.Contains(t.go.GetInstanceID())) continue;
    if (_meldCommittedIndices.Contains(t.idx)) continue;
    if (string.IsNullOrEmpty(t.id)) continue;
    unused.Add(t);
}
if (unused.Count == 0) return;

        // ---------- リーチ中のツモ判定 ----------
        if (_enemyIsInRiichi && _enemyIsInTenpai && _enemyTaatsu != null && _enemyTaatsu.Count == 2)
        {
            // ★ターツから待ち牌候補を列挙し、このリストに含まれる牌だけを「和了候補」として扱う
            var waits = EnemyAddon_GetWaitTilesForTaatsu(_enemyTaatsu);
            if (waits == null || waits.Count == 0)
                goto AfterRiichiTsumoCheck;
            // ★待ち牌比較は正規化して行う（*_sp / * を吸収）
            var waitsLogic = new HashSet<string>();
            for (int wi = 0; wi < waits.Count; wi++)
            {
                var w = StripTileIdForLogic(waits[wi]);
                if (!string.IsNullOrEmpty(w)) waitsLogic.Add(w);
            }
            string tsumoId = null;

            // 1) まずは lastEnemyTurnTiles（この敵ターンで新たに引いた牌）から探す
            if (lastEnemyTurnTiles != null && lastEnemyTurnTiles.Count > 0)
            {
                for (int i = lastEnemyTurnTiles.Count - 1; i >= 0; i--)
                {
                    var id = lastEnemyTurnTiles[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    if (waitsLogic.Contains(StripTileIdForLogic(id)))
                    {
                        tsumoId = id;
                        break;
                    }
                }
            }

            // 2) lastEnemyTurnTiles に含まれていないケース（例：過去ターンの牌しか残っていない）では、
            //    今回の trips のうち「最新の 1 枚」も保険として見る
            if (tsumoId == null && trips.Count > 0)
            {
                var latestTrip = trips[trips.Count - 1];
                if (!string.IsNullOrEmpty(latestTrip.id) && waitsLogic.Contains(StripTileIdForLogic(latestTrip.id)))
                {
                    tsumoId = latestTrip.id;
                }
            }

            // 3) それでも見つからなければ、今回の「未使用捨て牌（unused）」に含まれる牌も総当たりで確認する。
            //    これで、lastEnemyTurnTiles や trips に乗り切らなかった特殊な増分も拾える。
            if (tsumoId == null && unused != null && unused.Count > 0)
            {
                for (int i = unused.Count - 1; i >= 0; i--)
                {
                    var t = unused[i];
                    if (string.IsNullOrEmpty(t.id)) continue;
                    if (waitsLogic.Contains(StripTileIdForLogic(t.id)))
                    {
                        tsumoId = t.id;
                        break;
                    }
                }
            }

            if (tsumoId != null)
            {
                // リーチ中に待ち牌をツモったら必ず和了する
                _enemyLastWinWasRiichi = true;
                _enemyLastWinWasTsumo  = true;

                // リーチツモとして和了処理
                EnemyAddon_DoTsumoWin(tsumoId);
                return;
            }
        }

    AfterRiichiTsumoCheck:

        // ---------- まだ基礎手(3メンツ＋1雀頭)ができていない場合は、従来通り Pon/Chi/Pair を作る ----------
        if (!_enemyHasBaseHand)
        {
            bool progressed = false;

            // 3メンツになるまでポン・チーを優先して埋める
            while (_enemyCommittedMelds.Count < 3)
            {
                if (TryMakePon(unused)) { progressed = true; continue; }
                if (TryMakeChi(unused)) { progressed = true; continue; }
                break;
            }

            // 雀頭がまだ無ければ対子を作る
            if (_enemyCommittedPair == null)
            {
                if (TryMakePair(unused)) progressed = true;
            }

            if (_enemyCommittedMelds.Count >= 3 && _enemyCommittedPair != null)
                _enemyHasBaseHand = true;

            if (!progressed)
            {
                // 進展なしならここで終了
                return;
            }
        }

// ---------- 基礎手が完成済み＆まだテンパイしていないなら、ターツ候補から最良のものを選んでリーチ ----------
if (_enemyHasBaseHand && !_enemyIsInTenpai)
{
    if (EnemyAddon_TryPickBestTaatsu(unused, out int idx0, out int idx1, out var taatsu))
    {
        _enemyTaatsu     = taatsu;
        _enemyTaatsuIdx0 = idx0;
        _enemyTaatsuIdx1 = idx1;

        // ★追加(A)：ターツ確定直後に待ち牌（リーチ待ち）を必ず再生成
        EnemyAddon_RebuildRiichiWaits();

        _enemyIsInTenpai = true;

        // ★追加(B)：リーチ宣言（フラグ）前後でも待ち牌を必ず再生成
        _enemyIsInRiichi = true;     // 条件を満たしたら即リーチ
        _enemyIsRiichi   = true;     // ★重要：ロン判定側はこのフラグを見ている
        EnemyAddon_RebuildRiichiWaits();

        _enemyRiichiDeclaredTurnCounter = _enemyTurnCounter; // ★修正：この敵ターンが「リーチ宣言ターン」

        _enemyLastWinWasRiichi = true;


                // ロック（この2枚は敵の手牌に取り込まれた扱い）
                foreach (var t in trips)
                {

                    if (t.idx == idx0 || t.idx == idx1)
                    {
                        _meldCommittedIndices.Add(t.idx);
                        if (t.go != null)
                            _committedDiscardInstanceIDs.Add(t.go.GetInstanceID());
                    }
                }

                // リーチ用 UI に切り替え（まずカットイン → 3秒後に聴牌UI）
                StartCoroutine(EnemyAddon_ShowRiichiCutinThenEnterTenpaiUI());
            }
        }
    }

private bool EnemyAddon_WillEnemyWinThisEnemyTurn()
{
    if (!_enemyIsInRiichi) return false;

    // ★仕様⑤：リーチ宣言したターンは和了しない
    if (_enemyTurnCounter == _enemyRiichiDeclaredTurnCounter) return false;


    // ★このターンのツモ4枚は _enemyTurnHistory に積まれている
    if (_enemyTurnHistory == null || _enemyTurnHistory.Count == 0) return false;
    var thisTurnDraws = _enemyTurnHistory.Last();
    if (thisTurnDraws == null) return false;

    if (_enemyRiichiWaits == null || _enemyRiichiWaits.Count == 0) return false;

    foreach (var t in thisTurnDraws)
    {
        var core = StripStar(t);
        if (string.IsNullOrEmpty(core)) continue;
        core = StripTileIdForLogic(core);
        if (string.IsNullOrEmpty(core)) continue;

        if (_enemyRiichiWaits.Contains(core)) return true;
    }
    return false;
}

    // ★追加：リーチ待ち牌を必ず再生成（ロン判定・ツモ判定で共通利用）
    private void EnemyAddon_RebuildRiichiWaits()
    {
        if (_enemyRiichiWaits == null) return;

        _enemyRiichiWaits.Clear();

        if (_enemyTaatsu == null || _enemyTaatsu.Count != 2) return;

        var waits = EnemyAddon_GetWaitTilesForTaatsu(_enemyTaatsu);
        if (waits == null || waits.Count == 0) return;

        for (int i = 0; i < waits.Count; i++)
        {
            var w = waits[i];
            if (string.IsNullOrEmpty(w)) continue;

            // ★ロン側は StripStar(raw) を使って比較しているので、ここも同じ基準に寄せる
            w = StripStar(w);
            if (string.IsNullOrEmpty(w)) continue;

            // ★さらに *_sp 等も吸収（手牌・ツモ比較を安定させる）
            w = StripTileIdForLogic(w);
            if (string.IsNullOrEmpty(w)) continue;

            if (!_enemyRiichiWaits.Contains(w))
                _enemyRiichiWaits.Add(w);
        }
    }


    private bool TryMakePon(List<DiscardTrip> unused)
    {
        var byId = unused.GroupBy(t => t.id).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var kv in byId)
        {
            if (kv.Value.Count >= 3)
            {
                var pick = kv.Value.Take(3).ToList();
                CommitGroup(pick, false);
                foreach (var p in pick) unused.Remove(p);
                ApplyEffectsForTiles(pick.Select(p=>p.id));
                return true;
            }
        }
        return false;
    }


    private bool TryMakeChi(List<DiscardTrip> unused)
    {
        var suitMap = new Dictionary<int, HashSet<int>> { {0,new HashSet<int>()},{1,new HashSet<int>()},{2,new HashSet<int>()} };
        var idxMap  = new Dictionary<(int suit,int num), List<DiscardTrip>>();
        foreach (var t in unused.ToList())
        {
            if (!AddonTryParseSuitNum(t.id, out int s, out int n)) continue;
            if (s<0 || s>2) continue; if (n<1 || n>9) continue;
            suitMap[s].Add(n);
            if (!idxMap.ContainsKey((s,n))) idxMap[(s,n)] = new List<DiscardTrip>();
            idxMap[(s,n)].Add(t);
        }
        for (int s=0; s<=2; s++)
        {
            for (int n=1; n<=7; n++)
            {
                if (suitMap[s].Contains(n) && suitMap[s].Contains(n+1) && suitMap[s].Contains(n+2))
                {
                    var t0 = idxMap[(s,n)].First();
                    var t1 = idxMap[(s,n+1)].First();
                    var t2 = idxMap[(s,n+2)].First();
                    var ids = new List<string>{ AddonIdOf(s,n), AddonIdOf(s,n+1), AddonIdOf(s,n+2) };
                    CommitGroup(new List<DiscardTrip>{t0,t1,t2}, false);
                    unused.RemoveAll(x=>x.idx==t0.idx || x.idx==t1.idx || x.idx==t2.idx);
                    ApplyEffectsForTiles(ids);
                    return true;
                }
            }
        }
        return false;
    }

    private bool TryMakePair(List<DiscardTrip> unused)
    {
        var byId = unused.GroupBy(t => t.id).ToDictionary(g=>g.Key, g=>g.ToList());
        foreach (var kv in byId)
        {
            if (kv.Value.Count >= 2)
            {
                var pick = kv.Value.Take(2).ToList();
                CommitGroup(pick, true);
                foreach (var p in pick) unused.Remove(p);
                ApplyEffectsForTiles(pick.Select(p=>p.id));
                return true;
            }
        }
        return false;
    }
private void CommitGroup(List<DiscardTrip> picks, bool isPair)
{
    foreach (var p in picks)
    {
        _meldCommittedIndices.Add(p.idx); // legacy
        if (p.go != null) _committedDiscardInstanceIDs.Add(p.go.GetInstanceID()); // robust lock
        GreyHighlightEnemyDiscard(p.idx);
    }

    var tiles = picks.Select(p => p.id).ToList();

    // ★重要：役判定フォールバックが順子判定で並び順を前提にするため、ここで必ずソートして格納する
    //  - ポンは同牌なので影響なし
    //  - チーはここをやらないと順子判定が落ちやすく、結果「リーチ・ツモ」だけが残る
    if (!isPair && tiles.Count == 3)
    {
        tiles.Sort((a, b) =>
        {
            if (!AddonTryParseSuitNum(a, out int sa, out int na)) return -1;
            if (!AddonTryParseSuitNum(b, out int sb, out int nb)) return  1;
            int c = sa.CompareTo(sb);
            return (c != 0) ? c : na.CompareTo(nb);
        });
    }

    if (isPair) _enemyCommittedPair = new List<string>(tiles);
    else _enemyCommittedMelds.Add(new List<string>(tiles));

    RefreshEnemyMeldsPanel();
    GreyCommittedDiscards();
}

    // ===== Effects =====
    private void ApplyEffectsForTiles(IEnumerable<string> tileIds)
{
    if (DISABLE_LEGACY_ENEMY_DISCARD_EFFECTS) return;
        foreach (var id in tileIds)
        {
            if (!AddonTryParseSuitNum(id, out int suit, out int num)) continue;
            if (num<1 || num>9) continue;
            if (suit == 0) // Man → damage to player
            {
                int val = 1;
                try {
                    var f = typeof(GameManager).GetField("enemyManDamageByNumber", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if (f != null && f.GetValue(this) is int[] arr && num < arr.Length) val = arr[num];
                } catch {}
if (val > 0) ApplyDamageToPlayer_WithOmamori(val, "enemy_manzu_effect");

            }
            else if (suit == 1) // Pin → heal enemy
            {
                int val = 1;
                try {
                    var f = typeof(GameManager).GetField("enemyPinHealByNumber", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if (f != null && f.GetValue(this) is int[] arr && num < arr.Length) val = arr[num];
                } catch {}
                if (val>0) enemyHP = Mathf.Min(enemyMaxHP, enemyHP + val);
            }
            else if (suit == 2) // Sou → tsumo penalty add
            {
                int val = 0;
                try {
                    var f = typeof(GameManager).GetField("enemySouTsumoPenaltyByNumber", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                    if (f != null && f.GetValue(this) is int[] arr && num < arr.Length) val = arr[num];
                } catch {}
                if (val>0) AddonAddTsumoPenaltySafely(val);
            }
        }
    }

    
// ===== Standard-style scoring =====

private int ComputeEnemyScoreStandard()
{
    var tiles = new List<string>();
    foreach (var m in _enemyCommittedMelds) tiles.AddRange(m);
    if (_enemyCommittedPair != null) tiles.AddRange(_enemyCommittedPair);
    if (tiles.Count > 14) tiles = tiles.Take(14).ToList();

    // 1) 最優先：プレイヤーと同じスコアラーを直接呼び出す
    int same = TryInvokePlayerScorer(tiles);
    if (same > 0) return same;

    // 2) 次点：一般的なスコアラー（従来の探索）
    int ext = TryInvokeExternalScorer(tiles);
    if (ext > 0) return ext;

    // 3) フォールバック
    return FallbackComputePoints(tiles);
}



// ===== Player scorer bridge =====
private int TryInvokePlayerScorer(List<string> tiles)
{
    try
    {
        object tileObjArg = tiles;
        System.Type tileItemType = null;
        var tileTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .Where(t => t.IsClass && t.GetConstructor(System.Type.EmptyTypes) != null)
            .Where(t => t.Name.ToLower().Contains("tile") || t.Name.ToLower().Contains("pai") || t.Name.ToLower().Contains("tiledata"))
            .ToList();

        foreach (var tt in tileTypes)
        {
            var fId = tt.GetField("id", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic) ??
                      tt.GetField("tileId", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if (fId != null && fId.FieldType == typeof(string))
            {
                var list = (System.Collections.IList)System.Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(tt));
                foreach (var s in tiles)
                {
                    var inst = System.Activator.CreateInstance(tt);
                    fId.SetValue(inst, s);
                    list.Add(inst);
                }
                tileObjArg = list;
                tileItemType = tt;
                break;
            }
        }

        bool isDealer = false;
        try {
            var f = typeof(GameManager).GetField("isDealer", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if (f != null) isDealer = System.Convert.ToBoolean(f.GetValue(this));
        } catch {}

        object ronValue = null;
        try {
            var winType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                .FirstOrDefault(t => t.IsEnum && t.Name.ToLower().Contains("win") && (t.Name.ToLower().Contains("type") || t.Name.ToLower().Contains("kind")));
            if (winType != null)
            {
                var names = System.Enum.GetNames(winType);
                string ronName = names.FirstOrDefault(n => n.ToLower().Contains("ron")) ?? names.FirstOrDefault();
                if (ronName != null) ronValue = System.Enum.Parse(winType, ronName);
            }
        } catch {}

        System.Collections.Generic.IEnumerable<System.Reflection.MethodInfo> EnumerateCandidates()
        {
            var flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
            var gmMethods = typeof(GameManager).GetMethods(flags).Where(m =>
            {
                var n = m.Name.ToLowerInvariant();
                return (n.Contains("score") || n.Contains("point") || n.Contains("yaku") || n.Contains("agari") || n.Contains("hanfu") || n.Contains("han") || n.Contains("fu")) &&
                       (n.Contains("player") || n.Contains("hand")) &&
                       (n.Contains("calc") || n.Contains("compute") || n.Contains("count"));
            });

            var anyMethods = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
                .SelectMany(t => {
                    try {
                        return t.GetMethods(flags).Where(m => {
                            var n = m.Name.ToLowerInvariant();
                            return (n.Contains("score") || n.Contains("point") || n.Contains("yaku") || n.Contains("agari") || n.Contains("hanfu") || n.Contains("han") || n.Contains("fu")) &&
                                   (n.Contains("player") || n.Contains("hand")) &&
                                   (n.Contains("calc") || n.Contains("compute") || n.Contains("count"));
                        });
                    } catch { return new System.Reflection.MethodInfo[0]; }
                });

            return gmMethods.Concat(anyMethods);
        }

        foreach (var m in EnumerateCandidates())
        {
            var ps = m.GetParameters();
            object target = m.IsStatic ? null : (m.DeclaringType == typeof(GameManager) ? (object)this : System.Activator.CreateInstance(m.DeclaringType));

            if (ps.Length == 1 &&
                (ps[0].ParameterType == typeof(System.Collections.Generic.List<string>) ||
                 ps[0].ParameterType == typeof(string[]) ||
                 typeof(System.Collections.Generic.IEnumerable<string>).IsAssignableFrom(ps[0].ParameterType)))
            {
                object arg = tiles;
                if (ps[0].ParameterType == typeof(string[])) arg = tiles.ToArray();
                var r = m.Invoke(target, new object[]{arg});
                if (r is int iv && iv > 0) return iv;
            }

            if (ps.Length == 1 && tileItemType != null)
            {
                var ienumT = typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(tileItemType);
                if (ps[0].ParameterType.IsAssignableFrom(tileObjArg.GetType()) ||
                    ps[0].ParameterType.IsAssignableFrom(ienumT))
                {
                    var r = m.Invoke(target, new object[]{tileObjArg});
                    if (r is int iv && iv > 0) return iv;
                }
            }

            
            // 試行: (List<List<string>> melds, List<string> pair, ...)
            if (ps.Length >= 2)
            {
                bool firstIsMelds = ps[0].ParameterType.IsGenericType &&
                                    ps[0].ParameterType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>) &&
                                    ps[0].ParameterType.GetGenericArguments()[0].IsGenericType &&
                                    ps[0].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>);
                bool secondIsListString = ps[1].ParameterType == typeof(System.Collections.Generic.List<string>);
                if (firstIsMelds && secondIsListString)
                {
                    try
                    {
                        var argMelds = _enemyCommittedMelds.Select(m => new System.Collections.Generic.List<string>(m)).ToList();
                        var argPair  = _enemyCommittedPair != null ? new System.Collections.Generic.List<string>(_enemyCommittedPair) : new System.Collections.Generic.List<string>();
                        var args = new object[ps.Length];
                        args[0] = argMelds;
                        args[1] = argPair;
                        for (int i=2;i<ps.Length;i++)
                        {
                            var pt = ps[i].ParameterType;
                            if (pt == typeof(bool)) args[i] = false; // ron
                            else if (pt.IsEnum) args[i] = System.Enum.GetValues(pt).GetValue(0);
                            else if (pt == typeof(int)) args[i] = 0;
                            else if (pt == typeof(string)) args[i] = "ron";
                            else args[i] = System.Type.Missing;
                        }
                        var r = m.Invoke(target, args);
                        if (r is int iv && iv > 0) return iv;
                    } catch {}
                }
            }
if (ps.Length >= 2)
            {
                var args = new object[ps.Length];
                bool ok = true;
                for (int i=0;i<ps.Length;i++)
                {
                    var pt = ps[i].ParameterType;
                    if (i==0)
                    {
                        if (pt == typeof(System.Collections.Generic.List<string>)) args[i] = tiles;
                        else if (pt == typeof(string[])) args[i] = tiles.ToArray();
                        else if (typeof(System.Collections.Generic.IEnumerable<string>).IsAssignableFrom(pt)) args[i] = tiles;
                        else if (tileItemType != null && (pt.IsAssignableFrom(tileObjArg.GetType()) ||
                                (pt.IsGenericType && pt.GetGenericArguments().Length==1 && pt.GetGenericArguments()[0]==tileItemType)))
                        {
                            args[i] = tileObjArg;
                        }
                        else { ok = false; break; }
                    }
                    else
                    {
                        if (pt == typeof(bool)) args[i] = false; // ron 相当
                        else if (pt.IsEnum && ronValue != null && pt == ronValue.GetType()) args[i] = ronValue;
                        else if (pt == typeof(int)) args[i] = 0;
                        else if (pt == typeof(string)) args[i] = "ron";
                        else { args[i] = System.Type.Missing; }
                    }
                }
                if (ok)
                {
                    try
                    {
                        var r = m.Invoke(target, args);
                        if (r is int iv && iv > 0) return iv;
                    }
                    catch {}
                }
            }
        }
    }
    catch {}
    return 0;
}

private int TryInvokeExternalScorer(List<string> tiles)
{
    try
    {
        var asms = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in asms)
        {
            foreach (var t in asm.GetTypes())
            {
                foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance))
                {
                    string n = m.Name.ToLowerInvariant();
                    if (!(n.Contains("score") || n.Contains("point"))) continue;
                    if (!(n.Contains("calc") || n.Contains("compute") || n.Contains("count"))) continue;

                    var ps = m.GetParameters();
                    if (ps.Length == 1 &&
                        (ps[0].ParameterType == typeof(List<string>) ||
                         ps[0].ParameterType == typeof(string[]) ||
                         typeof(System.Collections.Generic.IEnumerable<string>).IsAssignableFrom(ps[0].ParameterType)))
                    {
                        object target = null;
                        if (!m.IsStatic)
                        {
                            try { target = Activator.CreateInstance(t); } catch { continue; }
                        }
                        object arg = tiles;
                        if (ps[0].ParameterType == typeof(string[])) arg = tiles.ToArray();
                        var res = m.Invoke(target, new object[]{arg});
                        if (res is int iv && iv > 0) return iv;
                    }
                    if (ps.Length == 2 &&
                        (ps[0].ParameterType == typeof(List<string>) ||
                         ps[0].ParameterType == typeof(string[]) ||
                         typeof(System.Collections.Generic.IEnumerable<string>).IsAssignableFrom(ps[0].ParameterType)) &&
                        (ps[1].ParameterType == typeof(bool)))
                    {
                        object target = null;
                        if (!m.IsStatic)
                        {
                            try { target = Activator.CreateInstance(t); } catch { continue; }
                        }
                        object arg0 = tiles;
                        if (ps[0].ParameterType == typeof(string[])) arg0 = tiles.ToArray();
                        bool isDealer = false;
                        try {
                            var f = typeof(GameManager).GetField("isDealer", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                            if (f != null) isDealer = System.Convert.ToBoolean(f.GetValue(this));
                        } catch {}
                        var res = m.Invoke(target, new object[]{arg0, isDealer});
                        if (res is int iv2 && iv2 > 0) return iv2;
                    }
                }
            }
        }
    }
    catch {}
    return 0;
}

private bool IsTerminal(string id)
{
    if (!AddonTryParseSuitNum(id, out int s, out int n)) return false;
    return n == 1 || n == 9;
}
private bool IsHonor(string id)
{
    if (string.IsNullOrEmpty(id) || id.Length < 3) return true;
    string p = id.Substring(0,3).ToLowerInvariant();
    return !(p == "man" || p == "pin" || p == "sou");
}

private int FallbackComputePoints(List<string> tiles)
{
    int ponSimples = 0;
    int ponTermHon = 0;
    int chiCount = 0;
    foreach (var m in _enemyCommittedMelds)
    {
        if (m.Count != 3) continue;
        bool isPon = (m[0] == m[1] && m[1] == m[2]);
        if (isPon)
        {
            bool termOrHon = IsTerminal(m[0]) || IsHonor(m[0]);
            if (termOrHon) ponTermHon++;
            else ponSimples++;
        }
        else
        {
            chiCount++;
        }
    }

    bool hasTerminalOrHonor = tiles.Any(id => IsTerminal(id) || IsHonor(id));
    int han = 0;
    if (!hasTerminalOrHonor) han += 1;
    if (chiCount == 4 && _enemyCommittedPair != null && !hasTerminalOrHonor) han += 1;
    han += ponSimples;
    han += ponTermHon * 2;

    int fu = (ponSimples + ponTermHon) > 0 ? 30 : 30;
    fu += ponSimples * 2;
    fu += ponTermHon * 4;

    double basic = fu * Math.Pow(2, 2 + Math.Max(0, han));
    double total = 4 * basic;
    int points = (int)(Math.Ceiling(total / 100.0) * 100);
    points = Mathf.Clamp(points, 1000, 24000);
    return points;
}


    private string AddonIdOf(int suit, int num) => suit==0 ? $"Man{num}" : (suit==1 ? $"Pin{num}" : $"Sou{num}");

    private bool AddonTryParseSuitNum(string id, out int suit, out int num)
    {
        suit = -1; num = -1;
        if (string.IsNullOrEmpty(id) || id.Length < 4) return false;
        string prefix = id.Substring(0,3).ToLowerInvariant();
        if (prefix == "man") suit = 0;
        else if (prefix == "pin") suit = 1;
        else if (prefix == "sou") suit = 2;
        else return false;
        if (!int.TryParse(id.Substring(3), out num)) return false;
        return true;
    }

// ★高速：Count 取得は “絶対にコピーしない”
private int Addon_GetEnemyDiscardCount()
{
    // GameManager 本体の enemyDiscards に直接アクセスできる（partial class なので可）
    if (enemyDiscards != null) return enemyDiscards.Count;

    // 直接参照できない/ null の場合だけ fallback
    var ids = Addon_ReadEnemyDiscardIds_Snapshot();
    return ids != null ? ids.Count : 0;
}

// ★スナップショットが必要なときだけコピーする版（旧 Addon_ReadEnemyDiscardIds の置き換え）
private List<string> Addon_ReadEnemyDiscardIds_Snapshot()
{
    // まずは直アクセス（最速）
    if (enemyDiscards != null) return new List<string>(enemyDiscards);

    // 直アクセスできない環境用：Reflection は “毎回” やらない（キャッシュ）
    if (_cachedEnemyDiscardsField == null && !_cachedEnemyDiscardsFieldSearched)
    {
        _cachedEnemyDiscardsFieldSearched = true;
        var t = GetType();
        _cachedEnemyDiscardsField = t.GetField(
            "enemyDiscards",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance
        );
    }

    try
    {
        if (_cachedEnemyDiscardsField != null)
        {
            var obj = _cachedEnemyDiscardsField.GetValue(this);
            var ls = obj as List<string>;
            if (ls != null) return new List<string>(ls); // snapshot
        }
    }
    catch { }

    // fallback: enemyDiscardArea から読む（最後の手段）
    var ids2 = new List<string>();
    if (enemyDiscardArea != null)
    {
        for (int i = 0; i < enemyDiscardArea.childCount; i++)
        {
            var tf = enemyDiscardArea.GetChild(i);

            string id = null;

            // sprite.name を優先（UIがあるならこれが一番“牌IDっぽい”）
            var img = tf ? tf.GetComponentInChildren<UnityEngine.UI.Image>(true) : null;
            if (img && img.sprite) id = img.sprite.name;

            // 最後に GO 名
            if (string.IsNullOrEmpty(id)) id = tf ? tf.name : "";

            if (!string.IsNullOrEmpty(id)) ids2.Add(id);
        }
    }
    return ids2;
}


// ★追加：Reflection キャッシュ用フィールド（クラス内どこでもOK）
private System.Reflection.FieldInfo _cachedEnemyDiscardsField;
private bool _cachedEnemyDiscardsFieldSearched;


    // ===== Reflection helpers =====
    private int Addon_GetIntField(string fieldName, int defaultValue = 0)
    {
        try {
            var f = typeof(GameManager).GetField(fieldName, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (f != null && (f.FieldType == typeof(int) || f.FieldType.IsEnum))
            {
                var v = f.GetValue(this);
                if (v != null) return Convert.ToInt32(v);
            }
        } catch {}
        return defaultValue;
    }

    private int Addon_GetCurrentEnemyIndex()
    {
        try {
            var t = System.Type.GetType("PlayerData");
            if (t != null)
            {
                var pi = t.GetProperty("CurrentEnemy", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                if (pi != null) return Mathf.Max(0, Convert.ToInt32(pi.GetValue(null)));
                var fi = t.GetField("CurrentEnemy", BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                if (fi != null) return Mathf.Max(0, Convert.ToInt32(fi.GetValue(null)));
            }
        } catch {}
        try {
            var f = typeof(GameManager).GetField("currentEnemyIndex", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if (f != null) return Mathf.Max(0, Convert.ToInt32(f.GetValue(this)));
        } catch {}
        return 0;
    }
private Transform _cachedEnemyDiscardContainer = null;
private bool _cachedEnemyDiscardContainerSearched = false;

private Transform LocateEnemyDiscardContainer()
{
    if (_cachedEnemyDiscardContainer != null) return _cachedEnemyDiscardContainer;
    if (_cachedEnemyDiscardContainerSearched) return null;

    _cachedEnemyDiscardContainerSearched = true;

    // ★最優先：GameManager.cs に実在する enemyDiscardArea を使う
    if (enemyDiscardArea != null)
    {
        _cachedEnemyDiscardContainer = enemyDiscardArea;
        return _cachedEnemyDiscardContainer;
    }

    // 次点：Reflection で候補フィールド名を探す（あっても1回だけ）
    var gmType = typeof(GameManager);
    var names = new string[]
    {
        "enemyDiscardsArea",
        "enemyDiscardArea",
        "enemyDiscardRoot",
        "enemyDiscardsRoot"
    };

    foreach (var nm in names)
    {
        var f = gmType.GetField(nm,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);

        if (f == null) continue;
        var tr = f.GetValue(this) as Transform;
        if (tr != null)
        {
            _cachedEnemyDiscardContainer = tr;
            return _cachedEnemyDiscardContainer;
        }
    }


    // 最後の手段：シーン内から名前で探す（これも1回だけ）
    var all = GameObject.FindObjectsOfType<RectTransform>(true);
    foreach (var rt in all)
    {
        if (rt == null) continue;
        var n = rt.name;
        if (n == "EnemyDiscardRow" || n == "EnemyDiscards" || n == "EnemyDiscardRoot")
        {
            _cachedEnemyDiscardContainer = rt.transform;
            return _cachedEnemyDiscardContainer;
        }
    }

    return null;
}



    private GameObject GetEnemyDiscardVisualByIndex(int index)
    {
        var container = LocateEnemyDiscardContainer();
        if (container != null && index >= 0 && index < container.childCount)
            return container.GetChild(index).gameObject;
        return null;
    }

    private void GreyHighlightEnemyDiscard(int discardIndex)
    {
        var go = GetEnemyDiscardVisualByIndex(discardIndex);
        if (!go) return;
        var imgs = go.GetComponentsInChildren<Image>(true);
        if (imgs != null && imgs.Length > 0)
        {
            foreach (var img in imgs) img.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            return;
        }
        var overlay = new GameObject("GreyOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(go.transform, false);
        var rt = overlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0.6f,0.6f,0.6f,0.4f);
    }
private void GreyCommittedDiscards()
{
    var container = LocateEnemyDiscardContainer();
    if (!container) return;
    for (int i = 0; i < container.childCount; i++)
    {
        var go = container.GetChild(i).gameObject;

        bool committed =
            _committedDiscardInstanceIDs.Contains(go.GetInstanceID()) ||
            _meldCommittedIndices.Contains(i) ||
            (enemyUsedIndices != null && enemyUsedIndices.Contains(i)); // ★追加：和了等でグレー確定した捨て牌も維持対象に

        var imgs = go.GetComponentsInChildren<Image>(true);
        if (imgs != null && imgs.Length > 0)
        {
            foreach (var img in imgs)
                img.color = committed ? new Color(0.6f,0.6f,0.6f,1f) : Color.white;
        }
    }
}


// ===== Win overlay =====
private void EnsureEnemyWinOverlay()
{
    if (_enemyWinOverlay != null) return;

    if (!useManualEnemyWinUI)
    {
        Debug.LogWarning("Enemy win overlay is manual-only. Set useManualEnemyWinUI = true to use the manual overlay. Overlay disabled for now.");
        return;
    }

    // オーバーレイでは「敵画像カットイン＋タイトル（ツモ）」だけ使うので、この2つだけ必須
    if (!enemyWinOverlayManualRoot || !enemyWinTitleManual)
    {
        Debug.LogWarning("Manual enemy win overlay requires enemyWinOverlayManualRoot and enemyWinTitleManual. Overlay disabled until you assign them.");
        return;
    }

    _enemyWinOverlay = enemyWinOverlayManualRoot;
    _enemyWinTitle   = enemyWinTitleManual;

    // 説明文／OKボタン／手牌表示はオーバーレイでは使わない（Inspector に割り当てても無視）
    _enemyWinTiles   = enemyWinTilesManual;
    _enemyWinBody    = enemyWinBodyManual;
    _enemyWinOk      = enemyWinOkManual;

    _enemyWinOverlay.gameObject.SetActive(false);

// デフォルトのタイトルは「ツモ」
_enemyWinTitle.text = EnemyAddon_FixedText_Local("win_tsumo");
    // 初期化成功フラグ
    _enemyWinOverlayInitAttempted = true;
}
private void ShowEnemyWinPanel(int score, int hpDmg, int prevPlayer, int curPlayer, int prevEnemy, int curEnemy)
{
    // ▼ここでも初期化を試す
    if (_enemyWinOverlay == null) EnsureEnemyWinOverlay();
    if (_enemyWinOverlay == null) return;

    _enemyWinOverlay.gameObject.SetActive(true);
// タイトルのみ（ツモ/ロン）
if (_enemyWinTitle) _enemyWinTitle.text = _enemyLastWinWasTsumo
    ? EnemyAddon_FixedText_Local("win_tsumo")
    : EnemyAddon_FixedText_Local("win_ron");
    // オーバーレイでは手牌表示・役評価は使わない（点数計算パネル側で表示する）
    if (_enemyWinTiles)
    {
        for (int j = _enemyWinTiles.childCount - 1; j >= 0; j--)
            UnityEngine.Object.Destroy(_enemyWinTiles.GetChild(j).gameObject);
    }
// 説明文があるなら最低限の情報だけ出す（無くてもOK）
if (_enemyWinBody)
{
    _enemyWinBody.text =
        EnemyAddon_FixedText_Local("enemy_win_body_line1") + "\n" +
        EnemyAddon_FixedText_Local("enemy_win_body_score_prefix") + score + "\n" +
        EnemyAddon_FixedText_Local("enemy_win_body_hp_damage_prefix") + hpDmg + "\n";
}
    // OKボタンが割り当てられている場合は有効化だけ
    if (_enemyWinOk) _enemyWinOk.gameObject.SetActive(true);
}
    
    /// <summary>
    /// Display enemy win result on the same scoring panel used for the player.
    /// Yaku and Fu lines are appended asynchronously by EnemyAddon_UIAutoYakuFu when available.
    /// </summary>
    private void ShowEnemyScoringMinimal(int score, int hpDmg, int prevPlayer, int curPlayer, int prevEnemy, int curEnemy)
    {
_addonLastScoringWasEnemy = true;

// ★hoist: 後段のカットイン呼び出しでも使うため、ここで宣言しておく
int bestHan = 0, bestFu = 0, bestPts = -1;

// Build enemy winning 14 tiles once for evaluation and overlay
var enemyAgari14 = new System.Collections.Generic.List<string>();
if (_enemyCommittedMelds != null) { foreach (var mm in _enemyCommittedMelds) if (mm != null) enemyAgari14.AddRange(mm); }
if (_enemyCommittedPair  != null) enemyAgari14.AddRange(_enemyCommittedPair);
if (enemyAgari14.Count > 14) enemyAgari14 = enemyAgari14.GetRange(0, 14);
try { enemyAgari14.Sort((a,b)=>ToSortKey(a).CompareTo(ToSortKey(b))); } catch {}

// Evaluate best yaku/points across all win tile candidates
var __bestYaku = new System.Collections.Generic.List<string>();
var __bestWinCandidates = new System.Collections.Generic.HashSet<string>();
var __bestYakuKeys = new System.Collections.Generic.List<string>();
var __bestYakumanKeys = new System.Collections.Generic.List<string>();
try
{
    // open 構築（"*" 除去）
    var open = new System.Collections.Generic.List<System.Collections.Generic.IList<string>>();

    // ★敵の「門前/副露」は open.Count では判定しない。
    //   _enemyCommittedMelds は“確定面子（手組）”も入るため、open.Count は0にならないケースがある。
    //   明鳴き印（"*"）が1つでもあるときだけ「副露あり」とみなす。
    bool enemyHasCalledOpen = false;

    foreach (var m in _enemyCommittedMelds)
    {
        if (m == null) continue;

        var clean = new System.Collections.Generic.List<string>();
        foreach (var s in m)
        {
            if (string.IsNullOrEmpty(s)) continue;

            if (s.EndsWith("*")) enemyHasCalledOpen = true;
            clean.Add(s.EndsWith("*") ? s.Substring(0, s.Length - 1) : s);
        }

        if (clean.Count >= 3) open.Add(clean);
    }

    bool isClosedForEnemy = !enemyHasCalledOpen;

// 表ドラ枚数（固定）— concealed14Only を作って重複カウントを防止
int dora = 0;
try
{
    // 1) まず「14枚」（enemyAgari14 / full14 など）から開始
    var concealed14Only = new System.Collections.Generic.List<string>(/* 元の14枚配列 */ enemyAgari14 /* or full14 */);

    // 2) open に含まれる牌は concealed から重複分だけ取り除く（＝面前14枚だけ残す）
    foreach (var om in open)
        foreach (var tid in om)
        {
            int idx = concealed14Only.IndexOf(tid);
            if (idx >= 0) concealed14Only.RemoveAt(idx);
        }

    // 3) 任意でソート（CountDoraHits はソート不要だが既存踏襲）
    try { concealed14Only.Sort((a,b)=>ToSortKey(a).CompareTo(ToSortKey(b))); } catch {}

    // 4) 面前14枚＋open でカウント（＝二重にならない）
    dora = CountDoraHits(concealed14Only, open, doraIndicators);
}
catch {}

    // 候補の和了牌ごとに評価
    foreach (var win in new System.Collections.Generic.HashSet<string>(enemyAgari14))
    {
        var concealed13 = new System.Collections.Generic.List<string>(enemyAgari14);

        concealed13.Remove(win);
        // open に含まれる牌を引く
        foreach (var om in open)
            foreach (var tid in om)
            {
                int idx = concealed13.IndexOf(tid);
                if (idx >= 0) concealed13.RemoveAt(idx);
            }
var eval = YakuEvaluator.EvaluateDetailed(
    concealed13, open, win,                      // ★open を渡す
    isTsumo: _enemyLastWinWasTsumo,               // ★実際の和了種別
    isClosed: isClosedForEnemy,                   // ★実際の門前/副露
    seatWind: GetWindSafe("seatWind"),
    roundWind: GetWindSafe("roundWind"));

// 敵の「リーチ」「ツモ」は状況に応じて付与（評価に含まれる分は二重加算しない）
int forced = 0;
string head = eval.breakdown ?? string.Empty;

// 役キーで判定し、まだ未対応のものだけ表示文字列フォールバックを併用
bool hadRiichi = EnemyAddon_HasYakuKey(eval.yakuKeys, "RIICHI") || EnemyAddon_TextHasYakuLike(head, "RIICHI");
bool hadMenzenTsumo = EnemyAddon_HasYakuKey(eval.yakuKeys, "MENZEN_TSUMO") || EnemyAddon_TextHasYakuLike(head, "TSUMO");
// リーチは「敵がリーチしていた」場合のみ
if (_enemyLastWinWasRiichi && !hadRiichi) forced += 1;

// 門前ツモは「ツモ」かつ「門前」の場合のみ
if (_enemyLastWinWasTsumo && isClosedForEnemy && !hadMenzenTsumo) forced += 1;

int hanWithDora = System.Math.Max(0, eval.han) + forced + dora;


int fu  = System.Math.Max(0, eval.fu);
int pts = EnemyAddon_EstimatePointsFromHanFu(hanWithDora, fu, false, true);

        bool better = pts > bestPts || (pts == bestPts && (hanWithDora > bestHan || (hanWithDora == bestHan && fu > bestFu)));
        if (better)
        {
            bestPts = pts; bestHan = hanWithDora; bestFu = fu;
            __bestWinCandidates.Clear();
            __bestWinCandidates.Add(win);

            __bestYakuKeys.Clear();
            if (eval.yakuKeys != null) __bestYakuKeys.AddRange(eval.yakuKeys);

            __bestYakumanKeys.Clear();
            if (eval.yakumanKeys != null) __bestYakumanKeys.AddRange(eval.yakumanKeys);

            __bestYaku.Clear();
            __bestYaku.AddRange(EnemyAddon_ExtractDisplayedYakuNames(eval.breakdown, dora));

            // ★用語統一＆不足補完：状況に応じて「リーチ」「ツモ」を表示。
            //   「門前清自摸和」はゲーム内表記「ツモ」に正規化。
            bool isYakuman = EnemyAddon_HasAnyYakumanKey(eval.yakumanKeys) || (eval.han >= 13) || EnemyAddon_TextHasYakuLike((eval.breakdown ?? string.Empty), "YAKUMAN");
            if (!isYakuman)
            {
                for (int i = 0; i < __bestYaku.Count; i++)
                {
                    __bestYaku[i] = EnemyAddon_NormalizeDisplayedYakuName(__bestYaku[i]);
                }
if (_enemyLastWinWasRiichi && !EnemyAddon_ListHasYakuLike(__bestYaku, "RIICHI"))
    __bestYaku.Add(EnemyAddon_FixedText_Local("yaku_riichi_short"));
if (_enemyLastWinWasTsumo && !EnemyAddon_ListHasYakuLike(__bestYaku, "TSUMO"))
    __bestYaku.Add(EnemyAddon_FixedText_Local("win_tsumo"));
            }
        }
        else if (pts == bestPts)
        {
            __bestWinCandidates.Add(win);
        }
    }
}
catch {}
string tsumoTile = null;
try
{
bool requiresSpecificWait =
    EnemyAddon_HasYakuKey(__bestYakuKeys, "PINFU") ||
    __bestYaku.Any(y => EnemyAddon_TextHasYakuLike(y, "PINFU"));

    if (requiresSpecificWait && __bestWinCandidates != null && __bestWinCandidates.Count > 0)
    {
        int bestIdx = -1;
        foreach (var c in __bestWinCandidates)
        {
            int idx = enemyDiscards != null ? enemyDiscards.LastIndexOf(c) : -1;
            if (idx > bestIdx) { bestIdx = idx; tsumoTile = c; }
        }
    }
    if (tsumoTile == null)
    {
        int bestIdx = -1; string pick = null;
        foreach (var s14 in enemyAgari14)
        {
            int idx = enemyDiscards != null ? enemyDiscards.LastIndexOf(s14) : -1;
            if (idx > bestIdx) { bestIdx = idx; pick = s14; }
        }
        tsumoTile = pick ?? (enemyAgari14.Count>0 ? enemyAgari14[enemyAgari14.Count-1] : null);
    }
} catch { tsumoTile = (enemyAgari14.Count>0 ? enemyAgari14[enemyAgari14.Count-1] : null); }

// Overlays: 13 tiles on handArea (sorted), plus 14th to the right with 1/4 gap
// ★修正：プレイヤーの handArea を使わず、敵専用 _enemyWinTiles に 14 枚を独立描画
try
{
    // 1) 古い表示を掃除（保険）
    if (_enemyWinTiles)
    {
        for (int i = _enemyWinTiles.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(_enemyWinTiles.GetChild(i).gameObject);
    }

    if (_enemyWinTiles)
    {
        // 並び順を整えた 13 + ツモ 1
        var list13 = new System.Collections.Generic.List<string>(enemyAgari14);
        if (!string.IsNullOrEmpty(tsumoTile)) list13.Remove(tsumoTile);
        try { list13.Sort((a,b)=>ToSortKey(a).CompareTo(ToSortKey(b))); } catch {}

        // タイルサイズを推定（tilePrefab 基準）
        float tileW = 64f, tileH = 72f;
        try
        {
            var r = tilePrefab ? (tilePrefab.transform as UnityEngine.RectTransform) : null;
            if (r) { tileW = r.rect.width; tileH = r.rect.height; }
        } catch {}

        // 2) 13枚を左から順に
        for (int i = 0; i < list13.Count && i < 13; i++)
        {
            var ghost = UnityEngine.Object.Instantiate(tilePrefab, _enemyWinTiles);
            ghost.name = "EnemyOverlay";
            SetTileSprite(ghost, list13[i]);
            if (ghost.TryGetComponent<UnityEngine.UI.Button>(out var b)) b.interactable = false;
            foreach (var img in ghost.GetComponentsInChildren<UnityEngine.UI.Image>(true)) img.raycastTarget = false;

            var rt = ghost.transform as UnityEngine.RectTransform;
            rt.anchorMin = new UnityEngine.Vector2(0, 0.5f);
            rt.anchorMax = new UnityEngine.Vector2(0, 0.5f);
            rt.pivot     = new UnityEngine.Vector2(0, 0.5f);
            rt.sizeDelta = new UnityEngine.Vector2(tileW, tileH);
            rt.anchoredPosition = new UnityEngine.Vector2(i * (tileW * 0.8f), 0f);
        }

        // 3) ツモ牌を 13 枚の右に少し隙間をあけて配置
        if (!string.IsNullOrEmpty(tsumoTile))
        {
            float gap = tileW * 0.25f;
            var extra = UnityEngine.Object.Instantiate(tilePrefab, _enemyWinTiles);
            extra.name = "EnemyOverlayExtra";
            SetTileSprite(extra, tsumoTile);
            if (extra.TryGetComponent<UnityEngine.UI.Button>(out var b3)) b3.interactable = false;
            foreach (var img in extra.GetComponentsInChildren<UnityEngine.UI.Image>(true)) img.raycastTarget = false;

            var rt3 = extra.transform as UnityEngine.RectTransform;
            rt3.anchorMin = new UnityEngine.Vector2(0, 0.5f);
            rt3.anchorMax = new UnityEngine.Vector2(0, 0.5f);
            rt3.pivot     = new UnityEngine.Vector2(0, 0.5f);
            rt3.sizeDelta = new UnityEngine.Vector2(tileW, tileH);
            rt3.anchoredPosition = new UnityEngine.Vector2(13 * (tileW * 0.8f) + gap, 0f);
        }
    }
}
catch {}
StartCoroutine(__WinCutInThenShowScoring(
    EnemyAddon_FixedText_Local("win_tsumo"), // label（中央の大文字）
    false,                                   // isPlayer=false → 敵ポートレートを表示
    hpDmg,                                   // finalDamage（プレイヤーへ与えた実ダメージ）
    score,                                   // finalBasePoints（敵側は撃等が無いので表示上は同値でOK）
    1f, 0f, 0f,                              // geKi, shun, iyu（敵側では適用なし）
    0, 0,                                    // finalMpHeal, finalHpHeal（敵側では表示0）
    __bestYaku,                              // 役行（表示時にUIAutoYakuFuからも追記される）
    null,                                    // used tiles（未使用）
    bestFu,                                  // 符
    bestHan,                                 // 翻
    "ツモ",                                  // baseWinKind（内部表示ラベル）
    tsumoTile ?? string.Empty                // 使用牌ラベル（決定できなければ空文字）
));
}
    // ===== Left panel =====
    private void EnsureEnemyMeldsPanel()
    {
            if (useManualEnemyMeldsUI && enemyMeldsManualRoot)
    {
        _enemyMeldsPanel = enemyMeldsManualRoot;   // 以後はこのTransform配下を使う
        return;
    }
        if (_enemyMeldsPanel != null) return;
        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (!canvas) return;

        var root = new GameObject("EnemyMeldsPanel", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        _enemyMeldsPanel = root.GetComponent<RectTransform>();
        root.GetComponent<Image>().color = new Color(0.1f, 0.3f, 0.4f, 0.9f);

        _enemyMeldsPanel.anchorMin = new Vector2(0f, 0.35f);
        _enemyMeldsPanel.anchorMax = new Vector2(0.2f, 0.95f);
        _enemyMeldsPanel.offsetMin = Vector2.zero;
        _enemyMeldsPanel.offsetMax = Vector2.zero;

var titleGO = new GameObject("敵の手牌", typeof(RectTransform));
titleGO.transform.SetParent(root.transform, false);
var title = titleGO.AddComponent<TextMeshProUGUI>();
title.text = EnemyAddon_FixedText_Local("enemy_hand_title");
title.alignment = TextAlignmentOptions.Center;
title.fontSize = 26;
var titleRT = title.GetComponent<RectTransform>();
titleRT.anchorMin = new Vector2(0f, 0.85f);
titleRT.anchorMax = new Vector2(1f, 1f);
titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(root.transform, false);
        _enemyMeldsContent = contentGO.GetComponent<RectTransform>();
        _enemyMeldsContent.anchorMin = new Vector2(0f, 0f);
        _enemyMeldsContent.anchorMax = new Vector2(1f, 0.85f);
        _enemyMeldsContent.offsetMin = new Vector2(8, 8);
        _enemyMeldsContent.offsetMax = new Vector2(-8, -8);
    }

private void RefreshEnemyMeldsPanel()
{
    // 手動UIが有効で、手動UIの親が割り当て済みなら、手動描画パスを優先する
    if (useManualEnemyMeldsUI && enemyMeldsManualRoot)
    {
        // いま確定しているメンツ/雀頭を取り出す
        // _enemyCommittedMelds : List<List<string>>（最大4組を想定）
        // _enemyCommittedPair  : List<string>(2) または null
        List<List<string>> groups = new List<List<string>>();

        if (_enemyCommittedMelds != null && _enemyCommittedMelds.Count > 0)
        {
            for (int i = 0; i < _enemyCommittedMelds.Count; i++)
            {
                var m = _enemyCommittedMelds[i];
                groups.Add(m != null ? new List<string>(m) : null);
            }
        }
        // 4組に満たない場合は null で埋める（Group1..4 を安定して扱うため）
        while (groups.Count < 4) groups.Add(null);

        List<string> pair = null;
        if (_enemyCommittedPair != null && _enemyCommittedPair.Count == 2)
            pair = new List<string>(_enemyCommittedPair);

        // Group1..4（各3スロット）
        for (int gi = 0; gi < 4; gi++)
        {
            var groupTf = enemyMeldsManualRoot.Find("Group" + (gi + 1)) as RectTransform;
            if (groupTf) ApplyTilesToSlots(groupTf, groups[gi], expectedSlots: 3);
        }

        // Pair（2スロット）
        {
            var pairTf = enemyMeldsManualRoot.Find("Pair") as RectTransform;
            if (pairTf) ApplyTilesToSlots(pairTf, pair, expectedSlots: 2);
        }

        return; // 手動描画が終わったらここで終了（自動側へは行かない）
    }

    // ここに来るのは手動UI未設定/無効時。従来の自動描画パスへ委譲する。
    if (_enemyMeldsContent == null)
    {
        EnsureEnemyMeldsPanel();        // 必要なら自動パネルを作る
        if (_enemyMeldsContent == null) return; // それでも無ければ何もできない
    }

    RefreshEnemyMeldsPanel_Auto_Body(); // 既存の自動描画ロジック（下の③）
}
// 手動コンテナ（Group* or Pair）の直下にある Slot* に画像を流し込む。
// tilesOrNull に要素が無いスロットは SetActive(false) にする。
// expectedSlots は Group=3 / Pair=2 を想定。
private void ApplyTilesToSlots(RectTransform parent, List<string> tilesOrNull, int expectedSlots)
{
    for (int i = 0; i < expectedSlots; i++)
    {
        var slot = parent.Find("Slot" + (i + 1)) as RectTransform;
        if (!slot) continue; // 見つからないスロットはスキップ

        bool has = (tilesOrNull != null && i < tilesOrNull.Count && !string.IsNullOrEmpty(tilesOrNull[i]));
        slot.gameObject.SetActive(has);
        if (!has) continue;

        // 画像コンポーネントを探す（見つからなければ付ける）
        var img = GetVisibleArtImage(slot) ?? slot.GetComponentInChildren<Image>(true);
        if (!img) img = slot.gameObject.AddComponent<Image>();

        // 既存の見た目に合わせるため、既存ヘルパーでスプライトを取得
        var sp = GetTileSprite(tilesOrNull[i]);
        img.sprite = sp;
        img.enabled = (sp != null);
        img.preserveAspect = true;
        img.raycastTarget = false; // 手動UIは操作不可の前提
    }
}
private void RefreshEnemyMeldsPanel_Auto_Body()
{
    if (_enemyMeldsContent == null) return;

    // 1) 既存の子（行やタイル）をすべてクリア
    for (int i = _enemyMeldsContent.childCount - 1; i >= 0; i--)
    {
        var child = _enemyMeldsContent.GetChild(i);
        if (child) UnityEngine.Object.Destroy(child.gameObject);
    }

    // 2) 表示対象の牌IDを収集：先にメンツ（最大4組）、その後に雀頭（あれば2枚）
    var sequence = new List<string>();

    if (_enemyCommittedMelds != null && _enemyCommittedMelds.Count > 0)
    {
        int meldTake = Mathf.Min(4, _enemyCommittedMelds.Count);
        for (int m = 0; m < meldTake; m++)
        {
            var meld = _enemyCommittedMelds[m];
            if (meld == null) continue;
            for (int k = 0; k < meld.Count; k++)
            {
                var id = meld[k];
                if (!string.IsNullOrEmpty(id)) sequence.Add(id);
            }
            // メンツ間の視覚的な区切りとして、薄いスペーサーを1つ入れる（後で LayoutElement で幅だけ持たせる）
            sequence.Add(null); // null は「スペーサー」の印
        }
        // 最後に追加したスペーサーを削除（末尾に不要な空白が出ないように）
        if (sequence.Count > 0 && sequence[sequence.Count - 1] == null)
            sequence.RemoveAt(sequence.Count - 1);
    }

    if (_enemyCommittedPair != null && _enemyCommittedPair.Count == 2)
    {
        // 既に何かある場合はメンツと雀頭の間にもスペーサーを1つ
        if (sequence.Count > 0) sequence.Add(null);
        if (!string.IsNullOrEmpty(_enemyCommittedPair[0])) sequence.Add(_enemyCommittedPair[0]);
        if (!string.IsNullOrEmpty(_enemyCommittedPair[1])) sequence.Add(_enemyCommittedPair[1]);
    }

    if (sequence.Count == 0)
    {
        // 何も確定していないなら描画不要
        return;
    }

    // 3) 1行（Row）を作成し、横並びで左寄せに並べる
    var rowGO = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
    var rowRT = rowGO.GetComponent<RectTransform>();
    var rowImg = rowGO.GetComponent<Image>();
    var hlg    = rowGO.GetComponent<HorizontalLayoutGroup>();

    rowGO.transform.SetParent(_enemyMeldsContent, false);

    // 背景は透明（念のため Image を持たせるが色は透明）
    rowImg.color = new Color(0, 0, 0, 0);

    // レイアウト設定（左上起点・子は幅だけ制御）
    rowRT.anchorMin = new Vector2(0f, 1f);
    rowRT.anchorMax = new Vector2(0f, 1f);
    rowRT.pivot     = new Vector2(0f, 1f);
    rowRT.anchoredPosition = new Vector2(0f, 0f);
    rowRT.sizeDelta = new Vector2(0f, 74f);

    hlg.childAlignment         = TextAnchor.UpperLeft;
    hlg.childControlWidth      = true;
    hlg.childControlHeight     = false;
    hlg.childForceExpandWidth  = false;
    hlg.childForceExpandHeight = false;
    hlg.spacing = 0f;
    hlg.padding = new RectOffset(6, 6, 6, 6);

    // タイルの既定サイズ
    float tileW = 54f;
    try
    {
        // フィールド enemyMeldTileWidth が定義されていればそれを優先
        var w = enemyMeldTileWidth; // float フィールド想定
        if (w > 0f) tileW = w;
    }
    catch { /* 無ければ既定値 54f を使う */ }

    float tileH = 64f; // 過去の自動生成と同等の目安サイズ

    // 4) タイルを順に生成（null はスペーサー）
    for (int i = 0; i < sequence.Count; i++)
    {
        string id = sequence[i];

        if (id == null)
        {
            // スペーサーを作る（LayoutElement で幅だけ確保、見た目は透明）
            var spGO = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            var spRT = spGO.GetComponent<RectTransform>();
            var le   = spGO.GetComponent<LayoutElement>();

            spGO.transform.SetParent(rowRT, false);

            spRT.anchorMin = new Vector2(0f, 1f);
            spRT.anchorMax = new Vector2(0f, 1f);
            spRT.pivot     = new Vector2(0f, 1f);
            spRT.sizeDelta = new Vector2(8f, tileH); // 8px くらいの区切り

            le.preferredWidth  = 8f;
            le.preferredHeight = tileH;
            continue;
        }

        // タイルを作る
        var tileGO = new GameObject("Tile_" + id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var tileRT = tileGO.GetComponent<RectTransform>();
        var tileImg= tileGO.GetComponent<Image>();
        var tileLE = tileGO.GetComponent<LayoutElement>();

        tileGO.transform.SetParent(rowRT, false);

        tileRT.anchorMin = new Vector2(0f, 1f);
        tileRT.anchorMax = new Vector2(0f, 1f);
        tileRT.pivot     = new Vector2(0.5f, 0.5f);
        tileRT.sizeDelta = new Vector2(tileW, tileH);

        tileLE.preferredWidth  = tileW;
        tileLE.preferredHeight = tileH;

        tileImg.preserveAspect = true;
        tileImg.raycastTarget  = false;

        // 既存のヘルパーでスプライト取得（無ければ薄く表示）
        var sp = GetTileSprite(id);
        if (sp)
        {
            tileImg.sprite = sp;
            tileImg.color  = Color.white;
            tileImg.enabled = true;
        }
        else
        {
            tileImg.sprite  = null;
            tileImg.color   = new Color(1f, 1f, 1f, 0.2f);
            tileImg.enabled = true;
        }
    }
}



// ===== Bootstrapper =====

}
internal class EnemyMeldAddonBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var go = new GameObject("EnemyMeldAddonRunner", typeof(EnemyMeldAddonBootstrap));
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.GetComponent<EnemyMeldAddonBootstrap>().StartCoroutine(EnableWhenReady());
        SceneManager.activeSceneChanged += (_, __) =>
        {
            var runner = UnityEngine.Object.FindObjectOfType<EnemyMeldAddonBootstrap>();
            if (runner && runner.isActiveAndEnabled)
                runner.StartCoroutine(EnableWhenReady());
        };
    }

    private static System.Collections.IEnumerator EnableWhenReady()
    {
        GameManager gm = null;
        int guard = 0;
        while (gm == null && guard < 600)
        {
            gm = UnityEngine.Object.FindObjectOfType<GameManager>(true);
            yield return null;
            guard++;
        }
        if (gm != null) gm.EnableEnemyMeldModeAddon();
        var runner = UnityEngine.Object.FindObjectOfType<EnemyMeldAddonBootstrap>();
        if (runner) UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
    }
}

public partial class GameManager
{
    // Estimate points from Han/Fu using the same scoring engine as the player (no UI side-effects).
    private int EnemyAddon_EstimatePointsFromHanFu(int han, int fu, bool dealer, bool isTsumo)
    {
        try
        {
            var sr = Scoring.TryScoreWin(fu, han, isTsumo: isTsumo, isDealer: dealer);
            return sr.totalPoints;
        }
        catch { }
        // fallback: very rough estimate
        try
        {
            // basic mangan-or-above cap
            if (han >= 13) return 8000; // yakuman base (child)
            if (han >= 11) return 6000;
            if (han >= 8)  return 4000;
            if (han >= 6)  return 3000;
            int basePoints = fu * (1 << (han + 2));
            // child tsumo three payments -> approximate total
            return Mathf.Max(100, basePoints);
        }
        catch { }
        return 0;
    }
private bool Addon_WasEnemyScoringAndReset()
{
    bool was = _addonLastScoringWasEnemy;
    _addonLastScoringWasEnemy = false;

    // ★敵スコアOKが押されたタイミングでは「敵手牌UI」は消さない。
    //   （灰色化は GameManager.cs 側の OnClickScoreOK() で行う）
    if (was && _enemyClearVisualPendingAfterScoringOk)
    {
        _enemyClearVisualPendingAfterScoringOk = false;

        // スコアパネル用のスナップショットは不要になるのでクリア（手牌UI表示とは別系統）
        try { EnemyAddon_ClearEnemyWinSnapshot(); } catch {}
    }

    // ★追加：敵の点数計算パネルOKで、敵の和了オーバーレイ（敵の和了カットイン相当）も確実に消す
    if (was)
    {
        try
        {
            if (_enemyWinOverlay != null)
            {
                _enemyWinOverlay.gameObject.SetActive(false);
            }

            // 念のため：Inspector参照している手動Rootも直接OFF（_enemyWinOverlayと同一でもOK）
            if (enemyWinOverlayManualRoot != null)
            {
                enemyWinOverlayManualRoot.gameObject.SetActive(false);
            }
        }
        catch { }
    }

    return was;
}
// addon: ensure enemy overlays are removed (13 overlays and the extra 14th tile)
private void Addon_ClearEnemyScoringOverlays()
{
    try
    {
        if (handArea)
        {
            for (int i = 0; i < handArea.childCount; i++)
            {
                var baseTile = handArea.GetChild(i) as UnityEngine.RectTransform;
                if (!baseTile) continue;
                var overlay = baseTile.Find("EnemyOverlay");
                if (overlay) UnityEngine.Object.Destroy(overlay.gameObject);
                SetBaseArtVisible(baseTile, true);
            }
            var extra = handArea.parent ? handArea.parent.Find("EnemyOverlayExtra") : null;
            if (extra) UnityEngine.Object.Destroy(extra.gameObject);
        }
        if (scoringHandParent)
        {
            for (int i = 0; i < scoringHandParent.childCount; i++)
            {
                var baseTile = scoringHandParent.GetChild(i);
                for (int j = baseTile.childCount - 1; j >= 0; j--)
                {
                    var ch = baseTile.GetChild(j);
                    if (ch && ch.name == "EnemyOverlay")
                        UnityEngine.Object.Destroy(ch.gameObject);
                }
                SetBaseArtVisible(baseTile, true);
            }
        }
    } catch {}
    try
    {
        if (_enemyWinTiles)
        {
            for (int i = _enemyWinTiles.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_enemyWinTiles.GetChild(i).gameObject);
        }
    } catch {}
}
private void __SetScoringPanelActive(bool isPlayer)
{
    if (scoringPanelPlayer) scoringPanelPlayer.SetActive(isPlayer);
    if (scoringPanelEnemy)  scoringPanelEnemy.SetActive(!isPlayer);
}
// ★追記：敵和了時のスコア表示用スナップショット（クリア前に保存）
// ★敵和了時のスコア表示用スナップショット（クリア前に保存）
//   旧：_enemyCommittedMelds/_enemyCommittedPair
//   新：_enemyHand(13) + _enemyLastWinTileId(14枚目)
private List<string> _enemyWinSnapshotHand13 = null;
private string _enemyWinSnapshotWinTile = null;
private List<List<string>> _enemyWinSnapshotMelds = null;
private List<string> _enemyWinSnapshotPair = null;
// /mnt/data/GameManager_EnemyMeldMode_Addon.cs（差し替え：全文）
private void EnemyAddon_CaptureEnemyWinSnapshot()
{
    try
    {
        _enemyWinSnapshotMelds = new List<List<string>>();
        if (_enemyCommittedMelds != null)
        {
            foreach (var m in _enemyCommittedMelds)
            {
                if (m == null) continue;
                _enemyWinSnapshotMelds.Add(new List<string>(m));
            }
        }

        _enemyWinSnapshotPair = null;
        if (_enemyCommittedPair != null)
            _enemyWinSnapshotPair = new List<string>(_enemyCommittedPair);

        // ★新仕様：実手牌13枚＋和了牌を保存（表示ズレ根絶）
        _enemyWinSnapshotHand13 = null;
        if (_enemyHand != null)
        {
            _enemyWinSnapshotHand13 = new List<string>(13);
            for (int i = 0; i < _enemyHand.Count && _enemyWinSnapshotHand13.Count < 13; i++)
            {
                var id = _enemyHand[i];
                if (!string.IsNullOrEmpty(id)) _enemyWinSnapshotHand13.Add(id);
            }
        }
        _enemyWinSnapshotWinTile = _enemyLastWinTileId;
    }
    catch
    {
        _enemyWinSnapshotMelds = null;
        _enemyWinSnapshotPair = null;

        _enemyWinSnapshotHand13 = null;
        _enemyWinSnapshotWinTile = null;
    }
}

private void EnemyAddon_ClearEnemyWinSnapshot()
{
    _enemyWinSnapshotHand13 = null;
    _enemyWinSnapshotWinTile = null;
}
private void EnemyAddon_PopulateEnemyScoringTilesManual()
{
    if (!scoringEnemyTilesManual) return;

    // いったん掃除
    for (int i = scoringEnemyTilesManual.childCount - 1; i >= 0; i--)
        UnityEngine.Object.Destroy(scoringEnemyTilesManual.GetChild(i).gameObject);

    // ★新仕様：13枚手牌（スナップショット優先）＋和了牌（別枠）で表示する
    var tiles13 = new System.Collections.Generic.List<string>(13);

    try
    {
        var src = (_enemyWinSnapshotHand13 != null) ? _enemyWinSnapshotHand13 : _enemyHand;
        if (src != null)
        {
            for (int i = 0; i < src.Count && tiles13.Count < 13; i++)
            {
                var s = src[i];
                if (!string.IsNullOrEmpty(s)) tiles13.Add(s);
            }
        }
    }
    catch { }

    if (tiles13.Count == 0) return;

    string winToShow = (_enemyWinSnapshotWinTile != null) ? _enemyWinSnapshotWinTile : _enemyLastWinTileId;

    // 13枚はリーパイ順（*_sp や * は StripTileIdForLogic 経由で sort key を作る）
    try
    {
        tiles13.Sort((a, b) => ToSortKey(StripTileIdForLogic(a)).CompareTo(ToSortKey(StripTileIdForLogic(b))));
    }
    catch { }
int tileW = GetScoringTileWidthSafe(scoringEnemyTilesManual as RectTransform);
// LayoutGroup がある場合：スペーサーを入れる（anchoredPosition が無効化されるため）
var hlg = scoringEnemyTilesManual.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
if (hlg != null && hlg.enabled)
{
    // ★重要：EnemyWinGap の幅が Inspector 値どおり効くように固定
    hlg.spacing = 0f;
    hlg.childControlWidth = false;
    hlg.childControlHeight = false;
    hlg.childForceExpandWidth = false;
    hlg.childForceExpandHeight = false;
}

bool usesLayout = (hlg != null && hlg.enabled);
float gapPx = tileW * Mathf.Max(0f, scoringWinTileGapInTiles);
float px = 0f;
if (usesLayout)
{
    foreach (var id in tiles13)
        CreateTileImage(scoringEnemyTilesManual, id, ref px, tileW);

if (!string.IsNullOrEmpty(winToShow))
{
    var spacer = new UnityEngine.GameObject(
        "EnemyWinGap",
        typeof(UnityEngine.RectTransform),
        typeof(UnityEngine.UI.LayoutElement)
    );
    spacer.transform.SetParent(scoringEnemyTilesManual, false);

    var spacerRt = spacer.GetComponent<UnityEngine.RectTransform>();
    if (spacerRt != null)
    {
        spacerRt.anchorMin = new UnityEngine.Vector2(0f, 0.5f);
        spacerRt.anchorMax = new UnityEngine.Vector2(0f, 0.5f);
        spacerRt.pivot = new UnityEngine.Vector2(0f, 0.5f);
        spacerRt.sizeDelta = new UnityEngine.Vector2(gapPx, 1f);
        spacerRt.anchoredPosition = UnityEngine.Vector2.zero;
    }

    var le = spacer.GetComponent<UnityEngine.UI.LayoutElement>();
    le.minWidth = gapPx;
    le.preferredWidth = gapPx;
    le.flexibleWidth = 0f;
    le.minHeight = 1f;
    le.preferredHeight = 1f;
    le.flexibleHeight = 0f;

    CreateTileImage(scoringEnemyTilesManual, winToShow, ref px, tileW);
}
}
else
{
    foreach (var id in tiles13)
        CreateTileImage(scoringEnemyTilesManual, id, ref px, tileW);

    if (!string.IsNullOrEmpty(winToShow))
    {
        px += gapPx;
        CreateTileImage(scoringEnemyTilesManual, winToShow, ref px, tileW);
    }
}
}
// /mnt/data/GameManager_EnemyMeldMode_Addon.cs（差し替え：全文）
private void EnemyAddon_RenderEnemyAgariTilesOnScoringPanel()
{
    // 表示ロジックを 1 箇所に統一（旧 meld/pair 参照を排除）
    EnemyAddon_PopulateEnemyScoringTilesManual();
}

// ★敵が和了した後に局を継続するため、敵の手牌構築ステートを空にする。
//   clearVisualNow=false の場合は「見た目（敵手牌UI）」はスコアOKまで残す。
private void EnemyAddon_ClearEnemyHandForContinueAfterWin(bool clearVisualNow = true)
{
    // 以降、この局では敵の手牌進行/和了判定を停止するため、構築ステートを空にする
    _enemyHasBaseHand = false;
    _enemyCommittedMelds.Clear();
    _enemyCommittedPair = null;

    _enemyTaatsu = null;
    _enemyTaatsuIdx0 = -1;
    _enemyTaatsuIdx1 = -1;
    _enemyIsInTenpai = false;
    _enemyIsInRiichi = false;

    // 見た目側（敵テンパイ手牌UI／副露UI）は「スコアOKで閉じたタイミングで消す」要件があるため、
    // clearVisualNow=false の場合はここでは触らない。
    if (clearVisualNow)
    {
        if (enemyTenpaiHandManualRoot)
        {
            for (int i = enemyTenpaiHandManualRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(enemyTenpaiHandManualRoot.GetChild(i).gameObject);
            enemyTenpaiHandManualRoot.gameObject.SetActive(false);
        }

        if (enemyMeldsManualRoot)
            enemyMeldsManualRoot.gameObject.SetActive(false);

        try { RefreshEnemyMeldsPanel(); } catch {}
    }
}
private void FinalizeEnemyWin_ShowScoringAndCleanup(int score, int hpDmg, int prevPl, int prevEn)
{
    // ===== Spec change: hand continues until BOTH have won or Ryukyoku =====
    // 敵が先に和了した場合：
    //  - 敵の「手牌進行/和了判定」は停止
    //  - ただし見た目（敵手牌UI）は「スコアOKで閉じたタイミング」で灰色化する（消さない）
    _enemyHasWonThisHand = true;

    // ★追加：このフローでは __WinCutInThenShowScoring() を通らないため、
    //         敵手牌表示用 snapshot（_enemyWonHandSnapshot）が未作成になり得る。
    //         次ターンで _enemyHand.Clear() されても表示が維持できるよう、ここで必ず確保する。
    if (_enemyWonHandSnapshot == null && _enemyHand != null)
        _enemyWonHandSnapshot = new System.Collections.Generic.List<string>(_enemyHand);

    // ★追加：カットインで手牌をオープンするタイミングでは灰色化しない
    _enemyGreyOutHandAfterScoreOk = false;

    // ★表示用に、和了直前の敵手牌（面子/雀頭）をスナップショットしておく
    EnemyAddon_CaptureEnemyWinSnapshot();

    // ★内部ステートは空にする（以降の敵の手牌進行を止める）が、見た目は残す
    _enemyClearVisualPendingAfterScoringOk = true;
    EnemyAddon_ClearEnemyHandForContinueAfterWin(clearVisualNow: false);

    // ★仕様変更：ツモ和了はダメージ50%、ロン和了は100%
    if (_enemyLastWinWasTsumo)
    {
        score = Mathf.Max(1, Mathf.RoundToInt(score * 0.5f));
        hpDmg = Mathf.Max(1, Mathf.RoundToInt(hpDmg * 0.5f));
    }

    int applied = 0;
    try
    {
        int beforeHP = playerHP;

        // ★仕様変更：ここでは playerHP を減らさず、「最終ダメージ量（お守り軽減後）」だけ確定する
        applied = ApplyDamageToPlayer_WithOmamori(hpDmg, "enemy_win");
        playerHP = beforeHP;
    }
    catch
    {
        applied = Mathf.Max(0, hpDmg);
    }
    // ★仕様変更：敵の和了でプレイヤーが受けるダメージは、スコアOK後に演出付きで反映する
    _pendingEnemyWinDamage = (applied > 0);
    _pendingEnemyWinDamageBase = Mathf.Max(0, hpDmg);
    _pendingEnemyWinDamageFinal = Mathf.Max(0, applied);

    _currentScoringAttackerIsPlayer = false;
    _addonLastScoringWasEnemy = true;

    __ClearAppliedSpecialTileUiCache();

    if (_legendaryDamageHalfTriggeredThisScoring && _legendaryDamageHalfTriggeredSourceTiles.Count > 0)
    {
        __AddAppliedSpecialTileUiLine(
            "・<color=#FF0000>予約されていた敵和了ダメージ半減がこの和了で発動</color>",
            _legendaryDamageHalfTriggeredSourceTiles
        );
    }

    // ★ここでは「敵スコアパネルを開く」までは行わず、
    //   カットイン用オーバーレイとドラ／手牌の準備だけを行う
    EnsureEnemyWinOverlay();                 // カットイン用オーバーレイ

if (_enemyWinTitle != null)
{
    _enemyWinTitle.text = _enemyLastWinWasTsumo
        ? EnemyAddon_FixedText_Local("win_tsumo")
        : EnemyAddon_FixedText_Local("win_ron");
}
    bool prevIncludeUra = _includeUraForScoring;
    _includeUraForScoring = _enemyLastWinWasRiichi;
    try { RevealUraDoraIfEligible(); } catch {}
    _includeUraForScoring = prevIncludeUra;

    RefreshScoringDoraUI_Enemy(doraIndicators);
    __CopyEnemyOverlayTilesIntoScoringEnemyPanel();   // ※オーバーレイ側が空なら実質何もしない

    // ★追加：敵の点数計算パネルの手牌を、オーバーレイ有無に関係なく必ず描画する
    EnemyAddon_PopulateEnemyScoringTilesManual();

    // ★ここで自動進行を完全停止し、確実にスコア状態へ固定
    _autoSkipPending = false;
    _autoConfirmOfferPending = false;
    phase = Phase.Scoring;
    if (_enemyWinOverlay != null)
    {
        StartCoroutine(__EnemyWin_ShowCutinAndScoring_Flow_Co(
            score, hpDmg, applied, prevPl, prevEn));
    }
else
{
    __SetScoringPanelActive(false);  // false = 敵側パネル
    WireScoringOK();
    __StartScoringStepReveal(false); // ★追加：敵パネルも段階表示を開始
}
    // 表ドラ／裏ドラ表示牌（インジケータ）を敵用パネルに反映
    RefreshScoringDoraUI_Enemy(doraIndicators);
}


System.Collections.IEnumerator __EnemyWin_ShowCutinAndScoring_Flow_Co(
    int score,
    int hpDmg,
    int appliedHpDamage,
    int prevPlayerHp,
    int prevEnemyHp)
{
    // ★追加：直前局の勝者が「敵」であることを記録（次局開始時の手牌リセット方式に使用）
    _addonLastHandWinnerWasEnemy = true;

    // 1) 和了確定から 0.5 秒待つ（敵のツモ演出用）
    yield return new UnityEngine.WaitForSeconds(1.5f);
// ★ここで敵の手牌をオープン
EnemyRevealHandNow();


    // 2) 敵カットイン（EnemyPortrait を含むオーバーレイ）をフェードイン表示
    if (_enemyWinOverlay != null)
    {
        var go = _enemyWinOverlay.gameObject;

        // CanvasGroup を取得（なければ追加）
        var cg = go.GetComponent<UnityEngine.CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<UnityEngine.CanvasGroup>();
        }

go.SetActive(true);

// ★敵ロン/ツモのカットイン：表示された瞬間にSE（AudioManagerへ集約）
if (AudioManager.Instance)
{
    if (_enemyLastWinWasTsumo)
        AudioManager.Instance.PlayCutin_EnemyTsumo();
    else
        AudioManager.Instance.PlayCutin_EnemyRon();
}

        // プレイヤー側のカットインと同様に、最初は透明にしてから
        // __Fade(CanvasGroup g, float from, float to, float dur) でフェードイン
        cg.alpha = 0f;
        yield return __Fade(cg, 0f, 1f, 0.1f);   // 0.1秒でスッとフェードイン
    }

    // 3) さらに 3.0 秒待つ（カットイン単体で見せる時間）
    yield return new UnityEngine.WaitForSeconds(3.0f);

    // 4) 敵用スコアパネルを表示
__SetScoringPanelActive(false); // false = 敵側パネル
EnemyAddon_RenderEnemyAgariTilesOnScoringPanel(); // ★和了牌を右出し表示


    // 5) 手動スコアUIを使っている場合、敵の点数計算パネルの中身をここで埋める
    if (__UseManualScoringUI())
    {
        // (1) お守り軽減率＆最終ダメージを計算して記録
        if (hpDmg > 0)
        {
            // hpDmg … 軽減前のダメージ
            // appliedHpDamage … お守りで軽減後に実際にプレイヤーが受けたダメージ
            float ratio = 1f - (float)appliedHpDamage / hpDmg;
            int pct = Mathf.Clamp(Mathf.RoundToInt(ratio * 100f), 0, 100);

            EnemyAddon_LastOmamoriPct   = pct;
            EnemyAddon_LastFinalDamage  = Mathf.Max(0, appliedHpDamage);
        }

        try
        {
// (2) 敵の和了手牌をスコアパネルに描画する
//     ※和了牌は「右端に間隔を空けて」表示したいので、専用メソッドに一元化する
EnemyAddon_RenderEnemyAgariTilesOnScoringPanel();
// 役
if (scoringRoleValue_Enemy != null)
    scoringRoleValue_Enemy.text = __NormalizeYakuDisplayText(EnemyAddon_LastYakuText ?? string.Empty);

if (scoringFuHanValue_Enemy != null)
{
    scoringFuHanValue_Enemy.text = BuildScoringHanFuText_Local(EnemyAddon_LastHan, EnemyAddon_LastFu);
}
            // 点数（基本点）
            if (scoringBasePointValue_Enemy != null)
            {
                // ★仕様変更：敵ツモ和了時は基礎点の表示を50%減＋ラベル追加
                int _addonDisplayBasePt = EnemyAddon_LastPoints;
                string _addonTsumoLabel = "";
                if (_enemyLastWinWasTsumo)
                {
                    _addonDisplayBasePt = Mathf.Max(1, Mathf.RoundToInt(_addonDisplayBasePt * 0.5f));
                    var _lmA = LocalizationManager.Instance;
                    var _langA = (_lmA != null) ? _lmA.CurrentLanguage : LocalizationManager.Language.Japanese;
                    switch (_langA)
                    {
                        case LocalizationManager.Language.English:           _addonTsumoLabel = " (Tsumo -50%)"; break;
                        case LocalizationManager.Language.ChineseSimplified: _addonTsumoLabel = "（自摸和　减50%）"; break;
                        default:                                             _addonTsumoLabel = "（ツモあがり　50％減）"; break;
                    }
                }
                scoringBasePointValue_Enemy.text = $"{_addonDisplayBasePt}{_addonTsumoLabel}";
            }
            // ★お守り軽減％と最終ダメージはここでは触らない

            // お守りによるダメージ軽減％
            if (scoringOmamoriReduceValue != null)
            {
                // EnemyAddon_LastOmamoriPct が 0 未満の場合は「0%」表示にしておく
                int pct = EnemyAddon_LastOmamoriPct;
                if (pct < 0) pct = 0;
                scoringOmamoriReduceValue.text = pct.ToString() + "%";
            }
// プレイヤーへの最終ダメージ（お守り適用後）
// ★表示は「実際に適用される最終値」に合わせる（特別牌レジェ②：直後の敵和了ダメージ半減を反映）
if (scoringFinalDamageToPlayerValue != null)
{
    int displayDamage = Mathf.Max(0, EnemyAddon_LastFinalDamage);

    // レジェ②は「スコアOK時に消費」なので、ここでは Preview で“本当に受ける量”を表示する
    displayDamage = PreviewLegendaryDamageHalfOnEnemyWin(displayDamage);

    scoringFinalDamageToPlayerValue.text = displayDamage.ToString();
}
            // ★追加：怒り/防御など「最終ダメージに影響した敵スキル」を更新
            UpdateScoringPanelUI_EnemyExtra();

            // ★お守り軽減％と最終ダメージはここで反映

        }
        catch
        {
            // ここで例外が出ても対局が止まらないように握りつぶし
        }
    }
    // ★6) カットイン＆スコア表示で使い終わった敵副露情報をここでクリア
    if (_enemyCommittedMelds != null)
        _enemyCommittedMelds.Clear();
    _enemyCommittedPair = null;

    // ★敵の手牌構築ステートもリセット（局は継続するが手牌はゼロから作り直す）
    _enemyHasBaseHand = false;
    _enemyTaatsu      = null;
    _enemyTaatsuIdx0  = -1;
    _enemyTaatsuIdx1  = -1;
    _enemyIsInTenpai  = false;
    _enemyIsInRiichi  = false;

    // ★ターツ・雀頭ごとのUIを再表示（次の手牌構築のため）
    if (enemyMeldsManualRoot)
        enemyMeldsManualRoot.gameObject.SetActive(true);

    // 敵の副露パネルを更新し、すでに使用済みの捨て牌はグレーハイライト維持
    RefreshEnemyMeldsPanel();
    GreyCommittedDiscards();
// リーチ用手牌 UI：
//  - 要件①：敵が和了した場合、スコアパネルOKが押されるまで「見た目」は残す
//  - そのため、敵和了フローの時点ではここでは消さず、OK押下時に消す
if (enemyTenpaiHandManualRoot)
{
    // 敵和了フローの時点ではここでは消さない。
    // スコアOK押下時（Addon_WasEnemyScoringAndReset）で消す。
    _enemyClearVisualPendingAfterScoringOk = true;
}
// 6) OK ボタンで次に進めるようにイベントを張る
WireScoringOK();
__StartScoringStepReveal(false); // ★追加：敵パネルも段階表示を開始
}


// プレイヤー検討→スコア（trueを返すと「後でスコア」を予約済み）
private bool TryOfferPlayerReactionThenScore(int score, int hpDmg, int prevPl, int prevEn)
{
    // このターンの敵捨て牌のうち、敵採用ロックされていない牌が1つでも“反応可能”なら検討フェーズを挟む
    int start = Math.Max(0, enemyDiscards.Count - lastEnemyTurnTiles.Count);
    bool actionable = false;
    for (int i = start; i < enemyDiscards.Count; i++)
    {
        var go = (enemyDiscardArea && i < enemyDiscardArea.childCount) ? enemyDiscardArea.GetChild(i).gameObject : null;
        if (go && !_committedDiscardInstanceIDs.Contains(go.GetInstanceID()))
        {
string raw = enemyDiscards[i];

// ★敵捨て牌は "*" や "_sp" 等を含み得るので、必ずロジック用に正規化して判定する
string tile  = StripStar(raw);
string logic = StripTileIdForLogic(tile);

bool ron  = !string.IsNullOrEmpty(logic) && CanRonWith(logic, out _, out _, out _, out _);
bool call = !string.IsNullOrEmpty(logic) && (CanPonWithBase(logic) || CanChiWithBase(logic) || CanKanWithBase(logic));

if (ron || call) { actionable = true; break; }

        }
    }
    if (!actionable) return false;

    StartCoroutine(__EnemyWinReactionThenScore_Co(score, hpDmg, prevPl, prevEn));
    return true;
}
private System.Collections.IEnumerator __EnemyWinReactionThenScore_Co(int score, int hpDmg, int prevPl, int prevEn)
{
    // 検討ウィンドウに入れる
    phase = Phase.EnemyTurn;
    selectedEnemyIndex = -1;
    RefreshEnemyDiscardUI(); // 内部で WireEnemyTurnClickTargets → ロック済み捨て牌は不可
    EvaluateWinUI_New();
    if (statusTMP) statusTMP.text = EnemyAddon_FixedText_Local("enemy_discard_call_or_ron_available");

    float t = 0f, max = 1.2f; // 必要なら調整可
    while (t < max)
    {
        if (phase != Phase.EnemyTurn && phase != Phase.ChoosingCall) break; // 鳴きに入った/抜けたら抜ける
        t += Time.unscaledDeltaTime;
        yield return null;
    }

    FinalizeEnemyWin_ShowScoringAndCleanup(score, hpDmg, prevPl, prevEn);
}
// ★敵オーバーレイ → スコアパネル（手動UI：敵欄）へタイル複製
private void __CopyEnemyOverlayTilesIntoScoringEnemyPanel()
{
    try
    {
        // 手動UIの敵タイル欄（GameManager.cs で宣言）:
        //   [SerializeField] private RectTransform scoringEnemyTilesManual;
        if (scoringEnemyTilesManual == null) return;

        // いったん全消去（残留していた“プレイヤー手牌”を確実に除去）
        for (int i = scoringEnemyTilesManual.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(scoringEnemyTilesManual.GetChild(i).gameObject);

        // オーバーレイ側の生成結果が無ければ終了
        if (_enemyWinTiles == null) return;

        // オーバーレイに並んだ見た目をそのまま複製（位置・サイズ・回転を引き継ぐ）
        for (int i = 0; i < _enemyWinTiles.childCount; i++)
        {
            var src = _enemyWinTiles.GetChild(i) as RectTransform;
            if (src == null) continue;

            var clone = UnityEngine.Object.Instantiate(src.gameObject, scoringEnemyTilesManual);
            var rt = clone.GetComponent<RectTransform>();
            var srt = src.GetComponent<RectTransform>();
            if (rt != null && srt != null)
            {
                rt.anchorMin = srt.anchorMin;
                rt.anchorMax = srt.anchorMax;
                rt.pivot     = srt.pivot;
                rt.anchoredPosition = srt.anchoredPosition;
                rt.sizeDelta = srt.sizeDelta;
                rt.localRotation = srt.localRotation;
                rt.localScale    = Vector3.one; // スコア欄は等倍で
            }
        }
    }
    catch { /* 安全に握りつぶし（落ちないこと最優先）*/ }
}
// 敵の和了演出（0.5秒待機 → カットイン＋スコアパネル表示）
private System.Collections.IEnumerator __EnemyWin_ShowCutinAndScoring_Co(
    int score, int hpDmg, int applied, int prevPl, int prevEn)
{
    // オーバーレイが無ければ即フォールバック
    if (_enemyWinOverlay == null)
    {
        WireScoringOK();
        yield break;
    }

    // 0.5秒の“間”を置く（敵のツモ演出のため）
    yield return new WaitForSeconds(0.5f);

    // カットイン付きオーバーレイを表示しつつ、敵側スコアパネルを開く
    // （カットイン用の EnemyOverlay / 手牌のコピーは EnsureEnemyWinOverlay で準備済み）
    ShowEnemyWinPanel(score, applied, prevPl, playerHP, prevEn, enemyHP);

    // ※ 手動スコアUIへの値セットは __ApplyScoringManualUI(...) 側で一度だけ行う。
    // ここではオーバーレイの表示と、スコアパネルの表示制御だけに留める。

    // 以降の「OK で閉じる」「次の進行へ」は、従来通りボタン側の CloseEnemyWinPanel() に任せる
}
private List<string> EnemyAddon_GetWaitTilesForTaatsu(List<string> taatsu)
{
    var result = new List<string>();
    if (taatsu == null || taatsu.Count != 2) return result;

    // ロジック上は「見た目差」(sp 等)を無視して評価したいので Strip して比較
    string a = StripTileIdForLogic(taatsu[0]);
    string b = StripTileIdForLogic(taatsu[1]);
    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return result;

    // 対子（シャンポン）:
    //  - ターツ自体が対子ならその牌は待ち
    //  - さらに「確定雀頭（_enemyCommittedPair）」も別対子なら、雀頭側でも和了できる（2対子のシャンポン）
    if (a == b)
    {
        result.Add(a); // ターツ側の対子待ち

        // ★追加：雀頭のすり替わり（2対子シャンポン）を考慮して、確定雀頭側も待ちに入れる
        if (_enemyCommittedPair != null && _enemyCommittedPair.Count == 2)
        {
            string p0 = StripTileIdForLogic(_enemyCommittedPair[0]);
            string p1 = StripTileIdForLogic(_enemyCommittedPair[1]);

            // 雀頭が正しい対子で、ターツ側と別なら追加
            if (!string.IsNullOrEmpty(p0) && p0 == p1 && p0 != a)
            {
                result.Add(p0);
            }
        }

        return result;
    }

    // 数牌以外（字牌）はターツになりえない（上で対子は処理済み）
    if (!AddonTryParseSuitNum(a, out int sa, out int na)) return result;
    if (!AddonTryParseSuitNum(b, out int sb, out int nb)) return result;
    if (sa != sb) return result;

    // 並び替え
    if (na > nb) { int tmp = na; na = nb; nb = tmp; }

    // リャンメン/ペンチャン
    if (nb - na == 1)
    {
        int left = na - 1;
        int right = nb + 1;
        if (left >= 1) result.Add(AddonIdOf(sa, left));
        if (right <= 9) result.Add(AddonIdOf(sa, right));
        return result;
    }

    // カンチャン
    if (nb - na == 2)
    {
        int mid = na + 1;
        if (mid >= 1 && mid <= 9) result.Add(AddonIdOf(sa, mid));
        return result;
    }

    // それ以外はターツとして不成立
    return result;
}
private bool EnemyAddon_TryPickBestTaatsu(
    List<DiscardTrip> unused,
    out int bestIdx0,
    out int bestIdx1,
    out List<string> bestTaatsu)
{
    bestIdx0   = -1;
    bestIdx1   = -1;
    bestTaatsu = null;

    if (unused == null || unused.Count < 2) return false;

    // ★安全弁：unused が肥大化すると O(n^2) でも重くなるので上限をかける
    const int MaxConsider = 40;
    if (unused.Count > MaxConsider)
        unused = unused.GetRange(unused.Count - MaxConsider, MaxConsider);

    // 優先：両面(3) > ペンチャン(2) > 嵌張(1) > 対子(0)
    int globalBestShapeRank = -1;
    int globalBestWaitKinds = -1;      // waits の “種類数”
    int globalBestFinishIdx = int.MaxValue;

    int ShapeRank(string a, string b, int waitKinds)
    {
        if (a == b) return 0; // 対子は最下位（シャンポン寄り）

        if (!AddonTryParseSuitNum(a, out int sa, out int na)) return 0;
        if (!AddonTryParseSuitNum(b, out int sb, out int nb)) return 0;
        if (sa != sb) return 0;

        if (na > nb) { int t = na; na = nb; nb = t; }

        if (nb - na == 1) return (waitKinds >= 2) ? 3 : 2; // 両面 or ペンチャン
        if (nb - na == 2) return 1;                        // 嵌張
        return 0;
    }

    for (int i = 0; i < unused.Count; i++)
    {
        for (int j = i + 1; j < unused.Count; j++)
        {
            var t1 = unused[i];
            var t2 = unused[j];

            var cand = new List<string> { t1.id, t2.id };

            // 待ち牌候補
            var waits = EnemyAddon_GetWaitTilesForTaatsu(cand);
            if (waits == null || waits.Count == 0) continue;

            // Distinct を避けて GC を抑える（種類数だけ欲しい）
            int waitKinds = 0;
            var seen = new HashSet<string>();
            for (int k = 0; k < waits.Count; k++)
            {
                var w = waits[k];
                if (string.IsNullOrEmpty(w)) continue;
                if (seen.Add(w)) waitKinds++;
            }
            if (waitKinds <= 0) continue;

            int shapeRank = ShapeRank(t1.id, t2.id, waitKinds);
            int finishIdx = Math.Max(t1.idx, t2.idx);

            bool better = false;
            if (shapeRank > globalBestShapeRank) better = true;
            else if (shapeRank == globalBestShapeRank)
            {
                if (waitKinds > globalBestWaitKinds) better = true;
                else if (waitKinds == globalBestWaitKinds && finishIdx < globalBestFinishIdx) better = true;
            }

            if (better)
            {
                globalBestShapeRank = shapeRank;
                globalBestWaitKinds = waitKinds;
                globalBestFinishIdx = finishIdx;

                bestIdx0   = t1.idx;
                bestIdx1   = t2.idx;
                bestTaatsu = cand;
            }
        }
    }

    return bestTaatsu != null;
}

    // ★敵がリーチしたときの手牌（聴牌形）を、専用の UI に描画する。
    //  - 3メンツ＋1雀頭＋ターツ(2枚) = 13枚として扱う。
    //  - 4メンツ1雀頭用 UI (enemyMeldsManualRoot etc.) は非表示にする。
    private void EnemyAddon_EnterRiichiUI()
    {
        if (!useManualEnemyMeldsUI) return;
        if (!enemyTenpaiHandManualRoot) return;
        if (_enemyCommittedPair == null || _enemyCommittedMelds == null) return;
        if (_enemyTaatsu == null || _enemyTaatsu.Count != 2) return;

        // まず既存の 4メンツ1雀頭 UI を消す
        if (enemyMeldsManualRoot)
            enemyMeldsManualRoot.gameObject.SetActive(false);

        // リーチ用 UI をクリア
        for (int i = enemyTenpaiHandManualRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(enemyTenpaiHandManualRoot.GetChild(i).gameObject);

        // 聴牌形の 13枚を組み立て
        var tiles13 = new List<string>();
        foreach (var mm in _enemyCommittedMelds)
            if (mm != null) tiles13.AddRange(mm);
        tiles13.AddRange(_enemyCommittedPair);
        tiles13.AddRange(_enemyTaatsu);

        // 念のため 13 枚に揃うよう制限
        if (tiles13.Count > 13)
            tiles13 = tiles13.Take(13).ToList();

        // 並び順は単純にソート（見た目優先ならここを調整）
        tiles13.Sort(StringComparer.Ordinal);

     float px = 0f;
     foreach (var id in tiles13)
     {
         int w = Mathf.RoundToInt(enemyTenpaiManualTileWidthPx);
         CreateTileImage(enemyTenpaiHandManualRoot, id, ref px, w);
     }

        enemyTenpaiHandManualRoot.gameObject.SetActive(true);
    }
private void EnemyAddon_DoTsumoWin(string winTileId)
{
    // テンパイ状態＋ターツが確定していなければ、ツモ和了は成立しない
    if (string.IsNullOrEmpty(winTileId)) return;

    // ★追加：直近の和了牌を保持
    _enemyLastWinTileId = winTileId;
    if (_enemyTaatsu == null || _enemyTaatsu.Count != 2) return;

    // ターツ＋和了牌で 1 面子を作り、敵の確定メンツリストに追加
    var finalMeld = new List<string> { _enemyTaatsu[0], _enemyTaatsu[1], winTileId };
    finalMeld.Sort(StringComparer.Ordinal);
    if (_enemyCommittedMelds != null)
        _enemyCommittedMelds.Add(finalMeld);

    // 和了種別フラグ（ここではツモ確定。リーチフラグは呼び出し側で設定済み）
    // _enemyLastWinWasRiichi は TryProgressEnemyHandFromDiscards 内で設定済み
    _enemyLastWinWasTsumo = true;

    int score;
    try
    {
        // プレイヤーと同じロジックで敵の点数・役を算出
        score = EnemyAddon_ComputeScoreLikePlayer(_enemyCommittedMelds, _enemyCommittedPair);
    }
    catch
    {
        // 万一計算に失敗した場合は従来の標準ロジックにフォールバック
        score = ComputeEnemyScoreStandard();
    }

    int hpDmg  = Mathf.Max(1, score);

    // ★追加：敵が攻撃側のダメージ最終調整（怒り倍率をここで適用して使い切る）
    try
    {
        bool prevAttackerIsPlayer = _currentScoringAttackerIsPlayer;
        _currentScoringAttackerIsPlayer = false; // 敵が攻撃側として扱う

        int dummyMp = 0;
        int dummyHp = 0;
        EnemySkills_ModifyDamageBeforeApply(ref hpDmg, ref dummyMp, ref dummyHp);

        _currentScoringAttackerIsPlayer = prevAttackerIsPlayer;
    }
    catch { }

    int prevPl = playerHP;
    int prevEn = enemyHP;

    _enemyWinDeclaredTurnCounter = _enemyTurnCounter; // ★追加：このターンは敵スキル発動禁止

    // 敵和了時のカットイン＋点数計算パネル表示まで一括で進行
    FinalizeEnemyWin_ShowScoringAndCleanup(score, hpDmg, prevPl, prevEn);
}


private bool EnemyAddon_TryRonOnPlayerDiscard(string discardedId)
{
    // プレイヤーの捨て牌が 1 枚だけのときは、共通ロジックに委譲
    if (string.IsNullOrEmpty(discardedId))
        return false;

    var list = new List<string> { discardedId };
    return EnemyAddon_TryRonOnPlayerDiscards(list);
}
private bool EnemyAddon_TryRonOnPlayerDiscards(List<string> discardedIds)
{
    if (_enemyHasWonThisHand) return false;

    var winCandidates = new List<(int idx, string raw, string baseId, int score)>();

    // ★仕様変更：リーチ中は _enemyRiichiWaits（ツモ判定と共通の待ち牌リスト）でロン判定する
    //   → IsAnyWinningShape の面子分解アルゴリズムに依存しないため確実
    bool useRiichiWaits = _enemyIsInRiichi
        && _enemyRiichiWaits != null
        && _enemyRiichiWaits.Count > 0
        && _enemyTurnCounter != _enemyRiichiDeclaredTurnCounter; // リーチ宣言ターンは和了しない

    for (int i = 0; i < discardedIds.Count; i++)
    {
        var raw = discardedIds[i];
        string baseId = StripTileIdForLogic(raw);
        if (string.IsNullOrEmpty(baseId)) continue;

        bool canRon = false;
        if (useRiichiWaits)
        {
            // リーチ中：待ち牌リストに含まれるなら即ロン
            canRon = _enemyRiichiWaits.Contains(baseId);
        }
        else
        {
            // リーチ前：従来の和了形チェック
            canRon = EnemyAddon_CanRonOnTileShapeOnly(baseId);
        }

        if (!canRon) continue;

        int score = EnemyAI_ComputeClosedHandScore(_enemyHand, baseId, isTsumo: false);
        if (score <= 0)
        {
            score = 1000;
        }

        winCandidates.Add((i, raw, baseId, score));
    }

    if (winCandidates.Count == 0) return false;

    winCandidates.Sort((a, b) => b.score.CompareTo(a.score));
    var best = winCandidates[0];

    int bestIdx = best.idx;
    string bestWinBaseId = best.baseId;
    int bestScore = best.score;

    int baseIndex = discards.Count - discardedIds.Count;
    int absoluteDiscardIndex = baseIndex + bestIdx;
    if (absoluteDiscardIndex >= 0)
    {
        _enemyRonGreyPlayerDiscardIndices.Add(absoluteDiscardIndex);
    }

    EnemyAI_DeclareEnemyRonWin(bestWinBaseId, bestScore);
    return true;
}
private bool EnemyAddon_HasRightInfoTargets()
{
    return
        _skillInfoTMP != null ||
        _skillNameTMP != null ||
        _skillDescTMP != null ||
        _skillTraitGekiTMP != null ||
        _skillTraitShunTMP != null ||
        _skillTraitIyuTMP != null ||
        _omamoriInfoTMP != null ||
        _ofudaInfoTMP != null;
}

// ★敵が和了した直後は、見た目（敵手牌UI）をスコアOKまで残すためのフラグ
private bool _enemyClearVisualPendingAfterScoringOk = false;

// ★追加：敵が和了しても「カットインで手牌オープンした直後」は灰色化しない。
//        灰色化は「敵の点数計算パネルを閉じた（スコアOK）」後にだけ行う。
private bool _enemyGreyOutHandAfterScoreOk = false;

private System.Collections.IEnumerator EnemyAddon_ShowRiichiCutinThenEnterTenpaiUI()
{
    // ★リーチ演出中フラグON（この間は進行を止める）
    _enemyRiichiCutinRunning = true;
    UpdateButtons();

    // ① まずリーチ用の聴牌UIに切り替え（リーパイして表示）
    EnemyAddon_EnterRiichiUI();

    if (!enemyRiichiCutinRoot)
    {
        _enemyRiichiCutinRunning = false;
        UpdateButtons();
        yield break;
    }

    yield return new WaitForSeconds(0.3f);

if (enemyRiichiTextTMP)
    enemyRiichiTextTMP.text = EnemyAddon_FixedText_Local("yaku_riichi_short");

    try
    {
        if (enemyRiichiImage != null)
        {
            string enemyName = GetCurrentEnemyNameFromExcelWithLoop();
            if (!string.IsNullOrEmpty(enemyName))
            {
                TryLoadEnemyBattlePortraitByName(enemyName);

                GameObject enemyPortrait = null;

                if (enemyWinOverlayManualRoot != null)
                {
                    var t = enemyWinOverlayManualRoot.transform.Find("EnemyPortrait");
                    if (t != null) enemyPortrait = t.gameObject;
                }

                if (enemyPortrait == null)
                {
                    enemyPortrait = GameObject.Find("EnemyPortrait");
                }

                if (enemyPortrait != null)
                {
                    var img = enemyPortrait.GetComponent<Image>();
                    if (img != null)
                    {
                        enemyRiichiImage.sprite = img.sprite;
                    }
                }
            }
        }
    }
    catch { }

    // ★追加：カットインが下のUI（スキップ等）をブロックしないように、raycastTarget を一時的に無効化
    UnityEngine.UI.Graphic[] riichiCutinGraphics = null;
    bool[] prevRaycastTargets = null;
    try
    {
        riichiCutinGraphics = enemyRiichiCutinRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        prevRaycastTargets = new bool[riichiCutinGraphics.Length];
        for (int i = 0; i < riichiCutinGraphics.Length; i++)
        {
            prevRaycastTargets[i] = riichiCutinGraphics[i].raycastTarget;
            riichiCutinGraphics[i].raycastTarget = false;
        }
    }
    catch { }

    enemyRiichiCutinRoot.SetActive(true);
// ★カットインが「表示された瞬間」にSE（AudioManagerへ集約）
if (AudioManager.Instance)
{
    AudioManager.Instance.PlayCutin_EnemyRiichi();
}
    // ★追加：CanvasGroup がある場合も raycast をブロックしない
    try
    {
        var cg = enemyRiichiCutinRoot.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
    }
    catch { }

    yield return new WaitForSeconds(2f);

    enemyRiichiCutinRoot.SetActive(false);

    // ★追加：raycastTarget を元に戻す
    try
    {
        if (riichiCutinGraphics != null && prevRaycastTargets != null)
        {
            int n = Mathf.Min(riichiCutinGraphics.Length, prevRaycastTargets.Length);
            for (int i = 0; i < n; i++)
            {
                if (riichiCutinGraphics[i] != null)
                    riichiCutinGraphics[i].raycastTarget = prevRaycastTargets[i];
            }
        }
    }
    catch { }

    _enemyRiichiCutinRunning = false;
    UpdateButtons();
}


}
