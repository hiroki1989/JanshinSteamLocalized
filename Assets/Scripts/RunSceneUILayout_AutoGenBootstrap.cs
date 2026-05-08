using System.Collections;
using UnityEngine;

/// <summary>
/// RunScene の UI 自動初期化ブートストラップ。
/// 既存仕様は維持したまま、GameManager が破棄済み／未生成のタイミングで
/// SendMessage を投げて MissingReferenceException が出る問題を防ぐため、
/// ① GameManager の出現を待機、② null/破棄チェック、③ 例外を握りつぶす
/// の3点のみを追加しています（メソッド名等の仕様は変更しません）。
/// </summary>
public class RunSceneUILayout_AutoGenBootstrap : MonoBehaviour
{
    [Header("Bootstrap")]
    [Tooltip("GameManager の生成を待つ最大時間（秒）")]
    [SerializeField] private float waitTimeout = 5f;

    [Tooltip("OnStart 時に GameManager へ送るメッセージ名（既存の値をそのまま使う想定）。空は無視。")]
    [SerializeField] private string onStartMessage1 = "OnUILoaded";
    [SerializeField] private string onStartMessage2 = "";
    [SerializeField] private string onStartMessage3 = "";

    private static bool IsAlive(Object o)
    {
        // Unity の Destroy 後にも参照が残るケースに対応
        return o != null && !ReferenceEquals(o, null);
    }

    private IEnumerator Start()
    {
        // 1) GameManager が生成されるまで待つ（最大 waitTimeout 秒）
        float t = 0f;
        GameManager gm = null;
        while (t < waitTimeout)
        {
            gm = FindObjectOfType<GameManager>();
            if (IsAlive(gm)) break;
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        if (!IsAlive(gm))
        {
            // 見つからなければ何もせず安全に終了（例外は出さない）
            yield break;
        }

        // 2) メッセージ送信（破棄/入れ替えに備え、直前にも生存確認）
        TrySend(gm, onStartMessage1);
        TrySend(gm, onStartMessage2);
        TrySend(gm, onStartMessage3);
    }

    private void TrySend(GameManager gm, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (!IsAlive(gm)) return;

        try
        {
            gm.SendMessage(msg, SendMessageOptions.DontRequireReceiver);
        }
        catch (System.MissingMethodException) { /* 受け手が無くても落とさない */ }
        catch (MissingReferenceException) { /* 破棄直後でも落とさない */ }
        catch (System.Exception)
        {
            // 想定外でもクラッシュは回避（ログは開発時のみ）
#if UNITY_EDITOR
            Debug.LogWarning($"RunSceneUILayout_AutoGenBootstrap: SendMessage('{msg}') failed but ignored.");
#endif
        }
    }
}