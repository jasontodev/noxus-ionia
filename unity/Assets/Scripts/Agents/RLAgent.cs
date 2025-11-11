using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

public interface IEnvQuery
{
    Vector2 NormalizePosition(Vector3 worldPos);
    bool InOwnBase(Team team, Vector3 p);
    bool InEnemyBase(Team team, Vector3 p);
    bool AtOwnCamp(Team team, Vector3 p);
    bool AtEnemyCamp(Team team, Vector3 p);
    (int ownIron, int ownMana, int enemyIron, int enemyMana) GetStocks(Team team);
    (float allies, float enemies) LocalDensities(Team team, Vector3 p);
    float TimeNormalized();
    void TryMove(RLAgent agent, Vector3 target);
    Transform NearestEnemyInRange(Team team, Vector3 p, float range);
    void TryPickupAtPosition(Team team, Transform who, Carry carry);
    void TryDropAtPosition(Team team, Transform who, Carry carry);
}
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Carry))]
[RequireComponent(typeof(BehaviorParameters))]
public class RLAgent : Agent
{
    [Header("Team & Movement")]
    public Team team = Team.Ionia;
    public float moveStep = 1.0f;
    public float attackRange = 1.5f;
    public int damage = 10;
    public int attackCooldownTicks = 8;

    [Header("Debug / Mode")]
    public bool forceHeuristic = true; // set FALSE for training
    public bool logActions = false;

    int cd;
    bool _configured;

    Health health;
    Carry carry;
    IEnvQuery env;

    // ---------------------------------------------------
    // EARLY caching so CollectObservations never sees null
    // ---------------------------------------------------
    void Awake()
    {
        health = GetComponent<Health>();
        carry  = GetComponent<Carry>();
        env    = FindObjectOfType<EnvironmentCoordinator>();
    
        // Always have a DecisionRequester
        var dr = GetComponent<DecisionRequester>() ?? gameObject.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = 1;
        dr.TakeActionsBetweenDecisions = true;
    }


    // ---------------------------------------------------
    // Configure — call while GO is INACTIVE, then SetActive(true)
    // ---------------------------------------------------
    public void Configure(Team t, string behaviorName = null, int? teamId = null)
    {
        team = t;

        var bp = GetComponent<BehaviorParameters>();
        bp.TeamId       = teamId ?? (t == Team.Noxus ? 0 : 1);
        bp.BehaviorName = behaviorName ?? (t == Team.Noxus ? "NoxusPolicy" : "IoniaPolicy");
        bp.BehaviorType = forceHeuristic ? BehaviorType.HeuristicOnly : BehaviorType.Default;
        bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(5, 4);

        if (health) health.team = t;
        tag  = (t == Team.Noxus) ? "Noxus" : "Ionia";
        name = $"{t}_Agent_{GetInstanceID()}";

        var coord = FindObjectOfType<EnvironmentCoordinator>();
        if (coord) coord.SwitchRegistration(this, t);

        _configured = true;
    }

    public override void Initialize()
    {
        // Safety: if someone forgot Configure() (should not happen)
        if (!_configured)
        {
            var bp = GetComponent<BehaviorParameters>();
            bp.TeamId       = (team == Team.Noxus) ? 0 : 1;
            bp.BehaviorName = (team == Team.Noxus) ? "NoxusPolicy" : "IoniaPolicy";
            bp.BehaviorType = forceHeuristic ? BehaviorType.HeuristicOnly : BehaviorType.Default;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(5, 4);
            if (health) health.team = team;
            tag = (team == Team.Noxus) ? "Noxus" : "Ionia";
            Debug.LogWarning($"[RLAgent] Initialize without Configure(); using prefab settings for {name}.");
        }

        // Re-cache env in case it was created after Awake (rare)
        if (env == null) env = FindObjectOfType<EnvironmentCoordinator>();
    }

    // Heartbeat: guarantees decisions even if anything hiccups
    float _tick;
    void FixedUpdate()
    {
        _tick += Time.fixedDeltaTime;
        if (_tick > 0.25f) { RequestDecision(); _tick = 0f; }
    }

