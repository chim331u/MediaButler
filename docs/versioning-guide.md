# Guida al Sistema di Versionamento Automatico (SemVer)

Benvenuto nella guida del sistema di versionamento semantico automatizzato di **MediaButler**.

Questo sistema gestisce in modo intelligente l'incremento di versione nel file `.csproj` dell'API principale (`MediaButler.API.csproj`) seguendo le specifiche del **Semantic Versioning (SemVer)** in formato `MAJOR.MINOR.PATCH` (es. `1.3.4`):
- **PATCH (Z):** Incrementata ad ogni **Commit** locale.
- **MINOR (Y):** Incrementata ad ogni **Push** remoto (azzerando la patch).
- **MAJOR (X):** Incrementata **Manualmente** dall'utente quando ci sono modifiche strutturali o breaking changes.

Ogni versione dinamica viene iniettata automaticamente all'interno di ogni singola riga di log dell'applicazione tramite **Serilog**.

---

## 1. Come Funzionano i Componenti del Sistema

Il sistema si compone di tre parti principali collegate tra loro:

```
[ Git Commit ] ------------> [ pre-commit hook ] ------------> Incrementa PATCH (+1 in csproj)
                                                                    
[ Git Push ] --------------> [ pre-push hook ] --------------> Incrementa MINOR (+1, patch=0 in csproj)
                                                               Crea commit di auto-bump e riesegue push
                                                                    
[ Sviluppatore ] -----------> [ Esecuzione Manuale ] -----------> Incrementa MAJOR (+1, minor=0, patch=0)
```

### A. Incremento Patch (ad ogni Commit)
L'hook di `.git/hooks/pre-commit` intercetta l'azione di commit del codice e:
- Richiama lo script `increment-version.sh patch`.
- Se nel file `.csproj` manca la configurazione iniziale di versione, inserisce automaticamente i tag `<Version>`, `<AssemblyVersion>` e `<FileVersion>` partendo da `1.0.0`.
- Modifica la patch incrementandola di `1` (es: `1.0.2` -> `1.0.3`).
- Esegue automaticamente `git add` sul file `.csproj` in modo che la nuova versione sia parte dello stesso identico commit in corso.

### B. Incremento Minor (ad ogni Push)
L'hook di `.git/hooks/pre-push` intercetta l'azione di push verso il server remoto e:
- Verifica se ci sono commit effettivi da inviare per evitare inutili auto-incrementi.
- Richiama lo script `increment-version.sh minor`.
- Incrementa il numero di versione minore di `1` e **azzera la patch** (es: `1.0.3` -> `1.1.0`).
- Salva la modifica creando un commit automatico con messaggio `"chore: bump version (minor)"`.
- Esegue in modo ricorsivo il push per assicurarsi che l'auto-bump venga inviato al server remoto. Il pre-push previene loop infiniti tramite variabili di ambiente protette.

### C. Integrazione con Serilog (Log Dinamici)
La classe statica `SerilogConfiguration.cs` arricchisce a runtime ogni singolo evento di log dell'applicazione estraendo la versione direttamente dall'assembly dell'applicazione. Le righe di log prodotte (sia in Console che su File) conterranno il campo JSON `"Version": "x.y.z"` grazie al template `{Properties:j}`.

---

## 2. Installazione e Configurazione Iniziale

Se stai configurando il repository per la prima volta su una nuova macchina, esegui i seguenti passaggi dalla cartella radice del repository:

```bash
# 1. Copia e abilita l'hook di commit (incrementa patch)
cp scripts/pre-commit.hook .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit

# 2. Copia e abilita l'hook di push (incrementa minor)
cp scripts/pre-push.hook .git/hooks/pre-push
chmod +x .git/hooks/pre-push

# 3. Assicurati che lo script principale sia eseguibile
chmod +x scripts/increment-version.sh
```

---

## 3. Come Incrementare Manualmente la Versione MAJOR

La versione **MAJOR** rappresenta un rilascio importante con potenziali rotture di retrocompatibilità (breaking changes). Questo incremento non è automatico e viene pilotato manualmente dallo sviluppatore.

Per incrementare manualmente la versione major, esegui il seguente comando nella radice del progetto:

```bash
./scripts/increment-version.sh major
```

### Cosa succede quando esegui questo comando?
1. La versione **MAJOR** viene incrementata di `1`.
2. Le versioni **MINOR** e **PATCH** vengono azzerate.
3. Esempio: se la versione corrente nel file `.csproj` era `1.4.12`, diventerà **`2.0.0`** (e l'AssemblyVersion/FileVersion diventerà `2.0.0.0`).

Dopo aver eseguito il comando, ti basta committare la modifica:
```bash
git add src/MediaButler.API/MediaButler.API.csproj
git commit -m "release: bump to major version 2.0.0"
```

---

## 4. Gestione Avanzata e Trucchi di Emergenza (DevOps)

### A. Come disattivare temporaneamente l'incremento di versione
Se per qualsiasi motivo hai bisogno di effettuare un commit o un push urgente senza toccare i numeri di versione del progetto, puoi farlo in due modi:

1. **Uso di variabili d'ambiente (Consigliato):**
   Imposta `SKIP_VERSION_BUMP=1` prima dell'esecuzione del comando git:
   ```bash
   # Salta il bump durante il commit (niente incremento patch)
   SKIP_VERSION_BUMP=1 git commit -m "fix: correzione urgente rapida"

   # Salta il bump durante il push (niente incremento minor)
   SKIP_VERSION_BUMP=1 git push
   ```

2. **Bypass dei git hook integrati:**
   Usa i flag nativi di Git per ignorare completamente l'attivazione dei githook:
   ```bash
   # Salta i controlli di pre-commit
   git commit --no-verify -m "fix: hotfix urgente"

   # Salta i controlli di pre-push
   git push --no-verify
   ```

### B. Gestione delle versioni di Pre-Release
Se desideri includere un tag di pre-release (come `-beta.1` o `-rc.2`) all'interno del tuo file `.csproj`, lo script lo rileverà e lo manterrà durante gli incrementi automatici:
- Versione attuale: `1.2.3-beta.1`
- Esecuzione commit (patch): diventerà `1.2.4-beta.1`
- Esecuzione push (minor): diventerà `1.3.0-beta.1`
