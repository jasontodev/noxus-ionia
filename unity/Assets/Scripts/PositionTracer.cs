using UnityEngine;

public class PositionTracer : MonoBehaviour
{
    public bool logStack = false;
    Vector3 last;

    void OnEnable()
    {
        last = transform.position;
        Debug.Log($"[Trace] {name} enabled at {last}");
    }

    void LateUpdate()
    {
        var p = transform.position;
        if ((p - last).sqrMagnitude > 0.0001f)
        {
            Debug.Log($"[Trace] {name} moved {last} -> {p}"
                + (logStack ? $"\n{new System.Diagnostics.StackTrace()}" : ""));
            last = p;
        }
    }
}
