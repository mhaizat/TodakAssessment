using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    // Store both the camera reference and who owns it
    private readonly Dictionary<ulong, CinemachineCamera> ownedCameras = new Dictionary<ulong, CinemachineCamera>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Registers a camera and associates it with a specific client ID.
    /// Automatically unregisters any existing camera for that client.
    /// </summary>
    public void RegisterCamera(CinemachineCamera cam, ulong ownerClientId)
    {
        if (cam == null)
        {
            Debug.LogWarning("[CameraManager] Tried to register a null camera.");
            return;
        }

        // Remove old camera owned by same client (if any)
        if (ownedCameras.TryGetValue(ownerClientId, out var existingCam))
        {
            if (existingCam != null && existingCam != cam)
            {
                Debug.Log($"[CameraManager] Replacing old camera ({existingCam.name}) for client {ownerClientId}");
                UnregisterCamera(existingCam);
            }
        }

        ownedCameras[ownerClientId] = cam;
        Debug.Log($"[CameraManager] Registered camera '{cam.name}' for client {ownerClientId}");
    }

    /// <summary>
    /// Unregisters a specific camera safely.
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
        if (ownedCameras.ContainsKey(ownerClientId))
        {
            var cam = ownedCameras[ownerClientId];
            ownedCameras.Remove(ownerClientId);

            if (cam != null)
                Debug.Log($"[CameraManager] Removed all cameras owned by client {ownerClientId} ({cam.name})");
        }
    }

    /// <summary>
    /// Returns the active camera owned by the given client, or null if none.
    /// </summary>
    public CinemachineCamera GetCameraByOwner(ulong ownerClientId)
    {
        if (ownedCameras.TryGetValue(ownerClientId, out var cam))
            return cam;

        return null;
    }

    /// <summary>
    /// Returns the first camera currently active in the scene (useful for debugging).
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
    /// Clears all registered cameras (useful for full scene reloads or shutdown).
    /// </summary>
    public void ClearAllCameras()
    {
        ownedCameras.Clear();
        Debug.Log("[CameraManager] Cleared all registered cameras.");
    }

    /// <summary>
    /// Returns all currently registered cameras for debugging.
    /// </summary>
    public List<CinemachineCamera> GetAllCameras()
    {
        return new List<CinemachineCamera>(ownedCameras.Values);
    }
}
