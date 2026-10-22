using UnityEngine;
using UnityEngine.SceneManagement;

// Chapter dan level yang dipilih pemain di menu, bisa dibaca oleh scene gameplay
public static class LevelSelection
{
    public static int Chapter { get; set; } = 1;
    public static int Level { get; set; } = 1;
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
    }

    [Header("Halaman")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject chaptersPage;
    [SerializeField] private GameObject levelsPage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject creditsPage;

    [SerializeField] private ChapterInfo[] chapters =
    {
        new ChapterInfo { title = "Train Loading", sceneName = "TrainLoading", levelCount = 6 },
        new ChapterInfo { title = "Ticket Inspection", sceneName = "TIcketInspection", levelCount = 2 },
        new ChapterInfo { title = "Runaway", sceneName = "Runaway", levelCount = 1 },
    };

    [Header("Levels")]
    [Tooltip("Card level 1 sampai 6, berurutan")]
    [SerializeField] private RectTransform[] levelCards;
    [SerializeField] private float levelSpacingX = 401f;
    [SerializeField] private float levelSpacingY = 408f;
    [Tooltip("Posisi Y tengah grid level")]
    [SerializeField] private float levelGridCenterY = -22f;

    private int selectedChapter = 1;

    void Start()
    {
        ShowPage(mainPage);
    }

    void Update()
    {
        // Escape sama dengan tombol kembali
        if (Input.GetKeyDown(KeyCode.Escape)) Back();
    }

    public void OpenChapters() => ShowPage(chaptersPage);

    public void OpenSettings() => ShowPage(settingsPage);

    public void OpenCredits() => ShowPage(creditsPage);

    // chapter dimulai dari 1
    public void OpenChapter(int chapter)
    {
        selectedChapter = chapter;
        LayoutLevelCards(chapters[chapter - 1].levelCount);
        ShowPage(levelsPage);
    }

    // level dimulai dari 1
    public void PlayLevel(int level)
    {
        LevelSelection.Chapter = selectedChapter;
        LevelSelection.Level = level;
        SceneManager.LoadScene(chapters[selectedChapter - 1].sceneName);
    }

    public void Back()
    {
        if (levelsPage.activeSelf) ShowPage(chaptersPage);
        else if (chaptersPage.activeSelf || settingsPage.activeSelf || creditsPage.activeSelf) ShowPage(mainPage);
    }

    // Tampilkan sejumlah card level, 3 per baris, tiap baris di tengah layar
    private void LayoutLevelCards(int count)
    {
        int rows = Mathf.CeilToInt(count / 3f);
        for (int i = 0; i < levelCards.Length; i++)
        {
            bool visible = i < count;
            levelCards[i].gameObject.SetActive(visible);
            if (!visible) continue;

            int row = i / 3;
            int col = i % 3;
            int colsInRow = Mathf.Min(3, count - row * 3);
            float x = (col - (colsInRow - 1) / 2f) * levelSpacingX;
            float y = levelGridCenterY + ((rows - 1) / 2f - row) * levelSpacingY;
            levelCards[i].anchoredPosition = new Vector2(x, y);
        }
    }

    private void ShowPage(GameObject page)
    {
        mainPage.SetActive(page == mainPage);
        chaptersPage.SetActive(page == chaptersPage);
        levelsPage.SetActive(page == levelsPage);
        settingsPage.SetActive(page == settingsPage);
        creditsPage.SetActive(page == creditsPage);
    }
}
