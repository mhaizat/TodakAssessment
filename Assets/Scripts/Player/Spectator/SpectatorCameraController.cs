using UnityEngine;
using System.Collections;
using Unity.Services.Authentication;

public class SpectatorCanvasController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas spectatorCanvas;

    // --------------------
    // UNITY LIFECYCLE
    // --------------------
    private void Start()
    {
        if (spectatorCanvas == null) return;

        StartCoroutine(EnableCanvasIfSpectator());
    }

    // --------------------
    // COROUTINES
    // --------------------
    private IEnumerator EnableCanvasIfSpectator()
    {
        // Wait until LobbyManager exists and has player data
        while (LobbyManager.Instance == null || LobbyManager.Instance.playersByUniqueId == null)
            yield return null;

        // Wait until local player data is available
        string playerId = AuthenticationService.Instance.PlayerId;
        while (!LobbyManager.Instance.playersByUniqueId.ContainsKey(playerId))
            yield return null;

        var pdata = LobbyManager.Instance.playersByUniqueId[playerId];

        spectatorCanvas.gameObject.SetActive(pdata.IsSpectator);
    }
}
