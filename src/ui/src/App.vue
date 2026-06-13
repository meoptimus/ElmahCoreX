<template>
  <div class="app-container" :class="{ 'dark-mode': isDarkMode }">
    <!-- Offline Banner -->
    <div v-if="!store.backendOnline" class="offline-banner">
      <i class="pi pi-exclamation-triangle mr-2"></i>
      Disconnected from Elmah backend. Retrying connection...
    </div>

    <div class="layout-wrapper">
      <!-- Main Content Area -->
      <div class="main-layout">
        <!-- Top bar header -->
        <header class="main-header">
          <div class="header-left">
            <div class="logo">
              <i class="pi pi-shield logo-icon"></i>
              <span class="logo-text">ElmahCoreX</span>
            </div>

            <!-- Top Navigation links -->
            <nav class="top-nav">
              <router-link to="/" class="nav-item" active-class="active">
                <i class="pi pi-list nav-icon"></i>
                <span class="nav-text">Errors</span>
                <span v-if="store.totalFiltered > 0" class="badge">
                  {{ store.totalFiltered }}
                </span>
              </router-link>

              <router-link to="/stats" class="nav-item" active-class="active">
                <i class="pi pi-chart-bar nav-icon"></i>
                <span class="nav-text">Statistics</span>
              </router-link>

              <router-link to="/settings" class="nav-item" active-class="active">
                <i class="pi pi-cog nav-icon"></i>
                <span class="nav-text">Settings</span>
              </router-link>
            </nav>
          </div>
          <div class="header-right">
            <!-- Environment Badge -->
            <span class="env-badge" :class="envType">
              {{ envType.toUpperCase() }}
            </span>

            <!-- Auto Refresh dropdown -->
            <div class="refresh-control">
              <i class="pi pi-sync mr-1"></i>
              <select :value="store.autoRefreshInterval" @change="changeAutoRefresh">
                <option :value="0">Manual Refresh</option>
                <option :value="10">Poll 10s</option>
                <option :value="30">Poll 30s</option>
                <option :value="60">Poll 60s</option>
              </select>
            </div>

            <!-- Refresh Button -->
            <button class="icon-btn" title="Refresh Now" @click="refreshCurrent">
              <i class="pi pi-refresh" :class="{ 'pi-spin': store.loading }"></i>
            </button>

            <!-- Dark Mode Toggle -->
            <button class="icon-btn" title="Toggle Dark/Light Mode" @click="toggleDarkMode">
              <i :class="isDarkMode ? 'pi pi-sun' : 'pi pi-moon'"></i>
            </button>

            <span class="version-tag">v3.0.0</span>
          </div>
        </header>

        <!-- Main View router-view -->
        <main class="content-body">
          <router-view v-slot="{ Component }">
            <transition name="fade" mode="out-in">
              <component :is="Component" />
            </transition>
          </router-view>
        </main>
      </div>
    </div>
    <Toast />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute } from 'vue-router';
import { useErrorStore } from './stores/errorStore';
import Toast from 'primevue/toast';

const store = useErrorStore();
const route = useRoute();

const isDarkMode = ref(localStorage.getItem('theme') === 'dark');

const envType = computed(() => {
  const host = window.location.hostname;
  if (host === 'localhost' || host === '127.0.0.1') return 'development';
  if (host.includes('staging') || host.includes('test')) return 'staging';
  return 'production';
});

const toggleDarkMode = () => {
  isDarkMode.value = !isDarkMode.value;
  localStorage.setItem('theme', isDarkMode.value ? 'dark' : 'light');
  updateThemeClass();
};

const updateThemeClass = () => {
  if (isDarkMode.value) {
    document.documentElement.classList.add('dark-mode');
  } else {
    document.documentElement.classList.remove('dark-mode');
  }
};

const changeAutoRefresh = (event) => {
  const val = parseInt(event.target.value, 10);
  store.startAutoRefresh(val);
  localStorage.setItem('auto_refresh', val.toString());
};

const refreshCurrent = () => {
  store.fetchErrors();
  store.fetchCounts();
};

// Keyboard Shortcuts Listener
const handleKeyDown = (e) => {
  if (e.key === 'r' || e.key === 'R') {
    if (document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'SELECT' && document.activeElement.tagName !== 'TEXTAREA') {
      refreshCurrent();
    }
  }
};

onMounted(() => {
  updateThemeClass();
  const savedInterval = parseInt(localStorage.getItem('auto_refresh') || '0', 10);
  store.startAutoRefresh(savedInterval);
  store.fetchCounts();
  window.addEventListener('keydown', handleKeyDown);
});

