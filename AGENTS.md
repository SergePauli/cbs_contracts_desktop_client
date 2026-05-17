use utf-8 for file creation or updates

Workflow rules:
- Do not create or run new tests until the code changes are approved.
- Add or update tests immediately before committing approved changes.
- Keep API request builders next to the entity store/state they serialize.
- Dialogs must not build API update payloads directly; they should collect UI input and delegate change serialization to the entity store/state payload builder.

Rails API rules:
- Treat `api/index` and `api/count` as read-only query endpoints.
- Never serialize an `edit`/`card`/`list` response row back into an update request.
- Create/update must go through `ReferenceCrudService` with explicit payload builders.
- Update payloads must include only the entity `id`, changed parameters, and `list_key` when the source entity has it.
- Nested changes are allowed only through explicit Rails nested attributes such as `comments_attributes` or `tasks_attributes`.
- Do not send read-model expansions such as `comments`, `contract`, `status`, `task_kind`, `tasks`, `revision`, `revisions`, or `stages` in update payloads.
- Keep the detailed contract in `docs/Rails-API-Contract.md` in sync with code and tests.
