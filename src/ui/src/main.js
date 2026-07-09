// URL Normalization for detail routes
(() => {
  const path = window.location.pathname;
  const search = window.location.search;
  
  // 1. Check for REST-style URL: /elmah/detail/{id}
  const detailRegex = /\/detail\/([a-f0-9-]+)/i;
  const match = path.match(detailRegex);
  if (match) {
    const id = match[1];
    const rootPath = path.substring(0, path.indexOf('/detail/'));
    window.location.href = window.location.origin + rootPath + '#/detail/' + id;
    return;
  }
  
  // 2. Check for query-style URL: /elmah/detail?id={id}
  if (path.endsWith('/detail') || path.endsWith('/detail/')) {
    const urlParams = new URLSearchParams(search);
    const id = urlParams.get('id');
    if (id) {
      const rootPath = path.replace(/\/detail\/?$/, '');
      window.location.href = window.location.origin + rootPath + '#/detail/' + id;
      return;
    }
  }
})();

import { createApp } from 'vue';
import { createPinia } from 'pinia';
import PrimeVue from 'primevue/config';
import Aura from '@primevue/themes/aura';
import ToastService from 'primevue/toastservice';
import App from './App.vue';
import { router } from './router';

import 'primeicons/primeicons.css';
import './style.css';

const app = createApp(App);

app.use(createPinia());
app.use(router);
app.use(PrimeVue, {
  theme: {
    preset: Aura,
    options: {
      darkModeSelector: '.dark-mode'
    }
  }
});
app.use(ToastService);

app.mount('#app');
