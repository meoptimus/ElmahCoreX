<template>
  <div class="errors-list-view" :class="{ 'has-selection': selectedErrorId }">
    <div
      class="split-container"
      :class="{ 'list-collapsed': listCollapsed, 'is-resizing': resizing }"
      :style="{ '--list-width': listWidth + 'px' }"
    >
      <!-- Shown in place of the list while it is collapsed -->
      <div class="list-rail">
        <button class="btn btn-text btn-sm btn-icon" title="Show the error list" aria-label="Show the error list" @click="setListCollapsed(false)">
          <i class="pi pi-angle-double-right"></i>
        </button>
        <span class="rail-count" :title="`${store.totalCount} errors`">{{ store.totalCount }}</span>
      </div>

      <!-- Left side: List pane (width is user-adjustable) -->
      <div class="list-pane">
        <!-- Search, Select All, and Filter Action Bar -->
        <div class="actions-panel">
          <div class="search-row">
          <div class="search-box">
            <i class="pi pi-search search-icon"></i>
            <input
              type="text"
              placeholder="Search error messages"
              aria-label="Search error messages"
              v-model="searchTerm"
              @input="onSearchInput"
            />
            <button v-if="searchTerm" class="clear-search" aria-label="Clear search" @click="clearSearch">
              <i class="pi pi-times"></i>
            </button>
          </div>
            <button class="btn btn-text btn-sm btn-icon btn-collapse-list" title="Hide the error list" aria-label="Hide the error list" @click="setListCollapsed(true)">
              <i class="pi pi-angle-double-left"></i>
            </button>
          </div>

          <div class="btn-group">
            <button
              class="btn btn-neutral btn-sm"
              :class="{ 'active': selectionMode }"
              @click="toggleSelectionMode"
            >
              <i class="pi pi-check-square"></i>
              Select
            </button>

            <button
              class="btn btn-neutral btn-sm"
              :class="{ 'active': showFilters }"
              @click="showFilters = !showFilters"
            >
              <i class="pi pi-filter"></i>
              Filters
              <span v-if="store.hasActiveFilters" class="active-dot" title="Filters are active"></span>
            </button>

            <button
              class="btn btn-danger-soft btn-sm btn-delete-all"
              title="Delete every error in the log"
              @click="confirmDeleteAll"
            >
              <i class="pi pi-trash"></i>
              Delete all
            </button>
          </div>
        </div>

        <!-- Collapsible Advanced Filters -->
        <transition name="slide-down">
          <div v-if="showFilters" class="filters-card">
            <div class="filters-grid">
              <div class="filter-field">
                <label>Host</label>
                <input type="text" v-model="filterHost" @change="updateFilter('host', filterHost)" placeholder="localhost" />
              </div>

              <div class="filter-field">
                <label>User</label>
                <input type="text" v-model="filterUser" @change="updateFilter('user', filterUser)" placeholder="User" />
              </div>

              <div class="filter-field">
                <label>Type</label>
                <input type="text" v-model="filterType" @change="updateFilter('type', filterType)" placeholder="Exception" />
              </div>

              <div class="filter-field">
                <label>Status</label>
                <input type="number" v-model="filterStatusCode" @change="updateFilter('statusCode', filterStatusCode)" placeholder="500" />
              </div>



              <div class="filter-field">
                <label>App</label>
                <input type="text" v-model="filterApplication" @change="updateFilter('application', filterApplication)" placeholder="App Name" />
              </div>

              <div class="filter-field">
                <label>From</label>
                <input type="date" v-model="filterFrom" @change="updateFilter('from', filterFrom)" />
              </div>

              <div class="filter-field">
                <label>To</label>
                <input type="date" v-model="filterTo" @change="updateFilter('to', filterTo)" />
              </div>
            </div>

            <div class="filters-footer">
              <button class="btn btn-sm btn-text" @click="resetFilters">Reset filters</button>
            </div>
          </div>
        </transition>

        <!-- Bulk Actions Toolbar -->
        <transition name="fade">
          <div v-if="store.selectedIds.length > 0" class="bulk-toolbar">
            <span class="selected-count">
              {{ store.selectedIds.length }} selected
            </span>
            <div class="bulk-actions">
              <button class="btn btn-sm btn-danger" @click="bulkDelete">
                <i class="pi pi-trash"></i> Delete selected
              </button>
            </div>
          </div>
        </transition>

        <!-- Global Select All Bar (shown when list has items) -->
        <div v-if="selectionMode && store.errors.length > 0" class="select-all-bar">
          <label class="select-all-label">
            <input 
              type="checkbox" 
              :checked="isAllSelected" 
              @change="toggleSelectAll"
              class="select-all-checkbox"
            />
            <span>Select all {{ store.errors.length }} loaded</span>
          </label>
        </div>

        <!-- Error Logs List Container -->
        <div class="error-list-container">
          <!-- Loading Overlay -->
          <div v-if="store.loading && !loadingMore && store.errors.length > 0" class="list-loading-overlay">
            <i class="pi pi-spin pi-spinner list-loading-spinner"></i>
          </div>

          <div v-if="store.loading && store.errors.length === 0" class="skeleton-container">
            <div v-for="i in 5" :key="i" class="skeleton-row"></div>
          </div>

          <div v-else-if="store.errors.length === 0" class="empty-state">
            <i class="pi pi-check-circle empty-icon"></i>
            <template v-if="store.hasActiveFilters">
              <h3>No matching errors</h3>
              <p>Nothing in the log matches this search and these filters.</p>
              <button class="btn btn-neutral btn-sm" @click="resetAll">Reset search and filters</button>
            </template>
            <template v-else>
              <h3>No errors logged</h3>
              <p>New errors show up here as soon as they are logged.</p>
            </template>
          </div>

          <!-- Flat Rows List -->
          <div v-else class="error-rows-list" ref="cardsListRef" @scroll="handleScroll">
            <div 
              v-for="entry in store.errors" 
              :key="entry.id" 
              class="error-list-item"
              :class="[
                'sev-' + getSeverityClass(entry.error.statusCode, entry.error.severity),
                { 'active-item': entry.id === selectedErrorId }
              ]"
              @click="viewDetails(entry.id)"
            >
              <div v-if="selectionMode" class="item-checkbox-container">
                <input
                  type="checkbox"
                  :value="entry.id"
                  v-model="store.selectedIds"
                  @click.stop
                  class="item-checkbox"
                  aria-label="Select this error"
                />
              </div>
              <div class="item-main">
                <div class="item-top-row">
                  <span class="status-chip" :class="getSeverityClass(entry.error.statusCode, entry.error.severity)">
                    {{ entry.error.statusCode || '500' }}
                  </span>
                  <span class="error-type-title" :title="entry.error.type">{{ getShortTypeName(entry.error.type) }}</span>
                  <span class="error-time-relative" :title="formatTimeFriendly(entry.error.time)">{{ formatTimeRelative(entry.error.time) }}</span>
                </div>
                <div class="item-bottom-row">
                  <span class="error-message-text" :title="entry.error.message">{{ entry.error.message }}</span>
                  <span
                    v-if="entry.error.url"
                    class="url-text font-mono"
                    :title="`${entry.error.method || 'GET'} ${entry.error.url}`"
                  >{{ entry.error.url }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Infinite Scroll Pager Status Footer -->
          <div v-if="store.errors.length > 0" class="infinite-scroll-footer">
            <template v-if="loadingMore">
              <i class="pi pi-spin pi-spinner mr-1"></i> Loading more…
            </template>
            <template v-else-if="store.errors.length >= store.totalCount">
              All {{ store.totalCount }} loaded
            </template>
            <template v-else>
              Showing {{ store.errors.length }} of {{ store.totalCount }}
            </template>
          </div>
        </div>
      </div>

      <!-- Drag to resize the list, double-click to reset -->
      <div
        class="pane-resizer"
        role="separator"
        aria-orientation="vertical"
        aria-label="Resize the error list"
        :aria-valuenow="listWidth"
        :aria-valuemin="LIST_MIN"
        :aria-valuemax="LIST_MAX"
        tabindex="0"
        title="Drag to resize. Double-click to reset."
        @pointerdown="startResize"
        @dblclick="resetListWidth"
        @keydown="onResizerKey"
      ></div>

      <!-- Right side: Details pane -->
      <div class="details-pane" :class="{ 'active': selectedErrorId }">
        <ErrorDetailPanel 
          v-if="selectedErrorId"
          :id="selectedErrorId" 
          @close="closeDetails" 
          @deleted="onDetailDeleted" 
        />
        <div v-else class="no-error-selected">
          <i class="pi pi-arrow-left placeholder-icon"></i>
          <p>Select an error to see its stack trace and request details.</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useErrorStore } from '../stores/errorStore';
