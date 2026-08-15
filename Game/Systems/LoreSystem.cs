// /Game/Systems/LoreSystem.cs
using System.Collections.Generic;
using Snake2000.Gameplay;

namespace Snake2000.Systems
{
    public class LoreSystem
    {
        private readonly Dictionary<GameMode, List<string>> _modeLore = new()
        {
            { GameMode.Solo, new List<string> { "The lone serpent ventures into the void.", "Seek the fruit of power.", "Survive the endless hunger." } },
            { GameMode.BossFight, new List<string> { "A legendary guardian blocks your path.", "Its roar shakes the digital realm.", "Only the worthy shall pass." } },
            { GameMode.Chaos, new List<string> { "Reality bends and breaks.", "The laws of physics are optional here.", "Chaos is the only constant." } }
            // Ajouter d'autres modes
        };

        public void DisplayIntro(GameMode mode)
        {
            if (_modeLore.TryGetValue(mode, out var loreLines))
            {
                foreach (var line in loreLines)
                {
                    System.Console.WriteLine($"[LORE] {line}"); // Remplacer par un système de dialogue ou UI
                }
            }
        }
    }
}