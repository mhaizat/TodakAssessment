using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

/// <summary>
/// Singleton manager to handle client-owned Cinemachine cameras.
/// </summary>
public class CameraManager : MonoBehaviour
{
    // -------------------------
    // SINGLETON
    // -------------------------
    public static CameraManager Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // -------------------------
    // CAMERA STORAGE
    // -------------------------
    // Stores cameras associated with a specific client ID
    private readonly Dictionary<ulong, CinemachineCamera> ownedCameras = new();

    // -------------------------
    // REGISTRATION
    // -------------------------
    /// <summary>
    /// Registers a camera and associates it with a specific client ID.
    /// Automatically replaces any existing camera for that client.
    /// </summary>
    public void RegisterCamera(CinemachineCamera cam, ulong ownerClientId)
    {
        if (cam == null)
        {
            Debug.LogWarning("[CameraManager] Tried to register a null camera.");
            return;
        }

        // Replace existing camera if needed
        if (ownedCameras.TryGetValue(ownerClientId, out var existingCam) && existingCam != cam)
        {
            Debug.Log($"[CameraManager] Replacing old camera ({existingCam.name}) for client {ownerClientId}");
            UnregisterCamera(existingCam);
        }

        ownedCameras[ownerClientId] = cam;
        Debug.Log($"[CameraManager] Registered camera '{cam.name}' for client {ownerClientId}");
    }

    /// <summary>
    /// Unregisters a specific camera.
    /// </summary>
    public void UnregisterCamera(CinemachineCamera cam)
    {
        if (cam == null) return;

        ulong? foundKey = null;
        foreach (var kvp in ownedCameras)
        {
            if (kvp.Value == cam)
            {
                foundKey = kvp.Key;
                break;
            }
        }

        if (foundKey.HasValue)
        {
            ownedCameras.Remove(foundKey.Value);
            Debug.Log($"[CameraManager] Unregistered camera '{cam.name}' (Client {foundKey.Value})");
        }
    }

    /// <summary>
    /// Unregisters all cameras owned by a specific client.
    /// </summary>
    public void UnregisterAllOwnedBy(ulong ownerClientId)
    {
        if (ownedCameras.TryGetValue(ownerClientId, out var cam))
        {
            ownedCameras.Remove(ownerClientId);
            if (cam != null)
                Debug.Log($"[CameraManager] Removed all cameras owned by client {ownerClientId} ({cam.name})");
        }
    }

    // -------------------------
    // GETTERS
    // -------------------------
    /// <summary>
    /// Returns the camera owned by the specified client, or null if none.
    /// </summary>
    public CinemachineCamera GetCameraByOwner(ulong ownerClientId)
    {
        return ownedCameras.TryGetValue(ownerClientId, out var cam) ? cam : null;
    }

    /// <summary>
    /// Returns the first active camera in the scene (useful for debugging).
    /// </summary>
    public CinemachineCamera GetActiveCamera()
    {
        foreach (var cam in ownedCameras.Values)
        {
            if (cam != null && cam.gameObject.activeInHierarchy)
                return cam;
        }
        return null;
    }

    /// <summary>
    /// Returns all currently registered cameras (for debugging).
    /// </summary>
    public List<CinemachineCamera> GetAllCameras()
    {
        return new List<CinemachineCamera>(ownedCameras.Values);
    }

    // -------------------------
    // UTILITY
    // -------------------------
    /// <summary>
    /// Clears all registered cameras.
    /// </summary>
    public void ClearAllCameras()
    {
        ownedCameras.Clear();
        Debug.Log("[CameraManager] Cleared all registered cameras.");
    }
}
