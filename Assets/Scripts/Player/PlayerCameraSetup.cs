using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using System.Collections;

public class PlayerCameraSetup : NetworkBehaviour
{
    [SerializeField] private GameObject playerCameraObj; // Child camera in prefab
    [SerializeField] private Transform cameraTarget;

    private void Awake()
    {
        if (playerCameraObj == null)
            playerCameraObj = transform.Find("PlayerCamera")?.gameObject;

        if (cameraTarget == null)
            cameraTarget = transform;

        // Disable camera by default
        if (playerCameraObj != null)
            playerCameraObj.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        // Only the local player cares about activating its camera
        if (!IsLocalPlayer) return;

        // Subscribe to scene reload
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Hook reconnect: NetworkManager triggers this for late joins or reconnects
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Start initial activation after spawn
        StartCoroutine(InitCameraDelayed());
    }

    private IEnumerator InitCameraDelayed()
    {
        // Wait 2 frames to ensure player object and network are fully initialized
        yield return null;
        yield return null;

        ActivateLocalCamera();
    }

    private void OnClientConnected(ulong clientId)
    {
        // Only care about local player camera
        if (!IsLocalPlayer) return;

        // Start a delayed activation in case this client just reconnected
        StartCoroutine(InitCameraDelayed());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsLocalPlayer) return;

        // Reactivate camera after scene load
        StartCoroutine(InitCameraDelayed());
    }

    private void ActivateLocalCamera()
    {
        if (playerCameraObj == null)
            playerCameraObj = transform.Find("PlayerCamera")?.gameObject;

        if (playerCameraObj == null)
        {
            Debug.LogWarning($"[PlayerCameraSetup] PlayerCamera missing for client {OwnerClientId}");
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            // Root is not active yet; wait a frame
            StartCoroutine(ActivateAfterRootActive());
            return;
        }

        // Activate only local player's camera
        playerCameraObj.SetActive(true);

        // Enable AudioListener only for local player
        var listener = playerCameraObj.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = true;

        // Register camera with CameraManager
        var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
        if (CameraManager.Instance != null && cineCam != null)
            CameraManager.Instance.RegisterCamera(cineCam);

        // Bind follow/look target
        if (cineCam != null)
        {
            cineCam.Follow = cameraTarget;
            cineCam.LookAt = cameraTarget;
        }

        Debug.Log($"[PlayerCameraSetup] Local camera activated for client {OwnerClientId}");
    }

    private IEnumerator ActivateAfterRootActive()
    {
        while (!gameObject.activeInHierarchy)
            yield return null;

        ActivateLocalCamera();
    }

    public override void OnNetworkDespawn()
    {
        // Only unregister if we had registered
        if (CameraManager.Instance != null && playerCameraObj != null)
        {
            var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
            if (cineCam != null)
                CameraManager.Instance.UnregisterCamera(cineCam);
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}