import { useToast } from 'primevue/usetoast';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import ErrorDetailPanel from '../components/ErrorDetailPanel.vue';

dayjs.extend(relativeTime);

const formatTimeRelative = (time) => {
  if (!time) return '';
  return dayjs(time).fromNow(true);
};

const props = defineProps({
  id: {
    type: String,
    required: false,
    default: null
  }
});

const store = useErrorStore();
const router = useRouter();
const route = useRoute();
const toast = useToast();

const cardsListRef = ref(null);
const showFilters = ref(false);
const selectionMode = ref(false);
const searchTerm = ref('');
const filterHost = ref('');
const filterUser = ref('');
const filterType = ref('');
const filterStatusCode = ref('');
const filterApplication = ref('');
const filterFrom = ref('');
const filterTo = ref('');

// List pane width and collapsed state, remembered between visits.
const LIST_MIN = 260;
const LIST_MAX = 800;
const LIST_DEFAULT = 400;

const readSetting = (key) => {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
};

const saveSetting = (key, value) => {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* storage unavailable: the setting lasts for this visit only */
  }
};

// Never let the list squeeze the detail pane below a readable width.
const clampListWidth = (width) =>
  Math.round(Math.min(Math.max(width, LIST_MIN), Math.max(LIST_MIN, Math.min(LIST_MAX, window.innerWidth - 360))));

