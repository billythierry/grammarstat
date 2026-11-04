using System;
using TMPro;
using UnityEngine;

// Layar debug seperti F3 di Minecraft: teks di pojok layar dengan latar gelap per baris.
// Isi teks diberikan script gameplay lewat TextProvider, jadi bisa dipakai di chapter lain
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [Tooltip("Teks hitam tepat di belakang Text, hanya untuk latar. Tag mark digambar di atas huruf, jadi latar dipisah agar teks tetap terang; warnanya tidak boleh transparan karena mark ikut hilang")]
    [SerializeField] private TMP_Text background;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [Tooltip("Izinkan di build rilis. Biarkan mati untuk build penelitian, karena layar ini menampilkan kunci jawaban")]
    [SerializeField] private bool allowInRelease;
    [SerializeField] private Color lineBackground = new Color(0f, 0f, 0f, 0.75f);

    // Diisi script gameplay; dipanggil tiap frame selama layar debug tampil
    public Func<string> TextProvider { get; set; }

    public bool IsVisible { get; private set; }

    private string markTag;

    void Awake()
    {
        markTag = "<mark=#" + ColorUtility.ToHtmlStringRGBA(lineBackground) + " padding=\"10,10,2,2\">";
        SetVisible(false);

        // Debug.isDebugBuild bernilai true di Editor dan Development Build
        if (!allowInRelease && !Debug.isDebugBuild) enabled = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetVisible(!IsVisible);
        if (!IsVisible || TextProvider == null) return;

        string content = TextProvider();
        text.text = content;
        background.text = WrapLines(content);
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;
        text.enabled = visible;
        background.enabled = visible;
    }

    // Tiap baris diberi latar sendiri, baris kosong dibiarkan sebagai jarak
    string WrapLines(string content)
    {
        string[] lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Length > 0) lines[i] = markTag + lines[i] + "</mark>";
        return string.Join("\n", lines);
    }
}
