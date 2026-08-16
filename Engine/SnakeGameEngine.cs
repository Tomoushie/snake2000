// Dans le constructeur de votre moteur de jeu
_movementSystem = new MovementSystem(_entityManager, _physicsEngine, _navMesh);
_systems.Add(_movementSystem);

// Dans la boucle de jeu, le MovementSystem.Update() appellera ses sous-systèmes.