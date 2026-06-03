# Checklist Attività - MediaButler

> [!NOTE]
> Questo file è strutturato in modalità **bottom-up**: i task aggiunti o aggiornati più di recente si trovano in alto.
> I task completati sono mostrati in modo compatto (senza dettagli), mentre quelli in corso o futuri mostrano i dettagli operativi.

## 📋 Elenco Attività (Nuovi in alto)

- [x] **Completato: Automazione del deploy remoto su NAS QNAP (Fase 19)**
    - [x] Implementazione dell'autenticazione singola tramite SSH Multiplexing e socket di controllo temporaneo.
    - [x] Estensione di `scripts/deploy-qnap.sh` con il comando `deploy` e configurabilità di rete per il trasferimento e l'attivazione in `/share/Storage/Docker/mediabutler/delivery`.
    - [x] Aggiornamento della guida di rilascio in `docs/deploy_guide.md`.
- [x] **Completato: Fase 18: Integrazione del gateway di notifiche "NotifyHub" (Canali Telegram/Discord e Gestione API Key)**
    - [x] Task 18.1: Modifica di `config.go` e integrazione database SQLite (`UserPreferences`) per la persistenza dei parametri di NotifyHub.
    - [x] Task 18.2: Estensione dell'endpoint `GET/POST /api/config` in `router.go` per abilitare la gestione dinamica delle credenziali NotifyHub.
    - [x] Task 18.3: Implementazione in `watcher.go` dell'invio asincrono in background (`sendNotifyHubNotification`) su rilevamento di nuovi file.
    - [x] Task 18.4: Creazione del pannello Svelte "🔔 NotifyHub Integration" nella scheda Settings di `App.svelte`.
    - [x] Task 18.5: Aggiornamento dei file `docker-compose` e testing di integrazione.
- [x] **Completato: Fase 17: History Globale, Ricerca, Paginazione e Modifica Inline (Android & Web)**
    - [x] Task 17.1: Sviluppo estensione API GET /api/files per parametro search e nuovo endpoint POST /api/files/{hash}/update in Go backend.
    - [x] Task 17.2: Sviluppo unit test automatici per i nuovi endpoint di ricerca e aggiornamento file in Go backend.
    - [/] Task 17.3: Sviluppo logica di aggiornamento inline ed integrazione della barra di ricerca Cobalt e dei badge dello stato nella Web UI Svelte.
- [x] **Completato: Configurazione del nome dell'applicazione ed eseguibile compilato in MediaButler, rimozione icone adattive e generazione icone desktop PNG per tutte le densità (mdpi, hdpi, xhdpi, xxhdpi, xxxhdpi) a partire da Butler with Play Button Tray.png**
- [x] **Completato: Integrazione del pulsante Move e modifica della categoria per i file confermati (READY_TO_MOVE/ERROR) nell'app Android nativa (Fase 16)**
- [x] **Completato: Allineamento funzionale completo dell'app Android (Presets dinamici, Ignora file con popup, Ri-classificazione asincrona, Slider ML threshold, Directory Explorer lazy-loaded) e risoluzione definitiva del crash all'avvio su Dispatchers.IO**
- [x] **Completato: Creazione della guida completa al deployment locale (Mac) e su NAS QNAP in docs/deploy_guide.md ed allineamento della cartella scripts/**
- [x] **Completato: Implementazione dell'azione "Non mostrare più" (status 8 e categoria NULL) con pop-up di conferma preventiva e pulsante premium 🚫 nella tabella Active Queue**
- [x] **Completato: Caricamento dinamico dei Quick Presets nel modal di conferma con gli ultimi 5 valori di categoria da DB e fallback "UNKNOW"**
- [x] **Completato: Aggiunta del pulsante "Ri-Classifica" in Dashboard Home con riclassificazione massiva in background ed aggiornamento reattivo SSE**
- [x] **Completato: Implementazione dei log di retraining dettagliati (durata, reset, file, parole chiavi) e slider mlThreshold dinamico con descrizione in Settings**
- [x] **Completato: Implementazione dell'albero delle directory espandibile in Settings con visualizzazione dei file, switch reattivo per file nascosti e percorso DB SQLite attivo**
- [x] **Completato: Rimozione definitiva degli hook git local/remote (`pre-commit`, `pre-push`) e script di versione obsoleti legati a dotnet**
- [x] **Completato: Ottimizzazione e stabilizzazione del test suite (`main_test.go`) tramite polling robusto per l'asincronia in sostituzione dei time.Sleep statici**
- [x] **Completato: Aggiunta del livello di log on-the-fly sulla pagina Settings (GET/POST `/api/config` e nuova scheda "Logging & Diagnostics" in Svelte)**
- [x] **Completato: Esecuzione con successo del commit e push delle modifiche sul repository remoto (bypassando i vecchi hook pre-push)**
- [x] **Completato: Traduzione in inglese di `DEVELOPER_GUIDELINES.md` per ottimizzazione dell'AI**
- [x] **Completato: Configurazione iniziale della checklist delle attività (`docs/task.md`)**
- [x] **Completato: Creazione delle linee guida dell'agente AI (`DEVELOPER_GUIDELINES.md`)**
- [x] **Completato: Pulizia della cartella `docs` dai riferimenti dotnet**
- [x] **Completato: Pulizia delle cartelle `build`, `docker` e `scripts` dai riferimenti dotnet**
- [x] **Completato: Refactoring e semplificazione del file `.gitignore`**
- [x] **Completato: Rimozione dei file e delle cartelle dotnet della root (`MediaButler.sln`, `Delivery`, `configs`, `models`, `scripts/MigrationTool.csproj`, `scripts/FileCatMigrationTool.cs`)**
- [x] **Completato: Ottimizzazione del server HTTP in `main.go` e della copia dei file in `copier.go` per il NAS QNAP**
