<template>
  <div class="stats-dashboard-view">
    <div v-if="loading" class="loading-state card">
      <i class="pi pi-spin pi-spinner loading-icon"></i>
      <p>Analyzing error logs and compiling statistics...</p>
    </div>

    <div v-else-if="errors.length === 0" class="empty-state card">
      <i class="pi pi-chart-bar empty-icon"></i>
      <h3>No Data Available</h3>
      <p>Please log some errors to see statistical dashboard insights.</p>
    </div>

    <div v-else class="dashboard-grid">
      <!-- Background Analysis Control Bar -->
      <div v-if="totalErrorsInDb > 500 || isAnalyzingAll" class="analysis-control-bar card">
        <div class="analysis-info">
          <span v-if="!isAnalyzingAll">
            Currently analyzing the latest <strong>{{ errors.length }}</strong> errors.
            Total errors in database: <strong>{{ totalErrorsInDb }}</strong>.
          </span>
          <span v-else>
            Analyzing all errors... Loaded <strong>{{ errors.length }}</strong> of <strong>{{ totalErrorsInDb }}</strong>
            ({{ Math.round((errors.length / totalErrorsInDb) * 100) }}%)
          </span>
        </div>
        <div class="analysis-actions">
          <button v-if="!isAnalyzingAll && errors.length < totalErrorsInDb" class="btn btn-primary" @click="startBackgroundAnalysis">
            <i class="pi pi-bolt mr-1"></i> Analyze All Errors
          </button>
          <button v-if="isAnalyzingAll" class="btn btn-danger" @click="stopBackgroundAnalysis">
            <i class="pi pi-times mr-1"></i> Stop Analysis
          </button>
        </div>
      </div>
      <div v-if="isAnalyzingAll" class="progress-bar-container">
        <div class="progress-bar-fill" :style="{ width: (errors.length / totalErrorsInDb * 100) + '%' }"></div>
      </div>
      <!-- 1. Key Metrics Cards -->
      <div class="metrics-row">
        <div class="metric-card card">
          <span class="metric-label">Total Logs Analyzed</span>
          <span class="metric-value">{{ errors.length }}</span>
        </div>
        <div class="metric-card card">
          <span class="metric-label">Unique Exception Types</span>
          <span class="metric-value">{{ uniqueTypesCount }}</span>
        </div>
        <div class="metric-card card clickable-card" @click="filterByUnreviewed">
          <span class="metric-label">Unreviewed Errors</span>
          <span class="metric-value text-danger">{{ unreviewedCount }}</span>
        </div>
        <div class="metric-card card clickable-card" @click="filterByType(topExceptionName)">
          <span class="metric-label">Most Common Exception</span>
          <span class="metric-value text-primary truncate-text" :title="topExceptionName">
            {{ topExceptionNameShort }}
          </span>
        </div>
      </div>

      <!-- 2. Charts Section -->
      <div class="charts-row">
        <!-- Error Rate Chart -->
        <div class="chart-card card col-span-2">
          <h3 class="chart-title"><i class="pi pi-chart-line mr-1"></i> Error Rate Trend</h3>
          <div class="chart-container">
            <canvas ref="trendChartCanvas"></canvas>
          </div>
        </div>

        <!-- Exception Types Distribution -->
        <div class="chart-card card">
          <h3 class="chart-title"><i class="pi pi-align-left mr-1"></i> Top Exceptions</h3>
          <div class="chart-container">
            <canvas ref="typeChartCanvas"></canvas>
          </div>
        </div>

        <!-- Status Code Distribution -->
        <div class="chart-card card">
          <h3 class="chart-title"><i class="pi pi-percentage mr-1"></i> Status Code Breakdown</h3>
          <div class="chart-container">
            <canvas ref="statusChartCanvas"></canvas>
          </div>
        </div>
      </div>

      <!-- 3. Tables/Lists Section -->
      <div class="lists-row">
        <!-- Top Impacted Endpoints -->
        <div class="list-card card">
          <h3 class="list-title"><i class="pi pi-link mr-1"></i> Top 5 Affected URLs</h3>
          <ul class="stats-list">
            <li v-for="(item, idx) in topUrls" :key="idx" class="clickable-item">
              <router-link :to="{ path: '/', query: { message: item.name } }" class="stats-list-link">
                <span class="list-index">{{ idx + 1 }}</span>
                <div class="list-content">
                  <span class="list-name font-mono text-break" :title="item.name">{{ item.name || 'N/A' }}</span>
                  <span class="list-count">{{ item.count }} occurrences</span>
                </div>
              </router-link>
            </li>
          </ul>
        </div>

        <!-- Top Affected Users -->
        <div class="list-card card">
          <h3 class="list-title"><i class="pi pi-users mr-1"></i> Top 5 Impacted Users</h3>
          <ul class="stats-list" v-if="topUsers.length > 0">
            <li v-for="(item, idx) in topUsers" :key="idx" :class="{ 'clickable-item': item.name && item.name !== 'Anonymous' }">
              <router-link v-if="item.name && item.name !== 'Anonymous'" :to="{ path: '/', query: { user: item.name } }" class="stats-list-link">
                <span class="list-index">{{ idx + 1 }}</span>
                <div class="list-content">
                  <span class="list-name font-bold">{{ item.name }}</span>
                  <span class="list-count">{{ item.count }} occurrences</span>
                </div>
              </router-link>
              <div v-else class="stats-list-link-disabled">
                <span class="list-index">{{ idx + 1 }}</span>
                <div class="list-content">
                  <span class="list-name font-bold text-muted">{{ item.name || 'Anonymous' }}</span>
                  <span class="list-count">{{ item.count }} occurrences</span>
                </div>
              </div>
            </li>
          </ul>
          <div v-else class="empty-list-state">
            <p>No user variables logged.</p>
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