const listWidth = ref(clampListWidth(parseInt(readSetting('list_pane_width'), 10) || LIST_DEFAULT));
const listCollapsed = ref(readSetting('list_pane_collapsed') === '1');
const resizing = ref(false);

const setListCollapsed = (collapsed) => {
  listCollapsed.value = collapsed;
  saveSetting('list_pane_collapsed', collapsed ? '1' : '0');
};

const setListWidth = (width) => {
  listWidth.value = clampListWidth(width);
  saveSetting('list_pane_width', String(listWidth.value));
};

const resetListWidth = () => setListWidth(LIST_DEFAULT);

const startResize = (e) => {
  if (e.button !== 0) return;
  e.preventDefault();
  const startX = e.clientX;
  const startWidth = listWidth.value;
  resizing.value = true;

  const onMove = (ev) => {
    listWidth.value = clampListWidth(startWidth + ev.clientX - startX);
  };
  const onEnd = () => {
    resizing.value = false;
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onEnd);
    window.removeEventListener('pointercancel', onEnd);
    saveSetting('list_pane_width', String(listWidth.value));
  };
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onEnd);
  window.addEventListener('pointercancel', onEnd);
};

const onResizerKey = (e) => {
  if (e.key === 'ArrowLeft') {
    e.preventDefault();
    setListWidth(listWidth.value - 24);
  } else if (e.key === 'ArrowRight') {
    e.preventDefault();
    setListWidth(listWidth.value + 24);
  } else if (e.key === 'Home') {
    e.preventDefault();
    resetListWidth();
  }
};

const toggleSelectionMode = () => {
  selectionMode.value = !selectionMode.value;
  if (!selectionMode.value) {
    store.selectedIds = [];
  }
};

const selectedErrorId = computed(() => props.id);

const closeDetails = () => {
  router.push('/');
};

const onDetailDeleted = async (deletedId) => {
  router.push('/');
  await store.fetchErrors();
  await store.fetchCounts();
};


const isAllSelected = computed(() => {
  if (store.errors.length === 0) return false;
  return store.errors.every(e => store.selectedIds.includes(e.id));
});

const toggleSelectAll = () => {
  if (isAllSelected.value) {
    store.selectedIds = store.selectedIds.filter(id => !store.errors.some(e => e.id === id));
  } else {
    const newIds = store.errors.map(e => e.id).filter(id => !store.selectedIds.includes(id));
    store.selectedIds.push(...newIds);
  }
};


let searchTimer = null;
const onSearchInput = () => {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    store.setFilter('message', searchTerm.value);
  }, 300);
};

const clearSearch = () => {
  searchTerm.value = '';
  store.setFilter('message', '');
};

const updateFilter = (key, val) => {
  store.setFilter(key, val);
};

const resetFilters = () => {
  filterHost.value = '';
  filterUser.value = '';
  filterType.value = '';
  filterStatusCode.value = '';
  filterApplication.value = '';
  filterFrom.value = '';
  filterTo.value = '';
  store.clearFilters();
};

