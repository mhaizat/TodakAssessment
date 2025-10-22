using UnityEngine;
using Unity.Netcode;
using System;

/// <summary>
/// Handles client handshake with the server by sending a PlayerJoinMessage upon connection.
/// Can optionally join as a spectator automatically.
/// </summary>
public class ClientHandshake : MonoBehaviour
{
    [Tooltip("If true, client will join as a spectator automatically.")]
    [SerializeField] private bool joinAsSpectator = false;

    // -------------------- UNITY LIFECYCLE --------------------
    private void Start()
    {
        // Only run this on clients (not host)
        if (NetworkManager.Singleton.IsHost) return;

        TrySendJoinImmediately();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // -------------------- NETWORK CALLBACK --------------------
    private void OnClientConnected(ulong clientId)
    {
        // Only send join for the local client
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SendPlayerJoin(joinAsSpectator);

        // Unsubscribe to prevent duplicate sends
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // -------------------- PRIVATE HELPERS --------------------
    /// <summary>
    /// Sends join immediately if the client is already connected when this script starts.
    /// </summary>
    private void TrySendJoinImmediately()
    {
        if (NetworkManager.Singleton.IsClient &&
            NetworkManager.Singleton.IsConnectedClient &&
            !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[ClientHandshake] Detected already-connected client. Sending join immediately...");
            SendPlayerJoin(joinAsSpectator);
        }
    }

    /// <summary>
    /// Sends a PlayerJoinMessage to the server and updates the local LobbyManager.
    /// </summary>
    private void SendPlayerJoin(bool asSpectator)
    {
        if (!NetworkManager.Singleton.IsClient) return;

        string persistentId = PlayerIdHelper.GetOrCreatePlayerId();
        var msg = new PlayerJoinMessage(persistentId);

        using (var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp))
        {
            writer.WriteValueSafe(msg);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                "PlayerJoin",
                NetworkManager.ServerClientId,
                writer
            );
        }

        Debug.Log($"[ClientHandshake] Sent PlayerJoinMessage with PersistentID: {persistentId}, Spectator={asSpectator}");

        // Immediately update the local LobbyManager role
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.AddPlayerInternal(
                NetworkManager.Singleton.LocalClientId,
                persistentId,
                reconnect: false,
                isSpectator: asSpectator
            );
        }
    }
}
