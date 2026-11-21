using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Gloomdelve
{
    public class ValidationResult
    {
        public bool ok;
        public bool exitReachable;
        public bool shopReachable = true;
        public bool goalReachable = true;
        /// <summary>True when a gear gate could be bypassed without the required gear (rejected).</summary>
        public bool bypassed;
        public int nodes;
        public int simulations;
        public double ms;
        public string reason = "";
    }

    /// <summary>
    /// Proves a level can be finished with the basic move set (no bombs, no ropes) by running the REAL PlayerMotor
    /// over a library of input patterns (walks, jumps of several heights and run-ups, ledge hangs, ladder climbs,
    /// platform drops) and searching the graph of resting states. Spikes and falls that would hurt are forbidden.
    /// The recorded inputs can be replayed by the bot in the actual game to confirm the result.
    /// </summary>
    public static class LevelValidator
    {
        enum K : byte { Walk, WalkRelease, Jump, CrawlHang, Drop, ClimbUp, ClimbDown, ClimbJump, HangUp, HangJump, HangDrop, WalkGlide }

        struct Pattern
        {
            public K kind; public int dir; public int hold; public int runUp; public int delay; public bool walk;
            public Pattern(K k, int dir = 0, int hold = 0, int runUp = 0, int delay = 0, bool walk = false)
            { kind = k; this.dir = dir; this.hold = hold; this.runUp = runUp; this.delay = delay; this.walk = walk; }
        }

        struct Node { public MotorState state; public int parent; public int pattern; }

        static readonly Pattern[] groundPatterns, climbPatterns, hangPatterns, glidePatterns;
        const int MaxFrames = 260;

        static LevelValidator()
        {
            var g = new List<Pattern>();
            foreach (int d in new[] { -1, 1 })
            {
                g.Add(new Pattern(K.Walk, d));
                g.Add(new Pattern(K.WalkRelease, d));
                g.Add(new Pattern(K.Walk, d, walk: true));
                g.Add(new Pattern(K.CrawlHang, d));
            }
            foreach (int hold in new[] { 4, 10, 30 }) g.Add(new Pattern(K.Jump, 0, hold));
            foreach (int d in new[] { -1, 1 })
            {
                foreach (int hold in new[] { 4, 10, 30 })
                    foreach (int run in new[] { 0, 10 })
                        g.Add(new Pattern(K.Jump, d, hold, run));
                g.Add(new Pattern(K.Jump, d, 30, 0, 8));
                g.Add(new Pattern(K.Jump, d, 30, 0, 16));
                g.Add(new Pattern(K.Jump, d, 30, 0, 0, true));
                g.Add(new Pattern(K.Jump, d, 12, 0, 0, true));
            }
            g.Add(new Pattern(K.Drop));
            g.Add(new Pattern(K.ClimbUp));
            g.Add(new Pattern(K.ClimbDown));
            groundPatterns = g.ToArray();

            // Extra patterns only tried when the glider is available: keep JUMP held through the whole fall.
            var gl = new List<Pattern>();
            foreach (int d in new[] { -1, 1 })
            {
                gl.Add(new Pattern(K.Jump, d, 240, 0));
                gl.Add(new Pattern(K.Jump, d, 240, 10));
                gl.Add(new Pattern(K.Jump, d, 240, 0, 14));
                gl.Add(new Pattern(K.WalkGlide, d));
            }
            gl.Add(new Pattern(K.Jump, 0, 240));
            glidePatterns = gl.ToArray();

            climbPatterns = new[]
            {
                new Pattern(K.ClimbUp), new Pattern(K.ClimbDown),
                new Pattern(K.ClimbJump, -1, 30), new Pattern(K.ClimbJump, 0, 30), new Pattern(K.ClimbJump, 1, 30),
                new Pattern(K.ClimbJump, -1, 30, 0, 10), new Pattern(K.ClimbJump, 1, 30, 0, 10),
            };
            hangPatterns = new[]
            {
                new Pattern(K.HangUp), new Pattern(K.HangDrop),
                new Pattern(K.HangJump, 0, 30), new Pattern(K.HangJump, -1, 30, 0, 12), new Pattern(K.HangJump, 1, 30, 0, 12),
                new Pattern(K.HangJump, -1, 30), new Pattern(K.HangJump, 1, 30),
            };
        }

        public struct Options
        {
            public MotorStats stats;
            public float fallLimit;
            public float speedMult;
            public float jumpMult;
            public float heavySpeed;
            public int maxNodes;
            public int ropeRise;
            public int ropeLength;
            public bool glide;
            /// <summary>Ground grip of the level (LevelData.grip): the proof and the bot slide exactly like the player.</summary>
            public float grip;
        }

        public static Options DefaultOptions(PlayerConfig cfg) => new Options
        {
            stats = cfg.motor, fallLimit = cfg.fallDamageHeight, speedMult = 1f, jumpMult = 1f, heavySpeed = cfg.heavyCarrySpeed, maxNodes = 6000,
            ropeRise = cfg.ropeThrowHeight, ropeLength = cfg.ropeLength
        };

        // ------------------------------------------------------------------ public API

        /// <summary>Validates entrance -> exit (and entrance -> shop when the level has one).</summary>
        /// <summary>
        /// The level as it is once the player has used the required gear: rubble cleared by bombs and ropes hooked
        /// where a rope thrown from each rope spot would really hook (same code as the rope itself).
        /// </summary>
        public static TileGrid GearGrid(LevelData d, Options o)
        {
            if (d.gateCount <= 0) return d.grid;
            var g = d.grid.Clone();
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                    if (g.Get(x, y) == TileType.Rubble) g.SetRaw(x, y, TileType.Empty);
            foreach (var s in d.ropeSpots)
            {
                int anchor = RopeProjectile.SimulateThrow(g, s.x, s.y, o.ropeRise);
                if (anchor < 0) continue;
                foreach (var c in RopeProjectile.ComputeCells(g, s.x, anchor, o.ropeLength)) g.AddRope(c.x, c.y, 1);
            }
            return g;
        }

        /// <summary>Options with the level's required gear available (e.g. glider -> gliding allowed).</summary>
        public static Options WithGear(LevelData d, Options o)
        {
            if (d.gateCount > 0) GearRegistry.Get(d.gateGear)?.ApplyToValidator(ref o);
            o.grip = d.grip;
            return o;
        }

        public static ValidationResult Validate(LevelData d, Options baseOptions)
        {
            var sw = Stopwatch.StartNew();
            var r = new ValidationResult();
            baseOptions.grip = d.grip;
            var noGear = baseOptions;
            noGear.glide = false;
            var o = WithGear(d, baseOptions);
            var grid = GearGrid(d, o);
            var start = MotorState.At(new Vector2(d.entrance.x + 0.5f, d.entrance.y));
            var shops = d.shopRects;
            bool needShop = shops.Count > 0;
            bool shopFound = !needShop, exitFound = false;
            Vector2Int exit = d.exit;

            Search(grid, start, o, s =>
            {
                if (!exitFound && IsAtCell(s, exit)) exitFound = true;
                if (!shopFound && s.mode == MotorMode.Normal && s.grounded)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(s.pos.x), Mathf.FloorToInt(s.pos.y + 0.01f));
                    foreach (var rr in shops) if (rr.Contains(c)) { shopFound = true; break; }
                }
                return exitFound && shopFound;
            }, out var stats, null);

            r.nodes = stats.nodes; r.simulations = stats.sims;
            r.exitReachable = exitFound;
            r.shopReachable = shopFound;
            if (d.goal.HasValue)
            {
                // Final level: entrance -> goal, then goal -> exit while carrying the heavy Heartstone.
                var plan = PlanFinal(d, o, out string why);
                r.goalReachable = plan != null;
                if (plan == null) r.reason += why;
                r.exitReachable = r.goalReachable;
            }
            if (d.gatesWanted > 0 && d.gateCount == 0) { r.reason += "no gear gate placed; "; r.ok = false; r.ms = sw.Elapsed.TotalMilliseconds; return r; }
            if (d.gateCount > 0 && r.exitReachable)
            {
                // The puzzle must be mandatory: without the gear the exit has to be out of reach.
                int found = Search(d.grid, start, noGear, s => IsAtCell(s, exit), out _, null);
                r.bypassed = found >= 0;
                if (r.bypassed) r.reason += "gate can be bypassed without " + d.gateGear + "; ";
            }
            r.ok = r.exitReachable && r.shopReachable && r.goalReachable && !r.bypassed;
            if (!r.exitReachable && !d.goal.HasValue) r.reason += "exit unreachable; ";
            if (!r.shopReachable) r.reason += "shop unreachable; ";
            r.ms = sw.Elapsed.TotalMilliseconds;
            return r;
        }

        public static bool IsAtCell(in MotorState s, Vector2Int c) =>
            s.mode == MotorMode.Normal && s.grounded && Mathf.Abs(s.pos.x - (c.x + 0.5f)) < 0.55f && Mathf.Abs(s.pos.y - c.y) < 0.05f;

        /// <summary>Input frames from start to the first resting state satisfying goal, or null.</summary>
        public static List<MotorInput> FindPath(TileGrid g, MotorState start, Options o, Func<MotorState, bool> goal, out MotorState end)
        {
            int found = Search(g, start, o, goal, out _, null, true);
            end = start;
            if (found < 0) return null;
            return Reconstruct(g, o, found, out end);
        }

        /// <summary>Full bot plan for the final level (walk to Heartstone, pick it up, carry it out).</summary>
        public static List<FrameInput> PlanFinal(LevelData d, Options o, out string reason)
        {
            reason = "";
            o.grip = d.grip;
            var g = d.grid;
            if (!d.goal.HasValue)
            {
                // a last level with no stone lying in it (its keeper carries it): the way is simply entrance -> gate
                var straight = PlanToExit(d, o);
                if (straight == null) reason = "exit unreachable; ";
                return straight;
            }
            var start = MotorState.At(new Vector2(d.entrance.x + 0.5f, d.entrance.y));
            var goal = d.goal.Value;
            var p1 = FindPath(g, start, o, s => IsAtCell(s, goal), out var atGoal);
            if (p1 == null) { reason = "heartstone unreachable; "; return null; }
            var frames = ToFrames(p1);
            // crouch + attack = pick up, then stand
            var s2 = atGoal;
            var pick = new MotorInput { down = true, speedMult = o.speedMult, jumpMult = o.jumpMult, grip = o.grip };
            PlayerMotor.Step(ref s2, pick, g, o.stats, GameConst.Dt);
            frames.Add(new FrameInput { down = true, attackPressed = true });
            var heavy = o;
            heavy.speedMult = o.speedMult * (o.heavySpeed > 0f ? o.heavySpeed : 1f);
            for (int i = 0; i < 3; i++)
            {
                PlayerMotor.Step(ref s2, new MotorInput { speedMult = heavy.speedMult, jumpMult = heavy.jumpMult, grip = o.grip }, g, o.stats, GameConst.Dt);
                frames.Add(default);
            }
            var p2 = FindPath(g, s2, heavy, s => IsAtCell(s, d.exit), out _);
            if (p2 == null) { reason = "exit unreachable while carrying the Heartstone; "; return null; }
            frames.AddRange(ToFrames(p2));
            frames.Add(new FrameInput { interactPressed = true });
            return frames;
        }

        /// <summary>Bot plan for a normal level: inputs from the entrance to the exit door, ending with "enter".</summary>
        public static List<FrameInput> PlanToExit(LevelData d, Options o)
        {
            var start = MotorState.At(new Vector2(d.entrance.x + 0.5f, d.entrance.y));
            o = WithGear(d, o);
            var p = FindPath(GearGrid(d, o), start, o, s => IsAtCell(s, d.exit), out _);
            if (p == null) return null;
            var frames = ToFrames(p);
            frames.Add(new FrameInput { interactPressed = true });
            return frames;
        }

        public static List<FrameInput> ToFrames(List<MotorInput> inputs)
        {
            var list = new List<FrameInput>(inputs.Count);
            foreach (var i in inputs)
                list.Add(new FrameInput { x = i.x, up = i.up, down = i.down, walk = i.walk, jumpPressed = i.jumpPressed, jumpHeld = i.jumpHeld });
            return list;
        }

        // ------------------------------------------------------------------ search

        public struct SearchStats { public int nodes; public int sims; }

        [ThreadStatic] static List<Node> nodes;
        [ThreadStatic] static TileGrid lastGrid;

        static int Key(TileGrid g, in MotorState s)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(s.pos.x), 0, g.Width - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(s.pos.y + 0.01f), 0, g.Height - 1);
            int m = s.mode == MotorMode.Climb ? 1 : s.mode == MotorMode.Hang ? (s.hangDir < 0 ? 2 : 3) : 0;
            return ((cy * g.Width) + cx) * 4 + m;
        }

        static int Search(TileGrid g, MotorState start, Options o, Func<MotorState, bool> goal, out SearchStats st, List<MotorInput> unused, bool keepNodes = false)
        {
            st = default;
            if (nodes == null) nodes = new List<Node>(4096);
            nodes.Clear();
            lastGrid = g;
            var visited = new bool[g.Width * g.Height * 4];
            var q = new Queue<int>();
            nodes.Add(new Node { state = start, parent = -1, pattern = -1 });
            visited[Key(g, start)] = true;
            q.Enqueue(0);
            if (goal(start)) return 0;
            while (q.Count > 0)
            {
                int n = q.Dequeue();
                var s = nodes[n].state;
                var pats = s.mode == MotorMode.Climb ? climbPatterns : s.mode == MotorMode.Hang ? hangPatterns : groundPatterns;
                int baseIdx = s.mode == MotorMode.Climb ? 1000 : s.mode == MotorMode.Hang ? 2000 : 0;
                int total = pats.Length + (o.glide && s.mode == MotorMode.Normal ? glidePatterns.Length : 0);
                for (int pi = 0; pi < total; pi++)
                {
                    bool extra = pi >= pats.Length;
                    var pat = extra ? glidePatterns[pi - pats.Length] : pats[pi];
                    if (!Applicable(pat, s, g)) continue;
                    st.sims++;
                    if (!Simulate(g, s, pat, o, out var end, null)) continue;
                    int k = Key(g, end);
                    if (visited[k]) continue;
                    visited[k] = true;
                    nodes.Add(new Node { state = end, parent = n, pattern = extra ? 3000 + (pi - pats.Length) : baseIdx + pi });
                    int idx = nodes.Count - 1;
                    st.nodes = nodes.Count;
                    if (goal(end)) return idx;
                    if (nodes.Count >= o.maxNodes) return -1;
                    q.Enqueue(idx);
                }
            }
            st.nodes = nodes.Count;
            return -1;
        }

        static Pattern GetPattern(int id)
        {
            if (id >= 3000) return glidePatterns[id - 3000];
            if (id >= 2000) return hangPatterns[id - 2000];
            if (id >= 1000) return climbPatterns[id - 1000];
            return groundPatterns[id];
        }

        static List<MotorInput> Reconstruct(TileGrid g, Options o, int idx, out MotorState end)
        {
            var chain = new List<int>();
            for (int i = idx; i > 0; i = nodes[i].parent) chain.Add(i);
            chain.Reverse();
            var inputs = new List<MotorInput>();
            end = nodes[idx].state;
            foreach (int i in chain)
            {
                var from = nodes[nodes[i].parent].state;
                Simulate(g, from, GetPattern(nodes[i].pattern), o, out _, inputs);
            }
            return inputs;
        }

        // ------------------------------------------------------------------ pattern simulation

        static bool Applicable(in Pattern p, in MotorState s, TileGrid g)
        {
            int cx = Mathf.FloorToInt(s.pos.x);
            switch (p.kind)
            {
                case K.Drop:
                {
                    int row = Mathf.FloorToInt(s.pos.y - 0.02f);
                    return s.grounded && g.IsPlatformTop(cx, row) && !g.IsSolid(cx, row);
                }
                case K.ClimbUp:
                    return s.mode == MotorMode.Climb || g.IsClimbable(cx, Mathf.FloorToInt(s.pos.y + 0.3f));
                case K.ClimbDown:
                    return s.mode == MotorMode.Climb || g.IsLadder(cx, Mathf.FloorToInt(s.pos.y - 0.05f));
                case K.CrawlHang:
                {
                    // only near an edge in that direction
                    int row = Mathf.FloorToInt(s.pos.y - 0.02f);
                    int nx = Mathf.FloorToInt(s.pos.x + p.dir * 0.9f);
                    return !g.IsSolid(nx, row) && !g.IsPlatformTop(nx, row);
                }
            }
            return true;
        }

        static bool IsRest(in MotorState s)
        {
            switch (s.mode)
            {
                case MotorMode.Normal: return s.grounded && Mathf.Abs(s.vel.x) < 0.01f && s.jumpBuffer <= 0f;
                case MotorMode.Climb: return s.vel.y == 0f && s.jumpBuffer <= 0f;
                case MotorMode.Hang: return s.jumpBuffer <= 0f;
                default: return false;
            }
        }

        static bool Simulate(TileGrid g, MotorState s, Pattern p, Options o, out MotorState end, List<MotorInput> record)
        {
            end = s;
            int startCX = Mathf.FloorToInt(s.pos.x);
            int startProbe = Mathf.FloorToInt(s.pos.y + 0.3f);
            MotorMode startMode = s.mode;
            bool released = false, airborneSeen = false, climbing = s.mode == MotorMode.Climb;
            int jumpFrame = p.runUp;
            var neutral = new MotorInput { speedMult = o.speedMult, jumpMult = o.jumpMult, glide = o.glide, grip = o.grip };

            for (int f = 0; f < MaxFrames; f++)
            {
                MotorInput i = neutral;
                if (!released)
                {
                    switch (p.kind)
                    {
                        case K.Walk:
                        case K.WalkRelease:
                            if (s.mode != MotorMode.Normal || (f > 0 && s.grounded && (Mathf.FloorToInt(s.pos.x) != startCX)) ||
                                (p.kind == K.WalkRelease && !s.grounded) || (airborneSeen && s.grounded) ||
                                (f > 3 && s.grounded && Mathf.Abs(s.vel.x) < 0.01f))
                                released = true;
                            else { i.x = p.dir; i.walk = p.walk; }
                            if (!s.grounded) airborneSeen = true;
                            break;
                        case K.Jump:
                        case K.ClimbJump:
                        case K.HangJump:
                            if (f > jumpFrame && (s.mode == MotorMode.Hang || s.mode == MotorMode.Climb || (airborneSeen && s.grounded)))
                            { released = true; break; }
                            if (f > jumpFrame && !s.grounded) airborneSeen = true;
                            if (f < jumpFrame) i.x = p.dir;
                            else
                            {
                                if (f == jumpFrame) i.jumpPressed = true;
                                i.jumpHeld = f < jumpFrame + p.hold;
                                if (f >= jumpFrame + p.delay) i.x = p.dir;
                                i.walk = p.walk;
                            }
                            if (f > jumpFrame + 200) released = true;
                            break;
                        case K.WalkGlide:
                            // walk off a ledge with JUMP held (no press): glide down
                            if (s.mode != MotorMode.Normal || (airborneSeen && s.grounded) || (f > 3 && s.grounded && Mathf.Abs(s.vel.x) < 0.01f))
                            { released = true; break; }
                            if (!s.grounded) airborneSeen = true;
                            i.x = p.dir; i.jumpHeld = true;
                            if (f > 240) released = true;
                            break;
                        case K.CrawlHang:
                            if (s.mode == MotorMode.Hang) { released = true; break; }
                            if (f > 90) return false;
                            i.down = true; i.x = p.dir;
                            break;
                        case K.Drop:
                            if (f == 0) { i.down = true; i.jumpPressed = true; i.jumpHeld = true; }
                            else if (f < 4) i.down = true;
                            else released = true;
                            break;
                        case K.ClimbUp:
                            if (s.mode == MotorMode.Climb) climbing = true;
                            if (climbing && (s.mode != MotorMode.Climb || Mathf.FloorToInt(s.pos.y + 0.3f) != startProbe || (f > 2 && s.vel.y == 0f)))
                            { released = true; break; }
                            if (!climbing && f > 4) return false;
                            i.up = true;
                            break;
                        case K.ClimbDown:
                            if (s.mode == MotorMode.Climb) climbing = true;
                            if (climbing && f > 1 && (s.mode != MotorMode.Climb || Mathf.FloorToInt(s.pos.y + 0.3f) != startProbe))
                            { released = true; break; }
                            if (!climbing && f > 4) return false;
                            i.down = true;
                            break;
                        case K.HangUp:
                            if (s.mode == MotorMode.Normal) { released = true; break; }
                            if (f > 60) return false;
                            i.up = true;
                            break;
                        case K.HangDrop:
                            if (f == 0) i.down = true; else released = true;
                            break;
                    }
                }

                record?.Add(i);
                PlayerMotor.Step(ref s, i, g, o.stats, GameConst.Dt);
                if (s.hitSpikes) return false;
                if (s.landed && s.fallDistance >= o.fallLimit) return false;
                if (s.pos.y < 0.5f || s.pos.x < 0.5f || s.pos.x > g.Width - 0.5f) return false;
                if (released && IsRest(s))
                {
                    end = s;
                    return true;
                }
            }
            return false;
        }
    }
}
