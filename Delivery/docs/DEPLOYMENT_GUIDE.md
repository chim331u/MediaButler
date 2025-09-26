# MediaButler QNAP Deployment Guide

🚀 **Complete deployment solution for QNAP NAS systems with 1GB RAM constraint**

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20ARM64%20|%20x64-green.svg)]()
[![Memory](https://img.shields.io/badge/memory-<300MB-orange.svg)]()
[![Docker](https://img.shields.io/badge/docker-required-blue.svg)]()

## 📋 Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Advanced Setup](#advanced-setup)
- [Monitoring & Maintenance](#monitoring--maintenance)
- [Troubleshooting](#troubleshooting)
- [Performance Optimization](#performance-optimization)

## 🎯 Overview

MediaButler QNAP Deployment provides a complete containerized solution optimized for QNAP NAS systems with limited resources. The deployment includes:

### **Architecture Overview**
```
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   Nginx Proxy   │  │  MediaButler    │  │  MediaButler    │
│   (~20MB RAM)   │  │     API         │  │     Web UI      │
│   Port: 80/443  │  │   (~150MB RAM)  │  │   (~100MB RAM)  │
└─────────────────┘  └─────────────────┘  └─────────────────┘
         │                     │                     │
         └─────────────────────┼─────────────────────┘
                               │
                    ┌─────────────────┐
                    │  Shared Storage │
                    │   - Database    │
                    │   - ML Models   │
                    │   - Media Files │
                    └─────────────────┘
```

### **Key Features**
- ✅ **1GB RAM Optimized**: Total memory usage <300MB
- ✅ **ARM32/ARM64 Compatible**: Native support for QNAP architectures
- ✅ **Automated Deployment**: Single script installation
- ✅ **Health Monitoring**: Automatic restart and monitoring
- ✅ **Backup & Restore**: Comprehensive backup solution
- ✅ **Rolling Updates**: Zero-downtime updates with rollback
- ✅ **SSL Ready**: HTTPS support with certificate management

## 🔧 Prerequisites

### **Hardware Requirements**
- **QNAP NAS** with Container Station
- **Minimum RAM**: 1GB (400MB+ available)
- **Architecture**: ARM32, ARM64, or x64
- **Storage**: 2GB+ free space
- **Network**: Internet access for initial setup

### **Software Requirements**
- **Container Station**: Installed and running
- **Docker**: Version 20.10+ (included with Container Station)
- **Docker Compose**: Version 1.29+ (included with Container Station)

### **Network Requirements**
- **Ports**: 80 (HTTP), 443 (HTTPS - optional)
- **Internet Access**: Required for GitHub downloads
- **Local Access**: LAN access to QNAP web interface

## 🚀 Quick Start

### **Step 1: Download Deployment Script**

```bash
# SSH into your QNAP NAS
ssh admin@your-qnap-ip

# Download the deployment script
wget -O deploy-mediabutler-qnap.sh https://raw.githubusercontent.com/your-repo/mediabutler/main/Delivery/scripts/deploy-mediabutler-qnap.sh

# Make executable
chmod +x deploy-mediabutler-qnap.sh
```

### **Step 2: Basic Deployment**

```bash
# Deploy with default settings
GITHUB_REPO_URL="https://github.com/your-username/mediabutler" ./deploy-mediabutler-qnap.sh
```

### **Step 3: Verify Installation**

After deployment completes, access your MediaButler instance:

- **Web Interface**: `http://your-qnap-ip:80`
- **API Documentation**: `http://your-qnap-ip:80/swagger`
- **Health Check**: `http://your-qnap-ip:80/health`

## ⚙️ Configuration

### **Environment Variables**

Create or modify the deployment configuration:

```bash
# Basic Configuration
export GITHUB_REPO_URL="https://github.com/your-username/mediabutler"
export GITHUB_BRANCH="main"
export PROXY_PORT="80"
export INSTALL_PATH="/share/Container/mediabutler"

# Memory Limits (adjust based on available RAM)
export MEMORY_LIMIT_API="150m"
export MEMORY_LIMIT_WEB="100m"
export MEMORY_LIMIT_PROXY="20m"

# Media Paths (map to your QNAP shares)
export MEDIA_LIBRARY_PATH="/share/Media/TV Shows"
export WATCH_FOLDER_PATH="/share/Downloads/Complete"

# Run deployment
./deploy-mediabutler-qnap.sh
```

### **Custom Ports**

To use custom ports (e.g., if port 80 is already in use):

```bash
export PROXY_PORT="8080"
export API_PORT="5001"
export WEB_PORT="3001"
./deploy-mediabutler-qnap.sh
```

### **SSL/HTTPS Configuration**

Enable HTTPS with your own certificates:

```bash
export SSL_ENABLED="true"
export SSL_CERT_PATH="/share/Container/ssl/cert.pem"
export SSL_KEY_PATH="/share/Container/ssl/key.pem"
export PROXY_SSL_PORT="443"
./deploy-mediabutler-qnap.sh
```

## 🏗️ Advanced Setup

### **Custom Docker Configuration**

Modify `docker-compose.yml` for advanced configurations:

```yaml
# Example: Add external database
services:
  postgres:
    image: postgres:13-alpine
    environment:
      POSTGRES_DB: mediabutler
      POSTGRES_USER: mediabutler
      POSTGRES_PASSWORD: your_password
    volumes:
      - postgres_data:/var/lib/postgresql/data
    deploy:
      resources:
        limits:
          memory: 100m

  mediabutler-api:
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=mediabutler;Username=mediabutler;Password=your_password
    depends_on:
      - postgres
```

### **Resource Optimization**

For systems with more RAM (2GB+), increase limits:

```bash
export MEMORY_LIMIT_API="250m"
export MEMORY_LIMIT_WEB="150m"
export MEMORY_LIMIT_PROXY="50m"
```

For systems with less RAM (512MB), reduce limits:

```bash
export MEMORY_LIMIT_API="100m"
export MEMORY_LIMIT_WEB="70m"
export MEMORY_LIMIT_PROXY="15m"
```

### **Multi-Instance Deployment**

Deploy multiple instances for different purposes:

```bash
# Production instance
export INSTALL_PATH="/share/Container/mediabutler-prod"
export PROXY_PORT="80"
export GITHUB_BRANCH="main"
./deploy-mediabutler-qnap.sh

# Development instance
export INSTALL_PATH="/share/Container/mediabutler-dev"
export PROXY_PORT="8080"
export GITHUB_BRANCH="development"
./deploy-mediabutler-qnap.sh
```

## 📊 Monitoring & Maintenance

### **Built-in Monitoring**

The deployment includes automated monitoring that runs every 5 minutes:

```bash
# View monitoring status
cd /share/Container/mediabutler
./scripts/monitor-mediabutler.sh --status

# View monitoring logs
./scripts/monitor-mediabutler.sh --logs

# View alerts
./scripts/monitor-mediabutler.sh --alerts
```

### **Manual Health Checks**

```bash
# Check container status
docker-compose ps

# View resource usage
docker stats --no-stream

# Check service endpoints
curl http://localhost:80/health
curl http://localhost:80/api/health
```

### **Log Management**

```bash
# View application logs
docker-compose logs -f mediabutler-api
docker-compose logs -f mediabutler-web

# View nginx logs
docker-compose logs -f nginx-proxy

# View all logs
docker-compose logs -f
```

### **Backup & Restore**

```bash
# Create full backup
./scripts/backup-mediabutler.sh --full

# Create configuration backup only
./scripts/backup-mediabutler.sh --config

# List available backups
./scripts/backup-mediabutler.sh --list

# Restore from backup
./scripts/backup-mediabutler.sh --restore backup_file.tar.gz
```

### **Updates**

```bash
# Check for updates
./scripts/update-mediabutler.sh --check

# Perform update
./scripts/update-mediabutler.sh

# Force update
./scripts/update-mediabutler.sh --force

# Show update status
./scripts/update-mediabutler.sh --status
```

## 🛠️ Troubleshooting

### **Common Issues**

#### **Port Already in Use**
```bash
# Error: Port 80 is already in use
# Solution: Use custom port
export PROXY_PORT="8080"
./deploy-mediabutler-qnap.sh
```

#### **Insufficient Memory**
```bash
# Error: Container killed due to memory
# Solution: Reduce memory limits
export MEMORY_LIMIT_API="120m"
export MEMORY_LIMIT_WEB="80m"
./deploy-mediabutler-qnap.sh
```

#### **Services Not Starting**
```bash
# Check Docker daemon
docker info

# Check container logs
docker-compose logs

# Restart services
docker-compose restart

# Full restart
docker-compose down && docker-compose up -d
```

#### **Health Check Failures**
```bash
# Check service endpoints manually
curl -v http://localhost:5000/health
curl -v http://localhost:3000
curl -v http://localhost:80/health

# Check container health
docker inspect mediabutler-api | grep Health -A 10
```

### **Performance Issues**

#### **High Memory Usage**
```bash
# Monitor memory usage
docker stats --no-stream

# Check for memory leaks
./scripts/monitor-mediabutler.sh --status

# Restart services to clear memory
docker-compose restart
```

#### **Slow Response Times**
```bash
# Check system load
uptime

# Check disk usage
df -h

# Check network connectivity
ping google.com

# Optimize containers
docker system prune -f
```

### **Network Issues**

#### **Cannot Access Web Interface**
```bash
# Check if services are running
docker-compose ps

# Check port bindings
docker port mediabutler-proxy

# Check firewall settings
# QNAP: Security Settings > Firewall

# Test local access
curl http://localhost:80
```

#### **SSL Certificate Issues**
```bash
# Verify certificate files
ls -la /path/to/ssl/certificates

# Check certificate validity
openssl x509 -in cert.pem -text -noout

# Test SSL configuration
curl -k https://localhost:443
```

## 📈 Performance Optimization

### **Memory Optimization**

1. **Adjust GC Settings**:
```yaml
# In docker-compose.yml
environment:
  - DOTNET_GCHeapHardLimit=140000000
  - DOTNET_GCConserveMemory=9
```

2. **Optimize Container Limits**:
```yaml
deploy:
  resources:
    limits:
      memory: 150m
      cpus: '0.5'
    reservations:
      memory: 100m
      cpus: '0.2'
```

### **Storage Optimization**

1. **Use SSD for Database**:
```bash
# Move data to SSD mount
export INSTALL_PATH="/share/CACHEDEV1_DATA/Container/mediabutler"
```

2. **Configure Log Rotation**:
```yaml
logging:
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
```

### **Network Optimization**

1. **Enable Gzip Compression**:
```nginx
# In nginx.conf
gzip on;
gzip_comp_level 6;
gzip_types text/plain application/json;
```

2. **Optimize Buffer Sizes**:
```nginx
client_body_buffer_size 128k;
client_max_body_size 100M;
proxy_buffering on;
proxy_buffer_size 4k;
```

## 🔒 Security Considerations

### **Container Security**
- All containers run as non-root users
- Resource limits prevent resource exhaustion
- Health checks enable automatic recovery
- Log rotation prevents disk exhaustion

### **Network Security**
- Default configuration only exposes necessary ports
- Nginx proxy provides additional security layer
- Rate limiting prevents abuse
- SSL/HTTPS support for encrypted traffic

### **Data Security**
- Database files are isolated in persistent volumes
- Configuration files are preserved during updates
- Backup system includes encryption options
- Sensitive data is excluded from logs

## 📚 Additional Resources

- **Project Repository**: [MediaButler on GitHub](https://github.com/your-username/mediabutler)
- **API Documentation**: Available at `/swagger` endpoint
- **Docker Documentation**: [Docker Official Docs](https://docs.docker.com/)
- **QNAP Container Station**: [QNAP Documentation](https://www.qnap.com/solution/container_station/)

## 📞 Support

For support and troubleshooting:

1. **Check the logs**: `./scripts/monitor-mediabutler.sh --logs`
2. **Review health status**: `./scripts/monitor-mediabutler.sh --status`
3. **Create an issue**: [GitHub Issues](https://github.com/your-username/mediabutler/issues)
4. **Community Forum**: [QNAP Community](https://forum.qnap.com/)

---

**MediaButler QNAP Deployment v1.0.0**
*Optimized for QNAP NAS systems with Container Station*