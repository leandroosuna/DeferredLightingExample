using Microsoft.Xna.Framework;

namespace DeferredLightingExample.Managers
{
    public class AmbientLight
    {
        public Vector3 position;
        public Vector3 color;
        public Vector3 ambientColor;
        public Vector3 specularColor;

        public AmbientLight(Vector3 position, Vector3 color, Vector3 ambientColor, Vector3 specularColor)
        {
            this.position = position;
            this.color = color;
            this.ambientColor = ambientColor;
            this.specularColor = specularColor;
        }
    }
}