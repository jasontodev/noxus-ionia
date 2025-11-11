using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;

public class EnvironmentCoordinator : MonoBehaviour, IEnvQuery
{
    [Header("Scene References")]
    public Camp campNoxus;
    public Camp campIonia;
    public Transform baseNoxus;
    public Transform baseIonia;

    [Tooltip("Spawn points for each team at episode start")]
    public Transform[] noxusSpawns;
    public Transform[] ioniaSpawns;

    [Header("Zones & Map")]
    public float baseRadius = 3f;
    public float campRadius = 3f;
    [Tooltip("XZ world bounds used for movement clamping & position normalization")]
    public Vector2 xzMin = new Vector2(-25f, -5f);
    public Vector2 xzMax = new Vector2( 25f,  5f);

    [Header("Episode Settings")]
    public bool autoResetOnStart = false;      // set false and call ResetEpisode() from your Bootstrap after spawning/configuring
    public int maxSteps = 600;                 // decisions per episode
    public bool noRespawn = true;
    public int initialIronNoxus = 100;
    public int initialManaIonia = 100;
    [Tooltip("Spawn a visible subset of world pickups for readability (0 = none)")]
    public int visiblePickupsPerCamp = 0;      // purely cosmetic

    [Header("Rewards")]
    public bool useShaping = false;            // off = terminal-only learning
    [Tooltip("Per-step small bonus based on change in own camp total")]
    public float stockDeltaReward = 0.01f;
    [Tooltip("Reward/penalty applied at timeout (tie)")]
    public float timeoutPenalty = -0.1f;
    [Tooltip("Win/Loss terminal rewards")]
    public float winReward = 1.0f;
    public float lossPenalty = -1.0f;

    [Header("Densities")]
    public float localDensityRadius = 2.5f;
    public int densityNormalizeCap = 5;

    [Header("Anti-Overlap (very light)")]
    public bool preventOverlap = true;
    public float minSeparation = 0.4f;

    [Header("Pickup Rules")]
    [Tooltip("Radius around the agent position within which a world pickup can be taken when verb=Pick")]
    public float worldPickRadius = 1.2f;

    [Header("Spawn Spread")]
    public float spawnGridStep = 0.6f;   // spacing between agents at a spawn
    public int   spawnGridCols = 3;      // wrap to next row after this many

    // --- runtime ---
    private readonly List<RLAgent> noxusAgents = new();
    private readonly List<RLAgent> ioniaAgents = new();
    private int stepCount;
    private int lastTotalNoxus; // campNoxus.iron + campNoxus.mana
    private int lastTotalIonia;

    // ---------- Agent registration API (called by RLAgent.Configure) ----------
    public void SwitchRegistration(RLAgent a, Team t)
    {
        if (!a) return;
        noxusAgents.Remove(a);
        ioniaAgents.Remove(a);
        if (t == Team.Noxus) { if (!noxusAgents.Contains(a)) noxusAgents.Add(a); }
        else                 { if (!ioniaAgents.Contains(a)) ioniaAgents.Add(a); }
    }
    public void RegisterAgent(RLAgent a)  => SwitchRegistration(a, a ? a.team : Team.Ionia);
    public void UnregisterAgent(RLAgent a){ noxusAgents.Remove(a); ioniaAgents.Remove(a); }

    void Start()
    {
        if (autoResetOnStart) ResetEpisode();   // otherwise let Bootstrap call it after spawning/configuring
    }

    void FixedUpdate()
    {
        stepCount++;

        // Optional shaping: +reward for change in own camp totals (team-shared)
        if (useShaping)
        {
            var nNow = campNoxus.iron + campNoxus.mana;
            var iNow = campIonia.iron + campIonia.mana;

            int dN = nNow - lastTotalNoxus;
            int dI = iNow - lastTotalIonia;

            if (dN != 0) AddTeamReward(Team.Noxus, stockDeltaReward * Mathf.Sign(dN));
            if (dI != 0) AddTeamReward(Team.Ionia, stockDeltaReward * Mathf.Sign(dI));

            lastTotalNoxus = nNow;
            lastTotalIonia = iNow;
        }

        // Terminal checks
        bool noxusAlive = TeamAlive(Team.Noxus);
        bool ioniaAlive = TeamAlive(Team.Ionia);

        bool done = false;
        int winner = -1; // 0 = Noxus, 1 = Ionia, -1 = tie/timeout

        if (!noxusAlive) { done = true; winner = 1; }
        else if (!ioniaAlive) { done = true; winner = 0; }
        else if ((campNoxus.iron + campNoxus.mana) >= 200) { done = true; winner = 0; }
        else if ((campIonia.iron + campIonia.mana) >= 200) { done = true; winner = 1; }
        else if (stepCount >= maxSteps) { done = true; winner = -1; }

        if (done)
        {
            if (winner == 0) { AddTeamReward(Team.Noxus, winReward); AddTeamReward(Team.Ionia, lossPenalty); }
            else if (winner == 1) { AddTeamReward(Team.Ionia, winReward); AddTeamReward(Team.Noxus, lossPenalty); }
            else { AddTeamReward(Team.Noxus, timeoutPenalty); AddTeamReward(Team.Ionia, timeoutPenalty); }

            EndEpisodeForAll();
            // IMPORTANT: Do NOT rely on a cached agent list; Bootstrap may respawn/Configure between episodes.
            ResetEpisode();
        }
    }

