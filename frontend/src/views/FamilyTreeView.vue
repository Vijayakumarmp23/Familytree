<script setup>
/**
 * Orchestrator screen. Loads the persons (nodes) + relationships (edges), builds
 * the genealogy model, converts it to a vue-flow graph with union nodes, and
 * wires up selection, lineage highlighting, collapse/expand, search, centering
 * and data entry.
 */
import { ref, computed, onMounted } from 'vue'
import { familyService } from '../services/familyService'
import {
  buildGenealogy,
  ancestorsOf,
  descendantsOf,
  lineageOf
} from '../graph/genealogy'
import { buildFlowGraph } from '../graph/buildFlowGraph'
import FamilyGraph from '../components/FamilyGraph.vue'
import PersonDetail from '../components/PersonDetail.vue'
import PersonForm from '../components/PersonForm.vue'
import SearchBar from '../components/SearchBar.vue'

const persons = ref([])
const relationships = ref([])
const genealogy = ref(null)

const selectedId = ref(null)
const detail = ref(null)
const activeHighlight = ref(null) // 'ancestors' | 'descendants' | 'lineage' | null
const collapsedIds = ref(new Set())
const showForm = ref(false)
const loading = ref(true)
const error = ref(null)

const graphRef = ref(null)

// ---- Data loading ----
async function load(selectAfter = null) {
  loading.value = true
  error.value = null
  try {
    const [p, r] = await Promise.all([
      familyService.getPersons(),
      familyService.getRelationships()
    ])
    persons.value = p
    relationships.value = r
    genealogy.value = buildGenealogy(p, r)
    if (selectAfter) await selectPerson(selectAfter)
    else if (selectedId.value) detail.value = await familyService.getPerson(selectedId.value)
  } catch (e) {
    error.value = e?.message || 'Failed to load family data.'
  } finally {
    loading.value = false
  }
}

onMounted(() => load())

// ---- Lineage highlight set ----
const highlight = computed(() => {
  if (!activeHighlight.value || selectedId.value == null || !genealogy.value) return null
  const g = genealogy.value
  const id = selectedId.value
  let nodes
  if (activeHighlight.value === 'ancestors') {
    nodes = ancestorsOf(g, id)
    nodes.add(id)
  } else if (activeHighlight.value === 'descendants') {
    nodes = descendantsOf(g, id)
    nodes.add(id)
  } else {
    nodes = lineageOf(g, id)
  }
  return { nodes }
})

// ---- The vue-flow graph (nodes + edges), rebuilt reactively ----
const flow = computed(() => {
  if (!genealogy.value) return { nodes: [], edges: [] }
  return buildFlowGraph(genealogy.value, {
    collapsedIds: collapsedIds.value,
    highlight: highlight.value,
    selectedId: selectedId.value
  })
})

// ---- Selection ----
async function selectPerson(id) {
  selectedId.value = id
  detail.value = await familyService.getPerson(id)
  // wait a tick for the graph to reflect the new selection, then recenter
  requestAnimationFrame(() => graphRef.value?.centerOnPerson(id))
}

function closeDetail() {
  selectedId.value = null
  detail.value = null
  activeHighlight.value = null
}

function centerOn(id) {
  graphRef.value?.centerOnPerson(id)
}

// ---- Collapse / expand ----
function toggleCollapse(id) {
  const next = new Set(collapsedIds.value)
  next.has(id) ? next.delete(id) : next.add(id)
  collapsedIds.value = next
}
function collapseAll() {
  // Collapse every person who has children -> only the oldest generation stays open.
  const next = new Set()
  for (const [parentId, kids] of genealogy.value.childrenOf) {
    if (kids.size) next.add(parentId)
  }
  collapsedIds.value = next
}
function expandAll() {
  collapsedIds.value = new Set()
}

// ---- Data entry ----
async function addPerson(payload) {
  showForm.value = false
  const created = await familyService.createPerson(payload)
  await load(created.id)
}
async function marry({ person1Id, person2Id }) {
  showForm.value = false
  await familyService.createRelationship(person1Id, person2Id, 'Spouse')
  await load()
}
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <div class="brand">🌳 Family Tree</div>
      <SearchBar @select="selectPerson" />
      <div class="toolbar">
        <button @click="expandAll" title="Expand all generations">⤢ Expand all</button>
        <button @click="collapseAll" title="Collapse all generations">⤡ Collapse all</button>
        <button @click="graphRef?.fitView({ padding: 0.2, duration: 400 })" title="Fit tree to screen">◱ Fit</button>
        <button class="primary" @click="showForm = true">+ Add / Marry</button>
      </div>
    </header>

    <main class="workspace">
      <section class="canvas">
        <div v-if="loading" class="overlay">Loading family…</div>
        <div v-else-if="error" class="overlay error">{{ error }}</div>
        <FamilyGraph
          v-else
          ref="graphRef"
          :nodes="flow.nodes"
          :edges="flow.edges"
          @select="selectPerson"
          @toggle-collapse="toggleCollapse"
        />
        <div class="legend">
          <span><i class="sw male"></i> Male</span>
          <span><i class="sw female"></i> Female</span>
          <span><i class="sw union"></i> Marriage</span>
        </div>
      </section>

      <PersonDetail
        :detail="detail"
        :active-highlight="activeHighlight"
        @close="closeDetail"
        @select="selectPerson"
        @center="centerOn"
        @highlight="k => (activeHighlight = k)"
      />
    </main>

    <PersonForm
      v-if="showForm"
      :people="persons"
      @close="showForm = false"
      @add-person="addPerson"
      @marry="marry"
    />
  </div>
</template>
