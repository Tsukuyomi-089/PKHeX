# PKHeX.Avalonia — interface native Linux

Une interface graphique **native, multi-plateforme** (Avalonia UI, `net10.0`) construite sur
`PKHeX.Core`, pour utiliser PKHeX sous Linux **sans Windows, sans Wine, sans WinForms**.

## Pourquoi un nouveau projet ?

Le GUI historique `PKHeX.WinForms` utilise **Windows Forms** et `System.Drawing.Common`, qui
ne fonctionnent pas nativement sous Linux. `PKHeX.Core` (toute la logique métier) est en revanche
100 % cross-platform. Ce projet fournit une nouvelle couche présentation native au-dessus de Core.

## Prérequis

- SDK **.NET 10** (installé ici dans `~/.dotnet`).

## Lancer

```bash
./PKHeX.Avalonia/run.sh
# ou directement :
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release
```

## Fonctionnalités actuelles

- 📂 Ouvrir une sauvegarde (tous formats reconnus par `SaveUtil`) — aussi via argument CLI
- Afficher dresseur / version / TID / génération
- Naviguer dans les **boîtes** et l'**équipe**, avec **sprites** (chargés nativement via SkiaSharp)
- Auto-sélection du premier emplacement occupé
- Éditeur complet : espèce, forme, niveau, genre, surnom, dresseur, nature, **talent**,
  **objet tenu**, **Ball**, amitié, **4 capacités**, **IV** (×6) et **EV** (×6)
- Bouton **✨ Rendre chromatique**
- **Analyse de légalité** en direct (`LegalityAnalysis`) avec rapport détaillé
- **Légaliser (best-effort)** : applique les corrections « suggérées » de Core (moveset, relearn, lieu,
  amitié, Ball, PP, stats). Corrige les Pokémon proches du légal ; ce n'est pas l'AutoLegalityMod
  complet (reconstruction d'encounter), qui est un projet tiers non publié sur NuGet.
- **Import / Export d'un Pokémon** (`.pk*`) avec conversion automatique au format de la save
- **Vider un emplacement** (🗑) et **glisser-déposer** pour échanger deux emplacements
- Onglet **Dresseur** : nom OT, TID/SID, genre, argent, temps de jeu
- Onglet **Inventaire** : édition des poches (ajout / modification / retrait d'objets)
- Onglet **Pokédex** : compteurs vus/capturés, éditer une espèce, compléter / tout marquer vu / effacer
- Onglet **Batch** : édition en masse à la syntaxe PKHeX (`.Propriété=Valeur`, filtres, portée boîte/toutes/équipe)
- 💾 Exporter la sauvegarde modifiée

Noms (espèces, natures, talents, objets, capacités) affichés en **français**.

## Sprites

Les PNG de `PKHeX.Drawing.PokeSprite/Resources/img` sont embarqués comme `AvaloniaResource`
et décodés par **SkiaSharp** via `Avalonia.Media.Imaging.Bitmap` — on contourne totalement
`System.Drawing.Common` (Windows-only). Voir `Services/SpriteLoader.cs`.

## Périmètre / limites

Base de portage ciblée sur les cas d'usage principaux, **pas** un portage 1:1 des 154 formulaires
WinForms. La **légalisation best-effort** couvre les corrections de Core ; l'*AutoLegalityMod* complet
(reconstruction d'encounter pour rendre légal n'importe quel Pokémon) est un projet tiers
(*PKHeX-Plugins*) non publié sur NuGet — l'intégrer nécessiterait de vendorer sa source. Manquent
aussi des éditeurs avancés (événements, records, RNG…).

Le Pokédex est spécifique à chaque génération (API `Zukan` distinctes) : l'implémentation invoque
au mieux la bonne méthode par réflexion. La barre de statut signale si une génération n'est pas
prise en charge par une action donnée. Chaque ajout consiste à brancher des contrôles Avalonia sur les propriétés/API de
`PKHeX.Core` déjà disponibles.
