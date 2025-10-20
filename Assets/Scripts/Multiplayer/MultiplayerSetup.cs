using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class MultiplayerSetup : MonoBehaviour
{
    public string CurrentJoinCode { get; private set; }
    private UnityTransport transport;

    private async void Start()
    {
        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"Signed in! PlayerID: {AuthenticationService.Instance.PlayerId}");
    }

    public async Task CreateRelay()
    {
        try
        {
            // Create relay and get join code
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(5);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            CurrentJoinCode = joinCode;

            // Apply relay data to transport
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            // Start host
            NetworkManager.Singleton.StartHost();
            Debug.Log($"Relay created, join code is {joinCode}");

            // Spawn LobbyManager prefab after host started
            if (NetworkManager.Singleton.IsServer)
            {
                var prefab = Resources.Load<GameObject>("LobbyManager");
                if (prefab != null)
                {
                    var lobbyManagerObj = Instantiate(prefab);
                    var netObj = lobbyManagerObj.GetComponent<NetworkObject>();
                    if (netObj != null)
                    {
                        netObj.Spawn();

                        // Directly show join code in UI for host
                        LobbyUI.Instance?.DisplayJoinCode(CurrentJoinCode);

                        Debug.Log("LobbyManager spawned and networked successfully.");
                    }
                    else
                    {
                        Debug.LogError("LobbyManager prefab is missing a NetworkObject!");
                    }
                }
                else
                {
                    Debug.LogError("LobbyManager prefab not found in Resources folder!");
                }
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
        }
    }

    public async Task JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            // Apply relay data to UnityTransport
            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            // Small delay to ensure transport is fully initialized
            await Task.Delay(100);

            // Start the client
            NetworkManager.Singleton.StartClient();
            Debug.Log("Joined relay as client!");
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay Join failed: {e.Message}");
        }
    }
}
