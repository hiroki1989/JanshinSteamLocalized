using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocalizationManager : MonoBehaviour
{
    public enum Language
    {
        Japanese,
        English,
        ChineseSimplified
    }

    [Serializable]
    public class FontSet
    {
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset titleFont;
        public TMP_FontAsset numberFont;
    }

    public const string PlayerPrefsKeyLanguage = "GameLanguage";

    private static LocalizationManager _instance;
    public static LocalizationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<LocalizationManager>();
                if (_instance == null)
                {
                    var go = new GameObject("LocalizationManager");
                    _instance = go.AddComponent<LocalizationManager>();
                }
            }
            return _instance;
        }
    }

    [Header("Current Language")]
    [SerializeField] private Language currentLanguage = Language.Japanese;

    [Header("Fonts")]
    [SerializeField] private FontSet japaneseFonts = new FontSet();
    [SerializeField] private FontSet englishFonts = new FontSet();
    [SerializeField] private FontSet chineseSimplifiedFonts = new FontSet();

    private readonly Dictionary<string, string> _ja = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _en = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _zhHans = new Dictionary<string, string>(StringComparer.Ordinal);

private bool _initialized = false;

public Language CurrentLanguage => currentLanguage;

public SystemLanguage CurrentSystemLanguage
{
    get
    {
        switch (currentLanguage)
        {
            case Language.English:
                return SystemLanguage.English;

            case Language.ChineseSimplified:
                return SystemLanguage.ChineseSimplified;

            case Language.Japanese:
            default:
                return SystemLanguage.Japanese;
        }
    }
}
public void SetLanguage(SystemLanguage language)
{
    switch (language)
    {
        case SystemLanguage.English:
            SetLanguage(Language.English);
            break;

        case SystemLanguage.ChineseSimplified:
            SetLanguage(Language.ChineseSimplified);
            break;

        case SystemLanguage.Japanese:
        default:
            SetLanguage(Language.Japanese);
            break;
    }
}
public string GetEnemyDisplayName(string rawName)
{
    InitializeIfNeeded();

    if (string.IsNullOrEmpty(rawName))
        return string.Empty;

    string source = rawName.Trim();
    if (source.Length == 0)
        return string.Empty;

    string baseName = source;
    string suffix = "";

    int plusIndex = source.IndexOf('+');
    if (plusIndex >= 0)
    {
        baseName = source.Substring(0, plusIndex).Trim();
        suffix = source.Substring(plusIndex).Trim();
    }

    string key = "enemy_name." + baseName;
    string localized = GetText(key);

    if (localized == key)
        localized = baseName;

    return localized + suffix;
}
public static event Action<Language> LanguageChanged;
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeIfNeeded();
        LoadLanguageFromPrefs();
    }

    private void InitializeIfNeeded()
    {
        if (_initialized) return;

        BuildJapaneseTable();
        BuildEnglishTable();
        BuildChineseSimplifiedTable();

        _initialized = true;
    }

    private void LoadLanguageFromPrefs()
    {
        try
        {
            string raw = PlayerPrefs.GetString(PlayerPrefsKeyLanguage, Language.Japanese.ToString());
            if (Enum.TryParse(raw, true, out Language parsed))
            {
                currentLanguage = parsed;
            }
            else
            {
                currentLanguage = Language.Japanese;
            }
        }
        catch
        {
            currentLanguage = Language.Japanese;
        }
    }
    public void SetLanguage(Language language)
    {
        InitializeIfNeeded();

        bool changed = currentLanguage != language;
        currentLanguage = language;

        try
        {
            PlayerPrefs.SetString(PlayerPrefsKeyLanguage, currentLanguage.ToString());
            PlayerPrefs.Save();
        }
        catch
        {
        }

        try
        {
            LanguageChanged?.Invoke(currentLanguage);
        }
        catch
        {
        }

        if (changed)
        {
            try
            {
                var roots = FindObjectsOfType<LocalizedTextUI>(true);
                if (roots != null)
                {
                    foreach (var x in roots)
                    {
                        if (x != null) x.RefreshNow();
                    }
                }
            }
            catch
            {
            }
        }
    }
        public int GetDropdownIndexForCurrentLanguage()
    {
        return GetDropdownIndex(currentLanguage);
    }

    public int GetDropdownIndex(Language language)
    {
        switch (language)
        {
            case Language.English:
                return 1;

            case Language.ChineseSimplified:
                return 2;

            case Language.Japanese:
            default:
                return 0;
        }
    }

    public Language GetLanguageFromDropdownIndex(int index)
    {
        switch (index)
        {
            case 1:
                return Language.English;

            case 2:
                return Language.ChineseSimplified;

            case 0:
            default:
                return Language.Japanese;
        }
    }
public string GetText(string key)
{
    InitializeIfNeeded();

    if (string.IsNullOrEmpty(key)) return string.Empty;

    key = key.Trim();

    string value;
    switch (currentLanguage)
    {
        case Language.English:
            if (_en.TryGetValue(key, out value)) return value;
            break;

        case Language.ChineseSimplified:
            if (_zhHans.TryGetValue(key, out value)) return value;
            break;

        case Language.Japanese:
        default:
            if (_ja.TryGetValue(key, out value)) return value;
            break;
    }

    if (_ja.TryGetValue(key, out value)) return value;

    return key;
}
    public string FormatText(string key, params object[] args)
    {
        string format = GetText(key);
        if (string.IsNullOrEmpty(format)) return string.Empty;

        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }
public string GetFixedText(string key)
{
    if (string.IsNullOrEmpty(key))
        return string.Empty;

    string normalized = key.Trim();
    if (normalized.Length == 0)
        return string.Empty;

    string directKey = normalized.StartsWith("fixed.", StringComparison.Ordinal)
        ? normalized
        : "fixed." + normalized;

    string value = GetText(directKey);
    if (!string.Equals(value, directKey, StringComparison.Ordinal))
        return value;

    string aliasKey = directKey;

    if (aliasKey.Contains("yaku."))
        aliasKey = aliasKey.Replace("yaku.", "yaku_");
    else if (aliasKey.Contains("yaku_"))
        aliasKey = aliasKey.Replace("yaku_", "yaku.");

    value = GetText(aliasKey);
    if (!string.Equals(value, aliasKey, StringComparison.Ordinal))
        return value;

    return value == aliasKey ? directKey : value;
}
    public string GetYakuDisplayName(string canonicalKey)
    {
        return GetText("yaku." + canonicalKey);
    }

    public string GetYakumanDisplayName(string canonicalKey)
    {
        return GetText("yakuman." + canonicalKey);
    }

    public string GetEnemySkillDisplayName(string canonicalKey)
    {
        return GetText("enemy_skill." + canonicalKey);
    }
public string GetRarityDisplayName(string canonicalKey)
{
    return GetText("rarity." + canonicalKey);
}

public string GetActiveSkillDisplayName(string canonicalKey)
{
    return GetText("active_skill." + canonicalKey);
}

public string GetActiveSkillActionName(string canonicalKey)
{
    return GetText("active_skill_action." + canonicalKey);
}

public string GetActiveSkillDescription(string canonicalKey)
{
    return GetText("active_skill_desc." + canonicalKey);
}

