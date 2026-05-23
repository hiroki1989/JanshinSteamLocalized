using System.Collections;
using UnityEngine;

/// <summary>
/// RunScene にアタッチするブートストラップ。
/// GameManager の InitMissionUI() を安全に呼び出し、
/// スコアリング段階表示の完了後にミッション達成パネルを表示するフックを接続する。
/// 
/// ★既存の RunSceneUILayout_AutoGenBootstrap と同じシーンに配置してください。
/// </summary>
public class MissionBootstrap : MonoBehaviour
{
    [Tooltip("GameManager の出現を待つ最大時間（秒）")]
    [SerializeField] private float waitTimeout = 5f;

    private IEnumerator Start()
    {
        // GameManager が生成されるまで待つ
        float t = 0f;
        GameManager gm = null;
        while (t < waitTimeout)
        {
            gm = FindObjectOfType<GameManager>();
            if (gm != null) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }

        if (gm == null) yield break;

        // ミッション初期化
        try { gm.InitMissionUI(); } catch (System.Exception e) { Debug.LogWarning("[MissionBootstrap] Init error: " + e); }
    }
}
