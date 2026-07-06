import api from './api'

/**
 * Graph-model API layer. The backend exposes people (nodes) and relationships
 * (edges) separately; the client assembles the diagram from both.
 */
export const familyService = {
  /** All person nodes (flat list). */
  getPersons() {
    return api.get('/persons').then(r => r.data)
  },

  /** One person with derived parents / spouses / children / siblings. */
  getPerson(id) {
    return api.get(`/persons/${id}`).then(r => r.data)
  },

  /** All relationship edges. */
  getRelationships() {
    return api.get('/relationships').then(r => r.data)
  },

  /** Search people by (partial) name. */
  search(name) {
    return api.get('/persons/search', { params: { name } }).then(r => r.data)
  },

  /**
   * Create a person. Optional fatherId / motherId / spouseId make the backend
   * create the matching relationship edges in the same request.
   */
  createPerson(person) {
    return api.post('/persons', person).then(r => r.data)
  },

  /**
   * Link two EXISTING people, e.g. marry two members already in the tree.
   * type is 'Spouse' or 'ParentChild'.
   */
  createRelationship(person1Id, person2Id, relationshipType) {
    return api
      .post('/relationships', { person1Id, person2Id, relationshipType })
      .then(r => r.data)
  }
}
