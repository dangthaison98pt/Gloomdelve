using System;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>What a quest hands over when it is done.</summary>
    public enum RewardKind { KeyItem, Embers, Gear, Gold }

    /// <summary>
    /// One thing to do. The game tells the quest log what happens ("talk" to "elder", "enter" the "hall", "kill" a
    /// "snake" - QuestLog.Notify); a step is done when its trigger has happened to its target 'count' times.
    /// Nothing about a step is known to the code: a new kind of step is a new pair of words.
    /// </summary>
    [Serializable]
    public class QuestStep
    {
        [Tooltip("What the screen says the player should do.")] public string text = "";
        [Tooltip("What has to happen: talk, enter, obtain, kill, free ... (QuestTrigger has the ones the game reports).")] public string trigger = QuestTrigger.Talk;
        [Tooltip("To whom or what: the id the thing reports itself by (IQuestTarget.QuestId, a creature's name, an item's id). Empty = anything.")] public string target = "";
        [Tooltip("How many times.")] public int count = 1;
        [Tooltip("Where the marker on the screen points: the QuestId of something in the level. Several, separated by ';': the nearest one counts (the elder; or, from the street, the door of his hall).")]
        public string waypoint = "";
        [Tooltip("For a 'talk' step: what the one spoken to says. The step is done when they have been heard out.")]
        [TextArea(2, 8)] public string lines = "";
    }

    [Serializable]
    public class QuestReward
    {
        public RewardKind kind = RewardKind.KeyItem;
        [Tooltip("Key item / gear id (not used for embers and gold).")] public string id = "";
        public int amount = 1;
        [Tooltip("How the item card names it, and what it says.")] public string displayName = "";
        [TextArea(2, 6)] public string description = "";
        public Sprite icon;
    }

    /// <summary>
    /// A quest: a name, steps in order, and what is handed over at the end. One asset each (Data/Quests): a new
    /// quest is a new asset, and the game picks it up from the catalogue (QuestCatalog) - nothing to write in code.
    /// QuestLog keeps what the player has done of each.
    /// </summary>
    [CreateAssetMenu(menuName = "Gloomdelve/Quest", fileName = "Quest_New")]
    public class QuestDef : ScriptableObject
    {
        [Tooltip("What the save knows it by. Never change it once players have it.")] public string id = "";
        public string title = "";
        [TextArea(2, 5)] public string summary = "";
        [Tooltip("Starts by itself as soon as what it requires is done (otherwise something has to start it: QuestLog.Start).")] public bool autoStart = true;
        [Tooltip("Quests (ids) that have to be done first.")] public string[] requires = new string[0];
        [Tooltip("A save that already has this key item counts the quest as done (saves from before the quest existed).")] public string doneIfKeyItem = "";
        [Tooltip("The village gate stays shut while this quest is open ...")] public bool holdsVillageGate;
        [Tooltip("... and this is what the gate says.")] public string gateMessage = "";
        public QuestStep[] steps = new QuestStep[0];
        public QuestReward[] rewards = new QuestReward[0];
    }
}
