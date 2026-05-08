using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AutoResumeRouter : MonoBehaviour
{
    [SerializeField] private string resumeSceneName = "RunScene"; // 必要ならInspectorで変更

void Awake()
{
    try
    {
        // 中断再開フラグを「消費」してから遷移（＝一度きり）
        if (PlayerPrefs.GetInt("PF_ResumeDirect", 0) == 1)
        {
            var target = PlayerPrefs.GetString("PF_ResumeScene", resumeSceneName);

            // ★ここで消費（次回以降は会話をスキップしない）
            PlayerPrefs.SetInt("PF_ResumeDirect", 0);
            PlayerPrefs.DeleteKey("PF_ResumeScene");
            PlayerPrefs.Save();

            SceneManager.LoadScene(string.IsNullOrEmpty(target) ? resumeSceneName : target);
        }
    }
    catch { /* 何があっても落とさない */ }
}

}
