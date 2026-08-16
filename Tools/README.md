# Tools — diagnostic de compilation

Trois scripts pour savoir où en est le code sans construire le projet. Ils ne
modifient rien : ils compilent une copie en mémoire et rendent un compte rendu.

Ils dépendent de la garde d'écriture de l'orchestrateur, dans
`E:\Corpus\OrchestratorAgent\file_manager.py`, pour écarter de la compilation
les fichiers vides et les fichiers-déchets. Sans ce filtre, les erreurs de
syntaxe de quelques fichiers noient le diagnostic.

## `diagnostic.py`

Compile les dossiers demandés ensemble et classe les erreurs.

```bash
python Tools/diagnostic.py Engine Game
```

Sans argument, prend `Engine Game`. Écrit la sortie brute du compilateur dans
`diag_erreurs.txt`, à côté du script.

## `zones.py`

Lit `diag_erreurs.txt` et répartit les erreurs entre `Game/` et `Engine/`, en
détaillant celles de `Game/`. À lancer après `diagnostic.py`.

## `doublons.py`

Détecte les types déclarés deux fois dans le **même** espace de noms — la
véritable erreur `CS0101`, par opposition aux homonymes logés dans des espaces
différents, qui sont légaux.

```bash
python Tools/doublons.py
```

## Chemins codés en dur

Les trois scripts pointent sur `E:\Corpus\Snake2000`, le SDK .NET 10.0.303 et
les assemblages de référence 8.0.30. À ajuster si l'un d'eux bouge.

## Pourquoi ne pas simplement lancer `dotnet build`

`Snake2000App.csproj` ne compile qu'un seul fichier, `..\Snake2000.cs`. Les
dossiers `Engine/`, `Game/`, `AI/` et `Systems/` sont hors du projet — c'est
délibéré tant que le moteur s'écrit. Ces scripts donnent donc l'état réel du
code sans exiger que le projet soit prêt à tourner.
