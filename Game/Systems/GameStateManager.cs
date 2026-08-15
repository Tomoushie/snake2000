// /Game/Systems/GameStateManager.cs
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Snake2000.Gameplay;
using Snake2000.Core;

namespace Snake2000.Systems
{
    public class GameStateManager
    {
        public GameState State { get; private set; } = GameState.Ready;
        private readonly IGame _game;

        // Callbacks pour les transitions d'état
        public Action<GameState> OnEnterState;
        public Action<GameState> OnExitState;

        private readonly Dictionary<GameState, Action<Keys>> _inputHandlers;

        public GameStateManager(IGame game)
        {
            _game = game;
            _inputHandlers = new Dictionary<GameState, Action<Keys>>
            {
                { GameState.Ready, HandleInputReady },
                { GameState.Playing, HandleInputPlaying },
                { GameState.Paused, HandleInputPaused },
                { GameState.GameOver, HandleInputGameOver }
            };
        }

        public void TransitionTo(GameState newState)
        {
            if (State != newState)
            {
                OnExitState?.Invoke(State); // Appel du callback de sortie

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
                }
            }
        }

        public void HandleInput(KeyEventArgs e)
        {
            if (_inputHandlers.TryGetValue(State, out var handler))
            {
                handler(e.KeyCode);
            }
        }

        private void HandleInputReady(Keys key)
        {
            if (key == Keys.Space) TransitionTo(GameState.Playing);
        }

        private void HandleInputPlaying(Keys key)
        {
            if (key == Keys.Escape) TransitionTo(GameState.Paused);
        }

        private void HandleInputPaused(Keys key)
        {
            if (key == Keys.Enter) TransitionTo(GameState.Playing);
            else if (key == Keys.Escape) TransitionTo(GameState.GameOver);
        }

        private void HandleInputGameOver(Keys key)
        {
            if (key == Keys.Space) TransitionTo(GameState.Ready); // Redémarrer
        }
    }
}