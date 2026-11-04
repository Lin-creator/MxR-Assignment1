# MxR Assignment 1 – 3D-Drucker im eigenen Raum

Mixed Reality Praktikum – Assignment 1 (Passthrough, Wandschild, interaktive Maschine) für **Meta Quest 2 / 3 / 3S**.

| Aufgabe | Status |
|---------|--------|
| 01 Passthrough aktiviert | ✅ |
| 02 Schild an der Wand (MRUK Key Wall) mit Name + Beschreibung | ✅ |
| 03 Importiertes Maschinenmodell im Raum platziert | ✅ |
| 04 Interaktion mit der Maschine | ✅ |

## 1.1 Allgemeine Projektinformationen

**Projektname:** 3D-Drucker im eigenen Raum (*MxR Assignment 1*)

**Kurze Beschreibung:**
Die App zeigt den echten Raum im Passthrough und ergänzt ihn um einen virtuellen 3D-Drucker:

- An der **Hauptwand des Raums** (MRUK *Key Wall*) hängt ein Infoschild mit Titel, Name des Autors,
  einer Beschreibung, wie ein FDM-Drucker funktioniert, und einer Live-Statuszeile.
- Der **3D-Drucker** steht auf dem größten Tisch im Raum oder, wenn es keinen passenden gibt, auf einem
  virtuellen Podest vor der Hauptwand neben dem Schild.
- Das **Display des Druckers ist ein Touchscreen:** Antippen (Finger oder Controller) startet, pausiert und setzt einen Druck fort;
  es „atmet“ (pulsierendes Leuchten), solange es auf eine Eingabe wartet.
- **Modellauswahl:** Fünf Modelle (Würfel, Zahnrad, gedrehter Turm, Schachbauer, Vase) stehen als halbtransparente
  „Geister“ auf dem Druckbett – das gewählte in der Mitte, das vorherige links und das nächste rechts, kleiner und
  noch blasser. Mit dem Finger (oder der Controller-Spitze) wischt man seitlich über die Vorschauen: sie leuchten auf, folgen dem Finger und rasten beim Loslassen auf dem gewählten Modell ein.
  Ein Druck dauert je nach Modell 5–10 Sekunden; der Geist bleibt als Umriss stehen, in den das Teil hineinwächst.
  Druckkopf, Bett und Portal fahren dabei realistisch, und auf dem Bett wächst Schicht für Schicht ein Bauteil.
  Feedback gibt es über das aufleuchtende Display, eine Statusleuchte, Text und Hinweise auf dem Display, Motorgeräusch, Pieptöne,
  einen Abschluss-Gong, Controller-Vibration und die Statuszeile am Schild.
- Das **fertige Bauteil kann man greifen** – ein schwebender Hinweis „Grab me!“ zeigt darauf –, werfen oder ablegen; es fällt auf den echten Boden oder Tisch
  (unsichtbare Raum-Collider der *Effect Mesh*).
- **Schild und Drucker lassen sich verschieben:** Mit dem Strahl (Controller-Trigger / Pinch mit der Hand)
  gleitet das Schild an den Wänden entlang und springt auf andere Wände; der Drucker wandert über Boden
  und Tische (auf Tischen verschwindet das Podest) und dreht sich dabei zum Nutzer.
- **Raumwechsel:** Hebt man die linke Hand bzw. den linken Controller ins Blickfeld, erscheint ein
  Handgelenk-Menü mit *Re-place* (Raum neu laden, alles neu platzieren) und *Scan room* (Space Setup).
  Wechselt man während die App läuft in einen anderen Raum, wird das automatisch erkannt.

## 1.2 Externe Ressourcen

### 3D-Modelle, Texturen, Materialien

