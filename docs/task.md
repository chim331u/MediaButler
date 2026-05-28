# Checklist Attività - MediaButler

> [!NOTE]
> Questo file è strutturato in modalità **bottom-up**: i task aggiunti o aggiornati più di recente si trovano in alto.
> I task completati sono mostrati in modo compatto (senza dettagli), mentre quelli in corso o futuri mostrano i dettagli operativi.

## 📋 Elenco Attività (Nuovi in alto)

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
