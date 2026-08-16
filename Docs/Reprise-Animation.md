# Reprise — le bloc Animation

Document de passation, écrit le 16 août 2026 à la fin d'une session consacrée
à l'assainissement de `Game/` et `Engine/`. Il contient ce qu'il faut pour
reprendre le chantier de l'animation sans refaire le diagnostic.

## Où en est le projet

Compilé avec `python Tools/diagnostic.py Engine Game` :

| zone | erreurs |
|---|---|
| `Game/` | **0** |
| `Engine/` hors animation | 118 |
| bloc animation | 1 502 |

Le point de départ de la session était 1 770 erreurs dont 14 dans `Game/`.

Commits de la session, tous poussés sur `origin/main` :

| commit | objet |
|---|---|
| `9c14c54` | EventBus : les abonnés disparaissaient au ramasse-miettes |
| `9496236` | trois conflits `CS0101` dans `Game/` |
| `fa9b773` | directives `using` manquantes |
| `a8ad521` | le moteur sort de l'espace de noms global |
| `a9edc30` | lot Movement complété |
| `f6b61ae` | **`Game/` compile sans erreur** |
| `d0c033a` | `IRenderEngine` rebranché |
| `7f5e617` | sept types d'IA déclarés |

## Le diagnostic du bloc animation

Trois fichiers portent 1 502 des 1 629 erreurs restantes.

| fichier | erreurs | nature |
|---|---|---|
| `Engine/IAnimationEngine.cs` | 560 | `CS0246` × 528 — types jamais déclarés |
| `Engine/Animation/DummyAnimationEngine.cs` | 488 | `CS0535` × 406 — membres d'interface non implémentés |
| `Engine/Animation/AnimationEngineStub.Core.cs` | 454 | `CS0535` × 415 — idem |

**La racine est `IAnimationEngine.cs`.** C'est une interface de 3 268 lignes
qui référence une cinquantaine de types jamais écrits — `AnimationClipInstance`,
`AnimationGraph`, `RootMotionDelta`, `AnimationBlendSpace`, `AnimationSkeleton`,
`AnimationIKRequest`, `AnimationLayer` — et qui déclare plusieurs centaines de
membres.

Les deux implémentations qui prétendent la respecter n'en implémentent
quasiment rien : 406 et 415 membres manquants. Autrement dit, **personne n'a
jamais implémenté ce contrat**. C'est une intention, pas une interface.

### La décision à prendre avant de coder

Deux voies, et elles n'ont pas le même coût :

1. **Réduire l'interface à ce qui est réellement utilisé.** Relever les membres
   effectivement appelés dans le reste du moteur et du jeu, tailler
   `IAnimationEngine` à cette taille, puis n'écrire que les types nécessaires.
   Les centaines de `CS0535` disparaissent d'elles-mêmes.
2. **Écrire les cinquante types manquants et implémenter les 800 membres.**
   Fidèle à l'intention d'origine, mais c'est plusieurs jours de génération
   pour un contrat que rien n'appelle encore.

La première voie est recommandée. Commencer par mesurer ce qui est appelé :

```bash
grep -rn "IAnimationEngine\|_animationEngine\." --include=*.cs Engine Game
```

### Les sept derniers conflits `CS0101`

Tous dans ce bloc, et tous du même geste : un fichier découpé sans que
l'original soit vidé.

| espace de noms | types en double | fichiers |
|---|---|---|
| `Engine.Animation` | `DiagnosticsLevel`, `IAnimationEngine`, `IAnimationSubsystem`, `VersionInfo` | `AnimationEngineStub.cs` + `AnimationEngineStub.Core.cs` |
| `Engine.Animation.Test` | `AlertPriority`, `AlertSeverity` | `Tests/CommonTypes.cs` + `Tests/OrchestratorDashboard.cs` |
| `Engine.Rendering` | `IRenderDevice` | `Animation/IRenderEngine.cs` + `Rendering/IRenderPipeline.cs` |

`python Tools/doublons.py` donne la liste complète et à jour.

### Deux conventions d'espaces de noms cohabitent

Le moteur porte `Snake2000.Engine.*` pour les cinq fichiers assainis pendant la
session, et `Engine.*` pour le bloc animation, rendu et jobsystem
(`Engine.Animation`, `Engine.Rendering`, `Engine.Core`, `Engine.Jobsystem`).
Unifier est souhaitable, mais **après** avoir tranché sur l'interface : le faire
avant reviendrait à ranger une pièce qu'on va vider.

## Ce qui reste hors animation — 118 erreurs

Deux fichiers, même patron que `Engine/AI/AITypes.cs` : un fichier compagnon de
types, produit par l'orchestrateur à partir des usages relevés.

- **`Engine/Jobsystem/ThreadAffinityManager.cs`** — 75 erreurs, 33 types absents
  (`ThreadAffinityManagerConfig`, `CPUTopology`, `CategoryAffinityMap`,
  `IAffinityPolicy`, `ReservedThreadInfo`, `ThreadLoadDistribution`…)
- **`Engine/Jobsystem/IJobsystem.cs`** — 32 erreurs, 19 types de rapport absents
  (`JobHeatmapReport`, `JobTelemetryData`, `JobBudgetTrendReport`,
  `ThreadTelemetryData`…)

Plus `OrchestratorDashboard.cs` (7) et `IRenderPipeline.cs` (4), qui relèvent du
bloc animation.

## La boucle de travail qui a fonctionné

1. Relever le contrat exact dans le fichier appelant — signatures de
   constructeurs, membres lus, interfaces imposées. C'est cette précision qui
   fait la qualité du résultat, pas la longueur de la demande.
2. Écrire un brief numéroté et envoyer à l'orchestrateur.
3. Relire. **Systématiquement**, Qwen respecte les bornes là où c'est commode et
   les abandonne dans les propriétés : `HealthSystem` exposait un
   `CurrentHealth` libre, `MovementImpactComponent` un `Bounciness` non serré.
   Vérifier chaque contrainte du brief une à une.
4. Vérifier au compilateur, pas à l'œil.

L'orchestrateur doit voir le projet, sinon il génère à l'aveugle :

```bash
$env:WORKSPACE_DIR = "E:\Corpus\Snake2000"
```

## Ce qu'il ne faut pas faire

Les 184 fichiers `.cs` vides ne sont **pas** des débris : ce sont des repères
créés à l'avance pour savoir quoi remplir. Ne pas les supprimer. La garde
d'écriture refuse d'écrire du vide, elle ne touche jamais à ce qui est déjà sur
le disque.

Le projet ne compile pas dans son ensemble, et c'est normal à ce stade : le
moteur s'écrit encore. `Snake2000App.csproj` ne référence volontairement que
`Snake2000.cs`.
