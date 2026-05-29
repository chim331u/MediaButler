# Guida di Rilascio e Deployment - MediaButler

Questa guida fornisce le istruzioni operative per compilare, testare ed effettuare il deploy del remake di **MediaButler** sia in ambiente di sviluppo locale (**macOS**) che in ambiente di produzione su **NAS QNAP** (architettura ARM32v7 con risorse limitate).

Tutti gli script di automazione sono stati centralizzati all'interno della cartella `scripts/` per garantire la massima pulizia del workspace.

---

## 🛠️ 1. Mappa degli Script di Automazione (`scripts/`)

Tutti gli script sono situati nella cartella `scripts/` ed automatizzano le diverse fasi del ciclo di vita del software:

| Script | Percorso | Scopo principale | Dove eseguirlo |
| :--- | :--- | :--- | :--- |
| **`build-qnap.sh`** | `scripts/build-qnap.sh` | Esegue la compilazione della Web UI (Svelte 5) e del backend Go per Mac (locale) o QNAP (cross-compilazione ARM32v7 CGO-free). | Mac (Sviluppatore) |
| **`deploy-qnap.sh`** | `scripts/deploy-qnap.sh` | **Fase 1 (build)**: Compila l'immagine Docker ARM32v7 via Buildx ed esporta il tarball.<br>**Fase 2 (run-nas)**: Rileva il volume del NAS, carica il tarball ed avvia lo stack Docker Compose. | Mac (Sviluppatore) / NAS (Produzione) |
| **`qnap-service.sh`** | `scripts/qnap-service.sh` | Controller daemon in stile BusyBox `init` per avviare/fermare il servizio Go nativo a basso impatto RAM senza Docker. | NAS (Produzione) |

---

## 💻 2. Rilascio in Locale (macOS / Testing)

L'ambiente locale consente lo sviluppo fluido ed il testing di integrazione.

### Opzione A: Esecuzione Nativa (Sviluppo Rapido)
Per compilare il frontend ed avviare il server Go nativo sul proprio Mac:

1. **Compilazione della Web UI Svelte e del Server Go locale**:
   ```bash
   bash scripts/build-qnap.sh --local
   ```
   *Questo compilerà la Web UI tramite Vite ed integrerà gli asset statici all'interno del backend Go, salvando l'eseguibile nativo (`mediabutler-local-darwin-arm64`) nella cartella `build/`.*

