using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Video;
using MathNet.Numerics.Statistics;
using System;
using System.Linq;
using UnityEngine.XR;
using Accord.Statistics.Distributions.Univariate;
using static IntensityManager;
using Oculus.Interaction;
using Unity.VisualScripting;

public class VideoScript : MonoBehaviour
{
    public VideoPlayer video;
    public GameObject canvas;
    public GameObject camera;
    public GameObject centerEye;
    private uint currentFrame;
    private bool videoPaused;
    private UIManager uiManager;
    private DataLogger dataLogger;
    private IntensityManager intensityManager;
    private bool hasVibrated = false;

    private const float VibrationFrequency = 1.0f;
    private const float VibrationAmplitude = 1.0f;
    private const float VibrationDuration = 0.1f;
    private const float VibrationPause = 0.1f;

    void Start()
    {
        InitializeComponents();
        SetupVideoPlayer();
        videoPaused = false;
        currentFrame = 0;
        canvas.transform.LookAt(centerEye.transform);
        canvas.transform.Rotate(0, 180, 0);
    }

    void Update()
    {
        currentFrame++;
        Vector3 headPosition = centerEye.transform.position;
        uiManager.AdjustAdaptiveVideoFOV();
        if (OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch))
            uiManager.MoveVideoPosition();

        HandleControllerInput(headPosition);
        dataLogger.EnqueueRecentData(headPosition);
        if (currentFrame >= intensityManager.intensityUpdateRate)
        {
            currentFrame = 0;
            //intensityManager.ComputeIntensity(dataLogger.recentHeadPositionData);
        }
    }

    private void InitializeComponents()
    {
        uiManager = GetComponent<UIManager>();
        dataLogger = GetComponent<DataLogger>();
        intensityManager = GetComponent<IntensityManager>();

        if (uiManager == null) { Debug.LogError("UIManager not found in scene"); }
        if (dataLogger == null) { Debug.LogError("DataLogger not found in scene"); }
        if (intensityManager == null) { Debug.LogError("IntensityManager not found in scene"); }
    }

    private void SetupVideoPlayer()
    {
        string videoPath = Path.Combine(Application.streamingAssetsPath, "familyguy.mp4");
        if (!File.Exists(videoPath))
        {
            Debug.LogError($"Video file not found at {videoPath}");
            return;
        }

        video.source = VideoSource.Url;
        video.url = "file://" + videoPath;
        video.Prepare();
    }

    private void HandleControllerInput(Vector3 centerEyePosition)
    {
        if (OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch))
        {
            if (OVRInput.GetDown(OVRInput.RawButton.Y))
            {
                dataLogger.StartOrStopRecording();
            }
        }

        if (OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch))
        {
            HandleRightControllerInput(centerEyePosition);
        }

        if (!videoPaused)
        {
            dataLogger.EnqueueData(centerEyePosition);
        }
    }

    private void HandleRightControllerInput(Vector3 centerEyePosition)
    {
        float verticalInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y;
        float horizontalInput = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;

        if (OVRInput.Get(OVRInput.RawButton.RHandTrigger))
        {
            HandleHandTriggerInput(horizontalInput, verticalInput);
        }
        else
        {
            if (!uiManager.SettingsMenuEnabled)
                HandleThumbstickInput(verticalInput);
        }
    }

    private void HandleHandTriggerInput(float horizontalInput, float verticalInput)
    {
        if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger)) // Toggle settings menu
        {
            uiManager.ToggleSettingsMenu();
        }

        if (uiManager.SettingsMenuEnabled)
            return;

        if (OVRInput.GetDown(OVRInput.RawButton.A)) // Restart video
        {
            video.time = 0;
            video.Stop();
        }

        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickDown)) // Cancel recording
        {
            dataLogger.CancelRecording();
        }

        if (video.isPrepared && video.isPlaying)
        {
            HandleVideoPlayback(horizontalInput, verticalInput);
        }
    }

    private void HandleVideoPlayback(float horizontalInput, float verticalInput)
    {
        if (horizontalInput > 0.5f) // Fast forward 
        {
            video.playbackSpeed = 4;
        }
        else if (horizontalInput < -0.5f) // Skip backwards a little
        {
            video.time -= 10;
        }
        else if (verticalInput > 0.5f) // Go forward 1 minute
        {
            video.time += 60;
        }
        else if (verticalInput < -0.5f) // Go backwards 1 minute
        {
            video.time -= 60;
        }
        else // Set to normal playback
        {
            video.playbackSpeed = 1;
        }
    }

    private void HandleThumbstickInput(float verticalInput)
    {
        if (OVRInput.GetDown(OVRInput.RawButton.A)) // Start/Pause
        {
            TogglePause();
        }

        if (OVRInput.GetDown(OVRInput.RawButton.B)) // Change method/viewing experience
        {
            int currentViewingExperience = uiManager.ChangeViewingExperience();
            StartCoroutine(VibrateXTimes(currentViewingExperience));
        }

        if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger)) // Change intensity (Low -> Medium -> High)
        {
            intensityManager.ChangeIntensityLevel();
            switch (intensityManager.CurrentIntensity)
            {
                case IntensityLevel.Low:
                    StartCoroutine(VibrateXTimes(1));
                    break;
                case IntensityLevel.Medium:
                    StartCoroutine(VibrateXTimes(2));
                    break;
                case IntensityLevel.High:
                    StartCoroutine(VibrateXTimes(3));
                    break;
            }
        }

        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickDown)) // Start video and record head movement for X minutes
        {
            dataLogger.StartOrStopRecording();
        }

        HandleRecordingDurationAdjustment(verticalInput);
    }

    private void HandleRecordingDurationAdjustment(float verticalInput)
    {
        if (verticalInput > 0.5f && !hasVibrated) // Recording length + 1min
        {
            if (dataLogger.RecordingDuration < 5)
            {
                dataLogger.RecordingDuration += 1;
                hasVibrated = true;
            }
            StartCoroutine(VibrateXTimes(dataLogger.RecordingDuration));
        }
        else if (verticalInput < -0.5f && !hasVibrated) // Recording length - 1min
        {
            if (dataLogger.RecordingDuration > 1)
            {
                dataLogger.RecordingDuration -= 1;
                hasVibrated = true;
            }
            StartCoroutine(VibrateXTimes(dataLogger.RecordingDuration));
        }
        else if (Mathf.Abs(verticalInput) < 0.1f && hasVibrated)
        {
            hasVibrated = false;
        }
    }

    private IEnumerator VibrateXTimes(int vibrations)
    {
        for (int i = 0; i < vibrations; i++)
        {
            OVRInput.SetControllerVibration(VibrationFrequency, VibrationAmplitude, OVRInput.Controller.RTouch);
            yield return new WaitForSeconds(VibrationDuration);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            yield return new WaitForSeconds(VibrationPause);
        }
    }
    private void TogglePause()
    {
        if (videoPaused)
        {
            video.Play();
        }
        else
        {
            video.Pause();
            Debug.Log($"Paused at time: {video.time} seconds");
        }
        videoPaused = !videoPaused;
    }

}
