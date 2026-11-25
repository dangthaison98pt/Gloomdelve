using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The kennel by the player's house in Brindlemoor. It keeps a dog (it puts the dog out itself when the
    /// village is built): see VillageDog. The player can crawl into it too - but only down on all fours in front of
    /// its door: crouched there, the button to crawl in shows; standing, nothing does. In, they crawl up to the door
    /// and through it into the room inside (KennelRoom); out again by that room's door, they crawl out here.
    /// </summary>
    public class Kennel : Prop, IInteractable, IHoldsPlayer
    {
        const float CrawlSpeed = 2.6f, OutTime = 0.45f;
        [Tooltip("How far from the middle of the door the player can be and still crawl in.")] public float doorReach = 0.9f;

        PlayerController guest;
        /// <summary>0 nobody, 1 crawling in, 2 through the door (the screen goes dark), 3 crawling out.</summary>
        int phase;
        float t, outToX;

        public bool Busy => guest != null;

        public static Kennel Find(World w)
        {
            if (w == null) return null;
            foreach (var e in w.Entities) if (e is Kennel k && !k.Dead) return k;
            return null;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            guest = null; phase = 0;
            var cfg = World.config;
            var prefab = cfg != null && cfg.prefabs != null ? cfg.prefabs.Get("villagedog") : null;
            if (prefab == null) return;
            var cell = new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.RoundToInt(pos.y));
            var dog = World.Spawn(prefab, new Vector2(pos.x - 1.1f, pos.y), new SpawnPoint("villagedog", cell)) as VillageDog;
            if (dog != null) dog.SetHome(new Vector2(pos.x - 1.1f, pos.y));
        }

        bool AtDoor(PlayerController p) =>
            p != null && !p.IsDead && p.grounded && Mathf.Abs(p.pos.x - pos.x) <= doorReach && Mathf.Abs(p.pos.y - pos.y) < 0.5f;

        public string GetPrompt(PlayerController p)
        {
            if (Busy || ScreenFade.Busy || !AtDoor(p) || !p.Crouching || KennelRoom.Find(World) == null) return null;    // standing up, the door is too low: nothing to do
            return "[enter]  Crawl in";
        }

        public void Interact(PlayerController p)
        {
            if (GetPrompt(p) == null) return;
            guest = p; phase = 1; t = 0f;
            outToX = p.pos.x >= pos.x ? pos.x + 0.8f : pos.x - 0.8f;      // (they come back out on the side they went in)
            p.restingIn = this; p.restAngle = 0f; p.restCrawl = true;
            p.facing = p.motor.facing = pos.x >= p.pos.x ? 1 : -1;
            Sfx.Play("land", pos, 0.35f, 1.4f);
        }

        public string HeldPrompt(PlayerController p) => null;
        public void LetGo(PlayerController p) { }

        /// <summary>Out of the room inside: they crawl out of the door, on the side they went in.</summary>
        public void ComeOut(PlayerController p)
        {
            if (p == null) return;
            if (outToX == 0f) outToX = pos.x + 0.8f;
            guest = p; phase = 3; t = 0f;
            VillageInterior.Place(p, new Vector2(pos.x, pos.y));
            p.motor.crouching = true;
            p.restingIn = this; p.restAngle = 0f; p.restCrawl = true;
            p.facing = p.motor.facing = outToX >= pos.x ? 1 : -1;
            Fx.Dust(new Vector2(pos.x, pos.y + 0.1f), 3);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (guest == null) return;
            if (guest.IsDead || guest.restingIn != this) { Release(); return; }
            t += dt;
            switch (phase)
            {
                case 1:
                {
                    // on all fours up to the door, and through it
                    float dx = pos.x - guest.pos.x;
                    float step = CrawlSpeed * dt;
                    if (Mathf.Abs(dx) > step) { Put(guest, guest.pos.x + Mathf.Sign(dx) * step); break; }
                    Put(guest, pos.x);
                    phase = 2;
                    Fx.Dust(new Vector2(pos.x, pos.y + 0.1f), 3);
                    var p = guest;
                    var room = KennelRoom.Find(World);
                    ScreenFade.Through(() =>
                    {
                        Release();
                        if (room != null && p != null && !p.IsDead) room.Enter(p);
                    });
                    break;
                }
                case 3:
                    Put(guest, Mathf.Lerp(pos.x, outToX, Mathf.Clamp01(t / OutTime)));
                    if (t >= OutTime) Release();
                    break;
            }
        }

        void Release()
        {
            if (guest != null)
            {
                guest.restCrawl = false;
                if (guest.restingIn == this) guest.restingIn = null;
            }
            guest = null; phase = 0;
        }

        static void Put(PlayerController p, float x)
        {
            p.motor.pos.x = x;
            p.pos = p.motor.pos;
            p.vel = Vector2.zero; p.motor.vel = Vector2.zero;
        }
    }
}
