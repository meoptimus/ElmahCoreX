<template>
  <div class="errors-list-view">
    <div class="split-container">
      <!-- Left side: List pane -->
      <div 
        class="list-pane" 
        :class="{ 'has-detail': selectedErrorId }"
      >
        <!-- Filter Toggle Bar & Quick Filters -->
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
              Clear All Logs
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
              {{ store.selectedIds.length }} items selected
            </span>
            <div class="bulk-actions">
              <button class="btn btn-sm btn-success mr-2" @click="bulkMarkReviewed(true)">
                <i class="pi pi-check mr-1"></i> Mark Reviewed
              </button>
              <button class="btn btn-sm btn-secondary mr-2" @click="bulkMarkReviewed(false)">
                <i class="pi pi-times-circle mr-1"></i> Mark Unreviewed
              </button>
              <button class="btn btn-sm btn-danger" @click="bulkDelete">
                <i class="pi pi-trash mr-1"></i> Delete Selected
              </button>
            </div>
          </div>
        </transition>

        <!-- Error Logs Table / Cards List -->
        <div class="card table-card" :class="{ 'cards-only-card': selectedErrorId }">
          <div v-if="store.loading && store.errors.length === 0" class="skeleton-container">
            <div v-for="i in 5" :key="i" class="skeleton-row"></div>
          </div>

          <div v-else-if="store.errors.length === 0" class="empty-state">
            <i class="pi pi-check-circle empty-icon text-success"></i>
            <h3>All Clear!</h3>
            <p>No logged errors matching the current filter criteria.</p>
          </div>

          <!-- Cards List (Side-bar mode when details pane is open) -->
          <div v-else-if="selectedErrorId" class="error-cards-list" ref="cardsListRef" @scroll="handleScroll">
            <div 
              v-for="entry in store.errors" 
              :key="entry.id" 
              class="error-card"
              :class="[
                { 
                  'active-card': entry.id === selectedErrorId, 
                  'reviewed-card': entry.error.isReviewed 
                },
                'severity-' + (entry.error.severity || 'Error').toLowerCase()
              ]"
              @click="viewDetails(entry.id)"
            >
              <div class="card-header">
                <div class="card-left">
                  <input 
                    type="checkbox" 
                    :checked="store.selectedIds.includes(entry.id)"
                    @change="toggleItemSelection(entry.id)"
                    @click.stop
                  />
                  <!-- Subtle status indicator code (no severity badge focus) -->
                  <span class="status-code-subtle" :class="getSeverityClass(entry.error.statusCode, entry.error.severity)">
                    {{ entry.error.statusCode || 'N/A' }}
                  </span>
                  <span class="error-type-title">{{ getShortTypeName(entry.error.type) }}</span>
                </div>
                <div class="card-right">
                  <span class="error-time-subtle">{{ formatTimeFriendly(entry.error.time) }}</span>
                </div>
              </div>

              <div class="card-body">
                <div class="error-message-text" :title="entry.error.message">
                  {{ truncate(entry.error.message, 180) }}
                </div>
              </div>

              <div class="card-footer-meta">
                <div class="meta-tags">
                  <span class="meta-tag" v-if="entry.error.hostName"><i class="pi pi-server"></i> {{ entry.error.hostName }}</span>
                  <span class="meta-tag" v-if="entry.error.user"><i class="pi pi-user"></i> {{ entry.error.user }}</span>
                </div>
                <div class="card-actions">
                  <button 
                    class="card-reviewed-toggle"
                    :class="{ 'is-reviewed': entry.error.isReviewed }"
                    @click.stop="store.toggleReview(entry.id, !entry.error.isReviewed)"
                    title="Toggle Reviewed Status"
                  >
                    <i :class="entry.error.isReviewed ? 'pi pi-check-circle' : 'pi pi-circle'"></i>
                  </button>
                </div>
              </div>
            </div>
          </div>

          <!-- Table View (Standard full view when no details open) -->
          <div v-else class="table-responsive" ref="tableContainerRef" @scroll="handleScroll">
            <table class="errors-table">
              <thead>
                <tr>
                  <th width="40">
                    <input 
                      type="checkbox" 
                      :checked="isAllSelected" 
                      @change="toggleSelectAll"
                    />
                  </th>
                  <th width="90">Severity</th>
                  <th width="80">Code</th>
                  <th>Error Details</th>
                  <th width="150">Host</th>
                  <th width="120">User</th>
                  <th width="180">Time</th>
                  <th width="130">Reviewed</th>
                  <th width="100">Actions</th>
                </tr>
              </thead>
              <tbody>
                <tr 
                  v-for="entry in store.errors" 
                  :key="entry.id" 
                  :class="[
                    entry.error.isReviewed ? 'reviewed-row' : '',
                    store.selectedIds.includes(entry.id) ? 'selected-row' : '',
                    entry.id === selectedErrorId ? 'active-row' : '',
                    'severity-' + (entry.error.severity || 'Error').toLowerCase()
                  ]"
                >
                  <td>
                    <input 
                      type="checkbox" 
                      :value="entry.id" 
                      v-model="store.selectedIds"
                    />
                  </td>
                  <td>
                    <span class="severity-badge" :class="getSeverityClass(entry.error.statusCode, entry.error.severity)">
                      {{ getSeverityText(entry.error.statusCode, entry.error.severity) }}
                    </span>
                  </td>
                  <td class="font-mono text-center font-bold">
                    {{ entry.error.statusCode || 'N/A' }}
                  </td>
                  <td class="error-summary-cell" @click="viewDetails(entry.id)">
                    <div class="error-type">{{ getShortTypeName(entry.error.type) }}</div>
                    <div class="error-message" :title="entry.error.message">
                      {{ truncate(entry.error.message, 120) }}
                    </div>
                  </td>
                  <td class="text-light font-mono">{{ entry.error.hostName || 'N/A' }}</td>
                  <td class="text-light">{{ entry.error.user || 'N/A' }}</td>
                  <td class="text-light font-mono">{{ formatTime(entry.error.time) }}</td>
                  <td>
                    <button 
                      class="reviewed-toggle-btn"
                      :class="{ 'is-reviewed': entry.error.isReviewed }"
                      @click="store.toggleReview(entry.id, !entry.error.isReviewed)"
                    >
                      <i :class="entry.error.isReviewed ? 'pi pi-check-circle' : 'pi pi-circle'"></i>
                      <span>{{ entry.error.isReviewed ? 'Reviewed' : 'Open' }}</span>
                    </button>
                  </td>
                  <td>
                    <div class="table-actions">
                      <button class="icon-btn" title="View details" @click="viewDetails(entry.id)">
                        <i class="pi pi-eye text-primary"></i>
                      </button>
                      <button class="icon-btn" title="Delete Log" @click="deleteItem(entry.id)">
                        <i class="pi pi-trash text-danger"></i>
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Infinite Scroll Pager Status Footer -->
          <div v-if="store.errors.length > 0" class="infinite-scroll-footer">
            <div class="scroll-status-left">
              Loaded {{ store.errors.length }} of {{ store.totalCount }} errors
            </div>
            <div v-if="store.loading || loadingMore" class="scroll-status-right">
              <i class="pi pi-spin pi-spinner mr-2"></i> Loading more...
            </div>
            <div v-else-if="store.errors.length >= store.totalCount" class="scroll-status-right text-muted">
              All errors loaded.
            </div>
          </div>
        </div>
      </div>

      <!-- Right side: Details pane -->
      <div class="details-pane" :class="{ 'active': selectedErrorId }" v-if="selectedErrorId">
        <ErrorDetailPanel 
          :id="selectedErrorId" 
          @close="closeDetails" 
          @deleted="onDetailDeleted" 
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
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
const tableContainerRef = ref(null);
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

