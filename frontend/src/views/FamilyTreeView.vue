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
  // Panel unmounts -> canvas grows back to full width. Wait for that resize to
  // settle (vue-flow re-measures via a ResizeObserver) before re-fitting, so
  // the tree animates back to its original framing.
  setTimeout(() => graphRef.value?.fitView({ padding: 0.12, duration: 500, maxZoom: 1.2 }), 160)
}

function centerOn(id) {
  graphRef.value?.centerOnPerson(id)
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
async function removePerson(person) {
  if (!confirm(`Remove ${person.fullName} from the tree? This also removes their relationship links.`)) return
  await familyService.deletePerson(person.id)
  closeDetail()
  await load()
}

// ---- Save edits: attributes + relationship reconciliation ----
// Current relatives are read from the loaded edge list; we only POST/DELETE the
// differences so no duplicate edges are ever created.
const currentChildren = id =>
  relationships.value.filter(r => r.relationshipType === 'ParentChild' && r.person1Id === id).map(r => r.person2Id)
const currentSpouses = id =>
  relationships.value
    .filter(r => r.relationshipType === 'Spouse' && (r.person1Id === id || r.person2Id === id))
    .map(r => (r.person1Id === id ? r.person2Id : r.person1Id))

const findEdgeId = (p1, p2, type) =>
  relationships.value.find(r => r.relationshipType === type && r.person1Id === p1 && r.person2Id === p2)?.id
const findSpouseEdgeId = (a, b) =>
  relationships.value.find(
    r => r.relationshipType === 'Spouse' &&
      ((r.person1Id === a && r.person2Id === b) || (r.person1Id === b && r.person2Id === a))
  )?.id

async function reconcile(current, desired, addFn, removeFn) {
  for (const id of desired) if (!current.includes(id)) await addFn(id)
  for (const id of current) if (!desired.includes(id)) await removeFn(id)
}

// Parents need the adoption flag: add/remove links AND re-create any kept link
// whose adopted/biological state changed (there is no PUT for relationships).
async function reconcileParents(id, desiredParentIds, adopted) {
  const currentEdges = relationships.value.filter(
    r => r.relationshipType === 'ParentChild' && r.person2Id === id
  )
  const currentParentIds = currentEdges.map(e => e.person1Id)
  for (const e of currentEdges) {
    if (!desiredParentIds.includes(e.person1Id)) {
      await familyService.deleteRelationship(e.id)
    } else if (!!e.isAdoptive !== !!adopted) {
      await familyService.deleteRelationship(e.id)
      await familyService.createRelationship(e.person1Id, id, 'ParentChild', adopted)
    }
  }
  for (const pid of desiredParentIds) {
    if (!currentParentIds.includes(pid)) {
      await familyService.createRelationship(pid, id, 'ParentChild', adopted)
    }
  }
}

async function savePerson(payload) {
  const id = payload.id
  // 1. attributes
  await familyService.updatePerson(id, payload.attributes)

  // 2. parents (person is the child) - carries the adopted flag
  await reconcileParents(id, payload.parentIds, payload.adopted)
  // 3. spouses (undirected)
  await reconcile(
    currentSpouses(id),
    payload.spouseIds,
    s => familyService.createRelationship(id, s, 'Spouse'),
    s => familyService.deleteRelationship(findSpouseEdgeId(id, s))
  )
  // 4. children (person is the parent)
  await reconcile(
    currentChildren(id),
    payload.childIds,
    c => familyService.createRelationship(id, c, 'ParentChild'),
    c => familyService.deleteRelationship(findEdgeId(id, c, 'ParentChild'))
  )

  await load(id)
}
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <div class="brand">🌳 Family Tree</div>
      <SearchBar @select="selectPerson" />
      <div class="toolbar">
        <button @click="graphRef?.fitView({ padding: 0.12, duration: 400, maxZoom: 1.2 })" title="Fit tree to screen">◱ Fit</button>
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
        />
        <div class="legend">
          <span><i class="sw male"></i> Male</span>
          <span><i class="sw female"></i> Female</span>
          <span><i class="sw union"></i> Marriage</span>
          <span><svg class="lg" viewBox="0 0 26 8"><line x1="1" y1="4" x2="25" y2="4" /></svg> Parent–child</span>
          <span><svg class="lg lg-dot" viewBox="0 0 26 8"><line x1="1" y1="4" x2="25" y2="4" /></svg> Adopted child</span>
          <span><svg class="lg lg-zig" viewBox="0 0 26 8"><polyline points="1,4 5,1 9,7 13,1 17,7 21,1 25,4" /></svg> Marriage within family</span>
        </div>
      </section>

      <PersonDetail
        v-if="detail"
        :detail="detail"
        :people="persons"
        :active-highlight="activeHighlight"
        @close="closeDetail"
        @select="selectPerson"
        @center="centerOn"
        @highlight="k => (activeHighlight = k)"
        @delete="removePerson"
        @save="savePerson"
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