public TMP_FontAsset GetBodyFont()
{
    return GetFontsForCurrentLanguage().bodyFont;
}
    public TMP_FontAsset GetTitleFont()
    {
        return GetFontsForCurrentLanguage().titleFont;
    }

    public TMP_FontAsset GetNumberFont()
    {
        return GetFontsForCurrentLanguage().numberFont;
    }

    public TMP_FontAsset GetFontByRole(string role)
    {
        if (string.IsNullOrEmpty(role))
            return GetBodyFont();

        switch (role.Trim().ToLowerInvariant())
        {
            case "title":
                return GetTitleFont();

            case "number":
                return GetNumberFont();

            case "body":
            default:
                return GetBodyFont();
        }
    }

    private FontSet GetFontsForCurrentLanguage()
    {
        switch (currentLanguage)
        {
            case Language.English:
                return englishFonts ?? new FontSet();

            case Language.ChineseSimplified:
                return chineseSimplifiedFonts ?? new FontSet();

            case Language.Japanese:
            default:
                return japaneseFonts ?? new FontSet();
        }
    }

    private void AddAll(Dictionary<string, string> dict, IEnumerable<KeyValuePair<string, string>> items)
    {
        if (dict == null || items == null) return;

        foreach (var kv in items)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            dict[kv.Key] = kv.Value ?? "";
        }
    }

    private void BuildJapaneseTable()
    {
        _ja.Clear();

        AddAll(_ja, new Dictionary<string, string>
        {
            { "fixed.ok", "OK" },
            { "fixed.reward_title", "報酬" },
            { "fixed.reward_none", "報酬なし" },
            { "fixed.reward_unknown_name", "？？？" },
            { "fixed.owned_none", "所持しているお守りがありません。" },
            { "fixed.over_cap_hint", "所持お守り数が上限を超えています。破棄してください。" },
{ "fixed.final_score_prefix", "最終スコア：" },
{ "fixed.gem_gain_prefix", "宝石を" },
{ "fixed.gem_gain_middle", "個獲得！" },
{ "fixed.gem_gain_suffix", "個獲得" },
{ "fixed.enemy_defeat_suffix", "撃破" },
{ "fixed.enemy_defeat_suffix_emphatic", "撃破！" },
            { "fixed.unique_title_error", "<color=#FF0000>神器お守り</color>" },
            { "fixed.unique_desc_error_line1", "<color=#FF0000>神器お守りの付与に失敗しました。</color>" },
            { "fixed.unique_desc_error_line2", "<color=#FF0000>（ハーデス撃破時の追加報酬IDが不正です）</color>" },
{ "fixed.han_only_format", "{0}翻" },
{ "fixed.han_fu_format", "{0}翻　{1}符" },
{ "fixed.base_point_label", "基礎点" },
{ "fixed.damage_to_enemy_format", "{0}へのダメージ　{1}" },
            { "fixed.suit_man", "萬" },
            { "fixed.suit_pin", "筒" },
            { "fixed.suit_sou", "索" },
            { "fixed.suit_honor", "字" },
{ "fixed.trait_load_failed", "該当役：取得失敗" },
{ "fixed.trait_upgrade_none", "強化：候補がありません" },
{ "fixed.trait_upgrade_level_line_format", "Lv{0} {1}　→　Lv{2} {3}" },
{ "fixed.trait_upgrade_cost_line_format", "　{0}" },
{ "fixed.deck_empty_cost", "ー" },
    { "active_skill_action.RandomMan", "色寄せ" },
    { "active_skill_action.EnhanceHand", "筆写" },
    { "active_skill_action.Capitalist", "市場操作" },
            { "fixed.hp_up_prefix", "HP +" },
            { "fixed.mp_up_prefix", "MP +" },
            { "fixed.cast_up_prefix", "ターン上限 +" },
            { "fixed.heal_hp_prefix", "HP 回復 +" },
            { "fixed.heal_mp_prefix", "MP 回復 +" },
            { "fixed.label_separator", "  /  　 " },
            { "fixed.total_bonus_prefix", "（累計 +" },
            { "fixed.total_bonus_suffix", "）" },

            { "fixed.poison_tick_prefix", "毒のダメージ " },
            { "fixed.poison_tick_middle", " （残り" },
            { "fixed.poison_tick_suffix", "ターン）" },
            { "fixed.paralysis_recovered", "麻痺が解けた" },

            { "fixed.anger_status_prefix", "敵が怒り状態！ " },
            { "fixed.anger_status_middle", "ターンの間、敵の和了ダメージ +" },
            { "fixed.anger_status_suffix", "％" },
{ "fixed.player_victory", "撃破" },
{ "fixed.player_defeat", "敗北" },
{ "fixed.trait_shop_title", "役強化" },
{ "fixed.trait_unlock_label", "解放" },
{ "fixed.trait_upgrade_label", "強化" },
{ "fixed.trait_unlock_button", "解放" },
{ "fixed.trait_upgrade_button", "強化" },
            { "fixed.poison_status_prefix", "毒！ " },
            { "fixed.poison_status_middle", "ターンの間、毎ターン" },
            { "fixed.poison_status_suffix", "ダメージ" },

            { "fixed.paralysis_status_prefix", "麻痺！ " },
            { "fixed.paralysis_status_suffix", "ターンの間、スキルと鳴きが封じられた" },

            { "fixed.skill_quoted_prefix", "敵スキル『" },
            { "fixed.skill_quoted_middle", "』！ " },
            { "fixed.attack_status_suffix", "ダメージ" },
            { "fixed.disturb_status_prefix", "MP を " },
            { "fixed.disturb_status_suffix", "失った" },
            { "fixed.trick_status_suffix", "手牌の一部が入れ替えられた" },
            { "fixed.trick_done_suffix", "手牌が書き換えられた" },
{ "fixed.han_limit_format", "{0}翻　{1}" },
{ "fixed.limit_mangan", "満貫" },
{ "fixed.limit_haneman", "跳満" },
{ "fixed.limit_baiman", "倍満" },
{ "fixed.limit_sanbaiman", "三倍満" },
{ "fixed.limit_yakuman", "役満" },
{ "fixed.limit_double_yakuman", "ダブル役満" },
{ "fixed.limit_triple_yakuman", "トリプル役満" },
{ "fixed.limit_quadruple_yakuman", "クアドラプル役満" },
{ "fixed.limit_quintuple_yakuman", "クインタプル役満" },
{ "fixed.limit_multi_yakuman_format", "{0}倍役満" },
            { "fixed.defense_status_prefix", "敵スキル『" },
            { "fixed.defense_status_middle", "』！ " },
            { "fixed.defense_status_turn_middle", "ターンの間、プレイヤー和了ダメージ " },
            { "fixed.defense_status_suffix", "% 減少" },

            { "fixed.countdown_every_turn_suffix", "：毎ターン発動（Z<=1）" },
            { "fixed.countdown_remain_prefix", "：あと" },
            { "fixed.countdown_remain_middle", "ターン（Z=" },
            { "achievement.yakuman_win", "役満を和了" },
{ "achievement.kokushi", "国士無双を和了" },
{ "achievement.suuankou", "四暗刻を和了" },
{ "achievement.daisangen", "大三元を和了" },
{ "achievement.tsuuiisou", "字一色を和了" },
{ "achievement.ryuuiisou", "緑一色を和了" },
{ "achievement.shousuushii", "小四喜を和了" },
{ "achievement.daisuushii", "大四喜を和了" },
{ "achievement.chuuren", "九蓮宝燈を和了" },
{ "achievement.chinroutou", "清老頭を和了" },
{ "achievement.suukantsu", "四カンツを和了" },
{ "achievement.chihou", "地和を和了" },
{ "achievement.tenhou", "天和を和了" },

{ "achievement.score_100k", "スコア10万点達成" },
{ "achievement.score_200k", "スコア20万点達成" },
{ "achievement.score_500k", "スコア50万点達成" },
{ "achievement.score_800k", "スコア80万点達成" },
{ "achievement.score_1000k", "スコア100万点達成" },

{ "achievement.tier1_clear", "Tier1をクリア" },
{ "achievement.tier2_clear", "Tier2をクリア" },
{ "achievement.tier3_clear", "Tier3をクリア" },
{ "achievement.tier4_clear", "Tier4をクリア" },
{ "achievement.tier5_clear", "Tier5をクリア" },

{ "achievement.dyemaster_tier1_clear", "染色師でTier1をクリア" },
{ "achievement.calligrapher_tier1_clear", "書家でTier1をクリア" },
{ "achievement.capitalist_tier1_clear", "資産家でTier1をクリア" },

{ "achievement.legendary_omamori", "レジェンダリーお守りを入手" },
{ "achievement.legendary_special_tile", "レジェンダリー特別牌を入手" },
{ "achievement.shinki_get", "神器を入手" },
{ "achievement.hades_defeat", "ハデスを撃破" },
{ "achievement.hades_defeat_hidden", "???を撃破" },

{ "achievement.reward_gems", "宝石を{0}個獲得" },
{ "achievement.reward_none", "報酬はありません" },
            { "fixed.countdown_remain_suffix", "）" }
        });
AddAll(_ja, new Dictionary<string, string>
{
    { "enemy_name.アマテラス", "アマテラス" },
    { "enemy_name.スサノオ", "スサノオ" },
    { "enemy_name.バステト", "バステト" },
    { "enemy_name.シヴァ", "シヴァ" },
    { "enemy_name.アヌビス", "アヌビス" },
    { "enemy_name.フレイヤ", "フレイヤ" },
    { "enemy_name.ポセイドン", "ポセイドン" },
    { "enemy_name.オーディン", "オーディン" },
    { "enemy_name.ルーナ", "ルーナ" },
    { "enemy_name.ゼウス", "ゼウス" },
    { "enemy_name.ハデス", "ハデス" }
});
        AddAll(_ja, new Dictionary<string, string>
        {
            { "rarity.Normal", "ノーマル" },
            { "rarity.Common", "コモン" },
            { "rarity.Rare", "レア" },
            { "rarity.Epic", "エピック" },
            { "rarity.Legendary", "レジェンダリー" }
        });
AddAll(_ja, new Dictionary<string, string>
{
    { "active_skill.RandomMan", "染色師" },
    { "active_skill.EnhanceHand", "書家" },
    { "active_skill.Capitalist", "資産家" }
});
        AddAll(_ja, new Dictionary<string, string>
        {
            { "active_skill_desc.RandomMan", "選んだ牌を、（選んだ牌を除く）自分の手牌で最も多い色（萬/筒/索）のランダムな牌に変える（同数時は 萬＞筒＞索）。" },
            { "active_skill_desc.EnhanceHand", "選んだ牌を同スートの5に変換して強化。" }
        });

        AddAll(_ja, new Dictionary<string, string>
        {
            { "enemy_skill.anger", "怒り" },
            { "enemy_skill.poison", "毒" },
            { "enemy_skill.paralysis", "麻痺" },
            { "enemy_skill.attack", "攻撃" },
            { "enemy_skill.defense", "防御" },
            { "enemy_skill.disturb", "妨害" },
            { "enemy_skill.trick", "細工" }
        });
        AddAll(_ja, new Dictionary<string, string>
        {
            { "yaku.KOKUSHI", "国士無双" },
            { "yaku.CHIITOITSU", "七対子" },
            { "yaku.MENZEN_TSUMO", "門前清自摸和" },
            { "yaku.TANYAO", "タンヤオ" },
            { "yaku.PINFU", "平和" },
            { "yaku.YAKUHAI", "役牌" },
            { "yaku.IIPEIKOU", "一盃口" },
            { "yaku.RYANPEIKOU", "二盃口" },
            { "yaku.SANSHOKU_DOUJUN", "三色同順" },
            { "yaku.ITTSU", "一気通貫" },
            { "yaku.CHANTA", "チャンタ" },
            { "yaku.JUNCHAN", "純チャン" },
            { "yaku.TOITOI", "対々和" },
            { "yaku.SANANKOU", "三暗刻" },
            { "yaku.SANKANTSU", "三カンツ" },
            { "yaku.SANSHOKU_DOUKOU", "三色同刻" },
            { "yaku.SHOUSANGEN", "小三元" },
            { "yaku.HONROUTOU", "混老頭" },
            { "yaku.HONITSU", "混一色" },
            { "yaku.CHINITSU", "清一色" }
        });

        AddAll(_ja, new Dictionary<string, string>
        {
            { "yakuman.CHUUREN_POUTOU", "九蓮宝燈" },
            { "yakuman.KOKUSHI", "国士無双" },
            { "yakuman.DAISANGEN", "大三元" },
            { "yakuman.DAISUUSHI", "大四喜" },
            { "yakuman.SHOUSUUSHI", "小四喜" },
            { "yakuman.TSUUIISOU", "字一色" },
            { "yakuman.CHINROUTOU", "清老頭" },
            { "yakuman.RYUUIISOU", "緑一色" },
            { "yakuman.SUUANKOU", "四暗刻" },
            { "yakuman.SUUKANTSU", "四カンツ" },
            { "yakuman.TENHOU", "天和" },
            { "yakuman.CHIHOU", "地和" },
            { "yakuman.RENHOU", "人和" }
        });
        AddAll(_ja, new Dictionary<string, string>
        {
            { "fixed.trait_geki", "撃" },
            { "fixed.trait_shun", "瞬" },
            { "fixed.trait_iyu", "癒" },
            { "fixed.none", "なし" },
            { "fixed.trait_hp_suffix", "HP" },
            { "fixed.omamori_trait_shun_prefix", "お守り（瞬） +" },
            { "fixed.omamori_trait_iyu_prefix", "お守り（癒） +" },
            { "fixed.omamori_trait_geki_prefix", "お守り（撃） +" },
            { "fixed.percent_suffix", "%" },
            { "fixed.recommended_damage_prefix", "→ 推奨与ダメージ: " },
            { "fixed.recommended_recover_prefix", " / 回復: " },
        });
        AddAll(_ja, new Dictionary<string, string>
        {
            { "fixed.skill_paralyzed_cannot_use", "麻痺中のためスキルは使用できません" },
            { "fixed.skill_not_equipped", "スキル未装備" },
            { "fixed.skill_turn_limit_reached", "今ターンの使用回数上限です" },
            { "fixed.skill_not_enough_mp", "MPが足りません" },
            { "fixed.skill_activation_failed_invalid_target", "スキル発動に失敗しました（対象の牌を正しく選択してください）" },
            { "fixed.skill_activated", "スキル発動" },
            { "fixed.skill_select_hand_target", "手牌から1枚選択してください（スキル対象）" },
            { "fixed.skill_invalid_selection", "選択が不正です" },
            { "fixed.skill_apply_failed", "スキル適用に失敗しました" },
            { "fixed.skill_select_number_for_calligrapher", "数牌を1枚選択してください（書家の対象）" },
            { "fixed.selected_tag", "選択中" },
            { "fixed.equip_header_prefix", "装備中：" },
            { "fixed.equip_none", "なし" },
            { "fixed.equip_owned_empty", "-" },
            { "fixed.skill_exhausted", "スキルは使い切りました" },
            { "fixed.skill_unique_max_two_selection", "神器使用時は手牌の選択は2枚までです" },
            { "fixed.call_paralyzed_cannot_call", "麻痺中のため鳴きはできません" },
            { "fixed.call_riichi_cannot_call", "リーチ中は鳴けません" },
            { "fixed.call_no_target", "鳴きの対象がありません" },
            { "fixed.call_no_hand", "手牌がありません" },
            { "fixed.call_chi_select_two", "チーは2枚選択してください" },
            { "fixed.call_invalid_sequence", "選択が順子になっていません" },
            { "fixed.call_chi_complete_discard_one", "チー成立：一枚切ってください" },
            { "fixed.call_selection_insufficient", "選択が不足しています" },
            { "fixed.demo_end_message", "Demo版はここまでです。面白かったらぜひ製品版のウィッシュリスト登録・ご購入をご検討ください" },
            { "fixed.legendary_damage_half_ongoing", "和了直後の敵和了ダメージを半減（敵を倒したら消滅）" },
            { "fixed.legendary_half_mp_cost_ongoing", "次の局はMP消費量が半分（敵を倒したら消滅）" },
            { "fixed.tier_select_resume_info", "中断データがありますが、続きから開始しますか？最初から開始しますか？" },
            { "fixed.tier_dropdown_item_format", "Tier{0}  (Lv{1}〜Lv{2} / 倍率 {3:0.0}x)" },
            { "fixed.tier_selected_format", "選択中：Tier{0}  (Lv{1}〜Lv{2}, 倍率 {3:0.0}x)" },
            { "fixed.tier_debug_enemy_on", "テスト開始敵：ON（EnemyIndex {0}）" },
            { "fixed.tier_debug_enemy_off", "テスト開始敵：OFF（0番から開始）" },
        });
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.confirm_discard", "捨てる" },
    { "fixed.confirm_tsumo", "ツモ" },
    { "fixed.button_skip", "スキップ" },
    { "fixed.button_skill", "スキル" },
    { "fixed.status_replace_to_tenpai_or_riichi", "入替え→[テンパイ確定]（任意）→[リーチ] or [捨てる]" },
    { "fixed.call_can_select_enemy_discard", "敵の捨て牌への鳴き/ロンを選べます" },
    { "fixed.call_no_kan_candidate", "カンできる牌がありません" },
    { "fixed.ankan_rinshan_draw", "暗槓 → リンシャン牌をツモ" },
    { "fixed.kakan_rinshan_draw", "加槓 → リンシャン牌をツモ" },
{ "fixed.omote_dora_label", "表ドラ" },
{ "fixed.ura_dora_label", "裏ドラ" },
    { "fixed.skill_duplicated_select_discard", "複製しました。別の1枚を選んで[捨てる]を押してください" },
    { "fixed.skill_dora_plus_one", "ドラ表示牌+1" },
    { "fixed.skill_nullify_enemy_effect_once", "次の敵効果を無効化します" },
    { "fixed.skill_force_draw_selected_next_turn", "次ターンで {0} をツモに追加" },

    { "fixed.tenpai_riichi_available", "テンパイ！ リーチ可能です" },
    { "fixed.not_tenpai", "未テンパイ" },

    { "fixed.relic_effect_transform", "神器効果：{0} に変換" },
    { "fixed.relic_effect_cannot_activate_three_or_more_honors", "神器効果：最も多い字牌が3枚以上あるため発動できません" },

    { "fixed.select_one_hand_tile", "手牌から1枚選んでください" },

    { "fixed.shanten_riichi", "リーチ" },
    { "fixed.shanten_agari", "アガリ" },
    { "fixed.shanten_tenpai", "テンパイ" },
    { "fixed.shanten_suffix", "シャンテン" },

    { "fixed.turn_suffix", "ターン目" },
    { "fixed.turn_unit", "ターン" },

    { "fixed.seat_east", "東家" },
    { "fixed.seat_south", "南家" },
    { "fixed.seat_west", "西家" },
    { "fixed.seat_north", "北家" },

    { "fixed.rightinfo_no_skill_equipped", "スキル未装備" },
    { "fixed.rightinfo_target_geki", "<b>撃の該当役</b>：" },
    { "fixed.rightinfo_target_shun", "<b>瞬の該当役</b>：" },
    { "fixed.rightinfo_target_iyu", "<b>癒の該当役</b>：" },
    { "fixed.none_plain", "なし" },
{ "fixed.special_tile_dora_plus_one", "ドラ+1" },
{ "fixed.special_tile_owned_none", "所持：なし" },
{ "fixed.special_tile_owned_header", "所持：" },
{ "fixed.special_tile_equipped_slots_format", "装備枠 {0}/{1}" },
{ "fixed.special_tile_equipped_none", "（なし）" },
{ "fixed.special_tile_legendary_effect_1", "和了時：表ドラ・裏ドラを追加で1枚ずつ開く（プレイヤーのみ）" },
{ "fixed.special_tile_legendary_effect_2", "和了直後の敵和了ダメージを半減（敵を倒したら消滅）" },
{ "fixed.special_tile_legendary_effect_3", "その和了の獲得GOLDが2倍" },
{ "fixed.special_tile_legendary_effect_4", "満貫未満なら撃・瞬・癒が2倍" },
{ "fixed.special_tile_legendary_effect_5", "次の局はMP消費量が半分" },
{ "fixed.special_tile_legendary_effect_6", "和了時：符+16" },
{ "fixed.special_tile_legendary_effect_unknown", "特殊効果" },
    { "fixed.ofuda_owned", "お札所持中" },
    { "fixed.ofuda_none", "お札なし" },

    { "fixed.active_skill_fallback", "スキル" }
});
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.enemy_discard_call_or_ron_available", "敵の捨て牌への鳴き/ロンを選べます" },
    { "fixed.enemy_hand_title", "敵の手牌" },
    { "fixed.win_tsumo", "ツモ" },
    { "fixed.win_ron", "ロン" },
    { "fixed.enemy_win_body_line1", "敵の和了！" },
    { "fixed.enemy_win_body_score_prefix", "点数: " },
    { "fixed.enemy_win_body_hp_damage_prefix", "HPダメージ: " },
    { "fixed.score_label", "SCORE" },
    { "fixed.ryukyoku", "流局" },
    { "fixed.select_tsumo_tile", "ツモ和了する牌をクリックして選択してください" },
    { "fixed.placeholder_dash", "ー" },
    { "fixed.enemy_generic_name", "敵" }
});
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.yaku.riichi_short", "リーチ" },
    { "fixed.yaku.double_riichi_short", "ダブル立直" },
    { "fixed.yaku.riichi", "リーチ" },
    { "fixed.yaku.double_riichi", "ダブル立直" },
    { "fixed.yaku.ippatsu_short", "一発" },
    { "fixed.yaku.ippatsu", "一発" },
    { "fixed.fu_prefix", "符: " },
    { "fixed.yaku_none", "役なし" },
    { "fixed.yaku_label_prefix", "役: " },
    { "fixed.han_fu_label", "翻・符" },
    { "fixed.han_suffix", "翻" },
    { "fixed.fu_suffix", "符" },
    { "fixed.dora_count_format", "ドラ×{0}" },
    { "fixed.special_tile_dora_count_format", "特別牌ドラ×{0}" },
    { "fixed.ura_dora_count_format", "裏ドラ×{0}" },
    { "fixed.dora_label_prefix", "ドラ: " }
});
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.HP", "HP" },
    { "fixed.初回撃破報酬（宝石×1）", "初回撃破報酬（宝石×1）" },
    { "fixed.取得済み", "取得済み" },
    { "fixed.未取得", "未取得" },
    { "fixed.スキル", "スキル" },
    { "fixed.なし", "なし" },
    { "fixed.{0}　プレイヤーHPに{1}ダメージ", "{0}　プレイヤーHPに{1}ダメージ" },
    { "fixed.{0}　{1}ターンスキル使用不可", "{0}　{1}ターンスキル使用不可" },
    { "fixed.{0}　{1}ターン毎ターン{2}ダメージ", "{0}　{1}ターン毎ターン{2}ダメージ" },
    { "fixed.{0}　次の和了ダメージ +{1}%", "{0}　次の和了ダメージ +{1}%" },
    { "fixed.{0}　次のプレイヤー和了ダメージ {1}%減少", "{0}　次のプレイヤー和了ダメージ {1}%減少" },
    { "fixed.{0}　プレイヤーMPを{1}減少", "{0}　プレイヤーMPを{1}減少" },
    { "fixed.{0}　手牌を{1}枚入れ替え", "{0}　手牌を{1}枚入れ替え" },
    { "yaku.ippatsu", "一発(+1)" },
    { "yaku.dora_count", "ドラ×{0}" },
    { "yaku.red_dora_count", "赤ドラ×{0}" },
    { "yaku.ura_dora_count", "裏ドラ×{0}" },
    { "yaku.legendary_fu_bonus", "レジェンダリー効果：符+{0}" },

    { "fixed.yaku.dora_count", "ドラ×{0}" },
    { "fixed.yaku.red_dora_count", "赤ドラ×{0}" },
    { "fixed.yaku.ura_dora_count", "裏ドラ×{0}" }
});
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.ofuda_owned", "お札所持中" },
    { "fixed.ofuda_none", "お札なし" },

    { "fixed.ofuda_empty_slot", "ー" },
    { "fixed.ofuda_capacity_format", "{0}/{1}" },

    { "fixed.active_skill_fallback", "スキル" }
});
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.round_wind_east", "東" },
    { "fixed.round_wind_south", "南" },
    { "fixed.round_label_format", "{0}{1}局" },
    { "fixed.round_suffix", "局" }
});
AddAll(_ja, new Dictionary<string, string>
{
    { "fixed.angel_speaker_name", "天使" },

    { "fixed.angel_secret_hades_intro_1", "その力でゼウスを倒したのなら冥府の王も黙ってはいません" },
    { "fixed.angel_secret_hades_intro_2", "・・・来ます" },
    { "fixed.angel_secret_hades_intro_3", "覚悟を決めて進んでください。" },

    { "fixed.angel_secret_hades_clear_1", "本当にやったのね・・・" },
    { "fixed.angel_secret_hades_clear_2", "冥府の王をも打ち破った今、あなたは神域に到達した" },
    { "fixed.angel_secret_hades_clear_3", "これは祝福ではなく証です　神器を授けます" },

    { "fixed.angel_defeat_1", "……残念。今回はここまでです。" },
    { "fixed.angel_defeat_2", "ですが、あなたの挑戦は無駄にはなりません。" },
    { "fixed.angel_defeat_3", "報酬を受け取り、次の試練に備えなさい。" },

    { "fixed.angel_clear_1", "おめでとう。試練を乗り越えました。" },
    { "fixed.angel_clear_2", "あなたの勝利は、確かな力となって残ります。" },
    { "fixed.angel_clear_3", "報酬を受け取り、次なる道へ進みなさい。" },

    { "fixed.angel_start_1", "ようこそ。これからあなたは神々との試練に挑みます。" },
    { "fixed.angel_start_enemy_1", "最初の相手は「{0}」。心して向かいなさい。" },
    { "specialtile.dora_plus_1", "ドラ+1" },
{ "specialtile.legendary_fx_1", "和了時：表ドラ・裏ドラを追加で1枚ずつ開く（プレイヤーのみ）" },
{ "specialtile.legendary_fx_2", "和了直後の敵和了ダメージを半減（敵を倒したら消滅）" },
{ "specialtile.legendary_fx_3", "その和了の獲得GOLDが2倍" },
{ "specialtile.legendary_fx_4", "満貫未満の和了なら撃・瞬・癒が2倍" },
{ "specialtile.legendary_fx_5", "次の局はMP消費量が半分（敵を倒したら消滅）" },
{ "specialtile.legendary_fx_6", "和了時 +16符" }
});
    }

    private void BuildEnglishTable()
    {
        _en.Clear();

        AddAll(_en, new Dictionary<string, string>
        {
            { "fixed.ok", "OK" },
            { "fixed.reward_title", "Reward" },
            { "fixed.reward_none", "No Reward" },
            { "fixed.reward_unknown_name", "???" },
            { "fixed.owned_none", "You do not own any Omamori." },
            { "fixed.over_cap_hint", "You are over the Omamori limit. Please discard one." },
{ "fixed.final_score_prefix", "Final Score: " },
{ "fixed.gem_gain_prefix", "Gained " },
{ "fixed.gem_gain_middle", " Gems!" },
{ "fixed.gem_gain_suffix", " Gems" },
{ "fixed.enemy_defeat_suffix", " Defeated" },
{ "fixed.enemy_defeat_suffix_emphatic", " Defeated!" },
            { "fixed.unique_title_error", "<color=#FF0000>Unique Omamori</color>" },
            { "fixed.unique_desc_error_line1", "<color=#FF0000>Failed to grant the unique Omamori.</color>" },
            { "fixed.unique_desc_error_line2", "<color=#FF0000>(Invalid extra reward ID after defeating Hades)</color>" },
{ "fixed.han_only_format", "{0} Han" },
{ "fixed.han_fu_format", "{0} Han {1} Fu" },
{ "fixed.base_point_label", "Base Points" },
{ "fixed.damage_to_enemy_format", "Damage to {0}  {1}" },
            { "fixed.suit_man", "Man" },
            { "fixed.suit_pin", "Pin" },
            { "fixed.suit_sou", "Sou" },
            { "fixed.suit_honor", "Honor" },
{ "fixed.trait_load_failed", "Target Yaku: Load Failed" },
{ "fixed.trait_upgrade_none", "Upgrade: No Candidates" },
{ "fixed.trait_upgrade_level_line_format", "Lv{0} {1} -> Lv{2} {3}" },
{ "fixed.trait_upgrade_cost_line_format", "  {0}" },
{ "fixed.deck_empty_cost", "-" },
{ "fixed.player_victory", "Victory" },
{ "fixed.player_defeat", "Defeat" },
{ "fixed.trait_shop_title", "Yaku Upgrade" },
{ "fixed.trait_unlock_label", "Unlock" },
{ "fixed.trait_upgrade_label", "Upgrade" },
{ "fixed.trait_unlock_button", "Unlock" },
{ "fixed.trait_upgrade_button", "Upgrade" },
            { "fixed.hp_up_prefix", "HP +" },
            { "fixed.mp_up_prefix", "MP +" },
            { "fixed.cast_up_prefix", "Turn Limit +" },
            { "fixed.heal_hp_prefix", "HP Heal +" },
            { "fixed.heal_mp_prefix", "MP Heal +" },
            { "fixed.label_separator", "  /  　 " },
            { "fixed.total_bonus_prefix", " (Total +" },
            { "fixed.total_bonus_suffix", ")" },

            { "fixed.poison_tick_prefix", "Poison Damage " },
            { "fixed.poison_tick_middle", " (Remaining " },
            { "fixed.poison_tick_suffix", " turns)" },
            { "fixed.paralysis_recovered", "Paralysis Wore Off" },

            { "fixed.anger_status_prefix", "Enemy Enraged! " },
            { "fixed.anger_status_middle", " turns, enemy win damage +" },
            { "fixed.anger_status_suffix", "%" },

            { "fixed.poison_status_prefix", "Poison! " },
            { "fixed.poison_status_middle", " turns, " },
            { "fixed.poison_status_suffix", " damage each turn" },

            { "fixed.paralysis_status_prefix", "Paralysis! " },
            { "fixed.paralysis_status_suffix", " turns, skills and calls are sealed" },

            { "fixed.skill_quoted_prefix", "Enemy Skill \"" },
            { "fixed.skill_quoted_middle", "\"! " },
            { "fixed.attack_status_suffix", " damage" },
            { "fixed.disturb_status_prefix", "Lost " },
            { "fixed.disturb_status_suffix", " MP" },
            { "fixed.trick_status_suffix", "Part of your hand was swapped" },
            { "fixed.trick_done_suffix", "Your hand was rewritten" },

            { "fixed.defense_status_prefix", "Enemy Skill \"" },
            { "fixed.defense_status_middle", "\"! " },
            { "fixed.defense_status_turn_middle", " turns, player win damage reduced by " },
            { "fixed.defense_status_suffix", "%" },

            { "fixed.countdown_every_turn_suffix", ": Activates every turn (Z<=1)" },
            { "fixed.countdown_remain_prefix", ": " },
            { "fixed.countdown_remain_middle", " turns left (Z=" },
            { "achievement.yakuman_win", "Win a yakuman" },
{ "achievement.kokushi", "Win with Kokushi Musou" },
{ "achievement.suuankou", "Win with Suuankou" },
{ "achievement.daisangen", "Win with Daisangen" },
{ "achievement.tsuuiisou", "Win with Tsuuiisou" },
{ "achievement.ryuuiisou", "Win with Ryuuiisou" },
{ "achievement.shousuushii", "Win with Shousuushii" },
{ "achievement.daisuushii", "Win with Daisuushii" },
{ "achievement.chuuren", "Win with Chuuren Poutou" },
{ "achievement.chinroutou", "Win with Chinroutou" },
{ "achievement.suukantsu", "Win with Suukantsu" },
{ "achievement.chihou", "Win with Chiihou" },
{ "achievement.tenhou", "Win with Tenhou" },
{ "fixed.han_limit_format", "{0} Han {1}" },
{ "fixed.limit_mangan", "Mangan" },
{ "fixed.limit_haneman", "Haneman" },
{ "fixed.limit_baiman", "Baiman" },
{ "fixed.limit_sanbaiman", "Sanbaiman" },
{ "fixed.limit_yakuman", "Yakuman" },
{ "fixed.limit_double_yakuman", "Double Yakuman" },
{ "fixed.limit_triple_yakuman", "Triple Yakuman" },
{ "fixed.limit_quadruple_yakuman", "Quadruple Yakuman" },
{ "fixed.limit_quintuple_yakuman", "Quintuple Yakuman" },
{ "fixed.limit_multi_yakuman_format", "{0}x Yakuman" },
{ "achievement.score_100k", "Reach 100,000 score" },
{ "achievement.score_200k", "Reach 200,000 score" },
{ "achievement.score_500k", "Reach 500,000 score" },
{ "achievement.score_800k", "Reach 800,000 score" },
{ "achievement.score_1000k", "Reach 1,000,000 score" },
    { "active_skill_action.RandomMan", "Chromaflow" },
    { "active_skill_action.EnhanceHand", "Inkscript" },
    { "active_skill_action.Capitalist", "Market Twist" },
{ "achievement.tier1_clear", "Clear Tier 1" },
{ "achievement.tier2_clear", "Clear Tier 2" },
{ "achievement.tier3_clear", "Clear Tier 3" },
{ "achievement.tier4_clear", "Clear Tier 4" },
{ "achievement.tier5_clear", "Clear Tier 5" },
{ "fixed.omote_dora_label", "Dora" },
{ "fixed.ura_dora_label", "Ura Dora" },
{ "achievement.dyemaster_tier1_clear", "Clear Tier 1 with Dye Master" },
{ "achievement.calligrapher_tier1_clear", "Clear Tier 1 with Calligrapher" },
{ "achievement.capitalist_tier1_clear", "Clear Tier 1 with Capitalist" },

{ "achievement.legendary_omamori", "Obtain a legendary omamori" },
{ "achievement.legendary_special_tile", "Obtain a legendary special tile" },
{ "achievement.shinki_get", "Obtain a divine artifact" },

{ "achievement.hades_defeat", "Defeat Hades" },
{ "achievement.hades_defeat_hidden", "Defeat ???" },

{ "achievement.reward_gems", "Gained {0} gems" },
{ "achievement.reward_none", "No reward" },
            { "fixed.countdown_remain_suffix", ")" }
        });
        AddAll(_en, new Dictionary<string, string>
{
    { "enemy_name.アマテラス", "Amaterasu" },
    { "enemy_name.スサノオ", "Susanoo" },
    { "enemy_name.バステト", "Bastet" },
    { "enemy_name.シヴァ", "Shiva" },
    { "enemy_name.アヌビス", "Anubis" },
    { "enemy_name.フレイヤ", "Freyja" },
    { "enemy_name.ポセイドン", "Poseidon" },
    { "enemy_name.オーディン", "Odin" },
    { "enemy_name.ルーナ", "Luna" },
    { "enemy_name.ゼウス", "Zeus" },
    { "enemy_name.ハデス", "Hades" }
});
AddAll(_en, new Dictionary<string, string>
{
    { "fixed.angel_speaker_name", "Angel" },

    { "fixed.angel_secret_hades_intro_1", "If you have defeated Zeus with that power, the King of the Underworld will not remain silent." },
    { "fixed.angel_secret_hades_intro_2", "...He comes." },
    { "fixed.angel_secret_hades_intro_3", "Steel yourself and move forward." },

    { "fixed.angel_secret_hades_clear_1", "You really did it..." },
    { "fixed.angel_secret_hades_clear_2", "Now that you have even defeated the King of the Underworld, you have reached the realm of the gods." },
    { "fixed.angel_secret_hades_clear_3", "This is not a blessing, but proof. I grant you a divine relic." },
{ "fixed.special_tile_dora_plus_one", "Dora +1" },
{ "fixed.special_tile_owned_none", "Owned: None" },
{ "fixed.special_tile_owned_header", "Owned:" },
{ "fixed.special_tile_equipped_slots_format", "Equipped {0}/{1}" },
{ "fixed.special_tile_equipped_none", "(None)" },
{ "fixed.special_tile_legendary_effect_1", "On Agari: reveal 1 extra Dora and 1 extra Ura Dora (player only)" },
{ "fixed.special_tile_legendary_effect_2", "Halves the next enemy Agari damage until that enemy is defeated" },
{ "fixed.special_tile_legendary_effect_3", "Gold earned from that Agari is doubled" },
{ "fixed.special_tile_legendary_effect_4", "If below Mangan, Geki/Shun/Iyu effects are doubled" },
{ "fixed.special_tile_legendary_effect_5", "MP cost is halved for the next hand" },
{ "fixed.special_tile_legendary_effect_6", "On Agari: +16 Fu" },
{ "fixed.special_tile_legendary_effect_unknown", "Special Effect" },
    { "fixed.angel_defeat_1", "...What a pity. This is where this attempt ends." },
    { "fixed.angel_defeat_2", "But your challenge has not been in vain." },
    { "fixed.angel_defeat_3", "Take your reward, and prepare for the next trial." },

    { "fixed.angel_clear_1", "Congratulations. You have overcome the trial." },
    { "fixed.angel_clear_2", "Your victory will remain as true strength." },
    { "fixed.angel_clear_3", "Take your reward, and proceed to the next path." },
    { "fixed.angel_start_1", "Welcome. From here, you will face the trials of the gods." },
    { "fixed.angel_start_enemy_1", "Your first opponent is \"{0}\". Face them with resolve." },
    { "yaku.ippatsu", "Ippatsu(+1)" },
    { "yaku.dora_count", "Dora x{0}" },
    { "yaku.red_dora_count", "Red Dora x{0}" },
    { "yaku.ura_dora_count", "Ura Dora x{0}" },
    { "yaku.legendary_fu_bonus", "Legendary Effect: Fu +{0}" },
    { "specialtile.dora_plus_1", "Dora +1" },
    { "specialtile.legendary_fx_1", "On win: reveal 1 extra Dora and 1 extra Ura-Dora (player only)" },
    { "specialtile.legendary_fx_2", "Halve the next enemy win damage after your win (removed when the enemy is defeated)" },
    { "specialtile.legendary_fx_3", "Gold gained from that win is doubled" },
    { "specialtile.legendary_fx_4", "If the win is below Mangan, Geki/Shun/Iyu are doubled" },
    { "specialtile.legendary_fx_5", "MP cost is halved for the next hand (removed when the enemy is defeated)" },
    { "specialtile.legendary_fx_6", "On win: +16 Fu" }
});
        AddAll(_en, new Dictionary<string, string>
        {
            { "rarity.Normal", "Normal" },
            { "rarity.Common", "Common" },
            { "rarity.Rare", "Rare" },
            { "rarity.Epic", "Epic" },
            { "rarity.Legendary", "Legendary" }
        });
AddAll(_en, new Dictionary<string, string>
{
    { "active_skill.RandomMan", "Dyer" },
    { "active_skill.EnhanceHand", "Calligrapher" },
    { "active_skill.Capitalist", "Capitalist" }
});
        AddAll(_en, new Dictionary<string, string>
        {
            { "active_skill_desc.RandomMan", "Transform the selected tile into a random tile of the most common suit in your hand (excluding the selected tile). If tied, priority is Man > Pin > Sou." },
            { "active_skill_desc.EnhanceHand", "Transform the selected tile into a 5 of the same suit." }
        });

        AddAll(_en, new Dictionary<string, string>
        {
            { "enemy_skill.anger", "Anger" },
            { "enemy_skill.poison", "Poison" },
            { "enemy_skill.paralysis", "Paralysis" },
            { "enemy_skill.attack", "Attack" },
            { "enemy_skill.defense", "Defense" },
            { "enemy_skill.disturb", "Disturb" },
            { "enemy_skill.trick", "Trick" }
        });
        AddAll(_en, new Dictionary<string, string>
        {
            { "yaku.KOKUSHI", "Kokushi Musou" },
            { "yaku.CHIITOITSU", "Chiitoitsu" },
            { "yaku.MENZEN_TSUMO", "Menzen Tsumo" },
            { "yaku.TANYAO", "Tanyao" },
            { "yaku.PINFU", "Pinfu" },
            { "yaku.YAKUHAI", "Yakuhai" },
            { "yaku.IIPEIKOU", "Iipeikou" },
            { "yaku.RYANPEIKOU", "Ryanpeikou" },
            { "yaku.SANSHOKU_DOUJUN", "Sanshoku Doujun" },
            { "yaku.ITTSU", "Ittsu" },
            { "yaku.CHANTA", "Chanta" },
            { "yaku.JUNCHAN", "Junchan" },
            { "yaku.TOITOI", "Toitoi" },
            { "yaku.SANANKOU", "Sanankou" },
            { "yaku.SANKANTSU", "Sankantsu" },
            { "yaku.SANSHOKU_DOUKOU", "Sanshoku Doukou" },
            { "yaku.SHOUSANGEN", "Shousangen" },
            { "yaku.HONROUTOU", "Honroutou" },
            { "yaku.HONITSU", "Honitsu" },
            { "yaku.CHINITSU", "Chinitsu" }
        });
        AddAll(_en, new Dictionary<string, string>
        {
            { "yakuman.CHUUREN_POUTOU", "Chuuren Poutou" },
            { "yakuman.KOKUSHI", "Kokushi Musou" },
            { "yakuman.DAISANGEN", "Daisangen" },
            { "yakuman.DAISUUSHI", "Daisuushi" },
            { "yakuman.SHOUSUUSHI", "Shousuushi" },
            { "yakuman.TSUUIISOU", "Tsuuiisou" },
            { "yakuman.CHINROUTOU", "Chinroutou" },
            { "yakuman.RYUUIISOU", "Ryuuiisou" },
            { "yakuman.SUUANKOU", "Suuankou" },
            { "yakuman.SUUKANTSU", "Suukantsu" },
            { "yakuman.TENHOU", "Tenhou" },
            { "yakuman.CHIHOU", "Chiihou" },
            { "yakuman.RENHOU", "Renhou" }
        });
        AddAll(_en, new Dictionary<string, string>
        {
            { "fixed.trait_geki", "Geki" },
            { "fixed.trait_shun", "Shun" },
            { "fixed.trait_iyu", "Iyu" },
            { "fixed.none", "None" },
            { "fixed.trait_hp_suffix", " HP" },
            { "fixed.omamori_trait_shun_prefix", "Omamori (Shun) +" },
            { "fixed.omamori_trait_iyu_prefix", "Omamori (Iyu) +" },
            { "fixed.omamori_trait_geki_prefix", "Omamori (Geki) +" },
            { "fixed.percent_suffix", "%" },
            { "fixed.recommended_damage_prefix", "Recommended Damage: " },
            { "fixed.recommended_recover_prefix", " / Recover: " },
        });
        AddAll(_en, new Dictionary<string, string>
        {
            { "fixed.skill_paralyzed_cannot_use", "Cannot use skills while paralyzed" },
            { "fixed.skill_not_equipped", "No skill equipped" },
            { "fixed.skill_turn_limit_reached", "Skill use limit reached this turn" },
            { "fixed.skill_not_enough_mp", "Not enough MP" },
            { "fixed.skill_activation_failed_invalid_target", "Skill activation failed (please select a valid target tile)" },
            { "fixed.skill_activated", "Skill Activated" },
            { "fixed.skill_select_hand_target", "Please select 1 tile from your hand (skill target)" },
            { "fixed.skill_invalid_selection", "Invalid selection" },
            { "fixed.skill_apply_failed", "Failed to apply skill" },
            { "fixed.skill_select_number_for_calligrapher", "Please select 1 suited tile (Calligrapher target)" },
            { "fixed.selected_tag", "Selected" },
            { "fixed.equip_header_prefix", "Equipped: " },
            { "fixed.equip_none", "None" },
            { "fixed.equip_owned_empty", "-" },
            { "fixed.skill_exhausted", "Skill charges are exhausted" },
            { "fixed.skill_unique_max_two_selection", "When using the relic effect, you may select up to 2 hand tiles" },
            { "fixed.call_paralyzed_cannot_call", "Cannot call while paralyzed" },
            { "fixed.call_riichi_cannot_call", "Cannot call while in Riichi" },
            { "fixed.call_no_target", "No tile available to call" },
            { "fixed.call_no_hand", "No tiles in hand" },
            { "fixed.call_chi_select_two", "Select 2 tiles for Chi" },
            { "fixed.call_invalid_sequence", "Selected tiles do not form a sequence" },
            { "fixed.call_chi_complete_discard_one", "Chi complete: discard 1 tile" },
            { "fixed.call_selection_insufficient", "Selection is incomplete" },
            { "fixed.demo_end_message", "This is the end of the demo. If you enjoyed it, please consider wishlisting or purchasing the full version." },
            { "fixed.legendary_damage_half_ongoing", "The next enemy win damage immediately after a win is halved (disappears after defeating the enemy)" },
            { "fixed.legendary_half_mp_cost_ongoing", "MP cost is halved for the next round (disappears after defeating the enemy)" },
            { "fixed.tier_select_resume_info", "Suspended data exists. Start from where you left off, or start from the beginning?" },
            { "fixed.tier_dropdown_item_format", "Tier{0}  (Lv{1}-Lv{2} / Multiplier {3:0.0}x)" },
            { "fixed.tier_selected_format", "Selected: Tier{0}  (Lv{1}-Lv{2}, Multiplier {3:0.0}x)" },
            { "fixed.tier_debug_enemy_on", "Test Start Enemy: ON (EnemyIndex {0})" },
            { "fixed.tier_debug_enemy_off", "Test Start Enemy: OFF (Start from index 0)" },
        });
        AddAll(_en, new Dictionary<string, string>
{
    { "fixed.round_wind_east", "East" },
    { "fixed.round_wind_south", "South" },
    { "fixed.round_label_format", "{0} {1}" },
    { "fixed.round_suffix", "" }
});
AddAll(_en, new Dictionary<string, string>
{
    { "fixed.confirm_discard", "Discard" },
    { "fixed.confirm_tsumo", "Tsumo" },
    { "fixed.button_skip", "Skip" },
    { "fixed.button_skill", "Skill" },
    { "fixed.status_replace_to_tenpai_or_riichi", "Replace -> [Confirm Tenpai] (optional) -> [Riichi] or [Discard]" },
    { "fixed.call_can_select_enemy_discard", "You can choose a call/Ron on the enemy discard" },
    { "fixed.call_no_kan_candidate", "There are no tiles you can Kan" },
    { "fixed.ankan_rinshan_draw", "Concealed Kan -> Draw a Rinshan tile" },
    { "fixed.kakan_rinshan_draw", "Added Kan -> Draw a Rinshan tile" },

    { "fixed.skill_duplicated_select_discard", "Duplicated. Select another tile and press [Discard]" },
    { "fixed.skill_dora_plus_one", "Dora indicator +1" },
    { "fixed.skill_nullify_enemy_effect_once", "The next enemy effect will be nullified" },
    { "fixed.skill_force_draw_selected_next_turn", "Add {0} to next turn's draw" },

    { "fixed.tenpai_riichi_available", "Tenpai! Riichi is available" },
    { "fixed.not_tenpai", "Not in Tenpai" },

    { "fixed.relic_effect_transform", "Relic effect: transformed into {0}" },
    { "fixed.relic_effect_cannot_activate_three_or_more_honors", "Relic effect: cannot activate because the most common honor tile count is 3 or more" },

    { "fixed.select_one_hand_tile", "Please select 1 tile from your hand" },

    { "fixed.shanten_riichi", "Riichi" },
    { "fixed.shanten_agari", "Agari" },
    { "fixed.shanten_tenpai", "Tenpai" },
    { "fixed.shanten_suffix", " Shanten" },

    { "fixed.turn_suffix", " turn" },
    { "fixed.turn_unit", "Turn" },

    { "fixed.seat_east", "East" },
    { "fixed.seat_south", "South" },
    { "fixed.seat_west", "West" },
    { "fixed.seat_north", "North" },

    { "fixed.rightinfo_no_skill_equipped", "No skill equipped" },
    { "fixed.rightinfo_target_geki", "<b>Geki Target Yaku</b>: " },
    { "fixed.rightinfo_target_shun", "<b>Shun Target Yaku</b>: " },
    { "fixed.rightinfo_target_iyu", "<b>Iyu Target Yaku</b>: " },
    { "fixed.none_plain", "None" },

    { "fixed.ofuda_owned", "Ofuda equipped" },
    { "fixed.ofuda_none", "No Ofuda" },

    { "fixed.active_skill_fallback", "Skill" }
});
AddAll(_en, new Dictionary<string, string>
{
    { "fixed.enemy_discard_call_or_ron_available", "You can choose a call/Ron on the enemy discard" },
    { "fixed.enemy_hand_title", "Enemy Hand" },
    { "fixed.win_tsumo", "Tsumo" },
    { "fixed.win_ron", "Ron" },
    { "fixed.enemy_win_body_line1", "Enemy wins!" },
    { "fixed.enemy_win_body_score_prefix", "Score: " },
    { "fixed.enemy_win_body_hp_damage_prefix", "HP Damage: " },
    { "fixed.score_label", "SCORE" },
    { "fixed.ryukyoku", "Draw" },
    { "fixed.select_tsumo_tile", "Please click the tile to win by Tsumo" },
    { "fixed.placeholder_dash", "-" },
    { "fixed.enemy_generic_name", "Enemy" }
});
AddAll(_en, new Dictionary<string, string>
{
    { "fixed.yaku.riichi_short", "Riichi" },
    { "fixed.yaku.double_riichi_short", "Double Riichi" },
    { "fixed.yaku.ippatsu_short", "Ippatsu" },
    { "fixed.yaku.riichi", "Riichi" },
    { "fixed.yaku.double_riichi", "Double Riichi" },
    { "fixed.yaku.ippatsu", "Ippatsu" },
    { "fixed.fu_prefix", "Fu: " },
    { "fixed.yaku_none", "No Yaku" },
    { "fixed.yaku_label_prefix", "Yaku: " },
    { "fixed.han_fu_label", "Han / Fu" },
    { "fixed.han_suffix", " Han" },
    { "fixed.fu_suffix", " Fu" },
    { "fixed.dora_count_format", "Dora x{0}" },
    { "fixed.special_tile_dora_count_format", "Special Tile Dora x{0}" },
    { "fixed.ura_dora_count_format", "Ura-Dora x{0}" },
    { "fixed.dora_label_prefix", "Dora: " }
});
AddAll(_en, new Dictionary<string, string>
{
    { "fixed.rightinfo_no_skill_equipped", "No skill equipped" },
    { "fixed.rightinfo_target_geki", "<b>Geki Target Yaku</b>: " },
    { "fixed.rightinfo_target_shun", "<b>Shun Target Yaku</b>: " },
    { "fixed.rightinfo_target_iyu", "<b>Iyu Target Yaku</b>: " },
    { "fixed.none_plain", "None" },

    { "fixed.ofuda_owned", "Ofuda equipped" },
    { "fixed.ofuda_none", "No Ofuda" },
    { "fixed.ofuda_empty_slot", "None" },
    { "fixed.ofuda_capacity_format", "Owned {0}/{1}" },
{ "fixed.yaku.dora_count", "Dora x{0}" },
{ "fixed.yaku.red_dora_count", "Red Dora x{0}" },
{ "fixed.yaku.ura_dora_count", "Ura Dora x{0}" },
    { "fixed.active_skill_fallback", "Skill" }
});
AddAll(_en, new Dictionary<string, string>
{
    { "fixed.HP", "HP" },
    { "fixed.初回撃破報酬（宝石×1）", "First Defeat Reward (Gem x1)" },
    { "fixed.取得済み", "Claimed" },
    { "fixed.未取得", "Not Claimed" },
    { "fixed.スキル", "Skills" },
    { "fixed.なし", "None" },

    { "fixed.{0}　プレイヤーHPに{1}ダメージ", "{0}  Deal {1} damage to player HP" },
    { "fixed.{0}　{1}ターンスキル使用不可", "{0}  Skills disabled for {1} turns" },
    { "fixed.{0}　{1}ターン毎ターン{2}ダメージ", "{0}  Deal {2} damage each turn for {1} turns" },
    { "fixed.{0}　次の和了ダメージ +{1}%", "{0}  Next win damage +{1}%" },
    { "fixed.{0}　次のプレイヤー和了ダメージ {1}%減少", "{0}  Reduce the player's next win damage by {1}%" },
    { "fixed.{0}　プレイヤーMPを{1}減少", "{0}  Reduce player MP by {1}" },

    { "fixed.{0}　手牌を{1}枚入れ替え", "{0}  Replace {1} tiles in hand" }
});
    }

    private void BuildChineseSimplifiedTable()
    {
        _zhHans.Clear();

        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "fixed.ok", "确定" },
            { "fixed.reward_title", "奖励" },
            { "fixed.reward_none", "无奖励" },
            { "fixed.reward_unknown_name", "？？？" },
            { "fixed.owned_none", "没有持有的御守。" },
            { "fixed.over_cap_hint", "持有的御守数量超过上限。请先丢弃。" },
            { "fixed.final_score_prefix", "最终分数：" },
            { "fixed.gem_gain_prefix", "获得" },
            { "fixed.gem_gain_suffix", "个宝石" },
            { "fixed.enemy_defeat_suffix", "击破" },
            { "fixed.enemy_defeat_suffix_emphatic", "击破！" },
            { "fixed.unique_title_error", "<color=#FF0000>神器御守</color>" },
            { "fixed.unique_desc_error_line1", "<color=#FF0000>神器御守发放失败。</color>" },
            { "fixed.unique_desc_error_line2", "<color=#FF0000>（击败哈迪斯后的额外奖励ID无效）</color>" },
{ "fixed.han_only_format", "{0}番" },
{ "fixed.han_fu_format", "{0}番　{1}符" },
{ "fixed.base_point_label", "基础点" },
{ "fixed.damage_to_enemy_format", "对{0}造成的伤害　{1}" },
            { "fixed.suit_man", "万" },
            { "fixed.suit_pin", "筒" },
            { "fixed.suit_sou", "索" },
            { "fixed.suit_honor", "字" },
{ "fixed.omote_dora_label", "表宝牌" },
{ "fixed.ura_dora_label", "里宝牌" },
{ "fixed.trait_load_failed", "对应役种：读取失败" },
{ "fixed.trait_upgrade_none", "强化：没有候选" },
{ "fixed.trait_upgrade_level_line_format", "Lv{0} {1}　→　Lv{2} {3}" },
{ "fixed.trait_upgrade_cost_line_format", "　{0}" },
{ "fixed.deck_empty_cost", "—" },
{ "fixed.player_victory", "胜利" },
{ "fixed.player_defeat", "败北" },
{ "fixed.trait_shop_title", "役种强化" },
{ "fixed.trait_unlock_label", "解锁" },
{ "fixed.trait_upgrade_label", "强化" },
{ "fixed.trait_unlock_button", "解锁" },
{ "fixed.trait_upgrade_button", "强化" },
            { "fixed.hp_up_prefix", "HP +" },
            { "fixed.mp_up_prefix", "MP +" },
            { "fixed.cast_up_prefix", "回合上限 +" },
            { "fixed.heal_hp_prefix", "HP 回复 +" },
            { "fixed.heal_mp_prefix", "MP 回复 +" },
            { "fixed.label_separator", "  /  　 " },
            { "fixed.total_bonus_prefix", "（累计 +" },
            { "fixed.total_bonus_suffix", "）" },
            { "fixed.poison_tick_prefix", "中毒伤害 " },
            { "fixed.poison_tick_middle", "（剩余" },
            { "fixed.poison_tick_suffix", "回合）" },
            { "fixed.paralysis_recovered", "麻痹解除" },

            { "fixed.anger_status_prefix", "敌人进入愤怒状态！ " },
            { "fixed.anger_status_middle", "回合内，敌人和牌伤害 +" },
            { "fixed.anger_status_suffix", "%" },

            { "fixed.poison_status_prefix", "中毒！ " },
            { "fixed.poison_status_middle", "回合内，每回合" },
            { "fixed.poison_status_suffix", "伤害" },

            { "fixed.paralysis_status_prefix", "麻痹！ " },
            { "fixed.paralysis_status_suffix", "回合内，技能和鸣牌被封锁" },

            { "fixed.skill_quoted_prefix", "敌方技能「" },
            { "fixed.skill_quoted_middle", "」！" },
            { "fixed.attack_status_suffix", "伤害" },
            { "fixed.disturb_status_prefix", "失去 " },
            { "fixed.disturb_status_suffix", " MP" },
            { "fixed.trick_status_suffix", "手牌的一部分被替换了" },
            { "fixed.trick_done_suffix", "手牌被改写了" },
    { "active_skill_action.RandomMan", "引色" },
    { "active_skill_action.EnhanceHand", "墨书" },
    { "active_skill_action.Capitalist", "控盘" },
            { "fixed.defense_status_prefix", "敌方技能「" },
            { "fixed.defense_status_middle", "」！" },
            { "fixed.defense_status_turn_middle", "回合内，玩家和牌伤害减少 " },
            { "fixed.defense_status_suffix", "%" },
    { "achievement.yakuman_win", "和出役满" },
{ "achievement.kokushi", "和出国士无双" },
{ "achievement.suuankou", "和出四暗刻" },
{ "achievement.daisangen", "和出大三元" },
{ "achievement.tsuuiisou", "和出字一色" },
{ "achievement.ryuuiisou", "和出绿一色" },
{ "achievement.shousuushii", "和出小四喜" },
{ "achievement.daisuushii", "和出大四喜" },
{ "achievement.chuuren", "和出九莲宝灯" },
{ "achievement.chinroutou", "和出清老头" },
{ "achievement.suukantsu", "和出四杠子" },
{ "achievement.chihou", "和出地和" },
{ "achievement.tenhou", "和出天和" },
{ "fixed.han_limit_format", "{0}番 {1}" },
{ "fixed.limit_mangan", "满贯" },
{ "fixed.limit_haneman", "跳满" },
{ "fixed.limit_baiman", "倍满" },
{ "fixed.limit_sanbaiman", "三倍满" },
{ "fixed.limit_yakuman", "役满" },
{ "fixed.limit_double_yakuman", "双倍役满" },
{ "fixed.limit_triple_yakuman", "三倍役满" },
{ "fixed.limit_quadruple_yakuman", "四倍役满" },
{ "fixed.limit_quintuple_yakuman", "五倍役满" },
{ "fixed.limit_multi_yakuman_format", "{0}倍役满" },
{ "achievement.score_100k", "达到10万分" },
{ "achievement.score_200k", "达到20万分" },
{ "achievement.score_500k", "达到50万分" },
{ "achievement.score_800k", "达到80万分" },
{ "achievement.score_1000k", "达到100万分" },
{ "achievement.tier1_clear", "通关Tier1" },
{ "achievement.tier2_clear", "通关Tier2" },
{ "achievement.tier3_clear", "通关Tier3" },
{ "achievement.tier4_clear", "通关Tier4" },
{ "achievement.tier5_clear", "通关Tier5" },

