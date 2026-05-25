use utf-8 for file creation or updates

Workflow rules:
- Do not create or run new tests until the code changes are approved.
- Add or update tests immediately before committing approved changes.
- Confirm application architecture decisions before implementing them: first discuss where new code belongs, how it should be named, and its ownership boundaries; only write code after the decision is approved.
- When creating form or dialog layouts, avoid showing semantically duplicate information; if a field is already present in the input area, do not repeat it in the summary block unless it adds distinct context.
- Do not mask data contract violations with defensive fallbacks, nullable workarounds, or silent checks; throw an exception at the boundary where invalid data is detected.
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
