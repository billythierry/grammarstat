using System;
using System.Collections;
using UnityEngine;

// Menggerakkan penumpang (berjalan dari satu titik ke titik lain dengan efek langkah)
public class PassengerController : MonoBehaviour
{
    [Tooltip("Tinggi pantulan tiap langkah (unit)")]
    [SerializeField] private float bobHeight = 0.12f;
    [Tooltip("Jumlah langkah per detik")]
    [SerializeField] private float stepsPerSecond = 2f;
    [Tooltip("Kemiringan badan saat berjalan (derajat)")]
    [SerializeField] private float tiltAngle = 3f;

    private Coroutine routine;

    public bool IsWalking { get; private set; }

    // Berjalan menuju jendela: melambat di akhir lalu berhenti
    public void WalkTo(Vector3 target, float duration, Action onArrived = null)
    {
        StartWalk(target, duration, true, onArrived);
    }

    // Berjalan meninggalkan jendela: kebalikan WalkTo, mulai pelan lalu makin cepat
    public void WalkAway(Vector3 target, float duration, Action onGone = null)
    {
        StartWalk(target, duration, false, onGone);
    }

    void StartWalk(Vector3 target, float duration, bool arriving, Action onDone)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(WalkRoutine(target, duration, arriving, onDone));
    }

    IEnumerator WalkRoutine(Vector3 target, float duration, bool arriving, Action onDone)
    {
        IsWalking = true;
        Vector3 start = transform.position;
        float dir = Mathf.Sign(target.x - start.x);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            // Datang: ease out (melambat di jendela). Pergi: ease in (mulai pelan dari jendela)
            float eased = arriving ? 1f - (1f - k) * (1f - k) : k * k;
            Vector3 pos = Vector3.Lerp(start, target, eased);
            float step = Mathf.Abs(Mathf.Sin(t * stepsPerSecond * Mathf.PI));
            // Pantulan langkah mengecil saat berhenti dan membesar saat mulai berjalan
            float bobScale = arriving ? 1f - k * 0.5f : 0.5f + k * 0.5f;
            pos.y += step * bobHeight * bobScale;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, 0f, -dir * tiltAngle * Mathf.Sin(t * stepsPerSecond * Mathf.PI));
            yield return null;
        }

        transform.position = target;
        transform.rotation = Quaternion.identity;
        IsWalking = false;
        routine = null;
        onDone?.Invoke();
    }
}
