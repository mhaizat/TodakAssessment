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

    // Authoritative position from the server
    private Vector3 serverPosition;
    private float reconciliationSpeed = 10f; // tweak for smoothness

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
    }

    private void Start()
    {
        // Initialize serverPosition to current position
        serverPosition = transform.position;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // ----- 1. Read input -----
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y) * moveSpeed * Time.deltaTime;

        // ----- 2. Client-side prediction -----
        controller.Move(move);

        // ----- 3. Send input to server -----
        if (move.sqrMagnitude > 0.0001f)
        {
            SendMovementInputToServerRpc(input);
        }

        // ----- 4. Smooth reconciliation -----
        if (Vector3.Distance(transform.position, serverPosition) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, serverPosition, reconciliationSpeed * Time.deltaTime);
        }
    }

    // ---------------- Server-side authoritative movement ----------------
    [ServerRpc]
    private void SendMovementInputToServerRpc(Vector2 input, ServerRpcParams rpcParams = default)
    {
        float deltaTime = Time.fixedDeltaTime; // optional, server delta
        Vector3 move = new Vector3(input.x, 0, input.y) * moveSpeed * deltaTime;

        // Update server authoritative position
        serverPosition = transform.position + move;

        // Move the server-side CharacterController if needed
        if (TryGetComponent(out CharacterController cc))
        {
            cc.Move(move);
        }

        // Send the authoritative position back to the client for reconciliation
        UpdateClientPositionClientRpc(serverPosition, rpcParams.Receive.SenderClientId);
    }

    // ---------------- Client-side reconciliation ----------------
    [ClientRpc]
    private void UpdateClientPositionClientRpc(Vector3 authoritativePosition, ulong targetClientId)
    {
        if (!IsOwner) return;

        // Update serverPosition for smooth reconciliation
        serverPosition = authoritativePosition;
    }
}
