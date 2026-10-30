# M2 — Rue vivante : rapport

## État
- Tests : EditMode 133/133, PlayMode 4/4 (vue = état après achat/palier/déblocage/gérant/prestige ; 1 h accélérée stable ; aucun client pour un commerce hors écran ; pools non réalloués ; `Time.timeScale` = 1).
- Simulation M1 : 124 OK / 0 échec, CSV inchangés (non-régression).
- Captures : `Tools/screenshots/m2_phone/` (téléphone), `Tools/screenshots/m2/` (PC). Scénario rejouable : APK de dev + `adb shell am start -n com.smartiaservices.gargotetycoon/com.unity3d.player.UnityPlayerGameActivity --ez gargoteCapture true` (résultats dans `Android/data/<package>/files/capture/`).

## Performance — Samsung Galaxy A23 (SM-A235F, Snapdragon 680 / Adreno 610, 4 Go, écran 90 Hz), APK dev IL2CPP ARM64
Nuit, 8 commerces ouverts, 6 gérants, foule au plafond de la qualité, 15 s par mesure.

| Qualité | FPS moyen | Frame p95 | Batches max | Triangles | Mémoire système | Frames avec GC |
|---|---|---|---|---|---|---|
| Bas | 59,9 | 16,8 ms | 65 | 10K | 391 Mo | 2/897 (1,5 Ko) |
| Moyen (détecté) | 59,9 | 16,8 ms | 71 | 15K | 392 Mo | 0 |
| Haut | 59,9 | 16,8 ms | 72 | 16K | 392 Mo | 0 |
| Haut, temps x100 (stress) | 59,9 | 16,9 ms | 80 | 16K | 392 Mo | 7/897 (≤ 11 Ko) |

Marge mesurée sans plafond (avant passage en 60 Hz) : Bas ≈ 86 FPS, Moyen ≈ 72, Haut ≈ 64.
Correctifs issus de la mesure : mode d'affichage 60 Hz demandé sur écrans 90/120 Hz (sinon 45 FPS), pipeline URP mobile imposé,
ombres « blob » pour les personnages, ombres coupées sur les petits éléments, pool de tweens préalloué.

Restes connus : allocations rares sous rafale (x100), popups qui se superposent aux badges « x2 », label du client spécial sous le HUD.
Non mesuré : Frame Debugger / Memory Profiler sur l'appareil (le Profiler peut se connecter à l'APK de dev), démarrage à froid.

## Réglages exposés (ScriptableObjects, `Assets/_Game/Data/Visual/`)
| Asset | Principaux réglages (défaut) |
|---|---|
| CameraConfig | pitch 45°, distance 40, demi-largeur visible 5,4, zoom 0,8–1,3, inertie 4,5, overscroll 3, recentrage 0,45 s |
| StreetConfig | 8 emplacements alternés x ±3,9 tous les 5 m, chaussée 1,3, stades aux niveaux 1/25/100/300/750, palette Dakar |
| CrowdConfig | 20 clients max, 6 par commerce, vitesse de marche, rush, tenues/teintes |
| JuiceConfig | 2–5 billets, regroupement « xN », popups, pile auto (délai), punch, confettis, secousse, bannière, main après 3 s, réplique gérant 20–60 s, son d'achat ≥ 50 ms, effets réduits (particules /3), 400 tweens |
| DayNightVisualConfig | gradients soleil/ciel/ambiance, lampes, cycle 24 min (économie M1) |
| QualityConfig | Bas/Moyen/Haut : ombres, MSAA, renderScale 0,75/0,85/1, clients 12/16/20, particules 0,4/0,7/1, 60 FPS ; pipeline mobile |
| AudioConfig + SoundBank | mixer Master/Music/SFX/Voice (exposés), 14 voix, pitch ±5 %, volumes et intervalles par son |
| SpecialCustomersConfig (`Data/`) | fonctionnaire, touriste, lutteur, délégation de mariage : poids, bonus en secondes de revenu, apparition 120–240 s |

## À fournir pour M4
- Captures de référence de vraies rues de Dakar (tangana, dibiterie, gargote, maïga la nuit) et des tenues.
- Musiques jour/nuit (mbalax instrumental libre de droits ou commandé), sabar enregistré, ambiances de rue, voix courtes des gérants.
- Choix du pack d'assets low-poly (licence) et police définitive.
- Relecture des textes de `docs/CULTURE_REVIEW.md` (section M2).
- Vidéo de 60 s sur téléphone : à enregistrer avec l'enregistreur d'écran du téléphone (APK de dev installé).
