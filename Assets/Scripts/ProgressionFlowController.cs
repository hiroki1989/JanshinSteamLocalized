using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ProgressionFlowController : MonoBehaviour
{
    public static ProgressionFlowController Instance { get; private set; }

[Header("Scene Names")]
[SerializeField] private string menuScene = "Menu";
[SerializeField] private string tierSelectScene = "TierSelectScene";
[SerializeField] private string angelConversationScene = "AngelDialogue";
[SerializeField] private string enemyConversationScene = "EnemyDialogue";
[SerializeField] private string battleScene = "RunScene";
[SerializeField] private string upgradeScene = "Upgrade";
[SerializeField] private string rewardScene = "StageClear";
    [Header("Behavior")]
    [SerializeField] private bool loopAfterLastEnemy = true;
    private const string KeyCurrentEnemyIndex = "PF_CurrentEnemyIndex";
    private const string KeyCurrentEnemyName  = "PF_CurrentEnemyName";
    private const string KeyCompatIndex = "CurrentEnemyIndex";
    private const string KeyCompatName  = "CurrentEnemyName";
private const string KeySecretHadesRoute = "PF_SecretHadesRoute";
private const string KeySecretMode = "PF_AngelModeSecret";
private const string SecretMode_Intro = "SecretHadesStart";
private const string SecretMode_Clear = "SecretHadesClear";

    // AngelDialogue 用（会話モードと、会話後の遷移先）
    private const string KeyAngelMode     = "PF_AngelDialogueMode";     // "Start" / "Defeat" / "Clear"
    private const string KeyAngelNextScene = "PF_AngelDialogueNextScene"; // 例："EnemyDialogue" / "StageClear"

    private string[] _enemyNamesCache;

    public static int CurrentEnemyIndex { get; private set; } = 0;
    public static string CurrentEnemyName { get; private set; } = "";
public static void ForceResetToFirstEnemy()
{
    // 1) 先頭インデックスと名前を決定
    string[] names = null;
    try { if (Instance != null) names = Instance.GetEnemyNames(); } catch {}
    int idx = 0;
    string nm = (names != null && names.Length > 0) ? names[0] : "";

    CurrentEnemyIndex = idx;
    CurrentEnemyName  = nm;

    // 2) 進行状態を PlayerPrefs に保存（SaveProgressionState と同等）
    try
    {
        PlayerPrefs.SetInt("PF_CurrentEnemyIndex", CurrentEnemyIndex);
        PlayerPrefs.SetString("PF_CurrentEnemyName", CurrentEnemyName ?? "");
        PlayerPrefs.SetInt("CurrentEnemyIndex", CurrentEnemyIndex);         // 互換キー
        PlayerPrefs.SetString("CurrentEnemyName", CurrentEnemyName ?? "");  // 互換キー
        PlayerPrefs.Save();
    }
    catch {}

    // 3) 新規対局開始：HP/MPは最大まで全回復させたいので、フラグを立てる
    try { PlayerPrefs.SetInt("PF_PendingFullHeal", 1); PlayerPrefs.Save(); } catch {}

    // 4) 新規対局開始なので、前ラン持ち越しのRun_*は掃除（不整合防止）
    try
    {
        PlayerPrefs.DeleteKey("Run_PlayerHP");
        PlayerPrefs.DeleteKey("Run_PlayerMP");

        // ★追加：敵HPの退避キーも新規Runでは必ず破棄
        PlayerPrefs.DeleteKey("Run_EnemyHP");
        PlayerPrefs.DeleteKey("Run_EnemyMaxHP");

        PlayerPrefs.Save();
    }
    catch {}

    // ★追加：会話/報酬フローの残骸を確実に消す（残ると「敗北天使→報酬」に飛ぶ）
    try
    {
        PlayerPrefs.DeleteKey("PF_AngelDialogueMode");
        PlayerPrefs.DeleteKey("PF_AngelDialogueNextScene");
        PlayerPrefs.DeleteKey("AngelDialogueScene");
        PlayerPrefs.DeleteKey("EnemyDialogueScene");

        PlayerPrefs.DeleteKey("PF_SecretHadesRoute");
        PlayerPrefs.DeleteKey("PF_AngelModeSecret");

        PlayerPrefs.Save();
    }
    catch {}

    // 5) 即座に GameManager 側へ同期（可能なら）
    try { if (Instance != null) Instance.TrySyncEnemyToGameManager(); } catch {}
}
    public void GoFromMenuToTierSelect()
    {
        LoadSceneSafe(tierSelectScene);
    }
public static void ForceSetCurrentEnemyIndex(int runtimeIndex)
{
    string[] names = null;
    try { if (Instance != null) names = Instance.GetEnemyNames(); } catch {}

    int maxIdx = 0;
    if (names != null && names.Length > 0) maxIdx = names.Length - 1;

    int idx = Mathf.Clamp(runtimeIndex, 0, Mathf.Max(0, maxIdx));
    string nm = (names != null && idx >= 0 && idx < names.Length) ? names[idx] : "";

    CurrentEnemyIndex = idx;
    CurrentEnemyName  = nm;

    try
    {
        PlayerPrefs.SetInt("PF_CurrentEnemyIndex", CurrentEnemyIndex);
        PlayerPrefs.SetString("PF_CurrentEnemyName", CurrentEnemyName ?? "");
        PlayerPrefs.SetInt("CurrentEnemyIndex", CurrentEnemyIndex);
        PlayerPrefs.SetString("CurrentEnemyName", CurrentEnemyName ?? "");
        PlayerPrefs.Save();
    }
    catch {}

    try { PlayerPrefs.SetInt("PF_PendingFullHeal", 1); PlayerPrefs.Save(); } catch {}

    try
    {
        PlayerPrefs.DeleteKey("Run_PlayerHP");
        PlayerPrefs.DeleteKey("Run_PlayerMP");
        PlayerPrefs.Save();
    }
    catch {}

    try { if (Instance != null) Instance.TrySyncEnemyToGameManager(); } catch {}
}
void Awake()
{
    if (Instance != null && Instance != this)
    {
        // 既存の常駐インスタンスを保持し、このシーンで生成された重複インスタンスは破棄する
        Destroy(gameObject);
        return;
    }

    Instance = this;
    DontDestroyOnLoad(gameObject);

    // ★シーン遷移でインスタンスが作り直されても、敵Indexが0に戻らないように復元する
    try { LoadProgressionState(); } catch {}
    try { EnsureEnemyNameInSync(); } catch {}
    try { TrySyncEnemyToGameManager(); } catch {}
}
void OnDestroy()
{
    if (Instance == this) Instance = null;
}
public void GoFromMenuToAngelConversation()
{
    // ★追加：中断データ（自動セーブ含む）があるなら、会話をスキップしてRunへ直行
    try
    {
        if (PlayerPrefs.GetInt("Run_HasSuspend", 0) == 1)
        {
            // GameManager.SaveSuspendSnapshot() が入れている直行先があればそれを優先
            string resumeScene = "";
            try { resumeScene = PlayerPrefs.GetString("PF_ResumeScene", ""); } catch { resumeScene = ""; }

            if (!string.IsNullOrEmpty(resumeScene))
            {
                LoadSceneSafe(resumeScene);
            }
            else
            {
                // フォールバック：ProgressionFlowController の battleScene
                LoadSceneSafe(battleScene);
            }
            return;
        }
    }
    catch {}

    // 「開始」会話：会話後は EnemyDialogue へ（従来）
    PlayerPrefs.SetString(KeyAngelMode, "Start");
    PlayerPrefs.SetString(KeyAngelNextScene, enemyConversationScene);
    PlayerPrefs.Save();

    LoadSceneSafe(angelConversationScene);
}
public void StartNewRunFromMenu()
{
    // ★敗北時と同等のリセット（ローグライト要素を破棄）
    try { StageClearManager.ResetEnemyProgressionNow(); } catch {}

    // デッキも初期化（既に StageClearManager.OnClickRewardOK でもやっているのと同じ）
    try { PlayerData.ResetDeckToDefault(); } catch {}

    // 開始フローへ
    GoFromMenuToAngelConversation();
}
    public void GoFromAngelToEnemyConversation()
    {
        EnsureEnemyNameInSync();
        TrySyncEnemyToGameManager();
        LoadSceneSafe(enemyConversationScene);
    }

    public void GoFromEnemyConversationToBattle()
    {
        EnsureEnemyNameInSync();
        TrySyncEnemyToGameManager();
        LoadSceneSafe(battleScene);
    }

    // ★変更: 勝利→強化画面遷移時にインタースティシャル広告を挟む
    public void GoFromBattleWinToUpgrade()
    {
        var adMgr = InterstitialAdManager.Instance;
        if (adMgr != null)
        {
            adMgr.ShowAdIfReady(() => LoadSceneSafe(upgradeScene));
        }
        else
        {
            LoadSceneSafe(upgradeScene);
        }
    }
    public void GoFromBattleLoseToReward()
    {
        // 「敗北」会話：会話後は 報酬(StageClear) へ
        PlayerPrefs.SetString(KeyAngelMode, "Defeat");
        PlayerPrefs.SetString(KeyAngelNextScene, rewardScene);
        PlayerPrefs.Save();

        LoadSceneSafe(angelConversationScene);
    }
    public void GoFromBattleClearToRewardViaAngel()
    {
        // 「クリア」会話：会話後は 報酬(StageClear) へ
        PlayerPrefs.SetString(KeyAngelMode, "Clear");
        PlayerPrefs.SetString(KeyAngelNextScene, rewardScene);
        PlayerPrefs.Save();

        LoadSceneSafe(angelConversationScene);
    }

    public void GoFromAngelToReward()
    {
        LoadSceneSafe(rewardScene);
    }
    // ===== 裏ボス（ハーデス）導線：ゼウス役満撃破→天使導入会話 =====
    public void GoFromZeusClearToSecretAngelIntro()
    {
        PlayerPrefs.SetString(KeyAngelMode, "SecretHadesIntro");
        PlayerPrefs.SetString(KeyAngelNextScene, enemyConversationScene);
        PlayerPrefs.Save();

        LoadSceneSafe(angelConversationScene);
    }

    // ===== 裏ボス（ハーデス）導線：天使導入会話→ハーデス会話へ（敵Index=10固定） =====
    public void GoFromSecretAngelIntroToHadesEnemyConversation()
    {
        CurrentEnemyIndex = 10;

        try
        {
            if (EnemyConfigExcel.TryGetForRuntimeIndex(10, out var cfg) && cfg != null && !string.IsNullOrEmpty(cfg.name))
                CurrentEnemyName = cfg.name;
            else
                CurrentEnemyName = "";
        }
        catch { CurrentEnemyName = ""; }

        try
        {
            PlayerPrefs.SetInt("PF_CurrentEnemyIndex", CurrentEnemyIndex);
            PlayerPrefs.SetString("PF_CurrentEnemyName", CurrentEnemyName ?? "");
            PlayerPrefs.SetInt("CurrentEnemyIndex", CurrentEnemyIndex);
            PlayerPrefs.SetString("CurrentEnemyName", CurrentEnemyName ?? "");
            PlayerPrefs.Save();
        }
        catch {}

        TrySyncEnemyToGameManager();
        LoadSceneSafe(enemyConversationScene);
    }

    // ===== 裏ボス（ハーデス）導線：ハーデス撃破→天使クリア会話 =====
    public void GoFromSecretHadesClearToSecretAngelClear()
    {
        PlayerPrefs.SetString(KeyAngelMode, "SecretHadesClear");
        PlayerPrefs.SetString(KeyAngelNextScene, rewardScene);
        PlayerPrefs.Save();

        LoadSceneSafe(angelConversationScene);
    }

    public void GoFromUpgradeToNextEnemyConversation()
    {
        AdvanceToNextEnemy();
        EnsureEnemyNameInSync();
        TrySyncEnemyToGameManager();
        LoadSceneSafe(enemyConversationScene);
    }

    public void ForceAdvanceAndGoToNextEnemyConversation()
    {
        AdvanceToNextEnemy();
        EnsureEnemyNameInSync();
        TrySyncEnemyToGameManager();
        LoadSceneSafe(enemyConversationScene);
    }

public void GoToEnemyDialogueForSecretHades()
{
    // 裏ボスは runtime index を 10 に固定して読み出す（Excel Key=10）
    try
    {
        PlayerPrefs.SetInt(KeyCurrentEnemyIndex, EnemyConfigExcel.SecretBossExcelKey);
        PlayerPrefs.Save();
    }
    catch {}

    string enemyTalk = PlayerPrefs.GetString("EnemyDialogueScene", "");
    if (!string.IsNullOrEmpty(enemyTalk))
    {
        SafeSceneLoader.Load(enemyTalk);
        return;
    }

    SafeSceneLoader.Load("RunScene");
}

private void AdvanceToNextEnemy()
{
    var names = GetEnemyNames();
    if (names == null || names.Length == 0) return;

    int next = CurrentEnemyIndex + 1;

    // ← ここでクリア（末尾→先頭ループ）を検知してフラグON
    bool clearedThisRun = (next >= names.Length);

    if (next >= names.Length) next = loopAfterLastEnemy ? 0 : names.Length - 1;

    CurrentEnemyIndex = Mathf.Clamp(next, 0, names.Length - 1);
    CurrentEnemyName  = names[CurrentEnemyIndex];
    SaveProgressionState();

    // 周回先頭に戻った＝一度ゲームが終わった扱い → 次回開始時に全回復
    if (clearedThisRun && loopAfterLastEnemy)
    {
        // GameManagerを参照せずに安全にフラグだけ立てる
        try { PlayerPrefs.SetInt("PF_PendingFullHeal", 1); PlayerPrefs.Save(); } catch {}
    }
}


    private void EnsureEnemyNameInSync()
    {
        var names = GetEnemyNames();
        if (names == null || names.Length == 0)
        {
            CurrentEnemyIndex = 0;
            CurrentEnemyName  = "";
            SaveProgressionState();
            return;
        }
        if (CurrentEnemyIndex < 0 || CurrentEnemyIndex >= names.Length)
            CurrentEnemyIndex = 0;
        CurrentEnemyName = names[CurrentEnemyIndex];
        SaveProgressionState();
    }

    private void SaveProgressionState()
    {
        PlayerPrefs.SetInt(KeyCurrentEnemyIndex, CurrentEnemyIndex);
        PlayerPrefs.SetString(KeyCurrentEnemyName, CurrentEnemyName ?? "");
        PlayerPrefs.SetInt(KeyCompatIndex, CurrentEnemyIndex);
        PlayerPrefs.SetString(KeyCompatName, CurrentEnemyName ?? "");
        PlayerPrefs.Save();
    }

    private void LoadProgressionState()
    {
        if (PlayerPrefs.HasKey(KeyCurrentEnemyIndex))
        {
            CurrentEnemyIndex = PlayerPrefs.GetInt(KeyCurrentEnemyIndex, 0);
            CurrentEnemyName  = PlayerPrefs.GetString(KeyCurrentEnemyName, "");
        }
        else
        {
            CurrentEnemyIndex = PlayerPrefs.GetInt(KeyCompatIndex, 0);
            CurrentEnemyName  = PlayerPrefs.GetString(KeyCompatName, "");
        }
    }
private void LoadSceneSafe(string sceneName)
{
    if (string.IsNullOrEmpty(sceneName))
    {
        Debug.LogError("[ProgressionFlow] Scene name is empty. Assign it in inspector.");
        return;
    }

    // 対局シーンから別シーンへ移る直前に、
    // GameManager 側の演出・コルーチン・UIを安全に止める
    try
    {
        var gms = GameObject.FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < gms.Length; i++)
        {
            var mb = gms[i];
            if (mb == null) continue;
            var t = mb.GetType();
            if (t == null) continue;
            if (t.Name != "GameManager") continue;

            try
            {
                mb.SendMessage("__PrepareForSceneUnload", SendMessageOptions.DontRequireReceiver);
            }
            catch
            {
            }
        }
    }
    catch
    {
    }

    SafeSceneLoader.Load(sceneName);
}
    private void TrySyncEnemyToGameManager()
    {
        try
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>(true))
            {
                var t = mb.GetType();
                if (t.Name != "GameManager") continue;

                var setIdx = t.GetMethod("SetCurrentEnemyIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (setIdx != null && setIdx.GetParameters().Length == 1)
                {
                    if (setIdx.IsStatic) setIdx.Invoke(null, new object[] { CurrentEnemyIndex });
                    else setIdx.Invoke(mb, new object[] { CurrentEnemyIndex });
                }
                else
                {
                    var f = t.GetField("CurrentEnemyIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (f != null && (f.FieldType == typeof(int))) 
                    {
                        if (f.IsStatic) f.SetValue(null, CurrentEnemyIndex);
                        else f.SetValue(mb, CurrentEnemyIndex);
                    }
                    var p = t.GetProperty("CurrentEnemyIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (p != null && p.CanWrite && p.PropertyType == typeof(int))
                    {
                        if (p.GetSetMethod(true).IsStatic) p.SetValue(null, CurrentEnemyIndex, null);
                        else p.SetValue(mb, CurrentEnemyIndex, null);
                    }
                }

                var setName = t.GetMethod("SetCurrentEnemyName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (setName != null && setName.GetParameters().Length == 1)
                {
                    if (setName.IsStatic) setName.Invoke(null, new object[] { CurrentEnemyName });
                    else setName.Invoke(mb, new object[] { CurrentEnemyName });
                }
                else
                {
                    var fn = t.GetField("CurrentEnemyName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (fn != null && (fn.FieldType == typeof(string)))
                    {
                        if (fn.IsStatic) fn.SetValue(null, CurrentEnemyName);
                        else fn.SetValue(mb, CurrentEnemyName);
                    }
                    var pn = t.GetProperty("CurrentEnemyName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (pn != null && pn.CanWrite && pn.PropertyType == typeof(string))
                    {
                        if (pn.GetSetMethod(true).IsStatic) pn.SetValue(null, CurrentEnemyName, null);
                        else pn.SetValue(mb, CurrentEnemyName, null);
                    }
                }

                var scale = t.GetMethod("ApplyEnemyScalingForIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (scale != null && scale.GetParameters().Length >= 1)
                {
                    if (scale.IsStatic) scale.Invoke(null, new object[] { CurrentEnemyIndex });
                    else scale.Invoke(mb, new object[] { CurrentEnemyIndex });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ProgressionFlow] GameManager sync failed: {e.Message}");
        }
    }
public string[] GetEnemyNames()
{
    var all = EnemyConfigExcel.LoadAll(); // Excel only
    var names = new List<string>();

    // Excelの EnemyIndex（キー）昇順で取り出す
    var keys = new List<int>(all.Keys);
    keys.Sort();
    foreach (var k in keys)
    {
        var v = all[k];
        if (v != null && !string.IsNullOrEmpty(v.name)) names.Add(v.name);
    }

    // ★重要：
    // 裏ボスのハーデスは runtime index 10 を使う。
    // ここで10件に切り詰めると index=10 が範囲外になり、
    // EnsureEnemyNameInSync() で 0 に戻されてしまう。
    return names.ToArray();
}
    private static string[] TryReadNamesFromType(Type gmType)
    {
        var f = gmType.GetField("EnemyNames", BindingFlags.Public | BindingFlags.Static);
        if (f != null)
        {
            if (f.FieldType == typeof(string[])) return f.GetValue(null) as string[];
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType))
            {
                var e = f.GetValue(null) as System.Collections.IEnumerable;
                if (e != null)
                {
                    var list = new List<string>();
                    foreach (var x in e) if (x != null) list.Add(x.ToString());
                    if (list.Count > 0) return list.ToArray();
                }
            }
        }
        var p = gmType.GetProperty("EnemyNames", BindingFlags.Public | BindingFlags.Static);
        if (p != null)
        {
            var v = p.GetValue(null, null);
            if (v is string[] arr) return arr;
            if (v is System.Collections.IEnumerable en)
            {
                var list = new List<string>();
                foreach (var x in en) if (x != null) list.Add(x.ToString());
                if (list.Count > 0) return list.ToArray();
            }
        }
        return null;
    }

    private static string[] TryReadNamesFromInstance(object gmInstance)
    {
        var t = gmInstance.GetType();

        var f = t.GetField("EnemyNames", BindingFlags.Public | BindingFlags.Instance);
        if (f != null)
        {
            if (f.FieldType == typeof(string[])) return f.GetValue(gmInstance) as string[];
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(f.FieldType))
            {
                var e = f.GetValue(gmInstance) as System.Collections.IEnumerable;
                if (e != null)
                {
                    var list = new List<string>();
                    foreach (var x in e) if (x != null) list.Add(x.ToString());
                    if (list.Count > 0) return list.ToArray();
                }
            }
        }

        var p = t.GetProperty("EnemyNames", BindingFlags.Public | BindingFlags.Instance);
        if (p != null)
        {
            var v = p.GetValue(gmInstance, null);
            if (v is string[] arr) return arr;
            if (v is System.Collections.IEnumerable en)
            {
                var list = new List<string>();
                foreach (var x in en) if (x != null) list.Add(x.ToString());
                if (list.Count > 0) return list.ToArray();
            }
        }

        return null;
    }

    public static string GetCurrentEnemyName() => CurrentEnemyName;
    public static int GetCurrentEnemyIndex() => CurrentEnemyIndex;
}
