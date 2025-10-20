using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;

public class LobbyManager : NetworkBehaviour
{
    public class PlayerData
    {
        public ulong ClientId;
        public string PlayerUniqueId;
        public string Name;
        public GameObject PlayerObject;
        public bool IsConnected;
    }

    public static LobbyManager Instance;
    public static event Action<List<string>> OnSlotsUpdated;

    public Dictionary<string, PlayerData> playersByUniqueId = new();
    private Dictionary<ulong, string> clientIdToUniqueId = new();

    private const int MaxPlayers = 2; // host + 1 client
    private const string gameplaySceneName = "Game";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // If you want LobbyManager to persist across scenes as part of a parent,
        // make sure the parent calls DontDestroyOnLoad. Alternatively, uncomment:
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // For now we keep runtime ID generation or persistent depending on PlayerIdHelper
        string id = PlayerIdHelper.GetOrCreatePlayerId();
        Debug.Log($"Generated Persistent ID: {id}");
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "PlayerJoin",
            OnPlayerJoinMessageReceived
        );

        // Host registers itself immediately (give host a persistent id if desired)
        string hostUniqueId = System.Guid.NewGuid().ToString();
        AddPlayerInternal(NetworkManager.Singleton.LocalClientId, hostUniqueId);

        // Subscribe to client disconnects
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        BroadcastLobbyUpdate();
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.CustomMessagingManager?.UnregisterNamedMessageHandler("PlayerJoin");
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        base.OnDestroy();
    }

    private void OnPlayerJoinMessageReceived(ulong senderClientId, FastBufferReader stream)
    {
        PlayerJoinMessage msg = default;
        stream.ReadValueSafe(out msg);

        string uniqueId = msg.PlayerId.ToString();

        // Always attempt to add or reclaim slot for this uniqueId
        AddPlayerInternal(senderClientId, uniqueId, reconnect: true);

        BroadcastLobbyUpdate();
    }

    private void AddPlayerInternal(ulong clientId, string uniqueId, bool reconnect = false)
    {
        if (!IsServer) return;

        // 1) If the player already exists by uniqueId, reclaim/update that slot immediately.
        // 1) If the player already exists by uniqueId, reclaim/update that slot immediately.
        if (playersByUniqueId.TryGetValue(uniqueId, out var existingPlayer))
        {
            existingPlayer.ClientId = clientId;
            existingPlayer.IsConnected = true;
            playersByUniqueId[uniqueId] = existingPlayer;
            clientIdToUniqueId[clientId] = uniqueId;

            Debug.Log($"[LobbyManager] Player {uniqueId} reconnected/resumed (ClientId {clientId})");

            // Reclaim ownership if the player object is still alive
            if (existingPlayer.PlayerObject != null)
            {
                var netObj = existingPlayer.PlayerObject.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    // Only give ownership if this is the **reconnected client**, not the host
                    if (clientId != NetworkManager.Singleton.LocalClientId)
                    {
                        netObj.ChangeOwnership(clientId);
                        Debug.Log($"[LobbyManager] Reassigned ownership of player object to ClientId {clientId}");
                    }
                }
                else
                {
                    // Object was destroyed, respawn
                    Debug.LogWarning($"[LobbyManager] Player object missing for {uniqueId}, respawning...");
                    GameSessionManager.Instance?.SpawnOrRestorePlayer(existingPlayer);
                }
            }
            else
            {
                // No object found, spawn new one
                Debug.LogWarning($"[LobbyManager] No existing player object for {uniqueId}, spawning new one...");
                GameSessionManager.Instance?.SpawnOrRestorePlayer(existingPlayer);
            }

            BroadcastLobbyUpdate();
            return;
        }


        // 2) Count currently connected players (only those marked IsConnected)
        int connectedCount = 0;
        foreach (var p in playersByUniqueId.Values)
            if (p.IsConnected) connectedCount++;

        // 3) If lobby is full of *connected players*, defer rejection briefly to avoid race with disconnect cleanup.
        if (connectedCount >= MaxPlayers)
        {
            Debug.Log($"[LobbyManager] Temporary lobby full (connectedCount={connectedCount}). Deferred reject for ClientId {clientId}");
            StartCoroutine(DeferredReject(clientId, 0.1f)); // small delay to let disconnects settle
            return;
        }

        // 4) Add as new player
        var pdata = new PlayerData
        {
            ClientId = clientId,
            PlayerUniqueId = uniqueId,
            IsConnected = true,
            PlayerObject = null
        };
        playersByUniqueId[uniqueId] = pdata;
        clientIdToUniqueId[clientId] = uniqueId;

        Debug.Log($"[LobbyManager] New player {uniqueId} added (ClientId {clientId})");

        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.SpawnOrRestorePlayer(pdata);

        BroadcastLobbyUpdate();
    }

    private IEnumerator DeferredReject(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);

        // If client is still connected after the small delay, reject explicitly.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            NotifyLobbyFullClientRpc(clientId);
            // Guard: don't try to disconnect the host (clientId 0)
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
            }
            Debug.Log($"[LobbyManager] Deferred reject executed for ClientId {clientId}");
        }
    }

    // When Netcode notifies that a client disconnected, we delay a bit to let transient states settle
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        StartCoroutine(HandleClientDisconnectAfterDelay(clientId, 0.15f));
    }

    private IEnumerator HandleClientDisconnectAfterDelay(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!clientIdToUniqueId.TryGetValue(clientId, out var uniqueId))
        {
            Debug.Log("[LobbyManager] Client disconnected but had no recorded uniqueId!");
            yield break;
        }

        if (!playersByUniqueId.TryGetValue(uniqueId, out var pdata))
        {
            Debug.Log("[LobbyManager] playersByUniqueId does not contain this uniqueId anymore!");
            clientIdToUniqueId.Remove(clientId);
            yield break;
        }

        pdata.IsConnected = false;
        playersByUniqueId[uniqueId] = pdata;

        Debug.Log($"[LobbyManager] OnClientDisconnected for {uniqueId} - marked disconnected. Can reconnect.");

        BroadcastLobbyUpdate();

        // Start timeout for real removal
        StartCoroutine(DisconnectTimeout(uniqueId));
    }

    // your existing timeout removal method (keeps same signature)
    private IEnumerator DisconnectTimeout(string uniqueId, float timeout = 60f)
    {
        yield return new WaitForSeconds(timeout);

        if (playersByUniqueId.TryGetValue(uniqueId, out var pdata) && !pdata.IsConnected)
        {
            if (pdata.PlayerObject != null)
                Destroy(pdata.PlayerObject);

            playersByUniqueId.Remove(uniqueId);
            Debug.Log($"[LobbyManager] Player {uniqueId} did not reconnect. Removed from lobby.");

            BroadcastLobbyUpdate();
        }
    }

    private List<string> GetSlotDisplayData()
    {
        List<string> slotData = new();
        var playerList = new List<PlayerData>(playersByUniqueId.Values);

        // Sort by ClientId to ensure host is always Player 1
        playerList.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));

        for (int i = 0; i < MaxPlayers; i++)
        {
            if (i < playerList.Count)
            {
                var pdata = playerList[i];
                string status = pdata.IsConnected ? "Connected" : "Disconnected";
                slotData.Add($"Player {i + 1} ({pdata.PlayerUniqueId}) [{status}]");
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
        List<string> slots = new List<string>();
        foreach (var fs in slotData.Values)
            slots.Add(fs.ToString());

        OnSlotsUpdated?.Invoke(slots);

        Debug.Log($"[LobbyUI] Slots updated: {string.Join(", ", slots)}");
    }

    [ClientRpc]
    private void NotifyLobbyFullClientRpc(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
            Debug.Log("Lobby is full!");
    }

    public void StartGame()
    {
        if (!IsServer) return;
        NetworkManager.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
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
}
