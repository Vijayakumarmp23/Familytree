<script setup>
import { computed } from 'vue'
import { BaseEdge } from '@vue-flow/core'

/**
 * Custom vue-flow edge that draws a zig-zag line between its endpoints, used
 * for consanguineous marriages ("married within the family"). Colour/width come
 * from the `.edge-spouse` CSS class on the edge group, same as normal marriage
 * lines - only the shape differs.
 */
const props = defineProps({
  id: { type: String, required: true },
  sourceX: { type: Number, required: true },
  sourceY: { type: Number, required: true },
  targetX: { type: Number, required: true },
  targetY: { type: Number, required: true },
  markerEnd: { type: String, default: '' }
})

const AMPLITUDE = 5 // how far each zig deviates from the straight line
const STEP = 13     // spacing between zig-zag vertices

const path = computed(() => {
  const { sourceX: sx, sourceY: sy, targetX: tx, targetY: ty } = props
  const dx = tx - sx
  const dy = ty - sy
  const len = Math.hypot(dx, dy) || 1
  const ux = dx / len
  const uy = dy / len
  const px = -uy // unit vector perpendicular to the line
  const py = ux
  const segments = Math.max(4, Math.round(len / STEP))

  let d = `M ${sx} ${sy}`
  for (let i = 1; i < segments; i++) {
    const t = i / segments
    const bx = sx + dx * t
    const by = sy + dy * t
    const sign = i % 2 === 0 ? 1 : -1
    d += ` L ${bx + px * AMPLITUDE * sign} ${by + py * AMPLITUDE * sign}`
  }
  d += ` L ${tx} ${ty}`
  return d
})
</script>

<template>
  <BaseEdge :id="id" :path="path" :marker-end="markerEnd" />
</template>
