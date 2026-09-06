using System;
using System.Collections.Generic;
using UnityEngine;
// Balance and processed transaction IDs are saved in the same record.
public static class GemWallet
{
    const string Key = "SP_WalletV1";
    [Serializable] public sealed class State {
        public int balance;
        public List<string> transactions = new List<string>();
        public bool Grant(string transaction, int amount) {
            if (string.IsNullOrWhiteSpace(transaction) || amount <= 0) throw new ArgumentException("Invalid grant");
            if (transactions.Contains(transaction)) return false;
            balance = checked(balance + amount); transactions.Add(transaction); return true;
        }
    }
    public static event Action Changed;
    static State Load() {
        if (!PlayerPrefs.HasKey(Key)) return new State { balance = Mathf.Max(0, PlayerPrefs.GetInt("SP_Gems", 0)) };
        var state = JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key));
        if (state == null || state.transactions == null || state.balance < 0) throw new InvalidOperationException("Invalid gem wallet");
        return state;
    }
    static void Save(State state) {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(state)); PlayerPrefs.Save();
        if (Changed != null) foreach (Action callback in Changed.GetInvocationList())
            try { callback(); } catch (Exception e) { Debug.LogException(e); }
    }
    public static int Balance => Load().balance;
    public static void Set(int amount) { var state = Load(); state.balance = Mathf.Max(0, amount); Save(state); }
    public static void Add(int amount) {
        if (amount <= 0) return;
        var state = Load(); state.balance = checked(state.balance + amount); Save(state);
    }
    public static bool GrantPurchase(string transaction, int amount) {
        var state = Load(); if (!state.Grant(transaction, amount)) return false; Save(state); return true;
    }
}
