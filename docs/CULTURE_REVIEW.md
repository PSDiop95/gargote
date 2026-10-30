# Relecture culturelle

Tout contenu culturel (noms, plats, répliques, anecdotes) est listé ici **avant** intégration.
Statut : `à relire` → `validé` / `à corriger` (avec commentaire du relecteur).

## M1 — identifiants et noms techniques (aucun texte affiché au joueur en M1)

| Élément | Où | Valeur | Statut | Commentaire |
|---|---|---|---|---|
| Ville | `economy_seed.json` | Dakar | à relire | |
| Commerce 1 | `forokh_thiaya` | Forokh Thiaya | à relire | Orthographe wolof à confirmer |
| Commerce 2 | `tangana` | Tangana / Ndéki | à relire | Affinité « matin » (x2) |
| Commerce 3 | `maiga` | Maïga | à relire | Affinité « nuit » (x2) |
| Commerce 4 | `gargote` | Gargote | à relire | |
| Commerce 5 | `dibiterie` | Dibiterie | à relire | Affinité « nuit » (x2) |
| Commerce 6 | `fataya_nems` | Fataya & Nems | à relire | |
| Commerce 7 | `restaurant_teranga` | Restaurant Teranga | à relire | |
| Commerce 8 | `traiteur_evenements` | Traiteur Événements | à relire | |
| Monnaie | `Money` / FCFA | FCFA | à relire | Affichage « 1,23K FCFA » |
| Prestige | `TotalStars` | Étoiles Teranga | à relire | |

| Upgrades FCFA | `dakar_up_01` … `dakar_up_40` | « Upgrade NN (à nommer) » | à nommer | 40 upgrades génériques (x2 un commerce, un sur cinq x2 tous) ; noms à proposer, ex. « Bouteille de gaz neuve », « Charbon de qualité », « Car rapide de livraison » (§6) |
| Upgrades d'étoiles | `gerants_fideles`, `commis_automatique`, `comme_avant` | Gérants fidèles, Commis automatique, Comme avant | à relire | Confort du prestige |

## M2 — textes affichés (table de localisation « Gargote », FR/EN)

Source : `Assets/_Game/Scripts/Editor/M2/LocalizationSetup.cs` (régénère la table). Les noms de commerces reprennent la table M1.

