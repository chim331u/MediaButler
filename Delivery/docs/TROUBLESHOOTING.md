# MediaButler QNAP Troubleshooting Guide

🔧 **Comprehensive troubleshooting guide for MediaButler on QNAP NAS systems**

## 📋 Quick Diagnosis

Before diving into specific issues, run this quick diagnostic:

```bash
# SSH into your QNAP
cd /share/Container/mediabutler

# Check overall system status
./scripts/monitor-mediabutler.sh --status

# Check service health
docker-compose ps

# Check resource usage
docker stats --no-stream

# Test endpoints
curl -s http://localhost:80/health | jq .
```

## 🚨 Emergency Procedures

### **System Completely Unresponsive**

1. **Force Restart All Services**:
```bash
cd /share/Container/mediabutler
docker-compose down --remove-orphans
sleep 10
docker-compose up -d
```

2. **If Still Unresponsive**:
```bash
# Emergency recovery
./scripts/monitor-mediabutler.sh
# This will attempt automatic recovery
```

3. **Last Resort - Complete Reset**:
```bash
# Stop everything
docker-compose down --remove-orphans

# Clean up Docker resources
docker system prune -f

# Restart from backup
./scripts/backup-mediabutler.sh --restore latest_backup.tar.gz
```

### **Out of Memory/Disk Space**

1. **Immediate Cleanup**:
```bash
# Clean Docker resources
docker system prune -f

# Clean application logs
find /share/Container/mediabutler/logs -name "*.log" -mtime +7 -delete

# Clean temporary files
rm -rf /tmp/*mediabutler* 2>/dev/null
```

2. **Reduce Memory Usage**:
```bash
# Edit memory limits
cd /share/Container/mediabutler
# Reduce memory limits in .env file
docker-compose up -d
```

## 📊 Diagnostic Tools

### **System Health Check**

```bash
#!/bin/bash
# Complete system diagnostic

echo "=== MediaButler System Diagnostic ==="
echo "Date: $(date)"
echo "Host: $(hostname)"
echo

echo "=== System Resources ==="
echo "Memory Usage:"
free -h

echo "Disk Usage:"
df -h /share/Container/mediabutler

echo "System Load:"
uptime

echo

echo "=== Docker Status ==="
echo "Docker Version:"
docker --version

echo "Docker Info:"
docker info | grep -E "Total Memory|Available Memory|CPUs"

echo

echo "=== MediaButler Services ==="
cd /share/Container/mediabutler
docker-compose ps

echo

echo "=== Service Health ==="
for service in mediabutler-api mediabutler-web mediabutler-proxy; do
    health=$(docker inspect --format='{{.State.Health.Status}}' $service 2>/dev/null || echo "no_health_check")
    echo "$service: $health"
done

echo

echo "=== Network Connectivity ==="
for port in 5000 3000 80; do
    if nc -z localhost $port 2>/dev/null; then
        echo "Port $port: OPEN"
    else
        echo "Port $port: CLOSED"
    fi
done

echo

echo "=== Recent Errors ==="
docker-compose logs --since="1h" | grep -i error | tail -10

echo "=== Diagnostic Complete ==="
```

### **Log Analysis**

```bash
#!/bin/bash
# Analyze logs for common issues

echo "=== Log Analysis ==="

echo "Recent API Errors:"
docker-compose logs mediabutler-api --since="24h" | grep -i "error\|exception\|fatal" | tail -5

echo

echo "Recent Web Errors:"
docker-compose logs mediabutler-web --since="24h" | grep -i "error\|exception\|fatal" | tail -5

echo

echo "Memory-related Issues:"
docker-compose logs --since="24h" | grep -i "memory\|oom\|killed" | tail -5

echo

echo "Database Issues:"
docker-compose logs mediabutler-api --since="24h" | grep -i "database\|sqlite\|connection" | tail -5

echo

echo "Classification Issues:"
docker-compose logs mediabutler-api --since="24h" | grep -i "classification\|ml\|model" | tail -5
```

## 🔍 Specific Issue Resolution

### **Issue: Services Won't Start**

**Symptoms**:
- Containers exit immediately after starting
- "Exited (1)" status in `docker-compose ps`
- Cannot access web interface

**Diagnosis**:
```bash
# Check container logs
docker-compose logs mediabutler-api
docker-compose logs mediabutler-web

# Check for port conflicts
netstat -tulpn | grep -E ":80|:3000|:5000"

# Check file permissions
ls -la /share/Container/mediabutler/
```

**Solutions**:

1. **Port Conflict**:
```bash
# Change ports in .env file
PROXY_PORT=8080
API_PORT=5001
WEB_PORT=3001

# Restart services
docker-compose up -d
```

2. **Permission Issues**:
```bash
# Fix permissions
sudo chown -R admin:administrators /share/Container/mediabutler/
sudo chmod -R 755 /share/Container/mediabutler/
```