const scrollToTop = () => {
  if (cardsListRef.value) {
    cardsListRef.value.scrollTop = 0;
  }
  if (tableContainerRef.value) {
    tableContainerRef.value.scrollTop = 0;
  }
};

const toggleItemSelection = (id) => {
  if (store.selectedIds.includes(id)) {
    store.selectedIds = store.selectedIds.filter(x => x !== id);
  } else {
    store.selectedIds.push(id);
  }
};

// Computed Paging helpers
const totalPages = computed(() => Math.ceil(store.totalCount / store.pageSize) || 1);
const currentPage = computed(() => Math.floor(store.pageIndex / store.pageSize) + 1);
const isFirstPage = computed(() => store.pageIndex === 0);
const isLastPage = computed(() => store.pageIndex + store.pageSize >= store.totalCount);
const startRange = computed(() => store.pageIndex + 1);
const endRange = computed(() => Math.min(store.pageIndex + store.pageSize, store.totalCount));

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

const formatTime = (time) => {
  return dayjs(time).format('YYYY-MM-DD h:mm:ss A');
};

const formatTimeFriendly = (time) => {
  return dayjs(time).format('MMM D, h:mm:ss A');
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

const getSeverityText = (statusCode, severity) => {
  if (severity) return severity.toUpperCase();
  if (!statusCode) return 'ERROR';
  if (statusCode < 200) return 'INFO';
  if (statusCode < 400) return 'SUCCESS';
  if (statusCode < 500) return 'WARNING';
  return 'ERROR';
};

// Page Operations
const viewDetails = (id) => {
  router.push({ name: 'detail', params: { id } });
};

const deleteItem = async (id) => {
  if (confirm('Are you sure you want to delete this log?')) {
    if (selectedErrorId.value === id) {
      router.push('/');
    }
    store.selectedIds = [id];
    await store.deleteSelected();
    toast.add({ severity: 'success', summary: 'Success', detail: 'Error log deleted', life: 3000 });
  }
};

const bulkMarkReviewed = async (reviewed) => {
  await store.markSelectedReviewed(reviewed);
  toast.add({ severity: 'success', summary: 'Bulk Action', detail: `Marked selected items as ${reviewed ? 'Reviewed' : 'Unreviewed'}`, life: 3000 });
};

const bulkDelete = async () => {
  if (confirm(`Are you sure you want to delete ${store.selectedIds.length} items?`)) {
    if (store.selectedIds.includes(selectedErrorId.value)) {
      router.push('/');
    }
    await store.deleteSelected();
    toast.add({ severity: 'success', summary: 'Bulk Action', detail: 'Selected items deleted', life: 3000 });
  }
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
  gap: 1.5rem;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  height: 100%;
}

.list-pane {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-width: 0;
  height: 100%;
  padding-right: 0.25rem;
}

.list-pane.has-detail {
  flex: 0 0 40%;
}

.details-pane {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  height: 100%;
  border-left: 1px solid var(--border-color);
  padding-left: 1.5rem;
}

.card {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03);
}

