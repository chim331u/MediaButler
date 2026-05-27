<script>
  import DirectoryTree from './DirectoryTree.svelte';
  // Svelte 5 Props
  let { path, label, showHidden = false } = $props();

  // Local State Runes
  let expanded = $state(false);
  let children = $state([]);
  let isLoading = $state(false);
  let error = $state(null);

  // Helper to format bytes
  function formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  // Fetch children lazy or when showHidden changes and it is expanded
  async function loadContents() {
    isLoading = true;
    error = null;
    try {
      const res = await fetch(`/api/fs/list?path=${encodeURIComponent(path)}&showHidden=${showHidden}`);
      if (res.ok) {
        children = await res.json();
      } else {
        const data = await res.json();
        error = data.error || 'Failed to load directory';
      }
    } catch (err) {
      error = 'Connection error';
    } finally {
      isLoading = false;
    }
  }

  function handleToggle(e) {
    e.stopPropagation();
    expanded = !expanded;
  }

  // Reactive effect when showHidden or expanded changes
  $effect(() => {
    if (expanded) {
      loadContents();
    }
  });
</script>

<div class="tree-node">
  <!-- Node Header (Toggle Folder or Info File) -->
  <button 
    type="button" 
    class="node-header" 
    onclick={handleToggle}
    aria-expanded={expanded}
  >
    <span class="node-arrow {expanded ? 'rotated' : ''}">
      ▶
    </span>
    <span class="node-icon">📁</span>
    <span class="node-label" title={path}>{label}</span>
    
    {#if isLoading}
      <span class="node-loader"></span>
    {/if}
  </button>

  {#if error}
    <div class="node-error">{error}</div>
  {/if}

  <!-- Children List -->
  {#if expanded && children.length > 0}
    <div class="node-children">
      {#each children as child}
        {#if child.isDir}
          <DirectoryTree path={child.path} label={child.name} showHidden={showHidden} />
        {:else}
          <div class="node-file">
            <span class="node-file-icon">📄</span>
            <span class="node-file-label" title={child.path}>{child.name}</span>
            <span class="node-file-size">({formatBytes(child.sizeBytes)})</span>
          </div>
        {/if}
      {/each}
    </div>
  {:else if expanded && !isLoading && children.length === 0 && !error}
    <div class="node-children empty">
      <span class="node-empty-text">Empty directory</span>
    </div>
  {/if}
</div>

<style>
  .tree-node {
    display: flex;
    flex-direction: column;
    width: 100%;
    font-family: inherit;
    font-size: 0.9rem;
    color: rgba(255, 255, 255, 0.85);
  }

  .node-header {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 8px;
    background: transparent;
    border: none;
    color: inherit;
    text-align: left;
    cursor: pointer;
    border-radius: 4px;
    transition: background 0.2s, color 0.2s;
    width: fit-content;
    max-width: 100%;
    outline: none;
  }

  .node-header:hover {
    background: rgba(255, 255, 255, 0.05);
    color: #fff;
  }

  .node-arrow {
    display: inline-block;
    font-size: 0.7rem;
    transition: transform 0.2s;
    color: rgba(255, 255, 255, 0.4);
    user-select: none;
  }

  .node-arrow.rotated {
    transform: rotate(90deg);
  }

  .node-icon {
    font-size: 1rem;
    line-height: 1;
    filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.2));
  }

  .node-label {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    font-weight: 500;
  }

  .node-children {
    display: flex;
    flex-direction: column;
    padding-left: 20px;
    border-left: 1px dashed rgba(255, 255, 255, 0.1);
    margin-left: 12px;
    gap: 2px;
  }

  .node-children.empty {
    padding: 4px 0 4px 28px;
  }

  .node-empty-text {
    font-size: 0.8rem;
    color: rgba(255, 255, 255, 0.4);
    font-style: italic;
  }

  .node-file {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 8px 6px 28px;
    border-radius: 4px;
    transition: background 0.2s;
  }

  .node-file:hover {
    background: rgba(255, 255, 255, 0.03);
  }

  .node-file-icon {
    font-size: 0.95rem;
    color: rgba(255, 255, 255, 0.5);
  }

  .node-file-label {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    color: rgba(255, 255, 255, 0.7);
  }

  .node-file-size {
    font-size: 0.75rem;
    color: rgba(255, 255, 255, 0.45);
    font-weight: 400;
  }

  .node-error {
    color: #ff6b6b;
    font-size: 0.8rem;
    padding-left: 28px;
    margin: 2px 0;
  }

  .node-loader {
    display: inline-block;
    width: 10px;
    height: 10px;
    border: 2px solid rgba(255, 255, 255, 0.2);
    border-top-color: #fff;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
    margin-left: 4px;
  }

  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }
</style>
