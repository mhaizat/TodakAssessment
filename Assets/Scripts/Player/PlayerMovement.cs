using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerMovement : NetworkBehaviour
{
    // -------------------------
    // INPUT SETTINGS
    // -------------------------
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction; // Assign in Inspector
    public float moveSpeed = 5f;

    private CharacterController controller;

    // -------------------------
    // PREDICTION & RECONCILIATION
    // -------------------------
    private Vector3 serverPosition;

    private struct InputState
    {
        public Vector2 input;
        public float deltaTime;
        public int tick;
    }

    private readonly List<InputState> inputBuffer = new();
    private int tickCounter = 0;

    // -------------------------
    // UNITY LIFECYCLE
    // -------------------------
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable() => moveAction.action.Enable();
    private void OnDisable() => moveAction.action.Disable();

    private void Start()
    {
        serverPosition = transform.position;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
    }

    // -------------------------
    // CLIENT-SIDE MOVEMENT
    // -------------------------
    private void HandleMovement()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        if (input.sqrMagnitude < 0.0001f) return;

        float deltaTime = Time.deltaTime;

        // Client-side prediction
        Vector3 move = new Vector3(input.x, 0, input.y) * moveSpeed * deltaTime;
        controller.Move(move);

        // Record input for reconciliation
        tickCounter++;
        InputState state = new InputState { input = input, deltaTime = deltaTime, tick = tickCounter };
        inputBuffer.Add(state);

        // Send input to server for authoritative processing
        SendMovementInputToServerRpc(input, deltaTime, tickCounter);
    }

    // -------------------------
    // SERVER-SIDE MOVEMENT
    // -------------------------
    [ServerRpc]
    private void SendMovementInputToServerRpc(Vector2 input, float deltaTime, int tick, ServerRpcParams rpcParams = default)
    {
        Vector3 move = new Vector3(input.x, 0, input.y) * moveSpeed * deltaTime;

        if (TryGetComponent(out CharacterController cc))
        {
            cc.Move(move);
        }

        serverPosition = transform.position;

        // Notify client of authoritative position
        UpdateClientPositionClientRpc(serverPosition, tick, rpcParams.Receive.SenderClientId);
    }

    // -------------------------
    // CLIENT-SIDE RECONCILIATION
    // -------------------------
    [ClientRpc]
    private void UpdateClientPositionClientRpc(Vector3 authoritativePosition, int lastProcessedTick, ulong targetClientId)
    {
        if (!IsOwner) return;

        // Snap to server position
        transform.position = authoritativePosition;
        serverPosition = authoritativePosition;

        // Remove inputs already processed by the server
        inputBuffer.RemoveAll(input => input.tick <= lastProcessedTick);

        // Replay remaining inputs for smooth prediction
        foreach (var input in inputBuffer)
        {
            Vector3 move = new Vector3(input.input.x, 0, input.input.y) * moveSpeed * input.deltaTime;
            controller.Move(move);
        }
    }
}
