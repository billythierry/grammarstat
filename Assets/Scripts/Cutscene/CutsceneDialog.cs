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

// Dialog cutscene bergaya visual novel: efek ketik, Enter untuk menampilkan seluruh kalimat / lanjut
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

    [Header("Skip")]
    [Tooltip("Lama Enter ditahan sebelum petunjuk skip muncul")]
    [SerializeField] private float skipRevealDelay = 1f;
    [Tooltip("Lama Enter tetap ditahan setelah petunjuk muncul sampai cutscene dilewati (bar terisi)")]
    [SerializeField] private float skipHoldDuration = 1.2f;
    [Tooltip("Petunjuk 'Tahan Enter untuk skip'")]
    [SerializeField] private CanvasGroup skipHint;
    [Tooltip("Bar progres skip, di-scale X dari 0 ke 1")]
    [SerializeField] private RectTransform skipFill;

    private List<CutsceneStage> stages = new List<CutsceneStage>();
    private List<CutsceneLine> lines = new List<CutsceneLine>();
    private string nextSceneName = "";
    private int index = -1;
    private int currentStage = -1;
    private bool isTyping;
    private bool isTransitioning;
    private bool finished;
    private float skipHeld;
    private Coroutine typeRoutine;

    void Awake()
    {
        // Cutscene pilihan menu/level diutamakan, lalu dikosongkan agar tidak terputar ulang
        CutsceneData data = CutsceneSelection.Current != null ? CutsceneSelection.Current : defaultCutscene;
        CutsceneSelection.Current = null;
        if (data == null) return;
        stages = data.stages;
        lines = data.lines;
        nextSceneName = data.nextSceneName;
    }

    void Start()
    {
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

        if (!isTransitioning && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
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
        dialogText.maxVisibleCharacters = int.MaxValue;
        SetIndicator(true);
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
        SetIndicator(false);
        if (skipHint != null) skipHint.alpha = 0f;
        if (!string.IsNullOrEmpty(nextSceneName)) StartCoroutine(LeaveRoutine());
    }

    // Layar menghitam dulu sebelum pindah ke scene level
    IEnumerator LeaveRoutine()
    {
        if (fader != null) yield return Fade(1f, stageFadeDuration);
        SceneManager.LoadScene(nextSceneName);
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