| Clé | FR | EN | Statut | Remarque |
|---|---|---|---|---|
| `manager.forokh_thiaya.1` | Pain chaud, viande chaude, client content ! | Hot bread, hot meat, happy customer! | à relire |  |
| `manager.forokh_thiaya.2` | Ma brouette, c'est mon bureau. | My cart is my office. | à relire |  |
| `manager.forokh_thiaya.3` | Jërëjëf, à demain ! | Jërëjëf, see you tomorrow! | à relire | « Jërëjëf » (wolof, merci) : orthographe à valider |
| `manager.tangana.1` | Le café Touba est prêt ! | The Touba coffee is ready! | à relire | Café Touba : référence confrérique (mouride), vérifier le ton |
| `manager.tangana.2` | Omelette ou niébé ? Les deux, bien sûr. | Omelette or beans? Both, of course. | à relire |  |
| `manager.tangana.3` | Le matin, ici, c'est la meilleure adresse. | Mornings, this is the place to be. | à relire |  |
| `manager.maiga.1` | La nuit est jeune, les brochettes aussi ! | The night is young, so are the skewers! | à relire |  |
| `manager.maiga.2` | Sous l'ampoule, tout est plus bon. | Under the bulb, everything tastes better. | à relire |  |
| `manager.maiga.3` | Une petite faim ? On a ce qu'il faut. | A little hungry? We've got you. | à relire |  |
| `manager.gargote.1` | Le thiéb' du jour sort de la marmite ! | Today's thieb is out of the pot! | à relire |  |
| `manager.gargote.2` | Assieds-toi, mange, c'est la maison. | Sit down, eat, you're home. | à relire |  |
| `manager.gargote.3` | Le secret ? La patience… et le riz cassé. | The secret? Patience… and broken rice. | à relire | Riz cassé du thiéboudienne |
| `manager.dibiterie.1` | Le dibi est grillé à point. | The dibi is grilled just right. | à relire |  |
| `manager.dibiterie.2` | Oignons, moutarde, et le tour est joué ! | Onions, mustard, and you're set! | à relire |  |
| `manager.dibiterie.3` | Le charbon chante, c'est bon signe. | The charcoal is singing, good sign. | à relire |  |
| `manager.fataya_nems.1` | Fatayas tout chauds, attention les doigts ! | Hot fatayas, mind your fingers! | à relire |  |
| `manager.fataya_nems.2` | Pastels, nems, fatayas : on a tout. | Pastels, nems, fatayas: we have it all. | à relire |  |
| `manager.fataya_nems.3` | Un pour toi, un pour la route. | One for you, one for the road. | à relire |  |
| `manager.restaurant_teranga.1` | Bienvenue chez nous, la Teranga d'abord ! | Welcome, hospitality comes first! | à relire |  |
| `manager.restaurant_teranga.2` | Table pour combien ? | Table for how many? | à relire |  |
| `manager.restaurant_teranga.3` | Le dessert est offert… aux habitués. | Dessert is on us… for regulars. | à relire |  |
| `manager.traiteur_evenements.1` | Trois cents invités ? Pas de souci. | Three hundred guests? No problem. | à relire |  |
| `manager.traiteur_evenements.2` | Les marmites sont en route ! | The pots are on their way! | à relire |  |
| `manager.traiteur_evenements.3` | Une fête réussie, ça se mange. | A good party is one you can eat. | à relire |  |
| `special.official` | Fonctionnaire pressé | Hurried Official | à relire |  |
| `special.tourist` | Touriste perdu | Lost Tourist | à relire | Humour bienveillant, à valider |
| `special.wrestler` | Lutteur affamé | Hungry Wrestler | à relire | Lutte sénégalaise : figure-type, aucun lutteur réel |
| `special.wedding` | Délégation de mariage | Wedding Delegation | à relire | Grosse commande (M3) |
| `hud.goal` | Objectif Dakar : | Dakar goal: | à relire |  |
| `hud.welcome` | Bienvenue x3 | Welcome x3 | à relire |  |
| `badge.morning` | x2 MATIN | x2 MORNING | à relire |  |
| `badge.night` | x2 NUIT | x2 NIGHT | à relire |  |
| `banner.unlock` | OUVERT ! | OPEN! | à relire |  |
| `banner.speed_x2` | VITESSE x2 ! | SPEED x2! | à relire |  |
| `banner.revenue_x2` | REVENUS x2 ! | PROFIT x2! | à relire |  |
| `banner.global` | TOUTE LA RUE x1,25 ! | WHOLE STREET x1.25! | à relire |  |
| `offline.title` | Pendant ton absence… | While you were away… | à relire |  |
| `offline.away` | Absent | Away | à relire |  |
| `offline.claim` | Récupérer | Collect | à relire |  |
| `offline.double` | x2 (pub) | x2 (ad) | à relire |  |

### M2 — placeholders non textuels

| Élément | Placeholder actuel | Statut | Remarque |
|---|---|---|---|
| Musique jour / nuit | Boucles synthétisées (pentatonique + basse), générées par code | placeholder | À remplacer en M4 (mbalax instrumental libre de droits ou commandé) |
| Son de déblocage | Roulement de tambour synthétisé « façon sabar » | placeholder | Vrai sabar enregistré en M4 |
| Monument | Silhouette abstraite (socle + colonne + sphère) | placeholder | Monument de la Renaissance stylisé en M4, sans reprendre de marque déposée |
| Personnages | Capsules colorées, teintes de peau variées | placeholder | Tenues (boubou, wax) et diversité à valider en M4 |
| Confettis | Couleurs « wax » (palette accents) | placeholder | Motifs wax génériques en M4 |

## À venir (M3/M4)

Gérants (Tonton Modou, Badara le Baye Fall, Ndèye la reine de la nuit, Adja Fatou, Oustaz Grill, Aïda Pastels,
Monsieur Teranga, Mère Coumba la traiteuse), répliques, anecdotes et recettes de l'album.
