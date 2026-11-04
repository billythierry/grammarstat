using System.Collections.Generic;
using UnityEngine;

public class WagonScript : MonoBehaviour
{
    [Tooltip("Drop area di gerbong ini, urut dari kiri ke kanan (sebanyak SlotLayout.MaxSlots)")]
    [SerializeField] private DropAreaScript[] dropAreas;

    // Drop area yang aktif untuk soal gerbong ini, urut dari kiri ke kanan
    public DropAreaScript[] DropAreas => activeDropAreas ?? dropAreas;

    private DropAreaScript[] activeDropAreas;

    // Box jawaban yang sudah "dimuat" ke gerbong ini
    private readonly List<BoxScript> loadedBoxes = new List<BoxScript>();

    // Tampilkan drop area sebanyak jumlah box soal, tersusun simetris di tengah gerbong
    public void Configure(int count)
    {
        count = Mathf.Clamp(count, 1, dropAreas.Length);
        float width = SlotLayout.Width(count);
        activeDropAreas = new DropAreaScript[count];

        for (int i = 0; i < dropAreas.Length; i++)
        {
            DropAreaScript area = dropAreas[i];
            bool used = i < count;
            area.gameObject.SetActive(used);
            if (!used) continue;

            Transform t = area.transform;
            t.localPosition = new Vector3(SlotLayout.X(i, count), t.localPosition.y, t.localPosition.z);
            t.localScale = new Vector3(width, t.localScale.y, t.localScale.z);
            activeDropAreas[i] = area;
        }
    }

    // Kunci box di drop area agar ikut bergerak bersama gerbong
    public void LoadBoxes()
    {
        foreach (DropAreaScript area in DropAreas)
        {
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
