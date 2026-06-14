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

    <!-- Opacity transition on loading new error details -->
    <div v-if="error" class="detail-content-wrapper">
      <transition name="fade-fast" mode="out-in">
        <div :key="error.id || props.id" class="detail-transition-container">
          
          <!-- Unified Error Details Header Section -->
          <div class="unified-header-section mb-4">
            <!-- Action Bar Row -->
            <div class="action-bar-section">
              <div class="action-top">
                <span class="status-code-badge" :class="getSeverityClass(error.statusCode)">
                  {{ error.statusCode || '500' }}
                </span>
                <div class="error-meta">
                  <div class="exception-type-tag font-mono">{{ error.type }}</div>
                  <h1 class="error-message-detail">{{ error.message }}</h1>
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

          <!-- Detail Tabs Container -->
          <div class="detail-card">
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
                    <span class="info-value font-bold">{{ error.message }}</span>
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
                    <div v-for="(paramEntry, idx) in error.params" :key="idx" class="param-card mb-3">
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
                    <p>No method parameters logged for this error.</p>
                  </div>
                </div>
              </div>

              <!-- SQL Log Tab -->
              <div v-if="activeTab === 'sqllog'" class="tab-pane">
                <div class="sqllog-container">
                  <div v-if="sqlEntries && sqlEntries.length > 0">
                    <div v-for="(sqlEntry, idx) in sqlEntries" :key="idx" class="sql-card mb-3">
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

        </div>
      </transition>
    </div>

    <!-- Loading Placeholder -->
    <div class="loading-container" v-else>
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
import axios from 'axios';

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
  return dayjs(time).format('YYYY-MM-DD HH:mm:ss.SSS');
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

  let processed = html.replace(/# caller: @([^\r\n]+)/g, (match, path) => {
    return `<span class="st-caller-line"># caller: <span class="st-caller-path">@${path}</span></span>`;
  });

  const lines = processed.split('\n').map(l => l.replace(/\r$/, ''));
  const formattedLines = lines.map(line => {
    if (line.includes('st-frame')) {
      return line;
    }

    const excMatch = line.match(/^(\s*--->\s*|\s*---&gt;\s*)?([A-Za-z0-9_.]*Exception):\s*(.*)/);
    if (excMatch) {
      const arrow = excMatch[1] || '';
      const excType = excMatch[2];
      const msg = excMatch[3];
      const highlightedArrow = arrow ? `<span class="st-arrow text-muted">${arrow}</span>` : '';
      return `<span class="st-exception-line">${highlightedArrow}<span class="st-exception-type">${excType}</span>: <span class="st-exception-msg font-bold">${msg}</span></span>`;
    }

    if (line.trim().startsWith('File name:') || line.trim().startsWith('FileNotFoundException:') || line.trim().includes('Assembly:')) {
      return `<span class="st-diagnostic-line text-muted">${line}</span>`;
    }

    return line;
  });

  return formattedLines.join('\n');
};

