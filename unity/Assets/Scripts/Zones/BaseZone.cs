using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BaseZone : MonoBehaviour
{
    public Team team;
    public int healPerSecond = 10;
    void OnTriggerStay(Collider other)
    {
        var h = other.GetComponent<Health>();
        if (h && h.team == team && h.IsAlive)
            h.Heal(Mathf.CeilToInt(healPerSecond * Time.deltaTime));
    }
}
