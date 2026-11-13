
#nullable enable

using System;

namespace Gameplay
{
    public sealed class PlayerCharacter : ICombatant
    {
        public PlayerCharacter(string name, int maxHealth, int attack)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);
            ArgumentOutOfRangeException.ThrowIfNegative(attack);

            Name = name;
            MaxHealth = maxHealth;
            Health = maxHealth;
            Attack = attack;
        }

        public string Name { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int Attack { get; }
        public string? FullBodyAsset => null;
        public int SelectedTarget { get; set; }

        public void TakeDamage(int amount)
        {
            Health = Math.Max(0, Health - Math.Max(0, amount));
        }
    }
}