    // ----------------------
    // Episode orchestration
    // ----------------------
    private void EndEpisodeForAll()
    {
        foreach (var a in noxusAgents) if (a) a.EndEpisode();
        foreach (var a in ioniaAgents) if (a) a.EndEpisode();
    }

    public void RecomputeBoundsFromScene(float margin = 10f)
    {
        var pts = new System.Collections.Generic.List<Vector3>();
    
        void add(Transform t) { if (t) pts.Add(t.position); }
        add(baseNoxus); add(baseIonia);
        add(campNoxus ? campNoxus.transform : null);
        add(campIonia ? campIonia.transform : null);
        if (noxusSpawns != null) foreach (var t in noxusSpawns) add(t);
        if (ioniaSpawns != null) foreach (var t in ioniaSpawns) add(t);
    
        if (pts.Count == 0) return;
    
        float minX = Mathf.Infinity, maxX = -Mathf.Infinity;
        float minZ = Mathf.Infinity, maxZ = -Mathf.Infinity;
        foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                                 minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
    
        xzMin = new Vector2(minX - margin, minZ - margin);
        xzMax = new Vector2(maxX + margin, maxZ + margin);
        Debug.Log($"[Env] Auto bounds set to x[{xzMin.x},{xzMax.x}] z[{xzMin.y},{xzMax.y}]");
    }


    public void ResetEpisode()
    {
        stepCount = 0;

        // Reset logical stocks
		Debug.Log($"[Env] ResetEpisode start. visiblePickupsPerCamp={visiblePickupsPerCamp}, initialIronNoxus={initialIronNoxus}, initialManaIonia={initialManaIonia}");
        campNoxus.iron = initialIronNoxus; campNoxus.mana = 0;
        campIonia.iron = 0;                campIonia.mana = initialManaIonia;
        lastTotalNoxus = campNoxus.iron + campNoxus.mana;
        lastTotalIonia = campIonia.iron + campIonia.mana;

        // (Optional) Clear & respawn visible world pickups (cosmetic only)
        if (visiblePickupsPerCamp > 0)
        {
			Debug.Log($"[Env] Spawning visual pickups per camp = {visiblePickupsPerCamp}");
            ClearCampVisualChildren(campNoxus.transform);
            ClearCampVisualChildren(campIonia.transform);
			if (campNoxus) campNoxus.SpawnInitialWorldPickups(visiblePickupsPerCamp);
			else Debug.LogError("[Env] campNoxus is null; cannot spawn Noxus pickups.");
			if (campIonia) campIonia.SpawnInitialWorldPickups(visiblePickupsPerCamp);
			else Debug.LogError("[Env] campIonia is null; cannot spawn Ionia pickups.");
        }

        if (noxusSpawns == null || noxusSpawns.Length == 0)
            Debug.LogWarning("[Env] Noxus spawns are empty. Using baseNoxus as fallback.");
        if (ioniaSpawns == null || ioniaSpawns.Length == 0)
            Debug.LogWarning("[Env] Ionia spawns are empty. Using baseIonia as fallback.");
        
        // Reset agents: HP, carry, pose, and place at team spawns
        ResetTeam(noxusAgents, noxusSpawns, baseNoxus);
        ResetTeam(ioniaAgents, ioniaSpawns, baseIonia);
    }

