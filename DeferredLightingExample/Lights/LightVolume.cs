using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeferredLightingExample.Effects;

namespace DeferredLightingExample.Managers
{
    public abstract class LightVolume
    {
        public static Model sphere;
        public static Model lightSphere;
        public static Model lightCone;
        public static Model cube;
        public static DeferredEffect deferredEffect;
        public static BasicModelEffect basicModelEffect;

        //public static Model cone;

        public Vector3 position;
        public BoundingSphere collider;
        public Vector3 color;
        public Vector3 ambientColor;
        public Vector3 specularColor;

        public bool enabled;
        public bool hasLightGeo;
        static DeferredGame game;
        public LightVolume(Vector3 position, Vector3 color, Vector3 ambientColor, Vector3 specularColor)
        {
            this.position = position;
            this.color = color;
            this.ambientColor = ambientColor;
            this.specularColor = specularColor;
            enabled = true;
            hasLightGeo = false;
        }
        public static void Init(Model sphereModel, Model lightSphereModel, Model lightConeModel, Model cubeModel)
        {
            sphere = sphereModel;
            cube = cubeModel;
            lightSphere = lightSphereModel;
            lightCone = lightConeModel;
            game = DeferredGame.GetInstance();
            deferredEffect = game.deferredEffect;
            basicModelEffect = game.basicModelEffect;
        }
        
        public abstract void Update();
        public abstract void Draw();

        public void DrawLightGeo()
        {
            if(hasLightGeo)
            {
                basicModelEffect.SetTech("color_lightDis");
                basicModelEffect.SetColor(color);
            
                foreach (var mesh in sphere.Meshes)
                {
                    var w = mesh.ParentBone.Transform * Matrix.CreateScale(0.01f) * Matrix.CreateTranslation(position);
                    basicModelEffect.SetWorld(w);
                    basicModelEffect.SetInverseTransposeWorld(Matrix.Invert(Matrix.Transpose(w)));
                    mesh.Draw();
                }
            }
        }
        

    }
}
