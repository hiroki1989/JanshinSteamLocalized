using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
//  First-Match Tutorial (non-invasive add-on)
//  ------------------------------------------------------------------------
//  仕様：
//   - 初めて対局を始めたときだけ表示される。
//   - 配牌が配られ、敵が捨て牌を切る前のタイミングで進行を一度停止し、
//     チュートリアルが表示される。
//   - Inspector で指定したパネル（GameObject）を、指定した順番で1枚ずつ表示。
//     パネルの数は Inspector で自由に設定できる。
//   - 各パネル上の「次へ」ボタンを押すと、次のパネルが表示される。
//   - 最後のパネルの「チュートリアル終了」ボタンを押すと、チュートリアルが終了し
//     元の進行（敵ターン）が再開する。
//   - 次回以降はもう表示されない（PlayerPrefs に記録）。
//   - パネルは手動でヒエラルキー上に作成し、ここで Inspector に登録する。
//
//  使い方（ヒエラルキー側の準備）：
//   1. チュートリアル用のパネルを必要な枚数だけ手動で作成する（最初は非アクティブ推奨）。
//   2. 各パネルの中に「次へ」ボタンを置く（最後のパネルは「チュートリアル終了」ボタン）。
//   3. GameManager の Inspector の "First Match Tutorial" セクションで、
//      ・Tutorial Panels に表示したいパネルを「表示したい順番」で登録
//      ・各 Tutorial Panel Entry の Next Button に、そのパネルの「次へ」ボタンを割り当てる
//        （最後のパネルのボタンは「チュートリアル終了」ボタンになる）
//   4. 任意で、暗転用の Tutorial Dim Root を割り当てる。
// ============================================================================

public partial class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialPanelEntry
    {
        [Tooltip("表示するパネル本体（ヒエラルキー上で手動作成したもの）。")]
        public GameObject panel;

        [Tooltip("このパネル上の『次へ』ボタン。最後のパネルでは『チュートリアル終了』ボタンを割り当てる。")]
        public Button nextButton;
    }

    [Header("First Match Tutorial")]
    [Tooltip("チュートリアル機能全体の有効/無効。")]
    [SerializeField] private bool tutorialEnabled = true;

    [Tooltip("初めて対局を始めたときだけ表示するパネル群。リストの上から順番に表示される。数は自由。")]
    [SerializeField] private List<TutorialPanelEntry> tutorialPanels = new List<TutorialPanelEntry>();

    [Tooltip("任意：チュートリアル中に表示する暗転（背景）ルート。未指定でも動作する。")]
    [SerializeField] private GameObject tutorialDimRoot;

    [Tooltip("チュートリアル表示済みかどうかを保存する PlayerPrefs キー。")]
    [SerializeField] private string tutorialDoneKey = "FirstMatchTutorialDoneV1";

    // 進行中フラグ（多重起動防止）
    private bool _tutorialRunning = false;

    // ----------------------------------------------------------------------
    //  表示すべきかどうか（初回のみ true）
    // ----------------------------------------------------------------------
    private bool __ShouldShowFirstMatchTutorial()
    {
        if (!tutorialEnabled) return false;
        if (_tutorialRunning) return false;
        if (tutorialPanels == null || tutorialPanels.Count == 0) return false;

        try
        {
            if (PlayerPrefs.GetInt(tutorialDoneKey, 0) != 0)
                return false;
        }
        catch
        {
            // PlayerPrefs 読み取り失敗時は、念のため表示しない（暴発防止）
            return false;
        }

        return true;
    }

    // ----------------------------------------------------------------------
    //  チュートリアル本体
    //  進行を _freezeProgression で止め、パネルを順番に表示する。
    //  全パネル完了後に進行を再開（フラグを戻す）。
    // ----------------------------------------------------------------------
    private IEnumerator __RunFirstMatchTutorial_Co()
    {
        if (_tutorialRunning) yield break;
        _tutorialRunning = true;

        // ★進行を停止（敵ターン等が割り込まないように）
        bool prevFreeze = _freezeProgression;
        _freezeProgression = true;

        // 操作ボタン類を無効化（任意・存在すれば）
        try { UpdateButtons(); } catch { }

        // 暗転ON
        if (tutorialDimRoot != null)
            tutorialDimRoot.SetActive(true);

        // まず全パネルを非表示にしておく
        for (int i = 0; i < tutorialPanels.Count; i++)
        {
            var e = tutorialPanels[i];
            if (e != null && e.panel != null)
                e.panel.SetActive(false);
        }

        // パネルを順番に表示
        for (int i = 0; i < tutorialPanels.Count; i++)
        {
            var entry = tutorialPanels[i];
            if (entry == null || entry.panel == null)
                continue;

            entry.panel.SetActive(true);

            // このパネルの「次へ／チュートリアル終了」ボタンが押されるまで待つ
            bool advanced = false;

            Button btn = entry.nextButton;
            if (btn != null)
            {
                btn.onClick.RemoveListener(__TutorialNoop); // 念のため
                System.Action onClick = () => advanced = true;
                UnityEngine.Events.UnityAction ua = () => advanced = true;
                btn.onClick.AddListener(ua);

                // 待機（TimeScale に依存しないように毎フレーム待つ）
                while (!advanced)
                    yield return null;

                btn.onClick.RemoveListener(ua);
            }
            else
            {
                // ボタン未割り当てのパネルは、画面タップで進める保険
                while (!__TutorialAnyTapDown())
                    yield return null;

                // タップが次パネルへ連続貫通しないよう1フレーム空ける
                yield return null;
            }

            // このパネルを閉じてから次へ
            entry.panel.SetActive(false);
        }

        // 暗転OFF
        if (tutorialDimRoot != null)
            tutorialDimRoot.SetActive(false);

        // ★表示済みを記録（次回以降は出さない）
        try
        {
            PlayerPrefs.SetInt(tutorialDoneKey, 1);
            PlayerPrefs.Save();
        }
        catch { }

        // ★進行を再開
        _freezeProgression = prevFreeze;
        try { UpdateButtons(); } catch { }

        _tutorialRunning = false;
    }

    private void __TutorialNoop() { }

    // 画面タップ検出（ボタン未割り当てパネル用の保険）
    private bool __TutorialAnyTapDown()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        }
        catch { }
        return false;
#else
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) return true;
        return false;
#endif
    }
}
