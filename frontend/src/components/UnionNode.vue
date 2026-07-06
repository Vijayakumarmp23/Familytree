<script setup>
import { Handle, Position } from '@vue-flow/core'

/**
 * Tiny "marriage" node that sits between a couple and their children. Spouse
 * edges come in the top, child edges leave the bottom, so the two spouse lines
 * plus this dot read as a single marriage connector that children descend from.
 */
defineProps({
  data: { type: Object, required: true }
})
</script>

<template>
  <div
    class="union-node"
    :class="{ 'hl-on': data.highlight === 'on', 'hl-dim': data.highlight === 'dim' }"
  >
    <Handle type="target" :position="Position.Top" />
    <Handle type="source" :position="Position.Bottom" />
  </div>
</template>
