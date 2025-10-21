using System.Collections.Generic;
using UnityEngine;

public class ReplayManager : MonoBehaviour
{
    // -------------------------
    // SINGLETON
    // -------------------------
    public static ReplayManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // -------------------------
    // SETTINGS & PREFABS
    // -------------------------
    [Header("Ghost Settings")]
    [SerializeField] private GameObject ghostPrefab;

    // -------------------------
    // STATE
    // -------------------------
    private bool isRecording = false;
    private bool isPlayingBack = false;
    private bool isPaused = false;
    private float playbackTime = 0f;
    private float startTime = 0f;
    private float playbackSpeed = 1f;

    public bool IsPlaying => isPlayingBack && !isPaused;

    // -------------------------
    // DATA STRUCTURES
    // -------------------------
    private readonly List<ReplayFrame> frames = new();
    private readonly List<GameObject> ghostInstances = new();
    private readonly Dictionary<string, GameObject> ghostMap = new();

    private class ReplayFrame
    {
        public readonly float timestamp;
        public readonly List<TransformSnapshot> snapshots;

        public ReplayFrame(float timestamp, List<TransformSnapshot> snapshots)
        {
            this.timestamp = timestamp;
            this.snapshots = snapshots;
        }
    }

    private class TransformSnapshot
    {
        public readonly string playerId;
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        public TransformSnapshot(string id, Transform t)
        {
            playerId = id;
            position = t.position;
            rotation = t.rotation;
        }
    }

    // -------------------------
    // UNITY LIFECYCLE
    // -------------------------
    private void Update()
    {
        if (isRecording)
            RecordAllPlayers();
        else if (isPlayingBack && !isPaused)
            PlaybackUpdate();
    }

    // -------------------------
    // RECORDING
    // -------------------------
    public void StartRecording()
    {
        isRecording = true;
        isPlayingBack = false;
        isPaused = false;
        frames.Clear();
        startTime = Time.time;
        Debug.Log("[ReplayManager] Recording started.");
    }

    public void StopRecording()
    {
        isRecording = false;
        Debug.Log($"[ReplayManager] Recording stopped. Total frames: {frames.Count}");
    }

    private void RecordAllPlayers()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        List<TransformSnapshot> snapshots = new();

        foreach (var player in players)
            snapshots.Add(new TransformSnapshot(player.name, player.transform));

        float timestamp = Time.time - startTime;
        frames.Add(new ReplayFrame(timestamp, snapshots));
    }

    // -------------------------
    // PLAYBACK
    // -------------------------
    public void StartPlayback()
    {
        if (frames.Count == 0)
        {
            Debug.LogWarning("[ReplayManager] No frames to play back!");
            return;
        }

        isRecording = false;
        isPlayingBack = true;
        isPaused = false;
        playbackTime = 0f;

        SpawnGhosts();
        Debug.Log("[ReplayManager] Ghost playback started.");
    }

    public void StopPlayback()
    {
        if (!isPlayingBack) return;

        isPlayingBack = false;
        isPaused = false;
        ClearGhosts();
        Debug.Log("[ReplayManager] Playback stopped and ghosts cleared.");
    }

    public void TogglePause()
    {
        if (!isPlayingBack) return;
        isPaused = !isPaused;
        Debug.Log(isPaused ? "[ReplayManager] Paused playback." : "[ReplayManager] Resumed playback.");
    }

    private void PlaybackUpdate()
    {
        if (frames.Count < 2) return;

        playbackTime += Time.deltaTime * playbackSpeed;

        if (playbackTime >= frames[^1].timestamp)
        {
            StopPlayback();
            Debug.Log("[ReplayManager] Playback finished.");
            return;
        }

        int nextIndex = frames.FindIndex(f => f.timestamp > playbackTime);
        if (nextIndex <= 0) nextIndex = 1;

        var prev = frames[nextIndex - 1];
        var next = frames[nextIndex];
        float t = Mathf.InverseLerp(prev.timestamp, next.timestamp, playbackTime);

        foreach (var snapshotPrev in prev.snapshots)
        {
            if (!ghostMap.TryGetValue(snapshotPrev.playerId, out var ghost)) continue;

            var snapshotNext = next.snapshots.Find(s => s.playerId == snapshotPrev.playerId);
            if (snapshotNext == null) continue;

            Vector3 interpPos = Vector3.Lerp(snapshotPrev.position, snapshotNext.position, t);
            Quaternion interpRot = Quaternion.Slerp(snapshotPrev.rotation, snapshotNext.rotation, t);
            ghost.transform.SetPositionAndRotation(interpPos, interpRot);
        }
    }

    // -------------------------
    // GHOST HANDLING
    // -------------------------
    private void SpawnGhosts()
    {
        ClearGhosts();
        ghostMap.Clear();

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            var ghost = Instantiate(ghostPrefab, player.transform.position, player.transform.rotation);
            ghost.name = $"Ghost_{player.name}";

            var r = ghost.GetComponentInChildren<Renderer>();
            if (r != null)
                r.material.color = new Color(0f, 1f, 1f, 0.5f);

            ghostInstances.Add(ghost);
            ghostMap[player.name] = ghost;
        }
    }

    private void ClearGhosts()
    {
        foreach (var g in ghostInstances)
            if (g != null) Destroy(g);
        ghostInstances.Clear();
    }

    // -------------------------
    // PLAYBACK CONTROLS / UTILITIES
    // -------------------------
    public void SetPlaybackSpeed(float newSpeed) => playbackSpeed = Mathf.Clamp(newSpeed, 0.1f, 5f);

    public void Rewind(float seconds)
    {
        if (!isPlayingBack) return;
        playbackTime = Mathf.Max(0f, playbackTime - seconds);
    }

    public void FastForward(float seconds)
    {
        if (!isPlayingBack) return;
        playbackTime = Mathf.Min(frames[^1].timestamp, playbackTime + seconds);
    }

    public float GetPlaybackProgress()
    {
        if (frames.Count == 0) return 0f;
        return Mathf.Clamp01(playbackTime / frames[^1].timestamp);
    }

    public void SeekToProgress(float normalizedValue)
    {
        if (frames.Count == 0) return;
        playbackTime = Mathf.Lerp(0f, frames[^1].timestamp, normalizedValue);
    }
}
