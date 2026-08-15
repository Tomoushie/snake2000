// /Game/Systems/GameStateManager.cs
using System;
using System.Drawing;
using System.Windows.Forms;
using Snake2000.Gameplay;

namespace Snake2000.Gameplay.Systems
{
    public class GameStateManager
    {
        public GameState State { get; private set; } = GameState.Ready;
        private readonly IGame _game;

        // Callbacks pour les transitions d'état
        public Action<GameState> OnEnterState;
        public Action<GameState> OnExitState;

        public GameStateManager(IGame game)
        {
            _game = game;
        }

        public void TransitionTo(GameState newState)
        {
            if (State != newState)
            {
                OnExitState?.Invoke(State); // Appel du callback de sortie

                // Logique de transition possible ici (effets, sons, etc.)
                State = newState;

                OnEnterState?.Invoke(State); // Appel du callback d'entrée

                switch (newState)
                {
                    case GameState.Playing:
                        _game.ResumeGame();
                        break;
                    case GameState.Paused:
                        _game.PauseGame();
                        break;
                    case GameState.GameOver:
                        _game.StopGame();
                        break;
                    // D'autres transitions peuvent être ajoutées
                }
            }
        }

        public void HandleInput(KeyEventArgs e)
        {
            switch (State)
            {
                case GameState.Ready:
                    if (e.KeyCode == Keys.Space) TransitionTo(GameState.Playing);
                    break;
                case GameState.Playing:
                    if (e.KeyCode == Keys.Escape) TransitionTo(GameState.Paused);
                    break;
                case GameState.Paused:
                    if (e.KeyCode == Keys.Enter) TransitionTo(GameState.Playing);
                    else if (e.KeyCode == Keys.Escape) TransitionTo(GameState.GameOver);
                    break;
                // D'autres états peuvent gérer l'input différemment
            }
        }
    }
}