using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Layar performa (F4): FPS, frame time, CPU, GPU, draw call, dan memori di pojok kanan atas.
// Gayanya sama dengan DebugOverlay (F3). Dibuat otomatis saat game dimulai dan ikut pindah scene,
// jadi tidak perlu dipasang di scene mana pun
public class PerformanceOverlay : MonoBehaviour
{
    const KeyCode ToggleKey = KeyCode.F4;
    // Angka diperbarui tiap interval ini (rata-rata), supaya terbaca dan tidak membebani tiap frame
    const float RefreshInterval = 0.5f;
    // Sama seperti F3: hanya aktif di Editor dan Development Build
    const bool AllowInRelease = false;

    static readonly Color LineBackground = new Color(0f, 0f, 0f, 0.75f);
    static readonly Regex ColorTags = new Regex("</?color[^>]*>");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!AllowInRelease && !Debug.isDebugBuild) return;
        var go = new GameObject("PerformanceOverlay");
        DontDestroyOnLoad(go);
        go.AddComponent<PerformanceOverlay>();
    }

    private Canvas canvas;
    private TMP_Text text;
    private TMP_Text background;
    private string markTag;
    private bool visible;
    private readonly StringBuilder sb = new StringBuilder(1024);

    // Penghitung untuk satu interval
    private int frames;
    private float elapsed;
    private float maxFrameTime;
    private long gcAllocTotal;
    private long gcAllocMax;

    // CPU proses: selisih waktu prosesor dibanding waktu nyata
    private System.Diagnostics.Process process;
    private double lastCpuSeconds;
    private double lastWallSeconds;
    private int gcCountAtStart;

    private ProfilerRecorder mainThread;
    private ProfilerRecorder batches;
    private ProfilerRecorder setPass;
    private ProfilerRecorder drawCalls;
    private ProfilerRecorder triangles;
    private ProfilerRecorder gcAllocInFrame;
    private readonly FrameTiming[] frameTimings = new FrameTiming[1];

    void Awake()
    {
        markTag = "<mark=#" + ColorUtility.ToHtmlStringRGBA(LineBackground) + " padding=\"10,10,2,2\">";
        BuildCanvas();
        SetVisible(false);

        try
        {
            process = System.Diagnostics.Process.GetCurrentProcess();
            lastCpuSeconds = process.TotalProcessorTime.TotalSeconds;
        }
        catch { process = null; }
        lastWallSeconds = Time.realtimeSinceStartupAsDouble;
        gcCountAtStart = System.GC.CollectionCount(0);
    }

    void OnEnable()
    {
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        gcAllocInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    void OnDisable()
    {
        mainThread.Dispose();
        batches.Dispose();
        setPass.Dispose();
        drawCalls.Dispose();
        triangles.Dispose();
        gcAllocInFrame.Dispose();
    }

    void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) SetVisible(!visible);

        float dt = Time.unscaledDeltaTime;
        frames++;
        elapsed += dt;
        if (dt > maxFrameTime) maxFrameTime = dt;
        if (gcAllocInFrame.Valid)
        {
            long alloc = gcAllocInFrame.LastValue;
            gcAllocTotal += alloc;
            if (alloc > gcAllocMax) gcAllocMax = alloc;
        }
        if (visible) FrameTimingManager.CaptureFrameTimings();

        if (elapsed < RefreshInterval) return;
        if (visible) Refresh();
        else SampleCpu(); // tetap diambil supaya angka CPU benar saat layar dibuka

        frames = 0;
        elapsed = 0f;
        maxFrameTime = 0f;
        gcAllocTotal = 0;
        gcAllocMax = 0;
    }

    void Refresh()
    {
        float avgFrameMs = elapsed / frames * 1000f;
        float fps = frames / elapsed;
        float minFps = maxFrameTime > 0f ? 1f / maxFrameTime : fps;
        int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
        float cpuPercent = SampleCpu();

        sb.Clear();
        sb.Append("<b>Performa</b> (F4)\n");
        sb.Append("FPS: <color=").Append(FpsColor(fps, target)).Append('>').Append(fps.ToString("F0")).Append("</color>");
        sb.Append("  (terendah ").Append(minFps.ToString("F0")).Append(")\n");
        sb.Append("Frame time: ").Append(avgFrameMs.ToString("F1")).Append(" ms  (maks ").Append((maxFrameTime * 1000f).ToString("F1")).Append(" ms)\n");
        sb.Append("Target: ").Append(Application.targetFrameRate > 0 ? Application.targetFrameRate + " FPS" : "tanpa batas");
        sb.Append("  |  VSync: ").Append(QualitySettings.vSyncCount > 0 ? "aktif" : "mati").Append('\n');
        sb.Append('\n');

        sb.Append("CPU proses: ").Append(cpuPercent >= 0f ? cpuPercent.ToString("F0") + "%" : "-");
        sb.Append(" dari ").Append(SystemInfo.processorCount).Append(" core\n");
        sb.Append("CPU main thread: ").Append(mainThread.Valid ? (AverageNs(mainThread) * 1e-6).ToString("F1") + " ms" : "-").Append('\n');
        sb.Append("GPU: ").Append(GpuFrameMs()).Append('\n');
        sb.Append('\n');

        sb.Append("Batches: ").Append(Count(batches)).Append("  |  SetPass: ").Append(Count(setPass)).Append('\n');
        sb.Append("Draw calls: ").Append(Count(drawCalls)).Append("  |  Segitiga: ").Append(Count(triangles)).Append('\n');
        sb.Append('\n');

        sb.Append("Memori: ").Append(Mb(Profiler.GetTotalAllocatedMemoryLong())).Append(" dipakai / ");
        sb.Append(Mb(Profiler.GetTotalReservedMemoryLong())).Append(" dipesan\n");
        sb.Append("Mono heap: ").Append(Mb(Profiler.GetMonoUsedSizeLong())).Append("  |  Grafis: ").Append(Mb(Profiler.GetAllocatedMemoryForGraphicsDriver())).Append('\n');
        if (gcAllocInFrame.Valid)
        {
            sb.Append("GC alloc/frame: ").Append(Bytes(gcAllocTotal / frames)).Append("  (maks ").Append(Bytes(gcAllocMax)).Append(")\n");
        }
        sb.Append("GC dijalankan: ").Append(System.GC.CollectionCount(0) - gcCountAtStart).Append("x\n");
        sb.Append('\n');

        sb.Append("Scene: ").Append(SceneManager.GetActiveScene().name).Append("  |  ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
        sb.Append(SystemInfo.processorType).Append('\n');
        sb.Append(SystemInfo.graphicsDeviceName);

        string content = sb.ToString();
        text.text = content;
        background.text = WrapLines(ColorTags.Replace(content, ""));
    }

    // Persentase CPU yang dipakai proses game sejak sampel terakhir, dibagi semua core. -1 bila tidak didukung
    float SampleCpu()
    {
        double wall = Time.realtimeSinceStartupAsDouble;
        if (process == null) { lastWallSeconds = wall; return -1f; }
        try
        {
            process.Refresh();
            double cpu = process.TotalProcessorTime.TotalSeconds;
            double wallDelta = wall - lastWallSeconds;
            float percent = wallDelta > 0
                ? (float)((cpu - lastCpuSeconds) / (wallDelta * SystemInfo.processorCount) * 100.0)
                : 0f;
            lastCpuSeconds = cpu;
            lastWallSeconds = wall;
            return Mathf.Clamp(percent, 0f, 100f);
        }
        catch
        {
            process = null;
            return -1f;
        }
    }

    string GpuFrameMs()
    {
        // Butuh "Frame Timing Stats" di Player Settings; di Editor biasanya tidak tersedia
        if (FrameTimingManager.GetLatestTimings(1, frameTimings) == 0 || frameTimings[0].gpuFrameTime <= 0) return "-";
        return frameTimings[0].gpuFrameTime.ToString("F1") + " ms";
    }

    static string FpsColor(float fps, int target)
    {
        if (fps >= target * 0.9f) return "#7CFC7C";
        if (fps >= target * 0.5f) return "#FFD966";
        return "#FF6B6B";
    }

    static double AverageNs(ProfilerRecorder recorder)
    {
        int count = recorder.Capacity;
        double sum = 0;
        int n = 0;
        for (int i = 0; i < count && i < recorder.Count; i++)
        {
            sum += recorder.GetSample(i).Value;
            n++;
        }
        return n > 0 ? sum / n : 0;
    }

    static string Count(ProfilerRecorder recorder)
    {
        if (!recorder.Valid) return "-";
        long v = recorder.LastValue;
        return v >= 10000 ? (v / 1000f).ToString("F1") + "k" : v.ToString();
    }

    static string Mb(long bytes) => (bytes / (1024f * 1024f)).ToString("F0") + " MB";

    static string Bytes(long bytes) => bytes >= 1024 ? (bytes / 1024f).ToString("F1") + " KB" : bytes + " B";

    // Tiap baris diberi latar sendiri, baris kosong dibiarkan sebagai jarak
    string WrapLines(string content)
    {
        string[] lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Length > 0) lines[i] = markTag + lines[i] + "</mark>";
        return string.Join("\n", lines);
    }

    void SetVisible(bool value)
    {
        visible = value;
        canvas.enabled = value;
        if (value)
        {
            // Mulai interval baru supaya angka pertama tidak tercampur waktu saat layar tertutup
            frames = 0;
            elapsed = 0f;
            maxFrameTime = 0f;
            gcAllocTotal = 0;
            gcAllocMax = 0;
            text.text = "<b>Performa</b> (F4)\nMengukur...";
            background.text = WrapLines("Performa (F4)\nMengukur...");
        }
    }

    void BuildCanvas()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // di atas menu pause
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // Tanpa GraphicRaycaster: layar ini tidak menghalangi klik ke game

        background = CreateText("Background", Color.black);
        text = CreateText("Text", Color.white);
    }

    TMP_Text CreateText(string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(24f, 24f);
        rt.offsetMax = new Vector2(-24f, -24f);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = 28;
        t.lineSpacing = 8;
        t.color = color;
        t.alignment = TextAlignmentOptions.TopRight;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }
}
