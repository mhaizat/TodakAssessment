using UnityEngine;

public class TestMultiplayer : MonoBehaviour
{
    private MultiplayerSetup setup;

    private async void Start()
    {
        setup = FindFirstObjectByType<MultiplayerSetup>();

        // Uncomment ONE of these for testing
        await setup.CreateRelay(); // HOST
        // await setup.JoinRelay("YOUR_JOIN_CODE_HERE"); // CLIENT
    }
}
