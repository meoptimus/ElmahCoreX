<template>
  <div class="settings-view">
    <div class="settings-grid">
      <!-- 1. Connection & Server Info -->
      <div class="card settings-card">
        <h2 class="section-title"><i class="pi pi-server mr-1 text-primary"></i> Backend Diagnostics</h2>
        <div class="info-list">
          <div class="info-row">
            <span class="label">Connection Status</span>
            <span class="value">
              <span class="status-indicator" :class="store.backendOnline ? 'online' : 'offline'"></span>
              {{ store.backendOnline ? 'Connected' : 'Disconnected' }}
            </span>
          </div>

          <div class="info-row" v-if="config">
            <span class="label">Log Provider</span>
            <span class="value font-bold">{{ config.logName || 'N/A' }}</span>
          </div>

          <div class="info-row" v-if="config">
            <span class="label">Application Name</span>
            <span class="value font-mono">{{ config.applicationName || 'Default (Not Set)' }}</span>
          </div>

          <div class="info-row flex-col" v-if="config && config.sourcePaths && config.sourcePaths.length > 0">
            <span class="label mb-2">Registered Source Code Paths</span>
            <ul class="paths-list">
              <li v-for="(path, idx) in config.sourcePaths" :key="idx" class="font-mono">
                {{ path }}
              </li>
            </ul>
          </div>
        </div>

        <div class="card-actions mt-4">
          <button class="btn btn-secondary" :disabled="testing" @click="testConnection">
            <i class="pi pi-spin pi-spinner mr-2" v-if="testing"></i>
            <i class="pi pi-wifi mr-2" v-else></i>
            Test Connectivity
          </button>
        </div>
      </div>

      <!-- 2. UI Preferences -->
      <div class="card settings-card">
        <h2 class="section-title"><i class="pi pi-sliders-h mr-1 text-primary"></i> UI Customization</h2>
        
        <div class="form-group mb-3">
          <label class="form-label">Active Theme Mode</label>
          <div class="toggle-group">
            <button 
              class="toggle-btn" 
              :class="{ 'active': !isDark }" 
              @click="setTheme(false)"
            >
              <i class="pi pi-sun mr-1"></i> Light
            </button>
            <button 
              class="toggle-btn" 
              :class="{ 'active': isDark }" 
              @click="setTheme(true)"
            >
              <i class="pi pi-moon mr-1"></i> Dark
            </button>
          </div>
        </div>

        <div class="form-group mb-3">
          <label class="form-label">Default Rows Per Page</label>
          <select class="form-select" v-model="defaultPageSize" @change="savePreferences">
            <option :value="10">10 Rows</option>
            <option :value="25">25 Rows</option>
            <option :value="50">50 Rows</option>
            <option :value="100">100 Rows</option>
          </select>
        </div>

        <div class="form-group mb-3">
          <label class="form-label">Auto Refresh Polling</label>
          <select class="form-select" v-model="defaultRefresh" @change="savePreferences">
            <option :value="0">Off (Manual Refresh)</option>
            <option :value="10">Every 10 Seconds</option>
            <option :value="30">Every 30 Seconds</option>
            <option :value="60">Every 60 Seconds</option>
          </select>
        </div>
      </div>

      <!-- 3. Links & Project Info -->
      <div class="card settings-card col-span-2">
        <h2 class="section-title"><i class="pi pi-info-circle mr-1 text-primary"></i> About ElmahCoreX</h2>
        <p class="about-text">
          ElmahCoreX is an advanced error logging modules and handlers library for ASP.NET Core web applications.
          This UI enhancement replaces the legacy SPA and provides features such as search filtering, bulk operations, 
          review status tracking, and analytics dashboards.
        </p>

        <div class="links-row">
          <a href="https://github.com/meoptimus/ElmahCoreX" target="_blank" class="project-link">
            <i class="pi pi-github mr-1"></i> GitHub Repository
          </a>
          <a href="https://www.nuget.org/packages/ElmahCoreX" target="_blank" class="project-link">
            <i class="pi pi-box mr-1"></i> NuGet Package
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