const resetAll = () => {
  searchTerm.value = '';
  resetFilters();
};

const loadingMore = ref(false);

const handleScroll = (e) => {
  const container = e.target;
  if (container.scrollHeight - container.scrollTop <= container.clientHeight + 50) {
    loadMore();
  }
};

const loadMore = async () => {
  if (loadingMore.value || store.loading) return;
  if (store.errors.length >= store.totalCount) return;

  loadingMore.value = true;
  store.pageIndex = store.errors.length;
  await store.fetchErrors(true);
  loadingMore.value = false;
};


const truncate = (str, len) => {
  if (!str) return '';
  return str.length > len ? str.substring(0, len) + '...' : str;
};

const formatTimeFriendly = (time) => {
  return dayjs(time).format('MMM D, HH:mm:ss');
};

const getShortTypeName = (type) => {
  if (!type) return 'Error';
  const parts = type.split('.');
  return parts[parts.length - 1];
};

const getSeverityClass = (statusCode, severity) => {
  const sev = (severity || '').toLowerCase();
  if (sev === 'info' || sev === 'success' || sev === 'warning' || sev === 'error') return sev;

  if (!statusCode) return 'error';
  if (statusCode < 200) return 'info';
  if (statusCode < 400) return 'success';
  if (statusCode < 500) return 'warning';
  return 'error';
};


const bulkDelete = async () => {
  if (store.selectedIds.length === 0) return;
  if (confirm(`Are you sure you want to delete ${store.selectedIds.length} selected error(s)?`)) {
    const count = store.selectedIds.length;
    await store.deleteSelected();
    selectionMode.value = false;
    toast.add({ severity: 'success', summary: 'Bulk Delete', detail: `Deleted ${count} errors.`, life: 3000 });
  }
};
const viewDetails = (id) => {
  router.push({ name: 'detail', params: { id } });
};

const confirmDeleteAll = async () => {
  if (confirm('WARNING: Are you sure you want to clear ALL errors in the log? This action cannot be undone.')) {
    router.push('/');
    await store.deleteAll();
    toast.add({ severity: 'warn', summary: 'System Action', detail: 'All error logs deleted', life: 5000 });
  }
};

const syncFromRouteQuery = () => {
  const query = route.query;
  const filterKeys = ['type', 'message', 'host', 'user', 'statusCode', 'from', 'to', 'application'];
  
  // Check if query has any of our filter keys
  const hasFilterInQuery = filterKeys.some(key => query[key] !== undefined);
  
  if (hasFilterInQuery) {
    // Reset all local refs and store filters first
    searchTerm.value = '';
    filterHost.value = '';
    filterUser.value = '';
    filterType.value = '';
    filterStatusCode.value = '';
    filterApplication.value = '';
    filterFrom.value = '';
    filterTo.value = '';
    
    // Clear store filters without fetching yet
    Object.keys(store.filters).forEach(key => {
      store.filters[key] = '';
    });
    
    // Apply query parameters
    filterKeys.forEach(key => {
      if (query[key] !== undefined) {
        const queryVal = query[key] === null ? '' : String(query[key]);
        store.filters[key] = queryVal;
        
        // Update local refs
        if (key === 'message') searchTerm.value = queryVal;
        else if (key === 'host') filterHost.value = queryVal;
        else if (key === 'user') filterUser.value = queryVal;
        else if (key === 'type') filterType.value = queryVal;
        else if (key === 'statusCode') filterStatusCode.value = queryVal;
        else if (key === 'application') filterApplication.value = queryVal;
        else if (key === 'from') filterFrom.value = queryVal;
        else if (key === 'to') filterTo.value = queryVal;
      }
    });
    
    return true; // filters changed
  }
  return false;
};

const loadInitial = async () => {
  syncFromRouteQuery();
  await store.fetchErrors();
  await store.fetchCounts();
  if (window.innerWidth > 768 && !selectedErrorId.value && store.errors.length > 0) {
    viewDetails(store.errors[0].id);
  }
};


watch(() => store.errors, (newErrors) => {
  if (window.innerWidth > 768 && !selectedErrorId.value && newErrors.length > 0) {
    viewDetails(newErrors[0].id);
  }
}, { deep: true });

