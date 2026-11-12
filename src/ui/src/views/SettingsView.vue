<template>
  <div class="settings-view">
    <div class="settings-grid">
      <!-- 1. Connection & Server Info -->
      <div class="card settings-card">
        <h2 class="section-title">Backend</h2>
        <div class="info-list">
          <div class="info-row">
            <span class="label">Connection</span>
            <span class="value">
              <span class="status-indicator" :class="store.backendOnline ? 'online' : 'offline'"></span>
              {{ store.backendOnline ? 'Connected' : 'Disconnected' }}
            </span>
          </div>

          <div class="info-row" v-if="config">
            <span class="label">Log provider</span>
            <span class="value font-bold">{{ config.logName || 'N/A' }}</span>
          </div>

          <div class="info-row" v-if="config">
            <span class="label">Application name</span>
            <span class="value font-mono">{{ config.applicationName || 'Not set' }}</span>
          </div>

          <div class="info-row flex-col" v-if="config && config.sourcePaths && config.sourcePaths.length > 0">
            <span class="label mb-2">Source code paths</span>
            <ul class="paths-list">
              <li v-for="(path, idx) in config.sourcePaths" :key="idx" class="font-mono">
                {{ path }}
              </li>
            </ul>
          </div>
        </div>

        <div class="card-actions mt-4">
          <button class="btn btn-secondary" :disabled="testing" @click="testConnection">
            <i class="pi pi-spin pi-spinner" v-if="testing"></i>
            <i class="pi pi-wifi" v-else></i>
            Test connection
          </button>
        </div>
      </div>

      <!-- 2. UI Preferences -->
      <div class="card settings-card">
        <h2 class="section-title">Preferences</h2>
        

        <div class="form-group mb-3">
          <label class="form-label" for="pref-page-size">Errors loaded per page</label>
          <select id="pref-page-size" class="form-select" v-model="defaultPageSize" @change="savePreferences">
            <option :value="10">10</option>
            <option :value="25">25</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
          </select>
        </div>

        <div class="form-group mb-3">
          <label class="form-label" for="pref-refresh">Auto refresh</label>
          <select id="pref-refresh" class="form-select" v-model="defaultRefresh" @change="savePreferences">
            <option :value="0">Off, refresh manually</option>
            <option :value="10">Every 10 seconds</option>
            <option :value="30">Every 30 seconds</option>
            <option :value="60">Every 60 seconds</option>
          </select>
        </div>
      </div>

      <!-- 3. Links & Project Info -->
      <div class="card settings-card col-span-2">
        <h2 class="section-title">About ElmahCoreX</h2>
        <p class="about-text">
          ElmahCoreX logs unhandled errors in ASP.NET Core applications and gives you this viewer to read them:
          search and filters, bulk delete, statistics and auto refresh.
        </p>

        <div class="links-row">
          <a href="https://github.com/meoptimus/ElmahCoreX" target="_blank" class="project-link">
            <i class="pi pi-github mr-1"></i> GitHub repository
          </a>
          <a href="https://www.nuget.org/packages/ElmahCoreX" target="_blank" class="project-link">
            <i class="pi pi-box mr-1"></i> NuGet package
          </a>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useErrorStore } from '../stores/errorStore';
import { elmahApi } from '../api/elmahApi';
import { useToast } from 'primevue/usetoast';

const store = useErrorStore();
const toast = useToast();

const config = ref(null);
const testing = ref(false);


const defaultPageSize = ref(parseInt(localStorage.getItem('default_page_size') || '25', 10));
const defaultRefresh = ref(parseInt(localStorage.getItem('auto_refresh') || '0', 10));

const loadConfig = async () => {
  try {
    const res = await elmahApi.getErrors(0, 0, {}); // placeholder, let's call new api
    const configRes = await elmahApi.getErrors(0, 0, {}).then(() => {
      // Call api/config
      return axios.get(`${elmahApi.getExportUrl('json')}`.replace('api/export', 'api/config'));
    });
    if (configRes.data && configRes.data.success) {
      config.value = configRes.data.data;
    }
  } catch (err) {
    console.error('Error fetching backend settings', err);
  }
};

const testConnection = async () => {
  testing.value = true;
  const online = await store.testConnection();
  testing.value = false;
  if (online) {
    toast.add({ severity: 'success', summary: 'Connected', detail: 'The ELMAH backend responded.', life: 3000 });
  } else {
    toast.add({ severity: 'error', summary: 'No response', detail: 'The ELMAH backend did not respond.', life: 4000 });
  }
};

const savePreferences = () => {
  localStorage.setItem('default_page_size', defaultPageSize.value.toString());
  localStorage.setItem('auto_refresh', defaultRefresh.value.toString());
  store.pageSize = defaultPageSize.value;
  store.startAutoRefresh(defaultRefresh.value);
  toast.add({ severity: 'success', summary: 'Preferences saved', detail: 'Your changes apply from now on.', life: 2500 });
};

import axios from 'axios';

onMounted(() => {
  loadConfig();
});
</script>

<style scoped>
.settings-view {
  padding: 20px;
}

.settings-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  max-width: 980px;
  margin: 0 auto;
  align-items: start;
}

.card {
  background-color: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: 18px 20px;
  min-width: 0;
}

.col-span-2 {
  grid-column: span 2;
}

.section-title {
  margin: 0 0 12px;
  font-size: 15px;
  font-weight: 600;
  color: var(--text);
}

.mb-3 {
  margin-bottom: 14px;
}

.mb-2 {
  margin-bottom: 8px;
}

.mt-4 {
  margin-top: 16px;
}

/* Backend facts */
.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  padding: 10px 0;
  border-bottom: 1px solid var(--border);
}

.info-row:last-child {
  border-bottom: none;
}

.info-row.flex-col {
  flex-direction: column;
  align-items: flex-start;
  gap: 0;
}

.label {
  color: var(--text-2);
}

.value {
  color: var(--text);
  display: flex;
  align-items: center;
  gap: 8px;
  text-align: right;
  overflow-wrap: anywhere;
}

.value.font-mono {
  font-size: 12.5px;
}

.status-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-indicator.online {
  background-color: var(--ok);
  box-shadow: 0 0 0 3px var(--ok-soft);
}

.status-indicator.offline {
  background-color: var(--danger);
  box-shadow: 0 0 0 3px var(--danger-soft);
}

.paths-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 100%;
}

.paths-list li {
  background-color: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 6px 10px;
  font-size: 12px;
  overflow-wrap: anywhere;
  color: var(--text-2);
}

/* Preferences */
.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 12.5px;
  font-weight: 500;
  color: var(--text-2);
}

.form-select {
  height: 34px;
  padding: 0 8px;
  border-radius: var(--radius);
  border: 1px solid var(--border-strong);
  background-color: var(--surface);
  color: var(--text);
  font-size: 13px;
  outline: none;
}

.form-select:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 3px var(--accent-soft);
}

/* About */
.about-text {
  max-width: 72ch;
  margin: 0 0 14px;
  line-height: 1.55;
  color: var(--text-2);
}

.links-row {
  display: flex;
  flex-wrap: wrap;
  gap: 18px;
}

.project-link {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  color: var(--accent-text);
  text-decoration: none;
  font-weight: 500;
}

.project-link:hover {
  text-decoration: underline;
}

@media (max-width: 768px) {
  .settings-view {
    padding: 12px;
  }

  .settings-grid {
    grid-template-columns: minmax(0, 1fr);
    gap: 12px;
  }

  .col-span-2 {
    grid-column: span 1;
  }
}
</style>
