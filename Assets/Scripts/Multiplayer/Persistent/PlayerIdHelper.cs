using UnityEngine;
using System;

public static class PlayerIdHelper
{
    private const string PersistentIdKey = "PersistentPlayerId";

    public static string GetOrCreatePlayerId()
    {
        if (PlayerPrefs.HasKey(PersistentIdKey))
        {
            string existingId = PlayerPrefs.GetString(PersistentIdKey);
            Debug.Log($"[PlayerIdHelper] Using stored persistent ID: {existingId}");
            return existingId;
        }

        string newId = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(PersistentIdKey, newId);
        PlayerPrefs.Save();
        Debug.Log($"[PlayerIdHelper] Generated and stored new persistent ID: {newId}");
        return newId;
    }

    public static void ClearStoredPlayerId()
    {
        if (PlayerPrefs.HasKey(PersistentIdKey))
        {
            PlayerPrefs.DeleteKey(PersistentIdKey);
            Debug.Log("[PlayerIdHelper] Cleared stored player ID.");
        }
    }
}
