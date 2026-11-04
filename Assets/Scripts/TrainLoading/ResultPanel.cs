using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Panel hasil di akhir level: judul lulus/gagal, nilai tiap bagian, bintang, tombol Menu dan Lanjut / Ulangi
public class ResultPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Kartu hasil, diberi animasi membesar saat muncul")]
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("3 gambar bintang, urut dari kiri")]
    [SerializeField] private Image[] stars;
    [SerializeField] private Color starOnColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
    [SerializeField] private Color starOffColor = new Color32(0x4A, 0x26, 0x12, 0x40);
    [SerializeField] private Button menuButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;

    [Header("Teks")]
    [SerializeField] private string passTitle = "BOOYAH !";
    [SerializeField] private string failTitle = "Aduh sayang sekali!";
    [Tooltip("Warna judul saat gagal. Saat lulus, judul memakai warna yang diatur di objek Title")]
    [SerializeField] private Color failTitleColor = new Color32(0xFF, 0x52, 0x52, 0xFF);
    [SerializeField] private string nextLabel = "Lanjut";
    [SerializeField] private string retryLabel = "Ulangi";

    [Header("Pindah scene")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [Tooltip("Chapter scene ini, untuk membuka halaman Levels yang benar saat kembali ke menu. 0 = pakai pilihan terakhir di menu")]
    [SerializeField] private int chapter;
    [Tooltip("Dipakai untuk layar hitam sebelum pindah scene (opsional)")]
    [SerializeField] private LevelIntro levelIntro;
    [Tooltip("Scene yang dibuka tombol Lanjut saat lulus di level terakhir, mis. chapter berikutnya. Kosong = tombol Lanjut disembunyikan di level terakhir")]
    [SerializeField] private string nextSceneOverride = "";
    [Tooltip("Chapter scene tujuan Next Scene Override, dimulai dari Level 1. 0 = tidak mengubah pilihan chapter/level")]
    [SerializeField] private int nextSceneChapter;
    [Tooltip("Tombol Lanjut tetap tampil saat lulus tetapi belum berfungsi (scene tujuan belum dibuat)")]
    [SerializeField] private bool nextNotReady;
    [Tooltip("Cutscene yang diputar tombol Lanjut saat lulus di level terakhir, mis. akhir chapter. Didahulukan dari Next Scene Override")]
    [SerializeField] private CutsceneData endCutscene;
    [SerializeField] private string cutsceneScene = "Cutscene";

    [Header("Animasi")]
    [SerializeField] private float fadeDuration = 0.35f;
    [Tooltip("Jeda antar bintang muncul")]
    [SerializeField] private float starDelay = 0.3f;

    private bool passed;
    private bool hasNextLevel;
    private bool leaving;
    private Color passTitleColor;

    void Awake()
    {
        SetVisible(false);
        if (titleText != null) passTitleColor = titleText.color;
        if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
        if (nextButton != null) nextButton.onClick.AddListener(NextOrRetry);
    }

    // lines: nilai tiap bagian yang sudah diformat, starCount 0..3
    public void Show(bool isPassed, string lines, int starCount, bool nextLevelExists)
    {
        passed = isPassed;
        hasNextLevel = nextLevelExists;
        if (titleText != null)
        {
            titleText.text = passed ? passTitle : failTitle;
            titleText.color = passed ? passTitleColor : failTitleColor;
        }
        if (scoreText != null) scoreText.text = lines;

        // Lulus di level terakhir: hanya tombol Menu, kecuali ada scene lanjutan (mis. chapter berikutnya)
        bool showNext = !passed || hasNextLevel || endCutscene != null || !string.IsNullOrEmpty(nextSceneOverride);
        if (nextButton != null) nextButton.gameObject.SetActive(showNext);
        if (nextButtonLabel != null) nextButtonLabel.text = passed ? nextLabel : retryLabel;
        if (menuButton != null && !showNext)
        {
            var rect = (RectTransform)menuButton.transform;
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
        }

        foreach (Image star in stars) star.color = starOffColor;
        StartCoroutine(ShowRoutine(starCount));
    }

    IEnumerator ShowRoutine(int starCount)
    {
        SetVisible(true);
        canvasGroup.interactable = false;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeDuration;
            canvasGroup.alpha = k;
            if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        canvasGroup.alpha = 1f;
        if (card != null) card.localScale = Vector3.one;
        canvasGroup.interactable = true;

        // Bintang yang didapat menyala satu per satu dengan efek membesar
        for (int i = 0; i < stars.Length && i < starCount; i++)
        {
            yield return new WaitForSecondsRealtime(starDelay);
            stars[i].color = starOnColor;
            yield return Pop(stars[i].rectTransform);
        }
    }

    static IEnumerator Pop(RectTransform target)
    {
        const float duration = 0.25f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            target.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t / duration * Mathf.PI));
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    public void GoToMenu()
    {
        // Kembali ke halaman Levels chapter ini, bukan halaman utama menu
        if (chapter > 0) LevelSelection.Chapter = chapter;
        LevelSelection.ReturnToLevels = true;
        Leave(mainMenuScene);
    }

    public void NextOrRetry()
    {
        if (passed && nextNotReady) return;
        if (passed && !hasNextLevel && endCutscene != null)
        {
            CutsceneSelection.Current = endCutscene;
            Leave(cutsceneScene);
            return;
        }
        if (passed && !hasNextLevel && !string.IsNullOrEmpty(nextSceneOverride))
        {
            if (nextSceneChapter > 0)
            {
                LevelSelection.Chapter = nextSceneChapter;
                LevelSelection.Level = 1;
            }
            Leave(nextSceneOverride);
            return;
        }
        if (passed && hasNextLevel) LevelSelection.Level++;
        Leave(SceneManager.GetActiveScene().name);
    }

    void Leave(string sceneName)
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(LeaveRoutine(sceneName));
    }

    IEnumerator LeaveRoutine(string sceneName)
    {
        canvasGroup.interactable = false;
        if (levelIntro != null) yield return levelIntro.FadeOut();
        SceneManager.LoadScene(sceneName);
    }
}