const formatRawStackTrace = (text) => {
  if (!text) return '';
  let escaped = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  const lines = escaped.split('\n').map(l => l.replace(/\r$/, ''));
  const formattedLines = lines.map(line => {
    const atMatch = line.match(/^(\s*at\s+)([A-Za-z0-9_.\+<>`\[\]]+)\(([^)]*)\)(?:\s+in\s+(.*?):line\s+(\d+))?/);
    if (atMatch) {
      const prefix = atMatch[1];
      const fullMethod = atMatch[2];
      const paramsText = atMatch[3];
      const filePath = atMatch[4];
      const lineNum = atMatch[5];

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

    const excMatch = line.match(/^(\s*--->\s*|\s*---&gt;\s*)?([A-Za-z0-9_.]*Exception):\s*(.*)/);
    if (excMatch) {
      const arrow = excMatch[1] || '';
      const excType = excMatch[2];
      const msg = excMatch[3];
      const highlightedArrow = arrow ? `<span class="st-arrow text-muted">${arrow}</span>` : '';
      return `<span class="st-exception-line">${highlightedArrow}<span class="st-exception-type">${excType}</span>: <span class="st-exception-msg font-bold">${msg}</span></span>`;
    }

    if (line.trim().startsWith('File name:') || line.trim().startsWith('FileNotFoundException:') || line.trim().includes('Assembly:')) {
      return `<span class="st-diagnostic-line text-muted">${line}</span>`;
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
.error-detail-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 1rem 1.5rem 1.5rem 1.5rem;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-color);
}

.breadcrumb {
  display: flex;
  align-items: center;
  font-size: 0.85rem;
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
  color: var(--text-light);
  font-size: 0.7rem;
}

.breadcrumb-current {
  color: var(--text-color);
  font-weight: 500;
}

.detail-content-wrapper {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  margin-top: 1rem;
}

.detail-transition-container {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

/* Unified Header Section */
.unified-header-section {
  display: flex;
  flex-direction: column;
  border: none;
  background: transparent;
}

.action-bar-section {
  padding-bottom: 1.25rem;
  border-bottom: 1px solid var(--border-color);
}

.action-top {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
}

.status-code-badge {
  padding: 0.35rem 0.75rem;
  border-radius: 0;
  font-size: 1.1rem;
  font-weight: 700;
  font-family: var(--font-mono);
  min-width: 50px;
  text-align: center;
  color: white;
}

.status-code-badge.error {
  background-color: var(--error-color);
}

.status-code-badge.warning {
  background-color: var(--warning-color);
}

.status-code-badge.success {
  background-color: var(--success-color);
}

.status-code-badge.info {
  background-color: var(--primary-color);
}

.error-meta {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.exception-type-tag {
  display: inline-block;
  font-family: var(--font-mono);
  font-size: 0.8rem;
  font-weight: 600;
  background-color: var(--border-color);
  color: var(--text-color);
  padding: 0.2rem 0.5rem;
  margin-bottom: 0.5rem;
  align-self: flex-start;
  word-break: break-all;
}

.error-message-detail {
  font-size: 16px;
  font-weight: 500;
  line-height: 1.5;
  color: var(--text-color);
  margin: 0;
}

.action-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.btn {
  padding: 0.35rem 0.75rem;
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

.btn-icon {
  width: 28px;
  height: 28px;
  padding: 0;
}

.btn-sm {
  padding: 0.25rem 0.5rem;
  font-size: 0.75rem;
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

.btn-danger:hover {
  filter: brightness(0.9);
}

/* Metadata Section */
.metadata-section {
  padding: 1.25rem 0;
  border-bottom: 1px solid var(--border-color);
  background: transparent;
}

.metadata-grid {
  display: flex;
  justify-content: space-between;
  gap: 1.5rem;
}

.metadata-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.metadata-grid-cols {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.75rem 1rem;
}

.metadata-item.span-full {
  grid-column: span 3;
}

.metadata-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  min-width: 100px;
}

.meta-label, .info-label {
  font-size: 10px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: var(--text-light) !important;
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
  border-left: 1px solid var(--border-color);
  padding-left: 1rem;
}

.client-icon-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background-color: transparent;
  border: 1px solid var(--border-color);
  border-radius: 0;
  padding: 0.35rem 0.5rem;
  min-width: 65px;
  text-align: center;
}

.client-icon-card i {
  font-size: 1.1rem;
  margin-bottom: 0.2rem;
}

.client-label {
  font-size: 0.65rem;
  font-weight: 600;
  color: var(--text-color);
}

/* Detail Card & Tabs */
.detail-card {
  padding: 0;
  display: flex;
  flex-direction: column;
  flex: 1;
  margin-bottom: 1.5rem;
}

.tabs-nav {
  display: flex;
  background: transparent;
  border-bottom: 1px solid var(--border-color);
  padding: 0;
  overflow-x: auto;
  margin-bottom: 1rem;
}

.tab-btn {
  background: none;
  border: none;
  padding: 0.5rem 1rem;
  color: var(--text-light);
  cursor: pointer;
  font-weight: 500;
  font-size: 0.85rem;
  border-bottom: 2px solid transparent;
  transition: all 150ms ease-in-out;
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.tab-btn:hover {
  color: var(--text-color);
  border-bottom-color: var(--border-color);
}

.tab-btn.active {
  color: var(--primary-color) !important;
  border-bottom-color: var(--primary-color) !important;
}

.tab-content {
  padding: 0.5rem 0;
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
  padding-bottom: 0.75rem;
}

.info-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.info-value {
  font-size: 0.85rem;
  color: var(--text-color);
  word-break: break-word;
}

/* Stack Trace */
.stacktrace-container {
  background-color: var(--bg-color);
  color: var(--text-color);
  border: 1px solid var(--border-color);
  padding: 1rem;
  border-radius: 0;
  overflow: auto;
  max-height: 500px;
}

:deep(.stacktrace-html) {
  font-family: var(--font-mono);
  font-size: 0.8rem;
  line-height: 1.6;
  white-space: pre-wrap;
}

:deep(.stacktrace-html a) {
  color: var(--primary-color);
  text-decoration: underline;
}

:deep(.st-frame) {
  display: block;
  padding: 0.1rem 0;
}

:deep(.st-type) {
  color: #2563eb;
  font-weight: 600;
}

:deep(.st-method) {
  color: #0d9488;
  font-weight: 700;
}

:deep(.params) {
  color: var(--text-light);
}

:deep(.st-param-type) {
  color: #1d4ed8;
}

:deep(.st-param-name) {
  color: var(--text-light);
  font-style: italic;
}

:deep(.st-file) {
  color: #c026d3;
  margin-left: 0.5rem;
}

:deep(.st-line) {
  color: #c026d3;
  font-weight: bold;
}

:deep(.st-caller-line) {
  display: block;
  background-color: var(--border-color);
  color: var(--text-color);
  padding: 0.5rem 0.75rem;
  border-radius: 0;
  border: 1px solid var(--border-color);
  margin-bottom: 0.75rem;
  font-weight: 600;
  font-family: var(--font-mono);
}

:deep(.st-caller-path) {
  color: var(--error-color);
  font-weight: 700;
  text-decoration: underline;
}

:deep(.st-exception-type) {
  color: var(--error-color);
  font-weight: bold;
}

:deep(.st-exception-msg) {
  color: var(--text-color);
}

html.dark-mode :deep(.st-type) {
  color: #60a5fa;
}

html.dark-mode :deep(.st-method) {
  color: #2dd4bf;
}

html.dark-mode :deep(.st-param-type) {
  color: #93c5fd;
}

html.dark-mode :deep(.st-param-name) {
  color: #cbd5e1;
}

html.dark-mode :deep(.st-file) {
  color: #f472b6;
}

html.dark-mode :deep(.st-line) {
  color: #f472b6;
}

html.dark-mode :deep(.st-exception-type) {
  color: #fda4af;
}

html.dark-mode :deep(.st-exception-msg) {
  color: #f1f5f9;
}

/* Source Context Code */
.source-card {
  border-top: 1px solid var(--border-color);
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
  font-family: var(--font-mono);
}

.source-code {
  margin: 0;
  padding: 1rem;
  background-color: var(--bg-color);
  color: var(--text-color);
  font-size: 0.8rem;
  overflow-x: auto;
  border: 1px solid var(--border-color);
  font-family: var(--font-mono);
}

/* Request parameters Table style */
.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
  margin-top: 0.5rem;
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
  text-transform: uppercase;
  font-size: 0.7rem;
  letter-spacing: 0.05em;
}

/* Request Body and parameters styling */
.body-card {
  background-color: var(--bg-color);
  border: 1px solid var(--border-color);
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
  border: 1px solid var(--border-color);
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
  border: 1px solid var(--border-color);
  overflow: auto;
  max-height: 350px;
  font-size: 0.8rem;
  white-space: pre-wrap;
}

/* Skeletons & placeholders */
.loading-container {
  display: flex;
  flex-direction: column;
  padding: 2rem;
}

.skeleton-title {
  height: 25px;
  width: 40%;
  background: var(--border-color);
}

.skeleton-para {
  height: 18px;
  background: var(--border-color);
}

/* SQL Log Tab Styling */
.sql-card {
  border: 1px solid var(--border-color);
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
  background-color: var(--bg-color);
  color: var(--text-color);
  padding: 1rem;
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 0.85rem;
  max-height: 300px;
  overflow-y: auto;
  font-family: var(--font-mono);
  border-top: 1px solid var(--border-color);
}

.badge-primary {
  background-color: var(--border-color);
  color: var(--text-color);
  padding: 0.2rem 0.5rem;
  font-size: 0.7rem;
  font-weight: 700;
  font-family: var(--font-mono);
}

.badge-warning {
  background-color: var(--warning-color);
  color: white;
  padding: 0.2rem 0.5rem;
  font-size: 0.7rem;
  font-weight: 700;
  font-family: var(--font-mono);
}

/* Fast Opacity Fade transition */
.fade-fast-enter-active,
.fade-fast-leave-active {
  transition: opacity 150ms ease-in-out;
}

.fade-fast-enter-from,
.fade-fast-leave-to {
  opacity: 0;
}

/* Icon Colors */
.pi.os-icon-windows {
  color: #0078d4;
}
.pi.os-icon-macintosh, .pi.os-icon-iphone, .pi.os-icon-ipad {
  color: var(--text-color);
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
.pi.browser-icon-safari {
  color: #0070c9;
}
.pi.browser-icon-edge {
  color: #0078d4;
}
.pi.browser-icon-opera {
  color: #cc0f35;
}
.pi.browser-icon-bot {
  color: var(--text-light);
}

.section-title {
  font-family: var(--font-mono);
  font-size: 0.85rem;
  font-weight: 700;
  margin: 1.5rem 0 0.5rem 0;
  color: var(--text-color);
}
</style>
