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
    }

    public static LobbyManager Instance;
    public static event Action<List<string>> OnSlotsUpdated;

    public Dictionary<string, PlayerData> playersByUniqueId = new();
    private Dictionary<ulong, string> clientIdToUniqueId = new();

    [Header("Lobby Settings")]
    [SerializeField][Range(2, 10)] private int MaxPlayers = 5;

    private const string gameplaySceneName = "Game";

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

        // Register host
        string hostUniqueId = System.Guid.NewGuid().ToString();
        AddPlayerInternal(NetworkManager.Singleton.LocalClientId, hostUniqueId);

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        BroadcastLobbyUpdate();
    }

    // 🔹 Handles client sync requests (when a new client joins mid-lobby)
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

            // Restore player ownership
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
            PlayerObject = null
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

        // Optional: freeze character input
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
}
