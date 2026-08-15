// /Game/Systems/AchievementSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Snake2000.Core;

namespace Snake2000.Systems
{
    public class AchievementSystem
    {
        private readonly Dictionary<string, Achievement> _achievements = new();
        private readonly HashSet<string> _unlocked = new();
        private readonly IEventBus _eventBus;

        public event Action<Achievement> OnUnlocked;

        public AchievementSystem(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _achievements["first_kill"] = new Achievement("First Blood", "Kill your first mini-boss.", "trophy");
            _achievements["speed_demon"] = new Achievement("Speed Demon", "Reach 1000 points in under 30 seconds.", "lightning");
            _achievements["chaos_master"] = new Achievement("Chaos Master", "Complete a level in Chaos Mode.", "skull");

            // Ecoute l'événement de fin de jeu pour potentiellement déclencher des achievements
            _eventBus.Subscribe<PlayerDiedEvent>(e => CheckCondition("lives_lost", e.LivesLeft));
        }

        public void CheckCondition(string conditionId, object value)
        {
            switch (conditionId)
            {
                case "score_reached":
                    if ((int)value >= 1000 && !_unlocked.Contains("speed_demon"))
                        Unlock("speed_demon");
                    break;
                case "boss_killed":
                    if (!_unlocked.Contains("first_kill"))
                        Unlock("first_kill");
                    break;
                case "chaos_completed":
                    if (!_unlocked.Contains("chaos_master"))
                        Unlock("chaos_master");
                    break;
                case "lives_lost":
                    if ((int)value <= 0) // Si le joueur est mort
                        CheckCondition("game_over", null);
                    break;
            }
        }

        private void Unlock(string id)
        {
            if (_achievements.TryGetValue(id, out var ach) && !_unlocked.Contains(id))
            {
                _unlocked.Add(id);
                _eventBus.Publish(new AchievementUnlockedEvent { Id = id, Name = ach.Name });
                OnUnlocked?.Invoke(ach);
            }
        }

        public IEnumerable<string> GetUnlocked() => _unlocked;

        public class Achievement
        {
            public string Name { get; }
            public string Description { get; }
            public string Icon { get; }
            public Achievement(string name, string desc, string icon) => (Name, Description, Icon) = (name, desc, icon);
        }
    }
}