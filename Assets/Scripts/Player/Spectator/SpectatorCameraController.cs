using UnityEngine;
using System.Collections;

public class SpectatorCanvasController : MonoBehaviour
{
    [SerializeField] private Canvas spectatorCanvas;

    private void Start()
    {
        if (spectatorCanvas == null) return;

        StartCoroutine(EnableIfSpectator());
    }

    private IEnumerator EnableIfSpectator()
    {
        // Wait until LobbyManager exists
        while (LobbyManager.Instance == null || LobbyManager.Instance.playersByUniqueId == null)
            yield return null;

        // Wait until local player data is available
        string playerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        while (!LobbyManager.Instance.playersByUniqueId.ContainsKey(playerId))
            yield return null;

        var pdata = LobbyManager.Instance.playersByUniqueId[playerId];

        spectatorCanvas.gameObject.SetActive(pdata.IsSpectator);
    }
}
