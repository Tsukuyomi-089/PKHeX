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

- 📂 Ouvrir une sauvegarde (tous formats reconnus par `SaveUtil`)
- Afficher dresseur / version / TID / génération
- Naviguer dans les **boîtes** et l'**équipe**
- Éditer un Pokémon : espèce, niveau, surnom, nature, dresseur (OT)
- Indicateur chromatique (✨)
- 💾 Exporter la sauvegarde modifiée

Noms d'espèces et de natures affichés en **français**.

## Périmètre / limites

C'est une base de portage volontairement ciblée sur les cas d'usage principaux, **pas** un
portage 1:1 des 154 formulaires WinForms. Les sprites (issus de `PKHeX.Drawing.PokeSprite`, qui
dépend de `System.Drawing.Common`) ne sont pas affichés — à porter vers SkiaSharp/ImageSharp
dans une itération ultérieure. Étendre l'éditeur (stats, IV/EV, capacités, objets…) se fait en
ajoutant des champs branchés sur les propriétés `PKM` correspondantes.
