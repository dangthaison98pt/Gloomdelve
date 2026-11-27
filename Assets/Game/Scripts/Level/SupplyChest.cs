using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A chest in a house that is filled again every real day (VillageInterior puts them out): Tam's holds a rope, the
    /// player's own a bomb. Opened (Enter), its one thing goes into the bag (unless that bag is full: then it stays
    /// shut). Emptied, it stands open; standing by it says it is empty and when it will be full again. The next
    /// calendar day - on the player's own clock - it is shut and full once more. (Once a day, not once a run: a run
    /// given up at once would otherwise fill the bags for nothing.) The day it was last emptied is kept in the save.
    /// </summary>
    public class SupplyChest : Entity, IInteractable
    {
        public enum Kind { Rope, Bomb }

        public Sprite closed, open;
        [Tooltip("Its outline while the player is in reach (full).")] public Sprite outline;
        public SpriteRenderer glow;

        public Kind kind;

        /// <summary>Today, on the player's own clock (tests set another day).</summary>
        public static System.Func<System.DateTime> Today = () => System.DateTime.Now;
        static string Day => Today().ToString("yyyy-MM-dd");

        // tests keep their days here, not in the save
        static readonly Dictionary<string, string> testDays = new Dictionary<string, string>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;
        public static void ResetTestState() { testDays.Clear(); Today = () => System.DateTime.Now; }

        string Id => kind == Kind.Rope ? "rope" : "bomb";

        static string TakenOn(string id)
        {
            if (Testing) return testDays.TryGetValue(id, out var d) ? d : null;
            var list = SaveSystem.Records.supplyDays;
            if (list == null) return null;
            foreach (var e in list) if (e != null && e.StartsWith(id + "|")) return e.Substring(id.Length + 1);
            return null;
        }

        static void MarkTaken(string id)
        {
            if (Testing) { testDays[id] = Day; return; }
            var rec = SaveSystem.Records;
            if (rec.supplyDays == null) rec.supplyDays = new List<string>();
            rec.supplyDays.RemoveAll(e => e != null && e.StartsWith(id + "|"));
            rec.supplyDays.Add(id + "|" + Day);
            SaveSystem.SaveRecords();
        }

        /// <summary>Full today (not emptied yet on this calendar day).</summary>
        public bool Full => TakenOn(Id) != Day;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            kind = sp.param == 1 ? Kind.Bomb : Kind.Rope;
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            Show();
        }

        void Show() { if (sr != null) sr.sprite = Full ? closed : open; }

        public string GetPrompt(PlayerController p)
        {
            if (Full) return "[enter]  Open the chest";
            // emptied today: it says so, and when it will be full again (words told, not a button)
            return kind == Kind.Rope ? "Tam's chest is empty. There will be another rope in it tomorrow."
                                     : "Your chest is empty. There will be another bomb in it tomorrow.";
        }

        public void Interact(PlayerController p)
        {
            if (!Full || p == null) return;
            var run = RunSession.Current;
            if (run == null) return;
            string res = kind == Kind.Rope ? Res.Ropes : Res.Bombs;
            int before = kind == Kind.Rope ? run.ropes : run.bombs;
            run.AddResource(res, 1);
            int after = kind == Kind.Rope ? run.ropes : run.bombs;
            if (after == before)
            {
                // the bag is full: the chest is left as it is, for another time today
                Sfx.Play("error", pos, 0.5f);
                Fx.Say(p, kind == Kind.Rope ? "No room for another rope." : "No room for another bomb.", new Color(1f, 0.92f, 0.8f), 1.8f);
                return;
            }
            MarkTaken(Id);
            Show();
            Sfx.Play("creak", pos, 0.5f, 1.3f);
            Sfx.Play("pickup", pos, 0.8f);
            Fx.Text(new Vector2(p.pos.x, p.pos.y + 2.2f), kind == Kind.Rope ? "+1 Rope" : "+1 Bomb", new Color(1f, 0.9f, 0.6f), 1.6f);
        }

        public override void Tick(float dt)
        {
            // past midnight with the house open: it is full again
            if (sr != null && sr.sprite != (Full ? closed : open)) Show();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (Full) InReach.Show(glow, outline, this);
            else if (glow != null) glow.enabled = false;
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
