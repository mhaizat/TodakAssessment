using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance;

    private readonly List<CinemachineCamera> cameras = new List<CinemachineCamera>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Registers a camera if it hasn't been registered yet.</summary>
    public void RegisterCamera(CinemachineCamera cam)
    {
        if (cam == null) return;

        if (!cameras.Contains(cam))
        {
            cameras.Add(cam);
            Debug.Log($"[CameraManager] Registered camera: {cam.name} (Parent: {cam.transform.root.name})");
        }
    }

    /// <summary>Unregisters a camera safely.</summary>
    public void UnregisterCamera(CinemachineCamera cam)
    {
        if (cam == null) return;

        if (cameras.Contains(cam))
        {
            cameras.Remove(cam);
            Debug.Log($"[CameraManager] Unregistered camera: {cam.name}");
        }
    }

    /// <summary>Returns a camera by index, or null if invalid.</summary>
    public CinemachineCamera GetCamera(int index)
    {
        if (index < 0 || index >= cameras.Count) return null;
        return cameras[index];
    }

    /// <summary>Returns the first camera currently active in the scene (usually local player camera).</summary>
    public CinemachineCamera GetLocalPlayerCamera()
    {
        return cameras.Find(cam => cam.gameObject.activeInHierarchy);
    }

    /// <summary>Returns all registered cameras.</summary>
    public List<CinemachineCamera> GetAllCameras()
    {
        return cameras;
    }

    /// <summary>Clear all cameras safely (optional, for scene unload).</summary>
    public void ClearAllCameras()
    {
        cameras.Clear();
        Debug.Log("[CameraManager] Cleared all registered cameras.");
    }
}
