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
        // Level 1 tiap chapter bisa diawali cutscene; cutscene lalu membuka scene level
        ChapterInfo chapter = chapters[selectedChapter - 1];
        bool playCutscene = level == 1 && chapter.introCutscene != null && !string.IsNullOrEmpty(cutsceneScene);
        if (playCutscene) CutsceneSelection.Current = chapter.introCutscene;
        SceneManager.LoadScene(playCutscene ? cutsceneScene : chapter.sceneName);
    }

    public void Back()
    {
        if (levelsPage.activeSelf) ShowPage(chaptersPage);
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
