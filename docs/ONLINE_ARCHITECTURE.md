# Architecture online — modèle hybride (proposition, à valider avant M5)

## Principe
Gargote Tycoon reste **100 % jouable hors ligne** (CLAUDE.md §1). Le serveur ne valide donc pas chaque tap.
Il est **l'autorité sur ce qui a une valeur hors de la partie** et **vérifie la plausibilité** du reste à chaque synchronisation.

La logique économique est en C# pur (`Scripts/Core`, `Scripts/Systems`) et ne dépend pas d'Unity : le **même code** peut
tourner dans **Cloud Code (modules C#)** pour recalculer et borner ce que le client déclare. Aucune double implémentation.

## Qui fait autorité
| Donnée | Autorité | Raison |
|---|---|---|
| FCFA, niveaux, gérants de base, upgrades FCFA | Client, vérifié à la synchro | Jeu hors ligne ; une triche ne touche que la partie du tricheur |
| Gains hors ligne | Serveur (horloge serveur) au retour en ligne ; client (horloge monotone + système, plafonnée) hors ligne | Pas d'exploitation par changement d'heure |
| Cauris (premium), achats IAP | Serveur uniquement | Valeur réelle ; reçus validés côté serveur |
| Récompenses (missions, connexion, roue, coffres, événements) | Serveur, idempotentes (1 réclamation par identifiant et période) | Empêche la double réclamation |
| Étoiles Teranga, ville atteinte | Client, vérifié à la synchro (formule de prestige recalculée) | Dérivées des gains |
| Classements | Serveur, à partir de données vérifiées | Équité |

## Synchronisation (Cloud Save + Cloud Code)
1. Le client envoie : sauvegarde versionnée (DTO M1), compteur de version, horodatages monotone et système, journal compact
   des événements de valeur (prestiges, achats d'upgrades d'étoiles).
2. Cloud Code recalcule une **borne haute** : revenu max atteignable depuis la dernière sauvegarde validée (état précédent,
   temps serveur écoulé, plafonds hors ligne, boosts actifs) avec `OfflineCalculator` / `PrestigeCalculator`.
3. Dans la borne : acceptée. Au-delà : ramenée à la borne et marquée (Analytics). Pas de bannissement automatique en V1.
4. Conflit entre deux appareils : la sauvegarde la plus avancée (version, puis gains totaux) gagne, l'autre est archivée.

## Journal économique
Chaque crédit et débit de **cauris** et chaque récompense serveur sont enregistrés : identifiant unique, source, horodatage serveur.
Les FCFA ne sont pas journalisés un par un (volume) : seuls les écarts de plausibilité sont tracés.

## Récompenses : en secondes de revenu, pas en montants fixes
Avec une croissance exponentielle, un montant fixe (ex. 20 000 FCFA) ne vaut plus rien après une heure de jeu.
Missions, coffres et clients spéciaux donnent **N secondes du revenu actuel** (avec un minimum), déjà le cas pour les
clients spéciaux (`SpecialCustomersConfig`). Les plafonds sont exprimés de la même façon et réglés par Remote Config.

## Services
- UGS Authentication (anonyme, puis liaison Google/Apple pour changer de téléphone), Cloud Save, Cloud Code, Remote Config,
  Analytics, Leaderboards ; Unity IAP pour l'argent réel.
- **UGS Economy n'est pas utilisé** : la note fournie indique qu'il n'accepte plus de nouveaux projets depuis le
  8 septembre 2026. Information à revérifier au début de M5, mais sans impact sur ce plan.

## Classements
Jamais sur le solde de FCFA (favorise l'ancienneté) : ligues hebdomadaires sur les **gains de la semaine** (§7.5),
classements par ville, succès.

## Hors périmètre au lancement
Échange de FCFA ou d'objets entre joueurs (fraude, contrôle de l'économie). Visites d'empires et coopératives en V2.

## Phasage
| Jalon | Contenu online |
|---|---|
| M5 | Authentification, Cloud Save + synchro avec borne de plausibilité, cauris serveur, IAP, Remote Config, Analytics |
| V1.1 | Ligues hebdomadaires, missions et récompenses serveur idempotentes, événements |
| V2 | Coopératives, visites, objectifs collectifs |
