using UnityEngine;
using Unity.Netcode;

public class ClientHandshake : MonoBehaviour
{
    private void Start()
    {
        // Only run this on clients, not the host
        if (NetworkManager.Singleton.IsHost) return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // Only send the join message from the local client
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        SendPlayerJoin();

        // Unsubscribe to prevent duplicates
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void SendPlayerJoin()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        string persistentId = PlayerIdHelper.GetOrCreatePlayerId();

        var msg = new PlayerJoinMessage(persistentId);

        // Correct way: serialize into a FastBufferWriter then send the writer
        using (var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp))
        {
            writer.WriteValueSafe(msg); // <-- correct
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                "PlayerJoin",
                NetworkManager.ServerClientId,
                writer
            );
        }

        Debug.Log($"[ClientHandshake] Sent PlayerJoinMessage with PersistentID: {persistentId}");
    }

}
