using System;
using UnityEngine;

namespace Gloomdelve
{
    [Serializable]
    public class MotorStats
    {
        [Header("Hitbox (tiles)")]
        public float width = 0.6f;
        public float height = 0.85f;

        [Header("Horizontal")]
        [Tooltip("Default movement speed (auto-run).")] public float runSpeed = 6.4f;
        [Tooltip("Speed while holding the Walk key (careful steps).")] public float walkSpeed = 3.0f;
        public float crawlSpeed = 1.8f;
        public float groundAccel = 55f;
        public float groundDecel = 62f;
        public float airAccel = 34f;
        public float airDecel = 12f;

        [Header("Vertical")]
        public float gravity = 52f;
        public float fallGravityMultiplier = 1.15f;
        public float maxFallSpeed = 19f;
        [Tooltip("Max jump height = v^2 / (2*gravity). 15.8 -> ~2.4 tiles.")] public float jumpVelocity = 15.8f;
        [Tooltip("Velocity multiplier when jump is released early (variable jump height).")] public float jumpCutMultiplier = 0.45f;
        [Tooltip("Coyote time: can still jump this long after walking off a ledge.")] public float coyoteTime = 0.08f;
        [Tooltip("Jump buffer: a press this early before landing still jumps.")] public float jumpBufferTime = 0.12f;

        [Header("Climb / ledge")]
        public float climbSpeed = 4.2f;
        [Tooltip("How far below the ledge top the head may be and still grab.")] public float ledgeGrabReach = 0.38f;
        public float pullUpTime = 0.22f;
        [Tooltip("Horizontal speed fraction when jumping off a ladder/rope.")] public float ladderJumpSideFactor = 0.6f;

        [Header("Glider (gear)")]
        [Tooltip("Max fall speed while gliding (hold JUMP while falling with the glider equipped).")] public float glideFallSpeed = 1.9f;
        [Tooltip("Horizontal acceleration while gliding.")] public float glideAirAccel = 20f;
    }

    public enum MotorMode : byte { Normal, Climb, Hang, PullUp }

    public struct MotorInput
    {
        public int x;
        public bool up, down, jumpPressed, jumpHeld, walk;
        public bool stunned, noGrab;
        /// <summary>Glider equipped: holding jump while falling glides.</summary>
        public bool glide;
        public float speedMult, jumpMult;
    }

    public struct MotorState
    {
        public Vector2 pos, vel;
        public MotorMode mode;
        public int facing;
        public bool grounded, crouching, jumping;
        public float coyote, jumpBuffer, grabCooldown, climbCooldown, dropThrough, pullTimer;
        public Vector2 pullFrom, pullTo;
        public int hangDir, hangX, hangY, climbX;
        public float apexY;

        // Per-step events
        public bool landed, jumpedThisStep, grabbedThisStep, hitSpikes, hitWall;
        public bool gliding;
        public float landImpact, fallDistance;

        public static MotorState At(Vector2 p) =>
            new MotorState { pos = p, facing = 1, grounded = true, apexY = p.y, mode = MotorMode.Normal };
    }

