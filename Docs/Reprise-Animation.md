# Reprise — le bloc Animation

Document de passation, écrit le 16 août 2026 et **réécrit le même jour** à la
fin de la session qui a tranché sur `IAnimationEngine`. Il remplace la version
précédente, dont l'état de départ n'existe plus.

## Où en est le projet

Mesuré avec `python Tools/diagnostic.py Engine Game` :

| | fichiers retenus | erreurs |
|---|---|---|
| avant cette session | 71 | 1 629 |
| **maintenant** | **77** | **509** |

Le nombre de fichiers compte autant que celui des erreurs : six fichiers sont
entrés dans la mesure, dont cinq très gros qui n'y étaient jamais apparus. Le
compteur d'hier était flatteur parce qu'il ignorait 380 Ko de code.

| fichier | erreurs | nature |
|---|---|---|
| `Game/…/MovementAnimationBridgeSystem.cs` | 179 | 83 membres dupliqués dans une classe, le reste en types absents |
| `Engine/Animation/DummyAnimationEngine.cs` | 94 | types absents |
| `Engine/Audio/IAudioEngine.cs` | 87 | types absents |
| `Engine/Animation/Tests/AnimationEngineStubOrchestrator.cs` | 51 | types absents |
| `Engine/Animation/AnimationEngineStub.cs` + `.Core.cs` | 53 | types absents |
| `Engine/Jobsystem/IJobsystem.cs` | 24 | 19 types de rapport absents |
| `Engine/Profiling/GPUProfilerHook.cs` | 12 | types absents |

Par code : `CS0246` × 372 domine tout le reste, puis `CS0102` × 83 — les 83 du
bridge — et `CS0234` × 18.

## Ce qui a été tranché, et qu'il ne faut pas rouvrir

### `IAnimationEngine` est réduit à trois membres

L'interface faisait 3 268 lignes, 448 méthodes et 207 propriétés, et
référençait près de 200 types jamais écrits. **La mesure des appelants donnait
zéro membre appelé sur 655.** Un seul consommateur existe hors du bloc
animation, `MovementAnimationBridgeSystem`, et il en attend trois :

```csharp
void InitializeBridge(IAnimationBridge bridge);
void ShutdownBridge(IAnimationBridge bridge);
Vector2 ExtractRootMotionDelta(Snake2000.Engine.Core.Entity entity);
```

Aucun des trois ne figurait dans les **trois** déclarations concurrentes du nom
`IAnimationEngine` qui coexistaient alors.

La déclaration d'origine est conservée dans `Docs/Intention/IAnimationEngine.cs.txt`.
Elle reste le repère de ce que le moteur vise, sans faire échouer la
compilation. **Ne la réintroduire dans `Engine/` sous aucun prétexte** : le jour
où un membre sera réellement appelé, il rejoindra le contrat un par un.

Le marqueur `IAnimationBridge` existe pour que le moteur reçoive le pont sans
dépendre de `Game/`. C'est le patron à reprendre chaque fois que `Engine`
semble avoir besoin d'un type de `Game`.

### Le découpage d'`AnimationEngineStub` est terminé

`AnimationEngineStub.cs` était l'original d'un découpage dont personne n'avait
vidé la source. Les 30 types et 54 champs qu'il redéclarait étaient identiques
à ceux des partiels, et sont partis. Les **méthodes**, elles, avaient divergé :
l'original gardait la logique métier — budget de frame, moniteur de threads,
détecteur de surcharge, mode safe, plugins — là où le partiel la remplaçait par
`// ... (logique de …) ...` tout en apportant l'instrumentation neuve
(`_metricCollector`, `_subsystemProfiler`, `_snapshotHistory`).

Règle appliquée, à reprendre si un cas analogue apparaît : **la version qui
porte la logique reste, l'instrumentation de l'autre est reportée dedans.**

## Le piège qui s'est présenté deux fois

Un type déclaré `struct` alors que le code appelant le lit par
`Volatile.Read`/`Volatile.Write`, qui exigent un type référence. Le symptôme est
un `CS0677` — « un champ volatile ne peut pas être de ce type » — et c'est un
**progrès déguisé** : il ne peut apparaître qu'une fois le type enfin résolu.

`ThreadAffinityManagerConfig` est devenu une classe pour cette raison. Il reste
deux `CS0677` dans le dépôt : les traiter de la même façon, en lisant le code
appelant avant de choisir la forme.

## La leçon principale de cette session

**Chercher le type dans le dépôt avant de conclure qu'il manque.** La version
précédente de ce document annonçait « 33 types absents » pour
`ThreadAffinityManager`. En les cherchant un par un, la moitié existait déjà —
`IJobSystem`, `JobHandle`, `JobCategory`, `CategoryAffinityMap` dans le fichier
voisin, `EventBus`, `Profiler`, `ResourceManager` dans `Engine/Engine.cs`. Trois
lignes d'`using` ont fait tomber ce fichier de 75 à 40 erreurs.

