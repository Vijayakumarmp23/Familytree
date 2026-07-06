import { createApp } from 'vue'
import App from './App.vue'

// Vue Flow core + add-on styles (required for nodes, edges, controls, minimap).
import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'
import '@vue-flow/controls/dist/style.css'
import '@vue-flow/minimap/dist/style.css'

import './style.css'

createApp(App).mount('#app')
