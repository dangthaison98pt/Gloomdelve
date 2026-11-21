using UnityEngine;
using UnityEngine.InputSystem;

namespace Gloomdelve
{
    /// <summary>
    /// Hidden help for whoever has to play a whole run through to test it (design, QA). Nothing on any screen says
    /// it exists. It is switched on by a code, during play:
    ///   keyboard: type  G D T E S T
    ///   gamepad:  up up down down left right left right  on the d-pad
    /// (the same code switches it off). While it is on:
    ///   F1 / Select + d-pad up     nothing can hurt (on / off; stays on from level to level)
    ///   F2 / Select + d-pad right  this level is done: on to the next (in the last hall: the run is won)
    ///   F3 / Select + d-pad down   the boss of this level falls
    ///   F4 / Select + d-pad left   full health, full bags, and $1000
    /// It lasts until the game is closed. Tests never see it.
    /// </summary>
    public static class Cheats
    {
        public static bool On { get; private set; }
        /// <summary>Nothing can hurt the player (kept from level to level: GameDirector applies it to each new player).</summary>
        public static bool God { get; private set; }

        static readonly Key[] Code = { Key.G, Key.D, Key.T, Key.E, Key.S, Key.T };
        static readonly int[] PadCode = { 0, 0, 1, 1, 2, 3, 2, 3 };       // up, down, left, right = 0..3
        static int typed, padAt;

        public static void Tick(GameDirector d)
        {
            if (d == null || ScenarioRunner.Active || AutoTest.Active) return;
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame)
            {
                if (kb[Code[typed]].wasPressedThisFrame) { if (++typed == Code.Length) { typed = 0; Toggle(d); } }
                else typed = kb[Code[0]].wasPressedThisFrame ? 1 : 0;
            }
            int pad = -1;
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) pad = 0;
                else if (gp.dpad.down.wasPressedThisFrame) pad = 1;
                else if (gp.dpad.left.wasPressedThisFrame) pad = 2;
                else if (gp.dpad.right.wasPressedThisFrame) pad = 3;
            }
            bool select = gp != null && gp.selectButton.isPressed;
            if (pad >= 0 && !select)
            {
                if (pad == PadCode[padAt]) { if (++padAt == PadCode.Length) { padAt = 0; Toggle(d); } }
                else padAt = pad == PadCode[0] ? 1 : 0;
            }
            if (!On) return;
            if ((kb != null && kb.f1Key.wasPressedThisFrame) || (select && pad == 0))
            {
                God = !God;
                if (d.Player != null) d.Player.godMode = God;
                d.ShowMessage(God ? "GD: nothing can hurt you." : "GD: you can be hurt again.");
            }
            else if ((kb != null && kb.f2Key.wasPressedThisFrame) || (select && pad == 3)) d.CheatSkipLevel();
            else if ((kb != null && kb.f3Key.wasPressedThisFrame) || (select && pad == 1)) d.CheatKillBoss();
            else if ((kb != null && kb.f4Key.wasPressedThisFrame) || (select && pad == 2))
            {
                var run = RunSession.Current;
                if (run == null) return;
                run.health = run.maxHealth;
                run.AddResource(Res.Bombs, 99); run.AddResource(Res.Ropes, 99); run.AddResource(Res.Pebbles, 99);
                run.AddResource(Res.Gold, 1000);
                d.ShowMessage("GD: full health, full bags, $1000.");
            }
        }

        static void Toggle(GameDirector d)
        {
            On = !On;
            if (!On) { God = false; if (d.Player != null) d.Player.godMode = false; }
            Sfx.PlayUI(On ? "idol" : "error");
            d.ShowMessage(On ? "GD mode ON.  F1 no damage   F2 next level   F3 kill boss   F4 refill + $1000" : "GD mode off.");
        }
    }
}
