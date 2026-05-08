using UnityEngine;
public class DevOnce : MonoBehaviour
{
    void Awake()
    {
        PlayerData.Coins = 100000;
        PlayerPrefs.Save();
        Debug.Log("Coins set to 100000");
        Destroy(this); // 実行後は自分を外す（任意）
    }
}