| Ressource | Quelle / Autor | Lizenz | Änderungen |
|-----------|----------------|--------|------------|
| 3D Printer (Maschinenmodell) | [Gregsterius](https://sketchfab.com/Gregsterius), [Sketchfab](https://sketchfab.com/3d-models/3d-printer-0e825a8d48b14bee883a131fe029d4fc) | [CC BY 4.0](http://creativecommons.org/licenses/by/4.0/) | Mit einem Blender-Skript (`Tools/Blender/prepare_printer.py`) bearbeitet: ~80 Einzelteile zu beweglichen Baugruppen zusammengefasst (Frame, Bed, Gantry, PrintHead, Kabel, Display), Pivots für die Animation gesetzt, auf 0,45 m skaliert, Display-Material leicht leuchtend (Emission), `DisplayAnchor` für den Display-Text ergänzt. Original unverändert in `SourceAssets/Printer3D/` |
| Poke-Button | Meta Interaction SDK, Sample-Prefab `OculusInteractionSamplePokeButton` | [Meta Platform Technologies SDK License](https://developer.oculus.com/licenses/oculussdk) | Prefab unverändert; zur Laufzeit unsichtbar auf dem Drucker-Display (Touch-Fläche) und im Handgelenk-Menü instanziiert |
| Druckbare Modelle (`Assets/Models/PrintModels`) | Eigene, erzeugt mit `Tools/Blender/make_print_models.py` | – | – |
| Materialien (`Assets/Materials`) | Eigene, abgeleitet von URPs `Lit.mat` (`PrintGhost`: URP Unlit, transparent) | – | Farben / Emission / Transparenz angepasst |
| Schrift LiberationSans SDF | TextMesh Pro Essential Resources (Unity) | OFL | Unverändert |

### Sounds und Animationen

- **Sounds:** alle selbst synthetisiert mit `Tools/Audio/generate_sounds.py` (nur Python-Standardbibliothek):
  Motorgeräusch, Start-/Pause-Pieptöne, Fertig-Gong, Aufnehmen-/Ablegen-Geräusch.
- **Animationen:** keine Animation-Clips; die Druckbewegung wird prozedural im Code berechnet (`PrinterController`).

### Plugins und Bibliotheken

Die Meta-Pakete stehen unter der [Meta Platform Technologies SDK License](https://developer.oculus.com/licenses/oculussdk) und kommen aus der Meta-Registry (`npm.developer.oculus.com`, in `Packages/manifest.json` eingetragen), die Unity-Pakete aus der Unity-Registry.

| Paket | Version | Wofür |
|-------|---------|-------|
| Meta XR Core SDK (`com.meta.xr.sdk.core`) | 207.0.1 | Kamera-Rig, Passthrough, Controller-Input, Building Blocks |
| Meta MR Utility Kit (`com.meta.xr.mrutilitykit`) | 207.0.1 | Raummodell (Wände, Boden, Tische), Key Wall, Raycasts gegen den Raum, Effect Mesh |
| Meta XR Interaction SDK (`com.meta.xr.sdk.interaction(.ovr)`) | 207.0.1 | Poke, Greifen (Hand + Controller), Strahl-Interaktion |
| Unity OpenXR Plugin (`com.unity.xr.openxr`) | 1.19.0 | OpenXR-Runtime mit Meta-XR-Feature-Gruppe |
| Universal Render Pipeline | 17.3.0 | Rendering |
| Input System | 1.20.1 | Unity-Standard-Input |
| uGUI / TextMesh Pro | 2.0.0 | 3D-Text auf Schild, Display und Menü |

## 1.3 Technische Details und Installation

### Unity-Version und Voraussetzungen

| Werkzeug | Version |
|----------|---------|
| Unity | **6000.3.27f1** mit *Android Build Support* (OpenJDK, Android SDK & NDK) |
| Git + **Git LFS** | Git LFS ≥ 3.x |
| Headset | Meta Quest 2, 3 oder 3S im Entwicklermodus |
| Optional | Blender 5.2 (nur um das Druckermodell neu zu erzeugen) |

Build-Einstellungen: Android, IL2CPP, ARM64, Min. SDK 32, Single-Pass-Instanced-Rendering, OpenXR.

### Dateistruktur

```
Assets/
  Scenes/MRScene.unity   die einzige Szene der App
  Scripts/
    Common/              wiederverwendbar: TextFactory, ControllerHaptics, RayDraggable, HoverTint
    Sign/                Wandschild: WallSign, WallSignPlacer, SignRepositioner
    Printer/             Drucker: PrinterController, PrinterPlacer, PrinterTouchscreen, PrinterFeedback,
                         PrinterColliders, PrintedPart, PrinterRepositioner, PrintModelSelector, GrabHint
    Room/                Raumwechsel: RoomManager, WristMenu
  Models/Printer3D/      bearbeitetes Druckermodell (FBX, Git LFS)
  Models/PrintModels/    fünf druckbare Modelle (FBX, Git LFS, Read/Write aktiv)
  Prefabs/               PrintedPart (greifbares Bauteil)
  Materials/  Audio/     eigene Materialien und Sounds
  TextMesh Pro/          TMP Essential Resources
  Settings/ XR/ Resources/ Oculus/ Plugins/Android/
                         URP-, OpenXR- und Meta-Einstellungen, AndroidManifest
SourceAssets/            unveränderte Originale von Drittanbietern (nicht von Unity importiert)
Tools/Blender/           Blender-Skripte, die Assets/Models erzeugen
Tools/Audio/             Python-Skript, das die Sounds in Assets/Audio erzeugt
Packages/                manifest.json + packages-lock.json (Abhängigkeiten)
ProjectSettings/         Unity-Projekteinstellungen
Library/ Temp/ Logs/ UserSettings/   werden lokal erzeugt – ignoriert (.gitignore)
```

### Installationsanleitung

```bash
# 1. Git LFS einmal pro Rechner installieren (sonst kommen Binärdateien nur als Text-Pointer an)
git lfs install

# 2. Klonen (LFS-Dateien werden automatisch geladen)
git clone <repo-url>
cd <repo>
git lfs pull   # nur nötig, falls LFS erst nach dem Klonen installiert wurde
```

3. Ordner in **Unity Hub → Add → Add project from disk** öffnen (Editor `6000.3.27f1`).
   Beim ersten Öffnen baut Unity `Library/` neu auf (dauert einige Minuten).
4. **Szene:** Es gibt nur eine Szene, **`Assets/Scenes/MRScene.unity`**; sie ist als einzige in den Build Settings eingetragen.
5. Quest per USB-C verbinden und USB-Debugging im Headset erlauben.
6. *File → Build Profiles →* **Android** wählen, *Switch Platform*, unter *Run Device* die Quest auswählen, **Build And Run**.
7. Im Headset: Raum einrichten, falls noch nicht geschehen (Quest 2: *Einstellungen → Physischer Raum → Space Setup*;
   Quest 3 scannt automatisch), und beim Start die Berechtigung für *Raumdaten* erlauben.
   Ohne Raumdaten werden Schild und Drucker vor dem Nutzer platziert.

### Bedienung

| Aktion | Hand | Controller |
|--------|------|------------|
| Modell wählen | mit dem Zeigefinger seitlich über die Vorschauen auf dem Druckbett wischen | ebenso mit der Controller-Spitze |
| Druck starten / pausieren / fortsetzen | Mitte des Displays antippen | ebenso mit der Controller-Spitze |
| Fertiges Bauteil nehmen | greifen | Grip-Taste |
| Schild / Drucker verschieben | mit dem Strahl zielen, Pinch halten | zielen, Trigger halten; Stick vor/zurück = weiter weg / näher |
| Handgelenk-Menü | linke Hand ins Blickfeld heben | linken Controller ins Blickfeld heben; Menü-Taste (≡) = *Re-place* |

### Szenenaufbau (`MRScene`)

| Objekt | Zweck |
|--------|-------|
| `[BuildingBlock] Camera Rig` | OVRCameraRig (Kopf- und Controller-Tracking). OVRManager: Passthrough + Scene Support erforderlich, Zielgeräte Quest 2/3/3S |
| `[BuildingBlock] Passthrough` | OVRPassthroughLayer als Underlay; die Kamera löscht transparent, so ist der echte Raum sichtbar |
| `[BuildingBlock] MR Utility Kit` | MRUK lädt das Raummodell aus dem Space Setup (Data Source: *Device*) |
| `[BuildingBlock] Interactions Rig` | Interaction-SDK-Interaktoren (Poke, Greifen, Strahl) für Hände und Controller; Locomotion deaktiviert (künstliche Bewegung würde virtuelle Inhalte gegen den echten Raum verschieben) |
| `[BuildingBlock] Effect Mesh` | unsichtbare Collider für den echten Raum, damit Bauteile auf Boden und Möbeln landen |
| `Directional Light` | Licht für die Lit-Materialien (ohne Schatten, Performance) |
| `WallSign` | `WallSign`, `WallSignPlacer`, `RayDraggable`, `SignRepositioner` |
| `Printer3D` | Druckermodell + `PrinterController`, `PrinterPlacer`, `PrinterTouchscreen`, `PrinterFeedback`, `PrinterColliders`, `RayDraggable`, `PrinterRepositioner` |
| `RoomManager` | `RoomManager` + `WristMenu` |

### Technische Umsetzung

**Wandschild** (`Assets/Scripts/Sign/`)
- `WallSign` baut das Schild aus Code: dunkle Rückwand, blaue Akzentbalken, TextMeshPro-Texte (Titel, Name,
  Beschreibung, Live-Statuszeile). Alle Texte sind im Inspector änderbar.
- `WallSignPlacer` wartet auf MRUKs *Scene loaded*-Callback, nimmt die **Key Wall** des Raums
  (`MRUKRoom.GetKeyWall` – die längste Wand ohne andere Wand dahinter) und zentriert das Schild 1,5 m über
  dem Boden, 1 cm vor der Wand, Blickrichtung in den Raum. Ohne Raumdaten: 2 m vor dem Nutzer.

**Drucker** (`Assets/Scripts/Printer/`)

| Komponente | Aufgabe |
|------------|---------|
| `PrinterController` | Simulierter Druckauftrag: bewegt Portal (Z), Druckkopf (X) und Bett (Y) prozedural, sodass die Düse Kreise zeichnet, während das Bauteil Schicht für Schicht wächst. API: `TogglePrint()`, `CancelPrint()`, `onStateChanged` (Idle → Printing ⇄ Paused → Finished) |
| `PrinterPlacer` | Nach dem Laden des Raums: auf dem größten Tisch, falls groß genug (zum Nutzer gedreht), sonst auf einem virtuellen Podest (0,6 × 0,55 m, 0,75 m hoch) vor der Key Wall neben dem Schild, sonst vor dem Nutzer |
| `PrinterTouchscreen` | Macht das Display zum Touchscreen: Der Poke-Button des Interaction SDK wird unsichtbar auf die Display-Oberfläche gesetzt (Position `DisplayAnchor`, Ausrichtung entlang der Bildschirmnormalen – aus den Bildschirmflächen des FBX gemessen mit `Tools/Blender/measure_screen.py` –, auf Bildschirmgröße skaliert); Antippen startet / pausiert / setzt fort. Feedback: das Display pulsiert, solange es auf eine Eingabe wartet, leuchtet beim Annähern heller und blitzt beim Antippen (Emission des Display-Materials), Klick-Sound, Vibration |
| `PrintModelSelector` | Fünf Modelle (Mesh aus `PrintModels.fbx`, Druckzeit 5–10 s) als transparente, sich drehende Geister auf dem Bett (Material `PrintGhost`, Alpha per `MaterialPropertyBlock`): Auswahl in der Mitte, vorheriges links / nächstes rechts kleiner und blasser; beim Wischen folgen sie dem Finger und rasten danach ein; ausgeblendet, solange ein fertiges Teil auf dem Bett liegt. Setzt den Auftrag per `PrinterController.SetJob` – das Bauteil-Prefab bekommt das Mesh und einen konvexen `MeshCollider` |
| `PreviewSwipe` | Nutzt die Poke-Interaktoren des Interaction SDK (Zeigefingerspitze bzw. Controller-Spitze) als Finger: In einer Box um die Vorschauen leuchten diese auf, seitliches Bewegen zieht das Karussell mit, nach einer halben Vorschau-Breite ist das nächste/vorherige Modell gewählt (Klick-Sound, Vibration) |
| `GrabHint` | Schwebendes Schild „Grab me!“ mit Pfeil über dem fertigen Teil, zum Nutzer gedreht; verschwindet, sobald das Teil gegriffen wird |
| `PrinterFeedback` | Statusleuchte (blau bereit, grün pulsierend druckt, gelb blinkend pausiert, weiß fertig), Text auf dem Display mit Hinweis, was Antippen als Nächstes bewirkt, räumliches Motorgeräusch + Pieptöne + Gong, Statuszeile am Schild |
| `PrintedPart` + `Prefabs/PrintedPart.prefab` | Bauteil mit Rigidbody, Collider, `Grabbable`, Hand- und Controller-Grab (Quick Action *Add Grab Interaction*). Während des Drucks nicht greifbar; danach wird es beim Loslassen vom Bett gelöst und per `Grabbable.ForceKinematicDisabled` zum Physikobjekt. Bis zu 5 entnommene Teile bleiben im Raum |
| `PrinterColliders` | MeshCollider am Rahmen, BoxCollider am Bett, damit man Teile zurücklegen kann |

**Verschieben** – `RayDraggable` (`Common/`) erzeugt zur Laufzeit einen Trigger-`BoxCollider` als Griff,
eine `ColliderSurface` und ein `RayInteractable`; Feedback: Aufhellen beim Zielen, blau beim Ziehen (`HoverTint`),
Aufnehmen-/Ablegen-Sound, Vibration und ein kurzer Tick beim Wand- bzw. Untergrundwechsel.

| Objekt | Verhalten |
|--------|-----------|
| Schild | `SignRepositioner`: `MRUKRoom.Raycast` gegen die Wand-Anker; das Schild folgt dem Strahl auf der Wand, bleibt innerhalb der Wandfläche und wechselt auf die Wand, auf die der Strahl zeigt |
| Drucker | `PrinterRepositioner`: hält den gegriffenen Punkt in fester Entfernung auf dem Strahl (Stick schiebt/zieht). Ein Raycast nach unten in das Raummodell findet den Untergrund: Tisch/Schrank → direkt darauf, Podest ausgeblendet; Boden → auf dem Podest; außerhalb des Raums → bleibt stehen. Dreht sich beim Verschieben zum Nutzer |

**Raumwechsel** (`Assets/Scripts/Room/`) – MRUK lädt das Raummodell nur beim Start. Geht der Nutzer in einen
anderen Raum (andere Boundary), lässt sich der alte Raum nicht mehr orten und die Inhalte blieben an den alten Koordinaten.

| Komponente | Aufgabe |
|------------|---------|
| `RoomManager` | Lädt den Raum neu (`MRUK.LoadSceneFromDevice`), sobald die App wieder Fokus bekommt oder die Ansicht neu zentriert wird (die Quest pausiert die App für ihren Boundary-Dialog). Ist der Nutzer in einem **anderen Raum** (andere Raum-UUID), werden die Effect-Mesh-Collider neu gebaut, entnommene Teile entfernt und Schild und Drucker neu platziert; sonst bewegt sich nichts, damit die eigene Anordnung erhalten bleibt |
| `WristMenu` | Panel über der linken Hand mit Raumstatus und zwei Poke-Buttons: **Re-place** (neu laden und neu platzieren) und **Scan room** (`OVRScene.RequestSpaceSetup`, danach neu laden). Erscheint erst, wenn die Hand kurz im Blickfeld bleibt (Hysterese gegen Flackern) |

MRUKs *Data Source* steht auf **Device**: Ohne Space-Setup-Daten werden die Inhalte vor dem Nutzer platziert
statt in einem zufälligen Beispielraum (*Device With Prefab Fallback*). Im Editor (keine Raumdaten) greift diese Fallback-Platzierung.

Die Materialien in `Assets/Materials` sind von URPs `Lit.mat` abgeleitet; Emission ist im Asset aktiviert,
damit die Shader-Variante mit Emission im Quest-Build enthalten ist.

**Modell-Pipeline (Blender)** – `Assets/Models/Printer3D/Printer3D.fbx` wird aus dem Original erzeugt:

```bash
blender --background --factory-startup --python Tools/Blender/prepare_printer.py -- \
    SourceAssets/Printer3D/Printer.fbx Assets/Models/Printer3D/Printer3D.fbx
```

| Objekt | Bewegung | Pivot |
|--------|----------|-------|
| `Frame` | statisch | Bodenmitte |
| `Bed` | Y-Achse (vor/zurück) | Mitte der Bettoberfläche |
| `Gantry` | Z-Achse (hoch/runter) | Mitte des Balkens |
| `PrintHead` | X-Achse auf dem Portal | Düsenspitze |
| `ZCable`, `FilamentTube` | werden mitgestreckt | festes Ende |
| `Display`, `DisplayAnchor` | statisch, Bildschirm leuchtet leicht blau | Mitte des Bildschirms |

Die fünf druckbaren Modelle erzeugt ein zweites Skript (Pivot jeweils unten in der Mitte, max. 6 × 6 × 6 cm):

```bash
blender --background --factory-startup --python Tools/Blender/make_print_models.py -- \
    Assets/Models/PrintModels/PrintModels.fbx
```

Alle Teile sind direkte Kinder von `Printer3D` (Blenders FBX-Option *Apply Transform* verdreht verschachtelte
Kinder), deshalb bewegt der Controller-Code `PrintHead` zusammen mit `Gantry`.

### Versionsverwaltung

- `.gitignore` basiert auf dem offiziellen [GitHub-Unity-Template](https://github.com/github/gitignore/blob/main/Unity.gitignore)
  (plus maschinenspezifische Meta-Einstellungen, Build-Nebenprodukte und Unitys Crash-Recovery-Szenen).
- Binärdateien (Modelle, Texturen, Audio, Schriften, native Bibliotheken) liegen in **Git LFS**, siehe `.gitattributes`;
  Unity-YAML wird mit LF-Zeilenenden gespeichert. Prüfen mit `git lfs ls-files`.
- Workflow: `main` bleibt lauffähig; jede Aufgabe wird auf einem `feature/*`-Branch entwickelt und mit `--no-ff` gemergt.

## 1.4 Eigenleistung und Reflexion

### Eigenleistung

**Konzept und Gestaltung.** Die Idee für die App und alle Interaktionen habe ich festgelegt und schrittweise
verfeinert: einen 3D-Drucker als Maschine, das Bauteil, das man nach dem Druck greifen kann, das Verschieben
von Schild und Drucker, das Handgelenk-Menü für den Raumwechsel, das Display als Touchscreen statt eines
separaten Knopfs und die Modellauswahl über halbtransparente Vorschauen auf dem Druckbett, durch die man
mit dem Finger wischt. Das Druckermodell habe ich auf Sketchfab ausgesucht (Lizenz, Polygonzahl, trennbare Teile).

**Unity Editor und Tests.** Selbst umgesetzt habe ich:
- das Projekt-Setup (Unity 6 / URP, Meta XR SDK über die Meta-Registry, OpenXR, Android-Build) und das Meta Project Setup Tool;
- den Szenenaufbau mit Building Blocks (Camera Rig, Passthrough, MR Utility Kit, Interactions Rig, Effect Mesh)
  und deren Konfiguration, z. B. das Abschalten der Locomotion und das Licht;
- das `PrintedPart`-Prefab mit der Grab-Interaktion (Quick Action *Add Grab Interaction*);
- das Bauen und Testen auf der Quest 2 und die Rückmeldungen daraus, z. B. dass der Display-Text auf einer
  Seite im Drucker verschwand.

**Code und Skripte mit KI-Unterstützung.** Die C#-Skripte, die Blender-Skripte (Druckermodell vorbereiten,
druckbare Modelle erzeugen, Bildschirm vermessen) und das Sound-Skript habe ich zusammen mit einem
KI-Assistenten (Claude) entwickelt: Ich habe die Anforderungen und das Verhalten vorgegeben, die Ergebnisse
im Editor und auf dem Headset geprüft und Korrekturen angestoßen; der Assistent hat den Großteil des Codes
geschrieben. Die betroffenen Commits sind mit `Co-Authored-By: Claude` gekennzeichnet, Commits mit reiner
Editor-Arbeit nicht.

**Zeitaufwand.** Insgesamt etwa 13–15 Stunden – einschließlich Einrichtung von Unity und Meta SDK,
Recherche, Modellsuche, Warten auf Builds und Tests auf dem Headset.

**Was am meisten Zeit gekostet hat:**
- **Quest-Builds:** jeder Build dauerte 10–15 Minuten, zwei Builds haben den PC zum Absturz gebracht
  (vermutlich zu wenig Arbeitsspeicher beim IL2CPP-Kompilieren). Deshalb habe ich selten gebaut und viele
  Änderungen erst am Ende zusammen auf dem Headset getestet. Die Einstellung *IL2CPP Code Generation:
  Faster (smaller) builds* hat geholfen.
- **Szenenaufbau und Fehlersuche im Editor:** das Interactions Rig mit aktiver Locomotion (der Nutzer fiel durch
  die Schwerkraft nach unten), die fehlende Lichtquelle (Lit-Materialien wirkten schwarz) und ein Editor-Absturz,
  nach dem die Szene aus Unitys Recovery-Datei wiederhergestellt werden musste.
- **Das Druckermodell für die Animation vorbereiten:** rund 80 lose Einzelteile den beweglichen Baugruppen
  zuordnen, Pivots setzen und das Achsensystem zwischen Blender und Unity richtig umrechnen.

### Reflexion

**Einfach** waren die Grundlagen, die Meta fertig mitliefert: Passthrough, Kamera-Rig und Hand-/Controller-Interaktion
über Building Blocks, die Raumdaten über das MR Utility Kit (Key Wall, Tische, Boden) und die Poke- und
Grab-Interaktion aus dem Interaction SDK. Auch Git LFS und eine saubere Branch-Struktur einzurichten war unkompliziert.

**Schwer** war alles, was man nur auf dem Headset sieht. Der Editor hat keine echten Raumdaten, daher ließen sich
Platzierung, Wischgeste und Ausrichtung des Displays erst auf der Quest beurteilen – bei 10–15 Minuten pro Build
ist das ein langsamer Kreislauf. Dazu kamen Linux-spezifische Probleme: Der Neustart-Dialog des Meta Setup Tools
ließ sich nicht bedienen, und Unity hat sich während eines Builds selbst auf *Unity Version Control* umgestellt.
Fehler in Annahmen zeigten sich spät, z. B. eine von Hand gemessene Bildschirmnormale, die um einige Grad daneben lag.

**Gelernt** habe ich vor allem, wie Mixed Reality Inhalte an den echten Raum bindet (Anker, Key Wall, Raumwechsel)
und wie man Interaktionen so gestaltet, dass sie sich selbst erklären: leuchtendes und pulsierendes Display,
Hinweise direkt am Objekt („Grab me!“), Geräusche und Vibration als Bestätigung. Beim nächsten Projekt würde
ich früher und öfter auf dem Headset testen und die Maße von Modellen gleich aus der Geometrie messen statt
sie zu schätzen.

## Autor

Linus Pihan
