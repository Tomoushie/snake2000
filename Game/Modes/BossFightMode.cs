// /Game/Modes/BossFightMode.cs
using Snake2000.Gameplay;
using Snake2000.Entities;

namespace Snake2000.Modes
{
    public class BossFightMode : IGameMode
    {
        public string Name => "Boss Fight";
        public GameMode Type => GameMode.BossFight;
        private BossEntity _boss;

        public void Initialize()
        {
            // Le boss est créé par SnakeGame, ici on ne fait que l'initialiser
        }

        public void Start()
        {
            // Le boss est déjà instancié dans SnakeGame
        }

        public void Update()
        {
            _boss?.Update();
        }

        public void End()
        {
            _boss = null;
        }

        public void SetBoss(BossEntity boss) => _boss = boss;
    }
}