using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using System.Collections;

public class PlayerCameraSetup : NetworkBehaviour
{
    [SerializeField] private GameObject playerCameraObj; // Child camera in prefab
    [SerializeField] private Transform cameraTarget;

    private bool isCameraActive = false;
    private bool hasActivatedCamera = false; // 🧩 prevents double InitCameraDelayed

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
        if (!IsLocalPlayer)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        StopAllCoroutines();
        StartCoroutine(InitCameraDelayed());
    }

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

    private IEnumerator InitCameraDelayed()
    {
        // 🧩 Only run once per reconnect/session
        if (hasActivatedCamera)
            yield break;

        // Wait a few frames for spawn/ownership sync
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

        if (isCameraActive)
            return;

        isCameraActive = true;
        playerCameraObj.SetActive(true);

        // 🎧 Audio Listener
        var listener = playerCameraObj.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = true;

        // 🧠 Disable other CinemachineBrains (host leftovers)
        var existingBrains = FindObjectsByType<Unity.Cinemachine.CinemachineBrain>(FindObjectsSortMode.None);

        foreach (var b in existingBrains)
        {
            if (b.isActiveAndEnabled)
            {
                b.enabled = false;
                Debug.Log($"[PlayerCameraSetup] ❌ Disabled old CinemachineBrain on {b.gameObject.name}");
            }
        }

        // ✅ Ensure a CinemachineBrain on our local camera
        var cam = playerCameraObj.GetComponent<Camera>();
        if (cam != null)
        {
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain == null)
            {
                brain = cam.gameObject.AddComponent<Unity.Cinemachine.CinemachineBrain>();
                Debug.Log("[PlayerCameraSetup] 🧠 Added CinemachineBrain to PlayerCamera");
            }

            cam.enabled = false;
            cam.enabled = true;
            cam.gameObject.tag = "MainCamera";
            brain.enabled = true;

            Debug.Log($"[PlayerCameraSetup] ✅ Local CinemachineBrain activated for {cam.name}");
        }
        else
        {
            Debug.LogWarning("[PlayerCameraSetup] ⚠️ No Camera component found on PlayerCameraObj");
        }

        // 🎯 Cinemachine virtual camera setup
        var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
        if (cineCam != null)
        {
            cineCam.Follow = cameraTarget;
            cineCam.LookAt = cameraTarget;
        }

        // 📋 CameraManager registration
        if (CameraManager.Instance != null && cineCam != null)
        {
            CameraManager.Instance.UnregisterAllOwnedBy(OwnerClientId);
            CameraManager.Instance.RegisterCamera(cineCam, OwnerClientId);
        }

        Debug.Log($"[PlayerCameraSetup] ✅ Local camera fully activated for client {OwnerClientId}");
        Debug.Log($"[PlayerCameraSetup] MainCamera: {Camera.main?.name ?? "null"}");
        Debug.Log($"[PlayerCameraSetup] ActiveCineBrain: {FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>()?.gameObject.name ?? "none"}");
    }

    private IEnumerator ActivateAfterRootActive()
    {
        while (!gameObject.activeInHierarchy)
            yield return null;

        ActivateLocalCamera();
    }

    public override void OnNetworkDespawn()
    {
        if (CameraManager.Instance != null && playerCameraObj != null)
        {
            var cineCam = playerCameraObj.GetComponent<CinemachineCamera>();
            if (cineCam != null)
                CameraManager.Instance.UnregisterCamera(cineCam);
        }

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
            Debug.Log($"[PlayerCameraSetup] 🎥 Gained ownership for client {OwnerClientId}, initializing camera...");
            StopAllCoroutines();
            StartCoroutine(InitCameraDelayed());
        }
    }

}
