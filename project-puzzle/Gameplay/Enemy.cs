#nullable enable

using System;

namespace Gameplay;

public sealed class Enemy : ICombatant
{
    public enum TargetDestroyedEffect
    {
        None,
        DisableAttack,
        ExposeWeakSpot
    }

    public sealed class Target
    {
        public Target(
            string id,
            string name,
            float hitChanceMultiplier,
            int? maxHealth = null,
            TargetDestroyedEffect destroyedEffect = TargetDestroyedEffect.None)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (maxHealth is <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (!float.IsFinite(hitChanceMultiplier) || hitChanceMultiplier < 0)
                throw new ArgumentOutOfRangeException(nameof(hitChanceMultiplier));

            Id = id;
            Name = name;
            HitChanceMultiplier = hitChanceMultiplier;
            MaxHealth = maxHealth;
            Health = maxHealth;
            DestroyedEffect = destroyedEffect;
        }

        public string Id { get; }
        public string Name { get; }
        public int? MaxHealth { get; }
        public int? Health { get; private set; }
        public float HitChanceMultiplier { get; }
        public TargetDestroyedEffect DestroyedEffect { get; }
        public bool IsDestroyed => Health == 0;

        public void TakeDamage(int amount)
        {
            if (Health is int health)
            {
                Health = Math.Max(0, health - Math.Max(0, amount));
            }
        }
    }

    public Enemy(
        string name,
        int maxHealth,
        int attack,
        Target[] targets,
        string? fullBodyAsset = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);
        ArgumentOutOfRangeException.ThrowIfNegative(attack);
        ArgumentNullException.ThrowIfNull(targets);

        foreach (Target target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
        }

        Name = name;
        MaxHealth = maxHealth;
        Health = maxHealth;
        Attack = attack;
        Targets = targets;
        FullBodyAsset = string.IsNullOrWhiteSpace(fullBodyAsset) ? null : fullBodyAsset;
    }

    public string Name { get; }
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public int Attack { get; }
    public Target[] Targets { get; }
    public string? FullBodyAsset { get; }

    public void TakeDamage(int amount)
    {
        Health = Math.Max(0, Health - Math.Max(0, amount));
    }
}
