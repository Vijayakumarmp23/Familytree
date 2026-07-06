<script setup>
import { computed, reactive, ref, watch } from 'vue'

/**
 * Side panel for the selected person. Has two modes:
 *  - VIEW: shows dates + derived parents/spouses/children/siblings, with
 *    lineage-highlight controls and a "Remove from tree" action.
 *  - EDIT: every attribute is editable (name, gender, birth, death, living),
 *    and relationships are editable too - pick up to two parents, add/remove
 *    spouses and children from the existing members. Existing people are
 *    LINKED, never duplicated.
 *
 * The panel only gathers input; the parent view performs the API calls
 * (attribute PUT + relationship add/remove reconciliation).
 */
const props = defineProps({
  detail: { type: Object, default: null },
  people: { type: Array, default: () => [] }, // all persons, for dropdowns
  activeHighlight: { type: String, default: null }
})

const emit = defineEmits(['close', 'select', 'highlight', 'center', 'delete', 'save'])

const editing = ref(false)

const form = reactive({
  fullName: '',
  gender: 'Male',
  dateOfBirth: '',
  dateOfDeath: '',
  isAlive: true,
  parent1Id: '',
  parent2Id: '',
  adopted: false,
  spouseIds: [],
  childIds: []
})

// Format for a <input type="date"> using LOCAL date parts. Using toISOString()
// here would shift the day in non-UTC timezones (e.g. 06-14 -> 06-13 at UTC+).
const toInput = d => {
  if (!d) return ''
  const dt = new Date(d)
  const mm = String(dt.getMonth() + 1).padStart(2, '0')
  const dd = String(dt.getDate()).padStart(2, '0')
  return `${dt.getFullYear()}-${mm}-${dd}`
}
const fmt = d =>
  d ? new Date(d).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : '—'

const nameById = computed(() => new Map(props.people.map(p => [p.id, p.fullName])))
const optionPeople = computed(() =>
  [...props.people]
    .filter(p => p.id !== props.detail?.id)
    .sort((a, b) => a.fullName.localeCompare(b.fullName))
)

// Leaving edit mode / switching person resets the panel back to VIEW.
watch(() => props.detail?.id, () => (editing.value = false))

function startEdit() {
  const d = props.detail
  form.fullName = d.fullName
  form.gender = d.gender || 'Male'
  form.dateOfBirth = toInput(d.dateOfBirth)
  form.dateOfDeath = toInput(d.dateOfDeath)
  form.isAlive = d.isAlive
  form.parent1Id = d.parents[0]?.id ?? ''
  form.parent2Id = d.parents[1]?.id ?? ''
  form.adopted = !!d.isAdopted
  form.spouseIds = d.spouses.map(s => s.id)
  form.childIds = d.children.map(c => c.id)
  editing.value = true
}

function cancelEdit() {
  editing.value = false
}

function save() {
  if (!form.fullName.trim()) return
  const parentIds = [...new Set([form.parent1Id, form.parent2Id])]
    .filter(v => v !== '' && v != null)
    .map(Number)
  emit('save', {
    id: props.detail.id,
    attributes: {
      fullName: form.fullName.trim(),
      gender: form.gender,
      dateOfBirth: form.dateOfBirth || null,
      dateOfDeath: form.isAlive ? null : form.dateOfDeath || null,
      isAlive: form.isAlive
    },
    parentIds,
    adopted: form.adopted,
    spouseIds: form.spouseIds.map(Number),
    childIds: form.childIds.map(Number)
  })
  editing.value = false
}

// -- add/remove helpers for the spouse & child lists in edit mode --
const spouseToAdd = ref('')
const childToAdd = ref('')

function addSpouse() {
  const id = Number(spouseToAdd.value)
  if (id && !form.spouseIds.includes(id)) form.spouseIds.push(id)
  spouseToAdd.value = ''
}
function addChild() {
  const id = Number(childToAdd.value)
  if (id && !form.childIds.includes(id)) form.childIds.push(id)
  childToAdd.value = ''
}
const removeFrom = (arr, id) => {
  const i = arr.indexOf(id)
  if (i > -1) arr.splice(i, 1)
}

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
      <!-- ===================== VIEW MODE ===================== -->
      <template v-if="!editing">
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
          <button class="edit" @click="startEdit">✎ Edit</button>
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

        <footer class="dp-footer">
          <button class="danger" @click="emit('delete', detail)">Remove from tree</button>
        </footer>
      </template>

      <!-- ===================== EDIT MODE ===================== -->
      <template v-else>
        <header class="dp-header">
          <h2>Edit person</h2>
          <button class="dp-close" title="Cancel" @click="cancelEdit">×</button>
        </header>

        <form class="pf-form" @submit.prevent="save">
          <label>Full name
            <input v-model="form.fullName" required />
          </label>

          <div class="pf-row">
            <label>Gender
              <select v-model="form.gender">
                <option>Male</option><option>Female</option><option>Other</option>
              </select>
            </label>
            <label class="pf-alive"><input type="checkbox" v-model="form.isAlive" /> Living</label>
            <label class="pf-alive"><input type="checkbox" v-model="form.adopted" /> Adopted</label>
          </div>

          <div class="pf-row">
            <label>Date of birth<input type="date" v-model="form.dateOfBirth" /></label>
            <label v-if="!form.isAlive">Date of death<input type="date" v-model="form.dateOfDeath" /></label>
          </div>

          <h3 class="dp-edit-h">Parents</h3>
          <div class="pf-row">
            <label>Parent 1
              <select v-model="form.parent1Id">
                <option value="">— none —</option>
                <option v-for="p in optionPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
              </select>
            </label>
            <label>Parent 2
              <select v-model="form.parent2Id">
                <option value="">— none —</option>
                <option v-for="p in optionPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
              </select>
            </label>
          </div>

          <h3 class="dp-edit-h">Spouse(s)</h3>
          <ul class="dp-chiplist">
            <li v-for="sid in form.spouseIds" :key="sid">
              {{ nameById.get(sid) || ('#' + sid) }}
              <button type="button" class="chip-x" @click="removeFrom(form.spouseIds, sid)">×</button>
            </li>
            <li v-if="!form.spouseIds.length" class="dp-none">None</li>
          </ul>
          <div class="pf-addrow">
            <select v-model="spouseToAdd">
              <option value="">Add spouse…</option>
              <option v-for="p in optionPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
            </select>
            <button type="button" @click="addSpouse">Add</button>
          </div>

          <h3 class="dp-edit-h">Children</h3>
          <ul class="dp-chiplist">
            <li v-for="cid in form.childIds" :key="cid">
              {{ nameById.get(cid) || ('#' + cid) }}
              <button type="button" class="chip-x" @click="removeFrom(form.childIds, cid)">×</button>
            </li>
            <li v-if="!form.childIds.length" class="dp-none">None</li>
          </ul>
          <div class="pf-addrow">
            <select v-model="childToAdd">
              <option value="">Add child…</option>
              <option v-for="p in optionPeople" :key="p.id" :value="p.id">{{ p.fullName }}</option>
            </select>
            <button type="button" @click="addChild">Add</button>
          </div>

          <div class="pf-buttons">
            <button type="button" class="ghost" @click="cancelEdit">Cancel</button>
            <button type="submit" class="primary">Save changes</button>
          </div>
        </form>
      </template>
    </template>

    <p v-else class="dp-empty">Select a person in the tree to see details.</p>
  </aside>
</template>
