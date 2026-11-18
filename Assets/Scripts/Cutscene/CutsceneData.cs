using System.Collections.Generic;
using UnityEngine;

// Isi satu cutscene: latar, baris dialog, dan scene tujuan setelah selesai / di-skip
[CreateAssetMenu(menuName = "Grammar Station/Cutscene Data", fileName = "Cutscene")]
public class CutsceneData : ScriptableObject
{
    public List<CutsceneStage> stages = new List<CutsceneStage>();
    public List<CutsceneLine> lines = new List<CutsceneLine>();
    [Tooltip("Scene yang dimuat setelah dialog terakhir (kosongkan jika tidak ada)")]
    public string nextSceneName = "";

    [Header("Penutup")]
    [Tooltip("Tampilkan layar The End setelah dialog terakhir, sebelum pindah ke Next Scene")]
    public bool showTheEnd;
    public string endTitle = "The End";
    [TextArea(1, 3)] public string endSubtitle = "Terima kasih telah bermain Grammar Station!";
}

// Cutscene yang akan diputar oleh scene Cutscene, diisi sebelum scene itu dimuat
public static class CutsceneSelection
{
    public static CutsceneData Current { get; set; }
}
