using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TMPro
{
    // A dropdown with TMP texts, like uGUI's Dropdown: a caption showing the selected option and,
    // when shown, a list built from the template under its own canvas, with a blocker behind it
    // that closes it. Choosing an item sets the value and hides the list.
    public partial class TMP_Dropdown
    {
        GameObject m_List;
        GameObject m_Blocker;
        readonly List<DropdownItem> m_Items = new();
        bool m_TemplateValidated;
        Coroutine m_Fade;
        static OptionData s_NoOptionData = new();

        protected TMP_Dropdown()
        {
            m_Options = new OptionDataList();
            m_OnValueChanged = new DropdownEvent();
            m_AlphaFadeSpeed = 0.15f;
        }

        public RectTransform template
        {
            get => m_Template;
            set
            {
                m_Template = value;
                RefreshShownValue();
            }
        }

        public TMP_Text captionText
        {
            get => m_CaptionText;
            set
            {
                m_CaptionText = value;
                RefreshShownValue();
            }
        }

        public Image captionImage
        {
            get => m_CaptionImage;
            set
            {
                m_CaptionImage = value;
                RefreshShownValue();
            }
        }

        public Graphic placeholder
        {
            get => m_Placeholder;
            set
            {
                m_Placeholder = value;
                RefreshShownValue();
            }
        }

        public TMP_Text itemText
        {
            get => m_ItemText;
            set
            {
                m_ItemText = value;
                RefreshShownValue();
            }
        }

        public Image itemImage
        {
            get => m_ItemImage;
            set
            {
                m_ItemImage = value;
                RefreshShownValue();
            }
        }

        public List<OptionData> options
        {
            get => m_Options.options;
            set
            {
                m_Options.options = value;
                RefreshShownValue();
            }
        }

        public DropdownEvent onValueChanged
        {
            get => m_OnValueChanged;
            set => m_OnValueChanged = value;
        }

        public float alphaFadeSpeed
        {
            get => m_AlphaFadeSpeed;
            set => m_AlphaFadeSpeed = value;
        }

        public int value
        {
            get => m_Value;
            set => SetValue(value, true);
        }

        public void SetValueWithoutNotify(int input) => SetValue(input, false);

        void SetValue(int input, bool sendCallback)
        {
            if (Application.isPlaying && (input == m_Value || options.Count == 0)) return;
            m_Value = Mathf.Clamp(input, m_Placeholder != null ? -1 : 0, options.Count - 1);
            RefreshShownValue();
            if (sendCallback) m_OnValueChanged.Invoke(m_Value);
        }

        public bool IsExpanded => m_List != null;

        public bool MultiSelect { get => m_MultiSelect; set => m_MultiSelect = value; }

        protected override void Awake()
        {
            base.Awake();
            if (m_CaptionImage != null) m_CaptionImage.enabled = m_CaptionImage.sprite != null && m_CaptionImage.color.a > 0f;
            if (m_Template != null) m_Template.gameObject.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
            RefreshShownValue();
        }

        protected override void OnDisable()
        {
            ImmediateDestroyList();
            if (m_Blocker != null) Destroy(m_Blocker);
            m_Blocker = null;
            base.OnDisable();
        }

        // The caption shows the selected option's text and image (the placeholder when none).
        public void RefreshShownValue()
        {
            var data = s_NoOptionData;
            if (options.Count > 0 && m_Value >= 0) data = options[Mathf.Clamp(m_Value, 0, options.Count - 1)];
            if (m_CaptionText != null) m_CaptionText.text = data.text ?? "";
            if (m_CaptionImage != null)
            {
                m_CaptionImage.sprite = data.image;
                m_CaptionImage.color = data.color;
                m_CaptionImage.enabled = m_CaptionImage.sprite != null && m_CaptionImage.color.a > 0f;
            }
            if (m_Placeholder != null) m_Placeholder.enabled = options.Count == 0 || m_Value == -1;
        }

        public void AddOptions(List<OptionData> options)
        {
            this.options.AddRange(options);
            RefreshShownValue();
        }

        public void AddOptions(List<string> options)
        {
            foreach (var s in options) this.options.Add(new OptionData(s));
            RefreshShownValue();
        }

        public void AddOptions(List<Sprite> options)
        {
            foreach (var s in options) this.options.Add(new OptionData(s));
            RefreshShownValue();
        }

        public void ClearOptions()
        {
            options.Clear();
            m_Value = m_Placeholder != null ? -1 : 0;
            RefreshShownValue();
        }

        public virtual void OnPointerClick(PointerEventData eventData) => Show();

        public virtual void OnSubmit(BaseEventData eventData) => Show();

        public virtual void OnCancel(BaseEventData eventData) => Hide();

        // ----- The list -----

        bool ValidateTemplate()
        {
            if (m_Template == null) return false;
            var templateGo = m_Template.gameObject;
            templateGo.SetActive(true);
            var toggle = m_Template.GetComponentInChildren<Toggle>();
            bool valid = toggle != null && toggle.transform != m_Template && toggle.transform.parent is RectTransform
                && (m_ItemText == null || m_ItemText.transform.IsChildOf(toggle.transform))
                && (m_ItemImage == null || m_ItemImage.transform.IsChildOf(toggle.transform));
            if (!valid)
            {
                templateGo.SetActive(false);
                Debug.LogError("The dropdown template is not valid. The template must have a child GameObject with a Toggle component serving as the item.", m_Template);
                return false;
            }
            var item = toggle.gameObject.AddComponent<DropdownItem>();
            item.text = m_ItemText;
            item.image = m_ItemImage;
            item.toggle = toggle;
            item.rectTransform = (RectTransform)toggle.transform;
            // The list draws above everything, in its own canvas.
            var canvas = templateGo.GetComponent<Canvas>() ?? templateGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            if (templateGo.GetComponent<GraphicRaycaster>() == null) templateGo.AddComponent<GraphicRaycaster>();
            if (templateGo.GetComponent<CanvasGroup>() == null) templateGo.AddComponent<CanvasGroup>();
            templateGo.SetActive(false);
            m_TemplateValidated = true;
            return true;
        }

        public void Show()
        {
            if (m_Fade != null)
            {
                StopCoroutine(m_Fade);
                m_Fade = null;
                ImmediateDestroyList();
            }
            if (!IsActive() || !IsInteractable() || m_List != null) return;
            var rootCanvas = RootCanvas();
            if (rootCanvas == null) return;
            if (!m_TemplateValidated && !ValidateTemplate()) return;

            m_Template.gameObject.SetActive(true);
            m_Template.GetComponent<Canvas>().sortingLayerID = rootCanvas.sortingLayerID;
            m_List = CreateDropdownList(m_Template.gameObject);
            m_List.name = "Dropdown List";
            m_List.SetActive(true);
            var listRect = (RectTransform)m_List.transform;
            listRect.SetParent(m_Template.parent, false);

            var itemTemplate = m_List.GetComponentInChildren<DropdownItem>();
            var content = (RectTransform)itemTemplate.rectTransform.parent;
            var templateItemRect = itemTemplate.rectTransform;
            templateItemRect.gameObject.SetActive(true);
            var contentRect = content.rect;
            var itemRect = templateItemRect.rect;
            Vector2 offsetMin = itemRect.min - contentRect.min + (Vector2)templateItemRect.localPosition;
            Vector2 offsetMax = itemRect.max - contentRect.max + (Vector2)templateItemRect.localPosition;
            Vector2 itemSize = itemRect.size;

            m_Items.Clear();
            Toggle previous = null;
            for (int i = 0; i < options.Count; i++)
            {
                var data = options[i];
                var item = AddItem(data, m_Value == i, itemTemplate, m_Items);
                if (item == null) continue;
                item.toggle.isOn = m_Value == i;
                int index = i;
                item.toggle.onValueChanged.AddListener(_ => OnSelectItem(item.toggle, index));
                if (item.toggle.isOn) item.toggle.Select();
                if (previous != null)
                {
                    var nav = previous.navigation;
                    var toggleNav = item.toggle.navigation;
                    nav.mode = Navigation.Mode.Explicit;
                    toggleNav.mode = Navigation.Mode.Explicit;
                    nav.selectOnDown = item.toggle;
                    nav.selectOnRight = item.toggle;
                    toggleNav.selectOnLeft = previous;
                    toggleNav.selectOnUp = previous;
                    previous.navigation = nav;
                    item.toggle.navigation = toggleNav;
                }
                previous = item.toggle;
            }

            // The content fits the items; the list stays inside the root canvas, flipping
            // above the dropdown when it would leave it.
            var sizeDelta = content.sizeDelta;
            sizeDelta.y = itemSize.y * m_Items.Count + offsetMin.y - offsetMax.y;
            content.sizeDelta = sizeDelta;
            float extraSpace = listRect.rect.height - content.rect.height;
            if (extraSpace > 0f) listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, listRect.sizeDelta.y - extraSpace);

            var corners = new Vector3[4];
            listRect.GetWorldCorners(corners);
            var rootRect = (RectTransform)rootCanvas.transform;
            var rootBounds = rootRect.rect;
            for (int axis = 0; axis < 2; axis++)
            {
                bool outside = false;
                for (int k = 0; k < 4; k++)
                {
                    var c = rootRect.InverseTransformPoint(corners[k]);
                    if ((c[axis] < rootBounds.min[axis] && !Mathf.Approximately(c[axis], rootBounds.min[axis]))
                        || (c[axis] > rootBounds.max[axis] && !Mathf.Approximately(c[axis], rootBounds.max[axis])))
                    {
                        outside = true;
                        break;
                    }
                }
                if (outside) RectTransformUtility.FlipLayoutOnAxis(listRect, axis, false, false);
            }

            for (int i = 0; i < m_Items.Count; i++)
            {
                var rt = m_Items[i].rectTransform;
                rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
                rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, offsetMin.y + itemSize.y * (m_Items.Count - 1 - i) + itemSize.y * rt.pivot.y);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, itemSize.y);
            }

            AlphaFadeList(m_AlphaFadeSpeed, 0f, 1f);
            m_Template.gameObject.SetActive(false);
            itemTemplate.gameObject.SetActive(false);
            m_Blocker = CreateBlocker(rootCanvas);
        }

        Canvas RootCanvas()
        {
            var canvases = GetComponentsInParent<Canvas>(false);
            if (canvases.Length == 0) return null;
            var root = canvases[canvases.Length - 1];
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i].isRootCanvas || canvases[i].overrideSorting) { root = canvases[i]; break; }
            return root;
        }

        // A full-screen transparent button under the list (in the root canvas, sorted just
        // below the list) that hides the list when clicked.
        protected virtual GameObject CreateBlocker(Canvas rootCanvas)
        {
            var blocker = new GameObject("Blocker", typeof(RectTransform));
            blocker.layer = rootCanvas.gameObject.layer;
            var rt = (RectTransform)blocker.transform;
            rt.SetParent(rootCanvas.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var canvas = blocker.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            var listCanvas = m_List.GetComponent<Canvas>();
            canvas.sortingLayerID = listCanvas.sortingLayerID;
            canvas.sortingOrder = listCanvas.sortingOrder - 1;
            Canvas parentCanvas = null;
            var parent = m_Template.parent;
            while (parent != null)
            {
                parentCanvas = parent.GetComponent<Canvas>();
                if (parentCanvas != null) break;
                parent = parent.parent;
            }
            if (parentCanvas != null)
            {
                var raycasters = parentCanvas.GetComponents<BaseRaycaster>();
                foreach (var r in raycasters)
                {
                    var type = r.GetType();
                    if (blocker.GetComponent(type) == null) blocker.AddComponent(type);
                }
            }
            else if (blocker.GetComponent<GraphicRaycaster>() == null) blocker.AddComponent<GraphicRaycaster>();
            var image = blocker.AddComponent<Image>();
            image.color = Color.clear;
            var button = blocker.AddComponent<Button>();
            button.onClick.AddListener(Hide);
            var group = blocker.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            return blocker;
        }

        protected virtual void DestroyBlocker(GameObject blocker) => Destroy(blocker);

        protected virtual GameObject CreateDropdownList(GameObject template) => Instantiate(template);

        protected virtual void DestroyDropdownList(GameObject dropdownList) => Destroy(dropdownList);

        protected virtual DropdownItem CreateItem(DropdownItem itemTemplate) => Instantiate(itemTemplate);

        protected virtual void DestroyItem(DropdownItem item) { }

        DropdownItem AddItem(OptionData data, bool selected, DropdownItem itemTemplate, List<DropdownItem> items)
        {
            var item = CreateItem(itemTemplate);
            item.rectTransform.SetParent(itemTemplate.rectTransform.parent, false);
            item.gameObject.SetActive(true);
            item.gameObject.name = "Item " + items.Count + (data.text != null ? ": " + data.text : "");
            if (item.toggle != null) item.toggle.isOn = false;
            if (item.text != null) item.text.text = data.text ?? "";
            if (item.image != null)
            {
                item.image.sprite = data.image;
                item.image.color = data.color;
                item.image.enabled = item.image.sprite != null && data.color.a > 0f;
            }
            items.Add(item);
            return item;
        }

        void AlphaFadeList(float duration, float start, float end)
        {
            var group = m_List.GetComponent<CanvasGroup>();
            if (group == null) return;
            if (duration <= 0f || end == start || !isActiveAndEnabled)
            {
                group.alpha = end;
                return;
            }
            group.alpha = start;
            m_Fade = StartCoroutine(Fade(group, duration, start, end, false));
        }

        IEnumerator Fade(CanvasGroup group, float duration, float start, float end, bool destroyAfter)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Lerp(start, end, Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (group != null) group.alpha = end;
            m_Fade = null;
            if (destroyAfter) ImmediateDestroyList();
        }

        public void Hide()
        {
            if (m_List != null)
            {
                var group = m_List.GetComponent<CanvasGroup>();
                if (m_Fade != null) StopCoroutine(m_Fade);
                if (group != null && m_AlphaFadeSpeed > 0f && isActiveAndEnabled)
                    m_Fade = StartCoroutine(Fade(group, m_AlphaFadeSpeed, group.alpha, 0f, true));
                else ImmediateDestroyList();
            }
            if (m_Blocker != null) DestroyBlocker(m_Blocker);
            m_Blocker = null;
            Select();
        }

        void ImmediateDestroyList()
        {
            foreach (var item in m_Items)
                if (item != null) DestroyItem(item);
            m_Items.Clear();
            if (m_List != null) DestroyDropdownList(m_List);
            if (m_Fade != null && isActiveAndEnabled) StopCoroutine(m_Fade);
            m_Fade = null;
            m_List = null;
        }

        void OnSelectItem(Toggle toggle, int index)
        {
            if (!toggle.isOn) toggle.isOn = true;
            value = index;
            Hide();
        }

        // ----- Nested types -----

        // An item of the shown list: its toggle, text and image.
        protected internal class DropdownItem : MonoBehaviour, IPointerEnterHandler, ICancelHandler
        {
            [SerializeField] TMP_Text m_Text;
            [SerializeField] Image m_Image;
            [SerializeField] RectTransform m_RectTransform;
            [SerializeField] Toggle m_Toggle;

            public TMP_Text text { get => m_Text; set => m_Text = value; }
            public Image image { get => m_Image; set => m_Image = value; }
            public RectTransform rectTransform { get => m_RectTransform; set => m_RectTransform = value; }
            public Toggle toggle { get => m_Toggle; set => m_Toggle = value; }

            public virtual void OnPointerEnter(PointerEventData eventData) => EventSystem.current?.SetSelectedGameObject(gameObject);

            public virtual void OnCancel(BaseEventData eventData)
            {
                var dropdown = GetComponentInParent<TMP_Dropdown>();
                if (dropdown != null) dropdown.Hide();
            }
        }

        public partial class OptionData
        {
            public OptionData()
            {
                m_Color = Color.white;
            }

            public OptionData(string text)
            {
                m_Text = text;
                m_Color = Color.white;
            }

            public OptionData(Sprite image)
            {
                m_Image = image;
                m_Color = Color.white;
            }

            public OptionData(string text, Sprite image, Color color)
            {
                m_Text = text;
                m_Image = image;
                m_Color = color;
            }

            public string text { get => m_Text; set => m_Text = value; }
            public Sprite image { get => m_Image; set => m_Image = value; }
            public Color color { get => m_Color; set => m_Color = value; }
        }

        public partial class OptionDataList
        {
            public OptionDataList()
            {
                m_Options = new List<OptionData>();
            }

            public List<OptionData> options { get => m_Options; set => m_Options = value; }
        }
    }
}
