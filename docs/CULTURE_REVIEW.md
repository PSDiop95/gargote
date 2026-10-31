# Relecture culturelle

Tout contenu culturel (noms, plats, répliques, anecdotes) est listé ici **avant** intégration.
Statut : `à relire` → `validé` / `à corriger` (avec commentaire du relecteur).

## M1 — identifiants et noms techniques (aucun texte affiché au joueur en M1)

| Élément | Où | Valeur | Statut | Commentaire |
|---|---|---|---|---|
| Ville | `economy_seed.json` | Dakar | à relire | |
| Commerce 1 | `forokh_thiaya` | Forokh Thiaya | à confirmer | Orthographe wolof à confirmer (producteur : « confirmer ») |
| Commerce 2 | `tangana` | Tangana / Ndéki | à confirmer | Affinité « matin » (x2) (producteur : « confirmer ») |
| Commerce 3 | `maiga` | Maïga | à confirmer | Affinité « nuit » (x2) (producteur : « confirmer ») |
| Commerce 4 | `gargote` | Gargote | validé | Repas du midi et dîner ; assiette à 2 000 FCFA |
| Commerce 5 | `dibiterie` | Dibiterie | à relire | Affinité « nuit » (x2) |
| Commerce 6 | `fataya_nems` | Plateau Salé | validé | Renommé (ex-« Fataya & Nems ») ; id interne inchangé |
| Commerce 7 | `restaurant_teranga` | Restaurant Teranga | à relire | |
| Commerce 8 | `traiteur_evenements` | Kiki Traiteur | à confirmer | Renommé (ex-« Traiteur Événements ») ; vérifier qu'aucune entreprise réelle ne porte ce nom |
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

### Économie réaliste — prix unitaires de Dakar (FCFA)

Chaque vente affichée (billets qui volent) correspond à un client servi à ce prix. Le prix ne monte jamais : l'empire
grandit par le nombre de points de vente et de clients. À vérifier : prix crédibles dans le Dakar d'aujourd'hui.

| Commerce | Ce qu'on vend (une vente) | Prix proposé | Statut | Remarque |
|---|---|---|---|---|
| Forokh Thiaya | Pain + viande grillée | 1 000 | à relire |  |
| Tangana / Ndéki | Omelette-pain + café | 1 000 | à relire |  |
| Maïga | Plateau de brochettes | 2 500 | à relire |  |
| Gargote | Assiette de thiéboudienne (midi et dîner) | 2 000 | validé | Ancien prix 6 000 (bol familial). Cycle 17,49 s → 5,83 s : 3 fois plus de clients, revenu/s identique |
| Dibiterie | 1 kg de dibi d'agneau, oignons, moutarde | 12 000 | à relire |  |
| Plateau Salé | Commande de 100 pièces (fatayas, nems, pastels) | 25 000 | à relire |  |
| Restaurant Teranga | Une table (menus complets) | 40 000 | à relire |  |
| Kiki Traiteur | Un mariage ou un baptême | 1 500 000 | à relire |  |

## M3 — méta : gérants à collectionner, album de recettes, succès (à relire avant M4)