3. **Configuration Issues**:
```bash
# Validate configuration
cd /share/Container/mediabutler
docker-compose config

# Check for syntax errors in docker-compose.yml
```

4. **Resource Constraints**:
```bash
# Check available resources
free -h
df -h

# Reduce resource limits if needed
# Edit .env file to lower memory limits
```

### **Issue: High Memory Usage / OOM Kills**

**Symptoms**:
- Containers randomly stopping
- "OOM Killed" messages in logs
- System becoming unresponsive

**Diagnosis**:
```bash
# Monitor memory usage in real-time
docker stats

# Check system memory
free -h

# Check for memory leaks
docker-compose logs | grep -i "memory\|oom"

# Check container resource limits
docker inspect mediabutler-api | grep -A 10 Resources
```

**Solutions**:

1. **Immediate Relief**:
```bash
# Restart services to clear memory
docker-compose restart

# Clean up Docker resources
docker system prune -f
```

2. **Reduce Memory Limits**:
```bash
# Edit .env file
MEMORY_LIMIT_API=100m
MEMORY_LIMIT_WEB=70m
MEMORY_LIMIT_PROXY=15m

# Apply changes
docker-compose up -d
```

3. **Optimize Application Settings**:
```bash
# Reduce batch processing
# Edit docker-compose.yml environment variables:
- MediaButler__ML__MaxBatchSize=5
- MediaButler__FileDiscovery__MaxConcurrentScans=1
- MediaButler__Performance__MaxConcurrentOperations=1
```

4. **System-Level Optimization**:
```bash
# Add swap file (if not present)
sudo fallocate -l 1G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile

# Make permanent
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

### **Issue: Files Not Being Detected**

**Symptoms**:
- Files in watch folder not appearing in interface
- No new files in "Recent Files" page
- Manual scan doesn't find files

**Diagnosis**:
```bash
# Check watch folder configuration
docker-compose logs mediabutler-api | grep -i "watch\|scan\|discovery"

# Check file permissions in watch folder
ls -la /path/to/watch/folder/

# Check if path mapping is correct
docker inspect mediabutler-api | grep -A 20 Mounts
```

**Solutions**:

1. **Verify Path Mapping**:
```bash
# Check volume mounts in docker-compose.yml
# Ensure watch folder is correctly mapped
volumes:
  - /your/actual/watch/path:/app/data/watch