    /// <summary>
    /// Pure, deterministic platformer movement. Used by PlayerController in-game AND by LevelValidator,
    /// so reachability checks use the exact same jump/climb/collision rules as the real character.
    /// </summary>
    public static class PlayerMotor
    {
        public static void Step(ref MotorState s, in MotorInput i, TileGrid g, MotorStats st, float dt)
        {
            s.landed = s.jumpedThisStep = s.grabbedThisStep = s.hitSpikes = s.hitWall = false;
            s.landImpact = 0f; s.fallDistance = 0f;

            s.jumpBuffer = Mathf.Max(0f, s.jumpBuffer - dt);
            if (i.jumpPressed) s.jumpBuffer = st.jumpBufferTime;
            s.coyote = Mathf.Max(0f, s.coyote - dt);
            s.grabCooldown = Mathf.Max(0f, s.grabCooldown - dt);
            s.climbCooldown = Mathf.Max(0f, s.climbCooldown - dt);
            s.dropThrough = Mathf.Max(0f, s.dropThrough - dt);

            if (s.mode != MotorMode.PullUp && BodyPhysics.Unstick(g, ref s.pos, st.width * 0.5f, st.height))
            {
                // Was inside a solid tile (e.g. a tile appeared on top of the player): pop out and fall.
                s.mode = MotorMode.Normal;
                s.vel = Vector2.zero;
                s.grounded = false;
                s.apexY = s.pos.y;
            }

            switch (s.mode)
            {
                case MotorMode.Normal: StepNormal(ref s, i, g, st, dt); break;
                case MotorMode.Climb: StepClimb(ref s, i, g, st, dt); break;
                case MotorMode.Hang: StepHang(ref s, i, g, st); break;
                case MotorMode.PullUp: StepPullUp(ref s, st, dt); break;
            }
        }

        static float JumpMult(in MotorInput i) => i.jumpMult > 0f ? i.jumpMult : 1f;

        static void StepNormal(ref MotorState s, in MotorInput i, TileGrid g, MotorStats st, float dt)
        {
            float hw = st.width * 0.5f, h = st.height;
            bool stunned = i.stunned;
            int ix = stunned ? 0 : Math.Sign(i.x);

            // --- Grab ladder / rope ---
            if (!stunned && s.climbCooldown <= 0f)
            {
                int cx = Mathf.FloorToInt(s.pos.x);
                float off = Mathf.Abs(s.pos.x - (cx + 0.5f));
                if (i.up && off < 0.45f && g.IsClimbable(cx, Mathf.FloorToInt(s.pos.y + 0.3f)))
                {
                    if (EnterClimb(ref s, g, st, cx)) return;
                }
                else if (i.down && s.grounded && off < 0.4f)
                {
                    int below = Mathf.FloorToInt(s.pos.y - 0.05f);
                    if (g.IsLadder(cx, below) && EnterClimb(ref s, g, st, cx))
                    {
                        s.pos.y = below + 0.69f;
                        return;
                    }
                }
            }

            bool wasGrounded = s.grounded;
            s.crouching = s.grounded && i.down && !stunned;

            // --- Horizontal ---
            float sm = i.speedMult > 0f ? i.speedMult : 1f;
            float maxSpeed = (s.crouching ? st.crawlSpeed : (i.walk ? st.walkSpeed : st.runSpeed)) * sm;
            float target = ix * maxSpeed;
            float accel;
            if (s.grounded)
            {
                if (stunned) accel = st.groundDecel * 0.5f;
                else if (ix != 0) accel = (Mathf.Abs(s.vel.x) > 0.01f && Mathf.Sign(target) != Mathf.Sign(s.vel.x)) ? st.groundAccel * 2f : st.groundAccel;
                else accel = st.groundDecel;
            }
            else accel = stunned ? st.airDecel * 0.3f : (ix != 0 ? st.airAccel : st.airDecel);
            s.vel.x = Mathf.MoveTowards(s.vel.x, target, accel * dt);
            if (ix != 0) s.facing = ix;

            // Crawling never walks off an edge: it lowers you into a ledge hang instead.
            if (s.crouching && ix != 0)
            {
                float nx = s.pos.x + s.vel.x * dt;
                if (!BodyPhysics.HasSupport(g, nx, s.pos.y, hw))
                {
                    if (TryEdgeHang(ref s, g, st, ix)) return;
                    s.vel.x = 0f;
                }
            }

            // --- Jump / drop-through ---
            if (s.jumpBuffer > 0f && !stunned && (s.grounded || s.coyote > 0f))
            {
                if (i.down && s.grounded && StandingOnPlatformOnly(g, s.pos, hw))
                {
                    s.dropThrough = 0.2f;
                    s.jumpBuffer = 0f;
                    s.grounded = false;
                    s.crouching = false;
                }
                else
                {
                    s.vel.y = st.jumpVelocity * JumpMult(i);
                    s.jumping = true;
                    s.grounded = false;
                    s.coyote = 0f;
                    s.jumpBuffer = 0f;
                    s.jumpedThisStep = true;
                    s.crouching = false;
                }
            }
            if (s.jumping && !i.jumpHeld && s.vel.y > 0f) { s.vel.y *= st.jumpCutMultiplier; s.jumping = false; }
            if (s.vel.y <= 0f) s.jumping = false;

            float grav = st.gravity * (s.vel.y < 0f ? st.fallGravityMultiplier : 1f);
            s.vel.y = Mathf.Max(s.vel.y - grav * dt, -st.maxFallSpeed);
            // Glider: holding jump while falling caps the fall speed (a long glide never counts as a fall).
            s.gliding = i.glide && !stunned && !s.grounded && i.jumpHeld && s.vel.y < 0f;
            if (s.gliding)
            {
                s.vel.y = Mathf.Max(s.vel.y, -st.glideFallSpeed);
                if (ix != 0) s.vel.x = Mathf.MoveTowards(s.vel.x, target, st.glideAirAccel * dt);
            }

            var b = new Body { pos = s.pos, vel = s.vel, hw = hw, h = h, grounded = s.grounded };
            BodyPhysics.Move(g, ref b, dt, s.dropThrough > 0f);
            s.pos = b.pos; s.vel = b.vel; s.grounded = b.grounded;
            s.hitWall = b.hitLeft || b.hitRight;
            if (BodyPhysics.SpikeHit(g, s.pos, hw, b.preVelY)) s.hitSpikes = true;

            if (s.grounded)
            {
                if (!wasGrounded)
                {
                    s.landed = true;
                    s.landImpact = b.impactVelY;
                    s.fallDistance = s.apexY - s.pos.y;
                }
                s.apexY = s.pos.y;
                s.coyote = 0f;
            }
            else
            {
                if (wasGrounded)
                {
                    s.apexY = s.pos.y;
                    if (!s.jumpedThisStep && s.dropThrough <= 0f) s.coyote = st.coyoteTime;
                }
                else s.apexY = Mathf.Max(s.apexY, s.pos.y);
                if (s.gliding) s.apexY = s.pos.y;
                s.crouching = false;

                if (s.vel.y <= 0f && !i.down && !i.noGrab && !stunned && s.grabCooldown <= 0f)
                    TryLedgeGrab(ref s, g, st);
            }
        }

