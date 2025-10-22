using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Handles local player camera activation and Cinemachine setup.
/// Ensures only the local player's camera is active and properly configured.
/// </summary>
public class PlayerCameraSetup : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject playerCameraObj; // Child camera object in prefab
    [SerializeField] private Transform cameraTarget;     // Transform to follow/look at

    // -------------------------
    // STATE FLAGS
    // -------------------------
    private bool isCameraActive = false;
    private bool hasActivatedCamera = false; // Prevents multiple InitCameraDelayed calls

    // -------------------------
    // UNITY LIFECYCLE
    // -------------------------
    private void Awake()
    {
        if (playerCameraObj == null)
            playerCameraObj = transform.Find("PlayerCamera")?.gameObject;

        if (cameraTarget == null)
            cameraTarget = transform;

        if (playerCameraObj != null)
            playerCameraObj.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        StopAllCoroutines();
        StartCoroutine(InitCameraDelayed());
    }

    public override void OnNetworkDespawn()
    {
        UnregisterCamera();

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        isCameraActive = false;
        hasActivatedCamera = false;
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();

        if (IsLocalPlayer)
        {
            Debug.Log($"[PlayerCameraSetup] Gained ownership for client {OwnerClientId}, initializing camera...");
            StopAllCoroutines();
            StartCoroutine(InitCameraDelayed());
        }
    }

    // -------------------------
    // NETWORK & SCENE CALLBACKS
    // -------------------------
    private void OnClientConnected(ulong clientId)
    {
        if (!IsLocalPlayer) return;

        if (clientId == OwnerClientId)
        {
            Debug.Log($"[PlayerCameraSetup] Client {clientId} reconnected, reinitializing camera...");
            StopAllCoroutines();
            StartCoroutine(InitCameraDelayed());
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsLocalPlayer) return;

        Debug.Log($"[PlayerCameraSetup] Scene '{scene.name}' loaded, reinitializing camera...");
        StopAllCoroutines();
        StartCoroutine(InitCameraDelayed());
    }

    // -------------------------
    // CAMERA ACTIVATION
    // -------------------------
    private IEnumerator InitCameraDelayed()
    {
        if (hasActivatedCamera)
            yield break;

        // Wait a few frames for ownership and spawn synchronization
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        ActivateLocalCamera();
        hasActivatedCamera = true;
    }

    private void ActivateLocalCamera()
    {
        if (!IsLocalPlayer)
        {
            Debug.Log($"[PlayerCameraSetup] Skipped camera activation on non-local client {OwnerClientId}");
            return;
        }

        if (playerCameraObj == null)
            playerCameraObj = transform.Find("PlayerCamera")?.gameObject;

        if (playerCameraObj == null)
        {
            Debug.LogWarning($"[PlayerCameraSetup] PlayerCamera missing for client {OwnerClientId}");
            return;
        }

        if (!gameObject.activeInHierarchy)
        {
            StartCoroutine(ActivateAfterRootActive());
            return;
        }

        if (isCameraActive) return;

        isCameraActive = true;
        playerCameraObj.SetActive(true);

        SetupAudioListener();
        DisableOtherCinemachineBrains();
        SetupCinemachineBrain();
        SetupCinemachineTarget();
        RegisterCameraWithManager();

        Debug.Log($"[PlayerCameraSetup] Local camera fully activated for client {OwnerClientId}");
    }

    private IEnumerator ActivateAfterRootActive()
    {
        while (!gameObject.activeInHierarchy)
            yield return null;

        ActivateLocalCamera();
    }

    // -------------------------
    // CAMERA UTILITY METHODS
    // -------------------------
    private void SetupAudioListener()
    {
        var listener = playerCameraObj.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = true;
    }

    private void DisableOtherCinemachineBrains()
    {
        var existingBrains = FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
        foreach (var b in existingBrains)
        {
            if (b.isActiveAndEnabled)
            {
                b.enabled = false;
                Debug.Log($"[PlayerCameraSetup] Disabled old CinemachineBrain on {b.gameObject.name}");
            }
        }
    }

    private void SetupCinemachineBrain()
    {
        var cam = playerCameraObj.GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("[PlayerCameraSetup] No Camera component found on PlayerCameraObj");
            return;
        }

        var brain = cam.GetComponent<CinemachineBrain>() ?? cam.gameObject.AddComponent<CinemachineBrain>();
        cam.enabled = false;
        cam.enabled = true;
        cam.gameObject.tag = "MainCamera";
        brain.enabled = true;

        Debug.Log($"[PlayerCameraSetup] Local CinemachineBrain activated for {cam.name}");
    }

    private void SetupCinemachineTarget()
    {
        var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
        if (cineCam != null && cameraTarget != null)
        {
            cineCam.Follow = cameraTarget;
            cineCam.LookAt = cameraTarget;
        }
    }

    private void RegisterCameraWithManager()
    {
        var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
        if (CameraManager.Instance != null && cineCam != null)
        {
            CameraManager.Instance.UnregisterAllOwnedBy(OwnerClientId);
            CameraManager.Instance.RegisterCamera(cineCam, OwnerClientId);
        }
    }

    private void UnregisterCamera()
    {
        if (CameraManager.Instance != null && playerCameraObj != null)
        {
            var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
            if (cineCam != null)
                CameraManager.Instance.UnregisterCamera(cineCam);
        }
    }
}
