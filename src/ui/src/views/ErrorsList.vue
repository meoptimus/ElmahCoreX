<template>
  <div class="errors-list-view">
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
              <div class="item-header">
                <div class="item-left">
                  <input 
                    type="checkbox" 
                    :value="entry.id"
                    v-model="store.selectedIds"
                    @click.stop
                    class="item-checkbox"
                  />
                  <span class="status-badge" :class="[getSeverityClass(entry.error.statusCode, entry.error.severity), 'status-' + entry.error.statusCode]">
                    {{ entry.error.statusCode || '500' }}
                  </span>
                  <span class="error-type-title">{{ getShortTypeName(entry.error.type) }}</span>
                </div>
                <div class="item-right">
                  <span class="error-time-subtle">{{ formatTimeFriendly(entry.error.time) }}</span>
                </div>
              </div>

              <div class="item-body">
                <div class="error-message-text" :title="entry.error.message">
                  {{ truncate(entry.error.message, 120) }}
                </div>
              </div>
            </div>
          </div>

          <!-- Infinite Scroll Pager Status Footer -->
          <div v-if="store.errors.length > 0" class="infinite-scroll-footer">
            Loaded {{ store.errors.length }} of {{ store.totalCount }}
            <span v-if="store.errors.length >= store.totalCount"> — All loaded</span>
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
import ErrorDetailPanel from '../components/ErrorDetailPanel.vue';

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

onMounted(() => {
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
  flex: 0 0 300px;
  width: 300px;
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
  flex-direction: column;
  padding: 10px 12px;
  border-bottom: 1px solid #e8e6e0;
  border-left: 2px solid transparent;
  cursor: pointer;
  transition: background-color 150ms ease, border-left-color 150ms ease;
  background-color: transparent;
  border-radius: 0 !important; /* NO border-radius on list rows */
}

.error-list-item:hover {
  background-color: #eeecea; /* Hover background */
}

.error-list-item.active-item {
  background-color: #ffffff !important; /* Active background */
  border-left: 2px solid #4a7fc1 !important; /* Active left border accent */
}

.error-list-item.reviewed-item {
  opacity: 0.55;
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.35rem;
  margin-bottom: 0.25rem;
}

.item-left {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
}

.status-badge {
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 10px;
  padding: 1px 4px;
  color: white;
  min-width: 28px;
  text-align: center;
  white-space: nowrap;
  background-color: #aaa9a3; /* neutral stone gray for non-500 codes */
  border-radius: 3px; /* 3-5px max */
}

.status-badge.error,
.status-badge.status-500 {
  background-color: #d94f4f !important; /* HTTP 500 badge ONLY */
}

.error-type-title {
  font-weight: 500;
  font-size: 12px;
  color: #1a1a2e;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.error-time-subtle {
  font-family: var(--font-mono);
  font-size: 10px;
  color: #aaa9a3;
  white-space: nowrap;
}

.item-body {
  padding-left: 1.5rem;
}

.error-message-text {
  font-size: 11px;
  color: #888780;
  line-height: 1.4;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.active-item .error-message-text {
  color: #1a1a2e;
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
@media (max-width: 1024px) {
  .split-container {
    flex-direction: column;
    height: auto;
    overflow: visible;
  }

  .list-pane {
    width: 100%;
    flex: none;
    height: auto;
    overflow: visible;
  }

  .error-rows-list {
    overflow: visible;
    height: auto;
    flex: none;
  }

  .details-pane {
    width: 100%;
    flex: none;
    height: auto;
    border-left: none;
    border-top: 1px solid #e8e6e0;
  }
}
</style>
