# Occult Weakness

Plugin Dalamud pour **FINAL FANTASY XIV** qui permet d'enregistrer la faiblesse élémentaire des monstres et de l'afficher près de l'interface de cible.

## Fonctionnalités

- Identification des monstres uniquement par leur nom.
- Comparaison des noms sans tenir compte de la casse.
- Fusion automatique des anciennes entrées portant le même nom.
- Faiblesses prises en charge : Feu, Glace, Vent et Foudre.
- Icône de faiblesse affichée de façon fixe près de l'interface de cible.
- Configuration via `/ocweak`.
- Liste sauvegardée entre les lancements du jeu.

## Installation via dépôt personnalisé

Ajoutez l'URL du dépôt maître dans Dalamud :

`https://raw.githubusercontent.com/Hylieno/DalaLeno/main/pluginmaster.json`

Puis recherchez **Occult Weakness** dans `/xlplugins`.

## Compilation

Ouvrez `OccultWeakness.sln`, sélectionnez `Release | x64`, puis compilez la solution.

Le projet utilise `Dalamud.NET.Sdk/15.0.0`.

## Licence

MIT. Voir [LICENSE](LICENSE).
