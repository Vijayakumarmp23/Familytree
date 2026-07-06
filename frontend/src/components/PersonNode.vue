<script setup>
import { computed } from 'vue'
import { Handle, Position } from '@vue-flow/core'

/**
 * Custom vue-flow node rendering one person as a card. Marriage/parent edges
 * attach to the top (target) and bottom (source) handles.
 */
const props = defineProps({
  id: { type: String, required: true },
  data: { type: Object, required: true }
})

const person = computed(() => props.data.person)

const years = computed(() => {
  const p = person.value
  const birth = p.dateOfBirth ? new Date(p.dateOfBirth).getFullYear() : '?'
  if (!p.isAlive) {
    const death = p.dateOfDeath ? new Date(p.dateOfDeath).getFullYear() : ''
    return `${birth} – ${death || '†'}`
  }
  return `b. ${birth}`
})

const genderClass = computed(() => {
  const g = (person.value.gender || '').toLowerCase()
  if (g.startsWith('m')) return 'male'
  if (g.startsWith('f')) return 'female'
  return 'unknown'
})
</script>

<template>
  <div
    class="person-node"
    :class="[
      genderClass,
      { selected: data.selected, deceased: !person.isAlive },
      data.highlight === 'on' ? 'hl-on' : '',
      data.highlight === 'dim' ? 'hl-dim' : ''
    ]"
  >
    <Handle type="target" :position="Position.Top" />

    <div class="pn-name">{{ person.fullName }}</div>
    <div class="pn-meta">
      <span class="pn-years">{{ years }}</span>
      <span v-if="!person.isAlive" class="pn-badge">deceased</span>
    </div>

    <Handle type="source" :position="Position.Bottom" />
  </div>
</template>
