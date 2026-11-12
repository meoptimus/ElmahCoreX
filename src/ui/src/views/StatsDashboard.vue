<template>
  <div class="stats-dashboard-view">
    <div v-if="loading" class="loading-state card">
      <i class="pi pi-spin pi-spinner loading-icon"></i>
      <p>Reading the error log…</p>
    </div>

    <div v-else-if="errors.length === 0" class="empty-state card">
      <i class="pi pi-chart-bar empty-icon"></i>
      <h3>Nothing to chart yet</h3>
      <p>Statistics appear here once errors have been logged.</p>
    </div>

    <div v-else class="dashboard-grid">
      <!-- Background Analysis Control Bar -->
      <div v-if="totalErrorsInDb > 500 || isAnalyzingAll" class="analysis-control-bar card">
        <div class="analysis-info">
          <span v-if="!isAnalyzingAll">
            These figures cover the latest <strong>{{ errors.length }}</strong> of
            <strong>{{ totalErrorsInDb }}</strong> errors in the log.
          </span>
          <span v-else>
            Analysing the whole log: <strong>{{ errors.length }}</strong> of <strong>{{ totalErrorsInDb }}</strong>
            ({{ Math.round((errors.length / totalErrorsInDb) * 100) }}%)
          </span>
        </div>
        <div class="analysis-actions">
          <button v-if="!isAnalyzingAll && errors.length < totalErrorsInDb" class="btn btn-primary" @click="startBackgroundAnalysis">
            Analyse all errors
          </button>
          <button v-if="isAnalyzingAll" class="btn btn-secondary" @click="stopBackgroundAnalysis">
            <i class="pi pi-times"></i> Stop analysing
          </button>
        </div>
        <div v-if="isAnalyzingAll" class="progress-bar-container">
          <div class="progress-bar-fill" :style="{ width: (errors.length / totalErrorsInDb * 100) + '%' }"></div>
        </div>
      </div>

      <!-- 1. Key Metrics Cards -->
      <div class="metrics-row">
        <div class="metric-card card">
          <span class="metric-label">Errors analysed</span>
          <span class="metric-value">{{ errors.length }}</span>
        </div>
        <div class="metric-card card">
          <span class="metric-label">Exception types</span>
          <span class="metric-value">{{ uniqueTypesCount }}</span>
        </div>
        <div class="metric-card card clickable-card" @click="filterByUnreviewed" title="Show unreviewed errors">
          <span class="metric-label">Unreviewed errors</span>
          <span class="metric-value">{{ unreviewedCount }}</span>
        </div>
        <div class="metric-card card clickable-card" @click="filterByType(topExceptionName)" :title="`Show ${topExceptionName} errors`">
          <span class="metric-label">Most common exception</span>
          <span class="metric-value metric-value-text truncate-text">
            {{ topExceptionNameShort }}
          </span>
        </div>
      </div>

      <!-- 2. Charts Section -->
      <div class="charts-row">
        <!-- Error Rate Chart -->
        <div class="chart-card card col-span-2">
          <h3 class="chart-title">Errors per day</h3>
          <div class="chart-container">
            <canvas ref="trendChartCanvas" role="img" aria-label="Line chart of errors per day"></canvas>
          </div>
        </div>

        <!-- Exception Types Distribution -->
        <div class="chart-card card">
          <h3 class="chart-title">Most frequent exception types</h3>
          <div class="chart-container">
            <canvas ref="typeChartCanvas" role="img" aria-label="Bar chart of the most frequent exception types"></canvas>
          </div>
        </div>

        <!-- Status Code Distribution -->
        <div class="chart-card card">
          <h3 class="chart-title">Errors by status code</h3>
          <div class="chart-container">
            <canvas ref="statusChartCanvas" role="img" aria-label="Bar chart of errors by HTTP status code"></canvas>
          </div>
        </div>
      </div>

      <!-- 3. Tables/Lists Section -->
      <div class="lists-row">
        <!-- Top Impacted Endpoints -->
        <div class="list-card card">
          <h3 class="list-title">Most affected URLs</h3>
          <ul class="stats-list">
            <li v-for="(item, idx) in topUrls" :key="idx" class="clickable-item">
              <router-link :to="{ path: '/', query: { message: item.name } }" class="stats-list-link">
                <span class="list-index">{{ idx + 1 }}</span>
                <div class="list-content">
                  <span class="list-name font-mono text-break" :title="item.name">{{ item.name || 'N/A' }}</span>
                  <span class="list-count">{{ item.count }}</span>
                </div>
              </router-link>
            </li>
          </ul>
        </div>

        <!-- Top Affected Users -->
        <div class="list-card card">
          <h3 class="list-title">Most affected users</h3>
          <ul class="stats-list" v-if="topUsers.length > 0">
            <li v-for="(item, idx) in topUsers" :key="idx" :class="{ 'clickable-item': item.name && item.name !== 'Anonymous' }">
              <router-link v-if="item.name && item.name !== 'Anonymous'" :to="{ path: '/', query: { user: item.name } }" class="stats-list-link">
                <span class="list-index">{{ idx + 1 }}</span>
                <div class="list-content">
                  <span class="list-name">{{ item.name }}</span>
                  <span class="list-count">{{ item.count }}</span>
                </div>
              </router-link>
              <div v-else class="stats-list-link-disabled">
                <span class="list-index">{{ idx + 1 }}</span>
                <div class="list-content">
                  <span class="list-name text-muted">{{ item.name || 'Anonymous' }}</span>
                  <span class="list-count">{{ item.count }}</span>
                </div>
              </div>
            </li>
          </ul>
          <div v-else class="empty-list-state">
            <p>None of the analysed errors has a signed-in user.</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, nextTick, onBeforeUnmount } from 'vue';
