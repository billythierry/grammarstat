using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BoxScript : MonoBehaviour
{
    [SerializeField] private TMP_Text wordText;
    [Tooltip("Jarak teks dari tepi kiri/kanan box (unit dunia)")]
    [SerializeField] private float textPadding = 0.15f;

    [Tooltip("Warna box saat menyala sebagai petunjuk tutorial")]
    [SerializeField] private Color highlightColor = new Color32(0xFF, 0xE2, 0x7A, 0xFF);

    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private bool highlighted;
    private Vector3 startDragPosition;
    private IBoxDropArea currentArea;   // area yang sedang ditempati box
    private IBoxDropArea dragStartArea; // area sebelum box di-drag
    private bool isDragging;

    // Kata/frasa yang tertulis di box ini
    public string Word { get; private set; }

    // Box sedang berada di salah satu drop area gerbong
    public bool IsInDropArea => currentArea is DropAreaScript;

    // Spawn area asal box (tempat kembali saat reset)
    public SpawnAreaScript HomeSpawnArea { get; private set; }

    // Box sudah dimuat ke gerbong dan tidak bisa di-drag lagi
    public bool IsLocked { get; private set; }

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    void Update()
    {
        if (!highlighted || spriteRenderer == null) return;
        float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
        spriteRenderer.color = Color.Lerp(baseColor, highlightColor, pulse);
    }

    // Box menyala sebagai petunjuk box yang harus dipindah berikutnya (tutorial)
    public void SetHighlight(bool on)
    {
        highlighted = on;
        if (!on && spriteRenderer != null) spriteRenderer.color = baseColor;
    }

    public void SetWord(string word)
    {
        Word = word;
        if (wordText == null) wordText = GetComponentInChildren<TMP_Text>(true);
        if (wordText != null) wordText.text = word;
        else Debug.LogWarning("BoxScript: tidak ada TMP_Text di box " + name);
        gameObject.name = "Box (" + word + ")";
    }

    // Ubah lebar box (dunia) tanpa membuat teks gepeng: skala teks dikompensasi
    public void SetWidth(float width)
    {
        Vector3 scale = transform.localScale;
        if (Mathf.Approximately(scale.x, width)) return;
        scale.x = width;
        transform.localScale = scale;

        if (wordText == null) wordText = GetComponentInChildren<TMP_Text>(true);
        if (wordText == null) return;
        Transform t = wordText.transform;
        // Skala dunia teks dibuat seragam mengikuti sumbu y (yang tidak berubah)
        float textWorldScale = t.localScale.y * scale.y;
        Vector3 textScale = t.localScale;
        textScale.x = textWorldScale / width;
        t.localScale = textScale;
        RectTransform rect = wordText.rectTransform;
        rect.sizeDelta = new Vector2((width - textPadding * 2f) / textWorldScale, rect.sizeDelta.y);
    }

    void OnMouseDown()
    {
        if (PauseMenu.IsPaused) return;
        isDragging = true;
        Debug.Log("Box clicked");
        startDragPosition = transform.position;

        dragStartArea = currentArea;
        if (currentArea != null)
        {
            currentArea.RemoveBox(this);
            currentArea = null;
        }

        transform.position = GetMousePositionInWorldSpace();
    }

    void OnMouseDrag()
    {
        if (!isDragging || PauseMenu.IsPaused) return;
        transform.position = GetMousePositionInWorldSpace();
    }

    void OnMouseUp()
    {
        // Abaikan jika klik dimulai saat pause
        if (!isDragging) return;
        isDragging = false;
        col.enabled = false;
        Collider2D hitCollider = Physics2D.OverlapPoint(transform.position);
        col.enabled = true;
        if (hitCollider != null && hitCollider.TryGetComponent(out IBoxDropArea boxDropArea) && boxDropArea.CanAccept(this))
        {
            boxDropArea.OnBoxDrop(this);
        }
        else
        {
            if (hitCollider != null && hitCollider.TryGetComponent(out DropAreaScript rejectedArea)) rejectedArea.NotifyRejected(this);
            // Area tujuan tidak ada atau sudah terisi: kembali ke area semula
            if (dragStartArea != null) dragStartArea.OnBoxDrop(this);
            else transform.position = startDragPosition;
        }
        dragStartArea = null;
    }

    public void SetHomeSpawnArea(SpawnAreaScript spawnArea)
    {
        HomeSpawnArea = spawnArea;
    }

    // Kembalikan box ke spawn area asalnya
    public void ResetToHome()
    {
        if (HomeSpawnArea == null) return;
        DetachFromArea();
        HomeSpawnArea.OnBoxDrop(this);
    }

    // Lepaskan box dari area yang sedang ditempati
    public void DetachFromArea()
    {
        if (currentArea != null)
        {
            currentArea.RemoveBox(this);
            currentArea = null;
        }
    }

    public void SetCurrentArea(IBoxDropArea area)
    {
        currentArea = area;
    }

    // Muat box ke gerbong: ikut bergerak bersama gerbong dan tidak bisa di-drag
    public void LockInto(Transform wagon)
    {
        SetHighlight(false);
        DetachFromArea();
        IsLocked = true;
        col.enabled = false;
        transform.SetParent(wagon, true);
    }

    // Kembalikan box ke pool (nonaktif) agar bisa dipakai untuk soal lain
    public void Release()
    {
        SetHighlight(false);
        DetachFromArea();
        IsLocked = false;
        col.enabled = true;
        transform.SetParent(null, true);
        HomeSpawnArea = null;
        gameObject.SetActive(false);
    }

    public Vector3 GetMousePositionInWorldSpace()
    {
        Vector3 p = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        p.z = transform.position.z;
        return p;
    }
}
