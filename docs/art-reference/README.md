# Références visuelles (fournies le 2026-10-30)

Planches d'intention pour M4 « Habillage Dakar ». Elles guident le style ; elles ne remplacent pas CLAUDE.md.

| Fichier | Contenu | Usage |
|---|---|---|
| 01-planche-dakar.webp | Personnages, gérants, 8 commerces Dakar, plats, icônes, décors, props | Brief modélisation M4 |
| 02-ui-hud-mockup.webp | Maquette écran de jeu : HUD, rails, barre d'onglets, panneaux | **Structure UI appliquée dès M3** (placeholders) |
| 03-forokh-evolution.webp | Forokh Thiaya en 5 évolutions, vues, Tonton Modou (6 poses) | Diorama par paliers (§9.2), animations gérant |
| 04-planche-generale.webp | Personnages, Dakar/Abidjan/Bamako, plats, collection de gérants | Écrans villes et collection |
| 05-planche-gargote.webp | Gargote isométrique, 3 niveaux, icônes | Style des commerces |
| 06-planche-personnages.webp | Personnages, clients, props, car rapide | Foule, véhicules |

## Repris dans M3 (structure, sans art final)
- HUD haut : pastille niveau + barre XP en %, FCFA, cauris.
- Rail droit : Roue, Coffre gratuit avec minuterie sous l'icône ; rail gauche : boost gérant avec minuterie.
- Bandeau « Objectif » sous le HUD (mission en cours + barre).
- Barre d'onglets bas à 5 entrées, icône + libellé.
- Panneau commerce en bas : nom, niveau, revenu/s, gros bouton vert « Améliorer » avec coût.
- Toast « Super ! Tu as gagné… » avec portrait du gérant.

## Écarts avec le cahier des charges (à ne pas reprendre tels quels)
- **Diamants, énergie, vies, pièces d'or** : hors design. Monnaie premium = **cauris** ; FCFA = **billets**. Pas d'énergie ni de vies (contraire au pilier idle, §0).
- **Pièce avec symbole $** (planche 01) : à remplacer par billet FCFA.
- **Onglets maquette** (Commerces / Villes / Gérants / Recettes / Événement) ≠ §10 (Rue / Gérants / Upgrades / Villes / Boutique). M3 garde §10 ; Recettes reste accessible par le rail (Album). Événement = live ops, hors M3.
- **« +200 % revenus / 0,2 s » par évolution** (planche 03) : chiffres illustratifs, l'équilibrage vient des ScriptableObjects.
- **Pack de démarrage 4,99 €** : M5, prix localisés.
- **Monuments** : rendu stylisé générique, pas de reproduction fidèle protégée à vérifier (Monument de la Renaissance).
- **Drapeaux et texte sur les enseignes** : texte en langue du joueur via localisation, pas dans la texture.
- Orthographe à relire (« Thiebou dienne », « Ceebu Jën », « Maïga ») : CULTURE_REVIEW.
- Personnages : aucun visage de personne réelle identifiable ; vérifier que les visages générés ne ressemblent à personne.
- Images générées : droits d'usage commercial et cohérence à confirmer avant de s'en servir comme assets (ici : référence seulement).
