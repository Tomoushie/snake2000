# Reprise — le bloc Animation

Document de passation, écrit le 16 août 2026 et tenu à jour depuis. Il porte
l'état **mesuré**, ce qui a été tranché et ne doit pas être rouvert, et la
méthode de travail.

**Commencer par la section « Le chiffre de ce compteur est un PLANCHER ».**
Sans elle, tous les nombres de ce document se lisent de travers.

## Où en est le projet

Mesuré avec `python Tools/diagnostic.py Engine Game` — **et ce compteur ne voit
que les erreurs de déclaration**, voir la section suivante :

| | fichiers retenus | erreurs de déclaration |
|---|---|---|
| 15 août | 71 | 1 629 |
| après la session « IAnimationEngine » | 77 | 509 |
| 17 août | 80 | 41 |

Le vrai total, au build complet, est plus bas dans cette page.

## Le chiffre de ce compteur est un PLANCHER, pas un état

C'est la chose la plus importante à savoir avant de reprendre, et elle a été
découverte le 17 août 2026.

**Roslyn s'arrête après l'étape de déclaration quand celle-ci échoue.** Tant
qu'une seule `CS0246` subsiste, les erreurs de **corps de méthode** ne sont
jamais publiées. `Tools/diagnostic.py` partage cet angle mort : il compile et
compte de la même façon.

La démonstration est nette. Trois membres sans appelant restaient, référençant
des types absents. En retirer **un seul** laissait 1 à 2 erreurs. En retirer
**les deux derniers** en a fait apparaître **629**.

| ce qu'on mesure | erreurs |
|---|---|
| le dépôt (`dotnet build Tools/Build-complet.csproj`) | **38** |
| … avec `-p:SansStub=true` | **184 distinctes** (593 le 17 août au matin) |

**Deux erreurs de mesure commises le 17 août, à ne pas refaire :**

- Retirer un type encore référencé affiche un chiffre **plus petit** — les
  `CS0246` qui en résultent font échouer l'étape de déclaration et masquent à
  nouveau tous les corps. Un total qui *baisse* après une suppression doit être
  vérifié : il peut être un masque, pas un progrès. Vu ici : 17 affichées pour
  555 réelles.
- Chercher `.Methode(` ne trouve **que les appels externes**. Les appels
  internes à une classe ne sont pas qualifiés. `GetMetricsSnapshot` semblait
  sans appelant ; il en avait onze.

Le premier piège s'est présenté **trois fois** en une journée, et la dernière
fois le compteur affichait **1** erreur pour 375 réelles. Le réflexe à prendre :
si le total s'effondre après un lot, regarder le CODE de l'erreur restante.
`CS0246`, `CS0234`, `CS0535`, `CS0539` sont des erreurs de **déclaration** — une
seule suffit à tout masquer. `CS0103`, `CS1061`, `CS0117`, `CS1729` sont des
erreurs de **corps** : celles-là ne s'affichent que lorsque la déclaration est
entièrement propre, et leur nombre est donc le vrai.

Les 184 : 45 `CS0103` (nom inexistant), 18 `CS1729` (constructeur absent),
16 `CS0019` (opérateur inapplicable), puis une longue traîne.

Le foyer dominant est **`AI/SnakeAI.cs`, 42** — et il n'est pas soluble par
relevé, voir « Ce qui reste à faire ». Le reste est éparpillé : plus aucun
fichier au-dessus de 20 hors celui-là.

### Deux erreurs de ciblage que j'ai commises, à ne pas refaire

**Un appel NON QUALIFIÉ dit dans quelle classe le membre doit vivre.** J'ai posé
onze méthodes sur `partial class AnimationEngineStub` alors que les appels
étaient dans `public static class AnimationEngineIndex`. Elles compilaient et ne
satisfaisaient rien — 23 `CS0103` intacts. Vérifier la classe englobante avant
d'écrire, pas seulement le nom du fichier.

**Retirer un membre sans suivre TOUS ses sites laisse un trou.** En archivant
`CaptureMetrics` j'ai emporté le champ `_lastMetrics`, que deux autres méthodes
lisent et écrivent. Seul le compilateur l'a vu.

### La méthode qui marche sur ces erreurs-là

**Le compilateur donne le contrat.** Un `CS1061` nomme le type ET le membre
manquant ; le site d'appel donne les types des paramètres. Extraire ça
mécaniquement produit un brief exact — c'est ainsi que les onze coquilles vides
d'affinité ont été remplies, et que `Vector2` a retrouvé `Zero`, `Dot`,
`Length`, `Normalized`.

