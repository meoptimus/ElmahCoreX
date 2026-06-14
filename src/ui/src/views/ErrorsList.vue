<template>
  <div class="errors-list-view">
    <div class="split-container">
      <!-- Left side: List pane -->
      <div class="list-pane">
        <!-- Search, Select All, and Filter Action Bar -->
        <div class="actions-panel mb-4">
          <div class="search-box">
            <i class="pi pi-search search-icon"></i>
            <input 
              type="text" 
              placeholder="Search by message or type..." 
              v-model="searchTerm" 
              @input="onSearchInput"
            />
            <button v-if="searchTerm" class="clear-search" @click="clearSearch">
              <i class="pi pi-times"></i>
            </button>
          </div>

          <div class="btn-group">
            <button 
              class="btn btn-secondary" 
              :class="{ 'active': showFilters }"
              @click="showFilters = !showFilters"
            >
              <i class="pi pi-filter mr-1"></i>
              Filters
              <span v-if="store.hasActiveFilters" class="active-dot"></span>
            </button>

            <button 
              class="btn btn-danger" 
              @click="confirmDeleteAll"
            >
              <i class="pi pi-trash mr-1"></i>
              Clear Logs
            </button>
          </div>
        </div>

        <!-- Collapsible Advanced Filters -->
        <transition name="slide-down">
          <div v-if="showFilters" class="filters-card card mb-4">
            <div class="filters-grid">
              <div class="filter-field">
                <label>Host</label>
                <input type="text" v-model="filterHost" @change="updateFilter('host', filterHost)" placeholder="e.g. localhost" />
              </div>

              <div class="filter-field">
                <label>User</label>
                <input type="text" v-model="filterUser" @change="updateFilter('user', filterUser)" placeholder="Username" />
              </div>

              <div class="filter-field">
                <label>Exception Type</label>
                <input type="text" v-model="filterType" @change="updateFilter('type', filterType)" placeholder="e.g. NullReferenceException" />
              </div>

              <div class="filter-field">
                <label>Status Code</label>
                <input type="number" v-model="filterStatusCode" @change="updateFilter('statusCode', filterStatusCode)" placeholder="e.g. 500" />
              </div>

              <div class="filter-field">
                <label>Review Status</label>
                <select v-model="filterIsReviewed" @change="updateFilter('isReviewed', filterIsReviewed)">
                  <option value="">All Statuses</option>
                  <option value="false">Unreviewed / Open</option>
                  <option value="true">Reviewed</option>
                </select>
              </div>

              <div class="filter-field">
                <label>Application</label>
                <input type="text" v-model="filterApplication" @change="updateFilter('application', filterApplication)" placeholder="App Name" />
              </div>

              <div class="filter-field">
                <label>Date From</label>
                <input type="date" v-model="filterFrom" @change="updateFilter('from', filterFrom)" />
              </div>

              <div class="filter-field">
                <label>Date To</label>
                <input type="date" v-model="filterTo" @change="updateFilter('to', filterTo)" />
              </div>
            </div>

            <div class="filters-footer">
              <button class="btn btn-sm btn-text" @click="resetFilters">Reset Filters</button>
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
              <button class="btn btn-sm btn-success mr-2" @click="bulkMarkReviewed(true)">
                <i class="pi pi-check mr-1"></i> Mark Reviewed
              </button>
              <button class="btn btn-sm btn-secondary mr-2" @click="bulkMarkReviewed(false)">
                <i class="pi pi-times-circle mr-1"></i> Mark Unreviewed
              </button>
              <button class="btn btn-sm btn-danger" @click="bulkDelete">
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
                    :checked="store.selectedIds.includes(entry.id)"
                    @change="toggleItemSelection(entry.id)"
                    @click.stop
                    class="item-checkbox"
                  />
                  <span class="status-badge" :class="getSeverityClass(entry.error.statusCode, entry.error.severity)">
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
            <div class="scroll-status-left">
              Loaded {{ store.errors.length }} of {{ store.totalCount }}
            </div>
            <div v-if="store.loading || loadingMore" class="scroll-status-right">
              <i class="pi pi-spin pi-spinner mr-2"></i> Loading...
            </div>
            <div v-else-if="store.errors.length >= store.totalCount" class="scroll-status-right text-muted">
              All loaded
            </div>
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

const toggleItemSelection = (id) => {
  if (store.selectedIds.includes(id)) {
    store.selectedIds = store.selectedIds.filter(x => x !== id);
  } else {
    store.selectedIds.push(id);
  }
};

// Computed selection helpers
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

// Search Debounce/Throttle
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

// Formatting helpers
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

// Page Operations
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

// Watch errors list to auto-select the first error when none is selected
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
  flex: 0 0 30%;
  width: 30%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  background-color: var(--bg-color);
  border-right: 1px solid var(--border-color);
}

.details-pane {
  flex: 0 0 70%;
  width: 70%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 100%;
  background-color: var(--panel-bg);
}

/* Actions Panel */
.actions-panel {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem 0.5rem 1rem;
  border-bottom: 1px solid var(--border-color);
}

.search-box {
  position: relative;
  flex: 1;
  min-width: 0;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  top: 50%;
  transform: translateY(-50%);
  color: var(--text-light);
  font-size: 0.8rem;
}

.search-box input {
  width: 100%;
  padding: 0.4rem 2rem;
  border-radius: 0;
  border: 1px solid var(--border-color);
  background-color: var(--panel-bg);
  color: var(--text-color);
  outline: none;
  font-size: 0.85rem;
  font-family: var(--font-family);
  transition: border-color 0.15s;
}

.search-box input:focus {
  border-color: var(--primary-color);
}

