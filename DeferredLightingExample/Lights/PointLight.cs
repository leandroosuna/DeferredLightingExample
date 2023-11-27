using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeferredLightingExample.Effects;

namespace DeferredLightingExample.Managers
{
    public class PointLight : LightVolume
    {
        float radius;
        float scale;
        public Matrix world;
        public PointLight(Vector3 position, float radius, Vector3 color, Vector3 specularColor ) : base(position, color, Vector3.Zero, specularColor)
        {
            this.radius = radius;
            collider = new BoundingSphere(position, radius);
            scale = 0.01f * radius;
            
            world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);

        }
        
        public override void Draw()
        {

            deferredEffect.SetLightDiffuseColor(color);
            deferredEffect.SetLightSpecularColor(specularColor);
            deferredEffect.SetLightPosition(position);
            deferredEffect.SetRadius(radius);
            

            foreach (var mesh in lightSphere.Meshes)
            {
                deferredEffect.SetWorld(mesh.ParentBone.Transform * world);

                //effect.Parameters["world"].SetValue(mesh.ParentBone.Transform * world);

                mesh.Draw();
            }
        }
        

        public override void Update()
        {
            collider.Center = position;

            world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            
        }
    }
}