import { useRouter } from 'vue-router';
import { useErrorStore } from '../stores/errorStore';
import { elmahApi } from '../api/elmahApi';
import Chart from 'chart.js/auto';
import dayjs from 'dayjs';

const store = useErrorStore();
const router = useRouter();

const errors = ref([]);
const loading = ref(true);
const totalErrorsInDb = ref(0);
const isAnalyzingAll = ref(false);


const trendChartCanvas = ref(null);
const typeChartCanvas = ref(null);
const statusChartCanvas = ref(null);


let trendChart = null;
let typeChart = null;
let statusChart = null;


const filterByUrl = (url) => {
  if (!url || url === 'N/A') return;
  store.clearFilters();
  store.setFilter('message', url);
  router.push('/');
};

const filterByUser = (user) => {
  if (!user || user === 'Anonymous') return;
  store.clearFilters();
  store.setFilter('message', user);
  router.push('/');
};

const filterByUnreviewed = () => {
  store.clearFilters();
  store.setFilter('isReviewed', 'false');
  router.push('/');
};

const filterByType = (type) => {
  if (!type || type === 'None') return;
  store.clearFilters();
  store.setFilter('type', type);
  router.push('/');
};

const uniqueTypesCount = computed(() => {
  const types = new Set(errors.value.map(e => e.error.type));
  return types.size;
});

const unreviewedCount = computed(() => {
  return errors.value.filter(e => !e.error.isReviewed).length;
});

const topExceptionName = computed(() => {
  const counts = {};
  errors.value.forEach(e => {
    const type = e.error.type || 'Unknown';
    counts[type] = (counts[type] || 0) + 1;
  });
  const sorted = Object.entries(counts).sort((a, b) => b[1] - a[1]);
  return sorted.length > 0 ? sorted[0][0] : 'None';
});

const topExceptionNameShort = computed(() => {
  const parts = topExceptionName.value.split('.');
  return parts[parts.length - 1];
});


const topUrls = computed(() => {
  const counts = {};
  errors.value.forEach(e => {

    const url = e.error.url || 'N/A';
    counts[url] = (counts[url] || 0) + 1;
  });
  return Object.entries(counts)
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 5);
});

