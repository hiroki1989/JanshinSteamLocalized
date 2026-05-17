using System.Collections;
using UnityEngine;

public class EnemyConfigBootstrap : MonoBehaviour
{
    private const string RunnerName = "EnemyExcelBootstrap";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        // ★多重生成ガード：既にランナーがあれば作らない
        var existing = GameObject.Find(RunnerName);
        if (existing != null) return;

        var go = new GameObject(RunnerName);
        Object.DontDestroyOnLoad(go);
        go.AddComponent<EnemyConfigBootstrap>();
    }

    private IEnumerator Start()
    {
        // 他の初期化完了を1フレ待つ
        yield return null;

        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null) { Destroy(gameObject); yield break; }

        // ★修正：中断復帰中はスナップショットの状態を優先する（HP/デッキを上書きしない）
        try
        {
            if (PlayerPrefs.GetInt("Run_HasSuspend", 0) == 1)
            {
                Destroy(gameObject);
                yield break;
            }
        }
        catch { }

        // ★修正：GameManager.Awake() で既に Excel 適用済みなら二重適用しない
        try
        {
            var field = typeof(GameManager).GetField("_excelEnemyApplied",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && (bool)field.GetValue(gm))
            {
                Destroy(gameObject);
                yield break;
            }
        }
        catch { }

        // 現在の敵インデックスの取得（どちらか取れた方）
        int idx = 0;
        try
        {
            var t = typeof(ProgressionFlowController);
            var f = t.GetField("CurrentEnemyIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (f != null) idx = Mathf.Max(0, (int)f.GetValue(null));
        }
        catch { }
        try { idx = Mathf.Max(idx, PlayerData.CurrentEnemy); } catch { }

        if (EnemyConfigExcel.TryGetForRuntimeIndex(idx, out var cfg))
        {
            gm.ApplyExcelEnemyConfig(cfg);
            Debug.Log($"[EnemyConfig] Excel applied (rowKey={EnemyConfigExcel.MapRuntimeIndexToExcelKey(idx)})");
        }
        else
        {
            Debug.LogError($"[EnemyConfig] Excel row NOT found (runtimeIdx={idx}, expectedKey={EnemyConfigExcel.MapRuntimeIndexToExcelKey(idx)})");
        }
        Destroy(gameObject);
    }
}
