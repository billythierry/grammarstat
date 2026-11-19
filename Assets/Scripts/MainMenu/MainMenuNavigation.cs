using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// Chapter dan level yang dipilih pemain di menu, bisa dibaca oleh scene gameplay
public static class LevelSelection
{
    public static int Chapter { get; set; } = 1;
    public static int Level { get; set; } = 1;
    // Diisi panel hasil level: menu langsung membuka halaman Levels chapter terakhir
    public static bool ReturnToLevels { get; set; }
}

// Perpindahan halaman di main menu: Main -> Chapters -> Levels -> gameplay, Main -> Pengaturan, dan Main -> Credits
public class MainMenuNavigation : MonoBehaviour
{
    [System.Serializable]
    public class ChapterInfo
    {
        public string title;
        [Tooltip("Scene gameplay yang dibuka oleh semua level di chapter ini")]
        public string sceneName;
        [Tooltip("Jumlah level yang tampil di halaman Levels")]
        [Range(1, 6)] public int levelCount = 6;
        [Tooltip("Cutscene yang diputar sebelum Level 1 chapter ini (kosongkan jika tidak ada)")]
        public CutsceneData introCutscene;
    }

    [Header("Halaman")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject chaptersPage;
    [SerializeField] private GameObject levelsPage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject creditsPage;

    [SerializeField] private ChapterInfo[] chapters =
    {
        new ChapterInfo { title = "Train Loading", sceneName = "TrainLoading", levelCount = 4 },
        new ChapterInfo { title = "Ticket Inspection", sceneName = "TIcketInspection", levelCount = 2 },
        new ChapterInfo { title = "Runaway", sceneName = "Runaway", levelCount = 1 },
    };

    [Tooltip("Scene yang memutar cutscene (CutsceneData dipilih lewat CutsceneSelection)")]
    [FormerlySerializedAs("introCutsceneScene")]
    [SerializeField] private string cutsceneScene = "Cutscene";

    [Header("Levels")]
    [Tooltip("Card level 1 sampai 6, berurutan")]
    [SerializeField] private RectTransform[] levelCards;
    [SerializeField] private float levelSpacingX = 401f;
    [SerializeField] private float levelSpacingY = 408f;
    [Tooltip("Posisi Y tengah grid level")]
    [SerializeField] private float levelGridCenterY = -22f;

    [Header("Bintang di card level")]
    [Tooltip("Nama objek di dalam card yang berisi 3 gambar bintang, urut dari kiri")]
    [SerializeField] private string starsObjectName = "Stars";
    [SerializeField] private Color starOnColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
    [SerializeField] private Color starOffColor = new Color32(0x4A, 0x26, 0x12, 0xFF);

    [Header("Kunci chapter dan level")]
    [Tooltip("Card chapter 1 sampai 3, berurutan")]
    [SerializeField] private RectTransform[] chapterCards;
    [Tooltip("Nama objek di dalam card yang tampil saat terkunci (lapisan gelap + gembok)")]
    [SerializeField] private string lockObjectName = "Lock";
    [Tooltip("Hanya di editor / development build: tombol untuk membuka semua chapter dan level selama sesi ini (untuk pengujian)")]
    [SerializeField] private KeyCode unlockAllKey = KeyCode.Alpha7;

    // Pengujian: semua terbuka tanpa mengubah progres yang tersimpan
    private static bool unlockAll;

    [Header("Reset progres (hanya di main menu)")]
    [Tooltip("Tombol keyboard untuk membuka dialog hapus progres")]
    [SerializeField] private KeyCode resetProgressKey = KeyCode.F6;
    [Tooltip("Dialog konfirmasi sebelum semua bintang dihapus")]
    [SerializeField] private GameObject resetDialog;

    private int selectedChapter = 1;

    void Start()
    {
        ShowPage(mainPage);
        if (LevelSelection.ReturnToLevels)
        {
            LevelSelection.ReturnToLevels = false;
            OpenChapter(LevelSelection.Chapter);
        }
    }

    void Update()
    {
        // Escape sama dengan tombol kembali
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
        // Pintasan tersembunyi untuk menghapus progres, minta konfirmasi dulu
        if (Input.GetKeyDown(resetProgressKey)) OpenResetDialog();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Angka 7 di numpad juga berlaku bila tombolnya angka 7
        bool keypad7 = unlockAllKey == KeyCode.Alpha7 && Input.GetKeyDown(KeyCode.Keypad7);
        if (Input.GetKeyDown(unlockAllKey) || keypad7)
        {
            unlockAll = !unlockAll;
            Debug.Log("Buka semua level: " + (unlockAll ? "aktif" : "nonaktif"));
            RefreshCards();
        }
#endif
    }

    // Chapter 1 selalu terbuka. Chapter berikutnya terbuka setelah level terakhir chapter sebelumnya lulus (minimal 1 bintang)
    public bool IsChapterUnlocked(int chapter)
    {
        if (chapter <= 1 || unlockAll) return true;
        ChapterInfo previous = chapters[chapter - 2];
        return IsChapterUnlocked(chapter - 1) && ProgressData.GetStars(chapter - 1, previous.levelCount) > 0;
    }

    // Level 1 terbuka bersama chapternya. Level berikutnya terbuka setelah level sebelumnya lulus
    public bool IsLevelUnlocked(int chapter, int level)
    {
        if (!IsChapterUnlocked(chapter)) return false;
        if (level <= 1 || unlockAll) return true;
        return ProgressData.GetStars(chapter, level - 1) > 0;
    }

    public void OpenChapters()
    {
        RefreshChapterCards();
        ShowPage(chaptersPage);
    }

    public void OpenSettings() => ShowPage(settingsPage);

    public void OpenResetDialog()
    {
        if (resetDialog != null) resetDialog.SetActive(true);
    }

    public void CloseResetDialog()
    {
        if (resetDialog != null) resetDialog.SetActive(false);
    }

    // Semua bintang dihapus, card yang sedang tampil langsung diperbarui (bintang dan kunci)
    public void ConfirmResetProgress()
    {
        ProgressData.ResetAll();
        CloseResetDialog();
        RefreshCards();
    }

    void RefreshCards()
    {
        RefreshChapterCards();
        if (levelsPage.activeSelf) LayoutLevelCards(chapters[selectedChapter - 1].levelCount);
    }

    void RefreshChapterCards()
    {
        if (chapterCards == null) return;
        for (int i = 0; i < chapterCards.Length; i++)
            if (chapterCards[i] != null) SetLocked(chapterCards[i], !IsChapterUnlocked(i + 1));
    }

    // Card terkunci: tombol tidak bisa diklik, gembok tampil, bintang disembunyikan
    void SetLocked(RectTransform card, bool locked)
    {
        var button = card.GetComponent<UnityEngine.UI.Button>();
        if (button != null) button.interactable = !locked;
        Transform lockObject = card.Find(lockObjectName);
        if (lockObject != null) lockObject.gameObject.SetActive(locked);
        Transform stars = card.Find(starsObjectName);
        if (stars != null) stars.gameObject.SetActive(!locked);
    }

    public void OpenCredits() => ShowPage(creditsPage);

    // Tombol Keluar: simpan pengaturan lalu tutup game. Di editor, Play mode dihentikan
    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // chapter dimulai dari 1
    public void OpenChapter(int chapter)
    {
        if (!IsChapterUnlocked(chapter)) return;
        selectedChapter = chapter;
        LayoutLevelCards(chapters[chapter - 1].levelCount);
        ShowPage(levelsPage);
    }

    // level dimulai dari 1
    public void PlayLevel(int level)
    {
        if (!IsLevelUnlocked(selectedChapter, level)) return;
        LevelSelection.Chapter = selectedChapter;
        LevelSelection.Level = level;
        // Level 1 tiap chapter bisa diawali cutscene; cutscene lalu membuka scene level
        ChapterInfo chapter = chapters[selectedChapter - 1];
        bool playCutscene = level == 1 && chapter.introCutscene != null && !string.IsNullOrEmpty(cutsceneScene);
        if (playCutscene) CutsceneSelection.Current = chapter.introCutscene;
        SceneManager.LoadScene(playCutscene ? cutsceneScene : chapter.sceneName);
    }

    public void Back()
    {
        if (resetDialog != null && resetDialog.activeSelf) CloseResetDialog();
        else if (levelsPage.activeSelf) OpenChapters();
        else if (chaptersPage.activeSelf || settingsPage.activeSelf || creditsPage.activeSelf) ShowPage(mainPage);
    }

    // Tampilkan sejumlah card level, 3 per baris (4 level jadi 2 x 2), tiap baris di tengah layar
    private void LayoutLevelCards(int count)
    {
        int perRow = count == 4 ? 2 : 3;
        int rows = Mathf.CeilToInt(count / (float)perRow);
        for (int i = 0; i < levelCards.Length; i++)
        {
            bool visible = i < count;
            levelCards[i].gameObject.SetActive(visible);
            if (!visible) continue;

            int row = i / perRow;
            int col = i % perRow;
            int colsInRow = Mathf.Min(perRow, count - row * perRow);
            float x = (col - (colsInRow - 1) / 2f) * levelSpacingX;
            float y = levelGridCenterY + ((rows - 1) / 2f - row) * levelSpacingY;
            levelCards[i].anchoredPosition = new Vector2(x, y);
            ShowStars(levelCards[i], ProgressData.GetStars(selectedChapter, i + 1));
            SetLocked(levelCards[i], !IsLevelUnlocked(selectedChapter, i + 1));
        }
    }

    // Bintang terbaik yang pernah didapat menyala, sisanya gelap
    private void ShowStars(RectTransform card, int stars)
    {
        Transform group = card.Find(starsObjectName);
        if (group == null) return;
        for (int i = 0; i < group.childCount; i++)
        {
            var image = group.GetChild(i).GetComponent<UnityEngine.UI.Image>();
            if (image != null) image.color = i < stars ? starOnColor : starOffColor;
        }
    }

    private void ShowPage(GameObject page)
    {
        CloseResetDialog();
        mainPage.SetActive(page == mainPage);
        chaptersPage.SetActive(page == chaptersPage);
        levelsPage.SetActive(page == levelsPage);
        settingsPage.SetActive(page == settingsPage);
        creditsPage.SetActive(page == creditsPage);
    }
}
