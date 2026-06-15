import axios from 'axios';


const elmahRoot = window.$elmah_root || '/elmah';
const cleanRoot = '/' + elmahRoot.replace(/^\/|\/$/g, '');

const api = axios.create({
  baseURL: cleanRoot,
  headers: {
    'Content-Type': 'application/json'
  }
});


api.interceptors.response.use(
  (response) => {
    const resData = response.data;
    if (resData && resData.success === false) {
      return Promise.reject(new Error(resData.error || 'API Request failed'));
    }
    return resData;
  },
  (error) => {
    const msg = error.response?.data?.error || error.message || 'Network error';
    return Promise.reject(new Error(msg));
  }
);

export const elmahApi = {
  getErrors: (i = 0, s = 25, filters = {}) => {
    const params = new URLSearchParams({ i: i.toString(), s: s.toString() });
    Object.entries(filters).forEach(([key, val]) => {
      if (val !== undefined && val !== null && val !== '') {
        params.append(key, val.toString());
      }
    });
    return api.get('api/errors', { params });
  },

  getError: (id) => {
    return api.get('api/error', { params: { id } });
  },

  setReviewed: (id, isReviewed) => {
    return api.patch('api/error/review', null, { params: { id, isReviewed } });
  },

  deleteErrors: (ids) => {
    return api.delete('api/errors/bulk', { data: ids });
  },

  deleteAllErrors: (application = '') => {
    const params = {};
    if (application) params.application = application;
    return api.delete('api/errors/all', { params });
  },

  getCounts: (filters = {}) => {
    const params = new URLSearchParams();
    Object.entries(filters).forEach(([key, val]) => {
      if (val !== undefined && val !== null && val !== '') {
        params.append(key, val.toString());
      }
    });
    return api.get('api/count', { params });
  },

  ping: () => {
    return api.get('api/ping');
  },

  getExportUrl: (format, filters = {}) => {
    const params = new URLSearchParams({ format });
    Object.entries(filters).forEach(([key, val]) => {
      if (val !== undefined && val !== null && val !== '') {
        params.append(key, val.toString());
      }
    });
    return `${cleanRoot}/api/export?${params.toString()}`;
  }
};
