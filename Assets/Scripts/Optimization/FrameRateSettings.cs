using UnityEngine;

// Batas frame rate untuk seluruh game. Dijalankan otomatis sekali saat game dimulai,
// jadi tidak perlu dipasang ke GameObject atau scene mana pun
public static class FrameRateSettings
{
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        // VSync harus mati, kalau tidak targetFrameRate diabaikan di PC
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
