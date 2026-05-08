// MenuSceneWiring.cs （メニューシーンの空オブジェクトに付ける）
using UnityEngine;
using UnityEngine.UI;

public class MenuSceneWiring : MonoBehaviour
{
    [SerializeField] private Button startButton;      // 「対局」ボタン
    [SerializeField] private Button otherButton;      // 「その他」ボタン
    [SerializeField] private MenuController menu;     // 同シーン内の MenuController

    private void Awake()
    {
        // 念のため
        Time.timeScale = 1f;

        if (!menu) return;

        if (startButton)
        {
            // 既存のリスナーを除去してから、確実に張り直す
            startButton.onClick.RemoveAllListeners();
            // MenuController に public な OnClickStartBattleFlow() がある前提
            startButton.onClick.AddListener(menu.OnClickStartBattleFlow);
        }

        if (otherButton)
        {
            otherButton.onClick.RemoveAllListeners();
            otherButton.onClick.AddListener(menu.OnClickOpenOtherScene);
        }
    }
}