Todo:

[] Hold piece
[X] Rework Player controls
[] AI system
[] Combat system
[] Score system
[] WIP Decorations
[] Clear effects
[] User interface
[] Add single player endless mode and 1vsAI gamemode and 1v1 multiplayer
[] Add wip story mode (4 stages of 1vsAI)
[] Experiment with enemy combat attacks
[] Finish the gameplay
[] Graphics overhaul


combat system draft:

Draft: "Pressure" system, built around your typed clears

Three separate meters — one per cell type (X / Fire / O).
Clearing a line of a given type fills that type's meter, not a shared universal meter. This matters because it makes which type you clear a strategic choice, not just "clear whatever," and it plugs directly into your Mono/Dual/Chaos piece identity instead of bolting on a generic combo counter.

Garbage is typed and locked.
When you spend a meter to attack, you send garbage cells of that specific type to the opponent. Locked garbage can only be cleared by matching a line of that same type — not by any clear. This means an X-attack specifically punishes an opponent's O/Fire-heavy board, forcing them to either already have the right color available or scramble a piece into place. It also makes your existing Mono pieces suddenly valuable defensively (a Mono X piece is the fastest way to clear X-garbage), while Chaos pieces become a "I can address any garbage type, but slowly and riskily" tool.

Chains scale attack size, not just score.
A cascade of 3 clears in one resolution should send meaningfully more garbage than 3 separate single clears over time — this is what creates the "build up, then release" tension Puyo lives on, and your gravity/cascade system already produces this naturally, you'd just need to hook attack-size to cascade depth.

Countering: the interaction layer.
If you clear a chain while garbage is incoming (within a short buffer window, ~1 second), your chain's power cancels part or all of the incoming attack instead of just adding to your own meter. This is the single most important piece — without it, the game is just "who fills up slower," not a fight. It also creates a genuine mind-game: do you spend your chain offensively now, or hold it in case they attack first?

Signature move — "Overload."
Your earlier idea (clearing 12 X-cells from all 4 directions at once) becomes the game's version of a Tetris/T-spin: resolving 2+ separate same-type lines in a single placement instantly maxes and spends that meter for a guaranteed heavy attack. Rare, high-skill, something to build toward rather than routine.

Optional twist — garbage enters from the bottom, not the top.
Since your board's already tall (8×14) and everything gravity-cascades, injecting garbage rows at the bottom and letting the existing stack ride upward on physics you've already built creates urgency (shrinking safe height) without needing new collision logic — and it's a genuinely different feel from every top-down garbage system in the genre.

Tuning knobs to playtest
How much garbage per meter-point (too little = no pressure, too much = solved matches)
Counter window length (too generous = no risk in attacking, too tight = feels unfair)
Whether Chaos pieces filling all three meters at once on a clean clear is a strong enough incentive to justify their placement risk

Clear effect ideas:

1. Per-run outline flash (cheapest, biggest impact)
Instead of just fading/popping matched cells, draw a distinct border around each run before it clears — one outline color for horizontal runs, a different one for vertical runs. If a cell is part of both, it gets both outlines (a cross/corner shape). This alone answers "why did this clear" at a glance since the player sees two separate colored outlines overlapping rather than one blob vanishing.

2. Sequential micro-delay instead of instant pop
Don't clear all matched cells in the same frame. Stagger it by ~50-80ms per run: horizontal run flashes and clears first, then vertical run clears a beat later (even if they share a cell). This turns a single confusing event into a readable two-step "this line, then this line" sequence — cheap to implement (just an index-ordered coroutine/timer), huge readability win.