onUnmounted(() => {
  store.stopAutoRefresh();
  window.removeEventListener('keydown', handleKeyDown);
});
</script>

<style>
/* CSS Variables & Global Dark/Light Theme System */
:root {
  --font-family: 'Inter', system-ui, sans-serif;
  --bg-color: #f8fafc;
  --panel-bg: #ffffff;
  --border-color: #e2e8f0;
  --text-color: #334155;
  --text-light: #64748b;
  --primary-color: #4f46e5;
  --primary-hover: #4338ca;
  --primary-light: #e0e7ff;
  --success-color: #10b981;
  --warning-color: #f59e0b;
  --error-color: #ef4444;
  --env-dev-bg: #dbeafe;
  --env-dev-text: #1e40af;
  --env-stg-bg: #fef3c7;
  --env-stg-text: #92400e;
  --env-prd-bg: #fee2e2;
  --env-prd-text: #991b1b;
}

html.dark-mode {
  --bg-color: #0f172a;
  --panel-bg: #1e293b;
  --border-color: #334155;
  --text-color: #f1f5f9;
  --text-light: #94a3b8;
  --primary-color: #6366f1;
  --primary-hover: #4f46e5;
  --primary-light: #312e81;
  --success-color: #10b981;
  --warning-color: #f59e0b;
  --error-color: #f87171;
  --env-dev-bg: #1e3a8a;
  --env-dev-text: #93c5fd;
  --env-stg-bg: #78350f;
  --env-stg-text: #fde68a;
  --env-prd-bg: #7f1d1d;
  --env-prd-text: #fca5a5;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  padding: 0;
  font-family: var(--font-family);
  background-color: var(--bg-color);
  color: var(--text-color);
  transition: background-color 0.2s, color 0.2s;
  overflow: hidden;
}

/* Global App Layout */
.app-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
}

.offline-banner {
  background-color: var(--error-color);
  color: white;
  text-align: center;
  padding: 0.5rem;
  font-weight: 600;
  font-size: 0.875rem;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
}

.layout-wrapper {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* Main Layout & Header */
.main-layout {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}

.main-header {
  height: 64px;
  background-color: var(--panel-bg);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1.5rem;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 2.5rem;
}

.logo {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 700;
  font-size: 1.25rem;
  color: var(--primary-color);
}

.logo-icon {
  font-size: 1.5rem;
}

.top-nav {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.nav-item {
  display: flex;
  align-items: center;
  padding: 0.5rem 0.85rem;
  gap: 0.5rem;
  color: var(--text-light);
  text-decoration: none;
  border-radius: 8px;
  font-weight: 600;
  font-size: 0.9rem;
  transition: all 0.2s;
  position: relative;
}

.nav-item:hover {
  background-color: var(--bg-color);
  color: var(--text-color);
}

.nav-item.active {
  background-color: var(--primary-light);
  color: var(--primary-color);
}

.nav-icon {
  font-size: 1rem;
}

.badge {
  background-color: var(--error-color);
  color: white;
  padding: 0.15rem 0.4rem;
  border-radius: 12px;
  font-size: 0.7rem;
  font-weight: 700;
  margin-left: 0.25rem;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.env-badge {
  padding: 0.25rem 0.6rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 700;
}

.env-badge.development {
  background-color: var(--env-dev-bg);
  color: var(--env-dev-text);
}

.env-badge.staging {
  background-color: var(--env-stg-bg);
  color: var(--env-stg-text);
}

.env-badge.production {
  background-color: var(--env-prd-bg);
  color: var(--env-prd-text);
}

.refresh-control {
  display: flex;
  align-items: center;
  font-size: 0.85rem;
  color: var(--text-light);
}

.refresh-control select {
  background: none;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  color: var(--text-color);
  padding: 0.2rem 0.4rem;
  margin-left: 0.25rem;
  outline: none;
}

.icon-btn {
  background: none;
  border: none;
  color: var(--text-light);
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 50%;
  font-size: 1.1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.icon-btn:hover {
  background-color: var(--border-color);
  color: var(--text-color);
}

.version-tag {
  font-size: 0.75rem;
  color: var(--text-light);
  border-left: 1px solid var(--border-color);
  padding-left: 0.75rem;
}

.content-body {
  flex: 1;
  overflow: hidden;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
}

.stats-dashboard-view,
.settings-view {
  overflow-y: auto;
  flex: 1;
  height: 100%;
  padding-right: 0.25rem;
}

/* Page Transition Animations */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.15s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 768px) {
  .logo-text, .version-tag {
    display: none;
  }
  .header-left {
    gap: 1rem;
  }
  .nav-text {
    display: none;
  }
  .nav-item {
    padding: 0.5rem;
  }
}
</style>
