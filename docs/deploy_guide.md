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