const topUsers = computed(() => {
  const counts = {};
  errors.value.forEach(e => {
    if (e.error.user) {
      counts[e.error.user] = (counts[e.error.user] || 0) + 1;
    }
  });
  return Object.entries(counts)
    .map(([name, count]) => ({ name, count }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 5);
});


const loadStatsData = async () => {
  loading.value = true;
  try {

    const res = await elmahApi.getErrors(0, 500, {});
    if (res.success && res.data) {
      errors.value = res.data.errors || [];
      totalErrorsInDb.value = res.data.totalCount || errors.value.length;
    }


    loading.value = false;


    await nextTick();
    renderCharts();
  } catch (err) {
    console.error('Error loading statistics', err);
    loading.value = false;
  }
};

const startBackgroundAnalysis = async () => {
  if (isAnalyzingAll.value) return;
  isAnalyzingAll.value = true;
  
  try {
    let offset = errors.value.length;
    const limit = 500;
    
    while (offset < totalErrorsInDb.value && isAnalyzingAll.value) {
      const res = await elmahApi.getErrors(offset, limit, {});
      if (!res.success || !res.data) break;
      
      const newErrors = res.data.errors || [];
      if (newErrors.length === 0) break;
      
      errors.value = [...errors.value, ...newErrors];
      offset += newErrors.length;
      
      // Update charts incrementally
      renderCharts();
      
      // Add a tiny delay to keep UI responsive
      await new Promise(resolve => setTimeout(resolve, 50));
    }
  } catch (err) {
    console.error('Error in background statistics analysis', err);
  } finally {
    isAnalyzingAll.value = false;
  }
};

const stopBackgroundAnalysis = () => {
  isAnalyzingAll.value = false;
};

// Charts are drawn on canvas, so they cannot inherit CSS: read the theme tokens
// when drawing and redraw whenever the theme class on <html> changes.
const token = (name) =>
  getComputedStyle(document.documentElement).getPropertyValue(name).trim();

const themeObserver = new MutationObserver(() => renderCharts());

onBeforeUnmount(() => {
  isAnalyzingAll.value = false;
  themeObserver.disconnect();
  [trendChart, typeChart, statusChart].forEach(c => c && c.destroy());
});

const renderCharts = () => {
  if (errors.value.length === 0) return;

  const accent = token('--accent');
  const gridColor = token('--border');
  const animation = isAnalyzingAll.value ? false : undefined;

  Chart.defaults.color = token('--text-3');
  Chart.defaults.borderColor = gridColor;
  Chart.defaults.font.family = token('--font-sans');
  Chart.defaults.font.size = 12;

  const tooltip = {
    backgroundColor: token('--text'),
    titleColor: token('--surface'),
    bodyColor: token('--surface'),
    padding: 8,
    cornerRadius: 6,
    displayColors: false
  };

  const countScale = {
    beginAtZero: true,
    ticks: { precision: 0 },
    grid: { color: gridColor },
    border: { display: false }
  };

  const labelScale = { grid: { display: false }, border: { color: gridColor } };

  const barChart = (canvas, labels, data) => new Chart(canvas, {
    type: 'bar',
    data: {
      labels,
      datasets: [{
        label: 'Errors',
        data,
        backgroundColor: accent,
        borderRadius: 4,
        maxBarThickness: 20
      }]
    },
    options: {
      indexAxis: 'y',
      responsive: true,
      maintainAspectRatio: false,
      animation,
      plugins: { legend: { display: false }, tooltip },
      scales: { x: countScale, y: labelScale }
    }
  });


  const dailyCounts = {};
  for (let i = 6; i >= 0; i--) {
    const day = dayjs().subtract(i, 'day').format('MM-DD');
    dailyCounts[day] = 0;
  }
  errors.value.forEach(e => {
    const day = dayjs(e.error.time).format('MM-DD');
    if (dailyCounts[day] !== undefined) {
      dailyCounts[day]++;
    } else {
      dailyCounts[day] = 1;
    }
  });
  

  const trendLabels = Object.keys(dailyCounts).sort();
  const trendData = trendLabels.map(lbl => dailyCounts[lbl]);

  if (trendChartCanvas.value) {
    if (trendChart) trendChart.destroy();
    trendChart = new Chart(trendChartCanvas.value, {
      type: 'line',
      data: {
        labels: trendLabels,
        datasets: [{
          label: 'Errors',
          data: trendData,
          borderColor: accent,
          backgroundColor: token('--accent-soft'),
          fill: true,
          tension: 0.25,
          borderWidth: 2,
          pointRadius: 3,
          pointHoverRadius: 5,
          pointBackgroundColor: accent,
          pointBorderColor: token('--surface'),
          pointBorderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { display: false }, tooltip },
        scales: { y: countScale, x: labelScale }
      }
    });
  }


  const typeCounts = {};
  errors.value.forEach(e => {
    const type = e.error.type || 'Unknown';
    const shortName = type.split('.').pop();
    typeCounts[shortName] = (typeCounts[shortName] || 0) + 1;
  });
  const topTypes = Object.entries(typeCounts)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 5);

  if (typeChartCanvas.value) {
    if (typeChart) typeChart.destroy();
    typeChart = barChart(
      typeChartCanvas.value,
      topTypes.map(t => t[0]),
      topTypes.map(t => t[1])
    );
  }


  const statusCounts = {};
  errors.value.forEach(e => {
    const code = e.error.statusCode || 500;
    statusCounts[code] = (statusCounts[code] || 0) + 1;
  });
  // Most frequent first. Bars rather than a ring, so codes with few errors stay readable.
  const topStatuses = Object.entries(statusCounts)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 6);

  if (statusChartCanvas.value) {
    if (statusChart) statusChart.destroy();
    statusChart = barChart(
      statusChartCanvas.value,
      topStatuses.map(s => `HTTP ${s[0]}`),
      topStatuses.map(s => s[1])
    );
  }
};

