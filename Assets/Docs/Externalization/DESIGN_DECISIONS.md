# RPG PROJECT — DESIGN DECISIONS & TECHNICAL STANDARDS
**Status Classification**: `CANONICAL` | `IMPLEMENTED` | `DOCUMENTED` | `INFERRED` | `PROPOSED` | `UNKNOWN` | `CONFLICTING`

---

## 1. Core Architectural Decisions (Jostin's Development Style)
1. **Decoupling via Service Locator**:
   - Monolithic singletons and direct manager couplings are prohibited.
   - Systems resolve dependencies at runtime using `ServiceLocator.Get<T>()`.
2. **Event-Driven Communication**:
   - Systems communicate across boundaries using `GameEventManager<T>.Publish(T eventData)`.
   - Observers subscribe in `OnEnable` and unsubscribe in `OnDisable`.
3. **Data / Instance Separation**:
   - ScriptableObjects are strictly immutable configurations, definitions, and catalogs (e.g. `SkillDefinitionSO`, `FactionMatrixSO`, `BlockCatalogSO`).
   - Pure C# classes or MonoBehaviours manage mutable runtime state (e.g. `SkillProgress`, `ActorHealth`).
4. **Code Quality Constraints**:
   - `PascalCase` for C# methods, properties, and classes.
   - Methods must remain concise (< 50 lines), breaking down complex logic into modular helper functions.
   - Clean region blocks (`#region ... #endregion`) and `[AddComponentMenu]` on all MonoBehaviours.

---

## 2. Asset & Rendering Decisions
1. **Universal Render Pipeline (URP 17.3.0)**:
   - All shaders must be URP Shader Graph or URP-compatible lit materials.
   - Built-in Standard shaders are strictly prohibited (they result in magenta/pink errors).
2. **Art Asset Purge**:
   - Modern assets (`SimpleApocalypse`, `RetroWeaponsPack`, `Game Kit Controller`) were purged to reclaim 2.77 GB disk space and eliminate conflicting weapon/input scripts.
   - Modular 3D fantasy environments are standardized on **Synty POLYGON Fantasy Kingdom**.
3. **Input Handling**:
   - Set to `activeInputHandler: 2` (Both) in `ProjectSettings.asset` to support both legacy standalone utilities and the new `com.unity.inputsystem`.
