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

        // Find all spawn points in the scene
        GameObject[] spawnObjs = GameObject.FindGameObjectsWithTag("SpawnPoint");
        foreach (var obj in spawnObjs)
            spawnPoints.Add(obj.transform);

        if (IsServer)
        {
            SpawnAllPlayers();
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

        int spawnIndex = 0;

        foreach (var kvp in LobbyManager.Instance.playersByUniqueId)
        {
            var pdata = kvp.Value;
            SpawnOrRestorePlayer(pdata, playerPrefab, spawnIndex);
            spawnIndex++;
        }
    }

    public void SpawnOrRestorePlayer(LobbyManager.PlayerData pdata, GameObject prefab = null, int spawnIndex = 0)
    {
        if (!IsServer) return;

        if (pdata.PlayerObject != null)
        {
            // Reactivate and restore ownership
            pdata.PlayerObject.SetActive(true);
            var netObj = pdata.PlayerObject.GetComponent<NetworkObject>();

            // Only transfer ownership if this is NOT the host
            if (pdata.ClientId != NetworkManager.Singleton.LocalClientId)
            {
                if (netObj.OwnerClientId != pdata.ClientId)
                    netObj.ChangeOwnership(pdata.ClientId);
            }

            return;
        }

        // Load prefab if not provided
        if (prefab == null)
            prefab = Resources.Load<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[GameSessionManager] Could not find {PlayerPrefabPath} in Resources.");
            return;
        }

        // Select spawn point
        Transform spawnTransform = spawnPoints.Count > 0 ? spawnPoints[spawnIndex % spawnPoints.Count] : null;
        Vector3 pos = spawnTransform != null ? spawnTransform.position : Vector3.zero;
        Quaternion rot = spawnTransform != null ? spawnTransform.rotation : Quaternion.identity;

        // Instantiate player object
        GameObject playerObj = Instantiate(prefab, pos, rot);
        var netObjComp = playerObj.GetComponent<NetworkObject>();
        if (netObjComp == null)
        {
            Debug.LogError("[GameSessionManager] Player prefab is missing NetworkObject!");
            Destroy(playerObj);
            return;
        }

        // Spawn as player object
        netObjComp.SpawnAsPlayerObject(pdata.ClientId);

        // Save reference for reconnects
        pdata.PlayerObject = playerObj;
    }
}
