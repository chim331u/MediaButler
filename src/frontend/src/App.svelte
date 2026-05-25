<script>
  import { onMount } from 'svelte';

  // --- Svelte 5 Runes for Declarative State ---
  let currentTab = $state('dashboard');
  let activeFiles = $state([]);
  let pendingFiles = $state([]);
  let historyFiles = $state([]);
  let config = $state(null);
  let sseConnected = $state(false);
  let isLoading = $state(false);
  let isSubmitting = $state(false);

  // Pagination for History Log
  let historySkip = $state(0);
  let historyTake = $state(10);
  let historyHasMore = $state(true);

  // Confirmation Modal State
  let showModal = $state(false);
  let selectedFile = $state(null);
  let customCategory = $state('');
  
  // Custom categories autocompletion list
  let dbCategories = $state([]);
  const presetCategories = ['MOVIES', 'TV SHOWS', 'MUSIC', 'DOCS', 'PHOTOS'];
  let dropdownOpen = $state(false);

  // Active SSE Move Progress tracking
  // Key: file hash, Value: MoveProgressPayload
  let activeProgresses = $state({});

  // Dynamic Toast Notifications
  let notifications = $state([]);

  // --- Filtering & Selection States (Svelte 5 Runes) ---
  let statusFilter = $state(null); // null means all
  let selectedHashes = $state(new Set());
  let expandedHashes = $state(new Set());

  // Filter and display files reactively using $derived
  let displayedFiles = $derived(
    activeFiles.filter(f => {
      if (statusFilter === null) return f.status !== 5 && f.status !== 8;
      if (statusFilter === 'new') return f.status === 0 || f.status === 7;
      if (statusFilter === 'processing') return f.status === 1 || f.status === 4 || f.status === 3;
      if (statusFilter === 'pending') return f.status === 2 || f.status === 6;
      if (statusFilter === 'moved') return f.status === 5;
      return f.status === statusFilter;
    })
  );

  // Eligible files for selection to organize/move (status Confirmed=3 or Error=6)
  let eligibleFiles = $derived(
    activeFiles.filter(f => f.status === 3 || f.status === 6)
  );

  function toggleSelectFile(hash) {
    if (selectedHashes.has(hash)) {
      selectedHashes.delete(hash);
    } else {
      selectedHashes.add(hash);
    }
    selectedHashes = new Set(selectedHashes); // force reactivity
  }

  function toggleSelectAll() {
    const eligibleHashes = eligibleFiles.map(f => f.hash);
    const allSelected = eligibleHashes.every(h => selectedHashes.has(h));
    if (allSelected) {
      selectedHashes = new Set();
    } else {
      selectedHashes = new Set(eligibleHashes);
    }
  }

  function toggleExpandRow(hash) {
    if (expandedHashes.has(hash)) {
      expandedHashes.delete(hash);
    } else {
      expandedHashes.add(hash);
    }
    expandedHashes = new Set(expandedHashes);
  }

  function toggleFilter(status) {
    if (statusFilter === status) {
      statusFilter = null;
    } else {
      statusFilter = status;
    }
  }

  function showToast(type, title, message) {
    const id = Math.random().toString(36).substring(2, 9);
    notifications = [...notifications, { id, type, title, message }];
    setTimeout(() => {
      notifications = notifications.filter(n => n.id !== id);
    }, 6000);
  }

  // --- API Connection Methods ---

  async function fetchConfig() {
    try {
      const res = await fetch('/api/config');
      if (res.ok) {
        config = await res.json();
      }
    } catch (err) {
      console.error('Failed to fetch config', err);
    }
  }

  async function fetchDbCategories() {
    try {
      const res = await fetch('/api/categories');
      if (res.ok) {
        dbCategories = await res.json();
      }
    } catch (err) {
      console.error('Failed to fetch db categories', err);
    }
  }

  async function fetchDashboard() {
    isLoading = true;
    try {
      // Get files including recently moved (take=100) to support dynamic moved filter
      const res = await fetch('/api/files?take=100');
      if (res.ok) {
        const data = await res.json();
        // Keep status 5 (Moved) in activeFiles so they can be filtered reattivamente,
        // but exclude Ignored (8)
        activeFiles = data.filter(f => f.status !== 8);
      }
    } catch (err) {
      showToast('error', 'Fetch Error', 'Failed to retrieve active files queue.');
    } finally {
      isLoading = false;
    }
  }

  async function fetchPending() {
    try {
      const res = await fetch('/api/files/pending');
      if (res.ok) {
        pendingFiles = await res.json();
      }
    } catch (err) {
      console.error('Failed to fetch pending files', err);
    }
  }

  async function fetchHistory() {
    try {
      const res = await fetch(`/api/files?status=5&skip=${historySkip}&take=${historyTake}`);
      if (res.ok) {
        const data = await res.json();
        historyFiles = data;
        historyHasMore = data.length === historyTake;
      }
    } catch (err) {
      showToast('error', 'Fetch Error', 'Failed to retrieve files history.');
    }
  }

  // Confirm File Category manually (does not start move, just updates status to Confirmed/ReadyToMove)
  async function confirmFile(hash, categoryToConfirm) {
    isSubmitting = true;
    const categoryUpper = categoryToConfirm.trim().toUpperCase();
    try {
      const res = await fetch(`/api/files/${hash}/confirm`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ category: categoryUpper })
      });
      
      if (res.ok) {
        showToast('success', 'Category Confirmed', `File category confirmed manually as "${categoryUpper}". Ready to move.`);
        closeConfirmModal();
        
        // Update file status in local dashboard to ReadyToMove (3)
        activeFiles = activeFiles.map(f => {
          if (f.hash === hash) {
            return { ...f, status: 3, category: categoryUpper };
          }
          return f;
        });

        // Refresh database categories
        await fetchDbCategories();
      } else {
        const errData = await res.json();
        showToast('error', 'Action Failed', errData.error || 'Failed to confirm category.');
      }
    } catch (err) {
      showToast('error', 'Connection Error', 'Network error while confirming category.');
    } finally {
      isSubmitting = false;
    }
  }

  // Trigger manual Watch Folder rescan on backend
  async function triggerRescan() {
    try {
      const res = await fetch('/api/rescan', { method: 'POST' });
      if (res.ok) {
        showToast('info', 'Scan Initiated', 'Manual Watch Folder rescan triggered in background.');
        setTimeout(fetchDashboard, 1000);
      } else {
        showToast('error', 'Action Failed', 'Failed to trigger manual rescan.');
      }
    } catch (err) {
      showToast('error', 'Connection Error', 'Network error while rescanning watch folders.');
    }
  }

  // Trigger manual Bayes retrain based on DB history
  async function triggerRetrain() {
    try {
      const res = await fetch('/api/retrain', { method: 'POST' });
      if (res.ok) {
        showToast('success', 'Model Retrained', 'Naive Bayes statistical model rebuilt successfully from history.');
        await fetchDbCategories();
      } else {
        showToast('error', 'Action Failed', 'Failed to retrain model.');
      }
    } catch (err) {
      showToast('error', 'Connection Error', 'Network error while retraining model.');
    }
  }

  // Force classification of a file (e.g. for New files)
  async function forceClassifyFile(hash) {
    try {
      const res = await fetch(`/api/files/${hash}/classify`, { method: 'POST' });
      if (res.ok) {
        const data = await res.json();
        showToast('success', 'Classification Complete', `File categorized successfully as "${data.suggestedCategory}" (${(data.confidence * 100).toFixed(0)}%)`);
        
        // Update file status locally to classified/updated
        activeFiles = activeFiles.map(f => {
          if (f.hash === hash) {
            return { ...f, status: data.status, suggestedCategory: data.suggestedCategory, confidence: data.confidence };
          }
          return f;
        });
      } else {
        showToast('error', 'Action Failed', 'Failed to classify file.');
      }
    } catch (err) {
      showToast('error', 'Connection Error', 'Network error while triggering classification.');
    }
  }

  // Perform bulk async file moves for all selected Confirmed or Error files sequentially
  async function moveSelectedFiles() {
    const hashesToMove = Array.from(selectedHashes);
    if (hashesToMove.length === 0) return;

    showToast('info', 'Move Started', `Starting organization of ${hashesToMove.length} selected files...`);
    
    // Clear selection
    selectedHashes = new Set();

    for (const hash of hashesToMove) {
      const file = activeFiles.find(f => f.hash === hash);
      if (!file) continue;

      // Optimistically update file status in local dashboard to Moving (4)
      activeFiles = activeFiles.map(f => {
        if (f.hash === hash) {
          return { ...f, status: 4 };
        }
        return f;
      });

      // Initialize progress tracker placeholder
      activeProgresses[hash] = {
        hash,
        fileName: file.fileName,
        progress: 0,
        bytesCopied: 0,
        totalBytes: file.fileSize
      };

      try {
        const res = await fetch(`/api/files/${hash}/move`, {
          method: 'POST'
        });
        if (!res.ok) {
          const errData = await res.json();
          showToast('error', 'Move Failed', `Failed to start move for "${file.fileName}": ${errData.error || 'Server error'}`);
        }
      } catch (err) {
        console.error(`Failed to move file ${hash}`, err);
      }
    }
    
    await fetchDashboard();
  }

  // Ignore / Remove file from queue
  function ignoreFile(hash) {
    // There is no custom ignore API yet, so we just remove it locally or mock ignoring.
    activeFiles = activeFiles.filter(f => f.hash !== hash);
    pendingFiles = pendingFiles.filter(f => f.hash !== hash);
    showToast('info', 'Ignored', 'File ignored and hidden from workspace.');
    closeConfirmModal();
  }

  // --- Real-time SSE Setup ---
  
  function connectSSE() {
    const sseUrl = '/api/events';
    console.log(`Connecting to SSE stream at ${sseUrl}...`);
    const eventSource = new EventSource(sseUrl);

    eventSource.onopen = () => {
      console.log('SSE Stream connected successfully.');
      sseConnected = true;
    };

    eventSource.onerror = (err) => {
      console.warn('SSE Stream disconnected. Reconnecting in 5s...', err);
      sseConnected = false;
    };

    // Connection Handshake
    eventSource.addEventListener('connected', () => {
      console.log('SSE Handshake received.');
      sseConnected = true;
    });

    // File discovered & classified by watcher
    eventSource.addEventListener('file.discovered', (e) => {
      try {
        const payload = JSON.parse(e.data);
        console.log(`File discovered: ${payload.fileName}`);
        showToast('info', 'New File Discovered', `"${payload.fileName}" detected in watch folder.`);
        fetchDashboard();
      } catch (err) {
        console.error('Failed to parse discovered SSE event', err);
      }
    });

    // File category confirmed (from other sessions or devices)
    eventSource.addEventListener('file.confirmed', (e) => {
      try {
        const payload = JSON.parse(e.data);
        console.log(`File confirmed: ${payload.hash} -> ${payload.category}`);
        activeFiles = activeFiles.map(f => {
          if (f.hash === payload.hash) {
            return { ...f, status: payload.status, category: payload.category };
          }
          return f;
        });
        fetchDbCategories();
      } catch (err) {
        console.error('Failed to parse confirmed SSE event', err);
      }
    });

    // Manual scan states
    eventSource.addEventListener('rescan.started', () => {
      showToast('info', 'Scan Running', 'Manual watch folder rescan is in progress...');
    });

    eventSource.addEventListener('rescan.completed', () => {
      showToast('success', 'Scan Completed', 'Watch folder rescan completed successfully.');
      fetchDashboard();
    });

    // Copy / Move progress update
    eventSource.addEventListener('file.move.progress', (e) => {
      try {
        const payload = JSON.parse(e.data);
        console.log(`Progress event: ${payload.fileName} -> ${payload.progress.toFixed(1)}%`);
        
        // Store in active progresses reactively
        activeProgresses = {
          ...activeProgresses,
          [payload.hash]: payload
        };

        // Update the file status to "Moving" (4) in our active file list
        activeFiles = activeFiles.map(f => {
          if (f.hash === payload.hash) {
            return { ...f, status: 4 };
          }
          return f;
        });
      } catch (err) {
        console.error('Failed to parse progress SSE event', err);
      }
    });

    // Move completed successfully
    eventSource.addEventListener('file.move.completed', (e) => {
      try {
        const payload = JSON.parse(e.data);
        console.log(`Move completed event: ${payload.fileName}`);
        showToast('success', 'Move Completed', `"${payload.fileName}" organized successfully to ${payload.targetPath}`);
        
        // Remove from dashboard list and progress tracker
        activeFiles = activeFiles.filter(f => f.hash !== payload.hash);
        
        const newProgresses = { ...activeProgresses };
        delete newProgresses[payload.hash];
        activeProgresses = newProgresses;

        // Refresh database lists
        fetchPending();
        fetchDbCategories();
        if (currentTab === 'history') {
          fetchHistory();
        }
      } catch (err) {
        console.error('Failed to parse completed SSE event', err);
      }
    });

    // Move error event
    eventSource.addEventListener('file.move.error', (e) => {
      try {
        const payload = JSON.parse(e.data);
        console.warn(`Move error event: ${payload.fileName} -> ${payload.error}`);
        showToast('error', 'Organization Failed', `Failed to move file "${payload.fileName}": ${payload.error}`);
        
        // Set error status in dashboard list and clear progress
        activeFiles = activeFiles.map(f => {
          if (f.hash === payload.hash) {
            return { ...f, status: 6, lastError: payload.error };
          }
          return f;
        });

        const newProgresses = { ...activeProgresses };
        delete newProgresses[payload.hash];
        activeProgresses = newProgresses;

        fetchPending();
      } catch (err) {
        console.error('Failed to parse error SSE event', err);
      }
    });

    return eventSource;
  }

  // --- Modal Helpers ---

  function openConfirmModal(file) {
    selectedFile = file;
    customCategory = file.suggestedCategory || '';
    showModal = true;
  }

  function closeConfirmModal() {
    showModal = false;
    selectedFile = null;
    dropdownOpen = false;
  }

  function selectCategory(cat) {
    customCategory = cat;
    dropdownOpen = false;
  }

  // Dynamic autocomplete categories from DB (fallback/merge with presets)
  let autocompleteCategories = $derived(
    Array.from(new Set([...dbCategories, ...presetCategories]))
      .filter(c => c.toLowerCase().includes(customCategory.toLowerCase()))
  );

  // --- Formatting Helpers ---

  function formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  function formatRelativeTime(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHr = Math.floor(diffMin / 60);

    if (diffSec < 60) return 'Just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    if (diffHr < 24) return `${diffHr}h ago`;
    return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  function getStatusDetails(status) {
    switch (status) {
      case 0: return { label: 'New', class: 'badge-new' };
      case 1: return { label: 'Processing', class: 'badge-processing' };
      case 2: return { label: 'Classified', class: 'badge-classified' };
      case 3: return { label: 'Ready', class: 'badge-ready' };
      case 4: return { label: 'Moving', class: 'badge-moving' };
      case 5: return { label: 'Moved', class: 'badge-moved' };
      case 6: return { label: 'Error', class: 'badge-error' };
      case 7: return { label: 'Retry', class: 'badge-processing' };
      case 8: return { label: 'Ignored', class: 'badge-ignored' };
      default: return { label: 'Unknown', class: 'badge-ignored' };
    }
  }

  // --- Pagination Controllers ---

  function nextHistoryPage() {
    if (historyHasMore) {
      historySkip += historyTake;
      fetchHistory();
    }
  }

  function prevHistoryPage() {
    if (historySkip >= historyTake) {
      historySkip -= historyTake;
      fetchHistory();
    }
  }

  // --- Initial Loading on Mount ---
  onMount(() => {
    fetchConfig();
    fetchDashboard();
    fetchPending();
    fetchDbCategories();
    
    const sse = connectSSE();

    return () => {
      sse.close();
    };
  });

  // Watch for tab change to load relative tab data
  $effect(() => {
    if (currentTab === 'history') {
      fetchHistory();
    } else if (currentTab === 'dashboard') {
      fetchDashboard();
      fetchPending();
    }
  });

</script>

<!-- Toast Notifications Container -->
<div class="notifications-container">
  {#each notifications as notif (notif.id)}
    <div class="toast glass-panel animate-slide-in {notif.type}">
      <div class="toast-header">
        <span class="toast-indicator"></span>
        <strong class="toast-title">{notif.title}</strong>
        <button class="toast-close" onclick={() => notifications = notifications.filter(n => n.id !== notif.id)}>×</button>
      </div>
      <div class="toast-body">{notif.message}</div>
    </div>
  {/each}
</div>

<div class="app-layout">
  <!-- Glowing background elements for visual premium styling -->
  <div class="bg-glow-1"></div>
  <div class="bg-glow-2"></div>

  <!-- Header Section -->
  <header class="glass-panel main-header">
    <div class="logo-area">
      <div class="logo-icon">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
        </svg>
      </div>
      <div class="logo-text">
        <h1>Media<span>Butler</span></h1>
        <p class="subtitle">AI-Powered Media Hub Organizer</p>
      </div>
    </div>

    <!-- SSE Connectivity Badge -->
    <div class="connection-status-wrapper">
      <span class="sse-indicator {sseConnected ? 'connected' : 'disconnected'}"></span>
      <span class="sse-text">{sseConnected ? 'SSE Stream Live' : 'Disconnected, retrying...'}</span>
    </div>

    <!-- Tabs Navigation -->
    <nav class="tabs-nav">
      <button class="tab-btn {currentTab === 'dashboard' ? 'active' : ''}" onclick={() => currentTab = 'dashboard'}>
        <svg class="tab-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2H6a2 2 0 01-2-2V6zm10 0a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2H6a2 2 0 01-2-2v-4zm10 0a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2h-2a2 2 0 01-2-2v-4z" /></svg>
        Dashboard
      </button>
      <button class="tab-btn {currentTab === 'history' ? 'active' : ''}" onclick={() => currentTab = 'history'}>
        <svg class="tab-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        History Log
      </button>
      <button class="tab-btn {currentTab === 'settings' ? 'active' : ''}" onclick={() => currentTab = 'settings'}>
        <svg class="tab-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" /><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /></svg>
        Settings
      </button>
    </nav>
  </header>

  <!-- Main Workspace -->
  <main class="workspace-content animate-slide-in">
    {#if currentTab === 'dashboard'}
      <!-- SUMMARY COUNTER CARDS -->
      <section class="summary-cards-section">
        <button type="button" class="glass-panel summary-card clickable-card cobalt-glow {statusFilter === 'new' ? 'active-filter' : ''}" onclick={() => toggleFilter('new')}>
          <div class="card-icon blue">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 13h6m-3-3v6m5 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
          </div>
          <div class="card-info">
            <h3>{activeFiles.filter(f => f.status === 0 || f.status === 7).length}</h3>
            <p>New Discovered</p>
          </div>
        </button>

        <button type="button" class="glass-panel summary-card clickable-card warning-glow {statusFilter === 'processing' ? 'active-filter' : ''}" onclick={() => toggleFilter('processing')}>
          <div class="card-icon amber">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.27 15" /></svg>
          </div>
          <div class="card-info">
            <h3>{activeFiles.filter(f => f.status === 1 || f.status === 4 || f.status === 3).length}</h3>
            <p>In Processing</p>
          </div>
        </button>

        <button type="button" class="glass-panel summary-card clickable-card purple-glow {statusFilter === 'pending' ? 'active-filter' : ''}" onclick={() => toggleFilter('pending')}>
          <div class="card-icon purple">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 .364l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" /></svg>
          </div>
          <div class="card-info">
            <h3>{activeFiles.filter(f => f.status === 2 || f.status === 6).length}</h3>
            <p>Pending Actions</p>
          </div>
        </button>

        <button type="button" class="glass-panel summary-card clickable-card emerald-glow {statusFilter === 'moved' ? 'active-filter' : ''}" onclick={() => toggleFilter('moved')}>
          <div class="card-icon green">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" /></svg>
          </div>
          <div class="card-info">
            <h3>{activeFiles.filter(f => f.status === 5).length}</h3>
            <p>Moved Successfully</p>
          </div>
        </button>
      </section>

      <!-- ACTIVE QUEUE WATCH FOLDER SECTION -->
      <section class="glass-panel active-queue-section">
        <div class="section-header-row">
          <div class="section-title-wrapper">
            <h2>Tracked Watch-Folder Files</h2>
            <p class="subtitle">Real-time status of files currently detected in active directories</p>
          </div>
          <button class="btn btn-secondary" onclick={fetchDashboard} disabled={isLoading}>
            {#if isLoading}
              <svg class="spinner" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
            {:else}
              <svg class="refresh-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 1121.27 15" /></svg>
            {/if}
            Refresh Queue
          </button>
        </div>

        {#if activeFiles.length === 0}
          <div class="empty-state-panel animate-slide-in">
            <div class="empty-icon">📂</div>
            <h3>No Active Files Found</h3>
            <p>Your watch folders are clean. Drop some media files in to trigger automated classification.</p>
          </div>
        {:else}
          <!-- Floating Selectable Bulk Action Header / Toolbar -->
          <div class="queue-toolbar glass-panel animate-slide-in">
            <div class="toolbar-left">
              <span class="queue-count">
                {#if statusFilter}
                  Showing {displayedFiles.length} of {activeFiles.length} files (Filtered)
                {:else}
                  Showing all {activeFiles.length} files
                {/if}
              </span>
              {#if selectedHashes.size > 0}
                <span class="selection-count">({selectedHashes.size} selected)</span>
              {/if}
            </div>
            <div class="toolbar-right">
              <button class="btn btn-secondary btn-sm" onclick={triggerRescan} title="Rescan Watch Folders manually to detect new network-share files">
                🔍 Rescan Folders
              </button>
              {#if selectedHashes.size > 0}
                <button class="btn btn-primary btn-sm action-confirm-btn" onclick={moveSelectedFiles}>
                  🚀 Organize Selected ({selectedHashes.size})
                </button>
              {/if}
            </div>
          </div>

          {#if displayedFiles.length === 0}
            <div class="empty-state-panel animate-slide-in">
              <div class="empty-icon">🔍</div>
              <h3>No Matching Files</h3>
              <p>No active files match the selected filter category.</p>
              <button class="btn btn-secondary btn-sm" onclick={() => statusFilter = null}>Clear Filter</button>
            </div>
          {:else}
            <!-- High-Density Expandable Table -->
            <div class="table-responsive files-list-container glass-panel animate-slide-in">
              <table class="files-list-table">
                <thead>
                  <tr>
                    <th class="col-select">
                      <input 
                        type="checkbox" 
                        class="custom-checkbox" 
                        checked={eligibleFiles.length > 0 && eligibleFiles.every(f => selectedHashes.has(f.hash))}
                        indeterminate={selectedHashes.size > 0 && !eligibleFiles.every(f => selectedHashes.has(f.hash))}
                        onclick={toggleSelectAll}
                        title="Select All Eligible (Classified/Error)"
                      />
                    </th>
                    <th class="col-expand"></th>
                    <th class="col-status">Status</th>
                    <th class="col-filename">File Name</th>
                    <th class="col-size">Size</th>
                    <th class="col-suggested">AI Suggested</th>
                    <th class="col-actions">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {#each displayedFiles as file (file.hash)}
                    {@const statusObj = getStatusDetails(file.status)}
                    {@const isExpanded = expandedHashes.has(file.hash)}
                    {@const isSelected = selectedHashes.has(file.hash)}
                    {@const isEligible = file.status === 3 || file.status === 6}
                    
                    <tr class="file-row-main {isExpanded ? 'row-expanded-border' : ''} {isSelected ? 'row-selected' : ''}">
                      <td class="col-select">
                        {#if isEligible}
                          <input 
                            type="checkbox" 
                            class="custom-checkbox" 
                            checked={isSelected}
                            onclick={(e) => { e.stopPropagation(); toggleSelectFile(file.hash); }}
                          />
                        {:else}
                          <input type="checkbox" class="custom-checkbox" disabled title="Only Classified or Error status files can be selected" />
                        {/if}
                      </td>
                      <td class="col-expand">
                        <button type="button" class="btn-icon expand-toggle-btn {isExpanded ? 'rotated' : ''}" onclick={() => toggleExpandRow(file.hash)} title="Toggle detail panel">
                          ▶
                        </button>
                      </td>
                      <td class="col-status">
                        <span class="badge {statusObj.class}">{statusObj.label}</span>
                      </td>
                      <td class="col-filename" onclick={() => toggleExpandRow(file.hash)}>
                        <span class="filename-text" title={file.fileName}>{file.fileName}</span>
                        <span class="time-subtext">{formatRelativeTime(file.lastUpdateDate)}</span>
                      </td>
                      <td class="col-size">{formatBytes(file.fileSize)}</td>
                      <td class="col-suggested">
                        {#if file.category || file.suggestedCategory}
                          <span class="suggestion-tag {file.category ? 'confirmed-tag' : ''}">
                            {file.category || file.suggestedCategory}
                            <span class="confidence-val">
                              {#if file.category}
                                Confirmed
                              {:else}
                                {(file.confidence * 100).toFixed(0)}%
                              {/if}
                            </span>
                          </span>
                        {:else}
                          <span class="no-suggestion">-</span>
                        {/if}
                      </td>
                      <td class="col-actions">
                        <div class="actions-cell-wrapper">
                          {#if file.status === 2}
                            <button class="btn btn-primary btn-sm fast-confirm-btn" onclick={(e) => { e.stopPropagation(); confirmFile(file.hash, file.suggestedCategory); }} title="Fast Confirm: Move instantly using AI category suggestion">
                              ✓ Fast Confirm
                            </button>
                            <button class="btn btn-secondary btn-sm" onclick={(e) => { e.stopPropagation(); openConfirmModal(file); }} title="Edit category before moving">
                              ✏️ Edit
                            </button>
                          {:else if file.status === 6}
                            <button class="btn btn-primary btn-sm retry-btn" onclick={(e) => { e.stopPropagation(); confirmFile(file.hash, file.category || file.suggestedCategory || 'MOVIES'); }} title="Retry organize file transfer">
                              ⟳ Retry
                            </button>
                            <button class="btn btn-secondary btn-sm" onclick={(e) => { e.stopPropagation(); openConfirmModal(file); }} title="Edit details & category">
                              ✏️ Edit
                            </button>
                          {:else if file.status === 0 || file.status === 7}
                            <button class="btn btn-secondary btn-sm classify-btn" onclick={(e) => { e.stopPropagation(); forceClassifyFile(file.hash); }} title="Force classification of this file">
                              🧠 Classify
                            </button>
                          {:else}
                            <span class="no-actions-badge">Running</span>
                          {/if}
                        </div>
                      </td>
                    </tr>

                    {#if isExpanded}
                      <tr class="file-row-details">
                        <td colspan="7">
                          <div class="details-pane-content glass-panel animate-slide-in">
                            <div class="details-grid">
                              <div class="details-block">
                                <span class="details-label">Original Location</span>
                                <code class="details-value path-style" title={file.originalPath}>{file.originalPath}</code>
                              </div>
                              
                              {#if file.targetPath}
                                <div class="details-block">
                                  <span class="details-label">Predicted Destination Target</span>
                                  <code class="details-value path-style" title={file.targetPath}>{file.targetPath}</code>
                                </div>
                              {/if}

                              <div class="details-meta-row">
                                <div class="meta-item">
                                  <span class="details-label">Date Discovered</span>
                                  <span class="details-value">{new Date(file.lastUpdateDate).toLocaleString()}</span>
                                </div>
                                <div class="meta-item">
                                  <span class="details-label">File MD5 Hash</span>
                                  <span class="details-value font-mono">{file.hash}</span>
                                </div>
                                <div class="meta-item">
                                  <span class="details-label">Confidence Score</span>
                                  <span class="details-value confidence-pct {file.confidence >= 0.8 ? 'high' : 'low'}">
                                    {(file.confidence * 100).toFixed(1)}%
                                  </span>
                                </div>
                              </div>

                              {#if activeProgresses[file.hash]}
                                <div class="progress-details-wrapper glass-panel">
                                  <div class="progress-info-row">
                                    <span>Transferring file bytes:</span>
                                    <strong>{activeProgresses[file.hash].progress.toFixed(1)}%</strong>
                                  </div>
                                  <div class="progress-bar-bg">
                                    <div class="progress-bar-value" style="width: {activeProgresses[file.hash].progress}%"></div>
                                  </div>
                                  <div class="progress-stats">
                                    <span>{formatBytes(activeProgresses[file.hash].bytesCopied)} / {formatBytes(activeProgresses[file.hash].totalBytes)}</span>
                                  </div>
                                </div>
                              {:else if file.status === 4 || file.status === 3}
                                <div class="progress-details-wrapper glass-panel">
                                  <div class="progress-info-row">
                                    <span>Queued for asynchronous transfer...</span>
                                    <strong>0%</strong>
                                  </div>
                                  <div class="progress-bar-bg">
                                    <div class="progress-bar-value moving-pulse" style="width: 15%"></div>
                                  </div>
                                </div>
                              {/if}

                              {#if file.status === 6 && file.lastError}
                                <div class="error-details-box">
                                  <strong>⚠️ Copy Error details:</strong>
                                  <p class="error-msg">{file.lastError}</p>
                                </div>
                              {/if}
                            </div>
                          </div>
                        </td>
                      </tr>
                    {/if}
                  {/each}
                </tbody>
              </table>
            </div>
          {/if}
        {/if}
      </section>

    {:else if currentTab === 'history'}
      <!-- HISTORY LOG SECTION -->
      <section class="glass-panel history-section">
        <div class="section-header-row">
          <div class="section-title-wrapper">
            <h2>Organized Media History</h2>
            <p class="subtitle">Complete archives of files organized into target structures</p>
          </div>
          
          <div class="pagination-header-info">
            Page {Math.floor(historySkip / historyTake) + 1}
          </div>
        </div>

        {#if historyFiles.length === 0}
          <div class="empty-state-panel">
            <div class="empty-icon">🕰️</div>
            <h3>No History Records</h3>
            <p>Files that are successfully organized will show up in this history log.</p>
          </div>
        {:else}
          <div class="table-responsive">
            <table class="history-table">
              <thead>
                <tr>
                  <th>Original FileName</th>
                  <th>Category</th>
                  <th>File Size</th>
                  <th>Destination Target Path</th>
                  <th>Date Organized</th>
                </tr>
              </thead>
              <tbody>
                {#each historyFiles as log (log.hash)}
                  <tr>
                    <td class="cell-filename" title={log.fileName}>
                      <strong>{log.fileName}</strong>
                    </td>
                    <td>
                      <span class="badge badge-moved">{log.category || 'N/A'}</span>
                    </td>
                    <td class="cell-size">{formatBytes(log.fileSize)}</td>
                    <td class="cell-path" title={log.movedToPath || log.targetPath}>
                      <span class="path-style">{log.movedToPath || log.targetPath || 'N/A'}</span>
                    </td>
                    <td class="cell-date">{formatRelativeTime(log.movedAt || log.lastUpdateDate)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </div>

          <!-- Pagination Controls -->
          <div class="pagination-footer">
            <button class="btn btn-secondary btn-sm" onclick={prevHistoryPage} disabled={historySkip === 0}>
              ← Previous
            </button>
            <span class="pagination-indicator">
              Showing logs {historySkip + 1} - {historySkip + historyFiles.length}
            </span>
            <button class="btn btn-secondary btn-sm" onclick={nextHistoryPage} disabled={!historyHasMore}>
              Next →
            </button>
          </div>
        {/if}
      </section>

    {:else if currentTab === 'settings'}
      <!-- SETTINGS PANEL -->
      <section class="glass-panel settings-section">
        <div class="section-header-row border-bottom">
          <div class="section-title-wrapper">
            <h2>System Settings & Preferences</h2>
            <p class="subtitle">Configuration loads directly from the backend system environment variables</p>
          </div>
        </div>

        {#if config}
          <div class="settings-grid">
            <div class="settings-card glass-panel">
              <h4>📁 File Directories Path</h4>
              
              <div class="settings-field">
                <span class="field-label">Watch Directories (comma-separated scan targets)</span>
                <div class="paths-tag-list">
                  {#each config.watchFolders as folder}
                    <code class="path-tag">{folder}</code>
                  {/each}
                </div>
              </div>

              <div class="settings-field">
                <span class="field-label">Destination Organized Directory</span>
                <code class="path-tag primary-border">{config.destFolder}</code>
              </div>
            </div>

            <div class="settings-card glass-panel">
              <h4>🧠 Classifier Engine Config</h4>
              
              <div class="settings-field">
                <span class="field-label">Naive Bayes Classification Threshold</span>
                <div class="slider-display">
                  <div class="slider-bar-bg">
                    <div class="slider-bar-value" style="width: {config.mlThreshold * 100}%"></div>
                  </div>
                  <strong class="slider-text">{(config.mlThreshold * 100).toFixed(0)}% Confidence</strong>
                </div>
                <p class="field-note">Files below this confidence threshold will prompt manual confirmation before organization.</p>
              </div>

              <div class="settings-field">
                <span class="field-label">Active SQLite Database Path</span>
                <code class="path-tag">{config.databasePath}</code>
              </div>

              <div class="settings-field" style="margin-top: 12px; padding-top: 16px; border-top: 1px solid rgba(255, 255, 255, 0.05);">
                <span class="field-label">Classifier Maintenance</span>
                <button class="btn btn-secondary" onclick={triggerRetrain} style="align-self: flex-start;">
                  🧠 Retrain Classifier Engine
                </button>
                <p class="field-note" style="margin-top: 4px;">Rebuilds Naive Bayes frequencies completely from all Moved (status 5) history records in the database.</p>
              </div>
            </div>
          </div>
        {:else}
          <div class="empty-state-panel">
            <svg class="spinner" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path></svg>
            <p>Loading System Configuration...</p>
          </div>
        {/if}
      </section>
    {/if}
  </main>
</div>

<!-- HTML5 Native Style Confirmation dialog / modal -->
{#if showModal && selectedFile}
  <div class="modal-backdrop" role="button" tabindex="-1" onclick={closeConfirmModal} onkeydown={(e) => e.key === 'Escape' && closeConfirmModal()}>
    <div class="modal-card glass-panel animate-slide-in" role="presentation" onclick={(e) => e.stopPropagation()}>

      <div class="modal-header">
        <div class="modal-title-wrapper">
          <h3>🧠 Category Classification Confirmation</h3>
          <p class="subtitle">Review the AI classifier results and confirm organization location</p>
        </div>
        <button class="modal-close" onclick={closeConfirmModal}>×</button>
      </div>

      <div class="modal-body">
        <div class="file-summary-box">
          <p class="box-filename" title={selectedFile.fileName}>{selectedFile.fileName}</p>
          <div class="box-grid">
            <div>
              <small>File Size</small>
              <strong>{formatBytes(selectedFile.fileSize)}</strong>
            </div>
            <div>
              <small>Original Directory</small>
              <strong class="path-text" title={selectedFile.originalPath}>{selectedFile.originalPath}</strong>
            </div>
          </div>
        </div>

        <!-- AI SUGGESTION BADGE -->
        <div class="ai-suggestion-box">
          <div class="ai-header">
            <span class="sparkle-icon">✨</span>
            <span>AI Suggested Destination Category</span>
          </div>
          <div class="suggestion-glowing-row">
            <span class="badge badge-classified badge-lg">
              {selectedFile.suggestedCategory || 'UNKNOWN'}
            </span>
            <div class="confidence-circle">
              <strong>{(selectedFile.confidence * 100).toFixed(0)}%</strong>
              <span>Confidence</span>
            </div>
          </div>
        </div>

        <!-- TARGET SELECTION COMBOBOX / AUTOCOMPLETE -->
        <div class="combobox-field-wrapper">
          <label class="combobox-label" for="category-combo">Target Destination Category</label>
          <div class="combobox-input-row">
            <div class="input-autocomplete-container">
              <input 
                id="category-combo"
                type="text" 
                placeholder="Type or select a category..." 
                bind:value={customCategory} 
                onfocus={() => dropdownOpen = true}
                class="combo-input"
                autocomplete="off"
              />
              
              {#if dropdownOpen}
                <ul class="autocomplete-dropdown glass-panel">
                  {#each autocompleteCategories as cat}
                    <li>
                      <button type="button" class="dropdown-item-btn" onclick={() => selectCategory(cat)}>
                        {cat}
                      </button>
                    </li>
                  {/each}
                  {#if autocompleteCategories.length === 0}
                    <li class="dropdown-no-match">
                      Press "Confirm" to create a new category: "<strong>{customCategory.toUpperCase()}</strong>"
                    </li>
                  {/if}
                </ul>
              {/if}
            </div>

            <!-- Toggle Arrow button -->
            <button 
              type="button" 
              class="combo-dropdown-arrow" 
              onclick={() => dropdownOpen = !dropdownOpen}
            >
              ▼
            </button>
          </div>

          <!-- Quick presets list -->
          <div class="presets-quick-selection">
            <span class="preset-title">Quick Presets:</span>
            <div class="preset-buttons">
              {#each presetCategories as cat}
                <button 
                  type="button" 
                  class="preset-btn {customCategory === cat ? 'active' : ''}" 
                  onclick={() => selectCategory(cat)}
                >
                  {cat}
                </button>
              {/each}
            </div>
          </div>
        </div>
      </div>

      <div class="modal-footer">
        <button class="btn btn-secondary" onclick={() => ignoreFile(selectedFile.hash)}>
          ❌ Ignora
        </button>
        <div class="footer-primary-actions">
          <button class="btn btn-secondary" onclick={closeConfirmModal}>Cancel</button>
          <button class="btn btn-primary" onclick={() => confirmFile(selectedFile.hash, customCategory)} disabled={isSubmitting || !customCategory}>
            {#if isSubmitting}
              Confirming...
            {:else}
              ✅ Conferma
            {/if}
          </button>
        </div>
      </div>
    </div>
  </div>
{/if}

<style>
  /* --- Page Layout & Styles --- */
  .app-layout {
    width: 100%;
    max-width: 1200px;
    margin: 0 auto;
    padding: 32px 16px;
    min-height: 100vh;
    display: flex;
    flex-direction: column;
    gap: 24px;
    position: relative;
    z-index: 1;
  }

  /* Decorative Glow Backdrops */
  .bg-glow-1, .bg-glow-2 {
    position: fixed;
    width: 400px;
    height: 400px;
    border-radius: 50%;
    filter: blur(100px);
    opacity: 0.15;
    z-index: 0;
    pointer-events: none;
  }
  .bg-glow-1 {
    top: 10%;
    left: -100px;
    background: hsl(var(--border-glow));
  }
  .bg-glow-2 {
    bottom: 10%;
    right: -100px;
    background: hsl(var(--accent-blue));
  }

  /* Header Premium Look */
  .main-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 24px 32px;
    gap: 20px;
    flex-wrap: wrap;
  }

  .logo-area {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  .logo-icon {
    width: 44px;
    height: 44px;
    border-radius: 12px;
    background: linear-gradient(135deg, hsl(var(--accent-purple)), hsl(var(--accent-blue)));
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    box-shadow: 0 4px 15px -3px hsla(265, 100%, 65%, 0.5);
  }
  .logo-icon svg {
    width: 24px;
    height: 24px;
  }

  .logo-text h1 {
    font-size: 1.5rem;
    font-weight: 700;
    line-height: 1;
    letter-spacing: -0.03em;
    color: white;
  }
  .logo-text h1 span {
    background: linear-gradient(to right, hsl(var(--accent-purple)), #d8b4fe);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
  }
  .logo-text .subtitle {
    font-size: 0.75rem;
    color: hsl(var(--text-muted));
    font-weight: 500;
    letter-spacing: 0.05em;
    text-transform: uppercase;
    margin-top: 4px;
  }

  .connection-status-wrapper {
    display: flex;
    align-items: center;
    gap: 8px;
    background: rgba(255, 255, 255, 0.03);
    padding: 6px 12px;
    border-radius: 9999px;
    border: 1px solid rgba(255, 255, 255, 0.05);
    font-size: 0.8rem;
    font-weight: 500;
  }

  .sse-indicator {
    width: 8px;
    height: 8px;
    border-radius: 50%;
  }
  .sse-indicator.connected {
    background: #10b981;
    box-shadow: 0 0 10px #10b981;
    animation: pulse-glow-green 2s infinite;
  }
  .sse-indicator.disconnected {
    background: #ef4444;
    box-shadow: 0 0 10px #ef4444;
  }

  @keyframes pulse-glow-green {
    0%, 100% { box-shadow: 0 0 6px #10b981; opacity: 0.8; }
    50% { box-shadow: 0 0 14px #10b981; opacity: 1; }
  }

  .tabs-nav {
    display: flex;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.06);
    border-radius: 10px;
    padding: 4px;
    gap: 4px;
  }

  .tab-btn {
    display: flex;
    align-items: center;
    gap: 8px;
    background: transparent;
    border: none;
    color: hsl(var(--text-secondary));
    padding: 8px 16px;
    border-radius: 8px;
    font-weight: 500;
    font-size: 0.875rem;
    cursor: pointer;
    transition: all 0.2s ease;
  }
  .tab-btn:hover {
    color: white;
    background: rgba(255, 255, 255, 0.04);
  }
  .tab-btn.active {
    color: white;
    background: rgba(255, 255, 255, 0.08);
    box-shadow: var(--shadow-sm);
  }
  .tab-icon {
    width: 16px;
    height: 16px;
  }

  /* Workspace Content */
  .workspace-content {
    display: flex;
    flex-direction: column;
    gap: 24px;
  }

  /* Summary Cards */
  .summary-cards-section {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 16px;
  }

  .summary-card {
    display: flex;
    align-items: center;
    padding: 20px 24px;
    gap: 16px;
  }

  .card-icon {
    width: 48px;
    height: 48px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
  }
  .card-icon svg {
    width: 24px;
    height: 24px;
  }
  .card-icon.blue { background: rgba(14, 165, 233, 0.1); color: #0ea5e9; }
  .card-icon.amber { background: rgba(245, 158, 11, 0.1); color: #f59e0b; }
  .card-icon.purple { background: rgba(168, 85, 247, 0.1); color: #a855f7; }
  .card-icon.green { background: rgba(16, 185, 129, 0.1); color: #10b981; }

  /* Glowing hover shadows on summary cards */
  .cobalt-glow:hover { box-shadow: 0 10px 25px -5px rgba(14, 165, 233, 0.15); border-color: rgba(14, 165, 233, 0.3); }
  .warning-glow:hover { box-shadow: 0 10px 25px -5px rgba(245, 158, 11, 0.15); border-color: rgba(245, 158, 11, 0.3); }
  .purple-glow:hover { box-shadow: 0 10px 25px -5px rgba(168, 85, 247, 0.15); border-color: rgba(168, 85, 247, 0.3); }
  .emerald-glow:hover { box-shadow: 0 10px 25px -5px rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.3); }

  .card-info h3 {
    font-size: 1.75rem;
    font-weight: 700;
    line-height: 1.1;
    color: white;
  }
  .card-info p {
    font-size: 0.8rem;
    color: hsl(var(--text-muted));
    font-weight: 500;
    margin-top: 2px;
  }

  /* Active Queue / Section Header */
  .active-queue-section {
    padding: 32px;
  }

  .section-header-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 24px;
    flex-wrap: wrap;
  }
  .section-header-row.border-bottom {
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    padding-bottom: 20px;
  }

  .section-title-wrapper h2 {
    font-size: 1.25rem;
    font-weight: 600;
    color: white;
  }
  .section-title-wrapper .subtitle {
    font-size: 0.875rem;
    color: hsl(var(--text-secondary));
    margin-top: 4px;
  }

  .refresh-icon {
    width: 14px;
    height: 14px;
  }
  .spinner {
    width: 14px;
    height: 14px;
    animation: spin-slow 1s linear infinite;
  }

  /* Empty State styling */
  .empty-state-panel {
    text-align: center;
    padding: 60px 20px;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 12px;
  }
  .empty-icon {
    font-size: 3rem;
  }
  .empty-state-panel h3 {
    font-size: 1.1rem;
    font-weight: 600;
    color: white;
  }
  .empty-state-panel p {
    color: hsl(var(--text-secondary));
    font-size: 0.9rem;
    max-width: 400px;
  }

  /* Active Files Grid */
  .files-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
    gap: 20px;
  }

  .file-card {
    display: flex;
    flex-direction: column;
    padding: 20px;
    border-radius: 12px;
    background: rgba(255, 255, 255, 0.02);
    min-height: 220px;
  }

  .file-card.pending-action-border {
    border-color: rgba(168, 85, 247, 0.35);
    box-shadow: 0 0 15px -5px rgba(168, 85, 247, 0.2);
    animation: pulse-glow-purple-border 4s infinite alternate;
  }

  @keyframes pulse-glow-purple-border {
    0% { border-color: rgba(168, 85, 247, 0.2); }
    100% { border-color: rgba(168, 85, 247, 0.55); }
  }

  .file-card-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 16px;
  }
  .file-time {
    font-size: 0.75rem;
    color: hsl(var(--text-muted));
    font-weight: 500;
  }

  .file-card-body {
    flex-grow: 1;
    display: flex;
    flex-direction: column;
    gap: 12px;
  }

  .file-title-text {
    font-size: 0.95rem;
    font-weight: 600;
    color: white;
    word-break: break-all;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    line-height: 1.35;
  }

  .file-paths-details {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .path-item {
    font-size: 0.75rem;
    color: hsl(var(--text-secondary));
    display: flex;
    gap: 4px;
    align-items: baseline;
  }
  .path-item strong {
    color: hsl(var(--text-muted));
    flex-shrink: 0;
  }
  .path-text {
    font-family: var(--font-mono);
    word-break: break-all;
    opacity: 0.85;
    background: rgba(255, 255, 255, 0.02);
    padding: 1px 4px;
    border-radius: 4px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    display: block;
    max-width: 100%;
  }

  .file-card-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-top: 16px;
    border-top: 1px solid rgba(255, 255, 255, 0.05);
    padding-top: 12px;
  }

  .file-size-badge {
    font-family: var(--font-mono);
    font-size: 0.75rem;
    font-weight: 500;
    color: hsl(var(--text-secondary));
    background: rgba(255, 255, 255, 0.05);
    padding: 2px 6px;
    border-radius: 4px;
  }

  .btn-sm {
    padding: 6px 12px;
    font-size: 0.8rem;
    border-radius: 6px;
  }

  .action-confirm-btn {
    animation: animate-pulse 2.5s infinite;
  }

  .btn-icon-svg {
    width: 14px;
    height: 14px;
  }

  @keyframes animate-pulse {
    0%, 100% { transform: scale(1); box-shadow: 0 4px 10px rgba(168, 85, 247, 0.3); }
    50% { transform: scale(1.03); box-shadow: 0 4px 18px rgba(168, 85, 247, 0.5); }
  }

  /* SSE Progress Indicators */
  .progress-container {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin-top: 4px;
    background: rgba(255, 255, 255, 0.02);
    padding: 10px;
    border-radius: 8px;
    border: 1px solid rgba(255, 255, 255, 0.04);
  }

  .progress-details-text {
    display: flex;
    justify-content: space-between;
    font-size: 0.75rem;
  }
  .progress-details-text span {
    color: hsl(var(--text-secondary));
  }
  .progress-details-text strong {
    color: hsl(var(--accent-blue));
  }

  .progress-bar-bg {
    width: 100%;
    height: 6px;
    background: rgba(255, 255, 255, 0.05);
    border-radius: 999px;
    overflow: hidden;
  }

  .progress-bar-value {
    height: 100%;
    background: linear-gradient(to right, hsl(var(--accent-blue)), hsl(var(--accent-purple)));
    border-radius: 999px;
    transition: width 0.3s ease;
  }

  .moving-pulse {
    animation: bar-loading-pulse 1.5s infinite linear;
  }

  @keyframes bar-loading-pulse {
    0% { transform: translateX(-100%); }
    100% { transform: translateX(400%); }
  }

  .bytes-copied-indicator {
    font-family: var(--font-mono);
    font-size: 0.65rem;
    color: hsl(var(--text-muted));
    text-align: right;
  }

  .error-panel {
    background: rgba(239, 68, 68, 0.08);
    border: 1px solid rgba(239, 68, 68, 0.15);
    border-radius: 6px;
    padding: 8px;
    font-size: 0.75rem;
    color: #f87171;
    word-break: break-all;
  }

  /* History Table Style */
  .history-section {
    padding: 32px;
  }

  .table-responsive {
    width: 100%;
    overflow-x: auto;
    border-radius: 8px;
    border: 1px solid rgba(255, 255, 255, 0.05);
  }

  .history-table {
    width: 100%;
    border-collapse: collapse;
    text-align: left;
    font-size: 0.875rem;
  }

  .history-table th {
    background: rgba(255, 255, 255, 0.02);
    color: hsl(var(--text-secondary));
    font-weight: 600;
    padding: 14px 16px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    letter-spacing: 0.03em;
    font-size: 0.75rem;
    text-transform: uppercase;
  }

  .history-table td {
    padding: 16px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.03);
    color: white;
    vertical-align: middle;
  }
  .history-table tbody tr:hover {
    background: rgba(255, 255, 255, 0.01);
  }

  .cell-filename {
    font-weight: 500;
    max-width: 250px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .cell-size {
    font-family: var(--font-mono);
    color: hsl(var(--text-secondary));
    font-size: 0.8rem;
  }

  .cell-path {
    max-width: 320px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .path-style {
    font-family: var(--font-mono);
    font-size: 0.8rem;
    opacity: 0.75;
    background: rgba(255, 255, 255, 0.02);
    padding: 2px 6px;
    border-radius: 4px;
    border: 1px solid rgba(255, 255, 255, 0.04);
  }

  .cell-date {
    color: hsl(var(--text-secondary));
    font-size: 0.8rem;
  }

  .pagination-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-top: 20px;
    gap: 16px;
  }

  .pagination-indicator {
    font-size: 0.8rem;
    color: hsl(var(--text-muted));
    font-weight: 500;
  }

  .pagination-header-info {
    font-size: 0.85rem;
    color: hsl(var(--text-secondary));
    background: rgba(255, 255, 255, 0.05);
    padding: 4px 12px;
    border-radius: 6px;
    font-weight: 500;
  }

  /* Settings Styling */
  .settings-section {
    padding: 32px;
  }

  .settings-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    gap: 24px;
    margin-top: 8px;
  }

  .settings-card {
    padding: 24px;
    display: flex;
    flex-direction: column;
    gap: 20px;
  }
  .settings-card h4 {
    color: white;
    font-size: 1.05rem;
    font-weight: 600;
    margin-bottom: 4px;
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .settings-field {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .field-label {
    font-size: 0.8rem;
    color: hsl(var(--text-muted));
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .paths-tag-list {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  .path-tag {
    font-family: var(--font-mono);
    font-size: 0.85rem;
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.05);
    padding: 8px 12px;
    border-radius: 8px;
    color: white;
    word-break: break-all;
  }
  .path-tag.primary-border {
    border-color: rgba(14, 165, 233, 0.2);
    background: rgba(14, 165, 233, 0.02);
  }

  .slider-display {
    display: flex;
    align-items: center;
    gap: 16px;
    background: rgba(255, 255, 255, 0.02);
    border: 1px solid rgba(255, 255, 255, 0.04);
    padding: 12px;
    border-radius: 8px;
  }

  .slider-bar-bg {
    flex-grow: 1;
    height: 8px;
    background: rgba(255, 255, 255, 0.05);
    border-radius: 999px;
    overflow: hidden;
  }
  .slider-bar-value {
    height: 100%;
    background: linear-gradient(to right, hsl(var(--accent-blue)), hsl(var(--accent-purple)));
  }

  .slider-text {
    font-size: 0.85rem;
    color: hsl(var(--accent-purple));
    flex-shrink: 0;
  }

  .field-note {
    font-size: 0.75rem;
    color: hsl(var(--text-muted));
    line-height: 1.4;
  }

  /* Toast Notification Popups */
  .notifications-container {
    position: fixed;
    top: 24px;
    right: 24px;
    z-index: 1000;
    display: flex;
    flex-direction: column;
    gap: 12px;
    width: 320px;
    max-width: 100%;
  }

  .toast {
    padding: 14px 18px;
    display: flex;
    flex-direction: column;
    gap: 6px;
    background: rgba(20, 22, 30, 0.8);
    border-left: 4px solid #a855f7;
  }

  .toast.success { border-left-color: #10b981; }
  .toast.error { border-left-color: #ef4444; }
  .toast.info { border-left-color: #3b82f6; }

  .toast-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
  }

  .toast-indicator {
    width: 6px;
    height: 6px;
    border-radius: 50%;
  }
  .toast.success .toast-indicator { background: #10b981; }
  .toast.error .toast-indicator { background: #ef4444; }
  .toast.info .toast-indicator { background: #3b82f6; }

  .toast-title {
    font-size: 0.85rem;
    font-weight: 600;
    color: white;
    flex-grow: 1;
  }

  .toast-close {
    background: transparent;
    border: none;
    color: hsl(var(--text-muted));
    font-size: 1.1rem;
    cursor: pointer;
    line-height: 1;
  }
  .toast-close:hover {
    color: white;
  }

  .toast-body {
    font-size: 0.75rem;
    color: hsl(var(--text-secondary));
    line-height: 1.4;
  }

  /* --- MODAL CONFIRMATION DIALOG --- */
  .modal-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(6, 8, 12, 0.75);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    z-index: 999;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 20px;
  }

  .modal-card {
    width: 100%;
    max-width: 520px;
    max-height: 90vh;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 24px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  }

  .modal-title-wrapper h3 {
    font-size: 1.1rem;
    font-weight: 600;
    color: white;
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .modal-title-wrapper .subtitle {
    font-size: 0.75rem;
    color: hsl(var(--text-secondary));
    margin-top: 4px;
  }

  .modal-close {
    background: transparent;
    border: none;
    color: hsl(var(--text-secondary));
    font-size: 1.5rem;
    cursor: pointer;
  }
  .modal-close:hover {
    color: white;
  }

  .modal-body {
    padding: 24px;
    display: flex;
    flex-direction: column;
    gap: 20px;
  }

  .file-summary-box {
    background: rgba(255, 255, 255, 0.02);
    border: 1px solid rgba(255, 255, 255, 0.04);
    border-radius: 8px;
    padding: 14px;
  }

  .box-filename {
    font-size: 0.9rem;
    font-weight: 600;
    color: white;
    word-break: break-all;
    margin-bottom: 8px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    padding-bottom: 6px;
  }

  .box-grid {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 16px;
    font-size: 0.75rem;
  }
  .box-grid small {
    color: hsl(var(--text-muted));
    display: block;
    text-transform: uppercase;
    font-weight: 600;
    letter-spacing: 0.05em;
  }
  .box-grid strong {
    color: white;
    font-family: var(--font-sans);
  }
  .box-grid .path-text {
    max-width: 250px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    display: inline-block;
  }

  /* AI Suggestion Area */
  .ai-suggestion-box {
    background: rgba(168, 85, 247, 0.04);
    border: 1px solid rgba(168, 85, 247, 0.15);
    border-radius: 10px;
    padding: 16px;
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .ai-header {
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: #c084fc;
  }

  .suggestion-glowing-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
  }

  .badge-lg {
    padding: 8px 18px;
    font-size: 1rem;
    box-shadow: 0 0 15px -3px rgba(168, 85, 247, 0.3);
  }

  .confidence-circle {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    text-align: right;
  }
  .confidence-circle strong {
    font-size: 1.35rem;
    font-weight: 700;
    color: #10b981;
    line-height: 1.1;
  }
  .confidence-circle span {
    font-size: 0.65rem;
    color: hsl(var(--text-muted));
    font-weight: 600;
    text-transform: uppercase;
  }

  /* Autocomplete & Select styling */
  .combobox-field-wrapper {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .combobox-label {
    font-size: 0.8rem;
    font-weight: 600;
    color: hsl(var(--text-secondary));
  }

  .combobox-input-row {
    display: flex;
    position: relative;
    border: 1px solid rgba(255, 255, 255, 0.08);
    background: rgba(255, 255, 255, 0.02);
    border-radius: 8px;
    overflow: visible;
  }

  .input-autocomplete-container {
    flex-grow: 1;
    position: relative;
  }

  .combo-input {
    width: 100%;
    background: transparent;
    border: none;
    padding: 10px 14px;
    font-size: 0.9rem;
    color: white;
    outline: none;
  }

  .combo-dropdown-arrow {
    background: transparent;
    border: none;
    border-left: 1px solid rgba(255, 255, 255, 0.08);
    padding: 0 12px;
    color: hsl(var(--text-muted));
    cursor: pointer;
    font-size: 0.65rem;
  }
  .combo-dropdown-arrow:hover {
    color: white;
    background: rgba(255, 255, 255, 0.02);
  }

  .autocomplete-dropdown {
    position: absolute;
    top: calc(100% + 4px);
    left: 0;
    right: -42px; /* cover arrow also */
    max-height: 180px;
    overflow-y: auto;
    z-index: 1000;
    list-style: none;
    padding: 4px;
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .dropdown-item-btn {
    width: 100%;
    text-align: left;
    background: transparent;
    border: none;
    padding: 8px 12px;
    border-radius: 6px;
    color: white;
    font-size: 0.85rem;
    cursor: pointer;
    transition: background 0.15s ease;
  }
  .dropdown-item-btn:hover {
    background: rgba(255, 255, 255, 0.08);
  }

  .dropdown-no-match {
    padding: 8px 12px;
    color: hsl(var(--text-secondary));
    font-size: 0.75rem;
    line-height: 1.4;
  }

  .presets-quick-selection {
    display: flex;
    flex-direction: column;
    gap: 6px;
    margin-top: 8px;
  }
  .preset-title {
    font-size: 0.7rem;
    color: hsl(var(--text-muted));
    font-weight: 600;
    text-transform: uppercase;
  }

  .preset-buttons {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }

  .preset-btn {
    background: rgba(255, 255, 255, 0.03);
    border: 1px solid rgba(255, 255, 255, 0.05);
    color: hsl(var(--text-secondary));
    padding: 4px 10px;
    border-radius: 6px;
    font-size: 0.75rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s ease;
  }
  .preset-btn:hover {
    color: white;
    background: rgba(255, 255, 255, 0.06);
    border-color: rgba(255, 255, 255, 0.1);
  }
  .preset-btn.active {
    color: white;
    background: rgba(168, 85, 247, 0.15);
    border-color: rgba(168, 85, 247, 0.4);
  }

  .modal-footer {
    padding: 18px 24px;
    border-top: 1px solid rgba(255, 255, 255, 0.05);
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    flex-wrap: wrap;
  }

  .footer-primary-actions {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  /* --- Clickable summary cards --- */
  .clickable-card {
    border: 1px solid rgba(255, 255, 255, 0.05);
    cursor: pointer;
    background: rgba(20, 22, 30, 0.65);
    text-align: left;
    outline: none;
    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  }
  .clickable-card:focus-visible {
    border-color: hsl(var(--border-glow));
    box-shadow: 0 0 0 2px hsla(265, 100%, 65%, 0.4);
  }
  .clickable-card.active-filter {
    border-color: hsl(var(--border-glow));
    background: rgba(168, 85, 247, 0.08);
    box-shadow: 0 0 15px -3px hsla(265, 100%, 65%, 0.2);
  }

  /* --- Queue Toolbar --- */
  .queue-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 14px 24px;
    margin-bottom: 16px;
    gap: 16px;
    flex-wrap: wrap;
    background: rgba(25, 27, 37, 0.4);
    border-radius: 12px;
  }
  .toolbar-left {
    display: flex;
    align-items: center;
    gap: 10px;
  }
  .queue-count {
    font-size: 0.9rem;
    font-weight: 600;
    color: white;
  }
  .selection-count {
    font-size: 0.8rem;
    font-weight: 500;
    color: hsl(var(--accent-purple));
    background: rgba(168, 85, 247, 0.1);
    padding: 2px 8px;
    border-radius: 4px;
  }
  .toolbar-right {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  /* --- High-Density Files Expandable List Table --- */
  .files-list-container {
    padding: 0;
    overflow-x: auto;
    border-radius: 14px;
    border: 1px solid rgba(255, 255, 255, 0.05);
    background: rgba(20, 22, 30, 0.45);
  }
  .files-list-table {
    width: 100%;
    border-collapse: collapse;
    text-align: left;
    font-size: 0.85rem;
  }
  .files-list-table th {
    background: rgba(255, 255, 255, 0.02);
    color: hsl(var(--text-secondary));
    font-weight: 600;
    padding: 12px 16px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }
  
  .col-select { width: 44px; text-align: center; padding: 0 8px; }
  .col-expand { width: 40px; text-align: center; }
  .col-status { width: 110px; }
  .col-filename { width: auto; }
  .col-size { width: 90px; }
  .col-suggested { width: 160px; }
  .col-actions { width: 190px; text-align: right; }

  .files-list-table td {
    padding: 10px 16px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.02);
    vertical-align: middle;
  }

  /* Custom Checkbox */
  .custom-checkbox {
    width: 16px;
    height: 16px;
    accent-color: hsl(var(--accent-purple));
    cursor: pointer;
    border-radius: 4px;
    border: 1.5px solid rgba(255, 255, 255, 0.3);
  }

  /* Main Interactive Row */
  .file-row-main {
    transition: background 0.15s ease;
  }
  .file-row-main:hover {
    background: rgba(255, 255, 255, 0.02);
  }
  .file-row-main.row-selected {
    background: rgba(168, 85, 247, 0.03);
  }
  .file-row-main.row-expanded-border td {
    border-bottom-color: transparent;
  }

  /* Expand toggle arrow */
  .btn-icon {
    background: transparent;
    border: none;
    color: hsl(var(--text-muted));
    cursor: pointer;
    font-size: 0.75rem;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 24px;
    height: 24px;
    border-radius: 50%;
    transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1), color 0.2s;
  }
  .btn-icon:hover {
    color: white;
    background: rgba(255, 255, 255, 0.05);
  }
  .btn-icon.rotated {
    transform: rotate(90deg);
    color: hsl(var(--accent-blue));
  }

  /* Text & badges within rows */
  .filename-text {
    font-weight: 500;
    color: white;
    word-break: break-all;
    display: block;
    line-height: 1.3;
    max-width: 480px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    cursor: pointer;
  }
  .filename-text:hover {
    color: hsl(var(--accent-blue));
  }
  .time-subtext {
    font-size: 0.7rem;
    color: hsl(var(--text-muted));
    display: block;
    margin-top: 2px;
  }

  .suggestion-tag {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    font-size: 0.75rem;
    font-weight: 600;
    color: #c084fc;
    background: rgba(168, 85, 247, 0.08);
    border: 1px solid rgba(168, 85, 247, 0.15);
    padding: 2px 8px;
    border-radius: 6px;
  }
  .suggestion-tag.confirmed-tag {
    color: #34d399;
    background: rgba(16, 185, 129, 0.08);
    border-color: rgba(16, 185, 129, 0.2);
  }
  .suggestion-tag.confirmed-tag .confidence-val {
    background: rgba(16, 185, 129, 0.15);
  }
  .confidence-val {
    font-size: 0.65rem;
    opacity: 0.75;
    background: rgba(168, 85, 247, 0.15);
    padding: 1px 4px;
    border-radius: 4px;
  }
  .no-suggestion {
    color: hsl(var(--text-muted));
    font-style: italic;
  }

  .actions-cell-wrapper {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: 6px;
  }
  .fast-confirm-btn {
    background: rgba(16, 185, 129, 0.1) !important;
    color: #34d399 !important;
    border: 1px solid rgba(16, 185, 129, 0.2) !important;
    box-shadow: none !important;
  }
  .fast-confirm-btn:hover {
    background: rgba(16, 185, 129, 0.25) !important;
    border-color: rgba(16, 185, 129, 0.4) !important;
  }
  .retry-btn {
    background: rgba(14, 165, 233, 0.1) !important;
    color: #38bdf8 !important;
    border: 1px solid rgba(14, 165, 233, 0.2) !important;
    box-shadow: none !important;
  }
  .retry-btn:hover {
    background: rgba(14, 165, 233, 0.25) !important;
    border-color: rgba(14, 165, 233, 0.4) !important;
  }
  .no-actions-badge {
    font-size: 0.75rem;
    color: hsl(var(--text-muted));
    font-style: italic;
  }

  /* --- Details Pane Expansion Row --- */
  .file-row-details td {
    padding: 0 16px 10px 16px !important;
    background: rgba(25, 27, 37, 0.15);
  }
  .details-pane-content {
    border-radius: 10px;
    padding: 16px 20px;
    border: 1px solid rgba(255, 255, 255, 0.04);
    background: rgba(20, 22, 30, 0.8);
    box-shadow: inset 0 2px 8px rgba(0, 0, 0, 0.4);
  }
  .details-grid {
    display: flex;
    flex-direction: column;
    gap: 12px;
  }
  .details-block {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }
  .details-label {
    font-size: 0.7rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: hsl(var(--text-muted));
  }
  .details-value {
    font-size: 0.8rem;
    color: white;
    word-break: break-all;
  }
  .details-value.path-style {
    font-family: var(--font-mono);
    padding: 4px 8px;
    background: rgba(255, 255, 255, 0.02);
    border: 1px solid rgba(255, 255, 255, 0.04);
    border-radius: 6px;
    display: block;
  }

  .details-meta-row {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    gap: 16px;
    padding-top: 8px;
    border-top: 1px solid rgba(255, 255, 255, 0.04);
  }
  .meta-item {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }
  .confidence-pct {
    font-weight: 700;
  }
  .confidence-pct.high { color: #10b981; }
  .confidence-pct.low { color: #f59e0b; }

  /* Progress details */
  .progress-details-wrapper {
    margin-top: 4px;
    padding: 12px 16px;
    border-radius: 8px;
    border-color: rgba(14, 165, 233, 0.1);
    background: rgba(14, 165, 233, 0.02);
  }
  .progress-info-row {
    display: flex;
    justify-content: space-between;
    font-size: 0.75rem;
    margin-bottom: 6px;
  }
  .progress-stats {
    font-family: var(--font-mono);
    font-size: 0.65rem;
    color: hsl(var(--text-muted));
    text-align: right;
    margin-top: 4px;
  }

  /* Error box */
  .error-details-box {
    margin-top: 4px;
    padding: 10px 14px;
    border-radius: 8px;
    border: 1px solid rgba(239, 68, 68, 0.2);
    background: rgba(239, 68, 68, 0.04);
    color: #f87171;
    font-size: 0.75rem;
  }
  .error-msg {
    font-family: var(--font-mono);
    margin-top: 4px;
    word-break: break-all;
    opacity: 0.9;
  }
</style>