```bash
dotnet build Tools/Build-complet.csproj -v q --nologo -p:SansStub=true
```

puis regrouper les `CS1061` par type porteur : chaque groupe est un lot.

**`Tools/Build-complet.csproj` existe pour ça** : il compile tout le dépôt —
`Snake2000.cs`, `Engine`, `Game`, `AI`, `Systems` — là où
`Snake2000App.csproj` ne référence que `Snake2000.cs`. C'est le seul moyen de
connaître l'état réel.

Ce qui reste vrai malgré tout : les 38 erreurs du build **sans** `SansStub` sont
**délibérées**. Ce sont huit types de `DummyAnimationEngine` qui n'apparaissent
qu'en signature ; les déclarer produirait des coquilles vides. Mais elles ne sont
pas le bout du chemin — elles sont le paravent qui cache les 209.

## La règle qui a tranché quatre fois

**Le contrat est ce que les appelants appellent, pas ce que la déclaration
annonce.** À chaque fois, la mesure a donné un résultat que personne n'aurait
deviné :

| déclaration | membres déclarés | appelés |
|---|---|---|
| `IAnimationEngine` | 655 | **0** |
| `IJobSystem` | 408 | **3** |
| `struct MovementComponent` (champs `*Knowledge`) | 1 509 | **0** |
| `ResourceManager` | 6 | **0** |

Le corollaire, appris à ses dépens : **le nombre d'usages ne dit rien du
contrat.** `IAnimationClip` compte dix usages et pas un seul membre lu — ce sont
des usages de *signature*. Seuls les membres nommés font un contrat, et c'est
la seule chose qu'un brief puisse exiger.

Le geste est toujours le même : la déclaration d'origine part dans
`Docs/Intention/`, le contrat garde les membres mesurés, et **un membre ne
rejoint le contrat que le jour où un appelant réel le demande**.

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

### `IJobSystem` est réduit à trois membres

408 méthodes déclarées sur 3 121 lignes. **Trois sont appelées dans tout le
dépôt**, toutes depuis `ThreadAffinityManager.cs` :

```csharp
int GetWorkerThreadCount();                                    // ligne 1107
void SuspendWorkerThread(int threadIndex);                     // 714, 738, 762
void SetJobAffinityHints(JobHandle jobHandle, AffinityHints hints);  // 476, 508
```

La troisième, appelée deux fois, **ne figurait pas parmi les 408**. Le seul
implémenteur, `DummyJobSystem`, en portait trois autres — aucun des appelés.
`DefaultJobSystem.cs`, `JobScheduler.cs` et `WorkerThread.cs` sont vides.

Archive dans `Docs/Intention/IJobSystem.cs.txt`, avec `IJobSystemGraphBuilder`
dont le `Build()` était le dernier appelant du type absent `IJobGraph`. Les 65
types annexes du fichier — `IJob`, `JobHandle`, `JobSystemConfig`,
`ThreadLoadReport`… — sont restés : ceux-là servent.

**Les 19 « types de rapport manquants » n'ont pas été générés, et c'est le
résultat principal de cette session.** Le relevé donnait 24 mentions, toutes
dans le fichier qui déclarait les types, **zéro construction et zéro lecture**.
Il n'y avait aucun contrat à exiger dans un brief : la génération aurait rendu
19 coquilles vides, de façon prévisible avant d'appuyer.

### L'arbitrage des dummies

Les stubs de `DummyAnimationEngine.cs` avaient été écrits contre un `Engine.cs`
qui n'est pas celui du dépôt. La mesure a tranché cas par cas, sans règle
globale :

| membre | appelants | décision |
|---|---|---|
| `EventBus.Publish` / `Subscribe` | 9 et 3, sur instances | la base s'ouvre : `virtual` |
| `Profiler.BeginSample` / `EndSample` | 2, **statiques** | le dummy perd ses `override` |
| `Profiler.MarkEvent` | 0, membre inexistant | supprimé |
| `ResourceManager`, tous membres | **0** | le dummy perd ses `override` |

Aucune substitution d'instance ne peut détourner un appel statique : ces
`override` ne remplaçaient rien. Les dummies restent des **types
substituables** pour les paramètres des `Initialize`, ce qu'ils sont réellement.

`IThreadAffinityManager` est implémenté (`AssignSystemToThread`, `RunOnThread`).
L'épinglage réel n'a pas lieu — `INativeAffinityProvider` est un marqueur sans
membre — et le commentaire le dit plutôt que de laisser le nom promettre.

## Le piège qui s'est présenté trois fois

