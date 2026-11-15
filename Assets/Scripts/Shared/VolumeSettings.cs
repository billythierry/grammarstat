using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Pengaturan volume musik dan SFX dari slider di menu pengaturan (main menu dan menu pause).
// Nilai 0-1 disimpan di PlayerPrefs sehingga berlaku di semua scene dan tetap diingat setelah game ditutup.
// Musik = AudioSource di Main Camera atau objek Music Object Name. SFX = AudioSource milik AudioManager.
// Volume asli tiap AudioSource menjadi patokan 100%
public class VolumeSettings : MonoBehaviour
{
    private const string MusicKey = "Volume_Music";
    private const string SfxKey = "Volume_SFX";

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [Tooltip("Nama objek musik latar bila musiknya tidak berada di Main Camera (mis. di main menu)")]
    [SerializeField] private string musicObjectName = "BackgroundMusic";

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicKey, 1f);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxKey, 1f);

    // Volume asli tiap AudioSource saat pertama kali diatur
    private static readonly Dictionary<AudioSource, float> baseVolumes = new Dictionary<AudioSource, float>();

    // Start: AudioManager sudah selesai Awake, termasuk AudioSource UI miliknya
    void Start()
    {
        SetupSlider(musicSlider, MusicVolume, OnMusicChanged);
        SetupSlider(sfxSlider, SfxVolume, OnSfxChanged);
        ApplyMusic();
        ApplySfx();
    }

    void OnDestroy()
    {
        PlayerPrefs.Save();
    }

    static void SetupSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);
    }

    void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicKey, value);
        ApplyMusic();
    }

    void OnSfxChanged(float value)
    {
        PlayerPrefs.SetFloat(SfxKey, value);
        ApplySfx();
    }

    void ApplyMusic()
    {
        Camera cam = Camera.main;
        if (cam != null) Apply(cam.GetComponents<AudioSource>(), MusicVolume);
        GameObject musicObject = string.IsNullOrEmpty(musicObjectName) ? null : GameObject.Find(musicObjectName);
        if (musicObject != null) Apply(musicObject.GetComponents<AudioSource>(), MusicVolume);
    }

    static void ApplySfx()
    {
        if (AudioManager.Instance != null) Apply(AudioManager.Instance.GetComponents<AudioSource>(), SfxVolume);
    }

    static void Apply(AudioSource[] sources, float volume)
    {
        foreach (AudioSource source in sources)
        {
            if (!baseVolumes.TryGetValue(source, out float baseVolume))
            {
                baseVolume = source.volume;
                baseVolumes[source] = baseVolume;
            }
            source.volume = baseVolume * volume;
        }
        // AudioSource dari scene sebelumnya sudah hancur, buang dari daftar
        var destroyed = new List<AudioSource>();
        foreach (AudioSource source in baseVolumes.Keys) if (source == null) destroyed.Add(source);
        foreach (AudioSource source in destroyed) baseVolumes.Remove(source);
    }
}
