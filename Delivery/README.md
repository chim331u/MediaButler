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

## 📦 Mac Cross-Compilation & Packaging (Highly Recommended for NAS)

If your NAS has a low-resource ARM CPU (ARM32/ARM64) or lacks QEMU virtualization support, compiling directly on the NAS can trigger **Illegal Instruction (SIGILL)** or **exec format** errors due to CPU instruction constraints. 

To bypass this completely, you can build the native NAS images on your **MacBook** and transfer them ready-to-load!

### Step 1: Package on Your Mac
Run the packaging script from the repository root on your Mac:
```bash
chmod +x Delivery/scripts/package-mediabutler-for-nas.sh
./Delivery/scripts/package-mediabutler-for-nas.sh
```
Select `1` for **ARM32** (`linux/arm/v7`) or `2` for **ARM64** (`linux/arm64`). This builds the optimized images and packages them as `.tar` files inside the `Delivery/dist/` directory.

### Step 2: Transfer to NAS
Copy the generated `.tar` files from your Mac to your NAS (via SMB, FTP, or File Station) into your target directory:
* `mediabutler_api_arm32.tar` (or `_arm64.tar`)
* `mediabutler_web_arm32.tar` (or `_arm64.tar`)

### Step 3: Load and Run on NAS
SSH into your NAS, navigate to the folder, and run the following commands to load and spin up the containers:
```bash
# Load images into Docker
docker load -i mediabutler_api_arm32.tar
docker load -i mediabutler_web_arm32.tar

# Spin up API Container
docker run -d --name mediabutler_api --restart always \
  -p 30129:8080 \
  -v /share/CACHEDEV1_DATA/Docker/mediabutler:/data \
  -v /share/Download/Incoming:/watch \
  -v /share/Video/Serie:/library \
  -v /share/CACHEDEV1_DATA/Docker/mediabutler/logs:/app/logs \
  -e "ASPNETCORE_ENVIRONMENT=Production" \
  -e "Security__ApiKey=mb-local-dev-key-8a9b2c" \
  -e "Security__JwtSecret=mb-local-dev-jwt-secret-9x8y7z" \
  -e "MediaButler__Paths__WatchFolder=/watch" \
  -e "MediaButler__Paths__MediaLibrary=/library" \
  -e "ConnectionStrings__DefaultConnection=Data Source=/data/mediabutler.db" \
  -e "MediaButler__ML__MaxBatchSize=10" \
  -e "MediaButler__FileDiscovery__ScanIntervalMinutes=5" \
  -e "MediaButler__ARM32__MemoryThresholdMB=140" \
  -e "MediaButler__ARM32__AutoGCTriggerMB=110" \
  --platform "linux/arm/v7" \
  mediabutler_api_image:latest

# Spin up Web UI Container
docker run -d --name mediabutler_web --restart always \
  -p 30139:8080 \
  -v /share/CACHEDEV1_DATA/Docker/mediabutler_web/logs:/var/log/nginx \
  -e "API_BASE_URL=http://localhost:30129/" \
  --platform "linux/arm/v7" \
  mediabutler_web_image:latest
```

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
    ├── package-mediabutler-for-nas.sh <-- macOS Cross-Compilation and packaging suite [NEW]
    ├── monitor-mediabutler.sh   <-- System monitoring dashboard
    └── update-mediabutler.sh    <-- Seamless repository update script
```
