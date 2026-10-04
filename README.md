# EffectsHUD

A [Vintage Story](https://www.vintagestory.at/) mod that adds a status-effect system
(buffs and debuffs) for players and an on-screen HUD that shows active effects
with their tier and remaining time.

**Download:** [mods.vintagestory.at/effectshud](https://mods.vintagestory.at/effectshud)

## Features

- 17 built-in effects, both positive and negative, with tiers
- A HUD with effect icons, tier badges and timers that adjusts its position
  around the minimap and the coordinates HUD
- Effects are kept across sessions and synced from server to client
- An invisibility effect that hides the player model from other clients
- An in-game GUI and a chat command for admins to apply effects
- A public API so other mods can register their own effects

## Built-in effects

| Effect | ID | Type |
|---|---|---|
| Regeneration | `regeneration` | Buff |
| Mining acceleration | `miningspeed` | Buff |
| Mining slow | `miningslow` | Debuff |
| Walk acceleration | `walkspeed` | Buff |
| Walk slow | `walkslow` | Debuff |
| Melee strength | `strengthmelee` | Buff |
| Melee weakness | `weakmelee` | Debuff |
| Bleeding | `bleeding` | Debuff |
| Thorns | `thorns` | Buff |
| Safe fall | `safefall` | Buff |
| Fire immunity | `firedamageimmune` | Buff |
| Invisibility | `invisibility` | Buff |
| Underwater breath | `extendedmaxbreath` | Buff |
| Weight bonus | `canweightbuff` | Buff |
| Temporal charge | `cantemporalcharge` | Buff |
| Temporal stability restore | `temporalstabilityrestore` | Buff (hidden) |
| Class forgetting | `forgetting` | Buff (hidden) |

## Controls

| Key | Action |
|---|---|
| `L` | Toggle the effects HUD |
| `Ctrl + L` | Open the effect selection GUI (admin) |

## Commands

```
/ef <effectId> <minutes> <tier> <playerName>
```

Requires the `controlserver` privilege and Creative mode.

## Configuration

`ModConfig/effectshud.json`:

| Key | Default | Description |
|---|---|---|
| `HUD_ALIGNMENT` | `11` | HUD anchor on screen |
| `TICK_EVERY_SECONDS` | `1` | Effect tick interval (seconds) |
| `EFFECT_ICON_SIZE` | `32` | Icon size in pixels |

## API for other mods

Create a subclass of `Effect` and register it on both sides:

```csharp
public class MyEffect : Effect
{
    public MyEffect() { effectTypeId = "myeffect"; }
    public override void OnTick() { /* called every tick */ }
}

// server
effectshud.RegisterEntityEffect("myeffect", typeof(MyEffect));
// client
effectshud.RegisterClientEffectData("myeffect", positive: true);

// apply
var ef = new MyEffect { Tier = 2 };
ef.SetExpiryInRealMinutes(5);
effectshud.ApplyEffectOnEntity(player.Entity, ef);
```

Lifecycle hooks: `OnStart`, `OnTick`, `OnStack`, `OnExpire`, `OnDeath`, `OnRevive`,
`OnShouldEntityReceiveDamage`, `DidAttack`.

Author: [KenigVovan](https://github.com/KenigVovan).
1.21 update contributed by [satyrnidae](https://github.com/satyrnidae).
