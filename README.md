[![License](https://img.shields.io/github/license/Tomoushie/snake2000)](LICENSE)

**Snake2000** — Un remake rétro moderne du classique Snake

![Screenshot](assets/screenshot.png)

Snake2000 rend hommage aux jeux portables des années 2000 avec une esthétique « LCD »
type Nokia 3310, des animations pixelisées et des mécaniques modernes : niveaux progressifs,
bonus/malus, profils avec classement et un mode duel local ou en ligne.

Principales caractéristiques

- **Ambiance rétro** : palette vert pâle, grille pixelisée et sons d'époque.
- **Gameplay évolutif** : la vitesse et la taille du plateau augmentent avec les niveaux.
- **Objets spéciaux** : fruits de vitesse, pièges et effets visuels qui dynamisent la partie.
- **Profils & classement** : sauvegarde locale des meilleurs scores par pseudonyme.
- **Duel** : affrontement local (flèches vs WASD) ou connexion directe TCP pour jouer à deux.

Contrôles

- Déplacer : Flèches ou WASD
- Pause / reprendre : `P`
- Démarrer / Rejouer : `Espace`
- Voir le classement : `L`
- Changer de nom : `N`
- Menu duel/multijoueur : `M`

Compilation et lancement

Deux façons simples d'exécuter le jeu :

- Ouvrir la solution `Snake2000.slnx` dans Visual Studio (Windows) et appuyer sur `F5`.
- Utiliser le CLI .NET depuis la racine du projet :

```powershell
dotnet build Snake2000.slnx
dotnet run --project Snake2000App
```

Remarques : le projet cible `.NET 8.0` avec Windows Forms — exécution possible uniquement
sur un environnement Windows compatible.

Mode duel en ligne

Le duel en ligne utilise une connexion directe (peer-to-peer) via le port TCP `7788`.
Le joueur hébergeant doit transmettre son adresse IP au partenaire (et éventuellement
configurer un transfert de port si nécessaire).

Contribution

Contributions bienvenues ! Ouvrez une issue pour proposer une amélioration ou soumettez
une pull request. Pour une PR :

1. Forkez le dépôt
2. Créez une branche `feature/ma-fonctionnalite`
3. Soumettez une PR décrivant vos changements

Licence

Ce projet est distribué sous la licence indiquée dans le fichier `LICENSE`.

Merci d'avoir regardé — amusez-vous bien !

---

## English

**Snake2000** — A retro-modern remake of the classic Snake

![Screenshot](assets/screenshot.png)

Snake2000 pays tribute to handheld games from the 2000s with a Nokia-3310-style LCD
look, pixel animations and modern gameplay features: progressive levels, buffs/debuffs,
profiles with local high scores and a local or online duel mode.

Key features

- **Retro vibe**: pale-green palette, pixel grid and old-school sound effects.
- **Progressive gameplay**: speed and board size increase as you advance through levels.
- **Special items**: speed fruits, traps and visual effects make matches dynamic.
- **Profiles & leaderboard**: local saving of best scores per nickname.
- **Duel**: play locally (arrows vs WASD) or connect directly over TCP for online play.

Controls

- Move: Arrow keys or WASD
- Pause / Resume: `P`
- Start / Retry: `Space`
- Show leaderboard: `L`
- Change name: `N`
- Duel / Multiplayer menu: `M`

Build & Run

Two easy ways to run the game:

- Open `Snake2000.slnx` in Visual Studio (Windows) and press `F5`.
- Use the .NET CLI from the repo root:

```powershell
dotnet build Snake2000.slnx
dotnet run --project Snake2000App
```

Note: the project targets `.NET 8.0` and uses Windows Forms — it runs on compatible
Windows environments only.

Online duel

Online duel uses a direct peer-to-peer TCP connection on port `7788`.
The hosting player should share their IP address with the opponent (and may need
to configure port forwarding on their router if required).

Contributing

Contributions welcome! Open an issue to suggest improvements or submit a pull request.
For a PR:

1. Fork the repository
2. Create a branch `feature/my-feature`
3. Submit a PR describing your changes

License

This project is distributed under the license shown in the `LICENSE` file.

Enjoy!
