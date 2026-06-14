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
              <router-link to="/" class="nav-item" active-class="active" title="View Error Logs">
                <i class="pi pi-list nav-icon"></i>
                <span class="nav-text">Errors</span>
                <span v-if="store.totalFiltered > 0" class="badge">
                  {{ store.totalFiltered }}
                </span>
              </router-link>

              <a :href="`${cleanRoot}/rss`" target="_blank" class="nav-item" title="RSS Feeds (Opens in new tab)">
                <i class="pi pi-rss nav-icon" style="color: #f97316;"></i>
                <span class="nav-text">RSS Feeds</span>
              </a>

              <a :href="`${cleanRoot}/digestrss`" target="_blank" class="nav-item" title="RSS Digest (Opens in new tab)">
                <i class="pi pi-envelope nav-icon" style="color: #ef4444;"></i>
                <span class="nav-text">RSS Digest</span>
              </a>

              <a :href="`${cleanRoot}/download`" target="_blank" class="nav-item" title="Download log as CSV">
                <i class="pi pi-download nav-icon" style="color: #22c55e;"></i>
                <span class="nav-text">Download Log</span>
              </a>

              <router-link to="/stats" class="nav-item" active-class="active" title="View Dashboard Statistics">
                <i class="pi pi-chart-bar nav-icon"></i>
                <span class="nav-text">Statistics</span>
              </router-link>

              <router-link to="/settings" class="nav-item" active-class="active" title="Configure Dashboard Settings">
                <i class="pi pi-cog nav-icon"></i>
                <span class="nav-text">Settings</span>
              </router-link>

              <a href="https://github.com/meoptimus/ElmahCoreX" target="_blank" class="nav-item" title="Help / Documentation">
                <i class="pi pi-question-circle nav-icon" style="color: #3b82f6;"></i>
                <span class="nav-text">Help</span>
              </a>

              <button class="nav-item btn-link-style" @click="showAboutModal = true" title="About ElmahCoreX">
                <i class="pi pi-info-circle nav-icon" style="color: #6366f1;"></i>
                <span class="nav-text">About</span>
              </button>
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

    <!-- About Modal Dialog -->
    <Transition name="fade">
      <div v-if="showAboutModal" class="about-modal-overlay" @click.self="showAboutModal = false">
        <div class="about-modal-card">
          <button class="about-close-btn" @click="showAboutModal = false">&times;</button>
          <div class="about-header">
            <i class="pi pi-shield about-logo-icon"></i>
            <h2>ElmahCoreX</h2>
            <span class="about-version">v2.2.5</span>
          </div>
          <div class="about-body">
            <p>A modernized, high-performance error logging and management dashboard for ASP.NET Core applications.</p>
            <div class="about-info-grid">
              <div class="info-row">
                <span class="label">Repository:</span>
                <span class="val"><a href="https://github.com/meoptimus/ElmahCoreX" target="_blank">meoptimus/ElmahCoreX</a></span>
              </div>
              <div class="info-row">
                <span class="label">Framework:</span>
                <span class="val">Vue 3 + Vite / .NET 8 / .NET 9</span>
              </div>
              <div class="info-row">
                <span class="label">Features:</span>
                <span class="val">Real-time polling, analytics, full-text filters, reviewed flag, bulk operations</span>
              </div>
            </div>
          </div>
          <div class="about-footer">
            <button class="btn-primary" @click="showAboutModal = false">Close</button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Toaster Notifications -->
    <div class="toaster-container">
      <transition-group name="toast">
        <div 
          v-for="toast in store.notifications" 
          :key="toast.id" 
          class="toast-item"
          :class="'severity-' + toast.type.toLowerCase()"
          @click="removeToast(toast.id)"
        >
          <div class="toast-icon">
            <i class="pi pi-bell"></i>
          </div>
          <div class="toast-content">
            <span class="toast-message">{{ toast.message }}</span>
          </div>
          <button class="toast-close">
            <i class="pi pi-times"></i>
          </button>
        </div>
      </transition-group>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute } from 'vue-router';
import { useErrorStore } from './stores/errorStore';
import Toast from 'primevue/toast';

const store = useErrorStore();
const route = useRoute();

const removeToast = (id) => {
  store.removeNotification(id);
};

const elmahRoot = window.$elmah_root || '/elmah';
const cleanRoot = '/' + elmahRoot.replace(/^\/|\/$/g, '');
const showAboutModal = ref(false);

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
  height: 48px;
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
  white-space: nowrap;
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
  padding: 0.5rem 0.75rem 0.1rem 0.75rem;
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

@media (max-width: 1366px) {
  /* On medium screens, only show labels for primary route (Errors) and use icons for secondary items */
  .top-nav .nav-item:not(:first-child) .nav-text {
    display: none;
  }
  .top-nav .nav-item:not(:first-child) {
    padding: 0.5rem;
  }
}

