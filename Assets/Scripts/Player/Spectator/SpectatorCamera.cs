using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class SpectatorCamera : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;

    private Vector2 moveInput;
    private Transform camTransform;

    private bool followMode = false;
    private int targetIndex = 0;
    private Transform targetPlayer;

    // --- Replay ---
    private ReplayManager replayManager;
    private ReplayControls replayInput;
    private bool isRecording = false;

    // -------------------- UNITY LIFECYCLE --------------------
    private void Awake()
    {
        camTransform = transform;

        // Initialize input early
        replayInput = new ReplayControls();

        // Try to find ReplayManager in scene
        replayManager = FindFirstObjectByType<ReplayManager>();

        if (replayManager == null)
            Debug.LogWarning("[SpectatorCamera] ReplayManager not found in scene. Recording will be disabled until found.");
    }

    private void OnEnable()
    {
        replayInput.Enable();

        // Toggle Record (R key or assigned button)
        replayInput.Replay.RecordToggle.performed += _ =>
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
        };

        // Playback (T key or assigned button)
        replayInput.Replay.Playback.performed += _ =>
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
        };
    }

    private void OnDisable()
    {
        replayInput.Disable();
    }

    // -------------------- MOVEMENT --------------------
    private void Update()
    {
        if (followMode && targetPlayer != null)
        {
            camTransform.position = targetPlayer.position + new Vector3(0, 15f, -15f);
            camTransform.LookAt(targetPlayer);
        }
        else
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
    }

    // -------------------- INPUT HANDLERS --------------------
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnToggleFollow(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        followMode = !followMode;

        if (followMode)
        {
            SetTargetPlayer(0);
        }
        else
        {
            targetPlayer = null;
        }
    }

    public void OnNextPlayer(InputAction.CallbackContext context)
    {
        if (!context.performed || !followMode) return;

        var players = GetConnectedPlayers();
        if (players.Count == 0) return;

        targetIndex = (targetIndex + 1) % players.Count;
        SetTargetPlayer(targetIndex);
    }

    // -------------------- UTILITY --------------------
    private void SetTargetPlayer(int index)
    {
        var players = GetConnectedPlayers();
        if (players.Count == 0) return;

        targetIndex = Mathf.Clamp(index, 0, players.Count - 1);
        targetPlayer = players[targetIndex];
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
