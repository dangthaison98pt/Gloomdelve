using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Something in a room to read - a note, a book, a letter, a chest with a scrap of paper in it. It is a thing you
    /// can see where it lies (on a table, a bed, a mantel, pinned to the wall); standing by it outlines it and offers
    /// "Read" (or "Open" for a chest that has not been opened yet), and Enter shows what is written. The houses of
    /// Brindlemoor have two or three each (VillageInterior puts them out): together they say a good deal about the
    /// people who live there, and about a winter that is not quite an ordinary winter.
    /// </summary>
    public class Curio : Entity, IInteractable
    {
        /// <summary>What it is. (A chest is drawn closed until it is opened, then open.)</summary>
        public enum Form { Note, Pinned, Book, Letter, Chest }

        public string label = "this";
        [TextArea] public string text = "";
        public Form form = Form.Note;

        [Tooltip("By Form, then the open chest last: note, pinned note, book, letter, chest (closed), chest (open).")] public Sprite[] looks;
        [Tooltip("The white outline of each look (same order): shown while the player is in reach.")] public Sprite[] outlines;
        [Tooltip("The outline's renderer (a child, just in front).")] public SpriteRenderer glow;

        /// <summary>Chests opened this session (by what they are called): they stay open.</summary>
        static readonly HashSet<string> opened = new HashSet<string>();

        /// <summary>How high it lies above the floor, in pixels of the room picture (on a table: 15; on the mantel: 46).</summary>
        int lift;

        public bool IsOpen => form == Form.Chest && opened.Contains(label);

        public override void Configure(SpawnPoint sp)
        {
            var at = pos;
            base.Configure(sp);
            pos = at;                       // just where the room put it (not the middle of a cell)
            facing = 1;
            form = (Form)Mathf.Clamp(sp.param, 0, 4);
            lift = sp.dir;
            if (string.IsNullOrEmpty(sp.text)) return;
            int cut = sp.text.IndexOf('|');
            if (cut < 0) { text = sp.text; return; }
            label = sp.text.Substring(0, cut);
            text = sp.text.Substring(cut + 1);
        }

        int Look => form == Form.Chest && IsOpen ? 5 : (int)form;

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (sr != null)
            {
                sr.transform.localPosition = new Vector3(0f, lift / 16f, 0f);
                sr.sprite = looks != null && Look < looks.Length ? looks[Look] : null;
            }
            if (glow != null) { glow.transform.localPosition = new Vector3(0f, lift / 16f, 0f); glow.enabled = false; }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (sr != null && looks != null && Look < looks.Length) sr.sprite = looks[Look];
            InReach.Show(glow, outlines != null && Look < outlines.Length ? outlines[Look] : null, this);
        }

        public string GetPrompt(PlayerController p) => form == Form.Chest && !IsOpen ? "[enter]  Open" : "[enter]  Read";

        public void Interact(PlayerController p)
        {
            var dir = GameDirector.Instance;
            if (form == Form.Chest && !IsOpen)
            {
                // the lid comes up; the scrap inside is read at once
                opened.Add(label);
                Sfx.Play("door", pos, 0.6f, 0.8f);
                Fx.Dust(new Vector2(pos.x, pos.y + lift / 16f + 0.8f), 4);
            }
            else Sfx.PlayUI("pickup", 0.6f);          // a page turned
            if (dir == null || dir.hud == null) return;
            dir.hud.ShowNote(text, HUD.ReadTime(text));      // read the way a sign is read
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }

    /// <summary>
    /// The outline that tells the player a thing in a room can be used: drawn round it while it is the one the player
    /// would use (in reach, the nearest), a soft white that breathes. Nothing marks it from afar.
    /// </summary>
    public static class InReach
    {
        public static void Show(SpriteRenderer glow, Sprite outline, Entity who)
        {
            if (glow == null) return;
            var p = who != null && who.World != null ? who.World.Player : null;
            bool on = outline != null && p != null && !p.IsDead && ReferenceEquals(p.Focus, who);
            glow.enabled = on;
            if (!on) return;
            glow.sprite = outline;
            glow.color = new Color(1f, 0.97f, 0.86f, 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 3f)));
        }
    }
}
