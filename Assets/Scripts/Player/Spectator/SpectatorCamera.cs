using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class SpectatorCamera : MonoBehaviour
{
    // -------------------------
    // CAMERA SETTINGS
    // -------------------------
    [Header("Movement Settings")]
    public float moveSpeed = 10f;

    private Vector2 moveInput;
    private Transform camTransform;

    private bool followMode = false;
    private int targetIndex = 0;
    private Transform targetPlayer;

    // -------------------------
    // REPLAY SYSTEM
    // -------------------------
    private ReplayManager replayManager;
    private ReplayControls replayInput;
    private bool isRecording = false;

    // -------------------------
    // UNITY LIFECYCLE
    // -------------------------
    private void Awake()
    {
        camTransform = transform;

        // Initialize input
        replayInput = new ReplayControls();

        // Find ReplayManager in the scene
        replayManager = FindFirstObjectByType<ReplayManager>();
        if (replayManager == null)
            Debug.LogWarning("[SpectatorCamera] ReplayManager not found in scene. Recording disabled until found.");
    }

    private void OnEnable()
    {
        replayInput.Enable();

        // Record toggle (R key)
        replayInput.Replay.RecordToggle.performed += _ => ToggleRecording();

        // Playback (T key)
        replayInput.Replay.Playback.performed += _ => StartPlayback();
    }

    private void OnDisable()
    {
        replayInput.Disable();
    }

    private void Update()
    {
        if (followMode && targetPlayer != null)
        {
            FollowTarget();
        }
        else
        {
            FreeMove();
        }
    }

    // -------------------------
    // CAMERA MOVEMENT
    // -------------------------
    private void FreeMove()
    {
        Vector3 forward = camTransform.forward;
        Vector3 right = camTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = (forward * moveInput.y + right * moveInput.x) * moveSpeed * Time.deltaTime;
        camTransform.position += move;
    }

    private void FollowTarget()
    {
        camTransform.position = targetPlayer.position + new Vector3(0, 15f, -15f);
        camTransform.LookAt(targetPlayer);
    }

    // -------------------------
    // INPUT HANDLERS
    // -------------------------
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnToggleFollow(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        followMode = !followMode;
        targetPlayer = followMode ? GetTargetPlayer(0) : null;
    }

    public void OnNextPlayer(InputAction.CallbackContext context)
    {
        if (!context.performed || !followMode) return;

        var players = GetConnectedPlayers();
        if (players.Count == 0) return;

        targetIndex = (targetIndex + 1) % players.Count;
        targetPlayer = GetTargetPlayer(targetIndex);
    }

    // -------------------------
    // REPLAY FUNCTIONS
    // -------------------------
    private void ToggleRecording()
    {
        if (replayManager == null)
        {
            replayManager = FindFirstObjectByType<ReplayManager>();
            if (replayManager == null)
            {
                Debug.LogWarning("[SpectatorCamera] No ReplayManager found, cannot record.");
                return;
            }
        }

        if (!isRecording)
        {
            replayManager.StartRecording();
            Debug.Log("🎥 Started recording gameplay.");
        }
        else
        {
            replayManager.StopRecording();
            Debug.Log("⏹️ Stopped recording gameplay.");
        }

        isRecording = !isRecording;
    }

    private void StartPlayback()
    {
        if (replayManager == null)
        {
            replayManager = FindFirstObjectByType<ReplayManager>();
            if (replayManager == null)
            {
                Debug.LogWarning("[SpectatorCamera] No ReplayManager found, cannot start playback.");
                return;
            }
        }

        replayManager.StartPlayback();
        Debug.Log("▶️ Started playback.");
    }

    // -------------------------
    // PLAYER TARGETING / UTILITY
    // -------------------------
    private Transform GetTargetPlayer(int index)
    {
        var players = GetConnectedPlayers();
        if (players.Count == 0) return null;

        targetIndex = Mathf.Clamp(index, 0, players.Count - 1);
        return players[targetIndex];
    }

    private List<Transform> GetConnectedPlayers()
    {
        List<Transform> list = new();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
                list.Add(client.PlayerObject.transform);
        }
        return list;
    }
}
