# MediaButler QNAP User Guide

📺 **Complete user guide for MediaButler on QNAP NAS systems**

## 📋 Table of Contents

- [Getting Started](#getting-started)
- [Daily Operations](#daily-operations)
- [Web Interface Guide](#web-interface-guide)
- [File Management](#file-management)
- [System Maintenance](#system-maintenance)
- [Troubleshooting](#troubleshooting)
- [Tips & Best Practices](#tips--best-practices)

## 🚀 Getting Started

### **First-Time Setup**

After deployment, follow these steps to configure MediaButler:

1. **Access Web Interface**
   - Open browser and navigate to `http://your-qnap-ip:80`
   - You should see the MediaButler dashboard

2. **Configure Media Paths**
   - Go to Configuration page
   - Set your media library path (e.g., `/share/Media/TV Shows`)
   - Set your watch folder path (e.g., `/share/Downloads/Complete`)

3. **Verify File Discovery**
   - Place a test video file in your watch folder
   - Check if it appears in the "Recent Files" page
   - Verify automatic classification is working

### **Understanding the Interface**

MediaButler provides several key pages:

- **🏠 Home**: Dashboard with system overview and recent activity
- **📁 File Management**: Bulk operations and file status management
- **🕒 Recent Files**: Recently processed files organized by category
- **⚙️ Configuration**: System settings and preferences
- **💓 System Health**: Performance monitoring and diagnostics

## 📅 Daily Operations

### **Adding New Media Files**

1. **Automatic Processing**:
   - Copy video files to your watch folder
   - MediaButler automatically detects new files
   - Files are classified using ML algorithms
   - Check the "Recent Files" page for results

2. **Manual Processing**:
   - Navigate to "File Management" page
   - Use "Scan Folder" to manually trigger discovery
   - Review and confirm classifications
   - Move files to organized locations

### **File Status Workflow**

MediaButler tracks files through several states:

```
NEW → PROCESSING → CLASSIFIED → READY TO MOVE → MOVED
                      ↓
                   (confirmation required)
```

**Status Descriptions**:
- **🆕 New**: Just discovered, awaiting processing
- **⚙️ Processing**: Currently being analyzed by ML
- **🤖 ML Classified**: Auto-classified, awaiting confirmation
- **✅ Ready To Move**: Confirmed and ready for organization
- **📦 Moved**: Successfully organized to final location
- **❌ Error**: Processing failed, requires attention
- **🚫 Ignored**: Marked to skip processing

### **Confirming Classifications**

1. **Review Suggestions**:
   - Check files with "ML Classified" status
   - Verify the suggested category is correct
   - Review confidence scores (higher is better)

2. **Confirm or Correct**:
   - ✅ **Accept**: Click confirm to approve the classification
   - ✏️ **Edit**: Modify the category name if needed
   - ❌ **Reject**: Choose a different category or mark as ignored

3. **Batch Operations**:
   - Select multiple files using checkboxes
   - Use bulk actions for faster processing
   - Apply same category to similar files

## 🖥️ Web Interface Guide

### **Dashboard Overview**

The main dashboard provides:

- **📊 System Statistics**: File counts by status
- **📈 Recent Activity**: Latest processed files
- **⚡ Quick Actions**: Common operations
- **🔔 Notifications**: System alerts and updates

### **File Management Page**

**Filter Options**:
- **Status Filter**: Show files by processing status
- **Category Filter**: Filter by TV series/category
- **Date Range**: Show files from specific time period
- **Search**: Find files by name or keyword

**Bulk Operations**:
- **✅ Confirm All**: Approve all displayed classifications
- **📦 Move All**: Organize all ready files
- **🚫 Ignore Selected**: Mark files to skip
- **🔄 Retry Failed**: Reprocess files with errors

### **Recent Files Page**

**Category Views**:
- **📁 Expandable Groups**: Click to show/hide files per series
- **🔍 Search**: Real-time search across all categories
- **📊 Statistics**: File counts per category
- **🕒 Timestamps**: When files were last processed

**File Actions**:
- **ℹ️ Details**: View file information and processing history
- **📂 Open Folder**: Navigate to file location
- **🔄 Reprocess**: Re-run classification if needed
- **🗑️ Remove**: Delete from tracking (file remains on disk)

### **Configuration Page**

**Path Settings**:
- **📂 Media Library**: Where organized files are stored
- **👀 Watch Folder**: Where new files are detected
- **⏳ Pending Review**: Temporary holding area

**Processing Settings**:
- **🤖 Auto-Classify Threshold**: Confidence level for auto-approval
- **⏱️ Scan Interval**: How often to check for new files
- **🔄 Max Concurrent**: Limit simultaneous operations

**Advanced Options**:
- **🏷️ Category Mapping**: Custom category name rules
- **🚫 Ignore Patterns**: Files/folders to skip
- **📝 Logging Level**: Detail level for system logs

## 📁 File Management

### **Supported File Types**

**Video Files**:
- `.mkv`, `.mp4`, `.avi`, `.mov`, `.wmv`, `.flv`
- `.m4v`, `.webm`, `.ts`, `.m2ts`

**Subtitle Files** (auto-moved with video):
- `.srt`, `.sub`, `.ass`, `.vtt`, `.ssa`

**Metadata Files** (auto-moved with video):
- `.nfo`, `.xml`, `.txt` (episode info)

### **File Organization Structure**

MediaButler organizes files in a flat structure:

```
/MediaLibrary/
├── BREAKING BAD/
│   ├── Breaking.Bad.S01E01.mkv
│   ├── Breaking.Bad.S01E01.srt
│   └── Breaking.Bad.S02E01.mkv
├── THE OFFICE/
│   └── The.Office.S01E01.mkv
└── GAME OF THRONES/
    ├── Game.of.Thrones.S01E01.mkv
    └── Game.of.Thrones.S08E06.mkv
```

**Organization Rules**:
- ✅ Flat folder structure (no season subfolders)
- ✅ UPPERCASE category names (series names)
- ✅ Original filenames preserved
- ✅ Related files (.srt, .nfo) moved together
- ✅ Invalid characters sanitized in folder names

### **Handling Special Cases**

**Movies vs TV Series**:
- System primarily designed for TV series
- Movies can be organized in a "MOVIES" category
- Consider separate watch folders for different content types

**Multi-Part Episodes**:
- System handles files like "Episode.Part1.mkv" and "Episode.Part2.mkv"
- Both parts moved to same category folder
- Maintains original naming for clarity

**Foreign Language Content**:
- Language indicators (ITA, ENG, FR) are preserved
- Category classification focuses on series name
- Subtitle files help identify language variants

## 🔧 System Maintenance

### **Regular Monitoring**

**Daily Checks**:
- ✅ Check "Recent Files" for new content
- ✅ Confirm any pending classifications
- ✅ Verify watch folder is being monitored

**Weekly Tasks**:
- 📊 Review system health page
- 🗂️ Clean up processed files from watch folder
- 🔍 Check for any stuck/error files

**Monthly Maintenance**:
- 💾 Review backup status
- 📈 Check system performance metrics
- 🔄 Consider system updates

### **Performance Monitoring**

Access system health via:
- **Web Interface**: Navigate to "System Health" page
- **SSH Command**: `./scripts/monitor-mediabutler.sh --status`

**Key Metrics to Watch**:
- **Memory Usage**: Should stay under 300MB total
- **CPU Usage**: Should be low except during processing
- **Disk Space**: Ensure adequate free space
- **Response Times**: API should respond within 5 seconds

### **Log Management**

**View Logs**:
```bash
# SSH into QNAP
cd /share/Container/mediabutler

# View monitoring logs
./scripts/monitor-mediabutler.sh --logs

# View application logs
docker-compose logs -f mediabutler-api
docker-compose logs -f mediabutler-web
```

**Log Locations**:
- `/logs/api/`: API application logs
- `/logs/web/`: Web interface logs
- `/logs/nginx/`: Proxy server logs
- `/logs/monitor.log`: System monitoring logs

## 🛠️ Troubleshooting

### **Common Issues & Solutions**

#### **Files Not Being Detected**

**Symptoms**: New files in watch folder not appearing in interface

**Solutions**:
1. **Check Watch Folder Path**:
   - Verify path in Configuration page
   - Ensure QNAP share is mounted correctly
   - Test with a simple file like "test.mkv"

2. **Check File Permissions**:
   ```bash
   # SSH into QNAP
   ls -la /share/Downloads/Complete/
   # Files should be readable by admin/administrators
   ```

3. **Manual Scan**:
   - Go to File Management page
   - Click "Scan Folder" button
   - Check system logs for errors

#### **Classification Not Working**

**Symptoms**: Files stuck in "NEW" or "PROCESSING" status

**Solutions**:
1. **Check ML Model**:
   - Verify model files exist in `/models/` directory
   - Check API logs for ML-related errors
   - Restart services if model failed to load

2. **Check File Names**:
   - Ensure filenames contain series information
   - Test with standard naming like "Series.Name.S01E01.mkv"
   - Avoid special characters or very long names

3. **Resource Issues**:
   - Check if system is running low on memory
   - Reduce batch processing if needed
   - Restart services to clear any stuck processes

#### **Web Interface Not Loading**

**Symptoms**: Cannot access web interface at configured port

**Solutions**:
1. **Check Services**:
   ```bash
   cd /share/Container/mediabutler
   docker-compose ps
   # All services should show "Up" status
   ```

2. **Check Network**:
   - Verify port is not blocked by firewall
   - Test from QNAP local console: `curl http://localhost:80`
   - Check QNAP Security Settings > Firewall

3. **Restart Services**:
   ```bash
   docker-compose restart
   # Wait 30 seconds then test access
   ```

#### **High Memory Usage**

**Symptoms**: System becoming slow, containers being killed

**Solutions**:
1. **Reduce Memory Limits**:
   ```bash
   # Edit .env file
   MEMORY_LIMIT_API=120m
   MEMORY_LIMIT_WEB=80m

   # Restart services
   docker-compose up -d
   ```

2. **Optimize Processing**:
   - Reduce batch size in Configuration
   - Increase scan interval to reduce frequency
   - Process fewer files simultaneously

3. **System Cleanup**:
   ```bash
   # Clean Docker resources
   docker system prune -f

   # Clear temporary files
   ./scripts/monitor-mediabutler.sh --cleanup
   ```

### **Getting Help**

When encountering issues:

1. **Collect Information**:
   ```bash
   # Get system status
   ./scripts/monitor-mediabutler.sh --status

   # Get recent logs
   docker-compose logs --tail=50

   # Check resource usage
   docker stats --no-stream
   ```

2. **Check Documentation**:
   - Review this user guide
   - Check deployment guide for configuration issues
   - Review API documentation at `/swagger`

3. **Report Issues**:
   - Create GitHub issue with logs and system info
   - Include steps to reproduce the problem
   - Mention QNAP model and Container Station version

## 💡 Tips & Best Practices

### **Optimal File Naming**

**Good Examples**:
- ✅ `Breaking.Bad.S01E01.Pilot.720p.mkv`
- ✅ `The.Office.US.S02E05.Halloween.mkv`
- ✅ `Game.of.Thrones.S08E06.The.Iron.Throne.1080p.x264.mkv`

**Avoid**:
- ❌ `episode1.mkv` (no series information)
- ❌ `[Random.Release.Group].Show.Name.mkv` (brackets confuse parser)
- ❌ Very long names (>200 characters)

### **Watch Folder Management**

**Best Practices**:
- 📁 Keep watch folder clean - move processed files
- 🏷️ Use consistent naming conventions
- 📦 Unpack archives before placing in watch folder
- 🚫 Avoid nested subdirectories in watch folder

**Automation Tips**:
- Set up download client to extract archives automatically
- Configure download client to use consistent naming
- Create separate watch folders for different content types

### **Category Management**

**Naming Convention**:
- Use title case for readability: "Breaking Bad"
- System will auto-convert to UPPERCASE for folders
- Keep names concise but descriptive
- Use consistent abbreviations

**Custom Categories**:
- Create custom categories for special content
- Use "MOVIES" for film content
- Consider "DOCUMENTARIES", "ANIME", etc.

### **Performance Optimization**

**For Limited RAM Systems**:
- Process files in smaller batches
- Increase scan interval to reduce frequency
- Monitor memory usage regularly
- Consider processing during off-peak hours

**For Better Performance**:
- Use SSD storage for database
- Ensure good network connectivity
- Keep system updated
- Regular cleanup of old logs and temporary files

### **Backup Strategy**

**Recommended Schedule**:
- **Daily**: Automatic configuration backup
- **Weekly**: Full system backup
- **Before Updates**: Pre-update backup
- **Monthly**: Offsite backup copy

**What to Backup**:
- ✅ Database files (tracked file information)
- ✅ Configuration files (settings, preferences)
- ✅ ML models (if customized)
- ❌ Media files (already backed up separately)
- ❌ Log files (regenerated)

---

**MediaButler QNAP User Guide v1.0.0**
*For questions and support, visit the project repository or community forums*