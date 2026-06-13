import { createRouter, createWebHashHistory } from 'vue-router';
import ErrorsList from '../views/ErrorsList.vue';
import ErrorDetail from '../views/ErrorDetail.vue';
import StatsDashboard from '../views/StatsDashboard.vue';
import SettingsView from '../views/SettingsView.vue';

const routes = [
  { path: '/', name: 'errors', component: ErrorsList },
  { path: '/detail/:id', name: 'detail', component: ErrorDetail, props: true },
  { path: '/stats', name: 'stats', component: StatsDashboard },
  { path: '/settings', name: 'settings', component: SettingsView }
];

export const router = createRouter({
  history: createWebHashHistory(),
  routes
});
