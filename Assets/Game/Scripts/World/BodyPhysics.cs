using UnityEngine;

namespace Gloomdelve
{
    /// <summary>Axis-aligned body. pos = bottom-centre (feet).</summary>
    public struct Body
    {
        public Vector2 pos, vel;
        public float hw, h;
        public bool grounded, hitLeft, hitRight, hitCeiling, landed;
        public float impactVelY;   // vertical velocity just before landing / ceiling hit
        public float impactVelX;   // horizontal velocity just before hitting a wall
        public float preVelY;      // vertical velocity at the start of the move
    }

    /// <summary>Deterministic tile collision. Max speed per step must stay below 1 tile.</summary>
    public static class BodyPhysics
    {
        public const float Eps = 0.01f;
        const float Skin = 0.0005f;

        public static void Move(TileGrid g, ref Body b, float dt, bool dropThrough)
        {
            b.hitLeft = b.hitRight = b.hitCeiling = b.landed = false;
            bool wasGrounded = b.grounded;
            b.preVelY = b.vel.y;
            b.impactVelY = 0f;
            b.impactVelX = 0f;

            // ---- X axis ----
            float dx = b.vel.x * dt;
            if (dx != 0f)
            {
                float nx = b.pos.x + dx;
                int y0 = Mathf.FloorToInt(b.pos.y + Eps);
                int y1 = Mathf.FloorToInt(b.pos.y + b.h - Eps);
                if (dx > 0f)
                {
                    int col = Mathf.FloorToInt(nx + b.hw);
                    for (int y = y0; y <= y1; y++)
                    {
                        if (g.IsSolid(col, y)) { nx = col - b.hw - Skin; b.hitRight = true; break; }
                    }
                }
                else
                {
                    int col = Mathf.FloorToInt(nx - b.hw);
                    for (int y = y0; y <= y1; y++)
                    {
                        if (g.IsSolid(col, y)) { nx = col + 1 + b.hw + Skin; b.hitLeft = true; break; }
                    }
                }
                if (b.hitLeft || b.hitRight) { b.impactVelX = b.vel.x; b.vel.x = 0f; }
                b.pos.x = nx;
            }

            // ---- Y axis ----
            float dy = b.vel.y * dt;
            int x0 = Mathf.FloorToInt(b.pos.x - b.hw + Eps);
            int x1 = Mathf.FloorToInt(b.pos.x + b.hw - Eps);
            if (dy < 0f)
            {
                float ny = b.pos.y + dy;
                int row = Mathf.FloorToInt(ny);
                bool land = false;
                for (int x = x0; x <= x1; x++)
                {
                    if (g.IsSolid(x, row) ||
                        (!dropThrough && g.IsPlatformTop(x, row) && b.pos.y >= row + 1 - 0.001f))
                    { land = true; break; }
                }
                if (land) { ny = row + 1; b.impactVelY = b.vel.y; b.vel.y = 0f; }
                b.pos.y = ny;
            }
            else if (dy > 0f)
            {
                float ny = b.pos.y + dy;
                int row = Mathf.FloorToInt(ny + b.h);
                for (int x = x0; x <= x1; x++)
                {
                    if (g.IsSolid(x, row)) { ny = row - b.h - Skin; b.hitCeiling = true; b.impactVelY = b.vel.y; b.vel.y = 0f; break; }
                }
                b.pos.y = ny;
            }

            // ---- Ground probe ----
            // Only snaps when the feet are (almost) exactly on a tile top. Feet deeper inside a cell (e.g. head
            // against a ceiling while the feet are inside a ladder-top / platform cell) must NOT be lifted up,
            // otherwise the body gets pushed into the ceiling and can climb through solid tiles.
            b.grounded = false;
            if (b.vel.y <= 0f)
            {
                int row = Mathf.FloorToInt(b.pos.y - 0.02f);
                if (b.pos.y - (row + 1) > -0.03f)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (g.IsSolid(x, row) || (!dropThrough && g.IsPlatformTop(x, row)))
                        {
                            b.grounded = true;
                            b.pos.y = row + 1;
                            break;
                        }
                    }
                }
            }
            b.landed = b.grounded && !wasGrounded;
            if (b.landed && b.impactVelY == 0f) b.impactVelY = b.preVelY;
        }

        /// <summary>True when a floor / one-way surface is directly under the body footprint at x.</summary>
        public static bool HasSupport(TileGrid g, float x, float y, float hw, bool allowPlatforms = true)
        {
            int row = Mathf.FloorToInt(y - 0.02f);
            if (y - (row + 1) < -0.03f) return false;
            int x0 = Mathf.FloorToInt(x - hw + Eps);
            int x1 = Mathf.FloorToInt(x + hw - Eps);
            for (int cx = x0; cx <= x1; cx++)
                if (g.IsSolid(cx, row) || (allowPlatforms && g.IsPlatformTop(cx, row))) return true;
            return false;
        }

        /// <summary>Spike rule shared by the game and the validator: moving downward into a spike cell kills.</summary>
        public static bool SpikeHit(TileGrid g, Vector2 pos, float hw, float vyBefore)
        {
            if (vyBefore > -1.5f) return false;
            int row = Mathf.FloorToInt(pos.y + 0.05f);
            int x0 = Mathf.FloorToInt(pos.x - hw + 0.1f);
            int x1 = Mathf.FloorToInt(pos.x + hw - 0.1f);
            for (int x = x0; x <= x1; x++)
                if (g.IsSpike(x, row)) return true;
            return false;
        }

        /// <summary>
        /// Safety net: if a body ended up inside solid tiles, move it to the nearest free spot (within 1.5 tiles).
        /// Returns true if it had to move. Prefers down, then sideways, then up.
        /// </summary>
        public static bool Unstick(TileGrid g, ref Vector2 pos, float hw, float h)
        {
            if (!OverlapsSolid(g, pos, hw, h)) return false;
            for (float d = 0.05f; d <= 1.5f; d += 0.05f)
            {
                if (!OverlapsSolid(g, new Vector2(pos.x, pos.y - d), hw, h)) { pos = new Vector2(pos.x, pos.y - d); return true; }
                if (!OverlapsSolid(g, new Vector2(pos.x - d, pos.y), hw, h)) { pos = new Vector2(pos.x - d, pos.y); return true; }
                if (!OverlapsSolid(g, new Vector2(pos.x + d, pos.y), hw, h)) { pos = new Vector2(pos.x + d, pos.y); return true; }
                if (!OverlapsSolid(g, new Vector2(pos.x, pos.y + d), hw, h)) { pos = new Vector2(pos.x, pos.y + d); return true; }
            }
            return false;
        }

        public static bool OverlapsSolid(TileGrid g, Vector2 pos, float hw, float h)
        {
            int x0 = Mathf.FloorToInt(pos.x - hw + Eps), x1 = Mathf.FloorToInt(pos.x + hw - Eps);
            int y0 = Mathf.FloorToInt(pos.y + Eps), y1 = Mathf.FloorToInt(pos.y + h - Eps);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (g.IsSolid(x, y)) return true;
            return false;
        }
    }
}
