using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Gameplay;

// One clear step of a resolution. Chain is 1 for the clear caused by the placed piece,
// 2 for the cascade after it, and so on.
public readonly struct ClearStep(IReadOnlyList<Cell> cleared, int chain)
{
    public IReadOnlyList<Cell> Cleared { get; } = cleared;
    public int Chain { get; } = chain;

    public int Count(CellState type) => Cleared.Count(c => !c.IsGarbage && c.State == type);
    public int GarbageCount => Cleared.Count(c => c.IsGarbage);
}

// Decides how boards attack each other. Grid reports clears and receives garbage; everything
// in between lives here, so a new combat idea is just a new ICombatRule set on GameScene.Combat.
// Useful Grid calls: SendGarbage, CancelGarbage, PendingGarbageCount.
public interface ICombatRule
{
    // After each clear step, while the chain may still continue.
    void OnCellsCleared(PlayerBoard board, ClearStep step) { }

    // Once the board has settled after a chain of at least one clear.
    void OnChainEnded(PlayerBoard board, int chain) { }

    // Every frame for each live board, for meters, timers, counter windows etc.
    void Update(PlayerBoard board, GameTime gameTime) { }
}

// No attacks. Placeholder until a real combat system is picked.
public class NoCombat : ICombatRule { }
