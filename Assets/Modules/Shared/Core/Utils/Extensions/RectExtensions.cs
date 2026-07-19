using UnityEngine;

namespace Vesolovsky.Core.Utils.Extensions
{
    public static class RectExtensions
    {
        #region Position Adjustments

        /// <summary>
        /// Moves the rectangle along the X-axis.
        /// </summary>
        public static Rect MoveX(this Rect rect, float offset)
        {
            rect.x += offset;
            return rect;
        }

        /// <summary>
        /// Moves the rectangle along the Y-axis.
        /// </summary>
        public static Rect MoveY(this Rect rect, float offset)
        {
            rect.y += offset;
            return rect;
        }

        #endregion

        #region Size Adjustments

        /// <summary>
        /// Sets a new width for the rectangle.
        /// </summary>
        public static Rect SetWidth(this Rect rect, float width)
        {
            rect.width = width;
            return rect;
        }

        /// <summary>
        /// Adds width to the rectangle from the right side.
        /// </summary>
        public static Rect AddWidthFromRight(this Rect rect, float amount)
        {
            return rect.SetWidthFromRight(rect.width + amount);
        }

        /// <summary>
        /// Adds width to the rectangle (both sides).
        /// </summary>
        public static Rect AddWidth(this Rect rect, float amount)
        {
            rect.width += amount;
            return rect;
        }

        /// <summary>
        /// Sets the rectangle width relative to its right edge.
        /// </summary>
        public static Rect SetWidthFromRight(this Rect rect, float width)
        {
            rect.x += rect.width;
            rect.width = width;
            rect.x -= rect.width;
            return rect;
        }

        /// <summary>
        /// Sets a new height for the rectangle.
        /// </summary>
        public static Rect SetHeight(this Rect rect, float height)
        {
            rect.height = height;
            return rect;
        }

        /// <summary>
        /// Adds height to the rectangle from the bottom side.
        /// </summary>
        public static Rect AddHeightFromBottom(this Rect rect, float amount)
        {
            return rect.SetHeightFromBottom(rect.height + amount);
        }

        /// <summary>
        /// Sets the rectangle height relative to its bottom edge.
        /// </summary>
        public static Rect SetHeightFromBottom(this Rect rect, float height)
        {
            rect.y += rect.height;
            rect.height = height;
            rect.y -= rect.height;
            return rect;
        }

        /// <summary>
        /// Adjusts the rectangle height by a specified amount relative to its center.
        /// </summary>
        public static Rect SetHeightFromMid(this Rect rect, float height)
        {
            rect.y += rect.height / 2;
            rect.height = height;
            rect.y -= rect.height / 2;
            return rect;
        }

        /// <summary>
        /// Adjusts the rectangle height by a specified amount relative to its center.
        /// </summary>
        public static Rect AddHeightFromMid(this Rect rect, float amount)
        {
            return rect.SetHeightFromMid(rect.height + amount);
        }

        #endregion

        #region Resizing

        /// <summary>
        /// Resizes the rectangle by reducing its width and height from all sides.
        /// </summary>
        public static Rect Resize(this Rect rect, float amount)
        {
            rect.x += amount;
            rect.y += amount;
            rect.width -= amount * 2;
            rect.height -= amount * 2;
            return rect;
        }

        #endregion
    }
}
