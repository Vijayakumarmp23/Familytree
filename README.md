# 🌳 Family Tree Management System

A full-stack **genealogical graph** application. People are nodes and every
relationship (parent↔child, marriage) is an edge, so the tree renders as a real
family diagram — nodes connected by relationship lines — instead of a nested
list. Because relationships are their own records, the same person can appear in
many relationships (e.g. a **cousin marriage**) without ever being duplicated.

- **Backend:** ASP.NET Core 8 Web API + Entity Framework Core (SQLite)
- **Frontend:** Vue 3 (Composition API) + Vite + **[Vue Flow](https://vueflow.dev/)** graph canvas + **[dagre](https://github.com/dagrejs/dagre)** layout
- **Model:** a `Person` table of nodes and a `Relationship` table of edges (`ParentChild` | `Spouse`)

```
familytree/
├── backend/FamilyTree.Api/
│   ├── Models/Person.cs             graph NODE (no relatives stored on it)
│   ├── Models/Relationship.cs       graph EDGE (Person1 -> Person2, type)
│   ├── Models/RelationshipType.cs   ParentChild | Spouse
│   ├── Data/                        EF Core context + seed (4 gens, cousin marriage)
│   ├── DTOs/                        person / detail / relationship / create shapes
│   ├── Services/GenealogyService.cs derives parents/spouses/children/siblings
│   └── Controllers/                 PersonsController + RelationshipsController
└── frontend/src/
    ├── graph/genealogy.js           adjacency maps + cycle-safe traversals
    ├── graph/buildFlowGraph.js      relationships -> Vue Flow nodes/edges + dagre
    ├── components/PersonNode.vue     custom person card node
    ├── components/UnionNode.vue      marriage/family connector node
    ├── components/FamilyGraph.vue    Vue Flow canvas (zoom / pan / minimap)
    ├── components/PersonDetail.vue   side panel + lineage highlight controls
    ├── components/PersonForm.vue     add person / marry two existing members
    └── views/FamilyTreeView.vue      orchestrator
```

---

## 1. Run the backend

Requires the **.NET 8 SDK** (or newer — the project targets `net8.0`; with only a
newer SDK installed, run with `DOTNET_ROLL_FORWARD=LatestMajor`).

```bash
cd backend/FamilyTree.Api
dotnet run
```

- API:      http://localhost:5000/api/persons
- Swagger:  http://localhost:5000/swagger

On first run a SQLite file `familytree.db` is created and seeded with four
generations including a cousin marriage. **Delete that file to re-seed** (the
schema changed from the old version, so delete any pre-existing `familytree.db`).

## 2. Run the frontend

Requires **Node.js 18+**.

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173. The dev server talks to the API at
`http://localhost:5000/api` (configurable via `VITE_API_BASE`). CORS for
`localhost:5173` is already allowed by the backend.

---

## 3. REST API

| Method | Route                        | Description                                   |
|--------|------------------------------|-----------------------------------------------|
| GET    | `/api/persons`               | All persons (graph nodes)                     |
| GET    | `/api/persons/{id}`          | One person + derived parents/spouses/children/siblings |
| GET    | `/api/persons/search?name=`  | Search by partial name                        |
| POST   | `/api/persons`               | Create a person (optionally link father/mother/spouse) |
| GET    | `/api/relationships`         | All relationships (graph edges)               |
| POST   | `/api/relationships`         | Link two existing people                      |

### Relationship examples

```jsonc
// marry two people already in the tree (e.g. a cousin marriage — no duplicates)
{ "person1Id": 10, "person2Id": 25, "relationshipType": "Spouse" }

// record a parent -> child link
{ "person1Id": 1, "person2Id": 10, "relationshipType": "ParentChild" }
```

### Create a person and link it in one call

```jsonc
POST /api/persons
{
  "fullName": "Sophie Smith",
  "gender": "Female",
  "dateOfBirth": "2018-05-09",
  "isAlive": true,
  "fatherId": 7,      // optional — creates a ParentChild edge
  "motherId": 10,     // optional
  "spouseId": null    // optional — creates a Spouse edge
}
```

### Sample `GET /api/persons/{id}` (Michael, who is in a cousin marriage)

```jsonc
{
  "id": 7, "fullName": "Michael Smith", "gender": "Male",
  "dateOfBirth": "1990-06-14", "dateOfDeath": null, "isAlive": true,
  "parents":  [ { "id": 3, "fullName": "John Smith" }, { "id": 4, "fullName": "Linda Smith" } ],
  "spouses":  [ { "id": 10, "fullName": "Sarah Brown" } ],   // his first cousin
  "children": [ { "id": 11, "fullName": "Oliver Smith" } ],
  "siblings": [ { "id": 8,  "fullName": "Emily Smith" } ]
}
```

---

## 4. How the diagram is built

The renderer never assumes a strict tree. It builds the diagram from the edge
list using **union (family) nodes** — the same technique Gramps/GenoPro use:

1. Every marriage and every co-parent set becomes a small **union node**, keyed
   by the *set* of its members. A married couple and the parents of their child
   therefore collapse to the **same** union.
2. Spouses connect *down* into the union (`person → union`); children descend
   *from* the union (`union → child`). Two spouse lines + the union dot read as
   one marriage connector that children hang below.
3. Because a union is keyed by member-set, one person can be a **child of one
   union and a spouse in another** — which is exactly how cousin marriages and
   reconnecting branches render with **no duplicate people**.
4. **[dagre](https://github.com/dagrejs/dagre)** assigns generations (ranks) and
   positions. Every graph traversal in `genealogy.js` carries a `visited` set,
   and dagre breaks accidental cycles, so **cyclic/malformed data never loops**.

The seeded data contains the cousin marriage below — Michael (grandchild via
John) marries Sarah (grandchild via Susan), reconnecting the two branches:

```
                 George Smith ─♥─ Margaret Smith
                          │
        ┌─────────────────┴─────────────────┐
  John ─♥─ Linda                       Susan ─♥─ Robert
        │                                    │
   ┌────┴────┐                          ┌────┴────┐
 Michael   Emily                      David     Sarah
    └──────────────────── ♥ ────────────────────┘   (first cousins)
                          │
                     Oliver Smith
```

## 5. Features

- **Visual genealogy graph** — person cards connected by marriage & parent lines
- **Zoom, pan, minimap** and fit-to-screen (Vue Flow canvas + controls)
- **Collapse / expand** a person's descendants (± on each node), or all at once
- **Highlight ancestors / descendants / full lineage** of the selected person
- **Search** by name (debounced) and **center** the canvas on any person
- **Person detail** side panel: dates, parents, spouse(s), children, siblings
- **Data entry**: add a new person linked to an existing father/mother/spouse,
  or **marry two existing members** — never creating duplicate records
- **Real genealogy support**: cousin marriages, descendant-to-descendant
  marriages, and reconnecting branches all render as a single graph
```
