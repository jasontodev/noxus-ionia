using UnityEngine;
using System;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Settings")]
    public Team team = Team.Noxus;
    public int maxHP = 100;
    [Tooltip("Current hit points")]
    public int hp = 100;

    [Header("Runtime")]
    public bool isDead = false;
    public bool IsAlive => !isDead && hp > 0;

    // Event fired when this agent dies (provides reference to this Health)
    public Action<Health> OnDeath;

    private void OnEnable()
    {
        // Reset when respawning or re-enabled
        isDead = false;
        hp = Mathf.Clamp(hp, 0, maxHP);
    }

    private void OnDisable()
    {
        // Clear delegates to avoid memory leaks if destroyed dynamically
        OnDeath = null;
    }

    /// <summary>
    /// Apply incoming damage to this agent. Fires OnDeath when HP <= 0.
    /// </summary>
    public void Damage(int dmg)
    {
        if (isDead || dmg <= 0) return;

        hp = Mathf.Max(0, hp - dmg);

        if (hp <= 0)
        {
            isDead = true;

            // Trigger death event for listeners (drops, particles, etc.)
            OnDeath?.Invoke(this);

            // Optional: destroy after a short delay so death visuals can finish
            Destroy(gameObject, 0.1f);
        }
    }

    /// <summary>
    /// Heal this agent by the given amount.
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        hp = Mathf.Min(maxHP, hp + amount);
    }

    /// <summary>
    /// Fully restore HP and clear death flag — useful for respawn systems.
    /// </summary>
    public void Revive()
    {
        hp = maxHP;
        isDead = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize HP in Scene view for debugging
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"HP: {hp}/{maxHP}");
    }
#endif
}
