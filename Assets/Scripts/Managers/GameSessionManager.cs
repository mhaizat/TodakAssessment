using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameSessionManager : NetworkBehaviour
{
    private const string PlayerPrefabPath = "Player";
    private static GameSessionManager instance;
    public static GameSessionManager Instance => instance;

    private List<Transform> spawnPoints = new List<Transform>();

    public override void OnNetworkSpawn()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[GameSessionManager] Duplicate detected, destroying this instance.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        foreach (var obj in GameObject.FindGameObjectsWithTag("SpawnPoint"))
            spawnPoints.Add(obj.transform);

        if (IsServer)
        {
            Debug.Log("[GameSessionManager] OnNetworkSpawn - Spawning all players");
            SpawnAllPlayers();
        }
        else
        {
            Debug.Log("[GameSessionManager] OnNetworkSpawn - Waiting for server to spawn player object");
        }
    }

    private void SpawnAllPlayers()
    {
        var playerPrefab = Resources.Load<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"[GameSessionManager] Could not find {PlayerPrefabPath} in Resources.");
            return;
        }

        // Make a copy of dictionary to avoid modification during iteration
        var playersSnapshot = new List<LobbyManager.PlayerData>(LobbyManager.Instance.playersByUniqueId.Values);

        int spawnIndex = 0;
        foreach (var pdata in playersSnapshot)
        {
            SpawnOrRestorePlayer(pdata, playerPrefab, spawnIndex);
            spawnIndex++;
        }
    }

    public void SpawnOrRestorePlayer(LobbyManager.PlayerData pdata, GameObject prefab = null, int spawnIndex = 0)
    {
        if (!IsServer) return;

        Debug.Log($"[GameSessionManager] === SpawnOrRestorePlayer() called for UniqueId={pdata.PlayerUniqueId}, ClientId={pdata.ClientId}, IsConnected={pdata.IsConnected}");

        // 1️⃣ Skip duplicates
        if (pdata.PlayerObject != null && pdata.PlayerObject.TryGetComponent(out NetworkObject existingNetObj))
        {
            if (existingNetObj.IsSpawned)
            {
                Debug.Log($"[GameSessionManager] Player {pdata.PlayerUniqueId} already has a spawned object. Skipping duplicate spawn.");

                // Restore ownership if reconnecting
                if (pdata.IsConnected && existingNetObj.OwnerClientId != pdata.ClientId)
                {
                    existingNetObj.ChangeOwnership(pdata.ClientId);
                    Debug.Log($"[GameSessionManager] Ownership reassigned to {pdata.ClientId}");
                }

                var move = pdata.PlayerObject.GetComponent<PlayerMovement>();
                if (move != null) move.enabled = pdata.IsConnected;
                return;
            }
        }

        if (prefab == null)
            prefab = Resources.Load<GameObject>(PlayerPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"[GameSessionManager] Missing prefab at path {PlayerPrefabPath}");
            return;
        }

        Transform spawnTransform = spawnPoints.Count > 0 ? spawnPoints[spawnIndex % spawnPoints.Count] : null;
        Vector3 pos = spawnTransform ? spawnTransform.position : Vector3.zero;
        Quaternion rot = spawnTransform ? spawnTransform.rotation : Quaternion.identity;

        GameObject playerObj = Instantiate(prefab, pos, rot);
        var netObj = playerObj.GetComponent<NetworkObject>();

        if (pdata.IsConnected)
        {
            netObj.SpawnAsPlayerObject(pdata.ClientId);
            Debug.Log($"[GameSessionManager] Spawned player for ClientId={pdata.ClientId} (connected)");
        }
        else
        {
            netObj.Spawn();
            Debug.Log($"[GameSessionManager] Spawned placeholder for disconnected ClientId={pdata.ClientId}");
        }

        pdata.PlayerObject = playerObj;
        LobbyManager.Instance.playersByUniqueId[pdata.PlayerUniqueId] = pdata;

        var controller = playerObj.GetComponent<PlayerMovement>();
        if (controller != null)
            controller.enabled = pdata.IsConnected;
    }
}