        static bool StandingOnPlatformOnly(TileGrid g, Vector2 pos, float hw)
        {
            int row = Mathf.FloorToInt(pos.y - 0.02f);
            int x0 = Mathf.FloorToInt(pos.x - hw + BodyPhysics.Eps), x1 = Mathf.FloorToInt(pos.x + hw - BodyPhysics.Eps);
            bool anyPlatform = false;
            for (int x = x0; x <= x1; x++)
            {
                if (g.IsSolid(x, row)) return false;
                if (g.IsPlatformTop(x, row)) anyPlatform = true;
            }
            return anyPlatform;
        }

        static bool EnterClimb(ref MotorState s, TileGrid g, MotorStats st, int cx)
        {
            var p = new Vector2(cx + 0.5f, s.pos.y);
            if (BodyPhysics.OverlapsSolid(g, p, st.width * 0.5f, st.height)) return false;
            s.mode = MotorMode.Climb;
            s.climbX = cx;
            s.pos = p;
            s.vel = Vector2.zero;
            s.jumping = false; s.grounded = false; s.crouching = false;
            s.apexY = s.pos.y;
            return true;
        }

        static bool TryEdgeHang(ref MotorState s, TileGrid g, MotorStats st, int dir)
        {
            float hw = st.width * 0.5f, h = st.height;
            int row = Mathf.FloorToInt(s.pos.y - 0.02f);
            int x0 = Mathf.FloorToInt(s.pos.x - hw + BodyPhysics.Eps), x1 = Mathf.FloorToInt(s.pos.x + hw - BodyPhysics.Eps);
            int c = int.MinValue;
            if (dir > 0) { for (int x = x1; x >= x0; x--) if (g.IsSolid(x, row)) { c = x; break; } }
            else { for (int x = x0; x <= x1; x++) if (g.IsSolid(x, row)) { c = x; break; } }
            if (c == int.MinValue) return false;
            int side = dir > 0 ? c + 1 : c - 1;
            if (g.IsSolid(side, row) || g.IsSolid(side, row + 1)) return false;
            float top = row + 1;
            var p = new Vector2(dir > 0 ? c + 1 + hw + 0.001f : c - hw - 0.001f, top + 0.1f - h);
            if (BodyPhysics.OverlapsSolid(g, p, hw, h)) return false;
            s.mode = MotorMode.Hang;
            s.hangDir = -dir; s.hangX = c; s.hangY = row;
            s.pos = p; s.vel = Vector2.zero;
            s.facing = -dir;
            s.grounded = false; s.crouching = false; s.jumping = false;
            s.grabbedThisStep = true;
            s.apexY = s.pos.y;
            return true;
        }