Un type déclaré `struct` alors que le code appelant le lit par
`Volatile.Read`/`Volatile.Write`, qui exigent un type référence. Le symptôme est
un `CS0677` — « un champ volatile ne peut pas être de ce type » — et c'est un
**progrès déguisé** : il ne peut apparaître qu'une fois le type enfin résolu.

`ThreadAffinityManagerConfig`, `AudioEngineConfig` et `GPUProfilerHookConfig`
sont tous les trois devenus des classes pour cette raison. **Il n'en reste
aucun** ; si un quatrième apparaît, le traiter de la même façon.

## Le geste le plus rentable de tout le chantier

**Chercher le type dans le dépôt avant de conclure qu'il manque.** C'est ce qui
a produit l'essentiel de la chute de 509 à 41 — pas la génération.

Premier cas rencontré : ce document annonçait « 33 types absents » pour
`ThreadAffinityManager`. En les cherchant un par un, la moitié existait déjà.
Trois lignes d'`using` ont fait tomber ce fichier de 75 à 40 erreurs. Le motif
s'est ensuite répété sur chaque gros fichier.

Le script est écrit, il suffit de l'adapter au fichier visé :

```bash
grep -rn "class MonType\b\|struct MonType\b\|interface MonType\b" --include=*.cs .
```

Six espaces de noms étaient importés un peu partout et **n'existent nulle
part** : `Engine.Events`, `Engine.Utilities`, `Engine.Services`, `Engine.Jobs`,
`Engine.Resources`, `Engine.Mathematics`. Ils désignaient une organisation
prévue, jamais créée. Tous ont été retirés. Les
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

Le chantier de compilation est **terminé** au sens où il ne reste aucune erreur
qui ne soit un choix assumé. La suite n'est plus de l'assainissement :

1. **Décider ce que devient `DummyAnimationEngine`** — 2 597 lignes que rien ne
   référence, 38 erreurs assumées. Le stub a été conservé pour des tests qui
   n'existent pas encore. S'ils n'arrivent pas, il rejoint `Docs/Intention/`.
2. **Unifier les espaces de noms — c'est devenu le chantier bloquant du pont.**
   Mesuré le 17 août : ses 55 erreurs restantes ne sont plus des membres
   manquants mais un désaccord de racine. Le code écrit
   `Movement.State.StaminaStateMachine`, `Movement.AudioVisual.MovementAudioMixer`,
   `Movement.Debug.MovementDebugOverlaySystem` — et les types sont déclarés sous
   `Game.Gameplay.Movement.*`, avec `Debugging` là où le code écrit `Debug`.

   **Ne pas coller des `global::` un par un** : ça compilerait et figerait
   l'incohérence. Il faut choisir une racine et s'y tenir. Le reste des 55 en
   dépend : les `CS1729` (constructeurs absents) portent sur des classes que
   j'ai générées sous la mauvaise racine, et les `CS0029` sur `Vector2` viennent
   du même désordre — `Snake2000.Engine.Core.Vector2` contre
   `System.Numerics.Vector2`, deux `IJob`, deux `Vector3`.

   Trois autres systèmes n'existent nulle part, quelle que soit la racine :
   `ProceduralFootPlacementSystem`, `MovementAudioMixer`, `SurfaceReactionSystem`.
   Leurs constructeurs sont relevés — tous prennent `EntityManager`, deux
   prennent en plus `NavMesh` ou `PhysicsSystem`.
