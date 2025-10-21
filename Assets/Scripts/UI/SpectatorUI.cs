using UnityEngine;
using UnityEngine.UI;

public class SpectatorUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Slider timelineSlider;

    private ReplayManager replay => ReplayManager.Instance;

    private bool isDraggingSlider = false;

    private void Start()
    {
        // Hook up buttons
        playButton.onClick.AddListener(OnPlayClicked);
        pauseButton.onClick.AddListener(OnPauseClicked);
        stopButton.onClick.AddListener(OnStopClicked);

        // Hook up slider events
        timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void Update()
    {
        // Update timeline slider dynamically while playback is running
        if (replay != null && replay.IsPlaying && !isDraggingSlider)
        {
            timelineSlider.value = replay.GetPlaybackProgress();
        }
    }

    // -------------------------
    // Button Callbacks
    // -------------------------
    private void OnPlayClicked()
    {
        if (replay == null) return;
        replay.StartPlayback();
    }

    private void OnPauseClicked()
    {
        if (replay == null) return;
        replay.TogglePause();
    }

    private void OnStopClicked()
    {
        if (replay == null) return;
        replay.StopPlayback();
        timelineSlider.value = 0f;
    }

    // -------------------------
    // Slider Callback
    // -------------------------
    private void OnSliderValueChanged(float value)
    {
        if (replay == null || isDraggingSlider) return;
        replay.SeekToProgress(value);
    }

    // Optional: For better control — call these from EventTriggers on slider
    public void OnBeginDrag() => isDraggingSlider = true;
    public void OnEndDrag()
    {
        isDraggingSlider = false;
        replay.SeekToProgress(timelineSlider.value);
    }
}
