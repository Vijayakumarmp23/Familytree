/**
 * Pure genealogy graph model built from the flat persons + relationships lists.
 *
 * Nothing here touches the DOM or vue-flow - it just answers questions about
 * the graph (who are X's parents / children / spouses / ancestors / ...). Every
 * traversal carries a `visited` set so malformed/cyclic data (e.g. a data-entry
 * mistake that makes someone their own ancestor) can never spin forever.
 */

/** Build fast lookup maps from the raw API payloads. */
export function buildGenealogy(persons, relationships) {
  const personById = new Map(persons.map(p => [p.id, p]))

  const parentsOf = new Map()   // childId  -> Set(parentId)
  const childrenOf = new Map()  // parentId -> Set(childId)
  const spousesOf = new Map()   // personId -> Set(spouseId)

  const add = (map, key, val) => {
    if (!map.has(key)) map.set(key, new Set())
    map.get(key).add(val)
  }

  for (const r of relationships) {
    if (r.relationshipType === 'ParentChild') {
      add(parentsOf, r.person2Id, r.person1Id)
      add(childrenOf, r.person1Id, r.person2Id)
    } else if (r.relationshipType === 'Spouse') {
      add(spousesOf, r.person1Id, r.person2Id)
      add(spousesOf, r.person2Id, r.person1Id)
    }
  }

  return { personById, parentsOf, childrenOf, spousesOf, relationships }
}

const asArray = set => (set ? [...set] : [])

export const parentsOf = (g, id) => asArray(g.parentsOf.get(id))
export const childrenOf = (g, id) => asArray(g.childrenOf.get(id))
export const spousesOf = (g, id) => asArray(g.spousesOf.get(id))

/** Siblings = everyone who shares at least one parent (minus self). */
export function siblingsOf(g, id) {
  const sibs = new Set()
  for (const parentId of parentsOf(g, id)) {
    for (const child of childrenOf(g, parentId)) sibs.add(child)
  }
  sibs.delete(id)
  return [...sibs]
}

/** All ancestors of `id` (transitive parents), cycle-safe. */
export function ancestorsOf(g, id) {
  const out = new Set()
  const stack = [...parentsOf(g, id)]
  while (stack.length) {
    const cur = stack.pop()
    if (out.has(cur)) continue // visited guard -> no infinite loops
    out.add(cur)
    stack.push(...parentsOf(g, cur))
  }
  return out
}

/** All descendants of `id` (transitive children), cycle-safe. */
export function descendantsOf(g, id) {
  const out = new Set()
  const stack = [...childrenOf(g, id)]
  while (stack.length) {
    const cur = stack.pop()
    if (out.has(cur)) continue
    out.add(cur)
    stack.push(...childrenOf(g, cur))
  }
  return out
}

/**
 * Full bloodline "lineage path" for a person: their ancestors, their
 * descendants and the person themselves.
 */
export function lineageOf(g, id) {
  const set = ancestorsOf(g, id)
  for (const d of descendantsOf(g, id)) set.add(d)
  set.add(id)
  return set
}
