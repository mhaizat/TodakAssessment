using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance;
    private MultiplayerSetup multiplayerSetup;

    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject joinLobbyPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Menu Panel")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button exitButton;

    [Header("Join Lobby Panel")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private TMP_InputField joinCodeInput;

    [Header("Lobby Panel")]
    [SerializeField] private List<TextMeshProUGUI> slotTexts;
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI codeText;
    [SerializeField] private Button readyButton;

    [Header("Role Selection")]
    [SerializeField] private Button playerButton;
    [SerializeField] private Button spectatorButton;
    [SerializeField] private TextMeshProUGUI roleText; // optional display

    [SerializeField] private TextMeshProUGUI statusText;

    private bool currentReadyState = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        multiplayerSetup = FindFirstObjectByType<MultiplayerSetup>();

        LobbyManager.OnSlotsUpdated += UpdateLobbySlots;

        SetupButtonListeners();
        ShowPanel(menuPanel);

        startButton.gameObject.SetActive(false);
        readyButton.gameObject.SetActive(false);
    }

    public void OnSelectPlayerRole(bool isSpectator)
    {
        // Send the selection to the server
        LobbyManager.Instance.SetLocalPlayerSpectator(isSpectator);

        // Update UI
        if (roleText != null)
            roleText.text = isSpectator ? "Spectator" : "Player";

        // Highlight buttons: selected role non-interactable
        playerButton.interactable = isSpectator;
        spectatorButton.interactable = !isSpectator;

        // Optionally disable ready button for spectators
        if (readyButton != null)
            readyButton.interactable = !isSpectator;
    }

    private IEnumerator Start()
    {
        // Wait until NetworkManager exists
        yield return new WaitUntil(() => NetworkManager.Singleton != null);

        // Wait until LobbyManager exists
        yield return new WaitUntil(() => LobbyManager.Instance != null);

        // Wait until host or client actually starts
        yield return new WaitUntil(() =>
            NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

        bool isHost = NetworkManager.Singleton.IsServer;
        startButton.gameObject.SetActive(isHost);
        readyButton.gameObject.SetActive(!isHost);

        // Default role = Player
        OnSelectPlayerRole(false);

        UpdateReadyButtonText();
    }

    public void SetRoleUI(bool isSpectator)
    {
        // Update buttons and text without sending to server
        if (roleText != null)
            roleText.text = isSpectator ? "Spectator" : "Player";

        playerButton.interactable = isSpectator;
        spectatorButton.interactable = !isSpectator;
    }


    private void OnDestroy()
    {
        LobbyManager.OnSlotsUpdated -= UpdateLobbySlots;
    }

    public void DisplayJoinCode(string code)
    {
        if (codeText != null)
            codeText.text = $"Join Code: {code}";
    }

    public void OnJoinLobbyPanelPressed() => ShowPanel(joinLobbyPanel);

    public void OnExitGamePressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnCancelJoinLobbyPressed() => ShowPanel(menuPanel);

    private void OnStartGamePressed()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            ShowStatus("Only the host can start the game.");
            return;
        }

        LobbyManager.Instance.TryStartGame();
    }

    private void ShowPanel(GameObject panelToShow)
    {
        menuPanel.SetActive(false);
        joinLobbyPanel.SetActive(false);
        lobbyPanel.SetActive(false);

        panelToShow?.SetActive(true);
    }

    private void SetupButtonListeners()
    {
        hostButton.onClick.AddListener(() => _ = HostGame());
        joinLobbyButton.onClick.AddListener(() => _ = JoinGame());
        joinButton.onClick.AddListener(OnJoinLobbyPanelPressed);
        startButton.onClick.AddListener(OnStartGamePressed);
        readyButton.onClick.AddListener(OnReadyButtonPressed);
        exitButton.onClick.AddListener(OnExitGamePressed);
        cancelButton.onClick.AddListener(OnCancelJoinLobbyPressed);
        playerButton.onClick.AddListener(() => OnSelectPlayerRole(false));
        spectatorButton.onClick.AddListener(() => OnSelectPlayerRole(true));
    }

    private async Task HostGame()
    {
        ShowStatus("Starting host...");
        await multiplayerSetup.CreateRelay();
        ShowPanel(lobbyPanel);

        LobbyManager.Instance?.BroadcastLobbyUpdate();
    }

    private async Task JoinGame()
    {
        var code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            ShowStatus("Please enter a join code.");
            return;
        }

        ShowStatus($"Joining with code {code}...");

        await multiplayerSetup.JoinRelay(code);

        codeText.text = $"Join Code: {code}";
        ShowPanel(lobbyPanel);
        ShowStatus("Joined successfully!");
    }

    public void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void UpdateLobbySlots(List<string> players)
    {
        for (int i = 0; i < slotTexts.Count; i++)
            slotTexts[i].text = i < players.Count ? players[i] : "Empty Slot";
    }

    private void OnReadyButtonPressed()
    {
        if (!NetworkManager.Singleton.IsClient) return;

        currentReadyState = !currentReadyState;
        LobbyManager.Instance.OnReadyButtonPressed();
        UpdateReadyButtonText();
    }

    private void UpdateReadyButtonText()
    {
        if (readyButton != null)
        {
            readyButton.GetComponentInChildren<TextMeshProUGUI>().text =
                currentReadyState ? "Unready" : "Ready";
        }
    }
}
