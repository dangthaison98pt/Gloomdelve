using System.Collections.Generic;
using UnityEngine;

namespace Gloomdelve
{
    /// <summary>
    /// Every quest the game has (GameConfig.quests). The project builder fills it with whatever QuestDef assets lie
    /// in Data/Quests, so adding a quest is adding an asset there.
    /// </summary>
    public class QuestCatalog : ScriptableObject
    {
        public List<QuestDef> quests = new List<QuestDef>();

        public QuestDef Find(string id)
        {
            foreach (var q in quests) if (q != null && q.id == id) return q;
            return null;
        }
    }
}
