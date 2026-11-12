<template>
  <div class="app-container">
    <!-- Offline Banner -->
    <div v-if="!store.backendOnline" class="offline-banner" role="status">
      <i class="pi pi-exclamation-triangle"></i>
      Can't reach the ELMAH backend. Retrying…
    </div>

    <header class="main-header">
      <div class="header-left">
        <router-link to="/" class="logo" title="ElmahCoreX">
          <!-- A stack of frames with the failing one on top -->
          <svg class="logo-mark" width="22" height="22" viewBox="0 0 22 22" aria-hidden="true">
            <rect x="2" y="3" width="13" height="4" rx="1.5" class="logo-frame-top" />
            <rect x="5" y="9" width="15" height="4" rx="1.5" class="logo-frame" />
            <rect x="5" y="15" width="10" height="4" rx="1.5" class="logo-frame logo-frame-dim" />
          </svg>
          <span class="logo-text">ElmahCoreX</span>
        </router-link>

        <nav class="top-nav">
          <router-link to="/" class="nav-item" :class="{ active: isErrorsRoute }" title="View error log">
            Errors
            <span class="nav-count" :title="`${store.totalFiltered} ${store.totalFiltered === 1 ? 'error' : 'errors'}`">{{ store.totalFiltered }}</span>
          </router-link>
          <router-link to="/stats" class="nav-item" active-class="active" title="Statistics">
            Stats
          </router-link>
          <router-link to="/settings" class="nav-item" active-class="active" title="Settings">
            Settings
          </router-link>
          <a :href="`${cleanRoot}/download`" target="_blank" class="nav-item" title="Download the log">
            Download
          </a>
          <a href="https://github.com/meoptimus/ElmahCoreX" target="_blank" class="nav-item" title="Help">
            Help
          </a>
          <button class="nav-item btn-link-style" @click="showAboutModal = true" title="About">
            About
          </button>
        </nav>
      </div>

      <div class="header-right">
        <span class="env-badge" :class="envType">{{ envLabel }}</span>

        <div class="refresh-control" title="Auto refresh">
          <select :value="store.autoRefreshInterval" @change="changeAutoRefresh" aria-label="Auto refresh interval">
            <option :value="0">Manual</option>
            <option :value="10">Every 10s</option>
            <option :value="30">Every 30s</option>
            <option :value="60">Every 60s</option>
          </select>
        </div>

        <button class="icon-btn" title="Refresh now (R)" aria-label="Refresh now" @click="refreshCurrent">
          <i class="pi pi-refresh" :class="{ 'pi-spin': store.loading }"></i>
        </button>

        <button
          class="icon-btn"
          :title="theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'"
          :aria-label="theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'"
          @click="toggleTheme"
        >
          <i class="pi" :class="theme === 'dark' ? 'pi-sun' : 'pi-moon'"></i>
        </button>

        <span class="version-tag">v{{ appVersion }}</span>
      </div>
    </header>

    <main class="content-body">
      <router-view v-slot="{ Component }">
        <transition name="fade" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </main>

    <Toast />

    <!-- About Modal Dialog -->
    <Transition name="fade">
      <div v-if="showAboutModal" class="about-modal-overlay" @click.self="showAboutModal = false">
        <div class="about-modal-card" role="dialog" aria-modal="true" aria-label="About ElmahCoreX">
          <button class="about-close-btn" aria-label="Close" @click="showAboutModal = false">
            <i class="pi pi-times"></i>
          </button>
          <div class="about-header">
            <h2>ElmahCoreX</h2>
            <span class="about-version">v{{ appVersion }}</span>
          </div>
          <p class="about-lead">Error logging and a log viewer for ASP.NET Core applications.</p>
          <div class="about-info-grid">
            <div class="info-row">
              <span class="label">Repository</span>
              <span class="val"><a href="https://github.com/meoptimus/ElmahCoreX" target="_blank">meoptimus/ElmahCoreX</a></span>
            </div>
            <div class="info-row">
              <span class="label">Built with</span>
              <span class="val">Vue 3 + Vite, .NET 8 / .NET 9</span>
            </div>
            <div class="info-row">
              <span class="label">Includes</span>
              <span class="val">Search and filters, bulk delete, statistics, auto refresh</span>
            </div>
          </div>
          <div class="about-footer">
            <button class="btn btn-primary" @click="showAboutModal = false">Close</button>
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
          <button class="toast-close" aria-label="Dismiss">
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

const appVersion = '2.2.15';

const removeToast = (id) => {
  store.removeNotification(id);
};

const elmahRoot = window.$elmah_root || '/elmah';
const cleanRoot = '/' + elmahRoot.replace(/^\/|\/$/g, '');
const showAboutModal = ref(false);

