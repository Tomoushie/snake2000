# Snake2000

[![License](https://img.shields.io/github/license/Tomoushie/snake2000)](../LICENSE)

**Snake2000** — A retro-modern remake of the classic Snake

![Screenshot](../assets/screenshot.png)

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
