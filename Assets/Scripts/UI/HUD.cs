using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class HUD : MonoBehaviour
{
    public Button GetReconnectButton() { return reconnectButton; }
    public Button GetDisconnectButton() { return disconnectButton; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button disconnectButton;

    private string playerId;

    private void Awake()
    {
        disconnectButton.onClick.AddListener(OnDisconnectPressed);
        GetReconnectButton().onClick.AddListener(OnReconnectPressed);

        // Disable buttons initially
        reconnectButton.gameObject.SetActive(false);
        disconnectButton.gameObject.SetActive(false);
    }

    private void Start()
    {
        playerId = AuthenticationService.Instance.PlayerId;

        // Always hide first
        reconnectButton.gameObject.SetActive(false);
        disconnectButton.gameObject.SetActive(false);
        statusText.text = "";

        // Only clients (not host) need reconnection logic
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(WaitAndShowButtonsIfControllingPlayer());

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private IEnumerator WaitAndShowButtonsIfControllingPlayer()
    {
        // Wait until LobbyManager and local player data are ready
        while (LobbyManager.Instance == null ||
               LobbyManager.Instance.playersByUniqueId == null ||
               !LobbyManager.Instance.playersByUniqueId.ContainsKey(AuthenticationService.Instance.PlayerId))
        {
            yield return null;
        }

        var pdata = LobbyManager.Instance.playersByUniqueId[AuthenticationService.Instance.PlayerId];

        // Only show buttons if this client is not a spectator
        if (!pdata.IsSpectator)
        {
            reconnectButton.gameObject.SetActive(true);
            disconnectButton.gameObject.SetActive(true);
            statusText.text = "Connected.";
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
        GetReconnectButton().gameObject.SetActive(false);

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

        GetReconnectButton().gameObject.SetActive(false);
        disconnectButton.gameObject.SetActive(true);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log("[HUDReconnection] Client disconnected.");
        statusText.text = "Disconnected.";

        GetReconnectButton().gameObject.SetActive(true);
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
        GetReconnectButton().gameObject.SetActive(true);
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
