using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DropAreaScript : MonoBehaviour, IBoxDropArea
{
    // Dipanggil saat box dilepas di drop area ini tapi ditolak (mis. salah kolom saat tutorial)
    public static event Action<DropAreaScript, BoxScript> Rejected;

    // Box yang sedang menempati drop area ini (null bila kosong)
    public BoxScript CurrentBox { get; private set; }

    // Syarat tambahan box yang boleh masuk (dipakai tutorial terpandu). null = semua box boleh
    public Func<BoxScript, bool> AcceptFilter { get; set; }

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private Color highlightColor;
    private bool highlighted;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    void Update()
    {
        if (!highlighted || spriteRenderer == null) return;
        // Berkedip lembut agar kolom tujuan mudah terlihat
        float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
        Color c = highlightColor;
        c.a = Mathf.Lerp(0.25f, 0.7f, pulse);
        spriteRenderer.color = c;
    }

    public void SetHighlight(bool on, Color color)
    {
        highlighted = on;
        highlightColor = color;
        if (!on && spriteRenderer != null) spriteRenderer.color = baseColor;
    }

    public bool CanAccept(BoxScript box)
    {
        if (CurrentBox != null && CurrentBox != box) return false;
        return AcceptFilter == null || AcceptFilter(box);
    }

    public void NotifyRejected(BoxScript box)
    {
        if (Rejected != null) Rejected(this, box);
    }

    public void OnBoxDrop(BoxScript box)
    {
        CurrentBox = box;
        box.SetCurrentArea(this);

        Vector3 newPosition = transform.position;
        newPosition.z = box.transform.position.z;

        box.transform.position = newPosition;
        Debug.Log("Box dropped here");
    }

    public void RemoveBox(BoxScript box)
    {
        if (CurrentBox == box)
        {
            CurrentBox = null;
        }
    }
}