@media (max-width: 768px) {
  .logo-text, .version-tag {
    display: none;
  }
  .header-left {
    gap: 0.5rem;
  }
  .top-nav {
    gap: 0.25rem;
  }
  .top-nav .nav-text {
    display: none !important;
  }
  .nav-item {
    padding: 0.5rem !important;
  }
}

/* About Modal Styles */
.about-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background-color: rgba(15, 23, 42, 0.65);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.about-modal-card {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-radius: 16px;
  width: 90%;
  max-width: 480px;
  padding: 2.5rem 2rem 2rem 2rem;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.2), 0 10px 10px -5px rgba(0, 0, 0, 0.1);
  position: relative;
  text-align: center;
  color: var(--text-color);
  transition: transform 0.3s ease;
}

.about-close-btn {
  position: absolute;
  top: 1rem;
  right: 1.25rem;
  background: none;
  border: none;
  font-size: 1.75rem;
  color: var(--text-light);
  cursor: pointer;
  line-height: 1;
}

.about-close-btn:hover {
  color: var(--text-color);
}

.about-logo-icon {
  font-size: 3.5rem;
  color: var(--primary-color);
  margin-bottom: 1rem;
}

.about-header h2 {
  margin: 0.5rem 0 0.25rem 0;
  font-size: 1.75rem;
  font-weight: 800;
}

.about-version {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-light);
  background-color: var(--bg-color);
  padding: 0.2rem 0.6rem;
  border-radius: 9999px;
  border: 1px solid var(--border-color);
}

.about-body {
  margin-top: 1.5rem;
  font-size: 0.95rem;
  line-height: 1.5;
}

.about-body p {
  color: var(--text-light);
  margin-bottom: 1.5rem;
}

.about-info-grid {
  background-color: var(--bg-color);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  padding: 1rem;
  text-align: left;
}

.info-row {
  display: flex;
  justify-content: space-between;
  padding: 0.5rem 0;
  font-size: 0.85rem;
  border-bottom: 1px solid var(--border-color);
}

.info-row:last-child {
  border-bottom: none;
}

.info-row .label {
  font-weight: 600;
  color: var(--text-light);
}

.info-row .val {
  font-weight: 600;
  color: var(--text-color);
}

.info-row .val a {
  color: var(--primary-color);
  text-decoration: none;
}

.info-row .val a:hover {
  text-decoration: underline;
}

.about-footer {
  margin-top: 2rem;
  display: flex;
  justify-content: center;
}

.btn-primary {
  background-color: var(--primary-color);
  color: white;
  border: none;
  padding: 0.6rem 2rem;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.2s;
}

.btn-primary:hover {
  background-color: var(--primary-hover);
}

.btn-link-style {
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
  text-align: left;
  outline: none;
}

/* Toaster Notifications */
.toaster-container {
  position: fixed;
  bottom: 1.5rem;
  right: 1.5rem;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  pointer-events: none;
}

.toast-item {
  pointer-events: auto;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 300px;
  max-width: 400px;
  background-color: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(8px);
  border: 1px solid var(--border-color);
  border-left: 4px solid var(--primary-color);
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05);
  border-radius: 8px;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

html.dark-mode .toast-item {
  background-color: rgba(30, 41, 59, 0.95);
  border-color: rgba(255, 255, 255, 0.1);
}

.toast-item:hover {
  transform: translateY(-2px);
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.15), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
}

/* Severity borders */
.toast-item.severity-info {
  border-left-color: #3b82f6;
}
.toast-item.severity-success {
  border-left-color: #10b981;
}
.toast-item.severity-warning {
  border-left-color: #f59e0b;
}
.toast-item.severity-error {
  border-left-color: #ef4444;
}

.toast-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--primary-color);
  font-size: 1.1rem;
}

.toast-item.severity-error .toast-icon {
  color: #ef4444;
}
.toast-item.severity-warning .toast-icon {
  color: #f59e0b;
}
.toast-item.severity-success .toast-icon {
  color: #10b981;
}

.toast-content {
  flex: 1;
}

.toast-message {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-color);
}

.toast-close {
  background: none;
  border: none;
  color: var(--text-light);
  cursor: pointer;
  padding: 0.2rem;
  display: flex;
  align-items: center;
  opacity: 0.6;
  transition: opacity 0.2s;
}

.toast-close:hover {
  opacity: 1;
}

/* Toast Transitions */
.toast-enter-from {
  opacity: 0;
  transform: translateX(100px) scale(0.9);
}
.toast-enter-to {
  opacity: 1;
  transform: translateX(0) scale(1);
}
.toast-leave-from {
  opacity: 1;
  transform: translateX(0) scale(1);
}
.toast-leave-to {
  opacity: 0;
  transform: translateX(100px) scale(0.9);
}
</style>
