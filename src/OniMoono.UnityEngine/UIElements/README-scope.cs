// UnityEngine.UIElements (UI Toolkit): scope of this implementation.
//
// Why these types exist. The shipped UnityEngine.UI.dll (uGUI) carries a UI Toolkit bridge
// (EventSystem's UIToolkitInteroperabilityBridge, PanelEventHandler, PanelRaycaster,
// WorldDocumentRaycaster, RaycastResult.document/element, BaseInputModule pointer-id
// conversion). That IL binds to UI Toolkit types and must load and JIT. The game ships no UI
// Toolkit content: no PanelSettings, no UIDocument, no VisualTreeAsset.
//
// The runtime is permanently panel-less. No panel can ever be created here: Unity creates
// panels from PanelSettings/UIDocument and the native panel system, none of which we implement.
// Consequently:
//   - BaseVisualElementPanel / Panel / BaseRuntimePanel / RuntimePanel have no reachable
//     constructor. Their instance members exist only where IL must bind to them; data-only
//     members follow Unity's fields, interface members follow what a panel root without content
//     would do.
//   - UIElementsRuntimeUtility's panel registry is empty: GetSortedPlayerPanels() returns an empty
//     list, onCreatePanel never fires. The event-system registration (RegisterEventSystem,
//     activeEventSystem) and PointerDeviceState (pressed buttons, soft capture) are ordinary
//     state, implemented in full.
//   - VisualElement has no public constructor here (Unity's builds style/layout/hierarchy state we
//     do not model; only UI Toolkit content creates elements), and FocusController / ClickDetector
//     likewise have none. VisualElement.panel is always null (only the panel system attaches an
//     element to a panel), so SendEvent forwards to nothing and focusController is null, as in
//     Unity for a detached element. EventBase.target / elementTarget are therefore always null.
//   - PanelInputConfiguration and UIDocument components: only the static state uGUI reads exists
//     (current, onApply, settings, EnabledDocumentCount). Their OnEnable/OnDisable lifecycles drive
//     UI Toolkit's own event system and panels and are not implemented; the game has neither
//     component.
//   - AssemblyInfo.cs grants UnityEngine.UI access to internals, as Unity's module does.
//   - Events are plain data holders with Unity's pooling (EventBase<T>.GetPooled / Dispose with
//     reference counting and a per-type pool of 100). They are never dispatched: dispatch is
//     the panel's job.
//
// Deliberately absent (they only act on a live panel, or need UI Toolkit types outside our
// surface; they are reached from uGUI methods that run only when a panel exists, so absence
// cannot fire): BaseVisualElementPanel.SendEvent / SetTopElementUnderPointer /
// CommitElementUnderPointers, BaseRuntimePanel.ScreenToPanel / PointerLeavesPanel, Panel.Blur,
// UIElementsRuntimeUtility.CreateEvent, ScreenOverlayPanelPicker.TryPick,
// PhysicsDocumentPicker.TryPickWithCapture, UIDocument.rootVisualElement / containerPanel,
// VisualElementExtensions.IntersectWorldRay, and the dispatch hooks of every event type
// (PreDispatch/PostDispatch/Dispatch, currentTarget, propagationPhase, imguiEvent).
