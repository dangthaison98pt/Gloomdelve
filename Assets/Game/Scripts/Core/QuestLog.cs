using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>The words the game reports things with (QuestLog.Notify). A quest step may use any word: these are the ones something says.</summary>
    public static class QuestTrigger
    {
        public const string Talk = "talk";        // spoken to (target: the QuestId of who)
        public const string Enter = "enter";      // gone into (target: the QuestId of the place)
        public const string Obtain = "obtain";    // got (target: the id the item card was shown for)
        public const string Kill = "kill";        // killed by the player (target: the creature's name)
        public const string Free = "free";        // a villager let out of a cage (target: the villager's id)
    }

    /// <summary>Something in a level a quest can point at or ask for by name: an elder, a house, a villager.</summary>
    public interface IQuestTarget
    {
        string QuestId { get; }
    }

    /// <summary>How far the player is with one quest (kept with the save's records).</summary>
    [Serializable]
    public class QuestState
    {
        public string id;
        public int step;
        public int progress;
        public bool done;
    }

    /// <summary>
    /// What the player is doing and has done: which quests are open, which step each is at, which are finished.
    /// It knows nothing about any particular quest - it reads them from the catalogue (QuestDef assets) and is
    /// told what happens in the game (Notify). Kept with the save; while tests run, in memory only.
    /// </summary>
    public static class QuestLog
    {
        static QuestCatalog catalog;
        static readonly List<QuestState> memory = new List<QuestState>();
        static bool Testing => ScenarioRunner.Active || AutoTest.Active;

        /// <summary>Something changed: a quest began, moved on a step, or was finished.</summary>
        public static event Action Changed;

        static List<QuestState> States
        {
            get
            {
                if (Testing) return memory;
                if (SaveSystem.Records.quests == null) SaveSystem.Records.quests = new List<QuestState>();
                return SaveSystem.Records.quests;
            }
        }

        static void Save() { if (!Testing) SaveSystem.SaveRecords(); }

        /// <summary>The game's quests. Starts whatever is due to start (not while tests run: they start what they test).</summary>
        public static void Use(QuestCatalog all, bool refresh = true)
        {
            catalog = all;
            if (refresh && !Testing) Refresh();
        }

        /// <summary>Forgets everything (tests).</summary>
        public static void ResetForTests() { memory.Clear(); Changed?.Invoke(); }

        public static QuestState StateOf(string id)
        {
            foreach (var s in States) if (s.id == id) return s;
            return null;
        }

        public static bool IsDone(string id) { var s = StateOf(id); return s != null && s.done; }
        public static bool IsActive(string id) { var s = StateOf(id); return s != null && !s.done; }

        static bool Ready(QuestDef q)
        {
            if (q.requires != null) foreach (var r in q.requires) if (!string.IsNullOrEmpty(r) && !IsDone(r)) return false;
            return true;
        }

        /// <summary>Starts every quest that starts by itself and whose turn has come.</summary>
        public static void Refresh()
        {
            if (catalog == null) return;
            bool any = false;
            foreach (var q in catalog.quests)
            {
                if (q == null || string.IsNullOrEmpty(q.id) || StateOf(q.id) != null) continue;
                if (!string.IsNullOrEmpty(q.doneIfKeyItem) && Villagers.HasKey(q.doneIfKeyItem))
                {
                    States.Add(new QuestState { id = q.id, done = true });        // (done before the quest existed)
                    any = true;
                    continue;
                }
                if (q.autoStart && Ready(q)) { Begin(q); any = true; }
            }
            if (any) { Save(); Changed?.Invoke(); }
        }

        /// <summary>Starts a quest that does not start by itself. False if it is unknown, open already, or done.</summary>
        public static bool Start(string id)
        {
            var q = catalog != null ? catalog.Find(id) : null;
            if (q == null || StateOf(id) != null) return false;
            Begin(q);
            Save(); Changed?.Invoke();
            return true;
        }

        static void Begin(QuestDef q)
        {
            States.Add(new QuestState { id = q.id });
            Say($"New task:  {q.title}");
        }

        static void Say(string text)
        {
            var d = GameDirector.Instance;
            if (d != null && d.hud != null) d.hud.ShowMessage(text, 5f);
        }

        /// <summary>The open quests, oldest first.</summary>
        public static IEnumerable<QuestDef> Active
        {
            get
            {
                if (catalog == null) yield break;
                foreach (var s in States)
                {
                    if (s.done) continue;
                    var q = catalog.Find(s.id);
                    if (q != null) yield return q;
                }
            }
        }

        /// <summary>The quest the screen shows: the oldest one open.</summary>
        public static QuestDef Tracked { get { foreach (var q in Active) return q; return null; } }

        /// <summary>The step a quest is at (null when it is not open).</summary>
        public static QuestStep StepOf(QuestDef q)
        {
            var s = q != null ? StateOf(q.id) : null;
            if (s == null || s.done || q.steps == null || s.step < 0 || s.step >= q.steps.Length) return null;
            return q.steps[s.step];
        }

        /// <summary>What the screen says for a quest: its step's words, with the count where there is one ("Kill snakes  2 / 5").</summary>
        public static string StepText(QuestDef q)
        {
            var st = StepOf(q);
            if (st == null) return "";
            var s = StateOf(q.id);
            return st.count > 1 ? $"{st.text}  {Mathf.Min(s.progress, st.count)} / {st.count}" : st.text;
        }

        /// <summary>
        /// Something has happened in the game. Every open quest whose step waits for exactly that moves on - and a
        /// quest that runs out of steps is finished: its rewards are handed over, and whatever it was holding back starts.
        /// </summary>
        public static void Notify(string trigger, string target, int amount = 1)
        {
            if (catalog == null || string.IsNullOrEmpty(trigger)) return;
            bool any = false;
            var open = new List<QuestDef>(Active);
            foreach (var q in open)
            {
                var st = StepOf(q);
                if (st == null || st.trigger != trigger) continue;
                if (!string.IsNullOrEmpty(st.target) && st.target != target) continue;
                var s = StateOf(q.id);
                s.progress += Mathf.Max(1, amount);
                any = true;
                if (s.progress < Mathf.Max(1, st.count)) continue;
                s.step++; s.progress = 0;
                if (s.step >= q.steps.Length) Finish(q, s);
            }
            if (!any) return;
            Refresh();
            Save();
            Changed?.Invoke();
        }

        static void Finish(QuestDef q, QuestState s)
        {
            s.done = true;
            Say($"Task done:  {q.title}");
            if (q.rewards == null) return;
            var d = GameDirector.Instance;
            var hud = d != null ? d.hud : null;
            foreach (var r in q.rewards)
            {
                if (r == null) continue;
                switch (r.kind)
                {
                    case RewardKind.KeyItem: Villagers.GiveKey(r.id); break;
                    case RewardKind.Embers: Villagers.AddEmbers(r.amount); break;
                    case RewardKind.Gold: if (RunSession.Current != null) RunSession.Current.AddResource(Res.Gold, r.amount); break;
                    case RewardKind.Gear: if (RunSession.Current != null && !RunSession.Current.gear.Contains(r.id)) RunSession.Current.gear.Add(r.id); break;
                }
                // every reward is shown the way anything new is: its card (HUD.Gain)
                if (hud != null && !string.IsNullOrEmpty(r.displayName))
                    hud.Gain("reward:" + q.id + ":" + r.id, r.displayName, r.icon, Mathf.Max(1, r.amount), r.description, true);
            }
        }

        /// <summary>
        /// Is somebody an open quest wants spoken to? Then 'step' has what they say; when they have been heard out,
        /// tell the log (Notify(QuestTrigger.Talk, who)).
        /// </summary>
        public static bool TalkStep(string who, out QuestStep step)
        {
            step = null;
            foreach (var q in Active)
            {
                var st = StepOf(q);
                if (st != null && st.trigger == QuestTrigger.Talk && st.target == who && !string.IsNullOrEmpty(st.lines)) { step = st; return true; }
            }
            return false;
        }

        /// <summary>Does an open quest keep the village gate shut? 'why' is what the gate says.</summary>
        public static bool GateHeld(out string why)
        {
            why = null;
            foreach (var q in Active)
                if (q.holdsVillageGate) { why = string.IsNullOrEmpty(q.gateMessage) ? StepText(q) : q.gateMessage; return true; }
            return false;
        }

        /// <summary>
        /// Where the tracked quest's step points in this level: the nearest thing that goes by one of the names in
        /// its 'waypoint' (IQuestTarget.QuestId). False when it points at nothing here.
        /// </summary>
        public static bool Waypoint(World w, Vector2 from, out Vector2 at)
        {
            at = default;
            var st = StepOf(Tracked);
            if (w == null || st == null || string.IsNullOrEmpty(st.waypoint)) return false;
            var names = st.waypoint.Split(';');
            float best = float.MaxValue;
            foreach (var e in w.Entities)
            {
                if (e == null || e.Dead || !(e is IQuestTarget t)) continue;
                string id = t.QuestId;
                bool wanted = false;
                foreach (var n in names) if (n.Trim() == id) { wanted = true; break; }
                if (!wanted) continue;
                float d = (e.Center - from).sqrMagnitude;
                if (d < best) { best = d; at = new Vector2(e.pos.x, e.pos.y + e.size.y + 0.5f); }
            }
            return best < float.MaxValue;
        }
    }
}
