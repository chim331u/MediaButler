# MediaButler Deployment Suite

This folder contains the complete, highly optimized, production-ready deployment suite for **MediaButler**. It is designed to run seamlessly on low-resource environments (such as QNAP/Synology NAS with CPU ARM32/ARM64 and 1GB/2GB of RAM) as well as macOS local development workstations.

The suite consists of two independent, platform-aware, and self-healing deployment scripts:
1. **MediaButler API** (`deploy-mediabutler-api.sh`): Deploys the .NET 10 high-performance backend, including auto-generated credentials and database structures.
2. **MediaButler Web** (`deploy-mediabutler-web.sh`): Deploys the Blazor WebAssembly frontend served by an extremely lightweight, secure, non-root Nginx server.

---

## 🚀 Quick Start Guide

To deploy the entire MediaButler system, execute the following commands from your terminal:

### Step 1: Deploy the Backend API

```bash
cd Delivery/scripts
chmod +x deploy-mediabutler-api.sh
./deploy-mediabutler-api.sh
```

### Step 2: Deploy the Web Frontend

```bash
chmod +x deploy-mediabutler-web.sh
./deploy-mediabutler-web.sh
```

---

## 📋 Architectural Highlights

### 🔒 Non-Root Security (Web Frontend)
The Web frontend Nginx server is optimized for maximal security:
* **Privileged Port Bypass:** Runs entirely as the non-root user `mediabutler` (UID/GID 1000) and listens on internal port `8080`.
* **Read-Only Compatibility:** Nginx PID files and caching structures are routed through `/tmp/nginx.pid` to allow running inside completely read-only container systems.
* **Aggressive Static Optimization:** Embedded Gzip compression, cache-control directives, and explicit WebAssembly MIME-type handling.

### ⚙️ Startup Config Injection
Instead of hardcoding the backend URL during static compilation, the Web deployment utilizes a **dynamic entrypoint** (`web-entrypoint.sh`). When the container starts, it reads the `API_BASE_URL` environment variable and injects it directly into the compiled Blazor `appsettings.json` file before launching Nginx.

### 🧠 Low-Memory OOM Shielding (.NET Build Stage)
To build robustly on a NAS or embedded hardware with very limited RAM (1GB/2GB), both build systems apply:
* Disabling the Server GC (`DOTNET_GCServer=0`, `DOTNET_gcConcurrent=false`).
* Strict single-threaded builds (`/p:MaxCpuCount=1`, `/p:BuildInParallel=false`) to cap compiler memory footprints within 150-200MB boundaries.
* Compiling on `linux/amd64` (multi-stage) and deploying lightweight native runtimes (`linux/arm/v7` or `linux/arm64`) to bypass compiler issues on ARM targets.

---

## 🔧 Platform Configurations & Defaults

The scripts automatically detect your operating system and offer guided setups:

### 1. MediaButler API Defaults

| Parameter | MacBook ARM64 (Local Dev) | QNAP/Synology NAS (Production) |
| :--- | :--- | :--- |
| **Host Port** | `30129` | `30129` |
| **Docker Platform** | `linux/arm64` | `linux/arm/v7` |
| **Data Directory** | `~/mediabutler/data` | `/share/CACHEDEV1_DATA/Docker/mediabutler` |
| **Watch Folder** | `~/mediabutler/watch` | `/share/Download/Incoming` |
| **Media Library** | `~/mediabutler/library` | `/share/Video/Serie` |
| **Logs Volume** | `~/mediabutler/logs` | `/share/CACHEDEV1_DATA/Docker/mediabutler/logs` |

> [!TIP]
> **Guided Security Setup:** During API deployment, if you leave the API Key or JWT Secret prompts empty, the script automatically generates highly secure, cryptographically random keys using `openssl` (or a secure local pseudo-random fallback).

### 2. MediaButler Web Defaults

| Parameter | MacBook ARM64 (Local Dev) | QNAP/Synology NAS (Production) |
| :--- | :--- | :--- |
| **Host Port** | `30139` (maps to internal `8080`) | `30139` (maps to internal `8080`) |
| **Docker Platform** | `linux/arm64` | `linux/arm/v7` |
| **API URL** | `http://localhost:30129/` | `http://localhost:30129/` *(or public URL)* |
| **Logs Volume** | `~/mediabutler_web/logs` | `/share/CACHEDEV1_DATA/Docker/mediabutler_web/logs` |

---

## 🛠️ Diagnostics & Maintenance

### Check Logs & Status
```bash
# View last 50 logs of the API
docker logs --tail 50 mediabutler_api

# View last 50 logs of the Web UI
docker logs --tail 50 mediabutler_web
```

### Resource Utilization
```bash
# Monitor container CPU, memory and network metrics
docker stats mediabutler_api mediabutler_web
```

### Self-Healing & Restarts
If you encounter runtime communication failures or need to apply immediate updates, run the deployment scripts again. They perform a **complete self-healing cycle** by gracefully stopping existing containers and cleaning up orphaned images before spinning up fresh instances.

---

## 📂 Active Folder Structure

Following a thorough optimization cleanup, the `Delivery` folder contains only the essential components:

```
Delivery/
├── README.md                <-- This documentation
├── docker/
│   ├── api-minimal.dockerfile   <-- API compilation and runtime optimizations
│   ├── Dockerfile.webassembly   <-- Blazor WebAssembly non-root compilation
│   └── web-entrypoint.sh        <-- Dynamic startup config injector
└── scripts/
    ├── deploy-mediabutler-api.sh <-- Interactive backend deployment
    ├── deploy-mediabutler-web.sh <-- Interactive frontend deployment
    ├── monitor-mediabutler.sh   <-- System monitoring dashboard
    └── update-mediabutler.sh    <-- Seamless repository update script
```