.mb-4 {
  margin-bottom: 1rem;
}

.mr-2 {
  margin-right: 0.5rem;
}

.mr-1 {
  margin-right: 0.25rem;
}

.font-mono {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
}

.font-bold {
  font-weight: 700;
}

.text-center {
  text-align: center;
}

/* Actions Panel */
.actions-panel {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.search-box {
  position: relative;
  flex: 1;
  max-width: 400px;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  top: 50%;
  transform: translateY(-50%);
  color: var(--text-light);
}

.search-box input {
  width: 100%;
  padding: 0.5rem 2.25rem;
  border-radius: 8px;
  border: 1px solid var(--border-color);
  background-color: var(--panel-bg);
  color: var(--text-color);
  outline: none;
  font-size: 0.9rem;
  transition: border-color 0.2s;
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
}

.btn-group {
  display: flex;
  gap: 0.5rem;
}

.btn {
  padding: 0.5rem 1rem;
  border-radius: 8px;
  font-weight: 600;
  font-size: 0.875rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid transparent;
  transition: all 0.2s;
}

.btn-sm {
  padding: 0.35rem 0.75rem;
  font-size: 0.8rem;
  border-radius: 6px;
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
  background-color: var(--primary-light);
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
  width: 6px;
  height: 6px;
  background-color: var(--primary-color);
  border-radius: 50%;
  display: inline-block;
  margin-left: 0.25rem;
}

/* Advanced Filters Card */
.filters-card {
  padding: 1rem;
  border-top: 2px solid var(--primary-color);
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.filter-field label {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--text-light);
  text-transform: uppercase;
}

.filter-field input,
.filter-field select {
  padding: 0.4rem 0.6rem;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  background-color: var(--panel-bg);
  color: var(--text-color);
  outline: none;
  font-size: 0.85rem;
}

.filter-field input:focus,
.filter-field select:focus {
  border-color: var(--primary-color);
}

.filters-footer {
  margin-top: 1rem;
  display: flex;
  justify-content: flex-end;
}

/* Bulk Toolbar */
.bulk-toolbar {
  background-color: var(--primary-light);
  border: 1px solid var(--primary-color);
  border-radius: 8px;
  padding: 0.75rem 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  animation: fadeIn 0.2s;
}

.selected-count {
  font-weight: 600;
  color: var(--primary-color);
  font-size: 0.9rem;
}

.bulk-actions {
  display: flex;
}

/* Errors Table styling */
.table-card {
  padding: 0;
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.table-responsive {
  width: 100%;
  flex: 1;
  overflow-y: auto;
  overflow-x: auto;
  min-height: 0;
}

.errors-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 0.875rem;
}

.errors-table th,
.errors-table td {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border-color);
  vertical-align: middle;
}