3. **`AI/` et `Systems/`, jamais mesurés — et il y a du vrai travail dedans.**
   `python Tools/diagnostic.py AI Systems` rend **0 fichier retenu** : aucun ne
   passe la validation. Mais les deux dossiers ne se ressemblent pas.

   `Systems/` : **traité.** Les dix fichiers sont désormais des repères vides et
   suivis par git. Tous portaient un appel d'outil JSON ou du JavaScript brut,
   tentatives avortées d'extraire du code depuis `Snake2000.cs`.

   Deux avaient été mis de côté par une garde qui refuse de vider ce qui
   contient une déclaration C#. **Après lecture, c'étaient des débris aussi** :
   l'un écrivait `"Achievement logged!"`, l'autre sortait sa classe d'une
   fonction nommée `generateWeatherSystemCode` précédée de
   `// Dummy function to simulate code generation` — mot pour mot l'exemple que
   la docstring de `file_manager.py` cite comme déchet. **Le rôle d'une garde
   est de forcer une lecture, pas de juger de la valeur** ; ne pas confondre
   « la garde a refusé » avec « il y avait quelque chose à sauver ».

   `WeatherSystemcs` n'a toujours pas d'extension `.cs` : aucun outil ne le voit,
   ni le diagnostic, ni une recherche `--include=*.cs`.

   `AI/SnakeAI.cs` : **24 Ko de C# écrit à la main** — `enum AIType`, des
   personnalités d'IA. Ce n'est pas un fichier compilable : il commence par
   « Ajouter dans la section des variables membres », c'est un fragment destiné
   à être collé dans `Snake2000.cs`. Il est **suivi par git**, contrairement à
   `Systems/`. **Ne pas le vider ni le supprimer.**

   **C'est fait.** Le fichier est enveloppé dans `namespace Snake2000` /
   `public partial class Snake2000` — sans répéter `: Form`, la classe de base
   ne se déclarant qu'une fois entre partiels. Pas une ligne de son contenu n'a
   bougé. Mesuré avec `AI Game Engine` : **81 fichiers, 41 erreurs, dont zéro
   pour `SnakeAI.cs`.**

   Mesuré seul (`diagnostic.py AI`), il en montre quatre : `Direction` vient de
   `Game/Enums.cs`, absent de cette compilation. **Artefact de mesure, pas
   défaut** — toujours mesurer un fichier avec les dossiers dont il dépend.
4. **Faire compiler le projet pour de bon.** `Snake2000App/Snake2000App.csproj`
   ne référence **que** `..\Snake2000.cs`, 113 lignes — les arbres `Engine/`,
   `Game/`, `AI/` et `Systems/` sont hors compilation. Les y faire entrer est un
   chantier à part entière : ce sont 80 fichiers, et le `.csproj` cible
   `net8.0-windows` avec `Nullable` et `ImplicitUsings` activés, deux réglages
   que le code existant n'a jamais eu à satisfaire.

### L'écart entre fichiers présents et fichiers mesurés

Il valait 19, il vaut **1**. Les 18 fichiers de débris ont été vidés — ils
rejoignent les 184 repères vides du dépôt, le nom gardant l'intention. Le seul
restant est `Engine/SnakeGameEngine.cs`, une note de câblage écrite à la main,
commentée pour rester lisible sans casser la compilation.

**Refaire ce contrôle après chaque gros lot** : un fichier syntaxiquement cassé
disparaît du compteur sans prévenir, et le compteur devient flatteur.

```bash
python Tools/diagnostic.py Engine Game
```

et comparer « fichiers retenus » au nombre de `.cs` non vides.

### La méthode, si un nouveau foyer d'erreurs apparaît

**Chercher les types dans le dépôt avant de les croire absents** : c'est ce qui
a produit tous les gros gains.

| fichier | avant | après | coût |
|---|---|---|---|
| le bridge | 97 | 13 | **14 lignes d'`using`** |
| `IAudioEngine` | 87 | 12 | 5 lignes |
| `AnimationEngineStub` ×3 | 57 | 37 | 10 `using` fantômes retirés |
| `GPUProfilerHook` | 12 | 0 | 2 `using` + un `struct` → `class` |
| `DummyAnimationEngine` | 94 | 74 | 3 lignes |

Le bridge déclarait ses propres types dans **six espaces de noms différents**,
qui ne se voyaient donc pas les uns les autres. Sur 22 types « introuvables »,
21 existaient — plusieurs dans le fichier lui-même.

Cinq espaces de noms sont importés un peu partout et **n'existent nulle part** :
`Engine.Events`, `Engine.Utilities`, `Engine.Services`, `Engine.Jobs`,
`Engine.Resources`, plus `Engine.Mathematics`. Les retirer coûte une ligne
chacun.

Le mot « dupliqué » d'un diagnostic mérite la même méfiance : les 82 `CS0102`
du bridge n'étaient pas deux versions à fusionner comme pour
`AnimationEngineStub`, mais les collisions internes de 1 509 champs
`*Knowledge` jamais lus (archive dans
`Docs/Intention/MovementComponent-Knowledge.cs.txt`).
2. **`DummyAnimationEngine`, 38 — tranché, et le stub est conservé.** Rien ne
   le référence (2 597 lignes, zéro appelant), mais il est cohérent et servira
   quand des tests arriveront. Des douze types absents, **quatre seulement
   avaient un membre nommé** et ont été déclarés dessus : `SkeletonInfo`,
   `AnimationPlaybackState`, `RootMotionSample`, `RootMotionMode`. Les huit
   autres n'apparaissent qu'**en signature** — les déclarer serait écrire huit
   coquilles vides. Ils restent absents, erreurs nommées plutôt que masquées.

   **La distinction à retenir : un type très utilisé peut n'avoir aucun
   contrat.** `IAnimationClip` compte dix usages et pas un seul membre lu. Le
   nombre d'usages ne dit rien ; seuls les membres nommés font un contrat, et
   c'est la seule chose qu'un brief puisse exiger.
