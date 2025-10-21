using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    [Serializable]
    public class PlayerData
    {
        public ulong ClientId;
        public string PlayerUniqueId;
        public string Name;
        public GameObject PlayerObject;
        public bool IsConnected;
        public bool IsReady;
        public bool IsSpectator; // true if this client is a spectator
    }

    public static LobbyManager Instance;
    public static event Action<List<string>> OnSlotsUpdated;

    public Dictionary<string, PlayerData> playersByUniqueId = new();
    private Dictionary<ulong, string> clientIdToUniqueId = new();

    [Header("Lobby Settings")]
    [SerializeField][Range(2, 10)] private int MaxPlayers = 5;

    private const string gameplaySceneName = "Game";
    public bool IsSpectator;

    // Tracks local client's ready state
    private bool currentReadyState = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetRoleServerRpc(ulong clientId, bool isSpectator)
    {
        if (!clientIdToUniqueId.TryGetValue(clientId, out var uniqueId)) return;
        if (!playersByUniqueId.TryGetValue(uniqueId, out var pdata)) return;

        pdata.IsSpectator = isSpectator;
        playersByUniqueId[uniqueId] = pdata;

        Debug.Log($"[LobbyManager] Player {uniqueId} role: {(isSpectator ? "Spectator" : "Player")}");
        BroadcastLobbyUpdate();
    }

    public void SetLocalPlayerSpectator(bool isSpectator)
    {
        // Tell server about role
        RequestSetRoleServerRpc(NetworkManager.Singleton.LocalClientId, isSpectator);

        // Only spawn camera if player chose spectator AND game scene is loaded
        if (isSpectator)
        {
            if (SceneManager.GetActiveScene().name == "Game")
            {
                SpawnLocalSpectatorCamera();
            }
            else
            {
                // Wait until game scene loads
                SceneManager.sceneLoaded += OnGameSceneLoadedSpawnCamera;
            }
        }
    }

    private void OnGameSceneLoadedSpawnCamera(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game") return;

        SceneManager.sceneLoaded -= OnGameSceneLoadedSpawnCamera;
        SpawnLocalSpectatorCamera();
    }

    public void StartGame()
    {
        if (!IsServer) return;
        NetworkManager.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "PlayerJoin",
            OnPlayerJoinMessageReceived
        );

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "RequestLobbySync",
            OnRequestLobbySyncReceived
        );

        // Register host and mark as ready automatically
        string hostUniqueId = Guid.NewGuid().ToString();
        AddPlayerInternal(NetworkManager.Singleton.LocalClientId, hostUniqueId);

        // Set host as ready
        SetPlayerReady(NetworkManager.Singleton.LocalClientId, true);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        BroadcastLobbyUpdate();
    }

    private void OnRequestLobbySyncReceived(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServer) return;
        Debug.Log($"[LobbyManager] Sending lobby sync to client {senderClientId}");

        var slotData = GetSlotDisplayData();
        SendLobbySyncClientRpc(new SlotData(slotData), new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { senderClientId } }
        });
    }

    [ClientRpc]
    private void SendLobbySyncClientRpc(SlotData slotData, ClientRpcParams rpcParams = default)
    {
        List<string> slots = new();
        foreach (var fs in slotData.Values)
            slots.Add(fs.ToString());

        OnSlotsUpdated?.Invoke(slots);
    }

    private void OnPlayerJoinMessageReceived(ulong senderClientId, FastBufferReader stream)
    {
        PlayerJoinMessage msg = default;
        stream.ReadValueSafe(out msg);

        string uniqueId = msg.PlayerId.ToString();
        AddPlayerInternal(senderClientId, uniqueId, reconnect: true);
        BroadcastLobbyUpdate();
    }

    public void AddPlayerInternal(ulong clientId, string uniqueId, bool reconnect = false, bool isSpectator = false)
    {
        if (!IsServer) return;

        if (playersByUniqueId.TryGetValue(uniqueId, out var existingPlayer))
        {
            existingPlayer.ClientId = clientId;
            existingPlayer.IsConnected = true;
            existingPlayer.IsSpectator = isSpectator; // update role if reconnecting
            playersByUniqueId[uniqueId] = existingPlayer;
            clientIdToUniqueId[clientId] = uniqueId;

            Debug.Log($"[LobbyManager] Player {uniqueId} reconnected/resumed (ClientId {clientId}), Spectator={isSpectator}");

            GameSessionManager.Instance?.SpawnOrRestorePlayer(existingPlayer);
            BroadcastLobbyUpdate();
            return;
        }

        // New player
        var pdata = new PlayerData
        {
            ClientId = clientId,
            PlayerUniqueId = uniqueId,
            IsConnected = true,
            PlayerObject = null,
            IsReady = false,
            IsSpectator = isSpectator // **use desired role**
        };

        playersByUniqueId[uniqueId] = pdata;
        clientIdToUniqueId[clientId] = uniqueId;

        Debug.Log($"[LobbyManager] New player {uniqueId} added (ClientId {clientId}), Spectator={isSpectator}");

        GameSessionManager.Instance?.SpawnOrRestorePlayer(pdata);

        BroadcastLobbyUpdate();

        // Spawn local spectator camera immediately if this client is a spectator
        if (pdata.IsSpectator && clientId == NetworkManager.Singleton.LocalClientId)
        {
            SpawnLocalSpectatorCamera();
        }
    }

    private void SpawnLocalSpectatorCamera()
    {
        // Prevent multiple spawns
        var pdata = playersByUniqueId.Values.FirstOrDefault(p => p.ClientId == NetworkManager.Singleton.LocalClientId);
        if (pdata != null && pdata.PlayerObject != null)
            return;

        GameObject specCamPrefab = Resources.Load<GameObject>("SpectatorCamera");
        if (specCamPrefab == null)
        {
            Debug.LogError("[LobbyManager] SpectatorCamera prefab missing in Resources!");
            return;
        }

        GameObject camObj = Instantiate(specCamPrefab);
        camObj.name = "LocalSpectatorCamera";

        // Disable the existing player camera if it exists
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.gameObject.SetActive(false);
        }

        // Update local player data
        if (pdata != null)
        {
            pdata.PlayerObject = camObj;
            playersByUniqueId[pdata.PlayerUniqueId] = pdata;
        }

        Debug.Log("[LobbyManager] Spawned local spectator camera for this client.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        StartCoroutine(HandleClientDisconnectAfterDelay(clientId, 0.15f));
    }

    private IEnumerator HandleClientDisconnectAfterDelay(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!clientIdToUniqueId.TryGetValue(clientId, out var uniqueId))
            yield break;

        if (!playersByUniqueId.TryGetValue(uniqueId, out var pdata))
        {
            clientIdToUniqueId.Remove(clientId);
            yield break;
        }

        pdata.IsConnected = false;
        playersByUniqueId[uniqueId] = pdata;

        if (pdata.PlayerObject != null)
        {
            var controller = pdata.PlayerObject.GetComponent<PlayerMovement>();
            if (controller != null)
                controller.enabled = false;
        }

        Debug.Log($"[LobbyManager] Player {uniqueId} disconnected. Character remains in scene.");
        BroadcastLobbyUpdate();
    }

    private List<string> GetSlotDisplayData()
    {
        List<string> slotData = new();
        var playerList = new List<PlayerData>(playersByUniqueId.Values);
        playerList.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));

        for (int i = 0; i < MaxPlayers; i++)
        {
            if (i < playerList.Count)
            {
                var pdata = playerList[i];
                string status = pdata.IsConnected ? "Connected" : "Disconnected";

                // Spectators are always "Ready" visually
                string ready = pdata.IsSpectator ? "Ready" : (pdata.IsReady ? "Ready" : "Not Ready");
                string role = pdata.IsSpectator ? "Spectator" : "Player";

                slotData.Add($"Player {i + 1} ({pdata.PlayerUniqueId}) [{role}, {status}, {ready}]");
            }
            else
            {
                slotData.Add("Empty Slot");
            }
        }

        return slotData;
    }

    public void BroadcastLobbyUpdate()
    {
        var slotData = GetSlotDisplayData();
        UpdateSlotsClientRpc(new SlotData(slotData));
        OnSlotsUpdated?.Invoke(slotData);
    }

    [ClientRpc]
    private void UpdateSlotsClientRpc(SlotData slotData)
    {
        List<string> slots = new();
        foreach (var fs in slotData.Values)
            slots.Add(fs.ToString());

        OnSlotsUpdated?.Invoke(slots);
    }

    [Serializable]
    public struct SlotData : INetworkSerializable
    {
        public FixedString128Bytes[] Values;

        public SlotData(List<string> list)
        {
            Values = new FixedString128Bytes[list.Count];
            for (int i = 0; i < list.Count; i++)
                Values[i] = list[i];
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int count = Values?.Length ?? 0;
            serializer.SerializeValue(ref count);

            if (serializer.IsReader)
            {
                Values = new FixedString128Bytes[count];
                for (int i = 0; i < count; i++)
                {
                    FixedString128Bytes val = default;
                    serializer.SerializeValue(ref val);
                    Values[i] = val;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var val = Values[i];
                    serializer.SerializeValue(ref val);
                }
            }
        }
    }

    // ---------------- Ready System ----------------
    public void SetPlayerReady(ulong clientId, bool ready)
    {
        if (!clientIdToUniqueId.TryGetValue(clientId, out var uniqueId)) return;
        if (!playersByUniqueId.TryGetValue(uniqueId, out var pdata)) return;

        pdata.IsReady = ready;
        playersByUniqueId[uniqueId] = pdata;

        Debug.Log($"[LobbyManager] Player {uniqueId} ready state: {ready}");
        BroadcastLobbyUpdate();
    }

    public void TryStartGame()
    {
        if (!IsServer) return;

        if (!AllPlayersReady())
        {
            Debug.Log("[LobbyManager] Cannot start game. Not all players are ready.");
            return;
        }

        Debug.Log("[LobbyManager] All players ready. Starting game...");
        StartGame();
    }

    private bool AllPlayersReady()
    {
        int playerCount = 0;
        foreach (var pdata in playersByUniqueId.Values)
        {
            if (!pdata.IsConnected || pdata.IsSpectator) continue; // ignore spectators
            playerCount++;
            if (!pdata.IsReady)
                return false;
        }

        return playerCount >= 1; // at least 1 real player required
    }

    [ClientRpc]
    public void SyncRoleClientRpc(bool isSpectator, ClientRpcParams rpcParams = default)
    {
        LobbyUI.Instance?.SetRoleUI(isSpectator);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSetReadyServerRpc(ulong clientId, bool ready)
    {
        SetPlayerReady(clientId, ready);
    }

    public void OnReadyButtonPressed()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        if (IsSpectator) return; // spectators are always considered ready

        bool newReadyState = !currentReadyState;
        currentReadyState = newReadyState;

        RequestSetReadyServerRpc(NetworkManager.Singleton.LocalClientId, newReadyState);
    }
}