.clear-search {
  position: absolute;
  right: 0.75rem;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  color: var(--text-light);
  cursor: pointer;
  padding: 0;
}

.btn-group {
  display: flex;
  gap: 0.25rem;
}

.btn {
  padding: 0.4rem 0.75rem;
  border-radius: 0 !important;
  font-weight: 600;
  font-size: 0.8rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid transparent;
  transition: all 0.15s;
}

.btn-sm {
  padding: 0.25rem 0.5rem;
  font-size: 0.75rem;
}

.btn-primary {
  background-color: var(--primary-color);
  color: white;
}

.btn-primary:hover {
  background-color: var(--primary-hover);
}

.btn-secondary {
  background-color: var(--panel-bg);
  border-color: var(--border-color);
  color: var(--text-color);
}

.btn-secondary:hover {
  background-color: var(--bg-color);
}

.btn-secondary.active {
  border-color: var(--primary-color);
  color: var(--primary-color);
}

.btn-danger {
  background-color: var(--error-color);
  color: white;
}

.btn-danger:hover {
  filter: brightness(0.9);
}

.btn-text {
  background: none;
  border: none;
  color: var(--text-light);
}

.btn-text:hover {
  color: var(--text-color);
}

.active-dot {
  width: 5px;
  height: 5px;
  background-color: var(--primary-color);
  border-radius: 50%;
  display: inline-block;
  margin-left: 0.25rem;
}

/* Advanced Filters Card */
.filters-card {
  margin: 0 1rem 0.75rem 1rem;
  padding: 0.75rem;
  border-radius: 0;
  border: 1px solid var(--border-color);
  background-color: var(--panel-bg);
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 0.5rem;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.filter-field label {
  font-size: 0.65rem;
  font-weight: 700;
  color: var(--text-light);
  text-transform: uppercase;
  font-family: var(--font-mono);
}

.filter-field input,
.filter-field select {
  padding: 0.3rem 0.4rem;
  border-radius: 0;
  border: 1px solid var(--border-color);
  background-color: var(--bg-color);
  color: var(--text-color);
  outline: none;
  font-size: 0.75rem;
}

.filter-field input:focus,
.filter-field select:focus {
  border-color: var(--primary-color);
}

.filters-footer {
  margin-top: 0.5rem;
  display: flex;
  justify-content: flex-end;
}

/* Bulk Toolbar */
.bulk-toolbar {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-left: 3px solid var(--primary-color);
  padding: 0.4rem 0.75rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin: 0 1rem 0.5rem 1rem;
}

.selected-count {
  font-weight: 600;
  color: var(--text-color);
  font-size: 0.8rem;
  font-family: var(--font-mono);
}

.bulk-actions {
  display: flex;
}

.mr-2 {
  margin-right: 0.5rem;
}

.mr-1 {
  margin-right: 0.25rem;
}

/* Select All Bar */
.select-all-bar {
  padding: 0.4rem 1rem;
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  background-color: var(--panel-bg);
}

.select-all-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.75rem;
  color: var(--text-light);
  font-weight: 600;
  cursor: pointer;
  user-select: none;
}

.select-all-checkbox, .item-checkbox {
  cursor: pointer;
  accent-color: var(--primary-color);
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
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border-color);
  border-left: 2px solid transparent;
  cursor: pointer;
  transition: background-color 150ms ease, border-left-color 150ms ease;
  background-color: var(--bg-color);
}

.error-list-item:hover {
  background-color: var(--panel-bg);
  border-left-color: var(--primary-color);
}

.error-list-item.active-item {
  background-color: var(--panel-bg) !important;
  border-left-color: var(--primary-color) !important;
}

.error-list-item.reviewed-item {
  opacity: 0.55;
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.35rem;
}

.item-left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
}

.status-badge {
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 0.7rem;
  padding: 0.1rem 0.35rem;
  color: white;
  min-width: 32px;
  text-align: center;
  white-space: nowrap;
}

.status-badge.error {
  background-color: var(--error-color);
}

.status-badge.warning {
  background-color: var(--warning-color);
}

.status-badge.info {
  background-color: var(--primary-color);
}

.status-badge.success {
  background-color: var(--success-color);
}

.error-type-title {
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 0.8rem;
  color: var(--text-color);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.error-time-subtle {
  font-family: var(--font-mono);
  font-size: 0.75rem;
  color: var(--text-light);
  white-space: nowrap;
}

.item-body {
  padding-left: 1.6rem;
}

.error-message-text {
  font-size: 0.75rem;
  color: var(--text-light);
  line-height: 1.35;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.active-item .error-message-text {
  color: var(--text-color);
}

/* Infinite Scroll Footer Styling */
.infinite-scroll-footer {
  padding: 0.25rem 1rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-top: 1px solid var(--border-color);
  font-size: 0.75rem;
  color: var(--text-light);
  background-color: var(--bg-color);
  z-index: 10;
  font-family: var(--font-mono);
}

.scroll-status-right {
  display: flex;
  align-items: center;
  font-weight: 600;
}

/* Empty & Skeleton states */
.empty-state {
  text-align: center;
  padding: 3rem;
  color: var(--text-light);
}

.empty-icon {
  font-size: 2.5rem;
  margin-bottom: 0.75rem;
}

.text-success {
  color: var(--success-color);
}

.skeleton-container {
  padding: 1rem;
}

.skeleton-row {
  height: 60px;
  background: linear-gradient(90deg, var(--bg-color) 25%, var(--border-color) 50%, var(--bg-color) 75%);
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
  color: var(--text-light);
  padding: 3rem;
  text-align: center;
}

.placeholder-icon {
  font-size: 2.5rem;
  margin-bottom: 0.75rem;
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
    border-top: 1px solid var(--border-color);
  }
}
</style>