{ "achievement.dyemaster_tier1_clear", "使用染色师通关Tier1" },
{ "achievement.calligrapher_tier1_clear", "使用书家通关Tier1" },
{ "achievement.capitalist_tier1_clear", "使用资本家通关Tier1" },

{ "achievement.legendary_omamori", "获得传说御守" },
{ "achievement.legendary_special_tile", "获得传说特殊牌" },
{ "achievement.shinki_get", "获得神器" },
{ "achievement.hades_defeat", "击败哈迪斯" },
{ "achievement.hades_defeat_hidden", "击败？？？" },
{ "fixed.special_tile_dora_plus_one", "宝牌+1" },
{ "fixed.special_tile_owned_none", "持有：无" },
{ "fixed.special_tile_owned_header", "持有：" },
{ "fixed.special_tile_equipped_slots_format", "装备栏 {0}/{1}" },
{ "fixed.special_tile_equipped_none", "（无）" },
{ "fixed.special_tile_legendary_effect_1", "和牌时：额外翻开1张表宝牌和1张里宝牌（仅玩家）" },
{ "fixed.special_tile_legendary_effect_2", "使下一次敌方和牌伤害减半，直到击败该敌人为止" },
{ "fixed.special_tile_legendary_effect_3", "该次和牌获得的Gold变为2倍" },
{ "fixed.special_tile_legendary_effect_4", "若未达满贯，击／瞬／愈效果变为2倍" },
{ "fixed.special_tile_legendary_effect_5", "下一局MP消耗减半" },
{ "fixed.special_tile_legendary_effect_6", "和牌时：符+16" },
{ "fixed.special_tile_legendary_effect_unknown", "特殊效果" },
{ "achievement.reward_gems", "获得{0}个宝石" },
{ "achievement.reward_none", "没有奖励" },
            { "fixed.countdown_every_turn_suffix", "：每回合发动（Z<=1）" },
            { "fixed.countdown_remain_prefix", "：还剩" },
            { "fixed.countdown_remain_middle", "回合（Z=" },
            { "fixed.countdown_remain_suffix", "）" },
            { "yaku.ippatsu", "一发(+1)" },
{ "yaku.dora_count", "宝牌×{0}" },
{ "yaku.red_dora_count", "赤宝牌×{0}" },
{ "yaku.ura_dora_count", "里宝牌×{0}" },
{ "yaku.legendary_fu_bonus", "传说效果：符+{0}" },
{ "specialtile.dora_plus_1", "宝牌+1" },
{ "specialtile.legendary_fx_1", "和了时：额外翻开1张表宝牌和1张里宝牌（仅玩家）" },
{ "specialtile.legendary_fx_2", "和了后受到的下一次敌方和了伤害减半（击败敌人后消失）" },
{ "specialtile.legendary_fx_3", "该次和了获得的GOLD变为2倍" },
{ "specialtile.legendary_fx_4", "若和了未达满贯，则击／瞬／愈效果变为2倍" },
{ "specialtile.legendary_fx_5", "下一局MP消耗减半（击败敌人后消失）" },
{ "specialtile.legendary_fx_6", "和了时 +16符" }
        });
        AddAll(_zhHans, new Dictionary<string, string>
{
    { "enemy_name.アマテラス", "天照" },
    { "enemy_name.スサノオ", "须佐之男" },
    { "enemy_name.バステト", "芭丝特" },
    { "enemy_name.シヴァ", "湿婆" },
    { "enemy_name.アヌビス", "阿努比斯" },
    { "enemy_name.フレイヤ", "芙蕾雅" },
    { "enemy_name.ポセイドン", "波塞冬" },
    { "enemy_name.オーディン", "奥丁" },
    { "enemy_name.ルーナ", "露娜" },
    { "enemy_name.ゼウス", "宙斯" },
    { "enemy_name.ハデス", "哈迪斯" }
});
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.round_wind_east", "东" },
    { "fixed.round_wind_south", "南" },
    { "fixed.round_label_format", "{0}{1}局" },
    { "fixed.round_suffix", "局" }
});
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.angel_speaker_name", "天使" },

    { "fixed.angel_secret_hades_intro_1", "若你凭这股力量击败了宙斯，冥府之王也不会坐视不理。" },
    { "fixed.angel_secret_hades_intro_2", "……他要来了。" },
    { "fixed.angel_secret_hades_intro_3", "请下定决心，继续前进。" },

    { "fixed.angel_secret_hades_clear_1", "你竟然真的做到了……" },
    { "fixed.angel_secret_hades_clear_2", "如今连冥府之王都被你击败，你已经抵达神域。" },
    { "fixed.angel_secret_hades_clear_3", "这不是祝福，而是证明。我将赐予你神器。" },

    { "fixed.angel_defeat_1", "……很遗憾，这次就到这里了。" },
    { "fixed.angel_defeat_2", "但你的挑战并没有白费。" },
    { "fixed.angel_defeat_3", "领取奖励，为下一场试炼做好准备吧。" },

    { "fixed.angel_clear_1", "恭喜你。你已经跨越了这场试炼。" },
    { "fixed.angel_clear_2", "你的胜利将化为真正的力量保留下来。" },
    { "fixed.angel_clear_3", "领取奖励，继续踏上下一个征程吧。" },

    { "fixed.angel_start_1", "欢迎。接下来，你将迎接诸神的试炼。" },
    { "fixed.angel_start_enemy_1", "你的第一个对手是“{0}”。请做好觉悟，前去面对吧。" }
});
        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "rarity.Normal", "普通" },
            { "rarity.Common", "常见" },
            { "rarity.Rare", "稀有" },
            { "rarity.Epic", "史诗" },
            { "rarity.Legendary", "传说" }
        });