onBeforeUnmount(() => {
  isAnalyzingAll.value = false;
});

const renderCharts = () => {
  if (errors.value.length === 0) return;


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
          label: 'Error Rate',
          data: trendData,
          borderColor: '#4f46e5',
          backgroundColor: 'rgba(79, 70, 229, 0.1)',
          fill: true,
          tension: 0.3,
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          y: { beginAtZero: true, grid: { color: 'rgba(148, 163, 184, 0.1)' } },
          x: { grid: { color: 'rgba(148, 163, 184, 0.1)' } }
        }
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
    typeChart = new Chart(typeChartCanvas.value, {
      type: 'bar',
      data: {
        labels: topTypes.map(t => t[0]),
        datasets: [{
          data: topTypes.map(t => t[1]),
          backgroundColor: ['#ef4444', '#f59e0b', '#3b82f6', '#10b981', '#6366f1'],
          borderRadius: 6
        }]
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { beginAtZero: true, grid: { color: 'rgba(148, 163, 184, 0.1)' } },
          y: { grid: { display: false } }
        }
      }
    });
  }


  const statusCounts = {};
  errors.value.forEach(e => {
    const code = e.error.statusCode || 500;
    statusCounts[code] = (statusCounts[code] || 0) + 1;
  });

  if (statusChartCanvas.value) {
    if (statusChart) statusChart.destroy();
    statusChart = new Chart(statusChartCanvas.value, {
      type: 'doughnut',
      data: {
        labels: Object.keys(statusCounts).map(k => `HTTP ${k}`),
        datasets: [{
          data: Object.values(statusCounts),
          backgroundColor: ['#ef4444', '#f59e0b', '#10b981', '#3b82f6', '#8b5cf6', '#64748b']
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            labels: { boxWidth: 12, padding: 8 }
          }
        }
      }
    });
  }
};

onMounted(() => {
  loadStatsData();
});
</script>

<style scoped>
.stats-dashboard-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px 20px;
}

.card {
  background-color: #ffffff;
  border: 1px solid #e8e6e0;
  border-radius: 0;
  padding: 16px 20px;
  box-shadow: none;
}

.mb-3 {
  margin-bottom: 12px;
}

.font-mono {
  font-family: var(--font-mono), monospace;
}

.font-bold {
  font-weight: 500;
  color: #1a1a2e;
}

.text-danger {
  color: #d94f4f;
}

.text-primary {
  color: #4a7fc1;
}

.truncate-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}


/* Loading & Empty states */
.loading-state,
.empty-state {
  text-align: center;
  padding: 40px;
  color: #888780;
}

