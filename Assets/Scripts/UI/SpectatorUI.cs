using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the Spectator UI for playback controls (Play, Pause, Stop) and timeline slider.
/// </summary>
public class SpectatorUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Slider timelineSlider;

    // Reference to the singleton ReplayManager
    private ReplayManager replay => ReplayManager.Instance;

    // Track whether the user is currently dragging the slider
    private bool isDraggingSlider = false;

    #region Unity Lifecycle

    private void Start()
    {
        HookUpUIEvents();
    }

    private void Update()
    {
        UpdateSliderDuringPlayback();
    }

    #endregion

    #region UI Event Setup

    private void HookUpUIEvents()
    {
        // Buttons
        playButton.onClick.AddListener(OnPlayClicked);
        pauseButton.onClick.AddListener(OnPauseClicked);
        stopButton.onClick.AddListener(OnStopClicked);

        // Slider
        timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    #endregion

    #region Update Helpers

    private void UpdateSliderDuringPlayback()
    {
        if (replay == null) return;

        // Only update slider if playback is running and user is not dragging
        if (replay.IsPlaying && !isDraggingSlider)
        {
            timelineSlider.value = replay.GetPlaybackProgress();
        }
    }

    #endregion

    #region Button Callbacks

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

    #endregion

    #region Slider Callbacks

    private void OnSliderValueChanged(float value)
    {
        if (replay == null || isDraggingSlider) return;

        // Seek playback to slider position if not dragging
        replay.SeekToProgress(value);
    }

    /// <summary>Call from EventTrigger onBeginDrag</summary>
    public void OnBeginDrag()
    {
        isDraggingSlider = true;
    }

    /// <summary>Call from EventTrigger onEndDrag</summary>
    public void OnEndDrag()
    {
        if (replay == null) return;

        isDraggingSlider = false;

        // Seek to the frame corresponding to slider value
        replay.SeekToProgress(timelineSlider.value);
    }

    #endregion
}
