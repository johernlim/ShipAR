using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Owns the experience UI and layered audio. Keeping the button listeners in
/// one runtime component makes the controls reliable in player builds and does
/// not depend on Visual Scripting event-unit serialization.
/// </summary>
public sealed class KrakensEmbraceRuntimeController : MonoBehaviour
{
    [SerializeField] ARTrackedImageManager trackedImageManager;
    [SerializeField] Button soundButton;
    [SerializeField] TMP_Text soundButtonLabel;
    [SerializeField] Button infoButton;
    [SerializeField] TMP_Text infoButtonLabel;
    [SerializeField] GameObject infoPanel;
    [SerializeField] AudioSource themeAudioSource;
    [SerializeField] AudioSource oceanAudioSource;

    bool soundEnabled = true;
    bool markerTracked;
    bool audioStarted;

    public void Configure(
        ARTrackedImageManager imageManager,
        Button soundToggle,
        TMP_Text soundLabel,
        Button informationToggle,
        TMP_Text informationLabel,
        GameObject informationPanel,
        AudioSource themeSource,
        AudioSource oceanSource)
    {
        trackedImageManager = imageManager;
        soundButton = soundToggle;
        soundButtonLabel = soundLabel;
        infoButton = informationToggle;
        infoButtonLabel = informationLabel;
        infoPanel = informationPanel;
        themeAudioSource = themeSource;
        oceanAudioSource = oceanSource;
    }

    void Awake()
    {
        if (soundButton != null)
            soundButton.onClick.AddListener(ToggleSound);
        if (infoButton != null)
            infoButton.onClick.AddListener(ToggleInfo);

        soundEnabled = true;
        markerTracked = false;
        audioStarted = false;
        SetAudioLabel();
        SetInfoVisible(false);
        PauseAudio();
    }

    void OnEnable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void Start()
    {
        RefreshTrackingState();
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        PauseAudio();
    }

    void OnDestroy()
    {
        if (soundButton != null)
            soundButton.onClick.RemoveListener(ToggleSound);
        if (infoButton != null)
            infoButton.onClick.RemoveListener(ToggleInfo);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            PauseAudio();
        else
            RefreshAudio();
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        RefreshTrackingState();
    }

    void RefreshTrackingState()
    {
        var isTracking = false;
        if (trackedImageManager != null)
        {
            foreach (var trackedImage in trackedImageManager.trackables)
            {
                if (trackedImage.trackingState == TrackingState.Tracking)
                {
                    isTracking = true;
                    break;
                }
            }
        }

        markerTracked = isTracking;
        RefreshAudio();
    }

    public void ToggleSound()
    {
        soundEnabled = !soundEnabled;
        SetAudioLabel();
        RefreshAudio();
    }

    public void ToggleInfo()
    {
        SetInfoVisible(infoPanel != null && !infoPanel.activeSelf);
    }

    void SetAudioLabel()
    {
        if (soundButtonLabel != null)
            soundButtonLabel.text = soundEnabled ? "Sound On" : "Sound Off";
    }

    void SetInfoVisible(bool visible)
    {
        if (infoPanel != null)
            infoPanel.SetActive(visible);
        if (infoButtonLabel != null)
            infoButtonLabel.text = visible ? "Close" : "Info";
    }

    void RefreshAudio()
    {
        if (!soundEnabled || !markerTracked)
        {
            PauseAudio();
            return;
        }

        if (audioStarted)
        {
            UnPause(themeAudioSource);
            UnPause(oceanAudioSource);
        }
        else
        {
            Play(themeAudioSource);
            Play(oceanAudioSource);
            audioStarted = true;
        }
    }

    void PauseAudio()
    {
        if (themeAudioSource != null)
            themeAudioSource.Pause();
        if (oceanAudioSource != null)
            oceanAudioSource.Pause();
    }

    static void Play(AudioSource source)
    {
        if (source != null && source.clip != null)
            source.Play();
    }

    static void UnPause(AudioSource source)
    {
        if (source != null && source.clip != null)
            source.UnPause();
    }
}
