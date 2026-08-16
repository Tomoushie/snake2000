// Note de cablage, conservee telle quelle : ce fichier n'a jamais porte de
// code compilable, seulement le rappel de la facon dont MovementSystem doit
// etre branche. Commente pour rester lisible sans casser la compilation.
//
// Dans le constructeur de votre moteur de jeu
// _movementSystem = new MovementSystem(_entityManager, _physicsEngine, _navMesh);
// _systems.Add(_movementSystem);

// Dans la boucle de jeu, le MovementSystem.Update() appellera ses sous-systèmes.