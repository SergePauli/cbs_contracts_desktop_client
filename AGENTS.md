use utf-8 for reading, file creation, or updates

Workflow rules:
- Do not create or run new tests until the code changes are approved.
- Add or update tests immediately before committing approved changes.
- When formatting UI output, render static text that is always present, such as labels like "прочие задачи: ", with the normal text style for that area. Render dynamic text that depends on the selected row or object, such as IDs, names, task lists, and performer lists, with the bold/accent style for that area.
- Confirm application architecture decisions before implementing them: first discuss where new code belongs, how it should be named, and its ownership boundaries; only write code after the decision is approved.
- When creating form or dialog layouts, avoid showing semantically duplicate information; if a field is already present in the input area, do not repeat it in the summary block unless it adds distinct context.
- Desktop layouts are for large screens, not touch-first UI: keep dialog/forms dense and compact, with small secondary labels, tight field heights, minimal padding/gaps, and no oversized touch spacing unless explicitly requested. Input font sizes, heights, paddings, and vertical text alignment must be visually consistent across all dialog inputs.
- When constructing dialog grids, date input columns must have a fixed width everywhere; do not place date inputs in star-sized columns because that creates visual stair-step alignment defects.
- Do not reduce input font size just to make forms denser. For compact inputs, calculate vertical padding from the relationship `control height - font size`; horizontal padding should be comparable to vertical padding or larger, never near-zero.
- Form editor values, including TextBox, ComboBox, date, number, and similar input controls, must use normal font weight; do not make entered/editable field values bold.
- Do not use the standard WinUI ComboBox in project UI. Use the custom Pauli.WinUiKit Dropdown control instead.
- Do not add defensive fallbacks to code without approval; propose them first, explain the scenario they handle, and wait for confirmation.
- Do not mask data contract violations with defensive fallbacks, nullable workarounds, or silent checks; throw an exception at the boundary where invalid data is detected.
- Keep API request builders next to the entity store/state they serialize.
- Dialogs must not build API update payloads directly; they should collect UI input and delegate change serialization to the entity store/state payload builder.
- Follow SRP strictly. This is especially important for asynchronous event handlers: memory cleanup, state updates, data loading, and UI invalidation must stay separate unless a single responsibility explicitly requires crossing that boundary.

Rails API rules:
- Treat `api/index` and `api/count` as read-only query endpoints.
- Never serialize an `edit`/`card`/`list` response row back into an update request.
- Create/update must go through `ModelMutationService` with explicit payload builders.
- Update payloads must include only the entity `id`, changed parameters, and `list_key` when the source entity has it.
- Nested changes are allowed only through explicit Rails nested attributes such as `comments_attributes` or `tasks_attributes`.
- Do not send read-model expansions such as `comments`, `contract`, `status`, `task_kind`, `tasks`, `revision`, `revisions`, or `stages` in update payloads.
- Keep the detailed contract in `docs/Rails-API-Contract.md` in sync with code and tests.
