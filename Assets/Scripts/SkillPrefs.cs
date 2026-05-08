using UnityEngine;

public static class SkillPrefs
{
    public const string KeyEquippedActiveSkill = "EquippedActiveSkill";

    // メニュー（装備画面）から呼び出してください。例: SkillPrefs.Equip("RandomMan");
    public static void Equip(string skillEnumName)
    {
        PlayerPrefs.SetString(KeyEquippedActiveSkill, skillEnumName);
        PlayerPrefs.Save();
    }
}
