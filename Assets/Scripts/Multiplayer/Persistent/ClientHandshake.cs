using UnityEngine;
using Unity.Netcode;

public class ClientHandshake : MonoBehaviour
{
    private void Start()
    {
        // Only run this on clients, not the host
        if (NetworkManager.Singleton.IsHost) return;

        // Always try immediately (in case we're already connected)
        TrySendJoinImmediately();

        // Then subscribe for future cases
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SendPlayerJoin();

        // Unsubscribe to prevent duplicate sends
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void TrySendJoinImmediately()
    {
        if (NetworkManager.Singleton.IsClient &&
            NetworkManager.Singleton.IsConnectedClient &&
            !NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[ClientHandshake] Detected already-connected client. Sending join immediately...");
            SendPlayerJoin();
        }
    }

    private void SendPlayerJoin()
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

        Debug.Log($"[ClientHandshake] ✅ Sent PlayerJoinMessage with PersistentID: {persistentId}");
    }
}
