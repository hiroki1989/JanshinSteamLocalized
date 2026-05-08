#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;   // ← 重要：EditorSceneManager
using UnityEngine.SceneManagement;

/// <summary>
/// メニューから即リセットするツール（エディタ専用）。
/// </summary>
public static class ProgressionResetMenu
{
    [MenuItem("Tools/Progression/Reset Progression (PlayerPrefs) %#r")] // Ctrl/Cmd+Shift+R
    public static void ResetProgression()
    {
        PlayerPrefs.DeleteKey("PF_CurrentEnemyIndex");
        PlayerPrefs.DeleteKey("PF_CurrentEnemyName");
        PlayerPrefs.DeleteKey("CurrentEnemyIndex");
        PlayerPrefs.DeleteKey("CurrentEnemyName");

        PlayerPrefs.DeleteKey("PF_SecretHadesRoute");
        PlayerPrefs.DeleteKey("SecretHades_BonusUniqueOmamoriId");
        PlayerPrefs.DeleteKey("PF_AngelDialogueMode");
        PlayerPrefs.DeleteKey("PF_AngelDialogueNextScene");

        PlayerPrefs.Save();

// ★追加：常に「次回全回復」フラグと持ち越しHPのクリアを先に確定
try {
    PlayerPrefs.DeleteKey("Run_PlayerHP");
    PlayerPrefs.SetInt("PF_PendingFullHeal", 1);
    PlayerPrefs.Save();
} catch {}

// ここでHP回復：GameManagerが居れば即時、居なければ次回起動時
try { GameManager.FullHealNowOrNextRun(); } catch { /* no-op（上で確定済み） */ }
        // ここでHP回復：GameManagerが居れば即時、居なければ次回起動時
        try { GameManager.FullHealNowOrNextRun(); } catch
        {
            // GameManagerが無い等で失敗したら、とりあえず次回起動時フラグだけ立てる
            try { PlayerPrefs.SetInt("PF_PendingFullHeal", 1); PlayerPrefs.Save(); } catch {}
        }

        EditorUtility.DisplayDialog("Progression Reset", "進行データをリセットし、HPを全回復しました（または次回開始時に全回復）。", "OK");

        // ★非Play時は EditorSceneManager でシーン再オープン（Awakeを確実に走らせる/画面更新）
        var active = SceneManager.GetActiveScene();
        if (!Application.isPlaying)
        {
            // 同じシーンを開き直し（パス指定）
            var path = active.path; // 例: "Assets/Scenes/Main.unity"
            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }
        else
        {
            // Play中は通常ロードでOK
            SceneManager.LoadScene(active.buildIndex);
        }
    }
}
#endif
