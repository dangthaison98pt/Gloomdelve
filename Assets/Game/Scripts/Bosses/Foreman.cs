using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Gearworks boss: the Foreman, a huge wind-up overseer behind a shield, on a floor of conveyor belts.
    /// Its rule: nothing gets through the shield from the front. It walks at the player and jabs up close, and it
    /// turns slowly - so the fight is about getting behind it (the belts help or hinder), stomping it, or waiting
    /// for the moment after its presses, when it has run down and must wind its own key: then it is wide open.
    /// Its moves come in random order (never the same twice running), none shared with the other bosses:
    ///  - Saws: it sends two loose saw blades rolling along the floor at the player (jump them; a belt under
    ///    them speeds them up or slows them down).
    ///  - Lever: every conveyor in the room runs the other way.
    ///  - Presses: a row of stamping presses comes down from the ceiling one after another, away from it and
    ///    across the player's side of the room - stand between two of them.
    ///  - Rewind: run down, it stops and winds its key. No shield for that long.
    /// Stomped too often, a blast of steam throws the player off and flips the belts.
    /// </summary>
    public class Foreman : Boss
    {
        public float walkSpeed = 1.3f;
        [Tooltip("Seconds it needs to turn round to a player behind it.")] public float turnDelay = 0.9f;
        public float actionGap = 2.4f;
        public float tellTime = 0.55f;
        [Header("Jab")]
        public float jabRange = 1.9f;
        public float jabWindup = 0.5f;
        public float jabTime = 0.2f;
        public float jabLunge = 6f;
        public float jabRecover = 1.0f;
        [Header("Saws")]
        public string sawId = "sawblade";
        public int saws = 2;
        public float sawGap = 0.6f;
        [Header("Presses")]
        public string pressId = "press";
        public int presses = 5;
        public float pressSpacing = 2.3f;
        public float pressInterval = 0.28f;
        [Tooltip("How long it stands winding its key after the presses: the opening.")] public float rewindTime = 2.2f;
        public SpriteAnim animWalk, animCharge, animDazed;

        static readonly Color Steam = new Color(0.95f, 0.9f, 0.8f);
        enum State { Walk, SawTell, LeverTell, PressTell, Rewind }
        State state;
        float timer, turnT, sawTimer;
        int step, sawsLeft, sawDir;

        public override bool Attacking => melee == MeleePhase.Strike;
        /// <summary>Run down and winding its key: the shield is off.</summary>
        public bool Rewinding => state == State.Rewind;

        public override void OnSpawn()
        {
            base.OnSpawn();
            state = State.Walk; timer = 1.8f;
            anim?.Play(animWalk);
        }

        /// <summary>Every conveyor in the room runs the other way.</summary>
        public static int ReverseBelts(World w)
        {
            int n = 0;
            foreach (var e in w.Entities) if (!e.Dead && e is Conveyor c) { c.Reverse(); n++; }
            return n;
        }

        float FloorY()
        {
            var g = World.Grid;
            int y = Mathf.FloorToInt(pos.y + 0.5f);
            while (y > 0 && !g.IsSolid(Mathf.FloorToInt(pos.x), y - 1)) y--;
            return y;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (HoldCounterPose(dt)) return;      // busy answering a stomp
            var p = Target;
            timer -= dt;
            // loose saws already promised keep coming whatever it does next
            if (sawsLeft > 0)
            {
                sawTimer -= dt;
                if (sawTimer <= 0f) { ThrowSaw(); sawsLeft--; sawTimer = sawGap; }
            }
            switch (state)
            {
                case State.Walk:
                {
                    float dx = p != null ? p.pos.x - pos.x : 0f;
                    bool behind = p != null && Mathf.Abs(dx) > 0.3f && (dx > 0 ? 1 : -1) != facing;
                    if (behind && melee == MeleePhase.None)
                    {
                        turnT += dt;
                        if (turnT >= turnDelay) { facing = -facing; turnT = 0f; }
                    }
                    else turnT = 0f;
                    bool near = p != null && !behind && Mathf.Abs(dx) < jabRange && Mathf.Abs(p.pos.y - pos.y) < 1.6f;
                    if (Melee(dt, near, jabWindup, jabTime, jabRecover))
                    {
                        vel.x = melee == MeleePhase.Strike ? (WallAhead(facing) ? 0f : facing * jabLunge) : 0f;
                        anim?.Play(animCharge);
                    }
                    else
                    {
                        vel.x = behind || p == null || Mathf.Abs(dx) < jabRange * 0.8f || WallAhead(facing) ? 0f : facing * walkSpeed;
                        anim?.Play(animWalk);
                        if (timer <= 0f && p != null && grounded && melee == MeleePhase.None)
                        {
                            // its moves, in random order: saws, lever, presses (after which it must rewind)
                            switch (NextMove(0, 1, 2))
                            {
                                case 0: state = State.SawTell; timer = tellTime; Tell(); anim?.Play(animCharge); Sfx.Play("brute", pos, 1f, 1.3f); break;
                                case 1: state = State.LeverTell; timer = tellTime; Tell(); Sfx.Play("warning", pos, 0.8f, 0.7f); break;
                                default: state = State.PressTell; timer = tellTime; Tell(); Sfx.Play("brute", pos, 1f, 0.6f); break;
                            }
                        }
                    }
                    break;
                }
                case State.SawTell:
                    vel.x = 0f;
                    if (timer <= 0f)
                    {
                        sawDir = p != null && p.pos.x < pos.x ? -1 : 1;
                        sawsLeft = saws; sawTimer = 0f;
                        state = State.Walk; timer = Rest(actionGap);
                    }
                    break;
                case State.LeverTell:
                    vel.x = 0f;
                    if (timer <= 0f)
                    {
                        if (ReverseBelts(World) > 0) Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.9f), "REVERSE!", Steam, 1f);
                        Fx.Shake(0.3f);
                        Sfx.Play("crack", pos, 1f, 0.6f);
                        state = State.Walk; timer = Rest(actionGap * 0.7f);
                    }
                    break;
                case State.PressTell:
                    vel.x = 0f;
                    if (timer <= 0f)
                    {
                        StampRow(p);
                        state = State.Rewind; timer = rewindTime;
                        anim?.Play(animDazed);
                        Fx.Text(new Vector2(Center.x, pos.y + size.y + 0.9f), "...", Steam, 1.2f);
                    }
                    break;
                case State.Rewind:
                    vel.x = 0f;
                    anim?.Play(animDazed);
                    if (timer <= 0f) { state = State.Walk; timer = Rest(actionGap * 0.6f); }
                    break;
            }
            StepPhysics(dt, gravity);
        }

        void ThrowSaw()
        {
            var w = World;
            float fy = FloorY();
            float x = pos.x + sawDir * (size.x * 0.5f + 0.6f);
            if (w.Grid.IsSolid(Mathf.FloorToInt(x), Mathf.FloorToInt(fy + 0.2f))) return;
            var saw = w.SpawnById(sawId, new Vector2(x, fy)) as SawBlade;
            if (saw == null) return;
            saw.Launch(sawDir);
            Sfx.Play("throw", pos, 1f, 0.7f);
        }

        /// <summary>A row of presses, starting next to it and marching away across the player's side of the room.</summary>
        void StampRow(PlayerController p)
        {
            var w = World;
            var g = w.Grid;
            int side = p != null && p.pos.x < pos.x ? -1 : 1;
            float fy = FloorY();
            for (int i = 0; i < presses; i++)
            {
                float x = Mathf.Floor(pos.x + side * (size.x * 0.5f + 1.2f + i * pressSpacing)) + 0.5f;
                int cx = Mathf.FloorToInt(x);
                if (cx < 1 || cx > g.Width - 2 || g.IsSolid(cx, Mathf.FloorToInt(fy + 0.2f))) break;
                // the ceiling of this column
                int cy = Mathf.FloorToInt(fy) + 1;
                while (cy < g.Height - 1 && !g.IsSolid(cx, cy)) cy++;
                var pr = w.SpawnById(pressId, new Vector2(x, cy - 0.95f)) as Press;
                if (pr != null) pr.Arm(fy, i * pressInterval);
            }
        }

        /// <summary>The shield: hits from the side it faces do nothing, except while it rewinds.</summary>
        public bool Blocks(in DamageInfo d)
        {
            if (state == State.Rewind || Countering) return false;
            if (d.type == DamageType.Explosion || d.type == DamageType.Stomp || d.type == DamageType.Spikes || d.type == DamageType.Crush) return false;
            if (d.source == null) return false;
            return (d.source.Center.x - pos.x) * facing > 0.05f;
        }

        public override bool TakeDamage(in DamageInfo d)
        {
            if (!Dead && Blocks(d))
            {
                Fx.Sparks(new Vector2(pos.x + facing * size.x * 0.5f, pos.y + size.y * 0.55f), new Color(1f, 0.9f, 0.6f), 8, 5f);
                Sfx.Play("crack", pos, 0.8f, 1.5f);
                return false;
            }
            return base.TakeDamage(d);
        }

        /// <summary>Stomped again: a blast of steam off its back throws the player high, and the belts flip.</summary>
        protected override void OnGreedyStomp(PlayerController p, int away)
        {
            p.Shove(new Vector2(away * 6f, 13f), 0.2f);
            ReverseBelts(World);
            Fx.Debris(new Vector2(pos.x, pos.y + size.y), Steam, 12);
            Fx.Shake(0.35f);
            Sfx.Play("crack", pos, 1f, 0.7f);
        }
    }
}
