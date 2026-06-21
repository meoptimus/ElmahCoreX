<template>
  <div class="errors-list-view" :class="{ 'has-selection': selectedErrorId }">
    <div class="split-container">
      <!-- Left side: List pane (fixed 300px) -->
      <div class="list-pane">
        <!-- Search, Select All, and Filter Action Bar -->
        <div class="actions-panel mb-4">
          <div class="search-box">
            <i class="pi pi-search search-icon"></i>
            <input 
              type="text" 
              placeholder="Search..." 
              v-model="searchTerm" 
              @input="onSearchInput"
            />
            <button v-if="searchTerm" class="clear-search" @click="clearSearch">
              <i class="pi pi-times"></i>
            </button>
          </div>

          <div class="btn-group">
            <button 
              class="btn btn-neutral btn-sm" 
              :class="{ 'active': showFilters }"
              @click="showFilters = !showFilters"
            >
              <i class="pi pi-filter mr-1"></i>
              Filters
              <span v-if="store.hasActiveFilters" class="active-dot"></span>
            </button>

            <button 
              class="btn btn-terracotta btn-sm" 
              @click="confirmDeleteAll"
            >
              <i class="pi pi-trash mr-1"></i>
              Clear
            </button>
          </div>
        </div>

        <!-- Collapsible Advanced Filters -->
        <transition name="slide-down">
          <div v-if="showFilters" class="filters-card card mb-4">
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
                <label>Reviewed</label>
                <select v-model="filterIsReviewed" @change="updateFilter('isReviewed', filterIsReviewed)">
                  <option value="">All</option>
                  <option value="false">Open</option>
                  <option value="true">Reviewed</option>
                </select>
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
              <button class="btn btn-sm btn-text" @click="resetFilters">Reset</button>
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
              <button class="btn btn-sm btn-neutral mr-2" @click="bulkMarkReviewed(true)">
                <i class="pi pi-check mr-1"></i> Reviewed
              </button>
              <button class="btn btn-sm btn-neutral mr-2" @click="bulkMarkReviewed(false)">
                <i class="pi pi-times-circle mr-1"></i> Open
              </button>
              <button class="btn btn-sm btn-terracotta" @click="bulkDelete">
                <i class="pi pi-trash mr-1"></i> Delete
              </button>
            </div>
          </div>
        </transition>

        <!-- Global Select All Bar (shown when list has items) -->
        <div v-if="store.errors.length > 0" class="select-all-bar">
          <label class="select-all-label">
            <input 
              type="checkbox" 
              :checked="isAllSelected" 
              @change="toggleSelectAll"
              class="select-all-checkbox"
            />
            <span>Select All loaded errors</span>
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
            <i class="pi pi-check-circle empty-icon text-success"></i>
            <h3>All Clear!</h3>
            <p>No logged errors matching the current filter criteria.</p>
          </div>

          <!-- Flat Rows List -->
          <div v-else class="error-rows-list" ref="cardsListRef" @scroll="handleScroll">
            <div 
              v-for="entry in store.errors" 
              :key="entry.id" 
              class="error-list-item"
              :class="{ 
                'active-item': entry.id === selectedErrorId, 
                'reviewed-item': entry.error.isReviewed 
              }"
              @click="viewDetails(entry.id)"
            >
              <div class="item-checkbox-container">
                <input 
                  type="checkbox" 
                  :value="entry.id"
                  v-model="store.selectedIds"
                  @click.stop
                  class="item-checkbox"
                />
              </div>
              <div class="item-left-col">
                <span class="status-badge-circle" :class="[getSeverityClass(entry.error.statusCode, entry.error.severity), 'status-' + entry.error.statusCode]">
                  {{ entry.error.statusCode || '500' }}
                </span>
                <span class="error-time-relative" :title="formatTimeFriendly(entry.error.time)">{{ formatTimeRelative(entry.error.time) }}</span>
              </div>
              <div class="item-right-col">
                <div class="error-type-title">{{ getShortTypeName(entry.error.type) }}</div>
                <div class="error-url-row" v-if="entry.error.url">
                  <span class="method-badge" :class="entry.error.method">{{ entry.error.method || 'GET' }}</span>
                  <span class="url-text font-mono" :title="entry.error.url">{{ entry.error.url }}</span>
                </div>
                <div class="error-message-text" :title="entry.error.message">
                  {{ entry.error.message }}
                </div>
              </div>
            </div>
          </div>

          <!-- Infinite Scroll Pager Status Footer -->
          <div v-if="store.errors.length > 0" class="infinite-scroll-footer">
            <template v-if="loadingMore">
              <i class="pi pi-spin pi-spinner mr-1"></i> Loading more...
            </template>
            <template v-else>
              Loaded {{ store.errors.length }} of {{ store.totalCount }}
              <span v-if="store.errors.length >= store.totalCount"> — All loaded</span>
            </template>
          </div>
        </div>
      </div>

      <!-- Right side: Details pane -->
      <div class="details-pane" :class="{ 'active': selectedErrorId }">
        <ErrorDetailPanel 
          v-if="selectedErrorId"
          :id="selectedErrorId" 
          @close="closeDetails" 
          @deleted="onDetailDeleted" 
        />
        <div v-else class="no-error-selected">
          <i class="pi pi-exclamation-circle placeholder-icon"></i>
          <p>Select an error from the list to view details.</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { useRouter } from 'vue-router';
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
const toast = useToast();