const isDark = ref(localStorage.getItem('theme') === 'dark');
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
    toast.add({ severity: 'success', summary: 'Ping Success', detail: 'Successfully pinged Elmah backend!', life: 3000 });
  } else {
    toast.add({ severity: 'error', summary: 'Ping Failed', detail: 'Unable to communicate with the backend.', life: 4000 });
  }
};

const setTheme = (dark) => {
  isDark.value = dark;
  localStorage.setItem('theme', dark ? 'dark' : 'light');
  if (dark) {
    document.documentElement.classList.add('dark-mode');
  } else {
    document.documentElement.classList.remove('dark-mode');
  }
  toast.add({ severity: 'info', summary: 'Theme Updated', detail: `Switched to ${dark ? 'Dark' : 'Light'} theme mode.`, life: 2000 });
};

const savePreferences = () => {
  localStorage.setItem('default_page_size', defaultPageSize.value.toString());
  localStorage.setItem('auto_refresh', defaultRefresh.value.toString());
  store.pageSize = defaultPageSize.value;
  store.startAutoRefresh(defaultRefresh.value);
  toast.add({ severity: 'success', summary: 'Preferences Saved', detail: 'User options updated successfully.', life: 2500 });
};

import axios from 'axios';

onMounted(() => {
  loadConfig();
});
</script>

<style scoped>
.settings-view {
  display: flex;
  flex-direction: column;
}

.settings-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.5rem;
}

.card {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03);
}

.col-span-2 {
  grid-column: span 1;
}

@media (min-width: 768px) {
  .col-span-2 {
    grid-column: span 2;
  }
}

.section-title {
  margin: 0 0 1.25rem 0;
  font-size: 1.15rem;
  font-weight: 700;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 0.5rem;
}

.text-primary {
  color: var(--primary-color);
}

.mb-3 {
  margin-bottom: 1rem;
}

.mb-2 {
  margin-bottom: 0.5rem;
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

.font-mono {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
}

.font-bold {
  font-weight: 700;
}

/* Info List */
.info-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.9rem;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 0.5rem;
}

.info-row.flex-col {
  flex-direction: column;
  align-items: flex-start;
  border-bottom: none;
}

.label {
  color: var(--text-light);
  font-weight: 500;
}

.value {
  color: var(--text-color);
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.status-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-indicator.online {
  background-color: var(--success-color);
  box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.2);
}

.status-indicator.offline {
  background-color: var(--error-color);
  box-shadow: 0 0 0 2px rgba(239, 68, 68, 0.2);
}

.paths-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  width: 100%;
}

.paths-list li {
  background-color: var(--bg-color);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  padding: 0.4rem 0.6rem;
  font-size: 0.8rem;
  word-break: break-all;
}

/* Form Styling */
.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-light);
}

.form-select {
  padding: 0.5rem;
  border-radius: 8px;
  border: 1px solid var(--border-color);
  background-color: var(--panel-bg);
  color: var(--text-color);
  outline: none;
  font-size: 0.9rem;
}

.form-select:focus {
  border-color: var(--primary-color);
}

/* Theme Toggle button */
.toggle-group {
  display: flex;
  background-color: var(--bg-color);
  border: 1px solid var(--border-color);
  padding: 0.25rem;
  border-radius: 8px;
  width: fit-content;
}

.toggle-btn {
  background: none;
  border: none;
  padding: 0.4rem 0.8rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-light);
  cursor: pointer;
  display: flex;
  align-items: center;
  transition: all 0.15s;
}

.toggle-btn:hover {
  color: var(--text-color);
}

.toggle-btn.active {
  background-color: var(--panel-bg);
  color: var(--primary-color);
  box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
}

/* Buttons */
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

.btn-secondary {
  background-color: var(--panel-bg);
  border-color: var(--border-color);
  color: var(--text-color);
}

.btn-secondary:hover {
  background-color: var(--bg-color);
}

.btn-secondary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* About Section */
.about-text {
  font-size: 0.9rem;
  line-height: 1.6;
  color: var(--text-color);
  margin-top: 0;
  margin-bottom: 1.5rem;
}

.links-row {
  display: flex;
  gap: 1.5rem;
}

.project-link {
  color: var(--primary-color);
  text-decoration: none;
  font-size: 0.9rem;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
}

.project-link:hover {
  text-decoration: underline;
}
</style>
