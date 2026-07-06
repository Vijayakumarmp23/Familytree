<script setup>
import { computed } from 'vue'

/**
 * Side panel for the selected person. All related people (parents, spouses,
 * children, siblings) come from the backend's derived detail endpoint, so the
 * same person is never duplicated. Buttons drive the lineage highlighting in
 * the graph and let you jump to any relative.
 */
const props = defineProps({
  detail: { type: Object, default: null },
  activeHighlight: { type: String, default: null } // 'ancestors' | 'descendants' | 'lineage' | null
})

const emit = defineEmits(['close', 'select', 'highlight', 'center'])

const fmt = d => (d ? new Date(d).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—')

const groups = computed(() => {
  if (!props.detail) return []
  return [
    { label: 'Parents', people: props.detail.parents },
    { label: 'Spouse(s)', people: props.detail.spouses },
    { label: 'Children', people: props.detail.children },
    { label: 'Siblings', people: props.detail.siblings }
  ]
})

function toggleHighlight(kind) {
  emit('highlight', props.activeHighlight === kind ? null : kind)
}
</script>

<template>
  <aside class="detail-panel">
    <template v-if="detail">
      <header class="dp-header">
        <div>
          <h2>{{ detail.fullName }}</h2>
          <span class="dp-sub">{{ detail.gender }} · {{ detail.isAlive ? 'Living' : 'Deceased' }}</span>
        </div>
        <button class="dp-close" title="Close" @click="emit('close')">×</button>
      </header>

      <dl class="dp-dates">
        <div><dt>Born</dt><dd>{{ fmt(detail.dateOfBirth) }}</dd></div>
        <div><dt>Died</dt><dd>{{ detail.isAlive ? '—' : fmt(detail.dateOfDeath) }}</dd></div>
      </dl>

      <div class="dp-actions">
        <button @click="emit('center', detail.id)">Center</button>
        <button :class="{ active: activeHighlight === 'ancestors' }" @click="toggleHighlight('ancestors')">Ancestors</button>
        <button :class="{ active: activeHighlight === 'descendants' }" @click="toggleHighlight('descendants')">Descendants</button>
        <button :class="{ active: activeHighlight === 'lineage' }" @click="toggleHighlight('lineage')">Lineage</button>
      </div>

      <section v-for="grp in groups" :key="grp.label" class="dp-group">
        <h3>{{ grp.label }}</h3>
        <ul v-if="grp.people.length">
          <li v-for="rel in grp.people" :key="rel.id">
            <button class="dp-rel" @click="emit('select', rel.id)">
              {{ rel.fullName }}
              <span v-if="!rel.isAlive" class="dp-dagger">†</span>
            </button>
          </li>
        </ul>
        <p v-else class="dp-none">None recorded</p>
      </section>
    </template>

    <p v-else class="dp-empty">Select a person in the tree to see details.</p>
  </aside>
</template>