.loading-icon {
  font-size: 32px;
  color: #4a7fc1;
  margin-bottom: 16px;
}

.empty-icon {
  font-size: 40px;
  margin-bottom: 16px;
}

.dashboard-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* Metrics Row */
.metrics-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.metric-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  background-color: #ffffff;
  border: 1px solid #e8e6e0;
}

.metric-label {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #aaa9a3;
  font-weight: 600;
}

.metric-value {
  font-size: 24px;
  font-family: var(--font-mono), monospace;
  font-weight: 500;
  color: #1a1a2e;
}

/* Charts Section */
.charts-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 16px;
}

@media (min-width: 1024px) {
  .charts-row {
    grid-template-columns: repeat(2, 1fr);
  }
  .col-span-2 {
    grid-column: span 2;
  }
}

.chart-card {
  display: flex;
  flex-direction: column;
  height: 320px;
}

.chart-title {
  margin: 0 0 16px 0;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #aaa9a3;
  font-weight: 600;
  border-bottom: 1px solid #e8e6e0;
  padding-bottom: 8px;
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

/* Lists section */
.lists-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 16px;
}

.list-card {
  display: flex;
  flex-direction: column;
}

.list-title {
  margin: 0 0 16px 0;
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #aaa9a3;
  font-weight: 600;
  border-bottom: 1px solid #e8e6e0;
  padding-bottom: 8px;
}

.stats-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
}

.stats-list li {
  border-bottom: 1px solid #e8e6e0;
  padding: 0;
}

.stats-list li:last-child {
  border-bottom: none;
}

.stats-list-link {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  width: 100%;
  text-decoration: none;
  color: inherit;
}

.stats-list-link-disabled {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  width: 100%;
  color: inherit;
}

.text-muted {
  color: #aaa9a3;
}

/* Background analysis control bar & progress bar styles */
.analysis-control-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background-color: #fcfbfa;
  border-color: #e8e6e0;
  gap: 12px;
  flex-wrap: wrap;
}

.analysis-info {
  font-size: 13px;
  color: #5f5e5a;
}

.progress-bar-container {
  width: 100%;
  height: 4px;
  background-color: #eeecea;
  border-radius: 2px;
  overflow: hidden;
  margin-top: -12px;
  margin-bottom: 4px;
}

.progress-bar-fill {
  height: 100%;
  background-color: #4a7fc1;
  transition: width 0.2s ease;
}

.btn {
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 500;
  border-radius: 4px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border: 1px solid transparent;
  transition: all 0.15s ease;
}

.btn-primary {
  background-color: #4a7fc1;
  color: white;
  border-color: #3b6da6;
}

.btn-primary:hover {
  background-color: #3b6da6;
}

.btn-danger {
  background-color: #d94f4f;
  color: white;
  border-color: #c04343;
}

.btn-danger:hover {
  background-color: #c04343;
}

.list-index {
  background-color: #eeecea;
  color: #5f5e5a;
  width: 24px;
  height: 24px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 500;
  font-size: 11px;
  font-family: var(--font-mono), monospace;
}

.list-content {
  flex: 1;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.list-name {
  font-size: 13px;
  word-break: break-all;
  flex: 1;
  color: #1a1a2e;
}

.list-count {
  font-size: 12px;
  color: #888780;
  white-space: nowrap;
}

.empty-list-state {
  text-align: center;
  padding: 32px;
  color: #888780;
  font-size: 13px;
}

.clickable-card,
.clickable-item {
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease;
}

.clickable-card:hover {
  background-color: #eeecea;
  border-color: #aaa9a3;
}

.clickable-item:hover {
  background-color: #eeecea;
}

@media (max-width: 768px) {
  .stats-dashboard-view {
    padding: 10px;
    gap: 12px;
  }
  .metrics-row {
    grid-template-columns: repeat(2, 1fr);
    gap: 10px;
  }
  .metric-card {
    padding: 10px 12px;
  }
  .metric-value {
    font-size: 18px;
  }
  .charts-row,
  .lists-row {
    grid-template-columns: 1fr !important;
    gap: 12px;
  }
  .chart-card {
    grid-column: span 1 !important;
    height: 260px;
    padding: 12px;
  }
  .list-card {
    padding: 12px;
  }
}
</style>