    private void ResetTeam(List<RLAgent> teamAgents, Transform[] spawns, Transform fallbackBase)
    {
        if (teamAgents == null || teamAgents.Count == 0) return;

        if (spawns == null || spawns.Length == 0)
        {
            Debug.LogError($"[Env] No spawns assigned for team {teamAgents[0].team}. "
                         + $"Assign {teamAgents.Count} (or more) transforms in the Inspector.");
        }

        int idx = 0;
        for (int i = 0; i < teamAgents.Count; i++)
        {
            var a = teamAgents[i];
            if (!a) continue;

            // reset hp/carry
            var h = a.GetComponent<Health>(); if (h) h.hp = h.maxHP;
            var c = a.GetComponent<Carry>();  if (c && c.HasItem) c.Drop();

            // choose a spawn transform (or base as a last resort)
            Vector3 basePos = (spawns != null && spawns.Length > 0)
                ? spawns[i % spawns.Length].position
                : (fallbackBase ? fallbackBase.position : new Vector3(xzMin.x, a.transform.position.y, xzMin.y));

            // add a tiny grid offset so agents don’t sit on the exact same point
            int col = idx % Mathf.Max(1, spawnGridCols);
            int row = idx / Mathf.Max(1, spawnGridCols);
            Vector3 spread = new Vector3(col * spawnGridStep, 0f, row * spawnGridStep);
            // center the grid on the spawn point
            float halfCols = (Mathf.Min(spawnGridCols, teamAgents.Count) - 1) * 0.5f * spawnGridStep;
            Vector3 centered = basePos + spread - new Vector3(halfCols, 0f, 0f);

            // clamp and apply
            a.transform.position = new Vector3(
                Mathf.Clamp(centered.x, xzMin.x, xzMax.x),
                a.transform.position.y,
                Mathf.Clamp(centered.z, xzMin.y, xzMax.y)
            );

            // face mid (optional)
            a.transform.forward = (Vector3.zero - a.transform.position).normalized;

            Debug.Log($"[Env] Reset {a.team} agent {i} to {a.transform.position}");
            idx++;
        }
    }

    private void ClearCampVisualChildren(Transform campRoot)
    {
        // Only remove spawned visuals (safe because SpawnInitialWorldPickups parents them to the camp)
        var toDelete = new List<GameObject>();
        foreach (Transform t in campRoot) toDelete.Add(t.gameObject);
        foreach (var go in toDelete) Destroy(go);
    }

    private bool TeamAlive(Team t)
    {
        var list = (t == Team.Noxus) ? noxusAgents : ioniaAgents;
        foreach (var a in list)
        {
            if (!a) continue;
            var h = a.GetComponent<Health>();
            if (h != null && h.IsAlive) return true;
        }
        return false;
    }

    private void AddTeamReward(Team t, float r)
    {
        var list = (t == Team.Noxus) ? noxusAgents : ioniaAgents;
        foreach (var a in list) if (a) a.AddReward(r);
    }

    // -------------------------
    // IEnvQuery implementations
    // -------------------------
    public Vector2 NormalizePosition(Vector3 worldPos)
    {
        float nx = Mathf.InverseLerp(xzMin.x, xzMax.x, worldPos.x); // 0..1
        float nz = Mathf.InverseLerp(xzMin.y, xzMax.y, worldPos.z); // 0..1
        return new Vector2(nx, nz);
    }

    bool InCircle(Transform center, Vector3 p, float r)
        => (center.position - p).sqrMagnitude <= r * r;

    public bool InOwnBase(Team team, Vector3 p)
        => team == Team.Noxus ? InCircle(baseNoxus, p, baseRadius) : InCircle(baseIonia, p, baseRadius);

    public bool InEnemyBase(Team team, Vector3 p)
        => team == Team.Noxus ? InCircle(baseIonia, p, baseRadius) : InCircle(baseNoxus, p, baseRadius);

    public bool AtOwnCamp(Team team, Vector3 p)
        => team == Team.Noxus ? InCircle(campNoxus.transform, p, campRadius) : InCircle(campIonia.transform, p, campRadius);

    public bool AtEnemyCamp(Team team, Vector3 p)
        => team == Team.Noxus ? InCircle(campIonia.transform, p, campRadius) : InCircle(campNoxus.transform, p, campRadius);

    public (int ownIron, int ownMana, int enemyIron, int enemyMana) GetStocks(Team team)
    {
        if (team == Team.Noxus) return (campNoxus.iron, campNoxus.mana, campIonia.iron, campIonia.mana);
        else                    return (campIonia.iron, campIonia.mana, campNoxus.iron, campNoxus.mana);
    }

