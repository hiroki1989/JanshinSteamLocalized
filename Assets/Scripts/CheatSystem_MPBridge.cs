
using System.Collections.Generic;
using UnityEngine;

// Bridge wrapper around the legacy CheatSystem so you can reuse its logic as "skills" without charges.
// Attach nothing; called from GameManager via BuildActiveSkillApplyFunc().
public static class CheatSystem_MPBridge
{
    // Example: re-roll selected tiles using legacy CheatSystem logic, but without its charge system.
    // Replace Deck/Hand/Tile with your project's concrete types if different.
    public static bool RerollSelected_NoCharge(object deck, object hand, List<object> selected)
    {
        // This is a placeholder to avoid compile-time dependency on your internal types.
        // Implement the actual reroll using your project's API.
        Debug.Log("[CheatBridge] RerollSelected_NoCharge called (implement with your Deck/Hand types).");
        return false;
    }
}
