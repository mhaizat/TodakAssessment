using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Responsible for spawning and managing player objects during the networked game session.
/// Only the server spawns normal players; spectators are handled locally.
/// </summary>
public class GameSessionManager : NetworkBehaviour
{
    private const string PlayerPrefabPath = "Player";

    private static GameSessionManager instance;
    public static GameSessionManager Instance => instance;

    private readonly List<Transform> spawnPoints = new();

    // -------------------- UNITY / NETCODE --------------------
    public override void OnNetworkSpawn()
    {
        // Singleton enforcement
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[GameSessionManager] Duplicate detected, destroying this instance.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Collect all spawn points in the scene
        foreach (var obj in GameObject.FindGameObjectsWithTag("SpawnPoint"))
            spawnPoints.Add(obj.transform);

        if (IsServer)
        {
            Debug.Log("[GameSessionManager] OnNetworkSpawn - Spawning all players");
            SpawnAllPlayers();
        }
        else
        {
            Debug.Log("[GameSessionManager] OnNetworkSpawn - Waiting for server to spawn player objects");
        }
    }

    // -------------------- PLAYER SPAWNING --------------------
    /// <summary>
    /// Spawns or restores all players in the session.
    /// </summary>
    private void SpawnAllPlayers()
    {
        var playerPrefab = Resources.Load<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"[GameSessionManager] Could not find {PlayerPrefabPath} in Resources.");
            return;
        }

        // Copy to avoid modifying dictionary during iteration
        var playersSnapshot = new List<LobbyManager.PlayerData>(LobbyManager.Instance.playersByUniqueId.Values);

        int spawnIndex = 0;
        foreach (var pdata in playersSnapshot)
        {
            SpawnOrRestorePlayer(pdata, playerPrefab, spawnIndex);
            spawnIndex++;
        }
    }

    /// <summary>
    /// Spawns or restores a single player object.
    /// </summary>
    /// <param name="pdata">Player data to spawn or restore.</param>
    /// <param name="prefab">Optional prefab reference.</param>
    /// <param name="spawnIndex">Index to select spawn point.</param>
    public void SpawnOrRestorePlayer(LobbyManager.PlayerData pdata, GameObject prefab = null, int spawnIndex = 0)
    {
        if (!IsServer) return; // Only server handles normal players
        if (pdata.IsSpectator) return; // Spectators are local-only

        // Skip duplicates
        if (pdata.PlayerObject != null &&
            pdata.PlayerObject.TryGetComponent(out NetworkObject existingNetObj))
        {
            if (existingNetObj.IsSpawned)
            {
                if (pdata.IsConnected && existingNetObj.OwnerClientId != pdata.ClientId)
                    existingNetObj.ChangeOwnership(pdata.ClientId);

                var move = pdata.PlayerObject.GetComponent<PlayerMovement>();
                if (move != null) move.enabled = pdata.IsConnected;

                return;
            }
        }

        // Load prefab if missing
        if (prefab == null)
            prefab = Resources.Load<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[GameSessionManager] Missing prefab at path {PlayerPrefabPath}");
            return;
        }

        // Select spawn position & rotation
        Transform spawnTransform = spawnPoints.Count > 0 ? spawnPoints[spawnIndex % spawnPoints.Count] : null;
        Vector3 pos = spawnTransform ? spawnTransform.position : Vector3.zero;
        Quaternion rot = spawnTransform ? spawnTransform.rotation : Quaternion.identity;

        // Instantiate and spawn player
        GameObject playerObj = Instantiate(prefab, pos, rot);
        var netObj = playerObj.GetComponent<NetworkObject>();

        if (pdata.IsConnected)
            netObj.SpawnAsPlayerObject(pdata.ClientId);
        else
            netObj.Spawn(); // placeholder for disconnected player

        // Update player data
        pdata.PlayerObject = playerObj;
        LobbyManager.Instance.playersByUniqueId[pdata.PlayerUniqueId] = pdata;

        // Enable or disable movement based on connection
        var controller = playerObj.GetComponent<PlayerMovement>();
        if (controller != null)
            controller.enabled = pdata.IsConnected;
    }
}
