<script setup>
import { ref, watch } from 'vue'
import { familyService } from '../services/familyService'

/** Debounced person search. Emits `select` with a person id when a hit is chosen. */
const emit = defineEmits(['select'])

const term = ref('')
const results = ref([])
const open = ref(false)
let timer = null

watch(term, value => {
  clearTimeout(timer)
  if (!value.trim()) {
    results.value = []
    open.value = false
    return
  }
  timer = setTimeout(async () => {
    results.value = await familyService.search(value.trim())
    open.value = true
  }, 250)
})

function choose(person) {
  emit('select', person.id)
  term.value = person.fullName
  open.value = false
}
</script>

<template>
  <div class="search-bar">
    <input
      v-model="term"
      type="text"
      placeholder="Search people by name…"
      @focus="open = results.length > 0"
    />
    <ul v-if="open && results.length" class="search-results">
      <li v-for="p in results" :key="p.id" @mousedown.prevent="choose(p)">
        {{ p.fullName }}
        <span class="sr-year" v-if="p.dateOfBirth">
          ({{ new Date(p.dateOfBirth).getFullYear() }})
        </span>
      </li>
    </ul>
    <ul v-else-if="open" class="search-results">
      <li class="sr-empty">No matches</li>
    </ul>
  </div>
</template>