// The list and the detail route share one view, so both light up "Errors".
const isErrorsRoute = computed(() => route.name === 'errors' || route.name === 'detail');

const envType = computed(() => {
  const host = window.location.hostname;
  if (host === 'localhost' || host === '127.0.0.1') return 'development';
  if (host.includes('staging') || host.includes('test')) return 'staging';
  return 'production';
});

const envLabel = computed(() => envType.value.charAt(0).toUpperCase() + envType.value.slice(1));

// Theme: a saved choice wins, otherwise follow the operating system.
const readSavedTheme = () => {
  try {
    return localStorage.getItem('theme');
  } catch {
    return null;
  }
};

const systemPrefersDark = () =>
  window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;

const theme = ref(readSavedTheme() || (systemPrefersDark() ? 'dark' : 'light'));

const applyTheme = () => {
  document.documentElement.classList.toggle('dark-mode', theme.value === 'dark');
};
applyTheme();

const toggleTheme = () => {
  theme.value = theme.value === 'dark' ? 'light' : 'dark';
  applyTheme();
  try {
    localStorage.setItem('theme', theme.value);
  } catch {
    /* storage unavailable: the choice lasts for this visit only */
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

const handleKeyDown = (e) => {
  if (e.key === 'Escape' && showAboutModal.value) {
    showAboutModal.value = false;
    return;
  }
  if (e.key === 'r' || e.key === 'R') {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    if (document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'SELECT' && document.activeElement.tagName !== 'TEXTAREA') {
      refreshCurrent();
    }
  }
};

onMounted(() => {
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
/* App shell */
.app-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  height: 100dvh;
  width: 100%;
}

.offline-banner {
  background-color: var(--danger);
  color: #ffffff;
  padding: 6px 12px;
  font-weight: 600;
  font-size: 12.5px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

/* Header */
.main-header {
  flex-shrink: 0;
  height: 52px;
  background-color: var(--header-bg);
  border-bottom: 1px solid var(--header-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 0 16px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 20px;
  min-width: 0;
}

.logo {
  display: flex;
  align-items: center;
  gap: 9px;
  text-decoration: none;
  flex-shrink: 0;
}

.logo-frame-top {
  fill: var(--logo-frame-top);
}

.logo-frame {
  fill: var(--logo-frame);
}

.logo-frame-dim {
  opacity: 0.55;
}

.logo-text {
  font-weight: 700;
  font-size: 15px;
  letter-spacing: -0.01em;
  color: var(--header-text);
}

.top-nav {
  display: flex;
  align-items: center;
  gap: 2px;
}

.nav-item {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 32px;
  padding: 0 11px;
  border-radius: var(--radius);
  color: var(--header-muted);
  text-decoration: none;
  font-weight: 500;
  font-size: 13px;
  white-space: nowrap;
  transition: background-color 0.12s, color 0.12s;
}

.nav-item:hover {
  background-color: var(--header-hover);
  color: var(--header-text);
}

.nav-item.active {
  background-color: var(--header-active);
  color: var(--header-active-text);
}

.nav-count {
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 700;
  line-height: 18px;
  min-width: 22px;
  padding: 0 6px;
  border-radius: 9px;
  text-align: center;
  background-color: var(--danger-soft);
  color: var(--danger-text);
}

.btn-link-style {
  background: none;
  border: none;
  cursor: pointer;
  font-family: inherit;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.env-badge {
  padding: 0 8px;
  line-height: 20px;
  border-radius: 10px;
  border: 1px solid var(--header-border);
  color: var(--header-muted);
  font-size: 11.5px;
  font-weight: 500;
}

.env-badge.production {
  border-color: var(--danger-border);
  color: var(--danger-text);
}

.refresh-control select {
  height: 30px;
  background-color: transparent;
  border: 1px solid var(--header-border);
  border-radius: var(--radius);
  color: var(--header-muted);
  font-size: 12px;
  font-weight: 500;
  padding: 0 6px;
  cursor: pointer;
}

.refresh-control select:hover {
  color: var(--header-text);
}

.refresh-control select option {
  background-color: var(--header-bg);
  color: var(--header-text);
}

.icon-btn {
  width: 30px;
  height: 30px;
  background: none;
  border: none;
  border-radius: var(--radius);
  color: var(--header-muted);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.12s, color 0.12s;
}

.icon-btn i {
  font-size: 14px;
}

.icon-btn:hover {
  background-color: var(--header-hover);
  color: var(--header-text);
}

.main-header :focus-visible {
  box-shadow: 0 0 0 2px var(--header-bg), 0 0 0 4px var(--accent);
}

.version-tag {
  font-size: 11px;
  font-family: var(--font-mono);
  color: var(--header-muted);
  opacity: 0.75;
  padding-left: 8px;
}

.content-body {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.stats-dashboard-view,
.settings-view {
  overflow-y: auto;
  flex: 1;
  height: 100%;
}

/* Page transition */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.12s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* About dialog */
.about-modal-overlay {
  position: fixed;
  inset: 0;
  background-color: rgba(12, 10, 28, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  z-index: 2000;
}

.about-modal-card {
  position: relative;
  width: 100%;
  max-width: 440px;
  padding: 24px;
  background-color: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-pop);
  color: var(--text);
}

.about-close-btn {
  position: absolute;
  top: 14px;
  right: 14px;
  width: 28px;
  height: 28px;
  background: none;
  border: none;
  border-radius: var(--radius);
  color: var(--text-3);
  cursor: pointer;
}

.about-close-btn:hover {
  background-color: var(--surface-hover);
  color: var(--text);
}

.about-header {
  display: flex;
  align-items: baseline;
  gap: 10px;
}

.about-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.01em;
}

.about-version {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--text-3);
}

.about-lead {
  margin: 8px 0 18px;
  color: var(--text-2);
  font-size: 13.5px;
}

.about-info-grid {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 2px 14px;
}

.about-info-grid .info-row {
  display: flex;
  justify-content: space-between;
  gap: 20px;
  padding: 10px 0;
  font-size: 13px;
  border-bottom: 1px solid var(--border);
}

.about-info-grid .info-row:last-child {
  border-bottom: none;
}

.about-info-grid .label {
  color: var(--text-3);
  flex-shrink: 0;
}

.about-info-grid .val {
  color: var(--text);
  text-align: right;
}

.about-info-grid .val a {
  text-decoration: none;
}

.about-info-grid .val a:hover {
  text-decoration: underline;
}

.about-footer {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

/* New-error notifications */
.toaster-container {
  position: fixed;
  bottom: 20px;
  right: 20px;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  gap: 10px;
  pointer-events: none;
}

.toast-item {
  pointer-events: auto;
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 280px;
  max-width: 400px;
  padding: 10px 12px;
  background-color: var(--surface);
  border: 1px solid var(--border);
  border-left: 3px solid var(--accent);
  border-radius: var(--radius);
  box-shadow: var(--shadow-pop);
  cursor: pointer;
  transition: opacity 0.25s ease, transform 0.25s ease;
}

.toast-item.severity-info {
  border-left-color: var(--info);
}

.toast-item.severity-success {
  border-left-color: var(--ok);
}

.toast-item.severity-warning {
  border-left-color: var(--warn);
}

.toast-item.severity-error {
  border-left-color: var(--danger);
}

.toast-icon {
  display: flex;
  color: var(--accent);
  font-size: 14px;
}

.toast-item.severity-info .toast-icon {
  color: var(--info);
}

.toast-item.severity-success .toast-icon {
  color: var(--ok);
}

.toast-item.severity-warning .toast-icon {
  color: var(--warn);
}

.toast-item.severity-error .toast-icon {
  color: var(--danger);
}

.toast-content {
  flex: 1;
  min-width: 0;
}

.toast-message {
  font-size: 12.5px;
  font-weight: 500;
  color: var(--text);
}

.toast-close {
  background: none;
  border: none;
  color: var(--text-3);
  cursor: pointer;
  padding: 2px;
  display: flex;
}

.toast-close:hover {
  color: var(--text);
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(40px);
}

@media (max-width: 900px) {
  .env-badge,
  .version-tag {
    display: none;
  }
}

@media (max-width: 768px) {
  /* Auto refresh stays reachable under Settings; the header has no room for it here. */
  .logo-text,
  .refresh-control {
    display: none;
  }

  .main-header {
    padding: 0 8px;
    gap: 8px;
  }

  .header-left {
    gap: 8px;
    flex: 1;
  }

  .header-right {
    gap: 2px;
  }

  .top-nav {
    overflow-x: auto;
    flex: 1;
    min-width: 0;
    scrollbar-width: none;
    -webkit-overflow-scrolling: touch;
    /* Fade the edge so a cut-off link reads as "scroll for more", not as a bug. */
    -webkit-mask-image: linear-gradient(to right, #000 calc(100% - 24px), transparent);
    mask-image: linear-gradient(to right, #000 calc(100% - 24px), transparent);
    padding-right: 20px;
  }

  .top-nav::-webkit-scrollbar {
    display: none;
  }

  .toaster-container {
    left: 12px;
    right: 12px;
    bottom: 12px;
  }

  .toast-item {
    min-width: 0;
    max-width: none;
  }
}
</style>
