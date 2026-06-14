<template>
  <div class="error-detail-panel">
    <!-- Panel Header with Navigation and Close Button -->
    <div class="panel-header mb-4">
      <div class="header-left">
        <button class="btn btn-secondary btn-sm close-text-btn" @click="emit('close')" title="Close details">
          <i class="pi pi-arrow-left mr-1"></i> Back to List
        </button>
      </div>

      <div class="header-right-actions">
        <div class="detail-nav-buttons mr-2" v-if="errorListIds.length > 0">
          <button 
            class="btn btn-secondary btn-sm btn-icon" 
            :disabled="!hasPrev" 
            @click="navigateError(prevId)"
            title="Previous Error"
          >
            <i class="pi pi-chevron-left"></i>
          </button>
          <span class="nav-position-text">
            {{ currentIndex + 1 }} of {{ errorListIds.length }}
          </span>
          <button 
            class="btn btn-secondary btn-sm btn-icon" 
            :disabled="!hasNext" 
            @click="navigateError(nextId)"
            title="Next Error"
          >
            <i class="pi pi-chevron-right"></i>
          </button>
        </div>

        <button class="btn-close-x" @click="emit('close')" title="Close details">
          <i class="pi pi-times"></i>
        </button>
      </div>
    </div>

    <!-- Unified Error Details Header Card -->
    <div class="card unified-header-card mb-4" v-if="error" :class="getSeverityClass(error.statusCode)">
      <!-- Action Bar Row -->
      <div class="action-bar-section">
        <div class="action-top">
          <span class="status-code-badge" :class="getSeverityClass(error.statusCode)">
            {{ error.statusCode || 'N/A' }}
          </span>
          <div class="error-meta">
            <h2 class="error-title">{{ getShortTypeName(error.type) }}</h2>
            <p class="error-subtitle font-mono">{{ error.message }}</p>
          </div>
        </div>
        <div class="action-buttons mt-3">
          <!-- Mark Reviewed Toggle -->
          <button 
            class="btn btn-sm" 
            :class="error.isReviewed ? 'btn-success' : 'btn-secondary'"
            @click="toggleReviewed"
          >
            <i :class="error.isReviewed ? 'pi pi-check-circle mr-1' : 'pi pi-circle mr-1'"></i>
            {{ error.isReviewed ? 'Reviewed' : 'Mark Reviewed' }}
          </button>

          <!-- Copy GUID -->
          <button class="btn btn-secondary btn-sm" @click="copyToClipboard(props.id, 'Error ID copied!')" title="Copy Error GUID">
            <i class="pi pi-copy mr-1"></i> Copy GUID
          </button>

          <!-- Share -->
          <button class="btn btn-secondary btn-sm" @click="shareLink" title="Copy Share Link">
            <i class="pi pi-share-alt mr-1"></i> Share
          </button>

          <!-- Delete -->
          <button class="btn btn-danger btn-sm" @click="deleteError">
            <i class="pi pi-trash mr-1"></i> Delete
          </button>
        </div>
      </div>

      <!-- Metadata Section -->
      <div class="metadata-section">
        <div class="metadata-grid">
          <div class="metadata-main">
            <div class="metadata-grid-cols">
              <div class="metadata-item">
                <span class="meta-label">When</span>
                <span class="meta-value font-mono">{{ formatTime(error.time) }}</span>
              </div>
              <div class="metadata-item" v-if="error.client">
                <span class="meta-label">Client IP</span>
                <span class="meta-value font-mono select-all">{{ error.client }}</span>
              </div>
              <div class="metadata-item" v-if="error.user">
                <span class="meta-label">User</span>
                <span class="meta-value select-all">{{ error.user }}</span>
              </div>
              <div class="metadata-item" v-if="error.hostName">
                <span class="meta-label">Host</span>
                <span class="meta-value font-mono">{{ error.hostName }}</span>
              </div>
              <div class="metadata-item" v-if="error.applicationName">
                <span class="meta-label">Application</span>
                <span class="meta-value">{{ error.applicationName }}</span>
              </div>
              <div class="metadata-item" v-if="error.source">
                <span class="meta-label">Source</span>
                <span class="meta-value font-mono text-break">{{ error.source }}</span>
              </div>
              <div class="metadata-item text-break span-full" v-if="error.url">
                <span class="meta-label">URL</span>
                <span class="meta-value font-mono select-all">
                  <span class="method-badge" :class="getSeverityClass(error.statusCode)">{{ error.method }}</span>
                  {{ error.url }}
                </span>
              </div>
            </div>
          </div>
          <div class="metadata-sidebar" v-if="error.os || error.browser">
            <div class="client-icon-card" v-if="error.os">
              <i class="pi" :class="[getOsIcon(error.os), 'os-icon-' + error.os.toLowerCase()]"></i>
              <span class="client-label">{{ error.os }}</span>
            </div>
            <div class="client-icon-card" v-if="error.browser">
              <i class="pi" :class="[getBrowserIcon(error.browser), 'browser-icon-' + error.browser.toLowerCase()]"></i>
              <span class="client-label">{{ error.browser }}</span>
            </div>
          </div>
        </div>
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
          </div>
        </div>

        <!-- 2. Stack Trace Tab -->
        <div v-if="activeTab === 'stacktrace'" class="tab-pane">
          <!-- Syntax Highlighted Stack Trace Frame -->
          <div class="stacktrace-container">
            <div v-if="error.htmlMessage" class="stacktrace-html" v-html="formatHtmlMessage(error.htmlMessage)"></div>
            <div v-else class="stacktrace-html" v-html="formatRawStackTrace(error.detail)"></div>
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
import { ref, computed, watch } from 'vue';
import { useRouter } from 'vue-router';
import { useErrorStore } from '../stores/errorStore';
import { elmahApi } from '../api/elmahApi';
import { useToast } from 'primevue/usetoast';
import dayjs from 'dayjs';
import axios from 'axios';

