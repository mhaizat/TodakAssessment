using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction; // Assign in Inspector
    public float moveSpeed = 5f;

    [Header("Prediction & Reconciliation")]
    private CharacterController controller;

    // Server authoritative position
    private Vector3 serverPosition;

    // Tick-based input prediction
    private struct InputState
    {
        public Vector2 input;
        public float deltaTime;
        public int tick;
    }

    private readonly List<InputState> inputBuffer = new();
    private int tickCounter = 0;

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

        // ----- 1. Read input -----
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        if (input.sqrMagnitude < 0.0001f) return;

        float deltaTime = Time.deltaTime;

        // ----- 2. Apply client-side prediction -----
        Vector3 move = new Vector3(input.x, 0, input.y) * moveSpeed * deltaTime;
        controller.Move(move);

        // ----- 3. Record input in local buffer -----
        tickCounter++;
        InputState state = new InputState { input = input, deltaTime = deltaTime, tick = tickCounter };
        inputBuffer.Add(state);

        // ----- 4. Send input to server -----
        SendMovementInputToServerRpc(input, deltaTime, tickCounter);
    }

    // ---------------- Server-side authoritative movement ----------------
    [ServerRpc]
    private void SendMovementInputToServerRpc(Vector2 input, float deltaTime, int tick, ServerRpcParams rpcParams = default)
    {
        Vector3 move = new Vector3(input.x, 0, input.y) * moveSpeed * deltaTime;

        // Apply movement on server
        if (TryGetComponent(out CharacterController cc))
        {
            cc.Move(move);
        }

        // Update authoritative position
        serverPosition = transform.position;

        // Send back authoritative position + last processed tick
        UpdateClientPositionClientRpc(serverPosition, tick, rpcParams.Receive.SenderClientId);
    }

    // ---------------- Client-side reconciliation ----------------
    [ClientRpc]
    private void UpdateClientPositionClientRpc(Vector3 authoritativePosition, int lastProcessedTick, ulong targetClientId)
    {
        if (!IsOwner) return;

        // Step 1: Snap to server position
        transform.position = authoritativePosition;
        serverPosition = authoritativePosition;

        // Step 2: Remove acknowledged inputs from buffer
        inputBuffer.RemoveAll(input => input.tick <= lastProcessedTick);

        // Step 3: Reapply unprocessed inputs
        foreach (var input in inputBuffer)
        {
            Vector3 move = new Vector3(input.input.x, 0, input.input.y) * moveSpeed * input.deltaTime;
            controller.Move(move);
        }
    }
}
