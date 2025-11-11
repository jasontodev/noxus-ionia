using UnityEngine;

public enum ResourceType { None, Iron, Mana }

public class Carry : MonoBehaviour
{
    public ResourceType carried = ResourceType.None;
    public bool CanPickup => carried == ResourceType.None;
    public bool HasItem => carried != ResourceType.None;
    public void Pickup(ResourceType r) { if (carried == ResourceType.None) carried = r; }
    public ResourceType Drop() { var r = carried; carried = ResourceType.None; return r; }
}