.errors-table th {
  background-color: var(--bg-color);
  color: var(--text-light);
  font-weight: 600;
  text-transform: uppercase;
  font-size: 0.75rem;
  letter-spacing: 0.05em;
  position: sticky;
  top: 0;
  z-index: 1;
}

.errors-table tbody tr {
  transition: background-color 0.15s;
}

.errors-table tbody tr:hover {
  background-color: var(--bg-color);
}

.reviewed-row {
  opacity: 0.65;
}

.selected-row {
  background-color: var(--primary-light) !important;
}

/* Active Highlight */
.errors-table tbody tr.active-row {
  background-color: var(--primary-light) !important;
  border-left: 3px solid var(--primary-color);
}

.severity-badge {
  padding: 0.2rem 0.5rem;
  border-radius: 6px;
  font-size: 0.7rem;
  font-weight: 700;
  text-align: center;
  display: inline-block;
  min-width: 60px;
}

.severity-badge.error {
  background-color: #fee2e2;
  color: #ef4444;
}

html.dark-mode .severity-badge.error {
  background-color: #7f1d1d;
  color: #fca5a5;
}

.severity-badge.warning {
  background-color: #fef3c7;
  color: #d97706;
}

html.dark-mode .severity-badge.warning {
  background-color: #78350f;
  color: #fde68a;
}

.severity-badge.success {
  background-color: #d1fae5;
  color: #10b981;
}

.severity-badge.info {
  background-color: #e0f2fe;
  color: #0284c7;
}

.error-summary-cell {
  cursor: pointer;
}

.error-type {
  font-weight: 600;
  color: var(--primary-color);
  font-size: 0.9rem;
  margin-bottom: 0.15rem;
}

.error-message {
  color: var(--text-color);
  font-size: 0.825rem;
  line-height: 1.25;
}

.text-light {
  color: var(--text-light);
}

/* Reviewed button toggle styling */
.reviewed-toggle-btn {
  background: none;
  border: 1px solid var(--border-color);
  padding: 0.25rem 0.5rem;
  border-radius: 6px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 0.75rem;
  color: var(--text-light);
  transition: all 0.2s;
}

.reviewed-toggle-btn:hover {
  background-color: var(--bg-color);
}

.reviewed-toggle-btn.is-reviewed {
  border-color: var(--success-color);
  color: var(--success-color);
  background-color: #e6fbf3;
}

html.dark-mode .reviewed-toggle-btn.is-reviewed {
  background-color: #064e3b;
}

.table-actions {
  display: flex;
  gap: 0.25rem;
}

/* Table Footer & Pagination */
.table-footer {
  padding: 0.75rem 1rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-top: 1px solid var(--border-color);
  font-size: 0.8rem;
  color: var(--text-light);
  background-color: var(--bg-color);
}

