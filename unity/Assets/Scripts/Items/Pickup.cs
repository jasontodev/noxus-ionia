using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Pickup : MonoBehaviour
{
    [Header("Pickup")]
    public ResourceType type = ResourceType.Iron;
    [Tooltip("Assigned when spawned; used by the camp to account stock.")]
    public Camp ownerCamp;

    [Header("Runtime")]
    public bool taken = false;          // true once an agent picks it up

    [Header("Carry Visuals")]
    public Vector3 carryOffset = new Vector3(0f, 1.0f, 0f);

    void Awake()
    {
        // Keep as a trigger so agents can overlap without physics forces
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        if (!ownerCamp) ownerCamp = GetComponentInParent<Camp>();
        if (ownerCamp) ownerCamp.Register(this);
    }

    void OnDestroy()
    {
        if (ownerCamp) ownerCamp.Unregister(this);
    }

    /// <summary>
    /// Called by the environment/camp when the agent executes the Pick action
    /// and is within grab radius. This marks the pickup as taken, decrements
    /// stock, and attaches the orb to the carrier as a visual.
    /// </summary>
    public void Take(Transform carrier, Carry carry)
    {
        if (taken || !carrier || !carry || !carry.CanPickup) return;

        // Logical handoff first (authoritative stock)
        if (ownerCamp) ownerCamp.NotifyWorldPickupTaken(type);

        // Give to agent
        carry.Pickup(type);
        taken = true;

        // Attach visually to carrier
        transform.SetParent(carrier);
        transform.localPosition = carryOffset;

        // Make this instance cosmetic only
        var col = GetComponent<Collider>(); if (col) col.enabled = false;
        var rb  = GetComponent<Rigidbody>(); if (rb) Destroy(rb);
        // keep renderer ON so the carried orb is visible
    }
}