Le script est écrit, il suffit de l'adapter au fichier visé :

```bash
grep -rn "class MonType\b\|struct MonType\b\|interface MonType\b" --include=*.cs .
```

Trois espaces de noms sont importés un peu partout et **n'existent nulle part** :
`Engine.Events`, `Engine.Utilities`, `Engine.Services`, `Engine.Jobs`,
`Engine.Resources`. Ils désignaient une organisation prévue, jamais créée. Les
retirer coûte une ligne et supprime des `CS0234`.

## Les fichiers écartés de la mesure

`Tools/diagnostic.py` ne compile que les fichiers que `valider_code` accepte.
Un fichier syntaxiquement cassé disparaît donc du compteur **sans prévenir**.
Quatre en sont sortis pendant cette session pour une faute unique :

| fichier | faute |
|---|---|
| `GPUProfilerHook.cs` | 20 `#region` pour 19 `#endregion` |
| `IAudioEngine.cs` | 22 pour 21 |
| `AnimationEngineStub.cs` | un paramètre nommé `params`, mot-clé |
| `AnimationEngineStubOrchestrator.cs` | une clause `using` après des déclarations |
| `MovementAnimationBridgeSystem.cs` | une virgule au lieu d'un point-virgule |

Huit lignes de diff pour 380 Ko réintégrés. **Il reste 19 fichiers non vides
hors mesure**, dont dix-sept contiennent un appel d'outil JSON au lieu de code —
`Engine/Data/`, `Engine/CollisionSystem.cs`, `Game/Gameplay/BossSystem.cs` et
voisins. Ce sont des débris de génération, pas des repères.

Pour les lister à tout moment :

```bash
python Tools/diagnostic.py Engine Game
```

et comparer « fichiers retenus » au nombre de `.cs` non vides.

## Ce qui reste à faire, par ordre de rendement

1. **`IJobsystem.cs`, 19 types de rapport** — `JobHeatmapReport`,
   `JobTelemetryData`, `ThreadTelemetryData`… Même patron que le lot d'affinité
   qui vient d'être généré. C'est le plus direct.
2. **`ThreadAffinityManager` doit implémenter `IThreadAffinityManager`** —
   `AssignSystemToThread` et `RunOnThread`, deux `CS0535` assumés. L'interface a
   été remontée de `Game` vers `Engine.Core` avec son contrat exact.
3. **Le bridge, 179** — dont 83 membres dupliqués dans une seule classe. Lot à
   part entière, même méthode que le découpage d'`AnimationEngineStub`.
4. **`IAudioEngine` 87 et `AnimationEngineStubOrchestrator` 51** — nouveaux
   venus dans la mesure, jamais examinés.
5. **Unifier les espaces de noms** — `Snake2000.Engine.*` contre `Engine.*`.
   Maintenant que l'arbitrage sur l'interface est fait, ce chantier est enfin
   ouvrable. Il ne l'était pas avant.

## La boucle de travail

L'orchestrateur écoute sur **`http://localhost:5001`**, pas 8000. Vérifier
d'abord :

```bash
curl -s http://localhost:5001/health
```

La réponse donne le modèle, le workspace et l'état du contrôle Roslyn. Il lui
faut Qwen sur Ollama en `http://localhost:11434`.

1. Relever le contrat exact dans le fichier appelant — chaque ligne qui
   mentionne le type manquant. C'est cette précision qui fait la qualité du
   résultat, pas la longueur de la demande.
2. Écrire un brief numéroté, passer par `/dry-run` avant
   `/generate-and-integrate`.
3. **Relire.** Systématiquement.
4. Vérifier au compilateur, pas à l'œil.

### Ce que la relecture a rattrapé cette fois

Le lot d'affinité est revenu avec **treize types sur quinze réduits à des
coquilles sans membre**. Le brief disait « n'invente pas de membres » et ne
couvrait, pour les membres exigés, que les types construits par `new X { … }`.
Qwen a suivi à la lettre. La compilation passe — le code appelant n'interroge
jamais ces types — mais ce sont des repères, pas des types.

**Pour le lot suivant, exiger les membres *lus* autant que ceux affectés à la
construction**, et donner dans le brief les lignes d'usage qui les montrent.

Le travers connu reste vrai par ailleurs : Qwen respecte les bornes dans les
méthodes et les abandonne dans les propriétés. Vérifier chaque contrainte du
brief une par une.

## Ce qu'il ne faut pas faire

Les 184 fichiers `.cs` vides ne sont **pas** des débris : ce sont des repères
créés à l'avance pour savoir quoi remplir. Ne pas les supprimer. La garde
d'écriture refuse d'écrire du vide, elle ne touche jamais à ce qui est déjà sur
le disque.

Le projet ne compile pas dans son ensemble, et c'est normal à ce stade : le
moteur s'écrit encore. `Snake2000App.csproj` ne référence volontairement que
`Snake2000.cs`.
