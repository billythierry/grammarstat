using System.Collections.Generic;
using UnityEngine;

public class WagonScript : MonoBehaviour
{
    [Tooltip("Drop area di gerbong ini, urut dari kiri ke kanan")]
    [SerializeField] private DropAreaScript[] dropAreas;

    public DropAreaScript[] DropAreas => dropAreas;

    // Box jawaban yang sudah "dimuat" ke gerbong ini
    private readonly List<BoxScript> loadedBoxes = new List<BoxScript>();

    // Kunci box di drop area agar ikut bergerak bersama gerbong
    public void LoadBoxes()
    {
        foreach (DropAreaScript area in dropAreas)
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
