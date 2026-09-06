using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipManager : MonoBehaviour
{

    [Header("UI")]
    public TextMeshProUGUI equippedTMP;         // 上部の「装備中：XXX」
    public Transform ownedListParent;           // ScrollView/Viewport/Content
    public GameObject omamoriItemPrefab;        // OmamoriItem プレハブ（Button+TMP）

    public Button equipNoneButton;              // 装備解除
    public Button backButton;                   // メニューへ

    // 追加（効果説明表示用）
    public TextMeshProUGUI equippedEffectsTMP;  // 上部の「効果：～」を表示するTMP

    // ★追加：所持数表示（例：11/20）
    public TextMeshProUGUI ownedCountTMP;

    // ★追加：破棄ボタン（選択中のお守りを破棄）
    public Button discardButton;

    [Header("Omamori Icon UI (Manual)")]
    [SerializeField] private Image equippedOmamoriIconImage;           // 上部：装備中お守りアイコン
    [SerializeField] private Sprite omamoriIconSprite;                 // お守りアイコンSprite（共通でOK）
    [SerializeField] private string ownedRowIconChildName = "Icon";    // 所持一覧行プレハブ内のアイコンImageの名前

    [Header("Owned Row Visuals (Manual)")]
    [SerializeField] private string ownedRowBackgroundChildName = "Background"; // 行プレハブ内の背景Image(任意)
    [SerializeField] private Color ownedRowNormalBgColor = Color.white;          // 非選択時の背景色
    [SerializeField] private Color ownedRowSelectedBgColor = new Color(0.65f, 0.65f, 0.65f, 1f); // 選択中の背景色
    [SerializeField] private string ownedRowEquippedMarkChildName = "EquippedMark"; // 装備中アイコンImage(任意)
    [Header("Owned Omamori List Text Style")]
    [SerializeField] private TMP_FontAsset ownedListFont;                 // 未指定ならプレハブ側のまま
    [SerializeField] private Color ownedListTextColor = Color.white;      // 本文色
    [SerializeField] private Color ownedListEquippedTagColor = new Color(0.4f, 1f, 0.666f, 1f); // (装備中) の色
    [SerializeField] private Color ownedListSelectedTagColor = new Color(1f, 0.85f, 0.4f, 1f);  // (選択中) の色
    [SerializeField] private float ownedListFontSize = 28f;               // 0以下なら触らない運用でもOK
    [SerializeField] private int ownedListTagSizePercent = 80;            // 例: 80 -> <size=80%>
    [SerializeField] private int ownedListDescSizePercent = 80;           // 例: 80 -> <size=80%>
[Header("Trait Icon Replacement (TMP)")]
[SerializeField] private bool replaceTraitWordsWithIcons = true;

// 「撃」「瞬」「癒」それぞれ別々にSpriteAssetを指定できるようにする
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAssetGeki = null;
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAssetShun = null;
[SerializeField] private TMP_SpriteAsset traitIconsSpriteAssetIyu  = null;

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

[SerializeField, Range(50, 150)] private int traitIconSizePercent = 100;

private void ApplyTraitSpriteAssetToTMP(TextMeshProUGUI tmp)
{
    if (!tmp) return;

    TMP_SpriteAsset primary = null;

    if (traitIconsSpriteAssetGeki != null) primary = traitIconsSpriteAssetGeki;
    else if (traitIconsSpriteAssetShun != null) primary = traitIconsSpriteAssetShun;
    else if (traitIconsSpriteAssetIyu != null) primary = traitIconsSpriteAssetIyu;

    if (primary == null) return;

    // ランタイム用SpriteAsset（fallback込み）を必要に応じて作り直す
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

        // アセット本体を汚さないように複製してfallbackを構築する
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
    private void SetSingleEquippedOmamori(int id)
    {
        id = Mathf.Max(0, id);

        // 単一装備
        PlayerData.EquippedOmamori = id;

        // 複数装備保存側も必ず同期
        if (id == 0)
        {
            PlayerData.EquippedOmamoriIds = new List<int>();
        }
        else
        {
            PlayerData.EquippedOmamoriIds = new List<int> { id };
        }

        PlayerPrefs.Save();
    }
private TMP_SpriteAsset _traitSpriteAssetRuntime = null;
private int _traitSpriteAssetRuntimeKey = 0;
private string ReplaceTraitWordsWithIcons(string src)
{
    if (!replaceTraitWordsWithIcons) return src;
    if (string.IsNullOrEmpty(src)) return src;

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
        if (ch >= '\u4E00' && ch <= '\u9FFF') return true;
        if (ch >= '\u3040' && ch <= '\u309F') return true;
        if (ch >= '\u30A0' && ch <= '\u30FF') return true;
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
    private static string GetEquipFixedText_Local(string key)
    {
        return LocalizationManager.Fixed(key);
    }

    private static string LocalizeEquippedHeader_Local(string omamoriText)
    {
        return GetEquipFixedText_Local("equip_header_prefix") + (omamoriText ?? "");
    }

    private static string GetEquipNoneText_Local()
    {
        return GetEquipFixedText_Local("equip_none");
    }

    private static string GetOwnedEmptyText_Local()
    {
        return GetEquipFixedText_Local("equip_owned_empty");
    }

    private static string ExtractOmamoriRarityKeyFromName_Local(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";

        string t = Regex.Replace(name, "<.*?>", "");
        t = t.Trim();

        if (t.StartsWith("【") || t.StartsWith("["))
            return "Legendary";

        int sp = t.IndexOf(' ');
        int jpSp = t.IndexOf('　');
        int cut = -1;

        if (sp >= 0 && jpSp >= 0) cut = Mathf.Min(sp, jpSp);
        else if (sp >= 0) cut = sp;
        else cut = jpSp;

        string head = cut >= 0 ? t.Substring(0, cut).Trim() : t;

        if (head.StartsWith("Lv", System.StringComparison.OrdinalIgnoreCase))
            return "";

        return NormalizeRarityKey_Local(head);
    }

    // 内部: 生成した行を保持（更新時に破棄）
    private readonly List<GameObject> rows = new();

    // ★追加：現在選択中（破棄対象）のお守りID
    private int _selectedOwnedId = 0;

    void Start()
    {
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() =>
                UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene"));
        }

        if (equipNoneButton)
        {
            equipNoneButton.onClick.RemoveAllListeners();
            equipNoneButton.onClick.AddListener(() =>
            {
                SetSingleEquippedOmamori(0);
                Refresh();

                // ★追加：RunScene 用ストアもクリア（空のリストで上書き）
                PushEquippedOmamoriToRunStore();
            });
        }

        // ★追加：破棄ボタン
        if (discardButton)
        {
            discardButton.onClick.RemoveAllListeners();
            discardButton.onClick.AddListener(() =>
            {
                if (_selectedOwnedId == 0) return;

                PlayerData.DiscardOwnedOmamori(_selectedOwnedId);
                _selectedOwnedId = 0;

                // 装備が外れた可能性があるので、RunScene 用ストアも更新
                PushEquippedOmamoriToRunStore();

                Refresh();
            });
        }

        Refresh();
    }
    private void PushEquippedOmamoriToRunStore()
    {
        // PlayerData 側の現在装備 ID を見て、表示用エントリを1件だけ作る
        var list = new List<GameManager.OmamoriEntry>();
        int eq = PlayerData.EquippedOmamori;

        if (eq != 0)
        {
            string name = PlayerData.GetOmamoriName_Localized(eq);
            string desc = PlayerData.GetOmamoriDesc_Localized(eq);
            list.Add(new GameManager.OmamoriEntry { name = name, desc = desc });
        }

        // ここで RunScene 用ストアを更新（空なら「無し」に相当）
        GameManager.SetEquippedOmamoriForNextRun(list);
    }
    private void Refresh()
    {
        // 上部ラベル
        int eq = PlayerData.EquippedOmamori;

        if (equippedTMP)
        {
            equippedTMP.text = (eq == 0)
                ? LocalizeEquippedHeader_Local(GetEquipNoneText_Local())
                : LocalizeEquippedHeader_Local(PlayerData.GetOmamoriName_Localized(eq));
        }
        // ★装備中お守りアイコン（装備時だけ表示、Tintをレア度色へ）
        RefreshEquippedOmamoriIcon(eq);
        if (equippedEffectsTMP)
        {
            equippedEffectsTMP.gameObject.SetActive(true);
            equippedEffectsTMP.enableWordWrapping = true;
            equippedEffectsTMP.alignment = TextAlignmentOptions.Left;
            equippedEffectsTMP.richText = true;

            if (eq == 0)
            {
                equippedEffectsTMP.text = GetEquipNoneText_Local();
            }
            else
            {
                ApplyTraitSpriteAssetToTMP(equippedEffectsTMP);
                equippedEffectsTMP.text = ReplaceTraitWordsWithIcons(PlayerData.GetOmamoriText_EquipUI_Localized(eq, true));
            }
        }
        
        // 一旦クリア
        foreach (var go in rows) if (go) Destroy(go);
        rows.Clear();

        if (!ownedListParent || !omamoriItemPrefab) return;

        // 所持しているIDを昇順で並べる
        var owned = new List<int>(PlayerData.OwnedOmamori);
        owned.Sort();

        // ★追加：所持数表示（例：11/20）
        if (ownedCountTMP)
            ownedCountTMP.text = $"{owned.Count}/{PlayerData.MaxOwnedOmamori}";

        // 選択中IDが消えていたらクリア
        if (_selectedOwnedId != 0 && !PlayerData.OwnedOmamori.Contains(_selectedOwnedId))
            _selectedOwnedId = 0;

        // ★追加：破棄ボタンの有効/無効
        if (discardButton)
            discardButton.interactable = (_selectedOwnedId != 0);
        if (owned.Count == 0)
        {
            var row = CreatePlainRow(GetOwnedEmptyText_Local());
            rows.Add(row);
            return;
        }
        // 1行ずつ生成
        foreach (int id in owned)
        {
            var go = Instantiate(omamoriItemPrefab, ownedListParent);
            rows.Add(go);

            // ★追加：縦リスト用の行高さを安定化
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = 180;
            le.preferredHeight = 180;
            le.flexibleHeight = 0;
            le.flexibleWidth = 0;

            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>();

            bool isEquipped = (PlayerData.EquippedOmamori == id);
            bool isSelected = (_selectedOwnedId == id);
            string name = PlayerData.GetOmamoriName_Localized(id);

            if (label)
            {
                label.enableWordWrapping = true;
                label.alignment = TextAlignmentOptions.Left;
                label.richText = true;

                if (TMPro.TMP_Settings.defaultFontAsset) label.font = TMPro.TMP_Settings.defaultFontAsset;
                if (ownedListFontSize > 0f) label.fontSize = ownedListFontSize;
                label.color = ownedListTextColor;

                // ローカライズ済み本文（1行目：レア度色＋Lv、2行目以降：-XXX）
                string uiText = PlayerData.GetOmamoriText_EquipUI_Localized(id, true);

                // 先頭行と残りに分ける（タグ表示は廃止）
                int nl = uiText.IndexOf('\n');
                string firstLine = (nl >= 0) ? uiText.Substring(0, nl) : uiText;
                string restLines = (nl >= 0) ? uiText.Substring(nl + 1) : "";

                // 装備中は太字のみ（(装備中)などのテキストは出さない）
                if (isEquipped)
                {
                    firstLine = $"<b>{firstLine}</b>";
                }

                ApplyTraitSpriteAssetToTMP(label);

                if (!string.IsNullOrEmpty(restLines))
                {
                    string composed = $"{firstLine}\n<size={ownedListDescSizePercent}%>{restLines}</size>";
                    label.text = ReplaceTraitWordsWithIcons(composed);
                }
                else
                {
                    label.text = ReplaceTraitWordsWithIcons(firstLine);
                }
            }
            
            // ★所持一覧：選択中背景グレーアウト／装備中アイコン表示
            ApplyOwnedRowVisual(go, isSelected, isEquipped);

            // ★所持一覧：行アイコン（Sprite＋レア度Tint）
            RefreshOwnedRowIcon(go, id);

            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    // ★追加：クリックしたものを「選択中」にする（破棄対象）
                    _selectedOwnedId = id;

                    // 既存仕様：クリックで装備もする
                    SetSingleEquippedOmamori(id);

                    // 装備反映（RunScene 用ストア）
                    PushEquippedOmamoriToRunStore();

                    Refresh();
                });
            }
        }

        // すぐにレイアウト反映
        var rt = ownedListParent as RectTransform;
        if (rt) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    // テキストだけのプレースホルダ行を作る
    GameObject CreatePlainRow(string message)
    {
        var go = new GameObject("Row_Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(ownedListParent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(0, 0);
        rt.offsetMax = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(0, 120);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 120;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.enableWordWrapping = true;

        if (TMPro.TMP_Settings.defaultFontAsset) tmp.font = TMPro.TMP_Settings.defaultFontAsset;
        tmp.color = ownedListTextColor;
        tmp.fontSize = (ownedListFontSize > 0f) ? ownedListFontSize : 28f;

        return go;
    }

    private void RefreshEquippedOmamoriIcon(int eqId)
    {
        if (!equippedOmamoriIconImage) return;

        if (eqId == 0)
        {
            if (equippedOmamoriIconImage.gameObject.activeSelf)
                equippedOmamoriIconImage.gameObject.SetActive(false);
            return;
        }

        if (omamoriIconSprite)
            equippedOmamoriIconImage.sprite = omamoriIconSprite;

        equippedOmamoriIconImage.preserveAspect = true;

        // ★神器（ユニーク）は必ず赤Tint（PlayerData 側で Color.red を返す）
        if (PlayerData.TryGetOmamoriRarityColor(eqId, out var cById))
        {
            equippedOmamoriIconImage.color = cById;
        }
        else
        {
            // フォールバック（何らかの理由でID判定できない場合のみ）
            string name = PlayerData.GetOmamoriName_Localized(eqId);
            string rarityKey = ExtractOmamoriRarityKeyFromName_Local(name);
            string rarityDisplay = string.IsNullOrEmpty(rarityKey) ? "" : LocalizationManager.Rarity(rarityKey);

            if (string.IsNullOrEmpty(rarityKey))
            {
                if (equippedOmamoriIconImage.gameObject.activeSelf)
                    equippedOmamoriIconImage.gameObject.SetActive(false);
                return;
            }

            equippedOmamoriIconImage.color = GetRarityColorSafe_Local(rarityKey, rarityDisplay);
        }
        if (!equippedOmamoriIconImage.gameObject.activeSelf)
            equippedOmamoriIconImage.gameObject.SetActive(true);
    }
    private void RefreshOwnedRowIcon(GameObject rowGo, int omamoriId)
    {
        if (!rowGo) return;

        var icon = FindOwnedRowIconImage(rowGo);
        if (!icon) return;

        if (omamoriId <= 0)
        {
            if (icon.gameObject.activeSelf)
                icon.gameObject.SetActive(false);
            return;
        }

        if (omamoriIconSprite)
            icon.sprite = omamoriIconSprite;

        icon.preserveAspect = true;

        // ★神器（ユニーク）は必ず赤Tint（PlayerData 側で Color.red を返す）
        if (PlayerData.TryGetOmamoriRarityColor(omamoriId, out var cById))
        {
            icon.color = cById;
        }
        else
        {
            // フォールバック（何らかの理由でID判定できない場合のみ）
            string name = PlayerData.GetOmamoriName_Localized(omamoriId);
            string rarityKey = ExtractOmamoriRarityKeyFromName_Local(name);
            string rarityDisplay = string.IsNullOrEmpty(rarityKey) ? "" : LocalizationManager.Rarity(rarityKey);

            if (string.IsNullOrEmpty(rarityKey))
            {
                if (icon.gameObject.activeSelf)
                    icon.gameObject.SetActive(false);
                return;
            }

            icon.color = GetRarityColorSafe_Local(rarityKey, rarityDisplay);
        }
        if (!icon.gameObject.activeSelf)
            icon.gameObject.SetActive(true);
    }

    private Image FindOwnedRowIconImage(GameObject rowGo)
    {
        if (!rowGo) return null;

        if (!string.IsNullOrEmpty(ownedRowIconChildName))
        {
            var tr = rowGo.transform.Find(ownedRowIconChildName);
            if (tr)
            {
                var img = tr.GetComponent<Image>();
                if (img) return img;
            }
        }

        // fallback：背景Image以外の最初のImage
        var all = rowGo.GetComponentsInChildren<Image>(true);
        foreach (var img in all)
        {
            if (!img) continue;
            if (img.gameObject == rowGo) continue;
            return img;
        }

        return null;
    }
    private void ApplyOwnedRowVisual(GameObject rowGo, bool isSelected, bool isEquipped)
    {
        if (!rowGo) return;

        var bg = FindOwnedRowBackgroundImage(rowGo);
        if (bg)
        {
            bg.color = isSelected ? ownedRowSelectedBgColor : ownedRowNormalBgColor;
        }

        var eqMark = FindOwnedRowEquippedMarkImage(rowGo);
        if (eqMark)
        {
            bool show = isEquipped;
            if (eqMark.gameObject.activeSelf != show) eqMark.gameObject.SetActive(show);
        }
    }

    private Image FindOwnedRowBackgroundImage(GameObject rowGo)
    {
        if (!rowGo) return null;

        if (!string.IsNullOrEmpty(ownedRowBackgroundChildName))
        {
            var tr = rowGo.transform.Find(ownedRowBackgroundChildName);
            if (tr)
            {
                var img = tr.GetComponent<Image>();
                if (img) return img;
            }
        }

        return rowGo.GetComponent<Image>();
    }

    private Image FindOwnedRowEquippedMarkImage(GameObject rowGo)
    {
        if (!rowGo) return null;
        if (string.IsNullOrEmpty(ownedRowEquippedMarkChildName)) return null;

        var tr = rowGo.transform.Find(ownedRowEquippedMarkChildName);
        if (!tr) return null;

        return tr.GetComponent<Image>();
    }

    private static string NormalizeRarityKey_Local(string rarityRaw)
    {
        if (string.IsNullOrEmpty(rarityRaw)) return "";

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

    private static Color GetRarityColorSafe_Local(string rarityKeyOrRaw, string japaneseTagText)
    {
        string key = NormalizeRarityKey_Local(rarityKeyOrRaw);

        try
        {
            var c = OfudaRarityColors.Get(key);
            if (!(c == Color.white && key != "Normal"))
                return c;
        }
        catch { }

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

        switch (japaneseTagText)
        {
            case "レジェンダリー": return new Color(1.00f, 0.55f, 0.00f);
            case "エピック":       return new Color(0.60f, 0.20f, 1.00f);
            case "レア":           return new Color(1.00f, 0.85f, 0.00f);
            case "コモン":         return new Color(0.20f, 0.60f, 1.00f);
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
}
