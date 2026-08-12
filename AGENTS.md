# AGENTS.md

## Frontend (Vite dev server)

During development the frontend runs on the Vite dev server (`cd frontend && npm run dev`,
proxies `/api` to `http://localhost:5090`).

- Do **not** run `npm run build` to check for errors during dev. Vite surfaces compile/TS
  errors in the dev-server terminal and as a browser overlay.
- For a fast typecheck without a full bundle, use `npx tsc --noEmit` in `frontend/`.
- `npm run build` is only for verifying the production bundle (e.g. before Docker rebuilds).
- Run frontend unit tests with `npm run test -- --run`.