watch(() => route.query, () => {
  const changed = syncFromRouteQuery();
  if (changed) {
    store.pageIndex = 0;
    store.fetchErrors();
    store.fetchCounts();
  }
}, { deep: true });

watch(() => store.filters.message, (newMsg) => {
  searchTerm.value = newMsg || '';
});

onMounted(() => {
  syncFromRouteQuery();
  searchTerm.value = store.filters.message || '';
  loadInitial();
});
</script>

<style scoped>
.errors-list-view {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  height: 100%;
  overflow: hidden;
}

.split-container {
  display: flex;
  flex: 1;
  min-height: 0;
  height: 100%;
  overflow: hidden;
}

.split-container.is-resizing {
  cursor: col-resize;
  user-select: none;
}

.list-pane {
  flex: 0 0 var(--list-width, 400px);
  width: var(--list-width, 400px);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  background-color: var(--surface);
  border-right: 1px solid var(--border);
}

.details-pane {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  background-color: var(--surface);
}

/* Collapsed list: a narrow rail with the way back */
.list-rail {
  display: none;
  flex: 0 0 40px;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding-top: 8px;
  background-color: var(--surface);
  border-right: 1px solid var(--border);
}

.rail-count {
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 700;
  color: var(--text-3);
}

/* The divider between the panes. Wider than it looks, so it is easy to grab. */
.pane-resizer {
  position: relative;
  z-index: 5;
  flex: 0 0 9px;
  margin: 0 -4px 0 -5px;
  cursor: col-resize;
  touch-action: none;
}

.pane-resizer::after {
  content: '';
  position: absolute;
  top: 0;
  bottom: 0;
  left: 3px;
  width: 3px;
  background-color: transparent;
  transition: background-color 0.12s;
}

.pane-resizer:hover::after,
.pane-resizer:focus-visible::after,
.is-resizing .pane-resizer::after {
  background-color: var(--accent);
}

.pane-resizer:focus-visible {
  box-shadow: none;
}

@media (min-width: 769px) {
  .list-collapsed .list-pane,
  .list-collapsed .pane-resizer {
    display: none;
  }

  .list-collapsed .list-rail {
    display: flex;
  }
}

/* Search and actions */
.actions-panel {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--border);
}

.search-row {
  display: flex;
  align-items: center;
  gap: 4px;
}

.search-box {
  position: relative;
  flex: 1;
  min-width: 0;
}

.search-icon {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--text-3);
  font-size: 12px;
  pointer-events: none;
}

.search-box input {
  width: 100%;
  height: 32px;
  padding: 0 30px 0 30px;
  border-radius: var(--radius);
  border: 1px solid var(--border-strong);
  background-color: var(--surface);
  color: var(--text);
  font-size: 13px;
  outline: none;
  transition: border-color 0.12s, box-shadow 0.12s;
}

.search-box input::placeholder {
  color: var(--text-3);
}

.search-box input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 3px var(--accent-soft);
}

.clear-search {
  position: absolute;
  right: 5px;
  top: 50%;
  transform: translateY(-50%);
  width: 22px;
  height: 22px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: none;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--text-3);
  font-size: 10px;
  cursor: pointer;
}

.clear-search:hover {
  background-color: var(--surface-hover);
  color: var(--text);
}

.btn-group {
  display: flex;
  gap: 6px;
}

.btn-delete-all {
  margin-left: auto;
}

.active-dot {
  width: 6px;
  height: 6px;
  background-color: var(--accent);
  border-radius: 50%;
}

/* Filters */
.filters-card {
  padding: 12px;
  background-color: var(--surface-2);
  border-bottom: 1px solid var(--border);
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 10px;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.filter-field label {
  font-size: 11.5px;
  font-weight: 500;
  color: var(--text-2);
}

.filter-field input {
  width: 100%;
  height: 28px;
  padding: 0 8px;
  border-radius: var(--radius);
  border: 1px solid var(--border-strong);
  background-color: var(--surface);
  color: var(--text);
  font-size: 12.5px;
  outline: none;
}

.filter-field input::placeholder {
  color: var(--text-3);
}

.filter-field input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 3px var(--accent-soft);
}

.filters-footer {
  margin-top: 8px;
  display: flex;
  justify-content: flex-end;
}

.slide-down-enter-active,
.slide-down-leave-active {
  transition: opacity 0.12s ease;
}

.slide-down-enter-from,
.slide-down-leave-to {
  opacity: 0;
}

