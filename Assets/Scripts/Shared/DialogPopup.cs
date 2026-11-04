using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class DialogLine
{
    [Tooltip("Nama pembicara. Kosongkan untuk narasi (tanpa name plate)")]
    public string speaker;
    [TextArea(2, 4)]
    public string text;
}

// Dialog box singkat di dalam level (tanpa pindah ke scene Cutscene): karakter + name plate + efek ketik.
// Enter / Spasi / klik untuk menampilkan seluruh kalimat atau lanjut
public class DialogPopup : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text dialogText;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Name plate yang disembunyikan saat baris narasi. Kosong = hanya teks nama yang disembunyikan")]
    [SerializeField] private GameObject namePlate;
    [Tooltip("Tanda >> saat kalimat sudah tampil semua")]
    [SerializeField] private TMP_Text nextIndicator;
    [Tooltip("Gambar karakter, masuk dengan efek bergeser (opsional)")]
    [SerializeField] private RectTransform character;

    [Header("Pengaturan")]
    [SerializeField] private float charsPerSecond = 40f;
    [SerializeField] private float fadeDuration = 0.35f;
    [Tooltip("Jarak geser karakter saat masuk")]
    [SerializeField] private float characterSlide = 120f;
    [SerializeField] private float indicatorBlinkSpeed = 4f;

    public bool IsOpen { get; private set; }

    private Vector2 characterHome;
    private bool nextRequested;

    void Awake()
    {
        if (character != null) characterHome = character.anchoredPosition;
        SetVisible(false);
    }

    // Semua kalimat diucapkan satu pembicara
    public IEnumerator Play(string speaker, string[] texts)
    {
        var lines = new DialogLine[texts.Length];
        for (int i = 0; i < texts.Length; i++) lines[i] = new DialogLine { speaker = speaker, text = texts[i] };
        yield return Play(lines);
    }

    // Baris dengan pembicara kosong adalah narasi. Karakter baru masuk saat pertama kali ada yang berbicara, lalu tetap tampil
    public IEnumerator Play(DialogLine[] lines)
    {
        IsOpen = true;
        dialogText.text = "";
        SetIndicator(false);

        bool characterShown = lines.Length > 0 && !IsNarration(lines[0]);
        if (character != null) character.gameObject.SetActive(characterShown);
        if (lines.Length > 0) ShowSpeaker(lines[0]);

        SetVisible(true);
        yield return Fade(0f, 1f);

        foreach (DialogLine line in lines)
        {
            ShowSpeaker(line);
            if (!characterShown && !IsNarration(line))
            {
                characterShown = true;
                yield return SlideCharacterIn();
            }

            yield return TypeRoutine(line.text);
            SetIndicator(true);
            yield return null;
            while (!PressedNext()) yield return null;
            SetIndicator(false);
        }

        yield return Fade(1f, 0f);
        SetVisible(false);
        IsOpen = false;
    }

    static bool IsNarration(DialogLine line)
    {
        return string.IsNullOrEmpty(line.speaker);
    }

    void ShowSpeaker(DialogLine line)
    {
        bool narration = IsNarration(line);
        if (nameText != null) nameText.text = narration ? "" : line.speaker;
        if (namePlate != null) namePlate.SetActive(!narration);
        else if (nameText != null) nameText.gameObject.SetActive(!narration);
    }

    IEnumerator SlideCharacterIn()
    {
        if (character == null) yield break;
        character.gameObject.SetActive(true);
        for (float t = 0f; t < fadeDuration; t += FrameStep())
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fadeDuration);
            character.anchoredPosition = characterHome - new Vector2(characterSlide * (1f - k), 0f);
            yield return null;
        }
        character.anchoredPosition = characterHome;
    }

    IEnumerator TypeRoutine(string text)
    {
        dialogText.text = text;
        dialogText.maxVisibleCharacters = 0;
        dialogText.ForceMeshUpdate();
        int total = dialogText.textInfo.characterCount;

        float shown = 0f;
        yield return null;
        while (shown < total)
        {
            // Tekan saat mengetik = tampilkan seluruh kalimat
            if (PressedNext()) break;
            shown += charsPerSecond * FrameStep();
            dialogText.maxVisibleCharacters = Mathf.Min(total, (int)shown);
            yield return null;
        }
        dialogText.maxVisibleCharacters = int.MaxValue;
    }

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeDuration; t += FrameStep())
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fadeDuration);
            ApplyFade(Mathf.Lerp(from, to, k));
            yield return null;
        }
        ApplyFade(to);
    }

    void ApplyFade(float alpha)
    {
        canvasGroup.alpha = alpha;
        if (character != null) character.anchoredPosition = characterHome - new Vector2(characterSlide * (1f - alpha), 0f);
    }

    void Update()
    {
        if (nextIndicator != null && nextIndicator.gameObject.activeSelf)
        {
            Color c = nextIndicator.color;
            c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * indicatorBlinkSpeed);
            nextIndicator.color = c;
        }
    }

    // Bisa dipanggil dari tombol UI, sama dengan menekan Enter
    public void Next()
    {
        nextRequested = true;
    }

    bool PressedNext()
    {
        bool requested = nextRequested;
        nextRequested = false;
        return requested || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
    }

    void SetIndicator(bool visible)
    {
        if (nextIndicator != null) nextIndicator.gameObject.SetActive(visible);
    }

    void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? canvasGroup.alpha : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    // Langkah waktu per frame dibatasi agar frame lambat tidak melompati animasi
    static float FrameStep()
    {
        return Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
    }
}
