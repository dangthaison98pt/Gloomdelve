using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// The village dog. By day (and at dawn and dusk) it is out in the street: it trots from place to place, sits
    /// a while, and when the player comes near it runs up and sits by them. At night (VillageClock) it lies
    /// asleep in its basket in the room inside the kennel (KennelRoom); by day its basket there is empty. Asleep,
    /// it can be petted - and grumbles to be let alone.
    /// </summary>
    public class VillageDog : Prop, IInteractable
    {
        [Tooltip("0 standing, 1 / 2 running, 3 sitting, 4 asleep.")] public Sprite[] frames;
        public float runSpeed = 5.2f;
        [Tooltip("How far from its kennel it roams.")] public float roam = 16f;
        [Tooltip("It notices the player from this far.")] public float noticeRange = 4.5f;

        enum State { Sleep, Wait, Run, Greet }
        State state;
        Vector2 home;
        float minX, maxX, targetX, timer, animT, greetCool, zT;
        bool moved;

        public bool Asleep => state == State.Sleep;

        // asleep in its basket it can be petted - and it says so, if it is woken
        static readonly string[] Grumbles = { "Grrr... not now.", "Hrrmph. Let me sleep.", "*one eye opens* ...Go away.", "Mmf. Do not disturb.", "Five more minutes.", "*a long sigh* ...Really?" };
        float grumbleCool;
        int lastGrumble = -1;
        public string LastGrumble { get; private set; }

        public string GetPrompt(PlayerController p) => InBasket && Asleep && grumbleCool <= 0f ? "[enter]  Pet the dog" : null;

        public void Interact(PlayerController p)
        {
            if (GetPrompt(p) == null) return;
            int i = Random.Range(0, Grumbles.Length);
            if (i == lastGrumble) i = (i + 1) % Grumbles.Length;
            lastGrumble = i;
            LastGrumble = Grumbles[i];
            grumbleCool = 1.2f; zT = 2.4f;
            Sfx.Play("rustle", pos, 0.5f, 0.8f);
            Fx.Say(this, LastGrumble, new Color(1f, 0.92f, 0.8f), 2f);
        }
        /// <summary>Asleep in its basket in the room inside the kennel.</summary>
        public bool InBasket { get; private set; }
        public static bool NightNow => VillageClock.PartOf(VillageClock.Hour) == VillageClock.Part.Night;

        public static VillageDog Find(World w)
        {
            if (w == null) return null;
            foreach (var e in w.Entities) if (e is VillageDog d && !d.Dead) return d;
            return null;
        }

        /// <summary>Into its basket in the room inside the kennel, asleep.</summary>
        public void GoToBed(KennelRoom room)
        {
            if (room == null) return;
            pos = prevPos = room.BasketSpot;
            state = State.Sleep; InBasket = true; facing = 1; zT = 1.2f;
            Show();
        }

        /// <summary>Out of the kennel again (the player left it by day): it stands at the door, then goes about its day.</summary>
        public void OutOfBed()
        {
            if (!InBasket) return;
            InBasket = false;
            pos = prevPos = home;
            state = State.Wait; timer = 1.2f; greetCool = 2f;
            Show();
        }
        public bool Greeting => state == State.Greet;

        public void SetHome(Vector2 at)
        {
            home = at;
            var g = World.Grid;
            int y = Mathf.RoundToInt(at.y), x0 = Mathf.FloorToInt(at.x), x1 = x0;
            while (x0 > 1 && !g.IsSolid(x0 - 1, y) && at.x - x0 < roam) x0--;
            while (x1 < g.Width - 2 && !g.IsSolid(x1 + 1, y) && x1 - at.x < roam) x1++;
            minX = x0 + 0.6f; maxX = x1 + 0.4f;
            bool night = VillageClock.PartOf(VillageClock.Hour) == VillageClock.Part.Night;
            state = night ? State.Sleep : State.Wait;
            timer = 1f; facing = -1;
            pos = at;
            Show();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            flipWithFacing = true;
            if (maxX <= minX) { home = pos; minX = pos.x - 4f; maxX = pos.x + 4f; state = State.Wait; timer = 1f; }
            Show();
        }

        bool PlayerNear(PlayerController p) =>
            p != null && !p.IsDead && greetCool <= 0f && Mathf.Abs(p.pos.x - pos.x) < noticeRange && Mathf.Abs(p.pos.y - pos.y) < 1.5f;

        public override void Tick(float dt)
        {
            animT += dt; greetCool -= dt; grumbleCool -= dt;
            var p = World.Player;
            switch (state)
            {
                case State.Sleep:
                    if (!InBasket) { var room = KennelRoom.Find(World); if (room != null) { GoToBed(room); break; } }       // (its bed is inside, by night)
                    zT -= dt;
                    if (zT <= 0f) { zT = 2.6f; Fx.Text(new Vector2(pos.x + 0.3f, pos.y + 0.7f), "z", new Color(0.8f, 0.86f, 1f), 1.4f); }
                    break;
                case State.Wait:
                    timer -= dt;
                    if (PlayerNear(p)) { state = State.Greet; timer = 4f; break; }
                    if (timer <= 0f) { targetX = Mathf.Lerp(minX, maxX, Hash(animT)); state = State.Run; }
                    break;
                case State.Run:
                    if (PlayerNear(p)) { state = State.Greet; timer = 4f; break; }
                    if (Step(targetX, runSpeed, dt)) { state = State.Wait; timer = 1f + Hash(animT * 3f) * 3f; }
                    break;
                case State.Greet:
                {
                    // up to the player, and sits there looking at them; then it has other things to do for a while
                    timer -= dt;
                    if (p == null || p.IsDead || timer <= 0f || Mathf.Abs(p.pos.y - pos.y) > 2f) { state = State.Wait; timer = 0.5f; greetCool = 7f; break; }
                    float want = Mathf.Clamp(p.pos.x + (pos.x >= p.pos.x ? 1.1f : -1.1f), minX, maxX);
                    if (Mathf.Abs(want - pos.x) > 0.25f) { Step(want, runSpeed * 1.15f, dt); timer = Mathf.Max(timer, 1.6f); }
                    else facing = p.pos.x >= pos.x ? 1 : -1;
                    break;
                }
            }
            Show();
        }

        bool Step(float to, float speed, float dt)
        {
            float dx = to - pos.x;
            if (Mathf.Abs(dx) <= 0.06f) { moved = false; return true; }
            facing = dx > 0 ? 1 : -1;
            pos.x = Mathf.Clamp(pos.x + facing * Mathf.Min(Mathf.Abs(dx), speed * dt), minX, maxX);
            moved = true;
            return false;
        }

        static float Hash(float v) { float f = Mathf.Sin(v * 91.7f) * 43758.5453f; return f - Mathf.Floor(f); }

        void Show()
        {
            if (sr == null || frames == null || frames.Length < 5) return;
            Sprite s;
            if (state == State.Sleep) s = frames[4];
            else if (moved && (state == State.Run || state == State.Greet)) s = frames[1 + (int)(animT * 10f) % 2];
            else if (state == State.Greet) s = frames[3];
            else s = timer > 0.8f ? frames[3] : frames[0];
            if (s != null) sr.sprite = s;
            moved = false;
        }
    }
}
