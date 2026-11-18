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
        bool shownFree, shelfChecked;

        public bool UpTriggers => true;
        bool villageWare;
        int priceOverride;
        bool Free => !villageWare && (keeper == null || keeper.Dead || keeper.angry);
        int Price => priceOverride > 0 ? priceOverride : offer.price;

        /// <summary>A ware set out by a villager in Brindlemoor: never free, sold at this price, paid from the gold the player carries.</summary>
        public void SetVillageWare(int price)
        {
            villageWare = true;
            priceOverride = price;
            RefreshTag();
        }

        public override void Configure(SpawnPoint sp)
        {
            base.Configure(sp);
            slot = sp.param;
            var shop = World.config.shop;
            offer = shop != null ? shop.Find(sp.text) : null;
            // Equipment is bought once per run: an owned piece is never put on display again.
            if (Owned(offer)) offer = Replacement();
        }

        static bool Owned(ShopOffer o)
        {
            var run = RunSession.Current;
            return o != null && o.isUpgrade && run != null && run.HasUpgrade(o.id);
        }

        /// <summary>Another piece of equipment that is neither owned nor already on display here, else bombs
        /// (supplies - bombs, ropes, hearts - can be bought again and again).</summary>
        ShopOffer Replacement()
        {
            var shop = World.config.shop;
            if (shop == null) return null;
            foreach (var o in shop.offers)
                if (o.isUpgrade && !Owned(o) && !OnDisplayElsewhere(o.id)) return o;
            return shop.Find("bombs");
        }

        bool OnDisplayElsewhere(string id)
        {
            foreach (var e in World.Entities)
                if (e is ShopItem s && s != this && !s.Dead && s.offer != null && s.offer.id == id) return true;
            return false;
        }

        /// <summary>True when an item earlier on the same keeper's shelf shows the same piece of equipment.</summary>
        bool DuplicateOnShelf()
        {
            foreach (var e in World.Entities)
            {
                if (e == this) return false;
                if (e is ShopItem s && !s.Dead && s.keeper == keeper && s.offer != null && s.offer.id == offer.id) return true;
            }
            return false;
        }

        void Show(ShopOffer o)
        {
            offer = o;
            if (offer == null) { Despawn(); return; }
            displayName = offer.displayName;
            if (icon != null) icon.sprite = offer.icon;
            RefreshTag();
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
            else priceTag.Set("$" + Price, new Color(1f, 0.85f, 0.35f));
        }

        public override void Tick(float dt)
        {
            bob += dt;
            if (icon != null) icon.transform.localPosition = new Vector3(0f, 0.12f + Mathf.Sin(bob * 3f) * 0.05f, 0f);
            if (Free != shownFree) RefreshTag();   // keeper linked, angered or killed
            if (offer == null || !offer.isUpgrade) return;
            // Got this piece somewhere else meanwhile (a chest, the same item on another shelf): it is gone from here.
            if (Owned(offer)) { Despawn(); return; }
            if (keeper != null && !shelfChecked)
            {
                shelfChecked = true;
                if (DuplicateOnShelf()) Show(Replacement());   // one shelf never shows the same equipment twice
            }
        }

        public string GetPrompt(PlayerController p)
        {
            if (offer == null) return null;
            string d = string.IsNullOrEmpty(offer.description) ? "" : "  -  " + offer.description;
            return Free ? $"[enter] / [up]  take {offer.displayName} (free!){d}" : $"[enter] / [up]  buy {offer.displayName} for ${Price}{d}";
        }

        public void Interact(PlayerController p)
        {
            var run = RunSession.Current;
            if (offer == null || run == null || Dead) return;
            if (Owned(offer)) { Despawn(); return; }   // never charge for equipment the run already has
            if (offer.resource == Res.Health && run.HealthFull)
            {
                Sfx.Play("error", pos);
                Fx.Text(Center + Vector2.up * 0.8f, "HEALTH IS FULL", new Color(1f, 0.4f, 0.4f));
                return;
            }
            if (!Free)
            {
                if (run.gold < Price)
                {
                    Sfx.Play("error", pos);
                    Fx.Text(Center + Vector2.up * 0.8f, "NOT ENOUGH GOLD", new Color(1f, 0.4f, 0.4f));
                    return;
                }
                run.gold -= Price;
                if (villageWare) Villagers.BankGold(run);       // spent gold is gone from the pouch too
            }
            Apply(run, offer);
            run.purchases.Add(offer.id);
            Sfx.Play("buy", pos);
            Fx.Sparks(Center, new Color(1f, 0.9f, 0.4f), 10, 4f);
            Fx.Text(Center + Vector2.up * 0.8f, offer.displayName.ToUpperInvariant(), new Color(0.6f, 1f, 0.6f), 1.4f);
            if (GameDirector.Instance != null && offer.isUpgrade)
                GameDirector.Instance.ShowMessage(run.IsWorn(offer.id)
                    ? $"{offer.displayName}: {offer.description}"
                    : $"{offer.displayName} is in your bag ({InventoryScreen.SlotLabel(offer.slot).ToLowerInvariant()} slot is taken). Press [inventory] to wear it.");
            Despawn();
        }

        /// <summary>Gives the offer to the run: its resource (if any) and, for upgrades, ownership + stat modifiers.</summary>
        public static void Apply(RunState run, ShopOffer o)
        {
            if (o.id == "vigor") { run.maxHealth += o.amount; run.health += o.amount; return; }       // the Healer's draught
            if (o.id == "lampoil") { run.extraTime += o.amount; return; }                              // the Lamplighter's oil: seconds
            if (!string.IsNullOrEmpty(o.resource)) run.AddResource(o.resource, o.amount);
            if (o.isUpgrade) run.AddUpgrade(o);
        }

        public override void OnExplosion(Vector2 center, float radius, Entity source, int generation, int damage)
        {
            if (keeper != null) keeper.Strike();   // blasting his wares counts as a hit (warnings first)
        }

        public override void OnWhipped(PlayerController p, int damage, int dir) { }
    }
}
