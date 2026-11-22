using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The hammer the old signs speak of (Story): whole once its three parts are in the bag. In hand, the use
    /// button swings it: it is raised over the shoulder and brought down in front of the player, and where it
    /// lands it does three damage to whatever stands there. Never used up.
    /// It is also the one thing that breaks the Sunstone: struck where it lies once its keeper has fallen (or
    /// while it is carried), the stone breaks, and the game ends as it really ends (GameDirector.BreakSunstone).
    /// </summary>
    public class HammerGear : GearBehaviour
    {
        public const int Damage = 3;
        /// <summary>How far in front of the player the blow reaches, the time from the button to the blow, and the rest before the next.</summary>
        public const float Reach = 1.7f, WindUp = 0.13f, Rest = 0.5f;

        static float readyAt;
        static readonly List<Entity> tmp = new List<Entity>();

        public override void Use(PlayerController p, FrameInput fi)
        {
            if (p == null || p.World == null || Time.time < readyAt) return;
            readyAt = Time.time + Rest;
            p.PlayThrowAnim(0.3f);
            Sfx.Play("whip", p.pos, 0.9f, 0.6f);
            var def = p.World.config != null ? p.World.config.FindGear(Story.HammerGear) : null;
            HammerSwing.Begin(p, def != null ? (def.inHand != null ? def.inHand : def.icon) : null);
        }

        /// <summary>The hammer lands: everything in front of the player takes the blow, and the Sunstone, if it is there, breaks.</summary>
        public static void Strike(PlayerController p)
        {
            if (p == null || p.World == null || p.IsDead) return;
            var hit = p.facing > 0 ? new Rect(p.pos.x, p.pos.y - 0.1f, Reach, 1.7f) : new Rect(p.pos.x - Reach, p.pos.y - 0.1f, Reach, 1.7f);
            p.World.Query(hit, tmp);
            Treasure stone = p.held is Treasure carried && carried.isHeartstone ? carried : null;
            foreach (var e in tmp)
            {
                if (e == null || e == p || e.Dead) continue;
                if (e is Treasure t && t.isHeartstone) { stone = t; continue; }
                if (e == p.held || e is Pickup || e is Door || e is ShopItem || e is Sign) continue;
                e.OnWhipped(p, Damage, p.facing);
                Fx.Sparks(e.Center, new Color(1f, 0.85f, 0.5f), 6, 4f);
            }
            var front = new Vector2(p.pos.x + p.facing * 1.05f, p.pos.y + 0.05f);
            Fx.Dust(front, 8);
            Fx.Sparks(front, new Color(0.7f, 0.95f, 1f), 8, 5f);
            LevelLighting.Flash(front, new Color(0.7f, 0.95f, 1f), 3.2f, 1.4f, 0.22f);
            Fx.Shake(0.22f);
            Sfx.Play("crack", p.pos, 1f, 0.8f);
            var dir = GameDirector.Instance;
            if (stone != null && dir != null) dir.BreakSunstone(stone);
        }
    }

    /// <summary>
    /// The hammer seen in the hand for the length of one blow: held by the end of its haft at the player's hand,
    /// raised back over the shoulder, brought down in front, and left there a moment. The blow itself
    /// (HammerGear.Strike) falls when it lands.
    /// </summary>
    public class HammerSwing : MonoBehaviour
    {
        PlayerController p;
        float t;
        bool struck;
        int facing;
        const float Stay = 0.16f;

        public static void Begin(PlayerController p, Sprite hammer)
        {
            var go = new GameObject("HammerSwing");
            var sw = go.AddComponent<HammerSwing>();
            sw.p = p;
            sw.facing = p.facing >= 0 ? 1 : -1;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = hammer;
            var body = p.GetComponentInChildren<SpriteRenderer>();
            if (body != null) { sr.sharedMaterial = body.sharedMaterial; sr.sortingLayerID = body.sortingLayerID; sr.sortingOrder = body.sortingOrder + 2; }
            // (never scaled: its pixels are the size of the player's own - the picture itself has the long haft)
            sw.Place(0f);
        }

        /// <summary>k: 0 = raised back over the shoulder, 1 = down in front.</summary>
        void Place(float k)
        {
            if (p == null) return;
            // (the picture is drawn head up, held by the foot of its haft: turned from leaning back to lying forward)
            float angle = Mathf.Lerp(55f, -112f, k) * facing;
            transform.position = new Vector3(p.transform.position.x + facing * 0.22f, p.transform.position.y + 0.62f, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void LateUpdate()
        {
            if (p == null || p.IsDead) { Destroy(gameObject); return; }
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / HammerGear.WindUp);
            Place(k * k);
            if (!struck && t >= HammerGear.WindUp) { struck = true; HammerGear.Strike(p); }
            if (t >= HammerGear.WindUp + Stay) Destroy(gameObject);
        }
    }
}
