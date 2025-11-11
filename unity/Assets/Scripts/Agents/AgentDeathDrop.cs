using UnityEngine;

[RequireComponent(typeof(Health))]
public class AgentDeathDrop : MonoBehaviour
{
    [Header("Prefabs to drop if no visual child is found")]
    public GameObject ironPrefab;
    public GameObject manaPrefab;

    [Header("Drop Settings")]
    [Tooltip("Random horizontal offset for where the dropped item lands.")]
    public Vector2 dropJitterXZ = new Vector2(0.3f, 0.3f);
    [Tooltip("Vertical drop height offset.")]
    public float dropHeight = 0.5f;
    [Tooltip("Initial impulse applied to dropped pickups.")]
    public Vector3 dropImpulse = new Vector3(0.5f, 1.0f, 0.5f);

    [Header("Optional")]
    [Tooltip("If set, tag reactivated visuals as this (e.g., 'Pickup'). Leave empty to skip.")]
    public string pickupTagOnReactivate = "Pickup";

    private Carry carry;
    private Health health;
    private bool hasDropped = false; // guard against multiple calls

    void Awake()
    {
        carry = GetComponent<Carry>();
        health = GetComponent<Health>();
        health.OnDeath += HandleDeath;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= HandleDeath;
    }

    void HandleDeath(Health h)
    {
        if (hasDropped) return;              // guard
        hasDropped = true;

        if (carry == null || !carry.HasItem)
            return;

        // 1) Prefer to reactivate a carried visual (child with Pickup)
        Transform carriedVisual = FindCarriedVisual();
        if (carriedVisual != null)
        {
            ReactivateVisualAsPickup(carriedVisual);
        }
        else
        {
            // 2) Fallback: spawn a new prefab
            SpawnFreshPickup();
        }

        // 3) Clear logical state
        carry.Drop();
    }

    Transform FindCarriedVisual()
    {
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Pickup>())
                return child;
        }
        return null;
    }

    void ReactivateVisualAsPickup(Transform visual)
    {
        visual.SetParent(null);

        Vector3 jitter =
            new Vector3(Random.Range(-dropJitterXZ.x, dropJitterXZ.x), dropHeight,
                        Random.Range(-dropJitterXZ.y, dropJitterXZ.y));
        visual.position = transform.position + jitter;

        // Mark as available again
        var pickup = visual.GetComponent<Pickup>();
        if (pickup) pickup.taken = false;

        // Ensure collider is enabled
        var col = visual.GetComponent<Collider>();
        if (col) col.enabled = true;

        // Ensure it’s visible (in case you hid it while carried)
        var rend = visual.GetComponent<Renderer>();
        if (rend) rend.enabled = true;

        // Optional: tag as pickup so other systems recognize it
        if (!string.IsNullOrEmpty(pickupTagOnReactivate))
        {
            visual.gameObject.tag = pickupTagOnReactivate;
        }

        // Give it a little life
        var rb = visual.GetComponent<Rigidbody>();
        if (!rb) rb = visual.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.AddForce(dropImpulse, ForceMode.Impulse);
    }

    void SpawnFreshPickup()
    {
        GameObject prefab = (carry.carried == ResourceType.Iron) ? ironPrefab : manaPrefab;
        if (!prefab) return;

        Vector3 jitter =
            new Vector3(Random.Range(-dropJitterXZ.x, dropJitterXZ.x), dropHeight,
                        Random.Range(-dropJitterXZ.y, dropJitterXZ.y));
        var go = Instantiate(prefab, transform.position + jitter, Quaternion.identity);

        // Make sure spawned prefab will behave like a pickup
        var col = go.GetComponent<Collider>();
        if (col) col.enabled = true;

        var rend = go.GetComponent<Renderer>();
        if (rend) rend.enabled = true;

        if (!string.IsNullOrEmpty(pickupTagOnReactivate))
        {
            go.tag = pickupTagOnReactivate;
        }

        var rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.AddForce(dropImpulse, ForceMode.Impulse);
    }
}