2. **Avvio del Servizio**:
   ```bash
   ./build/mediabutler-local-darwin-arm64
   ```
   *Il server si avvierà sulla porta `30149` (configurabile via variabile d'ambiente `PORT`).*

### Opzione B: Esecuzione con Docker Compose Locale (Emulazione)
Per testare il comportamento multi-container (inclusa la persistenza del database ed il watcher delle cartelle):

1. **Avvio dello stack locale**:
   ```bash
   docker-compose up --build -d
   ```
2. **Verifica dei Log**:
   ```bash
   docker-compose logs -f
   ```
3. **Cartelle di test locali**:
   Lo stack locale mappa la cartella `./temp` per emulare il database e le directory di Watch/Destinazione (`./temp/watch` e `./temp/dest`).

---

## 🐳 3. Rilascio Containerizzato su NAS QNAP (ARM32v7)

Questa è l'opzione raccomandata per garantire l'isolamento dei servizi su QTS utilizzando Container Station (Docker + Docker Compose).

### Passo 1: Compilazione ed Esportazione (Mac del Sviluppatore)
Esegui lo script per cross-compilare l'eseguibile Go CGO-free per ARM32v7 e pacchettizzarlo in un'immagine Docker ultra-leggera (~7.4 MB):
```bash
bash scripts/deploy-qnap.sh build
```
*Questo genererà il pacchetto tarball `build/mediabutler-qnap-arm32.tar`.*

### Passo 2: Copia degli Asset sul NAS
Trasferisci tramite SCP o File Station i seguenti tre file nella cartella `/share/Public` (o una cartella di lavoro a scelta) sul NAS QNAP:
* Il tarball dell'immagine: `build/mediabutler-qnap-arm32.tar`
* Il file Compose di produzione: `docker-compose.qnap.yml`
* Lo script di deployment: `scripts/deploy-qnap.sh`

Esempio via SCP:
```bash
scp build/mediabutler-qnap-arm32.tar docker-compose.qnap.yml scripts/deploy-qnap.sh admin@<IP_DEL_NAS>:/share/Public/
```

### Passo 3: Esecuzione e Avvio (SSH sul NAS QNAP)
1. Collegati in SSH sul NAS:
   ```bash
   ssh admin@<IP_DEL_NAS>
   ```
2. Spostati nella cartella contenente i file copiati:
   ```bash
   cd /share/Public
   ```
3. Esegui la fase di installazione dello script:
   ```bash
   bash deploy-qnap.sh run-nas
   ```
   *Cosa fa questo comando sul NAS:*
   * Rileva dinamicamente il volume di archiviazione attivo del QNAP (es. `/share/CACHEDEV1_DATA`).
   * Importa l'immagine Docker dal tarball (`docker load -i mediabutler-qnap-arm32.tar`).
   * Configura le directory per i dati, il database SQLite con PRAGMA WAL e le cartelle di Watch/Destinazione.
   * Avvia lo stack containerizzato tramite `docker-compose -f docker-compose.qnap.yml up -d`.

---

## 🔌 4. Rilascio Nativo (Daemon) su NAS QNAP

Se preferisci non utilizzare Docker per azzerare totalmente il footprint di memoria RAM (riducendo il consumo a meno di **15MB** complessivi):

1. **Compilazione del Binario ARM32v7 (Mac del Sviluppatore)**:
   ```bash
   bash scripts/build-qnap.sh --qnap
   ```
   *Genererà il binario nativo compattato con flag di striping (`build/mediabutler-qnap`) ottimizzato per ARM32v7.*

2. **Trasferimento sul NAS**:
   Copia il binario `build/mediabutler-qnap` ed il controller `scripts/qnap-service.sh` sul NAS:
   ```bash
   scp build/mediabutler-qnap scripts/qnap-service.sh admin@<IP_DEL_NAS>:/share/Public/mediabutler/
   ```

3. **Gestione del Servizio (SSH sul NAS)**:
   Lo script `qnap-service.sh` agisce come controller BusyBox nativo per gestire l'esecuzione in background via `nohup` ed il tracciamento del PID:
   ```bash
   cd /share/Public/mediabutler
   
   # Avvio del servizio
   bash qnap-service.sh start
   
   # Verifica dello stato
   bash qnap-service.sh status
   
   # Stop del servizio
   bash qnap-service.sh stop
   ```
   *I log strutturati ed il database SQLite verranno salvati localmente all'interno della cartella `./data/`.*

---

## 📱 5. Rilascio e Compilazione Mobile Android

L'applicazione mobile nativa Android è scritta in Kotlin con Jetpack Compose ed OkHttp-SSE per gli aggiornamenti in tempo reale. Il codice si trova interamente all'interno della directory `src/android`.

### 📋 Prerequisiti di Build
Prima di procedere, assicurati di avere installato sul tuo host locale:
* **Java Development Kit (JDK 17)**: Richiesto dalla versione moderna di Android Gradle Plugin (AGP).
* **Android SDK**: Strumenti a riga di comando (Command-line tools) o Android Studio per disporre della piattaforma SDK (API 34) e dei build-tools.
* **Gradle Wrapper**: Lo script autogestito `./gradlew` incluso nel progetto si occuperà di scaricare la versione corretta di Gradle ed i plugin necessari.

---

### 💻 Opzione A: Debug Locale ed Esecuzione in Sviluppo

Per eseguire l'applicazione su un emulatore o su un dispositivo Android fisico collegato in modalità Debug USB:

1. **Spostati nella cartella Android**:
   ```bash
   cd src/android
   ```
2. **Avvia la compilazione e l'installazione in modalità Debug**:
   Seleziona il dispositivo di target attivo tramite adb ed installa l'applicazione:
   ```bash
   ./gradlew installDebug
   ```
3. **Genera l'APK di debug** (senza installarlo direttamente):
   ```bash
   ./gradlew assembleDebug
   ```
    *L'eseguibile di debug risultante verrà salvato in:*
    `src/android/app/build/outputs/apk/debug/MediaButler-debug.apk`

---

### 📦 Opzione B: Rilascio Autonomo per Dispositivo (Release APK)

Per generare un APK compatto di release da installare manualmente sul proprio dispositivo o distribuire autonomamente, applichiamo l'ottimizzazione tramite **R8/ProGuard** (integrato in `build.gradle.kts` che riduce l'impronta complessiva a soli **1.46 MB**):

1. **Compila il pacchetto di Release**:
   ```bash
   cd src/android
   ./gradlew assembleRelease
   ```
   *Questo comando produce un APK non firmato in:*
   `src/android/app/build/outputs/apk/release/MediaButler-release-unsigned.apk`

2. **Allinea l'APK per ottimizzare l'uso della RAM** (utilizzando `zipalign` incluso nei build-tools dell'Android SDK):
   ```bash
   zipalign -v -p 4 MediaButler-release-unsigned.apk MediaButler-release-aligned.apk
   ```

3. **Firma l'APK** (utilizzando `apksigner` con il tuo certificato di firma JKS di produzione):
   ```bash
   apksigner sign --ks "/Users/luca/AndroidKey.jks" --out MediaButler-release.apk MediaButler-release-aligned.apk
   ```
   *L'eseguibile `MediaButler-release.apk` finale è pronto per essere installato su qualsiasi dispositivo Android.*

---

### 🚀 Opzione C: Pubblicazione su Google Play Store (App Bundle - AAB)

Google richiede il formato **Android App Bundle (AAB)** per i nuovi caricamenti sullo store, che ottimizza la distribuzione delle risorse in base al modello specifico dell'utente.

1. **Genera l'App Bundle di Release**:
   ```bash
   cd src/android
   ./gradlew bundleRelease
   ```
   *Il pacchetto AAB non firmato viene generato in:*
   `src/android/app/build/outputs/bundle/release/MediaButler-release.aab`

2. **Firma l'App Bundle**:
   A differenza degli APK, gli App Bundle (`.aab`) **non** supportano lo strumento `apksigner` dell'Android SDK. Devono essere firmati obbligatoriamente tramite **`jarsigner`** (lo strumento di firma standard del JDK Java):
   ```bash
   jarsigner -verbose -sigalg SHA256withRSA -digestalg SHA256 -keystore "/Users/luca/AndroidKey.jks" MediaButler-release.aab key1
   ```

3. **Caricamento**:
   Accedi alla console sviluppatore [Google Play Console](https://play.google.com/console/), crea una nuova release all'interno della dashboard del tuo progetto ed effettua l'upload del file `MediaButler-release.aab` firmato per la revisione interna, di beta-test o produzione.
