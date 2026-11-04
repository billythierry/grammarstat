using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Pengaturan satu gerbong untuk satu soal / tutorial
public struct WagonPlan
{
    public int boxCount;
    // Bentuk kalimat yang menentukan sprite gerbong (hijau positif, merah negatif, biru tanya)
    public SentenceForm form;
    // Label peran di bawah tiap kolom (hanya gerbong tutorial), null = tanpa label
    public string[] roles;
}

public class WagonScript : MonoBehaviour
{
    [Tooltip("Drop area di gerbong ini, urut dari kiri ke kanan (sebanyak SlotLayout.MaxSlots)")]
    [SerializeField] private DropAreaScript[] dropAreas;

    [Header("Sprite gerbong per bentuk kalimat")]
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Sprite positiveSprite;
    [SerializeField] private Sprite negativeSprite;
    [SerializeField] private Sprite interrogativeSprite;

    [Header("Label peran (tutorial)")]
    [SerializeField] private TMP_FontAsset roleFont;
    [SerializeField] private float roleFontSize = 3.2f;
    [Tooltip("Jarak label dari bawah kolom (unit dunia)")]
    [SerializeField] private float roleOffsetY = 0.45f;

    // Drop area yang aktif untuk soal gerbong ini, urut dari kiri ke kanan
    public DropAreaScript[] DropAreas => activeDropAreas ?? dropAreas;

    private DropAreaScript[] activeDropAreas;
    private readonly List<TextMeshPro> roleLabels = new List<TextMeshPro>();

    // Box jawaban yang sudah "dimuat" ke gerbong ini
    private readonly List<BoxScript> loadedBoxes = new List<BoxScript>();

    // Tampilkan drop area sebanyak jumlah box soal, tersusun simetris di tengah gerbong
    public void Configure(WagonPlan plan)
    {
        int count = Mathf.Clamp(plan.boxCount, 1, dropAreas.Length);
        float width = SlotLayout.Width(count);
        activeDropAreas = new DropAreaScript[count];
        if (body != null) body.sprite = SpriteFor(plan.form);

        for (int i = 0; i < dropAreas.Length; i++)
        {
            DropAreaScript area = dropAreas[i];
            area.AcceptFilter = null;
            area.SetHighlight(false, Color.clear);
            bool used = i < count;
            area.gameObject.SetActive(used);
            if (!used) continue;

            Transform t = area.transform;
            t.localPosition = new Vector3(SlotLayout.X(i, count), t.localPosition.y, t.localPosition.z);
            t.localScale = new Vector3(width, t.localScale.y, t.localScale.z);
            activeDropAreas[i] = area;
        }

        SetRoleLabels(plan.roles, width);
    }

    Sprite SpriteFor(SentenceForm form)
    {
        switch (form)
        {
            case SentenceForm.Negative: return negativeSprite;
            case SentenceForm.Interrogative: return interrogativeSprite;
            default: return positiveSprite;
        }
    }

    // Label peran (Subject, Verb 1, ...) di bawah tiap kolom gerbong tutorial
    void SetRoleLabels(string[] roles, float width)
    {
        int count = roles == null ? 0 : Mathf.Min(roles.Length, activeDropAreas.Length);
        while (roleLabels.Count < count) roleLabels.Add(CreateRoleLabel());

        for (int i = 0; i < roleLabels.Count; i++)
        {
            TextMeshPro label = roleLabels[i];
            bool used = i < count;
            label.gameObject.SetActive(used);
            if (!used) continue;

            Transform area = activeDropAreas[i].transform;
            Vector3 p = area.localPosition;
            p.y -= area.localScale.y * 0.5f + roleOffsetY;
            label.transform.localPosition = p;
            label.rectTransform.sizeDelta = new Vector2(width, 0.8f);
            label.text = roles[i];
        }
    }

    TextMeshPro CreateRoleLabel()
    {
        var labelObject = new GameObject("Role Label");
        Transform parent = dropAreas.Length > 0 ? dropAreas[0].transform.parent : transform;
        labelObject.transform.SetParent(parent, false);

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        if (roleFont != null) label.font = roleFont;
        label.fontSize = roleFontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1.5f;
        label.fontSizeMax = roleFontSize;
        label.alignment = TextAlignmentOptions.Center;
        // Project memakai color space Linear: warna vertex teks mesh harus dalam nilai linear
        label.color = Color.white.linear;
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        label.sortingOrder = 3;
        return label;
    }

    // Kunci box di drop area agar ikut bergerak bersama gerbong
    public void LoadBoxes()
    {
        foreach (DropAreaScript area in DropAreas)
        {
            area.AcceptFilter = null;
            area.SetHighlight(false, Color.clear);
            BoxScript box = area.CurrentBox;
            if (box == null) continue;
            box.LockInto(transform);
            loadedBoxes.Add(box);
        }
    }

    // Kembalikan box ke pool saat gerbong dipakai ulang
    public void ReleaseBoxes()
    {
        foreach (BoxScript box in loadedBoxes)
        {
            if (box != null) box.Release();
        }
        loadedBoxes.Clear();
    }
}
