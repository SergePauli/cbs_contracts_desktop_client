# Rails API Contract

## Read requests

- Table/list/card/edit data is loaded through `api/index` and `api/count`.
- `api/index` responses are read models. They may contain expanded objects and arrays such as `comments`, `contract`, `status`, `tasks`, `revision`, `stages`.
- Read models must not be reused as update payloads.

## Create and update requests

- Create uses `POST model/add/{Model}`.
- Update uses `PUT model/{Model}/{id}`.
- Payloads are wrapped as:

```json
{
  "data_set": "edit",
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
- Do not send expanded read-model fields such as `comments`, `contract`, `status`, `task_kind`, `tasks`, `revision`, `revisions`, `stages`.

## Stage update guard

`ReferenceCrudService.UpdateAsync` rejects Stage payloads that contain read-model keys before any HTTP request is sent. This is intentional: if it fails, fix the dialog payload builder instead of weakening the guard.
