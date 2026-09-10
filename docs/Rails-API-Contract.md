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

## Stage expansion in the contract editor

- `Stage.used` stores whether the stage branch is expanded in the contract editor.
  Multiple stages may have `used: true`; all stages may have `used: false`.
- Opening or rebuilding the editor reads this value without changing it. User expansion or
  collapse changes only that stage's edit state, independently of the selected stage.
- Expansion changes are persisted by the ordinary Save action through the existing explicit
  Stage payload builder and `ModelMutationService`; cancellation discards unsaved changes.
- Adding or deleting another stage must not normalize existing `used` values to a single active stage.
- This describes the desktop client's contract; server-side acceptance of multiple `used: true`
  values must also be maintained. `Revision.used` retains its existing meaning.

## Automatic workflow audit entries

- Automatic workflow changes are recorded by creating a separate `Audit` row after the related
  `Contract` or `Stage` mutation succeeds.
- The client-created Audit row describes the source field whose user-entered change triggered the
  automatic workflow cascade. Rails creates the ordinary Audit rows for every field actually changed
  by the resulting Contract and Stage mutations.
- The create payload contains only `auditable_type`, `auditable_id`, `auditable_field`, `action`,
  `detail`, `before`, `after`, `user_id`, and `person_id`.
- `action` uses the Rails enum key `updated`.
- `auditable_field`, `before`, and `after` identify the source change that triggered the cascade.
- A client-created cause Audit row is added only when the cascade actually changes at least one
  dependent field. Repeated changes of the same source field before save replace the pending cause
  row instead of creating duplicates.
- `detail` names only the dependent fields actually changed by that cascade. For contract signing,
  it distinguishes a single changed stage status from multiple statuses and mentions stage deadlines
  only when at least one start or deadline date changed.
- Automatic workflow text must not be added to `comments_attributes`; that collection is reserved
  for comments entered by the user.

```json
{
  "data_set": "item",
  "Audit": {
    "auditable_type": "Contract",
    "auditable_id": 456,
    "auditable_field": "signed_at",
    "action": "updated",
    "detail": "Изменение даты подписания запустило автоматическое изменение статусов и сроков этапов",
    "before": "-",
    "after": "29.08.2026",
    "user_id": 17,
    "person_id": 42
  }
}
```

## Stage update guard

`ModelMutationService.UpdateAsync` rejects Stage payloads that contain read-model keys before any HTTP request is sent. This is intentional: if it fails, fix the dialog payload builder instead of weakening the guard.

## Order cost from positions

- Creating an Order from selected needs includes `cost`, calculated as the decimal sum of the selected StageOrder `cost` values. A null position cost contributes zero; a missing or malformed field is a contract error.
- `StageOrderNeedsPayloadBuilder` serializes the calculated cost; creation and position linking use `ModelMutationService`.
- Editing an Order loads all its StageOrder.order positions through paginated read-only queries. Focusing the cost input compares its value with the same calculated sum.
- A mismatch offers an explicit correction in a flyout. Accepting changes only the editor value; the ordinary Order update payload includes `cost` only when changed. No position response rows are serialized back to the API.
