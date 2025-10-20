using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine.UI;
using TMPro;

public class HUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button disconnectButton;

    private string playerId;

    private void Awake()
    {
        disconnectButton.onClick.AddListener(OnDisconnectPressed);
        reconnectButton.onClick.AddListener(OnReconnectPressed);
    }

    private void Start()
    {
        playerId = AuthenticationService.Instance.PlayerId;

        // Only show for clients (not host)
        bool isClientOnly = NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer;
        reconnectButton.gameObject.SetActive(false && isClientOnly);
        disconnectButton.gameObject.SetActive(true && isClientOnly);

        statusText.text = isClientOnly ? "Connected." : "";

        if (isClientOnly)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }


    // ------------------------------
    // UI BUTTON HANDLERS
    // ------------------------------

    public void OnDisconnectPressed()
    {
        Debug.Log("[HUDReconnection] Disconnecting client...");
        statusText.text = "Disconnecting...";
        StartCoroutine(RestartClientAfterShutdown());
    }

    public void OnReconnectPressed()
    {
        Debug.Log("[HUDReconnection] Attempting to reconnect...");
        statusText.text = "Reconnecting...";
        reconnectButton.gameObject.SetActive(false);

        NetworkManager.Singleton.StartClient();
    }

    // ------------------------------
    // NETWORK CALLBACKS
    // ------------------------------

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[HUDReconnection] Connected as clientId: {clientId}");
        statusText.text = "Connected!";

        SendPlayerJoinMessage(playerId);

        reconnectButton.gameObject.SetActive(false);
        disconnectButton.gameObject.SetActive(true);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("[HUDReconnection] Client disconnected.");
        statusText.text = "Disconnected.";

        reconnectButton.gameObject.SetActive(true);
        disconnectButton.gameObject.SetActive(false);
    }

    // ------------------------------
    // RECONNECT HELPERS
    // ------------------------------

    private IEnumerator RestartClientAfterShutdown()
    {
        NetworkManager.Singleton.Shutdown();

        // Wait for shutdown to finish before allowing reconnect
        yield return new WaitUntil(() => !NetworkManager.Singleton.ShutdownInProgress);

        Debug.Log("[HUDReconnection] Shutdown complete, client can reconnect now.");
        statusText.text = "Disconnected. You can reconnect.";
        reconnectButton.gameObject.SetActive(true);
        disconnectButton.gameObject.SetActive(false);
    }

    // ------------------------------
    // MESSAGE SENDER
    // ------------------------------

    private void SendPlayerJoinMessage(string uniqueId)
    {
        var msg = new PlayerJoinMessage(uniqueId);

        using (var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp))
        {
            writer.WriteValueSafe(msg);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                "PlayerJoin",
                NetworkManager.ServerClientId,
                writer
            );
        }

        Debug.Log($"[HUDReconnection] Sent PlayerJoinMessage with ID: {uniqueId}");
    }
}