    public (float allies, float enemies) LocalDensities(Team team, Vector3 p)
    {
        float r2 = localDensityRadius * localDensityRadius;
        int allies = 0, enemies = 0;

        // Count across BOTH lists
        foreach (var a in noxusAgents)
        {
            if (!a) continue;
            var h = a.GetComponent<Health>(); if (h == null || !h.IsAlive) continue;
            float d2 = (a.transform.position - p).sqrMagnitude;
            if (d2 <= r2) { if (a.team == team) allies++; else enemies++; }
        }
        foreach (var a in ioniaAgents)
        {
            if (!a) continue;
            var h = a.GetComponent<Health>(); if (h == null || !h.IsAlive) continue;
            float d2 = (a.transform.position - p).sqrMagnitude;
            if (d2 <= r2) { if (a.team == team) allies++; else enemies++; }
        }

        float normAllies = Mathf.Clamp01(allies / (float)Mathf.Max(1, densityNormalizeCap));
        float normEnemies = Mathf.Clamp01(enemies / (float)Mathf.Max(1, densityNormalizeCap));
        return (normAllies, normEnemies);
    }

    public float TimeNormalized()
        => Mathf.Clamp01(1f - (stepCount / Mathf.Max(1f, (float)maxSteps)));

    public void TryMove(RLAgent agent, Vector3 target)
    {
        // Clamp to bounds
        target.x = Mathf.Clamp(target.x, xzMin.x, xzMax.x);
        target.z = Mathf.Clamp(target.z, xzMin.y, xzMax.y);

        // Commit move
        Vector3 pos = agent.transform.position;
        agent.transform.position = new Vector3(target.x, pos.y, target.z);

        // Very light separation (optional) — push away same-team neighbors a bit
        if (!preventOverlap) return;

        var neighbors = agent.team == Team.Noxus ? noxusAgents : ioniaAgents;
        for (int i = 0; i < neighbors.Count; i++)
        {
            var other = neighbors[i];
            if (other == null || other == agent) continue;
            Vector3 op = other.transform.position;
            Vector3 ap = agent.transform.position;
            Vector3 diff = ap - op; diff.y = 0f;
            float dist = diff.magnitude;
            if (dist > 0f && dist < minSeparation)
            {
                Vector3 push = diff.normalized * (minSeparation - dist) * 0.5f;
                agent.transform.position = ClampXZ(ap + push);
                other.transform.position = ClampXZ(op - push);
            }
        }
    }

    public Transform NearestEnemyInRange(Team team, Vector3 p, float range)
    {
        float r2 = range * range;
        float best = float.PositiveInfinity;
        Transform bestT = null;

        var candidates = team == Team.Noxus ? ioniaAgents : noxusAgents;
        foreach (var a in candidates)
        {
            if (!a) continue;
            var h = a.GetComponent<Health>(); if (h == null || !h.IsAlive) continue;
            float d2 = (a.transform.position - p).sqrMagnitude;
            if (d2 <= r2 && d2 < best) { best = d2; bestT = a.transform; }
        }
        return bestT;
    }

    public void TryPickupAtPosition(Team team, Transform who, Carry carry)
    {
        if (carry == null || carry.HasItem) return;

        // Only when inside ENEMY camp
        if (!AtEnemyCamp(team, who.position)) return;

        // Prefer world pickup in radius; if none, allow direct
        if (team == Team.Noxus)
        {
            if (!campIonia.TryPickWorld(who, carry, worldPickRadius))
                campIonia.TryPickDirect(carry);
        }
        else
        {
            if (!campNoxus.TryPickWorld(who, carry, worldPickRadius))
                campNoxus.TryPickDirect(carry);
        }
    }

    public void TryDropAtPosition(Team team, Transform who, Carry carry)
    {
        if (carry == null || !carry.HasItem) return;
        if (!AtOwnCamp(team, who.position)) return;

        var h = who.GetComponent<Health>();
        if (team == Team.Noxus) campNoxus.TryDropAtHome(h, carry);
        else                    campIonia.TryDropAtHome(h, carry);
    }

    // -------------
    // Utilities
    // -------------
    private Vector3 ClampXZ(Vector3 v)
    {
        v.x = Mathf.Clamp(v.x, xzMin.x, xzMax.x);
        v.z = Mathf.Clamp(v.z, xzMin.y, xzMax.y);
        return v;
    }
}