/* Bulk selection */
.bulk-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background-color: var(--accent-soft);
  border-bottom: 1px solid var(--border);
}

.selected-count {
  font-weight: 600;
  font-size: 12.5px;
  color: var(--accent-text);
}

.select-all-bar {
  padding: 8px 12px;
  border-bottom: 1px solid var(--border);
}

.select-all-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12.5px;
  color: var(--text-2);
  cursor: pointer;
  user-select: none;
}

.select-all-checkbox,
.item-checkbox {
  width: 15px;
  height: 15px;
  margin: 0;
  cursor: pointer;
  accent-color: var(--accent);
}

/* List */
.error-list-container {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  position: relative;
}

.list-loading-overlay {
  position: absolute;
  inset: 0;
  background-color: color-mix(in srgb, var(--surface) 60%, transparent);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 10;
}

.list-loading-spinner {
  font-size: 20px;
  color: var(--accent);
}

.error-rows-list {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow-y: auto;
  min-height: 0;
  -webkit-overflow-scrolling: touch;
}

/*
 * A row is two lines so a screenful shows as many errors as possible. The rail on
 * the left edge carries the severity for the whole list at a glance; the selected
 * row is marked by its background, so the two never compete.
 */
.error-list-item {
  position: relative;
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 7px 12px 7px 15px;
  border-bottom: 1px solid var(--border);
  cursor: pointer;
  transition: background-color 0.1s ease;
}

.error-list-item::before {
  content: "";
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background-color: var(--danger);
}

.error-list-item.sev-warning::before {
  background-color: var(--warn);
}

.error-list-item.sev-success::before {
  background-color: var(--ok);
}

.error-list-item.sev-info::before {
  background-color: var(--info);
}

.error-list-item:hover {
  background-color: var(--surface-hover);
}

.error-list-item.active-item {
  background-color: var(--accent-soft);
}

.item-checkbox-container {
  display: flex;
  padding-top: 3px;
  flex-shrink: 0;
}

.item-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.item-top-row,
.item-bottom-row {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.item-bottom-row {
  align-items: baseline;
}

.error-list-item .status-chip {
  height: 18px;
  min-width: 32px;
  font-size: 11px;
}

.error-type-title {
  flex: 1;
  min-width: 0;
  font-weight: 600;
  font-size: 13px;
  color: var(--text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.error-time-relative {
  font-size: 11.5px;
  color: var(--text-3);
  white-space: nowrap;
  flex-shrink: 0;
}

.error-message-text {
  flex: 1 1 auto;
  min-width: 0;
  font-size: 12.5px;
  color: var(--text-2);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.active-item .error-message-text {
  color: var(--text);
}

.url-text {
  flex: 0 1 auto;
  max-width: 42%;
  font-size: 11px;
  color: var(--text-3);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.infinite-scroll-footer {
  padding: 5px 12px;
  border-top: 1px solid var(--border);
  font-size: 11.5px;
  color: var(--text-3);
  text-align: center;
}

/* Empty and loading states */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 48px 24px;
  color: var(--text-2);
}

.empty-state h3 {
  margin: 0 0 4px;
  font-size: 15px;
  font-weight: 600;
  color: var(--text);
}

.empty-state p {
  margin: 0 0 14px;
  max-width: 260px;
}

.empty-icon {
  font-size: 26px;
  margin-bottom: 12px;
  color: var(--ok);
}

.skeleton-container {
  padding: 12px;
}

.skeleton-row {
  height: 44px;
  border-radius: var(--radius);
  background: linear-gradient(90deg, var(--surface-2) 25%, var(--border) 50%, var(--surface-2) 75%);
  background-size: 200% 100%;
  animation: loading 1.5s infinite;
  margin-bottom: 8px;
}

@keyframes loading {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

.no-error-selected {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  color: var(--text-3);
  padding: 48px;
  text-align: center;
}

.no-error-selected p {
  margin: 0;
}

.placeholder-icon {
  font-size: 20px;
}

@media (max-width: 768px) {
  .pane-resizer,
  .btn-collapse-list {
    display: none;
  }

  .list-pane {
    flex: 1 0 100%;
    width: 100%;
    border-right: none;
  }

  .details-pane {
    flex: 1 0 100%;
    width: 100%;
    display: none;
  }

  .details-pane.active {
    display: flex;
  }

  .errors-list-view.has-selection .list-pane {
    display: none;
  }
}
</style>
