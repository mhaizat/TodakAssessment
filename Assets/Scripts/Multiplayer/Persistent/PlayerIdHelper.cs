using UnityEngine;
using System;

/// <summary>
/// Utility for generating and retrieving a persistent unique player ID.
/// Supports editor-specific temporary IDs to avoid collisions in multiple editor instances.
/// </summary>
public static class PlayerIdHelper
{
    private const string PlayerIdKey = "PersistentPlayerId";

    /// <summary>
    /// Returns the saved UniqueId if it exists, or generates a new one.
    /// Editor-only: Uses a temporary per-instance suffix to avoid collisions in multiple editor clients.
    /// </summary>
    public static string GetOrCreatePlayerId()
    {
        string key = PlayerIdKey;

#if UNITY_EDITOR
        // Use a temporary per-editor-instance suffix
        key += "_" + System.Diagnostics.Process.GetCurrentProcess().Id;
#endif

        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetString(key);

        string newId = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(key, newId);
        PlayerPrefs.Save();

        Debug.Log("[PlayerIdHelper] Using Persistent ID: " + newId);
        return newId;
    }

    /// <summary>
    /// Deletes the saved UniqueId (useful for debugging).
    /// </summary>
    public static void ResetPlayerId()
    {
#if UNITY_EDITOR
        string key = PlayerIdKey + "_" + System.Diagnostics.Process.GetCurrentProcess().Id;
#else
        string key = PlayerIdKey;
#endif
        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Debug.Log("[PlayerIdHelper] Player ID has been reset.");
        }
    }
}
