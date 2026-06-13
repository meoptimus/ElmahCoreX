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
        <div class="metric-card card">
          <span class="metric-label">Unreviewed Errors</span>
          <span class="metric-value text-danger">{{ unreviewedCount }}</span>
        </div>
        <div class="metric-card card">
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
            <li v-for="(item, idx) in topUrls" :key="idx">
              <span class="list-index">{{ idx + 1 }}</span>
              <div class="list-content">
                <span class="list-name font-mono text-break" :title="item.name">{{ item.name || 'N/A' }}</span>
                <span class="list-count">{{ item.count }} occurrences</span>
              </div>
            </li>
          </ul>
        </div>

        <!-- Top Affected Users -->
        <div class="list-card card">
          <h3 class="list-title"><i class="pi pi-users mr-1"></i> Top 5 Impacted Users</h3>
          <ul class="stats-list" v-if="topUsers.length > 0">
            <li v-for="(item, idx) in topUsers" :key="idx">
              <span class="list-index">{{ idx + 1 }}</span>
              <div class="list-content">
                <span class="list-name font-bold">{{ item.name || 'Anonymous' }}</span>
                <span class="list-count">{{ item.count }} occurrences</span>
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
import { ref, computed, onMounted, nextTick } from 'vue';
import { elmahApi } from '../api/elmahApi';
import Chart from 'chart.js/auto';
import dayjs from 'dayjs';

const errors = ref([]);
const loading = ref(true);

// Canvas Refs
const trendChartCanvas = ref(null);
const typeChartCanvas = ref(null);
const statusChartCanvas = ref(null);

// Chart Instances
let trendChart = null;
let typeChart = null;
let statusChart = null;

// Metrics Computations
const uniqueTypesCount = computed(() => {
  const types = errors.value.map(e => e.error.type);
  return new Set(types).size;
});

const unreviewedCount = computed(() => {
  return errors.value.filter(e => !e.error.isReviewed).length;
});

const topExceptionName = computed(() => {
  const counts = {};
  errors.value.forEach(e => {
    counts[e.error.type] = (counts[e.error.type] || 0) + 1;
  });
  const sorted = Object.entries(counts).sort((a, b) => b[1] - a[1]);
  return sorted.length > 0 ? sorted[0][0] : 'None';
});

const topExceptionNameShort = computed(() => {
  const parts = topExceptionName.value.split('.');
  return parts[parts.length - 1];
});

// Aggregations for Lists
const topUrls = computed(() => {
  const counts = {};
  errors.value.forEach(e => {
    // Reconstruct URL
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

// API data load
const loadStatsData = async () => {
  loading.value = true;
  try {
    // Load last 500 errors to create a solid metrics baseline
    const res = await elmahApi.getErrors(0, 500, {});
    if (res.success && res.data) {
      errors.value = res.data.errors || [];
    }

    // Render charts
    await nextTick();
    renderCharts();
  } catch (err) {
    console.error('Error loading statistics', err);
  } finally {
    loading.value = false;
  }
};

const renderCharts = () => {
  if (errors.value.length === 0) return;

  // 1. Error Rate trend over days
  const dailyCounts = {};
  errors.value.forEach(e => {
    const day = dayjs(e.error.time).format('MM-DD');
    dailyCounts[day] = (dailyCounts[day] || 0) + 1;
  });
  
  // Sort keys chronologically
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

  // 2. Exception Types Top Bar Chart
  const typeCounts = {};
  errors.value.forEach(e => {
    const shortName = e.error.type.split('.').pop();
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

  // 3. Status Code Breakdown
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
  gap: 1.5rem;
}

.card {
  background-color: var(--panel-bg);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03);
}

.mb-3 {
  margin-bottom: 0.75rem;
}

.mr-1 {
  margin-right: 0.25rem;
}

.font-mono {
  font-family: SFMono-Regular, Consolas, Monaco, monospace;
}

.font-bold {
  font-weight: 700;
}

.text-danger {
  color: var(--error-color);
}

.text-primary {
  color: var(--primary-color);
}

.truncate-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.col-span-2 {
  grid-column: span 2;
}

/* Loading & Empty states */
.loading-state,
.empty-state {
  text-align: center;
  padding: 4rem;
  color: var(--text-light);
}

.loading-icon {
  font-size: 2.5rem;
  color: var(--primary-color);
  margin-bottom: 1rem;
}

.empty-icon {
  font-size: 3.5rem;
  margin-bottom: 1rem;
}

/* Metrics Row */
.metrics-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.25rem;
}

.metric-card {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.metric-label {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--text-light);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.metric-value {
  font-size: 1.75rem;
  font-weight: 800;
}

/* Charts Section */
.charts-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1.25rem;
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
  margin: 0 0 1rem 0;
  font-size: 1rem;
  font-weight: 700;
  color: var(--text-color);
}

.chart-container {
  flex: 1;
  position: relative;
  min-height: 0;
}

/* Lists section */
.lists-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1.25rem;
}

.list-card {
  display: flex;
  flex-direction: column;
}

.list-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  font-weight: 700;
}

.stats-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.stats-list li {
  display: flex;
  align-items: center;
  gap: 1rem;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 0.5rem;
}

.stats-list li:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.list-index {
  background-color: var(--primary-light);
  color: var(--primary-color);
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 0.8rem;
}

.list-content {
  flex: 1;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.list-name {
  font-size: 0.875rem;
  word-break: break-all;
  flex: 1;
}

.list-count {
  font-size: 0.8rem;
  color: var(--text-light);
  white-space: nowrap;
}

.empty-list-state {
  text-align: center;
  padding: 2rem;
  color: var(--text-light);
  font-size: 0.9rem;
}
</style>
