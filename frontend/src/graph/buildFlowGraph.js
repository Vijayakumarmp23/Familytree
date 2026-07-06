import dagre from '@dagrejs/dagre'
import { descendantsOf } from './genealogy'

/**
 * Turn the genealogy graph into vue-flow nodes + edges, laid out with dagre.
 *
 * The key idea (borrowed from Gramps/GenoPro) is a hidden **union node** for
 * every couple / co-parent set:
 *
 *     [Father]     [Mother]        <- persons, same rank
 *          \        /
 *          (union)                 <- tiny marriage node, one rank below
 *          /  |   \
 *       [c1] [c2] [c3]             <- children descend from the union
 *
 * Because a union is keyed by the SET of its members, a married couple and the
 * co-parents of a child collapse to the same union, and one person can be a
 * child of one union while being a spouse in another. That is what makes cousin
 * marriages / reconnecting branches render with no duplicate people.
 */

const PERSON_W = 190
const PERSON_H = 78
const UNION_SIZE = 16

const unionKey = ids => [...ids].sort((a, b) => a - b).join('-')

/**
 * @param g            genealogy model from buildGenealogy()
 * @param opts.collapsedIds  Set of person ids whose descendants are hidden
 * @param opts.highlight     { nodes:Set<id>, dim:boolean } lineage highlight
 * @param opts.selectedId    currently selected person id
 */
export function buildFlowGraph(g, opts = {}) {
  const collapsedIds = opts.collapsedIds || new Set()
  const highlight = opts.highlight || null
  const selectedId = opts.selectedId ?? null

  // ---- 1. Which people are hidden by a collapsed ancestor? ----
  const hidden = new Set()
  for (const cid of collapsedIds) {
    for (const d of descendantsOf(g, cid)) hidden.add(d)
  }
  const isVisible = id => !hidden.has(id)

  // ---- 2. Discover unions (couples + co-parent sets) ----
  // union -> { key, members:Set, children:Set }
  const unions = new Map()
  const ensureUnion = members => {
    const key = unionKey(members)
    if (!unions.has(key)) unions.set(key, { key, members: new Set(members), children: new Set() })
    return unions.get(key)
  }

  // Marriages become unions even when childless.
  for (const r of g.relationships) {
    if (r.relationshipType === 'Spouse') ensureUnion([r.person1Id, r.person2Id])
  }
  // Each child is attached to the union of its exact parent-set.
  for (const [childId, parentSet] of g.parentsOf) {
    if (!parentSet.size) continue
    ensureUnion([...parentSet]).children.add(childId)
  }

  // ---- 3. Build nodes ----
  const nodes = []
  const nodeIds = new Set()

  const highlightState = id => {
    if (!highlight) return 'none'
    return highlight.nodes.has(id) ? 'on' : 'dim'
  }

  for (const person of g.personById.values()) {
    if (!isVisible(person.id)) continue
    const childCount = g.childrenOf.get(person.id)?.size || 0
    nodes.push({
      id: `p${person.id}`,
      type: 'person',
      position: { x: 0, y: 0 }, // dagre fills this in
      data: {
        person,
        selected: person.id === selectedId,
        highlight: highlightState(person.id),
        hasDescendants: childCount > 0,
        collapsed: collapsedIds.has(person.id)
      }
    })
    nodeIds.add(`p${person.id}`)
  }

  // Union nodes: keep only those with at least one visible member.
  const visibleUnions = []
  for (const u of unions.values()) {
    const members = [...u.members].filter(isVisible)
    if (members.length === 0) continue
    const children = [...u.children].filter(isVisible)
    const uid = `u${u.key}`
    // Highlight a union if all its (visible) members are highlighted.
    const hl = highlight
      ? members.every(m => highlight.nodes.has(m)) && members.length > 0 ? 'on' : 'dim'
      : 'none'
    nodes.push({
      id: uid,
      type: 'union',
      position: { x: 0, y: 0 },
      selectable: false,
      data: { highlight: hl }
    })
    nodeIds.add(uid)
    visibleUnions.push({ uid, members, children })
  }

  // ---- 4. Build edges ----
  const edges = []
  const edgeOn = (a, b) =>
    highlight ? (highlight.nodes.has(a) && highlight.nodes.has(b)) : true

  for (const { uid, members, children } of visibleUnions) {
    // spouse/partner: person (bottom) -> union (top)
    for (const m of members) {
      edges.push({
        id: `s-${uid}-${m}`,
        source: `p${m}`,
        target: uid,
        type: 'smoothstep',
        class: edgeClass('spouse', highlight && !edgeOn(m, uid) ? 'dim' : 'on'),
        data: { kind: 'spouse' }
      })
    }
    // child: union (bottom) -> person (top)
    for (const c of children) {
      // highlight a child edge only when the child AND all its parents light up
      const parents = members
      const on = !highlight || (highlight.nodes.has(c) && parents.some(p => highlight.nodes.has(p)))
      edges.push({
        id: `c-${uid}-${c}`,
        source: uid,
        target: `p${c}`,
        type: 'smoothstep',
        class: edgeClass('child', on ? 'on' : 'dim'),
        data: { kind: 'child' }
      })
    }
  }

  layoutWithDagre(nodes, edges)
  return { nodes, edges }
}

function edgeClass(kind, state) {
  return `edge-${kind} edge-${state}`
}

/** Run dagre and write absolute positions back onto the vue-flow nodes. */
function layoutWithDagre(nodes, edges) {
  const g = new dagre.graphlib.Graph()
  g.setGraph({ rankdir: 'TB', nodesep: 40, ranksep: 60, marginx: 40, marginy: 40 })
  g.setDefaultEdgeLabel(() => ({}))

  for (const n of nodes) {
    const isUnion = n.type === 'union'
    g.setNode(n.id, {
      width: isUnion ? UNION_SIZE : PERSON_W,
      height: isUnion ? UNION_SIZE : PERSON_H
    })
  }
  for (const e of edges) g.setEdge(e.source, e.target)

  dagre.layout(g) // dagre internally breaks any accidental cycles -> never hangs

  for (const n of nodes) {
    const pos = g.node(n.id)
    const w = n.type === 'union' ? UNION_SIZE : PERSON_W
    const h = n.type === 'union' ? UNION_SIZE : PERSON_H
    n.position = { x: pos.x - w / 2, y: pos.y - h / 2 }
    // vue-flow needs explicit sizes to place handles/edges predictably
    n.width = w
    n.height = h
  }
}