onMounted(() => {
  loadStatsData();
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
});
</script>

<style scoped>
.stats-dashboard-view {
  padding: 20px;
}

.dashboard-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1400px;
  margin: 0 auto;
}

.card {
  background-color: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: 16px 18px;
}

.truncate-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Loading and empty states */
.loading-state,
.empty-state {
  max-width: 480px;
  margin: 48px auto 0;
  padding: 40px 24px;
  text-align: center;
  color: var(--text-2);
}

.empty-state h3 {
  margin: 0 0 4px;
  font-size: 15px;
  font-weight: 600;
  color: var(--text);
}

.loading-state p,
.empty-state p {
  margin: 0;
}

.loading-icon,
.empty-icon {
  font-size: 24px;
  margin-bottom: 12px;
  color: var(--text-3);
}

.loading-icon {
  color: var(--accent);
}

/* How much of the log these numbers cover */
.analysis-control-bar {
  position: relative;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  padding: 10px 14px;
  background-color: var(--surface-2);
  overflow: hidden;
}

.analysis-info {
  color: var(--text-2);
}

.analysis-info strong {
  color: var(--text);
  font-variant-numeric: tabular-nums;
}

.progress-bar-container {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  height: 3px;
  background-color: var(--border);
}

.progress-bar-fill {
  height: 100%;
  background-color: var(--accent);
  transition: width 0.2s ease;
}

/* Headline figures */
.metrics-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.metric-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.metric-label {
  font-size: 12.5px;
  color: var(--text-2);
}

.metric-value {
  font-size: 28px;
  font-weight: 600;
  line-height: 1.15;
  letter-spacing: -0.01em;
  color: var(--text);
  font-variant-numeric: tabular-nums;
}

/* A type name, not a number: set it smaller so it fits the tile */
.metric-value-text {
  font-family: var(--font-mono);
  font-size: 16px;
  line-height: 32px;
  letter-spacing: 0;
}

.clickable-card {
  cursor: pointer;
  transition: border-color 0.12s, background-color 0.12s;
}

.clickable-card:hover {
  border-color: var(--accent);
  background-color: var(--surface-hover);
}

/* Charts */
.charts-row,
.lists-row {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.col-span-2 {
  grid-column: span 2;
}

.chart-card {
  display: flex;
  flex-direction: column;
  height: 300px;
  min-width: 0;
}

.chart-title,
.list-title {
  margin: 0 0 12px;
  font-size: 13.5px;
  font-weight: 600;
  color: var(--text);
}

.chart-container {
  flex: 1;
  position: relative;
  min-height: 0;
  width: 100%;
}

.chart-container canvas {
  position: absolute;
  top: 0;
  left: 0;
  width: 100% !important;
  height: 100% !important;
}

/* Ranked lists */
.list-card {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.stats-list {
  list-style: none;
  padding: 0;
  margin: 0 -8px;
}

.stats-list li + li {
  border-top: 1px solid var(--border);
}

.stats-list-link,
.stats-list-link-disabled {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 9px 8px;
  border-radius: var(--radius);
  text-decoration: none;
  color: inherit;
}

.clickable-item .stats-list-link:hover {
  background-color: var(--surface-hover);
}

.list-index {
  width: 16px;
  flex-shrink: 0;
  text-align: right;
  color: var(--text-3);
  font-size: 12px;
  font-variant-numeric: tabular-nums;
}

.list-content {
  flex: 1;
  min-width: 0;
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 16px;
}

.list-name {
  flex: 1;
  min-width: 0;
  font-size: 12.5px;
  color: var(--text);
  overflow-wrap: anywhere;
}

.list-count {
  font-size: 13px;
  font-weight: 600;
  color: var(--text);
  font-variant-numeric: tabular-nums;
}

.empty-list-state {
  padding: 24px 0;
  color: var(--text-3);
}

.empty-list-state p {
  margin: 0;
}

@media (max-width: 1000px) {
  .metrics-row {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .stats-dashboard-view {
    padding: 12px;
  }

  .dashboard-grid,
  .metrics-row,
  .charts-row,
  .lists-row {
    gap: 12px;
  }

  .charts-row,
  .lists-row {
    grid-template-columns: minmax(0, 1fr);
  }

  .col-span-2 {
    grid-column: span 1;
  }

  .card {
    padding: 12px 14px;
  }

  .metric-value {
    font-size: 22px;
  }

  .metric-value-text {
    font-size: 14px;
    line-height: 26px;
  }

  .chart-card {
    height: 260px;
  }
}
</style>
