# PassiveMobs

A BepInEx mod for Valheim 1.0 with two independent features (both on by default):

1. **Passive creatures:** By default Lox, Asksvin, Moose, Boar, Hen, Neck, and Bjorn leave players alone until a player damages them. After that they fight back as normal for `ProvokedDuration` seconds after the last hit, then calm down again.
2. **Enemies fear strong players:** non-passive enemies run away from players whose gear outclasses it, instead of attacking.

## How "outclassed" is decided

The mod uses two checks, each can be turned on or off:

| Check | Passes when |
|---|---|
| `UseEnemyDamageCheck` | The enemy's strongest attack, after your armor and resistances, deals at most `MaxEnemyHitPercent` (default 10%) of your max health. |
| `UsePlayerDamageCheck` | Your equipped weapon kills the enemy in at most `MaxHitsToKill` (default 3) average hits. This includes your skill, active buffs and the enemy's resistances. |

The math uses the game's own formulas, including star level, world difficulty settings, player-count scaling and world level.
These are rough outcomes with the defaults, using 3★ gear and typical food:

| Your gear | Runs away | Still fights |
|---|---|---|
| Iron | Greydwarves, greydwarf brutes, skeletons | Trolls, draugr, wolves |
| Padded + Blackmetal | Everything above, plus draugr and wolves | Fenrings, goblins, trolls |
| Carapace + Mistwalker | Everything above, plus fenrings and goblins | Goblin brutes, seeker soldiers |

## Config

The config file is `BepInEx/config/com.lhoffl.TruePassiveMobs.cfg`. It reloads automatically when you save
it, so you can tune it while the game runs.

## Multiplayer

Creatures are simulated by whichever client owns them, so every player needs the mod. Each client
publishes its own player's armor, resistances and weapon damage, so enemies judge every player by
that player's real gear. Players without the mod are never feared. The config isn't synced, so
everyone should use the same settings.

## Building

```
dotnet build -c Release
```

The game path comes from the Steam registry keys. To override it, pass `-p:GamePath=...` or create
`Environment.props`; the top of `TruePassiveMobs.csproj` shows how. The build copies
`TruePassiveMobs.dll` to `BepInEx/plugins/TruePassiveMobs/`. Pass `-p:DeployToGame=false` to skip that step.