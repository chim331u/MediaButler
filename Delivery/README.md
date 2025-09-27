# MediaButler QNAP Deployment Package

🚀 **Separated deployment solution for MediaButler on QNAP NAS systems**

[![Version](https://img.shields.io/badge/version-2.0.0-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-QNAP%20ARM32%20|%20ARM64-green.svg)]()
[![Memory](https://img.shields.io/badge/memory-<300MB-orange.svg)]()
[![Docker](https://img.shields.io/badge/docker-required-blue.svg)]()

## 🎯 Overview

This package provides optimized, component-based deployment scripts for MediaButler on QNAP NAS systems. Following "Simple Made Easy" principles, each component can be deployed independently for better reliability and debugging.

### **What's Included**

- ✅ **Separated Deployment Scripts** - API and WEB deployed independently
- ✅ **Multiple Dockerfile Variants** - Optimized, simple, and minimal builds
- ✅ **Robust Error Handling** - Fallback mechanisms and comprehensive validation
- ✅ **ARM32 Optimizations** - Specifically tuned for 1GB RAM systems
- ✅ **Flexible Orchestration** - Deploy components together or separately

### **New Architecture (v2.0.0)**

```
Individual Components (Independent Deployment)
┌─────────────────┐  ┌─────────────────┐
│  MediaButler    │  │  MediaButler    │
│      API        │  │   Web UI        │
│  (~150MB RAM)   │  │  (~100MB RAM)   │
│  Port: 30129    │  │  Port: 30139    │
└─────────────────┘  └─────────────────┘
                    ┌─────────────────┐
                    │  Shared Storage │
                    │   - Database    │
                    │   - ML Models   │
                    │   - Media Files │
                    └─────────────────┘
```

## 📦 Package Contents

```
MediaButler/Delivery/
├── scripts/
│   ├── deploy-mediabutler-qnap.sh    # Orchestrator script
│   ├── deploy-mediabutler-api.sh     # API-only deployment
│   ├── deploy-mediabutler-web.sh     # WEB-only deployment
│   ├── monitor-mediabutler.sh        # Health monitoring
│   ├── backup-mediabutler.sh         # Backup & restore
│   └── update-mediabutler.sh         # Update management
├── docker/
│   ├── Dockerfile.api                # Standard API container
│   ├── Dockerfile.web                # Standard Web container
│   ├── api-minimal.dockerfile        # Fast build variant
│   ├── api-simple.dockerfile         # Balanced variant
│   └── api-optimized.dockerfile      # Production variant
└── config/
    ├── docker-compose.template.yml   # Legacy compatibility
    └── nginx.template.conf           # Nginx configuration
```

## 🚀 Quick Start

### **Prerequisites**

- **QNAP NAS** with Container Station installed
- **Minimum 1GB RAM** (400MB+ available)
- **2GB+ free disk space**
- **Internet connection** for initial setup

### **1. Download Package**

```bash
# SSH into your QNAP NAS
ssh admin@your-qnap-ip

# Download and extract deployment package
wget -O mediabutler-qnap.zip https://github.com/your-username/mediabutler/releases/latest/download/qnap-deployment.zip
unzip mediabutler-qnap.zip
cd MediaButler/Delivery
```

### **2. Configure Deployment**

```bash
# Set your GitHub repository URL
export GITHUB_REPO_URL="https://github.com/your-username/mediabutler"

# Optional: Customize paths and ports
export INSTALL_PATH="/share/Container/mediabutler"
export PROXY_PORT="80"
export MEDIA_LIBRARY_PATH="/share/Media/TV Shows"
export WATCH_FOLDER_PATH="/share/Downloads/Complete"
```

### **3. Deploy MediaButler**

```bash
# Make deployment scripts executable
chmod +x scripts/*.sh

# Deploy both API and WEB (default)
./scripts/deploy-mediabutler-qnap.sh

# Or deploy components separately:
# ./scripts/deploy-mediabutler-api.sh    # API only
# ./scripts/deploy-mediabutler-web.sh    # WEB only
```

### **4. Access Your Installation**

After deployment completes (5-15 minutes):

- **Web Interface**: `http://your-qnap-ip:30139`
- **API Documentation**: `http://your-qnap-ip:30129/swagger`
- **API Health Status**: `http://your-qnap-ip:30129/health`

## 📊 System Requirements

### **Minimum Requirements**
| Component | Requirement |
|-----------|-------------|
| **RAM** | 1GB total (400MB+ available) |
| **CPU** | ARM32/ARM64/x64 |
| **Storage** | 2GB free space |
| **Network** | Internet access + LAN |
| **Software** | Container Station |

### **Recommended Specifications**
| Component | Recommendation |
|-----------|----------------|
| **RAM** | 2GB+ for better performance |
| **Storage** | SSD for database |
| **Network** | Gigabit Ethernet |

### **Resource Usage**
| Service | Memory Limit | CPU Usage | Purpose |
|---------|-------------|-----------|---------|
| **API** | 150MB | ~10% | Core processing |
| **Web UI** | 100MB | ~5% | User interface |
| **Proxy** | 20MB | ~1% | Load balancing |
| **Total** | **~270MB** | **~16%** | **Complete system** |

## ⚙️ Configuration Options

### **Basic Configuration**

```bash
# Repository and source
export GITHUB_REPO_URL="https://github.com/chim331u/MediaButler.git"
export GITHUB_BRANCH="main"

# Network configuration
export API_PORT="30129"             # API service port
export WEB_PORT="30139"             # Web interface port
# Deployment options
export DEPLOY_API="true"            # Deploy API component
export DEPLOY_WEB="true"            # Deploy WEB component

# Installation paths
export INSTALL_PATH="/share/Container/mediabutler"
export MEDIA_LIBRARY_PATH="/share/Media/TV Shows"
export WATCH_FOLDER_PATH="/share/Downloads/Complete"
```

### **Advanced Configuration**

```bash
# Memory optimization
export MEMORY_LIMIT_API="150m"      # API container memory limit
export MEMORY_LIMIT_WEB="100m"      # Web container memory limit
export MEMORY_LIMIT_PROXY="20m"     # Proxy container memory limit

# SSL/HTTPS support
export SSL_ENABLED="true"
export SSL_CERT_PATH="/path/to/cert.pem"
export SSL_KEY_PATH="/path/to/key.pem"
export PROXY_SSL_PORT="443"

# Backup and monitoring
export BACKUP_ENABLED="true"
export MONITORING_ENABLED="true"
```

## 🛠️ Management Commands

After deployment, manage your MediaButler installation:

### **Service Management**
```bash
cd /share/Container/mediabutler

# Check status
docker-compose ps

# View logs
docker-compose logs -f

# Restart services
docker-compose restart

# Stop services
docker-compose down

# Start services
docker-compose up -d
```

### **Monitoring**
```bash
# Check system health
./scripts/monitor-mediabutler.sh --status

# View monitoring logs
./scripts/monitor-mediabutler.sh --logs

# View alerts
./scripts/monitor-mediabutler.sh --alerts

# Manual health check
./scripts/monitor-mediabutler.sh
```

### **Backup & Restore**
```bash
# Create full backup
./scripts/backup-mediabutler.sh --full

# List available backups
./scripts/backup-mediabutler.sh --list

# Restore from backup
./scripts/backup-mediabutler.sh --restore backup_file.tar.gz

# Show backup status
./scripts/backup-mediabutler.sh --status
```

### **Updates**
```bash
# Check for updates
./scripts/update-mediabutler.sh --check

# Perform update
./scripts/update-mediabutler.sh

# Force update
./scripts/update-mediabutler.sh --force

# Show update history
./scripts/update-mediabutler.sh --status
```

## 📈 Performance Optimization

### **For 1GB RAM Systems**
```bash
# Reduce memory limits
export MEMORY_LIMIT_API="120m"
export MEMORY_LIMIT_WEB="80m"
export MEMORY_LIMIT_PROXY="15m"

# Optimize processing
# Edit docker-compose.yml:
- MediaButler__ML__MaxBatchSize=5
- MediaButler__FileDiscovery__MaxConcurrentScans=1
```

### **For 2GB+ RAM Systems**
```bash
# Increase memory limits for better performance
export MEMORY_LIMIT_API="250m"
export MEMORY_LIMIT_WEB="150m"
export MEMORY_LIMIT_PROXY="50m"

# Enable more concurrent processing
- MediaButler__ML__MaxBatchSize=20
- MediaButler__FileDiscovery__MaxConcurrentScans=2
```

## 🔧 Troubleshooting

### **Common Issues**

#### **Services Won't Start**
```bash
# Check logs for errors
docker-compose logs

# Verify port availability
netstat -tulpn | grep :80

# Check resource usage
free -h
```

#### **High Memory Usage**
```bash
# Monitor resource usage
docker stats --no-stream

# Reduce memory limits
# Edit .env file and restart services
```

#### **Files Not Being Detected**
```bash
# Check path mappings
docker inspect mediabutler-api | grep Mounts -A 10

# Verify permissions
ls -la /path/to/watch/folder/

# Manual scan
curl -X POST http://localhost:80/api/scan/folder
```

#### **Web Interface Not Loading**
```bash
# Check service status
docker-compose ps

# Test local access
curl http://localhost:80

# Check firewall settings
```

## 📚 Documentation

Comprehensive documentation is included in the `docs/` directory:

- **[📖 Deployment Guide](docs/DEPLOYMENT_GUIDE.md)** - Complete installation and configuration
- **[👤 User Guide](docs/USER_GUIDE.md)** - Daily operations and usage
- **[🔧 Troubleshooting](docs/TROUBLESHOOTING.md)** - Problem resolution and diagnostics

### **Quick Links**

- **API Documentation**: Available at `/swagger` after deployment
- **GitHub Repository**: [MediaButler Project](https://github.com/your-username/mediabutler)
- **Container Station**: [QNAP Documentation](https://www.qnap.com/solution/container_station/)

## 🛡️ Security & Best Practices

### **Security Features**
- ✅ All containers run as non-root users
- ✅ Resource limits prevent system exhaustion
- ✅ Health checks enable automatic recovery
- ✅ Log rotation prevents disk filling
- ✅ SSL/HTTPS support available

### **Best Practices**
- 📅 **Regular Backups**: Automated weekly backups
- 🔍 **Health Monitoring**: Automated every 5 minutes
- 🔄 **Keep Updated**: Monthly update checks
- 📊 **Monitor Resources**: Regular performance review
- 🚫 **Access Control**: Secure your QNAP web interface

## 📞 Support & Community

### **Getting Help**

1. **📚 Check Documentation**: Start with included guides
2. **🔍 Search Issues**: [GitHub Issues](https://github.com/your-username/mediabutler/issues)
3. **💬 Community Forum**: [QNAP Community](https://forum.qnap.com/)
4. **🐛 Report Bugs**: Create detailed GitHub issues

### **Contributing**

We welcome contributions to improve the deployment package:

- 🐛 **Bug Reports**: Report issues with deployment scripts
- ✨ **Feature Requests**: Suggest improvements
- 📝 **Documentation**: Help improve guides
- 🔧 **Code Contributions**: Submit pull requests

## 📄 License

This deployment package is provided under the same license as the main MediaButler project.

## 🙏 Acknowledgments

- **QNAP Community** for Container Station platform
- **Docker Community** for containerization technology
- **MediaButler Contributors** for the core application

---

**MediaButler QNAP Deployment Package v1.0.0**

*Intelligent TV series file organization for QNAP NAS systems*

[![Deploy Now](https://img.shields.io/badge/Deploy%20Now-blue?style=for-the-badge)](./scripts/deploy-mediabutler-qnap.sh)
[![Documentation](https://img.shields.io/badge/Documentation-green?style=for-the-badge)](./docs/DEPLOYMENT_GUIDE.md)
[![Support](https://img.shields.io/badge/Support-orange?style=for-the-badge)](https://github.com/your-username/mediabutler/issues)