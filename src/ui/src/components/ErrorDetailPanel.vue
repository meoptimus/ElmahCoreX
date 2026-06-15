<template>
  <div class="error-detail-panel">
    <!-- Panel Header with Navigation and Close Button -->
    <div class="panel-header mb-4">
      <div class="header-left">
        <span class="status-code-badge" :class="[getSeverityClass(error?.statusCode), 'status-' + error?.statusCode]">
          {{ error?.statusCode || '500' }}
        </span>
        <span v-if="error" class="exception-type-tag font-mono">
          {{ error.type }}
        </span>
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
          <span class="nav-position-text font-mono">
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

    <!-- Opacity transition on loading new error details -->
    <div v-if="error" class="detail-content-wrapper">
      <transition name="fade-fast" mode="out-in">
        <div :key="error.id || props.id" class="detail-transition-container">
          
          <!-- Error message title -->
          <h1 class="error-message-detail">{{ error.message }}</h1>

          <!-- Action row below title, before tabs -->
          <div class="action-buttons mb-4">
            <!-- Mark Reviewed Toggle -->
            <button 
              class="btn btn-sm btn-neutral" 
              @click="toggleReviewed"
            >
              <i :class="error.isReviewed ? 'pi pi-check-circle mr-1' : 'pi pi-circle mr-1'"></i>
              {{ error.isReviewed ? 'Reviewed' : 'Mark Reviewed' }}
            </button>

            <!-- Copy GUID -->
            <button class="btn btn-neutral btn-sm" @click="copyToClipboard(props.id, 'Error ID copied!')" title="Copy Error GUID">
              <i class="pi pi-copy mr-1"></i> Copy GUID
            </button>

            <!-- Share -->
            <button class="btn btn-neutral btn-sm" @click="shareLink" title="Copy Share Link">
              <i class="pi pi-share-alt mr-1"></i> Share
            </button>

            <!-- Delete -->
            <button class="btn btn-delete-terracotta btn-sm" @click="deleteError">
              <i class="pi pi-trash mr-1"></i> Delete
            </button>
          </div>

          <!-- Metadata Section -->
          <div class="metadata-section mb-4">
            <div class="metadata-grid">
              <!-- Row 1 -->
              <div class="metadata-row">
                <div class="metadata-item">
                  <span class="meta-label">When</span>
                  <span class="meta-value font-mono">{{ formatTime(error.time) }}</span>
                </div>
                <div class="metadata-item">
                  <span class="meta-label">Client IP</span>
                  <span class="meta-value font-mono select-all">{{ error.client || 'N/A' }}</span>
                </div>
                <div class="metadata-item">
                  <span class="meta-label">User</span>
                  <span class="meta-value font-mono select-all">{{ error.user || 'N/A' }}</span>
                </div>
              </div>
              
              <!-- Row 2 -->
              <div class="metadata-row">
                <div class="metadata-item">
                  <span class="meta-label">Host</span>
                  <span class="meta-value font-mono">{{ error.hostName || 'N/A' }}</span>
                </div>
                <div class="metadata-item">
                  <span class="meta-label">Application</span>
                  <span class="meta-value font-mono">{{ error.applicationName || 'N/A' }}</span>
                </div>
                <div class="metadata-item">
                  <span class="meta-label">Source</span>
                  <span class="meta-value font-mono text-break">{{ error.source || 'N/A' }}</span>
                </div>
              </div>
            </div>
            
            <!-- URL row: separate full-width strip below meta grid -->
            <div class="url-row-strip" v-if="error.url">
              <span class="meta-label">URL</span>
              <span class="meta-value font-mono select-all url-value">
                <span class="method-badge" :class="error.method">{{ error.method }}</span>
                {{ error.url }}
              </span>
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
                    <span class="info-value">{{ error.message }}</span>
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


const hasItems = (obj) => {
  return obj && Object.keys(obj).length > 0;
};

const formatTime = (time) => {
  return dayjs(time).format('YYYY-MM-DD HH:mm:ss.SSS');
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
  padding: 16px 20px; /* Panel padding: 16px 20px */
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #e8e6e0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.header-right-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn-close-x {
  background: none;
  border: none;
  color: #aaa9a3;
  cursor: pointer;
  padding: 0.25rem;
  border-radius: 3px;
  font-size: 1.1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
}

