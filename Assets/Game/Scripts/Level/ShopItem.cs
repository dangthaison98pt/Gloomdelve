using UnityEngine;

namespace Gloomdelve
{
    /// <summary>An item on display in a shop. Stand on it and press A / Up to buy.</summary>
    public class ShopItem : Entity, IInteractable
    {
        public PixelText priceTag;
        public SpriteRenderer icon;

        [System.NonSerialized] public ShopOffer offer;
        [System.NonSerialized] public Shopkeeper keeper;
        float bob;
        int slot;
        bool shownFree;

        public bool UpTriggers => true;
        bool Free => keeper == null || keeper.Dead || keeper.angry;

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            slot = sp.param;
            var shop = World.config.shop;
            offer = shop != null ? shop.Find(sp.text) : null;
            var run = RunSession.Current;
            if (offer != null && offer.isUpgrade && run != null && run.HasUpgrade(offer.effect) && shop != null)
            {
                // Already owned: offer a different upgrade, or bombs.
                ShopOffer alt = null;
                foreach (var o in shop.offers) if (o.isUpgrade && !run.HasUpgrade(o.effect)) { alt = o; break; }
                offer = alt ?? shop.Find("bombs");
            }
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            if (offer == null) { Despawn(); return; }
            displayName = offer.displayName;
            if (icon != null) icon.sprite = offer.icon;
            // Neighbouring items stand in adjacent cells: stagger the price tags so they never touch.
            if (priceTag != null) priceTag.transform.localPosition = new Vector3(0f, 1.05f + (slot % 2) * 0.42f, 0f);
            RefreshTag();
        }

        void RefreshTag()
        {
            if (priceTag == null || offer == null) return;
            shownFree = Free;
            if (Free) priceTag.Set("FREE", new Color(1f, 0.4f, 0.4f));
            else priceTag.Set("$" + offer.price, new Color(1f, 0.85f, 0.35f));
        }

        public override void Tick(float dt)
        {
            bob += dt;
            if (icon != null) icon.transform.localPosition = new Vector3(0f, 0.12f + Mathf.Sin(bob * 3f) * 0.05f, 0f);
            if (Free != shownFree) RefreshTag();   // keeper linked, angered or killed
        }

        public string GetPrompt(PlayerController p)
        {
            if (offer == null) return null;
            string d = string.IsNullOrEmpty(offer.description) ? "" : "  -  " + offer.description;
            return Free ? $"A / UP : take {offer.displayName} (free!){d}" : $"A / UP : buy {offer.displayName} for ${offer.price}{d}";
        }

        public void Interact(PlayerController p)
        {
            var run = RunSession.Current;
            if (offer == null || run == null || Dead) return;
            if (!Free)
            {
                if (run.gold < offer.price)
                {
                    Sfx.Play("error", pos);
                    Fx.Text(Center + Vector2.up * 0.8f, "NOT ENOUGH GOLD", new Color(1f, 0.4f, 0.4f));
                    return;
                }
                run.gold -= offer.price;
            }
            Apply(run, offer);
            run.purchases.Add(offer.id);
            Sfx.Play("buy", pos);
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.4f), 10, 4f);
            Fx.Text(Center + Vector2.up * 0.8f, offer.displayName.ToUpperInvariant(), new Color(0.6f, 1f, 0.6f), 1.4f);
            if (GameDirector.Instance != null && offer.isUpgrade) GameDirector.Instance.ShowMessage($"{offer.displayName}: {offer.description}");
            Despawn();
        }

        public static void Apply(RunState run, ShopOffer o)
        {
            switch (o.effect)
            {
                case ShopEffect.Bombs: run.bombs += o.amount; break;
                case ShopEffect.Ropes: run.ropes += o.amount; break;
                case ShopEffect.Health: run.health += o.amount; break;
                case ShopEffect.SpringBoots: run.springBoots = true; break;
                case ShopEffect.IronWhip: run.ironWhip = true; break;
                case ShopEffect.Compass: run.compass = true; break;
                case ShopEffect.BlastPowder: run.blastPowder = true; break;
            }
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            if (keeper != null) keeper.Strike();   // blasting his wares counts as a hit (warnings first)
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