3. **`IAudioEngine` 87 et `AnimationEngineStubOrchestrator` 51** — jamais
   examinés. Commencer par y chercher les types qui existent déjà : c'est ce qui
   a fait tomber `DummyAnimationEngine` de 94 à 74 pour trois lignes d'`using`.
4. **Unifier les espaces de noms** — `Snake2000.Engine.*` contre `Engine.*`.
   Deux `IJob` et deux `Vector2` coexistent déjà et doivent être qualifiés à la
   main dans les fichiers qui importent les deux côtés.

## La boucle de travail

L'orchestrateur écoute sur **`http://localhost:5001`**, pas 8000. Lancement :
`python start_orchestrator.py` depuis `E:\Corpus\OrchestratorAgent` (qui est un
dépôt git depuis le 16 août). Vérifier d'abord `/health` : la réponse donne le
parc de modèles, le routage, les clés manquantes, le workspace et l'état du
contrôle Roslyn.

**Le parc, depuis le 16 août.** Un modèle est désigné par son rôle — `code`,
`code_rapide`, `code_large`, `raisonnement` — et le champ `tache` d'une requête
est routé par `config.ROUTAGE`. Mesuré ce jour-là : des six modèles `:cloud`,
**seul `gpt-oss:120b-cloud` répond** sur l'abonnement Ollama actuel ; les autres
exigent une souscription supérieure. Deux modèles locaux de 7 B ne tiennent pas
ensemble sur la 2080 Ti (11 Go). Mistral Large 2 existe sur Ollama mais fait
73 Go, contre 43 Go de VRAM + RAM sur cette machine : il ne se chargera pas.

**`/generate-batch` plutôt que `/dry-run` dès qu'il y a plus d'un fichier.**
Elle mène N générations de front et rend un **verdict, pas du code** — le code
part dans `OrchestratorAgent/workspace/tmp/`, un fichier par tâche. C'est ce qui
évite que 400 lignes générées entrent dans le contexte pour être lues une fois.
On n'ouvre que les échecs. Corps : `{"taches": [{"filepath", "instruction",
"tache"}], "ecrire": false}` — `ecrire` est à `false` par défaut, délibérément.

Sous PowerShell, `curl` est un alias d'`Invoke-WebRequest` et n'accepte pas
`-H` : passer par `Invoke-RestMethod -ContentType 'application/json' -Body`,
ou par `curl.exe`.

**Le levier qui rapporte le plus n'est pas un modèle, c'est un script.**
`releve.py` a condensé 6 300 lignes de C# en un relevé de 138 pour 70 lignes de
Python. Écrire un réducteur local avant de lire un gros fichier vaut mieux que
n'importe quelle délégation. Et **la relecture, elle, ne se délègue pas** : un
second modèle qui valide le premier rate les mêmes choses.

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

**Exiger les membres *lus* autant que ceux affectés à la construction**, et
donner dans le brief les lignes d'usage qui les montrent.

**Deux lots générés le 16 août ont marché du premier coup, et pour la même
raison** : les neuf types de `AnimationEngineStubOrchestrator` (51 erreurs → 0,
onze secondes) et les quatre du bridge. Dans les deux cas le brief énumérait
chaque membre avec son type, relevé sur son site d'appel — pas une consigne de
bon goût. Qwen a rendu exactement ce qui était demandé, sans un membre de plus.
La différence avec les échecs n'est pas le modèle : c'est qu'il y avait un
contrat à transmettre.

Le brief doit aussi dire ce qu'il ne faut **pas** écrire : `DrawHeatmapPixel`
apparaît sur `_renderSystem` mais son unique site d'appel est **en
commentaire**. Elle a été exclue du contrat, et le type est juste.

**Et d'abord : relever AVANT d'écrire le brief.** C'est ce relevé qui a arrêté
le lot des 19 types — zéro construction, zéro lecture, donc rien à exiger. Le
symptôme inverse existe aussi : `PoseBlendMode`, `BoneTransformSpace` et
`AnimationStreamingPriority` n'ont qu'**une seule valeur nommée chacune**, en
paramètre par défaut de méthodes sans appelant. Elles sont déclarées comme ça,
avec une valeur. Écrire `Additive`, `World` ou `High` pour faire complet aurait
été le même défaut que les coquilles vides, en sens inverse.

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