.btn-close-x:hover {
  background-color: #eeecea;
  color: #1a1a2e;
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

.status-code-badge {
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 11px;
  padding: 2px 6px;
  color: white;
  min-width: 32px;
  text-align: center;
  white-space: nowrap;
  background-color: #aaa9a3;
  border-radius: 3px;
}

.status-code-badge.error,
.status-code-badge.status-500 {
  background-color: #d94f4f !important; /* HTTP 500 badge ONLY */
}

.exception-type-tag {
  display: inline-block;
  font-family: var(--font-mono);
  font-size: 11px;
  background-color: #eeecea;
  border: 1px solid #dddbd4;
  color: #5f5e5a;
  padding: 2px 6px;
  border-radius: 3px;
  word-break: break-all;
}

.error-message-detail {
  font-size: 15px;
  font-weight: 500;
  line-height: 1.5;
  color: #1a1a2e;
  margin: 1rem 0;
  max-width: 700px;
}

.action-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
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

.btn-icon {
  width: 26px;
  height: 26px;
  padding: 0;
}

.btn-sm {
  padding: 3px 6px;
  font-size: 10px;
}

.btn-secondary {
  background-color: #ffffff;
  border-color: #dddbd4;
  color: #5f5e5a;
  border-radius: 3px;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #eeecea;
}

.btn-neutral {
  border: 1px solid #dddbd4;
  background: #fff;
  color: #5f5e5a;
  border-radius: 3px;
}

.btn-neutral:hover {
  background-color: #eeecea;
}

.btn-delete-terracotta {
  background-color: #fdf2ef;
  border: 1px solid #e8c4ba;
  color: #b05a4a;
  border-radius: 3px;
}

.btn-delete-terracotta:hover {
  background-color: #f9e2db;
}

.nav-position-text {
  font-size: 11px;
  color: #5f5e5a;
}

/* Metadata Section */
.metadata-section {
  padding: 0;
  border: 1px solid #e8e6e0;
  background: transparent;
}

.metadata-grid {
  display: flex;
  flex-direction: column;
}

.metadata-row {
  display: flex;
  width: 100%;
  border-bottom: 1px solid #e8e6e0;
}

.metadata-row:last-child {
  border-bottom: none;
}

.metadata-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding: 8px 12px;
  border-right: 1px solid #e8e6e0;
  min-width: 0;
}

.metadata-item:last-child {
  border-right: none;
}

.meta-label, .info-label {
  font-size: 10px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #aaa9a3 !important;
}

.meta-value {
  font-size: 13px;
  font-family: var(--font-mono);
  color: #1a1a2e;
  word-break: break-all;
}

.url-row-strip {
  padding: 8px 12px;
  border-top: 1px solid #e8e6e0;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.url-value {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 13px;
  color: #1a1a2e;
}

.method-badge {
  padding: 2px 6px;
  font-size: 11px;
  font-weight: 600;
  font-family: var(--font-mono);
  border-radius: 3px;
  text-transform: uppercase;
  background-color: #eeecea;
  color: #5f5e5a;
}

.method-badge.GET {
  background-color: #e3edf8 !important;
  color: #2b5fa0 !important;
}

.metadata-sidebar {
  display: flex;
  gap: 0.35rem;
  align-items: center;
  padding: 8px 12px;
}

.client-icon-card {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  background-color: transparent;
  border: 1px solid #dddbd4;
  border-radius: 3px;
  padding: 2px 6px;
}

.client-icon-card i {
  font-size: 11px;
}

.client-label {
  font-size: 10px;
  color: #5f5e5a;
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
  border-bottom: 1px solid #e8e6e0;
  padding: 0;
  overflow-x: auto;
  margin-bottom: 1rem;
}

.tab-btn {
  background: none;
  border: none;
  padding: 0.5rem 1rem;
  color: #888780;
  cursor: pointer;
  font-weight: 500;
  font-size: 12px; /* Inactive: 12px */
  border-bottom: 2px solid transparent;
  transition: all 150ms ease-in-out;
  display: flex;
  align-items: center;
  white-space: nowrap;
  border-radius: 0 !important;
}

