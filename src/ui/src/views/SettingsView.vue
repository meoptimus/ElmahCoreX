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
  padding: 16px 20px;
}

.settings-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 16px;
}

.card {
  background-color: #ffffff;
  border: 1px solid #e8e6e0;
  border-radius: 0;
  padding: 16px 20px;
  box-shadow: none;
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
  margin: 0 0 16px 0;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #aaa9a3;
  font-weight: 600;
  border-bottom: 1px solid #e8e6e0;
  padding-bottom: 8px;
}

.text-primary {
  color: #4a7fc1;
}

.mb-3 {
  margin-bottom: 12px;
}

.mb-2 {
  margin-bottom: 8px;
}

.mt-4 {
  margin-top: 16px;
}



.font-mono {
  font-family: var(--font-mono), monospace;
  color: #1a1a2e;
}

.font-bold {
  font-weight: 500;
  color: #1a1a2e;
}

/* Info List */
.info-list {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
  border-bottom: 1px solid #e8e6e0;
  padding: 10px 0;
}

.info-row:last-child {
  border-bottom: none;
}

.info-row.flex-col {
  flex-direction: column;
  align-items: flex-start;
}

.label {
  color: #aaa9a3;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-weight: 500;
}

.value {
  color: #1a1a2e;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.status-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-indicator.online {
  background-color: #10b981;
}

.status-indicator.offline {
  background-color: #d94f4f;
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
  background-color: #eeecea;
  border: 1px solid #dddbd4;
  border-radius: 4px;
  padding: 6px 10px;
  font-size: 11px;
  word-break: break-all;
  color: #5f5e5a;
}

/* Form Styling */
.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-label {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-weight: 600;
  color: #aaa9a3;
}

.form-select {
  padding: 8px;
  border-radius: 4px;
  border: 1px solid #dddbd4;
  background-color: #ffffff;
  color: #1a1a2e;
  outline: none;
  font-size: 13px;
  font-family: var(--font-sans);
}

.form-select:focus {
  border-color: #4a7fc1;
}

/* Theme Toggle button */
.toggle-group {
  display: flex;
  background-color: #eeecea;
  border: 1px solid #dddbd4;
  padding: 4px;
  border-radius: 4px;
  width: fit-content;
}

.toggle-btn {
  background: none;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-size: 12px;
  font-weight: 500;
  color: #888780;
  cursor: pointer;
  display: flex;
  align-items: center;
  transition: all 0.15s;
  font-family: var(--font-sans);
}

.toggle-btn:hover {
  color: #1a1a2e;
}

.toggle-btn.active {
  background-color: #ffffff;
  color: #1a1a2e;
  border: 1px solid #dddbd4;
}

/* Buttons */
.btn {
  padding: 8px 16px;
  border-radius: 4px;
  font-weight: 500;
  font-size: 12px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #dddbd4;
  transition: all 0.2s;
  font-family: var(--font-sans);
}

.btn-secondary {
  background-color: #ffffff;
  color: #5f5e5a;
}

.btn-secondary:hover {
  background-color: #eeecea;
}

.btn-secondary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* About Section */
.about-text {
  font-size: 13px;
  line-height: 1.5;
  color: #1a1a2e;
  margin-top: 0;
  margin-bottom: 16px;
}

.links-row {
  display: flex;
  gap: 16px;
}

.project-link {
  color: #4a7fc1;
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  display: inline-flex;
  align-items: center;
}

.project-link:hover {
  text-decoration: underline;
}
</style>
