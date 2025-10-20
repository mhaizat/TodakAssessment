// LobbyManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
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
    }

    public static LobbyManager Instance;
    public static event Action<List<string>> OnSlotsUpdated;

    public Dictionary<string, PlayerData> playersByUniqueId = new();
    private Dictionary<ulong, string> clientIdToUniqueId = new();

    [Header("Lobby Settings")]
    [SerializeField][Range(2, 10)] private int MaxPlayers = 5;

    private const string gameplaySceneName = "Game";

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
        string hostUniqueId = System.Guid.NewGuid().ToString();
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

    private void AddPlayerInternal(ulong clientId, string uniqueId, bool reconnect = false)
    {
        if (!IsServer) return;

        if (playersByUniqueId.TryGetValue(uniqueId, out var existingPlayer))
        {
            existingPlayer.ClientId = clientId;
            existingPlayer.IsConnected = true;
            playersByUniqueId[uniqueId] = existingPlayer;
            clientIdToUniqueId[clientId] = uniqueId;

            Debug.Log($"[LobbyManager] Player {uniqueId} reconnected/resumed (ClientId {clientId})");

            if (existingPlayer.PlayerObject != null)
            {
                var netObj = existingPlayer.PlayerObject.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    if (existingPlayer.IsConnected && clientId != NetworkManager.Singleton.LocalClientId)
                    {
                        netObj.ChangeOwnership(clientId);
                        Debug.Log($"[LobbyManager] Reassigned ownership of player object to ClientId {clientId}");
                    }

                    var controller = existingPlayer.PlayerObject.GetComponent<PlayerMovement>();
                    if (controller != null)
                        controller.enabled = true;
                }
                else
                {
                    Debug.LogWarning($"[LobbyManager] Player object missing for {uniqueId}, respawning...");
                    GameSessionManager.Instance?.SpawnOrRestorePlayer(existingPlayer);
                }
            }
            else
            {
                Debug.LogWarning($"[LobbyManager] No existing player object for {uniqueId}, spawning new one...");
                GameSessionManager.Instance?.SpawnOrRestorePlayer(existingPlayer);
            }

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
            IsReady = false
        };
        playersByUniqueId[uniqueId] = pdata;
        clientIdToUniqueId[clientId] = uniqueId;

        Debug.Log($"[LobbyManager] New player {uniqueId} added (ClientId {clientId})");

        GameSessionManager.Instance?.SpawnOrRestorePlayer(pdata);

        BroadcastLobbyUpdate();
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
                string ready = pdata.IsReady ? "Ready" : "Not Ready";
                slotData.Add($"Player {i + 1} ({pdata.PlayerUniqueId}) [{status}, {ready}]");
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

    // Called when host presses Start Game
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
        int connectedCount = 0;
        foreach (var pdata in playersByUniqueId.Values)
        {
            if (!pdata.IsConnected) continue;
            connectedCount++;

            if (!pdata.IsReady)
                return false;
        }

        // Require at least 2 connected players
        return connectedCount >= 2;
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestSetReadyServerRpc(ulong clientId, bool ready)
    {
        SetPlayerReady(clientId, ready);
    }

    public void OnReadyButtonPressed()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        bool newReadyState = !currentReadyState;
        currentReadyState = newReadyState;

        RequestSetReadyServerRpc(NetworkManager.Singleton.LocalClientId, newReadyState);
    }

    public bool AllConnectedPlayersReady()
    {
        foreach (var pdata in playersByUniqueId.Values)
        {
            if (pdata.IsConnected && !pdata.IsReady)
                return false;
        }
        return true;
    }

    public bool CanStartGame()
    {
        if (!IsServer) return false;

        // Must have at least 2 connected players
        int connectedPlayers = 0;
        foreach (var pdata in playersByUniqueId.Values)
            if (pdata.IsConnected) connectedPlayers++;

        if (connectedPlayers < 2)
            return false;

        // All connected players must be ready
        foreach (var pdata in playersByUniqueId.Values)
            if (pdata.IsConnected && !pdata.IsReady)
                return false;

        return true;
    }
}