.footer-right {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.page-size select {
  background: none;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  color: var(--text-color);
  padding: 0.15rem 0.3rem;
  outline: none;
}

.pagination-nav {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.page-indicator {
  font-weight: 500;
}

/* Empty & Skeleton states */
.empty-state {
  text-align: center;
  padding: 3rem;
  color: var(--text-light);
}

.empty-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.text-success {
  color: var(--success-color);
}

.skeleton-container {
  padding: 1.5rem;
}

.skeleton-row {
  height: 40px;
  background: linear-gradient(90deg, var(--bg-color) 25%, var(--border-color) 50%, var(--bg-color) 75%);
  background-size: 200% 100%;
  animation: loading 1.5s infinite;
  margin-bottom: 0.75rem;
  border-radius: 6px;
}

@keyframes loading {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

/* Transition Animations */
.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.2s ease-out;
  max-height: 300px;
  overflow: hidden;
}

.slide-down-enter-from,
.slide-down-leave-to {
  max-height: 0;
  opacity: 0;
  padding: 0 1rem;
  margin-bottom: 0;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

/* Error Card List Layout (Sidebar view) */
.error-cards-list {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.5rem;
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.error-card {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 0.4rem 0.6rem;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.error-card:hover {
  border-color: var(--primary-color);
  background-color: var(--bg-color);
}

.error-card.active-card {
  border-color: var(--primary-color);
  background-color: var(--primary-light);
  box-shadow: 0 0 0 1px var(--primary-color);
}

.error-card.reviewed-card {
  opacity: 0.7;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.4rem;
}

.card-left {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
}

.status-code-subtle {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  font-weight: 700;
  font-size: 0.725rem;
  padding: 0.05rem 0.25rem;
  border-radius: 4px;
  white-space: nowrap;
}

.status-code-subtle.error {
  color: var(--error-color);
  background-color: #fee2e2;
}
html.dark-mode .status-code-subtle.error {
  background-color: #7f1d1d;
}

.status-code-subtle.warning {
  color: var(--warning-color);
  background-color: #fef3c7;
}
html.dark-mode .status-code-subtle.warning {
  background-color: #78350f;
}

.status-code-subtle.success {
  color: var(--success-color);
  background-color: #d1fae5;
}
html.dark-mode .status-code-subtle.success {
  background-color: #064e3b;
}

.status-code-subtle.info {
  color: #0284c7;
  background-color: #e0f2fe;
}
html.dark-mode .status-code-subtle.info {
  background-color: #0c4a6e;
}

.error-type-title {
  font-weight: 700;
  color: var(--text-color);
  font-size: 0.8rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.error-time-subtle {
  font-size: 0.7rem;
  color: var(--text-light);
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  white-space: nowrap;
}

.card-body {
  min-width: 0;
}

.error-message-text {
  font-size: 0.725rem;
  color: var(--text-light);
  line-height: 1.25;
  word-break: break-word;
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.error-card.active-card .error-message-text {
  color: var(--text-color);
}

.card-footer-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 0.1rem;
  border-top: none;
  padding-top: 0;
  font-size: 0.7rem;
}

.meta-tags {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
  min-width: 0;
}

.meta-tag {
  color: var(--text-light);
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100px;
}

.meta-tag i {
  font-size: 0.7rem;
}

.card-actions {
  display: flex;
  align-items: center;
}

.card-reviewed-toggle {
  background: none;
  border: none;
  cursor: pointer;
  padding: 0.2rem;
  color: var(--text-light);
  font-size: 0.85rem;
  transition: color 0.15s;
}

.card-reviewed-toggle:hover {
  color: var(--primary-color);
}

.card-reviewed-toggle.is-reviewed {
  color: var(--success-color);
}

/* Sidebar Pager Styling */
.sidebar-pager {
  padding: 0.4rem 0.6rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-top: 1px solid var(--border-color);
  font-size: 0.8rem;
  color: var(--text-light);
  background-color: var(--bg-color);
  margin-top: auto; /* Push pager to the bottom of the list-pane */
}

.pager-right {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.infinite-scroll-loading i {
  animation: spin-loader 1s linear infinite;
}

.cards-only-card {
  padding: 0 !important;
  background-color: transparent !important;
  border: none !important;
  box-shadow: none !important;
}

@keyframes spin-loader {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* Responsive Overrides */
@media (max-width: 1024px) {
  .split-container {
    flex-direction: column;
    height: auto;
    overflow: visible;
  }

  .list-pane {
    height: auto;
    overflow: visible;
  }

  .list-pane.has-detail {
    display: none;
  }

  .error-cards-list,
  .table-responsive {
    overflow: visible;
    height: auto;
    flex: none;
  }

  .details-pane {
    flex: none;
    width: 100%;
    border-left: none;
    padding-left: 0;
  }
}

/* Infinite Scroll Footer Styling */
.infinite-scroll-footer {
  padding: 0.75rem 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-top: 1px solid var(--border-color);
  font-size: 0.85rem;
  color: var(--text-light);
  background-color: var(--panel-bg);
  z-index: 10;
}

.scroll-status-right {
  display: flex;
  align-items: center;
  font-weight: 600;
}

/* Card Severity Borders */
.error-card {
  border-left: 4px solid var(--border-color);
}
.error-card.severity-info {
  border-left-color: #3b82f6 !important;
}
.error-card.severity-success {
  border-left-color: #10b981 !important;
}
.error-card.severity-warning {
  border-left-color: #f59e0b !important;
}
.error-card.severity-error {
  border-left-color: #ef4444 !important;
}

/* Table Row Severity Borders */
.errors-table tr.severity-info td:first-child {
  border-left: 4px solid #3b82f6;
}
.errors-table tr.severity-success td:first-child {
  border-left: 4px solid #10b981;
}
.errors-table tr.severity-warning td:first-child {
  border-left: 4px solid #f59e0b;
}
.errors-table tr.severity-error td:first-child {
  border-left: 4px solid #ef4444;
}
</style>
