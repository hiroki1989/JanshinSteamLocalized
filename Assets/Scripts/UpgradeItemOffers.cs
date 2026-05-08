using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UpgradeScene 用の“ローグライト・アイテム3択”アドオン。
/// 既存の UpgradeManager を一切変更せず、このスクリプトを同じシーンに置くだけで
/// ランダム3択→1つ取得→Finish（戦闘へ戻る）を実現します。
/// </summary>
public class UpgradeItemOffers : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("3択を作る親。未指定なら Canvas 直下に自動生成します。")]
    public RectTransform offersRoot;
    [Tooltip("Finish ボタン（未指定でも自動生成します）")]
    public Button finishButton;

    // RunItems 保存キー（GameManager と共通）
    private const string RunItemsKey = "RunItems";

    // ラン中の所持アイテム
    private readonly HashSet<string> runItems = new HashSet<string>();

    // 最後に提示したID
    private readonly List<string> lastOfferIds = new List<string>(3);

    void Start()
    {
        LoadRunItems();
        BuildOffersUI();
    }

    // ===== RunItems I/O =====
    private void LoadRunItems()
    {
        runItems.Clear();
        var raw = PlayerPrefs.GetString(RunItemsKey, string.Empty);
        if (string.IsNullOrEmpty(raw)) return;
        var parts = raw.Split(new char[]{','}, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var id = parts[i].Trim();
            if (!string.IsNullOrEmpty(id)) runItems.Add(id);
        }
    }
    private void SaveRunItems()
    {
        string raw = string.Join(",", new List<string>(runItems).ToArray());
        PlayerPrefs.SetString(RunItemsKey, raw);
        PlayerPrefs.Save();
    }
    private bool HasRunItem(string id) => !string.IsNullOrEmpty(id) && runItems.Contains(id);

    // ===== Catalog: Sample1..Sample100 =====
    private static List<string> AllSampleIds()
    {
        var list = new List<string>(100);
        for (int i = 1; i <= 100; i++) list.Add("Sample"+i);
        return list;
    }
    private static string ToDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (id.StartsWith("Sample")) return "サンプル" + id.Substring("Sample".Length);
        return id;
    }

    // ===== UI =====
    private void BuildOffersUI()
    {
        // 親を確保
        if (!offersRoot)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            var go = new GameObject("ItemOffers", typeof(RectTransform));
            offersRoot = go.GetComponent<RectTransform>();
            if (canvas) offersRoot.SetParent(canvas.transform, false);
            offersRoot.anchorMin = offersRoot.anchorMax = offersRoot.pivot = new Vector2(0.5f, 0.5f);
            offersRoot.sizeDelta = new Vector2(640, 320);
            offersRoot.anchoredPosition = Vector2.zero;
        }
        // 既存子をクリア
        for (int i = offersRoot.childCount-1; i >= 0; i--) Destroy(offersRoot.GetChild(i).gameObject);

        // 候補（未所持）から3つ
        var pool = AllSampleIds();
        for (int i = pool.Count-1; i >= 0; i--) if (HasRunItem(pool[i])) pool.RemoveAt(i);
        for (int i = 0; i < pool.Count; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        int offerCount = Mathf.Min(3, pool.Count);
        lastOfferIds.Clear();
        for (int i = 0; i < offerCount; i++) lastOfferIds.Add(pool[i]);

        // ボタンを並べる
        float spacing = 210f;
        float startX = -spacing * (offerCount-1) * 0.5f;
        for (int i = 0; i < offerCount; i++)
        {
            string id = lastOfferIds[i];
            var btnGO = new GameObject("Offer_"+id, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = btnGO.GetComponent<RectTransform>();
            rt.SetParent(offersRoot, false);
            rt.sizeDelta = new Vector2(200, 120);
            rt.anchoredPosition = new Vector2(startX + i*spacing, 0f);
            btnGO.GetComponent<Image>().color = new Color(1,1,1,0.12f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = label.GetComponent<RectTransform>();
            lrt.SetParent(btnGO.transform, false);
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(190, 110);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 24;
            tmp.text = ToDisplayName(id) + "\n<size=18>リーチの役が含まれていると点数1.5倍。</size>";

            var btn = btnGO.GetComponent<Button>();
            btn.interactable = !HasRunItem(id);
            btn.onClick.AddListener(()=>OnPickItem(id));
        }

        // キャプション
        var cap = new GameObject("Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
        var crt = cap.GetComponent<RectTransform>();
        crt.SetParent(offersRoot, false);
        crt.anchorMin = new Vector2(0.5f, 1f);
        crt.anchorMax = new Vector2(0.5f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(640, 40);
        crt.anchoredPosition = new Vector2(0f, 24f);
        var ctmp = cap.GetComponent<TextMeshProUGUI>();
        ctmp.alignment = TextAlignmentOptions.Center;
        ctmp.fontSize = 24;
        ctmp.text = (offerCount > 0) ? "アイテムを1つ選んでください（重複入手なし）。"
                                     : "すべて入手済みです。Finishで戻れます。";

        // Finish ボタンが無ければ作る
        if (!finishButton)
        {
            var go = new GameObject("Finish", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(offersRoot, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, -60f);
            rt.sizeDelta = new Vector2(180, 44);
            var lbl = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.SetParent(go.transform, false);
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f,0.5f);
            lrt.sizeDelta = new Vector2(170, 40);
            var t = lbl.GetComponent<TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Center;
            t.fontSize = 22;
            t.text = "Finish";
            finishButton = go.GetComponent<Button>();
        }
        finishButton.onClick.RemoveAllListeners();
        finishButton.onClick.AddListener(OnFinish);
        // 候補が無くても戻れるように
        finishButton.interactable = true;
    }

    private void OnPickItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!runItems.Contains(id))
        {
            runItems.Add(id);
            SaveRunItems();
        }
        OnFinish();
    }

    // 戦闘シーンへ戻る（UpgradeManager と同じ名前の関数に合わせています）
    public void OnFinish()
    {
        // 戦闘シーン名はプロジェクトに合わせて変更してください（既定: RunScene）
        UnityEngine.SceneManagement.SceneManager.LoadScene("RunScene");
    }
}
