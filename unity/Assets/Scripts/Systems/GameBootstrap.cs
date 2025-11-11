using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [Header("Scene")]
    public EnvironmentCoordinator env;      // assign in Inspector
    public Camp campNoxus;
    public Camp campIonia;

    [Header("Agents")]
    public GameObject agentPrefab;          // must contain RLAgent + BehaviorParameters
    public int agentsPerTeam = 6;
    public Transform[] noxusSpawns;         // initial spawn transforms (also used by env on reset)
    public Transform[] ioniaSpawns;

    [Header("Visuals")]
    public Material matNoxus;
    public Material matIonia;

    [Header("Initial Stocks & Visual Pickups")]
    [Tooltip("Logical starting stock for camps. These will also be pushed into EnvironmentCoordinator.")]
    public int initialIronNoxus = 100;
    public int initialManaIonia = 100;

    [Tooltip("Purely cosmetic: how many visible orbs to show at each camp. Env will spawn them on ResetEpisode().")]
    public int visiblePickupsPerCamp = 12;

    [ContextMenu("Boot & Reset")]
    void Start()
    {
        if (!env) env = FindObjectOfType<EnvironmentCoordinator>();
        if (!env) { Debug.LogError("[Bootstrap] No EnvironmentCoordinator in scene."); return; }

        env.autoResetOnStart = false; // we reset after spawning

        // (Optional safety) purge any pre-placed RLAgents
        foreach (var a in FindObjectsOfType<RLAgent>())
    #if UNITY_EDITOR
            DestroyImmediate(a.gameObject);
    #else
            Destroy(a.gameObject);
    #endif

        // spawn both teams
        SpawnTeam(Team.Noxus, noxusSpawns, matNoxus);
        SpawnTeam(Team.Ionia, ioniaSpawns, matIonia);

        env.initialIronNoxus      = initialIronNoxus;      // e.g., 100
        env.initialManaIonia      = initialManaIonia;      // e.g., 100
        env.visiblePickupsPerCamp = visiblePickupsPerCamp; // e.g., 12
		env.noxusSpawns           = noxusSpawns;
		env.ioniaSpawns           = ioniaSpawns;

        env.RecomputeBoundsFromScene(20f);
        env.ResetEpisode();
    }


    private void SpawnTeam(Team team, Transform[] spawns, Material mat)
    {
        for (int i = 0; i < agentsPerTeam; i++)
        {
            var t   = spawns[i % spawns.Length];
            var pos = t.position + new Vector3(0.18f * (i % 3), 0f, 0.18f * (i / 3));
            var rot = t.rotation;
    
            // Prefab is inactive at creation (set in Prefab Mode)
            var go = Instantiate(agentPrefab, pos, rot);
    
            // Configure while inactive (no Initialize/OnEnable yet)
            var agent = go.GetComponent<RLAgent>();
            agent.Configure(team);                 // sets BehaviorParameters, DecisionRequester, registers with env
    
            // Optional tint
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend && mat) rend.material = mat;
    
            // Now allow ML-Agents to initialize
            go.SetActive(true);
    
            Debug.Log($"[Bootstrap] Spawned {team} #{i} at {pos}");
        }
    }



    // --------------------------
    // Optional convenience tools
    // --------------------------

    [ContextMenu("Hard Reset Episode")]
    public void HardResetEpisode()
    {
        // Useful if you change counts/materials in Play mode and want to re-seed
        if (!env)
        {
            env = FindObjectOfType<EnvironmentCoordinator>();
            if (!env) { Debug.LogError("[GameBootstrap] No EnvironmentCoordinator found."); return; }
        }

        // Push current inspector values into env
        env.initialIronNoxus      = initialIronNoxus;
        env.initialManaIonia      = initialManaIonia;
        env.visiblePickupsPerCamp = visiblePickupsPerCamp;
        env.noxusSpawns           = noxusSpawns;
        env.ioniaSpawns           = ioniaSpawns;

        env.ResetEpisode();
    }
}