.tab-btn i {
  font-size: 13px !important;
  margin-right: 4px;
}

.tab-btn:hover {
  color: #1a1a2e;
}

.tab-btn.active {
  color: #4a7fc1 !important;
  border-bottom-color: #4a7fc1 !important;
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
  gap: 0.75rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  border-bottom: 1px solid #e8e6e0;
  padding-bottom: 0.75rem;
}

.info-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.info-value {
  font-size: 13px;
  color: #1a1a2e;
  word-break: break-word;
}

.info-value.font-mono {
  font-family: var(--font-mono) !important;
  color: #4a5568 !important;
}

/* Stack Trace */
.stacktrace-container {
  background-color: #f7f6f2; /* matches warm parchment background */
  color: #1a1a2e;
  border: 1px solid #e8e6e0;
  padding: 1rem;
  border-radius: 3px;
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
  color: #4a7fc1;
  text-decoration: underline;
}

:deep(.st-frame) {
  display: block;
  padding: 0.1rem 0;
}

:deep(.st-type) {
  color: #2b5fa0;
  font-weight: 600;
}

:deep(.st-method) {
  color: #0d9488;
  font-weight: 700;
}

:deep(.params) {
  color: #888780;
}

:deep(.st-param-type) {
  color: #1d4ed8;
}

:deep(.st-param-name) {
  color: #888780;
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
  background-color: #eeecea;
  color: #1a1a2e;
  padding: 0.5rem 0.75rem;
  border-radius: 3px;
  border: 1px solid #dddbd4;
  margin-bottom: 0.75rem;
  font-weight: 600;
  font-family: var(--font-mono);
}

:deep(.st-caller-path) {
  color: #b05a4a;
  font-weight: 700;
  text-decoration: underline;
}

:deep(.st-exception-type) {
  color: #b05a4a;
  font-weight: bold;
}

:deep(.st-exception-msg) {
  color: #1a1a2e;
}

/* Source Context Code */
.source-card {
  border-top: 1px solid #e8e6e0;
  padding: 0;
}

.source-header {
  background-color: #eeecea;
  padding: 0.5rem 1rem;
  border-bottom: 1px solid #e8e6e0;
  display: flex;
  justify-content: space-between;
  font-size: 0.8rem;
  color: #888780;
  font-family: var(--font-mono);
}

.source-code {
  margin: 0;
  padding: 1rem;
  background-color: #f7f6f2;
  color: #1a1a2e;
  font-size: 0.8rem;
  overflow-x: auto;
  border: 1px solid #e8e6e0;
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
  border-bottom: 1px solid #e8e6e0;
}

.data-table th {
  text-align: left;
  background-color: #eeecea;
  color: #5f5e5a;
  font-weight: 600;
  text-transform: uppercase;
  font-size: 0.7rem;
  letter-spacing: 0.05em;
}

/* Request Body and parameters styling */
.body-card {
  background-color: #f7f6f2;
  border: 1px solid #e8e6e0;
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
  border: 1px solid #e8e6e0;
}

.param-header {
  padding: 0.4rem 0.8rem;
  background-color: #eeecea;
  border-bottom: 1px solid #e8e6e0;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.xml-raw {
  background-color: #f7f6f2;
  padding: 1rem;
  border: 1px solid #e8e6e0;
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
  background: #e8e6e0;
}

.skeleton-para {
  height: 18px;
  background: #e8e6e0;
}

/* SQL Log Tab Styling */
.sql-card {
  border: 1px solid #e8e6e0;
  overflow: hidden;
}

.sql-header {
  background-color: #eeecea;
  border-bottom: 1px solid #e8e6e0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 1rem;
}

.sql-text {
  background-color: #f7f6f2;
  color: #1a1a2e;
  padding: 1rem;
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 0.85rem;
  max-height: 300px;
  overflow-y: auto;
  font-family: var(--font-mono);
  border-top: 1px solid #e8e6e0;
}

.badge-primary {
  background-color: #eeecea;
  color: #5f5e5a;
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
  color: #1a1a2e;
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
  color: #888780;
}

.section-title {
  font-family: var(--font-mono);
  font-size: 0.85rem;
  font-weight: 700;
  margin: 1.5rem 0 0.5rem 0;
  color: #1a1a2e;
}
</style>
