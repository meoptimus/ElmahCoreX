<template>
  <div class="error-detail-view">
    <!-- Breadcrumb & Navigation -->
    <div class="detail-header mb-4">
      <div class="breadcrumb">
        <router-link to="/" class="breadcrumb-link">Dashboard</router-link>
        <span class="breadcrumb-separator"><i class="pi pi-chevron-right"></i></span>
        <span class="breadcrumb-current">Error Detail</span>
      </div>

      <div class="detail-nav-buttons" v-if="errorListIds.length > 0">
        <button 
          class="btn btn-secondary btn-sm" 
          :disabled="!hasPrev" 
          @click="navigateError(prevId)"
          title="Previous Error"
        >
          <i class="pi pi-chevron-left mr-1"></i> Prev
        </button>
        <span class="nav-position-text">
          {{ currentIndex + 1 }} of {{ errorListIds.length }}
        </span>
        <button 
          class="btn btn-secondary btn-sm" 
          :disabled="!hasNext" 
          @click="navigateError(nextId)"
          title="Next Error"
        >
          Next <i class="pi pi-chevron-right ml-1"></i>
        </button>
      </div>
    </div>

    <!-- Quick Action Bar -->
    <div class="card action-bar mb-4" v-if="error">
      <div class="action-left">
        <span class="status-code-badge" :class="getSeverityClass(error.statusCode)">
          {{ error.statusCode || 'N/A' }}
        </span>
        <div class="error-meta">
          <h2 class="error-title">{{ getShortTypeName(error.type) }}</h2>
          <p class="error-subtitle">{{ error.message }}</p>
        </div>
      </div>
      <div class="action-right">
        <!-- Mark Reviewed Toggle -->
        <button 
          class="btn" 
          :class="error.isReviewed ? 'btn-success' : 'btn-secondary'"
          @click="toggleReviewed"
        >
          <i :class="error.isReviewed ? 'pi pi-check-circle mr-1' : 'pi pi-circle mr-1'"></i>
          {{ error.isReviewed ? 'Reviewed' : 'Mark Reviewed' }}
        </button>

        <!-- Copy GUID -->
        <button class="btn btn-secondary" @click="copyToClipboard(props.id, 'Error ID copied!')" title="Copy Error GUID">
          <i class="pi pi-copy mr-1"></i> Copy GUID
        </button>

        <!-- Share -->
        <button class="btn btn-secondary" @click="shareLink" title="Copy Share Link">
          <i class="pi pi-share-alt mr-1"></i> Share
        </button>

        <!-- Delete -->
        <button class="btn btn-danger" @click="deleteError">
          <i class="pi pi-trash mr-1"></i> Delete
        </button>
      </div>
    </div>

    <!-- Detail Tabs -->
    <div class="card detail-card" v-if="error">
      <!-- Tabs Navigation -->
      <div class="tabs-nav">
        <button 
          v-for="tab in tabs" 
          :key="tab.id"
          class="tab-btn" 
          :class="{ 'active': activeTab === tab.id }"
          @click="activeTab = tab.id"
        >
          <i :class="tab.icon + ' mr-1'"></i>
          {{ tab.name }}
        </button>
      </div>

      <!-- Tabs Content -->
      <div class="tab-content">
        <!-- 1. Overview Tab -->
        <div v-if="activeTab === 'overview'" class="tab-pane">
          <div class="info-grid">
            <div class="info-item">
              <span class="info-label">Exception Type</span>
              <span class="info-value font-mono select-all">{{ error.type }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Error Message</span>
              <span class="info-value text-danger font-bold">{{ error.message }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Logged Time</span>
              <span class="info-value font-mono">{{ formatTime(error.time) }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Machine Name / Host</span>
              <span class="info-value font-mono">{{ error.hostName }}</span>
            </div>
            <div class="info-item" v-if="error.user">
              <span class="info-label">Logged User</span>
              <span class="info-value">{{ error.user }}</span>
            </div>
            <div class="info-item" v-if="error.url">
              <span class="info-label">Request URL</span>
              <span class="info-value font-mono text-primary select-all">
                <span class="method-tag">{{ error.method }}</span>
                {{ error.url }}
              </span>
            </div>
            <div class="info-item" v-if="error.client">
              <span class="info-label">Client IP Address</span>
              <span class="info-value font-mono">{{ error.client }}</span>
            </div>
            <div class="info-item" v-if="error.source">
              <span class="info-label">Assembly Source</span>
              <span class="info-value font-mono">{{ error.source }}</span>
            </div>
          </div>
        </div>

        <!-- 2. Stack Trace Tab -->
        <div v-if="activeTab === 'stacktrace'" class="tab-pane">
          <!-- Syntax Highlighted Stack Trace Frame -->
          <div class="stacktrace-container">
            <div v-if="error.htmlMessage" class="stacktrace-html" v-html="formatHtmlMessage(error.htmlMessage)"></div>
            <pre v-else class="stacktrace-raw">{{ error.detail }}</pre>
          </div>

          <!-- Source Code Preview (if available) -->
          <div v-if="error.sources && error.sources.length > 0" class="source-previews mt-4">
            <h3 class="section-title"><i class="pi pi-code mr-1"></i> Source Code Context</h3>
            <div v-for="(src, idx) in error.sources" :key="idx" class="source-card card mb-3">
              <div class="source-header">
                <span class="source-file"><i class="pi pi-file mr-1"></i> {{ src.file }} : Line {{ src.line }}</span>
                <span class="source-method">Method: {{ src.method }}</span>
              </div>
              <pre class="source-code"><code>{{ src.contextCode }}</code></pre>
            </div>
          </div>
        </div>

        <!-- 3. Request Parameters Tab -->
        <div v-if="activeTab === 'request'" class="tab-pane">
          <div class="request-tables">
            <!-- Headers -->
            <div v-if="hasItems(error.header)" class="request-section mb-4">
              <h3 class="section-title"><i class="pi pi-envelope mr-1"></i> HTTP Request Headers</h3>
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Header Name</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(val, key) in error.header" :key="key">
                    <td class="font-mono font-bold">{{ key }}</td>
                    <td class="font-mono text-break select-all">{{ val }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Query String -->
            <div v-if="hasItems(error.queryString)" class="request-section mb-4">
              <h3 class="section-title"><i class="pi pi-question-circle mr-1"></i> Query String Parameters</h3>
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Parameter Key</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(val, key) in error.queryString" :key="key">
                    <td class="font-mono font-bold text-primary">{{ key }}</td>
                    <td class="font-mono select-all">{{ val }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Form Data / Cookies -->
            <div v-if="hasItems(error.form)" class="request-section mb-4">
              <h3 class="section-title"><i class="pi pi-list mr-1"></i> Form Fields</h3>
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Field Name</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(val, key) in error.form" :key="key">
                    <td class="font-mono font-bold">{{ key }}</td>
                    <td class="font-mono select-all">{{ val }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Cookies -->
            <div v-if="hasItems(error.cookies)" class="request-section mb-4">
              <h3 class="section-title"><i class="pi pi-database mr-1"></i> Cookies</h3>
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Cookie Name</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(val, key) in error.cookies" :key="key">
                    <td class="font-mono font-bold">{{ key }}</td>
                    <td class="font-mono select-all">{{ val }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Server Variables -->
            <div v-if="hasItems(error.serverVariables)" class="request-section mb-4">
              <h3 class="section-title"><i class="pi pi-server mr-1"></i> Server Variables</h3>
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Variable Name</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(val, key) in error.serverVariables" :key="key">
                    <td class="font-mono font-bold">{{ key }}</td>
                    <td class="font-mono select-all">{{ val }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- 4. Request Body Tab -->
        <div v-if="activeTab === 'body'" class="tab-pane">
          <div class="body-container">
            <div v-if="error.body" class="body-card">
              <pre class="body-raw font-mono"><code>{{ formatBody(error.body) }}</code></pre>
            </div>
            <div v-else class="empty-state">
              <i class="pi pi-info-circle empty-icon"></i>
              <p>No request body logged for this error.</p>
            </div>
          </div>
        </div>

        <!-- 5. Method Parameters Tab -->
        <div v-if="activeTab === 'params'" class="tab-pane">
          <div class="params-container">
            <div v-if="error.params && error.params.length > 0">
              <div v-for="(paramEntry, idx) in error.params" :key="idx" class="param-card card mb-3">
                <div class="param-header">
                  <span class="param-method font-bold"><i class="pi pi-bolt mr-1"></i> {{ paramEntry.memberName }}</span>
                  <span class="param-time text-light font-mono">{{ formatTime(paramEntry.timeStamp) }}</span>
                </div>
                <table class="data-table font-mono">
                  <thead>
                    <tr>
                      <th width="200">Parameter</th>
                      <th>Value</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="kv in paramEntry.params" :key="kv.key">
                      <td class="font-bold">{{ kv.key }}</td>
                      <td class="select-all">{{ kv.value }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
            <div v-else class="empty-state">
              <i class="pi pi-info-circle empty-icon"></i>
              <p>No method parameters logged for this error. Logging method parameters requires calling <code>this.LogParams()</code> in C# code.</p>
            </div>
          </div>
        </div>

        <!-- SQL Log Tab -->
        <div v-if="activeTab === 'sqllog'" class="tab-pane">
          <div class="sqllog-container">
            <div v-if="sqlEntries && sqlEntries.length > 0">
              <div v-for="(sqlEntry, idx) in sqlEntries" :key="idx" class="sql-card card mb-3">
                <div class="sql-header">
                  <div class="header-left">
                    <span class="sql-command-type badge" :class="sqlEntry.commandType?.toLowerCase() === 'storedprocedure' ? 'badge-warning' : 'badge-primary'">
                      {{ sqlEntry.commandType || 'SQL' }}
                    </span>
                    <span class="sql-duration font-bold ml-2" :class="sqlEntry.durationMs > 100 ? 'text-danger' : 'text-success'">
                      {{ sqlEntry.durationMs }} ms
                    </span>
                  </div>
                  <span class="sql-time text-light font-mono">{{ formatTime(sqlEntry.timeStamp) }}</span>
                </div>
                <pre class="sql-text font-mono"><code>{{ sqlEntry.sqlText }}</code></pre>
              </div>
            </div>
            <div v-else class="empty-state">
              <i class="pi pi-database empty-icon"></i>
              <p>No SQL queries logged for this error.</p>
            </div>
          </div>
        </div>

        <!-- 6. Raw XML Tab -->
        <div v-if="activeTab === 'rawxml'" class="tab-pane">
          <div class="xml-container">
            <div class="xml-actions mb-3">
              <button class="btn btn-secondary btn-sm" @click="copyXml">
                <i class="pi pi-copy mr-1"></i> Copy XML
              </button>
            </div>
            <pre class="xml-raw font-mono select-all"><code>{{ rawXml }}</code></pre>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading Placeholder -->
    <div class="card loading-card" v-else>
      <div class="skeleton-title mb-3"></div>
      <div class="skeleton-para mb-2"></div>
      <div class="skeleton-para mb-2"></div>
      <div class="skeleton-para"></div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useErrorStore } from '../stores/errorStore';
import { elmahApi } from '../api/elmahApi';
import { useToast } from 'primevue/usetoast';
import dayjs from 'dayjs';

const props = defineProps({
  id: {
    type: String,
    required: true
  }
});

const store = useErrorStore();
const router = useRouter();
const toast = useToast();

const error = ref(null);
const rawXml = ref('');
const activeTab = ref('overview');

const tabs = [
  { id: 'overview', name: 'Overview', icon: 'pi pi-info-circle' },
  { id: 'stacktrace', name: 'Stack Trace', icon: 'pi pi-align-justify' },
  { id: 'request', name: 'Request', icon: 'pi pi-envelope' },
  { id: 'body', name: 'Request Body', icon: 'pi pi-file' },
  { id: 'params', name: 'Parameters', icon: 'pi pi-bolt' },
  { id: 'sqllog', name: 'SQL Log', icon: 'pi pi-database' },
  { id: 'rawxml', name: 'Raw XML', icon: 'pi pi-code' }
];

const sqlEntries = computed(() => {
  if (!error.value) return [];
  return error.value.sqlLog || error.value.SqlLog || [];
});

// Prev/Next Navigation helpers
const errorListIds = computed(() => store.errors.map(e => e.id));
const currentIndex = computed(() => errorListIds.value.indexOf(props.id));
const hasPrev = computed(() => currentIndex.value > 0);
const hasNext = computed(() => currentIndex.value >= 0 && currentIndex.value < errorListIds.value.length - 1);
const prevId = computed(() => hasPrev.value ? errorListIds.value[currentIndex.value - 1] : null);
const nextId = computed(() => hasNext.value ? errorListIds.value[currentIndex.value + 1] : null);

const fetchErrorDetails = async (id) => {
  error.value = null;
  rawXml.value = '';
  try {
    const res = await elmahApi.getError(id);
    if (res.success && res.data) {
      error.value = res.data.error;
    }

    // Also load raw XML
    const xmlUrl = `${elmahApi.getExportUrl('xml')}`.replace('api/export', 'xml') + `&id=${id}`;
    const xmlRes = await axios.get(xmlUrl, { responseType: 'text' });
    rawXml.value = xmlRes.data;
  } catch (err) {
    console.error(err);
    toast.add({ severity: 'error', summary: 'Error', detail: 'Could not fetch error details', life: 3000 });
  }
};

const navigateError = (id) => {
  router.push({ name: 'detail', params: { id } });
};

const toggleReviewed = async () => {
  const current = error.value.isReviewed;
  try {
    await store.toggleReview(props.id, !current);
    error.value.isReviewed = !current;
    toast.add({ 
      severity: 'success', 
      summary: 'Reviewed Toggle', 
      detail: !current ? 'Error marked as Reviewed' : 'Error marked as Open', 
      life: 2500 
    });
  } catch (err) {
    console.error(err);
  }
};

const deleteError = async () => {
  if (confirm('Are you sure you want to delete this error log?')) {
    try {
      await elmahApi.deleteErrors([props.id]);
      toast.add({ severity: 'success', summary: 'Success', detail: 'Error deleted successfully', life: 3000 });
      router.push('/');
    } catch (err) {
      console.error(err);
    }
  }
};

const shareLink = () => {
  const link = window.location.href;
  copyToClipboard(link, 'Shareable link copied to clipboard!');
};

const copyToClipboard = (text, successMsg) => {
  navigator.clipboard.writeText(text).then(() => {
    toast.add({ severity: 'info', summary: 'Copied', detail: successMsg, life: 2500 });
  }).catch(err => {
    console.error('Failed to copy text: ', err);
  });
};

const copyXml = () => {
  copyToClipboard(rawXml.value, 'Raw XML copied to clipboard!');
};

// Utilities
const hasItems = (obj) => {
  return obj && Object.keys(obj).length > 0;
};

const formatTime = (time) => {
  return dayjs(time).format('YYYY-MM-DD h:mm:ss.SSS A');
};

const getShortTypeName = (type) => {
  if (!type) return 'Error';
  const parts = type.split('.');
  return parts[parts.length - 1];
};

const getSeverityClass = (statusCode) => {
  if (!statusCode) return 'error';
  if (statusCode < 200) return 'info';
  if (statusCode < 400) return 'success';
  if (statusCode < 500) return 'warning';
  return 'error';
};

const formatBody = (body) => {
  if (!body) return '';
  try {
    const parsed = JSON.parse(body);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return body;
  }
};

const formatHtmlMessage = (html) => {
  if (!html) return '';
  return html.replace(/# caller: @([^\r\n]+)/g, (match, path) => {
    return `<span class="st-caller-line"># caller: <span class="st-caller-path">@${path}</span></span>`;
  });
};

// Listen for route ID changes (prev/next navigation)
watch(() => props.id, (newId) => {
  fetchErrorDetails(newId);
}, { immediate: true });

// Axios fallback for XML fetching (if not imported globally)
import axios from 'axios';
</script>

<style scoped>
.error-detail-view {
  display: flex;
  flex-direction: column;
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

.mb-3 {
  margin-bottom: 0.75rem;
}

.mt-4 {
  margin-top: 1.5rem;
}

.mr-1 {
  margin-right: 0.25rem;
}

.ml-1 {
  margin-left: 0.25rem;
}

.font-mono {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
}

.font-bold {
  font-weight: 700;
}

.select-all {
  user-select: all;
}

.text-break {
  word-break: break-all;
}

/* Header & Breadcrumb */
.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.breadcrumb {
  display: flex;
  align-items: center;
  font-size: 0.9rem;
  color: var(--text-light);
}

.breadcrumb-link {
  color: var(--primary-color);
  text-decoration: none;
  font-weight: 600;
}

.breadcrumb-link:hover {
  text-decoration: underline;
}

.breadcrumb-separator {
  margin: 0 0.5rem;
  font-size: 0.75rem;
}

.detail-nav-buttons {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.nav-position-text {
  font-size: 0.85rem;
  color: var(--text-light);
}

/* Action Bar Card */
.action-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  border-left: 4px solid var(--error-color);
}

.action-left {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.status-code-badge {
  padding: 0.4rem 0.8rem;
  border-radius: 8px;
  font-size: 1.15rem;
  font-weight: 800;
}

.status-code-badge.error {
  background-color: #fee2e2;
  color: #ef4444;
}

html.dark-mode .status-code-badge.error {
  background-color: #7f1d1d;
  color: #fca5a5;
}

.status-code-badge.warning {
  background-color: #fef3c7;
  color: #d97706;
}

.status-code-badge.success {
  background-color: #d1fae5;
  color: #10b981;
}

.error-meta {
  display: flex;
  flex-direction: column;
}

.error-title {
  margin: 0 0 0.25rem 0;
  font-size: 1.25rem;
  font-weight: 700;
}

.error-subtitle {
  margin: 0;
  color: var(--text-light);
  font-size: 0.9rem;
  line-height: 1.4;
}

.action-right {
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

.btn-success {
  background-color: var(--success-color);
  color: white;
}

.btn-secondary {
  background-color: var(--panel-bg);
  border-color: var(--border-color);
  color: var(--text-color);
}

.btn-secondary:hover {
  background-color: var(--bg-color);
}

.btn-danger {
  background-color: var(--error-color);
  color: white;
}

/* Detail Card & Tabs */
.detail-card {
  padding: 0;
  overflow: hidden;
}

.tabs-nav {
  display: flex;
  background-color: var(--bg-color);
  border-bottom: 1px solid var(--border-color);
  padding: 0 1rem;
  overflow-x: auto;
}

.tab-btn {
  background: none;
  border: none;
  padding: 1rem 1.25rem;
  color: var(--text-light);
  cursor: pointer;
  font-weight: 600;
  font-size: 0.9rem;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.tab-btn:hover {
  color: var(--text-color);
}

.tab-btn.active {
  color: var(--primary-color);
  border-bottom-color: var(--primary-color);
}

.tab-content {
  padding: 1.5rem;
}

/* Overview Info Grid */
.info-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 0.75rem;
}

.info-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.info-label {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--text-light);
  text-transform: uppercase;
}

.info-value {
  font-size: 0.95rem;
  color: var(--text-color);
  word-break: break-word;
}

.method-tag {
  background-color: var(--primary-color);
  color: white;
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 700;
  margin-right: 0.5rem;
}

/* Stack Trace */
.stacktrace-container {
  background-color: #f8fafc;
  color: #334155;
  border: 1px solid var(--border-color);
  padding: 1.5rem;
  border-radius: 8px;
  overflow: auto;
  max-height: 500px;
}

.stacktrace-raw {
  margin: 0;
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  font-size: 0.85rem;
  white-space: pre-wrap;
}

:deep(.stacktrace-html) {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  font-size: 0.85rem;
  line-height: 1.6;
  white-space: pre-wrap;
}

:deep(.stacktrace-html a) {
  color: #0284c7;
  text-decoration: underline;
}

:deep(.st-frame) {
  display: block;
  padding: 0.1rem 0;
}

:deep(.st-type) {
  color: #4f46e5; /* indigo for types/namespaces */
  font-weight: 600;
}

:deep(.st-method) {
  color: #0891b2; /* cyan/teal for method name */
  font-weight: 600;
}

:deep(.params) {
  color: #64748b; /* slate gray for parameter parens */
}

:deep(.st-param-type) {
  color: #2563eb; /* blue for parameter types */
}

:deep(.st-param-name) {
  color: #b45309; /* amber/brown for parameter names */
  font-style: italic;
}

:deep(.st-file) {
  color: #b91c1c; /* dark red for source files */
  margin-left: 0.5rem;
}

:deep(.st-line) {
  color: #c2410c; /* dark orange for line numbers */
  font-weight: bold;
}

:deep(.st-caller-line) {
  display: block;
  background-color: var(--primary-light);
  color: var(--primary-color);
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  margin-bottom: 0.75rem;
  font-weight: 600;
}

:deep(.st-caller-path) {
  color: #b91c1c;
  font-weight: 700;
  text-decoration: underline;
}

/* Source Context Code */
.source-card {
  border-top: 2px solid var(--success-color);
  padding: 0;
}

.source-header {
  background-color: var(--bg-color);
  padding: 0.5rem 1rem;
  border-bottom: 1px solid var(--border-color);
  display: flex;
  justify-content: space-between;
  font-size: 0.8rem;
  color: var(--text-light);
}

.source-code {
  margin: 0;
  padding: 1rem;
  background-color: #1e293b;
  color: #e2e8f0;
  font-size: 0.825rem;
  overflow-x: auto;
}

/* Request parameters Table style */
.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
}

.data-table th,
.data-table td {
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid var(--border-color);
}

.data-table th {
  text-align: left;
  background-color: var(--bg-color);
  color: var(--text-light);
  font-weight: 600;
}

/* Request Body and parameters styling */
.body-card {
  background-color: var(--bg-color);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 1rem;
}

.body-raw {
  margin: 0;
  font-size: 0.85rem;
  white-space: pre-wrap;
  word-break: break-all;
}

.param-card {
  padding: 0;
}

.param-header {
  padding: 0.5rem 1rem;
  background-color: var(--bg-color);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.xml-raw {
  background-color: var(--bg-color);
  padding: 1rem;
  border-radius: 8px;
  overflow: auto;
  max-height: 400px;
  font-size: 0.825rem;
  white-space: pre-wrap;
}

/* Skeletons */
.loading-card {
  display: flex;
  flex-direction: column;
}

.skeleton-title {
  height: 30px;
  width: 40%;
  background: var(--border-color);
  border-radius: 4px;
}

.skeleton-para {
  height: 20px;
  background: var(--border-color);
  border-radius: 4px;
}

/* SQL Log Tab Styling */
.sql-card {
  padding: 0 !important;
  overflow: hidden;
}

.sql-header {
  background-color: var(--bg-color);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 1rem;
}

.sql-text {
  background-color: #1e293b;
  color: #38bdf8;
  padding: 1rem;
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 0.85rem;
  max-height: 300px;
  overflow-y: auto;
}

.badge-primary {
  background-color: var(--primary-light);
  color: var(--primary-color);
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 700;
}

.badge-warning {
  background-color: #fef3c7;
  color: #d97706;
  padding: 0.2rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 700;
}
</style>
