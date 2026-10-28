# Gargote Tycoon — Cahier des charges (CLAUDE.md)

> Fichier de référence pour Claude Code, à la racine du projet Unity.
> Éditeur : **Smart IA Services**. Moteur : **Unity 6 LTS** (C#, URP mobile).
> Claude Code pilote l'éditeur via **Unity CLI / Unity MCP** : ne jamais éditer à la main les fichiers .unity, .prefab ou .asset (YAML).
> Ambition : le meilleur idle tycoon street food d'Afrique de l'Ouest, rentable, conçu pour durer des années.

---

## 0. Piliers (toute décision de design se juge contre ces 5 piliers)

1. **Toujours un prochain objectif à moins de 2 minutes.** Le joueur ne doit jamais se demander « je fais quoi maintenant ? ».
2. **Revenir paie.** Chaque retour (gains hors ligne, coffres, missions, événement) est une petite fête.
3. **La rue est vivante.** On joue un monde qui bouge, pas un tableur. Le joueur regarde son empire grandir.
4. **L'Afrique de l'Ouest comme jamais vue dans un jeu.** Authentique, drôle, chaleureuse, fière. Jamais caricaturale ou moqueuse.
5. **La pub est un cadeau, pas une punition.** Rewarded choisie > interstitiel. Un joueur qui ne paie jamais doit quand même aimer le jeu.

---

## 1. Vision & positionnement

**Pitch :** Tu démarres avec un fourneau sur brouette au coin d'une rue de Dakar. Tu finis à la tête d'un empire de la street food qui nourrit toute l'Afrique de l'Ouest, d'Abidjan à Lagos.

**Références et ce qu'on prend :**
| Jeu | On prend |
|---|---|
| AdVenture Capitalist | Boucle idle pure, paliers de vitesse, prestige à racine carrée, gros chiffres qui explosent |
| Eatventure | Une ville = une « saison » de commerces, progression par villes, cuisiniers visibles |
| My Perfect Hotel / Burger Please | Rue vivante, clients qui font la queue, piles d'argent à ramasser |
| Idle Miner Tycoon | Gérants-personnages forts, boosts actifs avec recharge, raretés |
| Cooking Fever | Plats appétissants, lisibilité, couleurs |

**Notre différence :** univers inédit (aucun tycoon majeur sur l'Afrique de l'Ouest), humour local, cuisine réelle de chaque ville, viralité WhatsApp, et une diaspora mondiale qui se reconnaît.

**Cible :** joueurs casual 18-45 ans. Audience mondiale (Europe et Amérique du Nord pour l'eCPM, diaspora africaine), cœur de communauté en Afrique francophone et anglophone.

**Plateformes :** Android (Android 8+, 2 Go RAM) puis iOS. Portrait. **100 % jouable hors ligne** (seuls pubs, achats et classements demandent le réseau).

**Langues :** français, anglais (lancement) ; wolof, pidgin, haoussa en touches culturelles (répliques, noms), puis portugais (Guinée-Bissau, Cap-Vert, Brésil) en V2.

---

## 2. Boucles de jeu

### 2.1 Boucle principale (secondes)
Taper sur un commerce → un client est servi → billets FCFA qui volent → acheter des niveaux → plus de revenus et de vitesse.

### 2.2 Boucle de session (minutes)
Débloquer le commerce suivant → embaucher son gérant (automatisation) → atteindre un palier (vitesse x2) → acheter un upgrade → compléter une mission → débloquer le commerce suivant.

### 2.3 Boucle quotidienne (heures)
Revenir → récupérer les gains hors ligne (x2 via rewarded) → coffre gratuit → missions du jour → roue Teranga → avancer l'événement en cours.

### 2.4 Boucle longue (semaines)
Atteindre l'objectif de ville → **ouvrir dans une nouvelle ville** (prestige) → nouvelle cuisine, nouveaux commerces, nouveaux gérants → Étoiles Teranga (bonus permanent) → collection de gérants et de recettes → pass saisonnier.

---

## 3. Le monde : villes et commerces

Chaque **ville** est une carte complète avec **8 commerces**, sa cuisine, son ambiance, sa musique et ses gérants. Changer de ville = prestige. Les villes restent visitables (revenus passifs réduits).

### 3.1 Dakar (Sénégal) — ville de départ
| # | Commerce | Ce qu'on vend | Cycle base |
|---|---|---|---|
| 1 | Forokh Thiaya | Fourneau sur brouette : pain + viande grillée | 1 s |
| 2 | Tangana / Ndéki | Petit-déj : omelette, spaghetti, niébé, pain-thon, macaroni, petits pois | 3 s |
| 3 | Maïga (la nuit) | Brochettes, omelette, spaghetti, frites | 6 s |
| 4 | Gargote | Thiéboudienne, yassa, mafé | 12 s |
| 5 | Dibiterie | Dibi d'agneau, oignons, moutarde | 24 s |
| 6 | Fataya & Nems | Fatayas, nems, pastels | 48 s |
| 7 | Restaurant Teranga | Menus complets | 96 s |
| 8 | Traiteur Événements | Mariages, baptêmes, Magal | 192 s |

### 3.2 Villes suivantes (contenu de lancement : Dakar + Abidjan + Bamako ; puis 1 ville toutes les 6-8 semaines)
| Ville | Commerces emblématiques (exemples, à valider culturellement) |
|---|---|
| **Abidjan** (Côte d'Ivoire) | Vendeuse d'alloco, garbadrome (garba), attiéké-poisson, maquis, choukouya, tchapalo, kédjénou, restaurant du Plateau |
| **Bamako** (Mali) | Brochettes, riz gras, tô, fakoye, dibi, thé de grin, capitaine braisé, traiteur |
| **Lagos** (Nigeria) | Suya spot, puff-puff, buka, jollof joint, pepper soup, mama put, small chops, party jollof |
| **Accra** (Ghana) | Kelewele, waakye, kenkey & fish, chop bar, fufu & light soup, banku, red red |
| **Conakry**, **Lomé**, **Cotonou**, **Ouagadougou**, **Saint-Louis**, **Praia**… | Live ops (V2+) |

Chaque ville a : 8 commerces, 8 gérants de base, 1 musique, 1 palette de couleurs, 1 monument en fond (Monument de la Renaissance, pont HKB, etc.), 1 marché local.

### 3.3 Rythme jour/nuit (cycle de 24 min réelles par défaut)
- Commerces du **matin** (Tangana, garba…) : x2 le matin du jeu.
- Commerces de **nuit** (Maïga, dibiterie, suya…) : x2 la nuit.
- Visuel : lumière qui change, lampes qui s'allument, foule différente.
- Pousse le joueur actif à acheter « au bon moment », sans pénaliser le joueur idle.

---

## 4. Économie (data-driven, simulée avant d'être codée)

Tout en **ScriptableObjects** dans `Assets/_Game/Data/` (`Cities/<Ville>/`, `Businesses`, `Managers`, `Upgrades`, `PrestigeConfig`, `EconomyConfig`), surchargeables par **Remote Config**. **Rien en dur.**

### 4.1 Formules
- Coût du niveau n : `cost(n) = base_cost × growth^n`, `growth` entre **1.07 et 1.15** selon le commerce (premiers commerces bon marché, derniers chers).
- Achat x1 / x10 / x100 / MAX / « prochain palier » : somme géométrique exacte, pas de boucle.
- Revenu par cycle : `base_revenue × level × multiplicateurs`.
- **Paliers** à 25 / 50 / 100 / 200 / 300 / 400 / 500 / 750 / 1000 : vitesse x2 (ou revenus x2 au-delà de la vitesse max). Barre « prochain palier » toujours visible.
- **Vitesse max** : cycle minimum 0,1 s ; au-delà, les gains de vitesse deviennent des multiplicateurs de revenu (pas d'animation folle).
- Grands nombres : struct `BigDouble` (mantisse double + exposant long, immuable, sans allocation). Affichage 1,23K · M · B · T · puis aa, ab… Jusqu'à 1e300+.

### 4.2 Prestige : « Ouvrir une nouvelle ville »
- Récompense : **Étoiles Teranga** = `k × sqrt(gains_totaux_de_la_ville / seuil)` (formule à racine carrée façon AdCap ; `k` et `seuil` dans `PrestigeConfig`).
- Chaque étoile : +2 % de revenus permanents (amélioré par des upgrades d'étoiles).
- Objectif de ville clair (« Gagner 1 T de FCFA à Dakar ») pour débloquer la ville suivante ; le prestige libre reste possible avant.
- Écran de prestige : « Tu vas gagner 34 étoiles → +68 % sur tout ». Le joueur doit **vouloir** prestiger.

### 4.3 Gains hors ligne
- 100 % des commerces gérés, plafond **2 h** au départ, étendu jusqu'à **12 h** par upgrades / gérants / pass.
- Retour : écran « Pendant ton absence… » avec pluie de billets, bouton **x2 (rewarded)**, puis x3 via pass.
- Protection horloge : temps hors ligne calculé sur horloge monotone + heure système, plafonné, sans pénaliser les changements de fuseau.

### 4.4 Monnaies
| Monnaie | Rôle | Source |
|---|---|---|
| **FCFA** (soft, par ville) | Niveaux, déblocages | Jeu |
| **Cauris** (premium) | Coffres, boosts, accélérations, cosmétiques | Missions, succès, pass, événements, IAP |
| **Étoiles Teranga** (prestige) | Bonus permanents | Changer de ville |
| **Jetons d'événement** | Boutique d'événement | Événements |
| **Cartes de gérant** | Monter les gérants de niveau | Coffres |

> Les **cauris** comme monnaie premium : référence historique, original, mémorable.

### 4.5 Simulation obligatoire
`EconomySimulator` (C# pur, lancé en batchmode via un test EditMode ou un menu Editor) simule des profils de joueurs (occasionnel 3 sessions/jour, régulier 6, accro 12 ; avec et sans pub) sur 30 jours et sort un CSV : temps pour chaque déblocage, chaque ville, chaque prestige, solde de cauris, pubs vues.
**Cibles :** 1er gérant < 3 min · commerce 4 < 15 min · 1er prestige possible jour 2-3 · Abidjan jour 5-7 · aucune « muraille » > 30 min sans objectif atteignable.

---

## 5. Gérants (le cœur émotionnel)

- **Un gérant par commerce** = automatisation. Embauche avec FCFA (gérants de base).
- **Gérants à collectionner** (Commun / Rare / Épique / Légendaire) obtenus en coffres : chacun a un **boost actif** (ex. x5 revenus 30 s, recharge 10 min) et un **bonus passif**. Monte de niveau avec des cartes en double.
- **Personnalité forte** : nom, réplique en bulle (FR / langue locale), animation idle unique, tenue.
- Exemples Dakar : **Tonton Modou** (Forokh Thiaya), **Badara le Baye Fall** (Tangana), **Ndèye la reine de la nuit** (Maïga), **Adja Fatou** (Gargote), **Oustaz Grill** (Dibiterie), **Aïda Pastels** (Fataya), **Monsieur Teranga** (Restaurant), **Mère Coumba la traiteuse** (Traiteur).
- Exemples Abidjan : **Tantie Alloco**, **Le Boss du Garba**, **Maman Maquis**…
- Gérants **légendaires** inspirés de figures-types (le lutteur star, la griotte, le DJ de mbalax, le footballeur) — **jamais de personnes réelles identifiables**.
- Règle culturelle : humour bienveillant, aucun stéréotype dégradant (ethnie, religion, genre). Toute réplique passe par une relecture humaine (`docs/CULTURE_REVIEW.md`).

---

## 6. Upgrades & boosts

- **Upgrades FCFA** (par ville, ~40) : « Bouteille de gaz neuve » x3 Gargote, « Charbon de qualité » x3 Dibiterie, « Car rapide de livraison » x2 global, « Pain de chez Boulangerie X » x2 Forokh Thiaya…
- **Upgrades Étoiles** (permanents) : gains hors ligne, plafond hors ligne, vitesse globale, coût réduit.
- **Investisseurs / sponsors** (V1.1) : objets permanents rares (la calebasse d'or, le thermos du grand-père…).
- **Boosts temporaires** : x2 4 h (rewarded, cumulable jusqu'à 24 h), x5 5 min, « Rush de midi » (tous les cycles x3 pendant 60 s).
- **Clients spéciaux** qui traversent la rue (tap = bonus) : le Fonctionnaire pressé, le Touriste perdu, le Lutteur affamé, la Délégation de mariage (grosse commande), le Contrôleur (mini-événement drôle).

---

## 7. Gamification & rétention

### 7.1 Toujours un objectif
- **Barre d'objectif de ville** en haut de l'écran.
- **3 missions actives** (« Niveau 50 Dibiterie », « Embauche Adja Fatou », « Sers 1 000 clients ») → cauris + progression.
- **Commandes clients** : bulle « 200 dibi pour un mariage dans 3 min ! » → gros bonus.

### 7.2 Quotidien
- Connexion 7 jours (jour 7 = gérant épique ou coffre), **série** avec flamme et bouclier (rewarded, 1/semaine).
- **Roue Teranga** : 1 tour gratuit/jour + tours rewarded (max 3).
- **Coffre gratuit** toutes les 4 h.
- **Défis du jour** renouvelés à minuit.

### 7.3 Long terme
- **Album de recettes** : chaque plat débloqué = carte illustrée + anecdote culturelle + **recette réelle courte** (contenu partageable !). Album de ville complet = bonus.
- **Succès** (100+) : « Roi du Dibi », « 1 milliard de FCFA », « Teranga absolue », « Tour de l'Afrique de l'Ouest ».
- **Niveau joueur** (XP) qui débloque les fonctionnalités progressivement (onboarding naturel, pas d'écran surchargé au début).
- **Décoration** (V1.1) : personnaliser ses commerces (bâches wax, enseignes, néons) → expression du joueur, ventes cosmétiques.

### 7.4 Événements (live ops = rétention à J30+)
- **Événements de 3-7 jours** avec mini-carte dédiée (5 commerces), monnaie d'événement, classement par ligue, récompenses exclusives :
  Tabaski, Korité, Magal, Gamou, Noël, Nouvel An, **CAN** (équipes au choix, x2 si ton équipe gagne en vrai = calendrier), Fête de l'Indépendance (par pays), Saint-Louis Jazz, FEMUA, Festival de Ouidah, Fête de la musique.
- Calendrier dans `data/events/` ; activation par date locale + **Remote Config** (pour décaler sans mise à jour).
- **Pass saisonnier** (30-40 paliers, piste gratuite + premium) : gérants exclusifs, cosmétiques, cauris, x2 hors ligne permanent pendant la saison.

### 7.5 Social (WhatsApp d'abord)
- **Partage** : carte image générée (« Mon empire vaut 2,3 milliards de FCFA ! », ville, gérant préféré) + lien de parrainage.
- **Parrainage** : récompenses pour les deux joueurs aux paliers 1 / 3 / 10 amis.
- **Ligues hebdomadaires** (Bronze → Diamant), 30 joueurs de niveau proche, basées sur les gains de la semaine (V1.1, backend léger).
- **Coopératives** (guildes, V2) : objectifs communs pendant les événements.

### 7.6 Notifications locales (max 2/jour, désactivables)
« Ton coffre est prêt », « Tes gains hors ligne sont pleins », « Ta série est en danger 🔥 », « L'événement Tabaski commence ! ».

---

## 8. Monétisation

### 8.1 Rewarded (pilier principal)
| Placement | Valeur |
|---|---|
| x2 gains hors ligne | au retour, chaque session |
| x2 revenus 4 h (cumulable 24 h) | bouton permanent |
| Client VIP | gros bonus instantané |
| Boost x5 5 min | bouton |
| Tours de roue, coffre bonus, double mission, bouclier de série | quotidien |
| Accélérer un boost de gérant | recharge |
Cible : **4 à 8 rewarded par joueur actif et par jour**, sans forcer. Bouton masqué si aucune pub disponible.

### 8.2 Interstitiel (rare)
Max 1 toutes les **4 min**, jamais pendant les 10 premières minutes de vie ni pendant un écran de récompense, jamais juste après une rewarded. Désactivé pour tout joueur ayant payé.

### 8.3 Achats intégrés (V1 dès le lancement — les idle vivent aussi de l'IAP)
- **Supprimer les pubs forcées** (≈ 2,99 $) — les rewarded restent disponibles avec récompense.
- **Pack de démarrage** (une fois, prix bas) : gérant épique + cauris + x2 permanent.
- **Packs de cauris**, **pass saisonnier premium**, **abonnement « Teranga VIP »** (V1.1 : x2 permanent, hors ligne 12 h, coffre quotidien).
- **Prix localisés** (paliers bas en Afrique), offres ciblées après un pic d'engagement (jamais au 1er lancement).
- Paiement mobile money (Orange Money, Wave, MTN MoMo) via agrégateur **hors Play Store uniquement si conforme** aux règles Google/Apple en vigueur — à vérifier avant toute implémentation.

### 8.4 Pub & achats : technique
- **LevelPlay** (package UPM Ads Mediation) avec médiation : AdMob, AppLovin, Meta, Unity Ads, Pangle, Mintegral… ; bidding activé. Alternative évaluée : AppLovin MAX. Choix final justifié par l'eCPM mesuré en soft launch.
- `IAdsService` (interface) → `LevelPlayAdsService` en prod, `MockAdsService` en éditeur et tests.
- Consentement (GDPR / UMP ou CMP compatible LevelPlay) avant toute requête ; ATT sur iOS ; **identifiants de test en dev** ; récompense donnée **uniquement** sur le callback de récompense ; pause du jeu + coupure du son pendant la pub.
- **Unity IAP** (Google Play Billing + StoreKit) : catalogue de produits, confirmation en 2 étapes, validation des reçus, restauration des achats ; `IIapService` + mock.
- Reprendre la **logique** validée sur Fruit Village (règles de fréquence, consentement, récompenses), réécrite en C#.

---

## 9. Direction artistique

### 9.1 Style
- **3D low-poly stylisé** (URP), toon shading 2-3 tons (Shader Graph) + contour, personnages **chibi** (grosse tête), formes rondes, couleurs saturées.
- **Une palette par ville** (Dakar : ocre, bleu océan, wax orange ; Abidjan : vert lagune, orange vif ; Bamako : terre rouge, indigo bogolan ; Lagos : jaune danfo, vert ; Accra : kente or et rouge).
- Motifs **wax, bogolan, kente, adinkra** dans l'UI et les décors (motifs génériques, aucun motif de marque déposée).
- Police ronde et grasse (Baloo 2 / Fredoka) en **TextMeshPro** (SDF), chiffres énormes et lisibles.
- **Assets** : base Asset Store low-poly (Synty POLYGON City / Street Food / Town ou équivalent, licence vérifiée), **réhabillée** en Blender (via MCP) pour l'Afrique de l'Ouest : façades, bâches wax, car rapide, danfo, tenues. Animations Mixamo ou packs Asset Store. Aucun asset ne doit rester « générique occidental » à l'écran.

### 9.2 Scène principale : la rue vivante
- Caméra **orthographique isométrique** (~45°), scroll vertical le long de la rue, pinch-zoom léger, tap sur un commerce = zoom doux + panneau d'achat.
- Chaque commerce = **diorama** qui évolue visuellement par paliers (5 évolutions : brouette → étal → bâche → boutique → façade néon).
- **Clients animés** qui arrivent, font la queue, mangent, repartent ; foule qui grossit avec la popularité.
- **Gérants et employés animés** (le dibiteur retourne la viande, Ndèye grille sous l'ampoule, la vendeuse d'alloco frit).
- Vie de rue : car rapide, taxi jaune-noir, danfo à Lagos, moto-taxi, chèvres, enfants qui jouent au foot, vendeur ambulant.
- **Monument** de la ville en arrière-plan, cycle jour/nuit, météo légère (harmattan, pluie d'hivernage).

### 9.3 Juice (non négociable)
- Billets FCFA qui volent vers le compteur en arc, popups « +1,2K » avec rebond.
- Squash & stretch sur tout ce qui se touche, vibration légère.
- Palier : confettis wax, « VITESSE x2 ! », zoom caméra.
- Déblocage : rideau de fer qui s'ouvre, foule qui accourt, son de sabar.
- Nouvelle ville : **cinématique de 5 s** (le car rapide part vers Abidjan sur une carte stylisée).
- Coffres : secousse, lumière, cartes qui jaillissent par rareté (moment fort à soigner).
- Compteur qui roule, pulse quand il monte.

### 9.4 Son
- Musique par ville (mbalax, coupé-décalé, musique mandingue, afrobeats, highlife) — **instrumentale, libre de droits ou commandée**, jamais de morceaux protégés.
- SFX : caisse, grill, friture, thé versé, foule, klaxons ; répliques vocales courtes par gérant.
- Bus Master / Music / SFX / Voice, réglages séparés.

---

## 10. UX
- **Onboarding sans texte** (< 60 s) : doigt animé → tap → achat → gérant. Fonctionnalités dévoilées une par une par le niveau joueur.
- HUD : FCFA, cauris, boost actif, objectif de ville ; barre basse : Rue / Gérants / Upgrades / Villes / Boutique.
- Bouton **« Acheter le meilleur »** (suggère l'achat au meilleur rendement) pour les joueurs casual.
- Cibles tactiles ≥ 48 dp, une main, lisible en plein soleil, option daltonisme.
- Option « effets réduits » + qualité Bas / Moyen / Haut automatique.

---

## 11. Technique (Unity 6 LTS)

### 11.1 Stack
| Besoin | Choix |
|---|---|
| Moteur | Unity 6 LTS, C# |
| Rendu | **URP** (profil mobile), Shader Graph (toon + contour), SRP Batcher, GPU Instancing |
| Contenu | **Addressables** : Dakar dans l'APK, autres villes téléchargées à la demande (+ Play Asset Delivery) |
| UI | uGUI + **TextMeshPro** (ou UI Toolkit si le choix est justifié), un seul thème, Safe Area gérée |
| Animation / tweens | Animator + **DOTween** (ou PrimeTween, zéro allocation) |
| Pub | **LevelPlay** (médiation) |
| Achats | **Unity IAP** |
| Live ops | **Unity Gaming Services** : Authentication (anonyme), **Remote Config**, **Cloud Save**, **Analytics**, **Leaderboards** (ligues), Cloud Code (validation serveur, V1.1) |
| Crash | Unity Cloud Diagnostics ou Firebase Crashlytics |
| Notifications | Mobile Notifications (package officiel) |
| Localisation | Unity Localization (tables FR, EN, WO, PCM, HA) |
| Entrées | Input System (tap, glisser, pinch) |
| Tests | **Unity Test Framework** (EditMode pour la logique, PlayMode pour l'intégration) |

### 11.2 Architecture
```
Assets/_Game/
  Scripts/
    Core/        BigDouble, EconomyMath, OfflineCalculator, PrestigeCalculator, GameClock
                 (C# pur, aucune dépendance à UnityEngine → testable et simulable)
    Systems/     EconomySystem, BusinessSystem, ManagerSystem, PrestigeSystem, MissionSystem,
                 DailyRewardSystem, CollectionSystem, EventSystem
    Services/    IAdsService, IIapService, IAnalyticsService, IRemoteConfigService,
                 ICloudSaveService, ISaveService (+ implémentations réelles et mocks)
    Presentation/ City, BusinessView, CustomerAgent, ManagerView, CameraRig, Juice, UI/…
    Bootstrap/   GameBootstrap (composition root : crée et injecte les services)
  Data/          ScriptableObjects (villes, commerces, gérants, upgrades, événements, configs)
  Art/           Models (.fbx/.glb), Materials, Shaders, Textures (atlas palette par ville), VFX
  Audio/         Music, SFX, Voice (+ AudioMixer)
  Addressables/  groupes par ville
  Tests/         EditMode/, PlayMode/
Tools/           blender/ (scripts bpy), screenshots/, build/
```
- **Logique pure séparée de la présentation** : les systèmes ne connaissent ni MonoBehaviour ni UI ; la présentation écoute des événements C# (ou un bus d'événements léger).
- **Injection simple** par le composition root (pas de singletons cachés ; un framework DI seulement si justifié).
- Commerces hors écran : simulation logique uniquement, aucune animation.
- Zéro allocation par frame dans la boucle de jeu (pas de LINQ, pas de string concat dans Update, pools partout).

### 11.3 Sauvegarde
- JSON versionné (Newtonsoft ou JsonUtility + DTO), migrations, écriture atomique, checksum anti-édition, autosave 30 s + `OnApplicationPause`.
- **Cloud Save UGS** dès la V1 (changement de téléphone fréquent en Afrique) : fusion locale/cloud par horodatage + compteur de version.

### 11.4 Performance (téléphone 2 Go, GPU entrée de gamme)
- URP mobile : pas de post-process lourd, ombres 1 cascade (désactivées en qualité Bas), MSAA x2 max, HDR off.
- < 100 000 triangles, < 80 batches (SRP Batcher + instancing), 60 FPS visés, 30 minimum, `Application.targetFrameRate` géré.
- ≤ 20 clients visibles, **pool d'objets**, LOD, culling ; atlas palette unique par ville ; textures ASTC/ETC2.
- IL2CPP + ARM64, managed stripping, APK de base < 80 Mo, villes en Addressables.
- Démarrage à froid < 3 s ; mesures au **Profiler** et **Memory Profiler** sur appareil réel à chaque jalon.
- Qualité auto Bas / Moyen / Haut selon RAM et GPU + option « effets réduits ».

### 11.5 Pièges connus
- Ne jamais éditer les YAML Unity à la main : passer par l'éditeur (Unity CLI / MCP) ou des scripts Editor.
- `.meta` toujours commités ; Git LFS pour les binaires ; `.gitignore` Unity standard.
- Pub : identifiants de test en dev, récompense sur callback uniquement.
- Achats : ne livrer le contenu qu'après confirmation (flux en 2 étapes), gérer les achats en attente et la restauration.
- Vérifier au moment de la release : target SDK, pages 16 Ko (Unity 6 compatible, vérifier chaque SDK natif), test fermé Play, ATT et confidentialité iOS.
- Licence Unity : vérifier le plan requis selon le chiffre d'affaires en vigueur.

### 11.6 Analytics & Remote Config
- Événements : session, tutoriel (chaque étape), déblocage, gérant, palier, prestige, ville, mission, coffre, pub (offerte / vue / récompensée / échouée par placement), IAP, partage, notification ouverte.
- **Remote Config** : équilibrage (coûts, gains, plafonds), fréquence d'interstitiel, événements, offres.
- **A/B tests** dès le lancement : plafond hors ligne, prix du pack de démarrage, fréquence d'interstitiel.

## 12. KPIs cibles & seuils de décision

| KPI | Cible | Si en dessous |
|---|---|---|
| Fin du tutoriel | ≥ 85 % | Simplifier l'onboarding |
| J1 | ≥ 40 % | Revoir les 10 premières minutes |
| J7 | ≥ 15 % | Rythme de déblocage, missions, gains hors ligne |
| J30 | ≥ 6 % | Prestige, événements, collection |
| Sessions / jour | ≥ 5 | Coffres, notifications, plafond hors ligne |
| Rewarded / joueur actif / jour | ≥ 4 | Placements, valeur des récompenses |
| Conversion payante | ≥ 1,5 % | Pack de démarrage, prix localisés |

Phase de **soft launch** dans 2-3 pays tests (ex. Sénégal, Côte d'Ivoire + un pays à fort eCPM) avant le lancement mondial.

---

## 13. Jalons

| Jalon | Contenu | Validation |
|---|---|---|
| **M1 — Cœur économique** | Projet Unity 6 + URP + structure + CI des tests, BigDouble, formules, 8 commerces de Dakar, tap, niveaux, gérants, paliers, achat xN/MAX, hors ligne, sauvegarde. UI placeholder, cubes. **Simulation économique + CSV.** | Tests verts, courbe de progression validée |
| **M2 — Rue vivante** | Scène isométrique, clients et employés animés (placeholders), cycle jour/nuit, ramassage de l'argent, juice complet, sons placeholders | Capture vidéo, 60 FPS |
| **M3 — Méta** | Prestige « nouvelle ville », Étoiles, upgrades, gérants à rareté, coffres, missions, connexion 7 j, roue, album, succès, niveau joueur | Simulation 30 jours conforme aux cibles |
| **M4 — Habillage Dakar** | Assets Asset Store réhabillés + modèles Blender (MCP), Shader Graph, UI finale, musique, répliques, onboarding | Planche visuelle validée avant intégration |
| **M5 — Monétisation & live ops** | LevelPlay (rewarded, interstitiel, médiation), Unity IAP, consentement/ATT, UGS (Analytics, Remote Config, Cloud Save) | Pubs de test OK, achats en sandbox OK |
| **M6 — Abidjan + Bamako** | 2 villes complètes en Addressables, cinématique de voyage (Timeline) | Relecture culturelle validée |
| **M7 — Release** | Build signé, fiche store FR/EN, captures, vidéo, soft launch | Checklist Play / App Store |
| **Live ops** | 1 événement / 2-3 semaines, 1 ville / 6-8 semaines, pass saisonnier, ligues, cloud save, coopératives | KPIs §12 |

---

## 14. Règles pour Claude Code
- Avancer **jalon par jalon** ; ne jamais commencer M(n+1) sans ma validation de M(n).
- **Économie d'abord** : aucune UI tant que les tests et la simulation ne sont pas verts.
- Toute valeur d'équilibrage dans des ScriptableObjects (surchargeables par Remote Config), jamais en dur.
- C# : logique pure (Core/Systems) sans dépendance Unity, présentation séparée, événements C#, tests Unity Test Framework systématiques (EditMode d'abord).
- Piloter l'éditeur via Unity CLI / MCP ou scripts Editor ; jamais d'édition manuelle des fichiers .unity / .prefab / .asset.
- Placeholders tant que les assets finaux ne sont pas validés.
- Tout contenu culturel (noms, plats, répliques, anecdotes) listé dans `docs/CULTURE_REVIEW.md` pour relecture humaine avant intégration.
- Reprendre les règles éprouvées sur Fruit Village (consentement, fréquence des pubs, sauvegarde, checklist de release), réécrites en C#.
- À chaque fin de jalon : tests verts, captures, mesures de performance, liste de ce que je dois fournir.
