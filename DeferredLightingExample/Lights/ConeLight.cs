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
    public class ConeLight: LightVolume
    {
        float radius;
        float scale;
        public Matrix world;
        float yaw, pitch;
        float width;
        Vector3 front;
        public ConeLight(Vector3 position, float radius, float yaw, float pitch, float width, Vector3 color, Vector3 specularColor ) : base(position, color, Vector3.Zero, specularColor)
        {
            this.radius = radius;
            collider = new BoundingSphere(position, radius);
            scale = 0.01f * radius;
            this.yaw = yaw;
            this.pitch = pitch;
            this.width = width;
            var tempFront = Vector3.Zero;
            tempFront.X = MathF.Cos(yaw * MathF.Cos(pitch));
            tempFront.Y = MathF.Sin(pitch);
            tempFront.Z = MathF.Sin(yaw * MathF.Cos(pitch));
            front = Vector3.Normalize(tempFront);


            world = Matrix.CreateScale(scale) *  Matrix.CreateFromYawPitchRoll(yaw, pitch, 0) * Matrix.CreateTranslation(position);

        }
        
        public override void Draw()
        {
            deferredEffect.SetTech("point_light");
            deferredEffect.SetLightDiffuseColor(color);
            deferredEffect.SetLightSpecularColor(specularColor);
            deferredEffect.SetLightPosition(position);
            deferredEffect.SetRadius(radius);
            

            foreach (var mesh in lightCone.Meshes)
            {
                deferredEffect.SetWorld(mesh.ParentBone.Transform * world);

                //effect.Parameters["world"].SetValue(mesh.ParentBone.Transform * world);

                mesh.Draw();
            }
        }
        

        public override void Update()
        {
            collider.Center = position;

            world = Matrix.CreateScale(scale) * Matrix.CreateFromYawPitchRoll(-yaw + MathHelper.Pi, pitch, 0) * Matrix.CreateTranslation(position + front * 10);

        }
    }
}
