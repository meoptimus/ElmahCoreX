import { defineStore } from 'pinia';
import { elmahApi } from '../api/elmahApi';

export const useErrorStore = defineStore('error', {
  state: () => ({
    errors: [],
    totalCount: 0,
    totalUnfiltered: 0,
    totalFiltered: 0,
    loading: false,
    pageIndex: 0,
    pageSize: 25,
    filters: {
      type: '',
      message: '',
      host: '',
      user: '',
      statusCode: '',
      isReviewed: '',
      from: '',
      to: '',
      application: ''
    },
    selectedIds: [],
    autoRefreshInterval: 0,
    refreshTimer: null,
    backendOnline: true,
    notifications: []
  }),

  getters: {
    hasActiveFilters(state) {
      return Object.values(state.filters).some(val => val !== '' && val !== null && val !== undefined);
    }
  },

  actions: {
    async fetchErrors(append = false) {
      this.loading = true;
      try {
        const res = await elmahApi.getErrors(this.pageIndex, this.pageSize, this.filters);
        if (res.success && res.data) {
          const newErrors = res.data.errors || [];
          if (append) {
            const existingIds = new Set(this.errors.map(e => e.id));
            const filteredNew = newErrors.filter(e => !existingIds.has(e.id));
            this.errors.push(...filteredNew);
          } else {
            this.errors = newErrors;
          }
          this.totalCount = res.data.totalCount || 0;
        }
        this.backendOnline = true;
      } catch (err) {
        console.error(err);
        this.backendOnline = false;
      } finally {
        this.loading = false;
      }
    },

    async refreshCurrentLoaded() {
      try {
        const pageSizeToLoad = this.errors.length || this.pageSize;
        const res = await elmahApi.getErrors(0, pageSizeToLoad, this.filters);
        if (res.success && res.data) {
          const newErrors = res.data.errors || [];
          

          if (this.errors.length > 0) {
            const oldIds = new Set(this.errors.map(e => e.id));
            const newAdded = newErrors.filter(e => !oldIds.has(e.id));
            if (newAdded.length > 0) {
              this.showNewErrorNotification(newAdded);
            }
          }

          this.errors = newErrors;
          this.totalCount = res.data.totalCount || 0;
        }
        this.backendOnline = true;
      } catch (err) {
        console.error(err);
        this.backendOnline = false;
      }
    },

    async fetchCounts() {
      try {
        const res = await elmahApi.getCounts(this.filters);
        if (res.success && res.data) {
          this.totalUnfiltered = res.data.total || 0;
          this.totalFiltered = res.data.filtered || 0;
        }
        this.backendOnline = true;
      } catch (err) {
        console.error(err);
        this.backendOnline = false;
      }
    },

    setFilter(key, value) {
      this.filters[key] = value;
      this.pageIndex = 0;
      this.fetchErrors();
      this.fetchCounts();
    },

    clearFilters() {
      Object.keys(this.filters).forEach(key => {
        this.filters[key] = '';
      });
      this.pageIndex = 0;
      this.fetchErrors();
      this.fetchCounts();
    },

    async toggleReview(id, isReviewed) {
      try {
        await elmahApi.setReviewed(id, isReviewed);
        const err = this.errors.find(e => e.id === id);
        if (err && err.error) {
          err.error.isReviewed = isReviewed;
        }
        this.fetchCounts();
      } catch (err) {
        console.error(err);
      }
    },

    async markSelectedReviewed(isReviewed) {
      if (this.selectedIds.length === 0) return;
      try {
        for (const id of this.selectedIds) {
          await elmahApi.setReviewed(id, isReviewed);
        }
        this.selectedIds = [];
        this.fetchErrors();
        this.fetchCounts();
      } catch (err) {
        console.error(err);
      }
    },

    async deleteSelected() {
      if (this.selectedIds.length === 0) return;
      try {
        await elmahApi.deleteErrors(this.selectedIds);
        this.selectedIds = [];
        this.pageIndex = 0;
        this.fetchErrors();
        this.fetchCounts();
      } catch (err) {
        console.error(err);
      }
    },

    async deleteAll() {
      try {
        await elmahApi.deleteAllErrors(this.filters.application);
        this.selectedIds = [];
        this.pageIndex = 0;
        this.fetchErrors();
        this.fetchCounts();
      } catch (err) {
        console.error(err);
      }
    },

    startAutoRefresh(seconds) {
      this.stopAutoRefresh();
      this.autoRefreshInterval = seconds;
      if (seconds > 0) {
        this.refreshTimer = setInterval(() => {
          this.refreshCurrentLoaded();
          this.fetchCounts();
        }, seconds * 1000);
      }
    },

    stopAutoRefresh() {
      this.autoRefreshInterval = 0;
      if (this.refreshTimer) {
        clearInterval(this.refreshTimer);
        this.refreshTimer = null;
      }
    },

    async testConnection() {
      try {
        const res = await elmahApi.ping();
        this.backendOnline = res.data === 'pong';
        return this.backendOnline;
      } catch {
        this.backendOnline = false;
        return false;
      }
    },

    showNewErrorNotification(newErrors) {
      const count = newErrors.length;
      const latestError = newErrors[0]?.error;
      const message = count === 1 
        ? `New error: ${latestError?.message || 'Logged successfully'}`
        : `${count} new errors have been logged`;
      
      const id = Date.now();
      this.notifications.push({
        id,
        message,
        type: latestError?.severity || 'Error',
        count
      });


      setTimeout(() => {
        this.removeNotification(id);
      }, 6000);
    },

    removeNotification(id) {
      this.notifications = this.notifications.filter(n => n.id !== id);
    }
  }
});
