using System;

namespace UnityEngine.UI
{
    /// The tint of a Selectable's target graphic in each state.
    public partial struct ColorBlock
    {
        public Color normalColor { get => m_NormalColor; set => m_NormalColor = value; }
        public Color highlightedColor { get => m_HighlightedColor; set => m_HighlightedColor = value; }
        public Color pressedColor { get => m_PressedColor; set => m_PressedColor = value; }
        public Color selectedColor { get => m_SelectedColor; set => m_SelectedColor = value; }
        public Color disabledColor { get => m_DisabledColor; set => m_DisabledColor = value; }
        public float colorMultiplier { get => m_ColorMultiplier; set => m_ColorMultiplier = value; }
        public float fadeDuration { get => m_FadeDuration; set => m_FadeDuration = value; }

        public static ColorBlock defaultColorBlock;

        static ColorBlock()
        {
            defaultColorBlock = new ColorBlock
            {
                m_NormalColor = new Color32(255, 255, 255, 255),
                m_HighlightedColor = new Color32(245, 245, 245, 255),
                m_PressedColor = new Color32(200, 200, 200, 255),
                m_SelectedColor = new Color32(245, 245, 245, 255),
                m_DisabledColor = new Color32(200, 200, 200, 128),
                m_ColorMultiplier = 1f,
                m_FadeDuration = 0.1f,
            };
        }

        public override bool Equals(object obj) => obj is ColorBlock other && Equals(other);

        public bool Equals(ColorBlock other)
            => normalColor == other.normalColor && highlightedColor == other.highlightedColor && pressedColor == other.pressedColor
               && selectedColor == other.selectedColor && disabledColor == other.disabledColor
               && colorMultiplier == other.colorMultiplier && fadeDuration == other.fadeDuration;

        public static bool operator ==(ColorBlock point1, ColorBlock point2) => point1.Equals(point2);
        public static bool operator !=(ColorBlock point1, ColorBlock point2) => !point1.Equals(point2);

        public override int GetHashCode() => base.GetHashCode();
    }

    /// How a Selectable finds its neighbours for keyboard and controller navigation.
    public partial struct Navigation
    {
        public Mode mode { get => m_Mode; set => m_Mode = value; }

        /// Horizontal or vertical navigation continues from the opposite end at the edge.
        public bool wrapAround { get => m_WrapAround; set => m_WrapAround = value; }

        public Selectable selectOnUp { get => m_SelectOnUp; set => m_SelectOnUp = value; }
        public Selectable selectOnDown { get => m_SelectOnDown; set => m_SelectOnDown = value; }
        public Selectable selectOnLeft { get => m_SelectOnLeft; set => m_SelectOnLeft = value; }
        public Selectable selectOnRight { get => m_SelectOnRight; set => m_SelectOnRight = value; }

        public static Navigation defaultNavigation => new Navigation { m_Mode = Mode.Automatic, m_WrapAround = false };

        public bool Equals(Navigation other)
            => mode == other.mode && selectOnUp == other.selectOnUp && selectOnDown == other.selectOnDown
               && selectOnLeft == other.selectOnLeft && selectOnRight == other.selectOnRight;
    }

    /// The sprites a Selectable's image swaps to in each state.
    public partial struct SpriteState
    {
        public Sprite highlightedSprite { get => m_HighlightedSprite; set => m_HighlightedSprite = value; }
        public Sprite pressedSprite { get => m_PressedSprite; set => m_PressedSprite = value; }
        public Sprite selectedSprite { get => m_SelectedSprite; set => m_SelectedSprite = value; }
        public Sprite disabledSprite { get => m_DisabledSprite; set => m_DisabledSprite = value; }

        public bool Equals(SpriteState other)
            => highlightedSprite == other.highlightedSprite && pressedSprite == other.pressedSprite
               && selectedSprite == other.selectedSprite && disabledSprite == other.disabledSprite;
    }

    /// The Animator triggers a Selectable sets in each state.
    public partial class AnimationTriggers
    {
        public AnimationTriggers()
        {
            m_NormalTrigger = "Normal";
            m_HighlightedTrigger = "Highlighted";
            m_PressedTrigger = "Pressed";
            m_SelectedTrigger = "Selected";
            m_DisabledTrigger = "Disabled";
        }

        public string normalTrigger { get => m_NormalTrigger; set => m_NormalTrigger = value; }
        public string highlightedTrigger { get => m_HighlightedTrigger; set => m_HighlightedTrigger = value; }
        public string pressedTrigger { get => m_PressedTrigger; set => m_PressedTrigger = value; }
        public string selectedTrigger { get => m_SelectedTrigger; set => m_SelectedTrigger = value; }
        public string disabledTrigger { get => m_DisabledTrigger; set => m_DisabledTrigger = value; }
    }
}
