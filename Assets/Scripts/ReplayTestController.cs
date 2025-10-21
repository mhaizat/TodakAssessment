using UnityEngine;

public class ReplayTestController : MonoBehaviour
{
    private ReplayManager replayManager;
    private ReplayControls replayInput; // auto-generated input class
    private bool isRecording = false;

    private void Awake()
    {
        replayInput = new ReplayControls();
    }

    private void OnEnable()
    {
        replayInput.Enable();

        replayInput.Replay.RecordToggle.performed += _ =>
        {
            if (!isRecording)
            {
                replayManager.StartRecording();
                Debug.Log("🎥 Started recording inputs.");
            }
            else
            {
                replayManager.StopRecording();
                Debug.Log("⏹️ Stopped recording inputs.");
            }

            isRecording = !isRecording;
        };

        replayInput.Replay.Playback.performed += _ =>
        {
            replayManager.StartPlayback();
            Debug.Log("▶️ Started playback.");
        };
    }

    private void OnDisable()
    {
        replayInput.Disable();
    }

    private void Start()
    {
        replayManager = Object.FindFirstObjectByType<ReplayManager>();
    }
}
