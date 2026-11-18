using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum CutsceneSpeaker
{
    Narrator,
    Thomas,
    Shelby,
    MrMister,
    ThomasAndShelby,
    Masinis
}

[System.Serializable]
public class CutsceneLine
{
    public CutsceneSpeaker speaker;
    [TextArea(2, 5)] public string text;
    [Tooltip("Indeks latar (daftar Stages) yang dipakai saat baris ini")]
    public int stage;
    [Tooltip("Tampilkan karakter di layar saat baris ini")]
    public bool showCharacters = true;
}

// Satu latar: gambar background + karakter yang hadir beserta posisi X-nya
[System.Serializable]
public class CutsceneStage
{
    public string name;
    public Sprite background;
    public List<StagedCharacter> characters = new List<StagedCharacter>();
}

[System.Serializable]
public class StagedCharacter
{
    public CutsceneSpeaker character;
    [Tooltip("Posisi X dari tengah layar; negatif = kiri, positif = kanan (name plate ikut sisi ini)")]
    public float posX;
}

[System.Serializable]
public class CharacterImage
{
    public CutsceneSpeaker character;
    public Image image;
}

// Dialog cutscene bergaya visual novel: efek ketik, Enter untuk menampilkan seluruh kalimat / lanjut.
// Cutscene penutup bisa diakhiri layar The End sebelum kembali ke menu
public class CutsceneDialog : MonoBehaviour
{
    [Tooltip("Cutscene yang diputar bila scene ini dibuka langsung (tanpa CutsceneSelection)")]
    [SerializeField] private CutsceneData defaultCutscene;

    [Header("Dialog Box")]
    [SerializeField] private TMP_Text dialogText;
    [SerializeField] private RectTransform namePlate;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Tanda >> saat kalimat sudah tampil semua")]
    [SerializeField] private TMP_Text nextIndicator;

    [Header("Latar & Karakter")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject charactersRoot;
    [SerializeField] private List<CharacterImage> characterImages = new List<CharacterImage>();
    [Tooltip("Warna karakter yang sedang tidak berbicara")]
    [SerializeField] private Color idleCharacterColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Tooltip("Layar hitam untuk transisi pindah latar")]
    [SerializeField] private CanvasGroup fader;
    [Tooltip("Durasi fade masuk di awal cutscene (hitam ke layar)")]
    [SerializeField] private float introFadeDuration = 1.2f;
    [Tooltip("Durasi tiap fade (gelap / terang) saat pindah latar")]
    [SerializeField] private float stageFadeDuration = 0.8f;
    [Tooltip("Lama layar tetap hitam di tengah pergantian latar")]
    [SerializeField] private float stageBlackHold = 0.4f;

    [Header("Pengaturan")]
    [SerializeField] private float charsPerSecond = 40f;
    [SerializeField] private float indicatorBlinkSpeed = 4f;

    [Header("Suara")]
    [Tooltip("AudioSource suara bicara (videogame_speak), diputar berulang selama teks diketik")]
    [SerializeField] private AudioSource typingAudio;
    [Tooltip("AudioSource musik latar cutscene (Days Gone By), diputar berulang sejak awal cutscene")]
    [SerializeField] private AudioSource bgmAudio;
    [Tooltip("Lama musik membesar di awal dan mengecil saat cutscene ditutup")]
    [SerializeField] private float bgmFadeDuration = 1.2f;

    [Header("Skip")]
    [Tooltip("Lama Enter ditahan sebelum petunjuk skip muncul")]
    [SerializeField] private float skipRevealDelay = 1f;
    [Tooltip("Lama Enter tetap ditahan setelah petunjuk muncul sampai cutscene dilewati (bar terisi)")]
    [SerializeField] private float skipHoldDuration = 1.2f;
    [Tooltip("Petunjuk 'Tahan Enter untuk skip'")]
    [SerializeField] private CanvasGroup skipHint;
    [Tooltip("Bar progres skip, di-scale X dari 0 ke 1")]
    [SerializeField] private RectTransform skipFill;

    [Header("Layar The End")]
    [Tooltip("Layar penutup cerita di atas Fader, tampil bila cutscene mengaktifkan Show The End")]
    [SerializeField] private CanvasGroup endScreen;
    [SerializeField] private TMP_Text endTitleText;
    [SerializeField] private TMP_Text endSubtitleText;
    [Tooltip("Durasi tulisan The End muncul / hilang")]
    [SerializeField] private float endFadeDuration = 1.5f;
    [Tooltip("Lama tulisan The End tampil penuh. Klik atau Enter setelah tulisan muncul akan langsung lanjut")]
    [SerializeField] private float endHoldDuration = 4f;

