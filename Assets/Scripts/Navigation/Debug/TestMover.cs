using System.Collections;
using UnityEngine;

public class TestMover : MonoBehaviour
{
    public CharacterGridNavigator navigator;
    public Transform testTarget;
    void Start()
    {
        StartCoroutine(MoveToTargetWithDelay());
    }
    public IEnumerator MoveToTargetWithDelay()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            MoveToTarget();
        }
    }
    [NaughtyAttributes.Button]
    public void MoveToTarget()
    {
        if (navigator == null)
        {
            Debug.LogWarning("[TestMover] No hay navigator asignado.", this);
            return;
        }
        if (testTarget == null)
        {
            Debug.LogWarning("[TestMover] No hay testTarget asignado.", this);
            return;
        }

        bool ok = navigator.MoveTo(testTarget.position);
        Debug.Log(ok
            ? $"[TestMover] Ruta calculada hacia {testTarget.position}."
            : $"[TestMover] No se pudo calcular ruta hacia {testTarget.position}.", this);
    }

    [NaughtyAttributes.Button]
    public void Stop()
    {
        if (navigator == null) return;
        navigator.Stop();
        Debug.Log("[TestMover] Movimiento detenido.", this);
    }
}
