using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Core.Utils.Extensions
{
    public static class ImageExtensions
    {
        /// <summary>
        /// Creates a line on UI by changing width. Will probably only look good when there's no image (empty)
        /// </summary>
        public static void CreateLine(this Image image, Vector2 startPosition, Vector2 endPosition)
        {
            image.transform.position = startPosition;

            var dist = (endPosition - startPosition);
            image.rectTransform.sizeDelta = image.rectTransform.sizeDelta.SetX(dist.x);
        }
    }
}