AddAll(_zhHans, new Dictionary<string, string>
{
    { "active_skill.RandomMan", "染色师" },
    { "active_skill.EnhanceHand", "书家" },
    { "active_skill.Capitalist", "资本家" }
});

        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "active_skill_desc.RandomMan", "将选中的牌变为你手牌中数量最多花色的随机牌（不包含选中的那张）。如果数量相同，则按 万 > 筒 > 索 的优先顺序。" },
            { "active_skill_desc.EnhanceHand", "将选中的牌变为同花色的5。" }
        });

        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "enemy_skill.anger", "愤怒" },
            { "enemy_skill.poison", "中毒" },
            { "enemy_skill.paralysis", "麻痹" },
            { "enemy_skill.attack", "攻击" },
            { "enemy_skill.defense", "防御" },
            { "enemy_skill.disturb", "妨害" },
            { "enemy_skill.trick", "细工" }
        });
        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "yaku.KOKUSHI", "国士无双" },
            { "yaku.CHIITOITSU", "七对子" },
            { "yaku.MENZEN_TSUMO", "门前清自摸和" },
            { "yaku.TANYAO", "断幺九" },
            { "yaku.PINFU", "平和" },
            { "yaku.YAKUHAI", "役牌" },
            { "yaku.IIPEIKOU", "一杯口" },
            { "yaku.RYANPEIKOU", "两杯口" },
            { "yaku.SANSHOKU_DOUJUN", "三色同顺" },
            { "yaku.ITTSU", "一气通贯" },
            { "yaku.CHANTA", "混全带幺九" },
            { "yaku.JUNCHAN", "纯全带幺九" },
            { "yaku.TOITOI", "对对和" },
            { "yaku.SANANKOU", "三暗刻" },
            { "yaku.SANKANTSU", "三杠子" },
            { "yaku.SANSHOKU_DOUKOU", "三色同刻" },
            { "yaku.SHOUSANGEN", "小三元" },
            { "yaku.HONROUTOU", "混老头" },
            { "yaku.HONITSU", "混一色" },
            { "yaku.CHINITSU", "清一色" }
        });

        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "yakuman.CHUUREN_POUTOU", "九莲宝灯" },
            { "yakuman.KOKUSHI", "国士无双" },
            { "yakuman.DAISANGEN", "大三元" },
            { "yakuman.DAISUUSHI", "大四喜" },
            { "yakuman.SHOUSUUSHI", "小四喜" },
            { "yakuman.TSUUIISOU", "字一色" },
            { "yakuman.CHINROUTOU", "清老头" },
            { "yakuman.RYUUIISOU", "绿一色" },
            { "yakuman.SUUANKOU", "四暗刻" },
            { "yakuman.SUUKANTSU", "四杠子" },
            { "yakuman.TENHOU", "天和" },
            { "yakuman.CHIHOU", "地和" },
            { "yakuman.RENHOU", "人和" }
        });
        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "fixed.trait_geki", "击" },
            { "fixed.trait_shun", "瞬" },
            { "fixed.trait_iyu", "愈" },
            { "fixed.none", "无" },
        });
        AddAll(_zhHans, new Dictionary<string, string>
        {
            { "fixed.skill_paralyzed_cannot_use", "麻痹状态下无法使用技能" },
            { "fixed.skill_not_equipped", "未装备技能" },
            { "fixed.skill_turn_limit_reached", "本回合技能使用次数已达上限" },
            { "fixed.skill_not_enough_mp", "MP不足" },
            { "fixed.skill_activation_failed_invalid_target", "技能发动失败（请正确选择目标牌）" },
            { "fixed.skill_activated", "技能发动" },
            { "fixed.skill_select_hand_target", "请选择手牌中的1张牌（技能目标）" },
            { "fixed.skill_invalid_selection", "选择无效" },
            { "fixed.skill_apply_failed", "技能应用失败" },
            { "fixed.skill_select_number_for_calligrapher", "请选择1张数牌（书家目标）" },
            { "fixed.selected_tag", "已选择" },
            { "fixed.equip_header_prefix", "已装备：" },
            { "fixed.equip_none", "无" },
            { "fixed.equip_owned_empty", "-" },
            { "fixed.skill_exhausted", "技能次数已耗尽" },
            { "fixed.skill_unique_max_two_selection", "使用神器效果时，手牌最多只能选择2张" },
            { "fixed.call_paralyzed_cannot_call", "麻痹状态下无法鸣牌" },
            { "fixed.call_riichi_cannot_call", "立直中无法鸣牌" },
            { "fixed.call_no_target", "没有可鸣的目标牌" },
            { "fixed.call_no_hand", "没有手牌" },
            { "fixed.call_chi_select_two", "吃请选择2张牌" },
            { "fixed.call_invalid_sequence", "所选牌不能组成顺子" },
            { "fixed.call_chi_complete_discard_one", "吃成功：请打出1张牌" },
            { "fixed.call_selection_insufficient", "选择不足" },
            { "fixed.demo_end_message", "Demo版到此结束。如果觉得有趣，欢迎将正式版加入愿望单或购买支持。" },
            { "fixed.legendary_damage_half_ongoing", "和了后紧接着的敌方和了伤害减半（击败敌人后消失）" },
            { "fixed.legendary_half_mp_cost_ongoing", "下一局MP消耗减半（击败敌人后消失）" },
            { "fixed.tier_select_resume_info", "存在中断数据。要从继续开始，还是从头开始？" },
            { "fixed.tier_dropdown_item_format", "Tier{0}  (Lv{1}-Lv{2} / 倍率 {3:0.0}x)" },
            { "fixed.tier_selected_format", "当前选择：Tier{0}  (Lv{1}-Lv{2}, 倍率 {3:0.0}x)" },
            { "fixed.tier_debug_enemy_on", "测试起始敌人：ON（EnemyIndex {0}）" },
            { "fixed.tier_debug_enemy_off", "测试起始敌人：OFF（从0号开始）" },
        });
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.confirm_discard", "打出" },
    { "fixed.confirm_tsumo", "自摸" },
    { "fixed.button_skip", "跳过" },
    { "fixed.button_skill", "技能" },
    { "fixed.status_replace_to_tenpai_or_riichi", "替换 -> [确定听牌]（可选） -> [立直] 或 [打出]" },
    { "fixed.call_can_select_enemy_discard", "可以对敌人的弃牌选择鸣牌/荣和" },
    { "fixed.call_no_kan_candidate", "没有可以杠的牌" },
    { "fixed.ankan_rinshan_draw", "暗杠 -> 摸岭上牌" },
    { "fixed.kakan_rinshan_draw", "加杠 -> 摸岭上牌" },
{ "fixed.gem_gain_prefix", "获得" },
{ "fixed.gem_gain_middle", "个宝石！" },
{ "fixed.gem_gain_suffix", "个宝石" },
    { "fixed.skill_duplicated_select_discard", "已复制。请选择另一张牌并按下[打出]" },
    { "fixed.skill_dora_plus_one", "宝牌指示牌+1" },
    { "fixed.skill_nullify_enemy_effect_once", "下一次敌方效果将被无效化" },
    { "fixed.skill_force_draw_selected_next_turn", "将 {0} 加入下回合摸牌" },

    { "fixed.tenpai_riichi_available", "听牌！可以立直" },
    { "fixed.not_tenpai", "未听牌" },

    { "fixed.relic_effect_transform", "神器效果：变为 {0}" },
    { "fixed.relic_effect_cannot_activate_three_or_more_honors", "神器效果：由于最多的字牌达到3张以上，无法发动" },

    { "fixed.select_one_hand_tile", "请选择手牌中的1张牌" },

    { "fixed.shanten_riichi", "立直" },
    { "fixed.shanten_agari", "和牌" },
    { "fixed.shanten_tenpai", "听牌" },
    { "fixed.shanten_suffix", "向听" },

    { "fixed.turn_suffix", "回合目" },
{ "fixed.turn_unit", "回合" },
    { "fixed.seat_east", "东家" },
    { "fixed.seat_south", "南家" },
    { "fixed.seat_west", "西家" },
    { "fixed.seat_north", "北家" },

    { "fixed.rightinfo_no_skill_equipped", "未装备技能" },
    { "fixed.rightinfo_target_geki", "<b>击的对应役种</b>：" },
    { "fixed.rightinfo_target_shun", "<b>瞬的对应役种</b>：" },
    { "fixed.rightinfo_target_iyu", "<b>愈的对应役种</b>：" },
    { "fixed.none_plain", "无" },

    { "fixed.ofuda_owned", "已持有符札" },
    { "fixed.ofuda_none", "没有符札" },

    { "fixed.active_skill_fallback", "技能" }
});
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.enemy_discard_call_or_ron_available", "可以对敌人的弃牌选择鸣牌/荣和" },
    { "fixed.enemy_hand_title", "敌人的手牌" },
    { "fixed.win_tsumo", "自摸" },
    { "fixed.win_ron", "荣和" },
    { "fixed.enemy_win_body_line1", "敌人和牌！" },
    { "fixed.enemy_win_body_score_prefix", "点数: " },
    { "fixed.enemy_win_body_hp_damage_prefix", "HP伤害: " },
    { "fixed.score_label", "SCORE" },
    { "fixed.ryukyoku", "流局" },
    { "fixed.select_tsumo_tile", "请点击要自摸和牌的牌" },
    { "fixed.placeholder_dash", "—" },
    { "fixed.enemy_generic_name", "敌人" }
});
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.yaku.riichi_short", "立直" },
    { "fixed.yaku.double_riichi_short", "双立直" },
    { "fixed.yaku.ippatsu_short", "一发" },
    { "fixed.yaku.riichi", "立直" },
{ "fixed.yaku.double_riichi", "双立直" },
{ "fixed.yaku.ippatsu", "一发" },
    { "fixed.fu_prefix", "符: " },
    { "fixed.yaku_none", "无役" },
    { "fixed.yaku_label_prefix", "役: " },
    { "fixed.han_fu_label", "番・符" },
    { "fixed.han_suffix", "番" },
    { "fixed.fu_suffix", "符" },
    { "fixed.dora_count_format", "宝牌×{0}" },
    { "fixed.special_tile_dora_count_format", "特殊牌宝牌×{0}" },
    { "fixed.ura_dora_count_format", "里宝牌×{0}" },
    { "fixed.dora_label_prefix", "宝牌: " }
});
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.HP", "HP" },
    { "fixed.初回撃破報酬（宝石×1）", "首次击败奖励（宝石×1）" },
    { "fixed.取得済み", "已领取" },
    { "fixed.未取得", "未领取" },
    { "fixed.スキル", "技能" },
    { "fixed.なし", "无" },

    { "fixed.{0}　プレイヤーHPに{1}ダメージ", "{0}　对玩家HP造成{1}伤害" },
    { "fixed.{0}　{1}ターンスキル使用不可", "{0}　{1}回合内无法使用技能" },
    { "fixed.{0}　{1}ターン毎ターン{2}ダメージ", "{0}　在{1}回合内每回合造成{2}伤害" },
    { "fixed.{0}　次の和了ダメージ +{1}%", "{0}　下一次和牌伤害+{1}%" },
    { "fixed.{0}　次のプレイヤー和了ダメージ {1}%減少", "{0}　使玩家下一次和牌伤害减少{1}%" },
    { "fixed.{0}　プレイヤーMPを{1}減少", "{0}　使玩家MP减少{1}" },
    { "fixed.{0}　手牌を{1}枚入れ替え", "{0}　替换手牌中的{1}张牌" }
});
AddAll(_zhHans, new Dictionary<string, string>
{
    { "fixed.rightinfo_no_skill_equipped", "未装备技能" },
    { "fixed.rightinfo_target_geki", "<b>击的对应役种</b>：" },
    { "fixed.rightinfo_target_shun", "<b>瞬的对应役种</b>：" },
    { "fixed.rightinfo_target_iyu", "<b>愈的对应役种</b>：" },
    { "fixed.none_plain", "无" },

    { "fixed.ofuda_owned", "已持有符札" },
    { "fixed.ofuda_none", "没有符札" },
    { "fixed.ofuda_empty_slot", "无" },
    { "fixed.ofuda_capacity_format", "持有 {0}/{1}" },
{ "fixed.yaku.dora_count", "宝牌×{0}" },
{ "fixed.yaku.red_dora_count", "红宝牌×{0}" },
{ "fixed.yaku.ura_dora_count", "里宝牌×{0}" },
    { "fixed.active_skill_fallback", "技能" }
});
    }

    public static string T(string key)
    {
        return Instance.GetText(key);
    }

    public static string F(string key, params object[] args)
    {
        return Instance.FormatText(key, args);
    }

    public static string Fixed(string key)
    {
        return Instance.GetFixedText(key);
    }

    public static string Yaku(string canonicalKey)
    {
        return Instance.GetYakuDisplayName(canonicalKey);
    }

    public static string Yakuman(string canonicalKey)
    {
        return Instance.GetYakumanDisplayName(canonicalKey);
    }

    public static string EnemySkill(string canonicalKey)
    {
        return Instance.GetEnemySkillDisplayName(canonicalKey);
    }

public static string Rarity(string canonicalKey)
{
    return Instance.GetRarityDisplayName(canonicalKey);
}

public static string ActiveSkill(string canonicalKey)
{
    return Instance.GetActiveSkillDisplayName(canonicalKey);
}

public static string ActiveSkillAction(string canonicalKey)
{
    return Instance.GetActiveSkillActionName(canonicalKey);
}

public static string ActiveSkillDesc(string canonicalKey)
{
    return Instance.GetActiveSkillDescription(canonicalKey);
}

public static TMP_FontAsset BodyFont()
{
    return Instance.GetBodyFont();
}
    public static TMP_FontAsset TitleFont()
    {
        return Instance.GetTitleFont();
    }

    public static TMP_FontAsset NumberFont()
    {
        return Instance.GetNumberFont();
    }

    public static TMP_FontAsset FontByRole(string role)
    {
        return Instance.GetFontByRole(role);
    }
}