    private List<CutsceneStage> stages = new List<CutsceneStage>();
    private List<CutsceneLine> lines = new List<CutsceneLine>();
    private string nextSceneName = "";
    private bool showTheEnd;
    private string endTitle = "";
    private string endSubtitle = "";
    private int index = -1;
    private int currentStage = -1;
    private bool isTyping;
    private bool isTransitioning;
    private bool finished;
    private float skipHeld;
    private Coroutine typeRoutine;
    private float typingBaseVolume = 1f;
    private float bgmBaseVolume = 1f;

    void Awake()
    {
        // Cutscene pilihan menu/level diutamakan, lalu dikosongkan agar tidak terputar ulang
        CutsceneData data = CutsceneSelection.Current != null ? CutsceneSelection.Current : defaultCutscene;
        CutsceneSelection.Current = null;
        if (data == null) return;
        stages = data.stages;
        lines = data.lines;
        nextSceneName = data.nextSceneName;
        showTheEnd = data.showTheEnd;
        endTitle = data.endTitle;
        endSubtitle = data.endSubtitle;
    }

    void OnEnable()
    {
        if (typingAudio != null)
        {
            typingBaseVolume = typingAudio.volume;
            typingAudio.playOnAwake = false;
            typingAudio.loop = true;
        }
        if (bgmAudio != null)
        {
            bgmBaseVolume = bgmAudio.volume;
            bgmAudio.playOnAwake = false;
            bgmAudio.loop = true;
        }
    }

    void Start()
    {
        if (endScreen != null)
        {
            endScreen.alpha = 0f;
            endScreen.blocksRaycasts = false;
        }
        // Musik latar mengikuti volume musik di pengaturan, membesar bersama layar yang terang
        if (bgmAudio != null && bgmAudio.clip != null)
        {
            bgmAudio.volume = 0f;
            bgmAudio.Play();
            StartCoroutine(FadeBgm(bgmBaseVolume * VolumeSettings.MusicVolume, bgmFadeDuration));
        }
        StartCoroutine(IntroRoutine());
    }

    // Awal cutscene: layar mulai hitam lalu terang penuh, baru dialog pertama diketik
    IEnumerator IntroRoutine()
    {
        isTransitioning = true;
        SetIndicator(false);
        dialogText.text = "";
        if (namePlate != null) namePlate.gameObject.SetActive(false);
        if (lines.Count > 0)
        {
            ApplyStage(lines[0].stage);
            if (charactersRoot != null) charactersRoot.SetActive(lines[0].showCharacters);
        }

        if (fader != null)
        {
            fader.alpha = 1f;
            yield return Fade(0f, introFadeDuration);
        }
        isTransitioning = false;
        ShowLine(0);
    }

