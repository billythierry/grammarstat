using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu pause untuk scene gameplay (dipakai lewat prefab PauseMenu)
// Esc saat bermain: pause. Esc saat pause: lanjut, atau kembali ke menu pause dari pengaturan/dialog
[RequireComponent(typeof(Canvas))]
public class PauseMenu : MonoBehaviour
{
    // Dibaca script gameplay untuk mengabaikan input mouse selama pause
    public static bool IsPaused { get; private set; }

    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Halaman")]
    [SerializeField] private GameObject pausePage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject quitDialog;

    [Header("Background")]
    [SerializeField] private Image overlay;
    [Range(0f, 1f)] [SerializeField] private float pauseOverlayAlpha = 0.7f;
    [Tooltip("Lebih gelap supaya panel pengaturan dan dialog terbaca jelas")]
    [Range(0f, 1f)] [SerializeField] private float panelOverlayAlpha = 0.85f;

    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private float timeScaleBeforePause = 1f;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();

        // Tombol UI butuh EventSystem; buat jika scene belum punya
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        IsPaused = false;
        SetVisible(false);
    }

    void OnDestroy()
    {
        // Jangan biarkan game tetap berhenti saat scene berganti
        if (IsPaused) RestoreTime();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (!IsPaused) Pause();
        else if (!pausePage.activeSelf) ShowPausePage();
        else Resume();
    }

    public void Pause()
    {
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        IsPaused = true;
        ShowPausePage();
        SetVisible(true);
    }

    public void Resume()
    {
        RestoreTime();
        SetVisible(false);
    }

    public void ShowPausePage() => ShowPage(pausePage, pauseOverlayAlpha);

    public void OpenSettings() => ShowPage(settingsPage, panelOverlayAlpha);

    public void AskQuit() => ShowPage(quitDialog, panelOverlayAlpha);

    // Progres level yang sedang dimainkan tidak disimpan
    public void QuitToMainMenu()
    {
        RestoreTime();
        SceneManager.LoadScene(mainMenuScene);
    }

    private void RestoreTime()
    {
        Time.timeScale = timeScaleBeforePause;
        AudioListener.pause = false;
        IsPaused = false;
    }

    private void ShowPage(GameObject page, float overlayAlpha)
    {
        pausePage.SetActive(page == pausePage);
        settingsPage.SetActive(page == settingsPage);
        quitDialog.SetActive(page == quitDialog);

        var c = overlay.color;
        c.a = overlayAlpha;
        overlay.color = c;
    }

    // Mematikan Canvas lebih ringan daripada SetActive: tidak ada rebuild hierarki saat dibuka/ditutup
    private void SetVisible(bool visible)
    {
        canvas.enabled = visible;
        if (raycaster != null) raycaster.enabled = visible;
    }
}
