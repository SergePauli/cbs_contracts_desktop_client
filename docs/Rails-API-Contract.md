# Rails API Contract

## Read requests

- Table/list/card/edit data is loaded through `api/index` and `api/count`.
- `api/index` responses are read models. They may contain expanded objects and arrays such as `comments`, `contract`, `status`, `tasks`, `revision`, `stages`.
- Read models must not be reused as update payloads.
- YrestAPI grouped filters use an `or` array whose entries contain `and` groups. Filters placed alongside `or` apply to the complete grouped expression.

## Create and update requests

- Create uses `POST model/add/{Model}`.
- Update uses `PUT model/{Model}/{id}`.
- Mutation requests always ask Rails for `data_set: "item"` because the response is only an acknowledgement; for newly created rows the client only needs the returned `id`.
- Full entity/table reads must be requested from the GO read/query layer, not inferred from Rails mutation responses.
- Payloads are wrapped as:

```json
{
  "data_set": "item",
  "Stage": {
    "id": 123,
    "status_id": 5,
    "comments_attributes": [
      { "content": "...", "profile_id": 1 }
    ]
  }
}
```

- Update payloads must contain only the entity `id`, changed scalar fields, `list_key` when the source entity has it, and Rails nested attributes intentionally built by a payload builder.
- For nested writes use `*_attributes`, for example `comments_attributes` and `tasks_attributes`.
- Contract responsible employees are changed only through `contract_responsibles_attributes`.
  New entries contain `list_key` and `employee_id`; removed persisted entries contain `id`, optional source `list_key`, and `_destroy: "1"`.
  Changing `contragent_id` clears the existing contract responsible composition in the same Contract mutation.
- In the contract editor, updates to persisted `Stage` and `Revision` rows are sent as separate
  `PUT model/Stage/{id}` and `PUT model/Revision/{id}` mutations so that each change is attributed
  to its own model in the audit log.
- `stages_attributes` and `revisions_attributes` in a Contract mutation are reserved for creating
  new child rows and destroying persisted child rows. They must not contain ordinary updates to
  persisted Stage or Revision fields.
- Do not send expanded read-model fields such as `comments`, `contract`, `status`, `task_kind`, `tasks`, `revision`, `revisions`, `stages`.

## Stage update guard

`ModelMutationService.UpdateAsync` rejects Stage payloads that contain read-model keys before any HTTP request is sent. This is intentional: if it fails, fix the dialog payload builder instead of weakening the guard.
