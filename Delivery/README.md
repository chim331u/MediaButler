# MediaButler Deployment

Simple deployment scripts for MediaButler API on QNAP NAS or MacBook development.

## Quick Start

### Deploy MediaButler API

```bash
cd Delivery/scripts
chmod +x deploy-mediabutler-api.sh
./deploy-mediabutler-api.sh
```

The script will:
1. Ask you to select deployment platform (QNAP NAS or MacBook)
2. Show all configuration values
3. Ask for confirmation before proceeding
4. Clone repository, build Docker image, and run container

### Platform Options

**Option 1: QNAP NAS (ARM32)**
- Production deployment
- ARM32 optimized (1GB RAM)
- QNAP volume paths (`/share/...`)
- Port: 30129

**Option 2: MacBook ARM64**
- Local development
- ARM64 native build
- Local paths (`~/mediabutler/...`)
- Port: 30129

## Configuration

All settings are configured automatically based on platform selection.

### QNAP NAS Defaults
```bash
HOST_PORT=30129
DATA_VOLUME=/share/CACHEDEV2_DATA/Storage/Docker/mediabutler:/data
WATCH_VOLUME=/share/Download/Incoming:/watch
LIBRARY_VOLUME=/share/Video/Serie:/library
```

### MacBook Defaults
```bash
HOST_PORT=30129
DATA_VOLUME=$HOME/mediabutler/data:/data
WATCH_VOLUME=$HOME/mediabutler/watch:/watch
LIBRARY_VOLUME=$HOME/mediabutler/library:/library
```

## Access After Deployment

- API: `http://localhost:30129`
- Swagger UI: `http://localhost:30129/swagger`
- Health Check: `http://localhost:30129/health`

## Additional Scripts

### Monitor System
```bash
./monitor-mediabutler.sh
```

### Backup Data
```bash
./backup-mediabutler.sh --full
```

### Update MediaButler
```bash
./update-mediabutler.sh
```

## Docker Files

Three Dockerfile variants are available in `docker/`:

- `api-optimized.dockerfile` - Production (recommended)
- `api-simple.dockerfile` - Development
- `api-minimal.dockerfile` - Minimal build

The deployment script automatically selects the best Dockerfile.

## Troubleshooting

### Check Container Status
```bash
docker ps
docker logs mediabutler_api
```

### View Container Resource Usage
```bash
docker stats mediabutler_api
```

### Restart Container
```bash
docker restart mediabutler_api
```

### Stop and Remove Container
```bash
docker stop mediabutler_api
docker rm mediabutler_api
```

## Requirements

- Docker installed
- Internet connection (for first deployment)
- QNAP: 1GB+ RAM, 2GB+ disk space
- MacBook: Docker Desktop installed

## Repository

Default repository: `https://github.com/chim331u/MediaButler.git`
Default branch: `delploy`

Override with environment variables:
```bash
export GITHUB_REPO=https://github.com/your-username/MediaButler.git
export GIT_BRANCH=main
./deploy-mediabutler-api.sh
```
