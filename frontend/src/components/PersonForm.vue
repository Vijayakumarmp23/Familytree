<script setup>
import { ref, reactive, computed } from 'vue'

/**
 * Modal for data entry. Two modes:
 *  - "add"   : create a NEW person and (optionally) pick an existing father,
 *              mother and spouse from dropdowns.
 *  - "marry" : link two people who are ALREADY in the tree (e.g. a cousin
 *              marriage). No new person record is created.
 *
 * The component only collects input and emits it; the parent performs the API
 * calls so all persons/relationships reload together.
 */
const props = defineProps({
  people: { type: Array, required: true } // [{id, fullName, ...}]
})

const emit = defineEmits(['close', 'add-person', 'marry'])

const mode = ref('add')

const form = reactive({
  fullName: '',
  gender: 'Male',
  dateOfBirth: '',
  dateOfDeath: '',
  isAlive: true,
  fatherId: '',
  motherId: '',
  spouseId: ''
})

const marry = reactive({ person1Id: '', person2Id: '' })

const sortedPeople = computed(() =>
  [...props.people].sort((a, b) => a.fullName.localeCompare(b.fullName))
)

const canAdd = computed(() => form.fullName.trim().length > 0)
const canMarry = computed(
  () => marry.person1Id && marry.person2Id && marry.person1Id !== marry.person2Id
)

function submitAdd() {
  if (!canAdd.value) return
  emit('add-person', {
    fullName: form.fullName.trim(),
    gender: form.gender,
    dateOfBirth: form.dateOfBirth || null,
    dateOfDeath: form.isAlive ? null : form.dateOfDeath || null,
    isAlive: form.isAlive,
    fatherId: form.fatherId ? Number(form.fatherId) : null,
    motherId: form.motherId ? Number(form.motherId) : null,
    spouseId: form.spouseId ? Number(form.spouseId) : null
  })
}

function submitMarry() {
  if (!canMarry.value) return
  emit('marry', { person1Id: Number(marry.person1Id), person2Id: Number(marry.person2Id) })
}
</script>

<template>
  <div class="modal-backdrop" @click.self="emit('close')">
    <div class="modal">
      <header class="modal-header">
        <div class="mode-tabs">
          <button :class="{ active: mode === 'add' }" @click="mode = 'add'">Add person</button>
          <button :class="{ active: mode === 'marry' }" @click="mode = 'marry'">Marry two members</button>
        </div>
        <button class="dp-close" @click="emit('close')">×</button>
      </header>

      <!-- ADD PERSON -->
      <form v-if="mode === 'add'" class="pf-form" @submit.prevent="submitAdd">
        <label>Full name
          <input v-model="form.fullName" required placeholder="e.g. Jane Smith" />
        </label>

        <div class="pf-row">
          <label>Gender
            <select v-model="form.gender">
              <option>Male</option><option>Female</option><option>Other</option>
            </select>
          </label>
          <label class="pf-alive">
            <input type="checkbox" v-model="form.isAlive" /> Living
          </label>
        </div>

        <div class="pf-row">
          <label>Date of birth<input type="date" v-model="form.dateOfBirth" /></label>
          <label v-if="!form.isAlive">Date of death<input type="date" v-model="form.dateOfDeath" /></label>
        </div>

        <label>Father (existing)
          <select v-model="form.fatherId">
            <option value="">— none —</option>
            <option v-for="p in sortedPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
          </select>
        </label>
        <label>Mother (existing)
          <select v-model="form.motherId">
            <option value="">— none —</option>
            <option v-for="p in sortedPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
          </select>
        </label>
        <label>Spouse (existing)
          <select v-model="form.spouseId">
            <option value="">— none —</option>
            <option v-for="p in sortedPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
          </select>
        </label>

        <div class="pf-buttons">
          <button type="button" class="ghost" @click="emit('close')">Cancel</button>
          <button type="submit" class="primary" :disabled="!canAdd">Add person</button>
        </div>
      </form>

      <!-- MARRY TWO EXISTING -->
      <form v-else class="pf-form" @submit.prevent="submitMarry">
        <p class="pf-hint">
          Marry two people who already exist in the tree — this is how cousin
          marriages and reconnecting branches are recorded. No duplicate person
          is created.
        </p>
        <label>Person 1
          <select v-model="marry.person1Id">
            <option value="">— select —</option>
            <option v-for="p in sortedPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
          </select>
        </label>
        <label>Person 2
          <select v-model="marry.person2Id">
            <option value="">— select —</option>
            <option v-for="p in sortedPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
          </select>
        </label>
        <div class="pf-buttons">
          <button type="button" class="ghost" @click="emit('close')">Cancel</button>
          <button type="submit" class="primary" :disabled="!canMarry">Marry</button>
        </div>
      </form>
    </div>
  </div>
</template>