Source : `Assets/_Game/Data/Seed/meta_seed.json` (noms, anecdotes, recettes) et `Assets/_Game/Scripts/Editor/M3/MetaStrings.cs`
(textes d'interface FR/EN). Les noms en wolof portent « (à valider) » : **relecture par un locuteur natif obligatoire**.
Aucun gérant ne représente une personne réelle identifiable : les légendaires sont des figures-types (le lutteur star,
la griotte, le DJ, le footballeur).

### Points à trancher en priorité (sensibilité religieuse ou communautaire)

| Élément | Statut | Décision du producteur |
|---|---|---|
| Badara le Baye Fall (Tangana) | à modifier → fait | Garde son nom, trait non religieux : **Badara le lève-tôt** / Badara the Early Riser |
| Oustaz Grill (Dibiterie) | à modifier → fait | Titre religieux retiré : **Tonton Braise** / Uncle Embers |
| Pape du café Touba | validé | Conservé tel quel |
| Tchapalo / Tonton Tchapalo (bière de mil) | rejeté → remplacé | **Pas de boisson alcoolisée dans le jeu.** Commerce `tchapalo` affiché « Jus de gingembre & bissap » ; gérant **Tata Bissap** / Auntie Bissap (« Bissap bien frais, gingembre qui pique ! »). Id interne `tchapalo` gardé (sauvegardes) |
| Le Roi des arènes / Le footballeur star / Le DJ coupé-décalé | à valider (M4) | Aucun visuel ne doit rappeler une personne réelle |

Règle ajoutée : aucun alcool, tabac ni jeu d'argent dans le jeu (noms, plats, décors, répliques).

### Gérants à collectionner (32)

| Ville | Commerce | Rareté | Nom FR | Nom EN | Avis |
|---|---|---|---|---|---|
| dakar | forokh_thiaya | Commun | Tonton Modou | Uncle Modou | |
| dakar | tangana | Commun | Badara le lève-tôt | Badara the Early Riser | renommé |
| dakar | maiga | Commun | Ndèye la reine de la nuit | Ndeye, Queen of the Night | |
| dakar | gargote | Commun | Adja Fatou | Adja Fatou | |
| dakar | dibiterie | Commun | Tonton Braise | Uncle Embers | renommé |
| dakar | fataya_nems | Commun | Aïda Pastels | Aida Pastels | |
| dakar | restaurant_teranga | Commun | Monsieur Teranga | Mister Teranga | |
| dakar | traiteur_evenements | Commun | Mère Coumba la traiteuse | Mother Coumba the Caterer | |
| dakar | tangana | Rare | Pape du café Touba | Pape the Touba Coffee Man | validé |
| dakar | forokh_thiaya | Rare | Fatou Bissap | Fatou Bissap | |
| dakar | dibiterie | Rare | Ousmane le livreur en Jakarta | Ousmane the Jakarta Rider | |
| dakar | gargote | Épique | Yaye la Cheffe | Yaye the Chef | |
| dakar | maiga | Épique | Le chauffeur de car rapide | The Car Rapide Driver | |
| dakar | restaurant_teranga | Épique | Mame la commerçante de Sandaga | Mame the Sandaga Trader | |
| dakar | traiteur_evenements | Légendaire | Le Roi des arènes | King of the Arena | |
| dakar | fataya_nems | Légendaire | La Grande Griotte | The Great Griotte | |
| abidjan | alloco | Commun | Tantie Alloco | Auntie Alloco | |
| abidjan | garbadrome | Commun | Le Boss du Garba | The Garba Boss | |
| abidjan | attieke_poisson | Commun | Maman Attiéké | Mama Attieke | |
| abidjan | maquis | Commun | Maman Maquis | Mama Maquis | |
| abidjan | choukouya | Commun | Le Roi du Choukouya | The Choukouya King | |
| abidjan | tchapalo | Commun | Tata Bissap | Auntie Bissap | remplacé (pas d'alcool) |
| abidjan | kedjenou | Commun | Tantie Kédjénou | Auntie Kedjenou | |
| abidjan | restaurant_plateau | Commun | Monsieur Plateau | Mister Plateau | |
| abidjan | garbadrome | Rare | Le chauffeur de gbaka | The Gbaka Driver | |
| abidjan | alloco | Rare | Aya la vendeuse de jus | Aya the Juice Seller | |
| abidjan | maquis | Rare | Koffi le livreur | Koffi the Delivery Man | |
| abidjan | kedjenou | Épique | La Reine du maquis | Queen of the Maquis | |
| abidjan | tchapalo | Épique | Le chanteur de zouglou | The Zouglou Singer | |
| abidjan | attieke_poisson | Épique | Mamie Wôrô-wôrô | Granny Woro-woro | |
| abidjan | restaurant_plateau | Légendaire | Le DJ coupé-décalé | The Coupe-Decale DJ | |
| abidjan | choukouya | Légendaire | Le footballeur star | The Star Footballer | |

### Recettes de l'album (22) — nom, anecdote, étapes

- **Pain-viande grillée** (Mburu ak yàpp (à valider) / Grilled meat sandwich), commerce `forokh_thiaya`, niveau 1
  - Anecdote : Le fourneau sur brouette s'installe au coin de la rue dès la tombée du jour. On y glisse la viande grillée dans un pain encore chaud.
  - Recette : Mariner la viande avec oignon, ail et poivre. / Griller sur le charbon en retournant souvent. / Ouvrir le pain et ajouter oignons et moutarde. / Garnir de viande et servir chaud.
- **Omelette du tangana** (Omlet (à valider) / Tangana omelette), commerce `tangana`, niveau 1
  - Anecdote : Au tangana, l'omelette se commande dans un pain pour bien commencer la journée. Chacun a son assaisonnement préféré.
  - Recette : Battre les œufs avec sel et poivre. / Faire revenir oignon et piment. / Cuire l'omelette à feu vif. / Servir dans un pain.
- **Spaghetti au pain** (Espageti (à valider) / Spaghetti in bread), commerce `tangana`, niveau 25
  - Anecdote : Le spaghetti en sandwich surprend les visiteurs mais régale les habitués. C'est un classique du petit-déjeuner de rue.
  - Recette : Cuire les pâtes. / Préparer une sauce tomate-oignon. / Mélanger pâtes et sauce. / Servir dans un pain.
- **Niébé** (Ñebbe (à valider) / Black-eyed peas), commerce `tangana`, niveau 50
  - Anecdote : Le niébé mijoté nourrit longtemps et coûte peu. Il se mange souvent avec du pain au tangana.
  - Recette : Faire tremper puis cuire les haricots. / Préparer une sauce oignon-tomate. / Laisser mijoter ensemble. / Servir avec du pain.
- **Pain-thon** (Mburu ak toon (à valider) / Tuna sandwich), commerce `tangana`, niveau 100
  - Anecdote : Rapide et pas cher, le pain-thon est le compagnon des matins pressés. On l'agrémente souvent de mayonnaise et de piment.
  - Recette : Émietter le thon. / Ajouter oignon, mayonnaise et piment. / Garnir le pain.
- **Macaroni** (Makaroni (à valider) / Macaroni), commerce `tangana`, niveau 200
  - Anecdote : Les macaronis en sauce sont une autre star du tangana. On les sert parfois avec un œuf.
  - Recette : Cuire les macaronis. / Préparer la sauce oignon-tomate. / Mélanger et servir.
- **Petits pois** (Petipwa (à valider) / Green peas), commerce `tangana`, niveau 300
  - Anecdote : Les petits pois en sauce garnissent le pain du matin. Un plat simple qui réchauffe.
  - Recette : Faire revenir oignon et tomate. / Ajouter les petits pois. / Laisser mijoter et servir dans un pain.
- **Brochettes** (Brosett (à valider) / Skewers), commerce `maiga`, niveau 1
  - Anecdote : La nuit, les brochettes grillent sous l'ampoule du maïga. L'odeur attire tout le quartier.
  - Recette : Couper la viande en dés. / Mariner avec épices et oignon. / Enfiler sur des pics. / Griller sur le charbon.
- **Frites du maïga** (Frit (à valider) / Maiga fries), commerce `maiga`, niveau 25
  - Anecdote : Les frites accompagnent les brochettes et l'omelette du soir. Croustillantes, elles partent vite.
  - Recette : Éplucher et couper les pommes de terre. / Frire une première fois. / Frire à nouveau plus chaud. / Saler et servir.
- **Thiéboudienne** (Ceebu jën / Thieboudienne), commerce `gargote`, niveau 1
  - Anecdote : Riz au poisson et légumes, le thiéboudienne est souvent présenté comme le plat national du Sénégal. Le riz du fond de la marmite, croustillant, est très apprécié.
  - Recette : Farcir le poisson d'une pâte de persil, ail et piment. / Faire revenir oignon et concentré de tomate. / Cuire poisson et légumes dans la sauce. / Cuire le riz dans le bouillon. / Servir le riz avec poisson et légumes.
- **Yassa** (Yaasa / Yassa), commerce `gargote`, niveau 25
  - Anecdote : Le yassa marie le citron et les oignons confits. Il se décline au poulet ou au poisson.
  - Recette : Mariner le poulet avec citron, oignons et moutarde. / Griller le poulet. / Faire fondre les oignons de la marinade. / Mijoter le poulet dans les oignons et servir avec du riz.
- **Mafé** (Maafe / Mafe), commerce `gargote`, niveau 50
  - Anecdote : Le mafé est une sauce à base de pâte d'arachide. Il est apprécié dans toute l'Afrique de l'Ouest.
  - Recette : Faire revenir la viande et l'oignon. / Ajouter tomate et pâte d'arachide délayée. / Mijoter avec les légumes. / Servir avec du riz.
- **Dibi d'agneau** (Dibi / Lamb dibi), commerce `dibiterie`, niveau 1
  - Anecdote : À la dibiterie, l'agneau grillé se commande au poids. Il se partage avec oignons et moutarde.
  - Recette : Couper l'agneau en morceaux. / Griller au charbon. / Assaisonner de sel et poivre. / Servir avec oignons et moutarde.
- **Fatayas** (Fataya / Fatayas), commerce `fataya_nems`, niveau 1
  - Anecdote : Les fatayas sont des chaussons frits farcis. Ils se mangent avec une sauce tomate pimentée.
  - Recette : Préparer une pâte souple. / Garnir de viande ou de poisson. / Fermer en chausson. / Frire jusqu'à dorure.
- **Nems** (Nem (à valider) / Spring rolls), commerce `fataya_nems`, niveau 25
  - Anecdote : Les nems font partie des en-cas de fête au Sénégal. Ils rappellent une histoire de rencontres entre cuisines.
  - Recette : Préparer la farce. / Rouler dans les galettes. / Frire et servir chaud.
- **Pastels** (Pastel / Pastels), commerce `fataya_nems`, niveau 50
  - Anecdote : Les pastels sont de petits chaussons au poisson. Ils sont incontournables dans les fêtes.
  - Recette : Émietter le poisson avec oignon et piment. / Garnir de petits ronds de pâte. / Frire et servir avec sauce.
- **Soupou kandja** (Supu kànja / Soupou kandja), commerce `restaurant_teranga`, niveau 1
  - Anecdote : Le soupou kandja est une sauce au gombo très appréciée. Elle se sert avec du riz blanc.
  - Recette : Couper le gombo. / Cuire viande et poisson. / Ajouter gombo et huile de palme. / Mijoter et servir avec du riz.
- **Caldou** (Kaldu (à valider) / Caldou), commerce `restaurant_teranga`, niveau 25
  - Anecdote : Le caldou est un poisson cuit dans un bouillon citronné. C'est un plat léger et parfumé.
  - Recette : Faire revenir oignon et piment. / Ajouter eau et citron. / Pocher le poisson. / Servir avec du riz.
- **Thiakry** (Caakiri / Thiakry), commerce `restaurant_teranga`, niveau 50
  - Anecdote : Le thiakry est un dessert de semoule de mil au lait caillé. Il se sert bien frais.
  - Recette : Cuire la semoule de mil à la vapeur. / Mélanger avec lait caillé et sucre. / Parfumer et servir frais.
- **Thiéré** (Cere / Millet couscous), commerce `traiteur_evenements`, niveau 1
  - Anecdote : Le thiéré, couscous de mil, accompagne de nombreuses fêtes familiales. Il se sert avec une sauce à la viande.
  - Recette : Cuire la semoule de mil à la vapeur. / Préparer une sauce à la viande. / Servir ensemble.
- **Ngalakh** (Ngalax / Ngalakh), commerce `traiteur_evenements`, niveau 25
  - Anecdote : Le ngalakh est un dessert de mil à l'arachide et au pain de singe. Il se partage entre voisins lors des fêtes.
  - Recette : Cuire la semoule de mil. / Préparer une crème d'arachide et de pain de singe. / Mélanger et servir.
- **Méchoui** (Mesuwi (à valider) / Roast lamb), commerce `traiteur_evenements`, niveau 50
  - Anecdote : Le mouton rôti entier est le roi des grandes fêtes. Préparer un méchoui demande des heures de patience.
  - Recette : Assaisonner le mouton. / Rôtir lentement sur le feu. / Arroser régulièrement. / Servir avec oignons et frites.

### Succès (62)

Roi du Forokh, Habitués du Forokh, Paliers du Forokh, Équipe du Forokh, Roi du Tangana, Habitués du Tangana, Paliers du Tangana, Équipe du Tangana, Roi du Maïga, Habitués du Maïga, Paliers du Maïga, Équipe du Maïga, Roi de la Gargote, Habitués de la Gargote, Paliers de la Gargote, Équipe de la Gargote, Roi du Dibi, Habitués du Dibi, Paliers du Dibi, Équipe du Dibi, Roi des Fatayas, Habitués des Fatayas, Paliers des Fatayas, Équipe des Fatayas, Roi de la Teranga, Habitués de la Teranga, Paliers de la Teranga, Équipe de la Teranga, Roi des Fêtes, Habitués des Fêtes, Paliers des Fêtes, Équipe des Fêtes, Le milliard, Toute la ville a mangé, Doigts de fée, Bâtisseur, La rue s'anime, Bon patron, Plus vite !, Bien équipé, Recommencer en mieux, Teranga absolue, Tour de l'Afrique de l'Ouest, Chasseur de coffres, Collectionneur, Talents rares, Épique !, Légende de la rue, École des gérants, Mission accomplie, Défi relevé, Fidèle au poste, Habitué, La roue tourne, Bon accueil, Coup de boost, Album gourmand, Grand patron, Collier de cauris, Toute la rue avance, Ça tourne sans moi, Merci les sponsors


## À venir (M3/M4)

Gérants (Tonton Modou, Badara le lève-tôt, Ndèye la reine de la nuit, Adja Fatou, Tonton Braise, Aïda Pastels,
Monsieur Teranga, Mère Coumba la traiteuse), répliques, anecdotes et recettes de l'album.
