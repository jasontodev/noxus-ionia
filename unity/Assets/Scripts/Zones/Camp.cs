using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Camp : MonoBehaviour
{
    [Header("Camp")]
    public Team team;            // Noxus source = IRON, Ionia source = MANA
    public int iron = 0;         // authoritative logical stocks
    public int mana = 0;

    [Header("Prefabs & Visuals")]
    public GameObject ironPrefab;
    public GameObject manaPrefab;
    public Transform pileAnchor;
    public Vector3 pileJitter = new Vector3(1f, 0f, 1f);
    public Vector3 holdOffset = new Vector3(0.3f, 0.8f, 0.4f);

    [Header("World Pickups (auto-managed)")]
    [SerializeField] private List<Pickup> pickups = new();

    public System.Action<Camp> OnStockChanged;

    Collider _col;

    void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }
    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }

    // ---- registry ----
    public void Register(Pickup p){ if (p && !pickups.Contains(p)) pickups.Add(p); }
    public void Unregister(Pickup p){ if (p) pickups.Remove(p); }

    /// <summary>
    /// Called by a Pickup when it's taken (env-driven).
    /// Decrements this camp's logical stock for that resource.
    /// </summary>
    public void NotifyWorldPickupTaken(ResourceType t)
    {
        if (t == ResourceType.Iron && iron > 0) iron--;
        if (t == ResourceType.Mana && mana > 0) mana--;
        OnStockChanged?.Invoke(this);
    }

    // ------------ ENV-DRIVEN ACTIONS ---------------

    /// <summary>
    /// Try to pick a world pickup within 'radius' of 'who' when the policy chooses Pick.
    /// Returns true if an item was taken (and attached visually).
    /// </summary>
    public bool TryPickWorld(Transform who, Carry carry, float radius)
    {
        if (!who || !carry || !carry.CanPickup) return false;

        float r2 = radius * radius;
        Pickup best = null; float bestD2 = float.PositiveInfinity;

        // Only allow stealing the resource that belongs to THIS camp
        ResourceType want = (team == Team.Noxus) ? ResourceType.Iron : ResourceType.Mana;

        for (int i = 0; i < pickups.Count; i++)
        {
            var p = pickups[i];
            if (!p || p.taken || p.type != want) continue;
            float d2 = (p.transform.position - who.position).sqrMagnitude;
            if (d2 <= r2 && d2 < bestD2) { bestD2 = d2; best = p; }
        }

        if (best)
        {
            best.Take(who, carry);   // decrements stock & attaches visual
            return true;
        }
        return false;
    }

    /// <summary>
    /// Fallback direct handoff (no world sphere available) – still only when the policy picked Pick.
    /// </summary>
    public bool TryPickDirect(Carry carry)
    {
        if (!carry || !carry.CanPickup) return false;

        if (team == Team.Noxus && iron > 0)
        {
            iron--; carry.Pickup(ResourceType.Iron);
            AttachCarryVisual(carry.transform, ResourceType.Iron);
            OnStockChanged?.Invoke(this);
            return true;
        }
        if (team == Team.Ionia && mana > 0)
        {
            mana--; carry.Pickup(ResourceType.Mana);
            AttachCarryVisual(carry.transform, ResourceType.Mana);
            OnStockChanged?.Invoke(this);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Deposit the carried item into the correct HOME camp and spawn a non-pickup pile orb.
    /// Called only when the policy chose Drop.
    /// </summary>
    public bool TryDropAtHome(Health h, Carry c)
    {
        if (!h || !c || !c.HasItem) return false;

        ResourceType r = c.carried;
        bool correct = (team == Team.Noxus && r == ResourceType.Iron) ||
                       (team == Team.Ionia && r == ResourceType.Mana);
        if (!correct) return false;

        if (r == ResourceType.Iron) iron++;
        if (r == ResourceType.Mana) mana++;
        c.Drop();

        SpawnPileVisual(r);
        OnStockChanged?.Invoke(this);
        return true;
    }

    // -------------- visuals --------------

    void SpawnPileVisual(ResourceType r)
    {
        GameObject prefab = (r == ResourceType.Iron) ? ironPrefab : manaPrefab;
        if (!prefab) return;

        Vector3 basePos = pileAnchor ? pileAnchor.position : transform.position;
        Vector3 jitter = new Vector3(
            Random.Range(-pileJitter.x, pileJitter.x),
            0.5f,
            Random.Range(-pileJitter.z, pileJitter.z)
        );
        var go = Instantiate(prefab, basePos + jitter, Quaternion.identity);
        var pick = go.GetComponent<Pickup>();
        if (pick) { pick.taken = true; pick.enabled = false; }   // not a pickup anymore
        var col = go.GetComponent<Collider>(); if (col) col.enabled = false;
        var rb  = go.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
    }

    void AttachCarryVisual(Transform carrier, ResourceType type)
    {
        GameObject prefab = (type == ResourceType.Iron) ? ironPrefab : manaPrefab;
        if (!prefab || !carrier) return;

        var vis = Instantiate(prefab, carrier);
        vis.transform.localPosition = holdOffset;

        var col = vis.GetComponent<Collider>(); if (col) col.enabled = false;
        var rb  = vis.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
        var p   = vis.GetComponent<Pickup>();   if (p) { p.taken = true; p.enabled = false; }
    }

    // -------------- helpers --------------

    /// <summary> Spawn N visible world pickups around this camp (purely visual until taken). </summary>
    public void SpawnInitialWorldPickups(int count)
    {
        ResourceType t = (team == Team.Noxus) ? ResourceType.Iron : ResourceType.Mana;
        GameObject prefab = (t == ResourceType.Iron) ? ironPrefab : manaPrefab;
		if (!prefab)
		{
			Debug.LogWarning($"[Camp] Missing {(t == ResourceType.Iron ? "ironPrefab" : "manaPrefab")} on camp '{name}' (team {team}). No pickups will spawn.");
			return;
		}

		Debug.Log($"[Camp:{name}] Spawning {Mathf.Max(0, count)} {(t == ResourceType.Iron ? "Iron" : "Mana")} pickups.");
		int total = Mathf.Max(0, count);
		int cols = Mathf.CeilToInt(Mathf.Sqrt(total));
		int rows = (cols == 0) ? 0 : Mathf.CeilToInt(total / (float)cols);
		float width  = pileJitter.x * 2f;
		float depth  = pileJitter.z * 2f;
		float stepX  = (cols > 1) ? width  / (cols - 1) : 0f;
		float stepZ  = (rows > 1) ? depth  / (rows - 1) : 0f;

		Vector3 basePos = pileAnchor ? pileAnchor.position : transform.position;

		for (int i = 0; i < total; i++)
		{
			int c = (cols == 0) ? 0 : (i % cols);
			int r = (cols == 0) ? 0 : (i / cols);
			float x = -pileJitter.x + c * stepX;
			float z = -pileJitter.z + r * stepZ;
			Vector3 offset = new Vector3(x, 0.5f, z);

			// Instantiate without parent so world scale remains at prefab defaults
			var go = Instantiate(prefab, basePos + offset, Quaternion.identity);
			// Ensure instance is active even if the prefab asset was saved inactive
			if (go && !go.activeSelf) go.SetActive(true);
			// Parent after instantiation, preserving world transform (keeps scale independent of camp scale)
			if (go) go.transform.SetParent(transform, true);
			if (!go) { Debug.LogError($"[Camp:{name}] Instantiate returned null at i={i}"); continue; }
            var pick = go.GetComponent<Pickup>();
            if (pick)
            {
                pick.type = t;
                pick.ownerCamp = this;
                pick.taken = false;
                Register(pick);
				Debug.Log($"[Camp:{this.name}] Spawned pickup '{go.name}' ({t}) at {go.transform.position}");
			}
			else
			{
				Debug.LogWarning($"[Camp:{name}] Spawned prefab has no Pickup component: '{prefab.name}'");
            }
        }
    }
}
