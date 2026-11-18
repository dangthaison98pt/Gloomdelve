using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Sandsunk Tombs: a cursed mummy. Slow and hard to put down, and it does not want blood: its grab takes
    /// gold. A grab that lands shoves the player back and strips some of the gold they carry (it only draws blood
    /// from someone who has none left to take). Whatever it has taken falls out of its wrappings when it is
    /// destroyed - so the gold is not gone, only held by something that has to be killed.
    /// It does not have to catch the player on foot: from a few steps away it casts a length of its wrapping
    /// (a raised arm first: the warning). Whoever that catches is dragged towards it and tangled - into the reach
    /// of the grab. The blade cuts the wrapping out of the air; a jump clears it.
    /// And it is not empty: when it is destroyed, the scarabs nesting in its wrappings come out.
    /// A Warding Scarab (StatModifiers.goldWard) keeps its hands off the wearer's gold.
    /// </summary>
    public class Mummy : Enemy
    {
        public float speed = 0.9f;
        public float chaseSpeed = 1.5f;
        public float sightRange = 6f;
        [Header("Grab")]
        public float grabRange = 1.1f;
        public float grabWindup = 0.5f;
        public float grabTime = 0.22f;
        public float grabLunge = 4.5f;
        public float grabRecover = 1.2f;
        [Tooltip("Gold taken by one grab (less if the player carries less).")] public int stealGold = 150;
        [Header("Wrapping cast")]
        [Tooltip("It casts its wrapping when the player is between these distances (tiles, sideways).")] public float castMin = 1.8f;
        public float castMax = 6f;
        [Tooltip("Seconds the arm is raised first: the warning.")] public float castTell = 0.55f;
        public float castSpeed = 13f;
        public float castCooldown = 3.2f;
        public string wrapId = "mummywrap";
        [Header("Destroyed")]
        [Tooltip("Scarabs that come out of its wrappings when it is destroyed.")] public int broodOnDeath = 2;
        public SpriteAnim animWalk;
        public SpriteAnim animGrab;

        int stolen;
        bool grabbed, casting;
        float castT, castWait = 1.2f;

        /// <summary>Wrappings cast so far (tests).</summary>
        public int Casts { get; private set; }

        /// <summary>Gold it is holding: falls out when it is destroyed.</summary>
        public int Stolen => stolen;
        /// <summary>Its touch never hurts by itself: the grab is handled here.</summary>
        public override int ContactDamage => 0;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (facing == 0) facing = 1;
            anim?.Play(animWalk);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            var p = Player;
            if (stunTimer <= 0f && grounded)
            {
                bool sees = SeesPlayer(sightRange) && p != null && Mathf.Abs(p.pos.y - pos.y) < 1.8f;
                bool near = sees && Mathf.Abs(p.pos.x - pos.x) < grabRange && Mathf.Abs(p.pos.y - pos.y) < 1f;
                if (melee == MeleePhase.None && sees && Mathf.Abs(p.pos.x - pos.x) > 0.1f) facing = p.pos.x > pos.x ? 1 : -1;
                var before = melee;
                castWait -= dt;
                float dx = sees ? Mathf.Abs(p.pos.x - pos.x) : 99f;
                if (casting)
                {
                    // arm raised, then the wrapping flies
                    vel.x = 0f;
                    anim?.Play(animGrab != null ? animGrab : animWalk);
                    castT -= dt;
                    if (castT <= 0f)
                    {
                        casting = false;
                        castWait = castCooldown;
                        var wrap = World.SpawnById(wrapId, new Vector2(pos.x + facing * 0.5f, pos.y + size.y * 0.5f)) as MummyWrap;
                        if (wrap != null) { wrap.vel = new Vector2(facing * castSpeed, 0f); wrap.owner = this; }
                        Casts++;
                        Sfx.Play("rope_throw", pos, 0.9f, 0.7f);
                    }
                }
                else if (melee == MeleePhase.None && !near && sees && castWait <= 0f && dx >= castMin && dx <= castMax && Mathf.Abs(p.pos.y - pos.y) < 0.9f)
                {
                    casting = true; castT = castTell;
                    vel.x = 0f;
                    Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.35f), "!", new Color(1f, 0.85f, 0.3f), 0.5f);
                    Sfx.Play("toad", pos, 0.7f, 0.5f);
                }
                else if (Melee(dt, near, grabWindup, grabTime, grabRecover))
                {
                    if (melee == MeleePhase.Strike && before != MeleePhase.Strike) grabbed = false;
                    vel.x = melee == MeleePhase.Strike ? (BlockedAhead(facing) ? 0f : facing * grabLunge) : 0f;
                    anim?.Play(animGrab != null ? animGrab : animWalk);
                    if (melee == MeleePhase.Strike && !grabbed && p != null && !p.IsDead && Overlaps(p)) Grab(p);
                }
                else
                {
                    anim?.Play(animWalk);
                    vel.x = sees ? (BlockedAhead(facing) ? 0f : facing * chaseSpeed) : PatrolStep() * speed;
                }
            }
            else if (grounded) { vel.x = Mathf.MoveTowards(vel.x, 0f, 20f * dt); casting = false; }
            var b = StepPhysics(dt, gravity);
            CheckSpikes(b);
            if (anim != null) anim.speed = stunTimer > 0f || (grounded && Mathf.Abs(vel.x) < 0.01f && melee == MeleePhase.None) ? 0f : 1f;
        }

        /// <summary>The grab landed: gold changes hands (or, with none to take, it hurts).</summary>
        public void Grab(PlayerController p)
        {
            grabbed = true;
            if (p.godMode) return;
            var run = RunSession.Current;
            var away = new Vector2((p.pos.x >= pos.x ? 1 : -1) * 6f, 6f);
            bool warded = run != null && run.stats.goldWard > 0f;
            int take = run != null && !warded ? Mathf.Min(run.gold, stealGold) : 0;
            if (take > 0)
            {
                run.gold -= take;
                stolen += take;
                p.Shove(away, 0.25f);
                Fx.Text(p.Center + Vector2.up * 0.9f, "-$" + take, new Color(1f, 0.8f, 0.3f), 1.1f);
                Fx.Debris(p.Center, new Color(1f, 0.85f, 0.35f), 8);
                Sfx.Play("coin", pos, 1f, 0.6f);
            }
            else if (warded)
            {
                p.Shove(away, 0.2f);
                Fx.Text(p.Center + Vector2.up * 0.9f, "WARDED", new Color(0.6f, 1f, 0.9f), 0.8f);
            }
            else p.TakeDamage(new DamageInfo(1, DamageType.Contact, this, away, "a mummy's grip"));
        }

        protected override void Die(in DamageInfo d)
        {
            // what it took falls out of the wrappings (in chunks of 30, as the gold on the ground is counted)
            var w = World;
            Vector2 at = Center;
            int chunks = Mathf.Min(12, stolen / 30);
            for (int i = 0; i < chunks && w != null; i++)
            {
                var e = w.SpawnById("gold_big", at);
                if (e != null) e.vel = new Vector2((i - (chunks - 1) * 0.5f) * 1.5f, 5f + i % 2);
            }
            stolen = 0;
            // and the scarabs that nested in it come out
            if (w != null && broodOnDeath > 0)
            {
                Scarab.Single = true;
                try
                {
                    for (int i = 0; i < broodOnDeath; i++)
                    {
                        var e = w.SpawnById("scarab", new Vector2(pos.x + (i % 2 == 0 ? -0.3f : 0.3f), pos.y));
                        if (e == null) continue;
                        e.vel = new Vector2((i % 2 == 0 ? -1 : 1) * 2.5f, 5f);
                        e.grounded = false;
                        if (e is Enemy en) en.summoned = true;        // no gold for them: the mummy was the prize
                    }
                }
                finally { Scarab.Single = false; }
            }
            base.Die(d);
        }
    }
}