        // ---------------- Observations (null-safe) ----------------
    public override void CollectObservations(VectorSensor sensor)
    {
		if (sensor == null) return;
        // Self (4)
        float hpNorm = (health != null && health.maxHP > 0) ? (float)health.hp / health.maxHP : 0f;
        sensor.AddObservation(hpNorm);
        sensor.AddObservation(carry != null && carry.HasItem ? 1f : 0f);
        sensor.AddObservation(carry != null && carry.carried == ResourceType.Iron ? 1f : 0f);
        sensor.AddObservation(carry != null && carry.carried == ResourceType.Mana ? 1f : 0f);

        // Try to acquire env if it's still null
        if (env == null) env = FindObjectOfType<EnvironmentCoordinator>();

        if (env == null)
        {
            // Fill with zeros to keep obs size consistent and avoid NREs
            // pos(2) + flags(4) + stocks(4) + densities(2) + time(1) = 13 zeros
            for (int i = 0; i < 13; i++) sensor.AddObservation(0f);
            return;
        }

        // Position (2)
        var p = env.NormalizePosition(transform.position);
        sensor.AddObservation(p.x); sensor.AddObservation(p.y);

        // Zone flags (4)
        sensor.AddObservation(env.InOwnBase(team, transform.position)   ? 1f : 0f);
        sensor.AddObservation(env.InEnemyBase(team, transform.position) ? 1f : 0f);
        sensor.AddObservation(env.AtOwnCamp(team, transform.position)   ? 1f : 0f);
        sensor.AddObservation(env.AtEnemyCamp(team, transform.position) ? 1f : 0f);

        // Camp stocks (4)
        var s = env.GetStocks(team);
        sensor.AddObservation(Mathf.Clamp01(s.ownIron   / 100f));
        sensor.AddObservation(Mathf.Clamp01(s.ownMana   / 100f));
        sensor.AddObservation(Mathf.Clamp01(s.enemyIron / 100f));
        sensor.AddObservation(Mathf.Clamp01(s.enemyMana / 100f));

        // Local densities (2)
        var d = env.LocalDensities(team, transform.position);
        sensor.AddObservation(Mathf.Clamp01(d.allies));
        sensor.AddObservation(Mathf.Clamp01(d.enemies));

        // Time (1)
        sensor.AddObservation(Mathf.Clamp01(env.TimeNormalized()));
    }


    // ---------------- Actions ----------------
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (health != null && !health.IsAlive) return;

        int move = actions.DiscreteActions[0]; // 0 idle,1 L,2 R,3 F,4 B
        int verb = actions.DiscreteActions[1]; // 0 none,1 atk,2 pick,3 drop
        if (logActions) Debug.Log($"[RL] {name} move={move} verb={verb}");

        if (env != null)
        {
            Vector3 d = Vector3.zero;
            if (move == 1) d += Vector3.left;
            if (move == 2) d += Vector3.right;
            if (move == 3) d += Vector3.forward;
            if (move == 4) d += Vector3.back;
            if (d.sqrMagnitude > 0f) env.TryMove(this, transform.position + d * moveStep);

            switch (verb)
            {
                case 1:
                    if (cd == 0)
                    {
                        var t = (env as EnvironmentCoordinator)?.NearestEnemyInRange(team, transform.position, attackRange);
                        if (t) { var h = t.GetComponent<Health>(); if (h) h.Damage(damage); cd = attackCooldownTicks; }
                    }
                    break;
                case 2: env.TryPickupAtPosition(team, transform, carry); break;
                case 3: env.TryDropAtPosition(team, transform, carry);   break;
            }
        }

        if (cd > 0) cd--;
    }

    // Heuristic: random walk when forceHeuristic = true (BehaviorType.HeuristicOnly)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.DiscreteActions;
        a[0] = Random.Range(0, 5);
        a[1] = 0;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var bp = GetComponent<BehaviorParameters>();
        if (bp != null) bp.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(5, 4);
    }
#endif
}