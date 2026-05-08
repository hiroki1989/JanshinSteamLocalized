using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI gemsTMP;          // 所持宝石（数値のみ）
    [SerializeField] private TextMeshProUGUI addedHpTMP;       // 追加HP（累積）
    [SerializeField] private TextMeshProUGUI addedMpTMP;       // 追加MP（累積）
    [SerializeField] private TextMeshProUGUI hpCostTMP;        // HP購入コスト
    [SerializeField] private TextMeshProUGUI mpCostTMP;        // MP購入コスト
    [SerializeField] private TextMeshProUGUI hpAddAmountTMP;   // 1回購入で増えるHP
    [SerializeField] private TextMeshProUGUI mpAddAmountTMP;   // 1回購入で増えるMP
    [SerializeField] private Button buyHpButton;
    [SerializeField] private Button buyMpButton;
    [SerializeField] private Button backButton;

    [Header("Gem Costs (Inspector)")]
    [SerializeField] private int hpUpgradeCostGems = 3;
    [SerializeField] private int mpUpgradeCostGems = 3;

    [Header("Upgrade Amount (Inspector)")]
    [SerializeField] private int hpIncreasePerPurchase = 5;
    [SerializeField] private int mpIncreasePerPurchase = 1;

    private const string PrefKey_PermHpBonus = "Perm_HPBonus";
    private const string PrefKey_PermMpBonus = "Perm_MPBonus";

    private void Start()
    {
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() =>
                UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene"));
        }

        if (buyHpButton)
        {
            buyHpButton.onClick.RemoveAllListeners();
            buyHpButton.onClick.AddListener(OnClickBuyHp);
        }

        if (buyMpButton)
        {
            buyMpButton.onClick.RemoveAllListeners();
            buyMpButton.onClick.AddListener(OnClickBuyMp);
        }

        RefreshAllUI();
    }

    private void OnEnable()
    {
        RefreshAllUI();
    }

    private void RefreshAllUI()
    {
        int gems = GetGemsSafe();

        int permHp = GetPermHpBonus();
        int permMp = GetPermMpBonus();

        int hpCost = Mathf.Max(0, hpUpgradeCostGems);
        int mpCost = Mathf.Max(0, mpUpgradeCostGems);

        int hpAdd = Mathf.Max(0, hpIncreasePerPurchase);
        int mpAdd = Mathf.Max(0, mpIncreasePerPurchase);

        if (gemsTMP) gemsTMP.text = gems.ToString();

        // 累積（過去購入分）
        if (addedHpTMP) addedHpTMP.text = FormatSignedPlus(permHp);
        if (addedMpTMP) addedMpTMP.text = FormatSignedPlus(permMp);

        // 今回購入で増える量（Inspectorの値）
        if (hpAddAmountTMP) hpAddAmountTMP.text = FormatSignedPlus(hpAdd);
        if (mpAddAmountTMP) mpAddAmountTMP.text = FormatSignedPlus(mpAdd);

        if (hpCostTMP) hpCostTMP.text = hpCost.ToString();
        if (mpCostTMP) mpCostTMP.text = mpCost.ToString();

        // ボタン活性
        // コスト>0 かつ 追加量>0 かつ 所持宝石が足りる
        if (buyHpButton) buyHpButton.interactable = (hpCost > 0) && (hpAdd > 0) && (gems >= hpCost);
        if (buyMpButton) buyMpButton.interactable = (mpCost > 0) && (mpAdd > 0) && (gems >= mpCost);
    }

    private void OnClickBuyHp()
    {
        int cost = Mathf.Max(0, hpUpgradeCostGems);
        int add  = Mathf.Max(0, hpIncreasePerPurchase);

        if (cost <= 0 || add <= 0) { RefreshAllUI(); return; }

        int gems = GetGemsSafe();
        if (gems < cost) { RefreshAllUI(); return; }

        if (!TrySpendGemsSafe(cost)) { RefreshAllUI(); return; }

        int cur = GetPermHpBonus();
        SetPermHpBonus(cur + add);

        RefreshAllUI();
    }

    private void OnClickBuyMp()
    {
        int cost = Mathf.Max(0, mpUpgradeCostGems);
        int add  = Mathf.Max(0, mpIncreasePerPurchase);

        if (cost <= 0 || add <= 0) { RefreshAllUI(); return; }

        int gems = GetGemsSafe();
        if (gems < cost) { RefreshAllUI(); return; }

        if (!TrySpendGemsSafe(cost)) { RefreshAllUI(); return; }

        int cur = GetPermMpBonus();
        SetPermMpBonus(cur + add);

        RefreshAllUI();
    }

    private int GetPermHpBonus()
    {
        try { return PlayerPrefs.GetInt(PrefKey_PermHpBonus, 0); } catch { return 0; }
    }

    private int GetPermMpBonus()
    {
        try { return PlayerPrefs.GetInt(PrefKey_PermMpBonus, 0); } catch { return 0; }
    }

    private void SetPermHpBonus(int v)
    {
        try { PlayerPrefs.SetInt(PrefKey_PermHpBonus, Mathf.Max(0, v)); PlayerPrefs.Save(); } catch {}
    }

    private void SetPermMpBonus(int v)
    {
        try { PlayerPrefs.SetInt(PrefKey_PermMpBonus, Mathf.Max(0, v)); PlayerPrefs.Save(); } catch {}
    }

    private string FormatSignedPlus(int v)
    {
        if (v <= 0) return "0";
        return "+" + v.ToString();
    }

    // ========= Gems =========

    private int GetGemsSafe()
    {
        int gems = 0;
        try
        {
            var t = typeof(SpecialTileSystem);

            var mi = t.GetMethod("GetGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mi != null && mi.ReturnType == typeof(int))
            {
                gems = (int)mi.Invoke(null, null);
                return Mathf.Max(0, gems);
            }

            var mi2 = t.GetMethod("GetGemCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mi2 != null && mi2.ReturnType == typeof(int))
            {
                gems = (int)mi2.Invoke(null, null);
                return Mathf.Max(0, gems);
            }

            var prop = t.GetProperty("Gems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.PropertyType == typeof(int))
            {
                gems = (int)prop.GetValue(null, null);
                return Mathf.Max(0, gems);
            }

            var prop2 = t.GetProperty("GemCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop2 != null && prop2.PropertyType == typeof(int))
            {
                gems = (int)prop2.GetValue(null, null);
                return Mathf.Max(0, gems);
            }
        }
        catch { gems = 0; }

        return Mathf.Max(0, gems);
    }

    private bool TrySpendGemsSafe(int cost)
    {
        if (cost <= 0) return true;

        try
        {
            var t = typeof(SpecialTileSystem);

            var miTry = t.GetMethod("TrySpendGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (miTry != null && miTry.ReturnType == typeof(bool))
            {
                return (bool)miTry.Invoke(null, new object[] { cost });
            }

            var miTry2 = t.GetMethod("TryConsumeGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (miTry2 != null && miTry2.ReturnType == typeof(bool))
            {
                return (bool)miTry2.Invoke(null, new object[] { cost });
            }

            var miSpend = t.GetMethod("SpendGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (miSpend != null)
            {
                miSpend.Invoke(null, new object[] { cost });
                return true;
            }

            var miConsume = t.GetMethod("ConsumeGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (miConsume != null)
            {
                miConsume.Invoke(null, new object[] { cost });
                return true;
            }

            var miAdd = t.GetMethod("AddGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (miAdd != null)
            {
                miAdd.Invoke(null, new object[] { -cost });
                return true;
            }
        }
        catch { }

        return false;
    }
}
