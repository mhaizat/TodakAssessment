using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class SpectatorCamera : MonoBehaviour
{
    public float moveSpeed = 10f;

    private Vector2 moveInput;
    private Transform camTransform;

    private bool followMode = false;
    private int targetIndex = 0;
    private Transform targetPlayer;

    private void Awake()
    {
        camTransform = transform;
    }

    // Input for free roam
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Input to toggle follow mode
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

    // Input to cycle to next player
    public void OnNextPlayer(InputAction.CallbackContext context)
    {
        if (!context.performed || !followMode) return;

        var players = GetConnectedPlayers();
        if (players.Count == 0) return;

        targetIndex = (targetIndex + 1) % players.Count;
        SetTargetPlayer(targetIndex);
    }

    private void Update()
    {
        if (followMode && targetPlayer != null)
        {
            camTransform.position = targetPlayer.position + new Vector3(0, 15f, -15f); // simple offset
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

    private List<Transform> GetConnectedPlayers()
    {
        List<Transform> list = new List<Transform>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
                list.Add(client.PlayerObject.transform);
        }
        return list;
    }

    private void SetTargetPlayer(int index)
    {
        var players = GetConnectedPlayers();
        if (players.Count == 0) return;

        targetIndex = Mathf.Clamp(index, 0, players.Count - 1);
        targetPlayer = players[targetIndex];
    }
}
