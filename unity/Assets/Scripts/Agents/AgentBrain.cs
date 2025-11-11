// using UnityEngine;
// using UnityEngine.AI;

// public class AgentBrain : MonoBehaviour
// {
//     public Team team = Team.Noxus;
//     public Transform[] waypoints; // 0..4 : Base_Noxus, Camp_Noxus, Forest, Camp_Ionia, Base_Ionia
//     public float attackRange = 1.5f;
//     public int damage = 10;
//     public float attackCooldown = 0.8f;

//     NavMeshAgent nav;
//     Health health;
//     Carry carry;
//     float cd;

//     void Awake()
//     {
//         nav = GetComponent<NavMeshAgent>();
//         health = GetComponent<Health>();
//         carry = GetComponent<Carry>();
//         health.team = team;
//     }

//     void Update()
//     {
//         if (!health.IsAlive) { nav.isStopped = true; return; }
//         cd -= Time.deltaTime;

//         // 1) Simple target selection: if carrying, go to own camp; else go to enemy camp.
//         int targetIndex = carry.HasItem ? (team == Team.Noxus ? 1 : 3) : (team == Team.Noxus ? 3 : 1);
//         var target = waypoints[targetIndex];
//         if (target) nav.SetDestination(target.position);

//         // 2) Interact when in camp triggers (handled in OnTriggerStay)
//         // 3) If enemy in range, attack
//         TryAttackNearest();
//     }

//     void TryAttackNearest()
//     {
//         if (cd > 0f) return;
//         var enemies = GameObject.FindGameObjectsWithTag(team == Team.Noxus ? "Ionia" : "Noxus");
//         Transform best = null; float dmin = 999f;
//         foreach (var e in enemies)
//         {
//             var h = e.GetComponent<Health>();
//             if (h && h.IsAlive)
//             {
//                 float d = Vector3.Distance(transform.position, e.transform.position);
//                 if (d < dmin) { dmin = d; best = e.transform; }
//             }
//         }
//         if (best && dmin <= attackRange)
//         {
//             best.GetComponent<Health>()?.Damage(damage);
//             cd = attackCooldown;
//         }
//     }

//     void OnTriggerStay(Collider other)
//     {
//         // pickup / drop logic when standing on a camp
//         var camp = other.GetComponent<Camp>();
//         if (!camp) return;

//         if (team == Team.Noxus)
//         {
//             if (camp.team == Team.Noxus && carry.HasItem) camp.TryDrop(health, carry);
//             else if (camp.team == Team.Ionia && !carry.HasItem) camp.TryPickup(carry);
//         }
//         else // Ionia
//         {
//             if (camp.team == Team.Ionia && carry.HasItem) camp.TryDrop(health, carry);
//             else if (camp.team == Team.Noxus && !carry.HasItem) camp.TryPickup(carry);
//         }
//     }
// }
