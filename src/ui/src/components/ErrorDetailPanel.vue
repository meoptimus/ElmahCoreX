<template>
  <div class="error-detail-panel">
    <!-- Panel Header with Navigation and Close Button -->
    <div class="panel-header mb-4">
      <div class="header-left">
        <button class="btn-back-mobile" @click="emit('close')" title="Back to list">
          <i class="pi pi-arrow-left"></i>
          <span>Back</span>
        </button>
        <span class="header-title-label font-mono">Error Details</span>
      </div>

      <div class="header-right-actions">
        <!-- Mark Reviewed Toggle -->
        <button 
          v-if="error"
          class="btn btn-secondary btn-sm" 
          @click="toggleReviewed"
          :title="error.isReviewed ? 'Mark as Open' : 'Mark as Reviewed'"
        >
          <i :class="error.isReviewed ? 'pi pi-check-circle text-success' : 'pi pi-circle'"></i>
          <span class="btn-reviewed-text ml-1">{{ error.isReviewed ? 'Reviewed' : 'Review' }}</span>
        </button>

        <!-- Delete -->
        <button 
          v-if="error"
          class="btn btn-delete-terracotta btn-sm btn-icon" 
          @click="deleteError"
          title="Delete error log"
        >
          <i class="pi pi-trash"></i>
        </button>

        <!-- Navigation Buttons -->
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
          
          <!-- Large Status Badge and Error message title / exception type -->
          <div class="error-header-main mb-4">
            <div class="status-badge-large" :class="[getSeverityClass(error?.statusCode), 'status-' + error?.statusCode]">
              {{ error?.statusCode || '500' }}
            </div>
            <div class="error-header-content">
              <h1 class="error-message-detail">{{ error.message }}</h1>
              
              <div class="exception-and-actions">
                <div class="exception-type-detail">{{ error.type }}</div>
                
                <div class="action-buttons">
                  <!-- Copy GUID -->
                  <button class="btn-action btn-icon" @click="copyToClipboard(props.id, 'Error ID copied!')" title="Copy Error GUID">
                    <i class="pi pi-copy"></i>
                  </button>

                  <!-- XML Link -->
                  <button class="btn-action btn-text" @click="openXml" title="Open XML in new tab">
                    xml
                  </button>

                  <!-- JSON Link -->
                  <button class="btn-action btn-text" @click="openJson" title="Open JSON in new tab">
                    json
                  </button>

                  <!-- External share link -->
                  <button class="btn-action btn-icon" @click="shareLink" title="Copy shareable link">
                    <i class="pi pi-external-link"></i>
                  </button>
                </div>
              </div>
            </div>
          </div>

          <!-- Metadata Section on light blue card -->
          <div class="metadata-card mb-4">
            <div class="metadata-table">
              <div class="metadata-row">
                <div class="metadata-key">When</div>
                <div class="metadata-val font-mono">{{ formatTime(error.time) }}</div>
              </div>
              <div class="metadata-row" v-if="error.url">
                <div class="metadata-key">URL</div>
                <div class="metadata-val font-mono">
                  <span class="method-badge" :class="error.method">{{ error.method || 'GET' }}</span>
                  <span class="url-text">{{ error.url }}</span>
                  <a :href="error.url" target="_blank" class="ext-link" title="Open URL in new tab">
                    <i class="pi pi-external-link"></i>
                  </a>
                </div>
              </div>
              <div class="metadata-row" v-if="error.client">
                <div class="metadata-key">Client IP</div>
                <div class="metadata-val font-mono">
                  <a :href="`https://ipinfo.io/${error.client}`" target="_blank" class="ip-link" title="IP Info lookup">
                    {{ error.client }}
                    <i class="pi pi-external-link ext-link-icon"></i>
                  </a>
                </div>
              </div>
              <div class="metadata-row" v-if="error.applicationName">
                <div class="metadata-key">Application</div>
                <div class="metadata-val">{{ error.applicationName }}</div>
              </div>
              <div class="metadata-row" v-if="error.source">
                <div class="metadata-key">Source</div>
                <div class="metadata-val">{{ error.source }}</div>
              </div>
              <div class="metadata-row" v-if="error.user">
                <div class="metadata-key">User</div>
                <div class="metadata-val">{{ error.user }}</div>
              </div>
            </div>

            <!-- OS & Browser Circular Badges on the right -->
            <div class="device-badges" v-if="osName || browserName">
              <div class="device-badge" v-if="osName">
                <div class="badge-circle os-badge">
                  <span v-if="getOsSvg(osName)" v-html="getOsSvg(osName)"></span>
                  <template v-else>
                    <div class="os-text-top">{{ getOsTopText(osName) }}</div>
                    <div class="os-text-bottom">OS</div>
                  </template>
                </div>
                <div class="badge-label">{{ osName }}</div>
              </div>
              <div class="device-badge" v-if="browserName">
                <div class="badge-circle browser-badge">
                  <span v-html="getBrowserSvg(browserName)"></span>
                </div>
                <div class="badge-label">{{ browserName }}</div>
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
                {{ tab.name }}
                <span class="tab-badge" v-if="tab.count !== null && tab.count !== undefined">{{ tab.count }}</span>
              </button>
            </div>

            <!-- Tabs Content -->
            <div class="tab-content">
              <!-- 1. Stack Trace Tab -->
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

              <!-- 2. Header Tab -->
              <div v-if="activeTab === 'header'" class="tab-pane">
                <div v-if="hasItems(error.header)">
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
                <div v-else class="empty-state">
                  <i class="pi pi-info-circle empty-icon"></i>
                  <p>No HTTP request headers logged for this error.</p>
                </div>
              </div>

              <!-- 3. Cookies Tab -->
              <div v-if="activeTab === 'cookies'" class="tab-pane">
                <div v-if="hasItems(error.cookies)">
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
                <div v-else class="empty-state">
                  <i class="pi pi-info-circle empty-icon"></i>
                  <p>No cookies logged for this error.</p>
                </div>
              </div>

              <!-- 4. Connection Tab -->
              <div v-if="activeTab === 'connection'" class="tab-pane">
                <div v-if="hasItems(error.connection || error.Connection)">
                  <table class="data-table">
                    <thead>
                      <tr>
                        <th>Connection Parameter</th>
                        <th>Value</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(val, key) in (error.connection || error.Connection)" :key="key">
                        <td class="font-mono font-bold">{{ key }}</td>
                        <td class="font-mono select-all">{{ val }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <div v-else class="empty-state">
                  <i class="pi pi-info-circle empty-icon"></i>
                  <p>No connection parameters logged for this error.</p>
                </div>
              </div>

              <!-- 5. Server Variables Tab -->
              <div v-if="activeTab === 'serverVariables'" class="tab-pane">
                <div v-if="hasItems(error.serverVariables)">
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
                <div v-else class="empty-state">
                  <i class="pi pi-info-circle empty-icon"></i>
                  <p>No server variables logged for this error.</p>
                </div>
              </div>

              <!-- 6. Query String Tab -->
              <div v-if="activeTab === 'querystring'" class="tab-pane">
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

              <!-- 7. Form Fields Tab -->
              <div v-if="activeTab === 'form'" class="tab-pane">
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

              <!-- 8. Request Body Tab -->
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

              <!-- 9. Parameters Tab -->
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
                </div>
              </div>

              <!-- 10. SQL Log Tab -->
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
                </div>
              </div>

              <!-- 11. Raw XML Tab -->
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
const activeTab = ref('stacktrace');

const elmahRoot = window.$elmah_root || '/elmah';
const cleanRoot = '/' + elmahRoot.replace(/^\/|\/$/g, '');

const xmlUrl = computed(() => `${cleanRoot}/xml?id=${props.id}`);
const jsonUrl = computed(() => `${cleanRoot}/json?id=${props.id}`);

const openXml = () => {
  window.open(xmlUrl.value, '_blank');
};

const openJson = () => {
  window.open(jsonUrl.value, '_blank');
};

const osName = computed(() => error.value?.os || error.value?.Os || '');
const browserName = computed(() => error.value?.browser || error.value?.Browser || '');

const getOsTopText = (os) => {
  if (!os) return 'sys';
  const name = os.toLowerCase();
  if (name.includes('mac') || name.includes('osx') || name.includes('ios')) return 'mac';
  if (name.includes('win')) return 'win';
  if (name.includes('linux')) return 'lin';
  if (name.includes('android')) return 'and';
  return os.substring(0, 3).toLowerCase();
};

const getOsSvg = (os) => {
  if (!os) return null;
  const name = os.toLowerCase();
  if (name.includes('mac') || name.includes('osx') || name.includes('ios') || name.includes('iphone') || name.includes('ipad')) {
    return `<svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" style="color: white;">
      <path d="M18.71,19.5 C17.88,20.74 17.0,21.95 15.66,21.97 C14.32,22.0 13.89,21.18 12.37,21.18 C10.84,21.18 10.37,21.95 9.1,22.0 C7.79,22.05 6.8,20.68 5.96,19.47 C4.25,17 2.94,12.45 4.7,9.39 C5.57,7.87 7.13,6.91 8.82,6.88 C10.1,6.86 11.32,7.75 12.11,7.75 C12.89,7.75 14.37,6.68 15.92,6.84 C16.57,6.87 18.39,7.1 19.56,8.82 C19.47,8.88 17.39,10.1 17.41,12.63 C17.44,15.65 20.06,16.66 20.1,16.67 C20.08,16.74 19.67,18.11 18.71,19.5 M15.98,4.17 C16.67,3.34 17.13,2.19 17.0,1.04 C16.02,1.08 14.83,1.69 14.13,2.51 C13.53,3.21 13.0,4.39 13.15,5.5 C14.24,5.58 15.3,4.97 15.98,4.17 Z" />
    </svg>`;
  }
  if (name.includes('win')) {
    return `<svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor" style="color: white;">
      <path d="M0,3.449 L9.75,2.1 L9.75,11.25 L0,11.25 L0,3.449 Z M0,12.75 L9.75,12.75 L9.75,21.9 L0,20.55 L0,12.75 Z M11.25,1.901 L24,0 L24,11.25 L11.25,11.25 L11.25,1.901 Z M11.25,12.75 L24,12.75 L24,24 L11.25,22.099 L11.25,12.75 Z" />
    </svg>`;
  }
  if (name.includes('android')) {
    return `<svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" style="color: white;">
      <path d="M6,18 C6,19.1 6.9,20 8,20 L16,20 C17.1,20 18,19.1 18,18 L18,8 L6,8 L6,18 Z M17.2,5.2 L18.6,3.8 C18.9,3.5 18.9,3 18.6,2.7 C18.3,2.4 17.8,2.4 17.5,2.7 L15.8,4.4 C14.7,3.9 13.4,3.6 12,3.6 C10.6,3.6 9.3,3.9 8.2,4.4 L6.5,2.7 C6.2,2.4 5.7,2.4 5.4,2.7 C5.1,3 5.1,3.5 5.4,3.8 L6.8,5.2 C4,7.2 2.2,10.4 2,14 L22,14 C21.8,10.4 20,7.2 17.2,5.2 Z M9,11 C8.4,11 8,10.6 8,10 C8,9.4 8.4,9 9,9 C9.6,9 10,9.4 10,10 C10,10.6 9.6,11 9,11 Z M15,11 C14.4,11 14,10.6 14,10 C14,9.4 14.4,9 15,9 C15.6,9 16,9.4 16,10 C16,10.6 15.6,11 15,11 Z" />
    </svg>`;
  }
  if (name.includes('linux')) {
    return `<svg width="24" height="24" viewBox="0 0 16 16" fill="currentColor" style="color: white;">
      <path d="M8.996 4.497c.104-.076.1-.168.186-.158s.022.102-.098.207c-.12.104-.308.243-.46.323-.291.152-.631.336-.993.336s-.647-.167-.853-.33c-.102-.082-.186-.162-.248-.221-.11-.086-.096-.207-.052-.204.075.01.087.109.134.153.064.06.144.137.241.214.195.154.454.304.778.304s.702-.19.932-.32c.13-.073.297-.204.433-.304M7.34 3.781c.055-.02.123-.031.174-.003.011.006.024.021.02.034-.012.038-.074.032-.11.05-.032.017-.057.052-.093.054-.034 0-.086-.012-.09-.046-.007-.044.058-.072.1-.089m.581-.003c.05-.028.119-.018.173.003.041.017.106.045.1.09-.004.033-.057.046-.09.045-.036-.002-.062-.037-.093-.053-.036-.019-.098-.013-.11-.051-.004-.013.008-.028.02-.034"/>
      <path fill-rule="evenodd" d="M8.446.019c2.521.003 2.38 2.66 2.364 4.093-.01.939.509 1.574 1.04 2.244.474.56 1.095 1.38 1.45 2.32.29.765.402 1.613.115 2.465a.8.8 0 0 1 .254.152l.001.002c.207.175.271.447.329.698.058.252.112.488.224.615.344.382.494.667.48.922-.015.254-.203.43-.435.57-.465.28-1.164.491-1.586 1.002-.443.527-.99.83-1.505.871a1.25 1.25 0 0 1-1.256-.716v-.001a1 1 0 0 1-.078-.21c-.67.038-1.252-.165-1.718-.128-.687.038-1.116.204-1.506.206-.151.331-.445.547-.808.63-.5.114-1.126 0-1.743-.324-.577-.306-1.31-.278-1.85-.39-.27-.057-.51-.157-.626-.384-.116-.226-.095-.538.07-.988.051-.16.012-.398-.026-.648a2.5 2.5 0 0 1-.037-.369c0-.133.022-.265.087-.386v-.002c.14-.266.368-.377.577-.451s.397-.125.53-.258c.143-.15.27-.374.443-.56q.036-.037.073-.07c-.081-.538.007-1.105.192-1.662.393-1.18 1.223-2.314 1.811-3.014.502-.713.65-1.287.701-2.016.042-.997-.705-3.974 2.112-4.2q.168-.015.321-.013m2.596 10.866-.03.016c-.223.121-.348.337-.427.656-.08.32-.107.733-.13 1.206v.001c-.023.37-.192.824-.31 1.267s-.176.862-.036 1.128v.002c.226.452.608.636 1.051.601s.947-.304 1.36-.795c.474-.576 1.218-.796 1.638-1.05.21-.126.324-.242.333-.4.009-.157-.097-.403-.425-.767-.17-.192-.217-.462-.274-.71-.056-.247-.122-.468-.26-.585l-.001-.001c-.18-.157-.356-.17-.565-.164q-.069.001-.14.005c-.239.275-.805.612-1.197.508-.359-.09-.562-.508-.587-.918m-7.204.03H3.83c-.189.002-.314.09-.44.225-.149.158-.276.382-.445.56v.002h-.002c-.183.184-.414.239-.61.31-.195.069-.353.143-.46.35v.002c-.085.155-.066.378-.029.624.038.245.096.507.018.746v.002l-.001.002c-.157.427-.155.678-.082.822.074.143.235.22.48.272.493.103 1.26.069 1.906.41.583.305 1.168.404 1.598.305.431-.098.712-.369.75-.867v-.002c.029-.292-.195-.673-.485-1.052-.29-.38-.633-.752-.795-1.09v-.002l-.61-1.11c-.21-.286-.43-.462-.68-.5a1 1 0 0 0-.106-.008M9.584 4.85c-.14.2-.386.37-.695.467-.147.048-.302.17-.495.28a1.3 1.3 0 0 1-.74.19.97.97 0 0 1-.582-.227c-.14-.113-.25-.237-.394-.322a3 3 0 0 1-.192-.126c-.063 1.179-.85 2.658-1.226 3.511a5.4 5.4 0 0 0-.43 1.917c-.68-.906-.184-2.066.081-2.568.297-.55.343-.701.27-.649-.266.436-.685 1.13-.848 1.844-.085.372-.1.749.01 1.097.11.349.345.67.766.931.573.351.963.703 1.193 1.015s.302.584.23.777a.4.4 0 0 1-.212.22.7.7 0 0 1-.307.056l.184.235c.094.124.186.249.266.375 1.179.805 2.567.496 3.568-.218.1-.342.197-.664.212-.903.024-.474.05-.896.136-1.245s.244-.634.53-.791a1 1 0 0 1 .138-.061q.005-.045.013-.087c.082-.546.569-.572 1.18-.303.588.266.81.499.71.814h.13c.122-.398-.133-.69-.822-1.025l-.137-.06a2.35 2.35 0 0 0-.012-1.113c-.188-.79-.704-1.49-1.098-1.838-.072-.003-.065.06.081.203.363.333 1.156 1.532.727 2.644a1.2 1.2 0 0 0-.342-.043c-.164-.907-.543-1.66-.735-2.014-.359-.668-.918-2.036-1.158-2.983M7.72 3.503a1 1 0 0 0-.312.053c-.268.093-.447.286-.559.391-.022.021-.05.04-.119.091s-.172.126-.321.238q-.198.151-.13.38c.046.15.192.325.459.476.166.098.28.23.41.334a1 1 0 0 0 .215.133.9.9 0 0 0 .298.066c.282.017.49-.068.673-.173s.34-.233.518-.29c.365-.115.627-.345.709-.564a.37.37 0 0 0-.01-.309c-.048-.096-.148-.187-.318-.257h-.001c-.354-.151-.507-.162-.705-.29-.321-.207-.587-.28-.807-.279m-.89-1.122h-.025a.4.4 0 0 0-.278.135.76.76 0 0 0-.191.334 1.2 1.2 0 0 0-.051.445v.001c.01.162.041.299.102.436.05.116.109.204.183.274l.089-.065.117-.09-.023-.018a.4.4 0 0 1-.11-.161.7.7 0 0 1-.054-.22v-.01a.7.7 0 0 1 .014-.234.4.4 0 0 1 .08-.179q.056-.069.126-.073h.013a.18.18 0 0 1 .123.05c.045.04.08.09.11.162a.7.7 0 0 1 .054.22v.01a.7.7 0 0 1-.002.17 1.1 1.1 0 0 1 .317-.143 1.3 1.3 0 0 0 .002-.194V3.23a1.2 1.2 0 0 0-.102-.437.8.8 0 0 0-.227-.31.4.4 0 0 0-.268-.102m1.95-.155a.63.63 0 0 0-.394.14.9.9 0 0 0-.287.376 1.2 1.2 0 0 0-.1.51v.015q0 .079.01.152c.114.027.278.074.406.138a1 1 0 0 1-.011-.172.8.8 0 0 1 .058-.278.5.5 0 0 1 .139-.2.26.26 0 0 1 .182-.069.26.26 0 0 1 .178.081c.055.054.094.12.124.21.029.086.042.17.04.27l-.002.012a.8.8 0 0 1-.057.277c-.024.059-.089.106-.122.145.046.016.09.03.146.052a5 5 0 0 1 .248.102 1.2 1.2 0 0 0 .244-.763 1.2 1.2 0 0 0-.11-.495.9.9 0 0 0-.294-.37.64.64 0 0 0-.39-.133z"/>
    </svg>`;
  }
  return null;
};

const getBrowserSvg = (browser) => {
  if (!browser) return '';
  const name = browser.toLowerCase();
  if (name.includes('chrome')) {
    return `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: white;">
      <circle cx="12" cy="12" r="10"></circle>
      <circle cx="12" cy="12" r="4"></circle>
      <line x1="21.17" y1="8" x2="12" y2="8"></line>
      <line x1="3.95" y1="6.06" x2="8.54" y2="14"></line>
      <line x1="10.88" y1="21.94" x2="15.46" y2="14"></line>
    </svg>`;
  }
  if (name.includes('safari')) {
    return `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: white;">
      <circle cx="12" cy="12" r="10"></circle>
      <polygon points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88 16.24 7.76"></polygon>
    </svg>`;
  }
  if (name.includes('firefox')) {
    return `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: white;">
      <circle cx="12" cy="12" r="10"></circle>
      <path d="M2 12h20"></path>
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
    </svg>`;
  }
  return `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: white;">
    <circle cx="12" cy="12" r="10"></circle>
    <line x1="2" y1="12" x2="22" y2="12"></line>
    <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
  </svg>`;
};

const tabs = computed(() => {
  const list = [
    { id: 'stacktrace', name: 'Stack Trace', count: null },
    { id: 'header', name: 'Header', count: Object.keys(error.value?.header || {}).length },
    { id: 'cookies', name: 'Cookies', count: Object.keys(error.value?.cookies || {}).length },
    { id: 'connection', name: 'Connection', count: Object.keys(error.value?.connection || error.value?.Connection || {}).length },
    { id: 'serverVariables', name: 'Server Variables', count: Object.keys(error.value?.serverVariables || {}).length }
  ];

  if (error.value?.queryString && Object.keys(error.value.queryString).length > 0) {
    list.push({ id: 'querystring', name: 'Query String', count: Object.keys(error.value.queryString).length });
  }
  if (error.value?.form && Object.keys(error.value.form).length > 0) {
    list.push({ id: 'form', name: 'Form Fields', count: Object.keys(error.value.form).length });
  }
  if (error.value?.body) {
    list.push({ id: 'body', name: 'Request Body', count: null });
  }
  if (error.value?.params && error.value.params.length > 0) {
    list.push({ id: 'params', name: 'Parameters', count: error.value.params.length });
  }
  if (sqlEntries.value && sqlEntries.value.length > 0) {
    list.push({ id: 'sqllog', name: 'SQL Log', count: sqlEntries.value.length });
  }
  list.push({ id: 'rawxml', name: 'Raw XML', count: null });

  return list;
});

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
  padding: 16px 20px;
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

.header-title-label {
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
  color: #aaa9a3;
  letter-spacing: 0.05em;
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

.error-header-main {
  display: flex;
  align-items: flex-start;
  gap: 16px;
}

.error-header-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex: 1;
}

.status-badge-large {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background-color: #d94f4f;
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  font-weight: bold;
  flex-shrink: 0;
  margin-top: 2px;
}

.status-badge-large.info {
  background-color: #3b82f6;
}

.status-badge-large.warning {
  background-color: #f59e0b;
}

.status-badge-large.success {
  background-color: #10b981;
}

.error-message-detail {
  font-size: 15px;
  font-weight: 500;
  line-height: 1.35;
  color: #1a1a2e;
  margin: 0;
}

.exception-and-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.exception-type-detail {
  color: #00a2ed;
  font-size: 11px;
  font-weight: 500;
  margin-top: 0;
}

.action-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
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

.btn-delete-terracotta {
  background-color: #fdf2ef;
  border: 1px solid #e8c4ba;
  color: #b05a4a;
  border-radius: 3px;
}

.btn-delete-terracotta:hover {
  background-color: #f9e2db;
}

.btn-action {
  background-color: #ffffff;
  border: 1px solid #00a2ed; /* blue/cyan */
  color: #00a2ed;
  border-radius: 4px;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 32px;
  min-width: 32px;
  transition: background-color 0.15s, color 0.15s;
}

.btn-action:hover {
  background-color: #e6f2f7;
  color: #0088cc;
}

.btn-action.btn-icon {
  padding: 0 8px;
}

.btn-action i {
  font-size: 14px;
}

.nav-position-text {
  font-size: 11px;
  color: #5f5e5a;
}

/* Metadata Section */
.metadata-card {
  background-color: #e6f2f7; /* soft light blue background */
  border-radius: 4px;
  padding: 16px 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 24px;
}

.metadata-table {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.metadata-row {
  display: flex;
  align-items: center;
  font-size: 13px;
  line-height: 1.5;
}

.metadata-key {
  width: 120px;
  font-weight: bold;
  color: #333333;
  flex-shrink: 0;
}

.metadata-val {
  color: #333333;
  display: flex;
  align-items: center;
  gap: 8px;
  word-break: break-all;
}

.metadata-val.font-mono {
  font-family: var(--font-mono);
}

.device-badges {
  display: flex;
  flex-direction: column;
  gap: 16px;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  padding-left: 20px;
  border-left: 1px solid rgba(0, 162, 237, 0.15);
}

.device-badge {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.badge-circle {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background-color: #00a2ed; /* blue/cyan */
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  font-weight: bold;
}

.os-text-top {
  font-size: 12px;
  line-height: 1;
}

.os-text-bottom {
  font-size: 12px;
  line-height: 1;
}

.badge-label {
  font-size: 12px;
  color: #00a2ed;
  margin-top: 4px;
  font-weight: 500;
}

/* Method badge inside table */
.method-badge {
  padding: 1px 4px;
  font-size: 10px;
  font-weight: bold;
  font-family: var(--font-mono);
  border-radius: 2px;
  background-color: #888780;
  color: #ffffff;
  text-transform: uppercase;
}

.method-badge.GET {
  background-color: #888780;
}

.method-badge.POST {
  background-color: #b05a4a;
}

/* External links in metadata values */
.ext-link {
  color: #00a2ed;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  transition: color 0.15s;
}

.ext-link:hover {
  color: #0088cc;
}

.ext-link i {
  font-size: 12px;
}

.ip-link {
  color: #00a2ed;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.ip-link:hover {
  text-decoration: underline;
}

.ext-link-icon {
  font-size: 11px;
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
  border-bottom: 2px solid #e8e6e0;
  padding: 0;
  overflow-x: auto;
  margin-bottom: 1rem;
}

.tab-btn {
  background: none;
  border: none;
  padding: 0.75rem 1.25rem;
  color: #888780;
  cursor: pointer;
  font-weight: 500;
  font-size: 12px;
  border-bottom: 3px solid transparent;
  transition: all 150ms ease-in-out;
  display: flex;
  align-items: center;
  white-space: nowrap;
  border-radius: 0 !important;
  margin-bottom: -2px; /* aligns with border-bottom of tabs-nav */
}

.tab-btn:hover {
  color: #1a1a2e;
}

.tab-btn.active {
  color: #1a1a2e !important;
  font-weight: bold;
  border-bottom-color: #333333 !important; /* dark underline matching photo */
}

.tab-badge {
  background-color: #888780;
  color: #ffffff;
  border-radius: 50%;
  font-size: 10px;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 18px;
  height: 18px;
  padding: 0 4px;
  margin-left: 4px;
  vertical-align: middle;
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

/* Stack Trace - Focused Text Style */
.stacktrace-container {
  background-color: #fcfcfc;
  color: #1a1a2e;
  border: 1px solid #e2e0da;
  padding: 1.25rem;
  border-radius: 4px;
  overflow: auto;
  max-height: 600px;
  box-shadow: inset 0 1px 3px rgba(0,0,0,0.02);
}

:deep(.stacktrace-html) {
  font-family: 'Consolas', 'Courier New', Courier, monospace;
  font-size: 0.8rem;
  line-height: 1.7;
  white-space: pre-wrap;
}

:deep(.stacktrace-html a) {
  color: #00a2ed;
  text-decoration: underline;
}

:deep(.st-frame) {
  display: block;
  padding: 0.15rem 0;
}

:deep(.st-type) {
  color: #0a84ae; /* blue/teal matching photo */
  font-weight: normal;
}

:deep(.st-method) {
  color: #0a84ae; /* blue/teal matching photo */
  font-weight: bold;
}

:deep(.params) {
  color: #888780;
}

:deep(.st-param-type) {
  color: #1a1a2e;
}

:deep(.st-param-name) {
  color: #888780;
  font-style: italic;
}

:deep(.st-file) {
  color: #b03060; /* purple/magenta matching photo */
  margin-left: 0.5rem;
}

:deep(.st-line) {
  color: #b03060; /* purple/magenta matching photo */
  font-weight: bold;
}

:deep(.st-caller-line) {
  display: block;
  color: #333333;
  margin-bottom: 0.5rem;
  font-family: var(--font-mono);
  font-size: 13px;
}

:deep(.st-caller-path) {
  color: #333333;
  text-decoration: none;
}

:deep(.st-exception-line) {
  font-size: 13px;
  line-height: 1.6;
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
  padding: 0.5rem 0.75rem;
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

.section-title {
  font-family: var(--font-mono);
  font-size: 0.85rem;
  font-weight: 700;
  margin: 1.5rem 0 0.5rem 0;
  color: #1a1a2e;
}

.detail-content-wrapper,
.tab-content,
.stacktrace-container,
.xml-raw,
.sql-text {
  -webkit-overflow-scrolling: touch;
}

.text-break {
  word-break: break-all !important;
  word-wrap: break-word !important;
  overflow-wrap: break-word !important;
}

.btn-back-mobile {
  display: none;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  color: #00a2ed;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  padding: 4px 8px 4px 0;
}

.btn-back-mobile i {
  font-size: 14px;
}

@media (max-width: 768px) {
  .btn-back-mobile {
    display: inline-flex;
  }

  .header-title-label {
    display: none;
  }

  .btn-close-x {
    display: none;
  }

  .nav-position-text {
    display: none;
  }

  .btn-reviewed-text {
    display: none;
  }

  .detail-nav-buttons {
    margin-right: 0 !important;
    gap: 4px;
  }

  .metadata-card {
    flex-direction: column;
    align-items: stretch;
    gap: 16px;
  }

  .device-badges {
    flex-direction: row;
    justify-content: center;
    border-left: none;
    border-top: 1px solid rgba(0, 162, 237, 0.15);
    padding-left: 0;
    padding-top: 16px;
    gap: 24px;
  }
}
</style>
