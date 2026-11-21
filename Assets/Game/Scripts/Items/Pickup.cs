using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Collected on touch: gives 'value' of 'resource' (gold / health / bombs / ropes or a custom counter id,
    /// see RunState.AddResource). A new pickup type is just a prefab with different values.
    /// </summary>
    public class Pickup : Entity
    {
        [Header("Pickup")]
        [Tooltip("The id of the thing this is (item card, list of things gained, quests): the same id as the shop offer where the thing is also sold - a bomb bag is 'bombs' on the ground and on the shelf. Empty = its name.")]
        public string itemId = "";
        /// <summary>The id this thing goes by everywhere (ItemLog, HUD.Gain, quests).</summary>
        public string ItemId => !string.IsNullOrEmpty(itemId) ? itemId : displayName;
        [Tooltip("Resource id: gold / health / bombs / ropes, or a custom counter id.")]
        public string resource = Res.Gold;
        public int value = 25;
        [Tooltip("Word shown after the amount when collected, e.g. 'BOMBS' -> '+3 BOMBS'. Empty = just the number.")]
        public string label = "";
        public string sfx = "coin";
        public Color textColor = new Color(1f, 0.85f, 0.3f);
        public float gravity = 30f;

        float age, fullSaidAt = -10f;

        public override void Tick(float dt)
        {
            age += dt;
            var b = StepPhysics(dt, gravity);
            if (b.landed && -b.impactVelY > 3f) { vel.y = -b.impactVelY * 0.3f; grounded = false; }
            if (b.hitLeft || b.hitRight) vel.x = -b.impactVelX * 0.4f;
            if (grounded) vel.x = Mathf.MoveTowards(vel.x, 0f, 12f * dt);
            if (pos.y < -4f) Despawn();
        }

        public void Collect(PlayerController p)
        {
            if (Dead || age < 0.15f) return;
            var run = RunSession.Current;
            if (run == null) return;
            if (resource == Res.Health && run.HealthFull) return;   // already at full health: the heart stays for later
            if (resource == "ember")
            {
                // not part of the run at all: it goes into the Ember Pouch, for good (see Villagers)
                int got = Villagers.AddEmbers(value);
                string say = got > 0 ? "+" + got + " EMBER" : Villagers.HasKey(Villagers.KeyPouch) ? "THE POUCH IS FULL" : "NOTHING TO CARRY IT IN";
                if (got > 0) Show(got); else Fx.Text(Center + Vector2.up * 0.4f, say, textColor, 1.2f);
                Fx.Sparks(Center, textColor, 8, 3.5f);
                Sfx.Play(sfx, pos);
                Despawn();
                return;
            }
            if (run.CapOf(resource) != int.MaxValue)
            {
                // bombs, ropes, pebbles: only so many can be carried. What fits is taken, the rest stays where it lies
                int room = run.RoomFor(resource);
                if (room <= 0)
                {
                    if (age - fullSaidAt > 2f) { fullSaidAt = age; Fx.Text(Center + Vector2.up * 0.4f, $"{(string.IsNullOrEmpty(label) ? resource.ToUpperInvariant() : label)} FULL ({run.CapOf(resource)})", textColor, 1f); }
                    return;
                }
                int take = Mathf.Min(value, room);
                run.AddResource(resource, take);
                Show(take);
                Fx.Sparks(Center, textColor, 4, 2.5f);
                Sfx.Play(sfx, pos);
                value -= take;
                if (value <= 0) Despawn();
                return;
            }
            run.AddResource(string.IsNullOrEmpty(resource) ? Res.Gold : resource, value);
            Show(value);
            Fx.Sparks(Center, textColor, 4, 2.5f);
            Sfx.Play(sfx, pos);
            Despawn();
        }

        /// <summary>Tells the screen what was picked up: a card the first time, a line at the left edge after that (HUD.Gain).</summary>
        void Show(int amount)
        {
            var dir = GameDirector.Instance;
            string res = string.IsNullOrEmpty(resource) ? Res.Gold : resource;
            if (dir == null || dir.hud == null) return;
            // gold is not an item: it never gets a card, only its line in the list at the left edge
if (res == Res.Gold) { dir.hud.Gain(ItemId, ItemLog.Title(displayName), sr != null ? sr.sprite : null, amount, null); return; }      // (each piece its own line: what that piece was worth)
            dir.hud.Gain(ItemId, ItemLog.Title(displayName), sr != null ? sr.sprite : null, amount, ItemLog.Describe(res, value));
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            Vector2 d = Center - center;
            d = d.sqrMagnitude < 0.0001f ? Vector2.up : d.normalized;
            vel = d * 7f + Vector2.up * 3f;
            grounded = false;
        }
    }
}