const props = defineProps({
  id: {
    type: String,
    required: true
  }
});

const emit = defineEmits(['close', 'deleted']);

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
  if (!id) return;
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
      emit('deleted', props.id);
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

const getOsIcon = (os) => {
  if (!os) return 'pi-desktop';
  const osLower = os.toLowerCase();
  if (osLower.includes('windows')) return 'pi-microsoft';
  if (osLower.includes('iphone') || osLower.includes('ipad') || osLower.includes('macintosh') || osLower.includes('apple')) return 'pi-apple';
  if (osLower.includes('android')) return 'pi-android';
  if (osLower.includes('linux')) return 'pi-server';
  return 'pi-desktop';
};

const getBrowserIcon = (browser) => {
  if (!browser) return 'pi-globe';
  const bLower = browser.toLowerCase();
  if (bLower.includes('chrome')) return 'pi-globe';
  if (bLower.includes('firefox')) return 'pi-globe';
  if (bLower.includes('safari') || bLower.includes('webkit')) return 'pi-compass';
  if (bLower.includes('edge')) return 'pi-microsoft';
  if (bLower.includes('opera')) return 'pi-globe';
  if (bLower.includes('msie')) return 'pi-microsoft';
  if (bLower.includes('bot')) return 'pi-android';
  return 'pi-globe';
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

const formatRawStackTrace = (text) => {
  if (!text) return '';
  // Escape HTML entities to prevent XSS
  let escaped = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  const lines = escaped.split('\n');
  const formattedLines = lines.map(line => {
    // 1. Check for stack trace frame: starts with spaces followed by "at " or "   at "
    const atMatch = line.match(/^(\s*at\s+)([A-Za-z0-9_.\+<>`\[\]]+)\(([^)]*)\)(?:\s+in\s+(.*?):line\s+(\d+))?/);
    if (atMatch) {
      const prefix = atMatch[1]; // "   at "
      const fullMethod = atMatch[2]; // e.g. "Namespace.Class.Method"
      const paramsText = atMatch[3]; // e.g. "String name, Int32 age"
      const filePath = atMatch[4]; // e.g. "C:\path\to\file.cs"
      const lineNum = atMatch[5]; // e.g. "123"

      const methodParts = fullMethod.split('.');
      const method = methodParts.pop();
      const typePath = methodParts.join('.');

      let methodHtml = '';
      if (typePath) {
        methodHtml = `<span class="st-type">${typePath}</span>.<span class="st-method">${method}</span>`;
      } else {
        methodHtml = `<span class="st-method">${method}</span>`;
      }

      let paramsHtml = '';
      if (paramsText.trim()) {
        const params = paramsText.split(',');
        paramsHtml = params.map(p => {
          const parts = p.trim().split(/\s+/);
          if (parts.length >= 2) {
            const name = parts.pop();
            const type = parts.join(' ');
            return `<span class="st-param-type">${type}</span> <span class="st-param-name">${name}</span>`;
          }
          return `<span class="st-param-type">${p}</span>`;
        }).join(', ');
      }

      let lineHtml = `${prefix}${methodHtml}<span class="params">(</span>${paramsHtml}<span class="params">)</span>`;
      if (filePath) {
        lineHtml += ` <span class="text-muted">in</span> <span class="st-file">${filePath}</span><span class="st-line">:line ${lineNum}</span>`;
      }
      return `<span class="st-frame">${lineHtml}</span>`;
    }

    // 2. Check for inner exception indicator: "---> ExceptionType: message"
    const innerMatch = line.match(/^(\s*--->\s+)?([A-Za-z0-9_.]*Exception):\s*(.*)/);
    if (innerMatch) {
      const arrow = innerMatch[1] || '';
      const excType = innerMatch[2];
      const msg = innerMatch[3];
      return `<span class="st-exception-line">${arrow}<span class="st-exception-type">${excType}</span>: <span class="st-exception-msg">${msg}</span></span>`;
    }

    // 3. Regular exception start line (e.g. "System.AggregateException: One or more...")
    const startMatch = line.match(/^([A-Za-z0-9_.]*Exception):\s*(.*)/);
    if (startMatch) {
      const excType = startMatch[1];
      const msg = startMatch[2];
      return `<span class="st-exception-line"><span class="st-exception-type">${excType}</span>: <span class="st-exception-msg font-bold">${msg}</span></span>`;
    }

    return line;
  });

  return formattedLines.join('\n');
};

// Listen for route ID changes (prev/next navigation)
watch(() => props.id, (newId) => {
  if (newId) {
    fetchErrorDetails(newId);
  }
}, { immediate: true });
</script>

<style scoped>
.error-detail-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-color);
}

.header-right-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn-close-x {
  background: none;
  border: none;
  color: var(--text-light);
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 50%;
  font-size: 1.2rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.btn-close-x:hover {
  background-color: var(--border-color);
  color: var(--text-color);
}

.card {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 1.25rem;
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

.mr-2 {
  margin-right: 0.5rem;
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
  flex-direction: column;
  border-left: 4px solid var(--error-color);
}

.action-top {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
}

.status-code-badge {
  padding: 0.4rem 0.8rem;
  border-radius: 8px;
  font-size: 1.15rem;
  font-weight: 800;
  min-width: 60px;
  text-align: center;
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
  flex: 1;
  min-width: 0;
}

.error-title {
  margin: 0 0 0.25rem 0;
  font-size: 1.15rem;
  font-weight: 700;
  word-break: break-all;
}

.error-subtitle {
  margin: 0 0 0.5rem 0;
  color: var(--text-color);
  font-size: 0.85rem;
  line-height: 1.4;
  word-break: break-word;
  opacity: 0.9;
}

.action-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.btn {
  padding: 0.4rem 0.8rem;
  border-radius: 8px;
  font-weight: 600;
  font-size: 0.85rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid transparent;
  transition: all 0.2s;
}

.btn-icon {
  width: 30px;
  height: 30px;
  padding: 0;
  border-radius: 6px;
}

.btn-sm {
  padding: 0.3rem 0.6rem;
  font-size: 0.775rem;
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
  display: flex;
  flex-direction: column;
  flex: 1;
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
  padding: 0.75rem 1rem;
  color: var(--text-light);
  cursor: pointer;
  font-weight: 600;
  font-size: 0.85rem;
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
  padding: 1.25rem;
  overflow-y: auto;
  flex: 1;
}

/* Overview Info Grid */
.info-grid {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 0.5rem;
}

.info-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.info-label {
  font-size: 0.7rem;
  font-weight: 700;
  color: var(--text-light);
  text-transform: uppercase;
}

.info-value {
  font-size: 0.9rem;
  color: var(--text-color);
  word-break: break-word;
}

.method-tag {
  background-color: var(--primary-color);
  color: white;
  padding: 0.1rem 0.3rem;
  border-radius: 4px;
  font-size: 0.7rem;
  font-weight: 700;
  margin-right: 0.5rem;
}

/* Stack Trace */
.stacktrace-container {
  background-color: #f8fafc;
  color: #334155;
  border: 1px solid var(--border-color);
  padding: 1rem;
  border-radius: 8px;
  overflow: auto;
  max-height: 400px;
}

.stacktrace-raw {
  margin: 0;
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  font-size: 0.8rem;
  white-space: pre-wrap;
}

:deep(.stacktrace-html) {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
  font-size: 0.8rem;
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

:deep(.st-exception-type) {
  color: #e11d48; /* rose/red for exception types */
  font-weight: bold;
}

:deep(.st-exception-msg) {
  color: #1e293b; /* dark slate for light mode */
}

html.dark-mode :deep(.st-exception-type) {
  color: #fb7185; /* lighter rose for dark mode */
}

html.dark-mode :deep(.st-exception-msg) {
  color: #cbd5e1; /* slate gray/light for dark mode */
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
  font-size: 0.8rem;
  overflow-x: auto;
}

/* Request parameters Table style */
.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
}

.data-table th,
.data-table td {
  padding: 0.4rem 0.6rem;
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
  font-size: 0.8rem;
  white-space: pre-wrap;
  word-break: break-all;
}

.param-card {
  padding: 0;
}

.param-header {
  padding: 0.4rem 0.8rem;
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
  max-height: 350px;
  font-size: 0.8rem;
  white-space: pre-wrap;
}

/* Skeletons */
.loading-card {
  display: flex;
  flex-direction: column;
}

.skeleton-title {
  height: 25px;
  width: 40%;
  background: var(--border-color);
  border-radius: 4px;
}

.skeleton-para {
  height: 18px;
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

/* Unified Header Card styling */
.unified-header-card {
  padding: 0 !important;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.unified-header-card.error {
  border-left: 4px solid var(--error-color);
}
.unified-header-card.warning {
  border-left: 4px solid var(--warning-color);
}
.unified-header-card.success {
  border-left: 4px solid var(--success-color);
}
.unified-header-card.info {
  border-left: 4px solid var(--primary-color);
}

.action-bar-section {
  padding: 0.6rem 0.8rem;
}

.metadata-section {
  background-color: #f0f9ff;
  border-top: 1px solid #bae6fd;
  padding: 0.6rem 0.8rem;
}

html.dark-mode .metadata-section {
  background-color: #0c4a6e;
  border-top-color: #0284c7;
}

.metadata-grid {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
}

.metadata-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.metadata-grid-cols {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.35rem 1rem;
}

.metadata-item.span-full {
  grid-column: span 3;
}

.metadata-item {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  min-width: 120px;
}

.meta-label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  color: #0369a1;
}

html.dark-mode .meta-label {
  color: #38bdf8;
}

.meta-value {
  font-size: 0.8rem;
  color: var(--text-color);
  word-break: break-all;
}

.method-badge {
  background-color: var(--primary-color);
  color: white;
  padding: 0.05rem 0.3rem;
  border-radius: 4px;
  font-size: 0.65rem;
  font-weight: 700;
  margin-right: 0.4rem;
  text-transform: uppercase;
}

.method-badge.error {
  background-color: var(--error-color);
}
.method-badge.warning {
  background-color: var(--warning-color);
}
.method-badge.success {
  background-color: var(--success-color);
}

.metadata-sidebar {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  border-left: 1px solid #bae6fd;
  padding-left: 1rem;
}

html.dark-mode .metadata-sidebar {
  border-left-color: #0284c7;
}

.client-icon-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background-color: rgba(255, 255, 255, 0.4);
  border: 1px solid rgba(2, 132, 199, 0.15);
  border-radius: 6px;
  padding: 0.35rem 0.5rem;
  min-width: 65px;
  text-align: center;
}

html.dark-mode .client-icon-card {
  background-color: rgba(0, 0, 0, 0.2);
  border-color: rgba(2, 132, 199, 0.3);
}

.client-icon-card i {
  font-size: 1.25rem;
  margin-bottom: 0.2rem;
}

.client-label {
  font-size: 0.65rem;
  font-weight: 600;
  color: var(--text-color);
}

/* Icon Colors */
.pi.os-icon-windows {
  color: #0078d4;
}
.pi.os-icon-macintosh, .pi.os-icon-iphone, .pi.os-icon-ipad {
  color: #555555;
}
html.dark-mode .pi.os-icon-macintosh, html.dark-mode .pi.os-icon-iphone, html.dark-mode .pi.os-icon-ipad {
  color: #f1f5f9;
}
.pi.os-icon-android {
  color: #3ddc84;
}
.pi.os-icon-linux {
  color: #e95420;
}
.pi.browser-icon-chrome {
  color: #4285f4;
}
.pi.browser-icon-firefox {
  color: #ff7139;
}
.pi.browser-icon-safari, .pi.browser-icon-androidbrowser {
  color: #0070c9;
}
.pi.browser-icon-edge {
  color: #0078d4;
}
.pi.browser-icon-opera {
  color: #cc0f35;
}
.pi.browser-icon-bot {
  color: #64748b;
}
</style>
