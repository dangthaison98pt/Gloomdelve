using System.Collections;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// A spirit bound to the spot where it died (Story): one in each of the three levels of the run's fourth area,
    /// once the old signs have been read. Freed, it rises and is gone. When the third is freed all three come
    /// back together and hand over the heart of the hammer.
    /// </summary>
    public class Ghost : Entity, IInteractable
    {
        [Tooltip("Picture of the hammer's heart (its item card).")] public Sprite coreIcon;
        [Tooltip("The chain that holds it (a child object): gone when it is freed.")] public SpriteRenderer chain;

        public static readonly string[] Thanks = { "Thank you.", "We could not leave.", "Take it. End the day." };
        static readonly Color Pale = new Color(0.72f, 0.92f, 1f);

        bool freed;
        float t, rise;
        Vector2 home;

        public bool Freed => freed;

        public override void OnSpawn()
        {
            base.OnSpawn();
            home = pos;
        }

        public string GetPrompt(PlayerController p) => freed ? null : "[enter]  Free the spirit";

        public void Interact(PlayerController p)
        {
            if (freed) return;
            freed = true;
            var run = RunSession.Current;
            int count = 0;
            if (run != null)
            {
                run.ghostMask |= 1 << Story.LevelOf(run.stageIndex);
                for (int i = 0; i < 3; i++) if ((run.ghostMask & (1 << i)) != 0) count++;
            }
            if (chain != null) chain.enabled = false;
            Fx.Sparks(Center, Pale, 14, 4f);
            Sfx.PlayUI("buy");
            Fx.Text(new Vector2(pos.x, pos.y + size.y + 0.4f), Thanks[0], Pale, 1.6f);
            var dir = GameDirector.Instance;
            if (count >= 3 && dir != null) dir.StartCoroutine(Gift(dir, sr != null ? sr.sprite : null));
            else if (dir != null && dir.hud != null)
                dir.hud.ShowBanner("A spirit is free", count == 1 ? "Two more are bound, further up." : $"{3 - count} more is bound, further up.", 4f);
        }

        public override void Tick(float dt)
        {
            t += dt;
            // bound, it sways on its chain; freed, it rises and thins out
            if (!freed) { pos = home + new Vector2(Mathf.Sin(t * 1.3f) * 0.12f, 0.25f + Mathf.Sin(t * 2.1f) * 0.1f); return; }
            rise += dt;
            pos += new Vector2(0f, dt * (0.8f + rise));
            if (sr != null) { var c = sr.color; c.a = Mathf.Clamp01(1f - rise / 1.8f); sr.color = c; }
            if (rise > 1.8f) Despawn();
        }

        /// <summary>The three, together again: they circle in on the player, say their piece, and the heart of the hammer is in the bag.</summary>
        static IEnumerator Gift(GameDirector dir, Sprite look)
        {
            yield return new WaitForSeconds(1.2f);
            var p = dir.Player;
            if (p == null || look == null) { Story.GotPart(RunSession.Current, dir.config, "hammercore"); yield break; }
            var three = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("GhostGift");
                three[i] = go.AddComponent<SpriteRenderer>();
                three[i].sprite = look;
                three[i].sortingOrder = 60;
                three[i].color = new Color(1f, 1f, 1f, 0f);
            }
            const float turn = 3.4f;
            int said = 0;
            for (float s = 0f; s < turn; s += Time.deltaTime)
            {
                float k = s / turn, r = Mathf.Lerp(3.2f, 0.9f, k * k * (3f - 2f * k));
                Vector2 c = p.Center + new Vector2(0f, 0.5f);
                for (int i = 0; i < 3; i++)
                {
                    float a = s * 2.2f + i * Mathf.PI * 2f / 3f;
                    three[i].transform.position = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.55f);
                    three[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 4f) * 0.9f);
                }
                if (said < Thanks.Length && s > 0.4f + said * 0.9f)
                {
                    Fx.Text(three[said].transform.position + new Vector3(0f, 0.9f, 0f), Thanks[said], Pale, 1.5f);
                    said++;
                }
                yield return null;
            }
            Fx.Sparks(p.Center + new Vector2(0f, 0.6f), Pale, 24, 5f);
            LevelLighting.Flash(p.Center, Pale, 6f, 1.6f, 0.6f);
            Story.GotPart(RunSession.Current, dir.config, "hammercore");
            for (float s = 0f; s < 1.2f; s += Time.deltaTime)
            {
                for (int i = 0; i < 3; i++)
                {
                    three[i].transform.position += new Vector3(0f, Time.deltaTime * 1.6f, 0f);
                    three[i].color = new Color(1f, 1f, 1f, 0.9f * (1f - s / 1.2f));
                }
                yield return null;
            }
            foreach (var g in three) if (g != null) Destroy(g.gameObject);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage) { }
        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
