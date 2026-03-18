using UnityEngine;

/// <summary>
/// Attach to a persistent GameObject in the first scene.
/// Sets global performance parameters for mobile.
/// </summary>
public class PerformanceBootstrap : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 60;
    [SerializeField] private bool disableVSync = true;
    [SerializeField] private bool preventScreenDimming = true;

    private void Awake()
    {
        Application.targetFrameRate = targetFrameRate;

        if (disableVSync)
            QualitySettings.vSyncCount = 0; // Required for targetFrameRate to work on Android

        if (preventScreenDimming)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    // Allow runtime adjustment (e.g., from a settings menu)
    public void SetTargetFrameRate(int fps)
    {
        targetFrameRate = Mathf.Clamp(fps, 30, 120);
        Application.targetFrameRate = targetFrameRate;
    }
}