```

2. **Fix File Permissions**:
```bash
# Ensure files are readable
chmod 644 /path/to/watch/folder/*
chown admin:administrators /path/to/watch/folder/*
```

3. **Check File Types**:
```bash
# Verify file extensions are supported
# Supported: .mkv, .mp4, .avi, .mov, .wmv, .flv, .m4v, .webm, .ts, .m2ts
```

4. **Manual Trigger**:
```bash
# Force rescan via API
curl -X POST http://localhost:80/api/scan/folder \
  -H "Content-Type: application/json" \
  -d '{"path": "/app/data/watch"}'
```

5. **Check File Size Limits**:
```bash
# Verify minimum file size setting
# Files smaller than MinFileSizeMB are ignored
# Check configuration: MediaButler__FileDiscovery__MinFileSizeMB
```

### **Issue: Classification Failing**

**Symptoms**:
- Files stuck in "PROCESSING" status
- Low confidence scores
- Classification errors in logs

**Diagnosis**:
```bash
# Check ML model status
docker-compose logs mediabutler-api | grep -i "model\|ml\|classification"

# Check if model files exist
docker exec mediabutler-api ls -la /app/models/

# Test classification endpoint
curl -X POST http://localhost:80/api/classification/classify \
  -H "Content-Type: application/json" \
  -d '{"fileName": "Test.Series.S01E01.mkv"}'
```

**Solutions**:

1. **Verify ML Model**:
```bash
# Check if model files are present
docker exec mediabutler-api find /app/models -name "*.bin" -o -name "*.model"

# If missing, rebuild containers
docker-compose build --no-cache
docker-compose up -d
```

2. **Test with Standard Naming**:
```bash
# Rename file to standard format
# Good: "Series.Name.S01E01.Episode.Title.720p.mkv"
# Bad: "random_file_name.mkv"
```

3. **Check Resource Availability**:
```bash
# Ensure enough memory for ML processing
docker stats mediabutler-api

# If memory constrained, reduce batch size
# MediaButler__ML__MaxBatchSize=1
```

4. **Reset Classification**:
```bash
# Clear stuck classifications via API
curl -X POST http://localhost:80/api/files/reset-processing
```

### **Issue: Web Interface Loading Slowly**

**Symptoms**:
- Long loading times for pages
- Timeouts when accessing interface
- Partial page loads

**Diagnosis**:
```bash
# Check web service logs
docker-compose logs mediabutler-web

# Check network latency
time curl -s http://localhost:80/ > /dev/null

# Check resource usage
docker stats mediabutler-web mediabutler-proxy
```

**Solutions**:

1. **Optimize Resource Allocation**:
```bash
# Increase web service memory
MEMORY_LIMIT_WEB=150m

# Restart services
docker-compose up -d
```

2. **Check Network Configuration**:
```bash
# Verify nginx configuration
docker exec mediabutler-proxy nginx -t

# Check for errors in proxy logs
docker-compose logs mediabutler-proxy
```

3. **Clear Browser Cache**:
- Clear browser cache and cookies
- Try accessing from different browser/device
- Check browser developer console for errors

4. **Database Optimization**:
```bash
# Check database size and performance
docker exec mediabutler-api sqlite3 /app/data/mediabutler.db "VACUUM;"
docker exec mediabutler-api sqlite3 /app/data/mediabutler.db "ANALYZE;"
```

### **Issue: API Endpoints Not Responding**

**Symptoms**:
- HTTP 502/503 errors
- API health check failing
- Swagger documentation not loading

**Diagnosis**:
```bash
# Test API directly
curl -v http://localhost:5000/health

# Check API logs
docker-compose logs mediabutler-api | tail -20

# Check if API container is running
docker inspect mediabutler-api | grep Status
```

**Solutions**:

1. **Restart API Service**:
```bash
docker-compose restart mediabutler-api

# Wait for health check
sleep 30
curl http://localhost:80/health
```

2. **Check Database Connectivity**:
```bash
# Verify database file exists and is readable
docker exec mediabutler-api ls -la /app/data/mediabutler.db

# Test database connection
docker exec mediabutler-api sqlite3 /app/data/mediabutler.db "SELECT COUNT(*) FROM TrackedFiles;"
```

3. **Verify Environment Configuration**:
```bash
# Check environment variables
docker exec mediabutler-api env | grep -E "ASPNETCORE|MediaButler"

# Validate configuration
docker exec mediabutler-api cat /app/appsettings.json
```

4. **Resource Issues**:
```bash
# Check if container is resource constrained
docker stats mediabutler-api

# Check for OOM kills
dmesg | grep -i "killed process"
```

## 🔄 Recovery Procedures

### **Rolling Back Updates**

If an update causes issues:

```bash
# Check update history
./scripts/update-mediabutler.sh --status

# Rollback to previous version
./scripts/backup-mediabutler.sh --list
./scripts/backup-mediabutler.sh --restore pre_update_YYYYMMDD_HHMMSS.tar.gz
```

### **Database Recovery**

If database becomes corrupted:

```bash
# Stop services
docker-compose down

# Backup current database
cp /share/Container/mediabutler/data/mediabutler.db /tmp/mediabutler.db.backup

# Try to repair
sqlite3 /share/Container/mediabutler/data/mediabutler.db "PRAGMA integrity_check;"

# If corrupted, restore from backup
./scripts/backup-mediabutler.sh --restore latest_data_backup.tar.gz

# Restart services
docker-compose up -d
```

### **Complete System Reset**

As a last resort:

```bash
# Backup important data
./scripts/backup-mediabutler.sh --data

# Stop and remove everything
docker-compose down --volumes --remove-orphans
docker system prune -a -f

# Re-deploy from scratch
GITHUB_REPO_URL="your-repo-url" ./scripts/deploy-mediabutler-qnap.sh

# Restore data
./scripts/backup-mediabutler.sh --restore data_backup.tar.gz
```

## 📞 Getting Help

### **Information to Collect**

When seeking help, gather this information:

```bash
# System information
uname -a
docker --version
docker-compose --version

# MediaButler status
./scripts/monitor-mediabutler.sh --status

# Recent logs
docker-compose logs --since="1h" > mediabutler_logs.txt

# Configuration (remove sensitive data)
cat .env | grep -v PASSWORD | grep -v SECRET
```

### **Where to Get Help**

1. **Documentation**:
   - Check this troubleshooting guide
   - Review deployment guide
   - Check user guide for usage questions

2. **Community Support**:
   - GitHub Issues (preferred for bugs)
   - QNAP Community Forums
   - Docker/Container Station forums

3. **Self-Service Tools**:
   - Built-in monitoring scripts
   - System health dashboard
   - API documentation at `/swagger`

### **Creating Effective Bug Reports**

Include these details:

- **Environment**: QNAP model, Container Station version, available RAM
- **Steps to Reproduce**: Exact steps that trigger the issue
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Logs**: Recent error logs from affected services
- **Configuration**: Relevant settings (sanitized)
- **Workarounds**: Any temporary fixes you've tried

---

**MediaButler QNAP Troubleshooting Guide v1.0.0**
*For additional help, visit the project repository or community forums*