const cardsListRef = ref(null);
const showFilters = ref(false);
const searchTerm = ref('');
const filterHost = ref('');
const filterUser = ref('');
const filterType = ref('');
const filterStatusCode = ref('');
const filterIsReviewed = ref('');
const filterApplication = ref('');
const filterFrom = ref('');
const filterTo = ref('');

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
  filterIsReviewed.value = '';
  filterApplication.value = '';
  filterFrom.value = '';
  filterTo.value = '';
  store.clearFilters();
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


const bulkMarkReviewed = async (isReviewed) => {
  if (store.selectedIds.length === 0) return;
  const count = store.selectedIds.length;
  await store.markSelectedReviewed(isReviewed);
  toast.add({ severity: 'success', summary: 'Bulk Update', detail: `Marked ${count} errors as ${isReviewed ? 'Reviewed' : 'Open'}.`, life: 3000 });
};

const bulkDelete = async () => {
  if (store.selectedIds.length === 0) return;
  if (confirm(`Are you sure you want to delete ${store.selectedIds.length} selected error(s)?`)) {
    const count = store.selectedIds.length;
    await store.deleteSelected();
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

const loadInitial = async () => {
  await store.fetchErrors();
  await store.fetchCounts();
  if (!selectedErrorId.value && store.errors.length > 0) {
    viewDetails(store.errors[0].id);
  }
};


watch(() => store.errors, (newErrors) => {
  if (!selectedErrorId.value && newErrors.length > 0) {
    viewDetails(newErrors[0].id);
  }
}, { deep: true });

watch(() => store.filters.message, (newMsg) => {
  searchTerm.value = newMsg || '';
});

onMounted(() => {
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
  overflow: hidden;
  height: 100%;
}

.list-pane {
  flex: 0 0 380px;
  width: 380px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  background-color: #f7f6f2; /* Warm parchment */
  border-right: 1px solid #e2e0da;
}

.details-pane {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  background-color: #ffffff; /* pure white */
}

/* Actions Panel */
.actions-panel {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.35rem;
  padding: 10px 12px;
  border-bottom: 1px solid #e8e6e0;
}

.search-box {
  position: relative;
  flex: 1;
  min-width: 0;
}

.search-icon {
  position: absolute;
  left: 0.6rem;
  top: 50%;
  transform: translateY(-50%);
  color: #aaa9a3;
  font-size: 0.75rem;
}

.search-box input {
  width: 100%;
  padding: 0.35rem 1.75rem;
  border-radius: 3px;
  border: 1px solid #dddbd4;
  background-color: #ffffff;
  color: #1a1a2e;
  outline: none;
  font-size: 11px;
  font-family: var(--font-family);
}

.search-box input::placeholder {
  color: #aaa9a3;
}

.clear-search {
  position: absolute;
  right: 0.6rem;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: #aaa9a3;
  cursor: pointer;
  padding: 0;
}

.btn-group {
  display: flex;
  gap: 0.25rem;
}

.btn {
  padding: 4px 8px;
  font-weight: 500;
  font-size: 11px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid transparent;
  transition: all 0.15s;
}

.btn-sm {
  padding: 3px 6px;
  font-size: 10px;
}

.btn-neutral {
  border: 1px solid #dddbd4;
  background-color: #ffffff;
  color: #5f5e5a;
  border-radius: 3px;
}

.btn-neutral:hover {
  background-color: #eeecea;
}

.btn-neutral.active {
  border-color: #4a7fc1;
  color: #4a7fc1;
}

.btn-terracotta {
  background-color: #fdf2ef;
  border: 1px solid #e8c4ba;
  color: #b05a4a;
  border-radius: 3px;
}

.btn-terracotta:hover {
  background-color: #f9e2db;
}

.btn-text {
  background: none;
  border: none;
  color: #888780;
}

.btn-text:hover {
  color: #1a1a2e;
}

.active-dot {
  width: 4px;
  height: 4px;
  background-color: #4a7fc1;
  border-radius: 50%;
  display: inline-block;
  margin-left: 0.2rem;
}

/* Advanced Filters Card */
.filters-card {
  margin: 0 10px 10px 10px;
  padding: 10px;
  border-radius: 3px;
  border: 1px solid #dddbd4;
  background-color: #ffffff;
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 0.35rem;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.filter-field label {
  font-size: 9px;
  font-weight: 700;
  color: #aaa9a3;
  text-transform: uppercase;
  font-family: var(--font-mono);
}

.filter-field input,
.filter-field select {
  padding: 0.25rem;
  border-radius: 3px;
  border: 1px solid #dddbd4;
  background-color: #ffffff;
  color: #1a1a2e;
  outline: none;
  font-size: 10px;
}

.filter-field input:focus,
.filter-field select:focus {
  border-color: #4a7fc1;
}

.filters-footer {
  margin-top: 0.35rem;
  display: flex;
  justify-content: flex-end;
}

/* Bulk Toolbar */
.bulk-toolbar {
  background-color: #eeecea; /* Bulk action bar */
  border: 1px solid #e8e6e0;
  padding: 6px 10px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin: 0 10px 8px 10px;
  border-radius: 3px;
}

.selected-count {
  font-weight: 500;
  color: #5f5e5a;
  font-size: 11px;
}

.bulk-actions {
  display: flex;
}


/* Select All Bar */
.select-all-bar {
  padding: 6px 12px;
  border-bottom: 1px solid #e8e6e0;
  display: flex;
  align-items: center;
  background-color: #ffffff;
}

.select-all-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 11px;
  color: #888780;
  font-weight: 500;
  cursor: pointer;
  user-select: none;
}

.select-all-checkbox, .item-checkbox {
  cursor: pointer;
  accent-color: #4a7fc1;
  width: 13px;
  height: 13px;
  border-radius: 3px;
  border: 1px solid #dddbd4;
}

/* Error List Container */
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
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(247, 246, 242, 0.6);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 10;
}

.list-loading-spinner {
  font-size: 1.5rem;
  color: #4a7fc1;
}

.error-rows-list {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

/* Error List Item Row */
.error-list-item {
  display: flex;
  align-items: flex-start;
  padding: 12px 10px;
  border-bottom: 1px solid #e8e6e0;
  border-left: 3px solid transparent;
  cursor: pointer;
  transition: background-color 150ms ease, border-left-color 150ms ease;
  background-color: transparent;
  gap: 8px;
  border-radius: 0 !important;
}

.error-list-item:hover {
  background-color: #eeecea;
}

.error-list-item.active-item {
  background-color: #ffffff !important;
  border-left: 3px solid #4a7fc1 !important;
}

.error-list-item.reviewed-item {
  opacity: 0.55;
}

.item-checkbox-container {
  display: flex;
  align-items: center;
  justify-content: center;
  padding-top: 10px;
  flex-shrink: 0;
}

.item-checkbox {
  cursor: pointer;
  accent-color: #4a7fc1;
  width: 13px;
  height: 13px;
  border-radius: 3px;
  border: 1px solid #dddbd4;
}

.item-left-col {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
  width: 46px;
}

.status-badge-circle {
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 10px;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  white-space: nowrap;
  background-color: #aaa9a3; /* fallback */
  flex-shrink: 0;
}

.status-badge-circle.error,
.status-badge-circle.status-500 {
  background-color: #d94f4f !important; /* HTTP 500 red circle */
}

.status-badge-circle.warning {
  background-color: #f59e0b !important;
}

.status-badge-circle.success {
  background-color: #10b981 !important;
}

.status-badge-circle.info {
  background-color: #3b82f6 !important;
}

.error-time-relative {
  font-family: var(--font-family);
  font-size: 10px;
  color: #aaa9a3;
  text-align: center;
  line-height: 1.1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 46px;
}

.item-right-col {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.error-type-title {
  font-weight: bold;
  font-size: 12px;
  color: #000000;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.error-url-row {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.method-badge {
  padding: 1px 4px;
  font-size: 9px;
  font-weight: bold;
  font-family: var(--font-mono);
  border-radius: 2px;
  color: #ffffff;
  background-color: #888780;
  flex-shrink: 0;
  text-transform: uppercase;
}

.method-badge.POST {
  background-color: #b05a4a;
}

.method-badge.GET {
  background-color: #888780;
}

.url-text {
  font-size: 11px;
  color: #333333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}

.error-message-text {
  font-size: 11px;
  color: #111111;
  line-height: 1.4;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.active-item .error-message-text {
  color: #000000;
}

/* Infinite Scroll Footer Styling */
.infinite-scroll-footer {
  padding: 8px 12px;
  border-top: 1px solid #e8e6e0;
  font-size: 11px;
  color: #aaa9a3;
  background-color: #f7f6f2;
  text-align: center;
}

/* Empty & Skeleton states */
.empty-state {
  text-align: center;
  padding: 3rem;
  color: #aaa9a3;
}

.empty-icon {
  font-size: 2rem;
  margin-bottom: 0.5rem;
}

.text-success {
  color: var(--success-color);
}

.skeleton-container {
  padding: 1rem;
}

.skeleton-row {
  height: 50px;
  background: linear-gradient(90deg, #f7f6f2 25%, #e8e6e0 50%, #f7f6f2 75%);
  background-size: 200% 100%;
  animation: loading 1.5s infinite;
  margin-bottom: 0.5rem;
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
  color: #aaa9a3;
  padding: 3rem;
  text-align: center;
}

.placeholder-icon {
  font-size: 2rem;
  margin-bottom: 0.5rem;
}

/* Responsive Overrides */
@media (max-width: 768px) {
  .list-pane {
    flex: 1 0 100% !important;
    width: 100% !important;
    max-width: 100% !important;
  }

  .details-pane {
    flex: 1 0 100% !important;
    width: 100% !important;
    max-width: 100% !important;
    display: none !important;
  }

  .details-pane.active {
    display: flex !important;
  }

  .errors-list-view.has-selection .list-pane {
    display: none !important;
  }
}

.error-rows-list {
  -webkit-overflow-scrolling: touch;
}
</style>
