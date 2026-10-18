using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// A legacy drop-down list: the caption shows the chosen option; Show() opens the list,
    /// built from the template, above everything else.
    public partial class Dropdown
    {
        /// The component on each item of an open list.
        protected internal class DropdownItem : MonoBehaviour, IPointerEnterHandler, ICancelHandler
        {
            [SerializeField] Text m_Text;
            [SerializeField] Image m_Image;
            [SerializeField] RectTransform m_RectTransform;
            [SerializeField] Toggle m_Toggle;

            public Text text { get => m_Text; set => m_Text = value; }
            public Image image { get => m_Image; set => m_Image = value; }
            public RectTransform rectTransform { get => m_RectTransform; set => m_RectTransform = value; }
            public Toggle toggle { get => m_Toggle; set => m_Toggle = value; }

            /// Hovering an item selects it.
            public virtual void OnPointerEnter(PointerEventData eventData) => EventSystem.current.SetSelectedGameObject(gameObject);

            public virtual void OnCancel(BaseEventData eventData)
            {
                var owner = GetComponentInParent<Dropdown>();
                if (owner) owner.Hide();
            }
        }

        static readonly OptionData noOption = new OptionData();

        [NonSerialized] GameObject list;
        [NonSerialized] GameObject blocker;
        [NonSerialized] readonly List<DropdownItem> items = new();
        [NonSerialized] bool templateReady;
        [NonSerialized] Coroutine closing;
        [NonSerialized] Coroutine fading;

        protected Dropdown()
        {
            m_Options = new OptionDataList();
            m_OnValueChanged = new DropdownEvent();
            m_AlphaFadeSpeed = 0.15f;
        }

        public RectTransform template { get => m_Template; set { m_Template = value; RefreshShownValue(); } }
        public Text captionText { get => m_CaptionText; set { m_CaptionText = value; RefreshShownValue(); } }
        public Image captionImage { get => m_CaptionImage; set { m_CaptionImage = value; RefreshShownValue(); } }
        public Text itemText { get => m_ItemText; set { m_ItemText = value; RefreshShownValue(); } }
        public Image itemImage { get => m_ItemImage; set { m_ItemImage = value; RefreshShownValue(); } }

        public List<OptionData> options
        {
            get => m_Options.options;
            set { m_Options.options = value; RefreshShownValue(); }
        }

        public DropdownEvent onValueChanged { get => m_OnValueChanged; set => m_OnValueChanged = value; }

        /// Seconds of the list's fade in and out.
        public float alphaFadeSpeed { get => m_AlphaFadeSpeed; set => m_AlphaFadeSpeed = value; }

        public int value
        {
            get => m_Value;
            set => Set(value);
        }

        public void SetValueWithoutNotify(int input) => Set(input, false);

        /// The value, clamped to the options; nothing happens without options or without a change.
        void Set(int value, bool sendCallback = true)
        {
            if (Application.isPlaying && (value == m_Value || options.Count == 0)) return;
            m_Value = Mathf.Clamp(value, 0, options.Count - 1);
            RefreshShownValue();
            if (!sendCallback) return;
            UISystemProfilerApi.AddMarker("Dropdown.value", this);
            m_OnValueChanged.Invoke(m_Value);
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_CaptionImage) m_CaptionImage.enabled = m_CaptionImage.sprite != null;
            if (m_Template) m_Template.gameObject.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
            RefreshShownValue();
        }

        protected override void OnDisable()
        {
            DestroyListNow();
            if (blocker != null) DestroyBlocker(blocker);
            blocker = null;
            base.OnDisable();
        }

        /// The caption shows the chosen option's text and image (hidden without an image).
        public void RefreshShownValue()
        {
            var data = options.Count > 0 ? options[Mathf.Clamp(m_Value, 0, options.Count - 1)] : noOption;
            if (m_CaptionText) m_CaptionText.text = data?.text ?? "";
            if (m_CaptionImage)
            {
                m_CaptionImage.sprite = data?.image;
                m_CaptionImage.enabled = m_CaptionImage.sprite != null;
            }
        }

        public void AddOptions(List<OptionData> options)
        {
            this.options.AddRange(options);
            RefreshShownValue();
        }

        public void AddOptions(List<string> options)
        {
            foreach (var o in options) this.options.Add(new OptionData(o));
            RefreshShownValue();
        }

        public void AddOptions(List<Sprite> options)
        {
            foreach (var o in options) this.options.Add(new OptionData(o));
            RefreshShownValue();
        }

        public void ClearOptions()
        {
            options.Clear();
            m_Value = 0;
            RefreshShownValue();
        }

        /// The template must have a Toggle below it (the item, on a RectTransform parent), with the
        /// item text and image inside the item. The template becomes a sorting-override canvas
        /// (order 30000) with its canvas's raycasters and a CanvasGroup for fading.
        void SetupTemplate()
        {
            templateReady = false;
            if (!m_Template)
            {
                Debug.LogError("The dropdown template is not assigned. The template needs to be assigned and must have a child GameObject with a Toggle component serving as the item.", this);
                return;
            }
            var go = m_Template.gameObject;
            go.SetActive(true);
            var toggle = m_Template.GetComponentInChildren<Toggle>();
            string problem = null;
            if (!toggle || toggle.transform == m_Template)
                problem = "The dropdown template is not valid. The template must have a child GameObject with a Toggle component serving as the item.";
            else if (toggle.transform.parent is not RectTransform)
                problem = "The dropdown template is not valid. The child GameObject with a Toggle component (the item) must have a RectTransform on its parent.";
            else if (itemText != null && !itemText.transform.IsChildOf(toggle.transform))
                problem = "The dropdown template is not valid. The Item Text must be on the item GameObject or children of it.";
            else if (itemImage != null && !itemImage.transform.IsChildOf(toggle.transform))
                problem = "The dropdown template is not valid. The Item Image must be on the item GameObject or children of it.";
            if (problem != null)
            {
                Debug.LogError(problem, m_Template);
                go.SetActive(false);
                return;
            }

            var item = toggle.gameObject.AddComponent<DropdownItem>();
            item.text = m_ItemText;
            item.image = m_ItemImage;
            item.toggle = toggle;
            item.rectTransform = (RectTransform)toggle.transform;

            var popup = GetOrAdd<Canvas>(go);
            popup.overrideSorting = true;
            popup.sortingOrder = 30000;
            CopyRaycasters(go, m_Template.parent);
            GetOrAdd<CanvasGroup>(go);
            go.SetActive(false);
            templateReady = true;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// The raycasters of the nearest canvas at or above `from` (a GraphicRaycaster without one).
        static void CopyRaycasters(GameObject to, Transform from)
        {
            Canvas parentCanvas = null;
            for (var t = from; t != null && parentCanvas == null; t = t.parent) parentCanvas = t.GetComponent<Canvas>();
            if (parentCanvas == null)
            {
                GetOrAdd<GraphicRaycaster>(to);
                return;
            }
            foreach (var r in parentCanvas.GetComponents<BaseRaycaster>())
                if (to.GetComponent(r.GetType()) == null) to.AddComponent(r.GetType());
        }

        public void Show()
        {
            if (closing != null)
            {
                StopCoroutine(closing);
                DestroyListNow();
            }
            if (!IsActive() || !IsInteractable() || list != null) return;

            var canvases = GetComponentsInParent<Canvas>(false);
            if (canvases.Length == 0) return;
            var root = canvases[canvases.Length - 1];
            foreach (var c in canvases)
                if (c.isRootCanvas)
                {
                    root = c;
                    break;
                }

            if (!templateReady)
            {
                SetupTemplate();
                if (!templateReady) return;
            }

            m_Template.gameObject.SetActive(true);
            m_Template.GetComponent<Canvas>().sortingLayerID = root.sortingLayerID;
            list = CreateDropdownList(m_Template.gameObject);
            list.name = "Dropdown List";
            list.SetActive(true);
            var listRect = (RectTransform)list.transform;
            listRect.SetParent(m_Template.transform.parent, false);

            var itemTemplate = list.GetComponentInChildren<DropdownItem>();
            var contentRect = (RectTransform)itemTemplate.rectTransform.parent;
            itemTemplate.rectTransform.gameObject.SetActive(true);

            // The item's insets in the content, kept for every item.
            var contentArea = contentRect.rect;
            var itemArea = itemTemplate.rectTransform.rect;
            var itemLocal = (Vector2)itemTemplate.rectTransform.localPosition;
            var insetMin = itemArea.min - contentArea.min + itemLocal;
            var insetMax = itemArea.max - contentArea.max + itemLocal;
            var itemSize = itemArea.size;

            items.Clear();
            Toggle previous = null;
            for (int i = 0; i < options.Count; i++)
            {
                var item = AddItem(options[i], itemTemplate);
                if (item == null) continue;
                item.toggle.isOn = value == i;
                var t = item.toggle;
                item.toggle.onValueChanged.AddListener(_ => OnSelectItem(t));
                if (item.toggle.isOn) item.toggle.Select();
                // Items navigate up and down the list.
                if (previous != null)
                {
                    var up = previous.navigation;
                    var down = item.toggle.navigation;
                    up.mode = Navigation.Mode.Explicit;
                    down.mode = Navigation.Mode.Explicit;
                    up.selectOnDown = item.toggle;
                    up.selectOnRight = item.toggle;
                    down.selectOnLeft = previous;
                    down.selectOnUp = previous;
                    previous.navigation = up;
                    item.toggle.navigation = down;
                }
                previous = item.toggle;
            }

            // The content holds the items; a list taller than that shrinks to it.
            var contentSize = contentRect.sizeDelta;
            contentSize.y = itemSize.y * items.Count + insetMin.y - insetMax.y;
            contentRect.sizeDelta = contentSize;
            float extra = listRect.rect.height - contentRect.rect.height;
            if (extra > 0f) listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, listRect.sizeDelta.y - extra);

            // A list reaching outside the root canvas opens the other way on that axis.
            var corners = new Vector3[4];
            listRect.GetWorldCorners(corners);
            var rootRect = (RectTransform)root.transform;
            var bounds = rootRect.rect;
            for (int axis = 0; axis < 2; axis++)
            {
                bool outside = false;
                foreach (var corner in corners)
                {
                    var p = rootRect.InverseTransformPoint(corner);
                    if (p[axis] < bounds.min[axis] && !Mathf.Approximately(p[axis], bounds.min[axis])
                        || p[axis] > bounds.max[axis] && !Mathf.Approximately(p[axis], bounds.max[axis]))
                    {
                        outside = true;
                        break;
                    }
                }
                if (outside) RectTransformUtility.FlipLayoutOnAxis(listRect, axis, false, false);
            }

            // Items stack from the top of the content.
            for (int i = 0; i < items.Count; i++)
            {
                var r = items[i].rectTransform;
                r.anchorMin = new Vector2(r.anchorMin.x, 0f);
                r.anchorMax = new Vector2(r.anchorMax.x, 0f);
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, insetMin.y + itemSize.y * (items.Count - 1 - i) + itemSize.y * r.pivot.y);
                r.sizeDelta = new Vector2(r.sizeDelta.x, itemSize.y);
            }

            FadeList(m_AlphaFadeSpeed, 0f, 1f);
            m_Template.gameObject.SetActive(false);
            itemTemplate.gameObject.SetActive(false);
            blocker = CreateBlocker(root);
        }

        /// A clear, full-canvas button just below the list: clicking outside the list closes it.
        protected virtual GameObject CreateBlocker(Canvas rootCanvas)
        {
            var go = new GameObject("Blocker");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(rootCanvas.transform, false);
            rect.anchorMin = Vector3.zero;
            rect.anchorMax = Vector3.one;
            rect.sizeDelta = Vector2.zero;
            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            var listCanvas = list.GetComponent<Canvas>();
            canvas.sortingLayerID = listCanvas.sortingLayerID;
            canvas.sortingOrder = listCanvas.sortingOrder - 1;
            CopyRaycasters(go, m_Template.parent);
            var image = go.AddComponent<Image>();
            image.color = Color.clear;
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(Hide);
            return go;
        }

        protected virtual void DestroyBlocker(GameObject blocker) => Destroy(blocker);

        protected virtual GameObject CreateDropdownList(GameObject template) => Instantiate(template);

        protected virtual void DestroyDropdownList(GameObject dropdownList) => Destroy(dropdownList);

        protected virtual DropdownItem CreateItem(DropdownItem itemTemplate) => Instantiate(itemTemplate);

        protected virtual void DestroyItem(DropdownItem item) { }

        /// A copy of the item template, named "Item N: text", showing the option.
        DropdownItem AddItem(OptionData data, DropdownItem itemTemplate)
        {
            var item = CreateItem(itemTemplate);
            item.rectTransform.SetParent(itemTemplate.rectTransform.parent, false);
            item.gameObject.SetActive(true);
            item.gameObject.name = "Item " + items.Count + (data.text != null ? ": " + data.text : "");
            if (item.toggle != null) item.toggle.isOn = false;
            if (item.text) item.text.text = data.text;
            if (item.image)
            {
                item.image.sprite = data.image;
                item.image.enabled = item.image.sprite != null;
            }
            items.Add(item);
            return item;
        }

        void FadeList(float duration, float alpha)
        {
            var group = list.GetComponent<CanvasGroup>();
            FadeList(duration, group.alpha, alpha);
        }

        void FadeList(float duration, float from, float to)
        {
            if (to.Equals(from)) return;
            if (fading != null) StopCoroutine(fading);
            fading = null;
            if (!gameObject.activeInHierarchy)
            {
                SetListAlpha(to);
                return;
            }
            fading = StartCoroutine(Fade(duration, from, to));
        }

        IEnumerator Fade(float duration, float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetListAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetListAlpha(to);
            fading = null;
        }

        void SetListAlpha(float alpha)
        {
            if (!list) return;
            list.GetComponent<CanvasGroup>().alpha = alpha;
        }

        /// Fades the list out and destroys it after the fade; removes the blocker and selects the
        /// dropdown again.
        public void Hide()
        {
            if (closing != null) return;
            if (list != null)
            {
                FadeList(m_AlphaFadeSpeed, 0f);
                // The value callback may have disabled the dropdown.
                if (IsActive()) closing = StartCoroutine(DestroyListAfter(m_AlphaFadeSpeed));
            }
            if (blocker != null) DestroyBlocker(blocker);
            blocker = null;
            Select();
        }

        IEnumerator DestroyListAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            DestroyListNow();
        }

        void DestroyListNow()
        {
            foreach (var item in items)
                if (item != null) DestroyItem(item);
            items.Clear();
            if (list != null) DestroyDropdownList(list);
            if (fading != null) StopCoroutine(fading);
            fading = null;
            list = null;
            closing = null;
        }

        /// Choosing an item (by its place among the content's children after the template) sets
        /// the value and closes the list.
        void OnSelectItem(Toggle toggle)
        {
            if (!toggle.isOn) toggle.isOn = true;
            var t = toggle.transform;
            int index = t.GetSiblingIndex() - 1;
            if (index < 0) return;
            value = index;
            Hide();
        }

        public virtual void OnPointerClick(PointerEventData eventData) => Show();

        public virtual void OnSubmit(BaseEventData eventData) => Show();

        public virtual void OnCancel(BaseEventData eventData) => Hide();

        public partial class OptionData
        {
            public OptionData() { }
            public OptionData(string text) => m_Text = text;
            public OptionData(Sprite image) => m_Image = image;

            public OptionData(string text, Sprite image)
            {
                m_Text = text;
                m_Image = image;
            }

            public string text { get => m_Text; set => m_Text = value; }
            public Sprite image { get => m_Image; set => m_Image = value; }
        }

        public partial class OptionDataList
        {
            public OptionDataList() => m_Options = new List<OptionData>();

            public List<OptionData> options { get => m_Options; set => m_Options = value; }
        }

        public partial class DropdownEvent
        {
            public DropdownEvent() { }
        }
    }
}
