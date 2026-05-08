using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StageSelectManager : MonoBehaviour
{
    public Button btnStage1, btnStage2, btnStage3;
    public Button btnBack; // ★ 追加

    void Start()
    {
        if (btnStage1) btnStage1.onClick.AddListener(()=>Select(0));
        if (btnStage2) btnStage2.onClick.AddListener(()=>Select(1));
        if (btnStage3) btnStage3.onClick.AddListener(()=>Select(2));
        if (btnBack)   btnBack.onClick.AddListener(BackToMenu); // ★ 追加
    }

    void Select(int stage)
    {
        PlayerData.CurrentStage = stage;
        PlayerData.CurrentEnemy = 0;
        SceneManager.LoadScene("RunScene"); // ←実際の対局シーン名に合わせる
    }

    void BackToMenu()  // ★ 追加
    {
        SceneManager.LoadScene("MenuScene");
    }
}