    void Update()
    {
        if (finished) return;

        // Enter atau klik kiri: penuhi kalimat yang sedang diketik, atau lanjut ke dialog berikutnya
        bool next = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
        if (!isTransitioning && next)
        {
            if (isTyping) CompleteLine();
            else ShowLine(index + 1);
        }
        UpdateSkip();

        if (nextIndicator != null && nextIndicator.gameObject.activeSelf)
        {
            Color c = nextIndicator.color;
            c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * indicatorBlinkSpeed);
            nextIndicator.color = c;
        }
    }

    void ShowLine(int i)
    {
        if (i >= lines.Count)
        {
            EndDialog();
            return;
        }

        index = i;
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(LineRoutine(lines[i]));
    }

    IEnumerator LineRoutine(CutsceneLine line)
    {
        // Pindah latar: layar menghitam dulu, ganti background & posisi karakter, lalu terang kembali
        bool stageChanged = line.stage != currentStage;
        if (stageChanged && currentStage >= 0 && fader != null && stageFadeDuration > 0f)
        {
            isTransitioning = true;
            SetIndicator(false);
            yield return Fade(1f, stageFadeDuration);
            dialogText.text = "";
            ApplyStage(line.stage);
            ApplySpeaker(line);
            yield return new WaitForSecondsRealtime(stageBlackHold);
            yield return Fade(0f, stageFadeDuration);
            isTransitioning = false;
        }
        else
        {
            if (stageChanged) ApplyStage(line.stage);
            ApplySpeaker(line);
        }

        yield return TypeRoutine(line.text);
    }

    IEnumerator Fade(float target, float duration)
    {
        float start = fader.alpha;
        fader.blocksRaycasts = true;
        for (float t = 0f; t < duration; t += FrameStep())
        {
            fader.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        fader.alpha = target;
        fader.blocksRaycasts = target > 0f;
    }

    void ApplyStage(int stageIndex)
    {
        currentStage = stageIndex;
        CutsceneStage stage = GetStage(stageIndex);
        if (stage == null) return;

        if (backgroundImage != null && stage.background != null) backgroundImage.sprite = stage.background;

        foreach (CharacterImage ci in characterImages)
        {
            if (ci.image == null) continue;
            StagedCharacter staged = FindStaged(stage, ci.character);
            ci.image.gameObject.SetActive(staged != null);
            if (staged != null)
            {
                Vector2 pos = ci.image.rectTransform.anchoredPosition;
                ci.image.rectTransform.anchoredPosition = new Vector2(staged.posX, pos.y);
            }
        }
    }

    // Name plate mengikuti sisi karakter yang berbicara, narator tanpa nama
    void ApplySpeaker(CutsceneLine line)
    {
        if (charactersRoot != null) charactersRoot.SetActive(line.showCharacters);

        bool isNarrator = line.speaker == CutsceneSpeaker.Narrator;
        if (namePlate != null)
        {
            namePlate.gameObject.SetActive(!isNarrator);
            if (!isNarrator)
            {
                bool right = GetSpeakerPosX(line.speaker) > 0f;
                float side = right ? 1f : 0f;
                namePlate.anchorMin = namePlate.anchorMax = new Vector2(side, 1f);
                namePlate.pivot = new Vector2(side, 0f);
                namePlate.anchoredPosition = Vector2.zero;
                if (nameText != null)
                {
                    nameText.text = GetDisplayName(line.speaker);
                    nameText.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                }
            }
        }

        // Karakter yang berbicara terang, sisanya diredupkan; saat narasi semua terang
        foreach (CharacterImage ci in characterImages)
        {
            if (ci.image == null) continue;
            bool lit = isNarrator || IsSpeaking(line.speaker, ci.character);
            ci.image.color = lit ? Color.white : idleCharacterColor;
        }
    }

    IEnumerator TypeRoutine(string text)
    {
        isTyping = true;
        SetIndicator(false);
        dialogText.text = text;
        dialogText.maxVisibleCharacters = 0;
        dialogText.ForceMeshUpdate();
        int total = dialogText.textInfo.characterCount;
        StartTypingSound();

        float shown = 0f;
        while (shown < total)
        {
            shown += charsPerSecond * FrameStep();
            dialogText.maxVisibleCharacters = Mathf.Min(total, (int)shown);
            yield return null;
        }
        CompleteLine();
    }

    // Tampilkan seluruh kalimat yang sedang diketik
    void CompleteLine()
    {
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = null;
        isTyping = false;
        StopTypingSound();
        dialogText.maxVisibleCharacters = int.MaxValue;
        SetIndicator(true);
    }

    // Suara bicara mengikuti volume SFX di pengaturan, mulai dari titik acak agar tiap kalimat terdengar berbeda
    void StartTypingSound()
    {
        if (typingAudio == null || typingAudio.clip == null) return;
        typingAudio.volume = typingBaseVolume * VolumeSettings.SfxVolume;
        typingAudio.time = Random.Range(0f, typingAudio.clip.length * 0.9f);
        typingAudio.Play();
    }

    void StopTypingSound()
    {
        if (typingAudio != null) typingAudio.Stop();
    }

    void SetIndicator(bool visible)
    {
        if (nextIndicator != null) nextIndicator.gameObject.SetActive(visible);
    }

    // Enter ditekan sekali = lanjut dialog. Ditahan 1 detik = petunjuk skip muncul,
    // lalu bar mulai terisi; tetap ditahan sampai bar penuh = lewati cutscene
    void UpdateSkip()
    {
        bool holding = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        skipHeld = holding ? skipHeld + Time.unscaledDeltaTime : 0f;
        bool revealed = skipHeld >= skipRevealDelay;
        float progress = revealed ? Mathf.Clamp01((skipHeld - skipRevealDelay) / skipHoldDuration) : 0f;

        if (skipFill != null) skipFill.localScale = new Vector3(progress, 1f, 1f);
        if (skipHint != null) skipHint.alpha = revealed ? 1f : 0f;

        if (progress >= 1f) EndDialog();
    }

    void EndDialog()
    {
        if (finished) return;
        finished = true;
        StopAllCoroutines();
        isTyping = false;
        StopTypingSound();
        SetIndicator(false);
        if (skipHint != null) skipHint.alpha = 0f;
        if (!string.IsNullOrEmpty(nextSceneName) || ShowsEndScreen) StartCoroutine(LeaveRoutine());
    }

    bool ShowsEndScreen => showTheEnd && endScreen != null;

    // Layar menghitam dulu sebelum pindah ke scene level. Bila cutscene penutup, layar The End tampil dulu
    IEnumerator LeaveRoutine()
    {
        bool ending = ShowsEndScreen;
        // Musik tetap mengiringi layar The End, baru mengecil saat tulisan menghilang
        if (!ending && bgmAudio != null && bgmAudio.isPlaying) StartCoroutine(FadeBgm(0f, stageFadeDuration));
        if (fader != null) yield return Fade(1f, stageFadeDuration);

        if (ending)
        {
            yield return EndScreenRoutine();
            if (bgmAudio != null && bgmAudio.isPlaying) yield return FadeBgm(0f, bgmFadeDuration);
        }
        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    // Tulisan The End muncul perlahan di layar hitam, bertahan sejenak (bisa dilewati dengan klik / Enter), lalu menghilang
    IEnumerator EndScreenRoutine()
    {
        if (endTitleText != null) endTitleText.text = endTitle;
        if (endSubtitleText != null)
        {
            endSubtitleText.text = endSubtitle;
            endSubtitleText.gameObject.SetActive(!string.IsNullOrEmpty(endSubtitle));
        }

        endScreen.blocksRaycasts = true;
        yield return FadeGroup(endScreen, 1f, endFadeDuration);

        for (float t = 0f; t < endHoldDuration; t += Time.unscaledDeltaTime)
        {
            bool next = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0);
            if (next) break;
            yield return null;
        }
        yield return FadeGroup(endScreen, 0f, endFadeDuration);
    }

    static IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < duration; t += FrameStep())
        {
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }

    IEnumerator FadeBgm(float target, float duration)
    {
        float start = bgmAudio.volume;
        for (float t = 0f; t < duration; t += FrameStep())
        {
            bgmAudio.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        bgmAudio.volume = target;
    }

    // Langkah waktu per frame dibatasi agar frame pertama yang lambat (saat scene dimuat) tidak melompati fade
    static float FrameStep()
    {
        return Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
    }

    CutsceneStage GetStage(int stageIndex)
    {
        return stageIndex >= 0 && stageIndex < stages.Count ? stages[stageIndex] : null;
    }

    static StagedCharacter FindStaged(CutsceneStage stage, CutsceneSpeaker character)
    {
        foreach (StagedCharacter s in stage.characters)
            if (s.character == character) return s;
        return null;
    }

    float GetSpeakerPosX(CutsceneSpeaker speaker)
    {
        CutsceneStage stage = GetStage(currentStage);
        if (stage == null) return 0f;
        // Thomas & Shelby berbicara bersama: name plate ikut posisi Thomas
        CutsceneSpeaker lookup = speaker == CutsceneSpeaker.ThomasAndShelby ? CutsceneSpeaker.Thomas : speaker;
        StagedCharacter staged = FindStaged(stage, lookup);
        return staged != null ? staged.posX : 0f;
    }

    static bool IsSpeaking(CutsceneSpeaker speaker, CutsceneSpeaker character)
    {
        if (speaker == CutsceneSpeaker.ThomasAndShelby)
            return character == CutsceneSpeaker.Thomas || character == CutsceneSpeaker.Shelby;
        return speaker == character;
    }

    static string GetDisplayName(CutsceneSpeaker speaker)
    {
        switch (speaker)
        {
            case CutsceneSpeaker.MrMister: return "Mr. Mister";
            case CutsceneSpeaker.ThomasAndShelby: return "Thomas & Shelby";
            default: return speaker.ToString();
        }
    }
}
