using UnityEngine;
using UnityEngine.SceneManagement;

// Menjaga tampilan game tetap 16:9 di layar dengan rasio lain (16:10, 4:3, ultrawide).
// Sisa layar diisi garis hitam (letterbox di atas-bawah atau pillarbox di kiri-kanan).
// Dibuat otomatis saat game dimulai, jadi tidak perlu dipasang di scene mana pun
public class AspectRatioLetterbox : MonoBehaviour
{
    const float TargetAspect = 16f / 9f;
    // Canvas UI dinaikkan urutannya supaya tetap di atas sprite gameplay setelah diikat ke kamera
    const int CanvasOrderOffset = 1000;
    // Jarak Canvas dari kamera; harus lebih kecil dari jarak kamera ke sprite (10) dan lebih besar dari near clip
    const float CanvasPlaneDistance = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        var go = new GameObject("AspectRatioLetterbox");
        DontDestroyOnLoad(go);
        go.AddComponent<AspectRatioLetterbox>();
    }

    private Camera target;
    private int lastWidth;
    private int lastHeight;

    void Awake()
    {
        // Kamera kosong di belakang kamera utama, tugasnya hanya mengisi area di luar 16:9 dengan hitam
        var bars = gameObject.AddComponent<Camera>();
        bars.depth = -100;
        bars.clearFlags = CameraClearFlags.SolidColor;
        bars.backgroundColor = Color.black;
        bars.cullingMask = 0;
        bars.orthographic = true;
        bars.useOcclusionCulling = false;
        bars.allowHDR = false;
        bars.allowMSAA = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        target = Camera.main;
        if (target == null) return;
        AttachCanvases(scene);
        ApplyViewport();
    }

    void LateUpdate()
    {
        // Ukuran jendela bisa berubah saat game berjalan (windowed, ganti resolusi)
        if (target == null || Screen.width == lastWidth && Screen.height == lastHeight) return;
        ApplyViewport();
    }

    void ApplyViewport()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        float screenAspect = (float)Screen.width / Screen.height;

        Rect rect;
        if (screenAspect > TargetAspect)
        {
            // Layar lebih lebar dari 16:9: garis hitam di kiri dan kanan
            float width = TargetAspect / screenAspect;
            rect = new Rect((1f - width) / 2f, 0f, width, 1f);
        }
        else
        {
            // Layar lebih tinggi dari 16:9 (16:10, 4:3): garis hitam di atas dan bawah
            float height = screenAspect / TargetAspect;
            rect = new Rect(0f, (1f - height) / 2f, 1f, height);
        }
        target.rect = rect;
    }

    // Canvas Overlay selalu memenuhi seluruh layar, jadi diubah ke mode Camera agar ikut area 16:9
    void AttachCanvases(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || !IsRoot(canvas)) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = target;
                canvas.planeDistance = CanvasPlaneDistance;
                canvas.sortingOrder += CanvasOrderOffset;
            }
        }
    }

    static bool IsRoot(Canvas canvas)
    {
        Transform parent = canvas.transform.parent;
        return parent == null || parent.GetComponentInParent<Canvas>(true) == null;
    }
}