        static bool TryLedgeGrab(ref MotorState s, TileGrid g, MotorStats st)
        {
            float hw = st.width * 0.5f, h = st.height;
            int dir = s.facing;
            float face = s.pos.x + dir * hw;
            int wx = Mathf.FloorToInt(face + dir * 0.05f);
            float surf = dir > 0 ? wx : wx + 1;
            if (Mathf.Abs(surf - face) > 0.06f) return false;
            float head = s.pos.y + h;
            int top = Mathf.CeilToInt(head - 0.15f);
            if (top - head > st.ledgeGrabReach) return false;
            if (!g.IsSolid(wx, top - 1) || g.IsSolid(wx, top)) return false;
            int px0 = Mathf.FloorToInt(s.pos.x - hw + BodyPhysics.Eps), px1 = Mathf.FloorToInt(s.pos.x + hw - BodyPhysics.Eps);
            for (int x = px0; x <= px1; x++) if (g.IsSolid(x, top)) return false;
            var hp = new Vector2(s.pos.x, top + 0.1f - h);
            if (BodyPhysics.OverlapsSolid(g, hp, hw, h)) return false;
            s.mode = MotorMode.Hang;
            s.hangDir = dir; s.hangX = wx; s.hangY = top - 1;
            s.pos = hp; s.vel = Vector2.zero;
            s.jumping = false; s.grounded = false;
            s.grabbedThisStep = true;
            return true;
        }

        static void StepHang(ref MotorState s, in MotorInput i, TileGrid g, MotorStats st)
        {
            if (i.stunned || !g.IsSolid(s.hangX, s.hangY))
            {
                s.mode = MotorMode.Normal; s.grabCooldown = 0.3f; s.apexY = s.pos.y;
                return;
            }
            s.vel = Vector2.zero;
            s.facing = s.hangDir;
            if (s.jumpBuffer > 0f)
            {
                s.jumpBuffer = 0f;
                s.mode = MotorMode.Normal;
                s.apexY = s.pos.y;
                if (i.down) { s.grabCooldown = 0.3f; return; }
                s.vel.y = st.jumpVelocity * JumpMult(i);
                s.jumping = true;
                s.jumpedThisStep = true;
                s.grabCooldown = 0.2f;
                return;
            }
            if (i.down)
            {
                s.mode = MotorMode.Normal; s.grabCooldown = 0.3f; s.apexY = s.pos.y;
                return;
            }
            if (i.up)
            {
                float hw = st.width * 0.5f;
                float tx = s.hangDir > 0 ? s.hangX + hw + 0.05f : s.hangX + 1 - hw - 0.05f;
                var to = new Vector2(tx, s.hangY + 1);
                if (!BodyPhysics.OverlapsSolid(g, to, hw, st.height))
                {
                    s.mode = MotorMode.PullUp;
                    s.pullFrom = s.pos; s.pullTo = to; s.pullTimer = 0f;
                }
            }
        }

