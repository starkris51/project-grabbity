#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Gameplay
{
    public enum EnemyTargetDestroyedEffect
    {
        // WIP
        None,
        DisableAttack,
        ExposeWeakSpot
    }

    public sealed class EnemyTargetDefinition(
        string id,
        string name,
        int maxHealth,
        float hitChanceMultiplier,
        EnemyTargetDestroyedEffect destroyedEffect = EnemyTargetDestroyedEffect.None)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public int MaxHealth { get; } = maxHealth;
        public float HitChanceMultiplier { get; } = hitChanceMultiplier;
        public EnemyTargetDestroyedEffect DestroyedEffect { get; } = destroyedEffect;
    }

    public sealed class EnemyDefinition
    {
        public EnemyDefinition(
            string id,
            string name,
            int maxHealth,
            int attack,
            string? fullBodyAsset,
            IEnumerable<EnemyTargetDefinition> targets)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);
            ArgumentOutOfRangeException.ThrowIfNegative(attack);
            ArgumentNullException.ThrowIfNull(targets);

            EnemyTargetDefinition[] targetArray = targets.ToArray();
            if (targetArray.Any(target => target is null))
                throw new ArgumentException("Enemy targets cannot contain null entries.", nameof(targets));
            if (targetArray.Select(target => target.Id).Distinct(StringComparer.Ordinal).Count() != targetArray.Length)
                throw new ArgumentException("Enemy target IDs must be unique.", nameof(targets));

            Id = id;
            Name = name;
            MaxHealth = maxHealth;
            Attack = attack;
            FullBodyAsset = string.IsNullOrWhiteSpace(fullBodyAsset) ? null : fullBodyAsset;
            Targets = Array.AsReadOnly(targetArray);
        }

        public string Id { get; }
        public string Name { get; }
        public int MaxHealth { get; }
        public int Attack { get; }
        public string? FullBodyAsset { get; }
        public IReadOnlyList<EnemyTargetDefinition> Targets { get; }
    }

    public sealed class EnemyCharacter : ICombatant
    {
        public EnemyCharacter(EnemyDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            Definition = definition;
            Name = definition.Name;
            MaxHealth = definition.MaxHealth;
            Health = definition.MaxHealth;
            Attack = definition.Attack;
            Targets = Array.AsReadOnly(
                definition.Targets.Select(target => new EnemyTarget(target)).ToArray());
        }

        public EnemyDefinition Definition { get; }
        public string Name { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int Attack { get; }
        public string? FullBodyAsset => Definition.FullBodyAsset;
        public IReadOnlyList<EnemyTarget> Targets { get; }

        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - Math.Max(0, amount));
        }
    }

    public sealed class EnemyTarget
    {
        public EnemyTarget(EnemyTargetDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            Definition = definition;
            Health = definition.MaxHealth;
        }

        public EnemyTargetDefinition Definition { get; }
        public int Health { get; private set; }
        public bool IsDestroyed => Health == 0;

        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - Math.Max(0, amount));
        }
    }
}
