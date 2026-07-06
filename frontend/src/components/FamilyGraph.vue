<script setup>
import { watch, nextTick } from 'vue'
import { VueFlow, useVueFlow } from '@vue-flow/core'
import { Background } from '@vue-flow/background'
import { Controls } from '@vue-flow/controls'
import { MiniMap } from '@vue-flow/minimap'
import PersonNode from './PersonNode.vue'
import UnionNode from './UnionNode.vue'

/**
 * The genealogy canvas. Wraps vue-flow (which gives us zoom + pan + minimap for
 * free) and registers the custom person / union node types. Selection, collapse
 * and "center on person" are driven by the parent view.
 */
const props = defineProps({
  nodes: { type: Array, required: true },
  edges: { type: Array, required: true }
})

const emit = defineEmits(['select', 'toggle-collapse'])

const { fitView, setCenter, findNode, onNodeClick } = useVueFlow()

onNodeClick(({ node }) => {
  if (node.type === 'person') emit('select', node.data.person.id)
})

// Re-fit whenever the graph shape changes (add person, collapse, etc.).
watch(
  () => props.nodes.length,
  async () => {
    await nextTick()
    fitView({ padding: 0.2, duration: 400 })
  }
)

/** Smoothly center the viewport on a given person node. Exposed to the parent. */
function centerOnPerson(personId, zoom = 1.1) {
  const node = findNode(`p${personId}`)
  if (!node) return
  const x = node.position.x + (node.dimensions?.width || 190) / 2
  const y = node.position.y + (node.dimensions?.height || 78) / 2
  setCenter(x, y, { zoom, duration: 600 })
}

defineExpose({ centerOnPerson, fitView })
</script>

<template>
  <VueFlow
    :nodes="nodes"
    :edges="edges"
    :min-zoom="0.1"
    :max-zoom="2.5"
    :nodes-draggable="false"
    fit-view-on-init
    class="family-flow"
  >
    <template #node-person="nodeProps">
      <PersonNode v-bind="nodeProps" @toggle-collapse="id => emit('toggle-collapse', id)" />
    </template>
    <template #node-union="nodeProps">
      <UnionNode v-bind="nodeProps" />
    </template>

    <Background pattern-color="#d5dbe6" :gap="22" />
    <Controls />
    <MiniMap pannable zoomable />
  </VueFlow>
</template>
