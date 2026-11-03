#nullable enable

namespace Gameplay;

public interface ICombatant
{
    string Name { get; }
    int MaxHealth { get; }
    int Health { get; }
    int Attack { get; }

    // Null means the combatant has no full-body portrait.
    string? FullBodyAsset { get; }
    void TakeDamage(int amount);
}