        static void StepPullUp(ref MotorState s, MotorStats st, float dt)
        {
            s.pullTimer += dt;
            float t = Mathf.Clamp01(s.pullTimer / Mathf.Max(0.01f, st.pullUpTime));
            var mid = new Vector2(s.pullFrom.x, s.pullTo.y);
            s.pos = t < 0.6f ? Vector2.Lerp(s.pullFrom, mid, t / 0.6f) : Vector2.Lerp(mid, s.pullTo, (t - 0.6f) / 0.4f);
            s.vel = Vector2.zero;
            if (t >= 1f)
            {
                s.pos = s.pullTo;
                s.mode = MotorMode.Normal;
                s.grounded = true;
                s.apexY = s.pos.y;
            }
        }

        static void StepClimb(ref MotorState s, in MotorInput i, TileGrid g, MotorStats st, float dt)
        {
            float hw = st.width * 0.5f, h = st.height;
            if (i.stunned || !g.IsClimbable(s.climbX, Mathf.FloorToInt(s.pos.y + 0.3f)))
            {
                s.mode = MotorMode.Normal; s.climbCooldown = 0.3f; s.apexY = s.pos.y;
                return;
            }
            s.pos.x = s.climbX + 0.5f;
            s.vel.x = 0f;
            if (s.jumpBuffer > 0f)
            {
                s.jumpBuffer = 0f;
                s.mode = MotorMode.Normal;
                s.climbCooldown = 0.25f;
                s.apexY = s.pos.y;
                if (!i.down)
                {
                    s.vel.y = st.jumpVelocity * JumpMult(i);
                    s.jumping = true;
                    s.jumpedThisStep = true;
                }
                int ix = Math.Sign(i.x);
                s.vel.x = ix * st.runSpeed * st.ladderJumpSideFactor;
                if (ix != 0) s.facing = ix;
                return;
            }

            float vy = i.up ? st.climbSpeed : (i.down ? -st.climbSpeed : 0f);
            s.vel.y = vy;
            if (vy == 0f) return;

            var b = new Body { pos = s.pos, vel = new Vector2(0f, vy), hw = hw, h = h };
            BodyPhysics.Move(g, ref b, dt, true);
            s.pos = b.pos;
            int probe = Mathf.FloorToInt(s.pos.y + 0.3f);
            if (vy > 0f)
            {
                if (b.hitCeiling) s.vel.y = 0f;
                if (!g.IsClimbable(s.climbX, probe))
                {
                    int below = probe - 1;
                    if (g.IsLadder(s.climbX, below))
                    {
                        var top = new Vector2(s.pos.x, below + 1);
                        if (!BodyPhysics.OverlapsSolid(g, top, hw, h))
                        {
                            s.pos = top; s.mode = MotorMode.Normal; s.grounded = true; s.vel = Vector2.zero; s.apexY = top.y;
                            return;
                        }
                    }
                    s.pos.y = probe - 0.3f - 0.001f;
                    s.vel.y = 0f;
                }
            }
            else
            {
                if (b.grounded)
                {
                    s.mode = MotorMode.Normal; s.grounded = true; s.vel = Vector2.zero; s.apexY = s.pos.y; s.climbCooldown = 0.15f;
                    return;
                }
                if (!g.IsClimbable(s.climbX, probe))
                {
                    s.mode = MotorMode.Normal; s.vel.y = 0f; s.apexY = s.pos.y; s.climbCooldown = 0.25f;
                }
            }
        }
    }
}
