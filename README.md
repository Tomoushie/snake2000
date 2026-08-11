# snake2000

A modernized version of the legendary Snake game from the 2000s — built as a single-file
C# / WinForms app with a monochrome Nokia 3310-style LCD look, progressive levels, bonus/malus
items, profiles with a persistent scoreboard, and local or online duel multiplayer.

## Features

- **Classic feel**: pale-green LCD palette, chunky pixel grid, wall collisions end the game
  (no wrap-around), retro beeper sound effects.
- **Sprites & animation**: rounded snake body with a directional head (eyes + tongue flick),
  an apple-shaped food sprite, pulsing/blinking effects, a screen flash on collision.
- **Progressive levels**: every few apples eaten, the snake speeds up; every few levels, the
  board itself grows and the window resizes with it.
- **Bonus & malus**: a speed fruit occasionally appears for a temporary speed boost, and a
  trap that shrinks the snake if eaten.
- **Profiles & scoreboard**: enter a pseudonym, your best score/time is saved locally
  (`%APPDATA%\Snake2000\profiles.txt`) and ranked against other profiles.
- **Duel mode**: play head-to-head against another snake, either locally on the same keyboard
  (arrows vs. WASD) or online over a direct TCP connection (one player hosts, the other joins
  by IP address).

## Controls

| Action | Key |
| --- | --- |
| Move | Arrow keys or WASD |
| Pause / resume | `P` |
| Start / retry | `Space` |
| Scoreboard (solo) | `L` |
| Change name (solo) | `N` |
| Duel / multiplayer menu | `M` |

In local duel, Player 1 uses the arrow keys and Player 2 uses WASD.

## Building & running

The game is a single file with no external dependencies beyond .NET's `System.Windows.Forms`
and `System.Drawing`. Compile it with the .NET Framework compiler that ships with Windows:

```
csc /target:winexe /r:System.Drawing.dll /r:System.Windows.Forms.dll Snake2000.cs
```

Then run the resulting `Snake2000.exe`.

## Online duel

Online duel is a direct, serverless connection (no matchmaking server): one player hosts —
their instance listens on TCP port `7788` and shows their local IP — and the other player
joins by entering that IP address. For players on different networks, the host needs to
forward port `7788` on their router/firewall.
