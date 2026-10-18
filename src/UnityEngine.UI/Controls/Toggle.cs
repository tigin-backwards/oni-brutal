using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// An on/off control: a click or submit flips isOn, shown by the alpha of `graphic`. In a
    /// ToggleGroup, at most one is on.
    public partial class Toggle
    {
        protected Toggle()
        {
            toggleTransition = ToggleTransition.Fade;
            onValueChanged = new ToggleEvent();
        }

        public ToggleGroup group
        {
            get => m_Group;
            set
            {
                SetToggleGroup(value, true);
                PlayEffect(true);
            }
        }

        public bool isOn
        {
            get => m_IsOn;
            set => Set(value);
        }

        public void SetIsOnWithoutNotify(bool value) => Set(value, false);

        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }

        protected override void OnDestroy()
        {
            if (m_Group != null) m_Group.EnsureValidState();
            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetToggleGroup(m_Group, false);
            PlayEffect(true);
        }

        protected override void OnDisable()
        {
            SetToggleGroup(null, false);
            base.OnDisable();
        }

        /// An animated graphic alpha drives the value.
        protected override void OnDidApplyAnimationProperties()
        {
            if (graphic != null)
            {
                bool shown = !Mathf.Approximately(graphic.canvasRenderer.GetColor().a, 0f);
                if (m_IsOn != shown)
                {
                    m_IsOn = shown;
                    Set(!shown);
                }
            }
            base.OnDidApplyAnimationProperties();
        }

        /// Leaves the current group; joins the new one while active (taking over as its toggle
        /// that is on, when on). setMemberValue keeps the new group as the toggle's group.
        void SetToggleGroup(ToggleGroup newGroup, bool setMemberValue)
        {
            if (m_Group != null) m_Group.UnregisterToggle(this);
            if (setMemberValue) m_Group = newGroup;
            if (newGroup != null && IsActive()) newGroup.RegisterToggle(this);
            if (newGroup != null && m_IsOn && IsActive()) newGroup.NotifyToggleOn(this);
        }

        /// In a group that may not be all off, the last toggle on stays on.
        void Set(bool value, bool sendCallback = true)
        {
            if (m_IsOn == value) return;
            m_IsOn = value;
            if (m_Group != null && m_Group.isActiveAndEnabled && IsActive())
            {
                if (m_IsOn || (!m_Group.AnyTogglesOn() && !m_Group.allowSwitchOff))
                {
                    m_IsOn = true;
                    m_Group.NotifyToggleOn(this, sendCallback);
                }
            }
            PlayEffect(toggleTransition == ToggleTransition.None);
            if (sendCallback)
            {
                UISystemProfilerApi.AddMarker("Toggle.value", this);
                onValueChanged.Invoke(m_IsOn);
            }
        }

        /// The graphic fades in or out over 0.1 s (at once with no transition).
        void PlayEffect(bool instant)
        {
            if (graphic == null) return;
            graphic.CrossFadeAlpha(m_IsOn ? 1f : 0f, instant ? 0f : 0.1f, true);
        }

        protected override void Start()
        {
            base.Start();
            PlayEffect(true);
        }

        void InternalToggle()
        {
            if (!IsActive() || !IsInteractable()) return;
            isOn = !isOn;
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            InternalToggle();
        }

        public virtual void OnSubmit(BaseEventData eventData) => InternalToggle();

        public partial class ToggleEvent
        {
            public ToggleEvent() { }
        }
    }

    /// Keeps at most one of its toggles on (exactly one unless allowSwitchOff).
    public partial class ToggleGroup
    {
        protected List<Toggle> m_Toggles = new List<Toggle>();

        protected ToggleGroup() { }

        public bool allowSwitchOff
        {
            get => m_AllowSwitchOff;
            set => m_AllowSwitchOff = value;
        }

        protected override void Start()
        {
            EnsureValidState();
            base.Start();
        }

        protected override void OnEnable()
        {
            EnsureValidState();
            base.OnEnable();
        }

        void ValidateToggleIsInGroup(Toggle toggle)
        {
            if (toggle == null || !m_Toggles.Contains(toggle))
                throw new ArgumentException($"Toggle {toggle} is not part of ToggleGroup {this}");
        }

        public void NotifyToggleOn(Toggle toggle, bool sendCallback = true)
        {
            ValidateToggleIsInGroup(toggle);
            foreach (var other in m_Toggles.ToArray())
            {
                if (other == toggle) continue;
                if (sendCallback) other.isOn = false;
                else other.SetIsOnWithoutNotify(false);
            }
        }

        public void UnregisterToggle(Toggle toggle) => m_Toggles.Remove(toggle);

        public void RegisterToggle(Toggle toggle)
        {
            if (!m_Toggles.Contains(toggle)) m_Toggles.Add(toggle);
        }

        /// Without allowSwitchOff the first toggle is turned on when none is; only the first of
        /// several toggles that are on stays on.
        public void EnsureValidState()
        {
            if (!allowSwitchOff && !AnyTogglesOn() && m_Toggles.Count != 0)
            {
                m_Toggles[0].isOn = true;
                NotifyToggleOn(m_Toggles[0]);
            }
            var on = ActiveToggles().ToList();
            if (on.Count <= 1) return;
            var first = GetFirstActiveToggle();
            foreach (var t in on)
                if (t != first) t.isOn = false;
        }

        public bool AnyTogglesOn() => m_Toggles.Find(t => t.isOn) != null;

        public IEnumerable<Toggle> ActiveToggles() => m_Toggles.Where(t => t.isOn);

        public Toggle GetFirstActiveToggle() => ActiveToggles().FirstOrDefault();

        /// Every toggle off, allowed for the moment even without allowSwitchOff.
        public void SetAllTogglesOff(bool sendCallback = true)
        {
            bool allowed = m_AllowSwitchOff;
            m_AllowSwitchOff = true;
            foreach (var t in m_Toggles.ToArray())
            {
                if (sendCallback) t.isOn = false;
                else t.SetIsOnWithoutNotify(false);
            }
            m_AllowSwitchOff = allowed;
        }
    }
}
