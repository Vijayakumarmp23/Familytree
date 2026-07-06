import axios from 'axios'

/**
 * Shared Axios instance. Point baseURL at the ASP.NET Core API.
 * Change this if your backend runs on a different port.
 */
const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE || 'http://localhost:5000/api',
  headers: { 'Content-Type': 'application/json' }
})

export default api
